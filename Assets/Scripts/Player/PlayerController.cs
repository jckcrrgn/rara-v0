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
	[SerializeField] private float shakeMagnitude = 0.08f;

	[Header("SFX")]
	[SerializeField] private AudioClip struggleSuccessClip;
	[SerializeField] private AudioClip struggleFailClip;
	[SerializeField] private AudioClip bondBreakClip;
	[Tooltip("Grunt of exertion. Plays on every kick attempt regardless of target.")]
	[SerializeField] private AudioClip kickEffortClip;
	[Tooltip("Default thud when Kick lands on nothing Kickable (or a Kickable rejecting the kick). " +
		"Kickables play their own per-hit SFX, so this only fires for misses.")]
	[SerializeField] private AudioClip kickMissThudClip;

	[Header("Restraint")]
	[SerializeField] private RestraintBase currentRestraint;

	public Rigidbody Rb { get; private set; }
	public bool IsGrounded { get; private set; }

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
		if (currentRestraint != null)
		{
			currentRestraint.HandleMovementInput(this);
		}

		if (Input.GetKeyDown(KeyCode.Space))
		{
			TryStruggle();
		}

		if (Input.GetKeyDown(KeyCode.E))
		{
			TryPickUp();
		}

		// NEW: Kick is now its own verb. Effectiveness scaled by restraint
		// (free legs = full force, floor-bound = reduced, hogtied = zero).
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

		// CHANGED: The bond.IsBroken / KickableDoor redirect is GONE. Struggle is now
		// purely bond-work. Door-kicking is the Kick verb's job. This resolves the
		// "why am I struggling against tape if I just need to kick" design smell.

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
		}
		else
		{
			if (AudioManager.Instance != null && struggleSuccessClip != null)
				AudioManager.Instance.PlaySFX(struggleSuccessClip, 1f, Random.Range(0.92f, 1.08f));
		}

		bond.ApplyStruggle(struggleAmount);
	}

	/// <summary>
	/// Kick verb. Strikes outward with the legs.
	/// - Force is scaled by the restraint's GetKickModifier (free=1.0, floor=~0.5, hogtied=0).
	/// - If the nearest interactable is a Kickable that accepts the kick, route force to it.
	/// - Otherwise (no target, wrong target, or position gate failing): play the thud SFX.
	///   This is the "kicking the wall of the van" feedback — emergent, in-character, free.
	/// </summary>
	void TryKick()
	{
		if (currentRestraint == null) return;

		float kickForce = currentRestraint.GetKickModifier();
		//if hogtied, kick is suppressed
		if (kickForce <= 0f) return;

		// Effort layer: plays on every kick, always.
		if (AudioManager.Instance != null && kickEffortClip != null)
		{
			AudioManager.Instance.PlaySFX(kickEffortClip, 1f, Random.Range(0.95f, 1.08f));
		}

		InteractableBase nearby = FindNearestInteractable();

		if (nearby is Kickable kickable && kickable.CanBeKicked(this))
		{
			kickable.OnKick(this, kickForce);
			// Kickable plays its own impact SFX in OnKickRegistered.
			return;
		}

		// Miss layer: kicked nothing useful. Plays alongside the grunt.
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
