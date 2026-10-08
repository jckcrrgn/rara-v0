// TODO: TEMPORARY SCAFFOLDING. Delete when character model + bond geometry exist.
// Replace with diegetic feedback -- bonds visually fraying/loosening on the player.
// See ideas.md "Diegetic struggle feedback (Day 15)".

using UnityEngine;
using UnityEngine.UI;

public class BondMeterUI : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private PlayerController player;
	[SerializeField] private Transform target;
	[SerializeField] private Image fillImage;

	[Tooltip("Faded to 0 once the bond is broken. Auto-found on this object if empty.")]
	[SerializeField] private CanvasGroup group;

	[Header("Positioning")]
	[SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

	[Header("Flash")]
	[SerializeField] private Color baseColor = new Color(0.8f, 0.2f, 0.2f, 1f);
	[SerializeField] private Color flashColor = new Color(1f, 1f, 0.6f, 1f);
	[SerializeField] private float flashDuration = 0.15f;

	private Camera cam;
	private float flashTimer;

	void Start()
	{
		cam = Camera.main;

		if (player == null)
			player = FindFirstObjectByType<PlayerController>();

		if (target == null && player != null)
			target = player.transform;

		if (group == null)
			group = GetComponent<CanvasGroup>();

		if (player != null)
			player.OnStruggleProgressChanged += HandleProgressChanged;
		if (fillImage != null)
			fillImage.color = baseColor;
		UpdateFill();
	}

	void OnDestroy()
	{
		if (player != null)
			player.OnStruggleProgressChanged -= HandleProgressChanged;
	}

	void LateUpdate()
	{
		if (cam == null) cam = Camera.main;
		if (target != null && cam != null)
		{
			Vector3 worldPos = target.position + worldOffset;
			transform.position = cam.WorldToScreenPoint(worldPos);
		}

		if (flashTimer > 0f && fillImage != null)
		{
			flashTimer -= Time.deltaTime;
			float t = Mathf.Clamp01(flashTimer / flashDuration);
			fillImage.color = Color.Lerp(baseColor, flashColor, t);
		}

		UpdateVisibility();
	}

	// Polled, not event-driven, on purpose. Hiding with SetActive(false) would
	// stop this LateUpdate, and the meter could never come back when the guard
	// catches her and re-binds. Alpha on the CanvasGroup keeps the script alive,
	// and polling means a re-bind shows the meter again whether or not the
	// reset fires OnStruggleProgressChanged.
	void UpdateVisibility()
	{
		if (group == null || player == null) return;

		bool broken = player.StruggleProgress >= player.BondStrength;

		// Let the final flash finish before the bar disappears.
		bool show = !broken || flashTimer > 0f;

		float want = show ? 1f : 0f;
		if (group.alpha == want) return;

		group.alpha = want;
		if (show) UpdateFill();   // coming back after a re-bind: refresh the bar
	}

	void HandleProgressChanged()
	{
		UpdateFill();
		flashTimer = flashDuration;
		if (fillImage != null)
			fillImage.color = flashColor;
	}

	void UpdateFill()
	{
		if (player == null || fillImage == null) return;
		float remaining = 1f - ((float)player.StruggleProgress / player.BondStrength);
		fillImage.fillAmount = Mathf.Clamp01(remaining);
	}
}
