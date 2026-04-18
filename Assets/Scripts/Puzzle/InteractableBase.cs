using UnityEngine;

// Base class for any object the player can interact with.
public abstract class InteractableBase : MonoBehaviour
{
	[Header("Interaction Settings")]
	[SerializeField] protected float interactionRange = 1.5f;

	public float InteractionRange => interactionRange;

	public virtual void OnStruggle(PlayerController player) { }
	public virtual void OnPickUp(PlayerController player) { }
	public virtual void OnPlayerInRange(PlayerController player) { }
}