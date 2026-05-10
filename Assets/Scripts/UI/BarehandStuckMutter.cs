using UnityEngine;

/// <summary>
/// Fires a "stuck-player rescue" mutter after the player has failed Struggle a
/// configurable number of times. The use case is L1: a player who heard the
/// entry mutter ("...without something sharp"), didn't internalize it, and is
/// now mashing Space at their starting position, getting only shake-and-fail
/// feedback. This mutter rescues them.
///
/// Listens to PlayerController.OnFailedStruggle (fires when a Struggle attempt
/// produced zero progress through the full pipeline). Counts attempts; once
/// the threshold is reached, calls MutterSystem.Play and self-disarms.
///
/// USAGE
/// -----
/// 1. Drop on any GameObject in the scene — typical placement is on a
///    "_Mutters" empty alongside MutterTriggers, so all of a level's mutter
///    authoring lives in one visible spot in the hierarchy.
/// 2. Write the rescue mutter content into the inspector.
/// 3. (Optional) Tune the threshold. Default 5 is conservative; lower =
///    earlier rescue, higher = trust the player longer.
///
/// FIRE-ONCE BEHAVIOR
/// ------------------
/// Same model as MutterTrigger: only consumes the fire-once charge if
/// MutterSystem.Play actually started the mutter. If the call is dropped
/// because another mutter is active, the count stays at threshold and the
/// next failed Struggle will retry. Prevents losing the rescue mutter to
/// timing accident.
///
/// COUNTER RESET
/// -------------
/// Counter does NOT reset when the player picks up a tool. Reasoning: once
/// they have a tool, OnFailedStruggle should stop firing (because Struggle
/// will now produce progress), and if it DOES still fire (wrong tool for
/// bond), they're still stuck and probably still need rescue. Either way,
/// fire-once self-disarm makes per-level reset unnecessary — each level
/// instance carries its own counter, gone on scene reload.
/// </summary>
public class BarehandStuckMutter : MonoBehaviour
{
	[Header("Mutter Content")]
	[Tooltip("The rescue mutter that fires after [threshold] failed Struggle attempts. " +
		"Quality bar: this is the moment a player most likely feels lost — the line should " +
		"feel like the detective genuinely re-orienting, not a tutorial popup. Reference " +
		"L1's entry mutter as the bar.")]
	[TextArea(2, 4)]
	[SerializeField] private string mutterContent;

	[Tooltip("How many failed Struggles before the rescue fires. Default 5 — a player " +
		"hammering Space hits this in 2-3 seconds, late enough to feel like a real " +
		"rescue, early enough to prevent give-up. Tune up if playtests show players " +
		"figure it out on their own; tune down if they get genuinely stuck.")]
	[SerializeField] private int threshold = 5;

	private PlayerController player;
	private int failedCount = 0;
	private bool fired = false;

	void Start()
	{
		// Auto-find the Player. Rara is single-character by design; there's no
		// scenario in v0 or planned content where multiple PlayerControllers
		// exist in one scene.
		player = FindFirstObjectByType<PlayerController>();
		if (player == null)
		{
			Debug.LogWarning($"BarehandStuckMutter '{name}': no PlayerController in scene. Component disabled.", this);
			enabled = false;
			return;
		}

		player.OnFailedStruggle += HandleFailedStruggle;
	}

	void OnDestroy()
	{
		// Defensive cleanup. Player is usually destroyed alongside this
		// component on scene unload, but unsubscribing prevents stale
		// references if the player outlives this object for any reason.
		if (player != null)
		{
			player.OnFailedStruggle -= HandleFailedStruggle;
		}
	}

	void HandleFailedStruggle()
	{
		if (fired) return;

		failedCount++;
		if (failedCount < threshold) return;

		if (string.IsNullOrEmpty(mutterContent)) return;
		if (MutterSystem.Instance == null) return;

		bool played = MutterSystem.Instance.Play(mutterContent);
		if (played)
		{
			fired = true;
		}
		// If not played (another mutter active), don't disarm. Next
		// failed Struggle will hit threshold again and retry.
	}
}
