using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Soft-pressure timer for levels that need one. Debuts in L6: lamp smash or
/// chair-tip crash starts it, threshold events drive guard-pressure mutters,
/// expiry triggers the failure loop.
///
/// Why standalone (not bolted onto LevelManager): LevelManager owns lifecycle
/// (load/complete/restart) and is per-scene by definition. Most levels won't
/// have a timer — L1–L5 don't, L7+ may or may not. Mixing them would conflate
/// concerns and bloat the levels that don't need it. Drop this component onto
/// a GameObject only in the levels that need pressure.
///
/// Singleton: `LevelTimer.Instance` is convenient from triggers
/// (LampSmashTrigger, ChairTipMarker) without needing a serialized reference
/// in each. Same pattern as MutterSystem.
///
/// IDEMPOTENT START
/// ----------------
/// `StartTimer()` is a no-op if the timer is already running. This is the
/// mechanic, not a safety net: the L6 spec is "lamp smash OR chair-tip crash,
/// first occurrence wins." Both events call StartTimer; the first wins, the
/// second is silently ignored. There is deliberately no Restart() method —
/// callers that genuinely want to restart must Reset + Start, making the
/// intent explicit.
///
/// THRESHOLDS
/// ----------
/// `thresholdsNormalized` is an array of normalized values in (0, 1). Each
/// one fires `OnThresholdReached(value)` exactly once per timer run, in
/// ascending order, when the elapsed fraction first crosses it. Spec only
/// calls out 0.5 today (Beat 5: offstage guard pressure); the array
/// future-proofs for a 0.75 "guard getting close" beat without needing a
/// second callback.
///
/// EVENTS
/// ------
/// UnityEvents (not C# events) so hookup happens in the inspector. Lets
/// LevelTimer fire MutterSystem.Play and FailureLoopController.OnTimerExpired
/// without a hard reference from this file to either system. Same pattern as
/// existing level wiring.
///
/// VISIBILITY
/// ----------
/// No countdown UI by design — L6's pressure is meant to be diegetic
/// (guard mutter ramp). Use the inspector's runtime view of `ElapsedNormalized`
/// or Debug.Log on the events for development visibility.
/// </summary>
public class LevelTimer : MonoBehaviour
{
	public static LevelTimer Instance { get; private set; }

	[Header("Timer")]
	[Tooltip("Total duration in seconds. L6 default 120s — pressure, not panic. " +
		"Tune in playtest.")]
	[SerializeField] private float totalDuration = 120f;

	[Tooltip("Normalized thresholds in (0,1) that fire OnThresholdReached(value). " +
		"Must be in ascending order. L6 default {0.5} for Beat 5 (offstage guard " +
		"pressure). Add 0.75 etc. as design demands.")]
	[SerializeField] private float[] thresholdsNormalized = { 0.5f };

	[Header("Events")]
	[Tooltip("Fires once when StartTimer succeeds (i.e. timer was not already " +
		"running). Use for one-time setup like ramping ambient audio.")]
	public UnityEvent OnTimerStart;

	[Tooltip("Fires when ElapsedNormalized first crosses a value in " +
		"thresholdsNormalized. Argument is the threshold that was crossed " +
		"(useful when wiring multiple thresholds through one handler).")]
	public UnityEvent<float> OnThresholdReached;

	[Tooltip("Fires once when the timer reaches totalDuration. Stops the timer " +
		"automatically — handler does not need to call StopTimer.")]
	public UnityEvent OnTimerExpired;

	// Runtime state. Exposed via properties for inspector visibility and
	// external read access; never set externally.
	private bool isRunning;
	private float elapsed;
	private int nextThresholdIndex;

	public bool IsRunning => isRunning;
	public float ElapsedNormalized => totalDuration <= 0f ? 0f : Mathf.Clamp01(elapsed / totalDuration);

	void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	void Update()
	{
		if (!isRunning) return;

		elapsed += Time.deltaTime;

		// Threshold sweep. Fire every threshold we've crossed this frame, in
		// order. Guards against large delta times skipping a threshold (rare,
		// but possible if a hitch lands while the timer is running). Each
		// threshold fires at most once per run; nextThresholdIndex advances
		// monotonically.
		float n = ElapsedNormalized;
		while (nextThresholdIndex < thresholdsNormalized.Length
			&& n >= thresholdsNormalized[nextThresholdIndex])
		{
			float threshold = thresholdsNormalized[nextThresholdIndex];
			nextThresholdIndex++;
			OnThresholdReached?.Invoke(threshold);
		}

		if (elapsed >= totalDuration)
		{
			isRunning = false;
			elapsed = totalDuration;
			OnTimerExpired?.Invoke();
		}
	}

	/// <summary>
	/// Start the timer. No-op if already running (intentional — this is the
	/// "first occurrence wins" mechanic, not a safety net). Fires OnTimerStart
	/// on the call that actually starts it.
	/// </summary>
	public void StartTimer()
	{
		if (isRunning) return;
		isRunning = true;
		OnTimerStart?.Invoke();
	}

	/// <summary>
	/// Stop the timer in place. Elapsed time is preserved; calling StartTimer
	/// again will resume from where it stopped. Use ResetTimer to clear elapsed
	/// back to zero. Does NOT fire OnTimerExpired — that's reserved for natural
	/// expiry.
	/// </summary>
	public void StopTimer()
	{
		isRunning = false;
	}

	/// <summary>
	/// Reset elapsed time and threshold progress to zero. Does not start the
	/// timer; pair with StartTimer if you want a fresh run. Used by the
	/// failure loop on attempt restart.
	/// </summary>
	public void ResetTimer()
	{
		isRunning = false;
		elapsed = 0f;
		nextThresholdIndex = 0;
	}

	public void DebugThreshold(float value) { Debug.Log($"[LevelTimer] threshold {value} reached"); }
	public void DebugExpired() { Debug.Log("[LevelTimer] expired"); }
}
