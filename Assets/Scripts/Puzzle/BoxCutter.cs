using UnityEngine;

public class BoxCutter : Pickupable
{
	// Box cutters massively boost struggle effectiveness when held
	public override int StruggleModifier => 10;
}