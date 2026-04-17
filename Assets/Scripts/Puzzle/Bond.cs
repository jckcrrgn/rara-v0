using UnityEngine;

public class Bond : MonoBehaviour
{
	[Header("Bond Stats")]
	[SerializeField] private int bondStrength = 25;
	[SerializeField] private int struggleProgress = 0;
	[SerializeField] private int bareHandsStruggleAmount = 1;

	public int BondStrength => bondStrength;
	public int StruggleProgress => struggleProgress;
	public int BareHandsStruggleAmount => bareHandsStruggleAmount;
	public bool IsBroken => struggleProgress >= bondStrength;

	public System.Action OnProgressChanged;
	public System.Action OnBroken;

	public void ApplyStruggle(int amount)
	{
		if (IsBroken) return;

		struggleProgress += amount;
		OnProgressChanged?.Invoke();
		Debug.Log($"Bond: +{amount} (total {struggleProgress}/{bondStrength})");

		if (IsBroken)
		{
			OnBroken?.Invoke();
		}
	}
}