using UnityEngine;

public class Nail : InteractableBase
{
	[Header("Nail Settings")]
	[SerializeField] private int strugglesRequired = 5;
	[SerializeField] private int strugglesUsed = 0;

	public override void OnStruggle(PlayerController player)
	{
		strugglesUsed++;
		Debug.Log($"Struggle against nail: {strugglesUsed}/{strugglesRequired}");

		if (strugglesUsed >= strugglesRequired)
		{
			EscapeRope();
		}
	}

	void EscapeRope()
	{
		Debug.Log("ESCAPED! Rope cut on the nail.");
		if (LevelManager.Instance != null)
		{
			LevelManager.Instance.CompleteLevel();
		}
	}
}