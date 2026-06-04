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
/// SCOPE — v2 (Day 48 session 2)
/// ------------------------------
/// Ships v1 spine PLUS chair management:
///   - Chair B swap: if Chair A is broken at failure time, hide Chair B from
///     its stored position and re-engage the ChairRestraint, returning Cassie
///     to a chair. Single-use per level (chairBSwapped guards repeated swaps).
///   - Three-case state machine in HandleChairManagement: in-chair (no swap),
///     on-floor-with-chair-B-available (swap), on-floor-with-chair-B-used
///     (stay floorbound).
///
/// DEFERRED to a follow-up session (still):
///   - Lamp-state persistence (smashed lamp does NOT respawn)
///   - Pen-state persistence (pen is gone if picked up before failure)
///   - Chair-shard persistence — already partially handled by yesterday's
///     scene-rooted shard spawning; needs verification under the new swap path
///   - Beat 6 mutter variation per attempt (single mutter pair for all attempts)
///
/// SEQUENCE (per spec §7)
/// ---------------------
///   1. LevelTimer.OnTimerExpired fires
///   2. Force-dismiss any active mutter (clears 50% pressure mutter etc.)
///   3. Lock input (player.enabled = false)
///   4. Fade to black (fadeDuration)
///   5. Play rebind SFX sequence (clips fire in order, black holds throughout)
///   6. Mutate state: chair management → bond escalation → position reset →
///      onStateReset UnityEvent → timer reset
///   7. Beat 6a (Guard) mutter fires — diegetic, over black
///   8. Player dismisses 6a
///   9. Fade in from black (fadeDuration)
///  10. Beat 6b (Cassie) mutter fires — auto-queued behind 6a
///  11. Player dismisses 6b
///  12. Release input — timer will re-trigger via lamp/chair-tip on this attempt
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
	[Tooltip("Guard's verdict lines, indexed per attempt (index 0 = first failure). " +
		"Fires while screen is still black, after the rebind SFX. Beat 6a. Speaker is " +
		"Guard (diegetic). Index clamps to the last entry, so attempt 4+ all use the cap line.")]
	[TextArea(2, 4)]
	[SerializeField]
	private string[] beat6aGuard =
	{
		"That ought to hold you.",                 // attempt 1 (adds Ankles)
		"Slippery, aren't you. Let's fix that.",   // attempt 2 (adds Elbows)
		"You're persistent, I'll give you that.",  // attempt 3 (adds Knees)
		"I'm out of rope. Just... stay.",          // attempt 4+ (cap, no new bond)
	};

	[Tooltip("Cassie's reaction lines, indexed per attempt — each reacts to the bond " +
		"just added (Ankles, Elbows, Knees, then cap). Auto-queued behind 6a; visible " +
		"after fade-in. Beat 6b. Index clamps to the last entry for attempt 4+.")]
	[TextArea(2, 4)]
	[SerializeField]
	private string[] beat6bCassie =
	{
		"My ankles too, huh? Not like I was walking anywhere to begin with...", // attempt 1
		"My ELBOWS? Really? Good thing I stretched.",                           // attempt 2
		"Knees now, huh? Finally, a challenge.",                                // attempt 3
		"What? No more tricks?",                                                // attempt 4+ (cap)
	};

	[Header("Position Reset")]
	[Tooltip("Transform marking where Cassie respawns at the start of each " +
		"failed-attempt restart. Inspector-authored, typically the center of " +
		"the room.")]
	[SerializeField] private Transform respawnPoint;

	[Header("Chair Management")]
	[Tooltip("The ChairRestraint component on the Player prefab — i.e. " +
		"\"Chair A\" in the spec's terminology. There's no separate Chair A " +
		"scene object; the chair is conceptually part of the player. Required " +
		"if this level has a chair-tip path that can lead to floorbound state " +
		"on failure. Drag the Player's ChairRestraint component here.")]
	[SerializeField] private ChairRestraint playerChairRestraint;

	[Tooltip("Chair B scene object — the spare chair stored against the wall, " +
		"swapped in on failure if Chair A is broken. Per spec §7, this is " +
		"room-consistency insurance: without it, the room would either " +
		"contradict itself or force the player into a floorbound attempt 2 " +
		"that the level isn't designed for as the default. Hidden when " +
		"swapped in (Architecture A from the design discussion — the player's " +
		"chair model handles the at-center visual; Chair B at the wall just " +
		"disappears, reading as \"the guard moved it\"). Leave null on levels " +
		"that don't need a swap (Cassie permanently floorbound after first " +
		"break is fine for those).")]
	[SerializeField] private GameObject chairBObject;

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

	// Tracks whether Chair B has already been swapped in on a prior failure.
	// Once true, Chair B is "used up" — subsequent failures that find Cassie
	// floorbound will keep her floorbound (no chair to swap in). This matches
	// spec §7 row 3 ("or floorbound if both broken").
	private bool chairBSwapped;

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
			// Same index the bond escalation uses (additionIndex = currentAttempt - 1),
			// clamped so attempt 4+ all land on the cap line. Locks each line to the
			// bond just added. Both arrays are the same length; guard's drives the clamp.
			int beatIdx = Mathf.Min(currentAttempt - 1, beat6aGuard.Length - 1);
			MutterSystem.Instance.Play(beat6aGuard[beatIdx], MutterSystem.Speaker.Guard);
			MutterSystem.Instance.Play(beat6bCassie[beatIdx], MutterSystem.Speaker.Cassie);
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
	/// chair management (un-break or Chair-B swap if applicable), bond
	/// escalation, position reset, scene-specific resets via UnityEvent,
	/// timer reset.
	///
	/// ORDERING: chair management runs BEFORE bond escalation, because if we're
	/// swapping back into a chair, the escalation should land on the chair's
	/// RestraintBase, not the floor's. Position reset runs after both, so we
	/// snap Cassie into place once she's in the right restraint.
	/// </summary>
	private void MutateState()
	{
		PlayerController player = GetPlayer();
		if (player == null)
		{
			Debug.LogWarning("FailureLoopController.MutateState: no PlayerController found.");
			return;
		}

		// --- Chair management ---
		// Three cases, decided by current restraint and chair-B availability:
		//
		//   1. Cassie is in ChairRestraint → she didn't tip her chair this
		//      attempt. Stay in chair, bond escalation will land on it.
		//      No chair swap needed.
		//
		//   2. Cassie is in FloorRestraint AND Chair B is available → her
		//      chair broke this attempt, but Chair B is still usable.
		//      Hide Chair B (the guard "drags" it to center, which mechanically
		//      means it disappears from the wall — the player's chair model
		//      handles the at-center visual), un-break the ChairRestraint, and
		//      swap player back to chair. Bond escalation will land on chair.
		//
		//   3. Cassie is in FloorRestraint AND Chair B is used → both chairs
		//      have been broken across prior attempts. Cassie stays floorbound.
		//      Bond escalation lands on floor restraint.
		//
		// Levels without a wired playerChairRestraint or chairBObject default
		// to "no swap available" — case 1 if in chair, case 3 if on floor.
		HandleChairManagement(player);

		// --- Confiscate held item ---
		// The guard takes back any tool Cassie picked up before failing (e.g.
		// the L6 pen). Per spec §7: "pen state — gone if picked up before
		// failure." This is the code that enforces it. Confiscation is
		// one-way: the held item's GameObject is disabled, not destroyed,
		// and the player's heldItem reference is cleared. Chair shards
		// and lamp shards are NOT held items at this point (they're
		// scene-rooted floor debris until the player picks one up) so
		// this only affects whatever's literally in Cassie's hand.
		player.ConfiscateHeldItem();

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
				Log($"Bond escalation: added {toAdd}. New BoundLimbs = {r.BoundLimbs} " +
					$"(on {r.GetType().Name})");
			}
		}
		else
		{
			Log($"Bond cap reached (attempt {currentAttempt}); no new bond added.");
		}
		// --- Reset cut progress ---
		// The guard re-ties her: any partial cut from the failed attempt is gone.
		// Runs on EVERY failure, INCLUDING the cap (attempt 4+) where no new limb
		// is added — she still loses her progress. Placing it outside the
		// escalation if/else is deliberate; inside the `if`, cap attempts would
		// keep their progress and the bug would survive at max bonds.
		player.ResetBondProgress();

		// --- Position reset ---
		// Snap Cassie back to the respawnPoint. Done AFTER chair management so
		// the player is in the correct restraint first; some restraints' OnEnter
		// reads transform state to set up internal references (steeringYaw, etc.).
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

		// --- Floor posture reset (stay-floorbound case) ---
		// If Cassie stayed floorbound (case 3 — both chairs broken), she never
		// exited FloorRestraint, so its OnEnter — which resets her to a prone,
		// head-first bind — did NOT fire. Reset it explicitly here, AFTER the
		// position snap, so her heading re-derives from the respawn point and she
		// comes back facedown rather than resuming whatever tangle she failed in.
		// The chair cases (1 and 2) re-entered a restraint via SetRestraint, so
		// their OnEnter already handled posture; this is the one gap.
		if (player.CurrentRestraint is FloorRestraint floorRestraint)
		{
			floorRestraint.ResetPosture(player);
			Log("Floor posture reset to prone (stay-floorbound re-bind).");
		}

		// --- Scene-specific resets via UnityEvent ---
		// Wire NightstandDrawer.Close() and other scene-specific resets here.
		// Per spec §7 persistence rules, lamp/pen/shard state PERSISTS across
		// attempts — so these resets should be careful to only touch things
		// that should reset (drawer, etc.).
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
	/// Decide and apply the chair-state change for this failure attempt.
	/// See the three cases documented in MutateState. Idempotent: safe to
	/// call when nothing needs to change (case 1, case 3 with no chairBObject
	/// wired).
	/// </summary>
	private void HandleChairManagement(PlayerController player)
	{
		RestraintBase current = player.CurrentRestraint;

		// Case 1: still in chair. Nothing to do here — bond escalation will
		// land on the chair restraint and that's correct.
		if (current is ChairRestraint)
		{
			Log("Chair management: still in chair, no swap needed.");
			return;
		}

		// Case 2 or 3: on the floor. Decide based on Chair B availability.
		if (current is FloorRestraint)
		{
			bool canSwap = !chairBSwapped
				&& playerChairRestraint != null
				&& chairBObject != null;

			if (canSwap)
			{
				// Case 2: Chair B swap.
				Log("Chair management: swapping Chair B in (Chair A was broken this attempt).");

				// Hide Chair B from its stored position. The visual is "the guard
				// dragged it to center" — the player's chair model handles the
				// at-center appearance, so we just need Chair B to disappear from
				// the wall. SetActive(false) is cleaner than physically moving it
				// because moving introduces risk of overlapping the player's chair
				// geometry.
				chairBObject.SetActive(false);

				// Un-break the ChairRestraint so it accepts the player again.
				playerChairRestraint.ResetBrokenState();

				// Hand the player back to ChairRestraint. SetRestraint fires
				// OnExit on the floor restraint and OnEnter on the chair, which
				// is the correct lifecycle.
				player.SetRestraint(playerChairRestraint);

				chairBSwapped = true;

				Log("Chair management: Chair B swap complete. Player is now in ChairRestraint.");
			}
			else
			{
				// Case 3: stay floorbound. Either chairBSwapped is true (both
				// chairs used) or the level isn't configured with a swap (chairs
				// not wired). Either way, bond escalation lands on the floor.
				if (chairBSwapped)
				{
					Log("Chair management: both chairs used. Cassie stays floorbound.");
				}
				else
				{
					Log("Chair management: no chair swap configured. Cassie stays floorbound.");
				}
			}
			return;
		}

		// Defensive: an unrecognized restraint type. Shouldn't happen in v0,
		// but log loudly if it does so the misconfiguration surfaces fast.
		Debug.LogWarning($"FailureLoopController.HandleChairManagement: unrecognized " +
			$"restraint type {current?.GetType().Name ?? "null"}. No chair management applied.");
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
