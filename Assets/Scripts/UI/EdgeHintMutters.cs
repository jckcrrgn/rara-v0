using UnityEngine;

/// <summary>
/// Day 220: Cassie hints the room, she doesn't prompt it. Prompts teach controls; mutters
/// hint the table edge (GDD, Table edge, locked Nov 14).
///
///   Barehand line  after a few good barehand pulls on her wrists, while they're still bound:
///                  "Could do this all night. Rather not."
///   Edge line      the first time she's within a SharpEdge's reach (its interactionRange,
///                  facing any way) with her wrists still bound:
///                  "That edge'll do."
///
/// Each fires once, as a Background mutter (no pause, mid-struggle is fine). A line that
/// gets dropped because another background mutter is up tries again on the next chance.
/// Once she has actually used the edge, neither line is needed, so both stand down.
///
/// Lives next to BondStruggle on Cassie.
/// </summary>
[RequireComponent(typeof(BondStruggle))]
public class EdgeHintMutters : MonoBehaviour
{
	[Header("Barehand line")]
	[TextArea(1, 3)] [SerializeField] private string barehandLine = "Could do this all night. Rather not.";
	[Tooltip("Good barehand pulls on the wrists before the line. 8 ≈ 6 s of clean pulling " +
		"(beat 0.7 s), about a fifth of the way through the wrists barehand.")]
	[SerializeField] private int barehandHits = 8;

	[Header("Edge line")]
	[Tooltip("Fires inside the edge's own reach (SharpEdge interactionRange, 0.75 in VS_Demo). " +
		"Not wider: she starts ~0.9 m from the table edge, so a wider ring fires on load.")]
	[TextArea(1, 3)] [SerializeField] private string edgeLine = "That edge'll do.";

	BondStruggle struggle;
	SharpEdge[] edges;
	int hits;
	bool barehandDone, edgeDone;

	void Start()
	{
		struggle = GetComponent<BondStruggle>();
		edges = FindObjectsByType<SharpEdge>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		struggle.OnPullResolved += OnPull;
	}

	void OnDestroy()
	{
		if (struggle != null) struggle.OnPullResolved -= OnPull;
	}

	bool WristsBound => struggle.WristsIndex >= 0 && struggle.bonds[struggle.WristsIndex].Bound;

	void OnPull(BondStruggle.PullKind kind, bool toolEngaged)
	{
		if (kind != BondStruggle.PullKind.Hit || struggle.Selected != struggle.WristsIndex) return;
		if (toolEngaged)
		{
			// She found it. No more hints.
			barehandDone = edgeDone = true;
			return;
		}
		if (barehandDone || !WristsBound) return;
		hits++;
		if (hits >= barehandHits && Say(barehandLine)) barehandDone = true;
	}

	void Update()
	{
		if (edgeDone || edges == null || struggle == null) return;
		if (!WristsBound) { edgeDone = true; return; }
		foreach (var e in edges)
		{
			if (e == null || !e.gameObject.activeInHierarchy) continue;
			if (Vector3.Distance(transform.position, e.transform.position) > e.InteractionRange) continue;
			if (Say(edgeLine)) edgeDone = true;
			return;
		}
	}

	static bool Say(string line)
	{
		if (string.IsNullOrEmpty(line) || MutterSystem.Instance == null) return false;
		return MutterSystem.Instance.Play(line, MutterSystem.Speaker.Cassie, MutterSystem.MutterMode.Background);
	}
}
