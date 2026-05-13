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
///   - Ankles: her ankles are bound to each other. Legs move as a single
///     unit ("mermaid kick" leverage) — reduced kick force, not zero.
///     Part of the hogtied restraint (post-v0).
///   - AnkledToChair: her ankles are anchored to the chair legs. Implies
///     Ankles (you can't be anchored to a chair without your ankles being
///     bound). Different mechanical state from Ankles alone: legs are
///     furniture, kick is fully suppressed. ChairRestraint canon for L1-L3
///     is Wrists | Ankles | AnkledToChair. Cleared when the chair breaks
///     (post-tip), leaving Ankles alone — she's free of the chair but her
///     legs are still tied together.
///   - Knees: reserved. Not part of any default Act 1 restraint; available
///     as a runtime escalation flag once it has a mechanical job. Leading
///     candidate (Day 35): disables mermaid-kick by killing hip leverage
///     when legs are bound together — Ankles alone allows a reduced kick,
///     Ankles + Knees fully suppresses it. See ideas.md for the parking-lot
///     entry and alternative candidates.
///
/// SEMANTIC INVARIANTS
/// -------------------
/// AnkledToChair implies Ankles. Callers that set AnkledToChair should
/// also set Ankles in the same operation (e.g. AddBondState(Ankles |
/// AnkledToChair) on chair-restraint entry). Callers that clear Ankles
/// should also clear AnkledToChair — losing the ankle binding entirely
/// necessarily breaks the chair anchor. The reverse is not true: clearing
/// AnkledToChair alone (chair breaks, ankles still bound to each other)
/// is a valid and important state — it's exactly what the chair-tip
/// transition produces.
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
	AnkledToChair = 1 << 4,
}
