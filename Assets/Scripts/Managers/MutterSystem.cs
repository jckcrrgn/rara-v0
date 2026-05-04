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
/// Aesthetic reference: Phoenix Wright, Undertale, classic Pokémon. Stylized
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
/// kick, move, flip — all gated. The dismiss key (Space by default) also
/// happens to be Struggle's key, so we explicitly suppress Struggle on the
/// frame the mutter is dismissed (see PlayerController).
/// </summary>
public class MutterSystem : MonoBehaviour
{
	public static MutterSystem Instance { get; private set; }

	[Header("UI References")]
	[Tooltip("Root GameObject of the mutter UI (panel + text). Toggled active/inactive " +
		"as mutters start and end.")]
	[SerializeField] private GameObject mutterRoot;

	[Tooltip("TextMeshProUGUI that displays the mutter content. Set its starting text to " +
		"empty in the inspector.")]
	[SerializeField] private TMP_Text mutterText;

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
	[Tooltip("Key the player presses to dismiss the mutter. Defaults to Space, which " +
		"matches Struggle's key — PlayerController will suppress Struggle on the " +
		"frame the mutter is dismissed so they don't double-fire.")]
	[SerializeField] private KeyCode dismissKey = KeyCode.Space;

	[Tooltip("Minimum seconds the mutter stays visible before dismissKey can dismiss it. " +
		"Prevents accidental skip if the player happened to press the key as the mutter " +
		"appeared. 0.3 is enough to feel intentional but not annoying.")]
	[SerializeField] private float minVisibleTime = 0.3f;

	// Timestamp when the current mutter started revealing. Used to compute whether
	// minVisibleTime has elapsed before allowing dismiss.
	private float mutterStartTime;
	private bool isRevealing;
	private bool isWaitingForDismiss;

	// Set true on the frame Dismiss() runs; cleared at the end of the next
	// LateUpdate. PlayerController checks this to suppress Struggle on the
	// dismiss frame, since Space is shared between dismiss and Struggle.
	private bool wasJustDismissed;

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
	}

	void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	void Update()
	{
		// Dismiss path: the player has read the mutter and presses dismissKey.
		// Only valid once the reveal is finished (isWaitingForDismiss) and the
		// minimum visible time has passed.
		if (isWaitingForDismiss
			&& Input.GetKeyDown(dismissKey)
			&& Time.time - mutterStartTime >= minVisibleTime)
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
		mutterStartTime = Time.time;
		mutterRoot.SetActive(true);
		mutterText.text = "";

		// Reveal character-by-character. Grunt SFX fires once per word — when we
		// hit the first non-space character of a new word.
		bool startOfWord = true;
		float secondsPerChar = 1f / Mathf.Max(charsPerSecond, 0.01f);

		for (int i = 0; i < content.Length; i++)
		{
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
		if (mutterRoot != null) mutterRoot.SetActive(false);
		if (mutterText != null) mutterText.text = "";
	}
}
