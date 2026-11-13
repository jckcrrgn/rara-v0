// TutorialPrompts.cs — Day 218. Teaches the controls one line at a time.
//
// Replaces reading the controls card cold. One short line at the bottom of the screen,
// typed in like a mutter, that stays until the player DOES the thing, then fades. Lines
// show when the thing becomes possible, not in card order:
//
//   line          shows when                                  leaves when
//   Struggle      start, after her entry mutter               hitsToLearn on-beat pulls
//   Choose        struggle learned, 2+ bonds holding her      she picks another bond
//   Move          choose learned (or shown)                   first hop / turn / scoot input
//   Untie         the selected knot is in reach               first correct untie
//   Hide          hands out, and untie is learned or he's     she starts hiding them
//                 on his way in
//   Pick up       struggle learned, something in reach        she's holding it
//
// Strike is not here: StrikeHintPrompt already pins it to the guard. Restart and the
// table-edge tip stay on the controls card (BondStruggleHUD, Back / Tab).
//
// RULES
//   - One line on screen at a time. Most urgent first: Hide, Untie, Pick up, then the
//     opening three in order.
//   - Anything the player does before its line comes up is learned: that line never shows.
//   - A line that isn't acted on leaves after giveUpAfter seconds and doesn't come back
//     this run. Struggle is the exception: it stays, nothing else can happen without it.
//   - A line whose moment passes (the knot is out of reach again) leaves and may return.
//   - Every scene load starts fresh. Nothing is remembered between runs, so a new tester
//     on the same launch gets the whole thing.
//   - Hidden during the caught sequence, after she slips out, and under the end card.
//
// NOT A SPEAKER. Same font and halo as the mutters, but fixed on screen, its own colour
// (not her cream, not his amber) and silent, so it doesn't read as her talking. It does
// not go through MutterSystem and never blocks input.
//
// Keys follow the last-touched device (RaraInput), like StrikeHintPrompt.
//
// Builds its own overlay canvas (sort order 50: over the HUD canvas, under the end card).
// Uses TMP's default font (Special Elite) unless one is set.
//
// SETUP: one TutorialPrompts component anywhere in the scene. Nothing to wire.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialPrompts : MonoBehaviour
{
	enum Step { Struggle = 0, Choose = 1, Move = 2, Untie = 3, Hide = 4, PickUp = 5 }
	const int StepCount = 6;

	// Most urgent first.
	static readonly Step[] Priority = { Step.Hide, Step.Untie, Step.PickUp, Step.Struggle, Step.Choose, Step.Move };

	[Header("Lines — {0} = the key on the last-touched device. Empty = skip that line.")]
	public string struggleLine = "{0}: struggle. Follow her rhythm.";
	public string chooseLine = "{0}: choose a bond";
	public string moveLine = "Hold {0}: move the chair";
	public string untieLine = "{0}: untie. Follow the loop.";
	public string hideLine = "{0}: hide your hands";
	public string pickUpLine = "{0}: pick up";

	[Header("Keyboard names (pad names come from the pad)")]
	public string struggleKey = "Space";
	public string chooseKey = "WASD";
	public string moveKey = "Shift + WASD";
	public string untieKey = "IJKL";
	public string hideKey = "G";
	public string pickUpKey = "E";

	[Header("Teaching")]
	[Tooltip("On-beat pulls before the struggle line counts as learned.")]
	[Min(1)] public int hitsToLearn = 3;
	[Tooltip("Seconds a line waits to be acted on before it leaves for good. The struggle " +
		"line ignores this and stays.")]
	public float giveUpAfter = 12f;

	[Header("Timing")]
	[Tooltip("Earliest the first line can show, seconds after the scene loads.")]
	public float startDelay = 1f;
	[Tooltip("The first line waits for her entry mutter to finish, but no longer than this.")]
	public float openingMaxWait = 8f;
	[Tooltip("Typing speed. Faster than a mutter (18): this is a prompt, not her talking.")]
	public float charsPerSecond = 40f;
	[Tooltip("Seconds the line holds after the player does it, before it fades.")]
	public float doneHold = 0.5f;
	public float fadeOut = 0.4f;
	[Tooltip("Seconds of empty screen between one line and the next.")]
	public float gapBetween = 0.8f;

	[Header("Look")]
	[Tooltip("Optional. Empty = TMP's default font.")]
	public TMP_FontAsset font;
	[Tooltip("Where the line sits: 0..1 across and up the screen. The line's bottom-centre " +
		"goes here. Tune by eye from the Game view.")]
	public Vector2 screenAnchor = new Vector2(0.5f, 0.07f);
	[Tooltip("Font size on a 1280 x 720 reference (the HUD canvas's).")]
	public float fontSize = 26f;
	[Tooltip("The sentence. Cool grey-blue: not Cassie's cream, not the guard's amber.")]
	public Color lineColor = new Color(0.70f, 0.82f, 0.88f);
	[Tooltip("The key name inside the sentence.")]
	public Color keyColor = Color.white;
	[Tooltip("Dark halo behind the letters, same values as the mutters' (MutterSystem).")]
	public Color haloColor = new Color(0f, 0f, 0f, 0.85f);
	[Range(0f, 1f)] public float haloDilate = 0.4f;
	[Range(0f, 1f)] public float haloSoftness = 0.6f;
	public Vector2 haloOffset = new Vector2(0.3f, -0.3f);

	enum Phase { Idle, Showing, Leaving }

	BondStruggle struggle;
	PlayerController player;
	CanvasGroup group;
	RectTransform lineRect;
	TextMeshProUGUI text;
	Material haloMat;

	readonly bool[] done = new bool[StepCount];      // the player has done it
	readonly bool[] retired = new bool[StepCount];   // its line won't show (again) this run
	readonly float[] onScreen = new float[StepCount];

	Phase phase = Phase.Idle;
	int current = -1;
	float typed;            // characters revealed so far
	int totalChars;
	float lineAlpha;        // the line's own fade
	float visAlpha = 1f;    // 0 while suppressed (caught, end card)
	float leaveHoldUntil;
	float nextLineAt;
	bool openingPassed;
	bool lastUsingPad;

	int hits;
	int lastSelected = -1;
	int lastCatches;
	bool pickUpInReach;
	float nextReachPoll;

	void Awake()
	{
		Build();
	}

	void Start()
	{
		struggle = FindFirstObjectByType<BondStruggle>();
		player = FindFirstObjectByType<PlayerController>();
		if (struggle == null || player == null)
		{
			Debug.LogWarning("[TutorialPrompts] No BondStruggle / PlayerController in scene. Prompts off.");
			enabled = false;
			return;
		}
		struggle.OnPullResolved += OnPull;
		struggle.OnUntieInput += OnUntie;
		lastSelected = struggle.Selected;
		lastCatches = struggle.Catches;
	}

	void OnDestroy()
	{
		if (struggle != null)
		{
			struggle.OnPullResolved -= OnPull;
			struggle.OnUntieInput -= OnUntie;
		}
		if (haloMat != null) Destroy(haloMat);
	}

	void OnPull(BondStruggle.PullKind kind, bool tool)
	{
		if (kind == BondStruggle.PullKind.Hit && ++hits >= hitsToLearn) done[(int)Step.Struggle] = true;
	}

	void OnUntie(int dir, bool correct)
	{
		if (correct) done[(int)Step.Untie] = true;
	}

	// LateUpdate: PlayerController.Update has run, so this frame's move taps and selection
	// are already on BondStruggle.
	void LateUpdate()
	{
		TrackDone();

		bool suppressed = Suppressed();
		visAlpha = Mathf.MoveTowards(visAlpha, suppressed ? 0f : 1f, Time.deltaTime / 0.2f);

		if (!suppressed) Advance();

		// Look values apply live, so they can be tuned in Play mode.
		lineRect.anchorMin = lineRect.anchorMax = screenAnchor;
		text.fontSize = fontSize;
		text.color = lineColor;
		group.alpha = lineAlpha * visAlpha;
	}

	// ---------------------------------------------------------------------------------
	// What the player has done
	// ---------------------------------------------------------------------------------

	void TrackDone()
	{
		// Choose: the selection moved while the old bond still held her (a freed bond or a
		// catch moves it too; those aren't her choosing).
		int sel = struggle.Selected;
		if (sel != lastSelected)
		{
			bool oldStillBound = lastSelected >= 0 && lastSelected < struggle.bonds.Length && struggle.bonds[lastSelected].Bound;
			if (oldStillBound && struggle.Catches == lastCatches && player.enabled) done[(int)Step.Choose] = true;
			lastSelected = sel;
		}
		lastCatches = struggle.Catches;

		// Move: a chair input with the move layer held.
		if (struggle.MoveLayerHeld && (struggle.MoveForwardTap || struggle.MoveLeftTap || struggle.MoveRightTap || struggle.MoveBackHeld))
			done[(int)Step.Move] = true;

		if (struggle.IsHidingHands) done[(int)Step.Hide] = true;

		if (player.GetHeldItem() != null) done[(int)Step.PickUp] = true;

		// Reach is a physics query: poll it a few times a second, and only while it matters.
		if (!done[(int)Step.PickUp] && !retired[(int)Step.PickUp] && Time.time >= nextReachPoll)
		{
			nextReachPoll = Time.time + 0.2f;
			pickUpInReach = player.PickUpInReach;
		}
	}

	bool Resolved(Step s) => done[(int)s] || retired[(int)s];

	int BoundCount()
	{
		int n = 0;
		foreach (var b in struggle.bonds) if (b.Bound) n++;
		return n;
	}

	bool GuardComing()
	{
		GuardController g = GuardController.Instance;
		if (g == null) return false;
		var s = g.CurrentState;
		return s == GuardController.GuardState.Approaching || s == GuardController.GuardState.AtDoor ||
			s == GuardController.GuardState.LeanIn || s == GuardController.GuardState.Checking;
	}

	/// <summary>The moment for this line is now.</summary>
	bool Available(Step s)
	{
		switch (s)
		{
			case Step.Struggle: return struggle.AnyBound;
			case Step.Choose:   return done[(int)Step.Struggle] && BoundCount() >= 2;
			case Step.Move:     return done[(int)Step.Struggle] && (Resolved(Step.Choose) || BoundCount() < 2);
			case Step.Untie:    return struggle.CanUntie(struggle.Selected);
			// Hands out. Wait for the untie lesson unless he's on his way: then it can't wait.
			case Step.Hide:     return struggle.HandsOut && !struggle.IsHidingHands && (Resolved(Step.Untie) || GuardComing());
			case Step.PickUp:   return done[(int)Step.Struggle] && pickUpInReach && player.GetHeldItem() == null;
			default:            return false;
		}
	}

	bool Suppressed()
	{
		if (!player.enabled) return true;                                   // caught sequence
		if (struggle.SlippedOut) return true;
		if (EndCard.Instance != null && EndCard.Instance.IsShowing) return true;
		return false;
	}

	// ---------------------------------------------------------------------------------
	// The line
	// ---------------------------------------------------------------------------------

	/// <summary>The most urgent line that still has something to teach, or -1.</summary>
	int Pick()
	{
		foreach (Step s in Priority)
		{
			int i = (int)s;
			if (done[i] || retired[i]) continue;
			if (string.IsNullOrEmpty(LineFor(s))) continue;
			if (Available(s)) return i;
		}
		return -1;
	}

	static int Rank(int step)
	{
		for (int i = 0; i < Priority.Length; i++) if ((int)Priority[i] == step) return i;
		return int.MaxValue;
	}

	void Advance()
	{
		if (!openingPassed)
		{
			float t = Time.timeSinceLevelLoad;
			bool mutterBusy = MutterSystem.Instance != null && MutterSystem.Instance.IsActive;
			if (t < startDelay || (mutterBusy && t < openingMaxWait)) return;
			openingPassed = true;
		}

		switch (phase)
		{
			case Phase.Idle:
			{
				if (Time.time < nextLineAt) return;
				int want = Pick();
				if (want >= 0) Begin(want);
				break;
			}

			case Phase.Showing:
			{
				bool usingPad = RaraInput.Read().usingPad;
				if (usingPad != lastUsingPad) Retext(usingPad);

				if (typed < totalChars)
				{
					typed = Mathf.Min(totalChars, typed + charsPerSecond * Time.deltaTime);
					text.maxVisibleCharacters = Mathf.FloorToInt(typed);
				}
				onScreen[current] += Time.deltaTime;

				if (done[current])
				{
					// Finish the sentence if she was faster than the typing, then hold and fade.
					typed = totalChars;
					text.maxVisibleCharacters = totalChars;
					retired[current] = true;
					Leave(doneHold, gapBetween);
				}
				else if (current != (int)Step.Struggle && giveUpAfter > 0f && onScreen[current] >= giveUpAfter)
				{
					retired[current] = true;          // not acted on: the card still has it
					Leave(0f, gapBetween);
				}
				else if (!Available((Step)current))
				{
					Leave(0f, gapBetween);            // the moment passed: may come back
				}
				else
				{
					int want = Pick();
					if (want >= 0 && Rank(want) < Rank(current)) Leave(0f, 0.15f);   // something more urgent
				}
				break;
			}

			case Phase.Leaving:
			{
				if (Time.time < leaveHoldUntil) return;
				lineAlpha = Mathf.MoveTowards(lineAlpha, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeOut));
				if (lineAlpha <= 0f)
				{
					phase = Phase.Idle;
					current = -1;
					text.text = "";
				}
				break;
			}
		}
	}

	void Begin(int step)
	{
		current = step;
		phase = Phase.Showing;
		lastUsingPad = RaraInput.Read().usingPad;
		SetText(lastUsingPad);
		typed = 0f;
		text.maxVisibleCharacters = 0;
		lineAlpha = 1f;
	}

	void Leave(float hold, float gap)
	{
		phase = Phase.Leaving;
		leaveHoldUntil = Time.time + hold;
		nextLineAt = Time.time + hold + fadeOut + gap;
	}

	/// <summary>The player switched device mid-line: swap the key name, keep what's typed.</summary>
	void Retext(bool usingPad)
	{
		lastUsingPad = usingPad;
		bool wasComplete = typed >= totalChars;
		SetText(usingPad);
		typed = wasComplete ? totalChars : Mathf.Min(typed, totalChars);
		text.maxVisibleCharacters = Mathf.FloorToInt(typed);
	}

	void SetText(bool usingPad)
	{
		Step s = (Step)current;
		string key = $"<color=#{ColorUtility.ToHtmlStringRGB(keyColor)}>{KeyFor(s, usingPad)}</color>";
		string line = LineFor(s);
		// A stray brace in an inspector-edited line must not throw every frame.
		try { text.text = string.Format(line, key); }
		catch (System.FormatException) { text.text = line; }
		text.ForceMeshUpdate();
		totalChars = text.textInfo.characterCount;
	}

	string LineFor(Step s) => s switch
	{
		Step.Struggle => struggleLine,
		Step.Choose => chooseLine,
		Step.Move => moveLine,
		Step.Untie => untieLine,
		Step.Hide => hideLine,
		Step.PickUp => pickUpLine,
		_ => "",
	};

	string KeyFor(Step s, bool usingPad)
	{
		if (!usingPad)
		{
			return s switch
			{
				Step.Struggle => struggleKey,
				Step.Choose => chooseKey,
				Step.Move => moveKey,
				Step.Untie => untieKey,
				Step.Hide => hideKey,
				Step.PickUp => pickUpKey,
				_ => "",
			};
		}
		return s switch
		{
			Step.Struggle => RaraInput.Label(RaraInput.Button.RT),
			Step.Choose => "L-stick",
			Step.Move => RaraInput.Label(RaraInput.Button.LT) + " + L-stick",
			Step.Untie => "D-pad",
			Step.Hide => RaraInput.Label(RaraInput.Button.LB),
			Step.PickUp => RaraInput.Label(RaraInput.Button.A),
			_ => "",
		};
	}

	// ---------------------------------------------------------------------------------
	// Build
	// ---------------------------------------------------------------------------------

	void Build()
	{
		var root = new GameObject("TutorialPromptsCanvas", typeof(RectTransform));
		root.transform.SetParent(transform, false);
		var canvas = root.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 50;
		var scaler = root.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1280f, 720f);
		scaler.matchWidthOrHeight = 0.5f;

		group = root.AddComponent<CanvasGroup>();
		group.interactable = false;
		group.blocksRaycasts = false;
		group.alpha = 0f;

		var go = new GameObject("Line", typeof(RectTransform));
		go.transform.SetParent(root.transform, false);
		lineRect = (RectTransform)go.transform;
		lineRect.anchorMin = lineRect.anchorMax = screenAnchor;
		lineRect.pivot = new Vector2(0.5f, 0f);
		lineRect.anchoredPosition = Vector2.zero;
		lineRect.sizeDelta = new Vector2(1100f, 90f);

		text = go.AddComponent<TextMeshProUGUI>();
		if (font != null) text.font = font;
		text.fontSize = fontSize;
		text.color = lineColor;
		text.alignment = TextAlignmentOptions.Bottom;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.raycastTarget = false;
		text.richText = true;
		text.text = "";

		// Halo: a material copy with TMP's underlay on, as MutterSystem.MakeFloating does.
		// A copy, so other text using the font is untouched.
		if (text.fontSharedMaterial != null)
		{
			haloMat = new Material(text.fontSharedMaterial) { name = text.fontSharedMaterial.name + " (Tutorial)" };
			haloMat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
			haloMat.SetColor(ShaderUtilities.ID_UnderlayColor, haloColor);
			haloMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, haloOffset.x);
			haloMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, haloOffset.y);
			haloMat.SetFloat(ShaderUtilities.ID_UnderlayDilate, haloDilate);
			haloMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, haloSoftness);
			text.fontSharedMaterial = haloMat;
			text.UpdateMeshPadding();
		}
	}
}
