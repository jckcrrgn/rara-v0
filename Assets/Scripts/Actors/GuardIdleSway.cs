// GuardIdleSway.cs — Rara Day 216. The guard is one unrigged mesh, so while he stands
// watching he was stock still. This gives him a slow weight shift and a breath.
// Day 216 tune: 1.5 deg roll + 0.6 deg rock read seasick / drunk. Two axes on different
// periods trace a wandering loop; one axis reads as a man shifting his weight. Rock kept
// as an option at 0.
//
// Moves ONLY his model children (Guard_D133, Guard_D133_Lean): local rotation, scale and
// the position offset that keeps his feet planted. Never the Guard transform itself:
// GuardController walks and turns that, and StrikeableGuard crumples it.
//
// Pivots at his FEET, found from each mesh's bounds (lowest point, centred), so it doesn't
// matter where the model's own pivot sits.
//
// Runs only while he's standing still; fades in/out over fadeTime. Off for good once struck.
// Periods are deliberately unrelated to each other (no visible loop) and slow, so nothing
// here reads as the 0.7 s struggle beat.
//
// SETUP: add to the Guard object (the GuardController's guardBody). Models empty = every
// direct child with a MeshFilter.

using UnityEngine;

public class GuardIdleSway : MonoBehaviour
{
	[Tooltip("Model children to sway. Empty = every direct child with a MeshFilter.")]
	public Transform[] models;

	[Header("Weight shift (side to side)")]
	public float rollDegrees = 0.5f;
	public float rollPeriod = 4.3f;

	[Header("Rock (forward / back)")]
	public float pitchDegrees = 0f;
	public float pitchPeriod = 3.1f;

	[Header("Breath (taller)")]
	[Tooltip("0.005 = 0.5% taller at the top of the breath.")]
	public float breath = 0.005f;
	public float breathPeriod = 3.7f;

	[Header("Gating")]
	[Tooltip("Seconds to fade in when he stops, and out when he moves.")]
	[Min(0.01f)] public float fadeTime = 0.4f;
	[Tooltip("He counts as still below this speed (m/s)...")]
	public float stillSpeed = 0.05f;
	[Tooltip("...and below this turn rate (deg/s).")]
	public float stillTurn = 10f;

	struct Model
	{
		public Transform t;
		public Vector3 pos, scale;
		public Quaternion rot;
		public Vector3 foot;     // feet, in this transform's local space
		public Vector3 footIn;   // the same point, in the model's own local space
		public int upAxis;       // the model-local axis closest to this transform's up
	}

	Model[] m;
	StrikeableGuard strike;
	Vector3 lastPos;
	Quaternion lastRot;
	float weight;
	float phaseRoll, phasePitch, phaseBreath;

	void Start()
	{
		strike = GetComponent<StrikeableGuard>();

		if (models == null || models.Length == 0)
		{
			var list = new System.Collections.Generic.List<Transform>();
			foreach (Transform c in transform)
				if (c.GetComponent<MeshFilter>() != null) list.Add(c);
			models = list.ToArray();
		}

		m = new Model[models.Length];
		for (int i = 0; i < models.Length; i++)
		{
			Transform t = models[i];
			if (t == null) continue;
			Model x = new Model { t = t, pos = t.localPosition, rot = t.localRotation, scale = t.localScale };
			x.foot = FindFeet(t);
			x.footIn = Inverse(x).MultiplyPoint3x4(x.foot);
			x.upAxis = UpAxis(t);
			m[i] = x;
		}

		// Random phases so restarts and the two models never look canned.
		phaseRoll = Random.value * rollPeriod;
		phasePitch = Random.value * pitchPeriod;
		phaseBreath = Random.value * breathPeriod;

		lastPos = transform.position;
		lastRot = transform.rotation;
	}

	void Update()
	{
		if (m == null) return;
		float dt = Time.deltaTime;

		bool still = false;
		if (dt > 0f)
		{
			float speed = (transform.position - lastPos).magnitude / dt;
			float turn = Quaternion.Angle(transform.rotation, lastRot) / dt;
			still = speed < stillSpeed && turn < stillTurn;
		}
		lastPos = transform.position;
		lastRot = transform.rotation;

		bool struck = strike != null && strike.IsStruck;
		if (struck) weight = 0f;   // the crumple owns him from frame one
		else weight = Mathf.MoveTowards(weight, still ? 1f : 0f, dt / fadeTime);

		float t = Time.time;
		float roll = rollDegrees * weight * Wave(t + phaseRoll, rollPeriod);
		float pitch = pitchDegrees * weight * Wave(t + phasePitch, pitchPeriod);
		// Breath only swells (0..1), never shrinks below his authored height.
		float swell = breath * weight * 0.5f * (1f + Wave(t + phaseBreath, breathPeriod));

		// Sway axes in this transform's space. If his model faces off this transform's forward,
		// roll and pitch trade places: still a sway, just swap the two amplitudes if it reads wrong.
		Quaternion q = Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.AngleAxis(pitch, Vector3.right);

		for (int i = 0; i < m.Length; i++)
		{
			Model x = m[i];
			if (x.t == null) continue;

			Vector3 s = x.scale;
			s[x.upAxis] *= 1f + swell;
			Quaternion r = q * x.rot;

			// Keep the foot point fixed: pos + r * (s ⊙ footIn) = foot.
			x.t.localRotation = r;
			x.t.localScale = s;
			x.t.localPosition = x.foot - r * Vector3.Scale(s, x.footIn);
		}
	}

	void OnDisable()
	{
		if (m == null) return;
		foreach (var x in m)
		{
			if (x.t == null) continue;
			x.t.localPosition = x.pos;
			x.t.localRotation = x.rot;
			x.t.localScale = x.scale;
		}
		weight = 0f;
	}

	static float Wave(float t, float period) =>
		Mathf.Sin(t / Mathf.Max(0.01f, period) * Mathf.PI * 2f);

	Matrix4x4 Inverse(Model x) => Matrix4x4.TRS(x.pos, x.rot, x.scale).inverse;

	/// Lowest point of the mesh bounds, centred under him, in this transform's space.
	/// Reads the shared mesh, so it works on the inactive lean model too.
	Vector3 FindFeet(Transform t)
	{
		var mf = t.GetComponent<MeshFilter>();
		if (mf == null || mf.sharedMesh == null) return t.localPosition;
		Bounds b = mf.sharedMesh.bounds;
		Matrix4x4 toHere = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);

		float minY = float.MaxValue;
		Vector3 sum = Vector3.zero;
		for (int i = 0; i < 8; i++)
		{
			Vector3 c = b.center + Vector3.Scale(b.extents,
				new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
			Vector3 p = toHere.MultiplyPoint3x4(c);
			minY = Mathf.Min(minY, p.y);
			sum += p;
		}
		Vector3 centre = sum / 8f;
		return new Vector3(centre.x, minY, centre.z);
	}

	/// Which of the model's local axes points closest to this transform's up
	/// (Blender exports are rotated -90 on X, so it's often Z, not Y).
	static int UpAxis(Transform t)
	{
		Vector3 up = Quaternion.Inverse(t.localRotation) * Vector3.up;
		up = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
		return up.x > up.y ? (up.x > up.z ? 0 : 2) : (up.y > up.z ? 1 : 2);
	}
}
