Shader "Rara/CelShaded"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadeBands ("Shading Bands", Range(1,4)) = 2
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.6

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width (world units)", Range(0,0.1)) = 0.015
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        // ----------------------------------------------------------
        // Pass 1 — Cel-lit (main light + additional lights, banded)
        // ----------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP lighting keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES          // <-- NEW: enables cookie variant
            // Forward+ light-loop path. THIS is what was missing: without it the
            // shader runs the plain-Forward path even when the renderer is Forward+,
            // and additional-light shadowAttenuation always reads 1.0 (no shadow).
            // Unity 6.0 = _FORWARD_PLUS. Unity 6.1+ renamed it to _CLUSTER_LIGHT_LOOP.
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LightCookie/LightCookie.hlsl"  // <-- NEW

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _ShadeBands;
                half   _AmbientStrength;
                half4  _OutlineColor;
                half   _OutlineWidth;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = nrmInputs.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            // Quantize a 0..1 light value into hard bands — this is the "cel" step.
            half Band (half v)
            {
                v = saturate(v);
                return round(v * _ShadeBands) / _ShadeBands;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half3 N = normalize(IN.normalWS);
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // ---- Main light (this carries the blind-cookie key light) ----
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // NEW: apply the blind cookie to the main light. Without this, the cookie
                // never touches this surface and dark slats stay fully lit.
            #if defined(_LIGHT_COOKIES)
                mainLight.color *= SampleMainLightCookie(IN.positionWS);
            #endif

                half  ndotl = dot(N, mainLight.direction);
                half  mainTerm = Band(ndotl * mainLight.shadowAttenuation);
                half3 lit = mainLight.color * mainTerm;

// ---- Additional lights (lamp practical + window-shaft spot) ----
                // Forward+ needs the LIGHT_LOOP macros + an InputData carrying the
                // screen UV so the cluster iterator can find this pixel's lights.
                // A plain for-loop reads the wrong buffer and returns
                // shadowAttenuation = 1.0 — the exact reason the chair's spot-shadow
                // never landed here while URP/Lit received it.
            #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS)
                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.normalWS                = N;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);

                half4 shadowMask = half4(1, 1, 1, 1);   // pure realtime, no baked occlusion
                uint  pixelLightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    // 3-arg overload fills shadowAttenuation AND folds this light's
                    // cookie into .color, so the manual cookie sample is gone.
                    Light l = GetAdditionalLight(lightIndex, IN.positionWS, shadowMask);

                    half nl   = dot(N, l.direction);
                    half term = Band(nl * l.shadowAttenuation) * l.distanceAttenuation;
                    lit += l.color * term;
                LIGHT_LOOP_END
            #endif

                // ---- Ambient: cool fill comes from scene Ambient Color (set in Lighting) ----
                half3 ambient = SampleSH(N) * _AmbientStrength;

                half3 color = baseTex.rgb * (ambient + lit);
                return half4(color, baseTex.a);
            }
            ENDHLSL
        }

        // ----------------------------------------------------------
        // Pass 2 — Inverted-hull outline
        // ----------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front   // render only back faces, pushed outward = a shell behind the model

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Must match the lit pass's CBUFFER exactly to stay SRP-Batcher compatible.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _ShadeBands;
                half   _AmbientStrength;
                half4  _OutlineColor;
                half   _OutlineWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings vertOutline (Attributes IN)
            {
                Varyings OUT;
                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                posWS += normWS * _OutlineWidth;            // push back faces out along normals
                OUT.positionHCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 fragOutline (Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ----------------------------------------------------------
        // Pass 3 — ShadowCaster (hand-written)
        // Replaces UsePass for two reasons: (a) keeps the SRP Batcher intact
        // (UsePass'ing another shader's pass breaks it), and (b) guarantees the
        // _CASTING_PUNCTUAL_LIGHT_SHADOW variant is compiled FOR THIS MATERIAL,
        // so the window-shaft SPOT throws a real caster. That punctual variant
        // is the bit the UsePass path wasn't reliably bringing along — directional
        // would have worked, the spot wouldn't, which is exactly the symptom.
        // ----------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            // Directional vs punctual (spot/point) caster-bias path.
            // Without this variant the spot's bias is computed as if the
            // light were directional, which is what kept the cookie key light
            // from throwing a clean chair/box shadow.
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Lighting.hlsl, NOT Shadows.hlsl: it brings ApplyShadowBias *and*
            // pulls LerpWhiteTo in the right include order. Shadows.hlsl alone
            // trips the undefined-symbol on this URP version.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Must mirror the lit/outline CBUFFER byte-for-byte for SRP-Batcher.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _ShadeBands;
                half   _AmbientStrength;
                half4  _OutlineColor;
                half   _OutlineWidth;
            CBUFFER_END

            // Populated by URP per shadow-casting light.
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip (Attributes IN)
            {
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirWS));

                // Clamp to near plane so casters behind the light don't pop.
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            Varyings ShadowVert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            half4 ShadowFrag (Varyings IN) : SV_Target
            {
                return 0;   // opaque — no alpha clip, depth is the whole point
            }
            ENDHLSL
        }

        // ----------------------------------------------------------
        // Pass 4 — DepthOnly (hand-written; depth prepass / SSAO / DoF source)
        // ----------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _ShadeBands;
                half   _AmbientStrength;
                half4  _OutlineColor;
                half   _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half DepthFrag (Varyings IN) : SV_Target
            {
                return IN.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
