using UnityEngine;

/// <summary>
/// Shatters the bottle on the contact frame of Cassie's strike.
///
/// Pure presentation. It subscribes to CassieStrikeDriver.OnContact, bursts
/// shards at the bottle's world position, and hides the bottle. It does not
/// touch the guard, the KO, or any gameplay state — that path is the Play()
/// onContact callback PlayerController owns, and per spec §13 the animation
/// layer doesn't modify gameplay hooks to serve itself.
///
/// WHY A SEPARATE EVENT INSTEAD OF CHAINING THE CALLBACK
/// -----------------------------------------------------
/// The driver's Play(onContact, onComplete) takes ONE callback, supplied per
/// strike by the caller, and nulls it after firing. That's the right shape for
/// the single gameplay consumer. It's the wrong shape for presentation, where
/// several unrelated components (smash, SFX, camera shake) each want the same
/// frame and each own their own lifetime. Hence the multicast OnContact event.
/// Stacking presentation onto the gameplay callback would mean PlayerController
/// knowing about VFX, which is precisely the coupling §13 exists to prevent.
///
/// WHY smashOrigin IS A FIELD AND NOT FOUND AUTOMATICALLY
/// ------------------------------------------------------
/// The burst has to happen where the bottle IS on the contact frame, which
/// means a transform that travels with the swinging hand. HandAnchor sits on the
/// Player root rather than under the hand bone, so it doesn't move with the
/// swing — bursting there puts the glass in her lap. Rather than guess at the
/// held-visual hierarchy, this is wired in the Inspector. Drag the object that
/// visibly moves with the bottle during the swing.
///
/// WHERE THE SWING VECTOR COMES FROM (Day 83)
/// -------------------------------------------
/// ShardBurst can now cone its fragments along a supplied direction. The honest
/// answer for that direction is the bottle's velocity at contact — the true
/// tangent of her arc. It is deliberately NOT what this uses, for two reasons.
///
/// First, measuring it means differencing smashOrigin across frames, and this
/// handler runs from inside CassieStrikeDriver.Contribute, i.e. mid-rig-update,
/// where the transforms still hold the PREVIOUS frame's pose. A measured tangent
/// would be a frame stale on top of a position that is already a frame stale
/// (see the known-issue note below). Two different staleness bugs interacting is
/// not a thing to introduce during a tuning session.
///
/// Second and more important: a measured tangent is NOISY between runs, and the
/// entire point of this session is turning cone knobs and reading the result. A
/// direction that differs slightly every playtest makes it impossible to tell
/// whether coneAngle helped or whether the aim just moved. Deterministic beats
/// accurate when you are tuning.
///
/// So the aim is (swingTarget - burst point), which is stable, needs one
/// Inspector reference, and is within roughly 20 degrees of the real tangent —
/// comfortably inside the cone's own spread. tangentSkew then rotates it toward
/// the side she carries through to, which is the part "point it at him" gets
/// wrong: a follow-through does not stop at the target, it travels past it.
///
/// Swapping to a measured tangent later is an ideas.md item, and wants a
/// post-write hook on CassieRig rather than a difference taken from in here.
///
/// KNOWN ISSUE — THE BURST POINT IS ONE FRAME STALE
/// -------------------------------------------------
/// OnContact fires synchronously from inside the driver's Contribute, which runs
/// before CassieRig writes the frame's bone poses. smashOrigin.position
/// therefore reports where the bottle was LAST frame. The swing eases IN, so
/// peak hand speed is at contact — call it 5 m/s, which at 60fps is about 8cm of
/// error, roughly one scatterRadius. Reads as the glass breaking slightly behind
/// the impact rather than at it.
///
/// Not fixed here. Deferring the burst a frame to get a fresh position trades a
/// spatial error for a temporal one on the beat the whole level pays off on,
/// which is the worse trade. The real fix is an after-write event on CassieRig
/// that presentation can hang off. ideas.md.
///
/// ONE-SHOT BY INHERITANCE
/// -----------------------
/// No local hasSmashed guard is needed: the driver's _contactFired already makes
/// OnContact fire at most once per strike, and the strike is terminal. The local
/// guard is kept anyway, cheap insurance against a future repeatable strike —
/// same defense-in-depth as LampSmashTrigger's two idempotency paths.
/// </summary>
[RequireComponent(typeof(ShardBurst))]
public class BottleSmashOnContact : MonoBehaviour
{
	[Header("Wiring")]
	[Tooltip("The strike driver whose contact frame triggers the smash. Leave " +
		"empty to search this GameObject and its parents on Awake.")]
	[SerializeField] private CassieStrikeDriver strikeDriver;

	[Tooltip("Transform marking where the bottle is at the moment of contact. " +
		"MUST be something that travels with the swinging hand — drag the held " +
		"bottle visual itself, or a marker parented under the hand bone. Do NOT " +
		"use HandAnchor unless you've confirmed it follows the swing; it sits on " +
		"the Player root and the shards will spawn in her lap. Falls back to this " +
		"component's own transform with a warning if left empty.")]
	[SerializeField] private Transform smashOrigin;

	[Tooltip("The bottle's visual, hidden on smash. Per the same design call as " +
		"the lamp: the object is consumed into its shards, so there's no " +
		"half-broken prop left sitting in her hand. Optional — leave empty if " +
		"something else already hides it.")]
	[SerializeField] private GameObject bottleVisual;

	[Header("Shard aim")]
	[Tooltip("What the swing is aimed at — the guard's head, or the Guard root. " +
		"The shard cone is biased along (this - burst point). Precision is not " +
		"important: 30cm of height error is nothing inside a 70 degree cone, so " +
		"the Guard root is fine if there's no head transform to grab. LEAVE " +
		"EMPTY to fall back to the old spherical burst — which is the correct " +
		"thing to do if you ever reuse this handler for a bottle that's dropped " +
		"rather than swung.")]
	[SerializeField] private Transform swingTarget;

	[Tooltip("Degrees to rotate the aim about world up, so the spray follows " +
		"THROUGH rather than stopping at him. A follow-through carries the " +
		"bottle past the target, and glass sprays along the travel, not along " +
		"the line of sight — aiming straight at him is the one thing 'point it " +
		"at the guard' reliably gets wrong.\n\n" +
		"Sign depends on which way she uncoils. Right-handed swing carries to " +
		"HER left, which in this scene's orientation is negative. If the spray " +
		"crosses him the wrong way, flip the sign — same deal as mirrorOffArm " +
		"on the driver, and for the same reason: the correct sign isn't knowable " +
		"from outside the authored pose. 15-35 in magnitude.")]
	[Range(-90f, 90f)]
	[SerializeField] private float tangentSkew = -25f;

	private ShardBurst _burst;
	private bool _hasSmashed;

	void Awake()
	{
		_burst = GetComponent<ShardBurst>();

		if (strikeDriver == null)
			strikeDriver = GetComponentInParent<CassieStrikeDriver>();

		if (strikeDriver == null)
		{
			Debug.LogWarning($"[BottleSmashOnContact] No CassieStrikeDriver found " +
				$"for '{name}'. The bottle will never smash. Wire it explicitly " +
				$"or parent this under the Player.");
		}

		if (smashOrigin == null)
		{
			Debug.LogWarning($"[BottleSmashOnContact] No smashOrigin wired on " +
				$"'{name}'. Falling back to this transform, which is almost " +
				$"certainly not where the bottle is at contact. Drag the held " +
				$"bottle visual in.");
			smashOrigin = transform;
		}

		// Not a warning. An unwired swingTarget is a legitimate configuration —
		// it means "spherical", which is right for anything that wasn't swung.
		// Warning on a valid choice trains you to ignore the console.
		if (swingTarget == null)
		{
			Debug.Log($"[BottleSmashOnContact] No swingTarget on '{name}'. Shards " +
				$"will scatter spherically. Wire the guard if you want the burst " +
				$"coned along the swing.");
		}
	}

	// Subscribe/unsubscribe in OnEnable/OnDisable rather than Awake/OnDestroy so
	// the component can be toggled without leaking a handler — and so a disabled
	// smash component genuinely doesn't fire.
	void OnEnable()
	{
		if (strikeDriver != null) strikeDriver.OnContact += HandleContact;
	}

	void OnDisable()
	{
		if (strikeDriver != null) strikeDriver.OnContact -= HandleContact;
	}

	private void HandleContact()
	{
		if (_hasSmashed) return;
		_hasSmashed = true;

		Vector3 pos = smashOrigin != null ? smashOrigin.position : transform.position;

		if (TryGetSwingDirection(pos, out Vector3 dir))
		{
			_burst.Burst(pos, dir);
			Debug.Log($"[BottleSmashOnContact] Bottle smashed at {pos}, coned along {dir}.");
		}
		else
		{
			_burst.Burst(pos);
			Debug.Log($"[BottleSmashOnContact] Bottle smashed at {pos}, spherical.");
		}

		if (bottleVisual != null) bottleVisual.SetActive(false);
	}

	/// <summary>
	/// Aim vector for the cone, or false if there isn't one and the caller should
	/// fall back to spherical.
	///
	/// The skew is applied about WORLD up rather than the aim's own perpendicular
	/// because a swing is a horizontal action — she's still belted to the chair,
	/// so there is no vertical component to carry through. Vertical shaping is
	/// ShardBurst's coneLift, which is a separate concern and belongs there:
	/// lift is about giving fragments air, skew is about where the swing went.
	/// </summary>
	private bool TryGetSwingDirection(Vector3 burstPos, out Vector3 dir)
	{
		dir = Vector3.zero;
		if (swingTarget == null) return false;

		Vector3 toTarget = swingTarget.position - burstPos;

		// Degenerate if the marker and the guard have ended up co-located — a
		// mis-wire, not a runtime condition. Spherical is a better answer than a
		// cone around a garbage axis on the beat that ends the level.
		if (toTarget.sqrMagnitude < 1e-4f)
		{
			Debug.LogWarning($"[BottleSmashOnContact] swingTarget is on top of the " +
				$"burst point on '{name}'. Falling back to spherical.");
			return false;
		}

		dir = Quaternion.AngleAxis(tangentSkew, Vector3.up) * toTarget.normalized;
		return true;
	}

	[ContextMenu("Debug: Force Smash")]
	private void DebugForceSmash()
	{
		if (_hasSmashed)
		{
			Debug.Log("[BottleSmashOnContact] Already smashed. Ignoring.");
			return;
		}
		HandleContact();
	}

#if UNITY_EDITOR
	// Draws the actual aim vector, skew included, from the actual burst point.
	// ShardBurst's own gizmo draws the cone along ITS transform's forward, which
	// is the wrong axis — that one reads the ANGLE, this one reads the AIM.
	// Between them you can dial both without entering Play Mode.
	private void OnDrawGizmosSelected()
	{
		if (swingTarget == null) return;

		Vector3 pos = smashOrigin != null ? smashOrigin.position : transform.position;
		Vector3 toTarget = swingTarget.position - pos;
		if (toTarget.sqrMagnitude < 1e-4f) return;

		// Line of sight, for comparison against the skewed aim.
		Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.5f);
		Gizmos.DrawLine(pos, swingTarget.position);

		Vector3 aim = Quaternion.AngleAxis(tangentSkew, Vector3.up) * toTarget.normalized;
		Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.95f);
		Gizmos.DrawLine(pos, pos + aim * toTarget.magnitude);
		Gizmos.DrawWireSphere(pos, 0.05f);
	}
#endif
}
