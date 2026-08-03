# Cassie Modeling Brief — VS / Rara
*Cel-shaded noir, low-poly. Pin next to Blender.*

**Status: REFINE PASS.** The blockout is closed (see appendix). This document now
governs turning the existing blockout mesh into the shippable Slice 1 character.

**Path locked (Rara Day 116):** refine the blockout mesh **in place**. Do not
rebuild, do not retopo from a sculpt, do not start a new file. In-place refine is
what preserves the skeleton, the bone rolls, and all six authored strike Eulers.
Every one of those is load-bearing and none of them survive a rebuild.

**Critical path.** This is the long pole to the Slice 1 date (late Sept – mid Oct).
Room dressing, SFX, and mechanics smoothing are all shorter and none of them gate
the others the way this does. When in doubt about what to work on, it is this.

---

## The design test

> **Capable, not helpless.**

Every choice — face, hair, posture, cloth, shading — gets asked this. Does it make
Cassie read as more capable, or more helpless? Peril is the cover promise;
competence is the payoff. Both, simultaneously. Restraint imagery is genre
iconography, not vulnerability signalling. If a detail only sells the peril, cut it.

Second test, always paired: **the anti-guard.** Where he is a bald slab — broad,
heavy, planted — she is slim, tapered, mobile, poised. Drop his silhouette beside
hers. If the two reads are close, the pass has failed regardless of how good the
detail is.

---

## HARD INVARIANTS — do not touch

### Bone rolls
L/R mirror exactly. This is what makes `mirrorOffArm = true` correct in
`CassieStrikeDriver` and what keeps every authored Euler below meaningful. Changing
a roll silently invalidates the entire strike.

| Bone     | Roll      |
|----------|-----------|
| Shoulder | ±102.26   |
| UpperArm | ±146.81   |
| LowerArm | ±146.70   |
| Hand     | ±147.35   |
| UpperLeg | ∓3.14     |
| LowerLeg | ∓6.04     |
| Foot     | ∓8.30     |
| Toe      | ±171.70   |

### Authored strike Eulers
Six hand-tuned local-Euler pose targets, live in the **scene**, not the script.
The `CassieStrikeDriver.cs` field initializers are placeholder defaults and differ
from every one of these. **Reverting the prefab or re-adding the component destroys
them.** Never press Revert All.

| Field                      | Value                        |
|----------------------------|------------------------------|
| `upperArmCoilEuler`        | (-10, -15, 0)                |
| `upperArmStrikeEuler`      | (-105.8, 138.6, 14.7)        |
| `forearmCoilEuler`         | (0, -10, 0)                  |
| `forearmStrikeEuler`       | (0.65, -41.6, 52.95)         |
| `postStrikeUpperArmEuler`  | (-46.3, 156.9, 25.57)        |
| `postStrikeForearmEuler`   | (-2.41, -31.8, 102.5)        |

*Verified against VS_Turnaround.unity, Rara Day 117.* Mirrored into a dated
comment block at the head of the pose-target fields in `CassieStrikeDriver.cs`
(applied Day 117), so the values survive loss of the scene file.

**These Eulers were never wrong.** The wrist drift chased for weeks was a 12.8%
forearm scale asymmetry (LowerArm.L 0.356 vs R 0.316), fixed on Day 116 — both now
symmetric to seven decimals. The authored values needed no retune and must not be
retuned now. If wrists drift again, check the scale chain first.

### Skeleton
Bone count, names, hierarchy, and parenting stay as-is. Mesh work only.

---

## Locked proportions (current)

Final height **1.68 m**. Blender data 3.88 BU; scale 0.4330 and apply, then Unity
Scale Factor 1.0. Verify against `_Ruler_2m`.

| Measure             | Length (m) | % of height |
|---------------------|-----------|-------------|
| Ground → hip        | 0.907     | 54.0        |
| Hip → shoulder      | 0.386     | 23.0        |
| Shoulder → crown    | 0.387     | 23.1        |
| Upper leg           | 0.428     | 25.5        |
| Upper arm           | 0.260     | 15.5        |
| Forearm             | 0.238     | 14.2        |
| Shoulder half-width | 0.152     | 9.0         |

Legs, torso, hips take GolemGirl's ratios wholesale. Arms and shoulders are a
deliberate midpoint between GolemGirl (12.6 / 12.8 / 7.3) and Cassie_Blockout
(18.5 / 15.5 / 10.8). GolemGirl's arms are well short of human and Cassie's whole
verb set is reach: bound wrists, Struggle, back-scoot, Pick Up, conceal, the strike
arc. Short arms compress every one of those reads.

> **VERIFIED against Cassie_Blockout.blend, Day 117. Closed — do not re-open.**
> No discrepancy ever existed. **0.260 is the UPPER arm**; the forearm is
> **0.238007**, exactly as tabled. The earlier "0.2599996 / 0.2599997 forearm"
> reading was the upper arm mislabelled.
>
> Also confirmed in the file: scale applied (1,1,1) on both mesh and rig;
> hip z = 0.9070; hip→shoulder 0.386; shoulder→crown 0.387. All on spec.
>
> **If a number ever disagrees with this document again, read the .blend.** It
> takes minutes. Do not put it on a list, do not treat it as a gate on mesh work.
> Three flagged "discrepancies" to date were all transcription errors, not rig
> errors.

### Measured armature (Day 117 — the reference)

| Bone | Length (m) | Roll (°) |
|------|-----------|----------|
| Shoulder.L/R    | 0.132484 | ±102.260 |
| UpperArm.L/R    | 0.260000 | ±146.806 |
| LowerArm.L/R    | 0.238007 | ±146.700 |
| Hand.L/R        | 0.106629 | ±147.348 |
| UpperLeg.L/R    | 0.427999 | ∓3.145   |
| LowerLeg.L/R    | 0.405132 | ∓6.040   |
| Foot.L/R        | 0.122224 | ∓8.303   |
| Foot.L.001/R.001| 0.036269 | ±171.697 |
| Spine           | 0.132451 | 0.000    |
| Chest           | 0.151373 | 4.538    |
| Neck            | 0.154190 | 4.538    |
| Head            | 0.190709 | 4.538    |
| Hips            | 0.121098 | 0.000    |

Every L/R pair is identical to six decimals and every roll pair mirrors exactly.
This is the verified foundation the authored strike Eulers rest on.

---

## Refine pass — target design

Canonical Cassie. This is the reference the pass is aiming at.

- **Hair:** shoulder-length auburn-red, worn **down** — wavy, tousled, side-swept.
  Not a ponytail. Not tied back.
- **Face:** defined, light freckles, full lips, arched brows, hazel-green eyes.
- **Top:** ivory ribbed cropped crew-neck sweater.
- **Trousers:** ultra high-waisted brown, waistband **above the navel**. The high
  waist is the silhouette's waist break — it does structural work, not just styling.
- **Midriff:** narrow strip only. The high trouser line keeps it narrow by design.
- **Jewellery:** small gold hoops.

---

## Refine pass — silhouette priorities

Outline is still everything in cel-shade. Order of what must read at a glance:

1. **V-to-waist taper** — shoulders to a nipped waist. The defining read.
2. **Visible neck** — head lifted clear off the shoulders.
3. **Hip flare** — the widen below the waist.
4. **Hair mass** — now a silhouette element, not a placeholder blob. Down and
   asymmetric (side-swept) breaks the head's symmetry and is a strong noir read
   against a slat-lit background.
5. **Slim tapered limbs.**

---

## Refine pass — stop conditions

Cel shading is flat colour plus outline. Anything that only exists under soft
shading dies on contact with the shader. **Every stop condition below is a test you
run in flat shading, at poster distance, not a poly count.**

**Face — done when:**
- Brow, nose, lip, and jaw read as *planes*, not as sculpted micro-detail.
- Eyes read at poster distance in flat colour. If they only work up close and
  smooth-shaded, they are too fine.
- Freckles are texture, never geometry.
- The expression reads dry and unimpressed at rest. Not scared, not neutral-blank.
  If the resting face reads afraid, that's a "capable, not helpless" failure.
- **Stop the moment you want to add a crease.** Cel shading will not show it.

**Hair — done when:**
- It is a small number of carved masses with a clear side-sweep, not strands.
- Silhouette reads shoulder-length and tousled from front, side, and back ortho.
- It survives the strike arc — check it at `debugScrub` 0.8 and 2.0.
- **Stop the moment you start separating individual locks.**

**Hands — done when:**
- Five fingers, posed to close on the bottle, and able to sit wrist-to-wrist bound
  without interpenetrating.
- Read as dextrous — normal-to-small, not a grabber. Opposite of his mitts.
- **Stop at knuckle planes. No nails, no tendons, no palm creases.**

**Clothing — done when:**
- Sweater, trousers, and waistband read as separate masses in silhouette.
- Crop hem and waistband edges are clean enough to hold a hard shader terminator.
- Ribbing is texture or a shallow repeat, never modelled folds.
- **Stop the moment you start simulating cloth.** No sim, no wrinkle passes.

**Whole-figure exit condition:**
Front + side + 3/4 ortho, in the actual cel shader, next to the guard's silhouette.
Unmistakably not-guard. Then re-run the strike and the struggle in Unity and confirm
wrists still hold. Then stop.

**Hard rule for the whole pass:** anything that fails a stop condition goes to
`ideas.md`, not into the mesh. The prior Cassie stall came from perfectionism at
exactly this stage, on the wrong pipeline. The pipeline is right now. The
perfectionism risk is unchanged.

---

## Export

FBX: **−Y Forward, Z Up**, Apply Scalings: FBX All, Selected Objects, **no Leaf
Bones**. Unity Scale Factor 1.0.

After every export, before anything else: confirm both forearms still read
symmetric, and confirm the strike still lands. Scale chain first, always.

---

## Appendix — Blockout (CLOSED)

*Historical. These rules governed the box-model blockout and are no longer in force.
Kept so the reasoning survives; do not apply them to the refine pass.*

Blockout target: 7.5 heads (half a head shorter than the guard), shoulders 2 heads
wide, visible ~½-head neck, softened cube head, V-to-waist taper, hips at or
slightly wider than waist, slim tapered limbs, mass centred and grounded, neutral
A-pose. Box-model from primitives, mirror on, in this order: torso box → hips block
→ neck cylinder → head block → upper arm + forearm → hand blocks → thigh + shin →
foot blocks.

Blockout stop conditions were: no face, no fingers, no hair strands, no folds, no
clothing — masses only. **All five are now superseded.** The refine pass exists
precisely to add face, fingers, hair, and clothing; use the refine stop conditions
above instead.

---

## Starting state (Day 117)

Read from the file, not from recall:

- **112 verts / 93 polys.** Pure blockout, as intended.
- **Skinned.** All 21 vertex groups present and matching bone names.
- **Modifiers: Mirror + Armature.** Mirror is live and unapplied — it halves the
  body work.

**Decide before you start hair:** the side-swept hair is asymmetric by definition
and fights the Mirror modifier. Either build hair as its own object outside the
mirror, or finish the mirrored body and apply first. Decide up front — this is
much cheaper than unpicking a symmetric hair blob three sessions in.

---

## Open items

- [x] ~~Resolve the forearm length discrepancy.~~ **Closed Day 117 — no
      discrepancy. See verified table above.**
- [x] ~~Mirror the six authored Eulers into a dated comment block in
      `CassieStrikeDriver.cs`.~~ **Applied Day 117.**
- [ ] `Hair_Mass` y-scale reads 0.3 in scene; the 0.34 in the recap appears to be
      an error. Moot once hair is modelled — the placeholder block goes away.
- [ ] `rig: {fileID: 0}` on the Sit and Struggle drivers. Non-blocking (the beat
      verifies end-to-end, and `player` resolves the same way). Confirm against
      `CassieRigLayer.Awake` when convenient.
