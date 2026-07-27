# Cassie Modeling Brief — VS / Rara
*Box-model blockout. Cel-shaded noir, low-poly. Pin next to Blender.*
*The protagonist — masses only. This is NOT the final Cassie.*

## Concept
The anti-guard. Where he's a bald slab — broad, heavy, planted — she's slim, tapered, mobile, poised. In silhouette they must never be confused. Drop the guard's outline beside hers: if the two reads are close, the blockout has failed.

The stall risk here is perfectionism, not proportion. This is the protagonist, so the pull to "just place one eye" / "just get the hair right" is strongest exactly here. Same rule as the guard: masses only. Refine, hair, and face are a later pass — never in the blockout. The prior Cassie stall came from the wrong pipeline (sculpt-then-retopo). Box-model from primitives, same as the mule.

## Proportions (head-heights)
Guard baseline (contrast): 8 heads, shoulders 3 heads wide, no neck, vertical fridge torso, narrow hips.

Cassie:
- **Height:** 7.5 heads. Half a head shorter than the guard — she looks up at him.
- **Shoulders:** 2 heads wide. A full head narrower than the guard. Sloped, soft — no hard flat slab top. Primary contrast tell.
- **Neck:** visible and slender, ~½ head. Head lifts clear off the shoulders — the exact opposite of his buried "no neck." Second contrast tell.
- **Head:** normal-to-slightly-large for the frame (delicate body reads the head up). Softer cube — round the corners he keeps squared.
- **Torso:** clear V-to-waist taper. Shoulders → nipped waist → hip flare. Where he stays a wall, she goes hourglass. Waist break sits high (the concept's ultra-high trouser line marks it).
- **Hips:** at or slightly wider than the waist — feminine flare below the nip. Contrast to his top-heavy narrow hips.
- **Limbs:** slim tapered cylinders. Forearms and hands normal-to-small — dextrous, not a grabber. Opposite of his oversized mitts.
- **Mass sits centered / grounded.** Upright and poised, no forward lean of menace. The contrapposto / hand-in-pocket in the concept is pose, not proportion — build neutral A-pose.

## Silhouette priorities (outline is everything in cel-shade)
In order of what must read at a glance:
1. **V-to-waist taper** — shoulders down to a nipped waist. The defining read; get this and half her silhouette is done.
2. **Visible neck** — head lifted clear off the shoulders.
3. **Hip flare** — the widen below the waist.
4. **Slim tapered limbs.**

## Box-model order (mirror on, primitives only)
1. **Torso box** — taper it: wider at shoulders, nip the waist. Anchor mass, sets scale.
2. **Hips block** — smaller box below, flared slightly wider than the waist.
3. **Neck cylinder** — slender, visible. Sets the head clear.
4. **Head block** — softened cube, near-normal size for the frame.
5. **Upper arm + forearm** — slim, tapered segments.
6. **Hand blocks** — small mitts, no fingers.
7. **Thigh + shin** — tapered, not slab (concept crops at mid-thigh; extrapolate legs from the 7.5H frame).
8. **Foot blocks.**

Then rotate to front + side ortho and check against the exit condition.

## Stop conditions
- **No face.** The instant you want to place an eye — stop. Smooth head mass, zero features. This is the protagonist and the hardest place to hold this line. Hold it.
- **No fingers.** Hand = one mitten mass.
- **No hair strands.** At most a single simple hair mass if the silhouette needs one — no waves, no tousle. (Concept hair is soft and down; that's a texture/refine problem, not a blockout one.)
- **No folds, no crop sweater, no midriff strip, no clothing.** Masses only.
- **Exit:** front + side ortho of a slim tapered figure, unmistakably not-guard. Drop his silhouette beside hers — if they could be confused, fix taper / shoulder width / neck before you stop.

## Export (when she's ready to import — not the blockout)
FBX: −Y forward, Y up. Same convention as the guard.

## Target proportions (Day 83) — locked

Final height 1.68 m. Blender data currently 3.88 BU; scale 0.4330 and apply,
then Unity Scale Factor 1.0. Verify against _Ruler_2m.

| Measure                | Length (m) | % of height |
|------------------------|-----------|-------------|
| Ground → hip           | 0.907     | 54.0        |
| Hip → shoulder         | 0.386     | 23.0        |
| Shoulder → crown       | 0.387     | 23.1        |
| Upper leg              | 0.428     | 25.5        |
| Upper arm              | 0.260     | 15.5        |
| Forearm                | 0.238     | 14.2        |
| Shoulder half-width    | 0.152     | 9.0         |

Legs, torso, hips take GolemGirl's ratios wholesale. Arms and shoulders are a
deliberate midpoint between GolemGirl (12.6 / 12.8 / 7.3) and Cassie_Blockout
(18.5 / 15.5 / 10.8) — GolemGirl's arms are well short of human and Cassie's
whole verb set is reach: bound wrists, Struggle, back-scoot, Pick Up, conceal,
the strike arc. Short arms compress every one of those reads.

DO NOT TOUCH BONE ROLLS. Cassie_Blockout's L/R rolls mirror exactly
(Shoulder ±102.26, UpperArm ±146.81, LowerArm ±146.70, Hand ±147.35,
UpperLeg ∓3.14, LowerLeg ∓6.04, Foot ∓8.30, Toe ±171.70), which is what
makes mirrorOffArm = true correct and what keeps the authored strike Eulers
meaningful. Lengths and head positions only.
