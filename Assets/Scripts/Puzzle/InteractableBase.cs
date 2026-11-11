using UnityEngine;

// Base class for any object the player can interact with.
public abstract class InteractableBase : MonoBehaviour
{
	[Header("Interaction Settings")]
	[SerializeField] protected float interactionRange = 1.5f;

	public float InteractionRange => interactionRange;

	public virtual void OnStruggle(PlayerController player) { }
	public virtual void OnPickUp(PlayerController player) { }

	/// <summary>Day 216: does the pick-up press (A / E) do anything on this object?
	/// Only these compete for it, so a table edge or a no-op drawer can't be "nearest"
	/// and swallow the press meant for the bottle beside it.</summary>
	public virtual bool AcceptsPickUp => false;
	public virtual void OnPlayerInRange(PlayerController player) { }
}