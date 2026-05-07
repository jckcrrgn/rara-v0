using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Retro-style mutter system. The detective is gagged and bound; she can't
/// dialogue properly. Instead: text appears in a box, character-by-character,
/// accompanied by random grunt clips per word. Player presses dismissKey to
/// continue.
///
/// Aesthetic reference: Phoenix Wright, Undertale, classic Pokemon. Stylized
/// gibberish-as-speech that fits a gagged character better than voice acting
/// would.
///
/// Singleton-ish: lives in the scene's Canvas, accessed via Instance. Only
/// one mutter plays at a time; new Play() calls during an active mutter are
/// dropped (logged). Could change to queue or interrupt later if v0 needs it.
///
/// Input integration: while a mutter is active, IsActive returns true.
/// PlayerController and FloorRestraint check this and freeze player input
/// (the world pauses while the player reads). Steering, struggle, pickup,
/// kick, move, flip -- all gated. The dismiss key (Space by default) also
/// happens to be Struggle's key, so we explicitly suppress Struggle on the
/// frame the mutter is dismissed (see PlayerController).
///
/// DISMISS BEHAVIOR (Day 28 update)
/// --------------------------------
/// dismissKey has two roles depending on reveal state:
///   - Pressed DURING reveal: skip-to-end. Whole content populates instantly,
///     state transitions to waiting-for-dismiss. Player can immediately press
///     again to dismiss. (Standard JRPG/VN convention.)
///   - Pressed AFTER reveal: dismiss the mutter.
///
/// DISMISS PROMPT BEHAVIOR (Day 28, second pass)
/// ---------------------------------------------
/// The dismiss prompt teaches the dismiss control. Once the player has
/// dismissed at least one mutter, the prompt has done its job -- showing it
/// every time afterward is visual noise that trains players to ignore it.
/// Behavior is controlled by promptMode (PromptMode enum):
///
///   - TieredDelay (default): prompt appears after a short delay on the
///     player's first mutter (promptDelayFirst, ~1.5s) and a longer delay
///     thereafter (promptDelaySubsequent, ~5s). Long delay means the prompt
///     only shows up when the player is genuinely stuck. Best balance of
///     teaching + not-noise for most situations.
///
///   - FirstMutterOnly: prompt appears on the first mutter the player has
///     ever seen (after promptDelayFirst), and never again that session.
///     Use when you trust the player to retain the dismiss control after
///     learning it once. Cleanest UX, riskier if the player is distracted
///     during their first mutter.
///
///   - Always: prompt appears after promptDelayFirst on every mutter. Use
///     for accessibility / "always show controls" toggle.
///
///   - Never: prompt never appears. Use for cinematic mutters where the
///     player is meant to wait or for testing.
///
/// Once visible, the prompt fades in over promptFadeDuration and pulses
/// gently to read as "active prompt" rather than "static HUD chrome."
/// (Pokemon-arrow convention: animation is the thing that keeps prompts
/// from going invisible to the eye.)
///
/// hasEverDismissed is a static bool: it persists across mutters within a
/// session but resets on game restart. That's correct -- a returning player
/// in a fresh session benefits from the teach-prompt timing on their first
/// mutter again.
/// </summary>
public class MutterSystem : MonoBehaviour
{
	/// <summary>
	/// How the dismiss prompt should behave across a session. See class docstring
	/// for the full rationale; quick guide:
	///   TieredDelay     - short delay first time, long delay thereafter (default)
	///   FirstMutterOnly - shows once on first mutter, never again that session
	///   Always          - shows on every mutter after the standard delay
	///   Never           - never shows (cinematic / testing)
	/// </summary>
	public enum PromptMode
	{
		TieredDelay,
		FirstMutterOnly,
		Always,
		Never,
	}

	public static MutterSystem Instance { get; private set; }

	// Static so it persists across MutterSystem instances within a session.
	// (Each level has its own MutterSystem, but the player's knowledge of how
	// to dismiss persists.) Resets on game restart, which is fine.
	private static bool hasEverDismissed = false;

	[Header("UI References")]
	[Tooltip("Root GameObject of the mutter UI (panel + text). Toggled active/inactive " +
		"as mutters start and end.")]
	[SerializeField] private GameObject mutterRoot;

	[Tooltip("TextMeshProUGUI that displays the mutter content. Set its starting text to " +
		"empty in the inspector.")]
	[SerializeField] private TMP_Text mutterText;

	[Tooltip("Optional. A small UI element (text saying '[SPACE]', a blinking " +
		"chevron, etc.) shown after a delay once the mutter has fully revealed. " +
		"Hidden during reveal and during the prompt delay window. Safe to leave " +
		"null while wiring up the prefab; the system still works without it.")]
	[SerializeField] private GameObject dismissPrompt;

	[Tooltip("Optional. CanvasGroup on the dismissPrompt (or one of its parents). " +
		"Used for fade-in and pulse animations. If null, the prompt just pops in " +
		"without animation -- still works, just less polished.")]
	[SerializeField] private CanvasGroup dismissPromptCanvasGroup;

	[Header("Reveal Tuning")]
	[Tooltip("Characters per second during the reveal animation. Lower = slower mutter " +
		"(she's gagged and labored). 15-20 is in the right zone for this character. " +
		"30+ feels too snappy.")]
	[SerializeField] private float charsPerSecond = 18f;

	[Header("Audio")]
	[Tooltip("Pool of grunt clips. One is picked at random per word during reveal. " +
		"Multiple clips give the gibberish a more lifelike texture; with one clip " +
		"it sounds machine-like.")]
	[SerializeField] private AudioClip[] gruntClips;

	[Tooltip("Volume for grunt clips. Quiet -- they're punctuation, not speech.")]
	[Range(0f, 1f)]
	[SerializeField] private float gruntVolume = 0.4f;

	[Tooltip("Random pitch range applied per grunt for variety.")]
	[SerializeField] private Vector2 gruntPitchRange = new Vector2(0.92f, 1.08f);

	[Header("Input")]
	[Tooltip("Key the player presses to skip-to-end (during reveal) or dismiss " +
		"(after reveal). Defaults to Space, which matches Struggle's key -- " +
		"PlayerController suppresses Struggle on the frame of dismissal so they " +
		"don't double-fire.")]
	[SerializeField] private KeyCode dismissKey = KeyCode.Space;

	[Tooltip("Minimum seconds the mutter stays visible before dismissKey can dismiss it. " +
		"Prevents accidental skip if the player happened to press the key as the mutter " +
		"appeared. 0.3 is enough to feel intentional but not annoying. Note: this only " +
		"gates the final dismiss -- skip-to-end during reveal is always allowed.")]
	[SerializeField] private float minVisibleTime = 0.3f;

	[Header("Dismiss Prompt Timing")]
	[Tooltip("How the dismiss prompt should behave. See PromptMode enum docstring.\n" +
		"  TieredDelay (default): short delay on first mutter, long delay after.\n" +
		"  FirstMutterOnly: shown once on first mutter, never again this session.\n" +
		"  Always: shown on every mutter after promptDelayFirst.\n" +
		"  Never: prompt never appears (cinematic / testing).")]
	[SerializeField] private PromptMode promptMode = PromptMode.TieredDelay;

	[Tooltip("Seconds after reveal completes before the dismiss prompt appears " +
		"on the player's FIRST EVER mutter. Short, because the prompt is teaching " +
		"the dismiss control. 1.5 is just past comfortable reading speed.")]
	[SerializeField] private float promptDelayFirst = 1.5f;

	[Tooltip("Seconds after reveal completes before the dismiss prompt appears " +
		"on every mutter AFTER the first dismiss. Long, because the player has " +
		"already learned the control -- prompt only needs to rescue genuinely " +
		"stuck players. 5 is past 'reading' and into 'they're lost.'")]
	[SerializeField] private float promptDelaySubsequent = 5f;

	[Tooltip("Seconds the dismiss prompt takes to fade in once its delay elapses. " +
		"~0.3 reads as a soft appearance rather than a UI pop.")]
	[SerializeField] private float promptFadeDuration = 0.3f;

	[Tooltip("Period of the prompt's pulse cycle in seconds. Lower = faster pulse. " +
		"~1.0-1.2 reads as a calm 'I'm here' rhythm; faster reads as urgent.")]
	[SerializeField] private float promptPulsePeriod = 1.1f;

	[Tooltip("Minimum alpha during pulse. The prompt fades between this and full " +
		"alpha (1.0). 0.4 keeps it always legible; lower feels more breathing.")]
	[Range(0f, 1f)]
	[SerializeField] private float promptPulseMinAlpha = 0.4f;

	// Timestamp when the current mutter started revealing. Used to compute whether
	// minVisibleTime has elapsed before allowing dismiss.
	private float mutterStartTime;
	private bool isRevealing;
	private bool isWaitingForDismiss;

	// Set true on the frame Dismiss() runs; cleared at the end of the next
	// LateUpdate. PlayerController checks this to suppress Struggle on the
	// dismiss frame, since Space is shared between dismiss and Struggle.
	private bool wasJustDismissed;

	// Set true when the player presses dismissKey during reveal. The reveal
	// coroutine checks this each iteration and short-circuits if set.
	private bool skipRequested;

	// Coroutine handle for the prompt animation (delay -> fade-in -> pulse).
	// Tracked so we can cancel it cleanly on dismiss.
	private Coroutine promptAnimCoroutine;

	/// <summary>
	/// True if a mutter is currently showing (either revealing characters or waiting
	/// for the player to dismiss it). Other systems gate input on this.
	/// </summary>
	public bool IsActive => isRevealing || isWaitingForDismiss;

	/// <summary>
	/// True for one frame after the player dismisses a mutter. Used by
	/// PlayerController to suppress Struggle on that frame (Space is shared).
	/// </summary>
	public bool WasJustDismissed => wasJustDismissed;

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogWarning("Multiple MutterSystems in scene; destroying duplicate.");
			Destroy(gameObject);
			return;
		}
		Instance = this;

		if (mutterRoot != null) mutterRoot.SetActive(false);
		if (dismissPrompt != null) dismissPrompt.SetActive(false);
	}

	void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	void Update()
	{
		if (!Input.GetKeyDown(dismissKey)) return;

		// Skip-to-end: pressed during reveal. The reveal coroutine sees the flag
		// and short-circuits, populating the rest of the text instantly and
		// transitioning to isWaitingForDismiss. The player can press again on a
		// subsequent frame to dismiss. Crucially: skip-to-end does NOT also
		// dismiss in the same frame -- two presses are required to leave a
		// mutter, ensuring the player gets to read the full text.
		if (isRevealing)
		{
			skipRequested = true;
			return;
		}

		// Dismiss: pressed after reveal, after minVisibleTime.
		if (isWaitingForDismiss && Time.time - mutterStartTime >= minVisibleTime)
		{
			Dismiss();
		}
	}

	/// <summary>
	/// Show a mutter. If one is already active, the new one is dropped (logged
	/// for now; could change to queue later). Returns true if the mutter started.
	/// </summary>
	public bool Play(string content)
	{
		if (IsActive)
		{
			Debug.Log($"MutterSystem: already active, dropping new mutter \"{content}\".");
			return false;
		}

		if (mutterRoot == null || mutterText == null)
		{
			Debug.LogError("MutterSystem: UI references not assigned.");
			return false;
		}

		StartCoroutine(RevealCycle(content));
		return true;
	}

	private IEnumerator RevealCycle(string content)
	{
		isRevealing = true;
		skipRequested = false;
		mutterStartTime = Time.time;
		mutterRoot.SetActive(true);
		if (dismissPrompt != null) dismissPrompt.SetActive(false);
		mutterText.text = "";

		// Reveal character-by-character. Grunt SFX fires once per word -- when we
		// hit the first non-space character of a new word. Loop checks
		// skipRequested each iteration; if set, populate the rest instantly.
		bool startOfWord = true;
		float secondsPerChar = 1f / Mathf.Max(charsPerSecond, 0.01f);

		for (int i = 0; i < content.Length; i++)
		{
			if (skipRequested)
			{
				// Player asked to skip. Dump the rest of the string in one go.
				// Skipping forfeits the remaining grunt SFX -- it would feel
				// noisy to fire all of them at once.
				mutterText.text = content;
				break;
			}

			char c = content[i];
			mutterText.text += c;

			if (char.IsWhiteSpace(c))
			{
				startOfWord = true;
			}
			else if (startOfWord)
			{
				PlayGrunt();
				startOfWord = false;
			}

			yield return new WaitForSeconds(secondsPerChar);
		}

		isRevealing = false;
		isWaitingForDismiss = true;
		// mutterStartTime stays as-is; minVisibleTime is measured from when the
		// mutter first appeared, not from when reveal finished.

		// Kick off the prompt's delayed-appear-then-pulse animation. The delay
		// to use (or whether to show at all) depends on promptMode and whether
		// the player has dismissed any mutter this session.
		if (dismissPrompt != null && TryGetPromptDelay(out float delay))
		{
			promptAnimCoroutine = StartCoroutine(PromptAnimCycle(delay));
		}
	}

	/// <summary>
	/// Resolve promptMode + hasEverDismissed into a delay value. Returns false
	/// if the prompt should not appear at all for this mutter.
	/// </summary>
	private bool TryGetPromptDelay(out float delay)
	{
		delay = 0f;
		switch (promptMode)
		{
			case PromptMode.Never:
				return false;

			case PromptMode.FirstMutterOnly:
				if (hasEverDismissed) return false;
				delay = promptDelayFirst;
				return true;

			case PromptMode.Always:
				delay = promptDelayFirst;
				return true;

			case PromptMode.TieredDelay:
			default:
				delay = hasEverDismissed ? promptDelaySubsequent : promptDelayFirst;
				return true;
		}
	}

	/// <summary>
	/// Wait, fade in, then pulse forever (until cancelled by Dismiss).
	///
	/// Why one coroutine instead of three: the lifecycle is a single timeline.
	/// Splitting into separate coroutines would mean tracking three handles
	/// for cancellation. One handle, one cancellation point.
	/// </summary>
	private IEnumerator PromptAnimCycle(float delay)
	{
		// Phase 1: wait. During this phase the prompt is inactive and invisible.
		yield return new WaitForSeconds(delay);

		// Phase 2: fade in. Activate the GameObject and ramp alpha 0 -> 1.
		// If no CanvasGroup is wired, fall back to instant pop -- still
		// functional, just less polished. (Wire up the CanvasGroup when you
		// have a moment; it's a 30-second prefab change.)
		dismissPrompt.SetActive(true);

		if (dismissPromptCanvasGroup != null)
		{
			float t = 0f;
			while (t < promptFadeDuration)
			{
				t += Time.deltaTime;
				dismissPromptCanvasGroup.alpha = Mathf.Clamp01(t / promptFadeDuration);
				yield return null;
			}
			dismissPromptCanvasGroup.alpha = 1f;
		}

		// Phase 3: pulse forever. Sin wave between min alpha and 1.0. Only
		// runs if we have a CanvasGroup -- otherwise the prompt sits at full
		// alpha (which is fine, just not animated).
		if (dismissPromptCanvasGroup != null)
		{
			float pulseT = 0f;
			while (true)
			{
				pulseT += Time.deltaTime;
				// 0.5 + 0.5*sin maps [-1, 1] -> [0, 1]; we then map [0, 1] -> [min, 1]
				float wave01 = 0.5f + 0.5f * Mathf.Sin(pulseT * 2f * Mathf.PI / promptPulsePeriod);
				dismissPromptCanvasGroup.alpha = Mathf.Lerp(promptPulseMinAlpha, 1f, wave01);
				yield return null;
			}
		}
	}

	private void PlayGrunt()
	{
		if (gruntClips == null || gruntClips.Length == 0) return;
		if (AudioManager.Instance == null) return;

		AudioClip clip = gruntClips[Random.Range(0, gruntClips.Length)];
		if (clip == null) return;

		float pitch = Random.Range(gruntPitchRange.x, gruntPitchRange.y);
		AudioManager.Instance.PlaySFX(clip, gruntVolume, pitch);
	}

	void LateUpdate()
	{
		// Clear the dismiss flag at end of frame. By LateUpdate of the dismiss
		// frame, all other Update() callers have had a chance to see it.
		wasJustDismissed = false;
	}

	private void Dismiss()
	{
		isWaitingForDismiss = false;
		wasJustDismissed = true;
		hasEverDismissed = true; // teach-mode is over for the rest of the session.

		// Cancel the prompt animation if it's still running (either still in
		// the delay phase, mid-fade, or pulsing). Without this, a fast dismiss
		// would leave the coroutine running until it next yields and noticed
		// the GameObject was inactive.
		if (promptAnimCoroutine != null)
		{
			StopCoroutine(promptAnimCoroutine);
			promptAnimCoroutine = null;
		}

		if (mutterRoot != null) mutterRoot.SetActive(false);
		if (dismissPrompt != null) dismissPrompt.SetActive(false);
		if (dismissPromptCanvasGroup != null) dismissPromptCanvasGroup.alpha = 0f;
		if (mutterText != null) mutterText.text = "";
	}
}
