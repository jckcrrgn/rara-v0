using System.Collections;
using UnityEngine;

/// <summary>
/// Owns the post-KO resolution beat for the Vertical Slice ("The Turnaround").
///
/// Subscribes to GuardController.OnStateChanged. When the guard hits Downed,
/// runs a short resolution sequence:
///   1. Beat pause — let the strike breathe.
///   2. Cassie's victory mutter.
///   3. Wait for dismiss.
///   4. Fire PlayerController.OnPlayerFreed — standard level-complete signal.
///
/// This closes the VS arc. Whatever is subscribed to OnPlayerFreed (fade,
/// scene load, credits) picks up from there — SliceCoordinator doesn't own
/// the exit, just the beat before it.
///
/// SETUP
/// -----
/// Drop on any scene GameObject. Assign playerController in the Inspector.
/// Wire victory audio if desired. The coordinator resolves once — Downed is
/// terminal so no re-entry guard is needed beyond the hasResolved flag.
/// </summary>
public class SliceCoordinator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The scene's PlayerController. OnPlayerFreed is fired here to " +
        "signal level complete — same event the rest of Act 1 uses.")]
    [SerializeField] private PlayerController playerController;

    [Header("Timing")]
    [Tooltip("Pause between the guard going down and Cassie's line. " +
        "Lets the strike impact settle before she speaks. 1–2s recommended.")]
    [SerializeField] private float beatPause = 1.5f;

    [Header("Mutter — Victory")]
    [Tooltip("Cassie's line after the guard goes down. Placeholder — swap in " +
        "the Inspector once the line is locked.\n\n" +
        "Options:\n" +
        "  \"Rookie knots.\"\n" +
        "  \"I've had worse. He hasn't.\"\n" +
        "  \"Next time, use the zip ties.\"\n" +
        "  \"Should've checked my hands.\"")]
    [TextArea(2, 4)]
    [SerializeField] private string victoryMutterLine = "Should've been watching my HANDS.";

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    // -------------------------------------------------------------------------
    // Runtime
    // -------------------------------------------------------------------------

    // One-shot guard — Downed is terminal, but belt-and-suspenders against
    // any edge case that fires OnStateChanged more than once.
    private bool hasResolved = false;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (GuardController.Instance == null)
        {
            Debug.LogWarning("[SliceCoordinator] No GuardController in scene — " +
                "resolution sequence will never fire.");
            return;
        }

        GuardController.Instance.OnStateChanged += OnGuardStateChanged;
        Log("Subscribed to GuardController.OnStateChanged.");
    }

    void OnDestroy()
    {
        if (GuardController.Instance != null)
            GuardController.Instance.OnStateChanged -= OnGuardStateChanged;
    }

    // -------------------------------------------------------------------------
    // Guard state listener
    // -------------------------------------------------------------------------

    private void OnGuardStateChanged(GuardController.GuardState newState)
    {
        if (newState == GuardController.GuardState.Downed && !hasResolved)
        {
            hasResolved = true;
            StartCoroutine(ResolutionSequence());
        }
    }

    // -------------------------------------------------------------------------
    // Resolution sequence
    // -------------------------------------------------------------------------

    private IEnumerator ResolutionSequence()
    {
        Log("Resolution sequence START.");

        // Beat pause — strike breathes before Cassie speaks.
        yield return new WaitForSeconds(beatPause);

        // Victory mutter.
        if (MutterSystem.Instance != null && !string.IsNullOrEmpty(victoryMutterLine))
        {
            MutterSystem.Instance.Play(victoryMutterLine, MutterSystem.Speaker.Cassie);

            // Wait for the mutter to finish before firing the level-complete event.
            yield return new WaitWhile(() =>
                MutterSystem.Instance != null && MutterSystem.Instance.IsActive);
        }

        // Level complete — same signal the rest of Act 1 uses.
        Log("Firing OnPlayerFreed — slice complete.");
        playerController?.OnPlayerFreed?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void Log(string msg)
    {
        if (verboseLogging) Debug.Log($"[SliceCoordinator] {msg}");
    }
}
