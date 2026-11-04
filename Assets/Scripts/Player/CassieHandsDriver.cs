using UnityEngine;

/// <summary>
/// Hands layer (Day 208). Mechanically there are three hand states; this is the layer that
/// makes the third one visible.
///
///   Bound      wrists behind her back, together. Rest pose + CassieStruggleDriver's grind.
///   Feigning   free but hidden: the SAME pose as bound. He must not be able to tell.
///              This layer contributes nothing. CassieSitDriver adds the compliant dip.
///   Hands out  both hands forward on the selected knot, working it. This layer.
///
/// HOW
/// ---
/// Two-bone IK per arm (upper arm + forearm), aimed at the knot of the selected bond. The
/// knot is the bond's anchor midpoint on her body (BondStruggle.TryAnchorWorld: feet for
/// Ankles, shins for Knees later, one foot for a one-sided ankle bond) plus knotOffset.
/// When the ropes exist, the knot moves onto the rope; until then it's computed, so this
/// needs no scene setup. Hands out with nothing untieable selected: hands rest in her lap.
/// Never while the wrists are bound: a bound bond is worked by the bound + struggle pose.
/// Lives on Cassie_D136 (the live model), next to CassieRig and the other layers.
///
/// The IK has to see where the torso lean leaves the shoulders, so it runs in PostApply,
/// after CassieRig has written every bone. The torso lean that makes the reach possible
/// is contributed normally, in Contribute, so it's in the pose the IK reads.
///
/// IK sets world rotations on top of the composed pose and never touches the skeleton,
/// so the bone-roll invariant holds. Blending is in local space, rest pose → solved pose,
/// by the reach weight.
///
/// TIMING = THE MECHANIC
/// ---------------------
/// Out: reachTime (0.25 s). Back: BondStruggle.handsBackTime (0.4 s), which IS the hide
/// budget — BondStruggle clears handsOut at the end of the same 0.4 s, so what the player
/// sees matches what the guard sees.
///
/// YIELDS
/// ------
/// The strike owns the arms while it plays or holds (CassieStrikeDriver.OwnsArms). A held
/// item (the bottle) keeps its own pose. Both fade this layer out over reachTime.
///
/// WORK MOTION
/// -----------
/// Each untie input tugs both hands along the pressed direction in screen space — the
/// same direction the bight will point. A fumble is a short, smaller tug back.
///
/// Unverified on this rig: pole side, knot offset, hand spread. All set by eye in the
/// scene. Gizmos draw the targets and poles with the component selected.
/// </summary>
public class CassieHandsDriver : CassieRigLayer
{
	[Header("Sources")]
	[Tooltip("Cassie's PlayerController. Empty = found in scene.")]
	[SerializeField] private PlayerController player;
	[Tooltip("Empty = found on the player.")]
	[SerializeField] private BondStruggle struggle;
	[Tooltip("Empty = found on this object. Yields the arms to it while it plays or holds.")]
	[SerializeField] private CassieStrikeDriver strike;
	[Tooltip("Cassie's Humanoid Animator. Empty = found on this object, parents, children.")]
	[SerializeField] private Animator animator;

	[Header("Timing")]
	[Tooltip("Seconds for the hands to come out to the knot. Back uses BondStruggle.handsBackTime.")]
	[SerializeField] private float reachTime = 0.25f;

	[Header("Torso (makes the reach possible)")]
	[Tooltip("Spine pitch (degrees, + forward like the struggle lean) while reaching a knot.")]
	[SerializeField] private float reachSpinePitch = 25f;
	[Tooltip("Chest pitch while reaching a knot.")]
	[SerializeField] private float reachChestPitch = 12f;
	[Tooltip("Share of the lean kept for the lap rest (hands out, no knot).")]
	[Range(0f, 1f)] [SerializeField] private float lapLeanShare = 0.25f;

	[Header("Targets")]
	[Tooltip("Knot = bond anchor midpoint + this, in her facing frame (x right, y up, z forward). " +
		"Default: a little above and in front of the ankles, where a knot sits on top of the rope.")]
	[SerializeField] private Vector3 knotOffset = new Vector3(0f, 0.06f, 0.05f);
	[Tooltip("Lap rest, as a blend from her hips (0) to her knees (1), plus lapLift up.")]
	[Range(0f, 1f)] [SerializeField] private float lapAlongThigh = 0.55f;
	[SerializeField] private float lapLift = 0.06f;
	[Tooltip("Distance between the two hand targets, metres, across her facing.")]
	[SerializeField] private float handSpread = 0.08f;
	[Tooltip("Wrist sits this far short of the target, towards the shoulder: the fingers do the " +
		"work, not the wrist bone. ~half her hand length (0.16).")]
	[SerializeField] private float wristBack = 0.07f;

	[Header("Elbow pole")]
	[Tooltip("Where each elbow points, from the shoulder, in her facing frame. x is mirrored per " +
		"side (+ = outward). Flip y or z if the elbows bend the wrong way.")]
	[SerializeField] private Vector3 elbowPole = new Vector3(0.35f, -0.1f, -0.1f);

	[Header("Work motion")]
	[Tooltip("Tug distance per correct untie input, metres, along the pressed direction.")]
	[SerializeField] private float workAmplitude = 0.035f;
	[Tooltip("Tug duration, seconds. Out and back.")]
	[SerializeField] private float workTime = 0.16f;
	[Tooltip("Fumble tug, as a share of workAmplitude, back toward the knot.")]
	[Range(0f, 1f)] [SerializeField] private float fumbleShare = 0.4f;

	[Header("Debug")]
	[Tooltip("Tuning only. Holds the reach on the selected bond's knot even while her wrists " +
		"are bound, so you can set knotOffset / pole / lean without freeing her first. Only for " +
		"bonds that have a knot (untieable: Ankles, later Knees). Wrists or elbows selected = " +
		"nothing, as in play. Remember to untick.")]
	[SerializeField] private bool debugForceReach = false;

	private float _weight;        // 0 = rest pose (bound / feigning), 1 = solved
	private float _lean;          // eased torso share: 1 at a knot, lapLeanShare in the lap
	private float _workT = 1f;    // 0..1 through the current tug; 1 = idle
	private Vector3 _workDir;     // world-space tug direction, signed
	private Vector3 _gizL, _gizR, _gizPoleL, _gizPoleR;
	private bool _gizValid;

	protected override void DeclareBones()
	{
		Declare(HumanBodyBones.Spine);
		Declare(HumanBodyBones.Chest);            // optional
		Declare(HumanBodyBones.LeftUpperArm);
		Declare(HumanBodyBones.LeftLowerArm);
		Declare(HumanBodyBones.RightUpperArm);
		Declare(HumanBodyBones.RightLowerArm);
	}

	protected override void Awake()
	{
		base.Awake();
		if (player == null) player = FindFirstObjectByType<PlayerController>();
		if (struggle == null && player != null) struggle = player.GetComponent<BondStruggle>();
		if (strike == null) strike = GetComponent<CassieStrikeDriver>();
		if (animator == null) animator = GetComponent<Animator>();
		if (animator == null) animator = GetComponentInParent<Animator>();
		if (animator == null) animator = GetComponentInChildren<Animator>();
		if (struggle == null)
			Debug.LogWarning("[CassieHandsDriver] No BondStruggle — hands never come out (legacy level).");
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (struggle != null) struggle.OnUntieInput += OnUntie;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (struggle != null) struggle.OnUntieInput -= OnUntie;
	}

	// ---- state ------------------------------------------------------------------------

	bool Yielding => (strike != null && strike.OwnsArms) || (player != null && player.GetHeldItem() != null);

	// Day 208 rule: her hands only leave the bound pose when they're free AND out. A
	// bound bond (wrists, elbows) is worked by the bound + struggle pose, never by reaching.
	bool RealOut => struggle != null && struggle.HandsOut && !struggle.IsHidingHands && !Yielding;

	/// <summary>Debug: the selected bond has a knot to reach, whatever her hands are doing.</summary>
	bool DebugKnot
	{
		get
		{
			if (!debugForceReach || struggle == null) return false;
			int i = struggle.Selected;
			return i >= 0 && i < struggle.bonds.Length && struggle.bonds[i].untieable && struggle.bonds[i].Bound;
		}
	}

	bool WantOut => DebugKnot || RealOut;

	bool AtKnot => DebugKnot || (struggle != null && struggle.CanUntie(struggle.Selected));

	void OnUntie(int dir, bool correct)
	{
		Camera cam = Camera.main;
		if (cam == null) return;
		Vector3 up = cam.transform.up, right = cam.transform.right;
		Vector3 d = dir == 0 ? up : dir == 1 ? right : dir == 2 ? -up : -right;
		_workDir = correct ? d : -d * fumbleShare;
		_workT = 0f;
	}

	// ---- Contribute: weights + the torso lean ----------------------------------------

	public override void Contribute(float dt)
	{
		bool hiding = struggle != null && struggle.IsHidingHands;
		float time = WantOut ? reachTime : hiding ? Mathf.Max(0.01f, struggle.handsBackTime) : reachTime;
		_weight = Mathf.MoveTowards(_weight, WantOut ? 1f : 0f, dt / Mathf.Max(0.01f, time));
		_lean = Mathf.MoveTowards(_lean, AtKnot ? 1f : lapLeanShare,
			dt / Mathf.Max(0.01f, reachTime));
		if (_workT < 1f) _workT = Mathf.Min(1f, _workT + dt / Mathf.Max(0.01f, workTime));

		float e = Ease(_weight) * _lean;
		if (e <= 0.0001f) return;
		AddLocalEuler(HumanBodyBones.Spine, reachSpinePitch * e, 0f, 0f);
		AddLocalEuler(HumanBodyBones.Chest, reachChestPitch * e, 0f, 0f);
	}

	// ---- PostApply: the IK, on the final pose ---------------------------------------

	public override void PostApply(float dt)
	{
		_gizValid = false;
		float w = Ease(_weight);
		if (w <= 0.0001f || animator == null) return;
		if (!Facing(out Vector3 right, out Vector3 up, out Vector3 fwd)) return;

		Vector3 center;
		int sel = struggle != null ? struggle.Selected : -1;
		if (AtKnot && struggle.TryAnchorWorld(sel, out Vector3 anchor))
			center = anchor + right * knotOffset.x + up * knotOffset.y + fwd * knotOffset.z;
		else if (!RealOut || !Lap(up, out center)) return;   // lap only for real hands-out

		if (_workT < 1f) center += _workDir * workAmplitude * Mathf.Sin(Mathf.PI * _workT);

		Vector3 half = right * (handSpread * 0.5f);
		_gizL = center - half;
		_gizR = center + half;
		Solve(HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,
			_gizL, -1f, right, up, fwd, w, out _gizPoleL);
		Solve(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
			_gizR, +1f, right, up, fwd, w, out _gizPoleR);
		_gizValid = true;
	}

	/// <summary>
	/// Analytic two-bone IK. Elbow bends in the plane of shoulder, target and pole. Solves in
	/// world space, then blends each bone's LOCAL rotation from the composed pose to the
	/// solution by w.
	/// </summary>
	void Solve(HumanBodyBones upB, HumanBodyBones loB, HumanBodyBones handB, Vector3 target,
		float side, Vector3 right, Vector3 up, Vector3 fwd, float w, out Vector3 polePos)
	{
		polePos = Vector3.zero;
		Transform upT = animator.GetBoneTransform(upB);
		Transform loT = animator.GetBoneTransform(loB);
		Transform hT  = animator.GetBoneTransform(handB);
		if (upT == null || loT == null || hT == null) return;

		Quaternion upLocal0 = upT.localRotation, loLocal0 = loT.localRotation;

		Vector3 S = upT.position, E = loT.position, W = hT.position;
		float a = (E - S).magnitude, b = (W - E).magnitude;
		if (a < 1e-4f || b < 1e-4f) return;

		// The wrist stops short of the target, so the fingers sit on the knot.
		Vector3 toT = target - S;
		if (toT.sqrMagnitude < 1e-6f) return;
		Vector3 wristTarget = target - toT.normalized * wristBack;

		Vector3 ST = wristTarget - S;
		float d = Mathf.Clamp(ST.magnitude, Mathf.Abs(a - b) + 1e-3f, (a + b) * 0.999f);
		Vector3 dir = ST.normalized;

		polePos = S + right * (elbowPole.x * side) + up * elbowPole.y + fwd * elbowPole.z;
		Vector3 toPole = polePos - S;
		Vector3 bend = toPole - Vector3.Project(toPole, dir);
		if (bend.sqrMagnitude < 1e-8f) bend = (E - S) - Vector3.Project(E - S, dir);
		if (bend.sqrMagnitude < 1e-8f) return;
		bend.Normalize();

		float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
		float sinA = Mathf.Sqrt(1f - cosA * cosA);
		Vector3 elbow = S + (dir * cosA + bend * sinA) * a;

		upT.rotation = Quaternion.FromToRotation(E - S, elbow - S) * upT.rotation;
		Vector3 E2 = loT.position, W2 = hT.position;
		loT.rotation = Quaternion.FromToRotation(W2 - E2, (S + dir * d) - E2) * loT.rotation;

		Quaternion upLocalIK = upT.localRotation, loLocalIK = loT.localRotation;
		upT.localRotation = Quaternion.Slerp(upLocal0, upLocalIK, w);
		loT.localRotation = Quaternion.Slerp(loLocal0, loLocalIK, w);
	}

	/// <summary>Her facing, from the seated pose: forward = hips → knees, flattened.</summary>
	bool Facing(out Vector3 right, out Vector3 up, out Vector3 fwd)
	{
		up = Vector3.up;
		right = fwd = Vector3.zero;
		Transform hl = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
		Transform hr = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
		Transform kl = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
		Transform kr = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
		if (hl == null || hr == null || kl == null || kr == null) return false;
		Vector3 f = (kl.position + kr.position - hl.position - hr.position) * 0.5f;
		f = Vector3.ProjectOnPlane(f, up);
		if (f.sqrMagnitude < 1e-6f) return false;
		fwd = f.normalized;
		right = Vector3.Cross(up, fwd);
		return true;
	}

	bool Lap(Vector3 up, out Vector3 p)
	{
		p = Vector3.zero;
		Transform hl = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
		Transform hr = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
		Transform kl = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
		Transform kr = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
		if (hl == null || hr == null || kl == null || kr == null) return false;
		Vector3 hips = (hl.position + hr.position) * 0.5f, knees = (kl.position + kr.position) * 0.5f;
		p = Vector3.Lerp(hips, knees, lapAlongThigh) + up * lapLift;
		return true;
	}

	static float Ease(float x) => x * x * (3f - 2f * x);   // smoothstep

	void OnDrawGizmosSelected()
	{
		if (!_gizValid) return;
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(_gizL, 0.015f);
		Gizmos.DrawWireSphere(_gizR, 0.015f);
		Gizmos.color = Color.cyan;
		Gizmos.DrawWireSphere(_gizPoleL, 0.02f);
		Gizmos.DrawWireSphere(_gizPoleR, 0.02f);
	}
}
