```
                        CASSIE CONROY: SUB ROSA

                             Hook Clip
                          Approx. 39 seconds


FADE IN:

INT. WAREHOUSE - NIGHT

TIGHT ON a pair of bound wrists. Rope. The fingers work at it --
patient, methodical, unhurried.

                    THUG (V.O.)
          Secured.

CUT TO:

WIDER. CASSIE CONROY, bound to a chair in the middle of the room.
Slat light across her. She does not look afraid. She looks determined.

                    BOSS (V.O.)
          So the problem is... what?

CUT TO:

A GUARD's head appears around the doorframe. He looks at her a
moment too long.

                    THUG (V.O.)
                (beat)
          She's... slippery.

CUT TO:

The guard leans in over her to gloat.

Her hands stay out of frame.

                    BOSS (V.O.)
                (scoffs)
          Come on. I'm sure you can handle
          one --

SMASH CUT TO:

A bottle swings up into frame and shatters across the guard's head.

                    BOSS (V.O.)
          -- helpless girl.

CUT TO BLACK.

TITLE CARD:

                        CASSIE CONROY

                         coming soon

FADE OUT.
```

---

## Timing

| # | Shot | Sec |
|---|------|-----|
| 1 | Knot | 8 |
| 2 | Poster | 7 |
| 3 | Peek | 6 |
| 4 | Gloat | 7 |
| 5 | Strike | 5 |
| 6 | Title | 6 |
| | **Total** | **39** |

The cut from 4 to 5 lands on the word *helpless*. Everything else can
flex; that one cannot.

> **EXT. Warehouse cut (Day 153).** Was shot 1, 9 seconds. Knot establishes
> instead. Every shot number below renumbered down by one — if you find an
> old reference to "shot 6 / the strike" anywhere, it means shot 5 now.
> `game-dev-plan.md` was already written in this numbering and needs no change
> except its stale "~48 seconds."
>
> **The opening V.O. went with it.** The exchange *"We got a problem, boss.
> We found that snoop digging around." / "And where is she now?"* played over
> the EXT and does not fit on an 8-second Knot. Cut rather than kept — the clip
> opens cold on "Secured," which is an answer to a question you never hear, and
> that is the right register. To restore it, Knot goes to ~13s and the clip
> back to 44s.

## Continuity

- Shot 1 plants that she is working the knots. Shot 5 reveals she won.
  Nothing between them may show her hands.
- She reads bound in shot 2. No bottle visible anywhere before shot 5.
- She acquires the bottle off-camera between shots 3 and 4.

---

## Staging invariants

**The rope-off and bottle-on are the same off-camera beat.** Between shot 3
(Peek) and shot 4 (Gloat), `Rope` disables and `Bottle_Held` enables. This is
why her hands are out of frame in Gloat — the rope is already gone by then, not
at the Strike. "Gone by Strike" is one shot too late; author the disable at the
3→4 gap.

Consequence: `Bottle_Held` exists in the scene across shots 1–3 even though the
continuity rule says no bottle before shot 5. **Cast Shadows off on
`Bottle_Held`** — an object out of frame still throws a shadow into it.

**Guard shoulder / upper-arm vs. the light shaft cone.** `armNear` currently has
**4 of 8 verts inside the cone**. That margin is the staging invariant, not the
guard's position — he was moved in to put the bottle on his temple, and the cone
intersection is the cost of that move. If the guard or the shaft is ever
re-staged, re-count. Zero verts inside reads as him standing outside his own
key; all eight reads as him lit like a subject rather than a slab.

**Before capture:** `verboseLogging` off on the live strike driver. Exactly one
Shot 5 camera enabled — `CAM_Shot5_Strike` and `CAM_Shot5_Alt` are both at
depth −1 and will fight for the output.

**`CAM_Shot5_Alt` is undecided.** Either it replaces the Shot 5 framing, or it
becomes the Knot angle and Shot 5 keeps `CAM_Shot5_Strike`. Ortho size untuned
either way. Decide before dressing anything to it.

**Frame Shot 5 in play mode only.** Cassie's runtime crown sits ~0.36 m below
her edit-mode position (Player y settles to 0.5 on Play from an edit-mode 0.86).
Edit-mode framing of this shot is wrong every time.
