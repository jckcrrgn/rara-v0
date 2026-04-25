using UnityEngine;

/// <summary>
/// Chair restraint: player is tied to a chair. Can rotate in place and hop forward.
/// This is the default restraint for Levels 1-3.
/// </summary>
public class ChairRestraint : RestraintBase
{
	[Header("Movement Settings")]
	[SerializeField] private float hopForce = 3f;
	[SerializeField] private float rotationSpeed = 100f;

	public override void HandleMovementInput(PlayerController player)
	{
		// Rotation: A/D (Horizontal axis) rotates the player in place.
		float rotateInput = Input.GetAxis("Horizontal");
		player.transform.Rotate(0f, rotateInput * rotationSpeed * Time.deltaTime, 0f);

		// Hop: W key, but only if grounded (can't hop in mid-air).
		if (Input.GetKeyDown(KeyCode.W) && player.IsGrounded)
		{
			Hop(player);
		}
	}

	private void Hop(PlayerController player)
	{
		Vector3 hopDirection = player.transform.forward + Vector3.up;
		player.Rb.AddForce(hopDirection * hopForce, ForceMode.Impulse);
	}
}
