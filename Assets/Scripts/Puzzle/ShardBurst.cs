using UnityEngine;

/// <summary>
/// Generic one-shot shard burst. Spawns fragments at a world position with
/// spherical scatter and outward velocity, then lets gravity finish the job.
///
/// WHAT THIS IS NOT
/// ----------------
/// This is the spawn half of LampSmashTrigger with every lamp-specific concern
/// removed: no velocity gate (the caller decides when it smashed), no LevelTimer
/// call (that's L6's soft timer, not a property of breaking glass), no floor
/// raycast, no RequireComponent(Rigidbody) — this component doesn't need to BE
/// the thing that broke. Call Burst(worldPos) and it makes debris there.
///
/// LampSmashTrigger is deliberately NOT refactored to route through this. It
/// works, it's in a shipped level, and converging them buys nothing today.
/// Converging is an ideas.md item.
///
/// WHY SPHERICAL, NOT THE LAMP'S RAYCAST-TO-FLOOR
/// ----------------------------------------------
/// The lamp smashes at or near floor level, so its shards have to be SEATED on
/// the floor or they spawn below it and fall into the void. A bottle smashes at
/// head height in open air. There's nothing to seat against — the shards should
/// leave the impact point in every direction and land wherever gravity puts
/// them. Random.onUnitSphere (surface, not interior) for both offset and launch
/// direction, so every shard gets a real outward push rather than some of them
/// getting a near-zero one.
///
/// WHY VelocityChange AND NOT Impulse (fixed Day 82)
/// -------------------------------------------------
/// The first pass used ForceMode.Impulse with a value carried over from the
/// lamp. Impulse divides by mass, so the field meant "newton-seconds" while it
/// was being reasoned about as "how fast do the pieces go" — and the bottle
/// shard prefab is 0.2 kg against the lamp shard's much heavier body. The lamp's
/// sane-looking 1.5 became 12.5 m/s here. Every fragment left at 28 mph and the
/// smash read as a grenade.
///
/// VelocityChange ignores mass, so launchSpeed IS metres per second and the
/// number in the Inspector is the number in the world. This also means editing
/// the prefab's mass later can't silently retune the effect — the old shape had
/// a physics property on an unrelated asset secretly owning the look of the VFX.
///
/// Same reasoning for spin: torque-as-impulse divides by the inertia tensor,
/// which for a splinter this small is ~1e-4, so the old value asked for roughly
/// 16,000 rad/s and got silently clamped to PhysX's 50 rad/s ceiling. Every
/// shard span at the engine maximum. As an angular VelocityChange the field is
/// rad/s and stays where it's put.
///
/// WHY THESE SHARDS DIE AND LAMP SHARDS DON'T
/// ------------------------------------------
/// Lamp shards are TOOLS — BladeTool subclasses the player picks up, which is
/// why they're scene-rooted and persist across failure-loop attempts (§7). These
/// are pure VFX for a terminal beat. Nothing picks them up, the demo ends
/// seconds later, and leaving rigidbodies simulating forever is a leak with no
/// upside. Hence shardLifetime.
/// </summary>
public class ShardBurst : MonoBehaviour
{
	[Header("Shards")]
	[Tooltip("Fragment prefab. Needs a mesh, a collider, and a Rigidbody. Does " +
		"NOT need a LampShard/BladeTool component — these aren't pickups. " +
		"Consider putting the prefab on a layer that ignores the Player so " +
		"fragments don't clatter off Cassie during her settle.")]
	[SerializeField] private GameObject shardPrefab;

	[Tooltip("Overall per-shard size multiplier, applied to all three axes " +
		"together before proportion variation. This is the 'how big is this " +
		"piece' knob. Range wide on purpose — a hierarchy of sizes is most of " +
		"what makes debris read as one thing that broke rather than a set of " +
		"props that spawned.")]
	[SerializeField] private Vector2 sizeRange = new Vector2(0.4f, 1.6f);

	[Tooltip("Skew of the size draw. 1 = uniform. Above 1 biases toward the " +
		"SMALL end, so most fragments are splinters and big pieces are rare — " +
		"which is how glass actually breaks, and why uniform randomization " +
		"reads as 'everything is medium'. 2-3 is a good range; 1 to disable.")]
	[SerializeField] private float sizeBias = 2f;

	[Tooltip("Per-axis proportion variation, applied on top of sizeRange. This " +
		"is the 'what shape is this piece' knob — keep it narrow, since its job " +
		"is to stop fragments looking like scaled copies of each other, not to " +
		"produce the size spread. That's sizeRange's job now.")]
	[SerializeField] private Vector2 proportionRange = new Vector2(0.7f, 1.4f);

	[Tooltip("How many fragments to spawn. Higher than the lamp's 3 because " +
		"these are decorative rather than a tool supply — the read is 'it " +
		"shattered', and 3 pieces reads as 'it came apart in three pieces'.")]
	[SerializeField] private int shardCount = 6;

	[Tooltip("Spawn scatter radius in world units, on the surface of a sphere " +
		"around the burst point. Small — this is a bottle, not an explosion. " +
		"Just enough that fragments don't spawn co-located and resolve their " +
		"overlap by launching each other across the room.")]
	[SerializeField] private float scatterRadius = 0.12f;

	[Tooltip("Outward launch speed in METRES PER SECOND, applied as a " +
		"VelocityChange so prefab mass doesn't enter into it. 1.5-2.5 reads as " +
		"glass coming apart; 4+ starts reading as an explosion, and by 10 the " +
		"pieces clear the room. Gravity and the prefab's drag take over almost " +
		"immediately, so this only shapes the first few frames — which is " +
		"exactly the part the eye reads as 'how hard did that break'.")]
	[SerializeField] private float launchSpeed = 1.8f;

	[Tooltip("Tumble rate in RADIANS PER SECOND, random axis, applied as an " +
		"angular VelocityChange. 6-12 is a lively tumble. PhysX clamps angular " +
		"velocity at 50 by default, so anything near that is both invisible as a " +
		"distinct value and a sign the units are wrong.")]
	[SerializeField] private float spin = 8f;

	[Tooltip("Seconds before each fragment is destroyed. 0 or less = never, " +
		"lamp-shard style. Keep it finite for pure-VFX bursts.")]
	[SerializeField] private float shardLifetime = 8f;

	[Header("SFX (optional)")]
	[Tooltip("Glass-break clip, played once at the burst. Routed through " +
		"AudioManager.PlaySFX (2D, non-diegetic) — same channel choice as " +
		"the lamp smash. Leave empty if nothing's wired yet.")]
	[SerializeField] private AudioClip smashClip;

	[Tooltip("Volume for the smash clip. Default 1.0.")]
	[SerializeField] private float smashVolume = 1.0f;

	

	/// <summary>
	/// Spawn the burst at a world position. Safe to call with no prefab wired —
	/// logs and no-ops, so a half-built scene doesn't throw during a terminal beat.
	/// Not idempotent by itself: the CALLER owns "only once" (BottleSmashOnContact
	/// gets that from the driver's _contactFired guard). Keeping it dumb means the
	/// same component can serve a repeatable effect later.
	/// </summary>
	public void Burst(Vector3 worldPos)
	{
		if (smashClip != null && AudioManager.Instance != null)
		{
			AudioManager.Instance.PlaySFX(smashClip, smashVolume, 1f);
		}

		if (shardPrefab == null)
		{
			Debug.LogWarning($"[ShardBurst] Burst at {worldPos} with no shardPrefab " +
				$"wired on '{name}'. Skipping. Wire the prefab if this burst is " +
				$"supposed to be visible.");
			return;
		}

		for (int i = 0; i < shardCount; i++)
		{
			// onUnitSphere, not insideUnitSphere: every fragment starts at the
			// full scatter radius and gets a full-magnitude outward push. The
			// interior variant hands some fragments a near-zero offset and a
			// near-zero velocity, which reads as pieces that didn't get the memo.
			Vector3 dir = Random.onUnitSphere;
			Vector3 spawnPos = worldPos + dir * scatterRadius;
			Quaternion spawnRot = Random.rotation;

			GameObject shard = Instantiate(shardPrefab, spawnPos, spawnRot);

			// Two-stage sizing. Stage one picks how big this fragment is, biased
			// small: raising a uniform 0-1 to a power > 1 pushes the mass of the
			// distribution toward zero, so most draws land near sizeRange.x and
			// the occasional one reaches the top. Stage two varies proportion per
			// axis so same-size pieces still aren't the same shape.
			//
			// Fusing these into one per-axis uniform (the old scaleRange) can't
			// produce a hierarchy: three independent draws average out, so every
			// fragment ends up near the middle of the range in overall volume
			// however wide you set it.
			float size = Mathf.Lerp(sizeRange.x, sizeRange.y,
				Mathf.Pow(Random.value, Mathf.Max(0.01f, sizeBias)));

			shard.transform.localScale = Vector3.Scale(
				shard.transform.localScale,
				new Vector3(
					size * Random.Range(proportionRange.x, proportionRange.y),
					size * Random.Range(proportionRange.x, proportionRange.y),
					size * Random.Range(proportionRange.x, proportionRange.y)));

			Rigidbody rb = shard.GetComponent<Rigidbody>();
			if (rb != null)
			{
				// Same reasoning as the lamp shards: small fast fragments are
				// exactly what tunnels a thin floor collider. Forced here so it
				// can't be forgotten on the prefab.
				rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

				// VelocityChange, not Impulse — see the class comment. These two
				// lines are the whole difference between a smash and a grenade.
				// Push along the same direction the fragment was offset, so the
				// burst expands coherently instead of fragments crossing paths.
				rb.AddForce(dir * launchSpeed, ForceMode.VelocityChange);
				rb.AddTorque(Random.onUnitSphere * spin, ForceMode.VelocityChange);
			}

			if (shardLifetime > 0f)
			{
				Destroy(shard, shardLifetime);
			}
		}

		Debug.Log($"[ShardBurst] Spawned {shardCount} shards at {worldPos} " +
			$"(scatter {scatterRadius}, speed {launchSpeed} m/s, spin {spin} rad/s, " +
			$"lifetime {shardLifetime}).");
	}

	[ContextMenu("Debug: Burst Here")]
	private void DebugBurstHere() => Burst(transform.position);
}