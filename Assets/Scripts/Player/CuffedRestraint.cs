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
	[SerializeField] private float tetherRadius = 0.45f;

	[Header("Rotation")]
	[Tooltip("Degrees per second the player rotates around the anchor with A/D.")]
	[SerializeField] private float orbitSpeed = 60f;

	[Header("Drag Verb")]
	[Tooltip("Forward cone reach distance for the Drag verb.")]
	[SerializeField] private float dragRange = 2.5f;
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

	public override void OnExit()
	{
		// Restore default rigidbody constraints (freeze rotation X/Z only, like a
		// standing character). The next restraint's OnEnter can override if needed.
		// Note: we don't have a reference to the player here per the base class signature.
		// If this becomes a problem, we can extend RestraintBase.OnExit to take a player.
		// For now, restraints that need to clean up rb constraints can do so in their
		// own OnEnter when becoming the new active restraint.
	}

	public override void HandleMovementInput(PlayerController player)
	{
		if (anchor == null)
		{
			Debug.LogWarning("CuffedRestraint.HandleMovementInput: anchor is null, returning early");
			return;
		}

		// A/D: orbit around the anchor.
		float rotateInput = Input.GetAxis("Horizontal");
		if (Mathf.Abs(rotateInput) > 0.001f)
		{
			currentAngle += rotateInput * orbitSpeed * Time.deltaTime;
			ApplyOrbitPosition(player);
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

	public override float GetStruggleModifier()
	{
		return struggleModifier;
	}
}
