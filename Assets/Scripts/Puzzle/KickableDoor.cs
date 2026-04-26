using System.Collections;
using UnityEngine;

/// <summary>
/// A door the player kicks open by winding up Struggle while bound-free
/// and adjacent to it. First concrete use of the Struggle-as-windup pattern
/// that L15 (kick-the-guard) will reuse with a different target.
///
/// Usage on L4:
///   - Place at the back of the van.
///   - Set requiredWindup to ~3 (three Struggle taps once bonds are broken).
///   - Assign leftPivot and rightPivot for double-door swing.
///   - Doors swing open + the level completes via LevelManager.
///
/// Pre-bond-break behavior: Struggle on this door does nothing
/// (you're still tied up — kicking is for free legs). The bond must be broken first.
/// </summary>
public class KickableDoor : InteractableBase
{
	[Header("Windup")]
	[Tooltip("How many Struggle taps to kick the door open after bonds are broken.")]
	[SerializeField] private int requiredWindup = 3;

	[Header("Door Animation")]
	[Tooltip("Pivot for the LEFT door panel. Sits at the left edge of the doorway.")]
	[SerializeField] private Transform leftPivot;
	[Tooltip("Pivot for the RIGHT door panel. Sits at the right edge of the doorway.")]
	[SerializeField] private Transform rightPivot;
	[Tooltip("How far each door swings open (degrees). Pivots rotate in opposite directions.")]
	[SerializeField] private float openAngle = 95f;
	[SerializeField] private float openDuration = 0.4f;

	[Header("SFX")]
	[SerializeField] private AudioClip windupClip;
	[SerializeField] private AudioClip kickOpenClip;

	[Header("Refs")]
	[Tooltip("The Bond on the player. Door only kicks once this Bond is broken.")]
	[SerializeField] private Bond playerBond;

	private int currentWindup = 0;
	private bool isOpen = false;

	/// <summary>
	/// Called by PlayerController.TryStruggle when this door is the nearest interactable
	/// AND the player is free of bonds. Treats Struggle as windup toward a kick.
	/// </summary>
	public void OnWindup(PlayerController player)
	{
		if (isOpen) return;

		// Gate: must be free of bonds. Pre-break, the door ignores Struggle.
		if (playerBond != null && !playerBond.IsBroken)
		{
			return;
		}

		currentWindup++;
		if (AudioManager.Instance != null && windupClip != null)
		{
			AudioManager.Instance.PlaySFX(windupClip, 1f, Random.Range(0.95f, 1.05f));
		}

		if (currentWindup >= requiredWindup)
		{
			StartCoroutine(KickOpen());
		}
	}

	private IEnumerator KickOpen()
	{
		isOpen = true;

		if (AudioManager.Instance != null && kickOpenClip != null)
		{
			AudioManager.Instance.PlaySFX(kickOpenClip, 1f, 1f);
		}

		if (leftPivot != null && rightPivot != null)
		{
			Quaternion leftStart = leftPivot.localRotation;
			Quaternion rightStart = rightPivot.localRotation;
			// Opposite signs so the doors swing AWAY from each other (outward).
			// If on playtest they clip into each other, swap the signs.
			Quaternion leftEnd = leftStart * Quaternion.Euler(0f, -openAngle, 0f);
			Quaternion rightEnd = rightStart * Quaternion.Euler(0f, openAngle, 0f);

			float elapsed = 0f;
			while (elapsed < openDuration)
			{
				float t = elapsed / openDuration;
				// Ease-out: doors snap fast, slow at the end.
				float eased = 1f - (1f - t) * (1f - t);
				leftPivot.localRotation = Quaternion.Slerp(leftStart, leftEnd, eased);
				rightPivot.localRotation = Quaternion.Slerp(rightStart, rightEnd, eased);
				elapsed += Time.deltaTime;
				yield return null;
			}
			leftPivot.localRotation = leftEnd;
			rightPivot.localRotation = rightEnd;
		}

		// Small beat before completing the level so the kick reads.
		yield return new WaitForSeconds(0.5f);

		if (LevelManager.Instance != null)
		{
			LevelManager.Instance.CompleteLevel();
		}
	}
}
