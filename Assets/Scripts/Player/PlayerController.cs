using UnityEngine;

public class PlayerController : MonoBehaviour
{
	[Header("Movement Settings")]
	[SerializeField] private float hopForce = 3f;
	[SerializeField] private float rotationSpeed = 100f;

	[Header("Interaction Settings")]
	[SerializeField] private float interactionCheckRadius = 1.5f;
	[SerializeField] private LayerMask interactableLayer = ~0;

	private Rigidbody rb;
	private bool isGrounded;

	void Start()
	{
		rb = GetComponent<Rigidbody>();
	}

	void Update()
	{
		// Rotate with A/D
		float rotateInput = Input.GetAxis("Horizontal");
		transform.Rotate(0f, rotateInput * rotationSpeed * Time.deltaTime, 0f);

		// Hop forward only on fresh W press AND when grounded
		if (Input.GetKeyDown(KeyCode.W) && isGrounded)
		{
			Hop();
		}

		// Struggle verb (Spacebar for now)
		if (Input.GetKeyDown(KeyCode.Space))
		{
			TryStruggle();
		}
	}

	void Hop()
	{
		Vector3 hopDirection = transform.forward + Vector3.up;
		rb.AddForce(hopDirection * hopForce, ForceMode.Impulse);
	}

	void TryStruggle()
	{
		// Find any interactables within range
		Collider[] hits = Physics.OverlapSphere(transform.position, interactionCheckRadius, interactableLayer);

		foreach (Collider hit in hits)
		{
			InteractableBase interactable = hit.GetComponent<InteractableBase>();
			if (interactable != null)
			{
				interactable.OnStruggle(this);
				return; // Only struggle against the first valid interactable found
			}
		}

		Debug.Log("Nothing to struggle against here.");
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