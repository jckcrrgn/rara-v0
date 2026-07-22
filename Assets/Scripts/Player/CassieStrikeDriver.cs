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
/// +1. The pose follows the slider live and the strike never fires, so you can
/// dial the Eulers while watching her instead of re-triggering the beat. Work
/// one axis of one bone at a time: get the SWING arm's strike pose reading
/// first (hands in front of her, bottle toward the guard), then its coil pose,
/// then the forearm, then the off arm. Untick when you're done.
///
/// If the off arm swings the wrong way, flip `mirrorOffArm` — whether the same
/// local Euler mirrors or duplicates across the body depends on the bone rolls
/// in the symmetrized armature, which is not knowable from here.
///
/// CONTACT FRAME (spec §11 — the fix for the Day 59 "finicky" instant-strike)
/// -------------------------------------------------------------------------
/// The KO does NOT fire on the input frame. `onContact` fires the frame `_s`
/// crosses `contactAt` going up — when the bottle is visually at the guard.
/// Keyed to _s rather than elapsed time so retuning the easing or the durations
/// can't desync the hit from the pose.
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
	[Tooltip("Value of the swing scalar at which the blow lands and onContact fires. " +
		"Runs -1 (coiled) to +1 (full follow-through); 0 is the seated pass-through. " +
		"0.5-0.7 puts contact near full extension, just before the arm decelerates. " +
		"Scrub to the value where the bottle reaches him and use that number.")]
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

	[Header("After the strike")]
	[Tooltip("How much of the strike pose the ARMS retain once the settle finishes, " +
		"0-1. The torso always returns to the seated pose, but her hands should not " +
		"go back behind her back — the freed hands are the reveal. 0.25-0.45 leaves " +
		"them forward and spent. Set 0 for a full return to the bound pose.")]
	[Range(0f, 1f)]
	[SerializeField] private float postStrikeArmHold = 0.35f;

	[Header("Debug")]
	[Tooltip("Overrides the timeline with the scrub slider below so you can tune " +
		"poses live in Play Mode. The strike will NOT fire while this is on. " +
		"Remember to untick.")]
	[SerializeField] private bool debugScrubEnabled = false;

	[Tooltip("Manual swing scalar. -1 = full coil, 0 = seated rest, +1 = full " +
		"follow-through. Only active while Debug Scrub Enabled is ticked.")]
	[Range(-1f, 1f)]
	[SerializeField] private float debugScrub = 0f;

	[SerializeField] private bool verboseLogging = true;

	// --- Runtime state -------------------------------------------------------

	private bool _playing;
	private float _t;                 // seconds elapsed since Play()
	private float _s;                 // signed swing scalar, -1..+1
	private float _sPrev;             // last frame's _s, for the contact crossing test
	private bool _contactFired;
	private bool _holdingArms;        // true after a completed strike, drives postStrikeArmHold

	private System.Action _onContact;
	private System.Action _onComplete;

	/// <summary>True while the swing is running. PlayerController folds this into IsBusy.</summary>
	public bool IsPlaying => _playing;

	private float TotalDuration =>
		Mathf.Max(0.01f, windupDuration) +
		Mathf.Max(0.01f, swingDuration) +
		Mathf.Max(0f, followThroughHold) +
		Mathf.Max(0.01f, settleDuration);

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
		_sPrev = 0f;
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
			ApplyTorso(debugScrub);
			ApplyArms(debugScrub, debugScrub);
			return;
		}

		if (!_playing)
		{
			// Post-strike residual: hands stay forward, torso is back at rest.
			if (_holdingArms && postStrikeArmHold > 0f)
				ApplyArms(postStrikeArmHold, postStrikeArmHold);
			return;
		}

		_t += dt;
		_sPrev = _s;
		_s = EvaluateSwing(_t);

		// Contact test: the frame _s crosses the threshold going UP. Keyed to the
		// pose, not the clock, so retuning the easing can't desync the hit.
		if (!_contactFired && _sPrev < contactAt && _s >= contactAt)
		{
			Log($"CONTACT at t={_t:F3}s (s={_s:F2}).");
			FireContactOnce();
		}

		// Forearm trails the upper arm — the whip. Sampling the swing scalar
		// slightly in the past is the cheapest honest way to lag a joint without
		// a second timeline.
		float sLag = EvaluateSwing(Mathf.Max(0f, _t - forearmLag * Mathf.Max(0.01f, swingDuration)));

		ApplyTorso(_s);
		ApplyArms(_s, sLag);

		if (_t >= TotalDuration)
		{
			// Guarantee contact fired even in a pathological frame (a single dt
			// spanning the whole swing — an editor hitch or a breakpoint).
			FireContactOnce();
			Complete();
		}
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
		float side = swingWithRightHand ? 1f : -1f;
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
	/// Both arms move the SAME direction (optionally mirrored across the body).
	/// They do NOT counter-rotate — that's the Struggle driver's wrist grind, and
	/// applying it here is what pulled her hands apart instead of bringing them
	/// around together.
	/// </summary>
	private void ApplyArms(float s, float sLag)
	{
		Vector3 upperE = s    < 0f ? upperArmCoilEuler * -s    : upperArmStrikeEuler * s;
		Vector3 lowerE = sLag < 0f ? forearmCoilEuler  * -sLag : forearmStrikeEuler  * sLag;

		HumanBodyBones swingUpper = swingWithRightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm;
		HumanBodyBones swingLower = swingWithRightHand ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm;
		HumanBodyBones offUpper   = swingWithRightHand ? HumanBodyBones.LeftUpperArm  : HumanBodyBones.RightUpperArm;
		HumanBodyBones offLower   = swingWithRightHand ? HumanBodyBones.LeftLowerArm  : HumanBodyBones.RightLowerArm;

		AddOffset(swingUpper, Quaternion.Euler(upperE));
		AddOffset(swingLower, Quaternion.Euler(lowerE));

		if (offArmFollow > 0f)
		{
			AddOffset(offUpper, Quaternion.Euler(OffArm(upperE) * offArmFollow));
			AddOffset(offLower, Quaternion.Euler(OffArm(lowerE) * offArmFollow));
		}
	}

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
	}

	private void Complete()
	{
		if (!_playing) return;
		_playing = false;
		_s = 0f;
		_sPrev = 0f;
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
