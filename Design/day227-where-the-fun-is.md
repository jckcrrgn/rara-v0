# Where the fun is — post-demo design notes (Rara Day 227, Sun Nov 22 2026)

*Input for the Nov 28 review (next-slice decision). Proposals, not decisions. Nothing here is built.*

## What Jack noticed (own play, after launch)

The dev knows every path, so this is n=1 and not cold. Still the clearest signal so far.

1. **Struggling out is the least fun part.** The rhythm struggle feels monotonous. Some of that is
   rote (he knows the routes), but not all of it.
2. **He mashed Struggle on purpose to summon the guard and play with him.** The guard reacts; the
   rope doesn't. Reacting things are what players play with.
3. **The fun was in baiting the guard to tie Cassie tighter**, not in escaping.
4. **The original prototype's fun:** watching the % match of each pull and chasing perfection.

**Read:** the guard is the toy. The struggle needs (a) a skill ceiling worth chasing and (b) a
reason to bring him in.

## Why the struggle is flat (from the code)

`BondStruggle.Pull`: `gain = gainPerPull × mult × accuracy ÷ tightness`, and a pull only counts at
`accuracy ≥ goodWindow` (0.6). So every scoring pull lands between 0.6 and 1.0. **A perfect pull
is 1.67× a barely-good one, on a straight line,** and nothing in the feedback marks it as a
different event. Perfect is just a slightly bigger number nobody sees.

## A. Perfect pulls + streak (do first)

- **Tiers.** Good 0.6–0.85 · Great 0.85–0.93 · **Perfect ≥ 0.93** (≈ ±25 ms at `beatInterval`
  0.7). Starting values; tune by feel.
- **Perfect is unmistakable:** ~2× gain over good (not 1.67× on a line), its own sound (rope
  creak/snap), the bond bar jumps, a small Cassie reaction (a breath, a smirk).
- **Streak:** consecutive perfects step the multiplier up (e.g. +0.25 per perfect, cap ×2). A miss
  or spam resets it. This is the "chase perfection" loop from the prototype.
- **Calibration comes before this.** Day 204 measured a steady ~−100 ms perceptual offset (on the old
  always-on cue; bout mode voided it). With a ±25 ms perfect window, an uncorrected −100 ms offset
  makes perfects nearly impossible. Re-measure on the bout cue, then add the judge offset if it's
  still there. **Clean day only** (timing data).
- **Subdivisions** (half-beat pulls to prove you've found the beat): later. They add input grammar
  before the base beat is fun. Revisit once perfects land.

## B. The cinch duel — "tense on his pull" (the core idea)

**Now:** a check that finds no evidence (`MostLoosened` < `checkEvidence` 0.5) calls
`Cinch(i, cinchSetback 0.15)`. A flat setback with no input.

**Proposal:** the cinch becomes a short duel.
- He re-ties the bond over **3 tugs at his own tempo**. Each tug is a visible yank, fastest at the
  peak (the `YankOnBeat` lesson: players lock onto the motion event).
- She **tenses on his tug** (same input as a pull). On-beat tense = slack banked; perfect tense =
  more. Miss or no input = he gets the full tug.
- **Outcome range:** no tensing = today's −0.15. All three good = roughly break-even. All three
  perfect = **net slack** (the bond ends looser than before he checked).
- A badly mistimed tense (a flinch) is a tell: "Quit squirming." Small suspicion, or one extra tug.

**Why it works:**
- **Baiting becomes a strategy.** Draw him in on purpose, let him re-tie, out-tense him, come out
  ahead. That's the thing Jack was already doing for fun, made into play.
- **The risk is built in.** He only cinches when the bonds *look* tight. Past 50% he catches you.
  So baiting means working a bond to ~40%, then calling him in. That's greed vs. evidence, a real
  decision.
- **It's the real trick.** Escape artists tense while being tied to bank slack. It fits the fiction,
  and it fits the governing test, *capable, not helpless*: she isn't being tied tighter, she's
  conning him.
- **It reuses what exists:** the guard check, `Cinch`, the beat grading, the yank cue.

**Open:**
- [ ] **Farming.** Bait → duel → bait forever? Options: slack per duel caps; his patience drops
      per visit (shorter linger, sooner suspicion); a second check in the same visit notices slack.
- [ ] Which input is "tense": RT (same as pull) or a hold? Same input is simpler; a hold reads more
      like bracing.
- [ ] Does he tug the most loosened bond (as now) or the one he's watching her work?

## C. Later — same idea, bigger

- **The catch re-tie as a big duel.** A catch re-ties her (and adds tightness). Make that a longer
  duel with higher stakes.
- **Surrender (Jack's "Rara v5" idea).** Cassie gives herself up; a calm prisoner gets tied
  looser, and she tenses for room. It's the cinch duel as a level opener, and a way deeper into
  the lair. Fits the con-artist framing.
- **Readable guard + beg / flatter / threaten.** Not a dialogue menu. He has a mood (bored, amused,
  suspicious), and each approach works on one mood and backfires on another, so the
  rock-paper-scissors is *read him, then pick*. Sketch: begging an amused guard makes him gloat and
  get careless; flattering a bored one makes him talk and look away (an untie window); threatening
  a suspicious one makes him cinch. Needs a mood system, so it's a slice of its own.

## For Nov 28

A candidate next slice that stays small: **A + B in the L6 room** (calibrate → perfect tier +
streak → cinch duel), one branch each, then a cold tester. It tests "is the guard the fun" before
committing to the knit-level pivot or C.
