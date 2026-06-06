# Rara — Vertical Slice Spec (v1)
### Working title: "The Turnaround"

> **Day 58 — initial draft.** Source of truth for the Patreon/SubscribeStar launch demo. Locked this session: (A) recurring **check-in rhythm**, (B) **strike** takedown (not kick). Smaller forks are spec'd to a buildable default and listed in §13 for confirmation.

---

## 1. Purpose & Scope

The VS is a standalone **5–10 minute demo** that compresses Rara's core fantasy into one self-contained arc. It is the launch artifact — the thing a prospective backer plays and immediately gets.

**The fantasy, in one line:** helplessness → secret agency → the tension of feigning → turning the tables.

**What it is NOT:** not a full level; the guard is a **scripted actor, not patrol AI**; leaner than L6 (no failure-loop chair management, no nightstand/lamp puzzle).

**Reuse (not built from scratch):**
- `ChairRestraint` — proven; the slice lives entirely in it.
- Struggle + tool-cut bond grammar — existing.
- `FailureLoopController`'s guard-return bones (offstage approach audio, fade, return sequence) — the **template** for the guard's check-in entrances. Feign is the inverse: instead of "fail to escape before he returns → he re-binds you," it's "stage yourself before he sees you → he doesn't realize."

---

## 2. The Arc

Start chair-bound and gagged → work the wrist bond between guard check-ins, dropping into a **Feign** pose each time he looks in → free the wrists (legs stay bound) → acquire and conceal a blunt object → on the final check-in, **lure** the guard into melee range → **strike** with the hidden object, KO him → finish freeing herself → exit triumphant.

---

## 3. New Mechanics Introduced

| Mechanic | Debut in VS? | Notes |
| :---- | :---- | :---- |
| **Feign** | ✅ the headline mechanic | New player state: re-stage as bound (hands behind back, stop struggling, re-gag, hide held object). Suppresses real verbs while held. Sampled by the guard at inspection. |
| **Guard (scripted actor)** | ✅ | Deterministic state sequence keyed to the check-in clock + player lure. ~200–300 line behavior script. NOT patrol AI. |
| **StrikeableGuard** | ✅ | Receives the concealed-object strike → stagger → down. Sibling to Kickable. (Revises the old "KickableGuard" framing — the payoff is a strike, not a kick.) |
| **Performative lure ("Beg")** | ⚠️ open (§13) | Interactive verb used while feigning to draw the guard into lean-in range. Degrades gracefully to scripted if cut. |
| **Gag-as-feign-state** | ✅ minor | Removable/replaceable gag; a component of the helpless pose, not its own system. |

---

## 4. Restraint & Escape State

- **Start:** `ChairRestraint`, `BoundLimbs = Wrists | Ankles | AnkledToChair`, gagged.
- **Escape target:** clear **Wrists** only — Struggle + a reachable tool (existing grammar). **Legs stay bound** for the whole secret-escape phase.
- **Gag:** feign-state component; part of the "look helpless" pose.

**Why partial freedom is load-bearing (not arbitrary):** if she fully frees herself there is no reason to confront the guard, and the chair-bound strike can't happen. Wrists-free / legs-bound is the entire engine of the trap-the-trapper beat — she has secret agency but is still anchored, so the confrontation is forced.

---

## 5. The Check-In Rhythm (core loop)

The guard cycles on a clock. Reuses the L6 soft-timer + approach-audio bones, but the **outcome branches on feign-state** instead of always failing.

Per cycle:
1. **Offstage** — she works freely (struggle, reach, pick up).
2. **Approach telegraph** — offstage footsteps begin (the guard-return audio). The **feign window** opens.
3. **Feign window** — player must enter the Feign pose before the guard reaches the door sightline.
4. **Inspection (AtDoor)** — guard samples her state. Feigning → pass; not feigning → **caught** (§10).
5. **Gloat beat** — a short line, then he leaves.
6. **Reset** — clock restarts; she resumes.

**Progress is independent of the clock** — she advances the escape whenever she isn't feigning, so a check-in never interrupts unfairly; it just forces her to hide what she's done.

**Routine vs. climactic check-ins:** check-ins are routine inspections until prerequisites are met (**wrists free + weapon acquired + concealed**). Once armed-and-feigning, the **next** entrance is flagged climactic — the guard can now be lured close (§8).

| Check-in | She accomplishes (offstage before it) | At inspection |
| :---- | :---- | :---- |
| 1 | Still working the wrist bond | Feign → routine gloat → leaves |
| 2 | Wrists free; reaching for / hiding the object | Feign (now hiding object) → routine gloat → leaves |
| 3 (climactic) | Armed and concealed | Feign → **lure → lean-in → strike** (§8) |

> Count of routine check-ins (2 above) is a tuning value — enough to teach and stress Feign without padding.

---

## 6. Feign Mechanic Spec

- **State:** new flag on the player (`IsFeigning`), gating verbs and driving the bound-pose visual. Likely lives with the player/`ChairRestraint`, not a restraint swap — she's still in the chair, just *posed*.
- **Trigger:** player verb (key) during the approach window.
- **Effect while held:** snap to bound pose — hands behind back, struggle halted, gag replaced, any held object hidden. Real verbs (Struggle, Pick Up, Strike) suppressed.
- **Detection:** guard samples `IsFeigning` at the inspection moment only. Holding the pose through inspection = pass.
- **Release:** auto-exits when the guard leaves (or player toggles off once clear).
- **Visual:** placeholder pose on the cube (pose-swap + the held-object hide are the readable signals); reads literally on the character model.

---

## 7. The Guard (scripted actor)

A deterministic state machine, not AI:

`Offstage → Approaching → AtDoor (Inspecting) → Gloating → [LeanIn / Close] → Leaving → Downed`

- **Approaching** fires the telegraph audio; transition to **AtDoor** closes the feign window.
- **Inspecting** branches on `IsFeigning` (pass → Gloating → Leaving; fail → caught).
- **LeanIn / Close** only reachable on the climactic check-in, via the lure (§8). This is the strike window.
- **Downed** is terminal (KO).
- Carries the **gloat mutter set** (Guard speaker). Cassie's reaction lines queue behind, same pattern as L6 Beat 6.

---

## 8. The Turnaround (climax)

1. **Lure** — *[open: interactive vs scripted, §13]*. Spec'd interactive: while feigning at the climactic check-in, a **Beg/Plead** verb triggers the guard's `LeanIn` branch (he steps in to gloat in her face / loosen her gag). If cut, the guard auto-leans-in during the climactic gloat — the beat survives either way.
2. **Strike** — enabled while the guard is in `LeanIn` **and** unaware. Player swings the concealed object → `StrikeableGuard` stagger → `Downed`. The catharsis is the reveal: the hands he thought were tied come around swinging.
3. **Window** — *[open, §13]*. Spec'd as a window that lapses: wait too long and the guard straightens, finishes gloating, and leaves — back into the loop for another attempt. No hard QTE; the window exists naturally because he's only close during `LeanIn`.

---

## 9. Resolution & Exit

- Guard **Downed**. No more time pressure.
- **Free the legs** — clear `Ankles | AnkledToChair` via Struggle/tool (now unhurried).
- **Tie up the guard** — *[open, §13]*. Spec'd **CUT from v1**, documented as a post-slice victory button (needs a bound-guard visual + a bind interaction).
- **Exit** — **implied-exit fade**: freed + guard down → final mutter → fade = demo complete. No stand-up/walk verb is built (parked for L7 in `ideas.md`); the implied exit dodges building locomotion just for the slice and matches L6's window grammar.

---

## 10. Win / Fail Conditions

- **Win:** guard KO'd **and** Cassie freed → exit trigger → complete.
- **Fail:** caught at inspection (not feigning in time / wrong pose). *[harshness open, §13]* — spec'd as a re-cinch/escalate beat reusing the failure-loop pattern, returning her to a tighter bind rather than a hard restart.

---

## 11. Room

> Resolves the "new room vs. adapt L6 geometry" question flagged last session.

The arc needs only: a **chair** (center), a **reachable weapon surface** she back-scoots to, a **door + hallway** (guard source and sightline), and an **exit**. That's roughly L6's footprint minus the nightstand/lamp/Chair-B/failure-loop apparatus.

**Recommendation: purpose-built lean room that cannibalizes L6's reusable bits** — keep the door + hallway + guard-audio source and the center-chair setup; drop the nightstand, lamp, Chair-B, and the whole failure-loop chair-management layer. Cleaner than adapting L6 in place (no risk of dragging L6-specific wiring into the demo) and faster than greenfielding the room services.

---

## 12. Build Order (de-risked)

1. **Feign** — highest risk, novel. Prove the state + verb gating on the existing `ChairRestraint` first; everything else hangs off it.
2. **Guard + check-in rhythm** — reuse the `FailureLoopController` guard-return bones; branch the inspection outcome on `IsFeigning`.
3. **StrikeableGuard + turnaround** — lure → lean-in → strike → down.
4. **Room** — purpose-built lean (§11).
5. **Mutters** — gloat + Cassie reactions; content, comes last.

Cut-candidates if scope tightens: the interactive lure (→ scripted), tie-up the guard (→ post-slice).

---

## 13. Open Questions / Decisions to Confirm

- [ ] **Lure** — interactive Beg verb vs. scripted approach. *(spec'd: interactive-minimal, degrades to scripted)*
- [ ] **Weapon placement** — a back-scoot surface (reuses the L6 bound-hands-behind-back reach pattern) vs. already near the chair. *(spec'd: back-scoot surface)*
- [ ] **Strike timing** — window-that-lapses vs. enabled-whenever-in-range. *(spec'd: window)*
- [ ] **Feign-fail harshness** — re-cinch/escalate vs. soft reset. *(spec'd: re-cinch)*
- [ ] **Room** — purpose-built lean vs. adapt L6. *(spec'd: purpose-built, cannibalize L6)*
- [ ] **Tie-up the guard** — confirm cut from v1?
- [ ] **Routine check-in count** — 2 before the climax? Tuning value.

---

## 14. Forward Hooks

- **Feign** generalizes to the deferred L6 **interruptible-untie** tension (guard returns mid-untie → Cassie feigns her hands are still bound until he leaves). The VS is where that mechanic is born.
- **StrikeableGuard** → future takedown/combat verbs and the L11/L12 villain confrontation.
- **Tie-up-the-guard** → victory-button stretch and future capture/turnabout mechanics.
