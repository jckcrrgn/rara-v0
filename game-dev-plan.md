# Rara — Plan
**Status Rara Day 228 / 2026-11-23:** free demo live on itch since Sat Nov 21 (donations).
**Active milestone: post-demo plan, decided at the Sat Nov 28 review.** Deload week
Nov 22–28: light dev, no new pillars.
*Refreshed Day 228. The Day 205 demo plan is collapsed to a record below.*

---

## The project (locked)

Twelve levels, three acts:

| Act | Levels | Register |
|-----|--------|----------|
| Act 1 | L1–5   | Noir |
| Act 2 | L6–10  | Thriller |
| Act 3 | L11–12 | Showpiece + Finale |

**Cassie:** auburn-red hair in a high ponytail, slightly loose and tousled, with
face-framing strands and a small gold hair tie. Cream/ivory ribbed cropped
long-sleeve with a high mock/funnel neck. Ultra high-waisted brown/camel pleated
trousers, waistband above the navel, narrow midriff strip. Small gold hoops.
Sardonic detective. Escapes on her own competence, every time, with no rescuer.

**The governing test, everywhere:** *capable, not helpless.* Peril is the cover
promise; competence is the payoff. Both, simultaneously, never one at the expense
of the other.

**What it takes:** years, not weeks. Ship slices, build an audience on each, fund the
rest. Revenue is a lagging indicator; an audience with expectations is the thing.

---

## Next — post-demo (decide Nov 28)

**Launch read (Day 227):** views flat at 3–4/day, 3 downloads (mostly retests),
2 Discord joins. Nobody has been pointed at the page yet. Jack's own read: not fun
yet, near-graybox. The struggle-out is the least fun part (monotonous); the fun was
baiting the guard into tying her tighter. Notes: `Design/day227-where-the-fun-is.md`.

**Candidate next slice:** A (perfect pulls + streak; calibrate the beat offset first)
+ B (cinch duel: she tenses on his tugs, baits him in, comes out looser; she's conning
him) in the L6 room, one branch each, then a cold tester. Tests "is the guard the fun"
before any new pillar. C (catch re-tie duel, surrender, readable guard) comes later.

**Nov 28 decides:**
1. The next slice (A + B above, vs the knit-level pivot / the L6 call).
2. The monetization move: membership off the demo audience, vs a store page +
   wishlists for the next slice.
3. A date for that slice's first public build.

**This week (deload):** re-measure the beat offset (clean day only; gates A's
±25 ms perfect window), line up 1–2 trusted async testers, crash fixes only.

---

## Platform

**itch is the address.** `jckcrrgn.itch.io/cassie-conroy`. Donations only.

> **Superseded Day 205: Patreon primary, the tiers, "Patreon page live", "read
> Patreon's guidelines."** Patreon is deferred, not dead. If it comes back, the
> Day 117 reasoning (quiet invisibility, not a ban, is the risk; lead the page hero
> with competence, not peril) still holds — see git history for the full text.

- [x] SubscribeStar handle claimed.
- [x] itch page live (Day 125 / Sep 19).
- [x] itch tax interview. Payouts unblocked.
- [x] Owned channel: Discord, linked from the end card.
- [x] Free demo live (Nov 21).
- [ ] Export the Discord member list monthly.

---

## Audience

**Launched to an empty room (Nov 21), on purpose for now.** Wide attention waits until
it's fun (Day 227). Until then: 1–2 trusted async testers (screen-record while thinking
out loud; never mention the table edge), not a crowd.

**Rule, once there's something to show:** two posts a week, ~15 minutes each, outside
the dev hour. If it eats dev time, it's being done wrong. Backlog to post from: the
wrist-drift story, the bone-roll invariant, blockout → refine, cel shader tests, the act
structure, why she never gets rescued, the half-beat lock (players pull on her fastest
motion, not her pose).

Public copy never says "one hour a day."

---

## Habit rules (carried forward — these worked)

- Same hour daily, 1:30 PM. Close everything else. One focused hour is the target.
- **Minimum viable session = 20 minutes.** Rough day, do 20 and stop. Don't skip.
- No coding energy → do design work. The habit is showing up.
- End every session with the devlog row. 30 seconds now saves 10 minutes tomorrow.
- **Feature creep goes to `ideas.md`, never into the build.**
- One headline mechanic per level plus its payoff. That's the budget.
- Design decisions before code. Read the actual files before proposing fixes.

---

## Invariants — do not violate under deadline pressure

- **Bone rolls are a hard project invariant.** Changing one silently invalidates
  every authored strike Euler.
- **The six authored strike Eulers + `contactAt` live in the scene, not the script.**
  Never press Revert All. Never `git checkout` the scene to "clean up."
- **Refine the blockout mesh in place.** No rebuild, no retopo, no new file.
- Full values and reasoning: `cassie-modeling-brief.md` and the dated comment
  block in `CassieStrikeDriver.cs`.

---

## Known risks

**Not fun yet.** The demo is a working loop, not a fun one (Jack, Day 227). Fix the
loop before adding pillars: free state, patrol and chase are each multi-week.

**Empty room.** The page has had no push. Fine while the loop isn't fun; not fine
forever. The Nov 28 monetization call has to include how people find it.

**Right wrist / forearm strain.** Typed field values over gizmo drags, left-hand
mouse, hotkeys, 30-minute movement breaks. Red flags (numbness, tingling, weakness,
grip failing) mean stop and get it looked at.

**A bad week.** Assume one.

---

## Record — Free demo (Day 205 → Nov 21)

Promised mid-October; the Oct 17 date slipped to Oct 24, then was pulled Oct 20 ("not
fun", the old TableEdge → bottle loop). The rhythm-struggle loop replaced it (tested
fun Oct 21, merged Day 202). **Shipped Sat Nov 21** as a free Windows build in
`VS_Demo.unity`: RT-start beat, guard watch states (suspicion → bond check → light
catch or cinch; seen free hands = heavy), table edge as the shortcut (~44 s barehand
vs ~10 s), scissors snip, Discord end card. The Nov 13 freeze was bent on purpose for
the table-edge fix; no pillars crept in. Cold test: Molli ×2 on Nov 14, both before
the edge fix; no stranger yet. Full dates, budget and fix list are in git history
(this file before Day 228).

**Lesson (Day 166):** shoot scenes switch off gameplay to get clean frames.
**Diff active states against the last playable scene before building from a shoot scene.**

---

## Record — Slice 1 launch (Day 117 → Sep 19)

The original plan scoped a small complete game shipped to itch in 13 weeks; it died
when Rara turned out to be a 12-level, multi-year project. The Day 117 plan replaced
it: refine Cassie in place (Sep 1 gate met — exported Day 138), dress and light L6,
shoot a seven-shot hook clip, launch Sep 19 on Patreon. Budget ran over; pre-committed
cuts were taken in order. **Launched Sep 19 as an itch page with donations; Patreon was
not opened.** Full schedule, budget, cut order and shot list are in git history
(this file before Day 205) and `cassie-hook-clip.md`.
