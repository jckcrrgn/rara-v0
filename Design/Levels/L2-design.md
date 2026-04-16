# Level 2 — The Storage Unit

## Purpose
Teach Pick Up. Establish that some bonds can't be broken bare-handed — tools are necessary, not optional.

## Setting
Small rented storage unit. Concrete floor, corrugated metal walls, single overhead bulb. Cluttered but not busy — a metal shelf unit against one wall, a few cardboard boxes on the floor, nothing else interactive. Muted greens/browns (Act 1 palette).

## Setup
- Player starts center-room, tied to a wooden chair with **zip ties** (not rope).
- Hands bound in front.
- A **box cutter** sits on the top shelf of a metal shelving unit against the far wall, blade retracted, barely visible.

## Why Zip Ties
Rope was L1. Zip ties are stronger — bare-hands Struggle does nothing against them. This is the first time the player hits a wall with Struggle, and it needs to feel intentional, not buggy. The detective's dialogue sells it.

## Solution (3 steps)
1. **Move** — Hop/scoot the chair to the shelving unit.
2. **Move into shelf** — Bump it. Box cutter falls to the floor.
3. **Pick Up** box cutter → **Struggle** now works. Zip ties cut in 2–3 presses.

## What Happens If the Player...

**Tries to Struggle first (expected):**
- Bond meter doesn't move. No progress at all.
- Mutter (after 2–3 presses): *"Zip ties. Struggling won't cut it."*
- Mutter (after 5+ presses): *"Need something sharp."*

**Tries to Pick Up with nothing nearby:**
- Mutter: *"Nothing in reach."*

**Bumps the shelf without being close enough:**
- Nothing falls. Player needs to be adjacent.

**Picks up box cutter and doesn't Struggle:**
- Nothing happens. Holding a tool doesn't auto-escape. You still have to Struggle — the tool just makes it work.

## Mutter Lines
| Trigger | Line |
|---------|------|
| Level start | *"Storage unit. Classy."* |
| Struggle with no tool (2–3 presses) | *"Zip ties. Struggling won't cut it."* |
| Struggle with no tool (5+ presses) | *"Need something sharp."* |
| Bump shelf, cutter falls | *"That'll work."* |
| Pick up box cutter | *(no line — the action feels good on its own)* |
| Struggle with tool, bonds break | *"Like butter."* |

## New Concepts Introduced
- **Bond type: zip ties** — immune to bare-hands Struggle, cuttable with blade tools.
- **Pick Up as Struggle enabler** — not just a modifier, a requirement.
- **Environmental interaction via Move** — bumping objects to displace them (started in L1 with movement, but here it has a clear purpose).

## Implementation Notes
- Zip ties need a bond type flag (e.g., `BondType.ZipTie`) that returns 0 progress from bare-hands Struggle. This is a data difference from L1's rope, not a code difference — the Struggle system just checks whether the player is holding a valid tool for the bond type.
- Box cutter is a `Pickupable` (already built). Needs a `ToolType` tag (e.g., `ToolType.Blade`) that the bond system checks.
- Shelf bump: trigger zone on the shelf. When the player collides with it, the box cutter's rigidbody activates and it falls. One-shot trigger — doesn't re-trigger.
- Reuse chair restraint type from L1. No new movement code needed.

## Metrics
- **Par solve:** ~60 seconds for a player who explores before mashing Struggle.
- **Minimum steps:** 3 (Move to shelf → bump → Pick Up → Struggle x2–3).
- **Maximum reasonable time:** 2–3 minutes if the player spends a while trying to Struggle first.
