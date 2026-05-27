using UnityEngine;

/// <summary>
/// Detects the lamp smashing — i.e. the lamp's Rigidbody striking another
/// collider with enough relative velocity to count as a "shatter" — and:
///   1. Starts the L6 soft timer (idempotent, "first occurrence wins")
///   2. Spawns LampShard fragments (scene-rooted, persist across attempts)
///   3. Disappears the lamp body itself
///
/// WHY VELOCITY-GATED
/// ------------------
/// The lamp lives on the nightstand and will register collisions constantly
/// during normal play: nightstand jostle nudges the lamp a millimeter, the
/// lamp wiggles against its resting collider, etc. Without a velocity gate,
/// every micro-collision would trigger the smash and break the "loud event"
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
/// SHARD SPAWN PATTERN
/// -------------------
/// Mirrors ChairRestraint.SpawnChairShards — same prefab+count+scatter+impulse
/// fields, same scene-root parenting (so shards persist across failure-loop
/// attempts per §7), same per-axis scale randomization for broken-glass variety.
/// Lamp shards subclass BladeTool (3-Struggle cut), not PointTool (5-Struggle
/// cut), making them the fastest cut in the level — paid for with the loudest
/// trigger and longest floor-crawl.
///
/// LAMP DISAPPEARS POST-SMASH
/// --------------------------
/// Per design call: the lamp body is consumed into shards. Cleaner visually
/// than a smashed-lamp prop sitting next to the shards, and removes any
/// ambiguity about whether the lamp can still be jostled/kicked further.
/// Disabling the GameObject also prevents further OnCollisionEnter fires,
/// matching the hasSmashed local guard.
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

	[Header("Shard Spawn")]
	[Tooltip("Prefab to instantiate on smash. Should have a LampShard component " +
		"(BladeTool subclass), a collider, and a Rigidbody for tumble-on-spawn. " +
		"Leave null on levels where you want a smash event without shards, " +
		"e.g. cinematic-only lamp smash; warning logs if shards were expected.")]
	[SerializeField] private GameObject lampShardPrefab;

	[Tooltip("Number of shards to spawn on smash. 3 is the L6 default — enough " +
		"that the player has shard supply across multiple failed attempts " +
		"(shards persist per §7) without flooding the floor.")]
	[SerializeField] private int shardCount = 3;

	[Tooltip("Horizontal scatter radius for spawned shards, in world units. " +
		"Each shard's XZ position is offset by a random unit-circle vector " +
		"scaled by this radius from the smash point. Keeps shards from " +
		"stacking inside one another at the impact point.")]
	[SerializeField] private float shardScatterRadius = 0.4f;

	[Tooltip("Small upward impulse applied to each shard at spawn. Gives a " +
		"brief tumble for visual life and lets the physics engine resolve any " +
		"initial collider overlap. Too high and shards fling across the room; " +
		"1.0-2.0 is the sweet spot.")]
	[SerializeField] private float shardSpawnImpulse = 1.5f;

	[Tooltip("Vertical offset applied to the spawn position relative to the " +
		"lamp's world position at impact. Lamps tend to be tall objects whose " +
		"transform pivot sits above the floor; a negative offset drops shards " +
		"to floor level so they read as fallen glass rather than mid-air debris. " +
		"Tune per lamp model.")]
	[SerializeField] private float shardSpawnYOffset = -0.1f;

	[Tooltip("Scale randomization range applied to each spawned shard. Each " +
		"axis is independently scaled by a value in this range, so shards " +
		"differ in proportion (thin/long shards vs squat ones) as well as " +
		"overall size. Identical shards read as 'three copies'; varied " +
		"shards read as 'pieces of one broken thing.'")]
	[SerializeField] private Vector2 shardScaleRange = new Vector2(0.6f, 1.4f);

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

		Smash();

		Debug.Log($"[LampSmashTrigger] Smash detected. Impact speed: " +
			$"{impactSpeed:F2} m/s (threshold {smashVelocityThreshold}).");
	}

	/// <summary>
	/// Execute the smash sequence: mark smashed, start timer, spawn shards,
	/// play SFX, disappear the lamp body. Extracted so the debug context menu
	/// can drive the same path as a real collision.
	/// </summary>
	private void Smash()
	{
		hasSmashed = true;

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

		if (lampShardPrefab != null)
		{
			SpawnLampShards(transform.position);
		}
		else
		{
			Debug.LogWarning("[LampSmashTrigger] Lamp smashed but no " +
				"lampShardPrefab is configured. Skipping shard spawn. The " +
				"loud/lamp-shard solve path requires shards — if this level " +
				"needs that path, wire the prefab.");
		}

		if (smashClip != null && AudioManager.Instance != null)
		{
			AudioManager.Instance.PlaySFX(smashClip, smashVolume, 1f);
		}

		// Disappear the lamp. SetActive(false) prevents further collision
		// events, hides the visual, and stops the Rigidbody from interacting
		// with the world — all in one call. We don't Destroy it because we
		// don't need to: the object lives on the scene root, the failure
		// loop doesn't repopulate it (§7: smashed stays smashed), and
		// keeping it around (disabled) is harmless and would let us
		// re-enable for debugging without scene reload.
		gameObject.SetActive(false);
	}

	/// <summary>
	/// Spawn shardCount LampShards around the given world position. Each
	/// shard is scene-rooted (no parent) — same parenting rule as
	/// ChairRestraint.SpawnChairShards, for the same reason: scene root
	/// is the only parent that survives the failure loop intact (chair
	/// resets, lamp disappears, player moves).
	///
	/// Rigidbody is required on the prefab so the upward impulse + gravity
	/// settle pass works. If the prefab lacks one, the impulse silently
	/// fails and shards spawn-and-rest at their initial Y — still
	/// functional, just less satisfying visually.
	/// </summary>
	private void SpawnLampShards(Vector3 originWorldPos)
	{
		for (int i = 0; i < shardCount; i++)
		{
			// Radial-outward placement on the unit circle (not insideUnitCircle):
			// guarantees each shard lands AT shardScatterRadius from the smash
			// point, not somewhere between 0 and shardScatterRadius. Mirrors
			// ChairRestraint's choice — keeps shards from spawning stacked
			// inside one another at the exact impact point.
			Vector2 offset2D = Random.insideUnitCircle.normalized * shardScatterRadius;
			Vector3 spawnPos = originWorldPos
				+ new Vector3(offset2D.x, shardSpawnYOffset, offset2D.y);

			// Random Y rotation only — shards look more natural lying flat in
			// varied orientations than tumbling with random pitch/roll baked in.
			Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

			GameObject shard = Instantiate(lampShardPrefab, spawnPos, spawnRot);

			// Per-axis scale randomization: three identical rectangles read as
			// "three copies of the same object spawned"; visibly different sizes
			// read as "pieces of something that broke." Each axis varies
			// independently so shards differ in proportion (thin/fat) as well
			// as overall size. Multiplies the prefab's base scale, so changes
			// to the prefab's authored proportions are preserved.
			float scaleX = Random.Range(shardScaleRange.x, shardScaleRange.y);
			float scaleY = Random.Range(shardScaleRange.x, shardScaleRange.y);
			float scaleZ = Random.Range(shardScaleRange.x, shardScaleRange.y);
			shard.transform.localScale = Vector3.Scale(
				shard.transform.localScale,
				new Vector3(scaleX, scaleY, scaleZ));

			// Brief upward impulse for tumble + collider-overlap resolution.
			// Mostly vertical with a tiny horizontal component so they spread
			// a bit rather than landing perfectly atop their spawn point.
			Rigidbody shardRb = shard.GetComponent<Rigidbody>();
			if (shardRb != null)
			{
				Vector3 impulseDir = (Vector3.up + Random.insideUnitSphere * 0.3f).normalized;
				shardRb.AddForce(impulseDir * shardSpawnImpulse, ForceMode.Impulse);
			}
		}

		Debug.Log($"[LampSmashTrigger] Spawned {shardCount} lamp shards at " +
			$"{originWorldPos} (scatter {shardScatterRadius}, impulse {shardSpawnImpulse}).");
	}

	[ContextMenu("Debug: Force Smash")]
	private void DebugForceSmash()
	{
		if (hasSmashed)
		{
			Debug.Log("[LampSmashTrigger] Already smashed. Ignoring.");
			return;
		}
		Debug.Log("[LampSmashTrigger] Debug-forced smash.");
		Smash();
	}
}
