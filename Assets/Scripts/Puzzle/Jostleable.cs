using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cumulative-bump trigger. Sibling to Bumpable.
///
/// Bumpable: discrete. "Knock the shelf hard enough -> stuff falls off." One event.
/// Jostleable: cumulative. "Knock the desk hard enough times -> drawer pops."
///   Each bump shakes a stuck drawer runner slightly looser; loosening doesn't
///   reverse on its own. So the accumulator just adds. No decay.
///
/// Why no decay: the puzzle on L3 is "figure out you can bump the desk to open
/// the drawer." Once the player has that insight, they should be able to bump
/// four times at their own pace. Decay would punish thinking between bumps,
/// which isn't the feel we want -- and there's no diegetic story for it either
/// (a real desk doesn't un-jostle itself).
///
/// Why impulse rather than displacement:
///   Drawers pop when shaken loose, not when their desk ends up in a particular
///   spot. Where the desk drifts to is incidental -- the diegetic readout of the
///   same collisions that drive the accumulator, but not a cause of the open.
///
/// Requires a non-kinematic Rigidbody on the same object so collision.impulse
/// returns a meaningful value.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class Jostleable : MonoBehaviour
{
	[Header("Trigger Conditions")]
	[Tooltip("Total accumulated impulse needed to fire OnJostleComplete. " +
	         "Tune by watching the logged values in the console while bumping.")]
	[SerializeField] private float requiredJostle = 8f;

	[Tooltip("Minimum per-collision impulse to register at all. Filters out " +
	         "incidental brushes from contributing to the accumulator.")]
	[SerializeField] private float minImpulse = 0.5f;

	[Tooltip("If true, the player must be the collider. Leave on for v0.")]
	[SerializeField] private bool requirePlayer = true;

	[Header("Feedback")]
	[Tooltip("Played on each registering bump. Volume and pitch scale with " +
	         "current progress (0..1) -- subtle at first, straining near threshold.")]
	[SerializeField] private AudioClip creakClip;

	[Tooltip("Volume at progress=0 and progress=1. Linear interpolation between.")]
	[SerializeField] private Vector2 creakVolumeRange = new Vector2(0.3f, 1.0f);

	[Tooltip("Pitch at progress=0 and progress=1. Lower pitch as it strains.")]
	[SerializeField] private Vector2 creakPitchRange = new Vector2(1.05f, 0.85f);

	[Header("Events")]
	public UnityEvent<float> OnJostleProgress;
	public UnityEvent OnJostleComplete;

	private float jostle = 0f;
	private bool hasFired = false;

	void OnCollisionEnter(Collision collision)
	{
		if (hasFired) return;

		if (requirePlayer && collision.gameObject.GetComponent<PlayerController>() == null)
			return;

		// collision.impulse is the actual physics impulse, not a velocity proxy.
		// Requires both bodies non-kinematic; we enforce a Rigidbody on this object,
		// and the player has one.
		float impulse = collision.impulse.magnitude;
		if (impulse < minImpulse)
		{
			// Light log during tuning; comment out once dialed in.
			Debug.Log($"Jostleable ({name}): impulse too soft ({impulse:F2} < {minImpulse}).");
			return;
		}

		jostle += impulse;
		float progress = Mathf.Clamp01(jostle / requiredJostle);

		Debug.Log($"Jostleable ({name}): +{impulse:F2}, total {jostle:F2} ({progress * 100f:F0}%).");

		PlayCreak(progress);

		OnJostleProgress?.Invoke(progress);

		if (jostle >= requiredJostle)
		{
			hasFired = true;
			Debug.Log($"Jostleable ({name}): COMPLETE.");
			OnJostleComplete?.Invoke();
		}
	}

	private void PlayCreak(float progress)
	{
		if (AudioManager.Instance == null || creakClip == null) return;

		float volume = Mathf.Lerp(creakVolumeRange.x, creakVolumeRange.y, progress);
		float pitch = Mathf.Lerp(creakPitchRange.x, creakPitchRange.y, progress)
		            * Random.Range(0.97f, 1.03f);
		AudioManager.Instance.PlaySFX(creakClip, volume, pitch);
	}
}
