// Assets/Editor/CassieScaleChainFix.cs
//
// Day 88 scale-chain fix. Two passes, run in order, with the Blender re-export
// between them.
//
//   PASS 1  (before re-export)  Tools/Rara/1. Revert Bone Position Overrides
//   -- re-export from Blender: Scale 1.00, Apply Scalings = All Local, Apply Unit --
//   PASS 2  (after reimport)    Tools/Rara/2. Rescale Attached Children x100
//
// WHY PASS 1: the scene instance holds ~20 bone m_LocalPosition overrides at the
// old 1/100 values (0.0015387445, 0.0035615675, ...). They are junk -- Unity
// serialized them as a side effect of posing bones; the rig writes rotation only.
// Left in place they would fight the re-exported prefab and collapse the skeleton
// to 1/100 size. Reverting them now is a visual no-op.
//
// WHY IT WALKS FROM Hips: Cassie_Rig's own localPosition override (0, -0.838, 0)
// is a deliberate grounding offset at a scale-1 level. It is correct now and stays
// correct after the re-export. Do NOT revert it. Starting the walk at Hips excludes
// Cassie_Rig and Cassie_Mesh by construction.
//
// WHY PASS 2: Hair_Mass is a plain scene object parented under the Head bone, not
// a prefab override, so pass 1 does not see it. Its transform was authored against
// lossyScale 100. When Head drops to lossyScale 1 it needs localPosition and
// localScale multiplied by 100.
//
// Rotations are never touched by either pass. They are scale-invariant; the seated
// rest pose and the authored strike Eulers survive untouched.
//
// SELECTION: the relevant prefab instance is Cassie_Blockout, which sits under
// Player -- Player is a separate prefab instance and asking it for Cassie's
// overrides silently returns nothing. ResolveRig finds Hips first and derives the
// instance root from there, so selecting Player, Cassie_Blockout, or Cassie_Rig
// all resolve to the same correct answer.
//
// Both passes default to DRY RUN. Read the console, then set DryRun false and
// re-run.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CassieScaleChainFix
{
	// Flip to false to actually mutate. Always dry-run first.
	// static readonly, not const: const folds the !DryRun branches into dead code
	// and trips CS0162.
	private static readonly bool DryRun = false;

	private const string RootBoneName = "Hips";
	private const float RescaleFactor = 100f;

	// ------------------------------------------------------------------
	// PASS 1 -- run BEFORE the Blender re-export
	// ------------------------------------------------------------------
	[MenuItem("Tools/Rara/1. Revert Bone Position Overrides")]
	private static void RevertBonePositions()
	{
		GameObject instanceRoot;
		Transform hips;
		if (!ResolveRig(out instanceRoot, out hips)) return;

		// Everything at or below Hips. Cassie_Rig and Cassie_Mesh are above it and
		// are therefore excluded -- which is the point.
		Transform[] bones = hips.GetComponentsInChildren<Transform>(true);

		// DIAGNOSTIC: raw dump of every m_LocalPosition modification on the
		// instance, independent of the per-bone query below. Note that
		// PropertyModification.target is the object in the prefab ASSET, not the
		// scene instance -- comparing it against scene Transforms never matches,
		// which is why an earlier version of this script always reported clean.
		PropertyModification[] mods = PrefabUtility.GetPropertyModifications(instanceRoot);
		int rawPosMods = 0;
		if (mods != null)
		{
			foreach (PropertyModification m in mods)
			{
				if (m.target == null) continue;
				if (!m.propertyPath.StartsWith("m_LocalPosition")) continue;
				rawPosMods++;
				Debug.Log(string.Format("[ScaleChainFix][raw] {0}.{1} = {2}   (source type {3})",
					m.target.name, m.propertyPath, m.value, m.target.GetType().Name));
			}
		}
		Debug.Log(string.Format("[ScaleChainFix] Raw m_LocalPosition modifications on instance: {0}", rawPosMods));

		// PRIMARY QUERY: ask each bone directly whether its position is overridden.
		// SerializedProperty.prefabOverride is evaluated on the instance object, so
		// there is no asset/instance identity problem. Tested per channel because a
		// composite Vector3 property does not reliably report the flag on itself.
		List<Transform> targets = new List<Transform>();
		int channelCount = 0;

		foreach (Transform t in bones)
		{
			SerializedObject so = new SerializedObject(t);
			int hits = 0;
			foreach (string ch in new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" })
			{
				SerializedProperty c = so.FindProperty(ch);
				if (c != null && c.prefabOverride) hits++;
			}

			if (hits > 0)
			{
				targets.Add(t);
				channelCount += hits;
			}
		}

		if (targets.Count == 0)
		{
			Debug.Log("[ScaleChainFix] No bone position overrides found by direct query. " +
				"If the raw count above is non-zero, stop and report both numbers.");
			return;
		}

		foreach (Transform t in targets.OrderBy(x => PathOf(instanceRoot.transform, x)))
		{
			Vector3 p = t.localPosition;
			Debug.Log(string.Format("[ScaleChainFix] {0}  {1}  ({2:F9}, {3:F9}, {4:F9})",
				DryRun ? "WOULD REVERT" : "REVERT",
				PathOf(instanceRoot.transform, t), p.x, p.y, p.z), t);

			if (!DryRun)
			{
				SerializedObject so = new SerializedObject(t);
				SerializedProperty prop = so.FindProperty("m_LocalPosition");
				PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
			}
		}

		Debug.Log(string.Format("[ScaleChainFix] Pass 1 {0}: {1} bones, {2} float channels.",
			DryRun ? "dry run complete -- set DryRun=false and re-run" : "APPLIED",
			targets.Count, channelCount));

		if (!DryRun)
		{
			EditorUtility.SetDirty(instanceRoot);
			Debug.Log("[ScaleChainFix] Save the scene, then re-export from Blender.");
		}
	}

	// ------------------------------------------------------------------
	// PASS 2 -- run AFTER the re-exported FBX has reimported
	// ------------------------------------------------------------------
	[MenuItem("Tools/Rara/2. Rescale Attached Children x100")]
	private static void RescaleAttachedChildren()
	{
		GameObject instanceRoot;
		Transform hips;
		if (!ResolveRig(out instanceRoot, out hips)) return;

		// Anything under a bone that is NOT itself part of the imported skeleton,
		// i.e. hand-parented scene objects such as Hair_Mass.
		List<Transform> attached = new List<Transform>();
		foreach (Transform t in hips.GetComponentsInChildren<Transform>(true))
		{
			if (t == hips) continue;
			if (PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) != null) continue;
			attached.Add(t);
		}

		if (attached.Count == 0)
		{
			Debug.Log("[ScaleChainFix] No hand-attached children found under the skeleton.");
			return;
		}

		foreach (Transform t in attached)
		{
			Vector3 p = t.localPosition;
			Vector3 s = t.localScale;
			Vector3 p2 = p * RescaleFactor;
			Vector3 s2 = s * RescaleFactor;

			Debug.Log(string.Format(
				"[ScaleChainFix] {0}  {1}\n    pos ({2:F9}, {3:F9}, {4:F9}) -> ({5:F6}, {6:F6}, {7:F6})\n    scl ({8:F9}, {9:F9}, {10:F9}) -> ({11:F6}, {12:F6}, {13:F6})",
				DryRun ? "WOULD RESCALE" : "RESCALE",
				PathOf(instanceRoot.transform, t),
				p.x, p.y, p.z, p2.x, p2.y, p2.z,
				s.x, s.y, s.z, s2.x, s2.y, s2.z), t);

			if (!DryRun)
			{
				Undo.RecordObject(t, "Rescale attached child");
				t.localPosition = p2;
				t.localScale = s2;
				EditorUtility.SetDirty(t);
			}
		}

		Debug.Log(string.Format("[ScaleChainFix] Pass 2 {0}: {1} objects.",
			DryRun ? "dry run complete -- set DryRun=false and re-run" : "APPLIED",
			attached.Count));
	}

	// ------------------------------------------------------------------

	// Finds Hips from whatever is selected, then derives the prefab instance root
	// from Hips rather than from the selection. Selecting Player would otherwise
	// resolve to Player's own prefab instance, whose modification list contains
	// none of Cassie's bone overrides -- a silent wrong answer.
	private static bool ResolveRig(out GameObject instanceRoot, out Transform hips)
	{
		instanceRoot = null;
		hips = null;

		GameObject sel = Selection.activeGameObject;
		if (sel == null)
		{
			Debug.LogError("[ScaleChainFix] Select Cassie_Blockout (or Player) in the Hierarchy first.");
			return false;
		}

		hips = FindDescendant(sel.transform, RootBoneName);
		if (hips == null)
		{
			Debug.LogError("[ScaleChainFix] No '" + RootBoneName + "' found under '" + sel.name +
				"'. Select Cassie_Blockout or one of its ancestors.");
			return false;
		}

		instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(hips.gameObject);
		if (instanceRoot == null)
		{
			Debug.LogError("[ScaleChainFix] '" + hips.name + "' is not part of a prefab instance.");
			return false;
		}

		Debug.Log("[ScaleChainFix] Resolved instance root: " + instanceRoot.name, instanceRoot);
		return true;
	}

	private static Transform FindDescendant(Transform root, string name)
	{
		foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
			if (t.name == name) return t;
		return null;
	}

	private static string PathOf(Transform root, Transform t)
	{
		string path = t.name;
		Transform c = t.parent;
		while (c != null && c != root)
		{
			path = c.name + "/" + path;
			c = c.parent;
		}
		return path;
	}
}
