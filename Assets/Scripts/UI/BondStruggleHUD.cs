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
//
// Day 218: BOND BAR — the bond list left the player's screen. Which bonds hold her is the
// ropes on her, which one is selected is her lean, untie is the loop. The one thing only
// the list had was how far along a bond is, so that moved onto her too: a short length of
// rope pinned under the selected bond, which wears away as the bond gives (full = tight,
// gone = free). The list (bonds, HANDS, caught n of m) now draws with the debug
// readout (F1). The catch count is the guard's job: he warns her on the catch before the
// last (GuardController.lastWarningGuardLine). Nothing player-facing draws under the end
// card or during the caught sequence.

using UnityEngine;

public class BondStruggleHUD : MonoBehaviour
{
	[SerializeField] private BondStruggle struggle;
	[Range(1, 4)] [SerializeField] private float uiScale = 2f;
	[Tooltip("Show the controls card on start. Day 218: off. TutorialPrompts teaches the " +
		"controls one at a time; the card is there on Back / Tab, and a corner tag says so.")]
	[SerializeField] private bool showControlsCard = false;
	[Tooltip("Day 218: small tag in the corner while the card is hidden, naming the key that shows it.")]
	[SerializeField] private bool showCardTag = true;
	[Tooltip("Bond list in the corner: bonds, selected bond, hands, catches. Day 218: off. " +
		"Players read her and the bond bar instead; the list always draws with the debug " +
		"readout (F1). Turn this on to show it to players again.")]
	[SerializeField] private bool showPlayerReadout = false;
	[Tooltip("Debug readout: pad status, noise, captor, last result, boost. Forced off in " +
		"release player builds. F1 toggles it in the editor and in Development builds. " +
		"Day 218: off at start; F1 brings it up.")]
	[SerializeField] private bool showDebug = false;

	[Header("Bond bar (Day 218)")]
	[Tooltip("How much of the selected bond is left, as a short length of rope pinned under that " +
		"bond on her. Wears away from the right as it loosens; gone = free. Uses the knot cue's " +
		"rope and outline colours.")]
	[SerializeField] private bool showBondBar = true;
	[Tooltip("Bar width as a share of screen height. 0.09 = ~97 px at 1080p.")]
	[Range(0.03f, 0.3f)] [SerializeField] private float bondBarWidth = 0.09f;
	[Tooltip("Bar thickness as a share of screen height. 0.014 = ~15 px at 1080p. Thinner than " +
		"about 0.011 and the rope twist stops reading.")]
	[Range(0.004f, 0.04f)] [SerializeField] private float bondBarHeight = 0.014f;
	[Tooltip("How far below the bond the bar sits, as a share of screen height. Negative = above. " +
		"While the knot loop shows, the bar moves down further by itself to stay clear of it.")]
	[Range(-0.2f, 0.2f)] [SerializeField] private float bondBarDrop = 0.045f;
	[Tooltip("Where the rope has worn away.")]
	[SerializeField] private Color bondBarTrack = new Color(0.26f, 0.24f, 0.21f);
	[Tooltip("The rope that's left flashes toward this colour when the bond gives.")]
	[SerializeField] private Color bondBarFlash = Color.white;
	[Tooltip("How strong that flash is.")]
	[Range(0f, 1f)] [SerializeField] private float bondBarFlashStrength = 0.6f;
	[Tooltip("Most rows of rope the bar will stack. Each time he ties a bond tighter it gains a " +
		"full row (one per BondStruggle.tightenPerCatch of tightness).")]
	[Range(1, 6)] [SerializeField] private int bondBarMaxRows = 4;
	[SerializeField] private float bondBarFlashTime = 0.18f;
	[Range(0f, 1f)] [SerializeField] private float bondBarAlpha = 0.92f;

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
	Texture2D _rope;       // Day 218: one twist of rope, tiles sideways (bond bar)
	float _pullT = 1f;     // 0..1 through a pull-through; 1 = idle
	int _pullDir;          // direction of the loop that was just pulled
	float _cinchT = 1f;    // 0..1 through a fumble cinch; 1 = idle
	int _cueBond = -1;     // bond the last pull / cinch happened on

	int _barBond = -1;     // bond the bar last showed
	float _barProg;        // its progress last frame
	float _barFlashT = 1f; // 0..1 through a flash; 1 = idle
	float _barPush;        // extra drop, pixels, to clear the knot loop

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
		_rope = BuildRope();
		if (struggle != null) struggle.OnUntieInput += OnUntie;
	}

	void OnDestroy()
	{
		if (struggle != null) struggle.OnUntieInput -= OnUntie;
		if (_bight != null) Destroy(_bight);
		if (_rope != null) Destroy(_rope);
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

		// Bond bar: flash when the selected bond's progress goes up (a good pull, a good untie).
		if (struggle != null)
		{
			int sel = struggle.Selected;
			float prog = sel >= 0 && sel < struggle.bonds.Length ? struggle.bonds[sel].progress : 0f;
			if (sel == _barBond && prog > _barProg + 0.0001f) _barFlashT = 0f;
			_barBond = sel;
			_barProg = prog;
		}
		if (_barFlashT < 1f) _barFlashT = Mathf.Min(1f, _barFlashT + Time.deltaTime / Mathf.Max(0.01f, bondBarFlashTime));
	}

	/// <summary>Day 218: the end card or the caught sequence has the screen. OnGUI draws over
	/// every canvas, the black fade and the end card included, so the player-facing marks
	/// (bond bar, knot cue, controls tag) stand down.</summary>
	static bool ScreenTaken()
	{
		if (EndCard.Instance != null && EndCard.Instance.IsShowing) return true;
		GuardController guard = GuardController.Instance;
		return guard != null && guard.CurrentState == GuardController.GuardState.Caught;
	}

	void OnGUI()
	{
		if (struggle == null) return;

		// On-her marks first, in raw screen pixels, under the readouts: bond bar, then knot cue.
		GUI.matrix = Matrix4x4.identity;
		bool screenTaken = ScreenTaken();
		if (Event.current.type == EventType.Repaint && !screenTaken)
		{
			if (showBondBar) DrawBondBar();
			if (showKnotCue) DrawKnotCue();
		}

		GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
		float sw = Screen.width / uiScale;

		float y = 50f;
		if (showPlayerReadout || showDebug) y = DrawPlayer(y);   // Day 218: the list is debug now
		if (showDebug) DrawDebug(y);
		if (showControlsCard) DrawControlsCard(sw);
		else if (showCardTag && !screenTaken) DrawCardTag(sw);
	}

	// The bond list. Day 218: draws with the debug readout (or showPlayerReadout). Returns the
	// y below the last line.
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
			// Day 218: no [untie] tag. The loop on her rope says it (knot cue), and twice was noise.
			GUI.Label(new Rect(20, y, 600, 22), $"{mark}{b.name,-8} {bar}{state}{tight}");
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

	// ---- Bond bar (Day 218) ----------------------------------------------------------

	/// <summary>
	/// What's left of the selected bond, pinned under it on her body: a length of rope on a
	/// dark track, wearing away from the right as the bond gives. Full = tight, gone = free.
	/// Only the selected bond gets one, so it doubles as "this one".
	///
	/// Rows: a bond he has tied tighter is more rope. One full row to start, one more full
	/// row each time it's tightened, stacked like coils. The whole stack is the bond: the
	/// top row wears away first, the bottom row last. The stack hangs down from where the
	/// single bar sits, so extra rows never climb over the bond itself.
	/// </summary>
	void DrawBondBar()
	{
		int sel = struggle.Selected;
		if (sel < 0 || sel >= struggle.bonds.Length) return;
		var b = struggle.bonds[sel];
		if (!b.Bound) return;
		if (!struggle.TryAnchorWorld(sel, out Vector3 world)) return;
		Camera cam = Camera.main;
		if (cam == null) return;
		Vector3 sp = cam.WorldToScreenPoint(world);
		if (sp.z <= 0f) return;   // behind the camera

		float H = Screen.height;
		float w = H * bondBarWidth;
		float h = Mathf.Max(3f, H * bondBarHeight);
		float top = (H - sp.y) + H * bondBarDrop;   // GUI y is down

		// While the knot loop shows on this bond, sit below its reach whichever way it
		// points, so the bar doesn't hop around as the loop turns. Glide there.
		float want = 0f;
		if (showKnotCue && struggle.CanUntie(sel) && TryCueWorld(sel, out Vector3 kw))
		{
			Vector3 ks = cam.WorldToScreenPoint(kw);
			if (ks.z > 0f) want = Mathf.Max(0f, (H - ks.y) + H * knotCueSize * 0.85f - top);
		}
		_barPush = Mathf.MoveTowards(_barPush, want, Time.deltaTime * H * 0.4f);
		top += _barPush;

		float o = Mathf.Max(1.5f, h * 0.22f);   // outline width
		float x = sp.x - w * 0.5f;
		float flash = 1f - Smooth(_barFlashT);

		// One row, plus one per tightening.
		int rows = 1;
		if (struggle.tightenPerCatch > 0.001f)
			rows += Mathf.RoundToInt(Mathf.Max(0f, b.tightness - 1f) / struggle.tightenPerCatch);
		rows = Mathf.Clamp(rows, 1, bondBarMaxRows);
		float pitch = h + o;                                      // rows share the outline between them
		float ropeLeft = (1f - Mathf.Clamp01(b.progress)) * rows; // rope still holding, in rows

		var prev = GUI.color;
		FillRect(new Rect(x - o, top - o, w + o * 2f, rows * pitch + o), knotOutline, bondBarAlpha);
		for (int i = 0; i < rows; i++)   // i = 0 is the top row: last on, first to go
		{
			float rowTop = top + i * pitch;
			FillRect(new Rect(x, rowTop, w, h), bondBarTrack, bondBarAlpha);
			float left = w * Mathf.Clamp01(ropeLeft - (rows - 1 - i));
			if (left < 1f) continue;
			var r = new Rect(x, rowTop, left, h);
			if (_rope != null)
			{
				// The twist keeps its size on screen: the texture repeats along the rope
				// that's left, it doesn't stretch.
				float tile = h * RopeTexW / RopeTexH;
				GUI.color = new Color(1f, 1f, 1f, bondBarAlpha);
				GUI.DrawTextureWithTexCoords(r, _rope, new Rect(0f, 0f, left / tile, 1f), true);
			}
			else FillRect(r, knotRope, bondBarAlpha);
			if (flash > 0.01f) FillRect(r, bondBarFlash, flash * bondBarFlashStrength);
		}
		GUI.color = prev;
	}

	const int RopeTexW = 24, RopeTexH = 16;

	/// <summary>
	/// One repeat of twisted rope: two slanted strands, lit along their middles, dark in the
	/// grooves between them, rounded top to bottom. Tiles sideways. Same cream as the knot cue.
	/// </summary>
	Texture2D BuildRope()
	{
		const int W = RopeTexW, H = RopeTexH;
		const float strand = W / 2f;   // two strands per repeat
		var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Bilinear,
			wrapModeU = TextureWrapMode.Repeat,
			wrapModeV = TextureWrapMode.Clamp,
			hideFlags = HideFlags.DontSave,
			name = "BondRope",
		};
		Color groove = knotRope * 0.42f;
		var px = new Color[W * H];
		for (int y = 0; y < H; y++)
		for (int x = 0; x < W; x++)
		{
			float v = (y + 0.5f) / H;
			// Each strand leans one strand-width over the rope's thickness.
			float t = (x + 0.5f + v * strand) / strand;
			float sPos = t - Mathf.Floor(t);                      // 0..1 across one strand
			float edge = Mathf.Min(sPos, 1f - sPos);              // 0 at a groove, 0.5 mid-strand
			float lit = 0.74f + 0.26f * Mathf.Sin(Mathf.PI * sPos);
			float round = 0.80f + 0.20f * Mathf.Sin(Mathf.PI * v);
			Color c = knotRope * (lit * round);
			c = Color.Lerp(groove, c, Edge(0.03f, 0.16f, edge));
			c.a = 1f;
			px[y * W + x] = c;
		}
		tex.SetPixels(px);
		tex.Apply(false, true);
		return tex;
	}

	static void FillRect(Rect r, Color c, float alpha)
	{
		GUI.color = new Color(c.r, c.g, c.b, c.a * alpha);
		GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
	}

	/// <summary>Where the knot cue sits for bond i: her hands' knot point, else the bond's anchor.</summary>
	bool TryCueWorld(int i, out Vector3 world)
	{
		if (handsDriver != null && handsDriver.TryKnotWorld(i, out world)) return true;
		return struggle.TryAnchorWorld(i, out world);
	}

	// ---- Knot cue (Day 212) ----------------------------------------------------------

	void DrawKnotCue()
	{
		if (_bight == null) return;
		int sel = struggle.Selected;
		if (!struggle.CanUntie(sel)) return;

		if (!TryCueWorld(sel, out Vector3 world)) return;
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

	// Day 218: the card's way back. Bottom-right and dim: top-right is where "Holding:" sits.
	// Key follows the last-touched device.
	void DrawCardTag(float sw)
	{
		float sh = Screen.height / uiScale;
		string key = RaraInput.Read().usingPad ? RaraInput.Label(RaraInput.Button.Select) : "Tab";
		var prev = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, 0.55f);
		var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, wordWrap = false };
		GUI.Label(new Rect(sw - 220f, sh - 36f, 200f, 22f), $"{key}: controls", st);
		GUI.color = prev;
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
