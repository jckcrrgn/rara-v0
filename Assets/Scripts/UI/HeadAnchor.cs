// HeadAnchor.cs — Day 215. Keeps a mutter anchor just above a character's head, every
// frame, so floating lines start at the speaker instead of a hand-placed point.
//
// Humanoid rig (Cassie): the Head bone, plus headBoneToTop to reach the crown.
// Anything else (the guard blockout): the top of the character's visible renderers.
// Both follow leans, hops and turns.
//
// SETUP: on the speaker's MutterSystem worldAnchor object, parented under the character.
// `character` empty = the parent.

using UnityEngine;

[DefaultExecutionOrder(-100)]   // before MutterSystem reads the anchor in LateUpdate
public class HeadAnchor : MonoBehaviour
{
	[Tooltip("Character to track. Empty = this object's parent.")]
	public Transform character;
	[Tooltip("Gap between the top of the head and the bottom of the text, in metres.")]
	public float lift = 0.08f;
	[Tooltip("Humanoid only: Head bone to the crown, in metres.")]
	public float headBoneToTop = 0.2f;

	Transform headBone;
	Renderer[] renderers;

	void Awake()
	{
		if (character == null) character = transform.parent;
		if (character == null) return;

		foreach (var anim in character.GetComponentsInChildren<Animator>(true))
		{
			if (!anim.gameObject.activeInHierarchy || !anim.isHuman) continue;
			headBone = anim.GetBoneTransform(HumanBodyBones.Head);
			if (headBone != null) break;
		}
		if (headBone == null) renderers = character.GetComponentsInChildren<Renderer>(true);
	}

	void LateUpdate()
	{
		if (headBone != null && headBone.gameObject.activeInHierarchy)
		{
			transform.position = headBone.position + Vector3.up * (headBoneToTop + lift);
			return;
		}
		if (renderers == null) return;

		bool any = false;
		Bounds b = default;
		foreach (var r in renderers)
		{
			if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
			if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
			if (!any) { b = r.bounds; any = true; }
			else b.Encapsulate(r.bounds);
		}
		if (any) transform.position = new Vector3(b.center.x, b.max.y + lift, b.center.z);
	}
}
