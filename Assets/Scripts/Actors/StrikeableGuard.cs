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
/// OnStruck → freeze guard movement → crumple (runs across staggerDuration)
/// → GuardController.OnGuardDowned() → optional down SFX → done.
///
/// CRUMPLE (Day 169)
/// -----------------
/// The guard is one unrigged mesh (FBX checked Day 168), so no ragdoll. The
/// crumple is procedural, on the guard body transform: a short buckle (sink +
/// squash), then a topple AWAY from Cassie with an ease-in so it reads as
/// falling, not rotating. Runs in parallel with the stagger hold and ends
/// exactly when it does, so Downed still fires at the same moment.
///
/// GuardController.FreezeMovement() is called first. Without it, a strike that
/// lands mid-walk leaves his MoveBodyAtSpeed / StepTurn coroutines running
/// until OnGuardDowned, and they fight the crumple. Freeze stops movement but
/// leaves the state at LeanIn, so Downed timing — and everything hung off it,
/// victory line included — is unchanged.
///
/// PIVOT ASSUMPTION: the topple rotates about the body transform's origin. If
/// the origin is at his feet, he tips over cleanly. If it's at his centre, his
/// feet will swing up — fix with a feet-level empty as crumpleBody's parent,
/// not by changing the maths.
///
/// PLACEMENT
/// ---------
/// Drop on the guard's GameObject alongside whatever visual/collider
/// represents him. The same component survives onto the character model.
/// </summary>
public class StrikeableGuard : MonoBehaviour
{
	[Header("Timing")]
	[Tooltip("How long the guard staggers before fully going down. " +
		"Long enough to read as impact; short enough not to drag. 0.8–1.2s. " +
		"The crumple animation spans exactly this duration.")]
	[SerializeField] private float staggerDuration = 1.0f;

	[Header("Crumple")]
	[Tooltip("Transform that buckles and topples. Leave empty to use this " +
		"GameObject — which is also GuardController's fallback guardBody, so " +
		"the standing and lean models both come along.")]
	[SerializeField] private Transform crumpleBody;

	[Tooltip("Fraction of staggerDuration spent buckling before the topple " +
		"starts. The rest is the fall.")]
	[Range(0.05f, 0.6f)]
	[SerializeField] private float buckleFraction = 0.25f;

	[Tooltip("How far he sinks during the buckle, in metres. Knees going.")]
	[SerializeField] private float buckleSink = 0.15f;

	[Tooltip("Local-scale multiplier at the bottom of the buckle. Y < 1 squashes, " +
		"X/Z > 1 bulges. If he squashes sideways instead of down, the model's " +
		"local up isn't Y — swap the components.")]
	[SerializeField] private Vector3 buckleScale = new Vector3(1.05f, 0.88f, 1.05f);

	[Tooltip("How far he tips over, in degrees. 90 = flat on the floor. Under 90 " +
		"reads as slumped against something and hides floor clipping.")]
	[Range(0f, 95f)]
	[SerializeField] private float toppleAngle = 80f;

	[Tooltip("How far he rises while toppling, in metres, to keep his body above " +
		"the floor. The topple pivots at the centre of his feet, so the half of " +
		"him on the fall side swings below the floor. Set this to about half his " +
		"front-to-back thickness. Too much and he floats; too little and he sinks.")]
	[SerializeField] private float toppleLift = 0.2f;

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

	// Re-entry guard. Once struck, the guard is going down.
	private bool hasBeenStruck = false;

	/// <summary>
	/// Gate checked by PlayerController.TryStrike() before doing anything.
	/// True only when the guard is in LeanIn AND hasn't already been struck.
	/// </summary>
	public bool CanBeStruck()
	{
		if (hasBeenStruck) return false;
		if (GuardController.Instance == null) return false;
		return GuardController.Instance.CurrentState == GuardController.GuardState.LeanIn;
	}

	/// <summary>
	/// Called by PlayerController.TryStrike() when all gates pass.
	/// </summary>
	public void OnStruck(PlayerController player)
	{
		if (hasBeenStruck) return;
		hasBeenStruck = true;

		Log("Strike landed. Freezing guard movement, starting crumple.");

		if (GuardController.Instance != null)
			GuardController.Instance.FreezeMovement();

		StartCoroutine(Crumple(player != null ? player.transform : null));
		StartCoroutine(StaggerSequence(player));
	}

	private IEnumerator StaggerSequence(PlayerController player)
	{
		if (AudioManager.Instance != null && strikeImpactClip != null)
			AudioManager.Instance.PlaySFX(strikeImpactClip, sfxVolume, 1f);

		if (MutterSystem.Instance != null && !string.IsNullOrEmpty(strikeMutterLine))
			MutterSystem.Instance.Play(strikeMutterLine, MutterSystem.Speaker.Cassie);

		yield return new WaitForSeconds(staggerDuration);

		if (AudioManager.Instance != null && guardDownClip != null)
			AudioManager.Instance.PlaySFX(guardDownClip, sfxVolume, 1f);

		if (GuardController.Instance != null)
			GuardController.Instance.OnGuardDowned();

		Log("Stagger complete. Guard is down.");
	}

	private IEnumerator Crumple(Transform cassie)
	{
		Transform b = crumpleBody != null ? crumpleBody : transform;

		Vector3 p0 = b.position;
		Quaternion r0 = b.rotation;
		Vector3 s0 = b.localScale;

		// Topple direction: away from Cassie, horizontal only.
		Vector3 away = cassie != null ? p0 - cassie.position : Vector3.zero;
		away.y = 0f;
		if (away.sqrMagnitude < 1e-4f)
		{
			Log("No usable direction away from Cassie — toppling along -forward.");
			away = -b.forward;
			away.y = 0f;
		}
		away.Normalize();

		// Rotating about Cross(up, away) by a positive angle tips the top toward 'away'.
		Vector3 axis = Vector3.Cross(Vector3.up, away);

		float buckleTime = staggerDuration * buckleFraction;
		float toppleTime = staggerDuration - buckleTime;

		// Phase 1 — buckle: sink + squash, eased.
		float t = 0f;
		while (t < buckleTime)
		{
			t += Time.deltaTime;
			float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / buckleTime));
			b.position = p0 + Vector3.down * (buckleSink * k);
			b.localScale = Vector3.Scale(s0, Vector3.Lerp(Vector3.one, buckleScale, k));
			yield return null;
		}
		b.position = p0 + Vector3.down * buckleSink;
		b.localScale = Vector3.Scale(s0, buckleScale);

		// Phase 2 — topple: ease-in (k²) so it accelerates like a fall.
		// The pivot is the centre of his feet, so as he tips, the half of him on
		// the fall side swings below the floor by roughly halfDepth * sin(angle).
		// Undo the buckle sink and lift by that amount so his body rests ON the
		// floor, not in it. (Day 169 fix: he sank into the floor on first test.)
		t = 0f;
		while (t < toppleTime)
		{
			t += Time.deltaTime;
			float k = Mathf.Clamp01(t / toppleTime);
			float ang = toppleAngle * k * k;
			ApplyTopple(b, p0, r0, axis, ang, k * k);
			yield return null;
		}
		ApplyTopple(b, p0, r0, axis, toppleAngle, 1f);
	}

	private void ApplyTopple(Transform b, Vector3 p0, Quaternion r0, Vector3 axis,
		float angle, float progress)
	{
		float sink = buckleSink * (1f - progress);
		float lift = toppleLift * Mathf.Sin(angle * Mathf.Deg2Rad);
		b.position = p0 + Vector3.up * (lift - sink);
		b.rotation = Quaternion.AngleAxis(angle, axis) * r0;
	}

	private void Log(string msg)
	{
		if (verboseLogging) Debug.Log($"[StrikeableGuard] {msg}");
	}
}
