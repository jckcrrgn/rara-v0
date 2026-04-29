using UnityEngine;

/// <summary>
/// Base class for all restraint types (Chair, Floor, Cuffed, Hanging, Tape, etc.).
/// A restraint defines HOW the player moves and how struggle behaves while in that state.
///
/// Each concrete restraint is a MonoBehaviour you attach to the Player GameObject.
/// PlayerController holds a reference to the active one and delegates movement to it.
/// </summary>
public abstract class RestraintBase : MonoBehaviour
{
	/// <summary>
	/// Called every frame by PlayerController.Update().
	/// The restraint reads input (WASD, etc.) and moves the player accordingly.
	/// 'player' is passed in so the restraint can access player.Rb, player.IsGrounded, etc.
	/// </summary>
	public abstract void HandleMovementInput(PlayerController player);

	/// <summary>
	/// Returns true if the player can currently struggle.
	/// Default: true. Override for restraints that block struggle in some states
	/// (e.g., a future "stunned" or "gagged" variant).
	/// </summary>
	public virtual bool CanStruggle()
	{
		return true;
	}

	/// <summary>
	/// Multiplier applied to struggle progress while in this restraint.
	/// 1.0 = normal. >1 = struggle is more effective. <1 = harder to escape.
	/// Use this to differentiate restraint types: duct tape might be 1.2, cuffs 0.8, etc.
	/// </summary>
	public virtual float GetStruggleModifier()
	{
		return 1f;
	}

	public virtual float GetKickModifier()
	{
		return 1f;
	}

	/// <summary>
	/// Called when this restraint becomes active (start of level, or when SetRestraint is called).
	/// Use for setup: configure rigidbody constraints, play an "entering" animation, etc.
	/// </summary>
	public virtual void OnEnter(PlayerController player) { }

	/// <summary>
	/// Called when this restraint is being swapped out for another.
	/// Use for cleanup: reset rigidbody settings, stop coroutines, etc.
	/// </summary>
	public abstract void OnExit(PlayerController player);
}
