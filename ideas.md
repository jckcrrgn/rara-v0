# Rara v0 — Ideas & Notes

Scratch pad for ideas, observations, and things to try later.

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


## Feedback Patterns
- Twist shake (Day 12): Rejection feedback reads best as slow windup + snap-past-origin + settle, rotation rather than position. Pattern is reusable for other "wrong tool / wrong action" moments.


## Bugs-That-Are-Features
- Chair tipping felt great (Day 2): When the cube fell over during early testing, hopping stopped working and it genuinely felt like a tied-up detective whose chair had tipped. Accidental but authentic. Could be a real mechanic — maybe struggling increases tip risk, or certain collisions tip you. Would transition player to floor restraint. Revisit when floor movement is built.
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