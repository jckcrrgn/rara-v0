using UnityEngine;
using TMPro;

/// <summary>
/// Floating "Strike (H)" prompt that pins to the guard on-screen during the
/// strike window. Shown only when PlayerController.CanStrikeNow() returns true
/// — wrists free, weapon held, guard in LeanIn, AND guard within strike range.
/// Pressing H dismisses the lean-in mutter and lands the strike in one press
/// (see PlayerController.Update).
///
/// PRESENTATION
/// ------------
/// Intentionally NOT a verb hint card (corner panel). It's a screen-projected
/// floating prompt that follows the guard — the player's eye is already on him
/// during LeanIn, so the cue lives there. Same family as Splinter Cell's
/// "press F to grab" or Hitman's contextual interaction prompts. The guard's
/// world position is projected to screen space each LateUpdate; the prompt's
/// RectTransform follows.
///
/// Screen-Space (Overlay) Canvas, not world-space — keeps the prompt crisp at
/// fixed pixel size regardless of camera distance, and avoids the billboard /
/// world-canvas scale fights that would come with going truly diegetic. The
/// follow happens in LateUpdate so the guard's coroutine-driven movement and
/// the camera have both settled for the frame.
///
/// VISIBILITY GATE
/// ---------------
/// Polls PlayerController.CanStrikeNow each frame. Polling is the honest choice
/// here: distance changes continuously as the guard walks in, and there's no
/// "now in range" event to subscribe to. The check is cheap — a flag read,
/// a weapon check, a state read, a distance test.
///
/// VISIBILITY MECHANISM — IMPORTANT
/// --------------------------------
/// This script does NOT use SetActive to hide the prompt when the script lives
/// on the same GameObject as the visible element (the common case — script on
/// the TMP_Text GameObject). SetActive(false) on this same GameObject would
/// kill LateUpdate, leaving the prompt permanently hidden with no way to
/// re-enable. Instead we toggle the TMP_Text's `enabled` flag, which hides the
/// rendered text but keeps the GameObject — and therefore this script — alive
/// and polling.
///
/// The optional `promptRoot` field exists for the wrapper-GameObject pattern,
/// where the script lives on a parent and toggles a child. In that case it's
/// safe to SetActive the child, and we do.
///
/// SETUP
/// -----
/// - Add a Screen Space - Overlay Canvas to the VS scene (or reuse the existing
///   UI canvas if there is one).
/// - Add a child GameObject with a TMP_Text component reading "Strike (H)".
///   Size and style to taste; a small pill or chevron-flanked label reads best.
/// - Put this component on that GameObject.
/// - Leave `promptRoot`, `label`, `guardBody`, `player`, and `cam` unwired —
///   they auto-resolve. (Wire `promptRoot` only if you've built a wrapper-GO
///   pattern with a separate visual root.)
/// - The RectTransform's anchor/pivot should be center-center so screen-space
///   position lands the prompt centered over the guard.
/// </summary>
public class StrikeHintPrompt : MonoBehaviour
{
	[Header("Visual")]
	[Tooltip("Optional. If wired to a SEPARATE GameObject from this one (a child or " +
		"sibling that holds the visual), this script will SetActive it for show/hide. " +
		"If left blank or pointed at this same GameObject, the script will instead " +
		"toggle the auto-resolved TMP_Text's `enabled` flag — required to avoid " +
		"deactivating the GameObject this script lives on (which would kill LateUpdate).")]
	[SerializeField] private GameObject promptRoot;

	[Tooltip("The TMP_Text whose `enabled` flag is toggled for show/hide when " +
		"`promptRoot` isn't a separate GameObject. Auto-resolved via " +
		"GetComponentInChildren at Awake if left blank — the typical case " +
		"(script on the TMP_Text GO).")]
	[SerializeField] private TMP_Text label;

	[Header("Positioning")]
	[Tooltip("Vertical offset above the guard's body origin, in world metres. " +
		"Lifts the prompt above his head so it doesn't overlap his face. Tune " +
		"to taste — ~1.7–2.0m reads as 'above his head' for a human-sized actor; " +
		"the graybox cube is shorter, so 1.0–1.3m may read better there.")]
	[SerializeField] private float worldOffsetY = 1.8f;

	[Header("References (auto-resolved if blank)")]
	[Tooltip("The transform to follow. If unwired, resolves to the scene's " +
		"StrikeableGuard transform — same single-guard assumption " +
		"PlayerController uses for the strike target.")]
	[SerializeField] private Transform guardBody;

	[Tooltip("The PlayerController whose CanStrikeNow drives visibility. If " +
		"unwired, resolves via FindFirstObjectByType at Start.")]
	[SerializeField] private PlayerController player;

	[Tooltip("Camera used to project the guard's world position to screen space. " +
		"If unwired, defaults to Camera.main.")]
	[SerializeField] private Camera cam;

	private RectTransform rt;

	// Tracks the currently-applied visibility so SetVisible can no-op when
	// nothing has changed. Initialized true so the first SetVisible(false) in
	// Awake actually applies (the GameObject and label both start enabled in
	// the scene).
	private bool currentlyVisible = true;

	// True when promptRoot is a SEPARATE GameObject from this one. Set once in
	// Awake; determines whether SetVisible toggles SetActive or label.enabled.
	private bool useSeparateRoot;

	void Awake()
	{
		rt = GetComponent<RectTransform>();

		// Auto-resolve the label (the typical setup has the TMP_Text on this
		// same GameObject, since that's the visible element). includeInactive
		// is true so a label that someone set inactive in the editor still resolves.
		if (label == null) label = GetComponentInChildren<TMP_Text>(includeInactive: true);

		// Decide once which visibility mechanism we'll use. Using a separate
		// root is safe — we can SetActive it without killing this script.
		// Using "self" requires toggling the label's enabled flag instead.
		useSeparateRoot = promptRoot != null && promptRoot != gameObject;

		// Start hidden. Neither path below deactivates THIS GameObject, so
		// LateUpdate runs normally from frame 1.
		SetVisible(false);
	}

	void Start()
	{
		if (player == null) player = FindFirstObjectByType<PlayerController>();
		if (cam == null) cam = Camera.main;

		// Same resolution pattern PlayerController.GetStrikeableGuard uses, so
		// both components agree on which transform represents the guard.
		if (guardBody == null)
		{
			StrikeableGuard sg = FindFirstObjectByType<StrikeableGuard>();
			if (sg != null) guardBody = sg.transform;
		}

		if (player == null)
			Debug.LogWarning("[StrikeHintPrompt] No PlayerController found — prompt will never show.");
		if (guardBody == null)
			Debug.LogWarning("[StrikeHintPrompt] No StrikeableGuard transform found — prompt will never show.");
		if (cam == null)
			Debug.LogWarning("[StrikeHintPrompt] No camera available — prompt will never show.");
		if (label == null && !useSeparateRoot)
			Debug.LogWarning("[StrikeHintPrompt] No TMP_Text label found on this GameObject and no " +
				"separate promptRoot wired — prompt has nothing to show or hide.");
	}

	void LateUpdate()
	{
		// Defensive: if anything's missing, ensure hidden and bail. Avoids
		// noisy per-frame errors if the scene is mid-setup.
		if (player == null || guardBody == null || cam == null)
		{
			SetVisible(false);
			return;
		}

		bool show = player.CanStrikeNow();

		if (!show)
		{
			SetVisible(false);
			return;
		}

		// Project the guard's world position (lifted by worldOffsetY) into
		// screen space.
		Vector3 worldPos = guardBody.position + Vector3.up * worldOffsetY;
		Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

		// Guard behind the camera — projection wraps weirdly (negative z). Hide.
		// In the VS this shouldn't normally happen, but it's cheap insurance.
		if (screenPos.z < 0f)
		{
			SetVisible(false);
			return;
		}

		SetVisible(true);

		// Screen Space - Overlay: position is in screen pixels (z is ignored).
		// Set the RectTransform's world position directly — Unity converts to
		// the canvas's coordinate space under the hood.
		if (rt != null)
			rt.position = new Vector3(screenPos.x, screenPos.y, 0f);
		else
			transform.position = new Vector3(screenPos.x, screenPos.y, 0f);
	}

	/// <summary>
	/// Show or hide the prompt. Uses SetActive on a separate promptRoot when
	/// one is wired (safe — doesn't deactivate this script); otherwise toggles
	/// the TMP_Text's `enabled` flag (also safe — keeps the GameObject alive,
	/// so LateUpdate keeps running). Early-outs when state is unchanged.
	/// </summary>
	private void SetVisible(bool visible)
	{
		if (visible == currentlyVisible) return;
		currentlyVisible = visible;

		if (useSeparateRoot)
		{
			promptRoot.SetActive(visible);
		}
		else if (label != null)
		{
			label.enabled = visible;
		}
	}
}
