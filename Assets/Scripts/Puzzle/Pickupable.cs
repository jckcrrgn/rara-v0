using UnityEngine;

// Base class for objects the player can carry.
public class Pickupable : InteractableBase
{
	[Header("Pickup Settings")]
	[SerializeField] private string itemName = "Item";

	[Tooltip("If true, this item can only be picked up while Cassie is down on " +
		"the floor — a restraint whose CanReachFloorTools() returns true, i.e. " +
		"FloorRestraint. Set this on tools that lie on the ground (e.g. the lamp " +
		"shard prefab) so the loud-smash solve path actually costs the floor " +
		"crawl instead of being grabbable from the chair. Leave FALSE for items " +
		"reached another way: the drawer pen is gated by the Drawer's collider, " +
		"not by floor access, so it stays false.")]
	[SerializeField] private bool requiresFloorAccess = false;

	[Tooltip("Only used when requiresFloorAccess is true. Radius (world units) " +
		"within which the player's bound hands (handAnchor) must be of this tool " +
		"to grab it. Tune against the hand anchor's dorsal offset: keep it SMALLER " +
		"than that offset so a prone or on-side player (anchor up in the air) " +
		"fails, while supine-over-the-tool succeeds. ~0.35 is a starting point for " +
		"a 1u-thick body.")]
	[SerializeField] private float grabRadius = 0.35f;

	[Tooltip("Mirror of requiresFloorAccess for the opposite posture: if true, " +
		"this item is only grabbable while Cassie can reach UP to furniture height " +
		"— a restraint whose CanReachUprightTools() returns true (chair-bound, " +
		"standing). Set on items reached from a seated posture that must NOT be " +
		"grabbable once she's floor-bound — e.g. the L6 pen (reachable from the " +
		"chair via the drawer, not from the floor until Stand-Up debuts in L7).")]
	[SerializeField] private bool requiresUprightReach = false;

	[Tooltip("Visual-only copy of this item parented to Cassie's hand bone. Enabled " +
	"while held, disabled when returned or confiscated. MUST be a child of the " +
	"hand bone in the scene — NOT a child of this GameObject, which gets " +
	"SetActive(false) on pickup and would take the hand copy down with it. " +
	"Strip the Pickupable, colliders and Rigidbody off the copy: it's presentation, " +
	"and the strike's contact is a callback, not a physical hit.")]
	[SerializeField] private GameObject heldVisual;

	[Tooltip("On a caught failure, does the guard put this back where he found it?\n\n" +
	"Set TRUE when the item is the level's ONLY route to the solve — the VS " +
	"bottle, where confiscation would leave the slice unsolvable.\n\n" +
	"Leave FALSE when other paths exist and losing this one should cost her — " +
	"the L6 pen, where the lamp and the chair are still on the table.")]
	[SerializeField] private bool returnedOnDisarm = false;
	public bool ReturnedOnDisarm => returnedOnDisarm;

	[Tooltip("True if this item can be used as the Strike weapon. " +
	"The blunt object Cassie conceals for the turnaround beat.")]
	[SerializeField] private bool isWeapon = false;
	public bool IsWeapon => isWeapon;

	public string ItemName => itemName;

	// Default tool type is BareHands (i.e. no upgrade). Subclasses override.
	public virtual ToolType ToolType => ToolType.BareHands;

	public override void OnPickUp(PlayerController player)
	{
		// Floor-access gate. A tool lying on the ground can only be taken when
		// Cassie is both (1) down on the floor and (2) has her bound hands over
		// it. Mirrors how Drawer.OnPickUp self-gates: check, bail with a cue if
		// unmet. The two tiers give distinct "can't reach" feedback.
		if (requiresFloorAccess)
		{
			// Tier 1 (#1): on the floor at all?
			RestraintBase restraint = player.CurrentRestraint;
			if (restraint == null || !restraint.CanReachFloorTools())
			{
				// Placeholder cue, same as Drawer's "can't reach" log. Swap for a
				// strain SFX / mutter when those exist.
				Debug.Log($"Pickupable ({itemName}): can't reach from here — " +
					$"Cassie needs to be down on the floor.");
				return;
			}

			// Tier 2 (#2): bound hands actually over it? With hands-behind
			// binding the anchor only reaches floor level when she's rolled
			// supine, so this check encodes "roll onto her back over the tool"
			// without a separate belly-up test.
			if (!player.AreHandsOver(transform.position, grabRadius))
			{
				Debug.Log($"Pickupable ({itemName}): on the floor but her hands " +
					$"aren't over it — roll onto her back over the tool.");
				return;
			}
		}

		// Upright-reach gate. Mirror of the floor-access gate for items reached
		// from a seated/standing posture. Single-tier — no hand-over check,
		// because the reach is "up to a surface," not "hands down onto the floor."
		if (requiresUprightReach)
		{
			RestraintBase restraint = player.CurrentRestraint;
			if (restraint == null || !restraint.CanReachUprightTools())
			{
				Debug.Log($"Pickupable ({itemName}): can't reach from here — " +
					$"Cassie can't get to it while she's down on the floor.");
				return;
			}
		}

		player.HoldItem(this);
		// Hide the world version while held — the hand copy takes over.
		if (heldVisual != null) heldVisual.SetActive(true);
		gameObject.SetActive(false);
	}

	// Called by player when they drop or use up the item
	public void DropFromPlayer()
	{
		HideHeldVisual();
		gameObject.SetActive(true);
	}

	/// Take the item out of her hand without restoring the world version.
	/// Confiscation needs this — it disables the world object directly and
	/// never routes through DropFromPlayer, so without it the hand copy
	/// would stay visible in a hand that isn't holding anything.
	public void HideHeldVisual()
	{
		if (heldVisual != null) heldVisual.SetActive(false);
	}
}
