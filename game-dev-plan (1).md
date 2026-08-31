# Rara — Slice 1 Launch Plan
*Written Rara Day 117 / 2026-08-03. Replaces the original game-dev-plan.md.*

**Launch date: Saturday, September 19, 2026.**

---

## Why the old plan died

The original plan scoped a small complete game shipped to itch.io in 13 weeks,
with Patreon as an afterthought. Rara is not that. It's a 12-level, three-act
third-person noir puzzle-escape game, and it will take years, not weeks. Every
milestone in the old document was measuring the wrong thing.

What replaced it: **ship a vertical slice, launch on the strength of it, fund the
rest.** The slice is *The Turnaround*. The launch asset is a 48-second clip.

---

## What this is called

**Launch.** Not "monetization."

What changes on September 19 is that Rara stops being private and acquires an
audience with expectations. Revenue is a lagging indicator of that and not the
thing to optimize. If the page goes up and thirty people follow and nobody pays,
that is a successful launch.

---

## The project (locked)

Twelve levels, three acts:

| Act | Levels | Register |
|-----|--------|----------|
| Act 1 | L1–5   | Noir |
| Act 2 | L6–10  | Thriller |
| Act 3 | L11–12 | Showpiece + Finale |

L6 is the threshold into Act 2 and is the Slice 1 level. GDD §2 reconciliation is
complete — this is settled, stop reopening it.

**Cassie:** auburn-red hair in a high ponytail, slightly loose and tousled, with
face-framing strands and a small gold hair tie. Cream/ivory ribbed cropped
long-sleeve with a high mock/funnel neck. Ultra high-waisted brown/camel pleated
trousers, waistband above the navel, narrow midriff strip. Small gold hoops.
Sardonic detective. Escapes on her own competence, every time, with no rescuer.

**The governing test, everywhere:** *capable, not helpless.* Peril is the cover
promise; competence is the payoff. Both, simultaneously, never one at the expense
of the other.

---

## Scope decision: launch on the clip, not a demo

**A playable build is NOT in the September scope.**

A Patreon page needs a pitch, not a download. Day-one backers are backing a person
and a promise. The hook clip is the promise.

This removes from the September list: mechanics smoothing, failure-loop polish,
win-condition UI, menus, build packaging, playtest rounds. What stays is only what
the camera sees.

The playable demo becomes the **first post-launch milestone, mid-October** — which
was the honest Slice 1 date all along. Day-one backers get something imminent to
wait for rather than arriving after the interesting part.

### The seven shots (locked Day 143)

| # | Shot | Function |
|---|------|----------|
| 1 | **Knot** | Establish. Bound wrists, close. The cover promise. |
| 2 | **Poster** | The hero frame. Page banner comes from here. |
| 3 | **Stills** | The room. Noir texture, slat light, atmosphere. |
| 4 | **Check** | She clocks the moment. Setup for the strike. |
| 5 | **Strike** | Dramatic peak, mid-clip. Bottle, contact, shards. |
| 6 | **Back On It** | Back to the bond. Competence, not victory lap. |
| 7 | **Free** | Hands free. The clip ends here, always. |

The peak sits at 5 and the clip still ends on escape. That ordering is the
genre read: puzzle-escape, not action. A clip that ends on the strike is a
different game than the one being built.

Two dressing zones cover all seven: the **chair corner** (1, 2, 3, 6, 7) and
the **contact zone** (4, 5).

### In scope for September 19

- L6 dressed and lit, camera-side only
- Cassie refined — one character model, shippable
- The seven-shot hook clip, ~48 seconds, cut
- Bottle smash + SFX pass
- Cel shader validated on the real character
- Patreon page live

### Explicitly out of scope

- Playable build of any kind
- Any level other than L6
- Guard model refinement (blockout is fine at this distance)
- Anything behind the camera in L6
- Menus, UI polish, settings, save systems

---

## Budget

*Rate revised Day 126.* Observed rate is ~99 sessions across 126 days = **0.79
sessions/day**. Against 38 days remaining to launch that is **~30 sessions**, not
40. The table below floors at 30.

**The plan's own arithmetic no longer clears its own minimum.** Week 2 finishing on
day 2 of 7 is real recovered slack the rate figure doesn't capture, so this is
tight rather than broken — but the pre-committed cut order below has stopped being
insurance and become a live schedule. Bottle SFX is first.

| Work | Sessions |
|---|---|
| Cassie refine — **critical path** | 15–20 |
| L6 dressing + lighting | 8–10 |
| Hook clip shoot + cut | 4–5 |
| Bottle SFX + audio | 1 |
| Page build + copy | 2–3 |
| **Total** | **30–39** |

There is no meaningful slack. That is a known property of this plan, not an
oversight — see *Pre-committed cuts* below.

---

## Schedule

| Week | Dates | Work |
|------|-------|------|
| 1 | Aug 3–9 | Resolve forearm number. Begin Cassie refine: body, hands, clothing masses. |
| 2 | Aug 10–16 | ~~Cassie: face and hair.~~ **Done Day 125** — hair arc and face *geometry*. Face texture moves to week 3. |
| 3 | Aug 17–23 | Cassie: **head unwrap (seams, unwrap, layout) → face texture authoring.** Cel shader validation. Strike + struggle re-verify. **Test export early in the week, not at the gate.** |
| 4 | Aug 24–30 | L6 dressing. Set pieces, props, the bar. |
| 5 | Aug 31–Sep 6 | L6 lighting. Slat shadow, key/rim, poster frame composition. |
| 6 | Sep 7–13 | Shoot the seven shots. Cut to ~48s. Bottle SFX (one session). |
| 7 | Sep 14–19 | Page build, copy, hero image. **Launch Sat Sep 19.** |

### The one gate

**September 1: Cassie must be done and exported.** If she isn't, the launch date
moves. Everything downstream is photography, and there is nothing to photograph
without her. Do not push into week 4 hoping to catch up — you won't, and you'll
discover it on September 14 instead of September 1 when it's still cheap.

---

## Pre-committed cuts

Decided now, in advance, so a bad week doesn't turn into a scope argument in
week 6. If sessions are lost, cut **in this order**:

1. **Bottle SFX.** Scoped at one session. The clip works silent with
   title-card text.
2. **Stills, then Back On It.** A five-shot clip still lands: establish,
   poster, the beat before, the strike, hands free. **Poster, Strike, and
   Free are never cut, and Knot is the only thing that establishes bound.**
3. **Tiers down to two.** $3 and $8, nothing else.
4. **L6 dressing reduced to the chair corner and the contact zone.** Dress
   the corners of the room that no camera sees never, but especially not
   under pressure.

**What is never cut:** Cassie. She's the critical path and the only thing that
can't be faked, shot around, or added later.

---

## Platform

**Patreon primary.** Best discovery, and Rara is noir peril rather than adult
content — restraint as genre iconography, competence as the payoff.

The real risk is not a ban. It's quiet invisibility: adult-adjacent content gets
restricted in search and recommendations, nothing visibly breaks, and you simply
never get found. Plan against that, not against a dramatic takedown.

### Set up on day one, not after a problem

- [x] ~~**Claim the SubscribeStar name now.**~~ **Done — handle claimed.** Free, five minutes, no obligation.
      A backup created after a suspension is not a backup — you lose the ability
      to tell anyone where you went.
- [ ] **Owned channel: Discord and/or email list.** The actual insurance. The
      audience relationship must live somewhere no platform can revoke. Every post
      points to it.
- [x] ~~**itch.io page as the permanent address.**~~ **Done Day 125 — `jckcrrgn.itch.io/cassie-conroy`.** Neutral, free, links out to
      whichever platform is current. This goes in every bio and every devlog so
      the links survive a move.
- [ ] **Export the member list monthly** while the account is in good standing.
      The difference between migrating an audience and starting over.

### Before building the page

- [ ] Read Patreon's community guidelines directly.
- [ ] Find 3–4 noir/thriller/peril games with comparable imagery and check where
      they actually live and whether they're visible in search. Twenty minutes,
      and worth more than any amount of guideline interpretation.

### Page hero

**Lead with competence, not peril.** The strike, the aftermath, Cassie upright with
the bottle. Shot 2 is a poster frame *inside a 48-second clip that ends on hands
free* — as a context-free static banner seen by a classifier or a moderator, it
reads as something else entirely.

This is not caving to a content policy. It is *capable, not helpless* applied to
the shop window.

### Tiers (draft)

- **$3** — Devlogs, work-in-progress stills, the backlog.
- **$8** — Early builds as they exist, plus vote on non-critical-path calls.
- **$15** — Name in credits.

Keep rewards deliverable by one person with one hour a day. Nothing physical.

---

## Audience (starts this week, not September)

**The single largest risk in this plan is launching to an empty room.** Forty days
of build with zero days of audience work produces a perfect clip that nobody sees.

The material already exists: ~100 sessions of devlog, a locked character design, a
poster frame, and a good story — solo dev, one hour a day, noir escape game, built
in public. Nothing new needs writing. It needs repackaging.

**Two posts a week, ~15 minutes each, outside the dev hour.** This is not a second
project and it does not touch the protected hour. If it starts eating dev time,
it's being done wrong.

Backlog is deep enough to post through launch without ever writing fresh material:
the wrist-drift debugging story, the bone-roll invariant, the blockout-to-refine
transition, cel shader tests, the act structure, why she never gets rescued.

---

## Habit rules (carried forward — these worked)

- Same hour daily, 1:30 PM. Close everything else.
- **Minimum viable session = 20 minutes.** Rough day, do 20 and stop. Don't skip.
- No coding energy → do design work. The habit is showing up.
- End every session with the devlog row. 30 seconds now saves 10 minutes tomorrow.
- **Feature creep goes to `ideas.md`, never into the build.**
- One headline mechanic per level plus its payoff. That's the budget.
- Design decisions before code. Read the actual files before proposing fixes.

---

## Invariants — do not violate under deadline pressure

Deadline pressure is exactly when these get broken.

- **Bone rolls are a hard project invariant.** Changing one silently invalidates
  every authored strike Euler.
- **The six authored strike Eulers live in the scene, not the script.** Never
  press Revert All. Never `git checkout` the scene to "clean up."
- **Refine the blockout mesh in place.** No rebuild, no retopo, no new file.
- Full values and reasoning: `cassie-modeling-brief.md` and the dated comment
  block in `CassieStrikeDriver.cs`.

---

## Known risks

**Cassie overruns.** Most likely failure. Mitigation is the stop conditions in the
modeling brief and the September 1 gate. The prior Cassie stall was perfectionism
at exactly this stage.

**Right wrist / forearm strain.** Fifteen-plus consecutive Blender sessions on an
active strain is a genuine schedule risk, not just a health one — a flare costs
more sessions than pacing does. Typed field values over gizmo drags, left-hand
mouse, hotkeys, 30-minute movement breaks. Red flags (numbness, tingling, weakness,
grip failing) mean stop and get it looked at, not push to the gate.

**Empty room at launch.** Addressed by starting audience work in week 1. The cost
of starting late is unrecoverable — there's no way to build a following in the
final week.

**A bad week.** The plan has no slack, so assume one happens. That's what the
pre-committed cut order is for. Take the cuts in order; don't renegotiate scope
under pressure.

---

## After launch

- **Mid-October:** playable Slice 1 demo to backers. First milestone delivered.
- Weekly devlog cadence continues.
- Next scope decision — L7, or polish L6 further — is deferred until after launch.
  Don't plan it now.
