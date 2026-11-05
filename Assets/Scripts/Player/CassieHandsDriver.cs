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
/// FOLD (Day 209): seated, a fixed lean leaves the ankle knot ~0.2 m out of reach. Now the
/// reach decides the lean: PostApply measures how far the shoulders must tip for both wrists
/// to land, Contribute eases the spine + chest pitch there next frame (cap maxFoldPitch). The
/// deep fold is the tell: the further she reaches, the more it shows. Comes back inside
/// handsBackTime with the hands.
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

	[Header("Fold (Day 209: the reach decides the lean)")]
	[Tooltip("Total forward pitch cap (spine + chest, degrees) at a knot. The two pitches above " +
		"are the MINIMUM lean and the spine/chest split. Past the minimum she folds exactly as far " +
		"as the knot needs, up to this.")]
	[SerializeField] private float maxFoldPitch = 60f;
	[Tooltip("Metres short of a dead-straight arm the fold aims for. Keeps a little elbow bend.")]
	[SerializeField] private float reachMargin = 0.03f;
	[Tooltip("Day 209: share of the fold PAST the minimum lean that goes to the pelvis (hip " +
		"flexion), not the spine. People fold at the hip. The thighs are counter-rotated so the " +
		"legs and the seat contact stay put. 0 = old spine-only fold.")]
	[Range(0f, 1f)] [SerializeField] private float hipFoldShare = 0.6f;

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
	private float _fold;          // eased extra pitch past the minimum, degrees
	private float _foldWanted;    // what the last PostApply asked for
	private float _applied;       // total pitch applied this frame (hips + spine + chest)
	private Quaternion _hipsRest;                       // seated rest local rotation
	private Vector3 _hipsRestPos;                       // seated rest local position
	private Quaternion _legRelL, _legRelR;              // thigh world rotation, in the Hips PARENT's frame
	private Vector3 _jointsRel;                         // hip-joint midpoint, in the Hips parent's frame
	private float _hipsDeg;                             // pelvis pitch applied this frame
	private bool _hipsOk;
	private Vector3 _gizL, _gizR, _gizPoleL, _gizPoleR;
	private bool _gizValid;

	protected override void DeclareBones()
	{
		Declare(HumanBodyBones.Hips);             // Day 209: the fold at the hip
		Declare(HumanBodyBones.LeftUpperLeg);     // counter-rotated: legs stay put
		Declare(HumanBodyBones.RightUpperLeg);
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

		// Same moment CassieRig captures rest (Declare, above): the authored seated pose.
		if (animator != null)
		{
			Transform h = animator.GetBoneTransform(HumanBodyBones.Hips);
			Transform l = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
			Transform r = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
			_hipsOk = h != null && l != null && r != null && h.parent != null;
			if (_hipsOk)
			{
				// Rest, measured in the frame of whatever the pelvis hangs from (not driven by
				// any layer). Pinning to this frame is independent of what bones sit between
				// Hips and the thighs.
				Transform hp = h.parent;
				_hipsRest = h.localRotation;
				_hipsRestPos = h.localPosition;
				_legRelL = Quaternion.Inverse(hp.rotation) * l.rotation;
				_legRelR = Quaternion.Inverse(hp.rotation) * r.rotation;
				_jointsRel = hp.InverseTransformPoint((l.position + r.position) * 0.5f);
			}
		}
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

		// Fold: eases toward what the reach asked for last frame. Comes back on the same clock
		// as the hands (handsBackTime while hiding), so the fold never outlives the hide budget.
		float baseP = reachSpinePitch + reachChestPitch;
		float foldMax = Mathf.Max(0f, maxFoldPitch - baseP);
		float foldGoal = WantOut && AtKnot ? Mathf.Clamp(_foldWanted, 0f, foldMax) : 0f;
		_fold = Mathf.MoveTowards(_fold, foldGoal, Mathf.Max(foldMax, 1f) * dt / Mathf.Max(0.01f, time));

		float e = Ease(_weight) * _lean;
		_applied = 0f;
		_hipsDeg = 0f;
		if (e <= 0.0001f) return;
		float total = (baseP + _fold) * e;
		float hipsDeg = HipsFold(_fold * e * hipFoldShare);
		_hipsDeg = hipsDeg;
		float torso = total - hipsDeg;
		float spineShare = baseP > 0.01f ? reachSpinePitch / baseP : 0.67f;
		AddLocalEuler(HumanBodyBones.Spine, torso * spineShare, 0f, 0f);
		AddLocalEuler(HumanBodyBones.Chest, torso * (1f - spineShare), 0f, 0f);
		_applied = total;
	}

	/// <summary>
	/// Day 209. Pitches the pelvis forward `deg` about her right axis, in WORLD space (so the
	/// Hips bone's own axes don't matter). The legs are put back in PostApply (PinLegs).
	/// Returns the degrees actually applied (0 if it couldn't).
	/// </summary>
	float HipsFold(float deg)
	{
		if (deg <= 0.0001f || !_hipsOk || animator == null) return 0f;
		if (!Facing(out Vector3 right, out _, out _)) return 0f;
		Transform h = animator.GetBoneTransform(HumanBodyBones.Hips);
		Quaternion parent = h.parent != null ? h.parent.rotation : Quaternion.identity;
		Quaternion w0 = parent * _hipsRest;
		Quaternion q = Quaternion.Inverse(w0) * Quaternion.AngleAxis(deg, right) * w0;   // world pitch → hips local
		AddOffset(HumanBodyBones.Hips, q);
		return deg;
	}

	/// <summary>
	/// Day 209, v2. Runs first in PostApply, on the written pose. The pelvis turned about the
	/// Hips bone, which isn't at the hip joints, and the thighs turned with it. Undo both:
	/// slide the pelvis so the hip-joint midpoint is back at rest, then set each thigh's WORLD
	/// rotation back to rest. Legs, knees and feet end exactly where they were. Hips position
	/// is rewritten from rest every frame, so nothing accumulates.
	/// </summary>
	void PinLegs()
	{
		if (!_hipsOk || animator == null) return;
		Transform h = animator.GetBoneTransform(HumanBodyBones.Hips);
		Transform l = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
		Transform r = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
		Transform hp = h.parent;
		h.localPosition = _hipsRestPos;
		if (_hipsDeg <= 0.0001f) return;

		Vector3 want = hp.TransformPoint(_jointsRel);
		Vector3 now = (l.position + r.position) * 0.5f;
		h.position += want - now;
		l.rotation = hp.rotation * _legRelL;
		r.rotation = hp.rotation * _legRelR;
	}

	/// <summary>
	/// Day 209. How much MORE pitch (degrees, can be negative) the shoulders need so both wrists
	/// reach their targets. Rotates the measured shoulders about the Spine bone around her right
	/// axis and bisects for the smallest pitch that reaches. Treats the chest share as pivoting
	/// at the spine too, which overshoots a little; next frame re-measures, so it self-corrects.
	/// </summary>
	float FoldDelta(Vector3 tL, Vector3 tR, Vector3 right, float baseP)
	{
		Transform sp = animator.GetBoneTransform(HumanBodyBones.Spine);
		if (sp == null) return 0f;
		// Pivot between spine and the hip joints by the hip share: the fold bends at both.
		Transform tl = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
		Transform tr = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
		Vector3 joints = tl != null && tr != null ? (tl.position + tr.position) * 0.5f : sp.position;
		Vector3 P = Vector3.Lerp(sp.position, joints, hipFoldShare);
		if (!Arm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, out Vector3 sL, out float rL)) return 0f;
		if (!Arm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, out Vector3 sR, out float rR)) return 0f;

		bool Reaches(float deg)
		{
			Quaternion q = Quaternion.AngleAxis(deg, right);   // + tips the shoulders forward
			Vector3 l = P + q * (sL - P), r = P + q * (sR - P);
			return (tL - l).magnitude - wristBack <= rL - reachMargin
				&& (tR - r).magnitude - wristBack <= rR - reachMargin;
		}

		float lo = baseP - _applied, hi = maxFoldPitch - _applied;
		if (hi <= lo) return lo;
		if (Reaches(lo)) return lo;
		if (!Reaches(hi)) return hi;
		for (int k = 0; k < 12; k++)
		{
			float mid = (lo + hi) * 0.5f;
			if (Reaches(mid)) hi = mid; else lo = mid;
		}
		return hi;
	}

	bool Arm(HumanBodyBones upB, HumanBodyBones loB, HumanBodyBones handB, out Vector3 shoulder, out float reach)
	{
		shoulder = Vector3.zero; reach = 0f;
		Transform u = animator.GetBoneTransform(upB), l = animator.GetBoneTransform(loB), h = animator.GetBoneTransform(handB);
		if (u == null || l == null || h == null) return false;
		shoulder = u.position;
		reach = (l.position - u.position).magnitude + (h.position - l.position).magnitude;
		return true;
	}

	// ---- PostApply: the IK, on the final pose ---------------------------------------

	public override void PostApply(float dt)
	{
		_gizValid = false;
		PinLegs();   // before anything reads the pose
		float w = Ease(_weight);
		if (w <= 0.0001f || animator == null) return;
		if (!Facing(out Vector3 right, out Vector3 up, out Vector3 fwd)) return;

		Vector3 center;
		Vector3 half = right * (handSpread * 0.5f);
		int sel = struggle != null ? struggle.Selected : -1;
		if (AtKnot && struggle.TryAnchorWorld(sel, out Vector3 anchor))
		{
			center = anchor + right * knotOffset.x + up * knotOffset.y + fwd * knotOffset.z;
			// Measured before the tug, so a tug never pumps the fold.
			float baseP = reachSpinePitch + reachChestPitch;
			_foldWanted = Mathf.Max(0f, _applied + FoldDelta(center - half, center + half, right, baseP) - baseP);
		}
		else
		{
			_foldWanted = 0f;
			if (!RealOut || !Lap(up, out center)) return;   // lap only for real hands-out
		}

		if (_workT < 1f) center += _workDir * workAmplitude * Mathf.Sin(Mathf.PI * _workT);

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
