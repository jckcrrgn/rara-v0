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

## Day 30 Playtest

Two playtesters: Retro (gamer friend, remote via Discord) and Molli
(relative non-gamer, in-person). Full L1–L5 playthrough both. Build
was the post-Day-29 chair-sync version with L4 wall distinction and
rewritten L4 entry mutter.

### Headline finding

The game does not communicate that barehanded struggle is sometimes
sufficient and sometimes not. Both playtesters, independently, learned
"Struggle is the answer" in L1 and carried that model forward; both
hit the same cascade of confusion when L2 didn't reward it. This is
not a polish issue — it's the core mechanic legibility breaking down,
and it shows up in the data three different ways:

- Retro L1→L2 cascade: in L1 he hopped over to the (decorative) box
  cutter, "stood on it," struggled, and escaped. He believed he was
  using the cutter. When L2 didn't work the same way he concluded the
  game was bugged. "It's not letting me use the cutter." A minute
  later, on discovering the verb cards: "Oh I'm stupid. I need to
  read."
- Molli L2 accidental-pickup: she got the cutter down from the shelf,
  struggled for a minute, then asked "Do I have to get the box cutter
  off [my head]?" — and resolved L2 likely by accidentally pressing E
  during her mashing. She did not find the verb cards UI until 5
  minutes into L4. She had no model of Pick Up as a verb until then.
- Molli L3 false-positive: she struggled out of L3 without touching
  the drawer, and asked "Does that count? I didn't touch the drawer,
  though..." She solved an intended-tool puzzle without the tool and
  the win felt unearned to her.

Root cause: barehand struggle was originally intended as a slow,
thoughtful process. Tuning collapsed it to a spammable Space-press.
The teaching loop the design notes describe ("Struggle is universal,
tools modify effectiveness") never fires because the unmodified
version is already fast enough to win.

### Secondary finding: ControlHintsPanel is invisible

Molli didn't find the verb cards until 5 minutes into L4 (worsened by
ultrawide monitor pushing them off to the side). Retro found them
mid-L2 and treated the discovery as a personal failure ("I'm stupid").
The panel is currently load-bearing for verb discoverability and not
catching anyone. Direction: demote it. Use diegetic teaching (mutters)
for first-time verb introduction; let panel stay as quiet always-on
reference but stop relying on it. Pause menu with controls reference
is the eventual right home for "what can I do" reference (Molli's
suggestion in debrief), but pause menu is not Tier 1.

### L4 specifics

L4 was the biggest hurdle. Retro 1m10s, Molli 5+ minutes. Molli's L4
time was largely downstream of still not having a verb model — she was
asking "what did the instructions say?" and "how do I go forward?"
because Pick Up / scoot toggle / kick were all simultaneously novel.
Once she found verb cards she resolved fast. Retro's 1m10s suggests
that with a verb model in hand, L4's design is approximately fine.

Retro flagged: L4 door reads as wall, would benefit from a line/seam
to read as a door. (Building on yesterday's wall-distinction work.)

### Bright spots (do not let these get drowned out)

- Both laughed at mutters. MutterSystem as tone vehicle: validated.
- Both liked physics interactions (Retro especially: drawer rattle,
  shelf bump).
- Both got through L5 in <1 minute once verb model was in place. L5
  is the proof that every level can feel that way once verb legibility
  is fixed. Hold this when tempted to add complexity to L5 — its
  current simplicity is correct.
- Difficulty ramp landed for Molli: "Each level gets harder."
- Tone landing: Molli laughed at L5 mutter ("handcuffs. How
  romantic."), Retro laughed at L4 mutter ("How do I keep getting
  myself in these situations?"). Character voice is working.
- Molli, debrief: "It's very charming."

### Verbatim quotes (devlog/Patreon)

- Molli: "handcuffs. How romantic." (L5 mutter)
- Molli: "when I got to L4 it's like... how the fuck do I kick?"
- Molli: "It's very charming."
- Molli: "Each level gets harder."
- Molli: "Do I have to get the box cutter off [my head]?"
- Retro: "It's not letting me use the cutter." (L2, pre-discovery)
- Retro: "Oh I'm stupid. I need to read." (verb card discovery)
- Retro: laughed at "How do I keep getting myself in these situations?"

### Tier 1 — Must ship before Playtest #2

Without these, Playtest #2 is poisoned data — every player will hit
the same cascade.

- Barehand struggle does nothing. ChairRestraint, FloorRestraint,
  CuffedRestraint: Struggle without a held tool produces zero bond
  progress. Effort SFX/animation still plays for "you tried" feedback
  (same pattern as L4 prone-kick suppression). Keystone fix; the rest
  of the cascade resolves from here. Note: this is a real shift from
  the design-notes framing of "Struggle is the universal verb" — Pick
  Up effectively becomes the gating verb, with Struggle as the closing
  verb. Update GDD to reflect. Revisiting universal Struggle as a
  meaningful verb in its own right is a v1 question (see thumbstick
  prototype note below).
- MutterTrigger component. Collider-based, fires
  MutterSystem.Play() on enter, configurable fire-once vs repeat.
  Already designed in the Mutter System notes above; pulled forward
  by today's findings. Infrastructure for the L1 + L4 teaching chains.
- L1 teaching mutter chain. Three beats:
    - Re-tune entry mutter to gesture toward "I need something to cut
      these"
    - MutterTrigger near cutter: "...if I could just pick that up."
    - Optional after N failed barehand struggles: "...too tight. Bare
      hands won't do it."
- L4 teaching mutter additions. Existing entry mutter stays (it
  landed). Add:
    - MutterTrigger near door for orientation cue: "...need to turn
      around. Get my feet to it."
    - Possibly a trigger on first failed face-on kick: "...wrong
      way."

Treat MutterTrigger + L1 chain as one composite feature — neither is
useful without the other. Effective max-3 for Playtest #2:
1. Barehand struggle does nothing
2. MutterTrigger + L1 teaching chain
3. L4 teaching mutter additions

### Tier 2 — Polish, log for after Playtest #2

These are real findings but not blocking. Some may auto-resolve once
Tier 1 lands; resist pre-emptive fixes — the point of waiting is to
see what's *left* after Tier 1.

- L4 door visual: add line/seam so it reads as a door not a wall.
  Iterating on yesterday's wall-distinction work.
- Pause menu with controls reference. Molli's debrief suggestion.
  The right long-term home for "what can I do" reference, but only
  worth building once mutter-as-teaching is validated.
- ControlHintsPanel ultrawide positioning. Anchor verb cards to
  respect ultrawide aspect ratios. Matters less if panel becomes
  non-load-bearing post-mutter-chain, but still real.
- L3 bond strength tuning. Molli struggled out without the scissors.
  Tier 1 #1 (no barehand progress) likely fixes this automatically.
  Verify after Tier 1; don't pre-tune.
- L1 cutter placement. The decorative cutter on L1's floor is what
  enabled Retro's "stand on it and struggle" misread. Tier 1 #1 will
  make standing-and-spamming-Space produce nothing, which teaches him
  to try E — but the cutter being there at all is questionable.
  L1's intended solve is the nail; the cutter is a redundant
  alternative that *taught a wrong lesson*. Consider removing from L1
  entirely and letting the nail be the single solve.

### Tier 3 — Notes, not the build

- Mutter system as tone vehicle: validated by both playtesters
  laughing.
- Physics interactions are loved: drawer, shelf, kick. Lean into
  this in future levels.
- L5 simplicity is a feature, not a bug. Both playtesters solved <1
  min once they had verb model. Don't add complexity to L5; let it
  be the easy denouement after L4's spike.
- "It's very charming" is the tone we're hitting. Hold this.
- Difficulty ramp ordering (excluding the verb-legibility issue) is
  felt by players. The intended curve works once the legibility
  fog clears.

### Future-iteration note: Struggle as a real verb

Today's decision to make barehand struggle do nothing is the right
call for v0 *given the current Space-spam implementation*. The reason
it works as a tone choice — "she's tied too well, this isn't
neighborhood kids" — is genuine, but it also tacitly admits that the
current Struggle verb has no mechanical body to it. A previous
prototype involved feeling around for "sweet spots" on dual
thumbsticks and rubbing across both simultaneously. That kind of
fleshed-out Struggle could re-justify universal barehand struggle as
a meaningful verb in its own right. Park for v1 / sequel. Worth
writing up properly — easy thing to forget if it lives only in
chat.
