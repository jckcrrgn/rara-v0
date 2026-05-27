using UnityEngine;

public class PlayerController : MonoBehaviour
{
	[Header("Interaction Settings")]
	[SerializeField] private float interactionCheckRadius = 1.5f;
	[SerializeField] private LayerMask interactableLayer = ~0;

	[Header("Bond")]
	[SerializeField] private Bond bond;

	[Header("Held Item")]
	[SerializeField] private Pickupable heldItem = null;

	[Header("Feedback")]
	[SerializeField] private Transform visualRoot;
	[SerializeField] private float shakeDuration = 0.2f;
	[SerializeField] private float shakeMagnitude = 8f;

	[Header("SFX")]
	[SerializeField] private AudioClip struggleSuccessClip;
	[SerializeField] private AudioClip struggleFailClip;
	[SerializeField] private AudioClip bondBreakClip;
	[Tooltip("Grunt of exertion. Plays on every kick attempt regardless of target — " +
		"including suppressed kicks (e.g. prone floor-restraint), so the player " +
		"hears that they tried even when no force was generated.")]
	[SerializeField] private AudioClip kickEffortClip;
	[Tooltip("Default thud when Kick lands on nothing Kickable (or a Kickable rejecting the kick). " +
		"Kickables play their own per-hit SFX, so this only fires for misses.")]
	[SerializeField] private AudioClip kickMissThudClip;

	[Header("Kick")]
	[Tooltip("How long a single kick attempt occupies. The detective's leg goes " +
		"through wind-up, strike, and recovery; she can't initiate another kick " +
		"until that window finishes. Applies even to suppressed (zero-force) " +
		"kicks — she still went through the motion. PLACEHOLDER VALUE: when the " +
		"character model arrives, replace with the actual kick animation length " +
		"(or a kick-active sub-window of it).")]
	[SerializeField] private float kickDuration = 0.5f;

	[Tooltip("Child Transform marking where the kick's physics cast originates. " +
		"Place it at foot height, offset along the body's kick axis (typically " +
		"local +Z, ~0.3-0.5u in front of player center) so the cast starts " +
		"outside the player's own collider. The cast DIRECTION comes from the " +
		"active restraint's GetKickDirection — default is +forward (she kicks " +
		"the way she faces), FloorRestraint overrides for prone/supine. " +
		"If left null, falls back to transform.position, which will probably " +
		"self-hit the player collider on cast.")]
	[SerializeField] private Transform footAnchor;

	[Tooltip("Sphere radius for the kick cast. Wider = more forgiving aim, but " +
		"also more likely to catch off-axis props. ~0.4u feels honest for a foot.")]
	[SerializeField] private float kickCastRadius = 0.4f;

	[Tooltip("Maximum distance along the kick direction the cast reaches. " +
		"Roughly leg-extension distance from the foot anchor.")]
	[SerializeField] private float kickCastDistance = 1.2f;

	[Tooltip("Base impulse magnitude applied to a loose Rigidbody on a 1.0-modifier " +
		"kick. Scaled by the restraint's GetKickModifier — mermaid-kick (0.4) " +
		"applies kickImpulseScale * 0.4 force. Tune via KickTestScaffold scene. " +
		"Day 44: bumped 8→10 after scaffold pass — 8 left the lamp prop " +
		"underpowered on free-kick. Calibration ongoing.")]
	[SerializeField] private float kickImpulseScale = 10f;

	[Tooltip("Layers the kick cast considers. Set to include world props, doors, " +
		"and anything else kickable; exclude the player's own layer to avoid " +
		"self-collision edge cases on top of the explicit self-Rigidbody filter.")]
	[SerializeField] private LayerMask kickCastLayers = ~0;

	[Header("Restraint")]
	[SerializeField] private RestraintBase currentRestraint;

	public Rigidbody Rb { get; private set; }
	public bool IsGrounded { get; private set; }

	// Read-only accessor for the kick-impulse scale. Exposed so debug/test
	// scaffolds can display the actual impulse magnitude that would be applied
	// on the next kick, rather than hard-coding the default. Production code
	// shouldn't depend on this — it's diagnostic surface only.
	public float KickImpulseScale => kickImpulseScale;

	// Kick state. Set true when a KickCycle is in flight.
	private bool isKicking = false;

	/// <summary>
	/// True if the player is currently committing the body to any action that
	/// should block other body-committing verbs (kick during inch, inch during
	/// flip, flip during kick, etc.). Aggregates the active restraint's busy
	/// state with the kick state.
	///
	/// Restraint coroutines (FloorRestraint's MoveCycle and FlipCycle) check
	/// this before starting a new cycle, so they refuse to start if a kick is
	/// in flight. KickCycle checks this before starting, so it refuses to fire
	/// if a move/flip is in progress.
	///
	/// Steering (A/D heading adjustment) does NOT contribute to IsBusy — it's
	/// aim, not body-committing motion.
	/// </summary>
	public bool IsBusy => isKicking || (currentRestraint != null && currentRestraint.IsBusy);

	// Read-only accessor so other systems (e.g. Kickable orientation gates) can
	// query restraint state without owning a reference.
	public RestraintBase CurrentRestraint => currentRestraint;

	public int StruggleProgress => bond != null ? bond.StruggleProgress : 0;
	public int BondStrength => bond != null ? bond.BondStrength : 1;
	public System.Action OnStruggleProgressChanged;
	public System.Action OnPlayerFreed;

	/// <summary>
	/// Fires when a Struggle attempt produces zero or negative bond progress
	/// (e.g. barehanded against a rope, wrong-tool-for-bond combinations). Used
	/// by stuck-player rescue components like BarehandStuckMutter to detect
	/// players who are spamming Struggle without success and need a nudge.
	///
	/// Fires AFTER restraint modifier and environmental-tool aggregation, so
	/// "failed" here means "the full pipeline still produced no progress" —
	/// exactly the condition the player experiences as "I'm pressing Space
	/// and nothing is happening."
	/// </summary>
	public System.Action OnFailedStruggle;

	/// <summary>
	/// Fires when SetRestraint changes the active restraint. UI subscribes to
	/// this so it can re-bind to the new restraint's OnHintsChanged event and
	/// rebuild the hints panel.
	/// </summary>
	public System.Action OnRestraintChanged;

	void Start()
	{
		Rb = GetComponent<Rigidbody>();

		if (bond != null)
		{
			bond.OnProgressChanged += () => OnStruggleProgressChanged?.Invoke();
			bond.OnBroken += EscapeBonds;
		}

		if (currentRestraint == null)
		{
			currentRestraint = GetComponent<RestraintBase>();
		}

		if (currentRestraint != null)
		{
			currentRestraint.OnEnter(this);
			OnRestraintChanged?.Invoke();
		}
		else
		{
			Debug.LogWarning("PlayerController has no RestraintBase. Movement will not work.");
		}
	}

	void Update()
	{
		// While a mutter is showing, world is paused — no movement, no verbs.
		// MutterSystem owns the dismissKey input itself (Space, currently shared
		// with Struggle); gating here keeps Struggle from double-firing on the
		// frame of dismissal.
		if (MutterSystem.Instance != null && MutterSystem.Instance.IsActive)
		{
			return;
		}

		// Suppress Struggle on the frame *after* a mutter dismisses. The dismiss
		// happens in MutterSystem.Update; depending on script execution order,
		// PlayerController.Update could see Space pressed AND IsActive already
		// false in the same frame. WasJustDismissed catches this.
		bool justDismissed = MutterSystem.Instance != null
			&& MutterSystem.Instance.WasJustDismissed;

		if (currentRestraint != null)
		{
			currentRestraint.HandleMovementInput(this);
		}

		if (Input.GetKeyDown(KeyCode.Space) && !justDismissed)
		{
			TryStruggle();
		}

		if (Input.GetKeyDown(KeyCode.E))
		{
			TryPickUp();
		}

		// Kick is its own verb. Effectiveness scaled by restraint
		// (free legs = full force, floor-bound scoot = reduced, floor-bound inch = zero, hogtied = zero).
		if (Input.GetKeyDown(KeyCode.F))
		{
			TryKick();
		}
	}

	void TryStruggle()
	{
		if (bond == null)
		{
			Debug.LogWarning("No Bond assigned to player.");
			return;
		}

		if (currentRestraint != null && !currentRestraint.CanStruggle())
		{
			return;
		}

		ToolType activeTool = heldItem != null ? heldItem.ToolType : ToolType.BareHands;
		int struggleAmount = bond.GetStruggleProgress(activeTool);

		InteractableBase nearby = FindNearestInteractable();

		if (nearby is EnvironmentalTool envTool)
		{
			struggleAmount += bond.GetStruggleProgress(envTool.ToolType);
			envTool.OnStruggle(this);
		}

		if (currentRestraint != null)
		{
			struggleAmount = Mathf.RoundToInt(struggleAmount * currentRestraint.GetStruggleModifier());
		}

		if (struggleAmount <= 0)
		{
			StartCoroutine(ShakeVisual());
			if (AudioManager.Instance != null && struggleFailClip != null)
				AudioManager.Instance.PlaySFX(struggleFailClip, 1f, Random.Range(0.95f, 1.05f));
			OnFailedStruggle?.Invoke();
		}
		else
		{
			if (AudioManager.Instance != null && struggleSuccessClip != null)
				AudioManager.Instance.PlaySFX(struggleSuccessClip, 1f, Random.Range(0.92f, 1.08f));
		}

		bond.ApplyStruggle(struggleAmount);
	}

	/// <summary>
	/// Kick verb entry point. Delegates to KickCycle coroutine, which owns the
	/// timing of the kick action (wind-up, strike, recovery). Gated on IsBusy AND 
	/// IsGrounded: can't kick mid-crawl, mid-flip, mid-kick,
	/// or mid-hop. Cassie's kinetic chain depends on having ground under her —
	/// you can't drive a kick from the air.
	///
	/// Why a coroutine instead of a cooldown timer:
	///   The kick is an action that takes time, not a button with a refractory
	///   period. The coroutine is the timeline. When the character model arrives,
	///   anim triggers, wind-up/strike/recovery SFX, hitbox enable/disable
	///   windows -- all of those slot into the coroutine at the right beats.
	///   Right now the cube has no animation so the coroutine is mostly just
	///   "play sound, apply force, wait." But the shape is correct.
	/// </summary>
	void TryKick()
	{
		if (currentRestraint == null) return;
		if (IsBusy) return;
		if (!IsGrounded) return;

		StartCoroutine(KickCycle());
	}

	/// <summary>
	/// One full kick cycle. Owns its own duration; gates re-entry via isKicking.
	///
	/// - Effort grunt plays on EVERY kick attempt — even suppressed ones — so the
	///   player always hears that they tried. Absence of impact SFX is the cue
	///   that the kick didn't generate force (try scoot, or escape your legs first).
	/// - Force is scaled by the restraint's GetKickModifier (free=1.0, floor-scoot=~0.5,
	///   floor-inch=0, hogtied=0). Zero force = effort grunt only, no thud, no Kickable hit.
	/// - If the nearest interactable is a Kickable that accepts the kick, route force to it.
	/// - Otherwise (no target, wrong target, or position gate failing): play the thud SFX.
	///   This is the "kicking the wall of the van" feedback — emergent, in-character, free.
	///
	/// The duration window applies regardless of outcome. Even a zero-force prone
	/// kick locks the verb for kickDuration -- she still went through the motion.
	/// This makes the prone-vs-scoot lesson clearer: spamming F in prone produces
	/// a deliberate, thwarted cadence of effort grunts, not rapid-fire mashing.
	/// </summary>
	System.Collections.IEnumerator KickCycle()
	{
		isKicking = true;

		// Effort layer: plays on every kick, always. Above the force check so
		// suppressed kicks (e.g. prone floor-restraint, hogtied) still give the
		// player audio feedback that they tried.
		if (AudioManager.Instance != null && kickEffortClip != null)
		{
			AudioManager.Instance.PlaySFX(kickEffortClip, 1f, Random.Range(0.95f, 1.08f));
		}

		float kickForce = currentRestraint.GetKickModifier();

		Debug.Log($"[Kick] Firing. restraint={currentRestraint.GetType().Name}, " +
			$"modifier={kickForce:F2}, scale={kickImpulseScale}, " +
			$"impulse={kickForce * kickImpulseScale:F2}");

		if (kickForce > 0f)
		{
			// Force-generating kick. Do a forward sphere cast from the foot anchor
			// along the restraint's kick direction, partition hits into accepting
			// Kickables vs. loose Rigidbodies, and resolve in priority order.
			//
			// Resolution rules (per Day 43 design):
			//   1. If any Kickable that returns CanBeKicked=true is in the hit set,
			//      route the kick to the NEAREST such Kickable. Other hits ignored.
			//      Preserves all existing Kickable behavior (e.g., L4 door).
			//   2. Else if any non-Kickable Rigidbody is in the hit set, apply an
			//      impulse to each scaled by kickForce. This is the emergent
			//      physics layer — kicks have real presence on world objects.
			//      Notably: Rigidbodies BELONGING to a Kickable that rejected the
			//      kick (e.g., L4 door pivots when out of zone) are excluded, so
			//      off-axis door kicks read as wall-thuds, not partial-feedback.
			//   3. Else miss thud SFX. Existing behavior.
			DoKickCast(kickForce);
		}
		// else: suppressed kick. Effort grunt already played; no thud, no Kickable.

		// Hold the kick state for the full duration regardless of outcome.
		// When animation arrives, replace with anim event hooks for wind-up,
		// strike, and recovery beats.
		yield return new WaitForSeconds(kickDuration);

		isKicking = false;
	}

	/// <summary>
	/// Forward sphere cast for the Kick verb. Encapsulated so KickCycle stays
	/// readable. See KickCycle for the resolution-priority commentary.
	///
	/// Partitioning logic:
	///   - Walk every collider hit by the cast.
	///   - For each, find the owning Kickable (GetComponentInParent) if any.
	///   - If a Kickable owns the collider AND CanBeKicked: candidate accepting Kickable.
	///   - If a Kickable owns the collider AND NOT CanBeKicked: excluded from BOTH
	///     buckets. The rejected Kickable doesn't receive impulse — wall-thud feel
	///     is preserved. This is the off-axis L4 door case.
	///   - If no Kickable owns the collider AND there's a Rigidbody: candidate
	///     loose Rigidbody.
	///   - Player's own Rigidbody filtered out explicitly — even with foot anchor
	///     offset, a cast that overshoots could come back and hit the player from
	///     behind on edge geometry.
	///
	/// Once partitioned, nearest accepting Kickable wins; else all loose Rigidbodies
	/// receive impulse; else miss thud.
	/// </summary>
	private void DoKickCast(float kickForce)
	{
		Vector3 origin = footAnchor != null ? footAnchor.position : transform.position;
		Vector3 direction = currentRestraint.GetKickDirection(this);

		RaycastHit[] hits = Physics.SphereCastAll(
			origin,
			kickCastRadius,
			direction,
			kickCastDistance,
			kickCastLayers,
			QueryTriggerInteraction.Ignore);

		Debug.Log($"[KickCast] origin={origin}, dir={direction}, " +
			$"radius={kickCastRadius}, dist={kickCastDistance}, hits={hits.Length}");
		foreach (RaycastHit h in hits)
		{
			Rigidbody hrb = h.collider != null ? h.collider.attachedRigidbody : null;
			string rbInfo = hrb != null
				? $"rb={hrb.name} mass={hrb.mass} kinematic={hrb.isKinematic}"
				: "rb=<none>";
			Debug.Log($"[KickCast]   hit: {h.collider?.name} ({rbInfo}) dist={h.distance:F2}");
		}

		Kickable nearestAcceptingKickable = null;
		float nearestKickableDist = float.MaxValue;
		System.Collections.Generic.HashSet<Rigidbody> looseRigidbodies =
			new System.Collections.Generic.HashSet<Rigidbody>();
		System.Collections.Generic.HashSet<Rigidbody> kickableOwnedRigidbodies =
			new System.Collections.Generic.HashSet<Rigidbody>();

		foreach (RaycastHit hit in hits)
		{
			if (hit.collider == null) continue;

			Kickable owningKickable = hit.collider.GetComponentInParent<Kickable>();
			Rigidbody hitRb = hit.collider.attachedRigidbody;

			// Always exclude the player's own Rigidbody from impulse application,
			// in case a forward cast wraps back through edge geometry. Belt-and-
			// suspenders alongside the foot anchor offset.
			if (hitRb == Rb) continue;

			if (owningKickable != null)
			{
				if (owningKickable.CanBeKicked(this) && hit.distance < nearestKickableDist)
				{
					nearestAcceptingKickable = owningKickable;
					nearestKickableDist = hit.distance;
				}
				// Track Kickable-owned Rigidbodies so we don't double-route them
				// as loose impulse targets even if they're hit by other colliders
				// in the same cast.
				if (hitRb != null) kickableOwnedRigidbodies.Add(hitRb);
			}
			else if (hitRb != null && !hitRb.isKinematic)
			{
				looseRigidbodies.Add(hitRb);
			}
		}

		// Priority 1: accepting Kickable wins. Route force to it; ignore everything else.
		if (nearestAcceptingKickable != null)
		{
			nearestAcceptingKickable.OnKick(this, kickForce);
			return;
		}

		// Priority 2: loose Rigidbodies receive impulse. Exclude any Rigidbody owned
		// by a rejected Kickable (e.g., L4 door pivots when out of zone) — those
		// stay un-impulsed so the off-axis door read remains "wall thud."
		looseRigidbodies.ExceptWith(kickableOwnedRigidbodies);
		if (looseRigidbodies.Count > 0)
		{
			float impulse = kickForce * kickImpulseScale;
			foreach (Rigidbody rb in looseRigidbodies)
			{
				// Apply at the closest point on the rigidbody's collider rather
				// than at center of mass — kicking a tall lamp at the base vs.
				// at the top produces meaningfully different toppling behavior,
				// and we want that to come out for free from the physics.
				Vector3 applyPoint = rb.ClosestPointOnBounds(origin);
				rb.AddForceAtPosition(direction * impulse, applyPoint, ForceMode.Impulse);
				Debug.Log($"[KickCast] impulse={impulse:F2} → {rb.name} (mass={rb.mass}) at {applyPoint}");
			}
			return;
		}

		// Priority 3: nothing useful in range. Miss thud, same as old behavior.
		if (AudioManager.Instance != null && kickMissThudClip != null)
		{
			AudioManager.Instance.PlaySFX(kickMissThudClip, 1f, Random.Range(0.92f, 1.05f));
		}
	}

	System.Collections.IEnumerator ShakeVisual()
	{
		if (visualRoot == null) yield break;
		Quaternion origin = visualRoot.localRotation;

		float direction = Random.value < 0.5f ? -1f : 1f;
		float windupAngle = shakeMagnitude * direction;
		float snapbackAngle = -shakeMagnitude * direction * 1.2f;

		float windupTime = shakeDuration * 0.6f;
		float snapbackTime = shakeDuration * 0.4f;

		float elapsed = 0f;
		while (elapsed < windupTime)
		{
			float t = elapsed / windupTime;
			float eased = t * t;
			float angle = Mathf.Lerp(0f, windupAngle, eased);
			visualRoot.localRotation = origin * Quaternion.Euler(0f, angle, 0f);
			elapsed += Time.deltaTime;
			yield return null;
		}

		elapsed = 0f;
		while (elapsed < snapbackTime)
		{
			float t = elapsed / snapbackTime;
			float eased = 1f - (1f - t) * (1f - t);
			float angle = Mathf.Lerp(windupAngle, snapbackAngle, eased);
			if (t > 0.66f)
			{
				float settleT = (t - 0.66f) / 0.34f;
				angle = Mathf.Lerp(angle, 0f, settleT);
			}
			visualRoot.localRotation = origin * Quaternion.Euler(0f, angle, 0f);
			elapsed += Time.deltaTime;
			yield return null;
		}

		visualRoot.localRotation = origin;
	}

	void TryPickUp()
	{
		if (heldItem != null)
		{
			Debug.Log($"Already holding {heldItem.ItemName}.");
			return;
		}

		InteractableBase nearby = FindNearestInteractable();
		if (nearby == null)
		{
			Debug.Log("Nothing to interact with here.");
			return;
		}

		// OnPickUp is the E-key dispatch hook on InteractableBase, not literally
		// "picking up." Pickupable subclasses use it for the held-item handoff
		// (player.HoldItem + SetActive(false) on world version). Non-Pickupable
		// Interactables (Drawer, future doors, etc.) can override OnPickUp to
		// mean their own E-key verb -- a back-facing drawer opens, a lever
		// flips, etc. Each subclass decides what E means in its context.
		nearby.OnPickUp(this);
	}

	public void HoldItem(Pickupable item)
	{
		heldItem = item;
		Debug.Log($"Picked up {item.ItemName}.");
	}

	public Pickupable GetHeldItem()
	{
		return heldItem;
	}

	/// <summary>
	/// Forcibly remove the player's held item without restoring it to the world.
	/// Used by FailureLoopController to model the guard confiscating a tool
	/// (e.g. the L6 pen) when re-binding Cassie. Distinct from Pickupable's
	/// DropFromPlayer flow, which re-enables the world version — confiscation
	/// is one-way: the item is gone.
	///
	/// The Pickupable's GameObject was already SetActive(false) on pickup
	/// (see Pickupable.OnPickUp); we defensively re-disable it here in case
	/// some future code path put it back. We do NOT Destroy it — same
	/// rationale as ChairRestraint's broken-chair handling: scene-rooted,
	/// inactive, available for debug inspection without scene reload.
	///
	/// No-op if nothing is held.
	/// </summary>
	public void ConfiscateHeldItem()
	{
		if (heldItem == null) return;
		Debug.Log($"[PlayerController] Confiscating held item: {heldItem.ItemName}.");
		if (heldItem.gameObject.activeSelf) heldItem.gameObject.SetActive(false);
		heldItem = null;
	}

	InteractableBase FindNearestInteractable()
	{
		// Broadphase: gather everything within the global interaction sweep.
		// interactionCheckRadius is a generous upper bound; the per-instance
		// filter below is what actually gates which objects accept interaction.
		Collider[] hits = Physics.OverlapSphere(transform.position, interactionCheckRadius, interactableLayer);

		InteractableBase nearest = null;
		float nearestDist = float.MaxValue;

		foreach (Collider hit in hits)
		{
			InteractableBase interactable = hit.GetComponent<InteractableBase>();
			if (interactable == null) continue;
			if (!interactable.gameObject.activeInHierarchy) continue;

			float dist = Vector3.Distance(transform.position, hit.transform.position);

			// Per-instance range filter: each interactable declares its own
			// reach distance via InteractionRange. The drawer's bound-hands
			// verb wants a short range (Cassie has to be close, simulating
			// limited reach behind her back). A pen on a table wants a
			// normal pickup range. The global interactionCheckRadius is the
			// broadphase upper bound; this is the per-object tuning knob.
			if (dist > interactable.InteractionRange) continue;

			if (dist < nearestDist)
			{
				nearest = interactable;
				nearestDist = dist;
			}
		}

		return nearest;
	}

	void EscapeBonds()
	{
		Debug.Log("FREE OF BONDS!");
		if (AudioManager.Instance != null && bondBreakClip != null)
			AudioManager.Instance.PlaySFX(bondBreakClip, 1f, 1f);

		OnPlayerFreed?.Invoke();
	}

	public void SetRestraint(RestraintBase newRestraint)
	{
		if (currentRestraint != null) currentRestraint.OnExit(this);
		currentRestraint = newRestraint;
		if (currentRestraint != null) currentRestraint.OnEnter(this);
		OnRestraintChanged?.Invoke();
	}

	void OnCollisionStay(Collision collision)
	{
		foreach (ContactPoint contact in collision.contacts)
		{
			if (contact.point.y < transform.position.y)
			{
				IsGrounded = true;
				return;
			}
		}
	}

	void OnCollisionExit(Collision collision)
	{
		IsGrounded = false;
	}
}
