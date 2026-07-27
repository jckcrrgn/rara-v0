# Guard Modeling Brief — VS / Rara
*Box-model blockout. Cel-shaded noir, low-poly. Pin next to Blender.*

## Concept
The anti-Cassie. She's slim, tapered, hair-soft, mobile. He's a bald slab — broad, heavy, planted. In silhouette they should never be mistaken. If you'd confuse the two outlines, the blockout has failed.

## Proportions (head-heights)
Cassie baseline (contrast): ~7.5 heads, shoulders ~2 heads wide, clear V-to-waist taper.

Guard:
- **Height:** 8 heads. Tall enough to loom.
- **Shoulders:** 3 heads wide. Primary tell — one full head wider than her. Get this and half the read is done.
- **Neck:** ~¼ head, buried in trap mass. Head sits almost on the shoulders. "No neck."
- **Head:** slightly small for the body (big body shrinks the head = imposing). Squared, blocky.
- **Torso:** near-vertical box. Minimal waist taper — a fridge, not an hourglass. Where she goes V-to-waist, he stays a wall.
- **Hips:** narrower than shoulders (top-heavy = threat). Thick legs under them.
- **Limbs:** thick cylinders. Forearms and hands oversized — he grabs, he doesn't jab.
- **Mass sits high** — chest/shoulders heavy, slight forward lean of menace.

## Silhouette priorities (outline is everything in cel-shade)
In order of what must read at a glance:
1. **Shoulder slab** — widest point, hard flat top edge.
2. **Head-on-shoulders** — no visible neck.
3. **Vertical torso box** — no waist.
4. **Oversized hands / thick forearms.**

Everything below this line is invisible at noir contrast. Don't spend time there.

## Box-model order (mirror on, primitives only)
1. **Torso box** — anchor mass, sets scale. Everything hangs off this.
2. **Hips block** — smaller box below.
3. **Head block** — squared cube, near-directly on torso.
4. **Upper arm + forearm** — thick segments.
5. **Hand blocks** — oversized mitts, no fingers.
6. **Thigh + shin** — thick, planted.
7. **Foot blocks.**

Then rotate to front + side ortho and check against the exit condition.

## Stop conditions
- **No face.** The instant you want to place an eye — stop. Bald block, zero features.
- **No fingers.** Hand = one mitten mass.
- **No folds, no detail, no clothing.** Masses only.
- **Exit:** front + side ortho of a big male slab, unmistakably not-Cassie. Drop her silhouette beside his — if they could be confused, fix width / neck / taper before you stop.
