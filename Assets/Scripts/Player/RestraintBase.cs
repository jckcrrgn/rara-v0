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
///
/// Bond state contract: every restraint tracks a BoundLimbs bitfield
/// describing which limbs are currently bound. Default is Wrists (the
/// baseline "she's tied up at all" state — every Act 1 restraint binds
/// the wrists). Additional limbs are typically added at runtime via
/// AddBondState — e.g. the L6 failure loop adds Elbows when the offstage
/// guard returns and re-restrains her tighter. Subclasses degrade their
/// own modifiers (struggle, kick, movement) by reading BoundLimbs in
/// their override of the respective Get*Modifier methods.
/// </summary>
public abstract class RestraintBase : MonoBehaviour
{
	/// <summary>
	/// Fires when GetControlHints would return a different list than before.
	/// Restraints with mode toggles (FloorRestraint inch/scoot) fire this on
	/// mode change. Restraints with fixed hints don't fire — UI just reads
	/// hints on restraint-change events.
	///
	/// Also fired automatically by the default OnBondStateChanged
	/// implementation when bond state changes, since adding/removing a
	/// bond often changes which verbs are conditionally available.
	/// </summary>
	public event Action OnHintsChanged;

	/// <summary>
	/// Fire from subclasses when state changes such that the hint list would differ.
	/// </summary>
	protected void RaiseHintsChanged()
	{
		OnHintsChanged?.Invoke();
	}

	[Header("Bond State")]
	[Tooltip("Which limbs are currently bound. Defaults to Wrists — every " +
		"current restraint binds the wrists. Additional bonds are typically " +
		"added at runtime via AddBondState (e.g. L6 failure loop adds Elbows).")]
	[SerializeField] private BoundLimbs boundLimbs = BoundLimbs.Wrists;

	/// <summary>
	/// Current bond state. Read-only externally — mutate via Add/Remove.
	/// </summary>
	public BoundLimbs BoundLimbs => boundLimbs;

	/// <summary>
	/// Add one or more limbs to the bound set. No-op if all specified
	/// limbs are already bound. Fires OnBondStateChanged on actual change.
	/// </summary>
	public void AddBondState(BoundLimbs added)
	{
		BoundLimbs prev = boundLimbs;
		boundLimbs |= added;
		if (prev != boundLimbs)
		{
			OnBondStateChanged(prev, boundLimbs);
		}
	}

	/// <summary>
	/// Remove one or more limbs from the bound set. No-op if none of the
	/// specified limbs were bound. Fires OnBondStateChanged on actual change.
	/// </summary>
	public void RemoveBondState(BoundLimbs removed)
	{
		BoundLimbs prev = boundLimbs;
		boundLimbs &= ~removed;
		if (prev != boundLimbs)
		{
			OnBondStateChanged(prev, boundLimbs);
		}
	}

	/// <summary>
	/// Called when bond state actually changes (no-op adds/removes don't fire).
	/// Default implementation fires RaiseHintsChanged, since most bond changes
	/// affect which verbs are conditionally available.
	///
	/// Subclasses override to add restraint-specific reactions: refreshing
	/// internal caches, swapping bond visual geometry, playing a tightening
	/// SFX, etc. Call base.OnBondStateChanged to preserve the hint refresh.
	/// </summary>
	protected virtual void OnBondStateChanged(BoundLimbs prev, BoundLimbs current)
	{
		RaiseHintsChanged();
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

	/// <summary>
	/// Multiplier on the restraint's primary movement (hop force, orbit step,
	/// inch distance, etc). Subclasses apply this scalar where they use their
	/// movement values. Default 1.0 (no scaling).
	///
	/// Subclasses degrade movement based on BoundLimbs — e.g. ChairRestraint
	/// returns a smaller value when Elbows is bound, modeling that elbow
	/// binding tightens posture and makes hops/turns less effective. Each
	/// restraint chooses how much each limb affects it (elbow-binding
	/// matters more to chair-tipping than to floor-inching).
	/// </summary>
	public virtual float GetMovementModifier() => 1f;

	public virtual Vector3 GetKickDirection(PlayerController player) => -player.transform.forward;
	public virtual bool IsBusy => false;
}
