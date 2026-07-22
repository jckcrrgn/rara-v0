using UnityEngine;

/// <summary>
/// Base for a Cassie presentation layer (spec §13). A layer CLAIMS the bones it
/// animates and CONTRIBUTES additive offsets each frame; the shared CassieRig
/// composes all layers and applies one write per bone (see CassieRig for the mold).
///
/// Replaces the old self-applying CassieRigDriver — needed once drivers layer
/// (Struggle over Sit), since two self-appliers on the same bone clobber.
///
/// A concrete layer implements:
///   DeclareBones() — claim the bones it touches (called once, at Awake).
///   Contribute(dt) — push this frame's offsets via AddLocalEuler / AddOffset.
///
/// Sit is the always-on state layer; Struggle / Wrist-break / Arm-and-conceal /
/// Strike are event-fired beat layers over the same base.
/// </summary>
public abstract class CassieRigLayer : MonoBehaviour
{
	[Header("Rig")]
	[Tooltip("The CassieRig applier. If unassigned, resolves from this object, then " +
		"parents, then children.")]
	[SerializeField] protected CassieRig rig;

	[Tooltip("Composition order — lower contributes first. Only matters when two " +
		"layers rotate the same bone on the same axis; for small offsets the " +
		"difference is negligible. Sit sits at 0; put beats above it (e.g. 1).")]
	[SerializeField] private int order = 0;

	public int Order => order;

	protected virtual void Awake()
	{
		ResolveRig();
		if (rig != null) DeclareBones();
	}

	private void ResolveRig()
	{
		if (rig == null) rig = GetComponent<CassieRig>();
		if (rig == null) rig = GetComponentInParent<CassieRig>();
		if (rig == null) rig = GetComponentInChildren<CassieRig>();

		if (rig == null)
			Debug.LogError($"[{GetType().Name}] No CassieRig found — layer is inert. " +
				"Add a CassieRig component to Cassie_Blockout.");
	}

	protected virtual void OnEnable()
	{
		if (rig != null) rig.Register(this);
	}

	protected virtual void OnDisable()
	{
		if (rig != null) rig.Unregister(this);
	}

	/// <summary>Claim the bones this layer animates (via Declare). Called once, at Awake.</summary>
	protected abstract void DeclareBones();

	/// <summary>Push this frame's offsets. Called by CassieRig each LateUpdate.</summary>
	public abstract void Contribute(float dt);

	// Passthroughs so concrete layers read cleanly.
	protected void Declare(HumanBodyBones b) { if (rig != null) rig.Claim(b); }
	protected void AddLocalEuler(HumanBodyBones b, float x, float y, float z) { if (rig != null) rig.AddLocalEuler(b, x, y, z); }
	protected void AddOffset(HumanBodyBones b, Quaternion q) { if (rig != null) rig.AddOffset(b, q); }
}
