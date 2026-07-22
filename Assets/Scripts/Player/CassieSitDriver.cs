using UnityEngine;

/// <summary>
/// Sit layer (spec §13). Cassie's chair-bound base idle.
///
///   - NOT feigning → a quiet, ALIVE bound idle: breathing plus a faint restless
///     sway. She is NOT working the ropes here — that's the Struggle verb (its own
///     key, CassieStruggleDriver). Between struggles she just sits, bound and tense.
///   - Feigning     → COMPLIANT: the restless sway blends out and a small "given-up"
///     pose blends in (head dips, spine rounds forward). Reads as helpless / still
///     for the guard's inspection.
///
/// Never touches the arms: her wrists are bound TOGETHER, so any arm swing that
/// separates the hands breaks the fiction. The arms rest wherever the seated pose
/// puts them. Wrist work lives in Struggle, as a twist (rotation, hands stay put).
///
/// Not guard-gated — reads only Cassie's own IsFeigning (§13: escalation lives in
/// HER state, never the guard's).
/// </summary>
public class CassieSitDriver : CassieRigLayer
{
	[Header("Feign source")]
	[Tooltip("Cassie's PlayerController. If unassigned, resolves via FindFirstObjectByType. " +
		"Only IsFeigning / OnFeignChanged are read — no gameplay is touched.")]
	[SerializeField] private PlayerController player;

	[Header("Breathing (always on)")]
	[Tooltip("Seconds per breath cycle (spine pitch).")]
	[SerializeField] private float breathPeriod = 4f;
	[Tooltip("Breath depth in degrees of spine pitch.")]
	[SerializeField] private float breathAmplitude = 1.5f;

	[Header("Idle life (not feigning)")]
	[Tooltip("A faint restless sway so the not-feigning idle reads as alive and tense, " +
		"not frozen. Slow and small — ambient life, NOT struggling.")]
	[SerializeField] private float idleSwayPeriod = 5.5f;
	[Tooltip("Peak head yaw sway in degrees.")]
	[SerializeField] private float idleHeadYaw = 2.5f;
	[Tooltip("Peak spine yaw sway in degrees (subtle whole-torso settle).")]
	[SerializeField] private float idleSpineYaw = 1f;

	[Header("Compliant pose (feigning)")]
	[Tooltip("The 'given up / helpless' offset blended in while feigning: head dips " +
		"toward the chest, spine rounds forward a touch. Sign depends on your rig's " +
		"local axes — flip in-editor if the dip goes the wrong way.")]
	[SerializeField] private float compliantHeadPitch = 8f;
	[SerializeField] private float compliantSpinePitch = 4f;
	[Tooltip("Seconds to blend between alive-idle and compliant when feign toggles.")]
	[SerializeField] private float feignBlend = 0.3f;

	// 0 = not feigning (alive idle), 1 = feigning (compliant / still).
	private float _feignWeight;
	private float _feignTarget;
	private float _t;

	protected override void DeclareBones()
	{
		Declare(HumanBodyBones.Spine);   // required
		Declare(HumanBodyBones.Chest);   // optional — skipped if absent
		Declare(HumanBodyBones.Head);    // required
	}

	protected override void Awake()
	{
		base.Awake();
		if (player == null) player = FindFirstObjectByType<PlayerController>();
		if (player == null)
			Debug.LogWarning("[CassieSitDriver] No PlayerController — feign won't drive; " +
				"she'll hold the alive idle.");
	}

	protected override void OnEnable()
	{
		base.OnEnable();   // registers with the rig
		if (player != null) player.OnFeignChanged += OnFeignChanged;

		_feignTarget = (player != null && player.IsFeigning) ? 1f : 0f;
		_feignWeight = _feignTarget;
	}

	protected override void OnDisable()
	{
		base.OnDisable();  // unregisters
		if (player != null) player.OnFeignChanged -= OnFeignChanged;
	}

	private void OnFeignChanged(bool feigning) => _feignTarget = feigning ? 1f : 0f;

	public override void Contribute(float dt)
	{
		_t += dt;

		_feignWeight = feignBlend > 0f
			? Mathf.MoveTowards(_feignWeight, _feignTarget, dt / feignBlend)
			: _feignTarget;

		float alive = 1f - _feignWeight;

		// --- Breathing: always on. ---
		float breath = Mathf.Sin(_t * TwoPiOver(breathPeriod));
		AddLocalEuler(HumanBodyBones.Spine, breath * breathAmplitude, 0f, 0f);

		// --- Alive idle (not feigning): faint restless sway. ---
		if (alive > 0.001f)
		{
			float sway = Mathf.Sin(_t * TwoPiOver(idleSwayPeriod));
			AddLocalEuler(HumanBodyBones.Head,  0f, sway * idleHeadYaw  * alive, 0f);
			AddLocalEuler(HumanBodyBones.Spine, 0f, sway * idleSpineYaw * alive, 0f);
		}

		// --- Compliant pose (feigning): head dips, spine rounds forward. ---
		if (_feignWeight > 0.001f)
		{
			AddLocalEuler(HumanBodyBones.Head,  compliantHeadPitch  * _feignWeight, 0f, 0f);
			AddLocalEuler(HumanBodyBones.Spine, compliantSpinePitch * _feignWeight, 0f, 0f);
		}
	}

	private static float TwoPiOver(float period) => (Mathf.PI * 2f) / Mathf.Max(0.01f, period);
}
