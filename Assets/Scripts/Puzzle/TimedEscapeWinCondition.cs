using System.Collections;
using UnityEngine;

/// <summary>
/// Timed-level win condition: the level is complete the moment the player breaks
/// free of their bonds. Same fiction as BondBreakWinCondition (cut = free = done)
/// with the two additions a level that runs a LevelTimer needs:
///
///   1. STOP THE TIMER on escape, FIRST. Without this, a bond-cut that lands on
///      (or just before) the same frame the LevelTimer expires could let
///      FailureLoopController.OnTimerExpired fire AFTER the escape and re-bind a
///      Cassie who already got free. Stopping the timer before anything else
///      makes the escape win the tie.
///
///   2. OPTIONAL closing mutter. If escapeMutter is set, it plays (blocking) the
///      instant Cassie is free, and the level-complete handoff WAITS until the
///      player dismisses it — so the closing beat lands instead of being stepped
///      on by the LEVEL COMPLETE panel. Leave escapeMutter empty and this behaves
///      exactly like BondBreakWinCondition (immediate complete). NOTE: the L6
///      spec's mutter chain (§5) has no success beat; this field is here for a
///      closing line only if you choose to author one.
///
/// Drop on the level's "WinCondition" GameObject INSTEAD of BondBreakWinCondition.
/// Untimed levels keep using BondBreakWinCondition; the LevelTimer null-check here
/// just means this component is safe to reuse on a level that has no timer.
/// </summary>
public class TimedEscapeWinCondition : MonoBehaviour
{
	[Tooltip("The Player whose bond-break triggers level completion. " +
	         "If left empty, will FindFirstObjectByType at Start.")]
	[SerializeField] private PlayerController player;

	[Tooltip("Optional. Cassie's closing line, played (blocking) the moment she " +
	         "is free. The level-complete handoff waits for the player to dismiss " +
	         "it so the beat isn't stepped on by the complete UI. Leave empty to " +
	         "complete immediately, exactly like BondBreakWinCondition. (The L6 " +
	         "spec has no success beat — only fill this if you want one.)")]
	[TextArea(2, 4)]
	[SerializeField] private string escapeMutter;

	// OnPlayerFreed fires from bond.OnBroken, which is itself one-shot — but a
	// future re-bind/refactor bug could in principle re-fire it. Guard so we can
	// never double-complete or double-stop.
	private bool handled;

	void Start()
	{
		if (player == null)
		{
			player = FindFirstObjectByType<PlayerController>();
		}

		if (player != null)
		{
			player.OnPlayerFreed += OnFreed;
		}
		else
		{
			Debug.LogWarning("TimedEscapeWinCondition: no PlayerController found. " +
				"Freed->complete handoff will not fire.");
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
		if (handled) return;
		handled = true;

		// Stop the clock FIRST. A clean escape must beat a same-frame timer
		// expiry, or the failure loop could re-bind a Cassie who already escaped.
		if (LevelTimer.Instance != null && LevelTimer.Instance.IsRunning)
		{
			LevelTimer.Instance.StopTimer();
		}

		// With a closing line: play it (blocking) and complete after the player
		// dismisses it. Without one: complete immediately, like L1-L5.
		if (!string.IsNullOrEmpty(escapeMutter) && MutterSystem.Instance != null)
		{
			MutterSystem.Instance.Play(escapeMutter);
			StartCoroutine(CompleteAfterMutter());
		}
		else
		{
			Complete();
		}
	}

	/// <summary>
	/// Wait out the closing mutter, then complete. The player is intentionally
	/// left enabled until Complete() runs so the dismiss key still works; the
	/// bond is already broken, so no further struggle/movement can affect state
	/// in the gap. yield-one-frame lets IsActive flip true on the reveal frame
	/// before we start polling it.
	/// </summary>
	private IEnumerator CompleteAfterMutter()
	{
		yield return null;
		while (MutterSystem.Instance != null && MutterSystem.Instance.IsActive)
		{
			yield return null;
		}
		Complete();
	}

	private void Complete()
	{
		if (LevelManager.Instance != null)
		{
			LevelManager.Instance.CompleteLevel();
		}
		else
		{
			Debug.LogWarning("TimedEscapeWinCondition: no LevelManager.Instance; " +
				"cannot complete level.");
		}
	}
}
