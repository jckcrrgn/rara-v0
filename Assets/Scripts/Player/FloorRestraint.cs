using System.Collections;
using UnityEngine;

/// <summary>
/// Floor restraint: player is bound on the floor (duct tape, hands tied, etc.).
/// Movement is tap-to-inch — each W press triggers one inchworm cycle:
///   lunge forward with a slight shoulder lead, pause, settle.
/// Shoulder lead alternates left/right between inches for organic motion.
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

	// --- Internal state ---
	// Steering yaw is the "real" facing direction (controlled by A/D).
	// Twist offset is the inchworm shoulder lead (controlled by the inch coroutine).
	// Final rotation each frame = steeringYaw + twistOffset, applied in LateUpdate.
	private float steeringYaw;
	private float twistOffset;
	private bool isInching = false;
	private bool nextLeadIsRight = true;

	public override void OnEnter(PlayerController player)
	{
		// Initialize steering yaw from current rotation so we don't snap on entry.
		steeringYaw = player.transform.eulerAngles.y;
		twistOffset = 0f;
		nextLeadIsRight = true;
	}

	public override void HandleMovementInput(PlayerController player)
	{
		// Steering: A/D updates the steering yaw. Always responsive.
		float rotateInput = Input.GetAxis("Horizontal");
		steeringYaw += rotateInput * rotationSpeed * Time.deltaTime;

		// Tap W to inch. Ignored while an inch is already in progress.
		if (Input.GetKeyDown(KeyCode.W) && !isInching)
		{
			player.StartCoroutine(InchCycle(player));
		}

		// Apply combined rotation: steering + twist.
		player.transform.rotation = Quaternion.Euler(0f, steeringYaw + twistOffset, 0f);
	}

	private IEnumerator InchCycle(PlayerController player)
	{
		isInching = true;

		// Decide which shoulder leads this inch, then flip for next time.
		float leadSign = nextLeadIsRight ? 1f : -1f;
		nextLeadIsRight = !nextLeadIsRight;
		float targetTwist = shoulderLeadAngle * leadSign;

		// LUNGE: ease-out forward movement + twist toward leading shoulder.
		// Forward direction is re-read each frame so A/D steering during lunge feels live.
		float elapsed = 0f;
		while (elapsed < lungeDuration)
		{
			float t = elapsed / lungeDuration;
			float eased = 1f - (1f - t) * (1f - t); // ease-out

			// Forward push: per-frame delta along current forward direction.
			Vector3 perFrameDelta = player.transform.forward * (inchDistance / lungeDuration) * Time.deltaTime;
			player.Rb.MovePosition(player.Rb.position + perFrameDelta);

			// Twist ramps from 0 to targetTwist with the same easing curve.
			twistOffset = Mathf.Lerp(0f, targetTwist, eased);

			elapsed += Time.deltaTime;
			yield return null;
		}

		// SETTLE: no forward motion. Twist eases back toward 0 (body untwists).
		float startTwist = twistOffset;
		elapsed = 0f;
		while (elapsed < settleDuration)
		{
			float t = elapsed / settleDuration;
			float eased = t * t * (3f - 2f * t); // smoothstep
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
}
