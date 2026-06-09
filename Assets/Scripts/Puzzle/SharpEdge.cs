using UnityEngine;

// Stationary sharp edge Cassie works her wrist bond against — a rough table
// lip, an exposed screw head, a chipped tile. She moves TO it and struggles;
// it is not carried (that's what makes it an EnvironmentalTool, not a Pickupable).
//
// ToolType.Blade cuts a Rope bond (Rope accepts Blade and Point). Sibling to
// Nail, which is the Point-typed environmental tool from L1.
//
// VS fiction: the lip of the table the guard set his bottle down on. She saws
// the rope against it to free her wrists, then arms herself with the bottle —
// keeping the bottle a purely blunt weapon (no glass-cutting).
//
// BACK-FACING GATE
// ----------------
// The interaction verb here is STRUGGLE, not Pick Up — so the gate lives in
// CanStruggleAgainst (called by PlayerController.TryStruggle), NOT in OnPickUp.
// (OnPickUp never fires for an environmental tool; that was the bug.)
//
// Cassie's wrists are bound behind her back, so she must back up to the table
// edge and reach behind her to saw the rope against it. The gate is a dot
// product on her facing vs. the direction TO this edge: if she's facing AWAY,
// Dot(-forward, dirToEdge) is positive and large. Same math as Drawer's
// requireBackFacing, just hung on the struggle path instead of the pickup path.
// When the gate fails, TryStruggle skips this tool's contribution and the
// struggle falls through to the fail-feedback path (shake + fail SFX) — the
// diegetic "she strains, nothing gives, reposition" beat.
public class SharpEdge : EnvironmentalTool
{
	public override ToolType ToolType => ToolType.Blade;

	[Header("Back-Facing Gate")]
	[Tooltip("If true, the struggle only cuts when Cassie is facing AWAY from the " +
		"edge — simulating bound hands reaching behind her back to saw the rope. " +
		"Leave false only for testing, or if the room geometry makes the approach " +
		"angle unambiguous without a code gate.")]
	[SerializeField] private bool requireBackFacing = true;

	[Tooltip("Dot product threshold for the back-facing check. " +
		"Dot(-forward, dirToEdge) must be >= this value. " +
		"0.6 ~= a 53-degree cone behind the player — slightly looser than " +
		"Drawer's 0.7 because a table edge is a larger target than a drawer face. " +
		"Tighten toward 1.0 if Cassie cuts from the wrong angle; loosen toward 0 " +
		"if the window feels too strict.")]
	[Range(0f, 1f)]
	[SerializeField] private float backFacingThreshold = 0.6f;

	public override bool CanStruggleAgainst(PlayerController player)
	{
		if (!requireBackFacing) return true;

		Vector3 dirToEdge = (transform.position - player.transform.position).normalized;
		float backwardness = Vector3.Dot(-player.transform.forward, dirToEdge);

		if (backwardness < backFacingThreshold)
		{
			Debug.Log($"SharpEdge ({name}): not back-facing " +
				$"(dot={backwardness:F2} < {backFacingThreshold}). " +
				$"Cassie's bound hands can't reach the edge — back up to it.");
			return false;
		}

		Debug.Log($"SharpEdge ({name}): back-facing gate passed (dot={backwardness:F2}). Sawing.");
		return true;
	}
}
