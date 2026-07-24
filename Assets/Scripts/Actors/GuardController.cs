using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scripted guard actor for the Vertical Slice ("The Turnaround").
///
/// NOT patrol AI — this is a deterministic state machine keyed to a
/// recurring check-in clock. The guard cycles offstage and returns at
/// fixed intervals; each return follows the same beat sequence. The
/// player's job is to enter the Feign pose before the guard reaches the
/// door sightline.
///
/// STATE MACHINE
/// -------------
/// Offstage → Approaching → AtDoor → LeanIn → Downed (strike lands)
///                                          ↘ Leaving → (back to Offstage)
///                                 ↘ Caught (not feigning at inspection)
///
/// On a PASSED inspection the guard walks straight in to Cassie and gloats in
/// her face (LeanIn). He does this EVERY check-in — it's his habit, the sadist
/// who can't resist getting close. There is no player verb to summon him; the
/// approach is unconditional. On the routine (unarmed) check-ins this is pure
/// threat — she endures him up close, unable to act. The turnaround happens on
/// whichever check-in she is finally armed: the same smug lean-in he's done
/// every time, except this time her hands come around swinging. The escalation
/// lives in HER state, not his — which is the whole engine of the dramatic
/// irony. (This replaces the earlier lure/"Call Out" verb, cut Day 62: in a
/// one-guard scripted slice, a summon-the-guard verb was agency theater. It
/// belongs to the AI levels, parked in ideas.md.)
///
/// Downed is terminal — StrikeableGuard calls OnGuardDowned() which stops all
/// coroutines and sets the state. No further cycle runs after Downed.
///
/// CLOCK
/// -----
/// The offstage phase runs for `offstageDuration` seconds. When it expires
/// the approach phase begins automatically. This is the check-in interval —
/// tune it so the player has enough time to make meaningful escape progress
/// between visits without the rhythm feeling absent.
///
/// FEIGN WINDOW
/// ------------
/// Opens when the guard enters Approaching (approach audio starts).
/// Closes when he reaches AtDoor. The player must press G during this window.
/// At AtDoor, IsFeigning is sampled once — pass → walk in (LeanIn); fail → Caught.
///
/// CLOSE-IN (the gloat)
/// --------------------
/// After passing inspection the guard enters LeanIn and physically walks to
/// Cassie's position, then taunts her up close. This is the strike window. He
/// does it on every passed inspection, armed or not. If she doesn't strike (no
/// weapon, or the player hesitates), he straightens after leanInDuration and
/// leaves — the loop continues and the next check-in is another chance.
///
/// His movement is SPEED-based (guardMoveSpeed, m/s), not duration-based: the
/// distance from the door to wherever Cassie sits varies, so a fixed duration
/// would make him crawl when she's near the door and lunge when she's far. A
/// constant speed reads as the same deliberate walk regardless of distance.
/// (The door APPROACH, by contrast, stays duration-based — that duration IS the
/// feign window, a gameplay clock, not a movement to be normalized.)
///
/// CAUGHT BRANCH
/// -------------
/// Not feigning at inspection → Caught state → re-cinch sequence (spec §10
/// default: re-cinch/escalate). Reuses the FailureLoopController fade pattern
/// conceptually, but lives here as a lighter version — the VS doesn't carry
/// the full failure loop apparatus (no Chair B, no LevelTimer, no lamp).
/// Re-cinch: fade to black → bond escalation → mutter → fade in.
///
/// MOVEMENT
/// --------
/// The guard's visible body (guardBody) physically lerps between positions:
/// offstage (out of sight) and door (inspection/gloat) are fixed anchors; the
/// lean-in target is computed live from Cassie's position (he leans into
/// wherever she actually is, stopping leanInStandoff short of her). Movement
/// rides the existing phase durations, so timing is unchanged. It is purely
/// presentational — strike validity is the LeanIn STATE, never the body's
/// distance from the player (see StrikeableGuard and PlayerController). If
/// anchors aren't wired, movement is skipped and the slice still runs on the
/// abstract state machine alone.
///
/// SINGLETON
/// ---------
/// Same pattern as LevelTimer and MutterSystem. Wire one instance per VS scene.
/// </summary>
public class GuardController : MonoBehaviour
{
	public static GuardController Instance { get; private set; }

	// -------------------------------------------------------------------------
	// State
	// -------------------------------------------------------------------------

	public enum GuardState
	{
		Offstage,
		Approaching,
		AtDoor,
		Leaving,
		Caught,
		LeanIn,
		Downed,
	}

	public GuardState CurrentState { get; private set; } = GuardState.Offstage;

	/// <summary>
	/// Fires on every state transition. Argument is the new state.
	/// UI and other systems subscribe to react to guard presence.
	/// </summary>
	public System.Action<GuardState> OnStateChanged;

	// -------------------------------------------------------------------------
	// Inspector — Timing
	// -------------------------------------------------------------------------

	[Header("Timing")]
	[Tooltip("How long the guard stays offstage between check-ins, in seconds. " +
		"This is the player's free-work window — enough to make meaningful escape " +
		"progress, short enough that the rhythm stays present. 45–60s is a " +
		"reasonable starting range; tune via playtesting.")]
	[SerializeField] private float offstageDuration = 50f;

	[Tooltip("How long the approach phase lasts — the time between 'footsteps " +
		"start' and 'guard is at the door.' This is the feign window duration. " +
		"Long enough to react even on first exposure (4–6s), short enough to " +
		"create real tension. The approach audio clip length should match or " +
		"be slightly shorter than this value.")]
	[SerializeField] private float approachDuration = 5f;

	[Tooltip("How long the guard stays at the door during inspection before the " +
		"outcome fires (pass → walk in / LeanIn, fail → Caught). A beat of held " +
		"tension " +
		"before the branch. 1–2s is enough to let the pause land.")]
	[SerializeField] private float inspectionHoldDuration = 1.5f;

	[Tooltip("How long the guard stays in LeanIn after he's finished closing the " +
		"distance — the strike window. His close-up gloat plays at the start of it. " +
		"If the player doesn't strike within this window (no weapon, or hesitation), " +
		"he straightens up and leaves; the next check-in is another chance. No hard " +
		"QTE — the window closes naturally when the timer expires. 4–6s gives enough " +
		"time to act without feeling infinite. Note: total time in-close = the " +
		"speed-based walk to Cassie PLUS this window.")]
	[SerializeField] private float leanInDuration = 5f;

	[Tooltip("Fallback duration for the leaving phase, used ONLY when no guardBody " +
		"or offstageAnchor is wired (abstract-testing mode). When anchors are " +
		"present the recede is speed-based (guardMoveSpeed). Keeps the abstract " +
		"state machine paced before the movement anchors are placed.")]
	[SerializeField] private float leavingDuration = 3f;

	// -------------------------------------------------------------------------
	// Inspector — Movement
	// -------------------------------------------------------------------------

	[Header("Movement")]
	[Tooltip("The guard's visible body — the Transform that physically moves " +
		"between anchors (in the graybox, the guard cube; later, the character " +
		"model root). If left unwired, falls back to the StrikeableGuard's " +
		"transform found in the scene. If neither resolves, movement is skipped " +
		"and phases just wait out their durations — the slice still runs.")]
	[SerializeField] private Transform guardBody;

	[Tooltip("Where the guard sits while Offstage — out of the player's sightline, " +
		"down the hallway past the door. He snaps here at the start of each " +
		"Offstage phase and lerps back here when Leaving.")]
	[SerializeField] private Transform offstageAnchor;

	[Tooltip("The doorway / inspection position. The guard lerps here during " +
		"Approaching and holds here through AtDoor; on a pass he walks on from here " +
		"to Cassie for the close-in.")]
	[SerializeField] private Transform doorAnchor;

	[Tooltip("How close the guard stops in FRONT of Cassie when he leans in, in " +
		"metres. He targets her live position (offset back toward the door so he " +
		"doesn't overlap her), not a fixed spot — he leans into wherever she " +
		"actually is. ~0.7–0.9 reads as in-her-face without clipping. Strike " +
		"validity is the LeanIn state, so this is purely how the lean reads.")]
	[SerializeField] private float leanInStandoff = 0.8f;

	[Tooltip("How fast the guard's body moves, in metres per second, for the " +
		"variable-distance walks: the lean-in to Cassie and the recede back " +
		"offstage. Speed-based (not duration-based) so the walk reads the same " +
		"whether she's sitting near the door or across the room. ~1.2–1.8 reads " +
		"as a deliberate, unhurried walk. For a seamless pace, set this near " +
		"(offstage→door distance / approachDuration) so the close-in matches the " +
		"door approach; set it deliberately different if you want the close-in to " +
		"feel like a distinct change of intent. The door approach itself is NOT " +
		"governed by this — its speed is set by approachDuration (the feign window).")]
	[SerializeField] private float guardMoveSpeed = 1.5f;

	[Tooltip("Ordered intermediate waypoints the guard walks THROUGH on his way " +
		"in to Cassie (and in reverse when leaving), routing around walls / " +
		"furniture that a straight door→Cassie lerp would clip through. " +
		"Hand-placed — this is a scripted actor on a fixed floorplan, not a " +
		"NavMesh agent. Leave empty for a clear straight shot (the guard then " +
		"walks door→Cassie directly, as before). The FINAL leg (last waypoint → " +
		"Cassie's live lean-in point) is still computed live, so place the " +
		"waypoint(s) in the OPEN part of the room — south of the bath partition — " +
		"and let that last leg cover wherever she actually hopped to. In this " +
		"room a single waypoint clears it: the partition is the only interior " +
		"obstacle, so one point south of it gives full line-of-sight to the rest.")]
	[SerializeField] private Transform[] leanInWaypoints;

	// -------------------------------------------------------------------------
	// Inspector — Audio
	// -------------------------------------------------------------------------

	[Header("Audio")]
	[Tooltip("Footsteps approaching — plays at the start of the Approaching state. " +
		"Length should be at or under approachDuration. This is the feign-window " +
		"telegraph: when the player hears this, they know to press G.")]
	[SerializeField] private AudioClip approachFootstepsClip;

	[Tooltip("Footsteps receding — plays at the start of the Leaving state.")]
	[SerializeField] private AudioClip leaveFootstepsClip;

	[Tooltip("Volume for all guard footstep audio. Diegetic — comes from offstage " +
		"so should be audible but not overpowering.")]
	[Range(0f, 1f)]
	[SerializeField] private float footstepsVolume = 0.85f;

	// -------------------------------------------------------------------------
	// Inspector — Mutter Content
	// -------------------------------------------------------------------------

	[Header("Mutter Content — Close Gloat (Guard)")]
	[Tooltip("Guard's taunt lines, spoken up close once he's leaned in. Played in " +
		"order, index 0 = first check-in, clamps to the last entry once exhausted. " +
		"Speaker: Guard. NOTE: he says the same kind of thing every check-in — he " +
		"can't perceive that she's armed, so there is deliberately no special " +
		"'climactic' line. The difference on the turnaround is entirely on her side. " +
		"Author these as in-her-face taunts, not door-distance glances.")]
	[TextArea(2, 4)]
	[SerializeField] private string[] routineGloatLines =
	{
		"Comfortable? Good. Don't go anywhere.",
		"Still with us? Wonderful.",
	};

	[Header("Mutter Content — Close Reaction (Cassie)")]
	[Tooltip("Cassie's internal reaction lines, queued behind each routine gloat. " +
		"Index matches routineGloatLines. Speaker: Cassie.")]
	[TextArea(2, 4)]
	[SerializeField] private string[] routineReactionLines =
	{
		"Keep walking.",
		"That's it. Trust the knots.",
	};

	[Header("Mutter Content — Caught")]
	[Tooltip("Guard's line when he catches Cassie not feigning. Plays over the " +
		"cut-to-black. Speaker: Guard.")]
	[TextArea(2, 4)]
	[SerializeField] private string caughtGuardLine =
		"I knew it. Let's try that again — tighter this time.";

	[Tooltip("Cassie's reaction after being caught and re-cinched. Plays after " +
		"fade-in. Speaker: Cassie.")]
	[TextArea(2, 4)]
	[SerializeField] private string caughtCassieReaction =
		"Back to square one. Think, Cassie.";

	// -------------------------------------------------------------------------
	// Inspector — Caught Re-Cinch
	// -------------------------------------------------------------------------

	[Header("Caught — Re-Cinch")]
	[Tooltip("Full-screen black CanvasGroup. Same pattern as FailureLoopController. " +
		"Alpha 0 = gameplay visible; alpha 1 = fully black. Required for the " +
		"caught sequence to read correctly — without it the state reset is instant " +
		"and jarring.")]
	[SerializeField] private CanvasGroup fadeOverlay;

	[Tooltip("Fade duration for the cut-to-black and fade-in on a caught event.")]
	[SerializeField] private float caughtFadeDuration = 0.5f;

	[Tooltip("Ordered SFX sequence played during the caught blackout — rope cinch, " +
		"Cassie grunt, etc. Same pattern as FailureLoopController.rebindSfxSequence.")]
	[SerializeField] private AudioClip[] caughtRebindSfx;

	[Tooltip("Volume for caught rebind SFX.")]
	[Range(0f, 1f)]
	[SerializeField] private float caughtRebindSfxVolume = 1f;

	[Tooltip("The bond added when Cassie is caught. Applied once per catch. " +
		"Spec default: escalate (Ankles first, then Elbows, etc.) — but for " +
		"v1 we apply a single fixed bond rather than a full escalation ladder. " +
		"Set to None (0) to skip bond escalation on catch (soft re-cinch).")]
	[SerializeField] private BoundLimbs caughtBondToAdd = BoundLimbs.None;

	[Tooltip("Respawn transform for Cassie after a caught reset. If null, " +
		"position is not changed — useful during early testing.")]
	[SerializeField] private Transform caughtRespawnPoint;

	[Tooltip("UnityEvent fired during the caught state mutation (same role as " +
		"FailureLoopController.onStateReset). Wire scene-specific resets here.")]
	[SerializeField] private UnityEvent onCaughtReset;

	// -------------------------------------------------------------------------
	// Inspector — LeanIn Scene Hook
	// -------------------------------------------------------------------------

	[Header("LeanIn — Scene Hook")]
	[Tooltip("UnityEvent fired at the start of the close-in (LeanIn) — the moment " +
		"the guard commits to walking in on Cassie. Wire scene-specific responses " +
		"here, e.g. a camera nudge, or showing a contextual 'Strike (H)' hint card " +
		"while she's armed and he's in range.")]
	[SerializeField] private UnityEngine.Events.UnityEvent onLeanInEntered;

	// -------------------------------------------------------------------------
	// Inspector — Debug
	// -------------------------------------------------------------------------

	[Header("Debug")]
	[SerializeField] private bool verboseLogging = true;

	// -------------------------------------------------------------------------
	// Runtime State
	// -------------------------------------------------------------------------

	// How many check-ins have completed. Used to index the close-gloat mutter
	// lines. Incremented once at the start of LeanInPhase.
	private int checkInCount = 0;

	// Re-entry guard for the caught sequence (same pattern as FailureLoopController).
	private bool caughtSequenceRunning = false;

	// Player reference — resolved once at Start.
	private PlayerController player;

	// -------------------------------------------------------------------------
	// Unity Lifecycle
	// -------------------------------------------------------------------------

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogWarning("[GuardController] Duplicate instance — destroying.");
			Destroy(gameObject);
			return;
		}
		Instance = this;

		if (fadeOverlay != null)
		{
			fadeOverlay.alpha = 0f;
			fadeOverlay.blocksRaycasts = false;
		}
	}

	void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	void Start()
	{
		player = FindFirstObjectByType<PlayerController>();
		if (player == null)
		{
			Debug.LogWarning("[GuardController] No PlayerController found. " +
				"Inspection outcomes will not fire correctly.");
		}

		// Fall back to the scene's StrikeableGuard transform if no explicit
		// body is wired — the cube/model carrying StrikeableGuard is the thing
		// that should move.
		if (guardBody == null)
		{
			StrikeableGuard sg = FindFirstObjectByType<StrikeableGuard>();
			if (sg != null) guardBody = sg.transform;
		}

		StartCoroutine(OffstagePhase());
	}

	// -------------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------------

	/// <summary>
	/// Hard-stop the guard cycle and move to Downed. Called by StrikeableGuard
	/// when the strike lands. StopAllCoroutines prevents any pending LeanIn /
	/// Leaving logic from running after the guard is KO'd.
	/// </summary>
	public void OnGuardDowned()
	{
		StopAllCoroutines();
		SetState(GuardState.Downed);
		Log("Guard DOWNED. Cycle stopped.");
	}

	// -------------------------------------------------------------------------
	// State Machine Phases
	// -------------------------------------------------------------------------

	private IEnumerator OffstagePhase()
	{
		SetState(GuardState.Offstage);
		Log($"Offstage phase — waiting {offstageDuration}s.");

		// Snap the body to its offstage anchor — he's out of sight between
		// check-ins. No-op if no body/anchor wired.
		if (guardBody != null && offstageAnchor != null)
			guardBody.position = offstageAnchor.position;

		// Auto-release feign at the start of the offstage window. The guard
		// has left; there's no reason to hold the pose. Covers the case where
		// the player forgets to toggle off manually.
		if (player != null && player.IsFeigning)
			player.CancelFeign();

		yield return new WaitForSeconds(offstageDuration);
		StartCoroutine(ApproachPhase());
	}

	private IEnumerator ApproachPhase()
	{
		SetState(GuardState.Approaching);
		Log("Guard approaching — feign window OPEN.");

		if (AudioManager.Instance != null && approachFootstepsClip != null)
			AudioManager.Instance.PlaySFX(approachFootstepsClip, footstepsVolume, 1f);

		// Walk to the door over the approach window — this IS the telegraph the
		// player reacts to. Move duration == approachDuration, so the feign
		// window timing is unchanged from the audio-only version.
		yield return MoveBody(doorAnchor, approachDuration);
		StartCoroutine(AtDoorPhase());
	}

	private IEnumerator AtDoorPhase()
	{
		SetState(GuardState.AtDoor);
		Log("Guard at door — feign window CLOSED. Inspecting...");

		// Hold beat before sampling — lets the tension land.
		yield return new WaitForSeconds(inspectionHoldDuration);

		// Inspection outcome. Feign is only load-bearing once Cassie has visible
		// escape evidence to hide. No evidence + not feigning = a bound prisoner
		// sitting there — nothing to catch — so it routes to the same lean-in/gloat
		// as a pass. Caught requires BOTH: evidence AND not feigning.
		bool feigning = player != null && player.IsFeigning;
		bool evidence = HasEscapeEvidence();
		bool caught = evidence && !feigning;
		Log($"Inspection: feigning={feigning}, evidence={evidence} -> {(caught ? "CAUGHT" : "SAFE (lean-in)")}");

		if (caught)
			StartCoroutine(CaughtPhase());
		else
			StartCoroutine(LeanInPhase());
	}

	/// <summary>
	/// The close-in: on a passed inspection the guard walks in to Cassie and
	/// gloats in her face. This is the strike window. He does it every check-in,
	/// armed or not — it's his habit. The player has the duration of the walk
	/// plus leanInDuration to press H (Strike); a strike only succeeds if Cassie
	/// is armed (PlayerController.CanStrikeNow gates on the held weapon), so the
	/// unarmed early check-ins are pure threat — he leans in, taunts, leaves.
	///
	/// GuardController does NOT poll for the strike here — PlayerController calls
	/// StrikeableGuard.OnStruck(), which calls OnGuardDowned(), which calls
	/// StopAllCoroutines() on this component. That stops this coroutine
	/// mid-execution: once the guard is down, none of the remaining LeanIn /
	/// Leaving logic should run.
	/// </summary>
	private IEnumerator LeanInPhase()
	{
		SetState(GuardState.LeanIn);

		// Index the close-gloat line, then increment. Index 0 = first check-in;
		// clamps to the last line once exhausted.
		int idx = Mathf.Min(checkInCount, routineGloatLines.Length - 1);
		checkInCount++;

		Log($"Guard CLOSE-IN (check-in #{checkInCount}) — strike window open.");

		// Scene hook — fire anything wired to the close-in moment (camera nudge,
		// strike-verb hint prompt, etc.).
		onLeanInEntered?.Invoke();

		// Walk the routing waypoints first (door → mid-point(s)), THEN the live
		// final leg to Cassie. The waypoints route him around the bath partition
		// (and anything else) that a naive straight door→Cassie lerp would clip
		// through. He's already in LeanIn for all of it, but CanStrikeNow's
		// proximity gate keeps the strike — and the Strike (H) prompt — closed
		// until the final leg brings him within strikeRange, so being in-state
		// during the far waypoint legs is harmless.
		if (leanInWaypoints != null)
		{
			foreach (Transform wp in leanInWaypoints)
			{
				if (wp == null) continue;
				yield return MoveBodyAtSpeed(wp.position, guardMoveSpeed);
			}
		}

		// Final leg: close on wherever Cassie actually is — computed from her live
		// position (she's feigning, so frozen for the duration of the walk). State
		// is already LeanIn, so a strike landing mid-walk is valid (she swings as
		// he closes); if it lands, OnGuardDowned's StopAllCoroutines freezes him here.
		yield return MoveBodyAtSpeed(ComputeLeanInPoint(), guardMoveSpeed);

		// In her face now — the taunt. He says the same kind of thing every time;
		// he can't tell this one's different. The player can cut him off by
		// striking (H dismisses the mutter and swings — handled in PlayerController).
		if (MutterSystem.Instance != null)
		{
			if (routineGloatLines.Length > 0)
				MutterSystem.Instance.Play(routineGloatLines[idx], MutterSystem.Speaker.Guard);
			if (routineReactionLines.Length > idx)
				MutterSystem.Instance.Play(routineReactionLines[idx], MutterSystem.Speaker.Cassie);
		}

		// Hold the strike window. If a strike lands, OnGuardDowned fires
		// StopAllCoroutines — this yield never returns.
		yield return new WaitForSeconds(leanInDuration);

		// Window lapsed (no weapon, or the player held off). Guard straightens
		// and leaves; the next check-in is another chance.
		Log("Strike window lapsed. Guard leaving without incident.");
		StartCoroutine(LeavingPhase());
	}

	private IEnumerator LeavingPhase()
	{
		SetState(GuardState.Leaving);
		Log("Guard leaving — receding to offstage.");

		if (AudioManager.Instance != null && leaveFootstepsClip != null)
			AudioManager.Instance.PlaySFX(leaveFootstepsClip, footstepsVolume, 1f);

		// Recede to the offstage anchor at walking speed. Speed-based for the
		// same reason as the lean-in: he now leaves from wherever he leaned in
		// (next to Cassie), so the distance varies — a fixed duration would make
		// his exit pace lurch. If no offstage anchor is wired, fall back to a
		// fixed beat so the abstract state machine still paces.
		// Leave by reversing the approach exactly: Cassie → waypoint(s) → door →
		// offstage. The approach ran offstage → door → waypoint(s) → Cassie, so
		// the recede retraces it. The door leg is NOT optional polish: waypoint0 →
		// offstage is not a clear straight line (it clips the partition and the
		// north wall), but routing back out through the doorway the same way he
		// came in is. All legs are speed-based — the door here is just a routing
		// point on the way out, not the feign-window telegraph it is on approach.
		// Body-gated; the abstract fallback below still paces the state machine
		// before any anchors/waypoints are placed.
		if (guardBody != null && leanInWaypoints != null)
		{
			for (int i = leanInWaypoints.Length - 1; i >= 0; i--)
			{
				if (leanInWaypoints[i] == null) continue;
				yield return MoveBodyAtSpeed(leanInWaypoints[i].position, guardMoveSpeed);
			}
		}

		// Back out through the doorway before receding offstage.
		if (guardBody != null && doorAnchor != null)
			yield return MoveBodyAtSpeed(doorAnchor.position, guardMoveSpeed);

		if (guardBody != null && offstageAnchor != null)
			yield return MoveBodyAtSpeed(offstageAnchor.position, guardMoveSpeed);
		else
			yield return new WaitForSeconds(leavingDuration);

		StartCoroutine(OffstagePhase());
	}

	// -------------------------------------------------------------------------
	// Caught Sequence
	// -------------------------------------------------------------------------

	private IEnumerator CaughtPhase()
	{
		if (caughtSequenceRunning)
		{
			Debug.LogWarning("[GuardController] CaughtPhase called while already running. Ignoring.");
			yield break;
		}
		caughtSequenceRunning = true;
		SetState(GuardState.Caught);
		Log("=== CAUGHT sequence START ===");

		// Step 1: Lock player input.
		if (player != null) player.enabled = false;

		// Step 2: Fade to black.
		yield return FadeOverlay(0f, 1f, caughtFadeDuration);

		// Step 3: Rebind SFX.
		yield return PlayCaughtSfxSequence();

		// Step 4: State mutation — bond escalation + position reset.
		MutateCaughtState();

		// Step 5: Guard caught line (over black).
		if (MutterSystem.Instance != null)
		{
			if (!string.IsNullOrEmpty(caughtGuardLine))
				MutterSystem.Instance.Play(caughtGuardLine, MutterSystem.Speaker.Guard);
			if (!string.IsNullOrEmpty(caughtCassieReaction))
				MutterSystem.Instance.Play(caughtCassieReaction, MutterSystem.Speaker.Cassie);
		}

		// Step 6: Wait for guard line dismiss, then fade in.
		yield return null;
		yield return new WaitUntil(() =>
			MutterSystem.Instance == null || MutterSystem.Instance.WasJustDismissed);

		// Step 7: Fade in.
		yield return FadeOverlay(1f, 0f, caughtFadeDuration);

		// Step 8: Wait for Cassie reaction to finish.
		yield return new WaitWhile(() =>
			MutterSystem.Instance != null && MutterSystem.Instance.IsActive);

		// Step 9: Release input and restart cycle.
		if (player != null) player.enabled = true;

		caughtSequenceRunning = false;
		Log("=== CAUGHT sequence END. Restarting cycle. ===");

		StartCoroutine(OffstagePhase());
	}

	private void MutateCaughtState()
	{
		if (player == null) return;

		// Bond escalation — add the configured bond (if any).
		if (caughtBondToAdd != 0 && player.CurrentRestraint != null)
		{
			player.CurrentRestraint.AddBondState(caughtBondToAdd);
			Log($"Caught bond escalation: added {caughtBondToAdd}. " +
				$"New BoundLimbs = {player.CurrentRestraint.BoundLimbs}");
		}

		// Reset bond cut progress — the guard re-ties her. ResetBondProgress
		// also clears PlayerController.wristsFree so phase 1 restarts correctly
		// if she was caught after having freed her wrists.
		player.ResetBondProgress();

		// Disarm on re-bind: the guard takes what's in her hands. Whether it comes
		// back is the item's policy (Pickupable.returnedOnDisarm) — the bottle is
		// returned to the table because it's the only weapon and the only win path,
		// and it reads as his hubris: he keeps leaving it within her reach.
		player.DisarmHeldItem();

		// Position reset.
		if (caughtRespawnPoint != null)
		{
			player.transform.position = caughtRespawnPoint.position;
			player.transform.rotation = caughtRespawnPoint.rotation;
			Log($"Position reset to {caughtRespawnPoint.position}.");
		}

		// Scene-specific resets.
		onCaughtReset?.Invoke();

		// Snap the guard back offstage while the screen is black, so he isn't
		// seen popping from the door after fade-in. OffstagePhase will also
		// place him there, but doing it under the fade avoids the visible pop.
		if (guardBody != null && offstageAnchor != null)
			guardBody.position = offstageAnchor.position;
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	private IEnumerator PlayCaughtSfxSequence()
	{
		if (caughtRebindSfx == null || caughtRebindSfx.Length == 0) yield break;

		foreach (AudioClip clip in caughtRebindSfx)
		{
			if (clip == null) continue;
			if (AudioManager.Instance != null)
				AudioManager.Instance.PlaySFX(clip, caughtRebindSfxVolume, 1f);
			yield return new WaitForSeconds(clip.length);
		}
	}

	/// <summary>
	/// Lerp the guard body to a target anchor over `duration` seconds. Used ONLY
	/// for the door approach, where the duration IS the feign window — a gameplay
	/// clock, deliberately fixed regardless of distance. (Distance-varying walks —
	/// lean-in, leaving — use MoveBodyAtSpeed instead.) Degrades gracefully: if no
	/// body or target is wired it just waits out the duration so the feign-window
	/// timing is preserved even before the movement anchors are placed.
	/// </summary>
	private IEnumerator MoveBody(Transform target, float duration)
	{
		if (guardBody == null || target == null)
		{
			yield return new WaitForSeconds(duration);
			yield break;
		}

		Vector3 start = guardBody.position;
		Vector3 end = target.position;
		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			guardBody.position = Vector3.Lerp(start, end, Mathf.Clamp01(t / duration));
			yield return null;
		}
		guardBody.position = end;
	}

	/// <summary>
	/// Move the guard body to an explicit world point at a constant `speed`
	/// (metres/second), so a longer walk simply takes longer rather than moving
	/// faster. Used for the variable-distance walks — the lean-in to Cassie and
	/// the recede offstage. Falls back to an immediate return if there's no body
	/// (the strike window / offstage wait provides pacing in abstract mode).
	/// </summary>
	private IEnumerator MoveBodyAtSpeed(Vector3 end, float speed)
	{
		if (guardBody == null) yield break;

		Vector3 start = guardBody.position;
		float distance = Vector3.Distance(start, end);

		// Degenerate cases: zero distance, or a non-positive speed that would
		// divide-by-zero / never arrive. Snap and bail.
		if (distance < 0.0001f || speed <= 0f)
		{
			guardBody.position = end;
			yield break;
		}

		float duration = distance / speed;
		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			guardBody.position = Vector3.Lerp(start, end, Mathf.Clamp01(t / duration));
			yield return null;
		}
		guardBody.position = end;
	}

	/// <summary>
	/// The world point the guard steps to when he leans in: leanInStandoff
	/// metres in front of Cassie, on the side he's approaching from (his
	/// current position, i.e. the door), so he stops at her face without
	/// overlapping her. Computed at the moment of lean-in — she's feigning and
	/// therefore stationary, so a one-shot snapshot is accurate. Keeps his own
	/// height so he doesn't sink into the floor.
	/// </summary>
	private Vector3 ComputeLeanInPoint()
	{
		if (guardBody == null) return Vector3.zero;
		if (player == null) return guardBody.position;

		Vector3 cassie = player.transform.position;
		Vector3 fromCassieToGuard = guardBody.position - cassie;
		fromCassieToGuard.y = 0f;

		// Degenerate case (guard already on top of her): just hold position.
		if (fromCassieToGuard.sqrMagnitude < 0.0001f) return guardBody.position;

		Vector3 point = cassie + fromCassieToGuard.normalized * leanInStandoff;
		point.y = guardBody.position.y;
		return point;
	}

	private IEnumerator FadeOverlay(float from, float to, float duration)
	{
		if (fadeOverlay == null) yield break;

		if (to > from) fadeOverlay.blocksRaycasts = true;

		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			fadeOverlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
			yield return null;
		}
		fadeOverlay.alpha = to;

		if (to <= 0f) fadeOverlay.blocksRaycasts = false;
	}

	private void SetState(GuardState newState)
	{
		CurrentState = newState;
		OnStateChanged?.Invoke(newState);
		Log($"State → {newState}");
	}

	private void Log(string msg)
	{
		if (verboseLogging) Debug.Log($"[Guard] {msg}");
	}

	/// <summary>
	/// True when Cassie is visibly mid-escape — the state a feign conceals.
	/// Wrists already free, any bond-cut progress, or a tool in hand. All three
	/// read from existing PlayerController surface. Moved-from-spawn intentionally
	/// excluded: noisy under struggle jitter, redundant in the VS.
	/// </summary>
	private bool HasEscapeEvidence()
	{
		if (player == null) return false;
		return player.WristsFree
			|| player.StruggleProgress > 0
			|| player.GetHeldItem() != null;
	}
}
