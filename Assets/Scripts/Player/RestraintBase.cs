using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for all restraint types. Owns the player's currently-bound
/// movement and verb behavior. Subclasses override what verbs do and which
/// inputs are available.
///
/// Hint contract: GetControlHints returns the current set of available verbs
/// and their keys, including conditional verbs (verbs that exist in this
/// restraint but aren't usable in the current sub-mode). When the hint list
/// would change (e.g. mode toggle), restraints fire OnHintsChanged so the
/// UI can refresh without polling. Restraints whose hints never change
/// (Chair, Cuffed) don't need to fire.
/// </summary>
public abstract class RestraintBase : MonoBehaviour
{
	/// <summary>
	/// Fires when GetControlHints would return a different list than before.
	/// Restraints with mode toggles (FloorRestraint inch/scoot) fire this on
	/// mode change. Restraints with fixed hints don't fire — UI just reads
	/// hints on restraint-change events.
	/// </summary>
	public event Action OnHintsChanged;

	/// <summary>
	/// Fire from subclasses when state changes such that the hint list would differ.
	/// </summary>
	protected void RaiseHintsChanged()
	{
		OnHintsChanged?.Invoke();
	}

	/// <summary>
	/// Return the current hint list for this restraint.
	///
	/// Convention: order is locomotion, then mode-toggle (if any), then verbs
	/// (kick, struggle, pickup). UI renders in returned order.
	///
	/// Default implementation returns the universal verbs (struggle, pickup) +
	/// nothing else. Subclasses override to add locomotion / verb-specific hints.
	/// </summary>
	public virtual List<ControlHint> GetControlHints()
	{
		return new List<ControlHint>
		{
			new ControlHint("Struggle", "Space"),
			new ControlHint("Pick Up", "E"),
		};
	}

	public abstract void HandleMovementInput(PlayerController player);
	public abstract void OnExit(PlayerController player);
	public virtual void OnEnter(PlayerController player) { }
	public virtual bool CanStruggle() => true;
	public virtual float GetStruggleModifier() => 1f;
	public virtual float GetKickModifier() => 1f;
	public virtual Vector3 GetKickDirection(PlayerController player) => -player.transform.forward;
	public virtual bool IsBusy => false;
}
