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

                // ---- Additional lights (e.g. the warm desk-lamp practical) ----
            #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light l = GetAdditionalLight(i, IN.positionWS);
                    half nl = dot(N, l.direction);
                    half term = Band(nl * l.shadowAttenuation) * l.distanceAttenuation;
                    lit += l.color * term;
                }
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

        // Stock URP shadow + depth passes, pulled in to keep this file lean.
        // NOTE: UsePass can disable SRP-Batcher for this shader. If you later need
        // batching, replace these two lines with hand-written ShadowCaster/DepthOnly passes.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
