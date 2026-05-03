using System.Collections;
using UnityEngine;

// Slides forward on local Z when Open() is called. Hook a Jostleable's (or
// Bumpable's) OnJostleComplete / OnBumped UnityEvent to this component's
// Open() method in the inspector.
//
// Loose-contents rattle: also exposes OnProgress(float), wired to a
// Jostleable's OnJostleProgress event. Every bump that registers above
// rattleThreshold plays a rattle SFX, scaled in volume by progress. Diegetic
// signpost that there's something inside worth shaking loose -- and unlike a
// fire-once cue, it stays present and intensifies as the drawer gets closer
// to popping, mirroring the desk's own creak.
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

	[Tooltip("Played on each registering bump where progress >= rattleThreshold. " +
	         "Volume scales with progress (subtle on first bump, louder as the desk " +
	         "gets closer to giving up the drawer). Wire OnProgress to a Jostleable's " +
	         "OnJostleProgress event in the inspector.")]
	[SerializeField] private AudioClip rattleClip;

	[Tooltip("Minimum progress (0..1) for the rattle to play at all. Filters out " +
	         "very-soft bumps that barely register. Default 0.1 means the rattle " +
	         "fires on essentially every registered bump.")]
	[Range(0f, 1f)]
	[SerializeField] private float rattleThreshold = 0.1f;

	[Tooltip("Volume at progress=0 and progress=1. Linear interpolation between.")]
	[SerializeField] private Vector2 rattleVolumeRange = new Vector2(0.5f, 1.0f);

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

	// Public entry point. Wire this to Jostleable.OnJostleComplete (or Bumpable.OnBumped)
	// in the inspector.
	public void Open()
	{
		if (isOpen || isOpening) return;
		StartCoroutine(SlideOpen());
	}

	// Wire this to Jostleable.OnJostleProgress in the inspector. Plays the
	// rattle on every registered bump above threshold, with volume scaling by
	// progress -- contents of the drawer respond more loudly as the disturbance
	// builds. Mirrors the desk's own creak escalation.
	public void OnProgress(float progress)
	{
		if (progress < rattleThreshold) return;

		if (AudioManager.Instance != null && rattleClip != null)
		{
			float volume = Mathf.Lerp(rattleVolumeRange.x, rattleVolumeRange.y, progress);
			AudioManager.Instance.PlaySFX(rattleClip, volume, Random.Range(0.95f, 1.05f));
		}
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
