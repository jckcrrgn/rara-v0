// StruggleProto.cs — throwaway greybox. Answers one question: is rhythm-struggle fun?
// Empty scene, no Cassie, no existing scripts touched. Drop on any GameObject, press Play.
// Uses legacy Input. If the project is Input System-only: Project Settings > Player >
// Active Input Handling = Both.
//
// Controls: WASD / stick = select bond. Space / Fire1 = pull. Pull ON the pulse.

using UnityEngine;

public class StruggleProto : MonoBehaviour
{
    [System.Serializable]
    public class Bond
    {
        public string name;
        public Vector2 dir;
        [Range(0, 1)] public float progress;
        [Tooltip("1 = normal. >1 = tighter (drag-back escalation hooks here).")]
        public float tightness = 1f;
    }

    public Bond[] bonds =
    {
        new Bond { name = "Elbows", dir = Vector2.up },
        new Bond { name = "Wrists", dir = Vector2.down },
        new Bond { name = "L Ankle", dir = Vector2.left },
        new Bond { name = "R Ankle", dir = Vector2.right },
    };

    [Header("Rhythm")]
    public float beatInterval = 0.7f;     // seconds between pulses
    [Range(0, 1)] public float goodWindow = 0.6f;  // accuracy needed to make progress
    public float gainPerPull = 0.08f;     // progress on a perfect pull, tightness 1
    public float spamGap = 0.25f;         // pulls closer than this = spam

    [Header("Noise")]
    public float noisePerMiss = 0.15f;
    public float noisePerSpam = 0.25f;
    public float noiseDecay = 0.2f;       // per second
    [Range(0, 1)] public float noise;

    int selected = -1;
    float lastPull = -99f;
    string lastResult = "";

    void Update()
    {
        // --- select ---
        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 0.25f)
        {
            float best = 0.5f;
            for (int i = 0; i < bonds.Length; i++)
            {
                float d = Vector2.Dot(input.normalized, bonds[i].dir);
                if (d > best) { best = d; selected = i; }
            }
        }

        // --- pull ---
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Fire1"))
            Pull();

        noise = Mathf.Max(0f, noise - noiseDecay * Time.deltaTime);
    }

    void Pull()
    {
        float now = Time.time;
        bool spam = now - lastPull < spamGap;
        lastPull = now;

        if (selected < 0) { lastResult = "no bond selected"; return; }
        if (spam) { noise = Mathf.Clamp01(noise + noisePerSpam); lastResult = "SPAM"; return; }

        float phase = (now % beatInterval) / beatInterval;          // 0..1
        float error = Mathf.Min(phase, 1f - phase) * 2f;            // 0 on beat, 1 off
        float accuracy = 1f - error;

        if (accuracy >= goodWindow)
        {
            var b = bonds[selected];
            b.progress = Mathf.Clamp01(b.progress + gainPerPull * accuracy / b.tightness);
            lastResult = $"pull {accuracy:P0}";
        }
        else
        {
            noise = Mathf.Clamp01(noise + noisePerMiss);
            lastResult = $"off-beat {accuracy:P0}";
        }
    }

    void OnGUI()
    {
        float phase = (Time.time % beatInterval) / beatInterval;
        float pulse = 1f - Mathf.Min(phase, 1f - phase) * 2f;      // 1 on beat

        GUI.Box(new Rect(20, 20, 60 + pulse * 120, 24), "PULSE");
        for (int i = 0; i < bonds.Length; i++)
        {
            var b = bonds[i];
            string mark = i == selected ? "> " : "  ";
            string bar = new string('|', Mathf.RoundToInt(b.progress * 30));
            string state = b.progress >= 1f ? "  FREE" : "";
            GUI.Label(new Rect(20, 60 + i * 22, 500, 22), $"{mark}{b.name,-8} {bar}{state}");
        }
        GUI.Label(new Rect(20, 160, 500, 22), $"NOISE {new string('#', Mathf.RoundToInt(noise * 30))}");
        GUI.Label(new Rect(20, 184, 500, 22), lastResult);
    }
}
