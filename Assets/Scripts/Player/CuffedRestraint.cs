using System.Collections;
using UnityEngine;

/// <summary>
/// Cuffed restraint: hands cuffed behind the player around a fixed anchor (a pipe,
/// a radiator, a railing). The player can rotate around the anchor but cannot
/// translate — body always faces outward, back to the pipe.
///
/// Adds a new verb: Drag (T key). Drags a small object on the floor toward the
/// player using her foot, bringing it within normal Pick Up range. Two-step:
/// Drag with T, then Pick Up with E.
///
/// STATUS: First pass. Camera is fixed-per-level (matches L1-L4). Body model is
/// still a cube; the rotate-around-anchor will need to be revisited once the
/// character model lands so her back actually faces the pipe visually.
/// </summary>
public class CuffedRestraint : RestraintBase
{
	[Header("Anchor")]
	[Tooltip("The point the player is cuffed to (e.g., the pipe). Must be assigned.")]
	[SerializeField] private Transform anchor;
	[Tooltip("Distance from anchor to player body. Roughly arm-length.")]
	[SerializeField] private float tetherRadius = 0.8f;

	[Header("Step Orbit")]
	[Tooltip("Degrees per step. Negative for A (CCW), positive for D (CW) — applied via input sign.")]
	[SerializeField] private float orbitStepAngle = 15f;
	[Tooltip("How long each step takes, in seconds. Lower = snappier shuffle.")]
	[SerializeField] private float orbitStepDuration = 0.8f;
	[Tooltip("Cooldown between steps, in seconds. Prevents instant re-trigger when holding A/D.")]
	[SerializeField] private float orbitStepCooldown = 0.3f;


	[Header("Drag Verb")]
	[Tooltip("Forward cone reach distance for the Drag verb.")]
	[SerializeField] private float dragRange = 2f;
	[Tooltip("Half-angle of the forward cone, in degrees. 30 = 60-degree total cone.")]
	[SerializeField] private float dragConeHalfAngle = 30f;
	[Tooltip("Maximum mass (kg) of an object the player can drag with her foot.")]
	[SerializeField] private float dragMassThreshold = 5f;
	[Tooltip("How long the drag motion takes, in seconds.")]
	[SerializeField] private float dragDuration = 0.5f;
	[Tooltip("How close to the player the dragged item ends up. Should be within PlayerController.interactionCheckRadius (1.5).")]
	[SerializeField] private float dragEndOffset = 0.6f;
	[Tooltip("Layer mask for items the Drag verb can target.")]
	[SerializeField] private LayerMask dragTargetLayer = ~0;

	[Header("Struggle Tuning")]
	[Tooltip("Cuffs are mechanically rigid — bare-hands struggle is near-useless. Bond config should already enforce this (bareHandsProgress = 0).")]
	[SerializeField] private float struggleModifier = 1f;

	// --- Internal state ---
	private float currentAngle; // angle around anchor in degrees
	private bool isDragging = false;
	private bool isStepping = false;
	private float nextStepAllowedTime = 0f;

	public override void OnEnter(PlayerController player)
	{
		Debug.Log($"CuffedRestraint.OnEnter called. Anchor: {(anchor != null ? anchor.name : "NULL")}");

		if (anchor == null)
		{
			Debug.LogError("CuffedRestraint has no anchor assigned. Player will not be positioned.");
			return;
		}

		// Compute initial angle from anchor to player's current XZ position so we
		// don't snap on entry. If the player happened to spawn exactly at the anchor,
		// default to angle 0 (player ends up on +X side of anchor).
		Vector3 fromAnchor = player.transform.position - anchor.position;
		fromAnchor.y = 0f;
		if (fromAnchor.sqrMagnitude < 0.0001f)
		{
			currentAngle = 0f;
		}
		else
		{
			currentAngle = Mathf.Atan2(fromAnchor.z, fromAnchor.x) * Mathf.Rad2Deg;
		}

		ApplyOrbitPosition(player);

		// Freeze rigidbody position so physics can't drift the player off the anchor.
		// We'll move her manually via MovePosition. Rotation is also fully manual here.
		if (player.Rb != null)
		{
			player.Rb.isKinematic = true;
		}
	}

	public override void OnExit(PlayerController player)
	{
		// Restore physics. Whatever restraint comes next can re-freeze if it needs to.
		if (player != null && player.Rb != null)
		{
			player.Rb.isKinematic = false;
		}
	}

	public override void HandleMovementInput(PlayerController player)
	{
		if (anchor == null)
		{
			Debug.LogWarning("CuffedRestraint.HandleMovementInput: anchor is null, returning early");
			return;
		}

		// A/D: step around the anchor in discrete chunks.
		// GetAxisRaw so holding the key gives clean -1/0/+1 with no smoothing ramp.
		float rotateInput = Input.GetAxisRaw("Horizontal");
		if (Mathf.Abs(rotateInput) > 0.001f && !isStepping && Time.time >= nextStepAllowedTime)
		{
			float stepDelta = Mathf.Sign(rotateInput) * orbitStepAngle;
			player.StartCoroutine(OrbitStepRoutine(player, stepDelta));
		}

		// T: drag a small object on the floor toward the player.
		if (Input.GetKeyDown(KeyCode.T) && !isDragging)
		{
			TryDrag(player);
		}
	}

	/// <summary>
	/// Position the player at currentAngle around the anchor, facing outward.
	/// Called on entry and every frame the player rotates.
	/// </summary>
	private void ApplyOrbitPosition(PlayerController player)
	{
		float rad = currentAngle * Mathf.Deg2Rad;
		Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * tetherRadius;
		Vector3 targetPos = anchor.position + offset;
		targetPos.y = player.transform.position.y; // preserve current Y (floor height)

		// Use MovePosition so we play nice with physics if the rigidbody isn't fully frozen.
		player.transform.position = targetPos;

		// Face outward from the anchor (back to the pipe).
		Vector3 outward = offset.normalized;
		if (outward.sqrMagnitude > 0.0001f)
		{
			player.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
		}
	}

	private void TryDrag(PlayerController player)
	{
		Pickupable target = FindDragTarget(player);
		if (target == null)
		{
			// Could play a "nothing in reach" SFX or shake here. For now, silent.
			return;
		}

		player.StartCoroutine(DragRoutine(player, target));
	}

	/// <summary>
	/// Forward-cone search for a Pickupable below the mass threshold.
	/// Returns the nearest valid target, or null.
	/// </summary>
	private Pickupable FindDragTarget(PlayerController player)
	{
		Collider[] hits = Physics.OverlapSphere(player.transform.position, dragRange, dragTargetLayer);

		Pickupable best = null;
		float bestDist = float.MaxValue;
		Vector3 forward = player.transform.forward;
		float cosThreshold = Mathf.Cos(dragConeHalfAngle * Mathf.Deg2Rad);

		foreach (Collider hit in hits)
		{
			Pickupable p = hit.GetComponent<Pickupable>();
			if (p == null) continue;
			if (!p.gameObject.activeInHierarchy) continue;

			// Mass check — must have a rigidbody under threshold.
			Rigidbody itemRb = hit.GetComponent<Rigidbody>();
			if (itemRb == null) continue;
			if (itemRb.mass > dragMassThreshold) continue;

			// Cone check — must be within forward cone.
			Vector3 toItem = hit.transform.position - player.transform.position;
			toItem.y = 0f;
			float distFlat = toItem.magnitude;
			if (distFlat < 0.001f) continue;
			float cosAngle = Vector3.Dot(forward, toItem.normalized);
			if (cosAngle < cosThreshold) continue;

			if (distFlat < bestDist)
			{
				best = p;
				bestDist = distFlat;
			}
		}

		return best;
	}

	/// <summary>
	/// Slide the target object toward the player's feet over dragDuration seconds.
	/// At end, the item sits within normal Pick Up range and player can E to grab it.
	/// </summary>
	private IEnumerator DragRoutine(PlayerController player, Pickupable target)
	{
		isDragging = true;

		Rigidbody itemRb = target.GetComponent<Rigidbody>();
		bool wasKinematic = false;
		if (itemRb != null)
		{
			wasKinematic = itemRb.isKinematic;
			itemRb.isKinematic = true; // pause physics so the slide is clean
			itemRb.linearVelocity = Vector3.zero;
			itemRb.angularVelocity = Vector3.zero;
		}

		Vector3 startPos = target.transform.position;
		// End position: in front of the player at floor height, just within Pick Up range.
		Vector3 endPos = player.transform.position + player.transform.forward * dragEndOffset;
		endPos.y = startPos.y; // keep item on floor

		float elapsed = 0f;
		while (elapsed < dragDuration)
		{
			float t = elapsed / dragDuration;
			float eased = 1f - (1f - t) * (1f - t); // ease-out — quick start, slow finish
			target.transform.position = Vector3.Lerp(startPos, endPos, eased);
			elapsed += Time.deltaTime;
			yield return null;
		}

		target.transform.position = endPos;

		if (itemRb != null)
		{
			itemRb.isKinematic = wasKinematic;
		}

		isDragging = false;
	}

	/// <summary>
	/// Rotate around the anchor by stepDelta degrees over orbitStepDuration seconds.
	/// Uses ease-in-out so the step has a "shuffle" feel — deliberate start, deliberate stop.
	/// </summary>
	private IEnumerator OrbitStepRoutine(PlayerController player, float stepDelta)
	{
		isStepping = true;

		float startAngle = currentAngle;
		float endAngle = currentAngle + stepDelta;
		float elapsed = 0f;

		while (elapsed < orbitStepDuration)
		{
			float t = elapsed / orbitStepDuration;
			// Ease-in-out: smoothstep. Slow start, slow end, faster middle.
			float eased = t * t * (3f - 2f * t);
			currentAngle = Mathf.Lerp(startAngle, endAngle, eased);
			ApplyOrbitPosition(player);
			elapsed += Time.deltaTime;
			yield return null;
		}

		currentAngle = endAngle;
		ApplyOrbitPosition(player);

		isStepping = false;
		nextStepAllowedTime = Time.time + orbitStepCooldown;
	}

	public override float GetStruggleModifier()
	{
		return struggleModifier;
	}
}
