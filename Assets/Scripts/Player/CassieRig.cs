using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The single applier for Cassie's VS presentation layers (spec §13). Owns the
/// Humanoid Animator, captures each driven bone's REST rotation once, and every
/// LateUpdate composes the offsets contributed by all active CassieRigLayers into
/// one write per bone.
///
/// WHY A COORDINATOR (the mold, revised Day 77)
/// --------------------------------------------
/// The drivers LAYER — Sit's breathing runs while a Struggle beat plays on top,
/// and later Wrist-break / Arm-and-conceal / Strike ride over Sit the same way.
/// If each driver reset-and-wrote its own bones in its own LateUpdate, two drivers
/// on the same bone (Sit + Struggle both want Spine) would clobber each other,
/// last-to-run wins. So there is ONE applier:
///
///   1. Layers CLAIM the bones they touch (rest captured once, from the authored
///      seated pose at Awake).
///   2. Each LateUpdate the rig clears offsets, asks every active layer to
///      CONTRIBUTE (additive offsets that compose), then writes bone = rest *
///      offset ONCE per bone. Non-accumulating and layer-safe.
///   3. Bones with no contribution this frame fall back to rest.
///
/// Put this on Cassie_Blockout (the object the Animator/Avatar resolves from),
/// alongside the layer components (CassieSitDriver, CassieStruggleDriver, ...).
/// </summary>
public class CassieRig : MonoBehaviour
{
	[Header("Rig")]
	[Tooltip("Cassie's Humanoid Animator (mapped Avatar). If unassigned, resolves " +
		"from this object, then parents, then children.")]
	[SerializeField] private Animator animator;

	private readonly Dictionary<HumanBodyBones, Transform> _bone = new Dictionary<HumanBodyBones, Transform>();
	private readonly Dictionary<HumanBodyBones, Quaternion> _rest = new Dictionary<HumanBodyBones, Quaternion>();
	private readonly Dictionary<HumanBodyBones, Quaternion> _offset = new Dictionary<HumanBodyBones, Quaternion>();
	private readonly List<CassieRigLayer> _layers = new List<CassieRigLayer>();

	private void Awake() => EnsureAnimator();

	private void EnsureAnimator()
	{
		if (animator != null) return;
		animator = GetComponent<Animator>();
		if (animator == null) animator = GetComponentInParent<Animator>();
		if (animator == null) animator = GetComponentInChildren<Animator>();

		if (animator == null)
			Debug.LogError("[CassieRig] No Animator found — presentation layers are inert.");
		else if (!animator.isHuman)
			Debug.LogError($"[CassieRig] Animator '{animator.name}' is not Humanoid — " +
				"GetBoneTransform returns null. Map the Avatar as Humanoid.");
	}

	/// <summary>
	/// Claim a bone for animation: resolve it on the Humanoid rig and capture its
	/// current localRotation as REST. Idempotent — the first claim wins the rest
	/// capture, so multiple layers can claim the same bone safely. Returns the bone
	/// Transform, or null if absent on the rig (missing bones degrade gracefully —
	/// all offsets against them no-op). Call from a layer's DeclareBones (Awake),
	/// before anything drives the pose, so rest is the authored seated pose.
	/// </summary>
	public Transform Claim(HumanBodyBones b)
	{
		if (_bone.TryGetValue(b, out Transform existing)) return existing;

		EnsureAnimator();
		Transform t = animator != null ? animator.GetBoneTransform(b) : null;
		if (t == null)
		{
			Debug.LogWarning($"[CassieRig] Bone {b} not present on rig — skipping. " +
				"(Expected on a minimal blockout; layers degrade gracefully.)");
			return null;
		}

		_bone[b] = t;
		_rest[b] = t.localRotation;   // authored (seated) pose, captured once
		return t;
	}

	public void Register(CassieRigLayer layer)
	{
		if (layer == null || _layers.Contains(layer)) return;
		_layers.Add(layer);
		_layers.Sort(CompareLayerOrder);   // keep ascending Order; register is rare
	}

	public void Unregister(CassieRigLayer layer) => _layers.Remove(layer);

	private static int CompareLayerOrder(CassieRigLayer a, CassieRigLayer b)
		=> a.Order.CompareTo(b.Order);

	/// <summary>Add a rotation offset to a claimed bone for this frame (composes).</summary>
	public void AddOffset(HumanBodyBones b, Quaternion q)
	{
		if (!_bone.ContainsKey(b)) return;
		_offset[b] = _offset.TryGetValue(b, out Quaternion cur) ? cur * q : q;
	}

	/// <summary>Add a local-space Euler offset (degrees) to a claimed bone this frame.</summary>
	public void AddLocalEuler(HumanBodyBones b, float x, float y, float z)
		=> AddOffset(b, Quaternion.Euler(x, y, z));

	private void LateUpdate()
	{
		if (animator == null) return;

		// 1. Clear last frame's offsets.
		_offset.Clear();

		// 2. Every active layer contributes, in ascending Order.
		float dt = Time.deltaTime;
		for (int i = 0; i < _layers.Count; i++)
		{
			CassieRigLayer layer = _layers[i];
			if (layer != null && layer.isActiveAndEnabled) layer.Contribute(dt);
		}

		// 3. Apply: each claimed bone = rest * composed offset. One write per bone,
		//    non-accumulating. Bones with no contribution snap back to rest.
		foreach (KeyValuePair<HumanBodyBones, Transform> kv in _bone)
		{
			Quaternion off = _offset.TryGetValue(kv.Key, out Quaternion q) ? q : Quaternion.identity;
			kv.Value.localRotation = _rest[kv.Key] * off;
		}
	}
}
