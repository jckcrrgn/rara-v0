using UnityEngine;

/// <summary>
/// Side-marker component for chair-tip detection. Lives on a child GameObject
/// of the chair, positioned at a point that would touch the ground when the
/// chair is fully tipped on its side. Typically two of these per chair (one
/// per side), but the count is not enforced — ChairRestraint.OnSideMarkerHitGround
/// is idempotent.
///
/// Setup (Unity-side):
///   1. Add an empty child GameObject to the chair (the player GameObject).
///   2. Position it at the spot that would contact the floor when the chair
///      is laid on its side (typically out past the side of the seat, at
///      seat-back height).
///   3. Add a Collider (Sphere or Box, small radius). Set isTrigger = true.
///   4. Add this component. Drag the parent's ChairRestraint into the
///      chairRestraint field. Set groundLayer to whatever layer your floor
///      uses (Default is usually fine for now).
///   5. Reference the marker in ChairRestraint.tipMarkers[].
///
/// Why a separate component instead of polling: collision events are
/// frame-perfect and event-driven, and they piggyback on Unity's physics
/// engine doing the spatial check we'd otherwise have to do manually. The
/// alternative — OverlapSphere on every Update — would either waste frames
/// or miss the moment of contact.
///
/// Why isTrigger=true: a non-trigger collider on the marker would interact
/// physically with the floor and could resist the tip itself. The trigger
/// just reports the event without exerting force.
/// </summary>
public class ChairTipMarker : MonoBehaviour
{
	[Tooltip("The ChairRestraint that owns this marker. Should be on the player " +
		"(parent) GameObject. The marker calls back into this restraint when " +
		"it hits the ground.")]
	[SerializeField] private ChairRestraint chairRestraint;

	[Tooltip("Layers that count as 'the ground' for tip detection. Default to " +
		"everything until you have a dedicated Floor layer; tighten later to " +
		"avoid spurious triggers off props.")]
	[SerializeField] private LayerMask groundLayer = ~0;

	[Tooltip("Reference to the PlayerController on the parent. Pre-wired so we " +
		"don't have to GetComponentInParent every collision frame.")]
	[SerializeField] private PlayerController player;

	void OnTriggerEnter(Collider other)
	{
		if (chairRestraint == null || player == null) return;

		// LayerMask check: bitshift the other's layer and AND against our mask.
		if ((groundLayer.value & (1 << other.gameObject.layer)) == 0) return;

		chairRestraint.OnSideMarkerHitGround(player);
	}
}
