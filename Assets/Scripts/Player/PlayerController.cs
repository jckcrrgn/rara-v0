using UnityEngine;

public class PlayerController : MonoBehaviour
{
	[Header("Movement Settings")]
	[SerializeField] private float hopForce = 3f;
	[SerializeField] private float rotationSpeed = 100f;

	[Header("Interaction Settings")]
	[SerializeField] private float interactionCheckRadius = 1.5f;
	[SerializeField] private LayerMask interactableLayer = ~0;

	[Header("Bond")]
	[SerializeField] private Bond bond;

	[Header("Held Item")]
	[SerializeField] private Pickupable heldItem = null;

	private Rigidbody rb;
	private bool isGrounded;

	// Public accessors kept for BondMeterUI compatibility
	public int StruggleProgress => bond != null ? bond.StruggleProgress : 0;
	public int BondStrength => bond != null ? bond.BondStrength : 1;
	public System.Action OnStruggleProgressChanged;

	void Start()
	{
		rb = GetComponent<Rigidbody>();

		if (bond != null)
		{
			bond.OnProgressChanged += () => OnStruggleProgressChanged?.Invoke();
			bond.OnBroken += EscapeBonds;
		}
	}

	void Update()
	{
		float rotateInput = Input.GetAxis("Horizontal");
		transform.Rotate(0f, rotateInput * rotationSpeed * Time.deltaTime, 0f);

		if (Input.GetKeyDown(KeyCode.W) && isGrounded)
		{
			Hop();
		}

		if (Input.GetKeyDown(KeyCode.Space))
		{
			TryStruggle();
		}

		if (Input.GetKeyDown(KeyCode.E))
		{
			TryPickUp();
		}
	}

	void Hop()
	{
		Vector3 hopDirection = transform.forward + Vector3.up;
		rb.AddForce(hopDirection * hopForce, ForceMode.Impulse);
	}

	void TryStruggle()
	{
		if (bond == null)
		{
			Debug.LogWarning("No Bond assigned to player.");
			return;
		}

		// Start with whatever's in our hands (BareHands by default)
		ToolType activeTool = heldItem != null ? heldItem.ToolType : ToolType.BareHands;
		int struggleAmount = bond.GetStruggleProgress(activeTool);

		// Check for an environmental tool nearby — stacks on top of held tool
		InteractableBase nearby = FindNearestInteractable();
		if (nearby is EnvironmentalTool envTool)
		{
			struggleAmount += bond.GetStruggleProgress(envTool.ToolType);
			envTool.OnStruggle(this);
		}

		bond.ApplyStruggle(struggleAmount);
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
		Debug.Log("ESCAPED THE BONDS!");
		if (LevelManager.Instance != null)
		{
			LevelManager.Instance.CompleteLevel();
		}
	}

	void OnCollisionStay(Collision collision)
	{
		foreach (ContactPoint contact in collision.contacts)
		{
			if (contact.point.y < transform.position.y)
			{
				isGrounded = true;
				return;
			}
		}
	}

	void OnCollisionExit(Collision collision)
	{
		isGrounded = false;
	}
}