 using UnityEngine;

public enum BondType
{
	Rope,
	ZipTie,
	Tape,
	Handcuffs,
	Chain
}

public enum ToolType
{
	BareHands,
	Blade,
	Point,
	Key,
	BruteForce
}

public class Bond : MonoBehaviour
{
	[Header("Bond Config")]
	[SerializeField] private BondType bondType = BondType.Rope;
	[SerializeField] private int bondStrength = 25;
	[SerializeField] private int struggleProgress = 0;

	[Header("Tool Compatibility")]
	[Tooltip("Progress amount granted when struggling with a blade tool.")]
	[SerializeField] private int bladeProgress = 10;
	[Tooltip("Progress amount granted when struggling with a point tool (nail, shard).")]
	[SerializeField] private int pointProgress = 5;
	[Tooltip("Progress amount granted when struggling with a key (usually for handcuffs).")]
	[SerializeField] private int keyProgress = 0;

	public BondType BondType => bondType;
	public int BondStrength => bondStrength;
	public int StruggleProgress => struggleProgress;
	public bool IsBroken => struggleProgress >= bondStrength;

	public System.Action OnProgressChanged;
	public System.Action OnBroken;

	public int GetStruggleProgress(ToolType tool)
	{
		return tool switch
		{
			// Day 31: barehand Struggle produces no bond progress in v0. Pick Up
			// is the gating verb; Struggle is the closing verb. PlayerController
			// still routes barehand attempts through ApplyStruggle so effort SFX
			// + rejection shake fire ("you tried" feedback, same pattern as L4
			// prone-kick suppression). See GDD "Verb roles in the puzzle loop"
			// and ideas.md "Day 30 Playtest" / "Future-iteration note: Struggle
			// as a real verb." Do NOT make this configurable without revisiting
			// the design — the Day 30 playtest cascade was caused by barehand
			// progress being non-zero.
			ToolType.BareHands => 0,
			ToolType.Blade => bladeProgress,
			ToolType.Point => pointProgress,
			ToolType.Key => keyProgress,
			ToolType.BruteForce => 0,
			_ => 0
		};
	}

	public void ApplyStruggle(int amount)
	{
		if (IsBroken) return;

		if (amount <= 0)
		{
			Debug.Log($"Bond ({bondType}): struggle had no effect. Need a better tool.");
			return;
		}

		struggleProgress += amount;
		OnProgressChanged?.Invoke();
		Debug.Log($"Bond ({bondType}): +{amount} (total {struggleProgress}/{bondStrength})");

		if (IsBroken)
		{
			OnBroken?.Invoke();
		}
	}

	/// <summary>
	/// Zero out cut progress. The failure loop calls this on re-bind: the guard
	/// re-ties Cassie, so any partial cut is gone — fresh rope. Fires
	/// OnProgressChanged so the struggle-progress UI clears with it.
	/// </summary>
	public void ResetProgress()
	{
		struggleProgress = 0;
		OnProgressChanged?.Invoke();
	}
}