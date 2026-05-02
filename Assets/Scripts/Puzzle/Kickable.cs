using UnityEngine;

/// <summary>
/// Abstract base for anything the Kick verb can target meaningfully.
/// Concrete subclasses: KickableDoor (L4), KickableShelf (future floor-level
/// where you knock a tool down with a kick), KickableGuard (finale strike).
///
/// Pattern mirrors Pickupable/InteractableBase: the verb (Kick) finds the
/// nearest Kickable and delegates the response to the target. Each Kickable
/// owns its own windup threshold and "fully kicked" payoff.
///
/// Anything that ISN'T a Kickable but still gets kicked (walls, furniture,
/// the floor) goes through Kick's default thud feedback in PlayerController.
/// </summary>
public abstract class Kickable : InteractableBase
{
	[Header("Kick")]
	[Tooltip("Cumulative kick force needed to trigger OnFullyKicked. " +
		"A free-legged kick adds 1.0; a floor-bound kick adds whatever the restraint's " +
		"GetKickModifier returns (e.g. 0.5). So requiredForce=3 means 3 free kicks " +
		"or 6 floor-bound kicks.")]
	[SerializeField] protected float requiredForce = 3f;

	protected float currentForce = 0f;
	protected bool isResolved = false;

	/// <summary>
	/// Returns true if this kickable accepts the kick right now (e.g. positioning
	/// gate satisfied). If false, PlayerController treats it as a thud target
	/// instead of routing the kick here.
	///
	/// Default: always accepts. Override for position-gated kickables like the van door.
	/// </summary>
	public virtual bool CanBeKicked(PlayerController player) => !isResolved;

	/// <summary>
	/// Called by PlayerController.TryKick when this is the nearest Kickable AND CanBeKicked.
	/// Accumulates force; once threshold met, fires OnFullyKicked exactly once.
	/// </summary>
	public virtual void OnKick(PlayerController player, float force)
	{
		if (isResolved) return;

		currentForce += force;
		Debug.Log($"{name}: kick registered (+{force:F2}). Progress: {currentForce:F2} / {requiredForce:F2}");
		OnKickRegistered(player, force);

		if (currentForce >= requiredForce)
		{
			isResolved = true;
			OnFullyKicked(player);
		}
	}

	/// <summary>
	/// Fires every time a kick lands. Override for per-hit feedback (windup SFX,
	/// door-shake-but-doesn't-open, etc.). Default: no-op.
	/// </summary>
	protected virtual void OnKickRegistered(PlayerController player, float force) { }

	/// <summary>
	/// Fires once when accumulated force >= requiredForce. Override for the
	/// payoff (door swings open, shelf topples, guard goes down).
	/// </summary>
	protected abstract void OnFullyKicked(PlayerController player);
}
