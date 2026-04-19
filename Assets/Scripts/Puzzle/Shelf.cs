using UnityEngine;

public class Shelf : MonoBehaviour
{
	[SerializeField] private Rigidbody boxCutterRigidbody;
	[SerializeField] private Vector3 fallImpulse = new Vector3(0f, 1f, -2f);

	private bool hasTriggered = false;

	void OnCollisionEnter(Collision collision)
	{
		if (hasTriggered) return;
		if (collision.gameObject.GetComponent<PlayerController>() == null) return;

		hasTriggered = true;
		boxCutterRigidbody.isKinematic = false;
		boxCutterRigidbody.AddForce(fallImpulse, ForceMode.Impulse);
	}
}