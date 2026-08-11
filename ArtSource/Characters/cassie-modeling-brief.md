# Cassie Modeling Brief — VS / Rara
*Cel-shaded noir, low-poly. Pin next to Blender.*
*Last read against `Cassie_Blockout.blend`: Rara Day 122 / 2026-08-08.*

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

## Skeletal landmarks vs. mesh silhouette

**These are two different sets of numbers. Do not use one for the other.**

The proportions table below is *skeletal* — joint positions and bone lengths. The
mesh silhouette sits outside it by whatever the muscle and cloth mass adds. The
shoulder is the case that bites: the skeletal half-width is 0.152 (the joint, where
`UpperArm` roots), but the **mesh** shoulder edge sits outside that by the deltoid.

Setting mesh verts to skeletal numbers pins the widest upper point to the joint
centre and reads narrow-shouldered no matter what happens below it.

**Measured mesh values (read from the .blend, Day 122).** The Day 118 four-row
table was set by eye and only its shoulder entry survived contact with the file.
Replaced wholesale rather than patched — patching entry by entry is what let three
wrong numbers sit in this document for four days.

| Ring | z | Half-width | Ratio to shoulder |
|------|---|-----------|-------------------|
| Shoulder | 1.323 | 0.1700 | 1.00 |
| Upper chest | 1.224 | 0.1177 | 0.69 |
| Lower chest | 1.184 | 0.0879 | 0.52 |
| **Waistband** | **1.120** | **0.0621** | **0.37** |
| Upper belly | 1.043 | 0.1042 | 0.61 |
| Lower belly | 0.964 | 0.1463 | 0.86 |
| Hip | 0.907 | 0.1648 | 0.97 |

The *shape* is correct — monotonic taper down to the waistband, monotonic flare out
to the hip. That is silhouette priority #1 doing its job.

**Open: the waistband pinch is 0.0621, which may be too deep.** Full width 0.124 m
= 0.55 head units. Note that the old 0.112 cannot simply be reinstated — it exceeds
the 0.0879 ring above it and would invert the taper. If this gets opened, the range
to test in front ortho is roughly 0.075–0.085, and it is a two-vertex edit.

Note the tension before deciding: an extreme hourglass serves the **anti-guard**
test (slim, tapered, mobile against his slab) but pushes toward decorative, which
fails **capable, not helpless**. Both tests apply; neither wins automatically.

**The ratios are the durable thing, not the absolutes.** If the shoulder ever
changes, re-derive the rest from it. Waist ring goes at the waistband line, not the
anatomical waist — the high trouser line is the silhouette's waist break and two
competing horizontals read muddy.

Same failure mode as the forearm: three "discrepancies" to date were all
transcription or category errors, never rig errors. **If a number disagrees with
this document, read the .blend.**

---

## Locked proportions (skeletal)

Final height **1.68 m**. Blender data 3.88 BU; scale 0.4330 and apply, then Unity
Scale Factor 1.0. Verify against `_Ruler_Heads`.

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

## Reference setup (built Day 118)

Everything below lives in the `_REF` collection with **Selectable off**. At export
time, tick Exclude on `_REF` — this hides the rulers and refs in one click and
keeps them out of a Selected Objects FBX. Do not parent any of it to `Cassie_Rig`.

| Object | What it is | What it's for |
|---|---|---|
| `_Ruler_Heads` | 8 bands, 0.224 m pitch, array | Head-unit landmarks. 7.5 heads = 1.68 |
| `refBodyFront` | Proportions sheet, front figure | Silhouette + proportion only |
| `refBodySide` | Proportions sheet, side figure | Depth / Y profile |
| `refFrontWardrobe` | Generated Cassie render | Wardrobe, hair, colour only |

**Head unit = 0.224 m.** Landmarks: hip 0.907 = 4.05 heads, shoulder 1.293 = 5.77,
crown 1.680 = 7.50.

Image empties: **Size sets the largest dimension** — both body refs take Size 1.7,
same value, and must display at identical height. Align on **crown and hip**, never
the floor; both figures are on pointed/heeled feet.

### The three-source hierarchy — do not let any source do another's job

- **Proportions and silhouette → the armature**, which encodes the proportions
  sheet. Locked. Never re-derived from a picture.
- **Wardrobe, hair, colour, face landmarks → `refFrontWardrobe`.** *Which* features
  Cassie has, never how big or how dense.
- **Rendering → neither.** The generated refs are soft-shaded. The blush, lash
  detail, nose shadow, and lip gradient all die on the cel shader.

The two reference styles disagree about head-to-body ratio, and that ratio is
already baked into the armature. This is a hierarchy, not a blend. Her head is
larger relative to the body than the generated render's, so the face gets **fewer,
larger, simpler** features than that render shows.

---

## Refine pass — target design

Canonical Cassie. This is the reference the pass is aiming at.

> **Hair and neckline resolved Day 118.** This supersedes the hair-down /
> crew-neck version, which is deleted rather than kept, because carrying both is
> what caused the contradiction. Reasoning is recorded below so the decision
> doesn't get relitigated on feel.

- **Hair:** auburn-red, **high ponytail** — slightly loose and tousled, with
  face-framing strands escaping at the temples. Small gold hair tie.
- **Face:** defined, light freckles, full lips, arched brows, hazel-green eyes.
- **Top:** cream/ivory ribbed cropped long-sleeve, **high mock/funnel neck**.
- **Trousers:** ultra high-waisted brown/camel, pleated, waistband **above the
  navel**. The high waist is the silhouette's waist break — it does structural
  work, not just styling.
- **Midriff:** narrow strip only. The high trouser line keeps it narrow by design.
- **Jewellery:** small gold hoops.

**Why ponytail, on the merits and not on taste:**
1. It is symmetric at the crown, so it mirrors cleanly and does not fight the live
   Mirror modifier. Side-swept hair is asymmetric by definition and would have
   forced either a separate object or an early Mirror apply.
2. It survives the strike arc. Loose shoulder-length hair at `debugScrub` 0.8 and
   2.0 needs either cheating or sim, and sim is explicitly out of scope.
3. *Capable, not helpless*: hair tied back reads as someone who expects to move.

**Open wardrobe question — decide before trouser geometry:** the generated refs
show **wide-leg** trousers, straight from hip to hem. That's a strong noir read and
cheap geometry, but it conflicts with "slim tapered limbs" in the silhouette
priorities and adds mass low in the figure that moves her *toward* the guard's
planted silhouette, not away. If wide-leg is kept, the hip flare must read at the
waistband or it will not read at all.

---

## Refine pass — silhouette priorities

Outline is still everything in cel-shade. Order of what must read at a glance:

1. **V-to-waist taper** — shoulders to a nipped waist. The defining read.
2. **Visible neck** — head lifted clear off the shoulders. The mock neck eats some
   of this; keep the chin-to-collar gap open or the read is lost.
3. **Hip flare** — the widen below the waist.
4. **Hair mass** — the ponytail is a silhouette element, not a placeholder blob. It
   breaks the head's outline up and back, and gives a strong diagonal against a
   slat-lit background.
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
- Crown and tail are a small number of carved masses, not strands.
- Silhouette reads high-ponytail from front, side, and back ortho — the tail must
  break the head outline in *side* view, which is where it does the most work.
- Face-framing strands are two or three carved shapes at most, not locks.
- It survives the strike arc — check at `debugScrub` 0.8 and 2.0. The tail is
  rigid; if it reads stiff, that is the correct trade.
- **Stop the moment you start separating individual locks.**

**Hands — done when:**
- Five fingers, posed to close on the bottle, and able to sit wrist-to-wrist bound
  without interpenetrating.
- Read as dextrous — normal-to-small, not a grabber. Opposite of his mitts.
- **Stop at knuckle planes. No nails, no tendons, no palm creases.**

> **Left hand CLOSED, Day 122.** Thumb built Day 121, grip curl authored Day 122.
> Full record in *Hand and grip (Day 122)* below. Remaining test is wrist-to-wrist
> bound, which is a Unity check against the mirrored right hand, not a Blender one.
> **Finger-separation notches were considered and deferred** — see the same section.

**Clothing — done when:**
- Sweater, trousers, and waistband read as separate masses in silhouette.
- Crop hem, waistband, and mock-neck roll are clean enough to hold a hard shader
  terminator.
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

## Hand and grip (Day 122)

All values read from `Cassie_Blockout.blend`, not from recall.

### Measured

| Measure | Value |
|---|---|
| Hand length, wrist ring centre → tip ring centre | **0.159798** |
| Finger section, knuckle ring → tip ring | 0.048266 |
| Finger section thickness | 0.0216 |
| Knuckle ring spread (Y) | 0.0749 |
| Tip ring spread (Y) | 0.0584 |
| Thumb length | 0.0797 |

**The hand length is 0.1598, not 0.170.** Any 0.170 in an older recap is the
shoulder mesh half-width, mislabelled — the same category error that produced the
forearm ghost. Skeletal `Hand.L` bone length is 0.106629 and is a *different
number again*; it is the bone, not the silhouette.

### The grip curl

The finger section is one segment from knuckle to tip and there are **no finger
bones** — the grip is modelled geometry in a fixed pose, not a posed rig. One rigid
segment cannot grip: a 90° pivot drives the tips 0.034 through the palm, and any
angle shallow enough to clear reads as a loose cup. So the section carries **one
mid-ring loop cut** at its midpoint, giving two bends.

Authored Day 122, measured back out of the file:

| | Target | Achieved |
|---|---|---|
| Knuckle bend | 75° | 74.59° |
| Mid-ring bend | 30° | 29.82° |
| Tip centre vs knuckle line | 0 | +0.4 mm proximal |
| **Grip tunnel** | 21.6 mm | **21.2 mm** |

Both segments came through at 0.024133 — pure rotation, no scale contamination.
Nearest fingertip sits 24 mm clear of the thumb.

**Past 90° cumulative, more rotation makes the tunnel smaller, not larger.** The
tips swing back up toward the knuckles. This is counterintuitive and will be
rediscovered painfully if it is not written down.

### Derived constraint — bottle neck ≤ 21 mm

The tunnel is 21.2 mm and the hard ceiling for this finger section is about
23.5 mm, reached only at a dead-straight 90° plate with no visible curl. **The hand
sets the bottle, not the other way round.** Full prop spec lives in the GDD; the
number originates here.

If the bottle is ever specced wider, the options are: lengthen the finger section
(changes hand length, currently on spec), shrink the bottle, or accept fingers
sinking into it and rely on camera distance. The third is probably survivable at L6
distance but should be a decision, not a discovery while framing shot 5.

### Finger-separation notches — DEFERRED to `ideas.md`

Considered Day 122 as the last silhouette item on the hand. Not built, for two
reasons that should stand unless the cel-shader test contradicts them:

1. **Resolution.** Tip spread is 0.0584. Three notches gives 0.0146-wide lobes,
   0.87% of her height — below the read at L6 camera distance. Two notches gives
   three lobes, which is the wrong finger count.
2. **Order.** Notches are a silhouette judgement, and the curl is what determines
   the silhouette. On a curled fist the tip cap stops being a silhouette element
   at all; what reads is the dorsal knuckle mass.

Notches are not in the hands stop condition. The curl is. Revisit only if the fist
reads as a mitten in the actual cel shader at poster distance — and if it does, the
fix is dorsal knuckle definition, not tip notches.

---

## Export

FBX: **−Y Forward, Z Up**, Apply Scalings: FBX All, Selected Objects, **no Leaf
Bones**. Unity Scale Factor 1.0.

Tick Exclude on `_REF` before exporting.

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

## State (Day 122)

Read from the file, not from recall:

- **154 verts / 132 faces / 279 edges** base mesh. Mirror unapplied, so this is the
  number to track — the viewport corner figure is the frame counter, not a vert
  count. (124 at Day 118, 138 pre-thumb, 150 post-thumb, 154 post-curl.)
- **Skinned.** All 21 vertex groups present and matching bone names. 21 bones.
- **Modifiers: Mirror (X, Clipping on) + Armature.** Mirror is live and unapplied —
  it halves the body work.
- Torso rings set: V-to-waist taper and hip flare read correctly in front ortho.
  Seven-ring profile measured — see *Skeletal landmarks vs. mesh silhouette*.
- **Left hand closed.** Thumb (12 verts, all 100% `Hand.L`) and grip curl authored.
- Side profile is **no longer flat** — this reverses the Day 118 note, which was
  already stale when written. Chest projects forward to y = −0.13727 at z = 1.224;
  seat projects back to y = +0.15348 at z = 0.907. Body depth 0.291 excluding feet.

**Mirror and hair:** resolved by the ponytail decision. A centred ponytail is
symmetric on X, so it can be built inside the mirror with the rest of the body. No
early apply, no separate object.

---

## Open items

- [x] ~~Resolve the forearm length discrepancy.~~ **Closed Day 117.**
- [x] ~~Mirror the six authored Eulers into `CassieStrikeDriver.cs`.~~ **Day 117.**
- [x] ~~Hair and neckline contradiction between brief and refs.~~ **Closed Day 118
      — ponytail and mock neck.**
- [x] ~~Side profile: chest projection forward, seat projection back.~~ **Closed
      Day 122 — both present in the file. The item was stale, not open.**
- [ ] **Waistband pinch: keep 0.0621 or open it toward 0.075–0.085?** Two verts.
      Silhouette priority #1. See the ring table for the argument on both sides.
- [ ] Wide-leg vs. tapered trousers. See target design above.
- [ ] **Wrist ring weight split.** On the ring where the hand meets the forearm,
      one edge reads `Hand.L 0.94 / LowerArm.L 0.05` and the opposite edge of the
      *same ring at the same axial position* reads `Hand.L 0.525 / LowerArm.L 0.471`
      — automatic weights doing proximity math on a diagonal ring. Risk is wrist
      shear during the strike, in the hand that holds the bottle. **Do not fix
      blind.** Check at `debugScrub` 0.8 next time the hand goes to Unity; if it
      shears, match the 0.525 edge to the 0.94 edge. Mesh weights only — nothing
      near the rig.
- [ ] **Weight sums run 0.94–0.99 mesh-wide**, left over from the original
      automatic weights. Blender's armature deform normalises internally and Unity
      normalises on FBX import, which is why nothing is broken and nothing has ever
      looked wrong. Recorded so it is not rediscovered as a mystery. **Do not run
      Normalize All under deadline pressure** — that is a whole-mesh operation on a
      rig whose behaviour is already verified end-to-end.
- [ ] `Hair_Mass` y-scale reads 0.3 in scene; the 0.34 in the recap appears to be
      an error. Moot once hair is modelled — the placeholder block goes away.
- [ ] `rig: {fileID: 0}` on the Sit and Struggle drivers. Non-blocking (the beat
      verifies end-to-end, and `player` resolves the same way). Confirm against
      `CassieRigLayer.Awake` when convenient.

on a beveled box, corner verts are rounding geometry, not rings. Move them with a ring and a flat corner cap becomes a spike. If a group of verts needs to move together, scale the whole bevel about a plane — don't translate its parts by matching deltas.
