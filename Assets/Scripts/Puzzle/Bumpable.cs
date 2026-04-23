using UnityEngine;
using UnityEngine.Events;

// Attach to any object that should react when the player bumps into it
// hard enough. Fires OnBumped once, then disables itself unless retriggerable.
//
// Decouples "detect the bump" from "do the thing" -- listeners handle the
// reaction via the UnityEvent (Drawer tween, Shelf drop, etc).
[RequireComponent(typeof(Collider))]
public class Bumpable : MonoBehaviour
{
	[Header("Trigger Conditions")]
	[Tooltip("Minimum collision relative velocity magnitude to count as a bump. " +
	         "Tune by watching the logged impulse values in the console.")]
	[SerializeField] private float minBumpForce = 1.5f;

	[Tooltip("If true, the player must be the collider. Leave on for v0.")]
	[SerializeField] private bool requirePlayer = true;

	[Tooltip("If true, this can be bumped more than once. Default: single-shot.")]
	[SerializeField] private bool retriggerable = false;

	[Header("Feedback")]
	[SerializeField] private AudioClip bumpClip;

	[Header("Events")]
	public UnityEvent OnBumped;

	private bool hasTriggered = false;

	void OnCollisionEnter(Collision collision)
	{
		if (hasTriggered && !retriggerable) return;

		if (requirePlayer && collision.gameObject.GetComponent<PlayerController>() == null)
			return;

		float force = collision.relativeVelocity.magnitude;
		if (force < minBumpForce)
		{
			// Log lightly during tuning; comment out once dialed in.
			Debug.Log($"Bumpable ({name}): bump too soft ({force:F2} < {minBumpForce}).");
			return;
		}

		hasTriggered = true;
		Debug.Log($"Bumpable ({name}): triggered at force {force:F2}.");

		if (AudioManager.Instance != null && bumpClip != null)
			AudioManager.Instance.PlaySFX(bumpClip, 1f, Random.Range(0.95f, 1.05f));

		OnBumped?.Invoke();
	}
}
