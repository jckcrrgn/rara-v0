using UnityEngine;

/// <summary>
/// Strike layer (spec §13, added Day 78; arms reworked Day 78 after first viewport test).
/// The payoff beat: torso uncoils, the concealed bottle swings around, the guard
/// goes down. One-shot and terminal — unlike Struggle there is no repeat, no mash.
///
/// THE TIMELINE
/// ------------
/// The whole swing is driven by a single signed scalar `_s`:
///
///     0  ──windup──►  -1  ──swing──►  +1  ──settle──►  0
///   (Sit pose)     (coiled back)  (follow-through)  (Sit pose)
///
/// _s = 0 IS the authored seated pose, so entry and exit are seamless with no
/// special-casing, and _s is continuous across every segment boundary so there
/// is nothing to pop — the principle the Struggle envelope landed on.
///
/// WHY THE ARMS USE POSE TARGETS, NOT AN AXIS (revised Day 78)
/// -----------------------------------------------------------
/// The first version rotated each arm bone about a single configurable local
/// axis by a single amplitude. That failed in the viewport: her wrists rolled
/// but her hands never left the small of her back. Two reasons, both fatal.
///
/// First, an axis-angle rotation about a bone-local axis that runs along the
/// BONE'S OWN LENGTH is a roll — the limb spins in place and the end effector
/// doesn't travel. With a hand-built armature there was no way to guess the
/// right perpendicular from outside the file.
///
/// Second, and more fundamental: her rest pose has her hands bound behind her
/// back. Getting them around to the front of her body is not one rotation on
/// one axis — it's shoulder flexion AND abduction AND the elbow opening, well
/// over 120 degrees of travel. A single scalar amplitude can't express that
/// path no matter which axis you pick.
///
/// So each arm bone now gets two explicit local-Euler POSE TARGETS — one at
/// full coil, one at full follow-through — and the driver lerps between them
/// through rest. Both ends tune independently, which matters because they
/// aren't symmetric: coiling back from a bound position is a small move, and
/// swinging around the front is a huge one.
///
/// TUNING (use the scrub slider)
/// -----------------------------
/// Enter Play Mode, tick `debugScrubEnabled`, and drag `debugScrub` from -1 to
/// +2 — the -1..+1 stretch is the swing, and +1..+2 is the settle into the
/// post-strike pose. The pose follows the slider live and the strike never fires,
/// so you can dial the Eulers while watching her instead of re-triggering the
/// beat. Work one axis of one bone at a time: get the SWING arm's strike pose
/// reading first (hands in front of her, bottle toward the guard), then its coil
/// pose, then the forearm, then the off arm. The post-strike pose is authored
/// last, parked at +2, in the same order — upper arm, then forearm. Untick when
/// you're done.
///
/// If the off arm swings the wrong way, flip `mirrorOffArm` — whether the same
/// local Euler mirrors or duplicates across the body depends on the bone rolls
/// in the symmetrized armature, which is not knowable from here.
///
/// CONTACT FRAME (spec §13 — the fix for the Day 59 "finicky" instant-strike)
/// -------------------------------------------------------------------------
/// The KO does NOT fire on the input frame. `onContact` fires the frame the
/// FOREARM scalar `sLag` crosses `contactAt` going up — when the bottle is
/// visually at the guard. Keyed to a pose scalar rather than elapsed time so
/// retuning the easing or the durations can't desync the hit from the pose.
///
/// It is keyed to `sLag`, not `_s` (changed Day 80). The bottle rides the hand,
/// which hangs off the forearm, so the forearm is the last link that decides
/// where the bottle actually is. Under the old `_s` keying the whip made the hit
/// unlandable: at forearmLag 0.35 on a 0.24s swing the forearm is still at
/// sLag = -0.16 when `_s` saturates at +1, and `_s` then pins at +1 through the
/// hold, so no value of contactAt could reach the bottle's actual arrival.
///
/// PLACEMENT
/// ---------
/// On Cassie_Blockout alongside CassieRig, CassieSitDriver, CassieStruggleDriver.
/// Order = 2 (above Sit's 0 and Struggle's 1) so the swing composes last.
/// </summary>
public class CassieStrikeDriver : CassieRigLayer
{
	[Header("Timing")]
	[Tooltip("Seconds to coil back before the swing. A spring loading, not a " +
		"telegraph. 0.15-0.3.")]
	[SerializeField] private float windupDuration = 0.22f;

	[Tooltip("Seconds for the swing itself, coil through to full follow-through. " +
		"The fast part. 0.18-0.3 — faster and the contact frame is hard to read, " +
		"slower and it stops feeling like a strike.")]
	[SerializeField] private float swingDuration = 0.24f;

	[Tooltip("Extra seconds holding full follow-through before the settle begins. " +
		"The held beat where the hit reads. 0.1-0.25 to punctuate it.")]
	[SerializeField] private float followThroughHold = 0.15f;

	[Tooltip("Seconds to settle from full follow-through back toward the seated " +
		"pose. Slow — she's spent, and the guard is going down during this. 0.6-1.2.")]
	[SerializeField] private float settleDuration = 0.9f;

	[Header("Contact")]
	[Tooltip("Value of the FOREARM swing scalar at which the blow lands and onContact " +
		"fires — not the torso scalar. Runs -1 (coiled) to +1 (elbow fully open, bottle " +
		"extended). The forearm trails the upper arm by forearmLag, and keeps climbing " +
		"into the follow-through hold, so values near +1 land the hit inside the held " +
		"beat. Scrub to where the bottle reaches him and use that number.")]
	[Range(-1f, 1f)]
	[SerializeField] private float contactAt = 0.6f;

	[Header("Swing side")]
	[Tooltip("Which hand carries the bottle. Check which hand handAnchor is actually " +
		"parented to before tuning anything else.")]
	[SerializeField] private bool swingWithRightHand = true;

	[Tooltip("Whether the off arm mirrors the swing arm's Euler (negating Y and Z) " +
		"or copies it outright. Depends on the bone rolls in the symmetrized " +
		"armature — if the off arm swings backward while the other swings forward, " +
		"flip this.")]
	[SerializeField] private bool mirrorOffArm = true;

	[Header("Torso (the uncoil)")]
	[Tooltip("Peak torso yaw, in degrees. She rotates AWAY by this much at full coil " +
		"and THROUGH by this much at follow-through. The biggest contributor to the " +
		"swing reading as whole-body rather than arm-only. 25-45.")]
	[SerializeField] private float torsoYaw = 35f;

	[Tooltip("Peak torso pitch, in degrees. Negative at coil (sits back and away), " +
		"positive at follow-through (drives forward over her knees). Legs are still " +
		"bound, so keep it modest — she can't stand into it. 8-18.")]
	[SerializeField] private float torsoPitch = 12f;

	[Tooltip("Fraction of the spine's rotation the chest adds on top. Above 1 the " +
		"upper body leads the hips, which is what an uncoil looks like. 1.0-1.5.")]
	[SerializeField] private float chestFollow = 1.25f;

	[Header("Head")]
	[Tooltip("Peak head yaw, in degrees. She turns INTO him through the swing — " +
		"opposite in feel to Struggle, where the head turns away to search. 15-30.")]
	[SerializeField] private float headYaw = 22f;

	/// AUTHORED VALUES LIVE IN THE SCENE, NOT HERE (recorded Day 117, 2026-08-03)
	/// -------------------------------------------------------------------------
	/// Every field initializer below is a PLACEHOLDER. The real, hand-tuned poses
	/// were authored via the scrub slider and serialized into VS_Turnaround.unity and VS_ShaderCheck.unity.
	/// They are far from these defaults — upperArmStrikeEuler is (-105.8, 138.6,
	/// 14.7) in the scene against (-30, 130, 20) here. The defaults are plausible
	/// enough that a silent revert to them may not be obvious in the viewport.
	///
	/// This means the authored strike exists in exactly 3 places: this block, VS_Turnaround.unity, and VS_ShaderCheck.unity. It is destroyed
	/// by: Revert All on the prefab, removing and re-adding this component, or a
	/// bad merge on the scene file. NEVER PRESS REVERT ALL.
	///
	/// Verified against the scene, Day 117:
	///
	///     upperArmCoilEuler        (-10,    -15,     0    )
	///     upperArmStrikeEuler      (-105.8,  138.6,  14.7 )
	///     forearmCoilEuler         (  0,    -10,     0    )
	///     forearmStrikeEuler       (  0.65, -41.6,  52.95 )
	///     postStrikeUpperArmEuler  (-46.3,  156.9,  25.57 )
	///     postStrikeForearmEuler   ( -2.41, -31.8, 102.5  )
	///     contactAt                (  0.8               )
	///
	/// contactAt added Day 143. It is not an Euler, which is why the Day 117 audit
	/// missed it — but it is exactly as scene-only and exactly as destroyable. The
	/// initializer above is 0.6; a silent revert would move the hit earlier in the
	/// swing without looking broken. Verified 0.8 in both VS_Turnaround.unity and
	/// VS_ShaderCheck.unity, Day 138 and Day 143.
	///
	/// mirrorOffArm = true. Correct ONLY because the armature's L/R rolls mirror
	/// exactly (Shoulder ±102.26, UpperArm ±146.81, LowerArm ±146.70, Hand
	/// ±147.35). The rolls are a project invariant — changing one silently
	/// invalidates every Euler above.
	///
	/// DO NOT RETUNE THESE. The wrist drift chased through Day 115 was never the
	/// Eulers — it was a 12.8% forearm scale asymmetry (LowerArm.L 0.356 vs R
	/// 0.316), fixed Day 116. Both forearms now symmetric to seven decimals and
	/// the authored poses read correctly untouched. If wrists drift again, check
	/// the scale chain FIRST.

	[Header("Swing arm — pose targets (local Euler, degrees)")]
	[Tooltip("UPPER ARM offset at full coil (s = -1). Small: she draws the bound " +
		"hands tighter and turns the shoulder back. Tune with the scrub slider.")]
	[SerializeField] private Vector3 upperArmCoilEuler = new Vector3(-10f, -15f, 0f);

	[Tooltip("UPPER ARM offset at full follow-through (s = +1). LARGE — this is the " +
		"rotation that carries her hand from behind her back to in front of her body. " +
		"Expect something over 100 degrees on at least one axis. This is the single " +
		"most important field in the component; tune it first.")]
	[SerializeField] private Vector3 upperArmStrikeEuler = new Vector3(-30f, 130f, 20f);

	[Tooltip("FOREARM offset at full coil. The elbow stays folded — she's still " +
		"hiding the bottle behind her at this point.")]
	[SerializeField] private Vector3 forearmCoilEuler = new Vector3(0f, -10f, 0f);

	[Tooltip("FOREARM offset at full follow-through. The elbow OPENS through the " +
		"swing, extending the bottle toward him — this is where the reach comes from. " +
		"Tune after the upper arm.")]
	[SerializeField] private Vector3 forearmStrikeEuler = new Vector3(0f, 60f, 0f);

	[Tooltip("Forearm lag, 0-1. How far the forearm trails the upper arm through the " +
		"swing. 0 = rigid arm (reads stiff), 0.3-0.5 = whip. Above 0.6 the elbow " +
		"looks broken.")]
	[Range(0f, 1f)]
	[SerializeField] private float forearmLag = 0.35f;

	[Header("Off arm")]
	[Tooltip("Fraction of the swing arm's motion the OFF arm follows. Her wrists just " +
		"came free — both hands come around the SAME way, the bottle hand just leads. " +
		"0.3-0.6 reads as a real body; 0 reads as a mannequin with one working arm.")]
	[Range(0f, 1f)]
	[SerializeField] private float offArmFollow = 0.45f;

	[Tooltip("Fraction of the POST-STRIKE pose the off arm adopts. Separate from " +
		"offArmFollow because they mean different things: offArmFollow is a " +
		"motion-follow fraction (the bottle hand leads, the off arm trails), which " +
		"is right for the swing. The post-strike pose is a DESTINATION, not a " +
		"motion — both wrists just came free, both hands end up down and forward. " +
		"At offArmFollow's 0.45 the off arm settles 45% along an arc that STARTS " +
		"behind her back, i.e. still partly behind her back — and the smaller the " +
		"bone's authored delta the worse it reads, which is why the off forearm " +
		"looked anatomically impossible while the off upper arm looked fine. Keep " +
		"slightly under 1 so the rest isn't mirror-symmetric — she did just swing " +
		"with the other one. 0.8-1.0.")]
	[Range(0f, 1f)]
	[SerializeField] private float postStrikeOffArmFollow = 0.9f;

	[Header("After the strike")]
	[Tooltip("UPPER ARM offset she settles INTO after the blow lands. Not a fraction " +
		"of the strike pose — an authored pose in its own right. That's the whole " +
		"point: the strike pose is the far end of an arc that STARTS behind her " +
		"back, so any fraction of it is a point partway back there, which is why " +
		"no value of the old postStrikeArmHold read as 'spent'. Keep most of the " +
		"strike's yaw (hands stay in front) and add the drop. Scrub past +1 to author.")]
	[SerializeField] private Vector3 postStrikeUpperArmEuler = new Vector3(15f, 95f, 5f);

	[Tooltip("FOREARM offset she settles INTO after the blow lands. The elbow re-folds " +
		"part way as the arm comes down — full extension is the follow-through, not " +
		"the rest. Author after the upper arm, same order as the strike pose.")]
	[SerializeField] private Vector3 postStrikeForearmEuler = new Vector3(0f, 30f, 0f);

	[Header("Debug")]
	[Tooltip("Overrides the timeline with the scrub slider below so you can tune " +
		"poses live in Play Mode. The strike will NOT fire while this is on. " +
		"Remember to untick.")]
	[SerializeField] private bool debugScrubEnabled = false;

	[Tooltip("Manual swing scalar. -1 = full coil, 0 = seated rest, +1 = full " +
		"follow-through, +2 = the authored post-strike pose. The 1→2 stretch is the " +
		"settle blend — drag right past 1 to author where she comes to rest. Only " +
		"active while Debug Scrub Enabled is ticked.")]
	[Range(-1f, 2f)]
	[SerializeField] private float debugScrub = 0f;

	[SerializeField] private bool verboseLogging = true;

	// --- Runtime state -------------------------------------------------------

	private bool _playing;
	private float _t;                 // seconds elapsed since Play()
	private float _s;                 // signed swing scalar, -1..+1
	private float _sLagPrev;          // last frame's sLag, for the contact crossing test
	private bool _contactFired;
	private bool _holdingArms;        // true after a completed strike, holds the post-strike pose

	private System.Action _onContact;
	private System.Action _onComplete;

	/// <summary>
	/// Multicast contact-frame event for PRESENTATION consumers (bottle smash, SFX,
	/// camera shake). Distinct from the `onContact` callback passed to Play(), which
	/// is the single gameplay consumer PlayerController owns and which is nulled
	/// after firing. Fires at most once per strike — same _contactFired guard — and
	/// also from OnDisable's soft-lock path. Subscribers unsubscribe in OnDisable;
	/// this layer outlives them. Per spec §13 nothing subscribed here touches
	/// gameplay state.
	/// </summary>
	public event System.Action OnContact;

	/// <summary>True while the swing is running. PlayerController folds this into IsBusy.</summary>
	public bool IsPlaying => _playing;

	private float TotalDuration =>
		Mathf.Max(0.01f, windupDuration) +
		Mathf.Max(0.01f, swingDuration) +
		Mathf.Max(0f, followThroughHold) +
		Mathf.Max(0.01f, settleDuration) +
		forearmLag * Mathf.Max(0.01f, swingDuration);   // let the lagged clock finish

	protected override void DeclareBones()
	{
		Declare(HumanBodyBones.Spine);
		Declare(HumanBodyBones.Chest);            // optional — skipped if absent
		Declare(HumanBodyBones.Head);
		Declare(HumanBodyBones.LeftUpperArm);
		Declare(HumanBodyBones.LeftLowerArm);
		Declare(HumanBodyBones.RightUpperArm);
		Declare(HumanBodyBones.RightLowerArm);
	}

	/// <summary>
	/// Fire the strike. Called by PlayerController.TryStrike() once every gate has
	/// passed — this layer does NOT re-check anything.
	///
	/// onContact fires mid-swing, when the bottle reaches him (see contactAt).
	/// onComplete fires when the settle finishes. Both optional.
	///
	/// Re-entrant calls are ignored: the strike is terminal, and StrikeableGuard's
	/// hasBeenStruck already blocks a second hit downstream.
	/// </summary>
	public void Play(System.Action onContact = null, System.Action onComplete = null)
	{
		if (_playing)
		{
			Log("Play() called while already swinging — ignored.");
			return;
		}

		if (debugScrubEnabled)
		{
			Log("Play() called while Debug Scrub is enabled — the pose is following " +
				"the slider. Untick Debug Scrub Enabled to play the strike for real.");
		}

		_playing = true;
		_t = 0f;
		_s = 0f;
		_sLagPrev = 0f;
		_contactFired = false;
		_onContact = onContact;
		_onComplete = onComplete;

		Log($"Swing started. Contact at s={contactAt:F2}, total {TotalDuration:F2}s.");
	}

	/// <summary>
	/// Safety net. If this component is disabled or destroyed mid-swing the contact
	/// callback would never fire and the guard would never go down — a soft-lock with
	/// no error, the worst failure shape for a terminal beat. Fire anything
	/// outstanding on the way out.
	/// </summary>
	protected override void OnDisable()
	{
		base.OnDisable();
		if (!_playing) return;

		Log("Disabled mid-swing — firing outstanding callbacks so the beat can't soft-lock.");
		FireContactOnce();
		Complete();
	}

	public override void Contribute(float dt)
	{
		// Tuning mode: pose follows the slider, nothing else runs.
		if (debugScrubEnabled)
		{
			// Past +1 the slider drives the settle: the torso comes home (1 → 0)
			// while the arms blend strike → post-strike. So scrub = 2 is the exact
			// frame the player is left looking at.
			float scrubS = debugScrub <= 1f ? debugScrub : Mathf.Lerp(1f, 0f, debugScrub - 1f);
			float scrubSettle = Mathf.Clamp01(debugScrub - 1f);
			ApplyTorso(scrubS);
			ApplyArms(scrubS, scrubS, scrubSettle, scrubSettle);
			return;
		}

		if (!_playing)
		{
			// Post-strike residual: torso is home, arms hold the authored post-strike
			// pose. settleP = 1 makes this bit-identical to the last settle frame,
			// which is what removes the pop. s is ignored in the settle branch.
			if (_holdingArms) ApplyArms(0f, 0f, 1f, 1f);
			return;
		}

		_t += dt;
		_s = EvaluateSwing(_t);

		// Forearm trails the upper arm — the whip. Sampling the swing scalar
		// slightly in the past is the cheapest honest way to lag a joint without
		// a second timeline. The settle reads the same shifted clock below.
		float tLag = Mathf.Max(0f, _t - forearmLag * Mathf.Max(0.01f, swingDuration));
		float sLag = EvaluateSwing(tLag);

		// Contact test: the frame the FOREARM scalar crosses the threshold going UP.
		//
		// Keyed to sLag, not _s (changed Day 80). The bottle rides the hand, which
		// hangs off the forearm, so the forearm's progress is what decides where the
		// bottle actually is. Keying to _s fired the KO with the elbow still folded:
		// at forearmLag 0.35 on a 0.24s swing the forearm is still at sLag = -0.16
		// when _s saturates at +1, so NO value of contactAt could land the hit on the
		// bottle's arrival — and _s pins at +1 through the hold, so it can't cross
		// anything later either. sLag keeps climbing into the follow-through hold
		// (it catches up forearmLag * swingDuration seconds in), which is what makes
		// contact-during-the-held-beat expressible at all.
		if (!_contactFired && _sLagPrev < contactAt && sLag >= contactAt)
		{
			Log($"CONTACT at t={_t:F3}s (sLag={sLag:F2}, s={_s:F2}).");
			FireContactOnce();
		}
		_sLagPrev = sLag;

		ApplyTorso(_s);

		// Once contact has landed her hands do not go back behind her back — the
		// freed hands are the reveal. The settle blends the arms from full
		// follow-through into the AUTHORED post-strike pose, not toward the bound
		// pose at a floor. The forearm settles on the lagged clock, so the upper
		// arm starts down while the hand is still out, and the hand lands last.
		//
		// Gated on _contactFired for the same reason the old floor was: a swing
		// that never connects should return her fully to the bound pose.
		float settleP = _contactFired ? EvaluateSettle(_t) : 0f;
		float settlePLag = _contactFired ? EvaluateSettle(tLag) : 0f;
		ApplyArms(_s, sLag, settleP, settlePLag);

		if (_t >= TotalDuration)
		{
			// Guarantee contact fired even in a pathological frame (a single dt
			// spanning the whole swing — an editor hitch or a breakpoint).
			FireContactOnce();
			Complete();
		}
	}

	/// <summary>
	/// Settle progress, 0 → 1 across the settle segment only, on the same EaseOut the
	/// torso settles with so arms and torso stay in phase. Returns 0 everywhere before
	/// the settle begins — which is what makes the handoff continuous: at settle start
	/// s is exactly +1, and Slerp(strike, post, 0) is the strike pose.
	/// </summary>
	private float EvaluateSettle(float t)
	{
		float settleStart = Mathf.Max(0.01f, windupDuration)
			+ Mathf.Max(0.01f, swingDuration)
			+ Mathf.Max(0f, followThroughHold);
		if (t <= settleStart) return 0f;
		return EaseOut((t - settleStart) / Mathf.Max(0.01f, settleDuration));
	}

	/// <summary>
	/// Map elapsed time to the signed swing scalar.
	///   windup : 0 → -1, decelerating (settles into the coil like a loading spring)
	///   swing  : -1 → +1, accelerating (peak speed at contact, near the top end)
	///   hold   : flat at +1
	///   settle : +1 → 0, decelerating (she comes down spent)
	/// </summary>
	private float EvaluateSwing(float t)
	{
		float wd = Mathf.Max(0.01f, windupDuration);
		float sd = Mathf.Max(0.01f, swingDuration);
		float hd = Mathf.Max(0f, followThroughHold);
		float td = Mathf.Max(0.01f, settleDuration);

		if (t < wd) return -EaseOut(t / wd);

		t -= wd;
		if (t < sd) return Mathf.Lerp(-1f, 1f, EaseIn(t / sd));

		t -= sd;
		if (t < hd) return 1f;

		t -= hd;
		if (t < td) return Mathf.Lerp(1f, 0f, EaseOut(t / td));

		return 0f;
	}

	/// <summary>
	/// Torso and head. Linear in s, so s = 0 contributes nothing — which is exactly
	/// the authored seated pose, meaning this layer is silent when idle.
	/// </summary>
	private void ApplyTorso(float s)
	{
		// Right-hand swing uncoils to her LEFT: the right shoulder travels forward and
		// across, so the torso yaws negative through follow-through. The old +1 pulled
		// that shoulder back while the arm swung front — the two fought.
		float side = swingWithRightHand ? -1f : 1f;
		float sw = s * side;

		float yaw = torsoYaw * sw;
		float pitch = torsoPitch * s;

		AddLocalEuler(HumanBodyBones.Spine, pitch, yaw, 0f);
		AddLocalEuler(HumanBodyBones.Chest, pitch * chestFollow, yaw * chestFollow, 0f);
		AddLocalEuler(HumanBodyBones.Head, 0f, headYaw * sw, 0f);
	}

	/// <summary>
	/// Arms, from explicit pose targets. Negative s blends toward the coil pose,
	/// positive s toward the strike pose, and s = 0 is rest — so the arm sweeps
	/// through the bound pose on its way around rather than snapping.
	///
	/// Once settleP goes positive it takes over entirely: the arm blends from full
	/// strike into the authored post-strike pose and s stops mattering. Upper arm
	/// and forearm carry their own settle scalars so the hand trails on the way down
	/// exactly as it trails on the way out.
	///
	/// The off arm carries TWO weights — offArmFollow through the swing,
	/// postStrikeOffArmFollow at the destination. See ArmQuat for why.
	///
	/// Both arms move the SAME direction (optionally mirrored across the body).
	/// They do NOT counter-rotate — that's the Struggle driver's wrist grind, and
	/// applying it here is what pulled her hands apart instead of bringing them
	/// around together.
	/// </summary>
	private void ApplyArms(float s, float sLag, float settleP, float settlePLag)
	{
		HumanBodyBones swingUpper = swingWithRightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm;
		HumanBodyBones swingLower = swingWithRightHand ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm;
		HumanBodyBones offUpper = swingWithRightHand ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
		HumanBodyBones offLower = swingWithRightHand ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;

		AddOffset(swingUpper, ArmQuat(upperArmCoilEuler, upperArmStrikeEuler,
			postStrikeUpperArmEuler, s, settleP, 1f, 1f));
		AddOffset(swingLower, ArmQuat(forearmCoilEuler, forearmStrikeEuler,
			postStrikeForearmEuler, sLag, settlePLag, 1f, 1f));

		if (offArmFollow > 0f)
		{
			AddOffset(offUpper, ArmQuat(OffArm(upperArmCoilEuler), OffArm(upperArmStrikeEuler),
				OffArm(postStrikeUpperArmEuler), s, settleP, offArmFollow, postStrikeOffArmFollow));
			AddOffset(offLower, ArmQuat(OffArm(forearmCoilEuler), OffArm(forearmStrikeEuler),
				OffArm(postStrikeForearmEuler), sLag, settlePLag, offArmFollow, postStrikeOffArmFollow));
		}
	}

	// One bone's offset for a given phase.
	//
	// TWO WEIGHTS, ON PURPOSE. `weight` is the swing-motion fraction (1 for the
	// swing arm, offArmFollow for the off arm); `settleWeight` is the fraction of
	// the post-strike DESTINATION. They differ because they mean different things:
	// trailing the lead hand by 45% is right for a motion, but landing 45% of the
	// way along an arc that starts behind her back is not a resting pose, it's a
	// half-bound arm. Continuity is unaffected — at settleP = 0 the Slerp returns
	// Blend(strike, weight) no matter what settleWeight is — so the weight shifts
	// gradually across the settle, which is also the right read: the off arm trails
	// through the swing and catches up as she comes down.
	private static Quaternion ArmQuat(Vector3 coil, Vector3 strike, Vector3 post,
		float s, float settleP, float weight, float settleWeight)
	{
		if (settleP > 0f)
			return Quaternion.Slerp(
				Blend(Shortest(strike), weight),
				Blend(Shortest(post), settleWeight),
				settleP);

		return Blend(Shortest(s < 0f ? coil : strike), Mathf.Abs(s) * weight);
	}

	// Folds any component authored past 180 to its short equivalent: 370.9 → 10.9.
	// Endpoint pose is identical; the path stops detouring through 185°.
	private static Vector3 Shortest(Vector3 e) => new Vector3(
		Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z));

	// Slerp from rest, so the bone sweeps one arc instead of three independently
	// scaled Euler channels racing each other.
	private static Quaternion Blend(Vector3 targetEuler, float amt)
		=> Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(targetEuler), Mathf.Clamp01(amt));

	// Mirroring a local Euler across the body plane negates the yaw and roll and
	// keeps the pitch. Whether that's correct here depends on the bone rolls the
	// symmetrize produced, hence the toggle.
	private Vector3 OffArm(Vector3 e)
		=> mirrorOffArm ? new Vector3(e.x, -e.y, -e.z) : e;

	private void FireContactOnce()
	{
		if (_contactFired) return;
		_contactFired = true;

		System.Action cb = _onContact;
		_onContact = null;
		cb?.Invoke();

		// Presentation last: a throwing VFX subscriber must not be able to prevent
		// the KO. Gameplay is already committed by the time we reach this line.
		OnContact?.Invoke();
	}

	private void Complete()
	{
		if (!_playing) return;
		_playing = false;
		_s = 0f;
		_sLagPrev = 0f;
		_holdingArms = _contactFired;

		System.Action cb = _onComplete;
		_onComplete = null;
		cb?.Invoke();

		Log("Swing complete — torso settled, hands held forward.");
	}

	// Decelerating: fast out of the gate, easing into the target.
	private static float EaseOut(float u)
	{
		u = Mathf.Clamp01(u);
		return 1f - (1f - u) * (1f - u);
	}

	// Accelerating: slow release, peak speed at the end.
	private static float EaseIn(float u)
	{
		u = Mathf.Clamp01(u);
		return u * u;
	}

	private void Log(string msg)
	{
		if (verboseLogging) Debug.Log($"[CassieStrikeDriver] {msg}");
	}
}
