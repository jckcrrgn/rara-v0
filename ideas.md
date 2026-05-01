# Rara v0 — Ideas & Notes

Scratch pad for ideas, observations, and things to try later.

## Polish Backlog (from Day 23 playtest)

Tier 1 — Legibility blockers (do before next playtest)
- L3 desk bump finickiness: Make desk non-kinematic with finite mass.
  Drawer opens via accumulated jostle (visible give), not hidden
  threshold. Generalizes to future "bump heavy thing" puzzles.
- L4 prone-kick suppression: Kick must be unavailable in prone (inch)
  mode. Logic + narrative + pedagogy fix in one — currently lets
  players win L4 without learning scoot. Gate via existing
  GetKickDirection / restraint state.
- L4 kick signaling: Nothing tells player kicking the door is the
  answer. Cheapest fix first: one-shot mutter line on entry, or faint
  door highlight, or door rattles when player is near + facing it.
- Win UI text: ✅ DONE Day 23. (LevelManager auto-advances; text
  trimmed to "LEVEL COMPLETE" across all scenes; gameCompleteUI slot
  available for L5 / future finale.)

Tier 2 — Feel pass
- L1 chair turn uniformity: Add jitter to ChairRestraint rotate
  routine. Per-step rotation ±20%, stutter in easing curve, occasional
  double-step or stuck-step. She's tied to a chair, not a microwave.
- L1 hop distance: Tune down. Single serialized field.
- L4 inch input: Hold-W instead of tap-W. Tap was right for chair
  (deliberate effort). Floor wants continuous grinding effort.
- L4 starting distance: Move player closer to door at spawn. Also
  reduces inch-tedium.
- KickableDoor stiffness: Add wind-up before door gives, or make hinge
  rotation progressive rather than binary closed/open.

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

## Mechanics
- Struggle as universal verb (Day 5): Struggle always works against bonds, just at different rates. Pick Up modifies struggle effectiveness via tools (nails, box cutters, etc.). Late-game difficulty comes from stronger bonds requiring stronger tools, plus timers preventing slow bare-hands escape. This is the core mechanic identity.
- Settings -> Keybinds
- Diegetic struggle feedback (Day 7): Bond progress should be communicated by the bonds themselves visually degrading — tight rope/zip-tie → frayed → loose → falls away. No HUD bars, no numbers. Immersion is the aim. Currently approximated with a worldspace bond meter above the player as scaffolding; delete and replace once the character model + bond geometry exist. The meter is temporary by design — do not polish.
- - Hands-behind as pickup range modifier (Day 13, future iteration):
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
- Kick-and-catch (Day 23): Objects on the floor can be kicked into the air and then caught with good timing.


## Feedback Patterns
- Twist shake (Day 12): Rejection feedback reads best as slow windup + snap-past-origin + settle, rotation rather than position. Pattern is reusable for other "wrong tool / wrong action" moments.


## Bugs-That-Are-Features
- Chair tipping felt great (Day 2): When the cube fell over during early testing, hopping stopped working and it genuinely felt like a tied-up detective whose chair had tipped. Accidental but authentic. Could be a real mechanic — maybe struggling increases tip risk, or certain collisions tip you. Would transition player to floor restraint. Revisit when floor movement is built.
-   -Chair-tipping to bring hands closer (Day 23): Chair-tipping could be intentional to bring hands closer to an object on the floor.
- Box cutter lands on Player's head (Day 10): During L2 shelf-bump tuning, the cutter fell directly onto the player cube's head and sat there. Felt authentic to the detective's whole vibe — long-suffering, things land on them. Could be a deliberate bit for L2: shelf bump always puts the cutter on/near the player, not on the floor. Revisit when character model replaces cube.
- Cutter mass tuning (Day 12): Box-cutter-on-head bit only works if mass is low (~0.1) and shelf fall impulse is tuned. At default mass the cutter crippled hop and killed L2. Emergent charm still needs a tuning pass to stay fun.


## Session Notes
- Cut Call Out from v0 (Day 6): Originally planned as the 4th verb, but in a single-room escape game with no stealth/dialogue/guard AI, it had no real job. Reserved for a potential larger sequel where stealth sections + guard personality dialogue (Charm/Intimidate/Beg) would justify it. For v0, three verbs (Struggle, Move, Pick Up) keeps the design tight.
- - L4 narrative redesign (Day 18, post-playable): The detective doesn't
  need to be fully unbound to kick. Rework so bonds are unbreakable
  bare-hands (zip ties + no tool, or extra-thick duct tape) and the
  kick is the only escape. Resolves the "why am I struggling against
  tape if I just need to kick" question and cleanly separates Struggle
  from Kick as verbs. Will need a tweak to KickableDoor's
  IsBroken gate — kick should work regardless of bond state on L4 specifically.
  Possibly: a per-door `requiresFreeBonds` bool, default true for
  finale, false for L4.