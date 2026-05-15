using UnityEngine;

/// <summary>
/// Fires a mutter when a tagged Player collider enters this trigger. Wraps
/// MutterSystem.Play() so individual GameObjects in the world can author their
/// own contextual mutters without LevelManager having to know about them.
///
/// USAGE
/// -----
/// 1. Create an empty GameObject in the scene.
/// 2. Add a Collider (Sphere or Box typically), check Is Trigger.
/// 3. Add this component, write the mutter content into the inspector field.
/// 4. Make sure the Player GameObject is tagged "Player".
///
/// FIRE-ONCE vs REPEAT
/// -------------------
/// fireOnce defaults true: most teaching mutters should fire on first encounter
/// and never again. After firing, the component disables itself so subsequent
/// entries are no-ops. The GameObject stays in the scene for inspector
/// visibility and easy reset between playtests.
///
/// IMPORTANT: "fired" means MutterSystem.Play() actually started a mutter, not
/// just that we called it. If a mutter is already active when the player walks
/// in, Play() returns false and the trigger does NOT self-disable — the player
/// will exit and re-enter and the trigger will retry. This matters because
/// otherwise a poorly-timed entry could silently consume a teaching mutter.
///
/// fireOnce = false: every entry calls Play(). MutterSystem itself handles the
/// "already active" case by dropping the call, so straddling-the-boundary spam
/// is naturally absorbed. No cooldown is needed for v0.
///
/// COMPOSING WITH OTHER MUTTERS
/// ----------------------------
/// MutterSystem queues new mutters while one is active (FIFO, cap 3,
/// drop-newest on overflow; Day 37). Sequential triggers fire cleanly:
/// the second mutter waits its turn, even if the player walks into both
/// triggers without dismissing the first. Play() still returns false ONLY
/// if the queue cap is hit — which is the case the fire-once gate cares
/// about. Normal level pacing won't fill the queue (cap 3 is generous);
/// the queue is there to support paired sequences like L6 Beat 6's
/// guard-then-Cassie. For author-controlled paired mutters, call
/// MutterSystem.Play() twice in the same frame and they'll play in
/// order.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class MutterTrigger : MonoBehaviour
{
	[Header("Mutter Content")]
	[Tooltip("The mutter line that fires when the player enters this trigger. " +
		"Leave empty for no mutter (the trigger becomes a no-op — useful for " +
		"temporarily disabling a mutter without removing the GameObject). " +
		"Quality bar: read it back in the detective's voice. If it sounds " +
		"like a tutorial popup, rewrite. Reference the L4 entry mutter " +
		"(\"...the door — if I can just turn around.\") as the bar.")]
	[TextArea(2, 4)]
	[SerializeField] private string mutterContent;

	[Header("Trigger Behavior")]
	[Tooltip("If true (default), the trigger fires once and then disables itself. " +
		"Use for first-time teaching mutters. If false, the trigger fires every " +
		"time the player enters — useful for ambient location-based mutters or " +
		"recurring beats. Note: 'fires' means MutterSystem actually started a " +
		"mutter; if one is already active, the trigger does not consume its " +
		"fire-once charge.")]
	[SerializeField] private bool fireOnce = true;

	[Tooltip("Tag the trigger filters on. Defaults to 'Player'. Change only if " +
		"future content needs non-player triggers (e.g. a guard's reaction mutter).")]
	[SerializeField] private string triggeringTag = "Player";

	private bool hasFired = false;

	void Reset()
	{
		// When the component is first added in the editor, force the collider
		// to be a trigger. Convenience -- prevents the "I added the component
		// and it didn't work" bug where the collider was non-trigger.
		Collider col = GetComponent<Collider>();
		if (col != null) col.isTrigger = true;
	}

	void OnTriggerEnter(Collider other)
	{
		if (hasFired) return;
		if (!other.CompareTag(triggeringTag)) return;
		if (string.IsNullOrEmpty(mutterContent)) return;

		if (MutterSystem.Instance == null)
		{
			Debug.LogWarning($"MutterTrigger '{name}': no MutterSystem in scene; mutter not played.", this);
			return;
		}

		bool played = MutterSystem.Instance.Play(mutterContent);
		if (!played) return; // Mutter dropped (another active); do NOT consume fire-once.

		if (fireOnce)
		{
			hasFired = true;
			// Disable the component, not the GameObject. GameObject stays in
			// the scene for inspector visibility and so any child colliders /
			// gizmos remain. Re-enabling the component (or calling ResetFire)
			// re-arms the trigger.
			enabled = false;
		}
	}

	/// <summary>
	/// Re-arm a fired trigger. Useful for testing iterations on mutter content
	/// without restarting the scene, or for level designs where a trigger
	/// should re-fire after some external event.
	/// </summary>
	public void ResetFire()
	{
		hasFired = false;
		enabled = true;
	}

	void OnDrawGizmos()
	{
		// Visualize the trigger zone in the editor so designers can see where
		// mutters will fire from. Color-coded by fire mode: green for fire-once
		// (the common case), yellow for repeat (less common, worth noticing).
		Collider col = GetComponent<Collider>();
		if (col == null) return;

		Color c = fireOnce ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 1f, 0f, 0.25f);
		if (hasFired) c = new Color(0.4f, 0.4f, 0.4f, 0.25f); // grey when spent

		Gizmos.color = c;

		if (col is SphereCollider sphere)
		{
			Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
		}
		else if (col is BoxCollider box)
		{
			Matrix4x4 m = Gizmos.matrix;
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawCube(box.center, box.size);
			Gizmos.matrix = m;
		}
		// Other collider types: skip the gizmo. Trigger still works; just no
		// visual aid.
	}
}
