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
/// BoundLimbs = Wrists | Ankles | Knees — Cassie's wrists are tied
/// behind the chair back and her legs are tied to the chair legs.
/// This is canon for L1-L3.
///
/// Bond effects:
///   - Elbows: GetMovementModifier 0.65, GetStruggleModifier 0.5
///     (L6 failure-loop adds this to model tighter re-binding)
///   - Ankles: GetKickModifier 0 (legs anchored — no kick)
///     L8 finale will clear Ankles for the gloat-kick payoff
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
		// Chair restraint canonically binds the legs to the chair legs,
		// which is why kick is suppressed in L1-L3. If a level instance
		// has Ankles cleared (e.g. L8 finale with Feign + free legs),
		// kick becomes available at full force.
		if ((BoundLimbs & BoundLimbs.Ankles) != 0)
		{
			return 0f;
		}
		return 1f;
	}

	public override List<ControlHint> GetControlHints()
	{
		bool ankleBound = (BoundLimbs & BoundLimbs.Ankles) != 0;

		return new List<ControlHint>
	{
		new ControlHint("Hop", "W"),
		new ControlHint("Turn", "A / D"),
		new ControlHint("Struggle", "Space"),
		new ControlHint("Kick", "F", ankleBound, ankleBound ? "(legs tied)" : null),
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

	[ContextMenu("Debug: Free Legs (clear Ankles + Knees)")]
	private void DebugFreeLegs()
	{
		RemoveBondState(BoundLimbs.Ankles | BoundLimbs.Knees);
		Debug.Log($"[ChairRestraint] Legs freed. BoundLimbs = {BoundLimbs}");
	}

	[ContextMenu("Debug: Bind Legs (add Ankles + Knees)")]
	private void DebugBindLegs()
	{
		AddBondState(BoundLimbs.Ankles | BoundLimbs.Knees);
		Debug.Log($"[ChairRestraint] Legs bound. BoundLimbs = {BoundLimbs}");
	}
}
