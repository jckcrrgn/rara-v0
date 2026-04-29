using UnityEngine;

public class PlayerController : MonoBehaviour
{
	// CHANGED: Movement settings (hopForce, rotationSpeed) moved to ChairRestraint.
	// Movement is now the restraint's job, not the player's.

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
	[SerializeField] private float shakeMagnitude = 0.08f;

	[Header("SFX")]
	[SerializeField] private AudioClip struggleSuccessClip;
	[SerializeField] private AudioClip struggleFailClip;
	[SerializeField] private AudioClip bondBreakClip;

	// NEW: The active restraint. Assign in Inspector, or auto-find on the GameObject.
	[Header("Restraint")]
	[SerializeField] private RestraintBase currentRestraint;

	// NEW: Rigidbody is now exposed so restraints can apply forces to it.
	// Restraints need access to physics; PlayerController owns the rb but lets restraints use it.
	public Rigidbody Rb { get; private set; }

	// NEW: Grounding moved here as a public field because multiple restraints may care about it,
	// but each restraint decides what "grounded" means for movement purposes.
	public bool IsGrounded { get; private set; }

	// Public accessors kept for BondMeterUI compatibility
	public int StruggleProgress => bond != null ? bond.StruggleProgress : 0;
	public int BondStrength => bond != null ? bond.BondStrength : 1;
	public System.Action OnStruggleProgressChanged;
	public System.Action OnPlayerFreed;

	void Start()
	{
		Rb = GetComponent<Rigidbody>();

		if (bond != null)
		{
			bond.OnProgressChanged += () => OnStruggleProgressChanged?.Invoke();
			bond.OnBroken += EscapeBonds;
		}

		// NEW: If no restraint assigned in Inspector, try to find one on this GameObject.
		// This lets you just slap a ChairRestraint component on the Player and it works.
		if (currentRestraint == null)
		{
			currentRestraint = GetComponent<RestraintBase>();
		}

		if (currentRestraint != null)
		{
			currentRestraint.OnEnter(this);
		}
		else
		{
			Debug.LogWarning("PlayerController has no RestraintBase. Movement will not work.");
		}
	}

	void Update()
	{
		// CHANGED: All movement input (rotation, W-key hop) is delegated to the restraint.
		// PlayerController no longer knows or cares HOW the player moves.
		if (currentRestraint != null)
		{
			currentRestraint.HandleMovementInput(this);
		}

		// Struggle and Pick Up are universal verbs — they work the same regardless of restraint.
		// (The restraint can still influence them via GetStruggleModifier / CanStruggle.)
		if (Input.GetKeyDown(KeyCode.Space))
		{
			TryStruggle();
		}

		if (Input.GetKeyDown(KeyCode.E))
		{
			TryPickUp();
		}
	}

	// REMOVED: Hop() — moved to ChairRestraint.

	void TryStruggle()
	{
		if (bond == null)
		{
			Debug.LogWarning("No Bond assigned to player.");
			return;
		}

		// NEW: Restraint can gate struggle entirely (e.g., a future "gagged" state might block it,
		// or a phase where the player can't struggle yet).
		if (currentRestraint != null && !currentRestraint.CanStruggle())
		{
			return;
		}

		// Start with whatever's in our hands (BareHands by default)
		ToolType activeTool = heldItem != null ? heldItem.ToolType : ToolType.BareHands;
		int struggleAmount = bond.GetStruggleProgress(activeTool);

		InteractableBase nearby = FindNearestInteractable();

		// Post-break: Struggle redirects from the (broken) bond to a windup target.
		// First concrete case is KickableDoor on L4. Future: L15 guard.
		if (bond.IsBroken && nearby is KickableDoor door)
		{
			// Windup runs on its own track: no bond progress, no restraint modifier,
			// no struggle SFX (door plays its own windup clip).
			door.OnWindup(this);
			return;
		}

		// Pre-break: tools nearby modify struggle effectiveness against the bond.
		if (nearby is EnvironmentalTool envTool)
		{
			struggleAmount += bond.GetStruggleProgress(envTool.ToolType);
			envTool.OnStruggle(this);
		}

		// NEW: Restraint can scale the result (e.g., FloorRestraint might make struggle slightly
		// more effective because you can use your whole body, or duct tape might make it weaker).
		if (currentRestraint != null)
		{
			struggleAmount = Mathf.RoundToInt(struggleAmount * currentRestraint.GetStruggleModifier());
		}

		if (struggleAmount <= 0)
		{
			StartCoroutine(ShakeVisual());
			if (AudioManager.Instance != null && struggleFailClip != null)
				AudioManager.Instance.PlaySFX(struggleFailClip, 1f, Random.Range(0.95f, 1.05f));
		}
		else
		{
			if (AudioManager.Instance != null && struggleSuccessClip != null)
				AudioManager.Instance.PlaySFX(struggleSuccessClip, 1f, Random.Range(0.92f, 1.08f));
		}

		bond.ApplyStruggle(struggleAmount);
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
		if (nearby is Pickupable pickupable)
		{
			pickupable.OnPickUp(this);
		}
		else
		{
			Debug.Log("Nothing to pick up here.");
		}
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

	InteractableBase FindNearestInteractable()
	{
		Collider[] hits = Physics.OverlapSphere(transform.position, interactionCheckRadius, interactableLayer);

		InteractableBase nearest = null;
		float nearestDist = float.MaxValue;

		foreach (Collider hit in hits)
		{
			InteractableBase interactable = hit.GetComponent<InteractableBase>();
			if (interactable == null) continue;
			if (!interactable.gameObject.activeInHierarchy) continue;

			float dist = Vector3.Distance(transform.position, hit.transform.position);
			if (dist < nearestDist)
			{
				nearest = interactable;
				nearestDist = dist;
			}
		}

		return nearest;
	}

	/// <summary>
	/// Fires when the player breaks free of their bonds. Per-level
	/// win-condition scripts (e.g. BondBreakWinCondition) listen for this.
	/// PlayerController itself is no longer responsible for level completion.
	/// </summary>
	

	void EscapeBonds()
	{
		Debug.Log("FREE OF BONDS!");
		if (AudioManager.Instance != null && bondBreakClip != null)
			AudioManager.Instance.PlaySFX(bondBreakClip, 1f, 1f);

		OnPlayerFreed?.Invoke();
	}

	// NEW: Public method so restraints (or other systems) can swap which restraint is active.
	// Useful later for the "freed mid-level, now floor-restrained" scenario from the GDD.
	public void SetRestraint(RestraintBase newRestraint)
	{
		if (currentRestraint != null) currentRestraint.OnExit(this);
		currentRestraint = newRestraint;
		if (currentRestraint != null) currentRestraint.OnEnter(this);
	}

	// CHANGED: Grounding kept here because it's a physics fact about the player's body,
	// not a restraint-specific concept. Restraints can read IsGrounded if they care.
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
