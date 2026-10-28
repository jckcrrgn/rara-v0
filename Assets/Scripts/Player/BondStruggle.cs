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
//   select bond           WASD              left stick
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
		[Tooltip("Stick / WASD direction that selects this bond.")]
		public Vector2 dir;
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
		new StruggleBond { name = "L Ankle", dir = Vector2.left,  limb = BoundLimbs.Ankles },
		new StruggleBond { name = "R Ankle", dir = Vector2.right, limb = BoundLimbs.Ankles },
	};

	[Header("Rhythm")]
	public float beatInterval = 0.7f;
	[Range(0, 1)] public float goodWindow = 0.6f;
	public float gainPerPull = 0.08f;
	public float spamGap = 0.25f;

	[Header("Noise")]
	[Tooltip("Even a clean pull makes some sound. Steady on-beat pulling never reaches the glance line.")]
	public float noisePerPull = 0.05f;
	public float noisePerMiss = 0.15f;
	public float noisePerSpam = 0.25f;
	public float noiseDecay = 0.1f;
	[Tooltip("Chair hop / turn-hop. Two quick hops = a call-out; any hop while he's watching = caught.")]
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

	// ---------------------------------------------------------------------------------

	public System.Action<StruggleBond> OnBondFreed;
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
	public bool CanUntie(int i) => HandsFree && i >= 0 && i < bonds.Length && bonds[i].untieable && bonds[i].Bound;
	/// <summary>0..1, 1 exactly on the beat.</summary>
	public float Pulse01 { get { float bp = (Time.time % beatInterval) / beatInterval; return 1f - Mathf.Min(bp, 1f - bp) * 2f; } }
	/// <summary>Beats elapsed (Time.time / beatInterval). Integers are the beats. CassieStruggleDriver locks to this.</summary>
	public float Beats => Time.time / Mathf.Max(0.01f, beatInterval);
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
	int prevStickZone = -1;   // -1 neutral, 0 up, 1 right, 2 down, 3 left

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

		// --- select ---
		if (!MoveLayerHeld && dirInput.sqrMagnitude > 0.25f)
		{
			float best = 0.5f;
			Vector2 n = dirInput.normalized;
			for (int i = 0; i < bonds.Length; i++)
			{
				if (!bonds[i].Bound) continue;   // can't select a free or reserve bond
				float d = Vector2.Dot(n, bonds[i].dir);
				if (d > best) { best = d; Selected = i; }
			}
		}

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

		if (spam) { AddNoise(noisePerSpam); LastResult = "SPAM"; return; }

		float beatPhase = (now % beatInterval) / beatInterval;
		float accuracy = 1f - Mathf.Min(beatPhase, 1f - beatPhase) * 2f;

		if (accuracy < goodWindow)
		{
			AddNoise(noisePerMiss);
			LastResult = $"off-beat {accuracy:P0}";
			return;
		}

		var b = bonds[Selected];
		float toolMult = ToolMultFor(Selected, out string tool);
		LastBoost = tool != null ? $"{tool} ×{toolMult:0.#}" : "";
		float mult = (HandsFree ? freeHandsStruggleMult : 1f) * toolMult;
		float gain = gainPerPull * mult * accuracy / b.tightness;
		AddNoise(noisePerPull);

		if (gain <= 0f)
		{
			LastResult = $"won't give — needs a tool";
			return;
		}

		b.progress = Mathf.Clamp01(b.progress + gain);
		LastResult = tool != null ? $"pull {accuracy:P0} · {LastBoost}" : $"pull {accuracy:P0}";
		if (Selected == WristsIndex) player.NotifyStruggleProgress();
		if (b.progress >= 1f) Freed(b);
	}

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
			if (b.progress >= 1f) Freed(b);
		}
		else
		{
			AddNoise(noisePerFumble);
			LastResult = "fumble";
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

	/// <summary>The guard reacted to this sound. It's spent — what catches her is the NEXT one.</summary>
	public void SpendNoise() => Noise = 0f;

	void Freed(StruggleBond b)
	{
		LastResult = b == bonds[WristsIndex] ? "WRISTS FREE — untie the rest, keep them hidden" : $"{b.name} FREE";
		Selected = NextBound(Selected);
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
