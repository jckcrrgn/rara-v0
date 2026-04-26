using UnityEngine;

/// <summary>
/// L1-L3 win condition: the level is complete the moment the player breaks
/// free of their bonds. Drop this on a "WinCondition" GameObject in any
/// level whose puzzle ends at the bond break.
///
/// L4+ levels DO NOT use this — their win conditions are owned by the puzzle
/// itself (e.g. KickableDoor calls LevelManager.CompleteLevel directly when
/// kicked open).
///
/// This decoupling lets PlayerController stay agnostic about how levels end.
/// </summary>
public class BondBreakWinCondition : MonoBehaviour
{
	[Tooltip("The Player whose bond-break triggers level completion. " +
	         "If left empty, will FindObjectOfType at Start.")]
	[SerializeField] private PlayerController player;

	void Start()
	{
		if (player == null)
		{
			player = FindObjectOfType<PlayerController>();
		}

		if (player != null)
		{
			player.OnPlayerFreed += OnFreed;
		}
		else
		{
			Debug.LogWarning("BondBreakWinCondition: no PlayerController found.");
		}
	}

	void OnDestroy()
	{
		if (player != null)
		{
			player.OnPlayerFreed -= OnFreed;
		}
	}

	void OnFreed()
	{
		if (LevelManager.Instance != null)
		{
			LevelManager.Instance.CompleteLevel();
		}
	}
}
