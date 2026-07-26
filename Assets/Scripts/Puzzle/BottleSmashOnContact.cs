using UnityEngine;

/// <summary>
/// Shatters the bottle on the contact frame of Cassie's strike.
///
/// Pure presentation. It subscribes to CassieStrikeDriver.OnContact, bursts
/// shards at the bottle's world position, and hides the bottle. It does not
/// touch the guard, the KO, or any gameplay state — that path is the Play()
/// onContact callback PlayerController owns, and per spec §13 the animation
/// layer doesn't modify gameplay hooks to serve itself.
///
/// WHY A SEPARATE EVENT INSTEAD OF CHAINING THE CALLBACK
/// -----------------------------------------------------
/// The driver's Play(onContact, onComplete) takes ONE callback, supplied per
/// strike by the caller, and nulls it after firing. That's the right shape for
/// the single gameplay consumer. It's the wrong shape for presentation, where
/// several unrelated components (smash, SFX, camera shake) each want the same
/// frame and each own their own lifetime. Hence the multicast OnContact event.
/// Stacking presentation onto the gameplay callback would mean PlayerController
/// knowing about VFX, which is precisely the coupling §13 exists to prevent.
///
/// WHY smashOrigin IS A FIELD AND NOT FOUND AUTOMATICALLY
/// ------------------------------------------------------
/// The burst has to happen where the bottle IS on the contact frame, which
/// means a transform that travels with the swinging hand. HandAnchor sits on the
/// Player root rather than under the hand bone, so it doesn't move with the
/// swing — bursting there puts the glass in her lap. Rather than guess at the
/// held-visual hierarchy, this is wired in the Inspector. Drag the object that
/// visibly moves with the bottle during the swing.
///
/// ONE-SHOT BY INHERITANCE
/// -----------------------
/// No local hasSmashed guard is needed: the driver's _contactFired already makes
/// OnContact fire at most once per strike, and the strike is terminal. The local
/// guard is kept anyway, cheap insurance against a future repeatable strike —
/// same defense-in-depth as LampSmashTrigger's two idempotency paths.
/// </summary>
[RequireComponent(typeof(ShardBurst))]
public class BottleSmashOnContact : MonoBehaviour
{
	[Header("Wiring")]
	[Tooltip("The strike driver whose contact frame triggers the smash. Leave " +
		"empty to search this GameObject and its parents on Awake.")]
	[SerializeField] private CassieStrikeDriver strikeDriver;

	[Tooltip("Transform marking where the bottle is at the moment of contact. " +
		"MUST be something that travels with the swinging hand — drag the held " +
		"bottle visual itself, or a marker parented under the hand bone. Do NOT " +
		"use HandAnchor unless you've confirmed it follows the swing; it sits on " +
		"the Player root and the shards will spawn in her lap. Falls back to this " +
		"component's own transform with a warning if left empty.")]
	[SerializeField] private Transform smashOrigin;

	[Tooltip("The bottle's visual, hidden on smash. Per the same design call as " +
		"the lamp: the object is consumed into its shards, so there's no " +
		"half-broken prop left sitting in her hand. Optional — leave empty if " +
		"something else already hides it.")]
	[SerializeField] private GameObject bottleVisual;

	private ShardBurst _burst;
	private bool _hasSmashed;

	void Awake()
	{
		_burst = GetComponent<ShardBurst>();

		if (strikeDriver == null)
			strikeDriver = GetComponentInParent<CassieStrikeDriver>();

		if (strikeDriver == null)
		{
			Debug.LogWarning($"[BottleSmashOnContact] No CassieStrikeDriver found " +
				$"for '{name}'. The bottle will never smash. Wire it explicitly " +
				$"or parent this under the Player.");
		}

		if (smashOrigin == null)
		{
			Debug.LogWarning($"[BottleSmashOnContact] No smashOrigin wired on " +
				$"'{name}'. Falling back to this transform, which is almost " +
				$"certainly not where the bottle is at contact. Drag the held " +
				$"bottle visual in.");
			smashOrigin = transform;
		}
	}

	// Subscribe/unsubscribe in OnEnable/OnDisable rather than Awake/OnDestroy so
	// the component can be toggled without leaking a handler — and so a disabled
	// smash component genuinely doesn't fire.
	void OnEnable()
	{
		if (strikeDriver != null) strikeDriver.OnContact += HandleContact;
	}

	void OnDisable()
	{
		if (strikeDriver != null) strikeDriver.OnContact -= HandleContact;
	}

	private void HandleContact()
	{
		if (_hasSmashed) return;
		_hasSmashed = true;

		Vector3 pos = smashOrigin != null ? smashOrigin.position : transform.position;
		_burst.Burst(pos);

		if (bottleVisual != null) bottleVisual.SetActive(false);

		Debug.Log($"[BottleSmashOnContact] Bottle smashed at {pos}.");
	}

	[ContextMenu("Debug: Force Smash")]
	private void DebugForceSmash()
	{
		if (_hasSmashed)
		{
			Debug.Log("[BottleSmashOnContact] Already smashed. Ignoring.");
			return;
		}
		HandleContact();
	}
}
