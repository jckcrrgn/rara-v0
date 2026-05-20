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
  **Day 37 update:** Chair-tipping branch is now L6-canonical, not
  L1-future (see Mechanics → Chair-tip gate Day 37, and GDD L6
  paragraph). The original L1-cutter narrative-weirdness motivation
  is also moot: the cutter was removed from L1 in Day 37 audit, so
  there's no longer a chair-tied-detective-picking-up-floor-cutter
  situation on L1 to solve. Kick-and-catch remains parked for post-v0.

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
- ✅ MutterTrigger component (Day 30s, shipped): Collider-based fire-on-enter
  wrapping MutterSystem.Play(). Configurable fire-once vs repeat. Used by L1
  + L4 teaching chains. Will also drive most of the L6 chain (see GDD L6
  mutter chain addendum).
- ⏳ Mutter queue (Day 37, scheduled for L6 infrastructure): Currently new
  Play() during active mutter is dropped. L6 Beat 6 fires guard-then-Cassie
  back-to-back; that's the level that needs the queue. Build pairs with
  per-character styling — neither ships meaningfully alone. Design: FIFO
  buffer, drain on completion of current mutter, decide on max queue depth
  and drop-vs-stall on overflow.
- ⏳ Per-character speakers (Day 37, scheduled for L6 infrastructure): L6
  debuts the offstage guard, who needs distinct grunt pool and text styling
  from Cassie. Threaded as Speaker enum/SO through MutterSystem.Play().
- Word-boundary refinement: em-dashes and ellipses currently get their own
  grunts (each non-space starts a "word"). May want to tune. See it run
  more before deciding. Low priority — not blocking any shipped or
  scheduled work.

## Mechanics
- Struggle as universal verb (Day 5): Struggle always works against bonds, just at different rates. Pick Up modifies struggle effectiveness via tools (nails, box cutters, etc.). Late-game difficulty comes from stronger bonds requiring stronger tools, plus timers preventing slow bare-hands escape. This is the core mechanic identity.
- Settings -> Keybinds
- Diegetic struggle feedback (Day 7): Bond progress should be communicated by the bonds themselves visually degrading — tight rope/zip-tie → frayed → loose → falls away. No HUD bars, no numbers. Immersion is the aim. Currently approximated with a worldspace bond meter above the player as scaffolding; delete and replace once the character model + bond geometry exist. The meter is temporary by design — do not polish.
- Knees as time-cost flag (Day 35): Currently in the BoundLimbs enum
  but not part of any default Act 1 restraint after the canon
  correction. Leading candidate for a job: time-cost flag that adds
  duration to leg-untie sequences. Distinct from the other bond flags
  which modify per-attempt action effectiveness (struggle/movement/kick
  multipliers); Knees would be the first sequence-duration modifier.
  Sets up the kind of decision gameplay we'd want in a future
  stealth-adjacent scenario: Cassie's freed her hands, started working
  on her legs, hears a guard approaching. Untie just Ankles fast and
  feign-still? Or risk untying both Knees + Ankles and bolt? Genuine
  stakes from a bound state. Park as the strongest candidate; revisit
  when stealth-between-escapes prototype lands (post-v0). Other
  candidates considered and rejected: mermaid-kick disabler (real
  mermaids kick fine with knees bound — the framing was reaching);
  gates a stand-and-waddle chair mode (no clear puzzle that needs it).
  If no time-cost mechanic ships by end of Act 2 design pass, cut
  Knees from the enum — vestigial flags are debt.
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
- Stand up from FloorRestraint (Day 35): The verb-counterpart to chair-tip.
  Two branches by leg state:
    - Free legs: trivial stand-up. Movement becomes a normal walk (new
      state, not yet designed — probably just "walks like a normal person"
      since by this point she's mostly free anyway).
    - Bound legs (Ankles set): requires wall affordance. Back up to a wall,
      use it to push upright. Once standing, movement is chair-style hops,
      but with a balance-keypress on landing — hit the hop key (or another
      key) within a window or fall over. Falling re-enters FloorRestraint
      AND makes noise (future stealth hook: attracts guard attention).
  This is a lot mechanically — three discrete features stacked (stand-up
  verb, wall-affordance detection + back-up-to-wall movement, balance
  timing input + fall transition). Needs its own level to teach cleanly,
  per the Day 30 playtest lesson about not introducing multiple novel
  verbs simultaneously. Candidate spotlight mechanic for L7 (the level
  after L6's chair-tip teaches the chair → floor transition; L7 then
  teaches what to do once floor-bound in a room without a tool at
  floor-level).
  Notable design properties:
    - Composes with chair-tip as transition pair: chair-tip is "upright
      to low," stand-up is "low to upright." Together they make
      vertical position a real puzzle dimension.
    - Balance-keypress introduces reactive timing — a verb shape we
      don't have yet. Distinct from the commit-and-watch-physics texture
      of every other current verb. Closest the game gets to dexterity
      gameplay, which is fitting for the bound-and-precarious state.
    - Fall-makes-noise is the seed of the Day 19 stealth-between-escapes
      vision. Even without guards in v0, prototyping the noise→consequence
      chain here de-risks the eventual stealth segments.
  Park for L7 design pass. Do not build into L6.
- ChairRestraint back-up verb (Day 38, mechanic refined Day 42):
  Reverse-locomotion verb for ChairRestraint, complementing the existing
  forward hop. Came up during L6 design when we considered whether
  Cassie could open the nightstand drawer from her seated position.
  Without back-up, she has to face away from the drawer to reach it
  with bound hands, which is awkward to choreograph. With back-up, the
  interaction reads naturally — she backs up, fumbles open the drawer
  behind her, pulls out the pen. Decided against adding to L6 to keep
  mechanical scope contained. Revisit when a level needs Cassie to
  interact with something at chair-height that requires hands-behind-
  back access — probably L7+ alongside Stand Up from FloorRestraint,
  since both expand the locomotion vocabulary.

  **Day 42 motion model:** Mirror the inch-forward pattern from
  FloorRestraint. Cassie pushes on the ground in front of her with
  her bound legs, scooting the chair backwards by small increments.
  Less travel per cycle than the FloorRestraint inch (the chair's
  mass + the contact geometry of chair legs vs. flat floor make it
  inherently less efficient than a prone body's coordinated push).
  Same discrete-cadence input feel — hold-S with interCycleDelay so
  it reads as automated rhythm, not smooth gliding. Matches the
  inch input pattern shipped Day 25. Animation work still non-
  trivial: backward chair-hop / chair-scoot is harder to make
  read clearly than forward, and the leg-push contact point will
  need to be visually legible.
- Chair-tip gate (Day 37): Chair-tipping is L6-exclusive in v0. The
  ChairRestraint rocking verb is gated behind a `rockingEnabled` bool
  (default false); L1-L3 leave it false, L6 flips it true and wires
  floorRestraintOnBreak. Two reasons: (1) chair-tip is canonically L6's
  debut mechanic per the GDD, and shipping it on L1 burns L6's spotlight;
  (2) tipping has no v0 recovery path (stand-up verb is L7), so any
  level without a floor-bound solve path softlocks on tip. L1
  specifically softlocks because its intended solve is hop-to-nail and
  floor-bound Cassie can't reach the nail. When rockingEnabled is false,
  Shift+A/D falls through to a normal turn-hop and the "Rock" hint is
  omitted from ControlHints (advertising a no-op verb is the Day 30
  legibility failure pattern). L6 will need its own mutter chain to
  teach rocking input cold; queue that for L6 design pass.
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

## Camera

- Follow camera mode (Day 38): Camera that tracks Cassie's position
  instead of staying fixed on the room. L1–L6 all use fixed-camera /
  whole-room-visible. Considered for L6 specifically because the
  offstage guard + interior tension might benefit from tighter framing
  on Cassie. Decided fixed camera is correct for v0 — consistent with
  prior levels, lets the player see the spatial puzzle clearly, fits
  "calculating Cassie" who's aware of her whole environment. Revisit
  if Act 2 levels start feeling spatially distant from the player.
  Case *for* follow: more cinematic, emphasizes Cassie's POV, makes
  offstage-ness more felt (you can't see the door from across the room
  when focus is on Cassie). Case *against*: breaks consistency with
  Act 1, may obscure spatial puzzle elements, more implementation
  work. Implementation: standard Cinemachine virtual camera with
  follow target on player. Could be per-level setting (some fixed,
  some follow) rather than global mode shift. Boundary-clamped so
  camera doesn't reveal off-room space.

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

## Design Principles

Meta-principles that emerged from specific design sessions. Captured
here so they don't evaporate by Act 2. When designing future levels,
re-read.

- Tipping ≠ jostling (Day 38, from L6 design): Tipping is for
  chair-break + floor-access. Jostling is for physics interaction with
  objects. Two independent verbs serving two independent purposes.
  Don't conflate them in future level design — early L6 drafts kept
  trying to make tipping do double-duty as a puzzle-interaction verb
  (tip into nightstand to knock lamp off), which muddied both
  mechanics. Separating them gave the player three clean escape paths
  instead of one tangled one.
- Diegetic timer triggers > level-load timers (Day 38, from L6
  design): L6's timer starts on Cassie's loud actions (lamp smash,
  chair-tip crash), not on level entry. Pattern: player action causes
  pressure to begin. Feels stronger than ambient timer pressure and
  should be the default for future timed levels. Players who play
  carefully experience the level differently than players who barrel
  in — that's a level with texture.
- NPC theory-of-the-player has blind spots (Day 38, from L6 design):
  L6's guard cleans lamp shards (obvious tool source) but not chair
  shards (just furniture damage in his perspective). The gap between
  his theory and Cassie's resourcefulness is where the puzzle lives.
  Worldbuilding through guard heuristics. Generalize: design NPCs
  with specific blind spots that become exploitable. Recurring puzzle
  structure for any future stealth-adjacent scenario.
- Persistence-of-some-state but not-others is a design knob (Day 38,
  from L6 design): L6 persists chair shards + lamp damage +
  pen-if-picked-up; resets chair position + drawer state + Cassie's
  bonds (to escalated state). This selective persistence makes
  attempts mechanically distinct without authoring new puzzles. Use
  deliberately, not by accident. Powerful for failure-loop levels.
- Indefinite loop with bond cap > hard fail (Day 38, from L6 design):
  Cassie can take as long as she wants; she just looks dumber the
  longer it takes. Bonds escalate to a cap, then stay there. No
  game-over screen needed. Aligns mechanic with character tone
  (Cassie is unruffled, not panicked). Consider before defaulting to
  hard-fail states in future levels.
- NPC reactions track observable actions, not omniscience (Day 38,
  from L6 design): L6's pen-only-removed-if-picked-up rule. The guard
  reacts to what he sees Cassie do, not what she could have done.
  Rewards player observation and creates strategic depth (opening
  drawer = safe; picking up pen = committing). Generalize: NPC
  responses should be driven by player-action signals the player can
  themselves reason about.

  ## When to reach for Jostleable vs raw physics

Jostleable is a CUMULATIVE-bump model: bumps accumulate, threshold fires
a discrete event. Right tool when the thing being "jostled" genuinely
loosens incrementally (L3 drawer runners shaking free) — there's a
diegetic story for why bumps add up over time.

WRONG tool for objects whose "falling" is per-bump and emergent (a lamp
sitting on a surface). Those want non-kinematic Rigidbodies and stacked
physics propagation. Per-bump chance to topple is whatever the physics
calculates — emphatically not a Random.value < N check.

Heuristic: if the answer to "why don't earlier bumps just do it?" is
"because they were below threshold," Jostleable fits. If the answer is
"because they hit at the wrong angle / didn't tip it past its base /
it landed back upright," raw physics fits.

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
- ## L7+ kick model gap (caught Day 42)

PlayerController.Kick currently has two pathways:
  1. Nearest Kickable in range → Kickable.OnKick → cumulative force →
     narrative payoff (van door opens). Position-gated, target-aware.
  2. No Kickable in range → SFX + animation only, no impulse spawned.

The missing third pathway: a physics-impulse zone that runs PARALLEL
to (1), affecting any Rigidbody in kick reach regardless of whether it's
a Kickable. This is what L6's nightstand-kick interaction needs —
mermaid-kick should jostle the nightstand emergently, with impulse
scaled by GetKickModifier (0.4 for mermaid-kick, 1.0 for free legs).

Likely implementation: forward-projected sphere or capsule cast on
PlayerController.Kick, find all Rigidbodies, apply impulse at hit point
scaled by modifier. Kickable system unchanged — it layers narrative
payoff on top of the physical baseline.

Tuning needs a kickable test scaffold (already on spec §10).

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

### Tier 1 — Must ship before Playtest #2 ✅ (all shipped by Day 37)

All three composite blockers landed. Playtest #2 is not poisoned data.
Historical record preserved below for retrospective use.

- ✅ Barehand struggle does nothing. ChairRestraint, FloorRestraint,
  CuffedRestraint: Struggle without a held tool produces zero bond
  progress. Effort SFX/animation still plays for "you tried" feedback
  (same pattern as L4 prone-kick suppression). Keystone fix; the rest
  of the cascade resolves from here. Note: this is a real shift from
  the design-notes framing of "Struggle is the universal verb" — Pick
  Up effectively becomes the gating verb, with Struggle as the closing
  verb. GDD updated to reflect. Revisiting universal Struggle as a
  meaningful verb in its own right is a v1 question (see thumbstick
  prototype note below).
- ✅ MutterTrigger component. Collider-based, fires
  MutterSystem.Play() on enter, configurable fire-once vs repeat.
  Infrastructure for the L1 + L4 teaching chains.
- ✅ L1 teaching mutter chain. Three beats:
    - Re-tuned entry mutter: "Great. Tied to a chair... These goons
      really know how to tie a knot. I can't get out without something
      sharp..."
    - After 5 failed barehand struggles: "I'm tied too tight. Think,
      Cass..."
    - MutterTrigger near nail: "That nail..."
- ✅ L4 teaching mutter additions. Existing entry mutter retained; added
  proximity + failed-kick beats.

### Tier 2 — Polish, log for after Playtest #2

These are real findings but not blocking. L3 auto-resolved exactly as
predicted — useful evidence for the "don't pre-tune" instinct. The
remaining three are still outstanding as of Day 37.

- L4 door visual: add line/seam so it reads as a door not a wall.
  Iterating on yesterday's wall-distinction work. **Still outstanding
  (Day 37).**
- Pause menu with controls reference. Molli's debrief suggestion.
  The right long-term home for "what can I do" reference, but only
  worth building once mutter-as-teaching is validated. **Still
  outstanding (Day 37);** mutter-as-teaching is validated post-Tier-1
  ship, so this is now unblocked but unprioritized.
- ControlHintsPanel ultrawide positioning. Anchor verb cards to
  respect ultrawide aspect ratios. Matters less if panel becomes
  non-load-bearing post-mutter-chain, but still real. **Still
  outstanding (Day 37).**
- ✅ L3 bond strength tuning (Day 37, auto-resolved). Molli's no-tool
  L3 exit is no longer possible — Tier 1 #1 (barehand struggle does
  nothing) removed the failure mode entirely without any L3-specific
  tuning. Evidence for the "wait and see what's left after Tier 1"
  discipline.
- L1 cutter placement. **✅ Shipped (Day 37, removed from L1).** The
  decorative cutter that enabled Retro's "stand on it and struggle"
  misread was removed; the nail is now L1's single solve.

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
