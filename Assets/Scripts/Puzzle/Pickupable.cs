using UnityEngine;

// Base class for objects the player can carry.
public class Pickupable : InteractableBase
{
	[Header("Pickup Settings")]
	[SerializeField] private string itemName = "Item";

	public string ItemName => itemName;

	public override void OnPickUp(PlayerController player)
	{
		player.HoldItem(this);
		// Hide the world version while held — visually represented by player holding it
		gameObject.SetActive(false);
	}

	// Called by player when they drop or use up the item
	public void DropFromPlayer()
	{
		gameObject.SetActive(true);
	}
}