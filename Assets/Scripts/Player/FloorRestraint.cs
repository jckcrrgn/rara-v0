using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Floor restraint: player is bound on the floor (duct tape, hands tied, etc.).
///
/// STATE MODEL (decoupled — Day 60 refactor)
/// -----------------------------------------
/// Two INDEPENDENT axes describe the body on the floor, where before a single
/// isScootMode bool conflated them:
///   - bodyRoll      : rotation about the heading-forward axis. 0 = belly-down
///                     (prone), 180 = belly-up (supine). Continuous. SOURCE OF
///                     TRUTH for belly orientation. Driven by Roll (Shift+A/D)
///                     and, as a convenience, by the C flip.
///   - leadYawOffset : 0 = head leads the heading, 180 = feet lead. Independent
///                     of belly. Driven by the C flip.
///
/// Why decouple: a log roll (Shift+A/D) changes belly ONLY — your head stays
/// your head. The old isScootMode coupled belly+lead into a single diagonal
/// (prone-headfirst <-> supine-feetfirst), so a plain roll had nowhere to live.
/// And the things isScootMode actually gated turned out to be belly conditions
/// wearing a mode-bool costume:
///   - Kick is suppressed prone because you can't kick off your stomach with
///     bound legs — that's belly-DOWN, not head-first. GetKickModifier now
///     reads IsBellyUp.
///   - The inch shoulder-lead twist is a prone tell — MoveCycle now applies it
///     when IsBellyDown.
/// Kick DIRECTION (which way the feet point) is the one genuinely lead-end
/// thing, so GetKickDirection reads the lead axis (IsFeetFirst), not belly.
///
/// REGRESSION INVARIANT (verify on L4): belly-down + head-first must feel like
/// old inch (twist + kick suppressed); belly-up + feet-first like old scoot
/// (no twist + kick enabled, +forward); and one C press must still toggle
/// directly between those two.
///
/// MOVEMENT (W) — unchanged feel
/// -----------------------------
/// Hold W to travel along +heading in either belly. Belly-down applies the
/// alternating shoulder-lead twist (inchworm); belly-up is symmetric (scoot).
/// Travel direction is the steering vector, never transform.forward (which now
/// carries the roll + lead + twist visual offsets).
///
/// ROLL (Shift+A/D) — the third mode, now implemented
/// --------------------------------------------------
/// Each Shift+A or Shift+D is one discrete roll cycle: the body log-rolls
/// rollAnglePerCycle degrees about the heading-forward axis AND translates one
/// rollLateralDistance sideways (left for A, right for D). Rolling both flips
/// belly (down<->up through the side) and walks her laterally — which is how she
/// brings her bound hands down onto a tool on the floor. The hand-over-tool
/// pickup gate is a SEPARATE component that keys off a hand anchor whose world
/// position tracks bodyRoll; this script just owns the locomotion. "Fast,
/// noisy" per the original reservation: short duration + roll SFX.
///
/// C FLIP — kept as a coupled convenience
/// --------------------------------------
/// C still does the old one-press inch<->scoot in a single action: it animates
/// bodyRoll to the opposite belly AND leadYawOffset to the opposite lead end at
/// once. It is no longer the ONLY way to change belly (Shift+A/D does that
/// independently); it's a shortcut that preserves L1–L5 muscle memory so the
/// refactor doesn't regress shipped levels' controls. To make C a lead-end-only
/// flip and let Roll own belly entirely, drop the bodyRoll line in FlipCycle
/// (see the "ONE-LINE DECOUPLE" marker).
///
/// VISUALS ON A CUBE
/// -----------------
/// bodyRoll is applied as a true roll about the heading-forward axis. On the
/// placeholder cube the belly portion is near-invisible (roughly symmetric
/// about that axis); the observable signals are the lateral translation, the
/// kick becoming available when belly-up, and the debug logs. When the
/// character model lands the roll reads literally.
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
		"Applies when BELLY-DOWN (prone); belly-up (scoot) is symmetric (both legs " +
		"push together) so this doesn't apply there.")]
	[SerializeField] private float shoulderLeadAngle = 12f;

	[Header("Rotation (steering)")]
	[Tooltip("A/D steering speed (deg/sec). Note: Shift+A/D is intercepted for " +
		"Roll and does NOT steer — plain A/D steers, Shift+A/D rolls.")]
	[SerializeField] private float rotationSpeed = 80f;

	[Header("Mode Toggle (C) — coupled convenience flip")]
	[Tooltip("Key for the one-press inch<->scoot flip. Animates BOTH belly " +
		"(bodyRoll 180) and lead end (leadYawOffset 180) together, reproducing " +
		"the old single-press behaviour. World-space travel direction does NOT " +
		"change. State persists across the FloorRestraint session.")]
	[SerializeField] private KeyCode modeToggleKey = KeyCode.C;
	[Tooltip("Duration of the C flip animation. W input is locked during the flip.")]
	[SerializeField] private float flipDuration = 2f;

	[Header("Roll (Shift+A/D)")]
	[Tooltip("Modifier key that turns A/D into a roll instead of a steer.")]
	[SerializeField] private KeyCode rollModifier = KeyCode.LeftShift;
	[Tooltip("Second accepted modifier (right shift) so either Shift works.")]
	[SerializeField] private KeyCode rollModifierAlt = KeyCode.RightShift;
	[Tooltip("Degrees of roll about the heading-forward axis per Shift+A/D press. " +
		"180 = a full barrel roll to the opposite belly (back<->front) in one go, " +
		"matching 'roll to the side and end up facedown'. Drop to 90 for finer " +
		"belly/hand positioning (down -> side -> up in two presses).")]
	[SerializeField] private float rollAnglePerCycle = 180f;
	[Tooltip("How far she translates sideways per roll cycle (left for Shift+A, " +
		"right for Shift+D). Roughly a body width — this is what walks her hands " +
		"laterally onto a tool. Tune against moveDistance.")]
	[SerializeField] private float rollLateralDistance = 0.5f;
	[Tooltip("Duration of one roll cycle. 'Fast' per the original reservation — " +
		"snappier than the C flip.")]
	[SerializeField] private float rollDuration = 0.5f;
	[Tooltip("Roll SFX ('noisy'). Played once at the start of each roll cycle " +
		"through AudioManager. Leave null until a clip is wired.")]
	[SerializeField] private AudioClip rollClip;
	[Tooltip("Volume for the roll clip.")]
	[SerializeField] private float rollVolume = 1f;

	[Header("Belly Classification")]
	[Tooltip("Half-width (deg) of the belly-down and belly-up bands. Within this " +
		"of 0 counts as belly-DOWN (twist applies); within this of 180 counts as " +
		"belly-UP (kick enabled). 60 leaves a neutral band around the sides (90) " +
		"where she's neither — can't kick, no twist. Lower = stricter postures.")]
	[Range(0f, 90f)]
	[SerializeField] private float bellyOrientationThreshold = 60f;

	[Header("Struggle Tuning")]
	[Tooltip("Floor-bound struggle uses the whole body — slightly more effective. 1.2 = 20% bonus.")]
	[SerializeField] private float struggleBonus = 1.2f;

	[Header("Kick Tuning")]
	[Tooltip("Kick force scalar while floor-bound AND belly-up. 0.5 = half the force of a free-legged kick. " +
		"Means floor-bound players need ~2x the reps to break the same Kickable. " +
		"Belly-down (and the neutral side band) returns 0 — kick is suppressed until she rolls belly-up.")]
	[SerializeField] private float kickModifier = 0.5f;

	// --- Internal state ---
	// bodyRoll and leadYawOffset are the decoupled body state and PERSIST across
	// re-entry (the old isScootMode persisted; these are its two halves).
	private float bodyRoll;          // 0 = belly-down (prone), 180 = belly-up (supine). Source of truth.
	private float leadYawOffset;     // 0 = head leads, 180 = feet lead.
	private float steeringYaw;       // heading (A/D). Persists across re-entry.
	private float twistOffset;       // transient per-inch shoulder lead.
	private bool isMoving = false;
	private bool isFlipping = false;
	private bool isRolling = false;
	private bool nextLeadIsRight = true;
	private bool hasInitializedHeading = false;

	// --- Derived posture queries ---
	// Belly conditions read bodyRoll. These are what used to hide behind isScootMode.
	private bool IsBellyUp => Mathf.Abs(Mathf.DeltaAngle(bodyRoll, 180f)) <= bellyOrientationThreshold;
	private bool IsBellyDown => Mathf.Abs(Mathf.DeltaAngle(bodyRoll, 0f)) <= bellyOrientationThreshold;
	// Lead end reads leadYawOffset. The one genuinely non-belly thing kick needs.
	private bool IsFeetFirst => Mathf.Abs(Mathf.DeltaAngle(leadYawOffset, 180f)) < 90f;

	// Inch cycle, scoot cycle, flip cycle, and roll cycle all commit the body to
	// a motion. Steering (plain A/D) does NOT — that's aim, not body-committing.
	public override bool IsBusy => isMoving || isFlipping || isRolling;

	public override void OnEnter(PlayerController player)
	{
		// Initialize heading from the transform only the FIRST time we enter —
		// after that, steeringYaw/bodyRoll/leadYawOffset are authoritative and
		// recovering yaw from eulerAngles would be wrong once a roll has been
		// applied (the composed rotation is no longer pure yaw).
		if (!hasInitializedHeading)
		{
			steeringYaw = player.transform.eulerAngles.y;
			bodyRoll = 0f;        // start belly-down (prone / inch)
			leadYawOffset = 0f;   // start head-first
			hasInitializedHeading = true;
		}
		twistOffset = 0f;
		nextLeadIsRight = true;
	}

	public override void HandleMovementInput(PlayerController player)
	{
		bool rollHeld = Input.GetKey(rollModifier) || Input.GetKey(rollModifierAlt);

		if (rollHeld)
		{
			// Shift+A/D rolls; suppress steering this frame so a roll input
			// doesn't also turn the heading.
			if (!player.IsBusy)
			{
				if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
					player.StartCoroutine(RollCycle(player, -1));   // roll left
				else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
					player.StartCoroutine(RollCycle(player, +1));   // roll right
			}
		}
		else
		{
			// Plain A/D steers the heading. Works during a flip/roll (course-correct).
			float rotateInput = Input.GetAxis("Horizontal");
			steeringYaw += rotateInput * rotationSpeed * Time.deltaTime;
		}

		if (Input.GetKeyDown(modeToggleKey) && !player.IsBusy)
		{
			player.StartCoroutine(FlipCycle(player));
		}

		if (Input.GetKey(KeyCode.W) && !player.IsBusy)
		{
			player.StartCoroutine(MoveCycle(player));
		}

		// Compose the visual: heading (steering + per-inch twist + lead-end offset)
		// on Y, then a true roll about the resulting forward axis for belly.
		Quaternion heading = Quaternion.Euler(0f, steeringYaw + twistOffset + leadYawOffset, 0f);
		Vector3 forwardAxis = heading * Vector3.forward;
		player.transform.rotation = Quaternion.AngleAxis(bodyRoll, forwardAxis) * heading;
	}

	/// <summary>
	/// One roll cycle (Shift+A/D). Log-rolls rollAnglePerCycle about the heading-
	/// forward axis and translates rollLateralDistance sideways. dir = -1 left
	/// (Shift+A), +1 right (Shift+D). This is the locomotion that lets her bring
	/// her bound hands down onto a floor tool — both the belly flip (hands swing
	/// from up to down) and the lateral walk (hands move over the target) come
	/// out of the same motion.
	///
	/// Physics is set kinematic during the roll so the cube doesn't fight the
	/// floor mid-rotation (same reason as FlipCycle). Lateral motion is written
	/// straight to transform.position, NOT via MovePosition: HandleMovementInput
	/// assigns transform.rotation every frame, and that direct transform write
	/// re-syncs the Rigidbody and discards any pending MovePosition target — so
	/// a kinematic MovePosition here silently does nothing. A direct position
	/// write isn't disturbed by the rotation write.
	///
	/// Roll/lateral sign pairing: a positive bodyRoll about forward reads as a
	/// LEFT roll in Unity's convention, so the roll delta is negated against dir
	/// to keep "Shift+D = roll right AND move right" consistent.
	///
	/// Hints refresh at the start because a roll can cross the belly-up threshold,
	/// flipping the Kick conditional state immediately.
	///
	/// Per-press (discrete) for positioning control. To hold-chain like W, mirror
	/// MoveCycle's tail check on the roll key.
	/// </summary>
	private IEnumerator RollCycle(PlayerController player, int dir)
	{
		isRolling = true;
		RaiseHintsChanged();

		if (AudioManager.Instance != null && rollClip != null)
			AudioManager.Instance.PlaySFX(rollClip, rollVolume, Random.Range(0.96f, 1.04f));

		Debug.Log($"FloorRestraint: ROLL {(dir < 0 ? "left" : "right")} " +
			$"(bodyRoll {bodyRoll:F0} -> {bodyRoll - rollAnglePerCycle * dir:F0}).");

		bool wasKinematic = player.Rb.isKinematic;
		player.Rb.isKinematic = true;

		// Lateral direction is perpendicular to the heading (steering only, not
		// the visual offsets): Shift+D (dir +1) -> her right, Shift+A -> her left.
		Vector3 lateralDir = (Quaternion.Euler(0f, steeringYaw, 0f) * Vector3.right) * dir;

		float startRoll = bodyRoll;
		// Negated against dir: +bodyRoll rolls LEFT, so D (+1) must DECREASE
		// bodyRoll to roll right and match the rightward lateral motion.
		float endRoll = bodyRoll - rollAnglePerCycle * dir;

		float elapsed = 0f;
		while (elapsed < rollDuration)
		{
			float t = elapsed / rollDuration;
			float eased = t * t * (3f - 2f * t); // smoothstep
			bodyRoll = Mathf.Lerp(startRoll, endRoll, eased);

			// Direct transform write (see method doc): MovePosition would be
			// clobbered by HandleMovementInput's per-frame rotation assignment.
			Vector3 perFrameDelta = lateralDir * (rollLateralDistance / rollDuration) * Time.deltaTime;
			player.transform.position += perFrameDelta;

			elapsed += Time.deltaTime;
			yield return null;
		}

		bodyRoll = Mathf.Repeat(endRoll, 360f);

		player.Rb.isKinematic = wasKinematic;
		isRolling = false;
		RaiseHintsChanged();
	}

	/// <summary>
	/// C flip: the coupled convenience toggle. Animates bodyRoll to the opposite
	/// belly AND leadYawOffset to the opposite lead end together, reproducing the
	/// old one-press inch<->scoot. Travel (steeringYaw) is untouched. W is locked
	/// during the flip; steering stays active so the player can course-correct.
	///
	/// Hint refresh fires at the start so the W label and F-kick conditional
	/// update the moment the player commits, not when the animation ends.
	/// </summary>
	private IEnumerator FlipCycle(PlayerController player)
	{
		isFlipping = true;

		// Snapshot targets at commit time (reading the derived queries once).
		float startRoll = bodyRoll;
		float endRoll = IsBellyUp ? 0f : 180f;          // ONE-LINE DECOUPLE: delete this pair + the bodyRoll lerp below to make C lead-only.
		float startYaw = leadYawOffset;
		float endYaw = IsFeetFirst ? 0f : 180f;

		RaiseHintsChanged();
		Debug.Log($"FloorRestraint: C FLIP (belly {startRoll:F0}->{endRoll:F0}, lead {startYaw:F0}->{endYaw:F0}).");

		// Disable physics during the flip so the cube can rotate cleanly without
		// the collider fighting the floor. Re-enabled on exit.
		bool wasKinematic = player.Rb.isKinematic;
		player.Rb.isKinematic = true;

		float elapsed = 0f;
		while (elapsed < flipDuration)
		{
			float t = elapsed / flipDuration;
			float eased = t * t * (3f - 2f * t); // smoothstep
			bodyRoll = Mathf.LerpAngle(startRoll, endRoll, eased);
			leadYawOffset = Mathf.LerpAngle(startYaw, endYaw, eased);
			elapsed += Time.deltaTime;
			yield return null;
		}

		bodyRoll = endRoll;
		leadYawOffset = endYaw;

		player.Rb.isKinematic = wasKinematic;
		isFlipping = false;
		RaiseHintsChanged();
	}

	/// <summary>
	/// One movement cycle. Always travels along the steering vector — the visual
	/// offsets (roll, lead, twist) don't affect travel, so W is consistent in any
	/// posture. Belly-DOWN applies the alternating shoulder-lead twist (inchworm);
	/// belly-up (and the neutral side band) is symmetric.
	///
	/// Hold-W: at the end of each cycle, if W is still held and nothing else is
	/// busy, the next cycle starts directly. interCycleDelay preserves the
	/// discrete-rep cadence. Releasing W mid-cycle lets the current one finish.
	/// </summary>
	private IEnumerator MoveCycle(PlayerController player)
	{
		isMoving = true;

		// Shoulder-lead twist only when belly-down (prone). Belly-up / on-side: symmetric.
		float targetTwist = 0f;
		if (IsBellyDown)
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

			// Travel along the steering vector, NOT transform.forward — the latter
			// carries roll/lead/twist offsets and would corrupt world-space travel.
			Vector3 steeringForward = Quaternion.Euler(0f, steeringYaw, 0f) * Vector3.forward;
			Vector3 perFrameDelta = steeringForward
				* (moveDistance / lungeDuration) * Time.deltaTime;
			player.Rb.MovePosition(player.Rb.position + perFrameDelta);

			// No-op when belly-up/on-side (targetTwist stays 0).
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

		if (interCycleDelay > 0f)
		{
			yield return new WaitForSeconds(interCycleDelay);
		}

		isMoving = false;

		if (Input.GetKey(KeyCode.W) && !player.IsBusy)
		{
			player.StartCoroutine(MoveCycle(player));
		}
	}

	public override float GetStruggleModifier()
	{
		return struggleBonus;
	}

	// Floor-bound legs can kick only when BELLY-UP (was: only in scoot mode).
	// Belly-down — and the neutral side band — return 0: you can't generate force
	// kicking off your stomach with bound legs, and this keeps "roll belly-up to
	// kick" a meaningful tactical choice. PlayerController still plays the effort
	// grunt on a 0-force kick, so "you tried, it didn't work" still reads.
	public override float GetKickModifier()
	{
		return IsBellyUp ? kickModifier : 0f;
	}

	// On the floor = can reach floor tools. The #1 gate; #2 adds the hand-over-tool
	// proximity check on top of this.
	public override bool CanReachFloorTools() => true;

	/// <summary>
	/// Feet point along the steering vector when feet-first, against it when
	/// head-first. Reads the LEAD axis (not belly) — which way the feet point is
	/// a head/feet question, independent of which way she's rolled. Uses steering
	/// yaw, not transform.forward, because the latter carries the visual offsets.
	/// </summary>
	public override Vector3 GetKickDirection(PlayerController player)
	{
		Vector3 steeringForward = Quaternion.Euler(0f, steeringYaw, 0f) * Vector3.forward;
		return IsFeetFirst ? steeringForward : -steeringForward;
	}

	public override List<ControlHint> GetControlHints()
	{
		// Posture-aware hints. W label tracks belly; Kick conditional tracks belly-up.
		List<ControlHint> hints = new List<ControlHint>();

		if (IsBellyUp)
		{
			hints.Add(new ControlHint("Scoot", "W (hold)"));
			hints.Add(new ControlHint("Kick", "F"));
		}
		else
		{
			hints.Add(new ControlHint(IsBellyDown ? "Inch" : "Crawl", "W (hold)"));
			// Kick exists but is suppressed until belly-up. Greyed + why.
			hints.Add(new ControlHint("Kick", "F", conditional: true, conditionalSuffix: "roll belly-up"));
		}

		hints.Add(new ControlHint("Roll", "Shift + A / D"));
		hints.Add(new ControlHint("Flip Over", "C"));
		hints.Add(new ControlHint("Turn", "A / D"));
		hints.Add(new ControlHint("Struggle", "Space"));
		hints.Add(new ControlHint("Pick Up", "E"));

		return hints;
	}

	public override void OnExit(PlayerController player)
	{
		// No cleanup needed — twistOffset resets on next OnEnter; bodyRoll,
		// leadYawOffset, and steeringYaw persist by design.
	}
}
