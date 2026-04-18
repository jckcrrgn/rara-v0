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
	[Tooltip("Progress amount granted by bare-hands struggle. Set to 0 for bonds that require tools (zip ties, handcuffs).")]
	[SerializeField] private int bareHandsProgress = 1;
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
			ToolType.BareHands => bareHandsProgress,
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
}