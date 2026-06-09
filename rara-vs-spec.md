# Rara — Vertical Slice Spec (v1)
### Working title: "The Turnaround"

> **Day 58 — initial draft.** Source of truth for the Patreon/SubscribeStar launch demo. Locked this session: (A) recurring **check-in rhythm**, (B) **strike** takedown (not kick). Smaller forks are spec'd to a buildable default and listed in §13 for confirmation.
>
> **Day 62 — revision: Lure cut.** The interactive lure/"Beg" verb (former §8 step 1) is removed — it was "Call Out" returning under a new name, and in a one-guard scripted slice a summon-the-guard verb is agency theater (and a third novel verb against a Feign+Strike budget). New climax: the guard **walks in and gloats up close on every passed inspection, unconditionally**; the turnaround is simply whichever check-in Cassie is armed on. Escalation lives in her state, not his — his constant approach is the engine of the dramatic irony. Guard movement is now speed-based, not duration-based. Lure preserved as a forward hook for the AI levels (§14). Sections updated below: §2, §3, §5, §7, §8, §12, §13, §14.

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

Start chair-bound and gagged → work the wrist bond between guard check-ins, dropping into a **Feign** pose each time he looks in → free the wrists (legs stay bound) → acquire and conceal a blunt object → on the next check-in the guard leans in to gloat in her face (he does this every time) → **strike** with the hidden object, KO him → finish freeing herself → exit triumphant.

---

## 3. New Mechanics Introduced

| Mechanic | Debut in VS? | Notes |
| :---- | :---- | :---- |
| **Feign** | ✅ the headline mechanic | New player state: re-stage as bound (hands behind back, stop struggling, re-gag, hide held object). Suppresses real verbs while held. Sampled by the guard at inspection. |
| **Guard (scripted actor)** | ✅ | Deterministic state sequence keyed to the check-in clock. On a passed inspection he walks in and gloats up close every time. ~200–300 line behavior script. NOT patrol AI. |
| **StrikeableGuard** | ✅ | Receives the concealed-object strike → stagger → down. Sibling to Kickable. (Revises the old "KickableGuard" framing — the payoff is a strike, not a kick.) |
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
5. **Close-in gloat** — on a pass he walks in to her and taunts her up close (this is the strike window, §8). A short line, then he straightens and leaves.
6. **Reset** — clock restarts; she resumes.

**Progress is independent of the clock** — she advances the escape whenever she isn't feigning, so a check-in never interrupts unfairly; it just forces her to hide what she's done.

**Every check-in is mechanically identical.** The guard always walks in and gloats up close on a pass — there is no special "climactic" guard behavior and no player verb to summon him. What changes across check-ins is *her* state. While she's unarmed, the close-in is pure threat — he leans into her face, taunts, leaves, and she can't act. The turnaround is simply whichever check-in she's finally **wrists-free + armed + concealed** on: the same smug lean-in he's done every time, except now her hands come around swinging. The dramatic irony rides on his routine being constant while hers isn't.

| Check-in | She accomplishes (offstage before it) | At the close-in |
| :---- | :---- | :---- |
| 1 | Still working the wrist bond | Feign → he leans in, taunts → leaves (pure threat) |
| 2 | Wrists free; reaching for / hiding the object | Feign (now hiding object) → he leans in, taunts → leaves |
| 3 (the turn) | Armed and concealed | Feign → he leans in as always → **strike** (§8) |

> Which check-in becomes "the turn" depends only on when she's armed — not on any guard-side flag. The count here is illustrative; see §13 for the tuning value (how many threat beats before she can realistically be armed).

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

`Offstage → Approaching → AtDoor (Inspecting) → LeanIn (walks in, close gloat) → Leaving → Downed`

- **Approaching** fires the telegraph audio; transition to **AtDoor** closes the feign window.
- **Inspecting** branches on `IsFeigning` (pass → **LeanIn**; fail → caught).
- **LeanIn** is reached on **every** passed inspection — he walks in to her (speed-based movement) and gloats up close. This is the strike window. He holds it for `leanInDuration`, then straightens and leaves if no strike landed. No lure, no summon verb.
- **Downed** is terminal (KO), set by `StrikeableGuard` → `OnGuardDowned()` (stops all guard coroutines).
- Carries the **close-gloat mutter set** (Guard speaker). Cassie's reaction lines queue behind, same pattern as L6 Beat 6. The guard's lines are identical every check-in — he can't perceive that she's armed, so there is deliberately no special climactic line (same causality rule that killed the climactic flag).

---

## 8. The Turnaround (climax)

1. **Close-in (automatic).** On the passed inspection where she's armed — same as every other check-in — the guard walks in to gloat in her face. No verb triggers this; it's his habit. The catharsis is set up by the constancy: he's done this every time, and this time she's ready.
2. **Strike** — enabled while the guard is in `LeanIn` **and** she's armed (`wristsFree && heldItem.IsWeapon`). Player swings the concealed object → `StrikeableGuard` stagger → `Downed`. Strike validity is the `LeanIn` *state*, not the guard's physical distance — he asserts "I'm in your face" by entering LeanIn and we trust that. The reveal: the hands he thought were tied come around swinging. (Pressing H breaks the feign and swings in one press — the reveal *is* the strike.)
3. **Window** — the `LeanIn` hold (`leanInDuration`, plus the speed-based walk-in before it). Wait too long, or be unarmed, and the guard straightens, finishes gloating, and leaves — back into the loop for another attempt. No hard QTE; the window exists naturally because he's only close during `LeanIn`.

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
3. **StrikeableGuard + turnaround** — auto lean-in (every passed inspection) → strike → down.
4. **Room** — purpose-built lean (§11).
5. **Mutters** — gloat + Cassie reactions; content, comes last.

Cut-candidates if scope tightens: tie-up the guard (→ post-slice).

---

## 13. Open Questions / Decisions to Confirm

- [x] **Lure** — ~~interactive Beg verb vs. scripted approach~~ **CUT Day 62.** Guard auto-approaches and gloats up close every check-in; no summon verb. Preserved as a forward hook for the AI levels (§14).
- [ ] **Weapon placement** — a back-scoot surface (reuses the L6 bound-hands-behind-back reach pattern) vs. already near the chair. *(spec'd: back-scoot surface)*
- [x] **Strike timing** — ~~window-that-lapses vs. enabled-whenever-in-range~~ **resolved Day 62:** strike is valid whenever the guard is in `LeanIn` (the close-gloat window), gated on her being armed. The window lapses naturally when he straightens and leaves.
- [ ] **Feign-fail harshness** — re-cinch/escalate vs. soft reset. *(spec'd: re-cinch)*
- [ ] **Room** — purpose-built lean vs. adapt L6. *(spec'd: purpose-built, cannibalize L6)*
- [ ] **Tie-up the guard** — confirm cut from v1?
- [ ] **Check-in count before the turn** — how many threat-only close-ins before she can realistically be armed. Tuning value; sets the pacing of the build before the payoff.

---

## 14. Forward Hooks

- **Feign** generalizes to the deferred L6 **interruptible-untie** tension (guard returns mid-untie → Cassie feigns her hands are still bound until he leaves). The VS is where that mechanic is born.
- **StrikeableGuard** → future takedown/combat verbs and the L11/L12 villain confrontation.
- **Tie-up-the-guard** → victory-button stretch and future capture/turnabout mechanics.
- **Lure / Call Out** (cut here, §13) → reintroduce in the patrol-AI levels, where a draw-the-guard verb has real tactical meaning (pull a guard off a position, bait him from a sightline). It's agency theater against a scripted actor; it earns its place against AI that can be meaningfully misdirected.
