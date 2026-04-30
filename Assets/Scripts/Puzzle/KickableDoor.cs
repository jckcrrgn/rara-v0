using System.Collections;
using UnityEngine;

/// <summary>
/// A door the player kicks open. First concrete Kickable.
///
/// L4 setup:
///   - Place at the back of the van.
///   - Set requiredForce ~3 (three free-leg kicks, or six floor-bound kicks).
///   - Assign leftPivot, rightPivot for double-door swing.
///   - Assign kickZone — an empty Transform sitting at the door's interior face.
///     The player must be inside kickZoneRadius AND have their feet pointed at
///     the door for the kick to register. Otherwise it's a wall-thud.
///
/// Why position-gated: per L4 redesign, bonds on this level are unbreakable
/// bare-hands. The puzzle is "tape your way close enough, orient correctly,
/// kick." This separates the verb cleanly from Struggle.
/// </summary>
public class KickableDoor : Kickable
{
	[Header("Position Gate")]
	[Tooltip("Empty Transform marking the spot the player must reach to kick. " +
		"Sit it at the door's interior face, roughly at floor level.")]
	[SerializeField] private Transform kickZone;
	[Tooltip("How close the player must be to kickZone (world-space distance).")]
	[SerializeField] private float kickZoneRadius = 1.0f;
	[Tooltip("Dot threshold for player's feet-vector vs. direction-to-door. " +
		"0.7 = ~45° cone. Higher is stricter.")]
	[Range(0f, 1f)]
	[SerializeField] private float feetDotThreshold = 0.7f;

	[Header("Door Animation")]
	[SerializeField] private Transform leftPivot;
	[SerializeField] private Transform rightPivot;
	[SerializeField] private float openAngle = 95f;
	[SerializeField] private float openDuration = 0.4f;

	[Header("SFX")]
	[Tooltip("Per-kick thud. Plays each time a kick registers but the door isn't open yet.")]
	[SerializeField] private AudioClip kickThudClip;
	[Tooltip("Big door-burst SFX when the door finally opens.")]
	[SerializeField] private AudioClip kickOpenClip;

	/// <summary>
	/// Position gate. Player must be in the kick zone AND have their feet oriented
	/// toward the door (so a "kick" reads as kicking forward into it from where
	/// the legs are pointed).
	///
	/// Uses the restraint's GetKickDirection rather than -transform.forward directly,
	/// because feet/forward relationship varies by restraint:
	///   - Inch (FloorRestraint, prone, head-leading): feet = -forward
	///   - Scoot (FloorRestraint, supine, feet-leading): feet = +forward
	///   - Chair / Cuffed / Hanging: feet = -forward (default)
	///
	/// Note: in inch mode the forward vector includes a small twist offset
	/// (max ~12° during shoulder-lead). The threshold of 0.7 has slack for that.
	/// </summary>
	public override bool CanBeKicked(PlayerController player)
	{
		if (!base.CanBeKicked(player)) return false;
		if (kickZone == null) return true; // No gate configured — accept always.

		// Proximity check.
		float dist = Vector3.Distance(player.transform.position, kickZone.position);
		if (dist > kickZoneRadius) return false;

		// Orientation check: player's feet vector should point roughly at the door.
		Vector3 toDoor = (transform.position - player.transform.position);
		toDoor.y = 0f;
		if (toDoor.sqrMagnitude < 0.0001f) return true; // Standing on the gate; accept.
		toDoor.Normalize();

		Vector3 feet = player.CurrentRestraint != null
			? player.CurrentRestraint.GetKickDirection(player)
			: -player.transform.forward; // Fallback if somehow no restraint is set.

		float dot = Vector3.Dot(feet, toDoor);
		return dot >= feetDotThreshold;
	}

	protected override void OnKickRegistered(PlayerController player, float force)
	{
		if (AudioManager.Instance != null && kickThudClip != null)
		{
			AudioManager.Instance.PlaySFX(kickThudClip, 1f, Random.Range(0.95f, 1.05f));
		}
		// TODO (post-v0 polish): brief "give" animation on the door per kick before it bursts open.
	}

	protected override void OnFullyKicked(PlayerController player)
	{
		StartCoroutine(KickOpen());
	}

	private IEnumerator KickOpen()
	{
		if (AudioManager.Instance != null && kickOpenClip != null)
		{
			AudioManager.Instance.PlaySFX(kickOpenClip, 1f, 1f);
		}

		if (leftPivot != null && rightPivot != null)
		{
			Quaternion leftStart = leftPivot.localRotation;
			Quaternion rightStart = rightPivot.localRotation;
			Quaternion leftEnd = leftStart * Quaternion.Euler(0f, -openAngle, 0f);
			Quaternion rightEnd = rightStart * Quaternion.Euler(0f, openAngle, 0f);

			float elapsed = 0f;
			while (elapsed < openDuration)
			{
				float t = elapsed / openDuration;
				float eased = 1f - (1f - t) * (1f - t);
				leftPivot.localRotation = Quaternion.Slerp(leftStart, leftEnd, eased);
				rightPivot.localRotation = Quaternion.Slerp(rightStart, rightEnd, eased);
				elapsed += Time.deltaTime;
				yield return null;
			}
			leftPivot.localRotation = leftEnd;
			rightPivot.localRotation = rightEnd;
		}

		yield return new WaitForSeconds(0.5f);

		if (LevelManager.Instance != null)
		{
			LevelManager.Instance.CompleteLevel();
		}
	}
}
