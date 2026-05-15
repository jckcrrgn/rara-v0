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
///      chairRestraint field.
///   5. Reference the marker in ChairRestraint.tipMarkers[].
///
/// LAYER SETUP (REQUIRED — Day 37):
///   Floors must be on a layer named "Ground". The default groundLayer below
///   resolves to LayerMask.GetMask("Ground") at script load; if the layer
///   doesn't exist, the mask is 0 and tip-detection silently stops working —
///   which is the failure mode we want for a destructive state change. The
///   original `~0` ("everything") default caused the Day 37 bug where a
///   mutter trigger volume registered as ground, flipping the player to
///   FloorRestraint on L1 the moment they entered the trigger.
///
///   To set up a new scene: create a "Ground" layer in Project Settings →
///   Tags and Layers, then set the Floor GameObject's layer to Ground.
///   Props, walls, mutter triggers, jostleables — anything that isn't
///   actually the floor — must NOT be on the Ground layer.
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

	[Tooltip("Layers that count as 'the ground' for tip detection. Defaults " +
		"to the 'Ground' layer at script load if left as 0 in the inspector. " +
		"Floors must be assigned to the Ground layer in each scene; props, " +
		"walls, mutter triggers, and jostleables must NOT be. If the Ground " +
		"layer doesn't exist, the mask resolves to 0 and tip-detection is " +
		"silently disabled — which is the desired failure mode for a " +
		"destructive state change (Day 37 lesson).")]
	[SerializeField] private LayerMask groundLayer = 0;

	[Tooltip("Reference to the PlayerController on the parent. Pre-wired so we " +
		"don't have to GetComponentInParent every collision frame.")]
	[SerializeField] private PlayerController player;

	void Awake()
	{
		// If the inspector left groundLayer at its default 0 (or the developer
		// forgot to set it), resolve to the Ground layer at load. This is a
		// soft default — explicit inspector values (including deliberately
		// empty masks) are preserved. The check is `value == 0` rather than
		// "is this the default sentinel" because Unity has no way to express
		// the latter in serialized fields; an explicit "no layers" mask would
		// be rare and is overridden harmlessly here.
		if (groundLayer.value == 0)
		{
			groundLayer = LayerMask.GetMask("Ground");
		}
	}

	void OnTriggerEnter(Collider other)
	{
		if (chairRestraint == null || player == null) return;

		// LayerMask check: bitshift the other's layer and AND against our mask.
		if ((groundLayer.value & (1 << other.gameObject.layer)) == 0) return;

		chairRestraint.OnSideMarkerHitGround(player);
	}
}
