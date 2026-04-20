# Rara v0 — Ideas & Notes

Scratch pad for ideas, observations, and things to try later.

## Mechanics
- Struggle as universal verb (Day 5): Struggle always works against bonds, just at different rates. Pick Up modifies struggle effectiveness via tools (nails, box cutters, etc.). Late-game difficulty comes from stronger bonds requiring stronger tools, plus timers preventing slow bare-hands escape. This is the core mechanic identity.
- Settings -> Keybinds
- Diegetic struggle feedback (Day 7): Bond progress should be communicated by the bonds themselves visually degrading — tight rope/zip-tie → frayed → loose → falls away. No HUD bars, no numbers. Immersion is the aim. Currently approximated with a worldspace bond meter above the player as scaffolding; delete and replace once the character model + bond geometry exist. The meter is temporary by design — do not polish.

## Feedback Patterns
- Twist shake (Day 12): Rejection feedback reads best as slow windup + snap-past-origin + settle, rotation rather than position. Pattern is reusable for other "wrong tool / wrong action" moments.


## Bugs-That-Are-Features
- Chair tipping felt great (Day 2): When the cube fell over during early testing, hopping stopped working and it genuinely felt like a tied-up detective whose chair had tipped. Accidental but authentic. Could be a real mechanic — maybe struggling increases tip risk, or certain collisions tip you. Would transition player to floor restraint. Revisit when floor movement is built.
- Box cutter lands on Player's head (Day 10): During L2 shelf-bump tuning, the cutter fell directly onto the player cube's head and sat there. Felt authentic to the detective's whole vibe — long-suffering, things land on them. Could be a deliberate bit for L2: shelf bump always puts the cutter on/near the player, not on the floor. Revisit when character model replaces cube.
- Cutter mass tuning (Day 12): Box-cutter-on-head bit only works if mass is low (~0.1) and shelf fall impulse is tuned. At default mass the cutter crippled hop and killed L2. Emergent charm still needs a tuning pass to stay fun.


## Session Notes
- Cut Call Out from v0 (Day 6): Originally planned as the 4th verb, but in a single-room escape game with no stealth/dialogue/guard AI, it had no real job. Reserved for a potential larger sequel where stealth sections + guard personality dialogue (Charm/Intimidate/Beg) would justify it. For v0, three verbs (Struggle, Move, Pick Up) keeps the design tight.