"""
The first circle's land (Limbo after Dante: the grey valley of the virtuous souls, the Noble
Castle of seven walls): cypresses, ruined arches and an aqueduct, a round temple, steles, crags,
the walkable bridge over the ravine, the castle's wall, towers and gate (the poet, the gate's
statues and the castle far off are sculpted: import_creatures.py).

Run headless:
  blender -b -P Tools/blender/make_limbo.py -- <out_dir> [asset ...]

Conventions as make_world.py: one GLB per asset, one root named after it (variants as children),
origins on the ground, forward = Blender -Y. The bridge's origin is the middle of its deck top.
"""
import math
import os
import random
import sys
import bmesh
import bpy
from mathutils import Vector, Matrix, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402
from lib import NT, C, UP, join, parent, lathe  # noqa: E402
from make_world import root, place, wall, ruin_stone, box, arch_prism  # noqa: E402


def dark_stone(name, moss=0.18):
    """Old grey stone gone dark with the fog's damp."""
    return ruin_stone(name, base="#5d615d", dark="#2b2e2c", light="#7c807b", moss=moss)


def mat_cypress(name):
    """A cypress's dense needles: near-black green, clumps catching a little grey light."""
    t = NT(name)
    clumps = t.voronoi(t.vec(), 5.0, out="Distance")
    n = t.noise(t.vec(), 14, detail=6)
    col = t.ramp(t.math("ADD", t.math("MULTIPLY", clumps, 0.7), t.math("MULTIPLY", n, 0.5)),
                 [(0.15, C("#0d130e")), (0.55, C("#1d2a1c")), (0.9, C("#3a4634"))])
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.6)), 0.7), col, C("#050806"))
    return t.finish(col, rough=0.85, normal=t.bump(t.math("ADD", clumps, n), 0.8, 0.05))


# ----------------------------------------------------------------------------- trees
def cypress_shape(name, h, r, seed, mat):
    """A tall narrow flame of foliage on a short trunk, lumpy with clumps."""
    prof = []
    n = 26
    for i in range(n + 1):
        t = i / n
        rad = r * (math.sin(math.pi * min(1.0, t * 1.05) ** 0.75) ** 0.9) * (1 - 0.15 * t)
        prof.append((max(0.02, rad) if i < n else 0.0, 0.8 + t * (h - 0.8)))
    ob = lathe(name, prof, steps=22)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    for v in bm.verts:
        p = v.co
        k = noise.noise(Vector((p.x * 1.3 + seed, p.y * 1.3, p.z * 0.6))) * 0.35 + noise.noise(Vector((p.x * 4, p.y * 4, p.z * 2 + seed))) * 0.1
        d = Vector((p.x, p.y, 0))
        if d.length > 1e-4:
            p += d.normalized() * k * r * 0.8
        p.x += math.sin(p.z * 0.35 + seed) * 0.08 * p.z / h
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.materials.append(mat)
    L.shade_smooth(ob, 80)
    trunk = L.tube(name + "_trunk", [((0, 0, 0), r * 0.18), ((0, 0, 1.2), r * 0.12)], seg=8)
    trunk.data.materials.append(L.mat_driftwood(name + "_bark", base="#3e3833", dark="#161412", light="#5e564e", moss=0.1))
    return join(name, [ob, trunk])


def build_cypress():
    r = root("cypress")
    m = mat_cypress("cypress_needles")
    for k, (h, rad) in enumerate(((9.5, 1.1), (7.5, 0.95), (11.5, 1.25))):
        c = cypress_shape(f"Cypress{k}", h, rad, 30 + k * 7, m)
        L.bake(c, size=512, metal=False)
        parent(c, r)
    L.export_tree("cypress", r)


# ----------------------------------------------------------------------------- ruins
def build_ruin_arch():
    """Freestanding arches of an old portico: Arch0 still whole, Arch1 with its right half fallen."""
    r = root("ruin_arch")
    m = dark_stone("ruin_arch_stone")
    a0 = wall("Arch0", 6.0, 7.0, 1.3, m, holes=[(0, 0, 3.4, 4.2, False)], seed=501, broken=0.15)
    cap = box("cap0", (6.4, 1.6, 0.5), (0, 0, 7.0), m, 0.05)
    a0 = join("Arch0", [a0, cap])
    a1 = wall("Arch1", 6.0, 7.0, 1.3, m, holes=[(0, 0, 3.4, 4.2, False)], seed=507, broken=0.95, keep_left=0.9)
    for ob in (a0, a1):
        L.bake(ob, size=1024, metal=False)
        parent(ob, r)
    L.export_tree("ruin_arch", r)


def build_aqueduct():
    """A run of an aqueduct's arches on tall piers, the channel on top broken through."""
    r = root("aqueduct")
    m = dark_stone("aqueduct_stone", moss=0.25)
    span, n, h = 5.0, 4, 11.0
    holes = [(-span * (n - 1) / 2 + i * span, 0, 3.4, 6.0, False) for i in range(n)]
    w = wall("Aqueduct", span * n, h, 1.6, m, holes=holes, seed=521, broken=0.35, keep_left=0.6)
    upper = [(-span * (n - 1) / 2 + i * span, 8.0, 1.6, 1.6, False) for i in range(n)]
    for x, z0, ww, hh, p in upper:
        c = arch_prism("u", ww, hh, 5, p)
        c.location = (x, 0, z0)
        L.transform_apply(c)
        L.boolean(w, c)
    L.bake(w, size=1024, metal=False)
    parent(w, r)
    L.export_tree("aqueduct", r)


def build_tholos():
    """A round temple on a hill: a ring of columns (two fallen), a cracked dome on them."""
    r = root("tholos")
    m = ruin_stone("tholos_stone", base="#8a8c86", dark="#3e403c", light="#aeb0aa", moss=0.3)
    parts = [lathe("steps", [(4.6, 0), (4.6, 0.35), (4.2, 0.35), (4.2, 0.7), (3.8, 0.7), (3.8, 1.0), (0, 1.0)], steps=48)]
    parts[0].data.materials.append(m)
    R, n = 3.3, 10
    for i in range(n):
        if i in (3, 7):
            continue
        a = i / n * math.tau
        c = L.column(f"c{i}", 5.0, 0.32, m, broken=(i == 5), seed=600 + i)
        c.location = (math.cos(a) * R, math.sin(a) * R, 1.0)
        L.transform_apply(c)
        parts.append(c)
    ring = lathe("entablature", [(3.9, 6.3), (3.9, 7.0), (2.9, 7.0), (2.9, 6.3)], steps=48)
    dome = lathe("dome", [(3.7, 7.0)] + [(3.7 * math.cos(t * math.pi / 2), 7.0 + 2.6 * math.sin(t * math.pi / 2)) for t in [i / 10 for i in range(1, 11)]], steps=48)
    for ob in (ring, dome):
        ob.data.materials.append(m)
    # a bite broken out of the dome and its ring
    bite = L.blob("bite", 2.2, seed=9, squash=(1, 1, 1), rough=0.4, sub=3)
    bite.location = (2.8, -1.8, 8.4)
    L.transform_apply(bite)
    bite2 = bite.copy()
    bite2.data = bite.data.copy()
    L.link(bite2)
    L.boolean(ring, bite)
    L.boolean(dome, bite2)
    parts += [ring, dome]
    ob = join("Tholos", parts)
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("tholos", r)


def build_stele():
    """Grave steles leaning in the grass: Stele0 tall, Stele1 broken, Stele2 a block with a carved panel."""
    r = root("stele")
    m = ruin_stone("stele_stone", base="#7a7c77", dark="#333532", light="#9c9e98", moss=0.4)
    s0 = wall("Stele0", 0.9, 2.2, 0.25, m, seed=541, broken=0.1)
    s1 = wall("Stele1", 0.9, 2.0, 0.25, m, seed=547, broken=0.9, keep_left=0.5)
    s2 = box("Stele2", (1.6, 0.8, 1.1), (0, 0, 0.55), m, 0.06)
    panel = box("panel", (1.2, 0.1, 0.6), (0, -0.42, 0.6), m, 0.02)
    s2 = join("Stele2", [s2, panel])
    for ob, tilt in ((s0, 0.08), (s1, -0.12), (s2, 0.03)):
        ob.rotation_euler = (tilt, tilt * 0.5, 0)
        L.transform_apply(ob)
        L.bake(ob, size=512, metal=False)
        parent(ob, r)
    L.export_tree("stele", r)


def build_crag():
    """Dark crags for the valley's rim and the poet's cliff: Crag0..3, from a boulder to a cliff."""
    r = root("crag")
    m = L.mat_rock("crag_rock", base="#4f514e", dark="#1e201f", light="#6f716c", moss=0.2, scale=1.2)
    for k, (s, sq) in enumerate(((2.5, (1.3, 1.0, 0.8)), (4.0, (1.6, 1.1, 1.0)), (6.0, (1.2, 1.0, 1.6)), (9.0, (2.0, 1.2, 1.1)))):
        c = L.craggy(f"Crag{k}", s, 700 + k, sq, m)
        bm = bmesh.new()
        bm.from_mesh(c.data)
        zmin = min(v.co.z for v in bm.verts)
        for v in bm.verts:   # a flat foot, sunk into the ground
            v.co.z -= zmin + s * 0.25 * sq[2]
        bm.to_mesh(c.data)
        bm.free()
        L.decimate_to(c, 4000)
        L.bake(c, size=1024, metal=False)
        parent(c, r)
    L.export_tree("crag", r)


# ----------------------------------------------------------------------------- the crossing and the castle
def build_bridge():
    """The bridge over the ravine: a deck 32 m long (along -Y), 4.5 wide, low parapets, on three arches
    down into the ravine. The origin is the middle of the deck's top."""
    r = root("bridge")
    m = dark_stone("bridge_stone", moss=0.3)
    Lb, W, D = 32.0, 4.5, 12.0
    # the arches: a wall across X turned along Y
    w = wall("piers", Lb, D, W - 0.4, m, holes=[(-10.5, 0, 7.5, 6.5, False), (0, 0, 7.5, 6.5, False), (10.5, 0, 7.5, 6.5, False)], seed=561, broken=0.0)
    w.location.z = -D - 0.6
    L.transform_apply(w)
    deck = box("deck", (Lb, W, 0.6), (0, 0, -0.3), m, 0.04)
    pl = box("parapet_l", (Lb, 0.4, 0.8), (0, W / 2 - 0.2, 0.4), m, 0.04)
    pr = box("parapet_r", (Lb, 0.4, 0.8), (0, -W / 2 + 0.2, 0.4), m, 0.04)
    # gaps knocked out of the parapets
    for x, side in ((-6, 1), (9, -1)):
        c = box("gap", (2.2, 1.0, 1.5), (x, side * (W / 2 - 0.2), 0.8), m, 0)
        L.boolean(pl if side > 0 else pr, c)
    ob = join("Bridge", [w, deck, pl, pr])
    ob.rotation_euler = (0, 0, math.pi / 2)
    L.transform_apply(ob)
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("bridge", r)


def battlements(name, length, z, thick, mat):
    out = []
    n = int(length / 1.6)
    for i in range(n):
        if i % 2:
            continue
        x = -length / 2 + (i + 0.5) * length / n
        out.append(box(f"{name}{i}", (length / n, thick, 1.1), (x, 0, z + 0.55), mat, 0.03))
    return out


def build_castle_wall():
    """The Noble Castle's wall: CastleWall a straight run 16 m long, 13 high, with battlements and
    buttresses; CastleTower a round tower with a pointed roof."""
    r = root("castle_wall")
    m = dark_stone("castle_stone", moss=0.12)
    Lw, Hw, T = 16.0, 13.0, 2.6
    w = wall("w", Lw, Hw, T, m, holes=[(-4, 8.5, 0.6, 1.6, True), (4, 8.5, 0.6, 1.6, True)], seed=581, broken=0.0)
    parts = [w] + battlements("b", Lw, Hw, T * 0.4, m)
    for x in (-Lw / 2 + 2, Lw / 2 - 2):
        b = box("buttress", (1.6, 2.0, 9.0), (x, -T / 2 - 0.9, 4.5), m, 0.05)
        parts.append(b)
    cw = join("CastleWall", parts)
    L.bake(cw, size=1024, metal=False)
    parent(cw, r)
    body = lathe("tower", [(4.2, 0), (4.0, 2), (3.6, 17), (3.9, 17.2), (3.9, 18.5), (0, 18.5)], steps=32)
    roof = lathe("roof", [(4.3, 18.5), (0.2, 28), (0, 28.5)], steps=32)
    body.data.materials.append(m)
    roof.data.materials.append(L.mat_rock("castle_roof", base="#2c2f31", dark="#121415", light="#45494b", moss=0.0, scale=4))
    for z in (6, 11, 15):
        for a in (0.3, 2.4, 4.4):
            c = arch_prism("win", 0.5, 1.2, 3, True)
            c.location = (math.cos(a) * 3.8, math.sin(a) * 3.8, z)
            c.rotation_euler = (0, 0, a + math.pi / 2)
            L.transform_apply(c)
            L.boolean(body, c)
    tw = join("CastleTower", [body, roof])
    L.bake(tw, size=1024, metal=False)
    parent(tw, r)
    L.export_tree("castle_wall", r)


def build_castle_gate():
    """The gate of the first wall: two square towers, a tall pointed passage 6 m wide between them
    (open: the way to the circle's end goes through it), plinths for statues in front."""
    r = root("castle_gate")
    m = dark_stone("gate_stone", moss=0.12)
    parts = []
    front = wall("front", 10.0, 15.0, 3.0, m, holes=[(0, 0, 6.0, 8.0, True)], seed=601, broken=0.0)
    parts += [front] + battlements("gb", 10.0, 15.0, 1.2, m)
    for side in (-1, 1):
        t = box("tower", (5.0, 6.0, 19.0), (side * 7.5, 0, 9.5), m, 0.06)
        cap = box("towercap", (5.6, 6.6, 0.8), (side * 7.5, 0, 19.4), m, 0.04)
        roof = lathe("spire", [(3.2, 19.8), (0.15, 27.0), (0, 27.4)], steps=4)
        roof.rotation_euler = (0, 0, math.pi / 4)
        roof.location = (side * 7.5, 0, 0)
        L.transform_apply(roof)
        roof.data.materials.append(m)
        plinth = box("plinth", (2.2, 2.2, 1.6), (side * 4.6, -4.5, 0.8), m, 0.05)
        parts += [t, cap, roof, plinth]
    ob = join("CastleGate", parts)
    L.bake(ob, size=2048, metal=False)
    parent(ob, r)
    L.export_tree("castle_gate", r)






ASSETS = {
    "cypress": build_cypress, "ruin_arch": build_ruin_arch, "aqueduct": build_aqueduct, "tholos": build_tholos,
    "stele": build_stele, "crag": build_crag, "bridge": build_bridge, "castle_wall": build_castle_wall,
    "castle_gate": build_castle_gate,
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    L.OUT = argv[0] if argv else os.path.abspath("Assets/Crab/Models/World")
    names = argv[1:] or list(ASSETS)
    os.makedirs(L.OUT, exist_ok=True)
    for n in names:
        L.reset()
        random.seed(n)
        ASSETS[n]()
    import shutil
    shutil.rmtree(os.path.join(L.OUT, "_tex"), ignore_errors=True)


if __name__ == "__main__":
    main()
