using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Rara Day 161 — recording aid for CAM_Shot4_Check. Not gameplay.
// Drives the PIVOT only: localPosition.x and local Euler Z, on one eased ramp.
// Position Y/Z and rotation X/Y are never touched.
//
// Play mode:  F8 = peek (from hidden)   F7 = snap back to hidden
//             or tick "Peek Now" in the Inspector, or context menu > Peek.
// Edit mode:  pose him, then context menu > Capture Hidden / Capture Peek.
public class GuardPeekDriver : MonoBehaviour
{
    [Header("Poses (pivot local)")]
    public float hiddenX = 1.5f;
    public float hiddenRotZ = 0f;
    public float peekX = 1.8f;
    public float peekRotZ = 16.6f;

    [Header("Timing")]
    [Min(0.01f)] public float duration = 1.2f;
    [Min(0f)] public float startDelay = 0f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Trigger")]
    public bool snapHiddenOnStart = true;
    public bool peekOnStart = false;
    [Tooltip("Tick in play mode to fire. Clears itself.")]
    public bool peekNow;

    Vector3 baseEuler;
    bool baseCached;
    float t;
    float delayLeft;
    bool running;

    void Start()
    {
        CacheBase();
        if (snapHiddenOnStart) Apply(0f);
        if (peekOnStart) Peek();
    }

    void Update()
    {
        if (PeekPressed() || peekNow) { peekNow = false; Peek(); }
        if (ResetPressed()) ResetHidden();
        if (!running) return;

        if (delayLeft > 0f) { delayLeft -= Time.deltaTime; return; }
        t += Time.deltaTime / duration;
        if (t >= 1f) { t = 1f; running = false; }
        Apply(ease.Evaluate(t));
    }

    [ContextMenu("Peek (play mode)")]
    public void Peek()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[GuardPeek] Peek runs in play mode only."); return; }
        t = 0f;
        delayLeft = startDelay;
        running = true;
        Apply(0f);
        Debug.Log($"[GuardPeek] Peek: X {hiddenX}→{peekX}, Z {hiddenRotZ}→{peekRotZ}, {duration}s");
    }

    [ContextMenu("Reset to Hidden")]
    public void ResetHidden()
    {
        running = false;
        RecordUndo(transform, "Guard Peek Reset");
        Apply(0f);
    }

    [ContextMenu("Capture Hidden From Current")]
    void CaptureHidden()
    {
        RecordUndo(this, "Capture Hidden");
        hiddenX = transform.localPosition.x;
        hiddenRotZ = SignedZ();
    }

    [ContextMenu("Capture Peek From Current")]
    void CapturePeek()
    {
        RecordUndo(this, "Capture Peek");
        peekX = transform.localPosition.x;
        peekRotZ = SignedZ();
    }

    void Apply(float k)
    {
        CacheBase();
        Vector3 p = transform.localPosition;
        p.x = Mathf.LerpUnclamped(hiddenX, peekX, k);
        transform.localPosition = p;

        Vector3 e = baseEuler;
        e.z = Mathf.LerpUnclamped(hiddenRotZ, peekRotZ, k);
        transform.localRotation = Quaternion.Euler(e);
    }

    // Play mode caches X/Y once so repeated decomposes can't drift them.
    // Edit mode re-reads, since the transform may have been edited by hand.
    void CacheBase()
    {
        if (baseCached && Application.isPlaying) return;
        baseEuler = transform.localEulerAngles;
        baseCached = true;
    }

    float SignedZ()
    {
        float z = transform.localEulerAngles.z;
        return z > 180f ? z - 360f : z;
    }

    static void RecordUndo(Object o, string label)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(o, label);
#endif
    }

    static bool PeekPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F8);
#else
        return false;
#endif
    }

    static bool ResetPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F7);
#else
        return false;
#endif
    }
}
