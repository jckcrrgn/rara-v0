# Rara — Level 6 Spatial Spec (v2)

**Status:** Design locked. Ready to drive Unity geometry pass.
**Day 57 revision:** Escape is defined as the bond-cut, not window traversal. Cutting the bonds frees Cassie completely — a free Cassie is out, and the window is the fiction's implied exit, never a played walk/crawl. This keeps L6's ending consistent with L1–L5's grammar (free of bonds = level complete) and avoids introducing a free-locomotion verb. See §7. The interruptible-untie idea that motivated a longer escape is deferred to a future Feign mechanic (§11).
**Purpose:** Source of truth for L6 spatial design, mutter chain, timer behavior, and failure loop mechanics.

---

## 1. Level Premise

Cassie is bound to a chair in the center of a mid-tier business hotel suite, under the loose supervision of a guard who is on a call in the hallway outside. There are two chairs in the room — Cassie's (Chair A), dragged to the center, and a second chair (Chair B) at a small table against the west wall. The nightstand to her right holds a lamp; she suspects the drawer holds something more useful. She has three viable escape paths and a soft timer dictated by the guard's attention. The emotional arc is dry annoyance to mild triumph — Cassie is unruffled, calculating, and willing to look dumber than she'd like in pursuit of getting out.

---

## 2. New Mechanics Introduced

L6 is Act 2's opening salvo. The chair-tip mechanic, soft timer, offstage guard audio, and failure-loop mutter sequence all debut here. The ChairRestraint back-scoot verb and the bound-hands drawer-open interaction also debut here — both are general-purpose verbs that will recur in future levels, not L6-exclusive. Tool-on-floor pickup carries forward from L2 template.

| Mechanic | Debut in L6? | Notes |
| :---- | :---- | :---- |
| Chair-tip rocking (Shift+A/D) | ✅ first taught here | `rockingEnabled = true` on this level only so far |
| ChairRestraint back-scoot (hold S) | ✅ first taught here | Hold-S coroutine cadence (lunge + settle + inter-cycle), mirrors FloorRestraint.Inch but smaller magnitude. Foot-push fiction: chair-back prevents lunging backward, so motion is small shimmies. Positioning verb for bound-hands interactions. |
| Bound-hands drawer open (E, back-facing) | ✅ first taught here | `Drawer.requireBackFacing` gates Open() on player back-facing the drawer (dot product against `backFacingThreshold`, default 0.7). General verb — applies to any back-facing interaction in chair-bound OR floor-bound state. Floor-bound version is parked until a level needs it. |
| Soft timer | ✅ first taught here | Triggers: lamp smash OR chair-tip crash (first occurrence wins) |
| Offstage guard audio | ✅ first taught here | Diegetic, stationary, hallway source |
| Failure-loop mutter sequence | ✅ first taught here | Beat 6: guard sets bond → Cassie reacts |
| Tool-on-floor pickup | Reused from L2 template | Carries over: lamp shards, chair shards, radiator edge all use this |
| PointTool struggle progression | Existing system | Pen-on-rope: `pointProgress=5` per Struggle against `bondStrength=25` = 5-press cut |

---

## 3. Top-Down Room Sketch

Clean diamond layout. Door north (where guard is), window south (Cassie's implied exit — see §7), nightstand east, Chair B/table west. Cassie at center facing the door.

```
                    HALLWAY (guard offstage, stationary)
                              |
              +---------------D---------------+
              |               ^               |
              |             [door]            |
              |                               |
              |                               |
              |                               |
              | [table]                       |
              | [ChB ]         C-A    [nstd]  |
              |                ^      [lamp]  |
              |                |              |
              |                               |
              |                               |
              |                               |
              |               [W]             |
              |          [window/exit]        |
              +-------[radiator under W]------+

  Room: ~12u wide x 8u tall (rectangular, longer east-west)
  Camera: fixed, whole room visible
```

**Legend:**

- C-A = Chair A, Cassie's starting position, facing door (north)
- ChB = Chair B, at small table against west wall (visible from level start)
- table = small table, west wall (Chair B's home position)
- nstd = nightstand, east wall (against bed-implied area, which is offscreen as part of the suite framing)
- lamp = lamp on top of nightstand
- D = door to hallway (north wall, ~center)
- W = window (south wall, ~center) — Cassie's *implied* exit. The level completes on the bond-cut (a free Cassie is out), not on reaching the window. No played traversal.
- radiator = under the window (south wall)
- G = guard audio source (north of door, in hallway, stationary)

### Camera framing

- **Mode**: Fixed camera, whole room visible.
- **Visibility from start**: Player sees everything — Cassie, both chairs, nightstand+lamp, window, radiator. The spatial puzzle is about traversal and choice, not discovery.
- **Parked**: Follow camera mode (see §11 / ideas.md).

---

## 4. Position & Distance Table

First-pass coordinates. Adjust in Unity once geometry is in.

| Object | Position (x, y) | Notes |
| :---- | :---- | :---- |
| Cassie's chair start (Chair A) | (6, 4) | Origin of attempt — center of room, faces north |
| Chair B (at table, attempt 1) | (2, 5) | West wall, ~Cassie's left rear |
| Small table | (1.5, 5) | West wall, holds Chair B at start |
| Nightstand | (10, 4) | East wall, ~Cassie's right |
| Lamp | (10, 4.5) | On top of nightstand |
| Drawer (in nightstand) | (10, 3.8) | Interactable face |
| Door | (6, 7) | North wall, ~center |
| Window/exit | (6, 1) | South wall, ~center |
| Radiator | (6, 1.2) | Just below/in front of window |
| Guard audio source | (6, 8) | North of door, in hallway, stationary |
| Wall bounds | (0,0) to (12,8) | Room dimensions |

### Rocking-arc / chair-tip notes

- Tip mechanism: ChairTipMarker collision, `rockAngularImpulse = 0.6` (verify with kickable test scaffold — parked).
- Expected rocks to tip: ~3 (physics-variable, depends on impulse accumulation).
- Floor space chair occupies after tip: ~1u along tip direction.
- Tipped chair does not block any traversal path.
- **Tip can happen from anywhere in the room** — tipping is for chair-break + floor-access, not for puzzle interaction with nightstand. The jostle verb handles the nightstand.

---

## 5. Mutter Chain → Geometry Mapping

7 beats total. Beat numbering updated to reflect the redesign (drawer-focused Beat 3, smash-reactive Beat 4).

| # | Beat | Trigger | Spatial requirement |
| :---- | :---- | :---- | :---- |
| 1 | Entry | LevelManager.Start (via `entryMutter`) | None — fires on load |
| 2 | 5-struggle failure | StruggleSystem counter hits 5 | None — counter-based |
| 3 | Nightstand proximity (drawer focus) | Player enters trigger near nightstand | Trigger radius: 2u. Mutter content nudges Cassie toward the drawer; player has to discover S + back-facing on their own (diegetic teaching, no rhythm or verb explanation) |
| 4 | First loud event reaction ("...shit") | Lamp smash OR chair-tip crash, whichever fires first | Trigger from either event; timer also starts here |
| 5 | Offstage guard pressure (~50% timer) | LevelTimer threshold event | Spatial: guard audio cue ramps |
| 6a | Guard sets bond (failure loop) | Failure-loop trigger (timer expires) | Audio source position drives stereo cue |
| 6b | Cassie indignation response | Queued after 6a | Inter-mutter gap: **target 0.6–1.0s** — tune in playtest |

**Note on Beat 6 pacing:** The queue system supports the guard→Cassie sequence mechanically. The risk is tonal — too tight reads scripted, too loose reads broken. Land the gap once guard grunt clips exist; placeholder spec is 0.6–1.0s.

**Note on Beat 6 variation:** With indefinite loop + escalating bonds, Beat 6a/6b should vary by failure count. First failure: composed annoyance. Third failure: more pointed (both guard and Cassie). Content authoring task, not engineering.

---

## 6. Soft Timer Spec

| Property | Value | Rationale |
| :---- | :---- | :---- |
| Total duration | 120s | Pressure, not panic. Tune in playtest. |
| **Trigger source** | **Lamp smash OR chair-tip crash, first occurrence** | Both are diegetic noise events that draw guard attention. Pre-trigger, no timer. |
| 50% threshold event | Trigger Beat 5 mutter (offstage guard pressure) | Audio cue ramps |
| 100% threshold event | Trigger failure loop (Beat 6a → 6b sequence, re-bind) | |
| Visibility | Mutter UI positioned over the hallway/exterior side of the door wall to highlight the source is the guard, not Cassie. Paired with GuardMutter audio routed through the guard's diegetic AudioSource. No countdown UI. | Diegetic-only |
| Reset on retry | Yes (timer fully resets on each new attempt) | |

---

## 7. Failure Loop Mechanics

**Failure trigger**: timer expires before Cassie cuts her bonds. Cutting the bonds (pen, chair shards, or lamp shards) frees her — the wrist-cut frees her hands to undo the rest, so the cut *is* the escape. The window is the fiction's implied exit, not a played traversal: a free Cassie is out. The race the timer creates is getting *to* a tool and cutting before the guard checks, not getting to the window. (Engineering: the level completes on `PlayerController.OnPlayerFreed`, which fires once when `bond.OnBroken` fires — same hook L1–L5 complete on.)

**On failure**: guard returns to the room (offstage → onstage briefly, but never rendered — implied through audio + state changes during a brief fade or blackout), re-binds Cassie in whichever chair is still intact, escalates her bonds, and resets specific environmental state. Then leaves.

### Attempt state matrix

| Attempt | Cassie's chair | Lamp | Pen in drawer | Chair A shards | Chair B shards | Bonds |
| :---- | :---- | :---- | :---- | :---- | :---- | :---- |
| 1 | Chair A (center) | Intact, on nightstand | Yes | — | — | Wrists |
| 2 | Whichever chair is intact (center) | Smashed-state persists; not respawned | Persists only if **not** picked up on attempt 1 | Persists if A was broken | Persists if B was broken (rare on attempt 2) | Wrists + Ankles |
| 3 | Whichever chair is intact, or floorbound if both broken | Same persistence rule | Same persistence rule | Persists | Persists | Wrists + Ankles + Elbows |
| 4+ | Same as attempt 3 + knees | Same | Same | Same | Same | Cap: Wrists + Ankles + Elbows + Knees |

### Persistence rules (what carries over, what resets)

**Resets each attempt:**
- Cassie's position (back to center, in remaining chair, or floorbound if both broken)
- Chair B's position (moved from table to center if needed)
- Drawer state (re-closed)
- Timer (fully reset, not running)

**Persists across attempts:**
- Lamp state (smashed stays smashed; shards stay on floor)
- Pen state (in drawer if not picked up; gone if picked up before failure)
- Chair shards (both A and B, if broken — guard does not clean these)
- Bonds (escalate by one tier per failure, cap at max)
- Radiator (always present, never affected by attempts)

### Why the pen-only-removed-if-picked-up rule matters

The guard reacts to what he *sees* Cassie do, not what she *could have* done. Opening the drawer is safe; picking up the pen is committing. If she opens the drawer and decides not to commit, the pen persists for future attempts. This rewards observation and creates a real strategic choice.

### Why chair shards aren't cleaned

The guard cleans evidence Cassie could exploit (lamp shards, which scream "tool source"). He doesn't think to clean the chair he just re-tied her to — it's furniture damage from his perspective. His theory of what Cassie can use is incomplete, and the player can exploit that.

### Three viable escape paths (any attempt)

1. **Patient (drawer path)**: Back-scoot (hold S) into position with back to the nightstand, press E to open the drawer (gated on back-facing — see `Drawer.requireBackFacing`), press E again to pick up the pen, Struggle (Space) until bond breaks. Pen is a PointTool — `pointProgress=5` against rope `bondStrength=25` = 5 Struggles to cut. Timer never starts if no lamp smash and no chair tip; this is the silent route.
2. **Loud (lamp smash path)**: Jostle nightstand until lamp falls and smashes, tip chair, crawl to lamp shards, use shard to cut bonds, escape. Timer starts on smash.
3. **Fast (chair-tip-first path)**: Tip chair immediately, use chair shards from the broken chair to cut bonds, escape. Timer starts on tip-crash.

Plus, on attempt 3 floorbound (or any floorbound state): **radiator edge** is always available as a last-ditch tool.

### Why Chair B exists

Chair B is room-consistency insurance for the failure loop. If Cassie tips
and breaks Chair A on attempt 1, then fails the level, the guard needs
*something* to re-bind her to. Without Chair B, the room either contradicts
itself (Chair A magically reappears) or the player gets thrown into a
floorbound state on attempt 2 that the level isn't designed for as the
default. Chair B handles this: on failure, if Chair A is broken, the guard
drags Chair B from the west wall to center and binds Cassie to it. The
attempt matrix in this section captures the resulting state.

Chair B is also tippable on its own — once it's the "active" chair, the
chair-tip escape path is back in play.

### Loop cap behavior

Indefinite retries; bonds cap at max (attempt 3 state). The guard does not reset the scene. Cassie can fail forever in the max-bond state if she chooses. The "stuck" state is recoverable by choosing to escalate strategy (e.g., a pure-drawer-path player who's stuck can choose to break a chair).

---

## 8. Audio Plan

| Source | Type | Position | Notes |
| :---- | :---- | :---- | :---- |
| Offstage guard footsteps/grunts/call | Diegetic | (6, 8) — north of door, in hallway | Stereo cue — direction matters. Stationary. |
| Guard mutter audio | **Diegetic** | Guard AudioSource (same as above) | Routes through world-positioned AudioSource, fades with distance — spatial cue is the mechanic. NEW: extend MutterSystem to accept an optional AudioSource override. |
| Cassie mutter audio | Non-diegetic | None | Existing MutterSystem default channel |
| Chair rocking SFX | Diegetic | Chair transform | Verify exists from Day 35-37 work |
| **Chair-tip crash SFX** | Diegetic | Chair transform | **Timer-start event** when this is the first loud action |
| **Lamp smash SFX** | Diegetic | Nightstand top transform | **Timer-start event** when this is the first loud action |
| Tool drop SFX | Diegetic | Tool transform | Existing from chair-tip work |
| Drawer open SFX | Diegetic | Nightstand drawer transform | NEW — needs sourcing |

**Routing decision (locked):** Guard mutters route through the guard's world-position AudioSource. Spatial cue is the mechanic; consistent volume would defeat the design.

---

## 9. Teaching Moments

L6 teaches: rocking/tipping, jostling-the-nightstand-causes-physics-consequences, soft timer, guard presence, failure loop. Diegetic only — no mutter explains rhythm or mechanics directly.

| Mechanic | How player learns it |
| :---- | :---- |
| Rocking + tipping (Shift+A/D) | Trust panel + physics affordance — chair visibly wobbles on input |
| ChairRestraint back-scoot (hold S) | ControlHintsPanel exposes the verb; player discovers the discrete-cycle cadence through use |
| Bound-hands drawer interaction | Drawer sits slightly ajar at scene start, pen visible inside — telegraphs "this opens." Player discovers proximity + E by experimentation. Back-facing requirement is taught through failure feedback (`Cassie's hands can't reach` debug log on wrong angle; promote to diegetic mutter or UI cue in future polish pass) |
| Kicking the nightstand causes physics consequences | Player kicks the nightstand → lamp wobbles → eventually falls and smashes (Day 44–45 calibration: mermaid-kick at 0.8 modifier, lamp mass 2, nightstand mass 4 yields a 2–3-kick topple) |
| Soft timer | Beat 4 mutter (Cassie reacts to noise) + Beat 5 mutter (guard pressure) — player infers from context, no UI |
| Offstage guard | Audio cue ramping at Beat 5; guard mutter through diegetic source on failure |
| Failure loop | Experienced, not explained — first failure plays Beat 6a/6b, player sees bonds escalate, environmental state shift |

---

## 10. Open Questions / Risks

- [ ] Lamp physics calibration: mass and impulse threshold for "wobble vs. fall." Use kickable test scaffold (currently parked — promote to next-session if L6 implementation starts).
- [ ] Guard-return-on-failure presentation: brief fade-to-black? Audio-only montage? Cut directly? Tonally significant.
- [ ] First Beat 6 mutter content: needs writing (Jack to author, per character voice precedent).
- [ ] How many distinct Beat 6 mutter variants to author for failure-count variation (1, 2, 3, max)?

---

## 11. Out-of-Scope for L6

Things that might tempt you but belong in L7+ or post-v0:

- [ ] Visible guard sprite/model
- [ ] Multiple guards
- [ ] Body-part-specific bonds (parked in ideas.md)
- [ ] **Floor-bound back-up verb** (would enable drawer-from-floor; ChairRestraint version shipped Day 46, floor version parked to ideas.md until a level needs it)
- [ ] **Stand Up from FloorRestraint** (debuts L7)
- [ ] **Follow camera mode** (parked to ideas.md — revisit if Act 2 tone demands tighter framing)
- [ ] Combinatorial puzzle paths (e.g., shard + drawer-key combined solution)
- [ ] Game-over screen design — N/A, indefinite loop with bond cap
- [ ] **Feign-still-bound** — a future level may make *undoing the remaining bonds after the wrist-cut* a timed, interruptible process (guard returns mid-untie → Cassie feigns her hands are still bound until he leaves). In L6 the cut is instantaneous freedom; that is deliberate, and it is what keeps L6's escape a clean state-flip consistent with L1–L5. Feign debuts when a level needs the interruptible-untie tension — not here. (Origin: Day 57 design discussion — the question of whether escape should be a process rather than a state-flip. Answer for L6: state-flip. The process version lives in Feign.)

---

## 12. Definition of Done (for this spec)

This spec is ready to drive Unity work when:

- [x] Room sketch is legible enough to build from
- [x] Position table has real numbers (first-pass)
- [x] All mutter beats are mapped to spatial/temporal triggers
- [x] Soft timer duration has a defensible first guess + clear trigger source
- [x] Failure loop mechanics are unambiguous (attempt matrix, persistence rules, paths)
- [x] At least one playtest-the-spec pass (read it back to yourself, look for gaps)

**Status: DONE. Ready for geometry pass.**

---

## 13. Implementation Notes — LevelTimer + MutterSystem AudioSource Override

Design pass from Day 40 session. These two systems unblock §6 (soft timer) and §8 (diegetic guard mutter routing). Drop-in for a medium-energy session.

### 13.1 MutterSystem AudioSource override

**Decision: per-speaker AudioSource on SpeakerConfig, not per-call parameter.**

Reasoning:
- The AudioSource for the guard is a property of the *guard*, not of any individual line. One inspector field to update if the guard moves or his source is replaced — not N callers.
- SpeakerConfig already owns audio routing decisions (grunt pool, volume, pitch). AudioSource fits the existing abstraction.
- Preserves existing call signature. Every existing caller stays the same. New diegetic speakers just need their AudioSource wired in their SpeakerConfig.

**Implementation:**

Add to `SpeakerConfig`:

```csharp
[Tooltip("Optional. If set, grunts for this speaker route through this " +
    "world-positioned AudioSource instead of AudioManager's 2D channel. " +
    "Use for diegetic speakers (e.g. the offstage guard) where spatial " +
    "attenuation is the mechanic. Leave null for Cassie / non-diegetic.")]
public AudioSource audioSourceOverride;
```

Update `PlayGrunt`:

```csharp
private void PlayGrunt(SpeakerConfig config)
{
    if (config == null) return;
    if (config.gruntClips == null || config.gruntClips.Length == 0) return;

    AudioClip clip = config.gruntClips[Random.Range(0, config.gruntClips.Length)];
    if (clip == null) return;

    float pitch = Random.Range(config.gruntPitchRange.x, config.gruntPitchRange.y);

    if (config.audioSourceOverride != null)
    {
        config.audioSourceOverride.pitch = pitch;
        config.audioSourceOverride.PlayOneShot(clip, config.gruntVolume);
    }
    else
    {
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.PlaySFX(clip, config.gruntVolume, pitch);
    }
}
```

**Gotcha to note in the code comment:** `PlayOneShot` with `pitch` set on the source applies pitch to all sounds currently playing on that source, not just the one-shot. Fine for sparse, one-at-a-time grunts. If overlap is ever introduced, the routing approach needs revisiting.

**Scene-specific reference:** `SpeakerConfig` is serialized on MutterSystem, which is per-scene. Guard's AudioSource reference is therefore also per-scene, which is correct — different levels can have the guard at different positions, or no guard at all.

### 13.2 LevelTimer component

**Decision: standalone component, not bolted onto LevelManager.**

Reasoning:
- LevelManager handles level lifecycle (load/complete/restart). Timer is a gameplay system. Mixing them conflates concerns.
- Not every level has a timer. L1–L5 don't. L7+ might or might not. Keep LevelManager generic; bolt LevelTimer on per-level as needed.
- Singleton convenience: `LevelTimer.Instance` accessible from anywhere (same pattern as MutterSystem).

**API surface:**

```csharp
public class LevelTimer : MonoBehaviour
{
    public static LevelTimer Instance { get; private set; }

    [SerializeField] private float totalDuration = 120f;
    [SerializeField] private float[] thresholdsNormalized = { 0.5f };

    public UnityEvent OnTimerStart;
    public UnityEvent<float> OnThresholdReached; // passes the threshold value
    public UnityEvent OnTimerExpired;

    public bool IsRunning { get; }
    public float ElapsedNormalized { get; } // [0..1]

    public void StartTimer();
    public void StopTimer();
    public void ResetTimer();
}
```

**Three locked design decisions:**

1. **Threshold array, not single 50% callback.** Spec only calls out 50% today, but a 75% "guard getting close" beat is plausible later. Array future-proofs cheaply.

2. **UnityEvents for hookup, not direct method calls.** Lets you wire LevelTimer → MutterSystem.Play in the inspector without LevelTimer having a hard dependency on MutterSystem. Same pattern as existing level wiring.

3. **`StartTimer()` is idempotent.** Calling it while running is a no-op, not a restart. Critical for "lamp smash OR chair-tip crash, first occurrence wins" — both events call `LevelTimer.Instance.StartTimer()`, the first wins, the second is silently ignored. Do NOT expose a `Restart()` method; force callers to `ResetTimer()` + `StartTimer()` explicitly.

**Out of scope for v1:** countdown UI or visual indicator. Spec is diegetic-only. Use `Debug.Log` or inspector runtime values for debug visibility.

### 13.3 Wiring map for L6

- `LampSmashTrigger` → `LevelTimer.Instance.StartTimer()`
- `ChairTipMarker` (on crash detection) → `LevelTimer.Instance.StartTimer()`
- `LevelTimer.OnThresholdReached(0.5)` → `MutterSystem.Instance.Play("...", Guard)` (Beat 5)
- `LevelTimer.OnTimerExpired` → FailureLoopController (does not exist yet — next-after-next system)

Guard's AudioSource: child GameObject of the Door (offstage hallway position), wired into Guard's SpeakerConfig.audioSourceOverride field on the L6 MutterSystem.

### 13.4 Suggested implementation order (next session)

1. Add `audioSourceOverride` to `SpeakerConfig`, update `PlayGrunt`. ~15 min.
2. Wire temporary AudioSource in L6 (child of Door), set as Guard's override. Test via debug binding calling `MutterSystem.Instance.Play("test", Speaker.Guard)`. ~15 min.
3. Build `LevelTimer.cs` per §13.2 API. ~25 min.
4. Wire LevelTimer into L6 with placeholder triggers (debug key starts it, threshold callback `Debug.Log`s). ~10 min.

End state: both systems shipped, guard audio routing verified, L6 ready for failure-loop wiring next.
