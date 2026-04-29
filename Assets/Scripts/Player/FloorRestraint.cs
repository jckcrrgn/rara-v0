using System.Collections;
using UnityEngine;

/// <summary>
/// Floor restraint: player is bound on the floor (duct tape, hands tied, etc.).
/// Movement is tap-to-inch — each W press triggers one inchworm cycle:
///   lunge forward with a slight shoulder lead, pause, settle.
/// Shoulder lead alternates left/right between inches for organic motion.
///
/// Kick: legs are bound but mobile enough to deliver a reduced-force kick.
/// This is the floor-restrained level pattern — kick a shelf to knock down a tool,
/// kick a door (twice as many reps as a free-leg kick).
///
/// STATUS: Stub-but-tuned. Will need revisiting once the character model
/// replaces the cube — body tilt should look like a real twist, not a Y-rotation.
/// </summary>
public class FloorRestraint : RestraintBase
{
	[Header("Inch Movement")]
	[Tooltip("How far the player travels per inch.")]
	[SerializeField] private float inchDistance = 0.4f;
	[Tooltip("Duration of the lunge phase (forward push).")]
	[SerializeField] private float lungeDuration = 0.25f;
	[Tooltip("Duration of the settle phase (pause + body untwists).")]
	[SerializeField] private float settleDuration = 0.35f;

	[Header("Shoulder Lead")]
	[Tooltip("Degrees of Y-rotation tilt during a lunge. Alternates sign per inch.")]
	[SerializeField] private float shoulderLeadAngle = 12f;

	[Header("Rotation (steering)")]
	[SerializeField] private float rotationSpeed = 80f;

	[Header("Struggle Tuning")]
	[Tooltip("Floor-bound struggle uses the whole body — slightly more effective. 1.2 = 20% bonus.")]
	[SerializeField] private float struggleBonus = 1.2f;

	[Header("Kick Tuning")]
	[Tooltip("Kick force scalar while floor-bound. 0.5 = half the force of a free-legged kick. " +
		"Means floor-bound players need ~2x the reps to break the same Kickable.")]
	[SerializeField] private float kickModifier = 0.5f;

	// --- Internal state ---
	private float steeringYaw;
	private float twistOffset;
	private bool isInching = false;
	private bool nextLeadIsRight = true;

	public override void OnEnter(PlayerController player)
	{
		steeringYaw = player.transform.eulerAngles.y;
		twistOffset = 0f;
		nextLeadIsRight = true;
	}

	public override void HandleMovementInput(PlayerController player)
	{
		float rotateInput = Input.GetAxis("Horizontal");
		steeringYaw += rotateInput * rotationSpeed * Time.deltaTime;

		if (Input.GetKeyDown(KeyCode.W) && !isInching)
		{
			player.StartCoroutine(InchCycle(player));
		}

		player.transform.rotation = Quaternion.Euler(0f, steeringYaw + twistOffset, 0f);
	}

	private IEnumerator InchCycle(PlayerController player)
	{
		isInching = true;

		float leadSign = nextLeadIsRight ? 1f : -1f;
		nextLeadIsRight = !nextLeadIsRight;
		float targetTwist = shoulderLeadAngle * leadSign;

		float elapsed = 0f;
		while (elapsed < lungeDuration)
		{
			float t = elapsed / lungeDuration;
			float eased = 1f - (1f - t) * (1f - t);

			Vector3 perFrameDelta = player.transform.forward * (inchDistance / lungeDuration) * Time.deltaTime;
			player.Rb.MovePosition(player.Rb.position + perFrameDelta);

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
		isInching = false;
	}

	public override float GetStruggleModifier()
	{
		return struggleBonus;
	}

	// NEW: Floor-bound legs can still kick, just with reduced force.
	// This is what enables the "kick a shelf to knock the tool down" floor-level pattern,
	// and forces the L4 player to commit more kicks if they choose not to escape the tape first.
	public override float GetKickModifier()
	{
		return kickModifier;
	}

	public override void OnExit(PlayerController player)
	{
		// No cleanup needed — steeringYaw/twistOffset reset on next OnEnter.
	}
}
