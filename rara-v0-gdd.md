# Rara v0 — Game Design Document

> **Scope note (Rara Day 122).** This document governs *design* — verbs, levels,
> restraints, mutters, architecture. It does **not** govern schedule, platform, or
> release scope; `game-dev-plan.md` supersedes it on all three. Where this document
> still says "60 days", "itch.io", or "v0 ships in 9 weeks", read `game-dev-plan.md`
> instead. Cassie's model, proportions, and wardrobe are governed by
> `cassie-modeling-brief.md`, which is the source of truth for every number.

## One-Line Pitch
A low-poly noir escape game where a captured detective uses Struggle, Move, Pick Up, and Kick to break free from a series of restraint scenarios — grounded rooms, escalating stakes.

## Core Info
| | |
|---|---|
| **Engine** | Unity (latest LTS, URP) |
| **Platform** | PC (itch.io release) |
| **Perspective** | Third-person, fixed or semi-fixed camera per room |
| **Art Style** | Low-poly 3D, cel-shaded with bold outlines; noir lighting — hard directional light, crushed shadows, cool/warm color split |
| **Tone** | Dry humor, noir-to-thriller escalation |
| **Target Playtime** | 30–60 minutes *(v0 figure; 12 levels is not a 30-minute game)* |
| **Dev Timeline** | ~~60 days at 1 hr/day~~ → see `game-dev-plan.md` |
| **Target Release** | itch page (donations) live since Sep 19 2026. Free demo on itch **Sat Nov 21 2026**. Patreon deferred. See `game-dev-plan.md` |

---

## Concept

You're a detective. You got too close. Now you wake up restrained in a room — tied to a chair, cuffed to a pipe, duct-taped on the floor. Each level is a new room, a new predicament, a new escape.

The game starts grounded and noir. By the end, you're defusing a bomb in the villain's study, still bound, the stakes finally external. The detective's dry commentary holds it all together — they're annoyed at first, then nervous, then completely out of their depth but still cracking jokes.

---

## Core Mechanics

### The Four Verbs
Every puzzle is solved using four actions in creative combinations. Each verb has a **strength axis** — a property that modulates its effectiveness based on what the player has access to:

| Verb | Strength axis | Source |
|------|---------------|--------|
| **Struggle** | Tool held | Pickupable items (BoxCutter, Nails…) and environmental tools. ~~Barehanded Struggle produces no progress in v0.~~ Barehand progresses since Day 201; tools are multipliers. |
| **Move** | Movement mode | Restraint type (chair=hop, floor=inch/roll, hanging=swing) |
| **Pick Up** | Reach / range | Restraint type (hands-front=in front of you, hands-behind=behind you, future variants) |
| **Kick** | Leg state | Restraint type (free=full force, floor-bound=reduced, hogtied=disabled) |

This is a deliberate design pattern: **restraints modulate verbs**. New restraint types are interesting because each one changes how the four verbs behave. No new verbs needed for new puzzle situations — just new restraint configurations.

> **Superseded for bound play (Rara Day 201–202).** Barehand Struggle progresses, so Pick
> Up no longer gates escape. Tools multiply (blade ×2.5, point ×1.6, key ×4); only bonds
> with `barehandGain` 0 (cuffs) still need one. The paragraph below and the Pick Up
> bullets are the v0 legacy path (no `BondStruggle` on the player), kept as a record.

**Verb roles in the puzzle loop:** Pick Up is the *gating* verb — the player must acquire a tool before bonds can be broken. Struggle is the *closing* verb — once a tool is held, Struggle applies it. Move connects the two by getting the detective to where the tool is. Kick is a separate solve path used when no tool exists in scene (L4) or when something needs to be struck (Act 2+ Kickables).

#### Struggle — Strain against your restraints

> **Superseded for bound play (Rara Day 201).** Barehand Struggle now makes progress;
> tools and keys are multipliers, and some bonds still need one. The rhythm Struggle
> and the listening captor are written up in **Loop-Refine Pillars**, below. The
> v0 bullets are kept as a record.

- **Tool-gated:** Bare-handed Struggle produces no bond progress. The detective is bound by professionals — rope, zip ties, cuffs — not neighborhood kids. Effort SFX and animation still play (same "you tried" pattern as L4 prone-kick suppression), but bonds don't loosen without a tool.
- Once a tool is held, Struggle applies it. Effectiveness scales with tool sharpness/force (nail = slow, scissors = fast).
- Can knock over nearby objects.
- Makes noise (which can be good or bad in stealth segments).
- Effectiveness modified by restraint type (e.g., floor-bound uses whole body, slight bonus).
- *(v1 question: a fleshed-out Struggle verb — feeling for "sweet spots" with dual thumbsticks, rubbing across both — could re-justify universal barehand struggle as a meaningful verb in its own right. See "Future Ideas.")*

#### Move — Scoot, hop, roll, or drag yourself while bound
- Movement mode is restricted by how you're restrained (chair = hop/scoot, floor = inch *or* roll, hanging = swing)
- Some restraints offer multiple movement modes with different tradeoffs. Floor restraint: **inch** (tap W) is forward, slow, quiet, precise; **roll** (Shift+A or Shift+D) is lateral-only, fast, noisy, needs open space. Player chooses based on the situation.
- Positioning matters — you need to be near things to interact with them, and for some verbs (Kick) you need to be *oriented* correctly, not just close
- Moving into objects can knock them over or push them
- Movement noise is a stealth lever in Act 2+: rolling alerts guards, inching doesn't.

#### Pick Up — Grab an object within reach
- **The gating verb.** Without a tool in hand, Struggle does nothing. Pick Up is the action that unlocks bond-breaking.
- Hands tied in front: limited grab range in front of you
- Hands tied behind: grab things behind you (post-v0 mechanical variant)
- Objects modify Struggle effectiveness (sharp edge speeds up bond-breaking) or serve as puzzle elements
- Core loop: Move to find tools → Pick Up → Struggle with tool to escape

#### Kick — Strike outward with the legs
- Force scales with leg state. Free legs deliver full force. Floor-bound legs deliver reduced force (~half) — kicking is still possible but takes more reps. Hogtied legs deliver none — the verb is suppressed.
- Used against **Kickables**: doors that burst open, shelves that topple to drop a tool, guards that go down (finale).
- **Posture-gated** in some scenarios (e.g. L4 the van door). When the detective is prone, kicks don't land effectively — the legs can't deliver force face-down. Flipping to supine unlocks kick effectiveness. Out-of-posture kicks land as a "thud" — diegetic feedback that nothing happened.
- **Position-gating** (must be near AND oriented toward a target) is reserved for Act 2+ Kickables where appropriate.
- Kicking anything that isn't a Kickable just thuds. The detective can kick the wall of a van out of frustration. It does nothing useful. It feels right.

### Feign (State, not a Verb)

> **Updated Rara Day 202 (VS_Demo).** Hidden hands *are* feign (`IsFeigning`), toggled by
> Hide (LB / G). Feign no longer locks struggling. Untie, pick-up with free hands and the
> strike bring the hands out. The bullets below are the v0 design, kept as a record.

The detective can voluntarily re-enter a "looks bound" state after freeing themselves. This is not a fifth verb — it's a *state* the player toggles, modifying how the existing verbs behave.

- **Toggle**: F (or context prompt when a guard is approaching). Note: F is also Kick when not feigning. Context-sensitive — feign is only available post-escape, kick is the verb otherwise.
- **In Feign state**: Move is disabled (you're holding still). Struggle and Pick Up are disabled. **Kick is the only available verb** — the detective is coiled, waiting for the right moment to strike.
- **Visual tell**: Detective slumps into bound posture, restraints visually re-applied (loose, but reads as bound from a distance).
- **Purpose**: Lets the player weaponize stillness. Plants the seed in Act 2 (avoid detection by feigning), pays off in the finale (turn the tables on the gloating guard with a single decisive kick).

### Controls

> **v0 keyboard map, legacy path.** The BondStruggle path uses the Day 202 pad map — see
> **Merge into VS_Demo**, below.

- **WASD / Left Stick** — Move (contextual: hop, scoot, inch, swing based on restraint type)
- **Shift+A / Shift+D** — Roll left / right (floor restraint only; lateral-only, fast, noisy)
- **Space** — Struggle
- **E** — Pick Up (interact with nearest pickupable)
- **F** — Kick (default) / Feign toggle (when free of bonds and context-appropriate)
- **R** — Reset room to starting state
- **No inventory system** — you use objects in place or carry one thing at a time
- **No combat** — Kick is structural, not a combat verb; this is still a brain game

### Restraint Types (Vary Per Level)
- **Chair** — tied to a wooden/metal chair. Can hop, scoot, tip over. Classic. Kick disabled (legs anchored to chair).
- **Floor** — hands bound, lying down. Can inch or roll. Reduced-force kick available.
- **Cuffed to fixture** — handcuffed to a pipe, radiator, railing. Limited radius of movement. Free legs — full kick.
- **Hanging** — suspended by wrists. Can swing, kick, use momentum. Full kick force at the right swing point.
- **Hogtied** *(post-v0)* — wrists and ankles bound together. No kick. Movement reduced to wriggle.
- **Duct tape / zip ties** — can be weakened by Struggle over time, unlike rope or cuffs.

Each restraint type changes how the four verbs behave, giving levels distinct feel without adding new mechanics.

---

## Loop-Refine Pillars — Struggle vs. Captor (prototype, Rara Day 197–201)

> **Status.** Governs bound-state Struggle and captor attention. Source of truth for
> values: `StruggleProto.cs` (greybox scene, no Cassie, no existing scripts touched).
> **Not yet prototyped:** the free walk-around state, patrol/chase, drag-back,
> escalating restraint types, and the "taken" timer. The four pillars (stated Day 197):
> captor AI, free state, Struggle rework, attention as activation energy.

### Struggle while bound
- **One bond at a time.** A direction selects the bond and a pull works it.
  Progress = gain × accuracy ÷ tightness.
- **Pull on the pulse** (`beatInterval` 0.7 s). Accuracy below `goodWindow` 0.6 is
  off-beat: no progress, and it makes noise.
- **Spam** (pulls < 0.25 s apart) makes loud noise and no progress.
- **Even a clean pull makes some sound** (0.05). Steady on-beat pulling never
  reaches the glance threshold, so it can't draw him by itself. Off-beat pulls and
  spam are what draw him.

### Free hands: untie, and the risk flips
- Struggling the wrists free frees her hands. Bonds she can reach (not elbows) can
  then be **untied**: follow a knot sequence of directions, ~10 correct inputs per
  bond at tightness 1. Untying is quiet (0.01). A wrong direction is a fumble (0.1).
- Struggling still works, ×1.5 with free hands.
- **The risk flips from noise to being seen.** Untying puts her hands out, and he
  must not see free hands. Hide (F / LB) takes 0.4 s. He sees free hands at
  attention ≥ 0.75.
- **Hands stay out until she hides them**, including while she struggles. Nothing
  hides them automatically. Hiding is a conscious choice to feign (Day 201).
- **Caught with free hands = escalation:** wrists back to zero, +0.3 extra tightness
  on top of the normal catch.

### The captor listens — greybox proto rules (`StruggleProto` only)

> **Does not govern VS_Demo.** The Day 202 revision does: noise never catches her, it
> only decides how often he looks. Seen free hands is the only catch, and a catch
> resets her to start. See **Merge into VS_Demo**, below.

- **Attention as activation energy.** The noise that gets her caught slides from
  0.85 (on his phone) down to 0.045 (watching). While he's watching, any pull gets
  her caught. **While he's on his phone, noise never catches her. It draws him over.**
- **Two tiers of reaction:**
  - noise ≥ 0.3 → **glance**: a short partial look, peaking at 0.6. That's below
    0.75, so a glance can't see free hands. More noise ≥ 0.3 during a glance → inspect.
  - noise ≥ 0.6 → **inspect**: fast turn (0.8 s), long full look (4–7 s).
- **Spent noise.** The sound that drew him is zeroed. What catches her is the
  *next* sound. That's why an inspection works as a warning, not as a catch.
- **Randomise when he looks, never how fast he turns.** Phone spells are random
  (3–10 s), a spell ends in a glance 40% of the time, and look length is random.
  Turn speed is fixed (look 1.4 s, inspect 0.8 s). The turn is the tell, and players
  learn its speed.
- **Caught:** input locked 1.5 s while he re-ties, −0.3 progress on every bond,
  +0.3 tightness, new knots, then a 3 s stare. The third catch = taken (loss).

### Hide budgets
Time from the start of a turn to the last safe hide press.
Budget = turn time × 0.75 − 0.4.

| Turn | Budget |
|---|---|
| Look (1.4 s) | **0.65 s** |
| Inspect (0.8 s) | **0.20 s** |
| Inspect escalated from a glance (starts at 0.6) | **−0.10 s** — always seen |

So if her hands are out during a glance that escalates, she's always seen, and a
cold inspect leaves only 0.20 s. In practice, inspect punishes making noise with
your hands out. Whether that's intended is listed under Open.

### Locked values (Day 200)

| Field | Value | Why |
|---|---|---|
| `glanceNoise` | 0.3 | ~~Two quick off-beat pulls, or two spams~~ → re-derived Day 206: rushing or mashing, not honest misses. See **Noise thresholds** below |
| `inspectNoise` | 0.6 | One loud event, or escalation from a glance |
| `glancePeak` | 0.6 | Below `seenAttention`: a glance can't see hands |
| `turnTime` | 1.4 | Look budget 0.65 s; measured hides 0.40–0.53 s fit inside |

Current tuning, not locked: `noiseDecay` 0.1, `distractedTime` 3–10,
`spontaneousGlanceChance` 0.4, `untieGain` 0.1, `handsBackTime` 0.4,
`seenAttention` 0.75, `maxCatches` 3.

### Playtest data (Day 200 — dev's own runs, 8)
- **6 clean, 2 sloppy.** Clean: 35–51 s, 0 catches, 96–100% on-beat. Sloppy:
  46 s / 0 catches / 71%, and 78 s / 1 catch / 60%.
- **He reacts to how you play.** Glances he *heard* vs glances he took on his own
  (idle): 1:7 in clean runs → 13:2 in sloppy ones.
- **Inspections read as warnings:** 3 of 4 survived.
- **Hide reaction on looks: 0.40–0.53 s**, against the 0.65 s budget.
- **Seen with free hands: 0 times in 8 runs.**
- **Caveat:** at least 5 of the 6 clean runs used stale scene values (`noiseDecay`
  0.2, `turnTime` 1.2, `distractedTime` 4–9), set Day 200. Twice the noise decay
  means fewer heard glances, so the clean-vs-sloppy split mixes skill with tuning.
  Re-check it on the current values.

### Open
- [ ] **Seen never fired: skill, or the hands just weren't out?** About 20 untie
      inputs fit inside one phone spell. The Day 201 telemetry (closest call,
      hands-out time) settles it. Fallback: `untieGain` 0.07 (~14 inputs per bond).
- [ ] **Inspect escalated from a glance is always seen.** Intended punishment, or
      give a sliver of budget?
- [x] ~~Should struggling put her hands back automatically?~~ **No (Day 201).**
      Hands stay out after untying, including while she struggles, until the player
      chooses to hide them. Hiding is a conscious choice to feign.
- [ ] **Cold playtest** (Day 201 build): do testers get seen? Do they read a glance
      as "hide"? Do they describe him as a person or a metronome?
- [ ] Feel verdict: does he feel like a person or like RNG?

### Merge into VS_Demo (Rara Day 202)

> **Status.** Code written (`BondStruggle`, `BondStruggleHUD`, `RaraInput`, plus patches
> to PlayerController, ChairRestraint, GuardController and MutterSystem). Played in
> VS_Demo Day 202; decision 1 revised after first play. `StruggleProto` stays as the
> greybox reference.

**Opt-in, per level.** `BondStruggle` on the player means the new path runs. Without it,
the legacy single-`Bond` path runs (tool-gated, barehand = 0). L1–L6 are untouched.
Barehand progress makes their nail, cutter and scissors optional, so bringing them over
is a design pass, not a drop-in. That's tied to the Oct 2 "knit the scenes into one
level" pivot, and stays parked for the weekly review.

**Decisions (Day 202):**
1. **Captor = offstage, noise calls him.** The check-in loop stays. Offstage he's "on his
   phone": noise can't catch her. Glance-level noise → he calls out (background mutter,
   1.6 s alert window). Inspect-level noise, or glance-level again while he's alerted →
   hurried approach (2.5 s, 0.8 s turn). Approaching: attention 0 until the last
   `turnTime` (1.4 s), then it ramps. Door + lean-in = watching. Leaving ramps down
   over 0.8 s, then he's listening again and can turn back. The door evidence check is
   off on these levels. Inspection is continuous.
   **Revised Day 202 after first play: noise never catches her.** He doesn't punish
   sound. He likes watching her struggle. What he's stopping is the escape, so the
   only catch is free hands out at attention ≥ 0.75. Noise only decides how often he
   looks, and therefore how much untie time she gets. Every catch is a seen catch:
   wrists from scratch, extra tight. A catch returns her to her start position.
2. **LT held = move layer.** Left stick moves the chair only while LT (keyboard: Shift)
   is held. Otherwise it picks bonds. Every hop is noise (0.12; back-scoot 0.06).
3. **Bonds: wrists + L/R ankle at start. Elbows tied on at the first catch** (struggle-
   only, can't untie). Later catches only tighten. Escalation line: "My ELBOWS? Really?"
4. **Every bond free = slipped out.** A stub second win alongside the bottle strike.
   The guard stands down, Cassie says "Rookie knots.", and `OnPlayerFreed` fires. The
   real free state (pillar 2) replaces this.

**Hands = feign.** Hidden hands *are* `IsFeigning`, so CassieSitDriver's feign pose
follows. Feign no longer locks struggling. Untie, pick-up with free hands, and strike
bring the hands out. A bottle in free hands counts as hands out.

**Tools are multipliers:** blade ×2.5, point ×1.6, key ×4. The TableEdge reaches the
wrists only. A held tool works the wrists while she's bound, then the untieable bonds
once her hands are free. A bond with `barehandGain` 0 (cuffs) needs one.

**Catches:** the proto rules, applied under the fade. Respawn, disarm and the caught
lines are the VS ones. A seen catch has its own guard line. The 3rd catch = taken:
guard line over black, then the scene reloads.

**Pad map:** left stick bond · RT pull · D-pad untie · LB hide · LT move · A pick
up / dismiss mutter · X strike. Keyboard: WASD · Space · IJKL · G · Shift · E · H.

**Open (Day 202):**
- [ ] First play in VS_Demo. Start `offstageRange` at 8–16 s. With 10–22 s, a clean
      player can slip out before the first check-in (wrists ~11 s barehand, ~4–5 s on
      the TableEdge, ankles ~5 s to untie).
- [x] ~~The approach footsteps are a free-noise window.~~ Moot: noise no longer catches.
- [ ] "He likes it": should struggling in front of him keep him watching longer? That
      would make noise cost untie time while he's in the room as well.
- [ ] The beat now lives in Cassie (CassieStruggleDriver sways on the pulse, idle 0.4).
      The HUD pulse bar is gone. Next: bias the sway toward the selected bond.
- [ ] TableEdge ×2.5 on the wrists. It used to be silently blocked when the bottle was
      the nearest interactable. Fixed: environmental tools are now searched directly.
- [ ] L6 rocking has no binding on the move layer (Shift is taken). Rebind before L6
      opts in.
- [ ] Kick has no pad binding (suppressed in the chair anyway).
- [ ] The HUD is OnGUI greybox. The bonds need to show the rhythm on the model.

### Beat timing — body cue (Rara Day 203–204)

**Diagnosis (Day 203).** Pulling "with her" was consistently off, on the same clock,
with no drift. The Day 202 sway (`CosineHold`) held her at the extremes on the beat and
put her *fastest* motion, the snap through neutral, on the half-beat. **Players lock
onto the motion event, not the pose.** `YankOnBeat` fixes the shape: still at neutral,
a sharp peak on the beat, magnitude (1 − 2d)^`beatShape`, where d is the distance to the
nearest beat. Fastest motion, simulated: CosineHold −350 ms (the half-beat), YankOnBeat
k=2 at 0 ms.

**Telemetry.** `BondStruggle.Pull` logs each pull's signed error to the nearest beat
(+ = late). A TIMING summary prints on unload, on play stop, and from the ContextMenu
"Dump timing". Uses circular stats, because −340 and +340 ms are the same miss and a
plain mean cancels them.
- **offset:** circular mean error.
- **R:** consistency, 0–1.
- **In window:** share within ±140 ms (`goodWindow` 0.6 at `beatInterval` 0.7).
- **Near half-beat:** share 250 ms or more off.
- Spam is logged but left out of the summary. **The dump is cumulative since scene
  load**, so a mid-run dump includes everything before it.

**Runs (dev's own, HUD off).** Day 204 lines are labelled by sample size. Their
mapping to shapes was inferred from the numbers.

| Day | Line | Shape | In window | R | Offset |
|---|---|---|---|---|---|
| 203 | Run 1 | Cosine | 19% | — | **+325 ms** |
| 203 | Run 2 | Yank | 54% | 0.61 | −123 ms |
| 204 | n=50 (mid-run dump) | Yank | 76% | 0.87 | −93 ms |
| 204 | n=130 (same run, end) | Yank | 71% | 0.80 | −102 ms |
| 204 | n=76 | Cosine | 25% | 0.52 | **−252 ms** |
| 204 | ~~n=215~~ | mixed? | 43% | 0.08 | −105 ms |

n=215 is **discarded**. R 0.08 means no lock at all, and with 27% near the half-beat
it looks like both shapes were played in one run.

**Result.** Cosine locks near the half-beat twice (+325, −252). Yank hits the window
about 3× as often, with high R. **`YankOnBeat` is the code default (Day 204).**

**The steady early offset (~−100 ms) is perceptual, not lag.** Input lag pushes
offsets *late*, so it can't explain an early one. Held steady at high R across every
Yank line, it's where players hear the beat in her motion: the rise into the peak,
not the peak itself.

**Open:**
- [ ] `beatShape` 3 + `idleIntensity` 0.7, 3 Yank runs. A sharper yank should pull the
      perceived event later. This fixes the cue itself.
- [ ] If the offset is still ~−100 after that, add a judge offset (centre the window
      ~100 ms early). That's standard rhythm-game calibration, but it hides a cue
      problem, so it's the second resort.
- [x] ~~`noisePerMiss` 0.15 → 0.08.~~ **Day 206.** Reasoning re-derived in **Noise
      thresholds**, below. Feel check pending a clean day.

### Struggle on demand (Rara Day 211)

**She sits still until the player starts her.** The always-on idle sway (Day 202,
`idleIntensity` 0.4) is gone from the default. The first RT press starts a **bout**. That
press *is* beat 0: it isn't scored, makes no progress and costs one clean pull of noise.
From there the player holds the tempo (`beatInterval` 0.7). Any press, hit or miss, keeps
the bout alive. `boutTimeoutBeats` (2) with no press ends it and she settles. A catch ends
it too.

**Why.** The constant writhe read as an animation playing, not as her struggling. Still
by default also gives composure a body: in front of him, still is what calm looks like.

**Code.** `BondStruggle.boutMode` (off = the Day 202 global clock). `Beats`, `Pulse01` and
the pull phase measure from the bout's beat 0. `CassieStruggleDriver` holds the idle
floor only `InBout`.

**Voids the Day 204 timing baseline.** The −100 ms offset was measured on an always-on cue.
The `beatShape` 3 runs (clean day) now test this cue, not that one.

**Open:**
- [ ] Play: does the first press feel like starting her, or like a dropped input?
- [ ] Is 2 beats the right settle? Shorter is stiller, longer forgives a missed beat.

### Feedback channels (Rara Day 205)

**One channel per job.** Each cue answers exactly one question, so the player never has
two clocks to choose between.

| Channel | Job | Rule |
|---|---|---|
| **Body** (Cassie's sway) | **The beat.** When to pull. | The only beat cue in a player build. The motion event sits on the beat (see above). |
| **Audio** (hit / miss / spam) | **Hit confirm.** Did that pull land? | Plays *after* the press, never ahead of it. Never a beat cue. Miss must read as different from hit at a glance. |
| **Player readout** (bonds + selected, hands, knot, catches) | **State.** What can I act on? | Every build. Never the beat. Interim (Day 207): bond choice and knot move onto her body post-demo. |
| **Debug HUD** (noise, captor, attention, last result) | **Debug.** | F1, editor / Development only. Off for timing runs. Noise and attention are read from *him*, never a bar. |

**Why.** Day 203 showed players lock onto one motion event and ignore the rest. A
second beat source, such as a tick or a pulse bar, either agrees exactly with her body
and adds nothing, or disagrees and becomes the metronome. Then Cassie is decoration
again. Audio confirm after the press can't compete, because it answers a different
question. Keeping the beat in her body is also *capable, not helpless*: the rhythm is
hers, not the UI's.

Already true in code: the HUD pulse bar is gone (Day 202), and the SFX hooks fire
after `Pull` resolves (Day 204: `hitClip`, `missClip`, `spamClip`; unassigned =
silent).

**Open:**
- [ ] **Music vs. the beat.** Act 2 music is specced "pulsing, heartbeat-like". A pulse
      under a bound struggle is a second beat channel. Either the music drops its pulse
      while she's bound, or it locks to `beatInterval` and the body's ~−100 ms perceptual
      offset has to be settled first, or the two cues will disagree by 100 ms.
- [ ] Bias the sway toward the selected bond (carried from Day 202). The body is now
      the beat *and* the bond selector. Check that the second job doesn't blur the
      first.

### Diegetic feedback (Rara Day 207)

**Rule. While she struggles, the eyes stay on her.** The room is read for tools and
openings. The bonds are read off her body. The player readout (Day 207) is interim and
dies when this lands. **In the demo** (decided Day 207).

| Job | Channel | Status |
|---|---|---|
| Beat | Her sway (YankOnBeat) | Done |
| Hit / miss | Audio after the press | Done |
| **Which bond** | Head and eyes go to it (wrists, an ankle, over the shoulder for elbows). Sway biased toward that limb | Code Day 208 (`CassieStruggleDriver` Bond look). Values by eye, pending |
| **Progress** | The rope gives: slack and slide with progress, snap on free. Strain on that limb grows | Next. Needs ankle ropes: only the wrists have rope today |
| Hands out / hidden | Feign pose (`IsFeigning`) | Done |
| Catches | Elbows tied on, ropes tighter, his line | Done |
| **Knot** | Bight glyph pinned to the knot, pointing `knot[0]` (Day 212) | Code Day 212 (`BondStruggleHUD` knot cue). Interim for the 3D bight |

**The knot — open.** Its job is a **speed tier**: struggle (slow, hands can stay hidden)
→ untie (faster, hands out) → cut (future: fastest, needs a blade, hands out). Each step
up is faster and more exposed. The knot is the linchpin of a bond.

**Keep the sequence.** Following it reads as really untying a knot. The open question
is how the rope itself shows the next direction, with nothing on screen.
Considered, not chosen: she leads with a hand tug and you follow (A); drop the sequence
and untie on the beat (B); hold to untie; fingers at double time.

**Leading candidate (Day 208): the bight points the way.** A bight (a loop of rope)
stands proud of the knot, pointing the next direction on screen. A correct input pulls
it through, the knot loosens, and the next bight shows. A wrong input cinches it back
(the fumble noise is already in). A bight, not a tail: pulling a tail tightens a knot,
pulling a bight loosens it. Only `knot[0]` shows. Reading it puts the eyes on her ankle
while his threat is at the door, so untying costs attention as well as hidden hands.
Risk: the bight is small at over-the-shoulder distance. Fixes in order: oversize it with
a contrasting colour, let the Day 208 head-look lead the eye, nudge the camera toward the
ankle while she unties. Test: read the direction from a Game-view still in under 0.3 s.

**Decided Day 212: a screen glyph pinned to the knot, for the demo.** No ankle rope
exists yet, so a 3D bight would be a loop floating on bare ankles. It would also be small
at OTS distance. Instead: a rope-loop glyph (cream, dark outline, darker knot disc) drawn
at the knot's screen position. `CassieHandsDriver.TryKnotWorld` is the single source, so
the glyph and the IK hands share one point. Fixed size (7% of screen height), so hands and
knees never hide it. Only `knot[0]` shows. **Hit:** the loop pulls through its own way,
and the next one grows in (0.15 s). **Fumble:** it cinches, shrinking and flushing red
(0.22 s). Dimmed (0.45) while the hands are hidden and untie is available. Full strength
once they're out. The KNOT sequence line is debug-only (F1).
**Post-demo:** the same cue becomes a 3D bight on the real ankle rope.
- [ ] Read test: direction from a Game-view still in under 0.3 s.
- [ ] Does the glyph over her hands bother anyone? `knotCueStandOff` pushes it out.

**Bond look (Day 208).** Head eases to the bond (τ 0.08, ~0.25 s). The head's beat yaw
drops to ×0.3 while a bond is selected, so the head reads selection and the torso reads
the beat. Torso bias is a constant offset with the sway centred on it, so the beat event
doesn't move. Her ankles are tied together, so they're one **Ankles** bond with one look
(one knot, no side shift). Hands out with an ankle selected: head all the way down to the knot.
Hiding the hands lifts it. Defaults are unverified on this rig, so flip signs by eye.

**Bond selection (Day 208): relative, in screen space.** A flick moves the selection from
the current bond to the nearest bound bond that way *on screen*. Down from wrists is
ankles, up is elbows. Later, down from knees is ankles, and with each ankle tied to its
own chair leg, left and right step between them as the camera shows them. Each bond sits
at the midpoint of its anchor bones (default by limb, overridable per bond). Cone ±45°;
closest wins, alignment breaks near-ties. One flick, one step: holding doesn't repeat.
Nothing that way: stay put. A freed bond hands the selection to the nearest one still tied.
Why screen space: it never contradicts the picture, whatever the camera does.

**Elbows tied block untie (Day 208).** Elbows bound behind her = her hands can't reach the
knots in front. Escalation after the first catch: wrists, then elbows, then untie.

**Hands (Day 208): three states, three poses.** Mechanically the game draws hard lines
between bound, feigning and hands out, so the body has to as well.

| State | Arms | Reads as |
|---|---|---|
| Bound | Behind her back, wrists together, the grind | "Struggle" |
| Feigning | **Identical to bound**, plus the compliant dip. Player's tell: the slack wrist rope (with rope progress) | He can't tell |
| Hands out | Forward on the selected knot (`CassieHandsDriver`, two-bone IK). Each untie input tugs along the pressed direction, the bight's direction | "Exposed, working" |

Out in 0.25 s. Back in `handsBackTime` (0.4 s), which is the hide budget, so the picture
matches what he sees. Hands out with nothing untieable selected: hands in her lap. The
strike and a held item own the arms; the layer fades out for them. Knot = the bond's
anchor midpoint + `knotOffset` until the ropes carry it. IK runs in a post-apply pass on
`CassieRig` so it sees the torso lean. Knees (escalation, post-demo) are a new bond with
knee anchors: no new animation.
- [ ] Set targets, pole, lean in VS_Demo by eye (Debug Force Reach holds the pose).
- [ ] Untested: can she reach her ankles seated without the arms locking straight? If not,
      more lean, then knot higher on the rope.

- [ ] Set the look values in VS_Demo (selection holds, use the debug scrub to freeze a frame).
- [ ] Clean day: does the bias or the head-look blur the beat? (Timing runs vs −100 ms.)

### Noise thresholds (Rara Day 206)

**`noisePerMiss` 0.15 → 0.08.** Under the Day 202 revision noise never catches her, it
only decides how often he looks. So the question is *which play earns a look.*

**The rule this sets: honest misses are quiet; rushing and mashing call him.** Decay is
0.1/s, which is 0.07 per 0.7 s beat. A 0.08 miss at tempo nearly decays out before the
next pull. Pulling faster than the beat, or mashing, outruns the decay.

Simulated (no spend, first crossing; `noisePerPull` 0.05, spam 0.25, decay 0.1):

| Play | 0.15 (old) | **0.08 (new)** |
|---|---|---|
| 2 off-beat pulls, 0.26 s apart | peak 0.27, no glance | peak 0.13, no glance |
| All misses, at tempo (1 / beat) | glance 1.4 s, inspect 4.2 s | **glance 16 s, never inspect** |
| Half misses, at tempo | glance 4.9 s | **never** |
| Rushing, 2 pulls / beat, all miss | glance 0.7 s | glance 1.8 s (~6 pulls), inspect 4.2 s |
| Mash (miss + spams, 0.15 s) | glance 2nd press | glance 2nd press, inspect 3rd–4th |
| Clean pulls at tempo | never | never |

**The old "two quick off-beat pulls" was already wrong.** It peaks at 0.27 under decay
0.1 (it took three). It was probably calibrated on an earlier decay.

**What it costs.** Day 200's best signal was "he reacts to how you play": heard vs
idle glances 1:7 clean, 13:2 sloppy. At 0.08 a sloppy player *at tempo* no longer
draws him. Only rushing and mashing do. If the cold test reads him as random again,
this is the first knob. The middle is 0.10–0.12.

**Open:**
- [ ] Feel check at 0.08 on a clean day: does a sloppy run still feel watched?
- [ ] Hops. `noisePerHop` 0.12: two quick hops peak at 0.21, three at ~0.30, right
      on the line. The old tooltip said two hops = call-out. Decide: 0.15 (two hops
      call him) or leave it (moving the chair is cheap).

### Guard watch states (Rara Day 211)

> Replaces "noise never catches her" (Day 202 rev). A same-day draft (mash in front of
> him = warning, then catch) was dropped: it didn't fit the fiction.

**Three states.**
1. **Not paying attention** (offstage, on his phone). Noise calls him. As now.
2. **Watching** (drawn in by noise, or a routine check-in). Mashing amuses him. He taunts
   ("Putting on a show? Nobody can hear you."), and lingers to enjoy it, which costs untie
   time but never catches her. On-beat pulls are just squirming. **Misses are a tell**:
   each miss in his sight builds suspicion, and a tool miss (sawing the table edge) builds
   a lot. Full suspicion = he checks.
3. **Checking her bonds.** He walks in and looks. **Evidence decides:** any bond past ~50%
   = light catch (`RegisterCatch(false)`). Otherwise "Still tight." and he cinches the bond
   he checked (small setback). That's pillar 1's re-secure.

Free hands seen while he's watching are still the instant, heavy catch.

**Built (Day 211, `day211-guard-suspicion`).** Values, all on `GuardController`:

| Field | Value | Reads as |
|---|---|---|
| `suspicionPerMiss` | 0.35 | 3 barehand misses in his sight = check |
| `suspicionPerToolMiss` | 0.7 | 2 missed saws, or 1 saw + 1 squirm |
| `suspicionTellAt` | 0.5 | "...Hm." once per visit: the warning |
| `checkEvidence` | 0.5 | any bond ≥ 50% = caught (light) |
| `checkDuration` | 1.2 s | he examines, at her side |
| `cinchSetback` | 0.15 | "Still tight." tugs the most loosened bond |
| `tauntCooldown` / `tauntLinger` / `maxLingerPerVisit` | 4 s / 2 s / 6 s | mashing keeps him watching |

- Suspicion resets every visit and after a clean check. Only pulls made while he's
  looking count (attention > 0). A freed bond counts as evidence (100%).
- Filled on the approach: he finishes walking up, then checks. From the door he takes the
  lean-in route; from inside the room he goes straight to her.
- New state `Checking` (attention 1, no strike window). `BondStruggle.OnPullResolved`
  (Start / Hit / Miss / Spam, tool engaged) is what he reads.

**Open:**
- [ ] Play: does "...Hm." read as the warning? Is 3 misses too few for a sloppy player?
- [ ] Mashing never catches. Mashing is also loud, so offstage it still calls him in. Check
      that it doesn't feel like a free action in front of him.
- [ ] Hands hidden + wrists already free = evidence 100% on a check. Intended: feign only
      holds until he looks closely.

---

## Character

**Cassie** — the detective. Low-poly, cel-shaded, defined face. Animate for personality: frustrated squirming, exasperated head shakes, smug grin when she figures something out.

**Canonical design is locked and lives in `cassie-modeling-brief.md`.** Summary only, and if this summary and the brief ever disagree, the brief wins:

- **Hair:** auburn-red, high ponytail, slightly loose and tousled, face-framing strands at the temples, small gold hair tie.
- **Face:** defined, light freckles, full lips, arched brows, hazel-green eyes. Resting expression reads dry and unimpressed — never afraid.
- **Top:** cream/ivory ribbed cropped long-sleeve, high mock/funnel neck.
- **Trousers:** ultra high-waisted brown/camel, pleated, waistband above the navel. Beltless contoured waistband, no belt loops.
- **Midriff:** narrow strip only. **Jewellery:** small gold hoops.
- Height 1.68 m, 7.5 heads — half a head shorter than the guard.

The trench-coat/rumpled-suit description in earlier drafts is dead. Superseded Rara Day 118.

**The governing test, everywhere:** *capable, not helpless.* Peril is the cover promise; competence is the payoff. Both, simultaneously, never one at the expense of the other. She escapes on her own competence, every time, with no rescuer.

**Voice / Muttering:** Cassie thinks aloud. This serves three purposes:
1. **Personality** — "Tied to a chair. Again. Wonderful."
2. **Hints** — "That drawer's half open... if I could reach it." (Contextual, triggers after idle time or failed attempts)
3. **Tonal escalation** — Early levels: calm, annoyed. Mid levels: nervous, talking faster. Late levels: panicked one-liners. "A bomb. Of course there's a bomb."

Keep lines short. 5–10 words max. No voice acting needed for v0 — text popups near the character's head work fine. Voice can be added later.

---

## Level Structure (12 Levels)

### Act 1 — Noir (Levels 1–5)
**Setting:** Grounded, gritty. Back rooms, storage units, a parked van, a basement office.
**Tension:** None. Pure puzzle solving. Take your time.
**Restraints:** Chair (L1–3), floor (L4), cuffed to pipe (L5).
**Detective mood:** Annoyed, confident. "Not my first time."

- **L1 — The Back Room:** Tied to a wooden chair. Room has a nail sticking out of the wall. Move to the wall, Struggle against the nail to cut rope. (Tutorial: Struggle + Move. The nail is an environmental tool — Struggle gates on being adjacent to it.)
- **L2 — The Storage Unit:** Chair again. A shelf nearby has a box cutter on the edge. Move to the shelf, bump it to knock the cutter down, Pick Up, Struggle to cut free. (Tutorial: Pick Up — tools must be acquired before Struggle works.)
- **L3 — The Office:** Chair, hands behind back. Desk nearby has a drawer slightly ajar with scissors inside. Move to desk, bump it to jostle the drawer open, Pick Up scissors, Struggle with scissors to cut through rope. (Tutorial: tools have different speeds — scissors are faster than the L1 nail or L2 cutter.)
- **L4 — The Van:** Duct-taped on the floor of a van, starting prone (face-down). Bonds are unbreakable bare-handed (no tools in scene). The only escape is to kick the back doors open. The detective must **flip to supine** (face-up) before kicks will land — prone kicks are suppressed (thud, no progress) since the legs can't deliver force face-down. Movement to the door is flexible: inch while prone, or flip first and scoot while supine. Once at the door in supine posture, Kick repeatedly. Floor-bound kicks are reduced force, so this takes ~6 reps. The sixth bursts the door open. (Tutorial: Kick verb. Tutorial: *posture* gates verb effectiveness — same "you tried" suppression pattern as barehand Struggle, but here the gate is body state rather than tool possession.)
- **L5 — The Basement:** Cuffed to a radiator pipe. Reach radius is limited. Must use objects within range creatively. First real multi-step puzzle combining Struggle, Move, and Pick Up.

### Act 2 — Thriller (Levels 6–10)
**Setting:** Escalating. A hotel room, a shipping container, a penthouse, a warehouse with catwalks.
**Tension:** Soft timers. Guards checking in on a schedule. You hear footsteps. A door opening down the hall. A voice saying "I'll be back in five minutes."
**Restraints:** Mix of all types, more complex setups (chair + handcuffs, floor + locked room).
**Detective mood:** Nervous, improvising. "Okay. Okay okay okay. Think."

- Puzzles require 4–6 steps
- Introduce guards as environmental obstacles (time your escape around their patrols, avoid detection)
- One level where you're restrained in a new way mid-level (freed from chair but room locks down)
- **Introduce Feign** in one Act 2 level: player frees themselves, hears a guard approaching, must Feign to avoid detection. Guard passes, scene continues. Plants the seed for the finale — Feign and Kick are introduced separately in Act 1 and Act 2 so the finale's combination of them lands.
- **At least one Kick puzzle that isn't a door:** e.g. floor-restrained, the only tool is on a high shelf. Kick the shelf to knock it down, then Pick Up. Demonstrates that Kick is a verb with general utility, not a one-trick L4 mechanic.
- At least one "aha moment" where a verb does something unexpected

**Pacing principle:** The antagonist guard is *unseen* in L6 — a voice and footsteps offstage — and only becomes *physically present* in L7/L8. Letting the guard be a threat-as-sound for a full level before becoming a body in the room is the thriller pacing that makes the Feign debut land harder when it arrives.

- **L6 — The Hotel Room** *(threshold into Act 2)*: A cheap hotel room with noir trappings. Cassie is tied to a wooden chair, hands behind back. The detective must escape before the guard returns — but he's never seen in this level. Offstage audio + mutter triggers convey his proximity (footsteps in hall, voice down the corridor, key in lock). Solve path: bump nightstand to knock a tool to the floor → **tip the chair** (debut mechanic) to bring hands within reach → Pick Up tool → Move while tipped → Struggle with tool to cut chair bonds → exit (likely bathroom, away from approaching guard). **Failure loop:** if the timer expires before escape, cut to black + offstage guard mutter (per-character speaker styling debuts here) → fade in with Cassie re-bound, **elbow bond added** (visible second bond), tool returned to start. Second attempt has degraded mechanics — chair-tipping range shorter, Move slower, Struggle harder — but the solve path remains the same, just worse. After second failure: true game over, level restart. **First level with a real fail state.** Tutorials: soft timer, chair-tipping mechanic, failure has consequence, the enemy adapts to your progress. Infrastructure built: runtime bond-state change on `RestraintBase`, per-character mutter styling, game-over flow. **L6-exclusive in v0:** rocking input (Shift+A/D) and chair-tip → floor handoff are gated behind ChairRestraint.rockingEnabled (default false); only L6's chair instance flips it true and wires floorRestraintOnBreak. L1–L3 chairs cannot tip. The stand-up verb (the floor → chair recovery counterpart) is L7's spotlight mechanic and is not in v0.

  **L6 mutter chain (Day 37 design):** L6 has more to teach than any prior level — soft timer, rocking input, tipping consequences, bump-to-jostle (familiar from Act 1), failure-loop semantics, and the per-character mutter styling debut. The chain mirrors L1's three-beat shape (entry → failure-of-obvious → proximity-to-affordance) and adds three L6-specific beats. All Cassie mutters use her established styling; the guard's mutters use the new per-character styling.

  | # | Trigger | Speaker | Content |
  |---|---------|---------|---------|
  | 1 | Level start | Cassie | "Hotel room. Cheap one. ...he'll be back. Find something sharp, get out." |
  | 2 | After 5 barehand struggles, fire-once | Cassie | "Tight. Tighter than usual. ...not getting out of this without help." |
  | 3 | MutterTrigger near nightstand, fire-once | Cassie | "That nightstand. Something in the drawer?" |
  | 4 | After tool falls from drawer, fire-once | Cassie | "There. ...can't reach it like this." |
  | 5 | Timer at ~50% AND player has not tipped yet | Guard (offstage) | "I'll check on her in a minute. She's a little tied up right now." |
  | 6a | Failure-loop entry, during cut-to-black | Guard (offstage) | "That ought to hold you." |
  | 6b | Failure-loop entry, on fade-in | Cassie | "My ELBOWS? Really? Good thing I stretched." |

  **Design notes:**
  - Beat 1 establishes location (per-character styling needs the player to feel where they are), timer pressure ("he'll be back"), and the goal in Cassie-words ("something sharp" — deliberate echo of the L1 entry, same detective).
  - Beat 2 punishes Space-mashing diegetically, same shape as L1's "I'm tied too tight. Think, Cass..." beat. "Tighter than usual" seeds the failure-loop concept and is a deliberate continuity hook — Acts 2-3 can echo or contradict.
  - Beat 3 names the affordance (nightstand) without prescribing action. Question form implies exploration not direction.
  - Beat 4 is the rocking-verb teaching beat. Per the Day 37 diegetic-teaching decision, the mutter does NOT explain rhythm or name the input — it states the problem ("can't reach it like this") and trusts the ControlHintsPanel + physics to teach the verb. Rocking input is gradient (rhythm-builds-amplitude), so the player needs the panel-and-experimentation loop, not a verbalized tutorial.
  - Beat 5 is the offstage-guard pressure beat. Per-character styling debut. The pun ("a little tied up") characterizes the guard as pleased-with-himself henchman, not as a serious antagonist — sets up his comeuppance as satisfying. The not-yet-tipped gate avoids spamming pressure on a player who's already mid-solve.
  - Beat 6 is the failure-loop entry, two-beat. Per design, the guard's "That ought to hold you" is the audio you hear *during* the cut-to-black — the line IS the re-binding from Cassie's POV. The fade-in reveals what he did to her. Cassie's reaction is the player's discovery too. Her indignant tone ("My ELBOWS? Really?") keeps swagger on second attempt — failure offends her, doesn't dim her. Critical for player motivation: a despondent Cassie on the second attempt risks "this is unwinnable" reads.

  **Infrastructure prerequisites (must build before L6 mutters land):**
  - **Per-character speaker styling** in MutterSystem. Guard needs distinct grunt pool and text styling from Cassie. Already on the L6 infrastructure list above.
  - **Mutter queue.** Beat 6 fires two mutters back-to-back (guard, then Cassie); the current MutterSystem drops the second on active-mutter Play(). Already flagged in ideas.md as "reconsider once a level wants two mutters back-to-back" — L6 is that level. Pair the queue build with per-character styling.
  - **Mutter emphasis support.** Beat 6b uses "ELBOWS" in caps for emphasis. Either mutter rendering supports this directly, OR the per-word grunt cadence handles it via word-spacing, OR we strip the caps for v0 and accept the line plays flat. Lowest priority of the three; revisit during L6 implementation.

  **Scene-design constraint from Beat 4:** the tool's landing position after the nightstand bump must be visible from chair-height — Cassie shouldn't react to something she can't see. Verify when L6 is blocked out. If the tool lands out of line-of-sight (under nightstand, behind chair), Beat 4 needs to fire on tool *audible* (drawer rattle + thud) rather than visible.

  **⚠ L6 design vs. the vertical slice beat — unresolved, Rara Day 122.** The VS
  beat built in `VS_Turnaround.unity` runs *struggle → free → pick up → conceal →
  check-in → strike → KO*, with a bottle and a guard who is physically present and
  gets hit. The L6 design above has no bottle, no strike, no KO, and rests on a
  **pacing principle that the L6 guard is never seen** — he becomes a body in the
  room in L7, and Feign debuts there so the finale lands.

  Both cannot be L6. Three ways out, in rough order of cheapness:

  1. **The clip is a showcase, not L6 gameplay.** The launch plan already says the
     September scope is a 48-second clip and explicitly not a playable build. The
     beat can be staged in the L6 *set* without being the L6 *level*. Costs nothing
     today; costs honesty on the Patreon page, which should not claim the clip is
     gameplay if it isn't.
  2. **Move the beat to L7** and dress L7's set instead. Preserves the pacing
     principle intact. Expensive — L7 is TBD and undesigned, and the whole
     September schedule is built on L6.
  3. **Rewrite L6** to include the visible guard and the strike. Cheapest for the
     clip, but it spends the unseen-guard pacing beat and pulls Feign's setup
     forward, which weakens the L12 payoff the whole three-act structure is aimed at.

  **⚠ It drifted (recorded Rara Day 205).** VS_Demo now has a visible guard, a bottle
  strike and the BondStruggle loop, and it ships as the free demo on Nov 21. That is
  option 3 in practice for the demo. The L6 *level* spec above (unseen guard,
  tool-gated, soft timer) is not retired yet. The real call is tied to the Oct 2
  "knit the bound scenes into one level" pivot and the escalation ladder in
  `ideas.md`. **Decide after the Nov 21 demo, not before.** Until then the demo
  is labelled a slice, not "Level 6".

  **Do not resolve this by drifting.** Pick one and write it down here. This does
  not block Cassie, the September 1 gate, or anything on the critical path — it
  blocks page copy in week 7, which is exactly the kind of thing that becomes
  expensive when discovered late.

- **L7 — TBD** *(Feign debut, visible guard)*: Continuation of L6's hotel/thriller setting. The offstage guard from L6 becomes physical — visible model, line-of-sight or proximity awareness, investigation behavior. Cassie escapes initial restraint, hears the guard approaching, must **Feign** (debut) to avoid detection: re-pose into bound posture, drift threshold determines whether guard is fooled or escalates to re-restraint. Pays off the offstage threat seeded in L6. Builds: guard AI, Feign verb + drift/detection mechanic, visible guard model + animations.

### Act 3 — Showpiece + Finale (Levels 11–12)
**Setting:** Confined and personal. The villain's study (L11), then a wider room in the same building (L12). Bookshelves, a desk, framed degrees on the wall, a humidor — the *person* responsible is finally implied by the space, not just by goons offstage. No environmental hazard systems (no rising water, no electrified floors, no tilting rooms, no conveyor belts, no laser grids — all cut from scope).
**Tension:** Showpiece-puzzle difficulty in L11; hard timer in L12 (the bomb).
**Restraints:** Layered/showpiece restraint in L11, designed to exercise the full verb vocabulary in sequence. L12 returns to a familiar restraint type to keep the finale's cognitive load on the *Feign* payoff, not on a new puzzle to learn.
**Detective mood:** Out of depth but quipping. The escalation from Act 2 is *stakes*, not *setting* — the world is the same Acts 1–2 world, but the consequences have gone external.

- Puzzles in L11 require 6–10 steps and exercise every verb in sequence — the hardest puzzle in the game, but a *puzzle*, not a hazard-management scenario
- L12 trades puzzle depth for tension: the restraint is solvable in 3–5 steps, but a hard timer + the Feign phase shift create the climactic pressure

- **L11 — The Villain's Study (Showpiece Restraint).** Cassie is finally face-to-face with the antagonist — offscreen-but-present: in another room, on a TV monitor, walking past the door while making tea. The threat is no longer "the goons will come back" but "the person who has been doing this all along is twenty feet away." She's in a layered restraint: multiple bonds, multiple steps to undo, each step a callback to a verb or tool from Acts 1–2. No hard timer, no environmental hazard — *the puzzle is the threat*. The hardest design challenge in the game; the most satisfying solve when it lands.

- **L12 — The Finale: Turn the Tables.** A bomb in the room. Hard timer, visible countdown. The most complex room. Multiple phases. Uses every restraint type and verb in sequence. The detective escapes a final, layered restraint — and just as the last bond falls, footsteps approach. Phase shift: the player must **Feign** before the guard enters, holding still while he saunters in to gloat over his apparently helpless captive. With Feign active, only Kick is available — and only when the guard is in range. Time the kick: too early, he's out of range and it thuds harmlessly; correctly timed, the detective kicks him, takes his keys (Pick Up), and walks out the door. Satisfying payoff that recontextualizes every verb the player has learned — Move becomes stillness, Struggle becomes patience, Pick Up becomes the keys, Kick becomes the strike. The bomb provides hard-timer tension without volcano-lair scaffolding.

**Why Act 3 was cut from 5 levels to 2 (scope decision, Day 40):** The original Act 3 ("Absurd") was a tonal pivot to James Bond — volcano lair, underwater base, rising water, electrified floors, conveyor belts, laser grids. Three problems: (1) the grounded "Cassie in real rooms" premise is the *interesting* thing about Rara, and a spy-pastiche pivot risked reading as the dev getting bored of his own premise; (2) every Act 3 level as originally written was 2–3× the build cost of an Act 1 level due to new environmental hazard systems; (3) the *threat escalation* (from "you'll get caught again" to "people will die") is a real escalation that doesn't require a vibe shift to land — externalizing the stakes via the bomb does the work that "the room is filling with water" was reaching for. Cutting Act 3 to two levels preserves the Feign payoff and the "uses every verb" finale, saves three levels of design/build/polish, and lets Act 2's visual identity extend into Act 3.

---

## Art Direction

### Environments
- Clean low-poly geometry, flat-shaded materials, no complex textures
- Each room has a distinct color accent (Act 1: muted greens/browns; Act 2: blues/grays; Act 3: Act 2's palette extended darker — the same world, just the lights are lower and the walls are closing in)
- Lighting tells the story: dim and moody early, harsh fluorescent mid, dramatic colored lighting late
- Interactive objects are visually distinct — slight glow, brighter color, or subtle animation (a drawer slightly ajar, a blade catching the light, a rope fraying)

### Character
- Low-poly, cel-shaded. Head is large relative to the body, so the face gets **fewer, larger, simpler** features than a realistic reference would suggest
- Silhouette read order: V-to-waist taper → visible neck → hip flare → ponytail mass → slim tapered limbs
- Second test, always paired with *capable, not helpless*: **the anti-guard.** Where he is a bald slab — broad, heavy, planted — she is slim, tapered, mobile, poised. Drop the two silhouettes side by side; if the reads are close, the pass failed
- Needs idle animations per restraint type (squirming in chair, struggling on floor, swinging while hanging)
- Needs a kick animation per kick-capable restraint (free-leg side kick, floor-bound mule kick, hanging swing-kick)
- Satisfying "freed" animation when you escape

### Props — locked dimensions

Dimensions that other geometry depends on. Locked means a change here breaks something downstream, so change it deliberately.

**Bottle — neck outer diameter ≤ 21 mm.** *(Locked Rara Day 122.)*

Derived from the hand, not chosen for the bottle. Cassie's grip tunnel — the clear gap between palm surface and nearest fingertip — measures 21.2 mm, and the hard ceiling for her finger section is about 23.5 mm, reachable only at a dead-straight 90° plate with no visible curl. She has no finger bones; the grip is modelled geometry in a fixed pose and cannot open wider at runtime.

Build to 21 and keep the margin. **Only the neck is constrained** — she grips the neck and swings the body.

Suggested full prop: **21 mm neck, 55–60 mm body, 260–280 mm tall.** That is wine/spirits proportion rather than beer — a slimmer neck on a full body. Reads more noir, gives the shard burst more mass to throw, and lands at roughly 1.7× her hand length, which is what a real bottle-to-hand ratio looks like.

If the bottle is ever specced wider: lengthen the finger section (changes hand length, currently on spec), shrink the bottle, or accept the fingers sinking into it and rely on camera distance. The third is probably survivable at L6 distance, but it should be a decision rather than a discovery while framing shot 5.

### UI
- Minimal HUD. Verb selection along the bottom (icons, highlights active verb)
- Room number / act shown briefly on level start
- Timer (when present) integrated into the world if possible (a clock on the wall, a bomb display, water level rising) — not a floating UI bar
- Mutter text appears near the character's head, fades after 2–3 seconds
- Pause menu, level select, settings — clean and simple

---

## Audio Direction

### SFX (Priority — get these in early)
- Struggle: rope creaking, chain rattling, tape stretching, wood straining
- Move: chair legs scraping, body dragging, hopping thuds
- Pick Up: object grab, metallic clink, sliding
- Kick: per-target thuds (door=hollow boom, wall=dull thud, shelf=rattling clatter, guard=meaty hit)
- Success: rope snap, cuff click open, satisfying "free" sound
- Failure: guard alert sound, buzzer, ominous door opening
- Timer: ticking, beeping (escalates as time runs out)

### Music
- Act 1: Low-key jazz or noir ambient. Calm, moody.
- Act 2: Tension building. Pulsing, minimal. Heartbeat-like.
- Act 3: Act 2's track intensified — the same instrumentation, faster, with the bomb-timer's tick layered in. Reinforces "same world, higher stakes."
- Can be 2 tracks total (Act 1 + Act 2/3 shared with an intensification pass). Source from free libraries or commission later.

---

## Technical Architecture (Unity)

### Scene Structure
- `MainMenu` — Title screen, level select, credits button
- `Level_XX` — One scene per level (15 scenes) or dynamic loading from level data
- `Credits` — Simple scroll

### Key Scripts
- `PlayerController` — Input routing, verb dispatch (Struggle, Pick Up, Kick), interaction raycasting. Movement is delegated to the active restraint.
- `RestraintBase` — Abstract base for restraints. Defines movement input handling, struggle/kick modifiers, struggle gating. Concrete: `ChairRestraint`, `FloorRestraint`, `CuffedRestraint`, `HangingRestraint`, future `HogtiedRestraint`.
- `Bond` — Per-restraint binding state. Handles struggle progress, breaking, tool effectiveness.
- `InteractableBase` — Base for all interactive objects.
- `Pickupable` — Items the Pick Up verb consumes. Carry tool type.
- `Kickable` — Abstract base for things the Kick verb targets. Concrete: `KickableDoor` (L4), `KickableShelf` (Act 2 puzzle), `KickableGuard` (finale).
- `EnvironmentalTool` — Stationary tools that modify Struggle (the wall nail, the radiator edge).
- `GuardAI` — Patrol/check-in behavior for Act 2–3.
- `TimerSystem` — Soft and hard timers, failure state.
- `MutterSystem` — Triggers character lines based on context (idle, hint, success, failure).
- `LevelManager` — Scene loading, progression, completion.
- `AudioManager` — SFX and music singleton.
- `UIManager` — Verb HUD, mutter text, menus, timer display.

### Data
- Level completion and best times stored in PlayerPrefs
- Restraint type configs as ScriptableObjects
- Mutter lines as ScriptableObjects (tagged by context: idle, hint, success, act)
- Puzzle step sequences defined per-level (ScriptableObject or serialized in-scene)

---

## Repo Structure
```
rara-v0/
├── Assets/
│   ├── Scripts/
│   │   ├── Player/
│   │   │   ├── PlayerController.cs
│   │   │   ├── RestraintBase.cs
│   │   │   ├── ChairRestraint.cs
│   │   │   ├── FloorRestraint.cs
│   │   │   └── ...
│   │   ├── Verbs/
│   │   │   ├── Bond.cs
│   │   │   ├── Pickupable.cs
│   │   │   └── Kickable.cs
│   │   ├── Puzzle/
│   │   │   ├── InteractableBase.cs
│   │   │   ├── KickableDoor.cs
│   │   │   ├── KickableShelf.cs
│   │   │   └── EnvironmentalTool.cs
│   │   ├── AI/
│   │   │   └── GuardAI.cs
│   │   ├── Systems/
│   │   │   ├── TimerSystem.cs
│   │   │   └── MutterSystem.cs
│   │   ├── Managers/
│   │   │   ├── LevelManager.cs
│   │   │   ├── AudioManager.cs
│   │   │   └── UIManager.cs
│   │   └── UI/
│   ├── Prefabs/
│   ├── Scenes/
│   ├── Art/
│   ├── Audio/
│   ├── ScriptableObjects/
│   └── Resources/
├── Packages/
├── ProjectSettings/
├── .gitignore
├── .gitattributes
├── rara-v0-gdd.md
└── README.md
```

---

## Milestone Schedule (~60 days) — **SUPERSEDED, kept as a build record**

> This schedule assumed a small complete game shipped to itch.io in 13 weeks. Rara
> is a 12-level three-act game and will take years, not weeks; every milestone below
> was measuring the wrong thing. **`game-dev-plan.md` is the live schedule.**
>
> Kept because the ticked boxes are an accurate record of what actually exists in
> the project. Do not plan against the unticked ones — plan against the vertical
> slice.

### Weeks 1–2 (Days 1–14): Foundation
- [x] Repo setup, Unity project, folder structure
- [x] PlayerController: chair-based movement (hop, scoot, tip)
- [x] Verb system: Struggle, Pick Up, with context-sensitive interact
- [x] One interactable object (rope on nail — cut free with Struggle)
- [x] Level 1 fully playable with placeholder art (Unity primitives)
- [x] Basic mutter system (text popup near character) — partial (worldspace bond meter scaffolding)

### Weeks 3–4 (Days 15–28): Core Systems
- [x] Second restraint type (floor) with different movement
- [x] Pick Up verb functional (tool-modified Struggle)
- [x] Kick verb introduced (L4 implementation)
- [ ] Levels 1–5 playable (Act 1 complete)
- [ ] Level progression (complete room → load next)
- [ ] Core SFX (struggle, move, pick up, kick, success, failure)
- [ ] Placeholder character model (can be Unity primitive humanoid or free asset)

### Weeks 5–6 (Days 29–42): Content + Polish
- [ ] Third restraint type (cuffed to fixture)
- [ ] Guard AI for Act 2 (simple patrol, alert state)
- [ ] Soft timer system
- [ ] Levels 6–10 designed and playable (Act 2 complete) — includes a non-door Kickable puzzle
- [ ] Visual polish: materials, lighting, color per room
- [ ] Level select screen
- [ ] Playtest #1

### Weeks 7–8 (Days 43–56): Final Act + Ship Prep
- [ ] Fourth restraint type (hanging)
- [ ] Hard timer system
- [ ] Levels 11–12 designed and playable (Act 3 complete)
- [ ] Title screen, credits
- [ ] Music integrated (1 track per act)
- [ ] Mutter lines written and implemented for all levels
- [ ] Playtest #2 and #3
- [ ] Bug fixing

### Week 9 (Days 57–63): Ship It
- [ ] Final bug pass and polish
- [ ] PC build
- [ ] itch.io page: screenshots, description, tags
- [ ] Upload and publish
- [ ] Share in 2–3 communities (Reddit, Discord, itch.io)

---

## Future Ideas (DO NOT BUILD THESE NOW)
- Additional level packs / "cases"
- Voice acting for mutter lines
- Star rating per level (time-based, move-count-based)
- Alternate solutions leaderboard
- Mobile port (touch controls for verb selection)
- Cosmetic unlocks (different detective outfits)
- Level editor / community levels
- Overarching story connecting all rooms (who keeps capturing this detective?)
- Co-op mode (two detectives in connected rooms, must communicate)
- Chair tipping as a restraint state transition — struggling too hard or physics collisions tip the chair, moving player from "chair" to "floor" restraint mid-level
- Body-Part Bonds — bonds scoped per limb (wrists, ankles, elbows) instead of package-deal restraints. Would let "legs free" emerge naturally from bond state instead of being a per-restraint configuration. **→ Promoted Day 202: `BondStruggle` bonds are wrists, L/R ankle, elbows.**
- Hands-behind as Pick Up range modifier — pickup cone limited to behind the player; combos with chair-tipping for emergent puzzle solutions.
- Stealth-between-escapes — guard AI segments where capture transitions to a new escape state instead of game over. Probably v1.0 or sequel. **→ Promoted: Loop-Refine pillars 1–2 (captor AI, free state). In progress; post-demo.**
- Double-cuffed escape — wrists AND elbows cuffed; freeing wrists from a pole still leaves elbows bound.
- **Struggle as a real mechanical verb (v1).** v0's choice to make barehand Struggle do nothing is correct given the current Space-spam implementation — but it tacitly admits Struggle has no mechanical body to it. A previous prototype involved feeling around for "sweet spots" on dual thumbsticks and rubbing across both simultaneously to work bonds loose. A fleshed-out Struggle of that kind could re-justify universal barehand struggle as a meaningful verb in its own right — and would change the puzzle grammar significantly (bonds become a real time/effort cost rather than a tool-presence binary). Park for v1 / sequel. **→ No longer parked: prototyped Day 197–201 as rhythm Struggle. See Loop-Refine Pillars.**
