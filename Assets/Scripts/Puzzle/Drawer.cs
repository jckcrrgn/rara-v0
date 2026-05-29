using System.Collections;
using UnityEngine;

// Slides forward on local Z when Open() is called. Two ways to trigger:
//   1. Wire a Jostleable.OnJostleComplete / Bumpable.OnBumped event to Open()
//      in the inspector (L3 pattern -- desk-bump cumulative jostle).
//   2. Set requireBackFacing = true and put Drawer on a layer the player's
//      interaction sweep sees. Player presses E while bound-hands range
//      reaches the drawer -- diegetic for chair-bound or floor-bound Cassie
//      whose hands are tied behind her back (L6 pattern).
//
// Loose-contents rattle: also exposes OnProgress(float), wired to a
// Jostleable's OnJostleProgress event. Every bump that registers above
// rattleThreshold plays a rattle SFX, scaled in volume by progress. Diegetic
// signpost that there's something inside worth shaking loose -- and unlike a
// fire-once cue, it stays present and intensifies as the drawer gets closer
// to popping, mirroring the desk's own creak.
//
// Contents (e.g. pen, scissors) are kept disabled until the drawer finishes
// opening, so the player can't pick them up through closed geometry.
public class Drawer : InteractableBase
{
	[Header("Slide Settings")]
	[Tooltip("How far the drawer slides on the slideAxis when opened.")]
	[SerializeField] private float slideDistance = 0.35f;

	[Tooltip("Local-space direction the drawer opens. Default (0,0,1) is local +Z. " +
	         "Use (-1,0,0) for a drawer that opens westward in world space when " +
	         "the GameObject has no rotation, etc. Will be normalized at use.")]
	[SerializeField] private Vector3 slideAxis = Vector3.forward;

	[Tooltip("Seconds the tween takes.")]
	[SerializeField] private float slideDuration = 0.4f;

	[Header("Bound-Hands Interaction (L6+)")]
	[Tooltip("If true, OnPickUp (E key) opens the drawer only when the player " +
	         "is facing AWAY from it -- simulating bound hands reaching behind. " +
	         "Leave false for L3-style bump-to-open drawers, which only respond " +
	         "to Jostleable/Bumpable event wiring.")]
	[SerializeField] private bool requireBackFacing = false;

	[Tooltip("Dot product threshold for the back-facing gate. 0.7 ~= 45 degree " +
	         "cone behind the player. Higher = stricter alignment required.")]
	[Range(0f, 1f)]
	[SerializeField] private float backFacingThreshold = 0.7f;

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

	[Header("Gated Pickup (L6+)")]
	[Tooltip("Interactable that stays VISIBLE from scene start (signpost) but is NOT " +
		 "pickupable until the drawer finishes opening. Unlike 'contents' (hidden " +
		 "entirely via SetActive until open), the gated pickup's mesh shows the whole " +
		 "time -- only its interaction collider is toggled. L6 pen: visible peeking out " +
		 "of the ajar drawer, but the drawer must be opened to fish it out. Null on L3.")]
	[SerializeField] private Collider gatedPickupCollider;

	[Header("Ajar State (L6+)")]
	[Tooltip("How far the drawer is already slid open at scene start, along slideAxis. " +
		 "The position you place this GameObject at in the scene IS the ajar resting " +
		 "position; this value tells Awake how far back the *true* closed position is, " +
		 "so Open() slides the full slideDistance from closed rather than from ajar. " +
		 "0 = starts fully closed (L3 behaviour). Set > 0 on L6 so the pen shows.")]
	[SerializeField] private float ajarOffset = 0f;

	private Vector3 closedLocalPos;
	private bool isOpen = false;
	private bool isOpening = false;

	void Awake()
	{
		// closedLocalPos = transform.localPosition;   // old
		closedLocalPos = transform.localPosition - slideAxis.normalized * ajarOffset;

		// Make sure contents start hidden. Belt-and-braces -- you should also
		// disable them in the scene, but this guarantees it.
		foreach (GameObject obj in contents)
		{
			if (obj != null) obj.SetActive(false);
		}

		// Gated pickup (L6 pen): visible from the start, interaction collider off so it
		// can't be targeted until the drawer opens. Mesh untouched.
		if (gatedPickupCollider != null)
			gatedPickupCollider.enabled = false;
	}

	// InteractableBase hook. Fires when the player presses E with this as the
	// nearest interactable. For bump-only drawers (L3), this is a no-op --
	// they only open via the Jostleable event wiring. For back-facing drawers
	// (L6+), this is the bound-hands open verb, gated by player facing.
	//
	// Note: "OnPickUp" is the player-facing verb (press E), not literally
	// "pick up." Future refactor candidate: rename to OnInteract on
	// InteractableBase. Not blocking.
	public override void OnPickUp(PlayerController player)
	{
		if (!requireBackFacing) return;
		if (isOpen || isOpening) return;

		Vector3 dirToDrawer = (transform.position - player.transform.position).normalized;
		float backwardness = Vector3.Dot(-player.transform.forward, dirToDrawer);

		if (backwardness < backFacingThreshold)
		{
			Debug.Log($"Drawer ({name}): not back-facing (dot={backwardness:F2} < {backFacingThreshold}). " +
			          $"Cassie's hands can't reach.");
			return;
		}

		Debug.Log($"Drawer ({name}): bound-hands open (dot={backwardness:F2}).");
		Open();
	}

	// Public entry point. Wire this to Jostleable.OnJostleComplete (or
	// Bumpable.OnBumped) in the inspector for L3-style bump-to-open.
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
		Vector3 end = closedLocalPos + slideAxis.normalized * slideDistance;

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

		// Reveal contents -- pen / scissors become pickupable now.
		foreach (GameObject obj in contents)
		{
			if (obj != null) obj.SetActive(true);
		}

		// Drawer's open now -- pen becomes fishable.
		if (gatedPickupCollider != null)
			gatedPickupCollider.enabled = true;

		isOpen = true;
		isOpening = false;
	}
}
