// EndCard.cs — Day 215. The demo's last screen.
//
// One card for all three endings: slipped out, strike, taken. Headline + one line per
// ending, then thanks, the itch link and the owned channel. Start / R plays again
// (LevelManager already owns restart). Replaces LEVEL COMPLETE and the taken reload
// in any scene that has one — scenes without an EndCard behave exactly as before.
//
// WHO CALLS IT
//   - LevelManager.CompleteLevel   -> ShowForLevelComplete() (slipped out or strike;
//                                    BondStruggle.SlippedOut tells them apart)
//   - GuardController.TakenPhase   -> Show(Outcome.Taken), instead of reloading
//
// INPUT (armed after armDelay, so the A that dismissed the last mutter can't open a browser)
//   itch page          A  /  E / Enter  /  click the line
//   channel            Y  /  J          /  click the line   (hidden while channelUrl is empty)
//   play again         Start  /  R      (LevelManager)
//
// Builds its own overlay canvas at sort order 100: draws over the HUD canvas and over the
// guard's black fade (the taken card sits on the black). Uses TMP's default font.
//
// SETUP: one EndCard component anywhere in the scene. Fill channelUrl once the Discord
// exists; until then the channel line and its prompt don't show.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndCard : MonoBehaviour
{
	public enum Outcome { SlippedOut, Strike, Taken }

	public static EndCard Instance { get; private set; }

	[Header("Links")]
	public string itchUrl = "https://jckcrrgn.itch.io/cassie-conroy";
	[Tooltip("Owned channel, e.g. the Discord invite. Empty = line and prompt hidden.")]
	public string channelUrl = "";
	public string channelLabel = "Discord";

	[Header("Copy — per ending")]
	public string slippedOutTitle = "SLIPPED OUT";
	[TextArea(1, 2)] public string slippedOutLine = "Gone before he thought to look.";
	public string strikeTitle = "LIGHTS OUT";
	[TextArea(1, 2)] public string strikeLine = "He was watching the wrong thing.";
	public string takenTitle = "TAKEN";
	[TextArea(1, 2)] public string takenLine = "He's moving her somewhere worse.";

	[Header("Copy — shared")]
	[TextArea(1, 2)] public string thanksLine = "Thanks for playing the demo.";
	public string linksHeader = "FOLLOW THE BUILD";

	[Header("Look")]
	[Tooltip("Optional. Empty = TMP's default font.")]
	public TMP_FontAsset font;
	public Color titleColor = Color.white;
	public Color takenTitleColor = new Color(0.82f, 0.22f, 0.2f);
	public Color bodyColor = new Color(0.85f, 0.85f, 0.85f);
	public Color dimColor = new Color(0.55f, 0.55f, 0.55f);
	[Range(0f, 1f)] public float backdropAlpha = 0.9f;

	[Header("Timing")]
	[Tooltip("Seconds for the card to fade in.")]
	public float fadeIn = 0.8f;
	[Tooltip("Seconds before link input is live and the prompts show.")]
	public float armDelay = 1.5f;

	public bool IsShowing { get; private set; }
	public Outcome Shown { get; private set; }

	CanvasGroup group;
	TextMeshProUGUI title, line, prompts;
	GameObject channelRow;
	float armedAt = float.MaxValue;
	bool padPrompts;

	void Awake()
	{
		if (Instance == null) Instance = this;
		else if (Instance != this) Destroy(this);
	}

	void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	// ---------------------------------------------------------------------------------
	// Show
	// ---------------------------------------------------------------------------------

	/// <summary>Level complete in a scene with a card: slipped out if every bond is free, else the strike.</summary>
	public void ShowForLevelComplete()
	{
		var struggle = FindFirstObjectByType<BondStruggle>();
		Show(struggle != null && struggle.SlippedOut ? Outcome.SlippedOut : Outcome.Strike);
	}

	public void Show(Outcome outcome)
	{
		if (IsShowing) return;
		IsShowing = true;
		Shown = outcome;
		Debug.Log($"[EndCard] {outcome}");

		var player = FindFirstObjectByType<PlayerController>();
		if (player != null) player.enabled = false;
		foreach (var hud in FindObjectsByType<BondStruggleHUD>(FindObjectsSortMode.None))
			hud.enabled = false;   // OnGUI draws over every canvas

		if (group == null) Build();

		switch (outcome)
		{
			case Outcome.SlippedOut: title.text = slippedOutTitle; line.text = slippedOutLine; break;
			case Outcome.Strike: title.text = strikeTitle; line.text = strikeLine; break;
			default: title.text = takenTitle; line.text = takenLine; break;
		}
		title.color = outcome == Outcome.Taken ? takenTitleColor : titleColor;
		channelRow.SetActive(HasChannel);

		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		StartCoroutine(FadeIn());
	}

	bool HasChannel => !string.IsNullOrWhiteSpace(channelUrl);

	IEnumerator FadeIn()
	{
		group.gameObject.SetActive(true);
		group.alpha = 0f;
		prompts.alpha = 0f;
		float t = 0f;
		while (t < fadeIn)
		{
			t += Time.unscaledDeltaTime;
			group.alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeIn));
			yield return null;
		}
		group.alpha = 1f;

		float wait = armDelay - fadeIn;
		if (wait > 0f) yield return new WaitForSecondsRealtime(wait);

		armedAt = Time.unscaledTime;
		group.interactable = true;
		group.blocksRaycasts = true;
		RefreshPrompts(true);
		for (float f = 0f; f < 0.3f; f += Time.unscaledDeltaTime)
		{
			prompts.alpha = f / 0.3f;
			yield return null;
		}
		prompts.alpha = 1f;
	}

	// ---------------------------------------------------------------------------------
	// Input
	// ---------------------------------------------------------------------------------

	void Update()
	{
		if (!IsShowing || Time.unscaledTime < armedAt) return;

		var pad = RaraInput.Read();
		if (pad.connected != padPrompts) RefreshPrompts(false);

		if (pad.interact || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
			OpenItch();
		else if (HasChannel && (pad.secondary || Input.GetKeyDown(KeyCode.J)))
			OpenChannel();
	}

	void OpenItch()
	{
		if (!string.IsNullOrWhiteSpace(itchUrl)) Application.OpenURL(itchUrl);
	}

	void OpenChannel()
	{
		if (HasChannel) Application.OpenURL(channelUrl);
	}

	void RefreshPrompts(bool force)
	{
		bool pad = RaraInput.Read().connected;
		if (!force && pad == padPrompts) return;
		padPrompts = pad;

		string itchKey = pad ? "A" : "E";
		string chanKey = pad ? "Y" : "J";
		string againKey = pad ? "Start" : "R";
		string s = $"<b>{itchKey}</b>  itch page";
		if (HasChannel) s += $"        <b>{chanKey}</b>  {channelLabel}";
		s += $"        <b>{againKey}</b>  play again";
		prompts.text = s;
	}

	// ---------------------------------------------------------------------------------
	// Build
	// ---------------------------------------------------------------------------------

	void Build()
	{
		var root = new GameObject("EndCardCanvas", typeof(RectTransform));
		root.transform.SetParent(transform, false);
		var canvas = root.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 100;
		var scaler = root.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		root.AddComponent<GraphicRaycaster>();

		group = root.AddComponent<CanvasGroup>();
		group.interactable = false;
		group.blocksRaycasts = false;

		var backdrop = NewRect("Backdrop", root.transform);
		Stretch(backdrop);
		var img = backdrop.gameObject.AddComponent<Image>();
		img.color = new Color(0f, 0f, 0f, backdropAlpha);
		img.raycastTarget = false;

		var column = NewRect("Column", root.transform);
		column.anchorMin = column.anchorMax = column.pivot = new Vector2(0.5f, 0.5f);
		column.sizeDelta = new Vector2(1400f, 0f);
		var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.childAlignment = TextAnchor.MiddleCenter;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		layout.spacing = 18f;
		var fitter = column.gameObject.AddComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		title = NewText("Title", column, 120f, titleColor, FontStyles.Bold);
		title.characterSpacing = 12f;
		line = NewText("Line", column, 42f, bodyColor, FontStyles.Italic);
		Spacer(column, 60f);
		NewText("Thanks", column, 38f, bodyColor, FontStyles.Normal).text = thanksLine;
		Spacer(column, 24f);
		var header = NewText("LinksHeader", column, 26f, dimColor, FontStyles.Normal);
		header.text = linksHeader;
		header.characterSpacing = 8f;

		var itch = NewText("Itch", column, 36f, Color.white, FontStyles.Normal);
		itch.text = $"itch   <color=#{ColorUtility.ToHtmlStringRGB(dimColor)}>·</color>   {Display(itchUrl)}";
		Clickable(itch, OpenItch);

		var chan = NewText("Channel", column, 36f, Color.white, FontStyles.Normal);
		chan.text = $"{channelLabel}   <color=#{ColorUtility.ToHtmlStringRGB(dimColor)}>·</color>   {Display(channelUrl)}";
		Clickable(chan, OpenChannel);
		channelRow = chan.gameObject;

		Spacer(column, 50f);
		prompts = NewText("Prompts", column, 28f, dimColor, FontStyles.Normal);

		root.SetActive(false);
	}

	static string Display(string url)
	{
		if (string.IsNullOrEmpty(url)) return "";
		string s = url.Trim();
		if (s.StartsWith("https://")) s = s.Substring(8);
		else if (s.StartsWith("http://")) s = s.Substring(7);
		if (s.StartsWith("www.")) s = s.Substring(4);
		return s.TrimEnd('/');
	}

	static RectTransform NewRect(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Stretch(RectTransform r)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.offsetMin = r.offsetMax = Vector2.zero;
	}

	TextMeshProUGUI NewText(string name, Transform parent, float size, Color color, FontStyles style)
	{
		var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null) t.font = font;
		t.fontSize = size;
		t.color = color;
		t.fontStyle = style;
		t.alignment = TextAlignmentOptions.Center;
		t.textWrappingMode = TextWrappingModes.Normal;
		t.raycastTarget = false;
		return t;
	}

	static void Spacer(Transform parent, float height)
	{
		var le = NewRect("Spacer", parent).gameObject.AddComponent<LayoutElement>();
		le.minHeight = le.preferredHeight = height;
	}

	static void Clickable(TextMeshProUGUI t, UnityEngine.Events.UnityAction onClick)
	{
		t.raycastTarget = true;
		var b = t.gameObject.AddComponent<Button>();
		b.transition = Selectable.Transition.None;
		b.navigation = new Navigation { mode = Navigation.Mode.None };   // pad A stays ours
		b.onClick.AddListener(onClick);
	}
}
