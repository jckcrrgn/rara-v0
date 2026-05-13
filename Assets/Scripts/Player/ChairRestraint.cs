using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chair restraint: player is tied to a chair. Can rotate in place (via
/// physical hop-turns) and hop forward. Default restraint for L1-L3.
///
/// ROTATION DESIGN
/// ---------------
/// Each A/D press fires a TURN-HOP: a single physics impulse with three
/// components applied simultaneously --
///   - Vertical (up): lifts the chair off the floor
///   - Lateral (body-relative right * direction): scoots toward the side pressed
///   - Angular torque (Y-axis * direction): twists the chair
///
/// Position AND yaw are both genuinely physics-driven. Yaw is NOT clamped to
/// a target -- the angular velocity decays via Rigidbody angular drag.
/// Heading drifts hop-to-hop, exactly like a real bound person on a chair
/// who can never quite pivot to a clean angle.
///
/// TAP-ONLY: one impulse per key DOWN. No hold-to-repeat. The cycle ends as
/// soon as the impulse is applied -- there's no busy window. The next press
/// can fire as soon as the player chooses, gated only on IsGrounded so a
/// chair mid-air doesn't gain free torque.
///
/// This relies on the player Rigidbody having FreezeRotation X and Z set
/// (or equivalent constraints). Without that, torque around Y will tumble
/// the chair through other axes. If you start to see chair-wobble in
/// playtest, that's the symptom -- check the Rigidbody Constraints in
/// the inspector.
///
/// TUNING NOTES
/// ------------
/// Three knobs are coupled and should be tuned in this order:
///   1. turnVerticalImpulse: dial up until the chair clearly leaves the floor
///      on a press. This is what makes it read as a hop, not a torque-pivot.
///   2. turnAngularImpulse: dial until a tap rotates ~20-30° before angular
///      drag settles it. Direction-of-impulse and degrees-actually-traveled
///      are decoupled now; you tune for "feels right" not "exact angle."
///   3. turnLateralImpulse: subtle. Tap and watch -- if the chair scoots
///      sideways too obviously, lower it. If it only spins in place with
///      no body translation, raise it. Small values (0.3-0.8).
///
/// Rigidbody.angularDrag is the secret fourth knob. If turns over-rotate
/// (chair keeps spinning past intent), increase angularDrag on the Player
/// Rigidbody. If turns feel sluggish/locked, decrease it. Try the inspector
/// value 5-15 range. (Linear drag on the Rigidbody is independent and
/// affects how fast the chair stops sliding.)
/// 
/// BOND-STATE
/// ----------
/// Default ChairRestraint instances should be configured with
/// BoundLimbs = Wrists | Ankles | AnkledToChair — Cassie's wrists are
/// tied behind the chair back, her ankles are bound to each other, AND
/// her ankles are anchored to the chair legs. This is canon for L1-L3.
///
/// The Ankles / AnkledToChair distinction matters: Ankles alone means
/// her legs are bound together (mermaid-kick — reduced force, not zero),
/// while AnkledToChair means her legs are also anchored to the chair
/// (kick is fully suppressed because the legs are furniture). The L6
/// design uses the gap between these states: she clears AnkledToChair
/// first, regains mermaid-kick, kicks the wall to tip backward.
///
/// Knees and hogtie are ADDITIVE runtime escalations, not part of the
/// chair default. The L6 failure-loop pattern is the model: a base
/// restraint state plus runtime AddBondState calls to escalate within
/// a level. Knees specifically is the leading candidate for "mermaid
/// kick disabler" — see BoundLimbs.cs and ideas.md.
///
/// Bond effects:
///   - Elbows: GetMovementModifier 0.65, GetStruggleModifier 0.5
///     (L6 failure-loop adds this to model tighter re-binding)
///   - AnkledToChair: GetKickModifier 0 (legs are furniture)
///   - Ankles without AnkledToChair: GetKickModifier 0.4 (mermaid-kick;
///     reduced force, used for chair-tip via wall reaction in L6)
///   - Ankles + Knees: GetKickModifier 0 (hip leverage gone, even
///     mermaid-kick fails). Reserved for L6 failure escalation.
/// </summary>
public class ChairRestraint : RestraintBase
{
	[Header("Forward Hop (W)")]
	[Tooltip("Impulse applied along (forward + up) when the player presses W. " +
		"Existing forward-hop verb, unchanged.")]
	[SerializeField] private float hopForce = 3f;

	[Header("Turn-Hop (A / D)")]
	[Tooltip("Vertical impulse component. Lifts the chair off the floor on " +
		"each tap so the rotation doesn't read as a slide. Tune this first.")]
	[SerializeField] private float turnVerticalImpulse = 2.0f;

	[Tooltip("Lateral impulse component, body-relative. Scoots toward the " +
		"side pressed (A = -right, D = +right). Subtle. 0.3-0.8 range.")]
	[SerializeField] private float turnLateralImpulse = 0.5f;

	[Tooltip("Angular impulse around Y-axis, scaled by direction. Higher = " +
		"more rotation per tap. Final angle traveled depends on this AND on " +
		"the Rigidbody's angularDrag. Tune by feel.")]
	[SerializeField] private float turnAngularImpulse = 0.2f;

	[Header("SFX (optional)")]
	[Tooltip("Plays on each turn-hop tap. Wooden creak, chair scuff, floor " +
		"thud. Optional -- safe to leave empty until SFX wiring pass.")]
	[SerializeField] private AudioClip turnHopClip;

	// Rotation is impulse-fired and grounded-gated, but doesn't have an
	// extended busy window -- you tap, the impulse fires, you can tap again.
	// IsBusy stays false: no other body verb needs to know about an in-flight
	// turn-hop because there's nothing to coordinate with.
	public override bool IsBusy => false;

	public override void HandleMovementInput(PlayerController player)
	{
		// Turn taps: A or D, single press, gated on grounded.
		if (Input.GetKeyDown(KeyCode.A) && player.IsGrounded)
		{
			ApplyTurnHop(player, -1f);
		}
		else if (Input.GetKeyDown(KeyCode.D) && player.IsGrounded)
		{
			ApplyTurnHop(player, +1f);
		}

		// Forward hop: W, single press, gated on grounded.
		if (Input.GetKeyDown(KeyCode.W) && player.IsGrounded)
		{
			ForwardHop(player);
		}
	}

	/// <summary>
	/// Apply a single turn-hop impulse. All three components fire on the
	/// same frame -- the chair lifts, scoots laterally, and spins angularly
	/// as one event. No coroutine, no busy window: physics owns the rest of
	/// the cycle (gravity lands the chair, angular drag settles the spin,
	/// linear drag settles the lateral slide).
	/// </summary>
	private void ApplyTurnHop(PlayerController player, float direction)
	{
		// Linear: vertical lift + body-relative lateral kick.
		// transform.right rotates with the chair so "left" and "right" follow
		// the chair's facing, which is what you want -- A always kicks toward
		// her left shoulder regardless of which way she's facing the world.
		float mod = GetMovementModifier();
		Vector3 lateral = player.transform.right * direction * turnLateralImpulse * mod;
		Vector3 vertical = Vector3.up * turnVerticalImpulse * mod;
		player.Rb.AddForce(lateral + vertical, ForceMode.Impulse);

		// Angular: Y-axis torque, signed by input direction. Rigidbody's
		// angularDrag setting decays the angular velocity over time -- that's
		// the natural-feeling settle. We apply this as ForceMode.Impulse on
		// AddTorque so the units match AddForce above (instantaneous kick,
		// not continuous force).
		Vector3 torque = Vector3.up * direction * turnAngularImpulse * mod;
		player.Rb.AddTorque(torque, ForceMode.Impulse);

		if (AudioManager.Instance != null && turnHopClip != null)
		{
			AudioManager.Instance.PlaySFX(turnHopClip, 1f, Random.Range(0.95f, 1.05f));
		}
	}

	private void ForwardHop(PlayerController player)
	{
		float mod = GetMovementModifier();
		Vector3 hopDirection = player.transform.forward + Vector3.up;
		player.Rb.AddForce(hopDirection * hopForce * mod, ForceMode.Impulse);
	}

	public override float GetKickModifier()
	{
		// AnkledToChair: legs are furniture. Kick fully suppressed regardless
		// of what else is going on with the ankles/knees. Default L1-L3 state.
		if ((BoundLimbs & BoundLimbs.AnkledToChair) != 0)
		{
			return 0f;
		}

		// Ankles + Knees: legs bound together AND hip leverage killed.
		// Even the mermaid-kick fails — no way to drive the kinetic chain.
		// Reserved for L6 failure-loop escalation.
		if ((BoundLimbs & BoundLimbs.Ankles) != 0 && (BoundLimbs & BoundLimbs.Knees) != 0)
		{
			return 0f;
		}

		// Ankles alone: legs bound together but free of the chair. Mermaid-
		// kick — reduced force from the single-unit constraint, but real.
		// This is the post-chair-break, pre-Ankles-cut state, and the
		// "kick the wall to tip" state if she clears AnkledToChair while
		// still in chair.
		if ((BoundLimbs & BoundLimbs.Ankles) != 0)
		{
			return 0.4f;
		}

		// Free legs.
		return 1f;
	}

	public override List<ControlHint> GetControlHints()
	{
		// Drive the disabled-kick hint off the actual modifier rather than
		// a specific flag, so the hint stays correct as the kick logic
		// evolves (AnkledToChair = 0, Ankles+Knees = 0, mermaid-kick = 0.4,
		// free = 1.0). Only the modifier=0 case reads as "can't kick" to
		// the player; mermaid-kick produces real force and shouldn't be
		// marked disabled.
		bool kickSuppressed = GetKickModifier() <= 0f;

		return new List<ControlHint>
	{
		new ControlHint("Hop", "W"),
		new ControlHint("Turn", "A / D"),
		new ControlHint("Struggle", "Space"),
		new ControlHint("Kick", "F", kickSuppressed, kickSuppressed ? "(legs tied)" : null),
		new ControlHint("Pick Up", "E"),
	};
	}

	public override void OnExit(PlayerController player)
	{
		// Nothing to clean up -- no coroutines, no persistent state. The
		// Rigidbody's angular velocity will decay naturally; if a level
		// transition needs an instant reset, that's the new restraint's
		// OnEnter responsibility.
	}

	public override float GetMovementModifier()
	{
		// Elbow binding tightens the shoulders to the torso, reducing
		// the leverage available to drive turn-hops and forward hops.
		// Wrists alone (default) = no degradation, since chair mechanics
		// don't recruit the wrists for hopping anyway.
		if ((BoundLimbs & BoundLimbs.Elbows) != 0)
		{
			return 0.65f;
		}
		return 1f;
	}

	public override float GetStruggleModifier()
	{
		// Elbow binding = more rope, tighter posture, less range of
		// motion to work the wrist bond. Struggle gets meaningfully
		// harder, not easier, despite there being "more to fray."
		// This is a player-experience call: tighter reads as worse.
		if ((BoundLimbs & BoundLimbs.Elbows) != 0)
		{
			return 0.5f;
		}
		return 1f;
	}

	[ContextMenu("Debug: Add Elbow Bond")]
	private void DebugAddElbowBond()
	{
		AddBondState(BoundLimbs.Elbows);
		Debug.Log($"[ChairRestraint] Elbow bond added. BoundLimbs = {BoundLimbs}");
	}

	[ContextMenu("Debug: Remove Elbow Bond")]
	private void DebugRemoveElbowBond()
	{
		RemoveBondState(BoundLimbs.Elbows);
		Debug.Log($"[ChairRestraint] Elbow bond removed. BoundLimbs = {BoundLimbs}");
	}

	[ContextMenu("Debug: Break Chair Anchor (clear AnkledToChair, keep Ankles)")]
	private void DebugBreakChairAnchor()
	{
		RemoveBondState(BoundLimbs.AnkledToChair);
		Debug.Log($"[ChairRestraint] Chair anchor cleared (mermaid-kick state). BoundLimbs = {BoundLimbs}");
	}

	[ContextMenu("Debug: Free Legs Fully (clear Ankles + AnkledToChair)")]
	private void DebugFreeLegs()
	{
		RemoveBondState(BoundLimbs.Ankles | BoundLimbs.AnkledToChair);
		Debug.Log($"[ChairRestraint] Legs fully freed. BoundLimbs = {BoundLimbs}");
	}

	[ContextMenu("Debug: Restore Chair Canon (add Ankles + AnkledToChair)")]
	private void DebugRestoreChairCanon()
	{
		AddBondState(BoundLimbs.Ankles | BoundLimbs.AnkledToChair);
		Debug.Log($"[ChairRestraint] Chair canon restored. BoundLimbs = {BoundLimbs}");
	}
}
