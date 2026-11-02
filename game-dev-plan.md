# Rara — Plan
**Status Rara Day 205 / 2026-10-31:** itch page live since Sep 19 (donations, no Patreon).
**Active milestone: free playable demo on itch, Sat Nov 21, 2026 — bound loop only.**
*Rewritten Day 205 weekly review. Replaces the Day 117 Slice 1 Launch Plan; the launch
record is collapsed below.*

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

## Demo — bound loop (Nov 21)

**Date: Sat Nov 21, 2026.** Public, free, on itch. The demo is the discovery asset.

The itch page promised a mid-October demo. The Oct 17 date moved to Oct 24, and on
Oct 20 it was pulled ("not fun" — the old TableEdge → bottle loop). The rhythm struggle
loop that replaced it tested fun (Oct 21) and is merged in VS_Demo (Day 202).

### Scope

**In:** the Day 202 BondStruggle loop in `VS_Demo.unity`. Struggle on the beat (body
cue = Cassie's YankOnBeat sway) → free the wrists → untie with hands hidden from him →
slip out, *or* take the bottle and strike. Caught with free hands → reset to start,
elbows tied on, tighter. Third catch = taken.

**Out (post-demo):** free walk-around state, patrol, chase, drag-back, the escalation
ladder, L1–L5 design pass, L7+, menus beyond an end card.

### Dates

| Date | Gate |
|---|---|
| This week | itch devlog posting the new date. itch tax interview done. |
| **Fri Nov 13** | **Feature freeze.** Nothing new after this; only fixes. |
| **Sat Nov 14** | **Cold playtest** — someone who has never seen it. |
| **Sat Nov 21** | **Ship on itch.** |

### Budget (~10–13 sessions, Nov 1–20 holds ~20)

| Work | Sessions |
|---|---|
| Tune the revised rules (`offstageRange`, catch/reset feel) | 2–3 |
| Beat cue: `beatShape` 3 runs, judge offset only if still ~−100 ms; sway bias toward selected bond | 2 |
| Fix list below | 3–4 |
| Cold-test fixes | 2–3 |
| Build + page update | 1 |

### Fix list (re-triaged Day 205)
- [x] Hit / miss SFX clips assigned (`chairCreak1` = hit, `slide-thump` = miss); `noisePerMiss`
      0.15 → 0.08. **Day 206.** Feel check pending a clean day — GDD *Noise thresholds*.
- [x] HUD off in the player build (debug channel only — see GDD Feedback channels). **Day 206.**
      Release builds never show it; F1 toggles it in editor / Development. Controls card stays.
- [ ] Guard facing — GuardController moves him, never turns him. Fixed 15° yaw from
      Guard_Pivot for the whole route.
- [ ] Guard lean — walks in and stays upright. Swap to Guard_D133_Lean on LeanIn,
      back on Leaving.
- [ ] Camera framing under the shoot lighting — judge from the Game view.
- [ ] End card — itch link + owned channel.
- [ ] Owned channel (Discord / email) — the end card needs it.
- [ ] Web vs Windows — WebGL cel-shader test if Web.
- [ ] Cold playtest (Nov 14).

**Scene:** `VS_Demo.unity`. **Build profile:** `Demo – Windows`, scene list = VS_Demo only.

### Kill rule
If the Nov 14 cold test says "not fun", fix the loop. **Do not add pillars.** Free
state, patrol and chase are each multi-week; a demo that waits on them has no date.

### Lesson (Day 166)
Shoot scenes switch off gameplay to get clean frames. VS_ShaderCheck had
GuardController, Bottle, TableEdge, Canvas, and the guard's pivot all inactive.
**Diff active states against the last playable scene before building from a shoot scene.**

---

## Platform

**itch is the address.** `jckcrrgn.itch.io/cassie-conroy`. Donations only.

> **Superseded Day 205: Patreon primary, the tiers, "Patreon page live", "read
> Patreon's guidelines."** Patreon is deferred, not dead. If it comes back, the
> Day 117 reasoning (quiet invisibility, not a ban, is the risk; lead the page hero
> with competence, not peril) still holds — see git history for the full text.

- [x] SubscribeStar handle claimed.
- [x] itch page live (Day 125 / Sep 19).
- [ ] **itch tax interview** — blocks payouts. Donations can't reach you until it's done.
- [ ] **Owned channel: Discord and/or email list.** The real insurance. The audience
      relationship lives somewhere no platform can revoke. Every post points to it.
- [ ] Export the follower/member list monthly once one exists.

---

## Audience

**The single largest risk is launching to an empty room.**

**Rule:** two posts a week, ~15 minutes each, outside the dev hour. If it eats dev
time, it's being done wrong.

**Status Day 205:** lapsed after ~2 Reddit posts. **Restart:** the new-date devlog this
week is post #1. Backlog to post from without writing fresh material: the wrist-drift
story, the bone-roll invariant, blockout → refine, cel shader tests, the act structure,
why she never gets rescued, and now — the half-beat lock (players pull on her fastest
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

**The demo waits on pillars.** Most likely failure now. The kill rule and the Nov 13
freeze are the mitigation.

**Silence reads as abandoned.** The page promised October. The new-date devlog fixes it;
skipping it makes the Nov 21 ship land on nobody.

**Right wrist / forearm strain.** Typed field values over gizmo drags, left-hand
mouse, hotkeys, 30-minute movement breaks. Red flags (numbness, tingling, weakness,
grip failing) mean stop and get it looked at.

**A bad week.** Assume one. The budget has ~7 sessions of slack; spend it on the cold
test, not on scope.

---

## Record — Slice 1 launch (Day 117 → Sep 19)

The original plan scoped a small complete game shipped to itch in 13 weeks; it died
when Rara turned out to be a 12-level, multi-year project. The Day 117 plan replaced
it: refine Cassie in place (Sep 1 gate met — exported Day 138), dress and light L6,
shoot a seven-shot hook clip, launch Sep 19 on Patreon. Budget ran over; pre-committed
cuts were taken in order. **Launched Sep 19 as an itch page with donations; Patreon was
not opened.** Full schedule, budget, cut order and shot list are in git history
(this file before Day 205) and `cassie-hook-clip.md`.
