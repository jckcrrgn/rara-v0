using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scaffold-scene controller for tuning the kick-impulse pathway. Lives in
/// KickTestScaffold.unity. Provides:
///
///   - A bond-state cycler. Number keys 1-4 swap the player's ChairRestraint
///     BoundLimbs between the four canonical kick-modifier states (free,
///     mermaid-kick, AnkledToChair, Ankles+Knees). This lets you A/B kick
///     magnitudes across states from inside Play mode without right-clicking
///     into context menus.
///   - A prop reset. R resets every KickTestProp in the scene to its starting
///     pose (overrides LevelManager's restart-level binding, because the
///     scaffold doesn't have a LevelManager).
///   - An on-screen overlay via OnGUI showing: current restraint, current
///     BoundLimbs, GetKickModifier(), kickImpulseScale * modifier (the actual
///     impulse magnitude that will be applied to loose Rigidbodies on the
///     next kick), and a per-prop label.
///
/// Setup notes for the scene itself (build by hand in editor):
///   - Drop a Player prefab in. Confirm ChairRestraint is the active restraint
///     with rockingEnabled=true.
///   - Lay a row of test props along the player's kick axis (local -Z from
///     player). Suggested: 1kg, 5kg, 10kg, 25kg, 50kg. Color-code via material.
///     Each prop needs Rigidbody + Collider + KickTestProp.
///   - Drop a KickableDoor as a regression test, off to one side so it's not
///     in the row's path.
///   - Drop this component on an empty GameObject; assign player reference.
///   - Add a Ground plane on a layer named "Ground" so props settle.
///
/// Calibration workflow:
///   1. Start in state 1 (free legs, modifier 1.0). Kick the 1kg prop. It
///      should fly meaningfully — if it barely moves, raise kickImpulseScale
///      on the player. If it goes into orbit, lower it.
///   2. Kick progressively heavier props. The 10kg "lamp-equivalent" should
///      tip but not launch; the 25kg "nightstand-equivalent" should rock or
///      slide but not tip.
///   3. Switch to state 2 (mermaid-kick, 0.4). Repeat against the lamp prop.
///      Mermaid-kick should still tip the lamp (this is the L6 emergent solve
///      path) but be visibly weaker than the free kick.
///   4. Switch to states 3 and 4 (zero modifier). Confirm kicks produce
///      effort grunt only, no prop movement.
///   5. Once impulse magnitudes feel right, lift the values into the Player
///      prefab and remove this scaffold scene from the build (or keep it as
///      a regression-test scene).
/// </summary>
public class KickTestScaffold : MonoBehaviour
{
	[Tooltip("Reference to the player in the scaffold scene. Used to read the " +
		"active ChairRestraint's BoundLimbs and to call AddBondState/RemoveBondState.")]
	[SerializeField] private PlayerController player;

	[Tooltip("Show the on-screen overlay. Toggle off for clean screenshots.")]
	[SerializeField] private bool showOverlay = true;

	private List<KickTestProp> props;
	private ChairRestraint chair;

	private void Start()
	{
		// Cache every prop in the scene up front; the row is static.
		props = new List<KickTestProp>(FindObjectsByType<KickTestProp>(FindObjectsSortMode.None));

		if (player != null)
		{
			chair = player.GetComponent<ChairRestraint>();
		}

		if (chair == null)
		{
			Debug.LogWarning("[KickTestScaffold] No ChairRestraint on player. Bond state cycling will not work.");
		}
	}

	private void Update()
	{
		HandleBondStateCycling();
		HandleReset();
	}

	/// <summary>
	/// 1: free legs (clear Ankles + AnkledToChair + Knees)
	/// 2: mermaid-kick (Ankles only)
	/// 3: AnkledToChair (Ankles + AnkledToChair — canonical chair state)
	/// 4: Ankles + Knees (mermaid-kick disabler, L6 failure-loop escalation)
	///
	/// Direct sets via the existing debug context-menu API on ChairRestraint —
	/// the scaffold is just a faster keyboard interface to the same operations.
	/// </summary>
	private void HandleBondStateCycling()
	{
		if (chair == null) return;

		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			chair.RemoveBondState(BoundLimbs.Ankles | BoundLimbs.AnkledToChair | BoundLimbs.Knees);
			Debug.Log($"[Scaffold] State 1: free legs. BoundLimbs = {chair.BoundLimbs}");
		}
		else if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			chair.RemoveBondState(BoundLimbs.AnkledToChair | BoundLimbs.Knees);
			chair.AddBondState(BoundLimbs.Ankles);
			Debug.Log($"[Scaffold] State 2: mermaid-kick. BoundLimbs = {chair.BoundLimbs}");
		}
		else if (Input.GetKeyDown(KeyCode.Alpha3))
		{
			chair.RemoveBondState(BoundLimbs.Knees);
			chair.AddBondState(BoundLimbs.Ankles | BoundLimbs.AnkledToChair);
			Debug.Log($"[Scaffold] State 3: AnkledToChair (canon). BoundLimbs = {chair.BoundLimbs}");
		}
		else if (Input.GetKeyDown(KeyCode.Alpha4))
		{
			chair.RemoveBondState(BoundLimbs.AnkledToChair);
			chair.AddBondState(BoundLimbs.Ankles | BoundLimbs.Knees);
			Debug.Log($"[Scaffold] State 4: Ankles+Knees (suppressor). BoundLimbs = {chair.BoundLimbs}");
		}
	}

	/// <summary>
	/// Reset all props to their starting pose. Bound to R, which mirrors the
	/// existing LevelManager restart-level binding, but the scaffold doesn't
	/// have a LevelManager so the binding is free here.
	/// </summary>
	private void HandleReset()
	{
		if (!Input.GetKeyDown(KeyCode.R)) return;

		foreach (KickTestProp prop in props)
		{
			if (prop != null) prop.ResetToStart();
		}
		Debug.Log($"[Scaffold] Reset {props.Count} props to start.");
	}

	private void OnGUI()
	{
		if (!showOverlay || player == null) return;

		const int width = 360;
		const int padding = 12;
		GUI.Box(new Rect(padding, padding, width, 180), "Kick Test Scaffold");

		float y = padding + 24;
		float lineH = 18;

		GUIStyle style = new GUIStyle(GUI.skin.label);
		style.fontSize = 12;

		string restraintName = player.CurrentRestraint != null
			? player.CurrentRestraint.GetType().Name
			: "<null>";
		float modifier = player.CurrentRestraint != null
			? player.CurrentRestraint.GetKickModifier()
			: 0f;

		// Read the actual tuned value from the player so overlay stays accurate.
		float displayedScale = player.KickImpulseScale;
		float impulse = modifier * displayedScale;

		GUI.Label(new Rect(padding + 8, y, width - 16, lineH), $"Restraint: {restraintName}", style); y += lineH;
		if (chair != null)
		{
			GUI.Label(new Rect(padding + 8, y, width - 16, lineH), $"BoundLimbs: {chair.BoundLimbs}", style); y += lineH;
		}
		GUI.Label(new Rect(padding + 8, y, width - 16, lineH), $"GetKickModifier(): {modifier:F2}", style); y += lineH;
		GUI.Label(new Rect(padding + 8, y, width - 16, lineH), $"Impulse magnitude: {impulse:F2} (scale {displayedScale:F1})", style); y += lineH;
		y += 6;
		GUI.Label(new Rect(padding + 8, y, width - 16, lineH), "1 free | 2 mermaid | 3 chair | 4 supp | R reset", style); y += lineH;
	}
}
