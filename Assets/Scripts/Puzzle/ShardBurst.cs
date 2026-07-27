using UnityEngine;

/// <summary>
/// Generic one-shot shard burst. Spawns fragments at a world position with
/// scatter and outward velocity, then lets gravity finish the job. The scatter
/// is either spherical (no direction known) or coned along a supplied swing
/// vector (Day 83).
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
/// WHY SPHERICAL WAS THE FIRST PASS, AND WHY IT ISN'T THE LAST (Day 83)
/// --------------------------------------------------------------------
/// The lamp smashes at or near floor level, so its shards have to be SEATED on
/// the floor or they spawn below it and fall into the void. A bottle smashes at
/// head height in open air. There's nothing to seat against, so the first pass
/// here scattered spherically — every direction equally likely.
///
/// That's correct for a bottle dropped. It's wrong for a bottle SWUNG. A
/// spherical burst throws as much glass backward into Cassie's face as forward
/// into the guard, which reads as an object that spontaneously disassembled
/// rather than one that was hit very hard in a specific direction. The swing is
/// the most expensive thing in the beat — 0.24s of hand-authored arc — and a
/// symmetric burst discards all of it at the exact frame it pays off.
///
/// So Burst now has a directional overload. Give it the swing vector and the
/// launch directions are drawn from a cone around it. The spherical path is
/// untouched and still the default, because a caller that doesn't know which
/// way the thing was moving should not be made to invent an answer.
///
/// WHY THE SPAWN OFFSET STAYED SPHERICAL WHILE THE LAUNCH WENT CONICAL
/// -------------------------------------------------------------------
/// These were one vector before, on purpose: sharing `dir` meant every fragment
/// got a full-magnitude push along the same line it was offset, which avoided
/// the interior-sphere failure where some pieces get a near-zero velocity and
/// read as not having got the memo.
///
/// The cone sampler guarantees a unit-length direction by construction, so the
/// near-zero case can't arise and the reason for the coupling is gone. They now
/// mean different things and are drawn separately: scatterRadius is 0.12m of
/// anti-co-location jitter so fragments don't spawn inside each other and
/// resolve the overlap by launching each other across the room. It is not a
/// shape control. The cone is the shape control.
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
		"around the burst point. Small — this is anti-co-location jitter, not a " +
		"shape control. Just enough that fragments don't spawn inside each other " +
		"and resolve their overlap by launching each other across the room. " +
		"Stays spherical even for a coned burst: the glass all came from the " +
		"same bottle regardless of which way it was travelling.")]
	[SerializeField] private float scatterRadius = 0.12f;

	[Tooltip("Base outward launch speed in METRES PER SECOND, applied as a " +
		"VelocityChange so prefab mass doesn't enter into it. 1.5-2.5 reads as " +
		"glass coming apart; 4+ starts reading as an explosion, and by 10 the " +
		"pieces clear the room. NOTE: a coned burst reads FASTER than a " +
		"spherical one at the same number, because all the fragments travel " +
		"together instead of half of them cancelling out visually — expect to " +
		"come down 15-25% when switching a burst from spherical to coned.")]
	[SerializeField] private float launchSpeed = 1.8f;

	[Tooltip("Tumble rate in RADIANS PER SECOND, random axis, applied as an " +
		"angular VelocityChange. 6-12 is a lively tumble. PhysX clamps angular " +
		"velocity at 50 by default, so anything near that is both invisible as a " +
		"distinct value and a sign the units are wrong.")]
	[SerializeField] private float spin = 8f;

	[Tooltip("Seconds before each fragment is destroyed. 0 or less = never, " +
		"lamp-shard style. Keep it finite for pure-VFX bursts.")]
	[SerializeField] private float shardLifetime = 8f;

	[Header("Cone (directional bursts only)")]
	[Tooltip("FULL cone angle in degrees around the supplied swing vector. " +
		"180 = a forward hemisphere, 360 = fully spherical (i.e. the directional " +
		"overload degrades gracefully into the old behaviour). Below about 30 it " +
		"stops reading as a smash and starts reading as a shotgun or a jet. " +
		"60-90 is the band where the glass still goes everywhere but you can " +
		"tell which way it was hit. Ignored by the non-directional Burst().")]
	[Range(0f, 360f)]
	[SerializeField] private float coneAngle = 70f;

	[Tooltip("Skew of the angular draw. 1 = even coverage of the cone's area. " +
		"Above 1 biases toward the AXIS, producing a dense core of fragments " +
		"travelling nearly straight along the swing plus a few wide stragglers — " +
		"same convention as sizeBias, and the same reason: even coverage of a " +
		"wide cone reads as 'a wide cone', while a core-plus-stragglers reads as " +
		"'most of it went that way'. 1.5-2.5. Set 1 while you're dialling " +
		"coneAngle so you're only tuning one thing.")]
	[Range(1f, 4f)]
	[SerializeField] private float coneBias = 1.6f;

	[Tooltip("Degrees to tilt the whole cone axis toward world UP before " +
		"sampling. A swing is roughly horizontal, and a horizontal spray at " +
		"these speeds is on the floor within half a second — the arc is most of " +
		"what sells the break, and you don't get an arc without some air. " +
		"8-20. Zero for a burst that should hug its own axis exactly.")]
	[Range(-45f, 45f)]
	[SerializeField] private float coneLift = 12f;

	[Tooltip("Per-shard multiplier range on launchSpeed. Uniform speed inside a " +
		"cone is the failure mode that makes a directional burst look WORSE than " +
		"a spherical one: every fragment sits on the same expanding shell, which " +
		"is unmistakably a spawn event. Spread them and the eye reads glass. " +
		"This matters more than coneAngle does — set it before you spend time on " +
		"the angle. 0.5-1.5 is a good spread; narrow it toward 1 only if the " +
		"stragglers are lingering too long.")]
	[SerializeField] private Vector2 speedVariance = new Vector2(0.55f, 1.45f);

	[Header("SFX (optional)")]
	[Tooltip("Glass-break clip, played once at the burst. Routed through " +
		"AudioManager.PlaySFX (2D, non-diegetic) — same channel choice as " +
		"the lamp smash. Leave empty if nothing's wired yet.")]
	[SerializeField] private AudioClip smashClip;

	[Tooltip("Volume for the smash clip. Default 1.0.")]
	[SerializeField] private float smashVolume = 1.0f;

	[Header("Debug")]
	[Tooltip("Draws the cone in the Scene view when this object is selected, " +
		"along this transform's forward. The real burst uses the swing vector " +
		"the caller passes, not this — the gizmo is for reading the ANGLE, not " +
		"the aim. Lets you dial coneAngle without entering Play Mode.")]
	[SerializeField] private bool drawConeGizmo = true;

	/// <summary>
	/// Spawn a SPHERICAL burst at a world position. Unchanged from Day 82 — this
	/// is the right call when the caller genuinely doesn't know which way the
	/// thing was moving (a dropped bottle, a shelf collapsing, a pressure vessel).
	///
	/// Safe to call with no prefab wired — logs and no-ops, so a half-built scene
	/// doesn't throw during a terminal beat. Not idempotent by itself: the CALLER
	/// owns "only once" (BottleSmashOnContact gets that from the driver's
	/// _contactFired guard). Keeping it dumb means the same component can serve a
	/// repeatable effect later.
	/// </summary>
	public void Burst(Vector3 worldPos)
	{
		Spawn(worldPos, Vector3.zero, false);
	}

	/// <summary>
	/// Spawn a CONED burst at a world position, biased along swingDirection.
	/// Pass the direction the breaking object was travelling at the moment it
	/// broke — for the bottle that's roughly (guard head - contact point), which
	/// is within about 20 degrees of the true tangent of her arc and therefore
	/// well inside the cone's own spread.
	///
	/// A zero or near-zero direction falls back to spherical rather than
	/// throwing or emitting a degenerate burst: a caller whose reference went
	/// missing should get a worse-looking smash, not no smash, in a beat that
	/// ends the level.
	/// </summary>
	public void Burst(Vector3 worldPos, Vector3 swingDirection)
	{
		if (swingDirection.sqrMagnitude < 1e-6f)
		{
			Debug.LogWarning($"[ShardBurst] Directional Burst at {worldPos} on " +
				$"'{name}' got a zero swing vector. Falling back to spherical. " +
				$"Check the caller's aim reference is assigned.");
			Spawn(worldPos, Vector3.zero, false);
			return;
		}

		Spawn(worldPos, swingDirection.normalized, true);
	}

	private void Spawn(Vector3 worldPos, Vector3 axis, bool useCone)
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

		// Lift applied once, to the axis, before any sampling — so the whole cone
		// tilts as a unit rather than each fragment getting an independent lift
		// that would widen the cone vertically and leave it narrow horizontally.
		if (useCone) axis = ApplyLift(axis);

		for (int i = 0; i < shardCount; i++)
		{
			// Spawn jitter is always spherical and always independent of the
			// launch direction — see the class comment. onUnitSphere, not
			// insideUnitSphere, so no two fragments start closer together than
			// they have to.
			Vector3 spawnPos = worldPos + Random.onUnitSphere * scatterRadius;
			Quaternion spawnRot = Random.rotation;

			Vector3 launchDir = useCone ? ConeDirection(axis) : Random.onUnitSphere;

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
				float speed = launchSpeed * Random.Range(speedVariance.x, speedVariance.y);
				rb.AddForce(launchDir * speed, ForceMode.VelocityChange);
				rb.AddTorque(Random.onUnitSphere * spin, ForceMode.VelocityChange);
			}

			if (shardLifetime > 0f)
			{
				Destroy(shard, shardLifetime);
			}
		}

		Debug.Log($"[ShardBurst] Spawned {shardCount} shards at {worldPos} " +
			$"({(useCone ? $"cone {coneAngle} deg bias {coneBias} lift {coneLift} about {axis}" : "spherical")}, " +
			$"scatter {scatterRadius}, speed {launchSpeed} m/s x{speedVariance.x}-{speedVariance.y}, " +
			$"spin {spin} rad/s, lifetime {shardLifetime}).");
	}

	// Tilt the axis toward world up, rotating about the horizontal perpendicular
	// so the lift stays in the vertical plane containing the swing rather than
	// yawing the burst sideways. Negative angle because Unity's AngleAxis is
	// clockwise looking down the axis, and (up x axis) points to the swing's
	// right — so a positive rotation would drive the cone into the floor.
	private Vector3 ApplyLift(Vector3 axis)
	{
		if (Mathf.Approximately(coneLift, 0f)) return axis;

		Vector3 right = Vector3.Cross(Vector3.up, axis);
		if (right.sqrMagnitude < 1e-6f) return axis;   // axis already vertical

		return Quaternion.AngleAxis(-coneLift, right.normalized) * axis;
	}

	// One unit vector inside the cone.
	//
	// Sampling uniformly in COS(theta) rather than in theta is what gives even
	// coverage of the cone's area — sampling the angle directly crowds fragments
	// toward the axis, because a ring at small theta has less area than a ring at
	// large theta and gets the same number of draws. Getting a dense core is a
	// choice worth making deliberately (coneBias), not a bug worth inheriting
	// from the sampler.
	private Vector3 ConeDirection(Vector3 axis)
	{
		float cosMax = Mathf.Cos(Mathf.Clamp(coneAngle, 0f, 360f) * 0.5f * Mathf.Deg2Rad);

		// Draw 0 lands exactly on the axis, draw 1 lands on the cone's rim.
		// Raising the uniform to a power > 1 pushes the mass toward 0, i.e.
		// toward the axis — same trick and same convention as sizeBias, where
		// above 1 means "toward the tight end".
		float u = Mathf.Pow(Random.value, Mathf.Max(0.01f, coneBias));
		float cosTheta = Mathf.Lerp(1f, cosMax, u);
		float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));
		float phi = Random.value * Mathf.PI * 2f;

		Vector3 local = new Vector3(
			sinTheta * Mathf.Cos(phi),
			sinTheta * Mathf.Sin(phi),
			cosTheta);

		return AxisRotation(axis) * local;
	}

	// LookRotation rather than FromToRotation(forward, axis): the latter is
	// undefined when the vectors are exactly opposed and silently picks an
	// arbitrary perpendicular. Roll is irrelevant for a symmetric cone, but a
	// stable frame keeps a burst reproducible when the swing happens to point
	// at -Z, which is exactly the axis Cassie swings along in VS_Turnaround.
	private static Quaternion AxisRotation(Vector3 axis)
	{
		Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f
			? Vector3.forward
			: Vector3.up;
		return Quaternion.LookRotation(axis, up);
	}

	[ContextMenu("Debug: Burst Here (spherical)")]
	private void DebugBurstHere() => Burst(transform.position);

	[ContextMenu("Debug: Burst Here (cone along forward)")]
	private void DebugConeBurstHere() => Burst(transform.position, transform.forward);

#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		if (!drawConeGizmo) return;

		Vector3 axis = ApplyLift(transform.forward);
		Vector3 origin = transform.position;
		const float len = 1f;

		Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
		Gizmos.DrawLine(origin, origin + axis * len);

		// Twelve rim rays. Reading the SPREAD is the whole point of the gizmo,
		// so the rim is drawn rather than the axis alone — an axis line tells
		// you nothing about a 70 degree cone versus a 110 degree one.
		float half = Mathf.Clamp(coneAngle, 0f, 360f) * 0.5f * Mathf.Deg2Rad;
		Quaternion frame = AxisRotation(axis);
		Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.35f);

		for (int i = 0; i < 12; i++)
		{
			float phi = i / 12f * Mathf.PI * 2f;
			Vector3 rim = frame * new Vector3(
				Mathf.Sin(half) * Mathf.Cos(phi),
				Mathf.Sin(half) * Mathf.Sin(phi),
				Mathf.Cos(half));
			Gizmos.DrawLine(origin, origin + rim * len);
		}

		Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
		Gizmos.DrawWireSphere(origin, scatterRadius);
	}
#endif
}
