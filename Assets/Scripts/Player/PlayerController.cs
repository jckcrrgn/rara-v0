using UnityEngine;

public class PlayerController : MonoBehaviour
{
	[Header("Movement Settings")]
	[SerializeField] private float hopForce = 3f;
	[SerializeField] private float hopCooldown = 0.5f;
	[SerializeField] private float rotationSpeed = 100f;

	[Header("Ground Check")]
	[SerializeField] private float groundCheckDistance = 0.6f;
	[SerializeField] private LayerMask groundLayer = ~0; // Everything by default

	private Rigidbody rb;
	private float lastHopTime;

	void Start()
	{
		rb = GetComponent<Rigidbody>();
	}

	void Update()
	{
		// Rotate with A/D
		float rotateInput = Input.GetAxis("Horizontal");
		transform.Rotate(0f, rotateInput * rotationSpeed * Time.deltaTime, 0f);

		// Hop forward with W (when grounded and cooldown passed)
		if (Input.GetKey(KeyCode.W) && IsGrounded() && Time.time - lastHopTime > hopCooldown)
		{
			Hop();
		}
	}

	void Hop()
	{
		Vector3 hopDirection = transform.forward + Vector3.up;
		rb.AddForce(hopDirection * hopForce, ForceMode.Impulse);
		lastHopTime = Time.time;
	}

	bool IsGrounded()
	{
		// Cast a short ray downward from the cube's center to check for ground
		return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
	}
}