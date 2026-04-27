using UnityEngine;

// Pickupable that acts as a point when held -- bobby pin, nail, paperclip, shard.
public class PointTool : Pickupable
{
	public override ToolType ToolType => ToolType.Point;
}