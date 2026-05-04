# Rara v0 — Ideas & Notes

Scratch pad for ideas, observations, and things to try later.

## Polish Backlog (from Day 23 playtest)

Tier 1 — Legibility blockers (do before next playtest)
- ✅ L3 desk bump finickiness (Day 25): replaced binary Bumpable with
  physics-driven Jostleable + per-bump rattle on Drawer. Cumulative
  impulse, no decay. Pattern generalizes to future bump-heavy-thing
  puzzles.
- ✅ L4 prone-kick suppression (Day 24): kick force = 0 in inch mode;
  effort grunt still plays so player gets "you tried" feedback.
- ✅ L4 kick signaling (Day 25): handled via MutterSystem entry mutter
  ("...the door — if I can just turn around."). Less spammy and more
  in-character than the originally-considered proximity rattle.
- ✅ Win UI text (Day 23): LevelManager auto-advances; text trimmed.

Tier 2 — Feel pass
- L1 chair turn uniformity: Add jitter to ChairRestraint rotate
  routine. Per-step rotation ±20%, stutter in easing curve, occasional
  double-step or stuck-step. She's tied to a chair, not a microwave.
- L1 hop distance: Tune down. Single serialized field.
- ✅ L4 inch input (Day 25): Hold-W instead of tap-W. interCycleDelay
  preserves discrete cadence so it reads as automated rhythm, not
  smooth gliding. Symmetric across inch + scoot.
- L4 starting distance: Move player closer to door at spawn.
- KickableDoor stiffness: Add wind-up before door gives, or make hinge
  rotation progressive rather than binary closed/open. TODO comment
  already in KickableDoor.cs at OnKickRegistered.

Tier 3 — Content & polish
- L2 room dressing: Currently big and bereft. Add furniture/debris/set
  dressing. Possibly defer until character model lands.
- SFX wiring pass: kick layer (effort, miss thud, door thud, door
  open), L2 shelf bump, L5 cuff rattle, pipe shuffle, drag scrape,
  pin-click. Sourced from OpenGameArt + Freesound.
- L5 simplicity: Hold off. L5 is intro to new restraint + new verb;
  simple is correct. Reassess after SFX pass.

Parked (do not build in v0)
- L1 box-cutter pickup logic: Currently narratively weird that
  chair-tied detective picks up box cutter from floor. Two ideas
  surfaced in playtest: chair-tipping to bring hands close, or
  kick-and-catch as a verb. Kick-and-catch is genuinely novel but is
  a feature not a fix — needs new physics state, timing window, catch
  input, fail feedback. Park for post-v0.

## Day 25 Bugs Caught
- Struggle-shake invisible by default: shakeMagnitude was 0.08 (degrees,
  invisible). Bumped default. Likely affected every level since Day 1;
  no one noticed because failed struggles also have audio feedback.
  Lesson: when a feature appears not to fire, "is the configured
  intensity actually visible" is a 30-second check that should come
  first.
- L4 visualRoot=Player means FloorRestraint stomps shake rotation
  between frames. Currently shake is short enough to flicker through
  acceptably. If shake ever needs to be longer/smoother, refactor:
  visualRoot should be a child of Player so cosmetic juice composes
  with movement-owned root rotation instead of fighting it. Not
  urgent.

## Mutter System (Day 25, shipped)
Retro-style text box with per-word grunt SFX (Phoenix Wright /
Undertale aesthetic). Currently triggered only by LevelManager's
entryMutter SerializeField on Start. Player presses Space to dismiss;
all input gated while active.

Next steps for mutter:
- MutterTrigger component: collider-based fire-on-enter wrapping
  MutterSystem.Play(). Generalizes to mid-level beats — "...wait,
  what is this?" approaches, transitional mutters between rooms,
  enemy-spotting beats in future stealth segments.
- Mutter queue: currently new Play() during active mutter is dropped.
  Reconsider once a level wants two mutters back-to-back.
- Word-boundary refinement: em-dashes and ellipses currently get their
  own grunts (each non-space starts a "word"). May want to tune. See
  it run more before deciding.
- Per-character speakers: when other characters get dialogue (kidnapper,
  victims), they'll need their own grunt pools and possibly text
  styling. Probably v1.

## Mechanics
- Struggle as universal verb (Day 5): Struggle always works against bonds, just at different rates. Pick Up modifies struggle effectiveness via tools (nails, box cutters, etc.). Late-game difficulty comes from stronger bonds requiring stronger tools, plus timers preventing slow bare-hands escape. This is the core mechanic identity.
- Settings -> Keybinds
- Diegetic struggle feedback (Day 7): Bond progress should be communicated by the bonds themselves visually degrading — tight rope/zip-tie → frayed → loose → falls away. No HUD bars, no numbers. Immersion is the aim. Currently approximated with a worldspace bond meter above the player as scaffolding; delete and replace once the character model + bond geometry exist. The meter is temporary by design — do not polish.
- Hands-behind as pickup range modifier (Day 13, future iteration):
  For v0, hands-behind is narrative only (mutter + anim). Post-v0, explore
  hands-behind as a real mechanical variant where pickup range is limited
  to a cone behind the player. Creates new solve patterns:
    - Back up into a table to reach a cutter on its surface
    - Tip the chair backward (see chair-tipping note) to land on a floor
      cutter, bringing it into the behind-hands pickup zone
    - Shelf-bump logic might need a "bump with back" variant
  Ties together three dormant ideas: chair-tipping transitions, hands-behind,
  and Pick Up range as a puzzle dimension. Worth prototyping in a post-v0
  level pack or sequel.
- Body-Part Bonds (Day 19). Currently restraints are package deals; works for v0 but feels rigid. Refactor candidate for sequel.
- Double-cuffed escape (Day 19): An evolution of Cuffed-to-Pole. Since the Detective is a known escape artiste, the enemy applies 2 handcuffs: wrists around a pole AND elbows. The Detective frees herself from her wrist cuffs and the pole, but her arms remain bound.
- Stealth-between-escapes (Day 19): The structural innovation that could carry the game beyond "puzzle anthology." Detective escapes a restrained state → enters a stealth navigation segment (warehouse, mansion, etc.) → if caught, doesn't game over but transitions to a new (likely more severe) restraint state in a new escape room. Inverts standard stealth game logic: capture isn't punishment, it's the genre the player is good at. Retroactively justifies escape mechanics as the spine of the game. Implications: needs guard AI, free movement, a hub location, new camera, capture/recapture flow. Probably v1.0 or sequel scope. v0 stays as discrete escape rooms with narrative interstitials. Do not build this in v0. Do not start "just prototyping" it.
- FloorRestraint Roll (Day 20): Shift+A/D to roll. Faster, but requires space to maneuver.
- FloorRestraint orientation refactor (Day 21): Currently inch moves
  the detective headfirst (on her belly, prone). For L4 this means
  she has to pirouette ~180° at the door to get her feet to it —
  narratively awkward. Future refinement: separate inch (headfirst,
  prone, precise) from scoot (feet-first, supine, approach for kicks).
  Three-state floor movement: inch / scoot / roll. Each has different
  uses. Probably v0.1 polish, not v0.
  → Partially addressed Day 22-25: scoot mode added via C-toggle, kick
  is scoot-only. Roll still parked.
- Kick-and-catch (Day 23): Objects on the floor can be kicked into the air and then caught with good timing.
- IsBusy verb mutex (Day 25, shipped): All body-committing verbs
  (move, flip, kick) gate on a shared IsBusy state. Aim (A/D) does
  not. Detective can't kick while crawling, can't crawl while flipping,
  etc. Generalizes well — new verbs and new restraints can opt into
  this gate cleanly. Pattern documented in RestraintBase.IsBusy.

## Feedback Patterns
- Twist shake (Day 12): Rejection feedback reads best as slow windup + snap-past-origin + settle, rotation rather than position. Pattern is reusable for other "wrong tool / wrong action" moments.
- Physics-as-feedback (Day 25): The Jostleable refactor crystallized
  a pattern: when a puzzle threshold is invisible (binary trigger,
  hidden float), the fix is usually to let the physics itself be the
  readout. Heavy thing visibly nudges → player sees their input
  registering → escalating SFX scales with progress → puzzle clears
  diegetically. Reusable across any "build up effort against a
  threshold" interaction.

## Bugs-That-Are-Features
- Chair tipping felt great (Day 2): When the cube fell over during early testing, hopping stopped working and it genuinely felt like a tied-up detective whose chair had tipped. Accidental but authentic. Could be a real mechanic — maybe struggling increases tip risk, or certain collisions tip you. Would transition player to floor restraint. Revisit when floor movement is built.
- Chair-tipping to bring hands closer (Day 23): Chair-tipping could be intentional to bring hands closer to an object on the floor.
- Box cutter lands on Player's head (Day 10): During L2 shelf-bump tuning, the cutter fell directly onto the player cube's head and sat there. Felt authentic to the detective's whole vibe — long-suffering, things land on them. Could be a deliberate bit for L2: shelf bump always puts the cutter on/near the player, not on the floor. Revisit when character model replaces cube.
- Cutter mass tuning (Day 12): Box-cutter-on-head bit only works if mass is low (~0.1) and shelf fall impulse is tuned. At default mass the cutter crippled hop and killed L2. Emergent charm still needs a tuning pass to stay fun.

## Session Notes
- Cut Call Out from v0 (Day 6): Originally planned as the 4th verb, but in a single-room escape game with no stealth/dialogue/guard AI, it had no real job. Reserved for a potential larger sequel where stealth sections + guard personality dialogue (Charm/Intimidate/Beg) would justify it. For v0, three verbs (Struggle, Move, Pick Up) keeps the design tight.
- L4 narrative redesign (Day 18, post-playable): The detective doesn't
  need to be fully unbound to kick. Rework so bonds are unbreakable
  bare-hands (zip ties + no tool, or extra-thick duct tape) and the
  kick is the only escape. Resolves the "why am I struggling against
  tape if I just need to kick" question and cleanly separates Struggle
  from Kick as verbs. Will need a tweak to KickableDoor's
  IsBroken gate — kick should work regardless of bond state on L4 specifically.
  Possibly: a per-door `requiresFreeBonds` bool, default true for
  finale, false for L4.
