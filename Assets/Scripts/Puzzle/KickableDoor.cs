using System.Collections;
using UnityEngine;

/// <summary>
/// A door the player kicks open. First concrete Kickable.
///
/// L4 setup:
///   - Place at the back of the van.
///   - Set requiredForce ~3 (three free-leg kicks, or six floor-bound kicks).
///   - Each door has a Rigidbody (kinematic by default) + HingeJoint constrained
///     to its pivot axis with limits set to the swing range.
///   - Assign leftPivot, rightPivot — the door Rigidbody Transforms.
///   - Assign leftStrikePoint, rightStrikePoint — child Transforms on each door
///     where the kick impulse is applied (interior face, mid-height).
///   - Assign kickZone — an empty Transform sitting at the door's interior face.
///     The player must be inside kickZoneRadius AND have their feet pointed at
///     the door for the kick to register. Otherwise it's a wall-thud.
///   - Level completion is NOT triggered here — place a LevelExitTrigger past
///     the van's threshold and the level completes when the player crosses it.
///
/// Why position-gated: per L4 redesign, bonds on this level are unbreakable
/// bare-hands. The puzzle is "tape your way close enough, orient correctly,
/// kick." This separates the verb cleanly from Struggle.
///
/// Per-kick give (Day 26): each registered kick rocks the doors outward a
/// small amount and settles back to rest. Rock magnitude scales with progress
/// so the third kick visibly leans further than the first. Doors stay
/// kinematic during the rock so the animation is clean.
///
/// Burst kick (Day 26 v2): the threshold-hitting kick switches doors to
/// non-kinematic and applies an impulse at the strike point. The hinge joint
/// constrains the swing to its axis with joint limits stopping the rotation
/// at the open angle. The player walking out of the van completes the level.
///
/// The handoff from kinematic-rock to physics-burst happens in OnFullyKicked.
/// The rock animation that's mid-flight when the burst lands is cancelled,
/// kinematic flips off, impulse fires from current rotation. No snap.
/// </summary>
public class KickableDoor : Kickable
{
	[Header("Position Gate")]
	[Tooltip("Empty Transform marking the spot the player must reach to kick. " +
		"Sit it at the door's interior face, roughly at floor level.")]
	[SerializeField] private Transform kickZone;
	[Tooltip("How close the player must be to kickZone (world-space distance).")]
	[SerializeField] private float kickZoneRadius = 1.0f;
	[Tooltip("Dot threshold for player's feet-vector vs. direction-to-door. " +
		"0.7 = ~45° cone. Higher is stricter.")]
	[Range(0f, 1f)]
	[SerializeField] private float feetDotThreshold = 0.7f;

	[Header("Door Rigidbodies")]
	[Tooltip("Left door's Rigidbody Transform. Should have a HingeJoint with limits " +
		"set to the swing range, and isKinematic=true at scene start.")]
	[SerializeField] private Transform leftPivot;
	[Tooltip("Right door's Rigidbody Transform. Same setup as leftPivot.")]
	[SerializeField] private Transform rightPivot;
	[Tooltip("Child Transform on the left door where kick impulse is applied. " +
		"Place on the interior face, mid-height, roughly center of the door's surface.")]
	[SerializeField] private Transform leftStrikePoint;
	[Tooltip("Child Transform on the right door where kick impulse is applied.")]
	[SerializeField] private Transform rightStrikePoint;

	[Header("Per-Kick Give")]
	[Tooltip("Max angle each door rocks outward on the strongest pre-threshold kick. " +
		"Actual rock per kick scales with progress: first kick rocks small, last " +
		"pre-threshold kick rocks near this max. Tune so 'rocking' reads distinct " +
		"from 'opening'.")]
	[SerializeField] private float maxGiveAngle = 12f;
	[Tooltip("How long the rock-out portion of a give takes.")]
	[SerializeField] private float giveOutDuration = 0.08f;
	[Tooltip("How long the settle-back portion of a give takes. Slightly longer than " +
		"the rock-out so the door 'falls back' rather than snapping.")]
	[SerializeField] private float giveSettleDuration = 0.18f;

	[Header("Burst Kick (Physics)")]
	[Tooltip("Impulse magnitude applied to each door on the threshold-hitting kick. " +
		"Tune relative to door mass. Higher = doors fly open faster and slam against " +
		"hinge limits harder. Start ~25-50, tune from there.")]
	[SerializeField] private float kickImpulse = 35f;

	[Header("SFX")]
	[Tooltip("Per-kick thud. Plays each time a kick registers but the door isn't open yet.")]
	[SerializeField] private AudioClip kickThudClip;
	[Tooltip("Big door-burst SFX when the door finally opens.")]
	[SerializeField] private AudioClip kickOpenClip;

	// Rest-pose rotations captured at Start so per-kick give has a stable origin.
	// Without this, repeated rocks would compound off whatever rotation the door
	// happened to be at last frame.
	private Quaternion leftRestRotation;
	private Quaternion rightRestRotation;
	private Coroutine giveRoutine;

	// Cached rigidbodies — looked up once at Start to avoid per-kick GetComponent.
	private Rigidbody leftRb;
	private Rigidbody rightRb;

	private void Start()
	{
		if (leftPivot != null)
		{
			leftRestRotation = leftPivot.localRotation;
			leftRb = leftPivot.GetComponent<Rigidbody>();
			if (leftRb == null)
				Debug.LogWarning($"{name}: leftPivot has no Rigidbody. Burst kick will fail.");
		}
		if (rightPivot != null)
		{
			rightRestRotation = rightPivot.localRotation;
			rightRb = rightPivot.GetComponent<Rigidbody>();
			if (rightRb == null)
				Debug.LogWarning($"{name}: rightPivot has no Rigidbody. Burst kick will fail.");
		}
	}

	/// <summary>
	/// Position gate. Player must be in the kick zone AND have their feet oriented
	/// toward the door (so a "kick" reads as kicking forward into it from where
	/// the legs are pointed).
	///
	/// Uses the restraint's GetKickDirection rather than -transform.forward directly,
	/// because feet/forward relationship varies by restraint:
	///   - Inch (FloorRestraint, prone, head-leading): feet = -forward
	///   - Scoot (FloorRestraint, supine, feet-leading): feet = +forward
	///   - Chair / Cuffed / Hanging: feet = -forward (default)
	///
	/// Note: in inch mode the forward vector includes a small twist offset
	/// (max ~12° during shoulder-lead). The threshold of 0.7 has slack for that.
	/// </summary>
	public override bool CanBeKicked(PlayerController player)
	{
		if (!base.CanBeKicked(player)) return false;
		if (kickZone == null) return true; // No gate configured — accept always.

		// Proximity check.
		float dist = Vector3.Distance(player.transform.position, kickZone.position);
		if (dist > kickZoneRadius) return false;

		// Orientation check: player's feet vector should point roughly at the door.
		Vector3 toDoor = (transform.position - player.transform.position);
		toDoor.y = 0f;
		if (toDoor.sqrMagnitude < 0.0001f) return true; // Standing on the gate; accept.
		toDoor.Normalize();

		Vector3 feet = player.CurrentRestraint != null
			? player.CurrentRestraint.GetKickDirection(player)
			: -player.transform.forward; // Fallback if somehow no restraint is set.

		float dot = Vector3.Dot(feet, toDoor);
		return dot >= feetDotThreshold;
	}

	protected override void OnKickRegistered(PlayerController player, float force)
	{
		if (AudioManager.Instance != null && kickThudClip != null)
		{
			AudioManager.Instance.PlaySFX(kickThudClip, 1f, Random.Range(0.95f, 1.05f));
		}

		// Per-kick rock-and-settle. Skip if this kick is the threshold-hitter —
		// OnFullyKicked will run the burst from current rotation and we don't
		// want a settle fighting that. Detection: currentForce already includes
		// this kick by the time OnKickRegistered fires (see Kickable.OnKick),
		// so >= threshold means this is the burst kick.
		if (currentForce >= requiredForce) return;

		// Progress-scaled rock magnitude. First kick rocks small, last pre-burst
		// kick rocks near maxGiveAngle. Same shape as the desk-jostle escalation.
		float progress = currentForce / requiredForce;
		float rockAngle = Mathf.Lerp(maxGiveAngle * 0.4f, maxGiveAngle, progress);

		// Cancel any in-flight give so a fast follow-up kick doesn't fight an
		// existing settle. New give starts from current rotation, settles to rest.
		if (giveRoutine != null) StopCoroutine(giveRoutine);
		giveRoutine = StartCoroutine(GiveRoutine(rockAngle));
	}

	/// <summary>
	/// Rock the doors outward by rockAngle, then settle back to rest pose.
	/// Outward direction matches the open swing direction (left negative, right positive).
	/// Doors are kinematic during the rock so the animation drives transform directly.
	/// </summary>
	private IEnumerator GiveRoutine(float rockAngle)
	{
		if (leftPivot == null || rightPivot == null) yield break;

		Quaternion leftStart = leftPivot.localRotation;
		Quaternion rightStart = rightPivot.localRotation;
		Quaternion leftRocked = leftRestRotation * Quaternion.Euler(0f, -rockAngle, 0f);
		Quaternion rightRocked = rightRestRotation * Quaternion.Euler(0f, rockAngle, 0f);

		// Rock out — quick.
		float elapsed = 0f;
		while (elapsed < giveOutDuration)
		{
			float t = elapsed / giveOutDuration;
			float eased = 1f - (1f - t) * (1f - t); // ease-out
			leftPivot.localRotation = Quaternion.Slerp(leftStart, leftRocked, eased);
			rightPivot.localRotation = Quaternion.Slerp(rightStart, rightRocked, eased);
			elapsed += Time.deltaTime;
			yield return null;
		}
		leftPivot.localRotation = leftRocked;
		rightPivot.localRotation = rightRocked;

		// Settle back — slower, falls.
		elapsed = 0f;
		while (elapsed < giveSettleDuration)
		{
			float t = elapsed / giveSettleDuration;
			float eased = t * t * (3f - 2f * t); // smoothstep
			leftPivot.localRotation = Quaternion.Slerp(leftRocked, leftRestRotation, eased);
			rightPivot.localRotation = Quaternion.Slerp(rightRocked, rightRestRotation, eased);
			elapsed += Time.deltaTime;
			yield return null;
		}
		leftPivot.localRotation = leftRestRotation;
		rightPivot.localRotation = rightRestRotation;

		giveRoutine = null;
	}

	protected override void OnFullyKicked(PlayerController player)
	{
		// Cancel any in-flight give so the burst starts from a known state
		// (the doors' current rotation, which may be mid-rock from the threshold kick).
		if (giveRoutine != null)
		{
			StopCoroutine(giveRoutine);
			giveRoutine = null;
		}

		// Burst SFX.
		if (AudioManager.Instance != null && kickOpenClip != null)
		{
			AudioManager.Instance.PlaySFX(kickOpenClip, 1f, 1f);
		}

		// Hand off from kinematic-driven rock to physics-driven swing.
		// The HingeJoint's limits will catch the swing at the open angle; mass
		// and any joint drag will determine settling behavior. The doors are
		// now in the world — they can collide with walls or each other.
		BurstDoor(leftRb, leftStrikePoint);
		BurstDoor(rightRb, rightStrikePoint);

		// Level completion is handled by LevelExitTrigger past the van threshold
		// — escaping the van completes the level, not the doors finishing their swing.
	}

	/// <summary>
	/// Switch a door from kinematic to physics-driven and apply the kick impulse
	/// at the strike point. Direction is the door's local outward axis at the
	/// strike point — pushing through the door's interior face.
	///
	/// Strike point matters: applying force at the door's pivot would do nothing
	/// (zero torque); applying at the far edge gives maximum torque. Place the
	/// strike point where the foot would actually land, and the impulse-to-rotation
	/// mapping comes out naturally.
	/// </summary>
	private void BurstDoor(Rigidbody rb, Transform strikePoint)
	{
		if (rb == null || strikePoint == null) return;

		rb.isKinematic = false;

		// Impulse direction: from the kicker's side to the outside, i.e. the
		// door's outward face normal. We approximate with the door's local
		// forward — set up the prefab so leftPivot.forward points outward from
		// the van. If your prefab orients differently, swap to -forward or
		// transform.right as needed.
		Vector3 impulseDir = rb.transform.forward;
		Debug.Log($"{rb.name} burst: dir={impulseDir}, mass={rb.mass}, kinematic={rb.isKinematic}");

		rb.AddForceAtPosition(impulseDir * kickImpulse, strikePoint.position, ForceMode.Impulse);
	}
}
