// BondStruggleHUD.cs — Day 202. TEMPORARY SCAFFOLDING, same status as BondMeterUI.
// The StruggleProto greybox readout, pointed at the real BondStruggle + GuardController.
// Replace with diegetic feedback (bonds on the model, the guard's head turn) — see the
// Day 201 note: "connect the rhythm to a visual presentation of the bonds."
//
// Drop on any always-active scene object. Finds Cassie's BondStruggle and the guard.
// Tab / pad Back toggles the controls card. F1 toggles the debug readout (editor and
// Development builds only; release builds never show it).
//
// Day 207 split. Two readouts:
//   PLAYER (every build)  bonds + selected marker + progress, HANDS out/hidden,
//                         catches. What the player needs to act. Interim until bond
//                         choice moves onto Cassie's body. (KNOT moved onto her Day 212.)
//   DEBUG (F1, dev only)  pad status, noise bar, captor state + attention, last result,
//                         boost. Never in a release build: noise and attention are what
//                         the player is meant to read from HIM, not from a bar.
// Neither readout carries the beat (GDD Feedback channels: the beat lives in her body).
//
// Day 212: KNOT CUE — the knot moved off the readout and onto her. A rope-loop glyph (a
// bight) pinned to the selected knot's screen position (CassieHandsDriver.TryKnotWorld,
// the same point her hands reach for). The loop stands out from the knot toward knot[0]:
// that's the direction to press. Hit = the loop pulls through that way and the next one
// shows. Fumble = it cinches (shrinks, flushes red). Dimmed while her hands are hidden
// ("you can untie this"), full strength once they're out. Fixed screen size, so it reads
// at over-the-shoulder distance and hands or knees never hide it. Interim for the demo:
// post-demo it becomes a 3D bight on the real ankle rope (GDD Diegetic feedback, Knot).

using UnityEngine;

public class BondStruggleHUD : MonoBehaviour
{
	[SerializeField] private BondStruggle struggle;
	[Range(1, 4)] [SerializeField] private float uiScale = 2f;
	[Tooltip("Show the controls card on start (cold playtest).")]
	[SerializeField] private bool showControlsCard = true;
	[Tooltip("Player readout: bonds, selected bond, hands, catches. Shown in every build. " +
		"Day 207: interim until bond choice is on Cassie's body. Day 212: the knot already is (knot cue).")]
	[SerializeField] private bool showPlayerReadout = true;
	[Tooltip("Debug readout: pad status, noise, captor, last result, boost. Forced off in " +
		"release player builds. F1 toggles it in the editor and in Development builds; turn it " +
		"off for timing runs.")]
	[SerializeField] private bool showDebug = true;

	[Header("Knot cue (Day 212)")]
	[Tooltip("The bight on the knot. Off = no untie cue at all (the KNOT line is debug only now).")]
	[SerializeField] private bool showKnotCue = true;
	[Tooltip("Empty = found in scene. Without it the cue sits on the bond anchor, no offset.")]
	[SerializeField] private CassieHandsDriver handsDriver;
	[Tooltip("Glyph size as a share of screen height. 0.07 = ~75 px at 1080p.")]
	[Range(0.02f, 0.2f)] [SerializeField] private float knotCueSize = 0.07f;
	[Tooltip("Push the drawn knot off the real one, along the loop's direction, as a share of " +
		"the glyph size. 0 = the drawn knot IS the knot. Raise it if the loop hides her fingers.")]
	[Range(0f, 1f)] [SerializeField] private float knotCueStandOff = 0f;
	[Tooltip("Rope colour. Cream against the dark floor and her brown trousers.")]
	[SerializeField] private Color knotRope = new Color(0.94f, 0.88f, 0.74f);
	[Tooltip("Outline. Bold and dark, like the cel shader's.")]
	[SerializeField] private Color knotOutline = new Color(0.07f, 0.06f, 0.05f);
	[Tooltip("Alpha while her hands are hidden (or going back). Untie is available, not active.")]
	[Range(0f, 1f)] [SerializeField] private float knotHiddenAlpha = 0.45f;
	[Tooltip("Hit: seconds for the loop to pull through and the next to show.")]
	[SerializeField] private float knotPullTime = 0.15f;
	[Tooltip("Hit: how far the old loop slides, as a share of the glyph size.")]
	[SerializeField] private float knotPullDistance = 0.6f;
	[Tooltip("Fumble: seconds for the cinch (shrink and back).")]
	[SerializeField] private float knotCinchTime = 0.22f;
	[Tooltip("Fumble: smallest scale during the cinch.")]
	[Range(0.3f, 1f)] [SerializeField] private float knotCinchScale = 0.7f;
	[SerializeField] private Color knotFumbleTint = new Color(1f, 0.35f, 0.3f);

	static bool DebugAllowed => Application.isEditor || Debug.isDebugBuild;

	Texture2D _bight;
	float _pullT = 1f;     // 0..1 through a pull-through; 1 = idle
	int _pullDir;          // direction of the loop that was just pulled
	float _cinchT = 1f;    // 0..1 through a fumble cinch; 1 = idle
	int _cueBond = -1;     // bond the last pull / cinch happened on

	// Texture geometry, pixels in a BightTex x BightTex texture. y up (row 0 = bottom).
	const int BightTex = 64;
	const float NubY = 9f;      // knot centre, from the bottom

	void Start()
	{
		if (struggle == null) struggle = FindFirstObjectByType<BondStruggle>();
		if (struggle == null) Debug.LogWarning("[BondStruggleHUD] No BondStruggle in scene.");
		if (handsDriver == null) handsDriver = FindFirstObjectByType<CassieHandsDriver>();
		if (!DebugAllowed) showDebug = false;
		_bight = BuildBight();
		if (struggle != null) struggle.OnUntieInput += OnUntie;
	}

	void OnDestroy()
	{
		if (struggle != null) struggle.OnUntieInput -= OnUntie;
		if (_bight != null) Destroy(_bight);
	}

	void OnUntie(int dir, bool correct)
	{
		_cueBond = struggle.Selected;
		if (correct) { _pullDir = dir; _pullT = 0f; _cinchT = 1f; }
		else _cinchT = 0f;
	}

	void Update()
	{
		bool back = false;
#if ENABLE_INPUT_SYSTEM
		var gp = UnityEngine.InputSystem.Gamepad.current;
		back = gp != null && gp.selectButton.wasPressedThisFrame;
#else
		back = Input.GetKeyDown(KeyCode.JoystickButton6);
#endif
		if (Input.GetKeyDown(KeyCode.Tab) || back) showControlsCard = !showControlsCard;
		if (DebugAllowed && Input.GetKeyDown(KeyCode.F1)) showDebug = !showDebug;

		if (_pullT < 1f) _pullT = Mathf.Min(1f, _pullT + Time.deltaTime / Mathf.Max(0.01f, knotPullTime));
		if (_cinchT < 1f) _cinchT = Mathf.Min(1f, _cinchT + Time.deltaTime / Mathf.Max(0.01f, knotCinchTime));
	}

	void OnGUI()
	{
		if (struggle == null) return;

		// Knot cue first, in raw screen pixels, under the readouts.
		GUI.matrix = Matrix4x4.identity;
		if (showKnotCue && Event.current.type == EventType.Repaint) DrawKnotCue();

		GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
		float sw = Screen.width / uiScale;

		float y = 50f;
		if (showPlayerReadout) y = DrawPlayer(y);
		if (showDebug) DrawDebug(y);
		if (showControlsCard) DrawControlsCard(sw);
	}

	// Player-facing: stays in every build. Returns the y below the last line.
	float DrawPlayer(float y)
	{
		var prev = GUI.color;

		// --- bonds ---
		for (int i = 0; i < struggle.bonds.Length; i++)
		{
			var b = struggle.bonds[i];
			if (!b.active) continue;   // reserve bonds (elbows before the first catch)
			string mark = i == struggle.Selected ? "> " : "  ";
			string bar = new string('|', Mathf.RoundToInt(b.progress * 30));
			string state = b.progress >= 1f ? "  FREE" : "";
			string tight = b.tightness > 1f ? $"  {1f / b.tightness:P0} per pull" : "";
			string untie = struggle.CanUntie(i) ? "  [untie]" : "";
			GUI.Label(new Rect(20, y, 600, 22), $"{mark}{b.name,-8} {bar}{state}{tight}{untie}");
			y += 22;
		}

		// --- hands + knot ---
		string hands;
		if (!struggle.HandsFree) hands = "bound";
		else if (struggle.IsHidingHands) hands = "FREE — hiding...";
		else if (struggle.HandsOut) hands = "FREE — OUT";
		else hands = "FREE — hidden";
		if (struggle.HandsOut) GUI.color = Color.red;
		GUI.Label(new Rect(20, y + 6, 500, 22), $"HANDS  {hands}{(struggle.MoveLayerHeld ? "    [MOVING CHAIR]" : "")}");
		GUI.color = prev;

		// Day 212: KNOT moved onto her (DrawKnotCue). The sequence line is debug only.

		// The third catch is a loss — the player gets to see it coming.
		GUI.Label(new Rect(20, y + 28, 500, 22), $"caught {struggle.Catches} of {struggle.maxCatches}");
		return y + 52f;
	}

	// ---- Knot cue (Day 212) ----------------------------------------------------------

	void DrawKnotCue()
	{
		if (_bight == null) return;
		int sel = struggle.Selected;
		if (!struggle.CanUntie(sel)) return;

		Vector3 world = Vector3.zero;
		bool found = handsDriver != null && handsDriver.TryKnotWorld(sel, out world);
		if (!found) found = struggle.TryAnchorWorld(sel, out world);
		if (!found) return;
		Camera cam = Camera.main;
		if (cam == null) return;
		Vector3 sp = cam.WorldToScreenPoint(world);
		if (sp.z <= 0f) return;   // behind the camera
		Vector2 knot = new Vector2(sp.x, Screen.height - sp.y);   // GUI y is down

		float size = Screen.height * knotCueSize;
		bool hidden = !struggle.HandsOut || struggle.IsHidingHands;
		float alpha = hidden ? knotHiddenAlpha : 1f;
		bool sameBond = _cueBond == sel;

		// Fumble: cinch — shrink and back, flushed red.
		float scale = 1f;
		Color tint = Color.white;
		if (sameBond && _cinchT < 1f)
		{
			float s = Mathf.Sin(Mathf.PI * _cinchT);
			scale = Mathf.Lerp(1f, knotCinchScale, s);
			tint = Color.Lerp(Color.white, knotFumbleTint, s);
		}

		var prevColor = GUI.color;
		if (sameBond && _pullT < 1f)
		{
			// Hit: the pulled loop slides out its own way and fades; the next one grows in.
			float e = Smooth(_pullT);
			Vector2 slide = DirGui(_pullDir) * (size * knotPullDistance * e);
			DrawBight(knot + slide, _pullDir, size, alpha * (1f - e), Color.white);
			DrawBight(knot, struggle.bonds[sel].knot[0], size * Mathf.Lerp(0.6f, 1f, e), alpha * e, Color.white);
		}
		else
		{
			DrawBight(knot, struggle.bonds[sel].knot[0], size * scale, alpha, tint);
		}
		GUI.color = prevColor;
	}

	/// <summary>One bight, its knot nub at `knot` (GUI pixels), loop pointing `dir`.</summary>
	void DrawBight(Vector2 knot, int dir, float size, float alpha, Color tint)
	{
		if (alpha <= 0.01f || size < 1f) return;
		// The drawn knot sits on the real one (knotCueStandOff 0) or a little out along the loop.
		Vector2 pivot = knot + DirGui(dir) * (size * knotCueStandOff);
		// Rect drawn pointing up, nub on the pivot: the nub is NubY/BightTex up from the bottom.
		float nubFromTop = size * (1f - NubY / BightTex);
		Rect r = new Rect(pivot.x - size * 0.5f, pivot.y - nubFromTop, size, size);

		Matrix4x4 m = GUI.matrix;
		GUIUtility.RotateAroundPivot(dir * 90f, pivot);   // GUI angles run clockwise: up, right, down, left
		GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
		GUI.DrawTexture(r, _bight, ScaleMode.StretchToFill, true);
		GUI.matrix = m;
	}

	/// <summary>Knot direction (0 up / 1 right / 2 down / 3 left) as a GUI-space unit vector.</summary>
	static Vector2 DirGui(int d) => d switch
	{
		0 => Vector2.down,    // GUI y is down: screen-up is -y
		1 => Vector2.right,
		2 => Vector2.up,
		3 => Vector2.left,
		_ => Vector2.zero,
	};

	static float Smooth(float x) => x * x * (3f - 2f * x);
	/// <summary>GLSL-style smoothstep: 0 below e0, 1 above e1. (Mathf.SmoothStep is a lerp, not this.)</summary>
	static float Edge(float e0, float e1, float x) => Smooth(Mathf.Clamp01((x - e0) / (e1 - e0)));

	/// <summary>
	/// The bight, pointing up: a round loop of rope whose two legs run down into the knot,
	/// drawn on top as a darker disc. Reads as "a loop coming out of a knot", so the loop
	/// end is the direction. Signed-distance strokes with a dark outline, so it stays clean
	/// at any size (checked down to 48 px). GUI.color tints it; the outline stays dark under
	/// the multiply. Colours are baked at Start.
	/// </summary>
	Texture2D BuildBight()
	{
		const int N = BightTex;
		const float cx = N * 0.5f;
		const float loopY = 38f, loopR = 15f;   // loop centre and radius
		const float knotR = 6.5f;               // the knot disc
		const float legIn = 4f, legY = 12f;     // where the legs meet the knot
		const float hw = 3.2f, outline = 2.2f;  // rope stroke half-width, outline width

		var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.DontSave,
			name = "KnotBight",
		};
		var px = new Color[N * N];
		Vector2 c = new Vector2(cx, loopY);
		Vector2 nub = new Vector2(cx, NubY);
		Vector2 lA = new Vector2(cx - legIn, legY), lB = new Vector2(cx - loopR, loopY);
		Vector2 rA = new Vector2(cx + legIn, legY), rB = new Vector2(cx + loopR, loopY);
		Color knotFill = knotRope * 0.72f;      // the knot: same rope, in its own shadow
		knotFill.a = 1f;

		for (int y = 0; y < N; y++)
		for (int x = 0; x < N; x++)
		{
			Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

			// Loop layer: two legs + the upper half of the circle.
			float d = Mathf.Min(SegDist(p, lA, lB), SegDist(p, rA, rB));
			if (p.y >= loopY) d = Mathf.Min(d, Mathf.Abs((p - c).magnitude - loopR));
			float fill = 1f - Edge(hw - 0.75f, hw + 0.75f, d);
			float rim = 1f - Edge(hw + outline - 0.75f, hw + outline + 0.75f, d);
			Color col = Color.Lerp(knotOutline, knotRope, rim > 0.001f ? fill / rim : 0f);
			float a = rim;

			// Knot layer, over the legs: outline, then fill.
			float dk = (p - nub).magnitude;
			float kFill = 1f - Edge(knotR - 0.75f, knotR + 0.75f, dk);
			float kRim = 1f - Edge(knotR + outline - 0.75f, knotR + outline + 0.75f, dk);
			col = Color.Lerp(col, knotOutline, kRim);
			col = Color.Lerp(col, knotFill, kFill);
			a = Mathf.Max(a, kRim);

			col.a = a;
			px[y * N + x] = col;
		}
		tex.SetPixels(px);
		tex.Apply(false, true);
		return tex;
	}

	static float SegDist(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
		return (p - (a + ab * t)).magnitude;
	}

	// Debug only (F1, editor / Development builds). Draws below the player readout.
	void DrawDebug(float y)
	{
		GuardController guard = GuardController.Instance;
		var prev = GUI.color;

		// Day 202 rev: no pulse bar. The beat lives in Cassie (CassieStruggleDriver
		// sways on it) — read her, not the HUD.
		string padStatus = RaraInput.Status();
		if (padStatus.Length > 0)
		{
			GUI.color = Color.yellow;
			GUI.Label(new Rect(20, 22, 520, 22), padStatus);
			GUI.color = prev;
		}

		// --- noise bar --- noise only calls him (never catches her). Yellow ticks:
		// call-out / comes in.
		float barX = 20f, barY = y + 8, barW = 300f;
		float noise = struggle.Noise;
		bool looking = guard != null && guard.Attention > 0f;
		GUI.Box(new Rect(barX, barY, barW, 18), "");
		if (noise > 0.01f) GUI.Box(new Rect(barX, barY, barW * noise, 18), "");
		GUI.Label(new Rect(barX + barW + 8, barY, 80, 20), "NOISE");
		if (guard != null)
		{
			GUI.color = new Color(1f, 0.85f, 0f, 0.8f);
			GUI.Box(new Rect(barX + barW * guard.GlanceNoise - 1, barY - 2, 2, 22), "");
			GUI.Box(new Rect(barX + barW * guard.InspectNoise - 1, barY - 2, 2, 22), "");
			GUI.color = prev;
		}

		// --- captor ---
		if (guard != null)
		{
			string captor = guard.CurrentState switch
			{
				GuardController.GuardState.Offstage => guard.IsAlerted ? "LISTENING" : "offstage",
				GuardController.GuardState.Approaching => looking ? "LOOKING IN..." : "footsteps...",
				GuardController.GuardState.AtDoor => "WATCHING",
				GuardController.GuardState.LeanIn => "IN YOUR FACE",
				GuardController.GuardState.Leaving => looking ? "turning away" : "walking off",
				GuardController.GuardState.Caught => "CAUGHT",
				GuardController.GuardState.Downed => "down",
				GuardController.GuardState.Checking => "CHECKING YOUR BONDS",
				_ => "",
			};
			if (looking && struggle.HandsOut) GUI.color = Color.red;   // the only danger
			else if (looking || guard.IsAlerted) GUI.color = Color.yellow;
			GUI.Label(new Rect(20, barY + 30, 600, 22),
				$"CAPTOR  {captor}  (attention {guard.Attention:P0}, suspicion {guard.Suspicion:P0})   {guard.ListenStatus}");
			GUI.color = prev;
		}

		GUI.Label(new Rect(20, barY + 54, 600, 22), struggle.LastResult);

		// Day 212: the full knot sequence, debug only. The player reads knot[0] off the bight.
		if (struggle.CanUntie(struggle.Selected))
		{
			var k = struggle.bonds[struggle.Selected].knot;
			string seq = "";
			for (int j = 0; j < k.Count; j++)
				seq += j == 0 ? $"[{BondStruggle.ArrowFor(k[j])}]" : $"  {BondStruggle.ArrowFor(k[j])}";
			GUI.Label(new Rect(20, barY + 76, 500, 22), $"KNOT   {seq}");
		}
		if (struggle.LastBoost.Length > 0 && !struggle.HandsFree)
		{
			GUI.color = new Color(0.6f, 1f, 0.6f);
			GUI.Label(new Rect(340, barY + 54, 300, 22), $"BOOST  {struggle.LastBoost}");
			GUI.color = prev;
		}
	}

	// Player-facing, not debug: stays in every build.
	void DrawControlsCard(float sw)
	{
		const float cw = 340f, ch = 268f;
		string card =
			"Get every bond free — or get the bottle.\n" +
			"Don't get caught.\n\n" +
			"Choose bond      L-stick  /  WASD\n" +
			"Struggle         RT  /  Space   (with her rhythm)\n" +
			"Back up to the table edge: wrists go faster\n" +
			"Untie            D-pad  /  IJKL   (follow the loop)\n" +
			"Hide hands       LB  /  G\n" +
			"Move chair       hold LT  /  Shift\n" +
			"Pick up / set down  A  /  E\n" +
			"Strike           X  /  H\n" +
			"Restart          Start  /  R\n\n" +
			"Back / Tab: hide this card";
		GUI.Box(new Rect(sw - cw - 20, 20, cw, ch), "");
		var cs = new GUIStyle(GUI.skin.label) { wordWrap = false };
		GUI.Label(new Rect(sw - cw - 10, 28, cw - 20, ch - 16), card, cs);
	}
}
