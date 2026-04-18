using UnityEngine;

// Base class for stationary environmental tools the player can struggle against
// (nails, sharp edges, broken glass, etc.). Not carryable — you move TO them.
public abstract class EnvironmentalTool : InteractableBase
{
	public abstract ToolType ToolType { get; }
}