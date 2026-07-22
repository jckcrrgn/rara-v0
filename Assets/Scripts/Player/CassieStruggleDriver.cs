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
	[Tooltip("Local axis of the FOREARM to roll about — the wrist-twist axis. Unknown " +
		"until you see it on the blockout; tune against the viewport. Normalised at runtime.")]
	[SerializeField] private Vector3 wristTwistAxis = new Vector3(0f, 0f, 1f);
	[Tooltip("Peak wrist roll in degrees. L and R roll in OPPOSITE directions so they " +
		"grind against each other — if they twist the SAME way on your rig, negate this " +
		"amplitude for one side or flip the axis.")]
	[SerializeField] private float wristTwistAmplitude = 20f;

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
		float w = Mathf.Sin(_phase);   // -1..1, the grind oscillation

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
		Vector3 axis = wristTwistAxis.sqrMagnitude > 0.0001f ? wristTwistAxis.normalized : Vector3.forward;
		float roll = wristTwistAmplitude * w * _intensity;
		AddOffset(HumanBodyBones.LeftLowerArm,  Quaternion.AngleAxis( roll, axis));
		AddOffset(HumanBodyBones.RightLowerArm, Quaternion.AngleAxis(-roll, axis));
	}
}
