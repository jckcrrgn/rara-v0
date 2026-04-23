using System.Collections;
using UnityEngine;

// Slides forward on local Z when Open() is called. Hook a Bumpable's
// OnBumped UnityEvent to this component's Open() method in the inspector.
//
// Contents (e.g. scissors) are kept disabled until the drawer finishes
// opening, so the player can't pick them up through closed geometry.
public class Drawer : MonoBehaviour
{
	[Header("Slide Settings")]
	[Tooltip("How far the drawer slides on its local Z axis when opened.")]
	[SerializeField] private float slideDistance = 0.35f;

	[Tooltip("Seconds the tween takes.")]
	[SerializeField] private float slideDuration = 0.4f;

	[Header("Contents")]
	[Tooltip("Objects (usually a Pickupable) to enable once the drawer is fully open.")]
	[SerializeField] private GameObject[] contents;

	[Header("Feedback")]
	[SerializeField] private AudioClip openClip;

	private Vector3 closedLocalPos;
	private bool isOpen = false;
	private bool isOpening = false;

	void Awake()
	{
		closedLocalPos = transform.localPosition;

		// Make sure contents start hidden. Belt-and-braces -- you should also
		// disable them in the scene, but this guarantees it.
		foreach (GameObject obj in contents)
		{
			if (obj != null) obj.SetActive(false);
		}
	}

	// Public entry point. Wire this to Bumpable.OnBumped in the inspector.
	public void Open()
	{
		if (isOpen || isOpening) return;
		StartCoroutine(SlideOpen());
	}

	IEnumerator SlideOpen()
	{
		isOpening = true;

		if (AudioManager.Instance != null && openClip != null)
			AudioManager.Instance.PlaySFX(openClip, 1f, Random.Range(0.97f, 1.03f));

		Vector3 start = closedLocalPos;
		Vector3 end = closedLocalPos + Vector3.forward * slideDistance;

		float elapsed = 0f;
		while (elapsed < slideDuration)
		{
			float t = elapsed / slideDuration;
			// Ease-out: drawer decelerates as it opens, feels like it hits a stop.
			float eased = 1f - (1f - t) * (1f - t);
			transform.localPosition = Vector3.Lerp(start, end, eased);
			elapsed += Time.deltaTime;
			yield return null;
		}

		transform.localPosition = end;

		// Reveal contents -- scissors become pickupable now.
		foreach (GameObject obj in contents)
		{
			if (obj != null) obj.SetActive(true);
		}

		isOpen = true;
		isOpening = false;
	}
}
