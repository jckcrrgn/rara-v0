using UnityEngine;

public class PlayerController : MonoBehaviour
{
	[Header("Movement Settings")]
	[SerializeField] private float hopForce = 3f;
	[SerializeField] private float rotationSpeed = 100f;

	[Header("Interaction Settings")]
	[SerializeField] private float interactionCheckRadius = 1.5f;
	[SerializeField] private LayerMask interactableLayer = ~0;

	[Header("Bonds")]
	[SerializeField] private int bondStrength = 25; // How much progress needed to escape
	[SerializeField] private int struggleProgress = 0;
	[SerializeField] private int bareHandsStruggleAmount = 1;

	[Header("Held Item")]
	[SerializeField] private Pickupable heldItem = null;

	private Rigidbody rb;
	private bool isGrounded;

	void Start()
	{
		rb = GetComponent<Rigidbody>();
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
		// Calculate struggle power: bare hands + held item bonus + nearby nail bonus
		int struggleAmount = bareHandsStruggleAmount;

		if (heldItem != null)
		{
			struggleAmount += heldItem.StruggleModifier;
		}

		// Check for nearby struggle-boosting interactables (like a nail)
		InteractableBase nearby = FindNearestInteractable();
		if (nearby != null && !(nearby is Pickupable))
		{
			struggleAmount += nearby.StruggleModifier;
			nearby.OnStruggle(this);
		}

		struggleProgress += struggleAmount;
		Debug.Log($"Struggle: +{struggleAmount} (total {struggleProgress}/{bondStrength})");

		if (struggleProgress >= bondStrength)
		{
			EscapeBonds();
		}
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