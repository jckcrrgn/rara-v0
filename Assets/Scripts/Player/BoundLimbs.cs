using System;

/// <summary>
/// Bitfield describing which of Cassie's limbs are currently bound.
///
/// Defaults to Wrists on RestraintBase — every Act 1 restraint binds the
/// wrists (that's the baseline "she's tied up at all" state). Other limbs
/// are additive and are typically applied at runtime via
/// RestraintBase.AddBondState (e.g. the L6 failure loop adds Elbows when
/// the offstage guard returns and re-restrains her tighter).
///
/// The enum is future-proofed: new limbs can be added without breaking
/// existing call sites thanks to the |= / &= pattern. Current uses:
///   - Wrists: baseline binding for every Act 1 restraint
///   - Elbows: L6 failure loop adds this when Cassie is re-restrained tighter
///   - Ankles, Knees: ChairRestraint canon (legs tied to chair legs);
///     also reserved for the hogtied restraint (post-v0)
///
/// What "bound" means mechanically is per-restraint: elbow-binding might
/// reduce chair-tipping range a lot, reduce hanging-restraint kicks a
/// little, and barely affect floor inching at all. RestraintBase doesn't
/// impose semantics — it just tracks state and lets subclasses degrade
/// their own modifiers (GetStruggleModifier, GetKickModifier,
/// GetMovementModifier) based on which limbs are bound.
/// </summary>
[Flags]
public enum BoundLimbs
{
	None = 0,
	Wrists = 1 << 0,
	Elbows = 1 << 1,
	Ankles = 1 << 2,
	Knees = 1 << 3,
}
