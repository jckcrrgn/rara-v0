using UnityEngine;

/// <summary>
/// Trigger volume that completes the current level when the player enters it.
///
/// L4 use: place past the van's rear threshold so the level completes when the
/// detective scoots/crawls out of the van after kicking the doors open.
/// Decouples level completion from door animation timing — the win condition
/// becomes "escape the van," which is what it should always have been.
///
/// Generalizes: any level where the win condition is "leave the room" can use
/// this. Drop a trigger collider, attach this script, done.
///
/// Setup:
///   - Empty GameObject with a BoxCollider (or other Collider) marked isTrigger.
///   - Place past the level's exit threshold.
///   - Sized generously enough that a slow-moving floor-bound player can't
///     accidentally cross only halfway and stall.
///   - Player GameObject must have the tag "Player" (or set playerTag below).
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelExitTrigger : MonoBehaviour
{
	[Tooltip("Tag the player GameObject must have for the trigger to fire. Default: 'Player'.")]
	[SerializeField] private string playerTag = "Player";

	[Tooltip("Optional delay (seconds) between trigger fire and level completion. " +
		"Useful if you want a beat for the player to feel like they've escaped " +
		"before the win UI appears. 0 = immediate.")]
	[SerializeField] private float completionDelay = 0.4f;

	private bool hasFired = false;

	private void Reset()
	{
		// When this script is added to a GameObject in the editor, force its
		// collider into trigger mode. Saves a forgotten-isTrigger debugging session.
		Collider col = GetComponent<Collider>();
		if (col != null) col.isTrigger = true;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (hasFired) return;
		if (!other.CompareTag(playerTag)) return;

		hasFired = true;

		if (completionDelay > 0f)
		{
			Invoke(nameof(FireCompletion), completionDelay);
		}
		else
		{
			FireCompletion();
		}
	}

	private void FireCompletion()
	{
		if (LevelManager.Instance != null)
		{
			LevelManager.Instance.CompleteLevel();
		}
		else
		{
			Debug.LogWarning($"{name}: LevelExitTrigger fired but LevelManager.Instance is null.");
		}
	}
}
