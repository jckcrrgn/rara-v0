using UnityEngine;

public class Nail : InteractableBase
{
	// Nails give a moderate struggle boost when struggled against
	public override int StruggleModifier => 5;

	public override void OnStruggle(PlayerController player)
	{
		// The nail itself doesn't track progress anymore.
		// The player's bonds track total struggle progress.
		Debug.Log("Struggling against the nail.");
	}
}