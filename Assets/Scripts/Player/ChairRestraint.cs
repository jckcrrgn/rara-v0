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
/// This relies on the player Rigidbody having FreezeRotation X set (or
/// equivalent constraint). Y rotation is needed for turn-hop torque, and
/// Z rotation is needed for the rocking verb (Shift+A/D) to tip the chair.
/// Only X rotation should be frozen — without that, torque could tumble
/// the chair forward/backward in ways that don't model a bound person on
/// a chair. If you start to see chair-wobble in playtest (especially
/// pitching forward/backward), that's the symptom — check the Rigidbody
/// Constraints in the inspector and confirm only X is frozen.
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
/// ROCKING DESIGN (Shift+A/D)
/// --------------------------
/// Rocking is a SECOND lateral verb, deliberately distinct from turn-hop:
///   - PURE lateral impulse, NO vertical. Chair stays on the floor and
///     pivots on its edge. Differentiates audibly and visually from a hop.
///   - PURE angular impulse around Z-axis (body-relative forward). Rotates
///     the chair toward the side pressed — that's what "rocking" is:
///     leaning the chair toward one set of legs.
///   - No A/D direction-of-input drift on the heading: rocking is a
///     side-to-side commitment, not a steering tool.
///
/// Rocking is GATED via the rockingEnabled flag, default OFF. Per the
/// v0 design (Day 37), chair-tipping is L6's canonical debut mechanic —
/// it does not appear on L1-L3. The gate also keeps L1-L3 from
/// softlocking on a tip the level has no recovery path for (the
/// stand-up verb is parked for L7 per ideas.md). When rockingEnabled
/// is false, Shift+A/D falls through to the regular turn-hop (same as
/// bare A/D) and the "Rock" hint is omitted from ControlHints.
///
/// Amplitude is RHYTHM-BASED, not magnitude-based. Each rock adds to the
/// Rigidbody's existing angularVelocity rather than overwriting it. If the
/// player times Shift+A → Shift+D → Shift+A in rhythm with the chair's
/// natural rock-back cadence, the angular velocity accumulates and the
/// chair tips further on each successive rock. Off-rhythm input fights
/// the existing velocity and damps the amplitude. The Rigidbody's
/// angularDrag is what creates the natural cadence — high drag = fast
/// settle, requires fast rhythm; low drag = slow settle, more forgiving.
///
/// Tip detection: a side-marker child GameObject on the chair has a
/// ChairTipMarker component. When that marker collides with the ground
/// (or any tip-surface layer), it calls back into ChairRestraint, which
/// triggers the chair-break sequence: zero velocity, hand off bonds to
/// FloorRestraint, SetRestraint to the floor instance.
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
///   - Ankles without AnkledToChair: GetKickModifier 0.7 (mermaid-kick;
///     reduced force, used to topple the L6 lamp off the nightstand.
///     Was 0.4 through Day 43; bumped Day 44 — 0.4 was too weak to
///     reliably clear the topple threshold)
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

	[Header("Rocking (Shift + A / D)")]
	[Tooltip("Master gate for the rocking verb. Default OFF — rocking is the " +
		"canonical L6 debut mechanic, and tipping has no recovery path in v0 " +
		"(no stand-up verb yet, parked for L7). On levels where the chair " +
		"shouldn't break (L1-L3), leave this false: Shift+A/D becomes a no-op " +
		"and the verb doesn't appear in ControlHints. Flip true on L6's " +
		"ChairRestraint instance, where floorRestraintOnBreak is also wired. " +
		"Disabling here also keeps L1-L3 from softlocking on a tip the level " +
		"has no solve path for.")]
	[SerializeField] private bool rockingEnabled = false;

	[Tooltip("Lateral impulse component, body-relative. Pure horizontal — NO " +
		"vertical lift, which is what differentiates rocking from turn-hop. " +
		"The chair pivots on its edge rather than hopping off the floor.")]
	[SerializeField] private float rockLateralImpulse = 1.5f;

	[Tooltip("Angular impulse around the body-relative FORWARD axis (Z), " +
		"scaled by direction. This is what tilts the chair side-to-side — " +
		"leaning toward the set of legs on the input side. Accumulates with " +
		"existing angular velocity (rhythm-based amplitude), so timing the " +
		"rocks in sync with the chair's natural rock-back cadence builds " +
		"toward a tip. Off-rhythm input damps the amplitude.")]
	[SerializeField] private float rockAngularImpulse = 0.6f;

	[Tooltip("Plays on each rock tap. Wooden creak under load, chair-legs " +
		"scuffing the floor. Optional — safe to leave empty.")]
	[SerializeField] private AudioClip rockClip;

	[Header("Chair Tip Detection")]
	[Tooltip("Reference to the FloorRestraint component pre-placed on the " +
		"player GameObject. On chair-break, this component's BoundLimbs is " +
		"configured with the carried bond state (current BoundLimbs minus " +
		"AnkledToChair) and SetRestraint hands control over. Inspector " +
		"tuning on the FloorRestraint instance is preserved. Leave empty " +
		"for levels where the chair shouldn't break (L1-L3 default). " +
		"Should be wired together with rockingEnabled=true (L6); leaving " +
		"one set and not the other is a misconfiguration. " +
		"The actual collision detection lives on ChairTipMarker components " +
		"on child GameObjects of the chair; they call back into this " +
		"restraint via OnSideMarkerHitGround.")]
	[SerializeField] private FloorRestraint floorRestraintOnBreak;

	[Tooltip("Plays once on chair-break. Wood snapping, rope-creak-release. " +
		"Optional but recommended — this is a big narrative beat.")]
	[SerializeField] private AudioClip chairBreakClip;

	[Header("SFX (optional)")]
	[Tooltip("Plays on each turn-hop tap. Wooden creak, chair scuff, floor " +
		"thud. Optional -- safe to leave empty until SFX wiring pass.")]
	[SerializeField] private AudioClip turnHopClip;

	// Rotation is impulse-fired and grounded-gated, but doesn't have an
	// extended busy window -- you tap, the impulse fires, you can tap again.
	// IsBusy stays false: no other body verb needs to know about an in-flight
	// turn-hop because there's nothing to coordinate with.
	public override bool IsBusy => false;

	// Set true once the tip event has fired and the handoff has begun. Prevents
	// double-fire from a second side-marker collision in the same frame (the
	// chair can land on a marker on either side; we only want to break once).
	private bool isBroken = false;

	public override void HandleMovementInput(PlayerController player)
	{
		if (isBroken) return;

		bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

		// Rocking takes priority over turn-hop on Shift+A/D. Check it first so
		// Shift+A doesn't also trigger a turn-hop in the same frame.
		// Gated on rockingEnabled — when disabled (L1-L3 default), Shift+A/D
		// falls through to the turn-hop block below, same as a bare A/D press.
		if (shiftHeld && rockingEnabled)
		{
			if (Input.GetKeyDown(KeyCode.A) && player.IsGrounded)
			{
				ApplyRock(player, -1f);
				return;
			}
			if (Input.GetKeyDown(KeyCode.D) && player.IsGrounded)
			{
				ApplyRock(player, +1f);
				return;
			}
		}

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

	/// <summary>
	/// Apply a single rocking impulse. Pure lateral + pure Z-axis angular,
	/// no vertical lift. The chair stays on the floor and pivots toward the
	/// side pressed. Angular impulse ADDS to existing angularVelocity rather
	/// than overwriting it — that's the rhythm-based amplitude mechanic. Time
	/// rocks with the chair's natural rock-back cadence (governed by the
	/// Rigidbody's angularDrag), and amplitude builds toward a tip.
	///
	/// Z-axis as the rock axis: in player-local space, +Z is forward and the
	/// chair "leans toward the legs on the input side" is a rotation around
	/// that axis. Using transform.forward keeps the rock direction body-relative,
	/// so a rotated chair still rocks in a way that feels right (left-shoulder
	/// dip on Shift+A regardless of which direction the chair is facing).
	/// </summary>
	private void ApplyRock(PlayerController player, float direction)
	{
		float mod = GetMovementModifier();

		// Pure lateral, no vertical. Body-relative right * direction so Shift+A
		// pushes toward her left, Shift+D toward her right.
		Vector3 lateral = player.transform.right * direction * rockLateralImpulse * mod;
		player.Rb.AddForce(lateral, ForceMode.Impulse);

		// Angular: rotate around body-relative forward (Z), signed by direction.
		// AddTorque with ForceMode.Impulse ADDS to existing angular velocity, which
		// is what gives rocking its rhythm-based amplitude. We do NOT clamp or
		// overwrite — natural angular drag handles decay.
		Vector3 torque = player.transform.forward * -direction * rockAngularImpulse * mod;
		player.Rb.AddTorque(torque, ForceMode.Impulse);

		if (AudioManager.Instance != null && rockClip != null)
		{
			AudioManager.Instance.PlaySFX(rockClip, 1f, Random.Range(0.92f, 1.05f));
		}
	}

	/// <summary>
	/// Called by a ChairTipMarker child when it collides with the ground. This
	/// is the trigger that fires the chair-break sequence. Idempotent — second
	/// call (from the other side-marker landing in the same frame) is a no-op.
	///
	/// Sequence:
	///   1. Mark broken so HandleMovementInput stops accepting input.
	///   2. Zero linear and angular velocity so the player doesn't inherit
	///      the chair's tumbling momentum.
	///   3. Capture current yaw — FloorRestraint.OnEnter reads
	///      player.transform.eulerAngles.y, so the captured heading transfers
	///      automatically. We don't need to do anything special.
	///   4. Compute carried bonds: current BoundLimbs with AnkledToChair
	///      cleared. Everything else her body has on it (Wrists, Ankles,
	///      Elbows from a failure escalation, Knees from a future hogtie
	///      escalation) is preserved. This is the fix from today's design
	///      review — see BoundLimbs.cs invariant comment.
	///   5. Push the carried bonds into the pre-placed FloorRestraint
	///      instance, then SetRestraint to hand control over.
	/// </summary>
	public void OnSideMarkerHitGround(PlayerController player)
	{
		if (isBroken) return;

		// Defense in depth: rockingEnabled is the master gate for the entire
		// tip-and-break feature. The input path is already gated in
		// HandleMovementInput; gating the collision path here too means a
		// misconfigured scene (floorRestraintOnBreak wired but rockingEnabled
		// left false) can't accidentally break the chair via collision. The
		// LogWarning is loud on purpose: this branch firing means the scene
		// is misconfigured in a way the bug will eventually be blamed on
		// something else (see Day 37 NailProximityTrigger bug for the
		// motivating case).
		if (!rockingEnabled)
		{
			Debug.LogWarning("[ChairRestraint] Side marker hit ground but " +
				"rockingEnabled is false. Ignoring. If this fires, the scene " +
				"likely has floorRestraintOnBreak wired on a level that " +
				"shouldn't tip — clear that override or flip rockingEnabled.");
			return;
		}

		if (floorRestraintOnBreak == null)
		{
			Debug.LogWarning("[ChairRestraint] Side marker hit ground but no " +
				"floorRestraintOnBreak is configured. Tip-and-break disabled " +
				"on this restraint instance.");
			return;
		}

		isBroken = true;

		// Soft timer trigger (§6). Chair-tip crash is one of the two events that
		// can start the L6 timer; LampSmashTrigger is the other. StartTimer is
		// idempotent — if the lamp already smashed, this is a silent no-op (first-
		// occurrence-wins). Null-check guards L1–L3 where no LevelTimer exists.
		if (LevelTimer.Instance != null)
		{
			LevelTimer.Instance.StartTimer();
		}

		// Zero velocity so the floor-restraint starts from rest. The chair was
		// mid-tumble; we don't want that momentum carrying into the inch crawl.
		player.Rb.linearVelocity = Vector3.zero;
		player.Rb.angularVelocity = Vector3.zero;

		// Bond handoff: drop AnkledToChair, preserve everything else.
		BoundLimbs carriedBonds = this.BoundLimbs & ~BoundLimbs.AnkledToChair;
		floorRestraintOnBreak.SetBoundLimbs(carriedBonds);

		if (AudioManager.Instance != null && chairBreakClip != null)
		{
			AudioManager.Instance.PlaySFX(chairBreakClip, 1f, 1f);
		}

		Debug.Log($"[ChairRestraint] Chair broke. Handing off to FloorRestraint " +
			$"with BoundLimbs = {carriedBonds}");

		player.SetRestraint(floorRestraintOnBreak);
	}

	[ContextMenu("Debug: Force Tip (test handoff)")]
	private void DebugForceTip()
	{
		PlayerController player = GetComponent<PlayerController>();
		if (player == null)
		{
			Debug.LogWarning("[ChairRestraint] Debug force-tip needs a " +
				"PlayerController on the same GameObject.");
			return;
		}
		OnSideMarkerHitGround(player);
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
		// Day 44: 0.4 → 0.7. At 0.4 mermaid-kick was too weak to reliably
		// topple the L6 lamp off the nightstand, which is the canonical
		// solve path (lamp smash → shard tool + start guard timer). The
		// gap between free-kick (1.0) and mermaid-kick (0.7) is now ~1.4×
		// rather than 2.5×; verify in scaffold A/B that the two states
		// still read as distinct.
		if ((BoundLimbs & BoundLimbs.Ankles) != 0)
		{
			return 0.7f;
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

		List<ControlHint> hints = new List<ControlHint>
	{
		new ControlHint("Hop", "W"),
		new ControlHint("Turn", "A / D"),
	};

		// Rock is L6-debut; omit the hint entirely on levels where rocking
		// is gated off. Advertising a verb that does nothing is exactly the
		// Day 30 legibility failure pattern.
		if (rockingEnabled)
		{
			hints.Add(new ControlHint("Rock", "Shift + A / D"));
		}

		hints.Add(new ControlHint("Struggle", "Space"));
		hints.Add(new ControlHint("Kick", "F", kickSuppressed, kickSuppressed ? "(legs tied)" : null));
		hints.Add(new ControlHint("Pick Up", "E"));

		return hints;
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
