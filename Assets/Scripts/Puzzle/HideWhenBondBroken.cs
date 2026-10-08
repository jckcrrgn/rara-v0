using UnityEngine;

/// <summary>
/// Turns a list of objects off while the bond is broken, and back on if the
/// guard re-binds her. Built for the two Rope_Wrists objects, which nothing in
/// VS_Demo references and which otherwise ride her forearm through the strike.
///
/// Lives on an always-active object (SliceCoordinator), NOT on the rope itself:
/// a script on an object it deactivates can never turn that object back on.
///
/// Polls the same two PlayerController properties BondMeterUI reads, so it
/// needs no new gameplay hook.
/// </summary>
public class HideWhenBondBroken : MonoBehaviour
{
	[SerializeField] private PlayerController player;

	[Tooltip("Objects shown only while she is bound. Drag Rope_Wrists_D154 and " +
		"Rope_Wrists_D154_B here.")]
	[SerializeField] private GameObject[] targets;

	void Start()
	{
		if (player == null)
			player = FindFirstObjectByType<PlayerController>();
	}

	void LateUpdate()
	{
		if (player == null || targets == null) return;

		bool bound = player.StruggleProgress < player.BondStrength;

		for (int i = 0; i < targets.Length; i++)
		{
			GameObject t = targets[i];
			if (t != null && t.activeSelf != bound) t.SetActive(bound);
		}
	}
}
