# Rara v0 — Game Design Document

## One-Line Pitch
A quirky third-person low-poly escape game where a captured detective uses Struggle, Move, and Pick Up to break free from increasingly absurd restraint scenarios.

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

### The Three Verbs
Every puzzle is solved using three actions in creative combinations:

**Struggle** — Strain against your restraints.
- Universal verb: always works against bonds, just at different rates
- Loosens bindings over time (may take multiple uses)
- Can knock over nearby objects
- Makes noise (which can be good or bad)
- May break something you're attached to
- Effectiveness modified by held tools (bare hands = slow, sharp object = fast)

**Move** — Scoot, hop, roll, or drag yourself while bound.
- Movement is restricted based on how you're restrained (chair = hop/scoot, floor = inch *or* roll, hanging = swing)
- Some restraints offer multiple movement modes with different tradeoffs. Floor restraint: **inch** (tap W) is forward, slow, quiet, precise; **roll** (Shift+A or Shift+D) is lateral-only, fast, noisy, needs open space. Player chooses based on the situation — open hallway vs. tight gap between furniture, guard nearby vs. alone.
- Positioning matters — you need to be near things to interact with them
- Moving into objects can knock them over or push them
- Movement noise is a stealth lever in Act 2+: rolling alerts guards, inching doesn't.

**Pick Up** — Grab an object within reach.
- Hands tied in front: limited grab range
- Hands tied behind: grab things behind you, use mouth or feet for things in front
- Objects modify Struggle effectiveness (sharp edge speeds up bond-breaking) or serve as puzzle elements (place object on pressure plate, throw to hit a switch)
- Core loop: Move to find tools → Pick Up → Struggle with tool to escape faster

### Feign (State, not a Verb)
The detective can voluntarily re-enter a "looks bound" state after freeing themselves. This is not a fourth verb — it's a *state* the player toggles, and the existing verbs behave differently inside it.

- **Toggle**: F (or context prompt when a guard is approaching)
- **In Feign state**: Move is disabled (you're holding still). Struggle becomes a windup — building force for a single decisive action (kick a door open, kick a guard, snap a final restraint). Pick Up disabled.
- **Visual tell**: Detective slumps into bound posture, restraints visually re-applied (loose, but reads as bound from a distance).
- **Purpose**: Lets the player weaponize stillness. Plants the seed in Act 2 (avoid detection by feigning), pays off in the finale (turn the tables on the gloating guard).

This keeps the three-verb identity intact while adding strategic depth.
- **WASD / Left Stick** — Move (contextual: hop, scoot, inch, swing based on restraint type)
- **Shift+A / Shift+D** — Roll left / right (floor restraint only; lateral-only, fast, noisy)
- **E / Face Button** — Context-sensitive interact (defaults to nearest valid action)
- **F / Button** — Toggle Feign state (only available when free of bonds; see Feign section above)
- **1–3 / D-Pad** — Select verb directly (Struggle, Move, Pick Up)
- **R / Button** — Reset room to starting state
- **No inventory system** — you use objects in place or carry one thing at a time
- **No combat** — this is a brain game

### Restraint Types (Vary Per Level)
- **Chair** — tied to a wooden/metal chair. Can hop, scoot, tip over. Classic.
- **Floor** — hands bound, lying down. Can roll, crawl, use feet.
- **Cuffed to fixture** — handcuffed to a pipe, radiator, railing. Limited radius of movement.
- **Hanging** — suspended by wrists. Can swing, kick, use momentum.
- **Duct tape / zip ties** — can be weakened by Struggle over time, unlike rope or cuffs.

Each restraint type changes how the three verbs behave, giving levels distinct feel without adding new mechanics.

---

## Character

**The Detective** — unnamed (or player-named). Low-poly, expressive face, trench coat or rumpled suit. Animate for personality: frustrated squirming, exasperated head shakes, smug grin when they figure something out.

**Voice / Muttering:** The detective thinks aloud. This serves three purposes:
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

- **L1 — The Back Room:** Tied to a wooden chair. Room has a nail sticking out of the wall. Struggle to loosen chair, Move to the wall, Struggle against the nail to cut rope. (Tutorial: Struggle + Move)
- **L2 — The Storage Unit:** Chair again. A shelf nearby has a box cutter on the edge. Move to the shelf, bump it to knock the cutter down, Pick Up, cut free. (Tutorial: Move + Pick Up)
- **L3 — The Office:** Chair, hands behind back. Desk nearby has a drawer slightly ajar with scissors inside. Move to desk, bump it to jostle the drawer open, Pick Up scissors, Struggle with scissors to cut through rope fast. (Tutorial: Pick Up as Struggle modifier — tools make escape faster)
- **L4 — The Van:** Duct-taped on the floor of a van. Struggle to weaken tape. Inch (precision) or Roll (faster but limited space in van) to the back door. Once free of tape, brace against the floor and Struggle-windup to kick the door open. (Introduces floor restraint, dual floor traversal modes, and the Struggle-as-windup pattern that the finale will reuse.)
- **L5 — The Basement:** Cuffed to a radiator pipe. Reach radius is limited. Must use objects within range creatively. First real multi-step puzzle combining all three verbs.

### Act 2 — Thriller (Levels 6–10)
**Setting:** Escalating. A hotel room, a shipping container, a penthouse, a warehouse with catwalks.
**Tension:** Soft timers. Guards checking in on a schedule. You hear footsteps. A door opening down the hall. A voice saying "I'll be back in five minutes."
**Restraints:** Mix of all types, more complex setups (chair + handcuffs, floor + locked room).
**Detective mood:** Nervous, improvising. "Okay. Okay okay okay. Think."

- Puzzles require 4–6 steps
- Introduce guards as environmental obstacles (time your escape around their patrols, avoid detection)
- One level where you're restrained in a new way mid-level (freed from chair but room locks down)
- **Introduce Feign** in one Act 2 level: player frees themselves, hears a guard approaching, must Feign to avoid detection. Guard passes, scene continues. No kick yet — this is just planting the seed for the finale.
- At least one "aha moment" where a verb does something unexpected

### Act 3 — Absurd (Levels 11–15)
**Setting:** Full spy-thriller. A villain's study, an underwater base, a room filling with water, a bomb scenario, a volcano lair.
**Tension:** Hard timers. Visible countdown. The room is changing around you (water rising, walls closing, laser grid activating).
**Restraints:** Creative combinations. Hanging + the floor is electrified. Chair on a conveyor belt. Cuffed inside a slowly tilting room.
**Detective mood:** Panicked but quipping. "A conveyor belt. Into a pit. Sure. Sure!"

- Puzzles require 6–10 steps
- Multiple valid solutions for some rooms (rewards creative thinking)
- Environmental hazards force prioritization (escape the restraint AND deal with the room)
- **Level 15 — The Finale: Turn the Tables.** The most complex room. Multiple phases. Uses every restraint type and verb in sequence. The detective escapes a final, layered restraint — and just as the last bond falls, footsteps approach. Phase shift: the player must **Feign** before the guard enters, holding still while he saunters in to gloat over his apparently helpless captive. Struggle (windup) charges a decisive kick. When the guard is in range, release: the detective kicks him, incapacitates him, takes his keys, and walks out the door. Satisfying payoff that recontextualizes every verb the player has learned — Move becomes stillness, Struggle becomes the strike, Pick Up becomes the keys.

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
- `PlayerController` — Movement per restraint type, verb selection, interaction raycasting
- `RestraintSystem` — Defines movement rules and verb behaviors per restraint type (chair, floor, cuffed, hanging, tape)
- `VerbSystem` — Handles the three core actions, context sensitivity, cooldowns
- `PuzzleManager` — Per-level puzzle state, step tracking, win condition
- `InteractableBase` — Base class for all interactive objects (sharp edge, key, switch, door, tool)
- `GuardAI` — Simple patrol/check-in behavior for Act 2–3 (waypoints, timer-based)
- `TimerSystem` — Manages soft and hard timers, triggers failure state
- `MutterSystem` — Triggers character lines based on context (idle, hint, success, failure)
- `LevelManager` — Scene loading, level progression, completion tracking
- `AudioManager` — Singleton for SFX and music, per-act music switching
- `UIManager` — Verb HUD, mutter text display, menus, timer display

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
│   │   │   └── RestraintSystem.cs
│   │   ├── Verbs/
│   │   │   └── VerbSystem.cs
│   │   ├── Puzzle/
│   │   │   ├── PuzzleManager.cs
│   │   │   └── InteractableBase.cs
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
│   │   ├── Player/
│   │   ├── Interactables/
│   │   ├── Guards/
│   │   └── UI/
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   ├── Levels/
│   │   └── Credits.unity
│   ├── Art/
│   │   ├── Models/
│   │   ├── Materials/
│   │   ├── Animations/
│   │   └── Textures/
│   ├── Audio/
│   │   ├── Music/
│   │   └── SFX/
│   ├── ScriptableObjects/
│   │   ├── Restraints/
│   │   ├── MutterLines/
│   │   └── PuzzleData/
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
- [ ] Repo setup, Unity project, folder structure
- [ ] PlayerController: chair-based movement (hop, scoot, tip)
- [ ] Verb system: three verbs selectable, context-sensitive interact
- [ ] One interactable object (rope on nail — cut free with Struggle)
- [ ] Level 1 fully playable with placeholder art (Unity primitives)
- [ ] Basic mutter system (text popup near character)

### Weeks 3–4 (Days 15–28): Core Systems
- [ ] Second restraint type (floor) with different movement
- [ ] Pick Up verb functional (tool-modified Struggle)
- [ ] Levels 1–5 playable (Act 1 complete)
- [ ] Level progression (complete room → load next)
- [ ] Core SFX (struggle, move, pick up, success, failure)
- [ ] Placeholder character model (can be Unity primitive humanoid or free asset)

### Weeks 5–6 (Days 29–42): Content + Polish
- [ ] Third restraint type (cuffed to fixture)
- [ ] Guard AI for Act 2 (simple patrol, alert state)
- [ ] Soft timer system
- [ ] Levels 6–10 designed and playable (Act 2 complete)
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
