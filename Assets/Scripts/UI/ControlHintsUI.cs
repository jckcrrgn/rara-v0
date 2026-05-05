using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Renders the current restraint's control hints in a stacked list.
///
/// Subscribes to PlayerController.OnRestraintChanged for restraint swaps and
/// to the active restraint's OnHintsChanged for in-restraint mode toggles
/// (e.g. FloorRestraint inch↔scoot). Re-binds the latter on every restraint
/// change so we don't leak subscriptions to old restraints.
///
/// Rendering: each hint becomes an instance of cardPrefab parented under
/// cardContainer (which should have a VerticalLayoutGroup). Cards are pooled
/// per-rebuild — old cards are destroyed, new ones are spawned. List sizes
/// are tiny (4-7 items) so the alloc cost is irrelevant.
///
/// Setup:
///   - Place this on the hints panel root in the Canvas (top-left area).
///   - cardContainer: a child RectTransform with a VerticalLayoutGroup,
///     ContentSizeFitter (vertical=preferred) so the panel grows to fit.
///   - cardPrefab: a small UI prefab with a VerbHintCard component (see below).
///   - playerController: drag the Player from the scene. Found at runtime if null.
/// </summary>
public class ControlHintsUI : MonoBehaviour
{
	[Header("References")]
	[Tooltip("The Player. If null, found at runtime via FindFirstObjectByType.")]
	[SerializeField] private PlayerController playerController;
	[Tooltip("Parent for spawned hint cards. Should have VerticalLayoutGroup.")]
	[SerializeField] private RectTransform cardContainer;
	[Tooltip("Prefab with a VerbHintCard component. Spawned once per hint.")]
	[SerializeField] private VerbHintCard cardPrefab;

	private RestraintBase boundRestraint;
	private readonly List<VerbHintCard> spawnedCards = new List<VerbHintCard>();

	private void Start()
	{
		if (playerController == null)
		{
			playerController = FindFirstObjectByType<PlayerController>();
			if (playerController == null)
			{
				Debug.LogError("ControlHintsUI: no PlayerController in scene. Hints will not render.");
				enabled = false;
				return;
			}
		}

		playerController.OnRestraintChanged += HandleRestraintChanged;

		// Initial bind — Start runs after PlayerController.Start has set the
		// initial restraint, so currentRestraint is already populated.
		HandleRestraintChanged();
	}

	private void OnDestroy()
	{
		if (playerController != null)
		{
			playerController.OnRestraintChanged -= HandleRestraintChanged;
		}
		UnbindFromRestraint();
	}

	private void HandleRestraintChanged()
	{
		UnbindFromRestraint();
		boundRestraint = playerController.CurrentRestraint;
		if (boundRestraint != null)
		{
			boundRestraint.OnHintsChanged += Rebuild;
		}
		Rebuild();
	}

	private void UnbindFromRestraint()
	{
		if (boundRestraint != null)
		{
			boundRestraint.OnHintsChanged -= Rebuild;
			boundRestraint = null;
		}
	}

	private void Rebuild()
	{
		// Clear old cards. Pool would be a small optimization here but with
		// 4-7 cards swapped on infrequent events (mode toggle, restraint
		// change), the simplicity of destroy-and-respawn is worth more than
		// the saved alloc.
		foreach (VerbHintCard card in spawnedCards)
		{
			if (card != null) Destroy(card.gameObject);
		}
		spawnedCards.Clear();

		if (boundRestraint == null) return;
		if (cardPrefab == null || cardContainer == null)
		{
			Debug.LogWarning("ControlHintsUI: cardPrefab or cardContainer not assigned. Hints will not render.");
			return;
		}

		List<ControlHint> hints = boundRestraint.GetControlHints();
		foreach (ControlHint hint in hints)
		{
			VerbHintCard card = Instantiate(cardPrefab, cardContainer);
			card.SetHint(hint);
			spawnedCards.Add(card);
		}
	}
}
