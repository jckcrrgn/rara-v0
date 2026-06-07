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
/// Offstage → Approaching → AtDoor → Gloating ─[lure]─→ LeanIn → Downed (strike lands)
///                                                               ↘ Leaving (window lapses)
///                                             → Leaving → (back to Offstage)
///                                  ↘ Caught (not feigning at inspection)
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
/// At AtDoor, IsFeigning is sampled once — pass → Gloating; fail → Caught.
///
/// LURE
/// ----
/// After passing inspection and entering Gloating, the player can fire the
/// lure verb (T key by default, while feigning) to draw the guard into
/// LeanIn. Fiction: Cassie calls out / strains against the gag; he steps
/// in to relish it, not knowing she's armed. The lure is available on every
/// check-in regardless of arm state — an unarmed lure produces a near-miss
/// (he leans in, gloats up close, leaves). The catharsis fires when she's
/// armed, wrists-free, and strikes during the LeanIn window.
///
/// AttemptLure() sets a flag that GloatPhase picks up on its next tick,
/// aborting the leave path and starting LeanIn. Mutter dismissal is handled
/// by PlayerController before the call, mirroring how TryStrike dismisses
/// the lean-in gloat.
///
/// CAUGHT BRANCH
/// -------------
/// Not feigning at inspection → Caught state → re-cinch sequence (spec §10
/// default: re-cinch/escalate). Reuses the FailureLoopController fade pattern
/// conceptually, but lives here as a lighter version — the VS doesn't carry
/// the full failure loop apparatus (no Chair B, no LevelTimer, no lamp).
/// Re-cinch: fade to black → bond escalation → mutter → fade in.
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
		Gloating,
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
		"outcome fires (pass → Gloating, fail → Caught). A beat of held tension " +
		"before the branch. 1–2s is enough to let the pause land.")]
	[SerializeField] private float inspectionHoldDuration = 1.5f;

	[Tooltip("How long the guard lingers during a routine gloat before leaving. " +
		"His mutter fires at the start of this window; he leaves when it expires " +
		"(or when the mutter is dismissed, whichever is longer). 0 = leave " +
		"immediately after mutter dismiss. A lure fired during this window " +
		"will divert to LeanIn regardless of whether the linger has expired.")]
	[SerializeField] private float gloatLingerDuration = 0f;

	[Tooltip("How long the guard stays in LeanIn — the strike window. If the " +
		"player doesn't strike within this window, he straightens up and leaves. " +
		"No hard QTE — the window closes naturally when the timer expires. " +
		"4–6s gives the player enough time to act without feeling infinite.")]
	[SerializeField] private float leanInDuration = 5f;

	[Tooltip("How long the leaving phase lasts (footsteps receding). Guards " +
		"the player from feign-releasing too early and breaking the fiction.")]
	[SerializeField] private float leavingDuration = 3f;

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

	[Header("Mutter Content — Routine Gloat (Guard)")]
	[Tooltip("Guard's gloat lines for routine check-ins, played in order. " +
		"Index 0 = first check-in. Clamps to last entry once exhausted, so " +
		"the final line repeats on any extra routine check-ins. Speaker: Guard.")]
	[TextArea(2, 4)]
	[SerializeField] private string[] routineGloatLines =
	{
		"Comfortable? Good. Don't go anywhere.",
		"Still with us? Wonderful.",
	};

	[Header("Mutter Content — Routine Reaction (Cassie)")]
	[Tooltip("Cassie's internal reaction lines, queued behind each routine gloat. " +
		"Index matches routineGloatLines. Speaker: Cassie.")]
	[TextArea(2, 4)]
	[SerializeField] private string[] routineReactionLines =
	{
		"Keep walking.",
		"That's it. Trust the knots.",
	};

	[Header("Mutter Content — LeanIn (Guard)")]
	[Tooltip("Guard's line when he leans in close — he thinks he's won; he " +
		"doesn't know she's armed. Speaker: Guard. Plays at the start of LeanIn.")]
	[TextArea(2, 4)]
	[SerializeField] private string leanInGuardLine =
		"Look at you. Helpless.";

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
	[SerializeField] private BoundLimbs caughtBondToAdd = BoundLimbs.Ankles;

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
	[Tooltip("UnityEvent fired at the start of LeanIn — the moment the guard " +
		"steps in close. Wire scene-specific responses here (e.g. a camera nudge, " +
		"enabling a UI hint for the strike key, triggering a Beg verb display).")]
	[SerializeField] private UnityEngine.Events.UnityEvent onLeanInEntered;

	// -------------------------------------------------------------------------
	// Inspector — Debug
	// -------------------------------------------------------------------------

	[Header("Debug")]
	[SerializeField] private bool verboseLogging = true;

	// -------------------------------------------------------------------------
	// Runtime State
	// -------------------------------------------------------------------------

	// How many check-ins have completed. Used to index mutter lines.
	// Incremented at the start of GloatPhase (before the wait loop) so both
	// exit paths — normal leave and lure-divert to LeanIn — see a consistent
	// count. No double-increment from LeanInPhase on lapse.
	private int checkInCount = 0;

	// Set by AttemptLure() when the player fires the lure verb during a gloat.
	// GloatPhase polls this each frame in its wait loop and diverts to LeanIn
	// when true. Cleared at the start of each GloatPhase to avoid stale state.
	private bool lureRequested = false;

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

		StartCoroutine(OffstagePhase());
	}

	// -------------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------------

	/// <summary>
	/// True while the guard is in the Gloating state — the only window in
	/// which a lure can draw him into LeanIn. PlayerController checks this
	/// before calling AttemptLure so the key is a silent no-op offstage.
	/// </summary>
	public bool CanBeLured => CurrentState == GuardState.Gloating;

	/// <summary>
	/// Called by PlayerController when the player fires the lure verb while
	/// feigning during a gloat. Sets lureRequested; GloatPhase picks it up
	/// on its next coroutine tick and diverts to LeanInPhase.
	///
	/// Mutter dismissal is handled by PlayerController before this call —
	/// same pattern as TryStrike dismissing the lean-in gloat mutter.
	/// Safe no-op if called outside Gloating state.
	/// </summary>
	public void AttemptLure()
	{
		if (!CanBeLured)
		{
			Log("AttemptLure called outside Gloating state — ignored.");
			return;
		}
		lureRequested = true;
		Log("Lure requested — diverting to LeanIn on next coroutine tick.");
	}

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

		yield return new WaitForSeconds(approachDuration);
		StartCoroutine(AtDoorPhase());
	}

	private IEnumerator AtDoorPhase()
	{
		SetState(GuardState.AtDoor);
		Log("Guard at door — feign window CLOSED. Inspecting...");

		// Hold beat before sampling — lets the tension land.
		yield return new WaitForSeconds(inspectionHoldDuration);

		// Sample IsFeigning ONCE. This is the only moment that matters.
		bool passed = player != null && player.IsFeigning;
		Log($"Inspection result: {(passed ? "PASS (feigning)" : "FAIL (not feigning)")}");

		if (passed)
			StartCoroutine(GloatPhase());
		else
			StartCoroutine(CaughtPhase());
	}

	private IEnumerator GloatPhase()
	{
		SetState(GuardState.Gloating);
		lureRequested = false; // clear any stale flag from a previous cycle

		// Compute the mutter index before incrementing so index 0 = first check-in.
		// Increment immediately so both exit paths (normal leave and lure-divert
		// to LeanIn) leave checkInCount consistent — no double-counting.
		int idx = Mathf.Min(checkInCount, routineGloatLines.Length - 1);
		checkInCount++;

		if (MutterSystem.Instance != null)
		{
			if (routineGloatLines.Length > 0)
				MutterSystem.Instance.Play(routineGloatLines[idx], MutterSystem.Speaker.Guard);
			if (routineReactionLines.Length > idx)
				MutterSystem.Instance.Play(routineReactionLines[idx], MutterSystem.Speaker.Cassie);
		}

		// Wait for the gloat to finish, watching for a lure request each frame.
		// The mutter is dismissed by PlayerController before AttemptLure is called,
		// so IsActive may already be false when lureRequested is set — check
		// lureRequested first so we always catch it regardless of ordering.
		float lingerTimer = 0f;
		while (lingerTimer < gloatLingerDuration ||
			   (MutterSystem.Instance != null && MutterSystem.Instance.IsActive))
		{
			if (lureRequested)
			{
				lureRequested = false;
				Log("Lure fired — guard stepping in close.");
				StartCoroutine(LeanInPhase());
				yield break;
			}
			lingerTimer += Time.deltaTime;
			yield return null;
		}

		// No lure — guard finishes gloating and leaves normally.
		StartCoroutine(LeavingPhase());
	}

	/// <summary>
	/// Guard leans in close. This is the strike window. The player has
	/// leanInDuration seconds to press H (Strike). If they don't, the guard
	/// straightens and leaves — the loop continues and she can lure him in
	/// again on the next check-in.
	///
	/// GuardController does NOT poll for the strike here — PlayerController
	/// calls StrikeableGuard.OnStruck(), which calls OnGuardDowned(), which
	/// calls StopAllCoroutines() on this component. That stops this coroutine
	/// mid-execution: once the guard is down, none of the remaining
	/// LeanIn / Leaving logic should run.
	/// </summary>
	private IEnumerator LeanInPhase()
	{
		SetState(GuardState.LeanIn);
		Log($"Guard LEAN IN — strike window open for {leanInDuration}s.");

		// Guard's close-up line — he thinks he's won.
		if (MutterSystem.Instance != null && !string.IsNullOrEmpty(leanInGuardLine))
			MutterSystem.Instance.Play(leanInGuardLine, MutterSystem.Speaker.Guard);

		// Scene hook — fire anything wired to the lean-in moment.
		onLeanInEntered?.Invoke();

		// Hold the strike window. If a strike lands, OnGuardDowned fires
		// StopAllCoroutines — this yield never returns.
		yield return new WaitForSeconds(leanInDuration);

		// Window lapsed. Guard straightens and leaves. No penalty — the lure
		// is available on the next check-in. checkInCount was already
		// incremented in GloatPhase when this check-in began.
		Log("Strike window lapsed. Guard leaving without incident.");
		StartCoroutine(LeavingPhase());
	}

	private IEnumerator LeavingPhase()
	{
		SetState(GuardState.Leaving);
		Log($"Guard leaving — {leavingDuration}s receding audio.");

		if (AudioManager.Instance != null && leaveFootstepsClip != null)
			AudioManager.Instance.PlaySFX(leaveFootstepsClip, footstepsVolume, 1f);

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

		// Position reset.
		if (caughtRespawnPoint != null)
		{
			player.transform.position = caughtRespawnPoint.position;
			player.transform.rotation = caughtRespawnPoint.rotation;
			Log($"Position reset to {caughtRespawnPoint.position}.");
		}

		// Scene-specific resets.
		onCaughtReset?.Invoke();
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
}
