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
/// The enum is intentionally future-proofed: Ankles is reserved for the
/// hogtied restraint (post-v0 in the GDD), and the |= / &= pattern means
/// new limbs can be added without breaking existing call sites.
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
	None    = 0,
	Wrists  = 1 << 0,
	Elbows  = 1 << 1,
	Ankles  = 1 << 2,
}
