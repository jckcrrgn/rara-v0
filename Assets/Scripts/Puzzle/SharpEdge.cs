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
public class SharpEdge : EnvironmentalTool
{
	public override ToolType ToolType => ToolType.Blade;
}
