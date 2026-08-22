# mark_body_seams.py -- Rara Day 134
# Marks the body (material slot 0) seam set on Cassie_Mesh and selects only
# material-0 faces, ready for U > Unwrap.
#
# Run with Cassie_Mesh in EDIT MODE. Touches seam flags and selection only --
# no geometry, no weights, no modifiers. To revert: select all, Edge > Clear Seam.
#
# Edge list derived from Cassie_Blockout.blend, not from recall.

import bpy
import bmesh

# --- seam set -------------------------------------------------------------
# Leg: from the mirror-plane boundary at the hip, across the seat, down the
# back-inner corner of the leg to the sole. Opens the leg tube so it unrolls.
LEG = [(12, 22), (22, 124), (124, 109), (109, 27),
       (27, 168), (168, 172), (172, 31), (31, 35)]

# Arm: wrist ring splits sleeve from hand (also gives a hard ivory/skin edge),
# plus one slit along the back-underside of the sleeve to the shoulder cap.
WRIST_RING = [(44, 46), (46, 139), (139, 138), (138, 44)]
SLEEVE_SLIT = [(44, 38), (38, 39)]

# Toe box: closed cube, needs a ring. Inner-face perimeter, splits it into
# one cap plus a five-face disk.
TOE = [(100, 101), (101, 103), (103, 102), (102, 100)]

SEAMS = LEG + WRIST_RING + SLEEVE_SLIT + TOE

# --- guards ---------------------------------------------------------------
# Spot-check three verts so a reordered or wrong mesh aborts instead of
# marking seams in random places.
SPOT = {12:  (0.0000, 0.1535, 0.9067),
        44:  (0.4505, 0.0485, 0.8937),
        100: (0.0260, -0.1500, 0.0010)}

obj = bpy.context.object
if obj is None or obj.type != 'MESH':
    raise RuntimeError("Select Cassie_Mesh and enter Edit Mode first.")
if obj.mode != 'EDIT':
    raise RuntimeError("Enter Edit Mode first.")

me = obj.data
if len(me.vertices) != 240:
    raise RuntimeError("Expected 240 verts, got %d -- wrong mesh or edited since Day 134." % len(me.vertices))

bm = bmesh.from_edit_mesh(me)
bm.verts.ensure_lookup_table()
bm.edges.ensure_lookup_table()
bm.faces.ensure_lookup_table()

for idx, expected in SPOT.items():
    co = bm.verts[idx].co
    if max(abs(co[i] - expected[i]) for i in range(3)) > 1e-3:
        raise RuntimeError("Vert %d moved (%s vs expected %s). Aborting -- re-derive the edge list."
                           % (idx, tuple(round(c, 4) for c in co), expected))

# --- mark -----------------------------------------------------------------
marked, missing = 0, []
for a, b in SEAMS:
    e = bm.edges.get((bm.verts[a], bm.verts[b]))
    if e is None:
        missing.append((a, b))
    else:
        e.seam = True
        marked += 1

# --- select material 0 only ----------------------------------------------
for f in bm.faces:
    f.select_set(False)
for e in bm.edges:
    e.select_set(False)
for v in bm.verts:
    v.select_set(False)

body = 0
for f in bm.faces:
    if f.material_index == 0:
        f.select_set(True)
        body += 1
bm.select_flush(True)

bmesh.update_edit_mesh(me)

print("seams marked: %d / %d" % (marked, len(SEAMS)))
print("material-0 faces selected: %d (expect 118)" % body)
if missing:
    print("MISSING EDGES -- do not unwrap, stop and check:", missing)
