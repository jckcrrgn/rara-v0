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
/// Offstage → Approaching → AtDoor → Gloating → Leaving → (back to Offstage)
///                                  ↘ Caught (not feigning at inspection)
///                                  ↘ LeanIn (climactic, feigning + prerequisites met)
///                                         ↘ Downed (strike lands during LeanIn)
///                                         ↘ Leaving (strike window lapses, guard unharmed)
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
/// CLIMACTIC CHECK-IN
/// ------------------
/// After prerequisites are met (wrists free + weapon acquired + concealed),
/// the NEXT check-in is flagged climactic. At that inspection, if the player
/// is feigning, the guard enters LeanIn instead of routine Gloating — that's
/// where the strike window lives (§8, next session). Climactic detection is
/// driven by the IsClimacticConditionMet() delegate, which the scene wires
/// to whatever tracks weapon/wrist state.
///
/// CAUGHT BRANCH
/// -------------
/// Not feigning at inspection → Caught state → re-cinch sequence (spec §10
/// default: re-cinch/escalate). Reuses the FailureLoopController fade pattern
/// conceptually, but lives here as a lighter version — the VS doesn't carry
/// the full failure loop apparatus (no Chair B, no LevelTimer, no lamp).
/// Re-cinch just: fade to black → add bond escalation → mutter → fade in.
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
		LeanIn,   // forward hook — §8 turnaround, not yet wired
		Downed,   // forward hook — terminal state after strike
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
		"immediately after mutter dismiss.")]
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

	[Header("Mutter Content — Climactic LeanIn (Guard)")]
	[Tooltip("Guard's line when he leans in during the climactic check-in — " +
		"he thinks he's gloating up close; he doesn't know she's armed. " +
		"Speaker: Guard. Plays at the start of LeanIn.")]
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
	// Inspector — Climactic Condition
	// -------------------------------------------------------------------------

	[Header("Climactic Check-In")]
	[Tooltip("When true, the NEXT check-in is climactic — the guard enters LeanIn " +
		"instead of routine Gloating. Wire this to a scene condition that tracks " +
		"whether Cassie is wrists-free AND holding the weapon. " +
		"Leave null to disable climactic check-ins (routine loop only — " +
		"useful during early development of the feign system).")]
	[SerializeField] private UnityEngine.Events.UnityEvent onClimacticInspectionPassed;

	// -------------------------------------------------------------------------
	// Inspector — Debug
	// -------------------------------------------------------------------------

	[Header("Debug")]
	[SerializeField] private bool verboseLogging = true;

	// -------------------------------------------------------------------------
	// Runtime State
	// -------------------------------------------------------------------------

	// How many routine check-ins have completed. Used to index mutter lines.
	private int checkInCount = 0;

	// Whether the next check-in should be treated as climactic.
	// Set externally via FlagNextCheckInAsClimatic() once prerequisites are met.
	private bool nextCheckInIsClimatic = false;

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

		// Start the first offstage phase automatically.
		StartCoroutine(OffstagePhase());
	}

	// -------------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------------

	/// <summary>
	/// Call this when the climactic prerequisites are met (wrists free + weapon
	/// acquired + concealed). The NEXT check-in will enter LeanIn on a pass
	/// instead of routine Gloating.
	/// </summary>
	public void FlagNextCheckInAsClimatic()
	{
		nextCheckInIsClimatic = true;
		Log("Next check-in flagged as climactic.");
	}

	/// <summary>
	/// Hard-stop the guard cycle and move to Downed. Called by StrikeableGuard
	/// when the strike lands (§8, next session).
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
		// has left; there's no reason to hold the pose. This covers the case
		// where the player forgets to toggle off manually.
		if (player != null && player.IsFeigning)
		{
			player.CancelFeign();
		}

		yield return new WaitForSeconds(offstageDuration);
		StartCoroutine(ApproachPhase());
	}

	private IEnumerator ApproachPhase()
	{
		SetState(GuardState.Approaching);
		Log("Guard approaching — feign window OPEN.");

		// Telegraph audio: footsteps start. This is the player's cue to press G.
		if (AudioManager.Instance != null && approachFootstepsClip != null)
		{
			AudioManager.Instance.PlaySFX(approachFootstepsClip, footstepsVolume, 1f);
		}

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
		{
			bool isClimatic = nextCheckInIsClimatic;
			nextCheckInIsClimatic = false; // consume the flag

			if (isClimatic)
			{
				StartCoroutine(LeanInPhase());
			}
			else
			{
				StartCoroutine(GloatPhase());
			}
		}
		else
		{
			StartCoroutine(CaughtPhase());
		}
	}

	private IEnumerator GloatPhase()
	{
		SetState(GuardState.Gloating);

		// Index into mutter arrays. Clamp so extra check-ins repeat the last line.
		int idx = Mathf.Min(checkInCount, routineGloatLines.Length - 1);

		if (MutterSystem.Instance != null)
		{
			if (routineGloatLines.Length > 0)
				MutterSystem.Instance.Play(routineGloatLines[idx], MutterSystem.Speaker.Guard);
			if (routineReactionLines.Length > idx)
				MutterSystem.Instance.Play(routineReactionLines[idx], MutterSystem.Speaker.Cassie);
		}

		// Wait for mutter to finish (or gloat linger, whichever is longer).
		float lingerTimer = 0f;
		while (lingerTimer < gloatLingerDuration ||
			   (MutterSystem.Instance != null && MutterSystem.Instance.IsActive))
		{
			lingerTimer += Time.deltaTime;
			yield return null;
		}

		checkInCount++;
		StartCoroutine(LeavingPhase());
	}

	/// <summary>
	/// Climactic check-in: guard leans in close, unaware Cassie is armed.
	/// This is the strike window. The player has leanInDuration seconds to
	/// press H (Strike). If they don't, the guard straightens and leaves —
	/// back into the routine loop for another attempt.
	///
	/// GuardController does NOT poll for the strike here — PlayerController
	/// calls StrikeableGuard.OnStruck(), which calls OnGuardDowned(), which
	/// calls StopAllCoroutines() on this component. That stops this coroutine
	/// mid-execution, which is the correct behavior: once the guard is down,
	/// none of the remaining LeanIn/Leaving logic should run.
	/// </summary>
	private IEnumerator LeanInPhase()
	{
		SetState(GuardState.LeanIn);
		Log($"Guard LEAN IN — strike window open for {leanInDuration}s.");

		// Guard's close-up gloat line — he thinks he's won.
		if (MutterSystem.Instance != null && !string.IsNullOrEmpty(leanInGuardLine))
		{
			MutterSystem.Instance.Play(leanInGuardLine, MutterSystem.Speaker.Guard);
		}

		// Fire the climactic-passed event for any scene hooks (e.g. enabling
		// the Beg verb if it's been built).
		onClimacticInspectionPassed?.Invoke();

		// Hold the strike window. PlayerController polls StrikeableGuard.CanBeStruck()
		// while H is pressed. If a strike lands, GuardController.OnGuardDowned()
		// fires StopAllCoroutines() — this yield never returns.
		yield return new WaitForSeconds(leanInDuration);

		// Window lapsed — guard missed his chance to notice, Cassie missed hers
		// to strike. He straightens up and leaves. No penalty; try again next
		// climactic check-in (FlagNextCheckInAsClimatic resets externally,
		// or the prerequisites condition re-flags it automatically on the next cycle).
		Log("Strike window lapsed. Guard leaving without incident.");

		// Re-flag climactic so the next check-in is another attempt.
		// The player already has the weapon; they just didn't strike in time.
		nextCheckInIsClimatic = true;

		checkInCount++;
		StartCoroutine(LeavingPhase());
	}

	private IEnumerator LeavingPhase()
	{
		SetState(GuardState.Leaving);
		Log($"Guard leaving — {leavingDuration}s receding audio.");

		if (AudioManager.Instance != null && leaveFootstepsClip != null)
		{
			AudioManager.Instance.PlaySFX(leaveFootstepsClip, footstepsVolume, 1f);
		}

		yield return new WaitForSeconds(leavingDuration);

		// Cycle complete. Return to offstage.
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

		// Restart the offstage phase — same as a normal loop reset.
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

		// Reset bond cut progress — the guard re-ties her.
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

	[ContextMenu("Debug: Flag Next Check-In As Climactic")]
	private void DebugFlagClimatic()
	{
		FlagNextCheckInAsClimatic();
	}

	private void Log(string msg)
	{
		if (verboseLogging) Debug.Log($"[Guard] {msg}");
	}
}
