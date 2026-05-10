using UnityEngine;

// Pickupable that acts as a blade when held -- scissors, box cutter, shard.
// Overrides the base Pickupable's ToolType so PlayerController.TryStruggle
// pulls from Bond.bladeProgress (Blade tool type) rather than the default 0.
public class BladeTool : Pickupable
{
	public override ToolType ToolType => ToolType.Blade;
}