using UnityEngine;

// Base class for stationary environmental tools the player can struggle against
// (nails, sharp edges, broken glass, etc.). Not carryable — you move TO them.
public abstract class EnvironmentalTool : InteractableBase
{
	public abstract ToolType ToolType { get; }

	// Whether the player can currently struggle against this tool. Default true:
	// the L1 Nail and most environmental tools accept a struggle from any angle
	// they're reachable at. Override to add a positional/orientation gate —
	// e.g. SharpEdge requires Cassie to be facing AWAY (bound hands behind her
	// back reaching the table lip). Called by PlayerController.TryStruggle before
	// the tool's progress is applied; returning false makes the struggle a no-op
	// for this tool (it falls through to the barehand / fail-feedback path).
	public virtual bool CanStruggleAgainst(PlayerController player) => true;
}
