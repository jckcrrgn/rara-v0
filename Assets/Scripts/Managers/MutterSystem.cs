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
/// queued (FIFO, cap 3, drop-newest on overflow). Queue drains on Dismiss().
/// Added Day 37 to support paired sequences like L6's guard-then-Cassie
/// failure-loop entry; see GDD L6 mutter chain addendum.
///
/// PER-SPEAKER STYLING (Day 37)
/// ----------------------------
/// Each mutter is attributed to a Speaker (enum: Cassie, Guard). Each speaker
/// has its own SpeakerConfig with grunt pool, volume, pitch range, and
/// optional text color override. Play() defaults speaker to Cassie when not
/// specified, preserving the existing call signature for callers that don't
/// need to specify (MutterTrigger, BarehandStuckMutter, LevelManager entry
/// mutters).
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
	/// Speakers in the world. Each one needs a corresponding entry in
	/// speakerConfigs at the matching array index. Cassie is index 0 and is
	/// the canonical default for Play() calls that don't specify a speaker.
	/// Add new entries at the end; do NOT reorder, since that would silently
	/// remap existing speakerConfigs entries to wrong speakers.
	///
	/// Roster:
	///   Cassie - the player character. Default speaker.
	///   Guard  - L6 offstage antagonist. Debuts in L6's failure-loop sequence
	///            (paired with mutter queue, also Day 37).
	/// </summary>
	public enum Speaker
	{
		Cassie = 0,
		Guard = 1,
	}

	/// <summary>
	/// Per-speaker audio + visual settings. One entry per Speaker enum value,
	/// stored in speakerConfigs array at the index matching the Speaker int.
	///
	/// Text color is OPTIONAL — when overrideTextColor is false, the speaker
	/// inherits whatever color is already on mutterText (i.e. the
	/// inspector-default Cassie styling). Guard sets overrideTextColor true
	/// with a distinct color so the player feels the speaker shift even
	/// without reading the line.
	/// </summary>
	[System.Serializable]
	public class SpeakerConfig
	{
		[Tooltip("Display name for this speaker. Editor-only convenience for " +
			"inspector legibility; not used at runtime. Match the Speaker enum " +
			"value at the same array index.")]
		public string speakerName = "Cassie";

		[Tooltip("Pool of grunt clips for this speaker. One is picked at random " +
			"per word during reveal. Multiple clips give the gibberish a more " +
			"lifelike texture; with one clip it sounds machine-like. Cassie " +
			"uses her existing pool; Guard needs his own (lower pitch, more " +
			"masculine).")]
		public AudioClip[] gruntClips;

		[Tooltip("Volume for grunt clips. Quiet — they're punctuation, not " +
			"speech. Reasonable starting point: 0.4 (Cassie's existing value).")]
		[Range(0f, 1f)]
		public float gruntVolume = 0.4f;

		[Tooltip("Random pitch range applied per grunt for variety. Cassie's " +
			"default is (0.92, 1.08) — a tight band. Guard can use a lower " +
			"band like (0.7, 0.85) to read masculine without needing distinct " +
			"clips on day one.")]
		public Vector2 gruntPitchRange = new Vector2(0.92f, 1.08f);

		[Tooltip("Optional. If set, grunts for this speaker route through this " +
			"world-positioned AudioSource instead of AudioManager's 2D channel. " +
			"Use for diegetic speakers (e.g. the offstage guard) where spatial " +
			"attenuation is the mechanic. Leave null for Cassie / non-diegetic. " +
			"Gotcha: PlayOneShot with pitch applies pitch to ALL sounds currently " +
			"playing on the source, not just the one-shot. Fine for sparse grunts; " +
			"revisit if grunts ever overlap on a single source.")]
		public AudioSource audioSourceOverride;

		[Tooltip("Optional. If set, this speaker's mutters appear in this panel " +
			"instead of the default mutterRoot. Use to position a speaker's UI " +
			"elsewhere on screen (e.g. the offstage guard's text anchored over " +
			"his hallway position). Must be a sibling of mutterRoot under the " +
			"same Canvas. Leave null for speakers that use the default panel.")]
		public GameObject mutterRootOverride;

		[Tooltip("Required if mutterRootOverride is set. The TMP_Text inside " +
			"the override panel that receives the character-by-character reveal. " +
			"Leave null if no root override.")]
		public TMP_Text mutterTextOverride;

		[Tooltip("Optional companion to mutterRootOverride. If set, the override " +
			"panel's screen position is updated each frame to track this " +
			"transform's projected position in world space. Use for diegetic " +
			"speakers whose UI should feel anchored to a world location (e.g. " +
			"the offstage guard's audio source). Leave null for static-position " +
			"override panels.")]
		public Transform worldAnchor;

		[Tooltip("If true, applies textColor to the mutter text when this " +
			"speaker is talking. If false, the speaker inherits whatever color " +
			"the mutterText was inspector-configured with (typically Cassie's " +
			"default white/cream). Leave false for Cassie's entry to preserve " +
			"existing styling.")]
		public bool overrideTextColor = false;

		[Tooltip("Text color to apply when overrideTextColor is true. Used to " +
			"visually distinguish speakers. Guard candidate: muted yellow or " +
			"desaturated red — reads as 'not Cassie' without screaming menace.")]
		public Color textColor = Color.white;
	}

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

	[Tooltip("Camera used to project world-anchored speaker panels to screen " +
		"space. If null, falls back to Camera.main. Required only if any " +
		"SpeakerConfig has a worldAnchor set.")]
	[SerializeField] private Camera anchorCamera;

	[Header("Reveal Tuning")]
	[Tooltip("Characters per second during the reveal animation. Lower = slower mutter " +
		"(she's gagged and labored). 15-20 is in the right zone for this character. " +
		"30+ feels too snappy.")]
	[SerializeField] private float charsPerSecond = 18f;

	[Header("Audio")]
	[Tooltip("Per-speaker configuration. Index of this array maps 1:1 to the " +
		"Speaker enum order — element 0 is Cassie, element 1 is Guard, etc. " +
		"Each entry carries that speaker's grunt pool, volume, pitch range, " +
		"and optional text color override. Cassie's entry should always exist " +
		"as the canonical default (Play() with no speaker arg defaults to her).")]
	[SerializeField] private SpeakerConfig[] speakerConfigs;

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

	// Set true when Dismiss() runs and there's another mutter queued behind
	// it. Forces the player to release dismissKey before the next mutter
	// accepts skip-to-end or dismiss input. Cleared in Update() the first
	// frame we see the key isn't held. Prevents machine-gunning through
	// paired mutters (e.g. L6 Beat 6 guard-then-Cassie sequence).
	private bool requireDismissKeyRelease;

	// Set true when the player presses dismissKey during reveal. The reveal
	// coroutine checks this each iteration and short-circuits if set.
	private bool skipRequested;

	// Coroutine handle for the prompt animation (delay -> fade-in -> pulse).
	// Tracked so we can cancel it cleanly on dismiss.
	private Coroutine promptAnimCoroutine;

	// The root/text actually in use for the current (or most-recently-active)
	// mutter. Resolved from SpeakerConfig.mutterRootOverride at the start of
	// RevealCycle; falls back to the default mutterRoot/mutterText. Tracked
	// so Dismiss() can hide and clear the right pair even when an override
	// was used. Null between mutters is fine — Dismiss is no-op-safe.
	private GameObject activeRoot;
	private TMP_Text activeText;
	private Transform activeWorldAnchor;

	/// <summary>
	/// True if a mutter is currently showing (either revealing characters or waiting
	/// for the player to dismiss it) OR if a mutter is queued and pending. Other
	/// systems gate input on this. Including queued mutters in this check ensures
	/// PlayerController never gets a one-frame input window between two queued
	/// mutters in a sequence.
	/// </summary>
	public bool IsActive => isRevealing || isWaitingForDismiss || queuedMutters.Count > 0;

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

		// Hide any per-speaker override roots so a speaker's panel isn't left
		// visible from inspector setup. Scene-authored state shouldn't survive
		// the first frame.
		if (speakerConfigs != null)
		{
			foreach (var cfg in speakerConfigs)
			{
				if (cfg != null && cfg.mutterRootOverride != null)
				{
					cfg.mutterRootOverride.SetActive(false);
				}
			}
		}
	}

	void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	void Update()
	{
		// Release-before-input gate: when a mutter is dismissed and the queue
		// has another mutter waiting, that next mutter must NOT accept input
		// until the dismiss key has been released at least once. Without this,
		// a rapid double-tap (or held key) on the dismiss key could
		// machine-gun through paired mutters like L6's Beat 6 guard-then-Cassie
		// sequence. Cleared the first frame we see the key isn't held.
		if (requireDismissKeyRelease)
		{
			if (!Input.GetKey(dismissKey))
			{
				requireDismissKeyRelease = false;
			}
			return;
		}

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
	/// Show a mutter. If one is already active, the new mutter is queued
	/// (up to QueueCapacity) and will fire when the active mutter is dismissed.
	/// Returns true if the mutter started immediately OR was successfully
	/// queued (caller treats both as "the player will see this, consume your
	/// fire-once charge"). Returns false ONLY if the queue is full and the
	/// new mutter was dropped — in that case the caller should NOT consume
	/// its fire-once charge.
	///
	/// Speaker defaults to Cassie when not specified, preserving the existing
	/// Play(content) call signature for all callers that don't need to
	/// specify a speaker.
	/// </summary>
	public bool Play(string content, Speaker speaker = Speaker.Cassie)
	{
		if (mutterRoot == null || mutterText == null)
		{
			Debug.LogError("MutterSystem: UI references not assigned.");
			return false;
		}

		// Queue if active; fire immediately if idle.
		if (IsActive)
		{
			if (queuedMutters.Count >= QueueCapacity)
			{
				Debug.Log($"MutterSystem: queue full ({QueueCapacity}), dropping new mutter \"{content}\".");
				return false;
			}
			queuedMutters.Enqueue(new QueuedMutter(content, speaker));
			return true;
		}

		StartCoroutine(RevealCycle(content, speaker));
		return true;
	}

	/// <summary>
	/// UnityEvent-friendly wrapper: plays a guard mutter. Inspector can't pass
	/// enum arguments, so we expose typed helpers for cross-system wiring.
	/// </summary>
	public void PlayAsGuard(string content) => Play(content, Speaker.Guard);

	/// <summary>
	/// Immediately close any active mutter and drain the queue. Used by
	/// systems that need to interrupt the mutter flow with their own
	/// sequence — e.g. FailureLoopController on timer expiry, where the
	/// in-progress 50% pressure mutter is rendered moot by the failure
	/// happening. Narratively: whatever was being said matters less than
	/// what's about to be said.
	///
	/// Differs from a regular Dismiss() in that it skips the
	/// requireDismissKeyRelease gate (the caller is in control of timing,
	/// not the player) and cancels any in-flight reveal coroutine. After
	/// this call, IsActive is false and the queue is empty — safe to
	/// immediately Play() a new mutter.
	///
	/// No-op if nothing is active.
	/// </summary>
	public void ForceDismissAndClear()
	{
		if (!IsActive) return;

		// Cancel any in-flight reveal so we don't get stray characters
		// appearing after we've torn down the UI.
		StopAllCoroutines();
		isRevealing = false;
		isWaitingForDismiss = false;
		skipRequested = false;
		requireDismissKeyRelease = false;

		// Drain the queue first so no further mutters fire on Dismiss().
		queuedMutters.Clear();

		// Reuse Dismiss for the rest of the teardown (panel hide, text clear,
		// prompt cancellation, hasEverDismissed bookkeeping, etc.). Since the
		// queue is now empty, Dismiss won't start anything new.
		Dismiss();
	}

	/// <summary>
	/// Cap on the mutter queue. 3 is intentional: the legitimate use case is
	/// paired mutters (queue depth 2, e.g. L6 Beat 6 guard-then-Cassie), and
	/// a +1 buffer absorbs accidental overlap without runaway. Drop-newest on
	/// overflow preserves the integrity of in-progress sequences.
	/// </summary>
	private const int QueueCapacity = 3;

	/// <summary>
	/// FIFO of mutters queued behind the active one. Drains via Dismiss().
	/// </summary>
	private readonly System.Collections.Generic.Queue<QueuedMutter> queuedMutters
		= new System.Collections.Generic.Queue<QueuedMutter>();

	private readonly struct QueuedMutter
	{
		public readonly string Content;
		public readonly Speaker Speaker;
		public QueuedMutter(string content, Speaker speaker)
		{
			Content = content;
			Speaker = speaker;
		}
	}

	private IEnumerator RevealCycle(string content, Speaker speaker)
	{
		isRevealing = true;
		skipRequested = false;
		mutterStartTime = Time.time;
		if (dismissPrompt != null) dismissPrompt.SetActive(false);

		// Resolve which root/text to use for this mutter. If the speaker has
		// an override root configured, use it; otherwise fall back to the
		// default mutterRoot/mutterText. Stored on instance fields so Dismiss
		// and LateUpdate can find the right pair without re-resolving.
		SpeakerConfig config = GetSpeakerConfig(speaker);
		ResolveActivePanel(config);

		if (activeRoot == null || activeText == null)
		{
			Debug.LogError("MutterSystem: no active root/text resolved for speaker " + speaker);
			isRevealing = false;
			yield break;
		}

		activeRoot.SetActive(true);
		activeText.text = "";

		// Apply per-speaker text color if configured. Cassie's entry leaves
		// overrideTextColor=false, so her mutters inherit whatever color
		// activeText was inspector-configured with. Guard sets the override
		// so his lines read as a distinct speaker even before the player
		// parses the words.
		if (config != null && config.overrideTextColor)
		{
			activeText.color = config.textColor;
		}

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
				activeText.text = content;
				break;
			}

			char c = content[i];
			activeText.text += c;

			if (char.IsWhiteSpace(c))
			{
				startOfWord = true;
			}
			else if (startOfWord)
			{
				PlayGrunt(config);
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
	/// Pick the root/text/anchor to use for the speaker, with fallback to the
	/// default mutterRoot/mutterText. A partially-configured override
	/// (root set but text not, or vice versa) is treated as misconfiguration:
	/// log and fall back to the default rather than half-render.
	/// </summary>
	private void ResolveActivePanel(SpeakerConfig config)
	{
		if (config != null && config.mutterRootOverride != null && config.mutterTextOverride != null)
		{
			activeRoot = config.mutterRootOverride;
			activeText = config.mutterTextOverride;
			activeWorldAnchor = config.worldAnchor;
			return;
		}

		if (config != null && (config.mutterRootOverride != null || config.mutterTextOverride != null))
		{
			Debug.LogWarning("MutterSystem: speaker has mutterRootOverride or " +
				"mutterTextOverride set but not both. Falling back to default panel.");
		}

		activeRoot = mutterRoot;
		activeText = mutterText;
		activeWorldAnchor = null;
	}

	/// <summary>
	/// Resolve a Speaker enum value to its SpeakerConfig. Returns null if the
	/// array index doesn't exist (misconfiguration); callers should fall back
	/// gracefully (no grunts, no color override) rather than throw.
	/// </summary>
	private SpeakerConfig GetSpeakerConfig(Speaker speaker)
	{
		int idx = (int)speaker;
		if (speakerConfigs == null || idx < 0 || idx >= speakerConfigs.Length)
		{
			Debug.LogWarning($"MutterSystem: no SpeakerConfig at index {idx} ({speaker}). " +
				"Check speakerConfigs array length matches Speaker enum.");
			return null;
		}
		return speakerConfigs[idx];
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

	private void PlayGrunt(SpeakerConfig config)
	{
		if (config == null) return;
		if (config.gruntClips == null || config.gruntClips.Length == 0) return;

		AudioClip clip = config.gruntClips[Random.Range(0, config.gruntClips.Length)];
		if (clip == null) return;

		float pitch = Random.Range(config.gruntPitchRange.x, config.gruntPitchRange.y);

		if (config.audioSourceOverride != null)
		{
			// Diegetic route: world-positioned source handles spatial attenuation.
			config.audioSourceOverride.pitch = pitch;
			config.audioSourceOverride.PlayOneShot(clip, config.gruntVolume);
		}
		else
		{
			// Default route: AudioManager's 2D channel.
			if (AudioManager.Instance == null) return;
			AudioManager.Instance.PlaySFX(clip, config.gruntVolume, pitch);
		}
	}

	void LateUpdate()
	{
		// Clear the dismiss flag at end of frame. By LateUpdate of the dismiss
		// frame, all other Update() callers have had a chance to see it.
		wasJustDismissed = false;

		// Track the active panel to a world position if one is set. Runs in
		// LateUpdate so any camera movement this frame is already applied.
		// The L6 camera is fixed, so this is essentially constant — but doing
		// it every frame is cheap and means we don't have to special-case
		// future levels that move the camera.
		if (activeWorldAnchor != null && activeRoot != null)
		{
			Camera cam = anchorCamera != null ? anchorCamera : Camera.main;
			if (cam != null)
			{
				RectTransform rt = activeRoot.transform as RectTransform;
				if (rt != null)
				{
					Vector3 screenPos = cam.WorldToScreenPoint(activeWorldAnchor.position);
					// WorldToScreenPoint returns screen coords; we want them
					// in the same space as the RectTransform's parent canvas.
					// For an Overlay canvas this is identical to screen coords;
					// for Camera/World canvas, a conversion would be needed.
					// L6 uses Overlay, so direct assignment is correct.
					rt.position = screenPos;
				}
			}
		}
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

		// Hide whichever root we actually showed (default or override), and
		// clear its paired text. activeWorldAnchor is also cleared so
		// LateUpdate stops trying to track a stale transform. The default
		// mutterRoot/mutterText are also hidden above for safety; one of these
		// pairs is redundant per dismiss but the cost is a no-op SetActive.
		if (activeRoot != null) activeRoot.SetActive(false);
		if (activeText != null) activeText.text = "";
		activeWorldAnchor = null;

		// Drain queue: if there's a mutter waiting, start it. Set the
		// release-required gate so the player has to lift the dismiss key
		// before the next mutter accepts input. Without the gate, a held or
		// rapidly-tapped key could machine-gun through paired sequences.
		if (queuedMutters.Count > 0)
		{
			QueuedMutter next = queuedMutters.Dequeue();
			requireDismissKeyRelease = true;
			StartCoroutine(RevealCycle(next.Content, next.Speaker));
		}
	}
}
