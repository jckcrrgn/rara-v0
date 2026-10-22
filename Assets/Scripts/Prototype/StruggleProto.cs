// StruggleProto.cs — throwaway greybox. Answers one question: is rhythm-struggle fun?
// Day 197: + captor attention + fail state. New question: does rhythm hold up when
// you also have to read the captor?
// Empty scene, no Cassie, no existing scripts touched. Drop on any GameObject, press Play.
// Uses legacy Input. If the project is Input System-only: Project Settings > Player >
// Active Input Handling = Both.
//
// Controls: WASD / stick = select bond. Space / Fire1 = pull ON the pulse. R = restart.

using UnityEngine;

public class StruggleProto : MonoBehaviour
{
    [System.Serializable]
    public class Bond
    {
        public string name;
        public Vector2 dir;
        [Range(0, 1)] public float progress;
        [Tooltip("1 = normal. >1 = tighter. Each catch adds tightenPerCatch.")]
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
    [Tooltip("Even a clean pull makes some sound. On-beat pulling plateaus near this.")]
    public float noisePerPull = 0.05f;
    public float noisePerMiss = 0.15f;
    public float noisePerSpam = 0.25f;
    public float noiseDecay = 0.1f;       // per second
    [Range(0, 1)] public float noise;

    [Header("Captor attention (activation energy)")]
    [Tooltip("Noise needed to draw him in when fully distracted (on his phone).")]
    public float distractedThreshold = 0.85f;
    [Tooltip("Noise needed when fully watching. Below noisePerPull = any pull while watched gets caught.")]
    public float watchingThreshold = 0.045f;   // Day 197 playtest: below noisePerPull = freeze when watched
    public Vector2 distractedTime = new Vector2(4f, 9f);   // random range
    public float turnTime = 1.2f;                          // the tell: attention ramps 0 -> 1
    public Vector2 watchTime = new Vector2(2f, 4f);        // random range
    public float turnAwayTime = 0.8f;
    [Range(0, 1)] public float attention;

    [Header("Caught")]
    public float retieTime = 1.5f;        // input locked while he re-secures her
    public float setback = 0.3f;          // progress lost on every bond (re-ties free ones too)
    public float tightenPerCatch = 0.3f;  // tightness added to every bond
    public float glareTime = 3f;          // forced full attention after the re-tie
    public int maxCatches = 3;            // hit this and she's taken. Loss.

    enum Phase { Distracted, Turning, Watching, TurningAway }
    Phase phase;
    float phaseStart, phaseEnd;

    int selected = -1;
    float lastPull = -99f;
    float lockedUntil;
    float glareUntil;
    int catches;
    bool over;
    string lastResult = "";
    string endText = "";

    // telemetry — the actual answer to today's question
    float startTime;
    int pulls, goodPulls;
    float accSum;

    float Threshold => Mathf.Lerp(distractedThreshold, watchingThreshold, attention);

    void Start() => Restart();

    void Restart()
    {
        foreach (var b in bonds) { b.progress = 0f; b.tightness = 1f; }
        noise = 0f; attention = 0f; catches = 0; over = false;
        selected = -1; lastPull = -99f; lockedUntil = 0f;
        lastResult = ""; endText = "";
        pulls = goodPulls = 0; accSum = 0f;
        startTime = Time.time;
        EnterPhase(Phase.Distracted);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
        if (over) return;

        UpdateCaptor();
        noise = Mathf.Max(0f, noise - noiseDecay * Time.deltaTime);

        if (Time.time < lockedUntil) return;   // being re-tied

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

        // --- resolve ---
        if (noise >= Threshold) Caught();
        else if (AllFree()) End($"FREE in {Time.time - startTime:F1}s");
    }

    // ---------------- captor ----------------

    void EnterPhase(Phase p, float duration = -1f)
    {
        phase = p;
        if (duration < 0f)
        {
            duration = p switch
            {
                Phase.Distracted => Random.Range(distractedTime.x, distractedTime.y),
                Phase.Turning => turnTime,
                Phase.Watching => Random.Range(watchTime.x, watchTime.y),
                _ => turnAwayTime,
            };
        }
        phaseStart = Time.time;
        phaseEnd = Time.time + duration;
    }

    void UpdateCaptor()
    {
        float t = Mathf.InverseLerp(phaseStart, phaseEnd, Time.time);
        switch (phase)
        {
            case Phase.Distracted: attention = 0f; break;
            case Phase.Turning: attention = t; break;
            case Phase.Watching: attention = 1f; break;
            case Phase.TurningAway: attention = 1f - t; break;
        }

        if (Time.time >= phaseEnd)
        {
            EnterPhase(phase switch
            {
                Phase.Distracted => Phase.Turning,
                Phase.Turning => Phase.Watching,
                Phase.Watching => Phase.TurningAway,
                _ => Phase.Distracted,
            });
        }
    }

    void Caught()
    {
        catches++;
        noise = 0f;

        if (catches >= maxCatches)
        {
            End("TAKEN — he's moving you somewhere worse");
            return;
        }

        foreach (var b in bonds)
        {
            b.progress = Mathf.Max(0f, b.progress - setback);
            b.tightness += tightenPerCatch;
        }

        lockedUntil = Time.time + retieTime;
        glareUntil = lockedUntil + glareTime;
        EnterPhase(Phase.Watching, retieTime + glareTime);   // he stares after re-tying
        lastResult = $"CAUGHT ({catches}/{maxCatches}) — re-tying, tighter";
    }

    // ---------------- player ----------------

    void Pull()
    {
        float now = Time.time;
        bool spam = now - lastPull < spamGap;
        lastPull = now;

        if (selected < 0) { lastResult = "no bond selected"; return; }

        pulls++;
        if (spam) { noise = Mathf.Clamp01(noise + noisePerSpam); lastResult = "SPAM"; return; }

        float beatPhase = (now % beatInterval) / beatInterval;      // 0..1
        float error = Mathf.Min(beatPhase, 1f - beatPhase) * 2f;    // 0 on beat, 1 off
        float accuracy = 1f - error;
        accSum += accuracy;

        if (accuracy >= goodWindow)
        {
            goodPulls++;
            var b = bonds[selected];
            b.progress = Mathf.Clamp01(b.progress + gainPerPull * accuracy / b.tightness);
            noise = Mathf.Clamp01(noise + noisePerPull);
            lastResult = $"pull {accuracy:P0}";
        }
        else
        {
            noise = Mathf.Clamp01(noise + noisePerMiss);
            lastResult = $"off-beat {accuracy:P0}";
        }
    }

    bool AllFree()
    {
        foreach (var b in bonds) if (b.progress < 1f) return false;
        return true;
    }

    void End(string text)
    {
        over = true;
        int nonSpam = Mathf.Max(1, pulls);
        endText = $"{text}\ncatches {catches}  pulls {pulls}  on-beat {goodPulls * 100 / nonSpam}%  " +
                  $"avg acc {(accSum / nonSpam):P0}  time {Time.time - startTime:F1}s\nR to restart";
    }

    // ---------------- greybox UI ----------------

    void OnGUI()
    {
        float bp = (Time.time % beatInterval) / beatInterval;
        float pulse = 1f - Mathf.Min(bp, 1f - bp) * 2f;             // 1 on beat

        GUI.Box(new Rect(20, 20, 60 + pulse * 120, 24), "PULSE");

        for (int i = 0; i < bonds.Length; i++)
        {
            var b = bonds[i];
            string mark = i == selected ? "> " : "  ";
            string bar = new string('|', Mathf.RoundToInt(b.progress * 30));
            string state = b.progress >= 1f ? "  FREE" : "";
            string tight = b.tightness > 1f ? $"  x{b.tightness:F1}" : "";
            GUI.Label(new Rect(20, 60 + i * 22, 500, 22), $"{mark}{b.name,-8} {bar}{state}{tight}");
        }

        // noise bar with the captor's threshold as a moving line
        const float barX = 20f, barY = 160f, barW = 300f;
        GUI.Box(new Rect(barX, barY, barW, 18), "");
        if (noise > 0.01f) GUI.Box(new Rect(barX, barY, barW * noise, 18), "");
        GUI.Label(new Rect(barX + barW + 8, barY, 80, 20), "NOISE");
        var prev = GUI.color;
        GUI.color = Color.red;
        GUI.Box(new Rect(barX + barW * Threshold - 2, barY - 4, 4, 26), "");
        GUI.color = prev;

        string captor = phase switch
        {
            Phase.Distracted => "on his phone",
            Phase.Turning => "LOOKING UP...",
            Phase.Watching => "WATCHING",
            _ => "looking away",
        };
        if (phase == Phase.Watching || phase == Phase.Turning) GUI.color = Color.red;
        GUI.Label(new Rect(20, 190, 500, 22), $"CAPTOR  {captor}  (attention {attention:P0})");
        GUI.color = prev;

        bool locked = Time.time < lockedUntil;
        GUI.Label(new Rect(20, 214, 500, 22), locked ? "...being re-tied" : lastResult);
        GUI.Label(new Rect(20, 238, 500, 22), $"catches {catches}/{maxCatches}");

        if (over) GUI.Box(new Rect(20, 270, 520, 64), endText);

        // catch feedback: strong flash during re-tie, faint tint + warning during glare
        if (!over && Time.time < glareUntil)
        {
            var c = GUI.color;
            bool retie = Time.time < lockedUntil;
            GUI.color = new Color(1f, 0f, 0f, retie ? 0.35f : 0.12f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = c;
            var big = new GUIStyle(GUI.skin.label) { fontSize = retie ? 48 : 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 80), retie ? $"CAUGHT  {catches}/{maxCatches}" : "HE'S STARING — HOLD STILL", big);
        }
    }
}
