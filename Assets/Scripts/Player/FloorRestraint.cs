using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Floor restraint: player is bound on the floor (duct tape, hands tied, etc.).
///
/// Movement has two modes, toggled by C:
///   - INCH (default): hold W to lunge headfirst (prone, on belly), one inch
///     at a time with alternating shoulder lead. Releasing W mid-cycle lets
///     the current inch finish; no new one starts.
///   - SCOOT: hold W to push feet-first (supine, on back). Same cadence as
///     inch but symmetric (no shoulder lead).
///
/// Hold-W rather than tap-W: tap-mashing was tedious on L4 (long stretch of
/// floor between spawn and door). Hold-W preserves the per-inch cadence —
/// the inchworm rhythm is doing real character work — but takes the carpal
/// tunnel out of it. Each inch is still a discrete cycle: lunge, settle,
/// brief inter-cycle pause, then if W is still held, another cycle starts
/// with the opposite shoulder lead. Speed is identical to perfect tap-mashing.
///
/// Travel direction = +transform.forward in both modes — the C-toggle only
/// flips visual orientation, not travel direction.
///
/// Why "visual flip only": the player is steering the detective toward something
/// (a door, a tool, an enemy). Pressing W should consistently push toward that
/// thing. Toggling C is a re-orientation of the body around the same heading —
/// head and feet swap visual positions, but "where the detective is going" stays
/// the same. So W travels along +forward in both modes, and the C-toggle animates
/// a 180° visual spin (and a Z-flip belly-up/belly-down) that's purely cosmetic.
///
/// Visual flip is animated on a separate "flipOffset" applied on top of steeringYaw,
/// so the underlying steering vector doesn't change. The flip takes flipDuration
/// seconds, during which W input is locked (so we can't move mid-spin and end up
/// with travel/visual desync).
///
/// On a cube, only the Y portion of the flip is observable (cubes are Y-asymmetric
/// in steering yaw if you've rotated with A/D); the Z-flip is invisible because
/// cubes are Z-symmetric. The Z animation is wired anyway so when the character
/// model lands, the belly-down → belly-up transition is already in place.
///
/// Roll (Shift+A/D) is reserved as a third mode — lateral, fast, noisy.
/// Not implemented yet (see ideas.md Day 20).
///
/// Kick: legs are bound but mobile enough to deliver a reduced-force kick —
/// but ONLY in scoot mode. Prone (inch) kicks are suppressed entirely:
/// anatomically you can't generate force kicking from your stomach with bound
/// legs, and design-wise this couples the verb to the mode (inch = position,
/// scoot = apply force). PlayerController still plays the effort grunt on a
/// suppressed kick, so the player gets feedback that they tried — the absence
/// of impact is the cue to try scoot.
/// </summary>
public class FloorRestraint : RestraintBase
{
	[Header("Inch/Scoot Movement")]
	[Tooltip("How far the player travels per inch or scoot.")]
	[SerializeField] private float moveDistance = 0.4f;
	[Tooltip("Duration of the lunge phase (push along +forward). Holding W " +
		"chains cycles back-to-back; lungeDuration sets how fast each one is.")]
	[SerializeField] private float lungeDuration = 0.25f;
	[Tooltip("Duration of the settle phase (pause + body untwists).")]
	[SerializeField] private float settleDuration = 0.35f;
	[Tooltip("Pause between back-to-back cycles when W is held. Preserves the " +
		"discrete-rep cadence — without it, hold-W reads as smooth gliding " +
		"instead of inching. Small value: 0.05-0.15 sec. Set 0 for max speed.")]
	[SerializeField] private float interCycleDelay = 0.08f;

	[Header("Inch Shoulder Lead")]
	[Tooltip("Degrees of Y-rotation tilt during an inch lunge. Alternates sign per inch — " +
		"reads as alternating shoulder lead, like an actual inchworm. " +
		"Inch is asymmetric (one shoulder, then the other); scoot is symmetric (both legs " +
		"push together) so this doesn't apply to scoot mode.")]
	[SerializeField] private float shoulderLeadAngle = 12f;

	[Header("Rotation (steering)")]
	[SerializeField] private float rotationSpeed = 80f;

	[Header("Mode Toggle")]
	[Tooltip("Key to toggle between inch (headfirst, prone) and scoot (feet-first, supine). " +
		"Visually flips the body 180° on Y (head/feet swap) and 180° on Z (belly up/down). " +
		"World-space travel direction does NOT change — W still moves along +forward in both modes. " +
		"Mode persists across the FloorRestraint session — re-entry preserves it.")]
	[SerializeField] private KeyCode modeToggleKey = KeyCode.C;
	[Tooltip("Duration of the visual flip animation. W input is locked during the flip.")]
	[SerializeField] private float flipDuration = 2f;

	[Header("Struggle Tuning")]
	[Tooltip("Floor-bound struggle uses the whole body — slightly more effective. 1.2 = 20% bonus.")]
	[SerializeField] private float struggleBonus = 1.2f;

	[Header("Kick Tuning")]
	[Tooltip("Kick force scalar while floor-bound AND in scoot mode. 0.5 = half the force of a free-legged kick. " +
		"Means floor-bound players need ~2x the reps to break the same Kickable. " +
		"Inch (prone) mode returns 0 — kick is suppressed entirely until the player flips to scoot.")]
	[SerializeField] private float kickModifier = 0.5f;

	// --- Internal state ---
	private float steeringYaw;
	private float twistOffset;
	private float visualYawOffset;   // 0 in inch, 180 in scoot. Animated during flip.
	private float visualRoll;        // 0 in inch (belly-down), 180 in scoot (belly-up). Animated during flip.
	private bool isMoving = false;
	private bool isFlipping = false;
	private bool nextLeadIsRight = true;
	private bool isScootMode = false;

	// Inch cycle, scoot cycle, and flip cycle all commit the body to a motion.
	// Steering (A/D) does NOT — that's aim adjustment, not body-committing motion.
	public override bool IsBusy => isMoving || isFlipping;

	public override void OnEnter(PlayerController player)
	{
		steeringYaw = player.transform.eulerAngles.y;
		twistOffset = 0f;
		nextLeadIsRight = true;
		// isScootMode persists across re-entry (see class comment). Sync visual offsets
		// to whatever mode we're currently in so the cube doesn't snap.
		visualYawOffset = isScootMode ? 180f : 0f;
		visualRoll = isScootMode ? 180f : 0f;
	}

	public override void HandleMovementInput(PlayerController player)
	{
		// Steering still works during a flip — the detective can technically course-correct
		// her heading mid-spin. Feels natural and prevents input-locked frustration.
		float rotateInput = Input.GetAxis("Horizontal");
		steeringYaw += rotateInput * rotationSpeed * Time.deltaTime;

		if (Input.GetKeyDown(modeToggleKey) && !player.IsBusy)
		{
			player.StartCoroutine(FlipCycle(player));
		}

		if (Input.GetKey(KeyCode.W) && !player.IsBusy)
		{
			player.StartCoroutine(MoveCycle(player));
		}

		// Apply: steering yaw + per-inch twist + visual flip offset on Y, plus Z roll for belly orientation.
		player.transform.rotation = Quaternion.Euler(visualRoll, steeringYaw + twistOffset + visualYawOffset, 0f);
	}

	/// <summary>
	/// Visual flip: animate Y by 180° and Z by 180° over flipDuration.
	/// Travel direction (steeringYaw) is untouched — only the visual offsets change.
	/// W input is locked during the flip; steering remains active so the player can
	/// keep adjusting heading mid-spin if they want.
	///
	/// Both Y and Z are animated together. On a cube the Z portion is invisible
	/// (Z-symmetric), but it's wired so the character-model transition is in place.
	///
	/// Hint refresh: fire RaiseHintsChanged at the start of the flip so the UI
	/// updates the W label ("Inch (hold)" → "Scoot (hold)") and the F-kick
	/// conditional state immediately when the player commits to the toggle,
	/// rather than waiting for the animation to finish. Feels more responsive
	/// and there's no race — the new mode is committed before the animation runs.
	/// </summary>
	private IEnumerator FlipCycle(PlayerController player)
	{
		isFlipping = true;
		isScootMode = !isScootMode;
		RaiseHintsChanged();

		// Debug.Log scaffolding — replace with mutter line / UI cue once those exist.
		Debug.Log(isScootMode ? "Floor mode: SCOOT (feet-first)" : "Floor mode: INCH (headfirst)");

		// Disable physics during the flip so the cube can rotate cleanly without
		// the collider fighting the floor. Re-enabled on exit. When the character
		// model lands this can probably go away — a humanoid rolling on its back
		// won't have the same floor-collision issue a cube has.
		bool wasKinematic = player.Rb.isKinematic;
		player.Rb.isKinematic = true;

		float startYaw = visualYawOffset;
		float endYaw = isScootMode ? 180f : 0f;
		float startRoll = visualRoll;
		float endRoll = isScootMode ? 180f : 0f;

		float elapsed = 0f;
		while (elapsed < flipDuration)
		{
			float t = elapsed / flipDuration;
			float eased = t * t * (3f - 2f * t); // smoothstep
			visualYawOffset = Mathf.LerpAngle(startYaw, endYaw, eased);
			visualRoll = Mathf.LerpAngle(startRoll, endRoll, eased);
			elapsed += Time.deltaTime;
			yield return null;
		}

		visualYawOffset = endYaw;
		visualRoll = endRoll;

		player.Rb.isKinematic = wasKinematic;
		isFlipping = false;
	}

	/// <summary>
	/// One movement cycle. Always travels along +transform.forward — the C-toggle
	/// changes only the visual offset, not the steering yaw, so the world-space
	/// direction of W is consistent across both modes.
	/// Inch applies an alternating shoulder-lead twist; scoot is symmetric (both legs
	/// pushing together) so no twist is applied.
	///
	/// Hold-W behavior: at the end of each cycle, if W is still held, the next
	/// cycle is started directly (no Update tick required). The interCycleDelay
	/// in the middle preserves the discrete-rep cadence — without it, holding W
	/// reads as smooth gliding rather than inching. Releasing W mid-cycle lets
	/// the current cycle finish; no new one starts.
	/// </summary>
	private IEnumerator MoveCycle(PlayerController player)
	{
		isMoving = true;

		// Inch only: pick this cycle's shoulder-lead direction and flip for next time.
		float targetTwist = 0f;
		if (!isScootMode)
		{
			float leadSign = nextLeadIsRight ? 1f : -1f;
			nextLeadIsRight = !nextLeadIsRight;
			targetTwist = shoulderLeadAngle * leadSign;
		}

		float elapsed = 0f;
		while (elapsed < lungeDuration)
		{
			float t = elapsed / lungeDuration;
			float eased = 1f - (1f - t) * (1f - t);

			// Travel along the steering vector, NOT transform.forward — transform.forward
			// includes the visual flip offset, which would invert travel in scoot mode.
			// We want world-space travel to stay consistent.
			Vector3 steeringForward = Quaternion.Euler(0f, steeringYaw, 0f) * Vector3.forward;
			Vector3 perFrameDelta = steeringForward
				* (moveDistance / lungeDuration) * Time.deltaTime;
			player.Rb.MovePosition(player.Rb.position + perFrameDelta);

			// Twist is a no-op in scoot mode (targetTwist stays 0).
			twistOffset = Mathf.Lerp(0f, targetTwist, eased);

			elapsed += Time.deltaTime;
			yield return null;
		}

		float startTwist = twistOffset;
		elapsed = 0f;
		while (elapsed < settleDuration)
		{
			float t = elapsed / settleDuration;
			float eased = t * t * (3f - 2f * t);
			twistOffset = Mathf.Lerp(startTwist, 0f, eased);
			elapsed += Time.deltaTime;
			yield return null;
		}

		twistOffset = 0f;

		// Inter-cycle pause: brief beat between reps. Preserves the discrete-rep
		// cadence so hold-W reads as automated rhythm, not smooth gliding. Also
		// gives the player a frame-perfect window to release W if they want to
		// stop exactly here.
		if (interCycleDelay > 0f)
		{
			yield return new WaitForSeconds(interCycleDelay);
		}

		isMoving = false;

		// Hold-W chaining: if W is still held and the player isn't busy with
		// any other body-committing action (flip, kick), start the next cycle
		// directly. Don't wait for the next Update tick — that would add an
		// unpredictable extra frame of dead time. Checking player.IsBusy here
		// rather than just local state means future busy states (new restraint
		// types, additional verbs) automatically gate this chain correctly.
		if (Input.GetKey(KeyCode.W) && !player.IsBusy)
		{
			player.StartCoroutine(MoveCycle(player));
		}
	}

	public override float GetStruggleModifier()
	{
		return struggleBonus;
	}

	// Floor-bound legs can still kick, but ONLY in scoot mode.
	// Inch (prone) returns 0 — anatomically can't generate force kicking from your
	// stomach with bound legs, and couples the kick verb to scoot mode so the
	// C-toggle becomes a meaningful tactical choice (position vs. apply force).
	// PlayerController still plays the effort grunt on a 0-force kick attempt, so
	// the player gets "you tried, it didn't work" feedback — the cue to try scoot.
	public override float GetKickModifier()
	{
		return isScootMode ? kickModifier : 0f;
	}

	/// <summary>
	/// Feet point along the steering vector in scoot mode (visual feet at +forward
	/// since the body has visually flipped) and against it in inch mode.
	///
	/// Note: we use the steering yaw, NOT transform.forward, because transform.forward
	/// includes the visual flip offset. Kickable orientation gates care about world-space
	/// foot direction, which depends on which body-end is currently the "lead" end.
	/// </summary>
	public override Vector3 GetKickDirection(PlayerController player)
	{
		Vector3 steeringForward = Quaternion.Euler(0f, steeringYaw, 0f) * Vector3.forward;
		return isScootMode ? steeringForward : -steeringForward;
	}

	public override List<ControlHint> GetControlHints()
	{
		// Mode-aware hints. The W label and F conditional state both flip with the mode.
		List<ControlHint> hints = new List<ControlHint>();

		if (isScootMode)
		{
			hints.Add(new ControlHint("Scoot", "W (hold)"));
			hints.Add(new ControlHint("Flip Over", "C"));
			hints.Add(new ControlHint("Kick", "F"));
		}
		else
		{
			hints.Add(new ControlHint("Inch", "W (hold)"));
			hints.Add(new ControlHint("Flip Over", "C"));
			// Conditional: kick exists in this restraint but is suppressed in inch mode.
			// Greyed out + parenthetical hint at WHY it's unavailable, teaching the
			// inch↔scoot relationship.
			hints.Add(new ControlHint("Kick", "F", conditional: true, conditionalSuffix: "flip first"));
		}

		hints.Add(new ControlHint("Turn", "A / D"));
		hints.Add(new ControlHint("Struggle", "Space"));
		hints.Add(new ControlHint("Pick Up", "E"));

		return hints;
	}

	public override void OnExit(PlayerController player)
	{
		// No cleanup needed — steeringYaw/twistOffset reset on next OnEnter.
		// isScootMode persists by design (see OnEnter note).
	}
}
