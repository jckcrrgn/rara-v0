using UnityEngine;
using TMPro;

public class HoldingIndicatorUI : MonoBehaviour
{
	[SerializeField] private TMP_Text holdingText;
	[SerializeField] private PlayerController player;
	[SerializeField] private GameObject indicatorRoot; // The whole panel to show/hide

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
}