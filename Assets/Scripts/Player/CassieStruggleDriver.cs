using UnityEngine;

/// <summary>
/// Struggle layer (spec §13, added Day 77; envelope revised Day 77 after mash test).
/// A continuous effort beat driven by PlayerController.OnStruggleAttempt — one press
/// gives a short burst of struggling; mashing sustains it into continuous work.
///
/// The motion: she leans forward and twists, searching for purchase, head turning
/// against the torso — and her WRISTS TWIST AGAINST EACH OTHER (a roll about the
/// forearm axis, counter-rotating). The twist is rotation, not translation, so the
/// bound hands stay together while she grinds them against the rope.
///
/// ENVELOPE (why it's built this way)
/// ----------------------------------
/// The first version restarted a one-shot beat on every press and flipped a discrete
/// side each time. Mashing then snapped the offsets to zero mid-motion (she popped
/// back to the Sit pose) and instantly reversed direction — visible glitching, and
/// pressing faster than the beat meant the motion never developed at all.
///
/// So: the oscillation PHASE advances continuously and is never restarted, and each
/// press only tops up an INTENSITY value that decays when she stops. Intensity
/// follows its target through a short smoothing ramp, so it can't step. Nothing is
/// ever discontinuous, at any mash rate. The alternating search direction falls out
/// of the sine oscillating either side of neutral — no discrete flip to pop.
///
/// Only fires on real struggles: the event can't fire while feigning (Struggle input
/// is suppressed there), so it's naturally silent during inspections.
///
/// DAY 156
/// -------
/// Two changes, both found while testing the wrist lashing against this layer.
///
/// 1. `wristTwistAxis` had never been tuned off its placeholder (0,0,1), which is a
///    TRANSVERSE axis on this rig. Every struggle since Day 77 has been swinging the
///    forearms, not rolling them — the hands came apart instead of grinding together,
///    which is the opposite of the motion this file's header describes. Long axis is
///    local Y. See the note on the field.
///
/// 2. Added a debug scrub, mirroring CassieStrikeDriver's. Unlike the strike this
///    layer has no timeline to scrub — the pose exists only while a press is decaying,
///    so there was no way to hold a frame, judge it, or shoot it.
/// </summary>
public class CassieStruggleDriver : CassieRigLayer
{
	[Header("Struggle source")]
	[Tooltip("Cassie's PlayerController. If unassigned, resolves via FindFirstObjectByType. " +
		"Only OnStruggleAttempt is read — no gameplay is touched.")]
	[SerializeField] private PlayerController player;

	[Header("Effort envelope")]
	[Tooltip("How long a single press keeps her struggling, in seconds. Each press " +
		"refunds this in full, so holding a mash going sustains the motion indefinitely; " +
		"stop pressing and she eases back to the idle over this long.")]
	[SerializeField] private float sustain = 0.45f;

	[Tooltip("Ramp time (seconds) for effort to rise/fall toward its target. This is " +
		"what makes mashing smooth instead of steppy — keep it small but non-zero. " +
		"0.06–0.12 feels responsive without popping.")]
	[SerializeField] private float attack = 0.08f;

	[Tooltip("Seconds per grind cycle — one full twist out and back. Faster reads as " +
		"more frantic. 0.35–0.5 reads as urgent effort.")]
	[SerializeField] private float cyclePeriod = 0.42f;

	[Header("Torso + head (searching for purchase)")]
	[Tooltip("Forward lean of the spine while struggling, in degrees. Unipolar — she " +
		"leans in and HOLDS it while working, rather than rocking backward on the " +
		"off-beat. Surges slightly at the peak of each twist.")]
	[SerializeField] private float leanPitch = 10f;
	[Tooltip("Peak torso yaw twist, in degrees. Oscillates side to side — working " +
		"different angles against the rope.")]
	[SerializeField] private float torsoTwist = 6f;
	[Tooltip("Peak head yaw, in degrees. Turns opposite the torso, straining to look " +
		"back at the knot.")]
	[SerializeField] private float headTwist = 10f;

	[Header("Wrists (twist against each other)")]
	[Tooltip("Local axis of the FOREARM to roll about — the wrist-twist axis. This MUST " +
		"be the bone's LONG axis (the one pointing at the hand), or AngleAxis becomes a " +
		"swing instead of a roll and the wrists translate apart instead of grinding " +
		"together. On Cassie_D136 the long axis is local Y — verified Day 156 against " +
		"LowerArm.R in the scene view. Normalised at runtime.")]
	// Day 156: was (0,0,1) — the untuned initializer, which is TRANSVERSE on this rig.
	// A 20 deg swing about it walks the wrist ~0.238 * sin(20) = 0.081 m, roughly nine
	// cord widths, which is what made the left wrist slide out of the lashing. The
	// scene instances serialize their own copy of this value: fixing it here does NOT
	// fix them. Set it on each Cassie instance by hand.
	[SerializeField] private Vector3 wristTwistAxis = new Vector3(0f, 1f, 0f);
	[Tooltip("Peak wrist roll in degrees. L and R roll in OPPOSITE directions so they " +
		"grind against each other — if they twist the SAME way on your rig, negate this " +
		"amplitude for one side or flip the axis.")]
	[SerializeField] private float wristTwistAmplitude = 20f;

	[Header("Debug")]
	[Tooltip("Overrides the press envelope with the two sliders below so you can HOLD a " +
		"struggle frame in Play Mode. Presses are ignored while this is on. Needed for " +
		"anything you have to judge or photograph — the struggle is event-driven and has " +
		"no timeline, so without this the only way to see it is to mash. Remember to untick.")]
	[SerializeField] private bool debugScrubEnabled = false;

	[Tooltip("Effort level to hold. 1 = the intensity a sustained mash settles at.")]
	[Range(0f, 1f)]
	[SerializeField] private float debugIntensity = 1f;

	[Tooltip("Position within one grind cycle, 0..1. The twist extremes — and so the " +
		"worst case for anything bound to a wrist — are at 0.25 and 0.75. 0 and 0.5 are " +
		"the neutral crossings.")]
	[Range(0f, 1f)]
	[SerializeField] private float debugPhase01 = 0.25f;

	private float _energy;      // press target: topped to 1 per attempt, decays over `sustain`
	private float _intensity;   // smoothed follower of _energy — what actually scales the motion
	private float _phase;       // continuous oscillation phase (radians), never restarted mid-motion

	protected override void DeclareBones()
	{
		Declare(HumanBodyBones.Spine);
		Declare(HumanBodyBones.Chest);          // optional — skipped if absent
		Declare(HumanBodyBones.Head);
		Declare(HumanBodyBones.LeftLowerArm);   // forearm roll = wrist twist
		Declare(HumanBodyBones.RightLowerArm);
	}

	protected override void Awake()
	{
		base.Awake();
		if (player == null) player = FindFirstObjectByType<PlayerController>();
		if (player == null)
			Debug.LogWarning("[CassieStruggleDriver] No PlayerController — struggle beats " +
				"won't fire.");
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (player != null) player.OnStruggleAttempt += OnStruggle;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (player != null) player.OnStruggleAttempt -= OnStruggle;
	}

	// Top up the effort. Deliberately the ONLY thing a press does — no phase reset,
	// no direction flip, so any mash rate stays continuous.
	private void OnStruggle() => _energy = 1f;

	public override void Contribute(float dt)
	{
		// Tuning mode: pose follows the sliders, the envelope doesn't run. On untick,
		// _energy is wherever the live path left it (normally 0) and _intensity eases
		// back to it over `attack` — so leaving scrub mode is smooth, not a pop.
		if (debugScrubEnabled)
		{
			_intensity = debugIntensity;
			_phase     = debugPhase01 * Mathf.PI * 2f;
			ApplyPose(Mathf.Sin(_phase));
			return;
		}

		// Effort decays toward zero; each press refunds it. Intensity chases through
		// the attack ramp so it can never step.
		_energy    = Mathf.MoveTowards(_energy, 0f, dt / Mathf.Max(0.01f, sustain));
		_intensity = Mathf.MoveTowards(_intensity, _energy, dt / Mathf.Max(0.01f, attack));

		// Fully at rest: contribute nothing and park the phase at neutral, so the next
		// struggle starts from a clean zero-crossing instead of mid-swing.
		if (_intensity <= 0.0001f)
		{
			_phase = 0f;
			return;
		}

		_phase += dt * (Mathf.PI * 2f) / Mathf.Max(0.01f, cyclePeriod);
		ApplyPose(Mathf.Sin(_phase));   // -1..1, the grind oscillation
	}

	/// <summary>
	/// Applies the pose for one oscillation sample. Split out Day 156 so the debug scrub
	/// and the live envelope drive exactly the same code — a scrub that poses her by a
	/// second route is a scrub you can't trust what you saw in.
	/// </summary>
	private void ApplyPose(float w)
	{
		// Torso: forward lean held for the duration of the effort (unipolar, with a
		// small surge at each twist peak) plus a side-to-side search twist. Chest adds
		// a fraction so it's a whole-upper-body strain, not a hinge at the waist.
		float lean = leanPitch * _intensity * (0.75f + 0.25f * Mathf.Abs(w));
		float twist = torsoTwist * w * _intensity;
		AddLocalEuler(HumanBodyBones.Spine, lean,        twist,        0f);
		AddLocalEuler(HumanBodyBones.Chest, lean * 0.5f, twist * 0.5f, 0f);

		// Head: turns opposite the torso, straining to look for the knot.
		AddLocalEuler(HumanBodyBones.Head, 0f, -headTwist * w * _intensity, 0f);

		// Wrists: roll about the forearm axis, counter-rotating — twisting against
		// each other within the rope. Rotation only, so the hands stay together.
		Vector3 axis = wristTwistAxis.sqrMagnitude > 0.0001f ? wristTwistAxis.normalized : Vector3.up;
		float roll = wristTwistAmplitude * w * _intensity;
		AddOffset(HumanBodyBones.LeftLowerArm,  Quaternion.AngleAxis( roll, axis));
		AddOffset(HumanBodyBones.RightLowerArm, Quaternion.AngleAxis(-roll, axis));
	}
}
