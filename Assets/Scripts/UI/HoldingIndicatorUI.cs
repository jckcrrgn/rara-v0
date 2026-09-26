using UnityEngine;
using TMPro;

/// <summary>
/// "Holding: X" HUD. Redraws from player.GetHeldItem() every frame.
///
/// SMASH SUPPRESSION (Day 169)
/// ---------------------------
/// The bottle is consumed into shards on the strike's contact frame, but the
/// player's held-item reference is never cleared — the strike is terminal, so
/// gameplay never needed it to be. The HUD did. Rather than clear the item on
/// the player (DisarmHeldItem() would honour returnedOnDisarm and put the
/// bottle back on the table), the HUD listens to CassieStrikeDriver.OnContact
/// — the same multicast presentation event BottleSmashOnContact uses — and
/// hides itself for good. Presentation only; no gameplay state touched.
///
/// If a future level has a repeatable or non-terminal strike, the real fix is
/// PlayerController consuming the held item on contact. ideas.md.
/// </summary>
public class HoldingIndicatorUI : MonoBehaviour
{
	[SerializeField] private TMP_Text holdingText;
	[SerializeField] private PlayerController player;
	[SerializeField] private GameObject indicatorRoot; // The whole panel to show/hide

	[Tooltip("Strike driver whose contact frame hides the indicator. Leave empty " +
		"to find it in the scene on Awake.")]
	[SerializeField] private CassieStrikeDriver strikeDriver;

	private bool _suppressed;

	void Awake()
	{
		if (strikeDriver == null)
			strikeDriver = FindFirstObjectByType<CassieStrikeDriver>();
	}

	void OnEnable()
	{
		if (strikeDriver != null) strikeDriver.OnContact += HandleContact;
	}

	void OnDisable()
	{
		if (strikeDriver != null) strikeDriver.OnContact -= HandleContact;
	}

	void Start()
	{
		if (player == null)
		{
			player = FindFirstObjectByType<PlayerController>();
		}

		if (indicatorRoot != null)
		{
			indicatorRoot.SetActive(false);
		}
	}

	void Update()
	{
		if (_suppressed) return;
		if (player == null || holdingText == null || indicatorRoot == null) return;

		Pickupable held = player.GetHeldItem();
		if (held == null)
		{
			indicatorRoot.SetActive(false);
		}
		else
		{
			indicatorRoot.SetActive(true);
			holdingText.text = $"Holding: {held.ItemName}";
		}
	}

	private void HandleContact()
	{
		_suppressed = true;
		if (indicatorRoot != null) indicatorRoot.SetActive(false);
	}
}
