using UnityEngine;

/// <summary>
/// Detects the lamp smashing — i.e. the lamp's Rigidbody striking another
/// collider with enough relative velocity to count as a "shatter" — and
/// fires the L6 soft timer.
///
/// WHY VELOCITY-GATED
/// ------------------
/// The lamp lives on the nightstand and will register collisions constantly
/// during normal play: nightstand jostle nudges the lamp a millimeter, the
/// lamp wiggles against its resting collider, etc. Without a velocity gate,
/// every micro-collision would trigger the timer and break the "loud event"
/// premise of §6.
///
/// IDEMPOTENT BY TWO PATHS
/// -----------------------
/// 1. Local: `hasSmashed` short-circuits after the first qualifying impact,
///    so this component never fires twice in a level lifetime.
/// 2. System: LevelTimer.StartTimer is itself idempotent — if the chair tip
///    fired first, our call is a silent no-op. This is the "first occurrence
///    wins" mechanic from §6, defense in depth.
///
/// PLACEMENT
/// ---------
/// Attach to the same GameObject as the lamp's Rigidbody + Collider. Needs
/// physical collision to fire — `OnCollisionEnter`, not trigger. Lamp should
/// be a non-kinematic Rigidbody so jostle-and-fall works.
///
/// OPTIONAL SFX
/// ------------
/// `smashClip` is the glass-break sound; routed through AudioManager 2D
/// channel so it reads as a Cassie-perspective event, not diegetic-spatial.
/// The guard mutter at the 50% threshold is the diegetic counterpart.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LampSmashTrigger : MonoBehaviour
{
	[Header("Smash Detection")]
	[Tooltip("Minimum relative velocity (m/s) at impact to count as a smash. " +
		"Below this, the collision is treated as a jostle and ignored. " +
		"Tune in playtest — first guess 2.5 covers a fall from nightstand " +
		"height under normal gravity without false-firing on bumps.")]
	[SerializeField] private float smashVelocityThreshold = 2.5f;

	[Header("SFX (optional)")]
	[Tooltip("Glass-break sound, played once on smash. Routed through " +
		"AudioManager.PlaySFX (2D, non-diegetic) — the diegetic guard " +
		"response is the mutter at the 50% timer threshold, not this " +
		"clip. Leave empty if no clip wired yet.")]
	[SerializeField] private AudioClip smashClip;

	[Tooltip("Volume for the smash clip. Default 1.0.")]
	[SerializeField] private float smashVolume = 1.0f;

	private bool hasSmashed;

	void OnCollisionEnter(Collision collision)
	{
		if (hasSmashed) return;

		// relativeVelocity is the relative speed at the moment of impact;
		// magnitude collapses it to a scalar comparable against the threshold.
		// This is the standard Unity idiom for "how hard did this hit?"
		float impactSpeed = collision.relativeVelocity.magnitude;
		if (impactSpeed < smashVelocityThreshold) return;

		hasSmashed = true;

		Debug.Log($"[LampSmashTrigger] Smash detected. Impact speed: " +
			$"{impactSpeed:F2} m/s (threshold {smashVelocityThreshold}).");

		if (LevelTimer.Instance != null)
		{
			LevelTimer.Instance.StartTimer();
		}
		else
		{
			Debug.LogWarning("[LampSmashTrigger] Smash detected but no " +
				"LevelTimer.Instance exists in this scene. Timer will not " +
				"start. This is fine for L1–L5 (no timer by design) but " +
				"means L6 is misconfigured if you see this in L6.");
		}

		if (smashClip != null && AudioManager.Instance != null)
		{
			AudioManager.Instance.PlaySFX(smashClip, smashVolume, 1f);
		}
	}

	[ContextMenu("Debug: Force Smash")]
	private void DebugForceSmash()
	{
		if (hasSmashed)
		{
			Debug.Log("[LampSmashTrigger] Already smashed. Ignoring.");
			return;
		}
		hasSmashed = true;
		Debug.Log("[LampSmashTrigger] Debug-forced smash.");
		if (LevelTimer.Instance != null) LevelTimer.Instance.StartTimer();
	}
}
