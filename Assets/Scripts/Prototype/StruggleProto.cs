// StruggleProto.cs — throwaway greybox. Answers one question: is rhythm-struggle fun?
// Day 197: + captor attention + fail state. New question: does rhythm hold up when
// you also have to read the captor?
// Day 198: R no longer leaves a stale glare. Telemetry excludes spam. RT pulls (edge-detected).
//          + UNTIE: once wrists are struggled free, bonds she can reach can be untied —
//          a knot sequence, quiet and fast. The risk flips from NOISE to BEING SEEN:
//          untying puts her hands out; he must not see free hands. Hide them before he looks.
//          Caught with free hands = escalation (wrists re-tied from scratch, extra tight).
// Day 199: telemetry — untie vs struggle-on-untieable, hide reaction time vs seen budget.
// Day 199 pm: THE CAPTOR LISTENS. Two tiers of noise response:
//          noise >= glanceNoise  -> GLANCE: short partial look. More noise during it -> inspect.
//          noise >= inspectNoise -> INSPECT: fast turn, long full look (the lamp off the table).
//          The sound that drew him is spent (noise zeroed). What catches you is the NEXT sound.
//          Noise never catches her while he's on his phone — it draws him in instead.
//          Unprompted rhythm broken: wider distracted range + random spontaneous glances.
//          Rule: randomise WHEN he looks, never HOW FAST he turns. The turn is the tell.
//          PAD: RT struggles (A no longer does). D-pad unties. LB hides. Y test-knock. Start restart.
// New question: does a captor who reacts to you feel like a person instead of a metronome?
// Day 200: pre-playtest fixes. Noise that would catch her while he's turning away now
//          catches her (it used to become a glance and LOWER his attention). Red line only
//          drawn when it can actually catch. Log adds inspect-from-glance seen budget.
// Day 201: cold-playtest build. coldPlaytest = on-screen controls card, test knock off.
//          End screens append to struggle_runs.txt (persistentDataPath). Telemetry splits
//          hides by glance / look / inspect, adds hands-out time + closest call (max
//          attention while hands were out). Seen never fired in 8 runs — closest call
//          says whether that's skill or the untie window just never overlapping a look.
//
// Empty scene, no Cassie, no existing scripts touched. Drop on any GameObject, press Play.
// Keyboard uses legacy Input: Project Settings > Player > Active Input Handling = Both.
//
// Controls              keyboard   pad
//   select bond         WASD       left stick
//   struggle (on pulse) Space      RT
//   untie (follow knot) IJKL       D-pad      (hands free only; not elbows)
//   hide hands          F          LB
//   test knock (loud)   N          Y          (stands in for knocking something over)
//   restart             R          Start
//
// PAD SETUP
//   Input System package installed (Active Input Handling = Both): nothing to do.
//   Legacy only: add these in the Input Manager or RT / D-pad stay dead (warning shown on screen):
//     "RightTrigger"  Joystick Axis, 10th axis, Joy Num all, Dead 0.05, Sens 1
//     "DPadX"         Joystick Axis, 6th axis,  Joy Num all, Dead 0.1,  Sens 1
//     "DPadY"         Joystick Axis, 7th axis,  Joy Num all, Dead 0.1,  Sens 1
//   (Windows XInput numbering.)

using System.Collections.Generic;
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

    [Header("Captor attention")]
    [Tooltip("Noise that catches her the instant he starts to look. Falls to watchingThreshold as attention rises. Does not apply while he's on his phone.")]
    public float distractedThreshold = 0.85f;
    [Tooltip("Noise that catches her when fully watching. Below noisePerPull = any pull while watched gets caught.")]
    public float watchingThreshold = 0.045f;
    public Vector2 distractedTime = new Vector2(3f, 10f);   // random range
    [Tooltip("When a distracted spell ends on its own: chance it's a glance rather than a full look.")]
    [Range(0, 1)] public float spontaneousGlanceChance = 0.4f;
    public float turnTime = 1.4f;                           // the tell: fixed on purpose
    public Vector2 watchTime = new Vector2(2f, 4f);         // random range
    public float turnAwayTime = 0.8f;
    [Range(0, 1)] public float attention;

    [Header("Glance (cursory)")]
    [Tooltip("Noise that draws a glance. Two quick off-beat pulls, or two spams.")]
    public float glanceNoise = 0.3f;
    [Tooltip("Peak attention of a glance. Below seenAttention = a glance can't spot free hands.")]
    [Range(0, 1)] public float glancePeak = 0.6f;
    public float glanceUpTime = 0.35f;
    public Vector2 glanceHoldTime = new Vector2(0.6f, 1.2f);
    public float glanceDownTime = 0.4f;

    [Header("Inspect (closer look)")]
    [Tooltip("Noise that makes him come over. Also: any glanceNoise made DURING a glance.")]
    public float inspectNoise = 0.6f;
    [Tooltip("Faster than turnTime — he's reacting, not idly looking up.")]
    public float inspectTurnTime = 0.8f;
    public Vector2 inspectTime = new Vector2(4f, 7f);
    [Tooltip("Test key (N / Y). Stands in for knocking something over.")]
    public float knockNoise = 0.7f;

    [Header("Caught")]
    public float retieTime = 1.5f;        // input locked while he re-secures her
    public float setback = 0.3f;          // progress lost on every bond (re-ties free ones too)
    public float tightenPerCatch = 0.3f;  // tightness added to every bond
    public float glareTime = 3f;          // forced full attention after the re-tie
    public int maxCatches = 3;            // hit this and she's taken. Loss.

    [Header("Free hands")]
    [Tooltip("Index into bonds. Struggling this one free frees her hands.")]
    public int wristsIndex = 1;
    [Tooltip("Index into bonds she can't reach to untie (elbows). -1 = none.")]
    public int unreachableIndex = 0;
    [Tooltip("Struggle gain multiplier on other bonds once hands are free.")]
    public float freeHandsStruggleMult = 1.5f;

    [Header("Untie")]
    [Tooltip("Progress per correct knot input, tightness 1. 0.1 = ~10 inputs per bond.")]
    public float untieGain = 0.1f;
    public float noisePerUntie = 0.01f;   // quiet, deliberately below watchingThreshold
    public float noisePerFumble = 0.1f;   // wrong direction
    [Tooltip("Attention at which he sees free hands that are out.")]
    [Range(0, 1)] public float seenAttention = 0.75f;
    [Tooltip("Time to get hands back behind her. Must finish before seenAttention.")]
    public float handsBackTime = 0.4f;
    [Tooltip("Extra tightness on the wrists, on top of tightenPerCatch, when caught with free hands.")]
    public float freeHandsExtraTighten = 0.3f;

    [Header("Legacy pad axes (ignored with Input System)")]
    public string pullAxis = "RightTrigger";
    [Range(0, 1)] public float triggerPress = 0.6f;
    [Range(0, 1)] public float triggerRelease = 0.3f;
    public string dpadXAxis = "DPadX";
    public string dpadYAxis = "DPadY";
#if !ENABLE_INPUT_SYSTEM
    bool hasPullAxis, triggerHeld, hasDpad, dpadHeld;
#endif

    enum Phase { Distracted, Glance, Turning, Watching, TurningAway }
    Phase phase;
    float phaseStart, phaseEnd, phaseStartAttention;
    float glanceHold;       // hold length of the current glance
    bool inspecting;        // current Turning/Watching is a reaction, not idle

    int selected = -1;
    float lastPull = -99f;
    float lockedUntil;
    float glareUntil;
    int catches;
    bool over;
    bool lastCatchSeen;
    string lastResult = "";
    string endText = "";

    // hands
    bool handsOut;          // free hands visibly working at a bond
    float hidingUntil;      // > 0 while putting hands back

    // knots: per bond, upcoming directions. 0 = up, 1 = right, 2 = down, 3 = left
    List<int>[] knots;
    static readonly string[] Arrow = { "^", ">", "v", "<" };
    const int KnotPreview = 3;

    // telemetry
    float startTime;
    int pulls, goodPulls, spams;   // pulls = non-spam pulls only
    int unties, fumbles, seenCatches;
    float accSum;
    int struggledUntieable;        // on-beat pulls on a bond she could have untied instead
    int hides, glanceHides;        // F presses; ones made during a glance
    int lookHides, inspectHides;   // ones made while he was turning (idle look / inspect)
    float lookReactSum, inspectReactSum;   // sum of (F press - turn start)
    float handsOutTime;            // seconds with free hands out
    float closestCall;             // max attention while hands were out
    int glancesHeard, glancesIdle, inspections, knocks;
    int runIndex;                  // not reset on Restart: numbers runs in the log
    string savedPath = "";

    [Header("Playtest")]
    [Tooltip("Cold playtest: controls card shown at start, test knock disabled.")]
    public bool coldPlaytest = false;
    bool showCard;

    float Threshold => Mathf.Lerp(distractedThreshold, watchingThreshold, attention);
    bool Attending => phase == Phase.Turning || phase == Phase.Watching;
    bool HandsFree => wristsIndex >= 0 && wristsIndex < bonds.Length && bonds[wristsIndex].progress >= 1f;
    bool CanUntie(int i) => HandsFree && i >= 0 && i != unreachableIndex && i != wristsIndex;
    // Seconds from the start of a turn (from attention 0) to the last safe F press. <= 0 = unwinnable.
    float Budget(float turn) => turn * seenAttention - handsBackTime;

    void Start()
    {
#if !ENABLE_INPUT_SYSTEM
        hasPullAxis = AxisExists(pullAxis, "RT");
        hasDpad = AxisExists(dpadXAxis, "D-pad") && AxisExists(dpadYAxis, "D-pad");
#endif
        Restart();
    }

    void Restart()
    {
        foreach (var b in bonds) { b.progress = 0f; b.tightness = 1f; }
        knots = new List<int>[bonds.Length];
        for (int i = 0; i < bonds.Length; i++) { knots[i] = new List<int>(); FillKnot(i); }
        noise = 0f; attention = 0f; catches = 0; over = false; lastCatchSeen = false;
        selected = NextUnfreed(-1); lastPull = -99f; lockedUntil = 0f; glareUntil = 0f;
        handsOut = false; hidingUntil = 0f; inspecting = false;
        lastResult = ""; endText = "";
        pulls = goodPulls = spams = unties = fumbles = seenCatches = 0; accSum = 0f;
        struggledUntieable = hides = glanceHides = lookHides = inspectHides = 0;
        lookReactSum = inspectReactSum = handsOutTime = closestCall = 0f;
        glancesHeard = glancesIdle = inspections = knocks = 0;
        if (runIndex == 0) showCard = coldPlaytest;   // first run only; Tab / Back toggles after
        startTime = Time.time;
        EnterPhase(Phase.Distracted);
        // Inspect almost always escalates from a glance, so it ramps from glancePeak, not 0.
        float fromGlance = inspectTurnTime * (seenAttention - glancePeak) / (1f - glancePeak) - handsBackTime;
        Debug.Log($"StruggleProto: seen budget — idle look {Budget(turnTime):F2}s, inspect {Budget(inspectTurnTime):F2}s, " +
                  $"inspect from glance peak {fromGlance:F2}s (<= 0 = hands out during a glance that escalates is always seen)");
    }

    void Update()
    {
        // Pad read every frame, even locked/over, so edges stay correct (no instant re-catch on unlock).
        var pad = ReadPad();
        if (Input.GetKeyDown(KeyCode.Tab) || pad.toggleCard) showCard = !showCard;
        if (Input.GetKeyDown(KeyCode.R) || pad.restart) { Restart(); return; }
        if (over) return;

        UpdateCaptor();
        noise = Mathf.Max(0f, noise - noiseDecay * Time.deltaTime);

        if (hidingUntil > 0f && Time.time >= hidingUntil) { handsOut = false; hidingUntil = 0f; lastResult = "hands hidden"; }
        if (HandsFree && handsOut)
        {
            handsOutTime += Time.deltaTime;
            closestCall = Mathf.Max(closestCall, attention);
        }

        if (Time.time < lockedUntil) return;   // being re-tied

        // --- select ---
        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (pad.stick.sqrMagnitude > input.sqrMagnitude) input = pad.stick;
        if (input.sqrMagnitude > 0.25f)
        {
            float best = 0.5f;
            for (int i = 0; i < bonds.Length; i++)
            {
                if (bonds[i].progress >= 1f) continue;   // can't select a free bond
                float d = Vector2.Dot(input.normalized, bonds[i].dir);
                if (d > best) { best = d; selected = i; }
            }
        }

        // --- struggle --- (Space / RT only. A is no longer a pull.)
        if (Input.GetKeyDown(KeyCode.Space) || pad.pull) Pull();

        // --- untie ---
        int untieDir = pad.dir;
        if (Input.GetKeyDown(KeyCode.I)) untieDir = 0;
        else if (Input.GetKeyDown(KeyCode.L)) untieDir = 1;
        else if (Input.GetKeyDown(KeyCode.K)) untieDir = 2;
        else if (Input.GetKeyDown(KeyCode.J)) untieDir = 3;
        if (untieDir >= 0) Untie(untieDir);

        // --- hide hands ---
        if ((Input.GetKeyDown(KeyCode.F) || pad.hide) && handsOut && hidingUntil <= 0f)
        {
            hidingUntil = Time.time + handsBackTime;
            lastResult = "hiding hands...";
            hides++;
            if (phase == Phase.Glance) glanceHides++;
            else if (phase == Phase.Turning)
            {
                float react = Time.time - phaseStart;
                if (inspecting) { inspectHides++; inspectReactSum += react; }
                else { lookHides++; lookReactSum += react; }
            }
        }

        // --- test knock --- (dev only)
        if (!coldPlaytest && (Input.GetKeyDown(KeyCode.N) || pad.knock))
        {
            knocks++;
            noise = Mathf.Clamp01(noise + knockNoise);
            lastResult = "CRASH — knocked something over";
        }

        ReactToNoise();

        // --- resolve --- (seen first: it's the worse catch)
        if (HandsFree && handsOut && attention >= seenAttention) Caught(true);
        else if (phase != Phase.Distracted && noise >= Threshold) Caught(false);
        else if (AllFree()) End($"FREE in {Time.time - startTime:F1}s");
    }

    // ---------------- input ----------------

    struct Pad { public bool pull, hide, knock, restart, toggleCard; public int dir; public Vector2 stick; }

    Pad ReadPad()
    {
        var p = new Pad { dir = -1 };
#if ENABLE_INPUT_SYSTEM
        var gp = UnityEngine.InputSystem.Gamepad.current;
        if (gp == null) return p;
        p.pull = gp.rightTrigger.wasPressedThisFrame;          // edge-detected by the Input System
        if (gp.dpad.up.wasPressedThisFrame) p.dir = 0;
        else if (gp.dpad.right.wasPressedThisFrame) p.dir = 1;
        else if (gp.dpad.down.wasPressedThisFrame) p.dir = 2;
        else if (gp.dpad.left.wasPressedThisFrame) p.dir = 3;
        p.hide = gp.leftShoulder.wasPressedThisFrame;
        p.knock = gp.buttonNorth.wasPressedThisFrame;
        p.restart = gp.startButton.wasPressedThisFrame;
        p.toggleCard = gp.selectButton.wasPressedThisFrame;
        p.stick = gp.leftStick.ReadValue();
#else
        p.pull = ReadTrigger();
        p.dir = ReadDpad();
        p.hide = Input.GetKeyDown(KeyCode.JoystickButton4);    // LB
        p.knock = Input.GetKeyDown(KeyCode.JoystickButton3);   // Y
        p.restart = Input.GetKeyDown(KeyCode.JoystickButton7); // Start
        p.toggleCard = Input.GetKeyDown(KeyCode.JoystickButton6); // Back
        // left stick arrives through legacy Horizontal/Vertical
#endif
        return p;
    }

    string PadStatus()
    {
#if ENABLE_INPUT_SYSTEM
        return UnityEngine.InputSystem.Gamepad.current == null ? "PAD: none detected" : "";
#else
        if (!hasPullAxis || !hasDpad)
            return $"PAD: {(hasPullAxis ? "" : "RT ")}{(hasDpad ? "" : "D-pad ")}not set up — see header";
        return "";
#endif
    }

#if !ENABLE_INPUT_SYSTEM
    static bool AxisExists(string axis, string label)
    {
        if (string.IsNullOrEmpty(axis)) return false;
        try { Input.GetAxisRaw(axis); return true; }
        catch (System.ArgumentException)
        {
            Debug.LogWarning($"StruggleProto: no Input Manager axis '{axis}'. {label} disabled.");
            return false;
        }
    }

    bool ReadTrigger()
    {
        if (!hasPullAxis) return false;
        float rt = Input.GetAxisRaw(pullAxis);
        if (!triggerHeld && rt > triggerPress) { triggerHeld = true; return true; }
        if (triggerHeld && rt < triggerRelease) triggerHeld = false;
        return false;
    }

    int ReadDpad()
    {
        if (!hasDpad) return -1;
        float x = Input.GetAxisRaw(dpadXAxis), y = Input.GetAxisRaw(dpadYAxis);
        if (dpadHeld)
        {
            if (Mathf.Abs(x) < 0.3f && Mathf.Abs(y) < 0.3f) dpadHeld = false;
            return -1;
        }
        int dir = -1;
        if (y > 0.5f) dir = 0; else if (x > 0.5f) dir = 1;
        else if (y < -0.5f) dir = 2; else if (x < -0.5f) dir = 3;
        if (dir >= 0) dpadHeld = true;
        return dir;
    }
#endif

    // ---------------- captor ----------------

    void EnterPhase(Phase p, float duration = -1f)
    {
        phase = p;
        phaseStartAttention = attention;   // turns ramp from wherever he is, not from 0
        if (duration < 0f)
        {
            switch (p)
            {
                case Phase.Distracted: duration = Random.Range(distractedTime.x, distractedTime.y); break;
                case Phase.Glance:
                    glanceHold = Random.Range(glanceHoldTime.x, glanceHoldTime.y);
                    duration = glanceUpTime + glanceHold + glanceDownTime;
                    break;
                case Phase.Turning: duration = inspecting ? inspectTurnTime : turnTime; break;
                case Phase.Watching:
                    duration = inspecting ? Random.Range(inspectTime.x, inspectTime.y)
                                          : Random.Range(watchTime.x, watchTime.y);
                    break;
                default: duration = turnAwayTime; break;
            }
        }
        phaseStart = Time.time;
        phaseEnd = Time.time + duration;
    }

    void UpdateCaptor()
    {
        float t = Mathf.InverseLerp(phaseStart, phaseEnd, Time.time);
        float el = Time.time - phaseStart;
        switch (phase)
        {
            case Phase.Distracted: attention = 0f; break;
            case Phase.Glance:
                if (el < glanceUpTime) attention = Mathf.Lerp(phaseStartAttention, glancePeak, el / glanceUpTime);
                else if (el < glanceUpTime + glanceHold) attention = glancePeak;
                else attention = Mathf.Lerp(glancePeak, 0f, (el - glanceUpTime - glanceHold) / glanceDownTime);
                break;
            case Phase.Turning: attention = Mathf.Lerp(phaseStartAttention, 1f, t); break;
            case Phase.Watching: attention = 1f; break;
            case Phase.TurningAway: attention = Mathf.Lerp(phaseStartAttention, 0f, t); break;
        }

        if (Time.time < phaseEnd) return;
        switch (phase)
        {
            case Phase.Distracted:
                if (Random.value < spontaneousGlanceChance) { glancesIdle++; EnterPhase(Phase.Glance); }
                else EnterPhase(Phase.Turning);
                break;
            case Phase.Glance: EnterPhase(Phase.Distracted); break;
            case Phase.Turning: EnterPhase(Phase.Watching); break;
            case Phase.Watching: inspecting = false; EnterPhase(Phase.TurningAway); break;
            default: EnterPhase(Phase.Distracted); break;
        }
    }

    // Noise while he's NOT looking draws him in. Noise while he IS looking is judged (Threshold).
    void ReactToNoise()
    {
        if (Attending) return;
        // Still mostly facing her while turning away: loud enough to catch = caught, not a reaction.
        if (phase == Phase.TurningAway && noise >= Threshold) return;
        if (noise >= inspectNoise || (phase == Phase.Glance && noise >= glanceNoise))
        {
            inspections++;
            noise = 0f;                       // that sound is spent; the next one catches her
            inspecting = true;
            EnterPhase(Phase.Turning);
            lastResult = "HE HEARD THAT — coming over";
        }
        else if (phase != Phase.Glance && noise >= glanceNoise)
        {
            glancesHeard++;
            noise = 0f;
            EnterPhase(Phase.Glance);
            lastResult = "he glances over";
        }
    }

    void Caught(bool seenFreeHands)
    {
        catches++;
        if (seenFreeHands) seenCatches++;
        noise = 0f;
        handsOut = false; hidingUntil = 0f;
        lastCatchSeen = seenFreeHands;
        inspecting = false;

        if (catches >= maxCatches)
        {
            End(seenFreeHands ? "TAKEN — he saw your hands. Somewhere worse."
                              : "TAKEN — he's moving you somewhere worse");
            return;
        }

        foreach (var b in bonds)
        {
            b.progress = Mathf.Max(0f, b.progress - setback);
            b.tightness += tightenPerCatch;
        }

        if (seenFreeHands)
        {
            // Escalation: he knows she got her hands out. Wrists from scratch, extra tight.
            var w = bonds[wristsIndex];
            w.progress = 0f;
            w.tightness += freeHandsExtraTighten;
            selected = wristsIndex;
        }

        // He re-tied them — new knots.
        for (int i = 0; i < bonds.Length; i++) { knots[i].Clear(); FillKnot(i); }
        if (selected < 0 || bonds[selected].progress >= 1f) selected = NextUnfreed(-1);

        lockedUntil = Time.time + retieTime;
        glareUntil = lockedUntil + glareTime;
        EnterPhase(Phase.Watching, retieTime + glareTime);   // he stares after re-tying
        lastResult = seenFreeHands
            ? $"CAUGHT WITH FREE HANDS ({catches}/{maxCatches}) — wrists from scratch, tighter"
            : $"CAUGHT ({catches}/{maxCatches}) — re-tying, tighter";
    }

    // ---------------- player ----------------

    void Pull()
    {
        float now = Time.time;
        bool spam = now - lastPull < spamGap;
        lastPull = now;

        if (selected < 0) return;

        if (spam) { spams++; noise = Mathf.Clamp01(noise + noisePerSpam); lastResult = "SPAM"; return; }
        pulls++;

        float beatPhase = (now % beatInterval) / beatInterval;      // 0..1
        float error = Mathf.Min(beatPhase, 1f - beatPhase) * 2f;    // 0 on beat, 1 off
        float accuracy = 1f - error;
        accSum += accuracy;

        if (accuracy >= goodWindow)
        {
            goodPulls++;
            if (CanUntie(selected)) struggledUntieable++;
            var b = bonds[selected];
            float mult = HandsFree ? freeHandsStruggleMult : 1f;
            b.progress = Mathf.Clamp01(b.progress + gainPerPull * mult * accuracy / b.tightness);
            noise = Mathf.Clamp01(noise + noisePerPull);
            lastResult = $"pull {accuracy:P0}";
            if (b.progress >= 1f) OnFreed(b);
        }
        else
        {
            noise = Mathf.Clamp01(noise + noisePerMiss);
            lastResult = $"off-beat {accuracy:P0}";
        }
    }

    void Untie(int dir)
    {
        if (!HandsFree) { lastResult = "hands are bound — struggle"; return; }       // no effect, no noise
        if (!CanUntie(selected)) { lastResult = "can't reach that one — struggle"; return; }
        if (hidingUntil > 0f) return;                                               // busy hiding

        handsOut = true;
        var b = bonds[selected];
        var k = knots[selected];

        if (dir == k[0])
        {
            unties++;
            k.RemoveAt(0);
            FillKnot(selected);
            b.progress = Mathf.Clamp01(b.progress + untieGain / b.tightness);
            noise = Mathf.Clamp01(noise + noisePerUntie);
            lastResult = "untying";
            if (b.progress >= 1f) OnFreed(b);
        }
        else
        {
            fumbles++;
            noise = Mathf.Clamp01(noise + noisePerFumble);
            lastResult = "fumble";
        }
    }

    void OnFreed(Bond b)
    {
        selected = NextUnfreed(selected);   // auto-advance so no press is wasted
        lastResult = b == bonds[wristsIndex] ? "WRISTS FREE — untie the rest, keep them hidden" : $"{b.name} FREE";
    }

    void FillKnot(int i)
    {
        var k = knots[i];
        while (k.Count < KnotPreview)
        {
            int prev = k.Count > 0 ? k[k.Count - 1] : -1;
            int d;
            do d = Random.Range(0, 4); while (d == prev);   // no repeats: reads as a path
            k.Add(d);
        }
    }

    // Next still-bound bond after `from`, in array order, wrapping. -1 if all free.
    int NextUnfreed(int from)
    {
        for (int k = 1; k <= bonds.Length; k++)
        {
            int i = (from + k + bonds.Length) % bonds.Length;
            if (bonds[i].progress < 1f) return i;
        }
        return -1;
    }

    bool AllFree()
    {
        foreach (var b in bonds) if (b.progress < 1f) return false;
        return true;
    }

    void End(string text)
    {
        over = true;
        runIndex++;
        int nonSpam = Mathf.Max(1, pulls);
        string Avg(float sum, int n) => n > 0 ? $"{sum / n:F2}s" : "—";
        endText = $"{text}   (run {runIndex}, {Time.time - startTime:F1}s)\n" +
                  $"catches {catches} (seen {seenCatches})  pulls {pulls}  spam {spams}  " +
                  $"on-beat {goodPulls * 100 / nonSpam}%  avg acc {(accSum / nonSpam):P0}\n" +
                  $"unties {unties}  fumbles {fumbles}  struggled-untieable {struggledUntieable}\n" +
                  $"glances heard {glancesHeard} / idle {glancesIdle}  inspections {inspections}  knocks {knocks}\n" +
                  $"hides {hides}: on glance {glanceHides}/{glancesHeard + glancesIdle}  " +
                  $"on look {lookHides} (react {Avg(lookReactSum, lookHides)} / budget {Budget(turnTime):F2}s)  " +
                  $"on inspect {inspectHides} (react {Avg(inspectReactSum, inspectHides)} / budget {Budget(inspectTurnTime):F2}s)\n" +
                  $"hands out {handsOutTime:F1}s  closest call {closestCall:P0} (seen at {seenAttention:P0})";

        Debug.Log($"StruggleProto run {runIndex}\n{endText}");
        try
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "struggle_runs.txt");
            System.IO.File.AppendAllText(path, $"--- {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{endText}\n\n");
            savedPath = path;
        }
        catch (System.Exception e) { Debug.LogWarning($"StruggleProto: couldn't save run log: {e.Message}"); savedPath = ""; }
    }

    // ---------------- greybox UI ----------------

    [Header("Greybox UI")]
    [Range(1, 4)] public float uiScale = 2f;

    void OnGUI()
    {
        // Scale everything; layout below stays in unscaled coords. sw/sh = virtual screen.
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        float sw = Screen.width / uiScale, sh = Screen.height / uiScale;
        var prev = GUI.color;

        float bp = (Time.time % beatInterval) / beatInterval;
        float pulse = 1f - Mathf.Min(bp, 1f - bp) * 2f;             // 1 on beat

        GUI.Box(new Rect(20, 20, 60 + pulse * 120, 24), "PULSE");

        string padStatus = PadStatus();
        if (padStatus.Length > 0)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(220, 22, 400, 22), padStatus);
            GUI.color = prev;
        }

        for (int i = 0; i < bonds.Length; i++)
        {
            var b = bonds[i];
            string mark = i == selected ? "> " : "  ";
            string bar = new string('|', Mathf.RoundToInt(b.progress * 30));
            string state = b.progress >= 1f ? "  FREE" : "";
            // Show pull effectiveness (1 / tightness), not tightness — higher reads as better.
            string tight = b.tightness > 1f ? $"  {1f / b.tightness:P0} per pull" : "";
            string untie = b.progress < 1f && CanUntie(i) ? "  [untie]" : "";
            GUI.Label(new Rect(20, 60 + i * 22, 600, 22), $"{mark}{b.name,-8} {bar}{state}{tight}{untie}");
        }

        // hands + knot
        string hands;
        if (!HandsFree) hands = "bound";
        else if (hidingUntil > 0f) hands = "FREE — hiding...";
        else if (handsOut) hands = "FREE — OUT";
        else hands = "FREE — hidden";
        if (HandsFree && handsOut) GUI.color = Color.red;
        GUI.Label(new Rect(20, 150, 500, 22), $"HANDS  {hands}");
        GUI.color = prev;

        if (CanUntie(selected) && bonds[selected].progress < 1f)
        {
            var k = knots[selected];
            string seq = "";
            for (int j = 0; j < k.Count; j++) seq += (j == 0 ? $"[{Arrow[k[j]]}]" : $"  {Arrow[k[j]]}");
            GUI.Label(new Rect(20, 172, 500, 22), $"KNOT   {seq}");
        }

        // noise bar. Yellow ticks: glance / inspect (noise draws him in).
        // Red line: catch threshold. Drawn only when it can catch — during a glance the yellow
        // tick always fires first, so a red line there would be a lie.
        const float barX = 20f, barY = 202f, barW = 300f;
        bool redLive = Attending || (phase == Phase.TurningAway && Threshold < glanceNoise);
        GUI.Box(new Rect(barX, barY, barW, 18), "");
        if (noise > 0.01f) GUI.Box(new Rect(barX, barY, barW * noise, 18), "");
        GUI.Label(new Rect(barX + barW + 8, barY, 80, 20), "NOISE");
        if (!redLive)
        {
            GUI.color = new Color(1f, 0.85f, 0f, 0.8f);
            GUI.Box(new Rect(barX + barW * glanceNoise - 1, barY - 2, 2, 22), "");
            GUI.Box(new Rect(barX + barW * inspectNoise - 1, barY - 2, 2, 22), "");
            GUI.color = prev;
        }
        if (redLive)
        {
            GUI.color = Color.red;
            GUI.Box(new Rect(barX + barW * Threshold - 2, barY - 4, 4, 26), "");
            GUI.color = prev;
        }

        string captor = phase switch
        {
            Phase.Distracted => "on his phone",
            Phase.Glance => "glances over",
            Phase.Turning => inspecting ? "HEARD THAT — COMING OVER" : "LOOKING UP...",
            Phase.Watching => inspecting ? "INSPECTING" : "WATCHING",
            _ => "looking away",
        };
        if (Attending) GUI.color = Color.red;
        else if (phase == Phase.Glance) GUI.color = Color.yellow;
        GUI.Label(new Rect(20, 232, 500, 22), $"CAPTOR  {captor}  (attention {attention:P0})");
        GUI.color = prev;

        bool locked = Time.time < lockedUntil;
        GUI.Label(new Rect(20, 256, 600, 22), locked ? "...being re-tied" : lastResult);
        GUI.Label(new Rect(20, 280, 500, 22), $"catches {catches}/{maxCatches}");

        if (over)
        {
            string footer = "\nR / Start to restart" + (savedPath.Length > 0 ? "   ·   saved to log" : "");
            GUI.Box(new Rect(20, 312, 640, 170), endText + footer);
        }

        if (showCard)
        {
            const float cw = 300f, ch = 196f;
            string card =
                "Get every bond free. Don't get caught.\n\n" +
                "Choose bond      WASD  /  left stick\n" +
                "Struggle         Space  /  RT   (on the pulse)\n" +
                "Untie            IJKL  /  D-pad   (follow the knot)\n" +
                "                 once your hands are free\n" +
                "Hide hands       F  /  LB\n" +
                "Restart          R  /  Start\n\n" +
                "Tab / Back: hide this card";
            GUI.Box(new Rect(sw - cw - 20, 20, cw, ch), "");
            var cs = new GUIStyle(GUI.skin.label) { wordWrap = false };
            GUI.Label(new Rect(sw - cw - 10, 28, cw - 20, ch - 16), card, cs);
        }

        // catch feedback: strong flash during re-tie, faint tint + warning during glare
        if (!over && Time.time < glareUntil)
        {
            bool retie = Time.time < lockedUntil;
            GUI.color = new Color(1f, 0f, 0f, retie ? 0.35f : 0.12f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = prev;
            var big = new GUIStyle(GUI.skin.label) { fontSize = retie ? 48 : 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            string banner = retie
                ? (lastCatchSeen ? $"HE SAW YOUR HANDS  {catches}/{maxCatches}" : $"CAUGHT  {catches}/{maxCatches}")
                : "HE'S STARING — HOLD STILL";
            GUI.Label(new Rect(0, sh * 0.35f, sw, 80), banner, big);
        }
    }
}
