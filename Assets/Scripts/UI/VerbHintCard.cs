using TMPro;
using UnityEngine;

/// <summary>
/// One row of the control hints panel. Two TextMeshPro labels: verb name and key.
/// Conditional hints (verb exists but unavailable in current sub-mode) are greyed
/// and may show a parenthetical hint at why.
///
/// Setup as a prefab:
///   - Root RectTransform with HorizontalLayoutGroup (or absolute layout).
///   - Child TMP_Text for verb (left-aligned).
///   - Child TMP_Text for key (right-aligned, monospace if you have it).
///   - This component on the root, with both texts wired up.
///   - Layout sized small — target ~150-200px wide, 24-32px tall per row.
/// </summary>
public class VerbHintCard : MonoBehaviour
{
	[Header("Text Targets")]
	[Tooltip("Label showing the verb name (e.g. 'Crawl', 'Kick'). May include a " +
		"parenthetical suffix for conditional hints (e.g. 'Kick (flip first)').")]
	[SerializeField] private TMP_Text verbLabel;
	[Tooltip("Label showing the key binding (e.g. 'W', 'F', 'Space').")]
	[SerializeField] private TMP_Text keyLabel;

	[Header("Conditional Styling")]
	[Tooltip("Color for active (usable) hints.")]
	[SerializeField] private Color activeColor = new Color(0.95f, 0.95f, 0.95f, 1f);
	[Tooltip("Color for conditional hints (verb exists but currently unavailable).")]
	[SerializeField] private Color conditionalColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

	public void SetHint(ControlHint hint)
	{
		string verbText = hint.verb;
		if (hint.conditional && !string.IsNullOrEmpty(hint.conditionalSuffix))
		{
			verbText += $" ({hint.conditionalSuffix})";
		}

		if (verbLabel != null) verbLabel.text = verbText;
		if (keyLabel != null) keyLabel.text = hint.key;

		Color c = hint.conditional ? conditionalColor : activeColor;
		if (verbLabel != null) verbLabel.color = c;
		if (keyLabel != null) keyLabel.color = c;
	}
}
