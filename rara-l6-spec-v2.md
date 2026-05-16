# Rara — Level 6 Spatial Spec (v2)

**Status:** Design locked. Ready to drive Unity geometry pass.
**Purpose:** Source of truth for L6 spatial design, mutter chain, timer behavior, and failure loop mechanics.

---

## 1. Level Premise

Cassie is bound to a chair in the center of a mid-tier business hotel suite, under the loose supervision of a guard who is on a call in the hallway outside. There are two chairs in the room — Cassie's (Chair A), dragged to the center, and a second chair (Chair B) at a small table against the west wall. The nightstand to her right holds a lamp; she suspects the drawer holds something more useful. She has three viable escape paths and a soft timer dictated by the guard's attention. The emotional arc is dry annoyance to mild triumph — Cassie is unruffled, calculating, and willing to look dumber than she'd like in pursuit of getting out.

---

## 2. New Mechanics Introduced

L6 is Act 2's opening salvo. The chair-tip mechanic, soft timer, offstage guard audio, and failure-loop mutter sequence all debut here. Nightstand interaction and tool-on-floor pickup carry forward from L2/L3 templates.

| Mechanic | Debut in L6? | Notes |
| :---- | :---- | :---- |
| Chair-tip rocking (Shift+A/D) | ✅ first taught here | `rockingEnabled = true` on this level only so far |
| Soft timer | ✅ first taught here | Triggers: lamp smash OR chair-tip crash (first occurrence wins) |
| Offstage guard audio | ✅ first taught here | Diegetic, stationary, hallway source |
| Failure-loop mutter sequence | ✅ first taught here | Beat 6: guard sets bond → Cassie reacts |
| Nightstand interaction | Reused from L3 template | Drawer jostle opens drawer; lamp on top responds to physics impulse |
| Tool-on-floor pickup | Reused from L2 template | Carries over: lamp shards, chair shards, radiator edge all use this |

---

## 3. Top-Down Room Sketch

Clean diamond layout. Door north (where guard is), window south (Cassie's exit), nightstand east, Chair B/table west. Cassie at center facing the door.

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
- W = window/exit (south wall, ~center)
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
| 3 | Nightstand proximity (drawer focus) | Player enters trigger near nightstand | Trigger radius: 2u (revisit in playtest) |
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

**Failure trigger**: timer expires before Cassie escapes through the window.

**On failure**: guard returns to the room (offstage → onstage briefly, but never rendered — implied through audio + state changes during a brief fade or blackout), re-binds Cassie in whichever chair is still intact, escalates her bonds, and resets specific environmental state. Then leaves.

### Attempt state matrix

| Attempt | Cassie's chair | Lamp | Pen in drawer | Chair A shards | Chair B shards | Bonds |
| :---- | :---- | :---- | :---- | :---- | :---- | :---- |
| 1 | Chair A (center) | Intact, on nightstand | Yes | — | — | Wrists |
| 2 | Whichever chair is intact (center) | Smashed-state persists; not respawned | Persists only if **not** picked up on attempt 1 | Persists if A was broken | Persists if B was broken (rare on attempt 2) | Wrists + Elbows |
| 3 | Whichever chair is intact, or floorbound if both broken | Same persistence rule | Same persistence rule | Persists | Persists | Wrists + Elbows + Ankles + Knees |
| 4+ | Same as attempt 3 (max state) | Same | Same | Same | Same | Cap: Wrists + Elbows + Ankles + Knees |

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

1. **Patient (drawer path)**: Jostle drawer until it opens, pick up pen, use pen to cut bonds, escape. Timer may never start if no lamp smash and no chair tip.
2. **Loud (lamp smash path)**: Jostle nightstand until lamp falls and smashes, tip chair, crawl to lamp shards, use shard to cut bonds, escape. Timer starts on smash.
3. **Fast (chair-tip-first path)**: Tip chair immediately, use chair shards from the broken chair to cut bonds, escape. Timer starts on tip-crash.

Plus, on attempt 3 floorbound (or any floorbound state): **radiator edge** is always available as a last-ditch tool.

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
| Jostling causes physics consequences | Player sees lamp wobble when hopping near nightstand; if they keep going, it falls |
| Soft timer | Beat 4 mutter (Cassie reacts to noise) + Beat 5 mutter (guard pressure) — player infers from context, no UI |
| Offstage guard | Audio cue ramping at Beat 5; guard mutter through diegetic source on failure |
| Failure loop | Experienced, not explained — first failure plays Beat 6a/6b, player sees bonds escalate, environmental state shift |

---

## 10. Open Questions / Risks

- [ ] Lamp physics calibration: mass and impulse threshold for "wobble vs. fall." Use kickable test scaffold (currently parked — promote to next-session if L6 implementation starts).
- [ ] Drawer-jostle implementation: does this reuse L3's exact mechanic, or is it physics-based like the lamp? Decide before implementing.
- [ ] Guard-return-on-failure presentation: brief fade-to-black? Audio-only montage? Cut directly? Tonally significant.
- [ ] First Beat 6 mutter content: needs writing (Jack to author, per character voice precedent).
- [ ] How many distinct Beat 6 mutter variants to author for failure-count variation (1, 2, 3, max)?

---

## 11. Out-of-Scope for L6

Things that might tempt you but belong in L7+ or post-v0:

- [ ] Visible guard sprite/model
- [ ] Multiple guards
- [ ] Body-part-specific bonds (parked in ideas.md)
- [ ] **ChairRestraint back-up verb** (would enable drawer-from-floor; parked to ideas.md)
- [ ] **Stand Up from FloorRestraint** (debuts L7)
- [ ] **Follow camera mode** (parked to ideas.md — revisit if Act 2 tone demands tighter framing)
- [ ] Combinatorial puzzle paths (e.g., shard + drawer-key combined solution)
- [ ] Game-over screen design — N/A, indefinite loop with bond cap

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
