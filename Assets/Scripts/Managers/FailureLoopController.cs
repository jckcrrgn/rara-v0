using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates L6's failure-loop: when the LevelTimer expires before Cassie
/// escapes, the guard "returns" (offstage, audio-only) and re-binds her with
/// escalated bonds. Player sees a cut-to-black, hears rebinding SFX, hears
/// the guard's verdict mutter (Beat 6a), then fades back in to Cassie's
/// reaction (Beat 6b) and the level reset state.
///
/// SCOPE — v1 (Day 48)
/// -------------------
/// Ships the SPINE of the failure loop:
///   - LevelTimer.OnTimerExpired hookup
///   - Input lockout (PlayerController.enabled = false during the sequence)
///   - Cut-to-black via a CanvasGroup fade (no separate ScreenFade system yet)
///   - Configurable rebind SFX sequence (per-level clip array)
///   - Bond escalation: Wrists → +Ankles → +Elbows → +Knees (cap at attempt 4+)
///   - Cassie repositioned to her current chair (or floorbound if she's already
///     on the floor); chair-B physical swap is DEFERRED to v2
///   - Drawer reset (closed) — wired via UnityEvent so this controller doesn't
///     have a hard dependency on the drawer component
///   - Timer reset (NOT restarted; restart is triggered by lamp-smash or
///     chair-tip on the new attempt, same as attempt 1)
///   - Beat 6a (Guard) mutter over black, then fade-in, then Beat 6b (Cassie)
///     auto-queued via MutterSystem's queue
///
/// DEFERRED to a follow-up session (next pass):
///   - Chair-B physical swap when Chair A is broken (spec §7 row 2)
///   - Lamp-state persistence (smashed lamp does NOT respawn)
///   - Pen-state persistence (pen is gone if picked up before failure)
///   - Chair-shard persistence on the floor across attempts
///   - Beat 6 mutter variation per attempt (1→2 vs 2→3 vs cap) — single
///     mutter pair used for all attempts in v1; spec §6 explicitly marks
///     this as content authoring, not engineering
///
/// SEQUENCE (per spec §7, confirmed Day 48)
/// ---------------------------------------
///   1. LevelTimer.OnTimerExpired fires
///   2. Lock input (player.enabled = false)
///   3. Fade to black (fadeDuration)
///   4. Play rebind SFX sequence (clips fire in order, black holds throughout)
///   5. Mutate state: bonds escalate, position reset, drawer event fires,
///      timer resets
///   6. Beat 6a (Guard) mutter fires — diegetic, over black
///   7. Player dismisses 6a
///   8. Fade in from black (fadeDuration)
///   9. Beat 6b (Cassie) mutter fires — auto-queued behind 6a
///  10. Player dismisses 6b
///  11. Release input — timer will re-trigger via lamp/chair-tip on this attempt
///
/// ORDERING NOTE on step 6 vs step 8:
///   Beat 6a fires while still black. This is intentional — the guard delivers
///   his verdict line over the black, then we fade in TO Cassie's reaction.
///   The MutterSystem queue means we Play() both back-to-back here; the
///   visibility timing is controlled by when we start the fade-in coroutine
///   relative to the first dismiss. See PlaySequence() for the gating mechanism.
///
/// SINGLETON
/// ---------
/// Same pattern as LevelTimer and MutterSystem. Not every level has a failure
/// loop (L1–L5 don't). Wire this component into the scenes that do.
/// </summary>
public class FailureLoopController : MonoBehaviour
{
	public static FailureLoopController Instance { get; private set; }

	[Header("Fade Overlay")]
	[Tooltip("CanvasGroup on a full-screen black Image. Alpha 0 = transparent " +
		"(gameplay visible), alpha 1 = fully black. Inspector-authored child " +
		"of the scene Canvas. If null, the loop still runs but without visual " +
		"fade — useful for testing.")]
	[SerializeField] private CanvasGroup fadeOverlay;

	[Tooltip("Seconds for the fade-to-black ramp (and the fade-in ramp). 0.5s " +
		"is the spec default — fast enough not to feel sluggish, slow enough " +
		"that the player registers it as a deliberate cut.")]
	[SerializeField] private float fadeDuration = 0.5f;

	[Header("Rebind SFX")]
	[Tooltip("Ordered sequence of audio clips that play during the cut-to-black, " +
		"BEFORE Beat 6a fires. Plays in array order, one after the next. " +
		"L6 default: rope cinch sequence. Future levels can sub in duct tape, " +
		"handcuffs, Cassie grunts, or mixed sequences. Leave empty for a silent " +
		"rebind (not recommended — the audio IS the moment).")]
	[SerializeField] private AudioClip[] rebindSfxSequence;

	[Tooltip("Volume for rebind SFX clips. 1.0 is a reasonable default since " +
		"these are foregrounded moments — they're meant to be heard.")]
	[Range(0f, 1f)]
	[SerializeField] private float rebindSfxVolume = 1.0f;

	[Tooltip("Seconds of silence between consecutive rebind clips. Small gap " +
		"keeps the sequence from feeling rushed; 0 plays them back-to-back. " +
		"0.1s reads as 'continuous action with breath between movements.'")]
	[SerializeField] private float rebindSfxGap = 0.1f;

	[Tooltip("Seconds of held black after the rebind SFX sequence completes, " +
		"before Beat 6a fires. Lets the audio land before the guard speaks.")]
	[SerializeField] private float postSfxHoldDuration = 0.3f;

	[Header("Mutter Content")]
	[Tooltip("Guard's verdict line. Fires while screen is still black, after " +
		"the rebind SFX. Beat 6a per the L6 mutter chain. Speaker is Guard " +
		"(diegetic, routed through Guard's SpeakerConfig.audioSourceOverride).")]
	[TextArea(2, 4)]
	[SerializeField] private string beat6aGuard = "That ought to hold you.";

	[Tooltip("Cassie's reaction line. Auto-queued behind 6a via MutterSystem's " +
		"queue; visible after fade-in. Beat 6b per the L6 mutter chain. " +
		"v1 uses a single line for all attempts; spec §6 calls out per-attempt " +
		"variation as future content work.")]
	[TextArea(2, 4)]
	[SerializeField] private string beat6bCassie = "My ELBOWS? Really? Good thing I stretched.";

	[Header("Position Reset")]
	[Tooltip("Transform marking where Cassie respawns at the start of each " +
		"failed-attempt restart. Inspector-authored, typically the center of " +
		"the room. The chair (if intact) should be at this position too — for " +
		"v1, Cassie just snaps here regardless of chair state.")]
	[SerializeField] private Transform respawnPoint;

	[Header("State Reset Hooks")]
	[Tooltip("UnityEvent fired during state mutation, after bonds escalate and " +
		"position resets but before the timer resets. Wire scene-specific reset " +
		"behavior here — e.g. NightstandDrawer.Close(), future hooks for chair-B " +
		"swap, lamp persistence checks, etc. Keeps this controller free of hard " +
		"dependencies on scene-specific components.")]
	[SerializeField] private UnityEngine.Events.UnityEvent onStateReset;

	[Header("Debug")]
	[Tooltip("If true, logs each step of the failure-loop sequence. Useful while " +
		"the system is new; can be turned off once it's stable.")]
	[SerializeField] private bool verboseLogging = true;

	// Attempt counter. Starts at 1 (the player's first attempt is attempt 1).
	// Incremented on each failure-loop trigger. Used to drive bond escalation.
	private int currentAttempt = 1;

	// True while the failure-loop sequence is running. Re-entry guard: if the
	// timer somehow fires again mid-sequence (it shouldn't, but defense in
	// depth), we ignore the second trigger.
	private bool isRunning;

	// Cached reference to the player. Resolved lazily on first failure trigger
	// so we don't depend on scene load order.
	private PlayerController playerCache;

	// Escalation ladder: index = attempt number (1-based, but we use 0-indexed
	// here for array convenience). attempt 1 → 2 transition adds entry 1's bonds.
	// attempt 2 → 3 transition adds entry 2's bonds. Etc.
	//
	// Per Day 48 design revision:
	//   attempt 1 starts with: Wrists
	//   attempt 2 starts with: Wrists + Ankles
	//   attempt 3 starts with: Wrists + Ankles + Elbows
	//   attempt 4+ starts with: Wrists + Ankles + Elbows + Knees (cap)
	//
	// So the bond ADDED on each failure-loop entry is:
	//   first failure (1→2): Ankles
	//   second failure (2→3): Elbows
	//   third failure (3→4): Knees
	//   fourth+ failure (4→4+): nothing (cap)
	private static readonly BoundLimbs[] EscalationAdditions = new BoundLimbs[]
	{
		BoundLimbs.Ankles,  // applied on the 1→2 failure transition
		BoundLimbs.Elbows,  // applied on the 2→3 failure transition
		BoundLimbs.Knees,   // applied on the 3→4 failure transition
		// 4+ adds nothing — cap reached.
	};

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogWarning("Multiple FailureLoopControllers in scene; destroying duplicate.");
			Destroy(gameObject);
			return;
		}
		Instance = this;

		// Initialize fade overlay to fully transparent so it doesn't cover the
		// scene at start. The overlay's GameObject should be active in the
		// scene; we just zero the alpha.
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
		// Subscribe to LevelTimer's OnTimerExpired event. We do this in Start
		// (not Awake) so LevelTimer has had a chance to initialize. If no
		// LevelTimer exists in the scene, the controller silently does nothing
		// — which is correct for levels that don't have failure loops.
		if (LevelTimer.Instance != null)
		{
			LevelTimer.Instance.OnTimerExpired.AddListener(OnTimerExpired);
		}
		else
		{
			Debug.LogWarning("FailureLoopController: no LevelTimer.Instance found in scene. " +
				"Failure loop will not trigger.");
		}
	}

	/// <summary>
	/// LevelTimer.OnTimerExpired callback. Starts the failure-loop sequence
	/// unless one is already running.
	/// </summary>
	private void OnTimerExpired()
	{
		if (isRunning)
		{
			Debug.LogWarning("FailureLoopController: OnTimerExpired fired while loop already running. Ignoring.");
			return;
		}

		StartCoroutine(PlaySequence());
	}

	/// <summary>
	/// The main failure-loop sequence. Walks through fade → SFX → state mutation
	/// → mutters → fade in. Each phase is gated on the previous one completing,
	/// so the player always experiences them in the spec'd order.
	/// </summary>
	private IEnumerator PlaySequence()
	{
		isRunning = true;
		Log($"=== Failure loop START. Attempt {currentAttempt} → {currentAttempt + 1}. ===");

		// --- Step 0: Clear any active mutter ---
		// If the 50% pressure mutter (or any other) is still showing when the
		// timer expires, it would otherwise hold over the cut-to-black and the
		// player's first dismiss would advance past IT, not Beat 6a. Force-
		// dismiss before we queue our own mutters so we start from a clean
		// state. Narratively: the failure interrupts whatever was being said.
		if (MutterSystem.Instance != null)
		{
			MutterSystem.Instance.ForceDismissAndClear();
		}

		// --- Step 1: Lock player input ---
		// Same pattern LevelManager uses for level-complete. Disabling the
		// PlayerController halts movement, struggle, kick, pickup, etc.
		PlayerController player = GetPlayer();
		if (player != null)
		{
			player.enabled = false;
		}

		// --- Step 2: Fade to black ---
		yield return FadeOverlay(0f, 1f, fadeDuration);

		// --- Step 3: Rebind SFX sequence ---
		// Plays clips in order, with rebindSfxGap between them. Black holds
		// throughout — no fade-in until the SFX is done AND Beat 6a is dismissed.
		yield return PlayRebindSfxSequence();

		// Small post-SFX hold so the audio lands before the guard speaks.
		yield return new WaitForSeconds(postSfxHoldDuration);

		// --- Step 4: State mutation ---
		// Bond escalation, position reset, drawer reset (via UnityEvent), timer reset.
		// Done while black, so the player doesn't see Cassie teleport.
		MutateState();

		// --- Step 5: Beat 6a (Guard) over black ---
		// Queue both 6a and 6b right now. MutterSystem will play 6a immediately,
		// queue 6b behind it. We'll fade in after the player dismisses 6a — which
		// we detect by polling IsActive transitioning to "queued only" (i.e. 6a
		// dismissed, 6b not yet started).
		//
		// Implementation note: MutterSystem.IsActive returns true while a mutter
		// is revealing/waiting OR queued. To know when 6a specifically has been
		// dismissed and 6b is the next in line, we'd need richer state from
		// MutterSystem. For v1, we just wait for the first dismiss (by tracking
		// WasJustDismissed) and use that as the cue to fade in. Beat 6b will then
		// drain from the queue automatically and appear over the faded-in scene.
		if (MutterSystem.Instance != null)
		{
			MutterSystem.Instance.Play(beat6aGuard, MutterSystem.Speaker.Guard);
			MutterSystem.Instance.Play(beat6bCassie, MutterSystem.Speaker.Cassie);
		}
		else
		{
			Debug.LogWarning("FailureLoopController: no MutterSystem.Instance. Skipping mutters.");
		}

		// --- Step 6: Wait for first dismiss (Beat 6a closed) ---
		// We wait one frame to let MutterSystem.Play register, then wait for
		// WasJustDismissed to pulse. That pulse marks the boundary between 6a
		// and 6b — perfect cue to begin fade-in.
		yield return null; // give MutterSystem a frame to enter active state
		yield return new WaitUntil(() => MutterSystem.Instance == null
			|| MutterSystem.Instance.WasJustDismissed);

		// --- Step 7: Fade in from black ---
		// 6b is now revealing (or about to) behind the fading-in overlay. By the
		// time the fade completes, the player sees Cassie's line over the scene.
		yield return FadeOverlay(1f, 0f, fadeDuration);

		// --- Step 8: Wait for 6b to finish ---
		// IsActive stays true until 6b is dismissed and the queue is drained.
		// Once it goes false, we can release input.
		yield return new WaitWhile(() => MutterSystem.Instance != null
			&& MutterSystem.Instance.IsActive);

		// --- Step 9: Release input ---
		if (player != null)
		{
			player.enabled = true;
		}

		currentAttempt++;
		isRunning = false;
		Log($"=== Failure loop END. Now on attempt {currentAttempt}. ===");
	}

	/// <summary>
	/// Linear interpolation of fadeOverlay.alpha from `from` to `to` over
	/// `duration` seconds. No-op if no overlay is wired.
	/// </summary>
	private IEnumerator FadeOverlay(float from, float to, float duration)
	{
		if (fadeOverlay == null)
		{
			yield break;
		}

		// blocksRaycasts at full black to be safe — if any UI is interactive
		// underneath, the player can't accidentally click through during the
		// blackout. Toggled off again on fade-out completion.
		if (to > from)
		{
			fadeOverlay.blocksRaycasts = true;
		}

		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			fadeOverlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
			yield return null;
		}
		fadeOverlay.alpha = to;

		if (to <= 0f)
		{
			fadeOverlay.blocksRaycasts = false;
		}
	}

	/// <summary>
	/// Play rebindSfxSequence in order, with rebindSfxGap silence between each
	/// clip. Routes through AudioManager (2D channel) — these are foregrounded
	/// moments, not diegetic. If the array is empty, returns immediately.
	/// </summary>
	private IEnumerator PlayRebindSfxSequence()
	{
		if (rebindSfxSequence == null || rebindSfxSequence.Length == 0)
		{
			Log("PlayRebindSfxSequence: no clips configured, skipping.");
			yield break;
		}

		for (int i = 0; i < rebindSfxSequence.Length; i++)
		{
			AudioClip clip = rebindSfxSequence[i];
			if (clip == null) continue;

			if (AudioManager.Instance != null)
			{
				AudioManager.Instance.PlaySFX(clip, rebindSfxVolume, 1f);
			}

			// Wait for the clip to finish before starting the next one.
			yield return new WaitForSeconds(clip.length);

			// Brief gap between clips (except after the last one — the
			// postSfxHoldDuration handles the trailing hold).
			if (i < rebindSfxSequence.Length - 1)
			{
				yield return new WaitForSeconds(rebindSfxGap);
			}
		}
	}

	/// <summary>
	/// Apply all state changes that happen during the cut-to-black:
	/// bond escalation, Cassie's position reset, scene-specific resets via
	/// UnityEvent, timer reset.
	/// </summary>
	private void MutateState()
	{
		PlayerController player = GetPlayer();
		if (player == null)
		{
			Debug.LogWarning("FailureLoopController.MutateState: no PlayerController found.");
			return;
		}

		// --- Bond escalation ---
		// EscalationAdditions is 0-indexed. attempt 1 → 2 uses index 0.
		// attempt 2 → 3 uses index 1. attempt 3 → 4 uses index 2. attempt 4+
		// is past the array — no new bond, cap reached.
		int additionIndex = currentAttempt - 1;
		if (additionIndex < EscalationAdditions.Length)
		{
			BoundLimbs toAdd = EscalationAdditions[additionIndex];
			RestraintBase r = player.CurrentRestraint;
			if (r != null)
			{
				// Compose the new bond state by adding the escalation bit to
				// whatever's currently set. SetBoundLimbs handles the
				// clear-and-reapply so any event side effects fire correctly.
				BoundLimbs newBonds = r.BoundLimbs | toAdd;
				r.SetBoundLimbs(newBonds);
				Log($"Bond escalation: added {toAdd}. New BoundLimbs = {r.BoundLimbs}");
			}
		}
		else
		{
			Log($"Bond cap reached (attempt {currentAttempt}); no new bond added.");
		}

		// --- Position reset ---
		// Snap Cassie back to the respawnPoint. The chair (if intact) is
		// authored to be at this position; if she's on the floor, she snaps
		// here too. v2 will handle chair-B swap-in if Chair A is broken.
		if (respawnPoint != null)
		{
			player.transform.position = respawnPoint.position;
			player.transform.rotation = respawnPoint.rotation;
			Log($"Position reset to {respawnPoint.position}.");
		}
		else
		{
			Debug.LogWarning("FailureLoopController: no respawnPoint set. Cassie will not be repositioned.");
		}

		// --- Scene-specific resets via UnityEvent ---
		// Wire NightstandDrawer.Close(), future chair-B swap, etc. here.
		if (onStateReset != null)
		{
			onStateReset.Invoke();
			Log("onStateReset UnityEvent invoked.");
		}

		// --- Timer reset ---
		// Reset (not start) — the timer re-arms via lamp-smash or chair-tip on
		// the new attempt, same as attempt 1.
		if (LevelTimer.Instance != null)
		{
			LevelTimer.Instance.ResetTimer();
			Log("LevelTimer reset.");
		}
	}

	/// <summary>
	/// Lazy player lookup. Caches the result so we don't repeatedly Find.
	/// </summary>
	private PlayerController GetPlayer()
	{
		if (playerCache == null)
		{
			playerCache = FindFirstObjectByType<PlayerController>();
		}
		return playerCache;
	}

	private void Log(string msg)
	{
		if (verboseLogging) Debug.Log($"[FailureLoop] {msg}");
	}
}
