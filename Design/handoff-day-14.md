# Rara v0 — Project Handoff (End of Day 14)

## Project Status Snapshot

**Day**: End of Day 14 (Week 2 complete)
**Streak**: 14 consecutive days of game dev sessions
**Repo**: github.com/jckcrrgn/rara-v0
**Engine**: Unity 6 (URP), C#

## What's Built

### Working Gameplay
- **Level 1** (`Assets/Scenes/Levels/Level_01.unity`) is fully playable end-to-end
- Player (cube placeholder) hops around an enclosed room with WASD
- Three valid solutions to escape:
  1. Slow: struggle against the nail repeatedly (1+5 progress per press, 25 needed)
  2. Fast: pick up box cutter, struggle anywhere (1+10 = 11 per press)
  3. Fastest: hold box cutter + struggle at nail (1+10+5 = 16 per press)
- Win UI displays "LEVEL COMPLETE / Press R to restart"
- R key restarts the level cleanly

### Scripts in Place
- `Assets/Scripts/Player/PlayerController.cs` — movement, ground check, struggle, pickup, held item tracking
- `Assets/Scripts/Puzzle/InteractableBase.cs` — abstract base class with `StruggleModifier` virtual property
- `Assets/Scripts/Puzzle/Pickupable.cs` — base class for carryable objects
- `Assets/Scripts/Puzzle/Nail.cs` — environmental struggle modifier (boost: 5)
- `Assets/Scripts/Puzzle/BoxCutter.cs` — pickupable struggle modifier (boost: 10)
- `Assets/Scripts/Managers/LevelManager.cs` — singleton, tracks win state, handles restart, manages win UI
- `Assets/Scripts/UI/HoldingIndicatorUI.cs` — shows held item in top-right when carrying something

### Scene Hierarchy (Level_01)
```
Main Camera (positioned at 0,8,-10, rotated 35° down)
Directional Light
Floor (plane)
Player (cube with Rigidbody, frozen X/Z rotation)
Walls
├── Wall_North, Wall_South, Wall_East, Wall_West
Nail (cylinder protruding from north wall, with Nail script)
LevelManager (empty GameObject with LevelManager script)
Canvas
├── HUD (bottom-stretch, 100px tall, Horizontal Layout Group)
│   ├── VerbPanel_Move ("MOVE / WASD")
│   ├── VerbPanel_Struggle ("STRUGGLE / SPACE")
│   └── VerbPanel_PickUp ("PICK UP / E")
├── LevelCompleteUI (Panel, full-screen stretch, disabled by default)
│   └── WinText (TMP)
└── HoldingIndicator (top-right, hidden by default)
    └── HoldingText (TMP)
EventSystem
BoxCutter (cube with BoxCutter script)
```

## Key Architectural Decisions

### Design Identity
**The core mechanic identity (decided Day 13):** Struggle is the universal verb that always works against bonds. Pick Up is a modifier system — held items boost struggle effectiveness. Difficulty scales by making bonds stronger and adding timers, requiring better tools.

### Verb Count
**Cut from 4 verbs to 3 (decided Day 14):** Removed Call Out from v0 scope. Original GDD had it, but in a single-room escape game with no stealth/dialogue/guard AI, it had no real job. Reserved for potential larger sequel. Three verbs (Struggle, Move, Pick Up) keeps the design tight.

### Patterns
- **InteractableBase** uses an abstract class with virtual methods so each interactable only overrides the verbs it cares about
- **Singleton pattern** on LevelManager (`LevelManager.Instance`) so any script can call `CompleteLevel()`
- **Bond progress lives on the Player**, not on the Nail — the bonds are what need defeating, the nail/cutter are tools that help
- **Collision-based grounding** instead of raycast — uses OnCollisionStay/Exit to track contact with anything below the player

## Known Quirks & Gotchas

### VS Code auto-imports
VS Code regularly auto-inserts `using System.Diagnostics;` at the top of new scripts. This causes ambiguous reference errors with `Debug`. **Always check the top of any new script and delete that line if present.** A permanent fix exists in VS Code settings (disable "show completion items from unimported namespaces") but the user hasn't applied it yet.

### Unity UI footguns we hit
- A new empty GameObject inside a Canvas has zero size unless anchors are set. Always set anchors + Left/Right/Top/Bottom or Width/Height after creating UI containers.
- A UI element with **Scale = 0** is invisible but reports as active. If something is "active but invisible," check Scale first.
- Don't put a "show/hide myself" script on the object it's hiding — once the object is disabled, the script can't re-enable it. Put toggle scripts on a parent that stays active (we put `HoldingIndicatorUI` on Canvas).

### Project file structure
```
Assets/
├── Materials/ (Mat_Nail, Mat_BoxCutter)
├── Scenes/Levels/ (Level_01.unity)
├── Scripts/
│   ├── Managers/ (LevelManager.cs)
│   ├── Player/ (PlayerController.cs)
│   ├── Puzzle/ (InteractableBase.cs, Pickupable.cs, Nail.cs, BoxCutter.cs)
│   └── UI/ (HoldingIndicatorUI.cs)
├── Settings/
└── TextMesh Pro/
```
Documentation files (`rara-v0-gdd.md`, `ideas.md`) live in repo root, NOT inside Assets/.

## Saved Ideas (in `ideas.md`)

- **Chair tipping mechanic (Day 10):** During early testing, the cube tipped over and hopping stopped working — felt authentic to a tied-up character. Could be reintroduced as intentional restraint state transition (chair → floor) triggered by struggling too hard or specific collisions.
- **Struggle as universal verb (Day 13):** Documented the core mechanic identity above.
- **Cut Call Out from v0 (Day 14):** Documented the scope decision above.

## What's Next

### Immediate (next 1-2 sessions)
1. **Update the GDD to reflect 3-verb design** — currently still says 4 verbs throughout level descriptions. Needs a pass.
2. **Add visual feedback for struggle progress** — currently only logged to console. Should be visible in-game (HUD bar showing bond strength being depleted, or a visual indicator on the player).
3. **Scaffold Level 2 + scene loading** — LevelManager has `LoadScene` for restart, but nothing loads "next level" after winning. Need a proper level-progression flow.

### Week 3-4 priorities (per GDD milestone schedule)
- Second restraint type (floor) with different movement (roll/crawl instead of hop)
- Second pickupable + better interaction visualization (highlight nearest interactable)
- Levels 2-5 designed and playable (Act 1 complete by end of Week 4)
- Core SFX (struggle, pickup, success, failure)
- Placeholder character model (currently just a white cube)

## User Context (for new conversation)

### The 90-Day Transformation
User is on a parallel 90-day program with three pillars:
1. **Sobriety**: 90 days clean from cannabis, alcohol, and porn
2. **Cut to 165 lbs** from starting 195 (currently around 192)
3. **Daily 1-hour game dev habit** (this project)

User wakes ~12pm (late shifts), works out at 2:30pm following a 5-day push/pull/legs hypertrophy split, and does game dev at 1:30pm.

### Communication Preferences
- Direct and honest, no sugarcoating
- Concise responses unless detail requested
- Help process slips without shame, then refocus
- Push to ship, not perfect
- Track progress across sessions (user does daily check-ins for cravings/weight/training)

### Working Style Observed
- User is novice at coding/Unity but learns fast and asks good questions
- Pushes back on design decisions when they don't feel right (this is good — produced the best decisions of the project)
- Articulates feature ideas well, including knowing when ideas are out of scope
- Uses `ideas.md` for design thoughts, GDD for canonical reference
- Maintains a separate devlog (not committed to repo) summarizing each session

### Known Triggers / Watch For
- Frustration during debugging can spike cravings — name it directly when you see it building
- Anticipates spike days (alone time, unstructured time) — help pre-plan responses
- 14 days in: cravings mostly 0/0/0 with occasional low spikes for porn

## How to Use This Doc

User will paste this at the start of the new conversation along with a Day X check-in. Read it, ask any clarifying questions, then proceed with the next session's work.
