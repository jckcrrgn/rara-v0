using System.Collections;
using UnityEngine;

/// <summary>
/// Component on the guard's GameObject. Receives the Strike verb from the
/// player and drives the stagger → Downed sequence.
///
/// Sibling to Kickable in concept: both are "interactable targets that
/// receive a player verb and produce a consequence." The difference is that
/// Strike is a deliberate, context-gated payoff (only available during
/// LeanIn, only with a held weapon) rather than a physics impulse available
/// at any time.
///
/// STRIKE GATE
/// -----------
/// CanBeStruck() is the single gate. PlayerController.TryStrike() calls it
/// before doing anything. Two conditions must both be true:
///   1. GuardController.CurrentState == LeanIn  (he's close and unaware)
///   2. The player is holding a weapon           (checked via player.GetHeldItem()
///                                                and Pickupable.IsWeapon)
///
/// If either fails, TryStrike is a no-op (no feedback yet — that's a
/// polish pass once the verb is proven).
///
/// SEQUENCE
/// --------
/// OnStruck → stagger hold (staggerDuration) → GuardController.OnGuardDowned()
/// → optional down SFX → done. GuardController.OnGuardDowned() stops the
/// cycle and sets the terminal Downed state. This component just owns the
/// moment between "strike lands" and "guard is fully down."
///
/// PLACEMENT
/// ---------
/// Drop on the guard's GameObject alongside whatever visual/collider
/// represents him. In the graybox, that's the guard cube. In the final
/// scene, the same component survives onto the character model.
/// </summary>
public class StrikeableGuard : MonoBehaviour
{
	[Header("Timing")]
	[Tooltip("How long the guard staggers before fully going down. " +
		"Long enough to read as impact; short enough not to drag. 0.8–1.2s.")]
	[SerializeField] private float staggerDuration = 1.0f;

	[Header("SFX")]
	[Tooltip("Plays the moment the strike lands — impact thud, Cassie effort grunt, " +
		"or both layered. Optional but highly recommended; this is the payoff beat.")]
	[SerializeField] private AudioClip strikeImpactClip;

	[Tooltip("Plays when the guard fully goes down (body hitting floor, etc.). " +
		"Optional. Layered on top of stagger — fires after staggerDuration.")]
	[SerializeField] private AudioClip guardDownClip;

	[Range(0f, 1f)]
	[SerializeField] private float sfxVolume = 1f;

	[Header("Mutter — Strike Beat")]
	[Tooltip("Cassie's line the moment the strike lands — the catharsis beat. " +
		"Plays immediately on strike, before the guard is fully down. " +
		"Speaker: Cassie. Leave empty to skip.")]
	[TextArea(2, 4)]
	[SerializeField] private string strikeMutterLine = "";

	[Header("Debug")]
	[SerializeField] private bool verboseLogging = true;

	// Re-entry guard. Once struck, the guard is going down — a second strike
	// call (theoretically impossible since CanBeStruck returns false after the
	// first hit, but belt-and-suspenders) is ignored.
	private bool hasBeenStruck = false;

	/// <summary>
	/// Gate checked by PlayerController.TryStrike() before doing anything.
	/// True only when the guard is in LeanIn AND hasn't already been struck.
	/// PlayerController also checks that the player is holding a weapon —
	/// that gate lives there, not here, because it's about the player's state.
	/// </summary>
	public bool CanBeStruck()
	{
		if (hasBeenStruck) return false;
		if (GuardController.Instance == null) return false;
		return GuardController.Instance.CurrentState == GuardController.GuardState.LeanIn;
	}

	/// <summary>
	/// Called by PlayerController.TryStrike() when all gates pass.
	/// Drives the stagger → down sequence and notifies GuardController.
	/// </summary>
	public void OnStruck(PlayerController player)
	{
		if (hasBeenStruck) return;
		hasBeenStruck = true;

		Log("Strike landed. Starting stagger sequence.");
		StartCoroutine(StaggerSequence(player));
	}

	private IEnumerator StaggerSequence(PlayerController player)
	{
		// Impact SFX — the moment the blow lands.
		if (AudioManager.Instance != null && strikeImpactClip != null)
		{
			AudioManager.Instance.PlaySFX(strikeImpactClip, sfxVolume, 1f);
		}

		// Cassie's catharsis line — fires immediately on strike, not after.
		// She says it as she swings, not after he's already on the floor.
		if (MutterSystem.Instance != null && !string.IsNullOrEmpty(strikeMutterLine))
		{
			MutterSystem.Instance.Play(strikeMutterLine, MutterSystem.Speaker.Cassie);
		}

		// Hold the stagger beat. Guard is mid-fall; player sees the consequence.
		yield return new WaitForSeconds(staggerDuration);

		// Down SFX — guard hits the floor.
		if (AudioManager.Instance != null && guardDownClip != null)
		{
			AudioManager.Instance.PlaySFX(guardDownClip, sfxVolume, 1f);
		}

		// Notify GuardController — stops the cycle, sets Downed state.
		if (GuardController.Instance != null)
		{
			GuardController.Instance.OnGuardDowned();
		}

		Log("Stagger complete. Guard is down.");
	}

	private void Log(string msg)
	{
		if (verboseLogging) Debug.Log($"[StrikeableGuard] {msg}");
	}
}
