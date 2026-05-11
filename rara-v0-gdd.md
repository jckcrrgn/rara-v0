# Rara v0 — Game Design Document

## One-Line Pitch
A quirky third-person low-poly escape game where a captured detective uses Struggle, Move, Pick Up, and Kick to break free from increasingly absurd restraint scenarios.

## Core Info
| | |
|---|---|
| **Engine** | Unity (latest LTS, URP) |
| **Platform** | PC (itch.io release) |
| **Perspective** | Third-person, fixed or semi-fixed camera per room |
| **Art Style** | Low-poly 3D, flat-shaded, bright chunky palette |
| **Tone** | Dry humor, noir-to-spy-thriller escalation |
| **Target Playtime** | 30–60 minutes |
| **Dev Timeline** | ~60 days at 1 hr/day |
| **Target Release** | itch.io, free or pay-what-you-want |

---

## Concept

You're a detective. You got too close. Now you wake up restrained in a room — tied to a chair, cuffed to a pipe, duct-taped on the floor. Each level is a new room, a new predicament, a new escape.

The game starts grounded and noir. By the end, you're defusing a bomb in a volcano lair. The detective's dry commentary holds it all together — they're annoyed at first, then nervous, then completely out of their depth but still cracking jokes.

---

## Core Mechanics

### The Four Verbs
Every puzzle is solved using four actions in creative combinations. Each verb has a **strength axis** — a property that modulates its effectiveness based on what the player has access to:

| Verb | Strength axis | Source |
|------|---------------|--------|
| **Struggle** | Tool held | Pickupable items (BoxCutter, Nails…) and environmental tools. Barehanded Struggle produces no progress in v0. |
| **Move** | Movement mode | Restraint type (chair=hop, floor=inch/roll, hanging=swing) |
| **Pick Up** | Reach / range | Restraint type (hands-front=in front of you, hands-behind=behind you, future variants) |
| **Kick** | Leg state | Restraint type (free=full force, floor-bound=reduced, hogtied=disabled) |

This is a deliberate design pattern: **restraints modulate verbs**. New restraint types are interesting because each one changes how the four verbs behave. No new verbs needed for new puzzle situations — just new restraint configurations.

**Verb roles in the puzzle loop:** Pick Up is the *gating* verb — the player must acquire a tool before bonds can be broken. Struggle is the *closing* verb — once a tool is held, Struggle applies it. Move connects the two by getting the detective to where the tool is. Kick is a separate solve path used when no tool exists in scene (L4) or when something needs to be struck (Act 2+ Kickables).

#### Struggle — Strain against your restraints
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
The detective can voluntarily re-enter a "looks bound" state after freeing themselves. This is not a fifth verb — it's a *state* the player toggles, modifying how the existing verbs behave.

- **Toggle**: F (or context prompt when a guard is approaching). Note: F is also Kick when not feigning. Context-sensitive — feign is only available post-escape, kick is the verb otherwise.
- **In Feign state**: Move is disabled (you're holding still). Struggle and Pick Up are disabled. **Kick is the only available verb** — the detective is coiled, waiting for the right moment to strike.
- **Visual tell**: Detective slumps into bound posture, restraints visually re-applied (loose, but reads as bound from a distance).
- **Purpose**: Lets the player weaponize stillness. Plants the seed in Act 2 (avoid detection by feigning), pays off in the finale (turn the tables on the gloating guard with a single decisive kick).

### Controls
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

## Character

**Cassie** — the detective. Low-poly, expressive face, trench coat or rumpled suit. Animate for personality: frustrated squirming, exasperated head shakes, smug grin when she figures something out.

**Voice / Muttering:** Cassie thinks aloud. This serves three purposes:
1. **Personality** — "Tied to a chair. Again. Wonderful."
2. **Hints** — "That drawer's half open... if I could reach it." (Contextual, triggers after idle time or failed attempts)
3. **Tonal escalation** — Early levels: calm, annoyed. Mid levels: nervous, talking faster. Late levels: panicked one-liners. "A laser grid. Because of course there's a laser grid."

Keep lines short. 5–10 words max. No voice acting needed for v0 — text popups near the character's head work fine. Voice can be added later.

---

## Level Structure (15 Levels)

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

- **L6 — The Hotel Room** *(threshold into Act 2)*: A cheap hotel room with noir trappings. Cassie is tied to a wooden chair, hands behind back. The detective must escape before the guard returns — but he's never seen in this level. Offstage audio + mutter triggers convey his proximity (footsteps in hall, voice down the corridor, key in lock). Solve path: bump nightstand to knock a tool to the floor → **tip the chair** (debut mechanic) to bring hands within reach → Pick Up tool → Move while tipped → Struggle with tool to cut chair bonds → exit (likely bathroom, away from approaching guard). **Failure loop:** if the timer expires before escape, cut to black + offstage guard mutter (per-character speaker styling debuts here) → fade in with Cassie re-bound, **elbow bond added** (visible second bond), tool returned to start. Second attempt has degraded mechanics — chair-tipping range shorter, Move slower, Struggle harder — but the solve path remains the same, just worse. After second failure: true game over, level restart. **First level with a real fail state.** Tutorials: soft timer, chair-tipping mechanic, failure has consequence, the enemy adapts to your progress. Infrastructure built: runtime bond-state change on `RestraintBase`, per-character mutter styling, game-over flow.
- **L7 — TBD** *(Feign debut, visible guard)*: Continuation of L6's hotel/thriller setting. The offstage guard from L6 becomes physical — visible model, line-of-sight or proximity awareness, investigation behavior. Cassie escapes initial restraint, hears the guard approaching, must **Feign** (debut) to avoid detection: re-pose into bound posture, drift threshold determines whether guard is fooled or escalates to re-restraint. Pays off the offstage threat seeded in L6. Builds: guard AI, Feign verb + drift/detection mechanic, visible guard model + animations.

### Act 3 — Absurd (Levels 11–15)
**Setting:** Full spy-thriller. A villain's study, an underwater base, a room filling with water, a bomb scenario, a volcano lair.
**Tension:** Hard timers. Visible countdown. The room is changing around you (water rising, walls closing, laser grid activating).
**Restraints:** Creative combinations. Hanging + the floor is electrified. Chair on a conveyor belt. Cuffed inside a slowly tilting room.
**Detective mood:** Panicked but quipping. "A conveyor belt. Into a pit. Sure. Sure!"

- Puzzles require 6–10 steps
- Multiple valid solutions for some rooms (rewards creative thinking)
- Environmental hazards force prioritization (escape the restraint AND deal with the room)
- **Level 15 — The Finale: Turn the Tables.** The most complex room. Multiple phases. Uses every restraint type and verb in sequence. The detective escapes a final, layered restraint — and just as the last bond falls, footsteps approach. Phase shift: the player must **Feign** before the guard enters, holding still while he saunters in to gloat over his apparently helpless captive. With Feign active, only Kick is available — and only when the guard is in range. Time the kick: too early, he's out of range and it thuds harmlessly; correctly timed, the detective kicks him, takes his keys (Pick Up), and walks out the door. Satisfying payoff that recontextualizes every verb the player has learned — Move becomes stillness, Struggle becomes patience, Pick Up becomes the keys, Kick becomes the strike.

---

## Art Direction

### Environments
- Clean low-poly geometry, flat-shaded materials, no complex textures
- Each room has a distinct color accent (Act 1: muted greens/browns, Act 2: blues/grays, Act 3: reds/oranges/neon)
- Lighting tells the story: dim and moody early, harsh fluorescent mid, dramatic colored lighting late
- Interactive objects are visually distinct — slight glow, brighter color, or subtle animation (a drawer slightly ajar, a blade catching the light, a rope fraying)

### Character
- Low-poly, ~500–1000 tris. Big head, simple face with eyebrows that emote
- Trench coat / rumpled suit reads as "detective" instantly
- Needs idle animations per restraint type (squirming in chair, struggling on floor, swinging while hanging)
- Needs a kick animation per kick-capable restraint (free-leg side kick, floor-bound mule kick, hanging swing-kick)
- Satisfying "freed" animation when you escape

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
- Act 3: Full thriller score. Driving percussion, urgency.
- Can be 3 tracks total (one per act). Source from free libraries or commission later.

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

## Milestone Schedule (~60 days)

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
- [ ] Levels 11–15 designed and playable (Act 3 complete)
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
- Body-Part Bonds — bonds scoped per limb (wrists, ankles, elbows) instead of package-deal restraints. Would let "legs free" emerge naturally from bond state instead of being a per-restraint configuration.
- Hands-behind as Pick Up range modifier — pickup cone limited to behind the player; combos with chair-tipping for emergent puzzle solutions.
- Stealth-between-escapes — guard AI segments where capture transitions to a new escape state instead of game over. Probably v1.0 or sequel.
- Double-cuffed escape — wrists AND elbows cuffed; freeing wrists from a pole still leaves elbows bound.
- **Struggle as a real mechanical verb (v1).** v0's choice to make barehand Struggle do nothing is correct given the current Space-spam implementation — but it tacitly admits Struggle has no mechanical body to it. A previous prototype involved feeling around for "sweet spots" on dual thumbsticks and rubbing across both simultaneously to work bonds loose. A fleshed-out Struggle of that kind could re-justify universal barehand struggle as a meaningful verb in its own right — and would change the puzzle grammar significantly (bonds become a real time/effort cost rather than a tool-presence binary). Park for v1 / sequel.
