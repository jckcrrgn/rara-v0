using UnityEngine;

/// <summary>
/// Rara Day 162 — recording aid for CAM_Shot4_Check. NOT gameplay.
///
/// Shot 4 "Check": she works the ropes hunched forward, the guard's motion reaches
/// her, she stops — then straightens and turns her head toward the door behind her.
///
/// WHAT THIS LAYER DOES NOT DO
/// ---------------------------
/// It does not animate struggling. CassieStruggleDriver owns that motion and owns
/// the decay out of it (sustain + attack). This layer only keeps that layer fed
/// during the struggle phase, via ShotPulse(), and stops feeding it at `struggleUntil`.
/// The still is therefore the authored envelope, not a second version of it.
///
/// What it contributes on its own: an extra forward hunch on Spine/Chest that
/// releases with a small overshoot past rest (so "straightens up" reads as an
/// action, not as the struggle merely ending), and a Neck+Head yaw for the look.
///
/// Order 3 — rides on top of Sit (0) and Struggle (1). Sit's alive-idle sway keeps
/// running underneath through the hold, which is what stops the final frames
/// reading as a freeze.
///
/// Play mode:  F9 = run the beat   F10 = reset to rest
///             or tick "Play Now" in the Inspector, or context menu > Play.
/// </summary>
public class CassieShot4Driver : CassieRigLayer
{
	[Header("Wiring")]
	[Tooltip("The struggle layer to feed. If unassigned, resolves from this object.")]
	[SerializeField] private CassieStruggleDriver struggle;

	[Header("Beat timing (seconds from Play)")]
	[Tooltip("Dead time before anything happens. Gives the recorder a clean handle.")]
	[SerializeField] private float startDelay = 0f;
	[Tooltip("She struggles until here, then the struggle layer eases out on its own " +
		"envelope (~sustain + attack, about 0.5s at current values).")]
	[SerializeField] private float struggleUntil = 1.75f;

	[Header("Hunch → straighten")]
	[Tooltip("Extra forward spine pitch while struggling, ON TOP of the struggle " +
		"layer's own leanPitch. Positive = forward, matching that layer's convention.")]
	[SerializeField] private float hunchPitch = 6f;
	[Tooltip("Fraction of the hunch the chest also takes, so it's an upper-body curl " +
		"rather than a hinge at the waist.")]
	[SerializeField] private float chestShare = 0.5f;
	[Tooltip("When the straighten begins. Slightly after struggleUntil — the body " +
		"stops first, then pulls up.")]
	[SerializeField] private float straightenAt = 1.80f;
	[SerializeField] private float straightenDuration = 0.50f;
	[Tooltip("Degrees she pulls up PAST rest before settling back. Small. This is what " +
		"makes it read as her deciding to stop rather than running out of effort.")]
	[SerializeField] private float straightenOvershoot = 3f;

	[Header("Look toward the door")]
	[Tooltip("Total yaw, split between Neck and Head. ~40 is a natural over-shoulder " +
		"turn. SIGN IS RIG-DEPENDENT — play it once and negate if she looks the wrong way.")]
	[SerializeField] private float lookYaw = 40f;
	[Tooltip("Share of the total yaw taken by the Neck bone. All of it on Head reads " +
		"as a swivel at the skull base. If Neck isn't mapped on the Avatar the rig " +
		"logs a warning at Claim — set this to 0 and put the full turn on Head.")]
	[Range(0f, 1f)]
	[SerializeField] private float neckShare = 0.4f;
	[Tooltip("When the turn begins. Should be after the struggle's own head twist has " +
		"decayed, or the two compose into a wobble.")]
	[SerializeField] private float lookAt = 2.20f;
	[SerializeField] private float lookDuration = 0.50f;

	[Header("Playback")]
	[Tooltip("Run the beat automatically on Play. Leave on for recording — then the " +
		"take is deterministic and no keypress timing lands in the footage.")]
	[SerializeField] private bool playOnStart = true;
	[Tooltip("Tick in play mode to fire. Clears itself.")]
	[SerializeField] private bool playNow;

	private float _t;
	private bool _running;

	protected override void DeclareBones()
	{
		Declare(HumanBodyBones.Spine);
		Declare(HumanBodyBones.Chest);   // optional — skipped if absent
		Declare(HumanBodyBones.Neck);    // optional — see neckShare
		Declare(HumanBodyBones.Head);
	}

	protected override void Awake()
	{
		base.Awake();
		if (struggle == null) struggle = GetComponent<CassieStruggleDriver>();
		if (struggle == null)
			Debug.LogWarning("[CassieShot4] No CassieStruggleDriver — she'll straighten " +
				"and look, but there will be no struggle to still.");
	}

	private void Start()
	{
		if (playOnStart) Play();
	}

	private void Update()
	{
		if (PlayPressed() || playNow) { playNow = false; Play(); }
		if (ResetPressed()) ResetBeat();
	}

	[ContextMenu("Play (play mode)")]
	public void Play()
	{
		if (!Application.isPlaying) { Debug.LogWarning("[CassieShot4] Play mode only."); return; }
		_t = 0f;
		_running = true;
		Debug.Log($"[CassieShot4] struggle→{struggleUntil}s, straighten @{straightenAt}s, " +
			$"look {lookYaw}° @{lookAt}s (+{startDelay}s delay)");
	}

	[ContextMenu("Reset to Rest")]
	public void ResetBeat()
	{
		_running = false;
		_t = 0f;
	}

	public override void Contribute(float dt)
	{
		if (!_running) return;   // contributes nothing → bones fall back to rest

		_t += dt;
		float t = _t - startDelay;
		if (t < 0f) return;

		// --- Keep the struggle layer fed. It owns both the motion and the easing out. ---
		if (t < struggleUntil && struggle != null) struggle.ShotPulse();

		// --- Hunch, releasing into a small pull-up past rest. ---
		float hunchWeight = 1f;
		float overshoot = 0f;
		if (t >= straightenAt)
		{
			float k = Mathf.Clamp01((t - straightenAt) / Mathf.Max(0.01f, straightenDuration));
			hunchWeight = 1f - Mathf.SmoothStep(0f, 1f, k);
			// Rise and settle: peaks mid-straighten, back to zero as she lands on rest.
			overshoot = straightenOvershoot * Mathf.Sin(k * Mathf.PI);
		}

		float pitch = hunchPitch * hunchWeight - overshoot;
		if (Mathf.Abs(pitch) > 0.001f)
		{
			AddLocalEuler(HumanBodyBones.Spine, pitch, 0f, 0f);
			AddLocalEuler(HumanBodyBones.Chest, pitch * chestShare, 0f, 0f);
		}

		// --- The look. Yaw is local Y, same convention as Sit and Struggle. ---
		if (t >= lookAt)
		{
			float k = Mathf.Clamp01((t - lookAt) / Mathf.Max(0.01f, lookDuration));
			float yaw = lookYaw * Mathf.SmoothStep(0f, 1f, k);
			AddLocalEuler(HumanBodyBones.Neck, 0f, yaw * neckShare, 0f);
			AddLocalEuler(HumanBodyBones.Head, 0f, yaw * (1f - neckShare), 0f);
		}
	}

	private static bool PlayPressed()
	{
#if ENABLE_INPUT_SYSTEM
		return UnityEngine.InputSystem.Keyboard.current != null
			&& UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
		return Input.GetKeyDown(KeyCode.F9);
#else
		return false;
#endif
	}

	private static bool ResetPressed()
	{
#if ENABLE_INPUT_SYSTEM
		return UnityEngine.InputSystem.Keyboard.current != null
			&& UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
		return Input.GetKeyDown(KeyCode.F10);
#else
		return false;
#endif
	}
}
