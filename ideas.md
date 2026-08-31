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

## Art / Rendering
- Cel outline — floating outline on hard/thin geometry (Day 63): inverted-hull
  outline pass splits at divergent normals (box corners, chair tubes), so the
  shell floats past the surface. Expected artifact of the technique, not a bug.
  Cheap knob: lower _OutlineWidth (0.015 → ~0.007) on shared mat 3ed0d346.
  Real fix: averaged/smoothed normals baked to a spare UV channel, read in the
  outline vert. Do on real Cassie/hero meshes — NOT placeholders. Accepted as-is
  on VS greybox beauty shot.

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

## Day 46 — L6 patient path shipped

### L3 vs L6 drawer fiction inconsistency

L3 establishes that bumping a desk hard enough opens its drawer
(cumulative Jostleable mechanic). L6's nightstand has a drawer that
does NOT open from kicks/bumps, despite kicks being capable of
toppling the lamp sitting on top of the same nightstand. The L6
drawer requires the new back-facing bound-hands verb (S to back-scoot
in, E to open).

This is a real fictional inconsistency: same furniture grammar,
different rules. The justification is design-internal (loud path vs.
patient path must be genuinely different verbs), but a playtester
paying attention might catch it. Acceptable for v0; flag if Playtest
2 catches it. Potential reconciliations if needed:

- L6 nightstand drawer has a "lock or stuck mechanism" that bumping
  can't defeat (fictional add).
- L3 drawer also requires back-facing interaction (would invalidate
  L3's existing solve flow — bigger change).
- Accept the inconsistency as a design grammar shift between Act 1
  and Act 2 (Act 2 introduces bound-hands as a real positioning verb).

Lean: third option. Park.

### Floor-bound back-up verb (parked)

Day 46's back-scoot is ChairRestraint-only. The bound-hands verb
(`Drawer.requireBackFacing`) works from any state — the dot product
gate is purely about player facing — but floor-bound Cassie has no
way to position herself for a back-facing interaction yet. Use case:
future level where she stands up from FloorRestraint (L7 verb),
inches forward to a table, then needs to back up to position her
bound hands over a tool.

When implemented, this should mirror FloorRestraint.Inch's hold-key
cadence (the existing floor verb grammar is hold-W discrete cycles),
so it'll be hold-S with cycle-by-cycle backward movement. Smaller
magnitude than Inch — same reason ChairRestraint back-scoot is
smaller than ForwardHop. Probably a 30-45 min session of its own
once L7 design starts to pressure for it.

### InteractableBase.OnPickUp naming

The drawer-open verb routes through `InteractableBase.OnPickUp`,
which is semantically a stretch (opening a drawer isn't picking up).
Player-facing this is fine — E is the key, the verb just happens to
be context-sensitive — but the internal name will get confusing as
more non-Pickupable interactables are added. Rename candidate:
`OnInteract` or `OnPress`, with `Pickupable` overriding to do its
held-item handoff. Refactor when there's a third non-Pickupable
InteractableBase subclass; not yet.

### InteractableBase.InteractionRange now actually used

The per-instance `interactionRange` field on InteractableBase was
exposed but ignored — `FindNearestInteractable` gathered everything
inside PlayerController's global `interactionCheckRadius` and took
the nearest. As of Day 46, it filters per-instance: each
interactable's own `InteractionRange` gates whether it's a valid
candidate. L6 nightstand drawer tuned to 1.3 (pivot-to-pivot,
geometry has ~1u half-extents on X/Z so 1.3 = ~0.3 outside the
mesh). Defaults stay at 1.5 across the board, so existing levels
*should* be unaffected — but the change is a real shift in pickup
semantics, worth a regression sanity-check on L1–L5 next session.

## Day 48 — L6 failure loop shipped

### RestraintBase API consolidation (small cleanup, not blocking)

Session 1 promoted `SetBoundLimbs` from FloorRestraint to RestraintBase
so ChairRestraint and any future subclass inherit it; FloorRestraint's
duplicate was removed. Worth a sweep of other Add/Remove-pattern
methods that may have drifted between restraints — anything where a
method body is essentially "RemoveX then AddX" or other shape-matching
duplication is a candidate. Not urgent; promote-as-found is fine.

### §7 persistence — deferred items from failure-loop v2

Chair-B swap shipped in session 2. Still outstanding from spec §7:

- Lamp-state persistence: smashed lamp should NOT respawn on attempt
  restart. Currently the lamp object presumably gets re-instantiated
  with scene reset patterns (or doesn't — needs verification).
- Pen-state persistence: pen is gone if picked up before failure,
  persists in drawer if not. Implementation likely needs a small
  per-scene "PersistentSceneState" object that the failure loop
  consults rather than blanket-resetting.
- Chair-shard persistence verification under the Chair-B swap path:
  shards are scene-rooted per Day 47's spawn architecture, which
  should already handle this correctly — but worth a deliberate test
  with attempt 1 chair-tip → fail → attempt 2 chair-tip → fail to
  confirm both shard sets persist on the floor through the swap.

These probably ship together as one "spec §7 compliance" pass.
~45-60 min depending on whether the PersistentSceneState pattern
turns out to be heavyweight.

### Spec/GDD attempt matrix bond-ladder update

The spec §7 attempt matrix (and any GDD reference to it) still shows
the old bond escalation ladder: Wrists → Wrists+Elbows → Wrists+
Elbows+Ankles+Knees. Day 48 revised this to Wrists → +Ankles →
+Elbows → +Knees, putting the ELBOWS line on attempt 2→3 where elbows
are actually added. Update the matrix table and any prose references
on the next low-energy doc-pass session.

L6 latent: Chair-B swap (HandleChairManagement case 2) does not sync BoundLimbs floor->chair, though the chair->floor break does. Only bites if a level re-binds to a chair after floor escalation; L6 never does. Backlog.
L6 latent: FloorRestraint sets the Elbows flag but its modifier math ignores it, so failure-loop Elbows escalation has no mechanical bite on the floor (reads as "more rope" only). Decide floor Elbows numbers if/when it matters.

[VS / GuardController] Caught condition is currently "not feigning at inspection" regardless of player state. Should only trigger caught if Cassie has made meaningful progress (bond cut progress > 0, moved from spawn, or holding a tool). Raw "not feigning" = caught is too punishing for a player who hasn't done anything yet. Design question: what's the right threshold?

## VS Playtest Notes — Day 62 (resolved)

### ✅ [POLISH] Guard close-in speed — RESOLVED (became guardMoveSpeed)
Original note: the lean-in step was duration-based, which read as a lunge.
Superseded by the Day 62 lure-cut rewrite (see below). The close-in is now
SPEED-based — `GuardController.guardMoveSpeed` (m/s, default 1.5) — so the walk
reads the same whether Cassie sits near the door or across the room. Duration-
based movement (`leanInStepDuration`) is gone for the variable-distance walks
(lean-in + leaving); only the door approach stays duration-based, because that
duration IS the feign window (a gameplay clock, not a movement to normalize).
Lesson: ask "is this a timer or a movement?" — normalize movements by speed,
leave gameplay clocks as durations.

### ✅ [DESIGN] SharpEdge back-facing gate — DONE (Day 62)
Resolved, but NOT via either option originally floated. The interaction verb for
SharpEdge is STRUGGLE, not Pick Up — so the gate lives in a new
`EnvironmentalTool.CanStruggleAgainst(player)` virtual (default true), which
SharpEdge overrides with the same back-facing dot product as
`Drawer.requireBackFacing` (`Dot(-forward, dirToEdge) >= threshold`, default 0.6).
`PlayerController.TryStruggle` gates the environmental-tool branch on it; gate
fail → tool contributes nothing → falls through to the existing struggle-fail
feedback (shake + SFX). Initial mistake: the gate was first written on
`SharpEdge.OnPickUp`, which never fires for an environmental tool — Struggle is
the verb, so OnPickUp was dead code. Lesson logged: match the gate to the actual
interaction verb, not the sibling component's verb (mirrored Drawer's *pattern*
without checking Drawer used a *different verb*).

### ✂️ [CUT] Lure verb card — CUT WITH THE LURE (Day 62)
The whole lure verb was cut (see below), so the planned verb-card prompt and the
lure SFX are both gone; the `LureHintPrompt` component built for it was discarded
before import. The legibility need it addressed transfers to STRIKE — the player
still needs to know H is live when armed and the guard is in close. Repurpose
target: a `StrikeHintPrompt`, show condition `wristsFree && heldItem.IsWeapon &&
guard in LeanIn`, drivable off the `GuardController.onLeanInEntered` hook +
`PlayerController.OnFeignChanged`. Not yet built — candidate for the room pass or
just after.

## VS — Lure Cut & Guard Auto-Approach (Day 62)

Cut the Lure verb (T) from the VS. It was the return of "Call Out," axed early in
dev — reintroducing it under a new name resurfaced the same problem without
solving it. In a one-guard scripted slice, a summon-the-guard verb is agency
theater: you press T, he comes, there's no tactical choice in it. It was also a
THIRD novel verb in a slice whose one-mechanic budget is Feign (headline) +
Strike (payoff).

New model: on a passed inspection the guard walks straight in to Cassie and
gloats in her face — EVERY check-in, unconditionally. It's his habit (the sadist
who can't resist getting close). On the unarmed early check-ins this is pure
threat: he leans in, taunts, leaves, she can't act. The turnaround happens on
whichever check-in she's finally armed — the same smug lean-in he's done every
time, except this time her hands come around swinging. The escalation lives in
HER state, not his; the constancy of his approach is the *engine* of the dramatic
irony, not a flattening of it.

Implementation: `GuardController` AtDoor(pass) → LeanIn directly. `GloatPhase`
removed; its mutter + check-in counting folded into `LeanInPhase`. Stripped:
`AttemptLure`, `CanBeLured`, `lureRequested`, the gloat polling loop,
`leanInGuardLine`, `gloatLingerDuration`, the `Gloating` enum value.
`PlayerController` stripped: `lureKey`, `lureSfx`/`lureSfxVolume`, `CanLureNow`,
`TryLure`, the lure input block. Strike gating UNCHANGED — it already keys on
`wristsFree && heldItem.IsWeapon && guard-in-LeanIn`, so the unarmed check-ins
self-gate (no weapon → logged no-op).

Mutter consequence: there is deliberately NO special climactic guard line. He
can't perceive that she's armed (same causality rule that killed the climactic
flag), so his close-gloat lines are identical every check-in. `routineGloatLines`
now play up close — author them as in-her-face taunts, not door-distance glances.

### Forward hook — Lure / Call Out belongs to the AI levels
The player-authored lure isn't dead, it's deferred. In the future patrol-AI
levels (see Stealth-between-escapes) a draw-the-guard verb has real tactical
meaning — pull a guard off a position, bait him away from a sightline. That's
where Call Out / Lure earns its place: against AI that can be meaningfully
misdirected, not a scripted actor on a fixed clock. Reintroduce there.

### Contextual-hint registry — generalize when a SECOND prompt appears
`ControlHintsUI` is restraint-driven (rebuilds from
`RestraintBase.GetControlHints()` on restraint/mode change). The strike prompt
(and the cut lure prompt) are state-driven — guard + feign state — which the
restraint can't and shouldn't know about. For now a dedicated component is the
right call: one instance doesn't justify the abstraction. But the L7
interruptible-untie feign (see Forward Hooks) will want its own state-driven
prompt. When that SECOND prompt appears, generalize: a contextual-hint registry
any system can push/pop hints into, with `ControlHintsUI` consuming both
restraint and contextual sources. Two instances justify it; one doesn't.

## Cassie Blockout — Deferred (Day 72)
~~Apply object scale (0.5249)~~ **STALE — see Day 126.** + reconcile guard unit scale (~9u vs her 3.96u) — rig/export time
REF_Guard linked collection, excluded from view layer — Link not Append, repeatable silhouette check
Refine pass: ribcage/bust mass, then re-judge torso; candidate crotch 3.75→3.65 only if still short after

- **Cone-biased shard burst.** ShardBurst scatters on a full sphere; a bottle
  broken on a head throws glass forward and down, not backward into Cassie's
  face. Bias the launch direction into a cone around the swing vector (driver
  already knows it — swingWithRightHand + torso yaw). Spherical is honest enough
  at 1.8 m/s; the cone is what sells the fiction. Pairs with the
  LampSmashTrigger → ShardBurst convergence item.

  - ~~**Cone-biased shard burst.**~~ SHIPPED Day 83. Directional Burst(pos, dir)
  overload with coneAngle / coneBias / coneLift / speedVariance; aim comes from
  BottleSmashOnContact's swingTarget + tangentSkew, not from the driver. Values
  still untuned.

  ## Day 83 — Shard burst deferrals

- **Burst point is one frame stale.** OnContact fires synchronously inside
  CassieStrikeDriver.Contribute, which runs before CassieRig writes the frame's
  bone poses, so smashOrigin.position reports last frame. ~8 cm at peak hand
  speed, about one scatterRadius. Real fix is an after-write event on CassieRig
  for presentation to hang off. Deferring the burst a frame trades a spatial
  error for a temporal one on the payoff beat — worse trade.
- **Measured-tangent aim.** swingTarget + tangentSkew is a stable approximation
  chosen because a measured hand velocity is noisy between runs and makes cone
  tuning unreadable. Once the values are locked, the true tangent is more
  correct. Wants the CassieRig after-write hook above.
- **Size-speed coupling on shards.** Small splinters should fly faster than big
  chunks — one line off the existing size draw. Cut from Day 83 as a refinement
  on a refinement; speedVariance covers the same legibility need.
- **Shards as escape tool.** Bottle shards are pure VFX and self-destruct at 8 s.
  Lamp shards are BladeTool pickups. Converging them is the LampSmashTrigger
  item; making bottle glass usable is a design question, not a refactor.
- **Bottle-neck stub.** Held remnant after the smash, instead of hiding the
  visual outright.
- **Security cam that tracks Cassie.** The fixed overhead framing is thematically
  right and a static reframe is worse. A camera that pans to follow her is
  better than either. Not a reframe — a behaviour.

  Scene Cassie has 11 bone position overrides pinning pre-retarget lengths
(LowerArm.L 0.0035 vs FBX 0.0025). FBX and brief are correct, scene is not.
Harmless while drivers write localRotation directly. Resolve when the real
Cassie model replaces the blockout, or before any humanoid clip work.

DiD: Detective in Distress — Day 123. Deliberate genre targeting, not accidental. Open question: whether the title needs to carry the signal when tags and communities do the discovery. Revisit before the page build, week 7.

## Day 126 — Cassie refine, file read

- **Finger-separation notches.** (Owed from Day 122 — the brief said this was
  deferred here and it never arrived.) Tip spread 0.0584; three notches gives
  0.0146-wide lobes, 0.87% of height, below the read at L6 distance. Two notches
  gives the wrong finger count. Order also wrong: on a curled fist the tip cap
  isn't a silhouette element, the dorsal knuckle mass is. Revisit only if the fist
  reads as a mitten in the real cel shader at poster distance — and if it does,
  the fix is knuckle definition, not notches.

- **Elbow and knee landmarks.** LowerArm.L and LowerLeg.L own zero vertices. The
  strike bends clean at debugScrub 0.8 and 1.0, no pinch, so this is not a
  deformation bug. It's a silhouette absence. Cost is ~8 verts against a budget
  that is already at 240/240. **Do not open until shot 5 is framed** — if the shot
  doesn't resolve an elbow, there's nothing to add.

- **Vert cap is a project decision, not a platform limit.** 240 is self-imposed and
  now fully spent, 43% of it in the head. If geometry is genuinely needed later,
  raising the cap is legitimate. Raising it *at the gate* is not. Decide in advance
  or not at all.

- **Ribcage/bust mass** — carried forward from Day 72, still unscheduled. Current
  read: probably already sufficient. Chest projects forward to y = −0.13727 at
  z = 1.224 and the seven-ring taper reads in front ortho. Under a ribbed mock-neck
  sweater in flat colour with a hard terminator, that may be the whole read. Treat
  as a decision to close, not a task to schedule.

- **Guard silhouette check is not repeatable.** Day 72 called for REF_Guard as a
  linked collection excluded from the view layer, Link not Append. That didn't
  happen — the guard is local objects and the Day 126 arrangement was unsaved when
  the file was read, which made a file-only read show him unassembled. Do the
  linked-collection version before the whole-figure exit condition, or the check
  has to be rebuilt by hand every time.

- **STALE — strike this Day 72 line:** `Apply object scale (0.5249)`. Superseded.
  The brief specifies 0.4330 and apply, and Day 117 verified scale is already
  (1,1,1) on both mesh and rig. The 0.5249 is from a dead scaling attempt.

## Day 130 — Cassie jawline pass (deferred)

Surfaced after the face texture read landed. Three profile corrections shipped
this session (nose projection, cheekbone, lip prognathism); this is the fourth
and it was too big for the remaining time.

- **The chin is square.** Bottom ring half-width is **0.0360** against a skull
  half-width of 0.0695 — the chin is **52% of head width** at its lower edge. The
  canon render reads closer to 30%. This is the largest remaining gap between the
  mesh and `refFrontWardrobe`, and the one Jack's eye keeps returning to
  ("square chin that would make Batman jealous").

- **Not a two-vert nudge — it's a shaped pass across three rings.** Each ring
  needs both its verts moved together or the chin cap goes lopsided. Narrowing
  the outer vert without bringing its inner partner along puts two verts ~4 mm
  apart and produces a spike instead of a taper.

  | z | verts | current x (outer / mid / inner) |
  |---|---|---|
  | 1.4849 | 63, 66, 238 | 0.0466 / 0.0435 / 0.0217 |
  | 1.4681 | 235, 239 | 0.0397 / 0.0199 |
  | 1.4512 | 60, 237 | 0.0360 / 0.0180 |

  Rough target shape: bottom ring outer to ~0.022 with its inner partner to
  ~0.011, 1.4681 outer to ~0.030, 1.4849 largely held so the jaw *angle* stays
  and only the chin cap narrows. Tune as a curve, not three independent numbers.

- **Estimate:** 20–30 minutes with a read after. High-energy session task, not a
  tail-end-of-hour task. Vert count unchanged, UVs unchanged, mirror handles the
  right side. No rig contact.

- **Stop condition before opening it:** front ortho + 3/4, flat shading, at
  poster distance, against the guard. Done when the chin reads tapered rather
  than slabbed. **Not** when it matches the render — see below.

- **The render will always look better resolved.** `refFrontWardrobe` governs
  *which* features Cassie has, not how big or how dense, and her head is larger
  relative to her body than the render's. A jawline that takes two painted
  strokes is six verts here. Judge against the guard, not against the painting.

### Width readings must be filtered by material

The first pass at this diagnosis measured a "widest point" of 0.0824 at
z = 1.5301 and concluded the mass sat too low on the face. **That vertex (176) is
material 0 — the hair shell, not the skull.** Filtering to material 1 gave the
real profile: a constant 0.0695 half-width from 14% to 67% of head height, i.e.
no cheekbone at all, which is a different defect with a different fix.

Same category error the brief keeps logging (the forearm ghost, the 0.170 hand
length, the 0.5249 scale). Adding it here because the trigger is new: **head
width readings include the hair shell unless you filter by material.**

## Day 131 — Neck/head junction (deferred)

Surfaced right after the jawline + chin-cap Y pass closed. Jack's read: "worried
about how her neck meets her head." Not opened — two geometry passes already ran
this session and the chin was the gate-critical one.

**The junction is interpenetration, not a seam.** Neck top ring sits at
z=1.4662; head bottom ring sits at z=1.4512. The neck column pokes 15mm *up
into* a flat head underside. There is no transition geometry — no submandibular
slope, no jaw-to-neck taper. The head bottom is a flat horizontal plane.

| ring | z | verts | mat | half-width | y span |
|---|---|---|---|---|---|
| neck top | 1.4662 | 53, 55, 57, 59 | 0 | 0.0339 | −0.0448 → +0.0231 |
| head bottom | 1.4512 | 60, 237, 94, 76, 89 | 1 | 0.0360 (v76 rear) | −0.1020 → +0.0558 |
| neck next | 1.4050 | 164–167 | 0 | 0.0339 | −0.0459 → +0.0242 |

**Today's pass probably sharpened this read.** Chin cap outer went 0.0360 →
0.0225. Neck half-width is 0.0339 — so the neck is now *wider than the chin*,
which it wasn't this morning. The junction didn't change; the thing above it
did. Expect the fix to be partly in the neck, not only at the seam.

Second contributor: chin front is y=−0.1020, neck front is y=−0.0448. A 57mm
overhang, against a head height of 0.2138. Some overhang is correct — that's
the jaw. Whether it's this much is the open question.

**Material trap, again:** the neck is material 0, the skull is material 1.
Any width or profile reading across this junction has to be filtered or it
mixes two objects. Same category error as the hair shell.

**Not yet scoped.** Decide first whether the hook clip ever frames it — if no
shot sees under the jaw, this is post-launch. Check against the seven shots
before spending a session on it.

## Day 134 — Body UV unwrap (fallout and constraints)

Body unwrapped. Three things banked out of it, each with the numbers needed to
reopen without re-diagnosing.

### Hand island self-overlaps

The 25-face hand island (mesh z 0.78–0.93) is cut only at the wrist ring, so the
branched blob — palm, curled finger section, thumb — pancakes onto itself.
`UV > Select > Overlap` catches it every time.

**Harmless as long as the hand stays one flat skin colour.** Nothing reads
wrong because every overlapping texel is the same colour. It only becomes a
defect if dorsal knuckle shading is ever painted.

**The fix, if it's ever needed:** ring seam at the thumb base, making the thumb
its own island. Not a lengthwise palm cut — the curl means the palm side is
never a silhouette element anyway (see the Day 122 notch reasoning in the
brief, same argument).

### Mirror U is ON — painted body art must be duplicated and flipped

Cassie's Mirror modifier is flag 75 = clipping + vertex groups + X axis +
**mirror U**. The mirrored half samples `1 − u`, not the same UVs.

So the base half's islands live in **u 0–0.5** and the right side of her body
reads from **u 0.5–1**, which is empty until it's painted. Workflow: paint the
left half, then duplicate the layer and flip horizontally in Photopea. Region
[a, b] maps exactly onto [1−b, 1−a].

This is why the body was packed and then squashed with `S X 0.5` rather than
filling the square. The head unwrap already followed this convention — face
islands sit at u[0.138, 0.500] — so it's the file's existing rule, not a new one.

Texels are 2:1 horizontally as a result. Invisible on flat colour, and a
horizontal hem line stays horizontal. Do not "fix" it.

**Why keep Mirror U on:** it's one modifier for the whole mesh, so it's
all-or-nothing, and the face wants asymmetric freckles. The face wins.

### Body and face are separate materials — separate textures

`Cassie_Body` and `Cassie_Face` are different material slots with different
images (`cassie_body_D.png`, `cassie_face_D.png`). The body does **not** need
to pack around the head islands and doesn't.

**If they're ever atlased into one material** — for the draw-call saving — the
body needs a full repack, because both currently occupy overlapping regions of
0–1 in the same UV layer. Cheap now, annoying later. Decide before painting
anything detailed.

### Material slot trap (cost: ~10 min this session)

The material-name field in the shader editor header **replaces the material in
the active slot**. It does not switch which slot you're editing — that's the
`Slot N` dropdown to its left.

Selecting `Cassie_Body` from that field while slot 3 was active overwrote
`Cassie_Hair`. It dropped to zero users, and zero-user datablocks are not
written on save. Recovered from the browse list before Blender restarted; had
it restarted, yesterday's `#B84921` would have been gone.

Face assignments survive this — the 28 crown/ponytail faces stayed on slot 3
throughout. Only the material datablock is at risk.

**Related:** `Ctrl+S` does not save image pixels. `Image > Save` is a separate
action, every time.

## Day 136 — FBX round trip (first since Day 116)

Throwaway export, 15 minutes, budgeted as a de-risk. It found two things and
both were worth the trip.

**All four checks passed.** Upright, correct axis (−Z Forward / Y Up), three
material slots survived, nothing shears in the arms.

### Textures do not travel through FBX

FBX carries a filepath string and material slots, not images. Both textures
are file-backed and unpacked (`//cassie_body_D.png`, `//cassie_face_D.png`,
`packedfile = 0`) and live next to the .blend, outside `Assets/`. Unity had
nothing to resolve, so it generated untextured materials. Nothing was lost —
the clothing block-in is intact in the PNGs.

The hair still came through auburn in the Project thumbnail. That is
`#B84921` arriving as a Principled base colour, which is the one material
property FBX carries without a file. Do not read it as "textures worked."

**Real-export material step (~15 min, do once):** copy both PNGs to
`Assets/Textures/`, build materials on `Rara/CelShaded` with each as Base
Map, assign to the three slots, then set the model importer's Materials to
None so Unity stops generating throwaway materials on every reimport.

### Scale: NOTHING NEEDS APPLYING

Round trip ran with no scale applied and put her at ~1.68 m against
`_Ruler_2m`, and ~0.11 m under the guard's crown — matching the Day 126
anti-guard delta. The chain already produces correct height end to end.

**STRIKE from `cassie-modeling-brief.md`, Export section:** *"Blender data
3.88 BU; scale 0.4330 and apply, then Unity Scale Factor 1.0."* Wrong number
— 0.4330 is the armature span, the mesh factor is 0.4245 (mesh height
3.9572 BU) — but more importantly the wrong *instruction*. Applying either
value on top of a chain that already lands at 1.68 shrinks her to ~0.71 m.

Same double-discount shape as the already-struck 0.5249 line: a correction
applied to something that was corrected upstream. Third occurrence. The
tell is a scale factor written down before the pipeline it describes exists.

### Anti-guard condition now confirmed in-engine

Front-on under the actual slat lighting, not only in Blender ortho. He reads
as a fridge, she reads as a figure. The whole-figure exit condition's
anti-guard half holds in the shipping renderer.

Palette (exact, read from cassie_body_D.png Day 136):
skin #E8B79A / camel #9B7346 / ivory #EDE3D1 / hair #B84921
Four colours, zero anti-aliasing. Keep it that way — hard edges are the
cel read.

Face texture: fill the FULL 1024 canvas with skin before painting.
cassie_body_D is 59% black between islands, which is invisible on the body
but would bleed a dark rim at the jaw and hairline on the face.

CASSIE SEATED BOUND POSE — Cassie_Blockout scene overrides
Captured Rara Day 137 from VS_ShaderCheck.unity (identical in VS_Turnaround.unity).
NOT in the FBX, NOT in the prefab asset. Scene-only. Destroyed by Revert All.
fileIDs are Cassie_Blockout model-asset IDs; D136 is a different asset, so
transfer is manual either way.

fileID                 Euler (x, y, z)                quat (x, y, z, w)
---------------------  -----------------------------  -----------------------------------
-8679921383154817045   0, 0, 0        [ROOT]          0, 0, 0, 1     pos y -0.838
-8790819200991850735   1.855, -4.231, 1.044           0.0158, -0.0371, 0.0097, 0.9991
-5729378154859122509   7.906, (y/z not overridden)    0.0689, 0, 0, 0.9976
 5545462827199411212   -5.613, -0.463, -1.369         -0.0489, -0.0046, -0.0121, 0.9987
-6131915425308919250   -0.047, -0.278, 48.052         -0.0014, -0.0021, 0.4071, 0.9134
-5933143353430679065   9.006, -12.148, 88.01          -0.0171, -0.1301, 0.6947, 0.7073
 2965579352698239797   -7.585, 12.464, -61.797        -0.112, 0.0592, -0.5032, 0.8548
 392872707865606862    -7.39, 2.52, -79.733           -0.0635, -0.0245, -0.6384, 0.7667
-1907169310371057590   63.229, -103.654, -103.742     0.7267, -0.1585, -0.1596, 0.6491
-719924342113578459    100.528, 32.265, 47.16         0.7481, -0.1327, 0.0498, 0.6483
 8259906762516723408   67.219, 137.824, 141.052       0.799, 0.0713, 0.1103, 0.5868
 8407238315529531089   110.027, 39.83, 68.735         0.7461, -0.2736, 0.074, 0.6025

## Day 140 — Forearm roll: thumbs wrong at rest

Found at debugScrub 0.8 once the thumb existed. Not a regression — roll
about the forearm axis was invisible on a thumbless hand, so it was never
constrained when the strike was authored. Same class as the lower-leg kink:
correct pose, wrong-looking geometry, only visible after a later refine.

**BOTH forearms, at rest (s = 0). Not a mirror fault** — a bad mirror breaks
one side. Thumbs face into the body; should face up. Systematic hand-to-bone
orientation, present since the blockout.

Twist axis on the forearm is **Y**, not X.

**Measured:** forearmCoilEuler Y = 174.23 puts thumbs right AT COIL, in play
mode only (not saved). That is a **184° delta** from the authored -10, but
the eyeball estimate was 90°. Those disagree by 2×. Read the real angle off
the Hand bone in Blender before committing to any rotation amount.

**Coil is not the fix.** s = 0 is the seated bound pose — Sit, Struggle base,
and the shot 2 poster frame. Patching coil fixes the one instant nobody
photographs. It also makes Y sweep 174.23 → -41.6 = 216° across a 0.24s
whip. Any correction composes WITH the authored -10, not instead of it.

**Three fix classes:**
1. *Rest pose (12 scene overrides).* Fixes all four layers at once. But
   composition is `rest * offset`, so rolling rest moves every authored
   strike pose, not just rolls it. Cost = fix + full strike re-author.
2. *Per-layer Euler patches.* Preserves everything verified. Fixes nothing
   at s = 0 unless Sit and Struggle carry their own offsets too. Three
   drivers. Poster frame is the hardest one.
3. *Roll the hand verts in Blender about the forearm axis.* Rig, bone rolls,
   six Eulers, 12 overrides all untouched. Correct in every pose free,
   because the hand rides the bone. **Likely cheapest correct fix.** Risk:
   the wrist ring already splits Hand 0.94/0.05 one edge, 0.525/0.471 the
   other (existing open item) — rolling introduces twist across that
   junction. Probably invisible at 248 verts at poster distance; check it.

**Gated on shot framing, not on geometry.** If hands read at ~40px in the
poster frame, option 3 is a 20-min edit and the wrist twist is free. If
shot 2 is close on bound wrists, different budget. Do not pick a fix
before L6 shots 2 and 5 are framed.

Any fix: re-verify wrist-to-wrist bound at 0.8 (validated Day 138 under the
old orientation) and post-strike at 2.0 separately.
