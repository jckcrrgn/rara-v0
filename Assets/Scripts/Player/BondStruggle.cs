// BondStruggle.cs — Day 202. The StruggleProto loop, moved onto Cassie.
//
// Ports the Day 197–201 prototype (Prototype/StruggleProto.cs) into the real player:
// one bond at a time, pull on the pulse, noise, untie once the wrists are free, hide
// your hands. Values are the proto's, including the Day 200 locked set. The CAPTOR half
// of the proto does NOT live here — it lives in GuardController, which reads Noise and
// HandsOut from this component and calls RegisterCatch when he catches her.
//
// OPT-IN. Add this next to PlayerController and PlayerController routes Struggle through
// it. Without it, the legacy single-Bond path (Act 1, tool-gated, barehand = 0) runs
// exactly as before. L1–L6 are untouched.
//
// OWNS
//   - Bond list (progress 0..1, tightness, knot), selection, rhythm, untie, hide.
//   - Noise (0..1), decaying. Guard listens; chair hops add to it.
//   - Hands state. Out after untying or taking an item with free hands. Stay out until
//     the player hides them (Day 201: hiding is a conscious choice to feign). Hidden =
//     PlayerController.IsFeigning, so CassieSitDriver's feign pose follows for free.
//   - BoundLimbs on the active restraint, recomputed from the bonds, so ChairRestraint's
//     kick / movement modifiers keep working.
//   - Wrists free -> PlayerController.WristsFree -> OnWristsFreed (bottle / strike path).
//   - Every bond free -> slipped out (Day 202 stub for the free state): second win.
//
// BARE HANDS MAKE PROGRESS (GDD Day 201 supersedes Bond.cs's Day 31 rule). Tools are
// multipliers: a reachable EnvironmentalTool (the TableEdge) on the wrists, or a held
// tool. A bond with barehandGain 0 (cuffs) needs one.
//
// Controls                keyboard          pad
//   select bond           WASD (flick)      left stick (flick) — Day 208: relative, screen space
//   struggle (on pulse)   Space             RT
//   untie (follow knot)   IJKL              D-pad     (hands free only; not elbows)
//   hide hands / feign    G                 LB
//   move the chair        Shift + WASD      LT + left stick
//   pick up               E                 A
//   strike                H                 X
// Pad setup: see RaraInput.cs.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class BondStruggle : MonoBehaviour
{
	[System.Serializable]
	public class StruggleBond
	{
		public string name;
		[Tooltip("Day 208: FALLBACK only. Selection is relative, in screen space, from the anchors " +
			"below. This is used as a stand-in screen position when there's no camera or rig.")]
		public Vector2 dir;
		[Tooltip("Day 208: bones whose midpoint is where this bond sits on her body, for relative " +
			"selection. Empty = derived from limb (Elbows: forearms, Wrists: hands, Ankles: feet, " +
			"Knees: shins). Set it for a one-sided bond, e.g. LeftFoot for an ankle tied to its own " +
			"chair leg.")]
		public HumanBodyBones[] anchors;
		[Tooltip("BoundLimbs flag this bond holds on the restraint. Both ankle bonds map to " +
			"Ankles; Ankles + AnkledToChair clear only when both are free.")]
		public BoundLimbs limb;
		[Tooltip("Bound at level start. Off = held in reserve until addedOnCatch.")]
		public bool startsBound = true;
		[Tooltip("Catch number that ties this bond on (1 = first catch). 0 = never.")]
		public int addedOnCatch;
		[Tooltip("Can be untied once her hands are free. Off for elbows (can't reach) and " +
			"wrists (they ARE the hands).")]
		public bool untieable = true;
		[Tooltip("Environmental tools (table edge, nail) reach this bond. Wrists only — " +
			"her hands are behind her back.")]
		public bool envToolsReach;
		[Tooltip("Barehand pull effectiveness. 1 = rope. 0 = needs a tool or key (cuffs).")]
		public float barehandGain = 1f;

		[System.NonSerialized] public bool active;
		[System.NonSerialized] public float progress;
		[System.NonSerialized] public float tightness = 1f;
		[System.NonSerialized] public List<int> knot = new List<int>();

		public bool Bound => active && progress < 1f;
	}

	public struct CatchResult
	{
		public bool taken;
		public bool seen;
		public string addedBond;   // null if nothing new was tied on
		public int catches;
	}

	[Header("Bonds")]
	public StruggleBond[] bonds =
	{
		new StruggleBond { name = "Elbows",  dir = Vector2.up,    limb = BoundLimbs.Elbows,
			startsBound = false, addedOnCatch = 1, untieable = false },
		new StruggleBond { name = "Wrists",  dir = Vector2.down,  limb = BoundLimbs.Wrists,
			untieable = false, envToolsReach = true },
		// Day 208: her ankles are tied TOGETHER — one rope, one bond. Separate L/R ankle bonds
		// come back when a level ties each ankle to its own chair leg (set their anchors).
		new StruggleBond { name = "Ankles",  dir = new Vector2(0f, -2f), limb = BoundLimbs.Ankles },
	};

	[Header("Rhythm")]
	public float beatInterval = 0.7f;
	[Range(0, 1)] public float goodWindow = 0.6f;
	public float gainPerPull = 0.08f;
	public float spamGap = 0.25f;

	[Header("Struggle on demand (Day 211)")]
	[Tooltip("She sits still until a pull starts a bout. The first press is beat 0 (no progress); " +
		"the player keeps the tempo from there. Off = Day 202 always-on beat clock.")]
	public bool boutMode = true;
	[Tooltip("Beats without a press before the bout ends and she settles.")]
	public float boutTimeoutBeats = 2f;

	[Header("Noise")]
	[Tooltip("Even a clean pull makes some sound. Steady on-beat pulling never reaches the glance line.")]
	public float noisePerPull = 0.05f;
	[Tooltip("Day 206: 0.15 -> 0.08. A miss at tempo nearly decays out before the next beat " +
		"(0.1/s x 0.7 s = 0.07), so honest misses don't call him. Rushing and mashing do. " +
		"See GDD Loop-Refine, noise thresholds.")]
	public float noisePerMiss = 0.08f;
	public float noisePerSpam = 0.25f;
	public float noiseDecay = 0.1f;
	[Tooltip("Chair hop / turn-hop. Noise only calls him (Day 202 rev). Two quick hops peak ~0.21, " +
		"three ~0.30 — right on the glance line. Not retuned Day 206; see GDD Open.")]
	public float noisePerHop = 0.12f;
	[Tooltip("One back-scoot shimmy.")]
	public float noisePerScoot = 0.06f;

	[Header("Free hands")]
	public float freeHandsStruggleMult = 1.5f;
	[Tooltip("Progress per correct knot input, tightness 1. 0.1 = ~10 inputs per bond.")]
	public float untieGain = 0.1f;
	public float noisePerUntie = 0.01f;
	public float noisePerFumble = 0.1f;
	[Tooltip("Time to get hands back behind her. Must finish before the guard's seenAttention.")]
	public float handsBackTime = 0.4f;

	[Header("Tools (multipliers on a good pull)")]
	public float bladeMult = 2.5f;
	public float pointMult = 1.6f;
	public float keyMult = 4f;

	[Header("Caught")]
	public float setback = 0.3f;
	public float tightenPerCatch = 0.3f;
	[Tooltip("Extra tightness on the wrists, on top of tightenPerCatch, when caught with free hands.")]
	public float freeHandsExtraTighten = 0.3f;
	[Tooltip("Hit this and she's taken. Loss.")]
	public int maxCatches = 3;

	[Header("Slipped out (Day 202 stub for the free state)")]
	[TextArea(1, 3)] public string slippedOutLine = "Rookie knots.";

	[Header("Telemetry (Day 203)")]
	[Tooltip("Logs signed error to the nearest beat on every pull (+ = late) and a summary " +
		"when the scene unloads or play stops. Right-click the component > Dump timing for " +
		"a mid-run summary.")]
	public bool logTiming = true;

	[Header("Pull SFX (Day 204) — audio = hit confirm")]
	[Tooltip("Plays on a good pull. Confirms the hit after the press; never cues the beat. " +
		"Leave empty for timing runs that need no audio feedback.")]
	public AudioClip hitClip;
	[Tooltip("Plays on an off-beat pull. Must read as different from hitClip at a glance.")]
	public AudioClip missClip;
	[Tooltip("Plays on a spam pull. Empty = uses missClip.")]
	public AudioClip spamClip;
	[Range(0, 1)] public float hitVolume = 0.8f;
	[Range(0, 1)] public float missVolume = 0.7f;
	[Tooltip("± random pitch, so repeated pulls don't machine-gun.")]
	[Range(0, 0.2f)] public float pitchJitter = 0.05f;

	// ---------------------------------------------------------------------------------

	public System.Action<StruggleBond> OnBondFreed;

	/// <summary>Day 211: how a pull resolved. The guard reads misses (suspicion) and spam (taunts).</summary>
	public enum PullKind { Start, Hit, Miss, Spam }
	/// <summary>Day 211: fired at the end of every pull. bool = a tool was engaged (table edge,
	/// held blade) — a missed saw is a lot more suspicious than a missed squirm.</summary>
	public System.Action<PullKind, bool> OnPullResolved;
	/// <summary>Day 208: every untie input on a reachable knot. (direction 0 up / 1 right /
	/// 2 down / 3 left in screen space, correct). CassieHandsDriver works the knot on it.</summary>
	public System.Action<int, bool> OnUntieInput;
	public System.Action OnAllFree;

	public float Noise { get; private set; }
	public int Selected { get; private set; } = -1;
	public int Catches { get; private set; }
	public string LastResult { get; private set; } = "";
	public bool SlippedOut { get; private set; }

	public bool MoveLayerHeld { get; private set; }
	public bool MoveForwardTap { get; private set; }
	public bool MoveLeftTap { get; private set; }
	public bool MoveRightTap { get; private set; }
	public bool MoveBackHeld { get; private set; }

	public int WristsIndex { get; private set; } = -1;
	public bool HandsFree => WristsIndex >= 0 && !bonds[WristsIndex].Bound;
	/// <summary>Free hands visibly out — what the guard can see. True while still hiding.</summary>
	public bool HandsOut => HandsFree && handsOut;
	public bool IsHidingHands => hidingUntil > 0f;
	/// <summary>Day 208: elbows tied behind her = her hands can't reach the knots in front.</summary>
	public bool ElbowsBound
	{
		get { foreach (var b in bonds) if ((b.limb & BoundLimbs.Elbows) != 0 && b.Bound) return true; return false; }
	}
	/// <summary>Day 212: holding an item (the bottle). Untie takes both hands, and she has no
	/// way to put it down, so picking it up commits her to the strike. Struggle still works.</summary>
	public bool HandsFull => player != null && player.GetHeldItem() != null;
	public bool CanUntie(int i) => HandsFree && !ElbowsBound && !HandsFull && i >= 0 && i < bonds.Length && bonds[i].untieable && bonds[i].Bound;
	/// <summary>0..1, 1 exactly on the beat.</summary>
	public float Pulse01 { get { float bp = BeatPhase(Time.time); return 1f - Mathf.Min(bp, 1f - bp) * 2f; } }
	/// <summary>Beats elapsed since the bout started (Day 211; Time.time when boutMode is off).
	/// Integers are the beats. CassieStruggleDriver locks to this.</summary>
	public float Beats => (Time.time - beatOrigin) / Mathf.Max(0.01f, beatInterval);
	/// <summary>Day 211: she's mid-struggle. Always true when boutMode is off.</summary>
	public bool InBout => !boutMode || Time.time < boutUntil;
	/// <summary>Any bond still holding her. The beat-locked sway runs while true.</summary>
	public bool AnyBound => NextBound(-1) >= 0;
	/// <summary>Tool that boosted the last good pull ("TableEdge ×2.5"), or "" if none.</summary>
	public string LastBoost { get; private set; } = "";

	static readonly string[] Arrow = { "^", ">", "v", "<" };
	public static string ArrowFor(int d) => d >= 0 && d < 4 ? Arrow[d] : "?";
	const int KnotPreview = 3;

	PlayerController player;
	bool handsOut;
	float hidingUntil;
	float lastPull = -99f;
	float beatOrigin;         // Day 211: beat 0 = the press that started the bout
	float boutUntil;
	int prevStickZone = -1;   // -1 neutral, 0 up, 1 right, 2 down, 3 left

	[Header("Bond selection (Day 208)")]
	[Tooltip("Cassie's Humanoid Animator, for bond anchors. Empty = found under the player, " +
		"then via CassieRig.")]
	[SerializeField] Animator anchorRig;
	[Tooltip("Half-angle of the flick cone, degrees. A bond counts as 'that way' if its " +
		"direction from the selected bond is within this of the flick.")]
	[Range(10f, 89f)] [SerializeField] float selectCone = 45f;
	bool selectHeld;   // stick has been flicked and not yet returned to neutral

	// ---------------------------------------------------------------------------------

	void Awake()
	{
		player = GetComponent<PlayerController>();
		for (int i = 0; i < bonds.Length; i++)
			if (bonds[i].limb == BoundLimbs.Wrists) { WristsIndex = i; break; }
		if (WristsIndex < 0)
			Debug.LogWarning("[BondStruggle] No bond with limb = Wrists. Hands can never be free.");

		foreach (var b in bonds)
		{
			b.active = b.startsBound;
			b.progress = 0f;
			b.tightness = 1f;
			b.knot = new List<int>();
			FillKnot(b);
		}
		Selected = NextBound(-1);
	}

	// One frame late: PlayerController.Start resolves currentRestraint, and script order
	// between the two isn't guaranteed.
	IEnumerator Start()
	{
		yield return null;
		SyncLimbs();
	}

	// Timers run even while PlayerController is disabled (caught sequence) or a mutter
	// holds the world. Input does not — that comes through Tick.
	void Update()
	{
		Noise = Mathf.Max(0f, Noise - noiseDecay * Time.deltaTime);

		if (hidingUntil > 0f && Time.time >= hidingUntil)
		{
			hidingUntil = 0f;
			handsOut = false;
			LastResult = "hands hidden";
			player.SetFeignState(true);
		}
	}

	/// <summary>
	/// Called by PlayerController.Update in place of the legacy struggle input, after the
	/// mutter gate. Reads select / pull / untie / hide and samples the move layer for the
	/// restraint. `justDismissed` suppresses Space on the frame a mutter closes.
	/// </summary>
	public void Tick(bool justDismissed)
	{
		var pad = RaraInput.Read();
		bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

		// --- direction: WASD and stick are one input ---
		Vector2 kb = Vector2.zero;
		if (Input.GetKey(KeyCode.W)) kb.y += 1f;
		if (Input.GetKey(KeyCode.S)) kb.y -= 1f;
		if (Input.GetKey(KeyCode.D)) kb.x += 1f;
		if (Input.GetKey(KeyCode.A)) kb.x -= 1f;
		Vector2 dirInput = pad.stick.sqrMagnitude > kb.sqrMagnitude ? pad.stick : kb;

		// --- move layer (LT / Shift): the stick drives the chair, selection freezes ---
		MoveLayerHeld = pad.moveLayer || shift;
		SampleMoveTaps(dirInput, shift);
		if (SlippedOut) return;

		// --- select (Day 208: relative, screen space) ---
		// A flick moves the selection from the current bond to the nearest bound bond that
		// way on screen: down from wrists is ankles, up is elbows. One flick, one step —
		// holding doesn't repeat. Hysteresis: fires past 0.5, re-arms below 0.3. Flicks made
		// while the move layer is held don't select, and still have to return to neutral.
		float mag2 = dirInput.sqrMagnitude;
		if (!selectHeld && mag2 > 0.25f)
		{
			selectHeld = true;
			if (!MoveLayerHeld) SelectToward(dirInput.normalized);
		}
		else if (selectHeld && mag2 < 0.09f) selectHeld = false;

		// --- struggle ---
		if ((Input.GetKeyDown(KeyCode.Space) && !justDismissed) || pad.pull) Pull();

		// --- untie ---
		int untieDir = pad.dpad;
		if (Input.GetKeyDown(KeyCode.I)) untieDir = 0;
		else if (Input.GetKeyDown(KeyCode.L)) untieDir = 1;
		else if (Input.GetKeyDown(KeyCode.K)) untieDir = 2;
		else if (Input.GetKeyDown(KeyCode.J)) untieDir = 3;
		if (untieDir >= 0) Untie(untieDir);

		// --- hide hands (= feign) ---
		if (Input.GetKeyDown(KeyCode.G) || pad.hide) HideHands();
	}

	// Edge-detected taps for ChairRestraint: entering a direction zone from neutral.
	void SampleMoveTaps(Vector2 v, bool shift)
	{
		MoveForwardTap = MoveLeftTap = MoveRightTap = false;
		int zone = -1;
		if (v.sqrMagnitude > 0.36f)
		{
			if (Mathf.Abs(v.y) >= Mathf.Abs(v.x)) zone = v.y > 0f ? 0 : 2;
			else zone = v.x > 0f ? 1 : 3;
		}
		bool entered = zone != prevStickZone;
		prevStickZone = zone;

		MoveBackHeld = MoveLayerHeld && zone == 2;
		if (!MoveLayerHeld || !entered) return;
		MoveForwardTap = zone == 0;
		MoveRightTap = zone == 1;
		MoveLeftTap = zone == 3;
	}

	// ---------------------------------------------------------------------------------
	// Verbs
	// ---------------------------------------------------------------------------------

	void Pull()
	{
		float now = Time.time;
		bool spam = now - lastPull < spamGap;
		lastPull = now;

		if (Selected < 0 || !bonds[Selected].Bound) return;

		// Presentation: she commits to a struggle, pass or fail.
		player.NotifyStruggleAttempt();

		// Day 211: struggle on demand. A press while she's still starts a bout: it IS
		// beat 0, so it isn't scored and makes no progress. Every press, hit or not,
		// keeps the bout alive.
		bool starting = !InBout;
		boutUntil = now + boutTimeoutBeats * beatInterval;
		if (starting)
		{
			beatOrigin = now;
			AddNoise(noisePerPull);
			LastResult = "starts struggling";
			OnPullResolved?.Invoke(PullKind.Start, false);
			return;
		}

		float beatPhase = BeatPhase(now);
		float accuracy = 1f - Mathf.Min(beatPhase, 1f - beatPhase) * 2f;
		LogTiming(beatPhase, accuracy, spam);

		if (spam)
		{
			AddNoise(noisePerSpam);
			PlaySfx(spamClip != null ? spamClip : missClip, missVolume);
			LastResult = "SPAM";
			OnPullResolved?.Invoke(PullKind.Spam, ToolEngaged(Selected));
			return;
		}

		if (accuracy < goodWindow)
		{
			AddNoise(noisePerMiss);
			PlaySfx(missClip, missVolume);
			LastResult = $"off-beat {accuracy:P0}";
			OnPullResolved?.Invoke(PullKind.Miss, ToolEngaged(Selected));
			return;
		}

		var b = bonds[Selected];
		float toolMult = ToolMultFor(Selected, out string tool);
		LastBoost = tool != null ? $"{tool} ×{toolMult:0.#}" : "";
		float mult = (HandsFree ? freeHandsStruggleMult : 1f) * toolMult;
		float gain = gainPerPull * mult * accuracy / b.tightness;
		AddNoise(noisePerPull);
		// On the beat = hit, even when the bond won't give (cuffs). The sound grades the
		// timing; "needs a tool" is the mutter's job.
		PlaySfx(hitClip, hitVolume);

		if (gain <= 0f)
		{
			LastResult = $"won't give — needs a tool";
			return;
		}

		b.progress = Mathf.Clamp01(b.progress + gain);
		LastResult = tool != null ? $"pull {accuracy:P0} · {LastBoost}" : $"pull {accuracy:P0}";
		if (Selected == WristsIndex) player.NotifyStruggleProgress();
		OnPullResolved?.Invoke(PullKind.Hit, tool != null);
		if (b.progress >= 1f) Freed(b);
	}

	// Day 211: would a pull on bond i be using a tool? Same reach rules as ToolMultFor, no
	// side effects (ToolMultFor pings the env tool).
	bool ToolEngaged(int i)
	{
		if (i < 0 || i >= bonds.Length) return false;
		var b = bonds[i];
		if (b.envToolsReach && player.FindUsableEnvTool() != null) return true;
		Pickupable held = player.GetHeldItem();
		bool heldReaches = i == WristsIndex ? !HandsFree : b.untieable;
		return held != null && heldReaches && MultFor(held.ToolType) > 0f;
	}

	// Day 211: 0..1 within the beat, measured from the bout's beat 0.
	float BeatPhase(float t) => Mathf.Repeat(t - beatOrigin, beatInterval) / beatInterval;

	// Barehand gain x best available tool. Env tools reach only envToolsReach bonds (wrists,
	// hands behind her back). A held tool works the wrists while bound, and anything she
	// can untie once her hands are free. Never the elbows.
	float ToolMultFor(int i, out string toolName)
	{
		toolName = null;
		var b = bonds[i];
		float best = 0f;

		if (b.envToolsReach)
		{
			EnvironmentalTool env = player.FindUsableEnvTool();
			if (env != null)
			{
				float m = MultFor(env.ToolType);
				if (m > best) { best = m; toolName = env.name; }
				env.OnStruggle(player);
			}
		}

		Pickupable held = player.GetHeldItem();
		bool heldReaches = i == WristsIndex ? !HandsFree : b.untieable;
		if (held != null && heldReaches)
		{
			float m = MultFor(held.ToolType);
			if (m > best) { best = m; toolName = held.ItemName; }
		}

		if (best > 0f) return Mathf.Max(best, b.barehandGain);
		return b.barehandGain;
	}

	// ---------------------------------------------------------------------------------
	// Telemetry (Day 203)
	// ---------------------------------------------------------------------------------
	// Signed error = distance to the nearest beat, + = late. Max |error| is half a beat
	// (0.35 s at 0.7). Locked onto the half-beat snap shows as errors piling up near
	// ±0.35, which a plain mean hides (+0.33 and −0.33 average to 0 but are the SAME
	// point on the circle). So the summary uses circular stats on the phase:
	//   offset = circular mean, the true average error, signed
	//   R      = 0..1, how consistent. High R + offset near ±half-beat = locked wrong.

	readonly List<float> timingPhases = new List<float>();

	void LogTiming(float beatPhase, float accuracy, bool spam)
	{
		if (!logTiming) return;
		float err = (beatPhase < 0.5f ? beatPhase : beatPhase - 1f) * beatInterval;
		if (!spam) timingPhases.Add(beatPhase);   // spam is mashing, not an attempt at the beat
		string tag = spam ? "  SPAM" : accuracy < goodWindow ? "  MISS" : "";
		Debug.Log($"[BondStruggle] pull {err * 1000f:+0;-0;0} ms  acc {accuracy:P0}{tag}");
	}

	void OnDestroy() => DumpTiming();

	[ContextMenu("Dump timing")]
	void DumpTiming()
	{
		int n = timingPhases.Count;
		if (!logTiming || n == 0) return;

		float half = beatInterval * 0.5f;
		float window = (1f - goodWindow) * 0.5f * beatInterval;   // ±0.14 s at 0.7 / 0.6
		float nearHalfLine = half - 0.1f;                         // ≥ 0.25 s off
		float sx = 0f, sy = 0f;
		int inWindow = 0, nearHalf = 0;
		foreach (float p in timingPhases)
		{
			float a = p * Mathf.PI * 2f;
			sx += Mathf.Cos(a); sy += Mathf.Sin(a);
			float e = Mathf.Abs((p < 0.5f ? p : p - 1f) * beatInterval);
			if (e <= window) inWindow++;
			if (e >= nearHalfLine) nearHalf++;
		}
		float offset = Mathf.Atan2(sy, sx) / (Mathf.PI * 2f) * beatInterval;   // −half..+half
		float r = Mathf.Sqrt(sx * sx + sy * sy) / n;

		Debug.Log($"[BondStruggle] TIMING n={n}  offset {offset * 1000f:+0;-0;0} ms  R {r:0.00}  " +
			$"in window (±{window * 1000f:0} ms) {inWindow / (float)n:P0}  " +
			$"near half-beat (≥{nearHalfLine * 1000f:0} ms) {nearHalf / (float)n:P0}");
	}

	void PlaySfx(AudioClip clip, float volume)
	{
		if (clip == null || AudioManager.Instance == null) return;
		AudioManager.Instance.PlaySFX(clip, volume, 1f + Random.Range(-pitchJitter, pitchJitter));
	}

	float MultFor(ToolType t) => t switch
	{
		ToolType.Blade => bladeMult,
		ToolType.Point => pointMult,
		ToolType.Key => keyMult,
		_ => 0f,
	};

	void Untie(int dir)
	{
		if (!HandsFree) { LastResult = "hands are bound — struggle"; return; }
		if (ElbowsBound) { LastResult = "elbows tied — can't reach. Struggle them free"; return; }
		if (HandsFull) { LastResult = "hands full — struggle"; return; }
		if (!CanUntie(Selected)) { LastResult = "can't reach that one — struggle"; return; }
		if (hidingUntil > 0f) return;   // busy hiding

		BringHandsOut();
		var b = bonds[Selected];

		if (dir == b.knot[0])
		{
			b.knot.RemoveAt(0);
			FillKnot(b);
			b.progress = Mathf.Clamp01(b.progress + untieGain / b.tightness);
			AddNoise(noisePerUntie);
			LastResult = "untying";
			OnUntieInput?.Invoke(dir, true);
			if (b.progress >= 1f) Freed(b);
		}
		else
		{
			AddNoise(noisePerFumble);
			LastResult = "fumble";
			OnUntieInput?.Invoke(dir, false);
		}
	}

	void HideHands()
	{
		if (!HandsFree || !handsOut || hidingUntil > 0f) return;
		hidingUntil = Time.time + handsBackTime;
		LastResult = "hiding hands...";
	}

	/// <summary>
	/// Free hands come out: untie, pick-up, strike, or a cancelled feign. No-op while her
	/// wrists are bound. Ends the feign pose.
	/// </summary>
	public void BringHandsOut()
	{
		if (!HandsFree) { player.SetFeignState(false); return; }
		handsOut = true;
		hidingUntil = 0f;
		player.SetFeignState(false);
	}

	/// <summary>PlayerController.HoldItem: grabbing something with free hands puts them out.</summary>
	public void OnItemTaken() => BringHandsOut();

	public void AddNoise(float amount) => Noise = Mathf.Clamp01(Noise + amount);

	/// <summary>
	/// Day 211: the guard checks her bonds. Returns the most loosened bond she's been tied
	/// with (a freed one counts — it's the loudest evidence), or -1. found = its progress.
	/// </summary>
	public int MostLoosened(out float found)
	{
		int best = -1; found = 0f;
		for (int i = 0; i < bonds.Length; i++)
		{
			var b = bonds[i];
			if (!b.active || b.progress <= found) continue;
			best = i; found = b.progress;
		}
		return best;
	}

	/// <summary>Day 211: "Still tight." He gives bond i a tug. Progress only, no new knot.</summary>
	public void Cinch(int i, float amount)
	{
		if (i < 0 || i >= bonds.Length || !bonds[i].Bound) return;
		bonds[i].progress = Mathf.Max(0f, bonds[i].progress - amount);
		LastResult = $"he cinched your {bonds[i].name.ToLower()}";
		if (i == WristsIndex) player.NotifyStruggleProgress();
		SyncLimbs();
	}

	/// <summary>The guard reacted to this sound. It's spent — what catches her is the NEXT one.</summary>
	public void SpendNoise() => Noise = 0f;

	void Freed(StruggleBond b)
	{
		LastResult = b == bonds[WristsIndex] ? "WRISTS FREE — untie the rest, keep them hidden" : $"{b.name} FREE";
		Selected = NearestBound(Selected);   // Day 208: nearest on screen, not next in list
		SyncLimbs();
		OnBondFreed?.Invoke(b);

		if (NextBound(-1) < 0 && !SlippedOut)
		{
			SlippedOut = true;
			OnAllFree?.Invoke();
			StartCoroutine(SlippedOutSequence());
		}
	}

	// ---------------------------------------------------------------------------------
	// Caught
	// ---------------------------------------------------------------------------------

	/// <summary>
	/// The guard caught her. Called by GuardController under the fade, before the respawn.
	/// Proto rules: every active bond loses `setback` and tightens; caught with free hands =
	/// wrists from scratch and extra tight. Reserve bonds whose addedOnCatch is reached get
	/// tied on (Day 202: elbows on the first catch). New knots. Returns taken = loss.
	/// </summary>
	public CatchResult RegisterCatch(bool seenFreeHands)
	{
		Catches++;
		var r = new CatchResult { seen = seenFreeHands, catches = Catches };
		Noise = 0f;
		boutUntil = 0f;   // Day 211: she's re-tied and still; the next press starts fresh
		handsOut = false;
		hidingUntil = 0f;
		player.SetFeignState(false);

		if (Catches >= maxCatches)
		{
			r.taken = true;
			LastResult = "TAKEN";
			return r;
		}

		foreach (var b in bonds)
		{
			if (!b.active) continue;
			b.progress = Mathf.Max(0f, b.progress - setback);
			b.tightness += tightenPerCatch;
		}

		foreach (var b in bonds)
		{
			if (b.active || b.addedOnCatch <= 0 || Catches < b.addedOnCatch) continue;
			b.active = true;
			b.progress = 0f;
			b.tightness = 1f + tightenPerCatch;
			r.addedBond = b.name;
		}

		if (seenFreeHands && WristsIndex >= 0)
		{
			var w = bonds[WristsIndex];
			w.progress = 0f;
			w.tightness += freeHandsExtraTighten;
			Selected = WristsIndex;
		}

		foreach (var b in bonds) { b.knot.Clear(); FillKnot(b); }
		if (Selected < 0 || !bonds[Selected].Bound) Selected = NextBound(-1);

		SyncLimbs();
		player.NotifyStruggleProgress();
		LastResult = seenFreeHands
			? $"CAUGHT WITH FREE HANDS ({Catches}/{maxCatches}) — wrists from scratch, tighter"
			: $"CAUGHT ({Catches}/{maxCatches}) — re-tied, tighter";
		return r;
	}

	// ---------------------------------------------------------------------------------
	// Sync
	// ---------------------------------------------------------------------------------

	/// <summary>
	/// Push bond state out: BoundLimbs on the restraint (kick / movement modifiers, hints)
	/// and WristsFree on the player (strike gate, OnWristsFreed).
	/// </summary>
	void SyncLimbs()
	{
		BoundLimbs want = BoundLimbs.None;
		foreach (var b in bonds)
		{
			if (!b.Bound) continue;
			want |= b.limb;
			if (b.limb == BoundLimbs.Ankles) want |= BoundLimbs.AnkledToChair;
		}

		RestraintBase r = player.CurrentRestraint;
		if (r != null && r.BoundLimbs != want) r.SetBoundLimbs(want);

		player.SetWristsFreeFromStruggle(HandsFree);
	}

	void FillKnot(StruggleBond b)
	{
		while (b.knot.Count < KnotPreview)
		{
			int prev = b.knot.Count > 0 ? b.knot[b.knot.Count - 1] : -1;
			int d;
			do d = Random.Range(0, 4); while (d == prev);   // no repeats: reads as a path
			b.knot.Add(d);
		}
	}

	// ---- Day 208: relative bond selection, screen space ------------------------------

	void SelectToward(Vector2 flick)
	{
		if (Selected < 0 || !bonds[Selected].Bound) { Selected = NextBound(-1); return; }
		Vector2 from = ScreenPos(Selected);
		float cosCone = Mathf.Cos(selectCone * Mathf.Deg2Rad);
		int best = -1;
		float bestScore = float.MaxValue;
		for (int i = 0; i < bonds.Length; i++)
		{
			if (i == Selected || !bonds[i].Bound) continue;
			Vector2 d = ScreenPos(i) - from;
			float len = d.magnitude;
			if (len < 0.001f) continue;
			float cos = Vector2.Dot(d / len, flick);
			if (cos < cosCone) continue;
			float score = len * (2f - cos);   // closer wins; better aligned breaks near-ties
			if (score < bestScore) { bestScore = score; best = i; }
		}
		if (best >= 0) Selected = best;   // nothing that way: stay put
	}

	/// <summary>Nearest still-bound bond to bond `from` on screen. Falls back to list order.</summary>
	int NearestBound(int from)
	{
		if (from < 0) return NextBound(-1);
		Vector2 p = ScreenPos(from);
		int best = -1;
		float bestD = float.MaxValue;
		for (int i = 0; i < bonds.Length; i++)
		{
			if (i == from || !bonds[i].Bound) continue;
			float d = (ScreenPos(i) - p).sqrMagnitude;
			if (d < bestD) { bestD = d; best = i; }
		}
		return best >= 0 ? best : NextBound(from);
	}

	/// <summary>
	/// Where a bond sits on screen: the midpoint of its anchor bones through the main camera.
	/// Screen y is up, same as the stick. No camera or no rig → the bond's `dir` as a stand-in.
	/// </summary>
	Vector2 ScreenPos(int i)
	{
		Camera cam = Camera.main;
		if (cam == null || !TryAnchorWorld(i, out Vector3 w)) return bonds[i].dir;
		Vector3 sp = cam.WorldToScreenPoint(w);
		return new Vector2(sp.x, sp.y);
	}

	/// <summary>World position of bond i on her body: the midpoint of its anchor bones.
	/// Day 208: also where CassieHandsDriver puts the knot.</summary>
	public bool TryAnchorWorld(int i, out Vector3 world)
	{
		world = Vector3.zero;
		if (i < 0 || i >= bonds.Length) return false;
		Animator a = ResolveAnchorRig();
		if (a == null) return false;
		var b = bonds[i];
		HumanBodyBones[] bones = b.anchors != null && b.anchors.Length > 0 ? b.anchors : DefaultAnchors(b.limb);
		int n = 0;
		foreach (var hb in bones)
		{
			Transform t = a.GetBoneTransform(hb);
			if (t != null) { world += t.position; n++; }
		}
		if (n == 0) return false;
		world /= n;
		return true;
	}

	static readonly HumanBodyBones[] ElbowAnchors = { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm };
	static readonly HumanBodyBones[] WristAnchors = { HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
	static readonly HumanBodyBones[] AnkleAnchors = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
	static readonly HumanBodyBones[] KneeAnchors  = { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };
	static readonly HumanBodyBones[] NoAnchors    = { };

	static HumanBodyBones[] DefaultAnchors(BoundLimbs limb)
	{
		if ((limb & BoundLimbs.Elbows) != 0) return ElbowAnchors;
		if ((limb & BoundLimbs.Wrists) != 0) return WristAnchors;
		if ((limb & BoundLimbs.Knees) != 0)  return KneeAnchors;
		if ((limb & BoundLimbs.Ankles) != 0) return AnkleAnchors;
		return NoAnchors;
	}

	Animator ResolveAnchorRig()
	{
		if (anchorRig != null && anchorRig.isHuman) return anchorRig;
		anchorRig = GetComponentInChildren<Animator>();
		if (anchorRig == null || !anchorRig.isHuman)
		{
			var rig = FindFirstObjectByType<CassieRig>();
			if (rig != null)
			{
				anchorRig = rig.GetComponent<Animator>();
				if (anchorRig == null) anchorRig = rig.GetComponentInParent<Animator>();
				if (anchorRig == null) anchorRig = rig.GetComponentInChildren<Animator>();
			}
		}
		return anchorRig != null && anchorRig.isHuman ? anchorRig : null;
	}

	int NextBound(int from)
	{
		for (int k = 1; k <= bonds.Length; k++)
		{
			int i = (from + k + bonds.Length) % bonds.Length;
			if (bonds[i].Bound) return i;
		}
		return -1;
	}

	// Day 202 stub. The walk-around free state (pillar 2) replaces this. For now: the
	// guard stands down, Cassie gets her line, the level completes through the same
	// OnPlayerFreed signal the strike path uses (SliceCoordinator) — second way out.
	IEnumerator SlippedOutSequence()
	{
		if (GuardController.Instance != null) GuardController.Instance.StandDown();
		yield return new WaitForSeconds(0.6f);

		if (MutterSystem.Instance != null && !string.IsNullOrEmpty(slippedOutLine))
		{
			MutterSystem.Instance.Play(slippedOutLine, MutterSystem.Speaker.Cassie);
			yield return null;
			yield return new WaitWhile(() => MutterSystem.Instance != null && MutterSystem.Instance.IsActive);
		}

		Debug.Log("[BondStruggle] Every bond free — slipped out. Firing OnPlayerFreed.");
		player.OnPlayerFreed?.Invoke();
	}
}
