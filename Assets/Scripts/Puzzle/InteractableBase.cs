using UnityEngine;

// Base class for any object the player can interact with.
// Specific interactables (Nail, Phone, Lever, etc.) inherit from this
// and override OnStruggle, OnPickUp, or OnCallOut as needed.
public abstract class InteractableBase : MonoBehaviour
{
	[Header("Interaction Settings")]
	[SerializeField] protected float interactionRange = 1.5f;

	public float InteractionRange => interactionRange;

	// Each verb has a virtual method. Subclasses override the ones they care about.
	public virtual void OnStruggle(PlayerController player) { }
	public virtual void OnPickUp(PlayerController player) { }
	public virtual void OnCallOut(PlayerController player) { }

	// Called every frame the player is within range. Useful for highlighting.
	public virtual void OnPlayerInRange(PlayerController player) { }
}