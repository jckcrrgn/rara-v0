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

	[Header("Feedback")]
	[SerializeField] private Transform visualRoot; // assign to child holding the mesh
	[SerializeField] private float shakeDuration = 0.2f;
	[SerializeField] private float shakeMagnitude = 0.08f;

	[Header("SFX")]
	[SerializeField] private AudioClip struggleSuccessClip;
	[SerializeField] private AudioClip struggleFailClip;
	[SerializeField] private AudioClip bondBreakClip;

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

		// Check for an environmental tool nearby -- stacks on top of held tool
		InteractableBase nearby = FindNearestInteractable();
		if (nearby is EnvironmentalTool envTool)
		{
			struggleAmount += bond.GetStruggleProgress(envTool.ToolType);
			envTool.OnStruggle(this);
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

		// Pick a direction (left or right twist) and magnitude in degrees
		float direction = Random.value < 0.5f ? -1f : 1f;
		float windupAngle = shakeMagnitude * direction;
		float snapbackAngle = -shakeMagnitude * direction * 1.2f; // overshoot past origin

		float windupTime = shakeDuration * 0.6f;   // slower windup
		float snapbackTime = shakeDuration * 0.4f; // faster snapback + settle

		// Windup: ease-in twist toward windupAngle
		float elapsed = 0f;
		while (elapsed < windupTime)
		{
			float t = elapsed / windupTime;
			float eased = t * t; // ease-in
			float angle = Mathf.Lerp(0f, windupAngle, eased);
			visualRoot.localRotation = origin * Quaternion.Euler(0f, angle, 0f);
			elapsed += Time.deltaTime;
			yield return null;
		}

		// Snapback: rapid swing past origin, then settle to 0
		elapsed = 0f;
		while (elapsed < snapbackTime)
		{
			float t = elapsed / snapbackTime;
			float eased = 1f - (1f - t) * (1f - t); // ease-out
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
		Debug.Log("ESCAPED THE BONDS!");
		if (AudioManager.Instance != null && bondBreakClip != null)
			AudioManager.Instance.PlaySFX(bondBreakClip, 1f, 1f);

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