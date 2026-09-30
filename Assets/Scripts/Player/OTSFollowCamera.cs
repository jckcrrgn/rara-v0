using UnityEngine;

/// <summary>
/// Over-the-shoulder follow camera for VS_Demo. Day 172.
///
/// Sits behind and to one side of the target, in the target's YAW-ONLY local
/// space, so it swings with A/D turns but ignores hop bob and chair tilt.
/// Runs in LateUpdate so it reads the target after this frame's movement.
///
/// Put it on Main Camera. Drag Player into Target. Tune in Play mode, then
/// right-click the component > Copy Component, exit Play, Paste Component
/// Values — Play-mode edits are otherwise lost.
///
/// If the camera ends up in front of her face, Player's forward isn't the way
/// Cassie faces: set Yaw Offset to 180 (or 90 / -90). Don't rotate the Player.
/// </summary>
public class OTSFollowCamera : MonoBehaviour
{
	[Header("Target")]
	[SerializeField] private Transform target;

	[Tooltip("Degrees added to the target's yaw. Use when Player's forward " +
		"isn't the direction Cassie faces.")]
	[SerializeField] private float yawOffset = 0f;

	[Header("Framing (metres, target-local: x right, y up, z forward)")]
	[Tooltip("Camera position. Positive X = over her right shoulder, negative = left. " +
		"Negative Z = behind her.")]
	[SerializeField] private Vector3 offset = new Vector3(0.45f, 1.35f, -1.4f);

	[Tooltip("Point the camera looks at. Z ahead of her; X nudges her off-centre " +
		"toward the opposite third of the frame.")]
	[SerializeField] private Vector3 lookPoint = new Vector3(0.15f, 0.9f, 2.5f);

	[Header("Smoothing")]
	[Tooltip("Seconds to catch up on position. Higher = lazier.")]
	[SerializeField] private float positionSmoothTime = 0.25f;

	[Tooltip("How fast rotation catches up. Higher = snappier. Keep it low-ish; " +
		"a fast swing on A/D in a small room is nauseating.")]
	[SerializeField] private float rotationSharpness = 6f;

	[Header("Walls")]
	[Tooltip("Layers the camera must not pass through. Nothing = disabled. " +
		"Never include the Player's own layer or she'll push the camera into her head.")]
	[SerializeField] private LayerMask obstacleMask = 0;

	[SerializeField] private float cameraRadius = 0.2f;

	private Vector3 velocity;

	private void Start()
	{
		Snap();
	}

	private void LateUpdate()
	{
		if (target == null) return;

		ComputeDesired(out Vector3 pos, out Quaternion rot);

		transform.position = Vector3.SmoothDamp(transform.position, pos,
			ref velocity, positionSmoothTime);

		float t = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
		transform.rotation = Quaternion.Slerp(transform.rotation, rot, t);
	}

	/// <summary>Jump straight to the framed position. Also on the component's ⋮ menu.</summary>
	[ContextMenu("Snap To Target")]
	public void Snap()
	{
		if (target == null) return;
		ComputeDesired(out Vector3 pos, out Quaternion rot);
		transform.SetPositionAndRotation(pos, rot);
		velocity = Vector3.zero;
	}

	private void ComputeDesired(out Vector3 pos, out Quaternion rot)
	{
		Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y + yawOffset, 0f);

		pos = target.position + yaw * offset;

		if (obstacleMask.value != 0)
		{
			// Cast from above her head out to the camera spot; stop short of any wall.
			Vector3 pivot = target.position + Vector3.up * offset.y;
			Vector3 toCam = pos - pivot;
			float dist = toCam.magnitude;
			if (dist > 1e-4f && Physics.SphereCast(pivot, cameraRadius, toCam / dist,
				out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
			{
				pos = pivot + toCam / dist * hit.distance;
			}
		}

		Vector3 look = target.position + yaw * lookPoint;
		rot = Quaternion.LookRotation(look - pos, Vector3.up);
	}
}
