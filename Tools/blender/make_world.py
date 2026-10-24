"""
The worlds: the beach of the prologue, Limbo (the dead world), the circle of Wrath (dunes, lava,
volcanoes) and the circle of Greed (the golden fortress city).

Run headless:
  blender -b -P Tools/blender/make_world.py -- <out_dir> [asset ...]

Each asset is one GLB with one root named after it (variants are children: Rock0, Rock1...).
Origins are at the ground; forward = Blender -Y (Unity +Z). Glow* parts keep a plain emissive
material. Matter sources (driftwood, rubble, planks, scrap) are ordinary models: the game
decides what they give.
"""
import math
import os
import random
import sys
import bmesh
import bpy
from mathutils.bvhtree import BVHTree
from mathutils import Vector, Matrix, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402
from lib import NT, C, UP, join, set_origin, parent, uv_sphere, tube, sweep, lathe  # noqa: E402


def glow(name, color, strength=6.0):
    return L.mat_plain(name, color, rough=0.4, emission=(color, strength))


def root(name):
    r = bpy.data.objects.new(name, None)
    L.link(r)
    return r


def place(ob, loc=(0, 0, 0), rot=(0, 0, 0)):
    ob.matrix_world = Matrix.Translation(loc) @ Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Rotation(rot[1], 4, "Y") @ Matrix.Rotation(rot[0], 4, "X")
    L.transform_apply(ob)
    return ob


def arch_prism(name, w, h, depth, pointed=False):
    """A closed cutter: a rectangle of width w and height h topped with a (pointed) arch,
    extruded across Y by depth. Origin at the bottom centre."""
    pts = [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, h)]
    n = 10
    for i in range(1, n):
        a = math.pi * i / n
        if pointed:
            x = w / 2 * math.cos(a)
            y = h + w * 0.75 * math.sin(a) ** 0.8 * (1 - abs(math.cos(a)) * 0.25)
        else:
            x, y = w / 2 * math.cos(a), h + w / 2 * math.sin(a)
        pts.append((x, y))
    pts.append((-w / 2, h))
    bm = bmesh.new()
    f = [bm.verts.new((x, -depth / 2, z)) for x, z in pts]
    b = [bm.verts.new((x, depth / 2, z)) for x, z in pts]
    bm.faces.new(f)
    bm.faces.new(list(reversed(b)))
    k = len(pts)
    for i in range(k):
        bm.faces.new((f[(i + 1) % k], f[i], b[i], b[(i + 1) % k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return L.from_bmesh(name, bm, smooth=False)


def wall(name, length, height, thick, mat, holes=(), seed=0, broken=0.6, keep_left=0.0):
    """A ruined wall along X (centred), thickness along Y: subdivided so the top can crumble,
    with arched holes [(x, z0, w, h, pointed)]."""
    bm = bmesh.new()
    nx, nz = max(4, int(length * 3)), max(4, int(height * 3))
    bmesh.ops.create_grid(bm, x_segments=nx, y_segments=nz, size=0.5)
    for v in bm.verts:
        v.co = Vector((v.co.x * length, 0, (v.co.y + 0.5) * height))
    ext = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    for v in [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]:
        v.co.y += thick
    for v in bm.verts:
        v.co.y -= thick / 2
        # crumbling top: higher where the wall still stands, ragged noise everywhere
        if v.co.z > height * 0.35:
            t = (v.co.z - height * 0.35) / (height * 0.65)
            stand = keep_left * max(0.0, -v.co.x / (length / 2))
            drop = broken * (0.5 + 0.5 * noise.noise(Vector((v.co.x * 0.8 + seed, 0.3, seed)))) * (1 - stand)
            v.co.z -= height * 0.65 * t * drop
            v.co.z += noise.noise(Vector((v.co.x * 3.1 + seed, seed, 1.0))) * 0.18 * t
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = L.from_bmesh(name, bm, smooth=False)
    for x, z0, w, h, pointed in holes:
        c = arch_prism("hole", w, h, thick * 3, pointed)
        c.location = (x, 0, z0)
        L.transform_apply(c)
        L.boolean(ob, c)
    ob.data.materials.append(mat)
    return ob


def ruin_stone(name, base="#73776f", dark="#4f534d", light="#8d918a", moss=0.25):
    """Weathered masonry: big blocks close in tone (the voronoi of mat_stone, calmed down)."""
    return L.mat_stone(name, base=base, dark=dark, light=light, block=1.7, stretch=(1, 1, 2.2), mortar=0.03, moss=moss, dirt=0.55)


# ----------------------------------------------------------------------------- the beach
def mat_palm_bark(name):
    t = NT(name)
    z = t.sep(t.coord)[2]
    rings = t.ramp(t.math("FRACT", t.math("MULTIPLY", z, 3.2)), [(0.0, 0.0), (0.12, 1.0), (0.85, 1.0), (1.0, 0.3)])
    n = t.noise(t.vec(scale=(1, 1, 0.3)), 12, detail=6)
    col = t.ramp(t.math("ADD", t.math("MULTIPLY", n, 0.5), t.math("MULTIPLY", rings, 0.5)), [(0.2, C("#4a3a28")), (0.6, C("#8a7458")), (0.9, C("#b39c7a"))])
    return t.finish(col, rough=0.9, normal=t.bump(t.math("ADD", rings, t.math("MULTIPLY", n, 0.3)), 0.9, 0.03))


def mat_leaf(name, base="#3f7a26", tip="#7aa83a", dry="#8a7a3a"):
    t = NT(name)
    uv = t.n("ShaderNodeUVMap", uv_map="Grain").outputs["UV"]
    u, v, _ = t.sep(uv)
    vein = t.ramp(t.math("ABSOLUTE", t.math("SUBTRACT", u, 0.5)), [(0.0, 1.0), (0.03, 0.0)])
    col = t.ramp(v, [(0.0, C(base)), (0.8, C(tip)), (1.0, C(dry))])
    col = t.mix(t.math("MULTIPLY", vein, 0.6), col, C("#c9d48a"))
    col = t.mix(t.ramp(t.noise(t.vec(), 6, detail=4), [(0.6, 0.0), (0.75, 0.5)]), col, C(dry))
    return t.finish(col, rough=0.55)


def frond(name, base, direction, length, width, droop, mat):
    """A palm frond: a spine arching out and down, leaflets as a ribbon with a Grain UV."""
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("Grain")
    side = direction.cross(UP).normalized()
    n = 18
    rows = []
    for i in range(n + 1):
        s = i / n
        p = base + direction * length * s + UP * (length * 0.35 * math.sin(s * math.pi * 0.6) - droop * s * s)
        w = width * math.sin(math.pi * min(1.0, s * 1.15)) * (1 - 0.2 * s)
        sag = UP * (-0.12 * w)
        rows.append([bm.verts.new(p + side * w + sag), bm.verts.new(p + UP * 0.02), bm.verts.new(p - side * w + sag)])
    for i in range(n):
        a, b = rows[i], rows[i + 1]
        for j in range(2):
            f = bm.faces.new((a[j], a[j + 1], b[j + 1], b[j]))
            for loop, uv in zip(f.loops, ((j / 2, i / n), ((j + 1) / 2, i / n), ((j + 1) / 2, (i + 1) / n), (j / 2, (i + 1) / n))):
                loop[uvl].uv = uv
    ob = L.from_bmesh(name, bm)
    L.mod(ob, "SOLIDIFY", thickness=0.012, offset=0)
    L.apply_all(ob)
    ob.data.materials.append(mat)
    return ob


def build_palm():
    r = root("palm")
    m_bark, m_leaf = mat_palm_bark("palm_bark"), mat_leaf("palm_leaf")
    top = Vector((1.1, -0.4, 6.0))
    trunk = sweep("trunk", [[((0, 0, 0), 0.24), ((0.15, -0.05, 1.5), 0.2), ((0.45, -0.15, 3.2), 0.17), ((0.8, -0.3, 4.8), 0.15), (tuple(top), 0.14)]], seg=14, sub=4)
    trunk.data.materials.append(m_bark)
    parts = [trunk]
    for k in range(10):
        a = k / 10 * math.tau + random.uniform(-0.2, 0.2)
        d = Vector((math.cos(a), math.sin(a), 0))
        parts.append(frond(f"frond{k}", top + Vector((0, 0, 0.1)), d, random.uniform(2.4, 3.2), 0.38, random.uniform(1.2, 2.0), m_leaf))
    m_nut = L.mat_wood("coconut", base="#6a4a22", dark="#2e1e0c", light="#8a6a3a", rings=20)
    for k in range(4):
        a = k / 4 * math.tau
        nut = uv_sphere("nut", 0.13, top + Vector((math.cos(a) * 0.16, math.sin(a) * 0.16, -0.15)), seg=12, rings=8)
        nut.data.materials.append(m_nut)
        parts.append(nut)
    ob = join("Palm", parts)
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("palm", r)


def build_beach_rocks():
    r = root("beach_rocks")
    m = L.mat_rock("beach_rock", base="#8a8478", dark="#45403a", light="#b8b0a2", moss=0.12, scale=2.0)
    for k, (rad, sq) in enumerate(((0.6, (1.3, 1.0, 0.6)), (1.0, (1.2, 0.9, 0.7)), (0.35, (1.0, 1.0, 0.8)), (1.6, (1.4, 1.0, 0.55)))):
        ob = L.rock_obj(f"Rock{k}", rad, 20 + k, sq, 0.28, m, sub=5, detail=0.07)
        L.bake(ob, size=1024, metal=False)
        parent(ob, r)
    L.export_tree("beach_rocks", r)


def build_shelter():
    """A flat slab resting on two rocks: a gap underneath where the crab hides from birds."""
    r = root("shelter")
    m = L.mat_rock("shelter_rock", base="#7e786c", dark="#3c3833", light="#ada596", moss=0.15, scale=1.6)
    a = L.rock_obj("a", 0.55, 31, (1.0, 1.1, 0.75), 0.25, m, sub=4)
    place(a, (-0.95, 0, 0.25))
    b = L.rock_obj("b", 0.5, 32, (1.1, 1.0, 0.8), 0.25, m, sub=4)
    place(b, (0.95, 0.1, 0.25))
    slab = L.rock_obj("slab", 1.5, 33, (1.25, 0.95, 0.22), 0.2, m, sub=5)
    place(slab, (0, 0, 0.92), (0.05, -0.04, 0.2))
    ob = join("Shelter", [a, b, slab])
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("shelter", r)


def build_driftwood():
    r = root("driftwood")
    paths = [[((-0.8, 0, 0.1), 0.09), ((-0.3, 0.05, 0.13), 0.08), ((0.2, -0.04, 0.12), 0.07), ((0.75, 0.02, 0.08), 0.05)],
             [((-0.1, 0.0, 0.13), 0.04), ((0.05, 0.25, 0.22), 0.03), ((0.12, 0.45, 0.3), 0.012)],
             [((0.4, -0.02, 0.11), 0.035), ((0.55, -0.22, 0.2), 0.022), ((0.62, -0.38, 0.24), 0.008)]]
    ob = sweep("Driftwood", paths, seg=10, sub=4)
    ob.data.materials.append(L.mat_driftwood("driftwood", base="#a08568", dark="#4a3a2a", light="#d2bfa2", moss=0.0))
    L.bake(ob, size=512, metal=False)
    parent(ob, r)
    L.export_tree("driftwood", r)


def build_seaweed():
    r = root("seaweed")
    m = mat_leaf("seaweed", base="#2e3a14", tip="#5a6a22", dry="#3a3418")
    parts = []
    for k in range(9):
        a = random.uniform(0, math.tau)
        d = Vector((math.cos(a), math.sin(a), 0))
        parts.append(frond(f"blade{k}", Vector((random.uniform(-0.1, 0.1), random.uniform(-0.1, 0.1), 0.02)), d, random.uniform(0.35, 0.6), 0.05, 0.25, m))
    ob = join("Seaweed", parts)
    L.bake(ob, size=512, metal=False)
    parent(ob, r)
    L.export_tree("seaweed", r)


def build_morsel():
    """A small dead fish washed up on the sand: food."""
    r = root("morsel")
    body = lathe("fish", [(0.0, -0.11), (0.018, -0.09), (0.03, -0.04), (0.032, 0.02), (0.022, 0.07), (0.008, 0.1), (0.0, 0.105)], steps=20)
    for v in body.data.vertices:
        v.co = Vector((v.co.x * 0.6, v.co.z, v.co.y + 0.03))
    tail = L.from_bmesh("tail", bmesh.new())
    bm = bmesh.new()
    a, b, c = bm.verts.new((0, 0.1, 0.03)), bm.verts.new((0, 0.16, 0.06)), bm.verts.new((0, 0.16, 0.0))
    bm.faces.new((a, b, c))
    tail = L.from_bmesh("tail", bm)
    L.mod(tail, "SOLIDIFY", thickness=0.004)
    t = NT("morsel")
    col = t.ramp(t.sep(t.geo("Normal"))[2], [(-0.5, C("#e8e4dc")), (0.4, C("#9aa6aa")), (0.9, C("#4a5a66"))])
    m = t.finish(col, rough=0.3, metal=0.2)
    for o in (body, tail):
        o.data.materials.append(m)
    ob = join("Morsel", [body, tail])
    L.bake(ob, size=256)
    parent(ob, r)
    L.export_tree("morsel", r)


def build_gull():
    """A seagull in rigid parts: Body > Head, WingL, WingR, Tail (wings hinge at the shoulders)."""
    m_white = L.mat_plain("gull_white", "#f1f0ea", rough=0.7)
    m_grey = L.mat_plain("gull_grey", "#9aa3aa", rough=0.7)
    m_black = L.mat_plain("gull_black", "#1a1a1c", rough=0.6)
    m_beak = L.mat_plain("gull_beak", "#e8b82a", rough=0.4)
    body = uv_sphere("Body", 1.0, (0, 0, 0), scale=(0.11, 0.26, 0.12), seg=20, rings=12)
    for v in body.data.vertices:
        v.co.z += 0.0
    body.data.materials.append(m_white)
    head = uv_sphere("head", 0.075, (0, -0.27, 0.07), seg=16, rings=10)
    head.data.materials.append(m_white)
    beak = tube("beak", [((0, -0.33, 0.065), 0.018), ((0, -0.4, 0.06), 0.012), ((0, -0.43, 0.05), 0.003)], seg=8, sub=2)
    beak.data.materials.append(m_beak)
    eyes = [uv_sphere("eye", 0.012, (sx * 0.05, -0.3, 0.09), seg=8, rings=6) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(m_black)
    head = join("Head", [head, beak] + eyes)
    set_origin(head, (0, -0.2, 0.05))
    parent(head, body)
    tail = L.from_bmesh("t", bmesh.new())
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in ((-0.06, 0.2, 0.02), (0.06, 0.2, 0.02), (0.08, 0.38, 0.0), (-0.08, 0.38, 0.0))]
    bm.faces.new(vs)
    tail = L.from_bmesh("Tail", bm)
    L.mod(tail, "SOLIDIFY", thickness=0.01)
    L.apply_all(tail)
    tail.data.materials.append(m_white)
    set_origin(tail, (0, 0.2, 0.02))
    parent(tail, body)
    parts = [body, head, tail]
    for side, sx in (("L", 1), ("R", -1)):
        bm = bmesh.new()
        prof = [(0.0, -0.06), (0.25, -0.1), (0.55, -0.02), (0.75, 0.06), (0.55, 0.12), (0.25, 0.12), (0.0, 0.1)]
        vs = [bm.verts.new((sx * (0.08 + x), y, 0.03 + 0.04 * x)) for x, y in prof]
        bm.faces.new(vs if sx > 0 else list(reversed(vs)))
        w = L.from_bmesh("Wing" + side, bm)
        L.mod(w, "SOLIDIFY", thickness=0.012)
        L.apply_all(w)
        w.data.materials.append(m_grey)
        # black tips: a second material on the outer third
        w.data.materials.append(m_black)
        for p in w.data.polygons:
            if abs(p.center.x) > 0.6:
                p.material_index = 1
        set_origin(w, (sx * 0.08, 0.02, 0.03))
        parent(w, body)
        parts.append(w)
    L.bake_group(parts, "gull", size=512, ao_dist=0.05)
    L.export_tree("gull", body)


# ----------------------------------------------------------------------------- Limbo
def build_house(variant=0):
    name = f"house{variant}"
    r = root(name)
    m = ruin_stone(f"{name}_stone")
    W, D, H, T = (6.0, 5.0, 4.4, 0.42) if variant == 0 else (5.0, 6.5, 6.2, 0.45)
    walls = []
    door = (0.0, 0.0, 1.3, 1.6, True)
    win = lambda x: (x, 1.3, 0.9, 1.0, variant == 1)  # noqa: E731
    front = wall("front", W, H, T, m, holes=[door, win(-1.8), win(1.8)], seed=1 + variant * 10, broken=0.55, keep_left=0.6)
    place(front, (0, -D / 2, 0))
    back = wall("back", W, H, T, m, holes=[win(-1.5), win(1.5)], seed=2 + variant * 10, broken=0.75)
    place(back, (0, D / 2, 0), (0, 0, math.pi))
    left = wall("left", D - T, H * (1.15 if variant == 1 else 1.0), T, m, holes=[win(0.0)], seed=3 + variant * 10, broken=0.35, keep_left=0.8)
    place(left, (-W / 2 + T / 2, 0, 0), (0, 0, math.pi / 2))
    right = wall("right", D - T, H, T, m, holes=[win(-0.8)], seed=4 + variant * 10, broken=0.9)
    place(right, (W / 2 - T / 2, 0, 0), (0, 0, -math.pi / 2))
    walls = [front, back, left, right]
    # the collapsed corner and the rubble it left
    rub = []
    m_r = ruin_stone(f"{name}_rubble", moss=0.4)
    for k in range(9):
        b = L.rock_obj("rb", random.uniform(0.2, 0.45), 40 + k + variant * 9, (1.2, 1.0, 0.7), 0.3, m_r, sub=3)
        place(b, (W / 2 - random.uniform(0.3, 1.6), random.uniform(-D / 2 + 0.6, D / 2 - 0.6) * 0.6, 0.12), (0, 0, random.uniform(0, 3)))
        rub.append(b)
    m_w = L.mat_wood(f"{name}_beam", base="#4a4038", dark="#1e1a16", light="#6e645a", rings=6, along="X")
    beams = []
    for k in range(3):
        y = -D / 2 + 1.0 + k * 1.4
        bm_ = sweep("beam", [[((-W / 2 + 0.3, y, H * 0.85), 0.11), ((0.0, y + 0.1, H * 0.8 - 0.4 * k), 0.11), ((W / 2 - 1.2, y + 0.2, 0.2 + 0.8 * k), 0.1)]], seg=6, sub=2)
        bm_.data.materials.append(m_w)
        beams.append(bm_)
    ob = join("House", walls + rub + beams)
    L.bake(ob, size=2048, metal=False)
    parent(ob, r)
    L.export_tree(name, r)


def build_tower():
    r = root("tower")
    m = ruin_stone("tower_stone", base="#767a78", dark="#2a2e2d", light="#9ea29f", moss=0.2)
    S, H, T = 4.0, 11.0, 0.55
    walls = []
    for k in range(4):
        holes = [(0.0, 7.0, 1.2, 1.6, True)]
        if k == 0:
            holes.append((0.0, 0.0, 1.6, 2.2, True))
        else:
            holes.append((0.0, 3.2, 0.5, 1.4, True))
        w = wall(f"w{k}", S, H, T, m, holes=holes, seed=50 + k, broken=0.25 + 0.2 * (k % 2), keep_left=0.5)
        a = k * math.pi / 2
        place(w, (math.sin(a) * (S / 2 - T / 2) * 0 + (-math.sin(a)) * 0, 0, 0))
        place(w, (0, 0, 0), (0, 0, a))
        w.location = (math.sin(a) * (S / 2 - T / 2), -math.cos(a) * (S / 2 - T / 2), 0)
        L.transform_apply(w)
        walls.append(w)
    # a buttress at each corner
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        bpy.ops.mesh.primitive_cube_add(size=1, location=(math.cos(a) * S * 0.72, math.sin(a) * S * 0.72, 2.5))
        b = bpy.context.active_object
        b.scale = (0.7, 0.7, 5.0)
        b.rotation_euler = (0, 0, a)
        L.mod(b, "BEVEL", width=0.08, segments=2)
        b.data.materials.append(m)
        walls.append(b)
    ob = join("Tower", walls)
    L.bake(ob, size=2048, metal=False)
    parent(ob, r)
    L.export_tree("tower", r)


def build_wall():
    r = root("wall")
    m = ruin_stone("wall_stone")
    w = wall("Wall", 7.0, 3.6, 0.5, m, holes=[(0.0, 0.0, 2.0, 1.8, False)], seed=70, broken=0.5, keep_left=0.4)
    L.bake(w, size=1024, metal=False)
    parent(w, r)
    L.export_tree("wall", r)


def build_column():
    r = root("column")
    m = ruin_stone("column_stone", base="#8a8c88", dark="#3a3c3a", light="#b0b2ad", moss=0.3)
    c = L.column("Column", 3.4, 0.38, m, broken=True, seed=5)
    L.bake(c, size=1024, metal=False)
    parent(c, r)
    L.export_tree("column", r)


def build_rubble():
    """Mineral source: a heap of broken masonry."""
    r = root("rubble")
    m = ruin_stone("rubble_stone", moss=0.15)
    parts = []
    for k in range(14):
        s = random.uniform(0.18, 0.45)
        b = L.rock_obj("rb", s, 60 + k, (1.3, 1.0, 0.8), 0.18, m, sub=3, detail=0.03)
        a, d = random.uniform(0, math.tau), random.uniform(0, 0.8) * (1 - k / 20)
        place(b, (math.cos(a) * d, math.sin(a) * d, 0.1 + (14 - k) * 0.035), (random.uniform(-0.3, 0.3), random.uniform(-0.3, 0.3), random.uniform(0, 3)))
        parts.append(b)
    ob = join("Rubble", parts)
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("rubble", r)


def build_planks():
    """Organic source: broken boards and a split barrel."""
    r = root("planks")
    m = L.mat_wood("planks_wood", base="#5e5246", dark="#221c16", light="#857868", rings=10, along="X", rough=0.9)
    parts = []
    for k in range(8):
        bpy.ops.mesh.primitive_cube_add(size=1)
        b = bpy.context.active_object
        L_ = random.uniform(1.0, 1.8)
        b.scale = (L_, 0.2, 0.04)
        L.mod(b, "BEVEL", width=0.01, segments=1)
        L.apply_all(b)
        for v in b.data.vertices:   # splintered ends
            if abs(v.co.x) > 0.45:
                v.co.x += noise.noise(Vector((v.co.y * 20, k, 0))) * 0.06
        place(b, (random.uniform(-0.4, 0.4), random.uniform(-0.4, 0.4), 0.05 + k * 0.05),
              (random.uniform(-0.15, 0.15), random.uniform(-0.2, 0.2), random.uniform(0, math.pi)))
        b.data.materials.append(m)
        parts.append(b)
    ob = join("Planks", parts)
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("planks", r)


def build_scrap():
    """Metal source: pipes, bent plates, a cart wheel and a can, rusting together."""
    r = root("scrap")
    m = L.mat_metal("scrap_iron", base="#5a5856", patina="#7a3a18", patina_amt=0.7, rough=0.5, metal_max=0.85)
    m2 = L.mat_metal("scrap_tin", base="#8a8a86", patina="#6a3a1c", patina_amt=0.5, rough=0.4, metal_max=0.9)
    parts = []
    for k in range(5):
        p = sweep("pipe", [[((-0.6, 0, 0), 0.05), ((0.0, 0.05 * k, 0.1), 0.05), ((0.6, 0.0, 0.0), 0.05)]], seg=10, sub=2)
        place(p, (random.uniform(-0.3, 0.3), random.uniform(-0.3, 0.3), 0.06 + k * 0.08), (0, random.uniform(-0.3, 0.3), random.uniform(0, math.pi)))
        p.data.materials.append(m)
        parts.append(p)
    for k in range(4):
        bpy.ops.mesh.primitive_grid_add(x_subdivisions=6, y_subdivisions=6, size=1)
        g = bpy.context.active_object
        g.scale = (random.uniform(0.6, 1.0), random.uniform(0.5, 0.8), 1)
        L.apply_all(g)
        L.transform_apply(g)
        for v in g.data.vertices:
            v.co.z += 0.12 * math.sin(v.co.x * 3 + k) * math.cos(v.co.y * 2)
        L.mod(g, "SOLIDIFY", thickness=0.015)
        L.apply_all(g)
        place(g, (random.uniform(-0.4, 0.4), random.uniform(-0.4, 0.4), 0.15 + k * 0.1), (random.uniform(-0.5, 0.5), random.uniform(-0.5, 0.5), random.uniform(0, 3)))
        g.data.materials.append(m2)
        parts.append(g)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.4, minor_radius=0.035, location=(0.3, 0.2, 0.5), rotation=(1.2, 0.3, 0))
    wh = bpy.context.active_object
    wh.data.materials.append(m)
    parts.append(wh)
    for k in range(6):
        a = k / 6 * math.tau
        s = tube("spoke", [(Vector((0.3, 0.2, 0.5)), 0.015), (Vector((0.3 + math.cos(a) * 0.38, 0.2 + math.sin(a) * 0.38 * math.cos(1.2), 0.5 + math.sin(a) * 0.38 * math.sin(1.2))), 0.015)], seg=6, sub=1)
        s.data.materials.append(m)
        parts.append(s)
    ob = join("Scrap", parts)
    L.bake(ob, size=1024)
    parent(ob, r)
    L.export_tree("scrap", r)


def build_deadtree():
    r = root("deadtree")
    paths = [[((0, 0, 0), 0.3), ((0.1, 0, 1.2), 0.24), ((-0.1, 0.1, 2.4), 0.18), ((0.2, 0.0, 3.4), 0.12), ((0.5, -0.1, 4.2), 0.05)]]
    for k in range(6):
        h = random.uniform(1.6, 3.4)
        a = random.uniform(0, math.tau)
        d = Vector((math.cos(a), math.sin(a), 0))
        b = Vector((0, 0, h))
        L_ = random.uniform(1.0, 1.8)
        paths.append([(tuple(b), 0.1), (tuple(b + d * L_ * 0.5 + UP * 0.4), 0.06), (tuple(b + d * L_ + UP * random.uniform(0.2, 0.9)), 0.015)])
    roots_ = []
    for k in range(5):
        a = k / 5 * math.tau
        d = Vector((math.cos(a), math.sin(a), 0))
        roots_.append([((0, 0, 0.3), 0.18), (tuple(d * 0.6 + UP * 0.05), 0.1), (tuple(d * 1.1 - UP * 0.1), 0.03)])
    ob = sweep("DeadTree", paths + roots_, seg=10, sub=4)
    ob.data.materials.append(L.mat_driftwood("deadtree_wood", base="#5c5852", dark="#1e1c1a", light="#86827a", moss=0.1))
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("deadtree", r)


def build_lamp():
    r = root("lamp")
    m = L.mat_metal("lamp_iron", base="#2e2e2e", patina="#5a3a20", patina_amt=0.5, rough=0.5, metal_max=0.8)
    post = lathe("post", [(0.18, 0.0), (0.16, 0.25), (0.08, 0.35), (0.06, 3.0), (0.09, 3.1), (0.0, 3.12)], steps=16)
    arm = tube("arm", [((0, 0, 3.0), 0.04), ((0.3, 0, 3.25), 0.035), ((0.62, 0, 3.15), 0.03)], seg=8, sub=2)
    cage = lathe("cage", [(0.0, -0.05), (0.14, 0.0), (0.18, 0.25), (0.12, 0.35), (0.0, 0.38)], steps=8)
    place(cage, (0.62, 0, 2.75), (0.3, 0.2, 0))
    for o in (post, arm, cage):
        o.data.materials.append(m)
    ob = join("Lamp", [post, arm, cage])
    place(ob, (0, 0, 0), (0.06, 0.04, 0))
    L.bake(ob, size=512)
    parent(ob, r)
    L.export_tree("lamp", r)


def build_beacon():
    """Checkpoint: a stone brazier; the green fire's core is a Glow part."""
    r = root("beacon")
    m = ruin_stone("beacon_stone", base="#6e7270", moss=0.1)
    ped = lathe("ped", [(0.55, 0.0), (0.5, 0.2), (0.32, 0.3), (0.26, 0.9), (0.4, 1.0), (0.62, 1.15), (0.66, 1.3), (0.52, 1.32), (0.0, 1.2)], steps=24)
    ped.data.materials.append(m)
    L.bake(ped, size=512, metal=False)
    ped.name = "Beacon"
    parent(ped, r)
    core = uv_sphere("GlowFire", 0.28, (0, 0, 1.42), scale=(1, 1, 1.3), seg=14, rings=10)
    core.data.materials.append(glow("beacon_fire", "#7dffb2", 8.0))
    parent(core, ped)
    L.export_tree("beacon", r)


def build_rift():
    """The frame of a rift: jagged black stones standing in an upright oval (the opening is
    drawn by the game). The opening faces Blender -Y (Unity +Z)."""
    r = root("rift")
    m = L.mat_basalt("rift_basalt", moss=0.0)
    parts = []
    n = 13
    for k in range(n):
        a = k / n * math.tau
        x, z = math.cos(a) * 1.35, 1.75 + math.sin(a) * 1.75
        s = random.uniform(0.32, 0.5) * (1.25 if z < 0.6 else 1.0)
        b = L.craggy("rk", s, 80 + k, (0.8, 0.9, 1.3), m)
        place(b, (x, 0, max(z, s * 0.5)), (random.uniform(-0.3, 0.3), 0, -a + math.pi / 2))
        parts.append(b)
    ob = join("Rift", parts)
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("rift", r)


# ----------------------------------------------------------------------------- the second circle: Wrath
def charred_stone(name):
    """Masonry blackened by fire, the joints still glowing faintly."""
    return L.mat_stone(name, base="#3a302c", dark="#1c1614", light="#5a4a42", block=1.7, stretch=(1, 1, 2.2), mortar=0.03, moss=0.0, dirt=0.9)


def mat_magma(name):
    t = NT(name)
    n = t.noise(t.vec(), 1.2, detail=6, rough=0.7, distortion=0.4)
    col = t.ramp(n, [(0.25, C("#100a08")), (0.55, C("#2c2220")), (0.85, C("#4a3a32"))])
    cr = t.voronoi(t.vec(), 1.5, feature="DISTANCE_TO_EDGE")
    col = t.mix(t.ramp(cr, [(0.0, 1.0), (0.035, 0.0)]), col, C("#ff6a10"))
    col = t.mix(t.math("MULTIPLY", t.ramp(cr, [(0.0, 1.0), (0.08, 0.0)]), 0.45), col, C("#8a2408"))
    return t.finish(col, rough=0.8, normal=t.bump(n, 1.0, 0.05))


def build_volcano():
    """A volcano for the horizon: a jagged cone with a glowing crater and lava streaks."""
    r = root("volcano")
    m = mat_magma("volcano_rock")
    cone = lathe("cone", [(60, 0), (48, 6), (30, 20), (16, 34), (11, 40), (9, 39), (0, 33)], steps=64)
    L.mod(cone, "SUBSURF", levels=2)
    L.apply_all(cone)
    for v in cone.data.vertices:
        nz = noise.noise(v.co * 0.08) * 6 + noise.noise(v.co * 0.25) * 1.5
        d = Vector((v.co.x, v.co.y, 0))
        if d.length > 1:
            v.co += d.normalized() * nz * (1 - v.co.z / 45)
    cone.data.materials.append(m)
    L.bake(cone, size=1024, metal=False)
    cone.name = "Volcano"
    parent(cone, r)
    crater = uv_sphere("GlowCrater", 9.5, (0, 0, 37), scale=(1, 1, 0.12), seg=24, rings=10)
    crater.data.materials.append(glow("crater", "#ff7a1a", 20.0))
    parent(crater, cone)
    # lava running down from the crater: glowing tubes that follow the slope (a ray down onto the cone)
    bvh = BVHTree.FromObject(cone, bpy.context.evaluated_depsgraph_get())
    streaks = []
    for k in range(6):
        a = k / 6 * math.tau + random.uniform(-0.3, 0.3)
        pts = []
        for j in range(14):
            f = j / 13
            rr = 9 + f * 50
            aa = a + math.sin(f * 7 + k) * 0.06
            x, y = math.cos(aa) * rr, math.sin(aa) * rr
            hit = bvh.ray_cast(Vector((x, y, 60)), Vector((0, 0, -1)))
            if hit[0] is None:
                break
            pts.append((hit[0] + hit[1] * 0.4, 0.5 + f * 1.4 * (1 - f * 0.6)))
        if len(pts) > 3:
            streaks.append(tube("streak", pts, seg=8, sub=2))
    if streaks:
        st = join("GlowStreaks", streaks)
        st.data.materials.append(glow("lava_streak", "#ff5a14", 8.0))
        parent(st, cone)
    L.export_tree("volcano", r)


def build_spires():
    r = root("spires")
    m = L.mat_basalt("obsidian", moss=0.0)
    for k in range(4):
        h = random.uniform(3.5, 8)
        ob = L.craggy(f"Spire{k}", 1.0, 90 + k, (0.6, 0.6, h / 2), m)
        for v in ob.data.vertices:   # taper to a point
            f = max(0.15, 1 - max(0.0, v.co.z) / (h * 0.55))
            v.co.x *= f
            v.co.y *= f
        place(ob, (0, 0, h * 0.45))
        L.bake(ob, size=512, metal=False)
        parent(ob, r)
    for k in range(3):
        ob = L.craggy(f"Rock{k}", random.uniform(0.8, 1.6), 100 + k, (1.2, 1.0, 0.7), m)
        L.bake(ob, size=512, metal=False)
        parent(ob, r)
    L.export_tree("spires", r)


def build_burnt_house():
    r = root("burnt_house")
    m = charred_stone("burnt_stone")
    W, D, H, T = 6.5, 5.5, 4.8, 0.45
    win = lambda x: (x, 1.4, 1.0, 1.1, True)  # noqa: E731
    front = wall("front", W, H, T, m, holes=[(0.0, 0.0, 1.5, 1.8, True), win(-2.0), win(2.0)], seed=201, broken=0.7, keep_left=0.5)
    place(front, (0, -D / 2, 0))
    back = wall("back", W, H, T, m, holes=[win(0)], seed=202, broken=0.8)
    place(back, (0, D / 2, 0), (0, 0, math.pi))
    left = wall("left", D - T, H, T, m, holes=[win(0.5)], seed=203, broken=0.5, keep_left=0.7)
    place(left, (-W / 2 + T / 2, 0, 0), (0, 0, math.pi / 2))
    rubble = []
    m_r = L.mat_basalt("burnt_rubble", moss=0.0)
    for k in range(10):
        b = L.rock_obj("rb", random.uniform(0.25, 0.5), 210 + k, (1.2, 1.0, 0.7), 0.3, m_r, sub=3)
        place(b, (W / 2 - random.uniform(0.3, 2.0), random.uniform(-D / 2 + 0.5, D / 2 - 0.5), 0.15))
        rubble.append(b)
    ob = join("BurntHouse", [front, back, left] + rubble)
    L.bake(ob, size=2048, metal=False)
    parent(ob, r)
    L.export_tree("burnt_house", r)


def build_burnt_wall():
    r = root("burnt_wall")
    w = wall("BurntWall", 8.0, 4.2, 0.6, charred_stone("burnt_wall_stone"), holes=[(0.0, 0.0, 2.2, 2.2, True)], seed=220, broken=0.55, keep_left=0.5)
    L.bake(w, size=1024, metal=False)
    parent(w, r)
    L.export_tree("burnt_wall", r)


def build_vent():
    """A fire vent: a ring of magma rocks around a hole in the ground (the game draws the fire)."""
    r = root("vent")
    m = mat_magma("vent_rock")
    parts = []
    n = 11
    for k in range(n):
        a = k / n * math.tau
        s = random.uniform(0.5, 0.9)
        b = L.craggy("rk", s, 300 + k, (0.8, 0.8, 1.5), m)
        place(b, (math.cos(a) * 2.0, math.sin(a) * 2.0, s * 0.6), (random.uniform(-0.3, 0.3), random.uniform(-0.3, 0.3), a))
        parts.append(b)
    ob = join("Vent", parts)
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("vent", r)


def build_totem():
    """A demon totem: a pillar of stacked stones crowned with a horned skull."""
    r = root("totem")
    m = charred_stone("totem_stone")
    mb = L.mat_plain("totem_bone", "#cbbd9a", rough=0.6)
    parts = []
    z = 0
    for k in range(4):
        s = 0.6 - 0.08 * k
        b = L.rock_obj("st", s, 320 + k, (1, 1, 0.6), 0.15, m, sub=3)
        place(b, (0, 0, z + s * 0.5), (0, 0, k))
        z += s * 0.95
        parts.append(b)
    skull = uv_sphere("skull", 0.35, (0, -0.05, z + 0.25), scale=(1, 1.15, 0.9), seg=20, rings=14)
    skull.data.materials.append(mb)
    parts.append(skull)
    for sx in (-1, 1):
        h = tube("horn", [((sx * 0.25, 0, z + 0.4), 0.09), ((sx * 0.6, 0.1, z + 0.6), 0.06), ((sx * 0.7, 0.0, z + 1.1), 0.01)], seg=8, sub=3)
        h.data.materials.append(mb)
        parts.append(h)
    ob = join("Totem", parts)
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("totem", r)


def build_ribs():
    """The ribcage of some giant beast, half buried in the sand."""
    r = root("ribs")
    m = L.mat_plain("bone", "#d6c8a4", rough=0.7)
    paths = [[((-4.5, 0, 0.3), 0.35), ((0, 0, 0.6), 0.4), ((4.5, 0, 0.3), 0.3)]]
    for k in range(9):
        x = -3.8 + k * 0.95
        for sx in (-1, 1):
            paths.append([((x, 0, 0.5), 0.14), ((x + 0.2, sx * 1.6, 2.4), 0.11), ((x + 0.4, sx * 2.6, 1.0), 0.07), ((x + 0.45, sx * 2.7, -0.3), 0.04)])
    ob = sweep("Ribs", paths, seg=8, sub=4)
    t = NT("ribs_bone")
    n = t.noise(t.vec(), 6, detail=5)
    col = t.ramp(n, [(0.3, C("#8a7a5a")), (0.7, C("#e0d4b4"))])
    ob.data.materials.append(t.finish(col, rough=0.65, normal=t.bump(n, 0.4, 0.02)))
    L.bake(ob, size=1024, metal=False)
    parent(ob, r)
    L.export_tree("ribs", r)


def build_burnt_tree():
    r = root("burnt_tree")
    paths = [[((0, 0, 0), 0.25), ((0.1, 0, 1.4), 0.2), ((-0.15, 0.1, 2.8), 0.12), ((0.1, 0.0, 3.6), 0.04)]]
    for k in range(5):
        h = random.uniform(1.4, 3.0)
        a = random.uniform(0, math.tau)
        d = Vector((math.cos(a), math.sin(a), 0))
        b = Vector((0, 0, h))
        paths.append([(tuple(b), 0.08), (tuple(b + d * 0.6 + UP * 0.3), 0.05), (tuple(b + d * 1.2 + UP * random.uniform(0.0, 0.6)), 0.012)])
    ob = sweep("BurntTree", paths, seg=8, sub=4)
    ob.data.materials.append(L.mat_driftwood("burnt_wood", base="#2a2220", dark="#0c0806", light="#4a3a32", moss=0.0))
    L.bake(ob, size=512, metal=False)
    parent(ob, r)
    L.export_tree("burnt_tree", r)



# ---------------------------------------------------------------------- III. the circle of Greed
def mat_gold(name, rough=0.3, patina=0.22):
    """Polished gold, darker and duller in the cavities. Only half metallic: pure metal shows the
    sky, and the skies of hell are dark — this keeps it reading as gold under any of them."""
    return L.mat_metal(name, base="#f0b840", patina="#6a4612", patina_amt=patina, rough=rough, metal_max=0.5)


def gold_stone(name):
    """Pale golden sandstone in big dressed blocks (the city's walls)."""
    return L.mat_stone(name, base="#c4a46a", dark="#8a6e40", light="#e2cc96", block=1.6, stretch=(1, 1, 2.4), mortar=0.025, moss=0.0, dirt=0.45, rough=0.7)


def box(name, size, loc, mat, bevel=0.03, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    b = bpy.context.active_object
    b.name = name
    b.scale = size
    L.apply_all(b)
    L.transform_apply(b)
    if bevel:
        L.mod(b, "BEVEL", width=bevel, segments=2)
        L.apply_all(b)
    b.data.materials.append(mat)
    return b


def onion(name, r, z, mat, h=None):
    """An onion dome with a spire, standing at height z."""
    h = h or r * 1.6
    prof = [(r * 0.9, 0), (r, h * 0.12), (r * 1.08, h * 0.35), (r * 0.9, h * 0.6), (r * 0.45, h * 0.85), (r * 0.12, h * 0.97), (0.05, h * 1.05), (0.05, h * 1.4), (0.0, h * 1.45)]
    d = lathe(name, [(x, y + z) for x, y in prof], steps=32)
    L.shade_smooth(d, 60)
    d.data.materials.append(mat)
    return d


def merlons(length, z, thick, mat, n=None):
    n = n or max(2, int(length / 1.1))
    out = []
    for k in range(n):
        x = -length / 2 + (k + 0.5) * length / n
        out.append(box("merlon", (length / n * 0.55, thick, 0.6), (x, 0, z + 0.3), mat, 0.02))
    return out


def build_gold_wall():
    """A segment of the fortress wall: dressed golden stone, a gilded band and battlements."""
    r = root("gold_wall")
    m = gold_stone("gwall_stone")
    mg = mat_gold("gwall_gold")
    L_, H, T = 10.0, 7.0, 1.4
    w = wall("w", L_, H, T, m, holes=[(-2.5, 4.5, 0.5, 1.2, True), (2.5, 4.5, 0.5, 1.2, True)], seed=301, broken=0.0)
    parts = [w] + merlons(L_, H, T * 0.9, m)
    parts.append(box("band", (L_ + 0.02, T + 0.12, 0.3), (0, 0, H - 0.6), mg, 0.03))
    parts.append(box("plinth", (L_ + 0.02, T + 0.3, 0.6), (0, 0, 0.3), m, 0.05))
    ob = join("GoldWall", parts)
    L.bake(ob, size=1024)
    parent(ob, r)
    L.export_tree("gold_wall", r)


def build_gold_tower():
    r = root("gold_tower")
    m = gold_stone("gtower_stone")
    mg = mat_gold("gtower_gold")
    body = lathe("body", [(3.0, 0), (2.8, 0.8), (2.5, 1.0), (2.4, 11.0), (2.9, 11.4), (2.9, 12.2), (0.0, 12.2)], steps=40)
    body.data.materials.append(m)
    parts = [body]
    for k in range(12):
        a = k / 12 * math.tau
        parts.append(box("merlon", (0.7, 0.6, 0.8), (math.cos(a) * 2.6, math.sin(a) * 2.6, 12.6), m, 0.02, rot=(0, 0, a)))
    bpy.ops.mesh.primitive_torus_add(major_radius=2.45, minor_radius=0.12, location=(0, 0, 8.0))
    band = bpy.context.active_object
    band.data.materials.append(mg)
    parts.append(band)
    parts.append(onion("dome", 1.8, 12.2, mg, 3.4))
    for k in range(4):
        a = k / 4 * math.tau + 0.4
        win = arch_prism("win", 0.5, 1.0, 1.4, True)
        win.location = (math.cos(a) * 2.4, math.sin(a) * 2.4, 6.0)
        win.rotation_euler = (0, 0, a + math.pi / 2)
        L.transform_apply(win)
        L.boolean(body, win)
    ob = join("GoldTower", parts)
    L.bake(ob, size=1024)
    parent(ob, r)
    L.export_tree("gold_tower", r)


def build_gold_house():
    """A rich house: a cube of golden stone, arched windows, a gilded onion dome."""
    r = root("gold_house")
    m = gold_stone("ghouse_stone")
    mg = mat_gold("ghouse_gold")
    W, D, H, T = 6.0, 6.0, 5.5, 0.5
    win = lambda x: (x, 1.6, 0.9, 1.4, True)  # noqa: E731
    walls = [place(wall("front", W, H, T, m, holes=[(0, 0, 1.4, 2.0, True), win(-1.8), win(1.8)], seed=311, broken=0.0), (0, -D / 2, 0)),
             place(wall("back", W, H, T, m, holes=[win(-1.5), win(1.5)], seed=312, broken=0.0), (0, D / 2, 0), (0, 0, math.pi)),
             place(wall("left", D - T, H, T, m, holes=[win(0)], seed=313, broken=0.0), (-W / 2 + T / 2, 0, 0), (0, 0, math.pi / 2)),
             place(wall("right", D - T, H, T, m, holes=[win(0)], seed=314, broken=0.0), (W / 2 - T / 2, 0, 0), (0, 0, -math.pi / 2))]
    parts = walls + [box("roof", (W + 0.4, D + 0.4, 0.4), (0, 0, H + 0.2), m, 0.05), box("cornice", (W + 0.6, D + 0.6, 0.18), (0, 0, H - 0.1), mg, 0.03)]
    parts.append(onion("dome", 2.2, H + 0.4, mg, 3.6))
    ob = join("GoldHouse", parts)
    L.bake(ob, size=1024)
    parent(ob, r)
    L.export_tree("gold_house", r)


def build_gold_gate():
    """A great gate: two towers of golden stone, an arch between them and two gilded doors on
    hinges (DoorL, DoorR: origins at the hinges), closed."""
    r = root("gold_gate")
    m = gold_stone("ggate_stone")
    mg = mat_gold("ggate_gold", rough=0.22)
    W, H = 6.0, 7.5
    frame = wall("frame", 14.0, 11.0, 2.2, m, holes=[(0, 0, W, H - W / 2, False)], seed=321, broken=0.0)
    parts = [frame] + merlons(14.0, 11.0, 2.0, m)
    for sx in (-1, 1):
        parts.append(box("pylon", (2.2, 2.8, 12.5), (sx * 6.2, 0, 6.25), m, 0.05))
        parts.append(onion("dome", 1.3, 12.5, mg, 2.4))
        parts[-1].location.x = sx * 6.2
        L.transform_apply(parts[-1])
    parts.append(box("band", (14.1, 2.35, 0.35), (0, 0, 10.2), mg, 0.03))
    ob = join("Gate", parts)
    L.bake(ob, size=1024)
    parent(ob, r)
    doors = []
    for side, sx in (("L", -1), ("R", 1)):
        leaf = box("leaf", (W / 2 - 0.05, 0.25, H - 0.1), (sx * W / 4, 0, (H - 0.1) / 2), mg, 0.03)
        studs = []
        for i in range(3):
            for j in range(5):
                studs.append(uv_sphere("stud", 0.09, (sx * (0.45 + i * 0.85), -0.15, 0.8 + j * 1.3), seg=10, rings=6))
        for st in studs:
            st.data.materials.append(mg)
        ring = tube("ring", [((sx * 0.35, -0.2, 3.3), 0.05), ((sx * 0.35, -0.3, 3.0), 0.05), ((sx * 0.35, -0.2, 2.7), 0.05)], seg=8, sub=2)
        ring.data.materials.append(mg)
        d = join("Door" + side, [leaf, ring] + studs)
        L.bake(d, size=512)
        set_origin(d, (sx * W / 2, 0, 0))
        parent(d, r)
    L.export_tree("gold_gate", r)


def build_gold_pile():
    """Heaps of coins and goblets (gold sources): Pile0..2."""
    r = root("gold_pile")
    mg = mat_gold("pile_gold", rough=0.3, patina=0.3)
    for k, (rad, h) in enumerate(((0.9, 0.5), (1.3, 0.75), (0.6, 0.35))):
        heap = L.blob("heap", rad, seed=330 + k, squash=(1, 1, h / rad), rough=0.08, sub=4)
        heap.data.materials.append(mg)
        parts = [heap]
        for i in range(int(18 * rad)):
            a = random.uniform(0, math.tau)
            d = random.uniform(0.1, rad * 0.95)
            z = h * max(0.05, 1 - (d / rad) ** 2) * 0.95
            bpy.ops.mesh.primitive_cylinder_add(vertices=14, radius=0.07, depth=0.015, location=(math.cos(a) * d, math.sin(a) * d, z),
                                                rotation=(random.uniform(-0.6, 0.6), random.uniform(-0.6, 0.6), 0))
            c = bpy.context.active_object
            c.data.materials.append(mg)
            parts.append(c)
        if k != 2:
            g = lathe("goblet", [(0.09, 0), (0.03, 0.03), (0.02, 0.15), (0.1, 0.22), (0.12, 0.34), (0.11, 0.34), (0.0, 0.24)], steps=16)
            g.location = (rad * 0.3, -rad * 0.2, h * 0.7)
            g.rotation_euler = (0.4, 0.2, 0)
            L.transform_apply(g)
            g.data.materials.append(mg)
            parts.append(g)
        ob = join(f"Pile{k}", parts)
        L.bake(ob, size=512)
        parent(ob, r)
    L.export_tree("gold_pile", r)


def build_crystal():
    """Crystal clusters (crystal sources): a rock base (Crystal0..2) with glowing shards (GlowShards)."""
    r = root("crystal")
    mb = L.mat_basalt("crystal_rock", moss=0.0)
    gm = glow("crystal_glow", "#4ad8ff", 3.5)
    for k in range(3):
        base = L.craggy("base", 0.6 + 0.2 * k, 340 + k, (1.2, 1.0, 0.5), mb)
        L.bake(base, size=512, metal=False)
        base.name = f"Crystal{k}"
        parent(base, r)
        shards = []
        for i in range(5 + k * 2):
            a = random.uniform(0, math.tau)
            tilt = random.uniform(0.0, 0.6)
            ln = random.uniform(0.6, 1.5) * (1 + 0.3 * k)
            rad = random.uniform(0.08, 0.16)
            bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=rad, radius2=rad * 0.7, depth=ln, location=(0, 0, ln / 2))
            sh = bpy.context.active_object
            bm_ = sh.data
            for v in bm_.vertices:   # a pointed tip
                if v.co.z > ln / 2 - 1e-4:
                    v.co.x *= 0.15
                    v.co.y *= 0.15
                    v.co.z += rad * 1.6
            sh.rotation_euler = (math.cos(a) * tilt, math.sin(a) * tilt, random.uniform(0, 1))
            sh.location = (math.cos(a) * random.uniform(0, 0.35), math.sin(a) * random.uniform(0, 0.35), 0.15)
            L.transform_apply(sh)
            sh.data.materials.append(gm)
            shards.append(sh)
        g = join("GlowShards", shards)
        parent(g, base)
    L.export_tree("crystal", r)


def build_pylon():
    """An energy pylon (energy source): a gilded post with rings and a violet core (GlowCore)."""
    r = root("pylon")
    m = gold_stone("pylon_stone")
    mg = mat_gold("pylon_gold")
    parts = [lathe("post", [(0.6, 0), (0.55, 0.3), (0.3, 0.45), (0.25, 2.2), (0.42, 2.4), (0.42, 2.5), (0, 2.5)], steps=24)]
    parts[0].data.materials.append(m)
    for z in (1.0, 1.6):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.3, minor_radius=0.05, location=(0, 0, z))
        t = bpy.context.active_object
        t.data.materials.append(mg)
        parts.append(t)
    for k in range(3):
        a = k / 3 * math.tau
        c = tube("claw", [((math.cos(a) * 0.35, math.sin(a) * 0.35, 2.45), 0.06), ((math.cos(a) * 0.5, math.sin(a) * 0.5, 2.9), 0.05), ((math.cos(a) * 0.25, math.sin(a) * 0.25, 3.4), 0.02)], seg=8, sub=2)
        c.data.materials.append(mg)
        parts.append(c)
    ob = join("Pylon", parts)
    L.bake(ob, size=512)
    parent(ob, r)
    core = uv_sphere("GlowCore", 0.26, (0, 0, 2.95), seg=16, rings=12)
    core.data.materials.append(glow("pylon_core", "#b07aff", 9.0))
    parent(core, ob)
    L.export_tree("pylon", r)


def build_obelisk():
    """A gilded obelisk with a coin emblem: the city's monuments to wealth."""
    r = root("obelisk")
    m = gold_stone("obelisk_stone")
    mg = mat_gold("obelisk_gold")
    parts = [box("base", (2.0, 2.0, 0.8), (0, 0, 0.4), m, 0.05), box("step", (1.5, 1.5, 0.4), (0, 0, 1.0), m, 0.04)]
    sh = lathe("shaft", [(0.62, 0), (0.45, 6.5), (0.0, 7.3)], steps=4)
    sh.location = (0, 0, 1.2)
    sh.rotation_euler = (0, 0, math.pi / 4)
    L.transform_apply(sh)
    sh.data.materials.append(m)
    parts.append(sh)
    tip = lathe("tip", [(0.47, 0), (0.0, 0.8)], steps=4)
    tip.location = (0, 0, 7.7)
    tip.rotation_euler = (0, 0, math.pi / 4)
    L.transform_apply(tip)
    tip.data.materials.append(mg)
    parts.append(tip)
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.45, depth=0.1, location=(0, -0.58, 3.5), rotation=(math.pi / 2, 0, 0))
    coin = bpy.context.active_object
    coin.data.materials.append(mg)
    parts.append(coin)
    ob = join("Obelisk", parts)
    L.bake(ob, size=512)
    parent(ob, r)
    L.export_tree("obelisk", r)


def build_gold_column():
    r = root("gold_column")
    m = gold_stone("gcol_stone")
    mg = mat_gold("gcol_gold")
    c = L.column("col", 7.0, 0.6, m)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.6, minor_radius=0.08, location=(0, 0, 5.5))
    t = bpy.context.active_object
    t.data.materials.append(mg)
    ob = join("GoldColumn", [c, t])
    L.bake(ob, size=512)
    parent(ob, r)
    L.export_tree("gold_column", r)


def build_throne():
    """The Usurer's throne: wide steps of gold, a seat for a giant, a back like a wall of coins."""
    r = root("throne")
    m = gold_stone("throne_stone")
    mg = mat_gold("throne_gold", rough=0.2)
    mr = L.mat_plain("throne_velvet", "#5a0f1a", rough=0.8)
    parts = []
    for k in range(3):
        parts.append(box("step", (12 - k * 2, 9 - k * 1.5, 0.5), (0, 0.8 + k * 0.5, 0.25 + k * 0.5), m, 0.05))
    parts.append(box("seat", (6.5, 4.5, 1.6), (0, 2.6, 2.3), mg, 0.08))
    parts.append(box("cushion", (5.6, 3.8, 0.5), (0, 2.6, 3.3), mr, 0.15))
    parts.append(box("back", (7.5, 1.2, 9.0), (0, 4.8, 5.9), mg, 0.1))
    for sx in (-1, 1):
        parts.append(box("arm", (0.9, 4.0, 1.2), (sx * 3.4, 2.6, 3.6), mg, 0.1))
        parts.append(uv_sphere("knob", 0.55, (sx * 3.4, 0.5, 4.3), seg=16, rings=10))
        parts[-1].data.materials.append(mg)
    # a crown of coins along the top of the back
    for k in range(9):
        x = -3.3 + k * 0.825
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.55, depth=0.15, location=(x, 4.6, 10.6 + 0.4 * math.cos((k - 4) * 0.5)), rotation=(math.pi / 2, 0, 0))
        c = bpy.context.active_object
        c.data.materials.append(mg)
        parts.append(c)
    ob = join("Throne", parts)
    L.bake(ob, size=1024)
    parent(ob, r)
    L.export_tree("throne", r)


def build_workshop():
    """The crab's workshop. Base: the stone floor of a ruined forge (always there). Build: what the
    crab raises from matter — a forge with a glowing mouth (GlowForge), an anvil, a workbench and a
    crane arm."""
    r = root("workshop")
    m = gold_stone("ws_stone")
    mi = L.mat_metal("ws_iron", base="#4a4846", patina="#6a3416", patina_amt=0.4, rough=0.45, metal_max=0.9)
    mw = L.mat_wood("ws_wood", base="#5a3a22", dark="#2a180a", light="#7a5838", rings=20, along="X")
    base = [box("floor", (6.0, 5.0, 0.3), (0, 0, 0.15), m, 0.05)]
    for k in range(6):
        b = L.rock_obj("rb", random.uniform(0.2, 0.4), 350 + k, (1.2, 1.0, 0.7), 0.3, m, sub=3)
        place(b, (random.uniform(-2.8, 2.8), random.uniform(-2.3, 2.3), 0.35), (0, 0, random.uniform(0, 3)))
        base.append(b)
    bo = join("Base", base)
    L.bake(bo, size=512)
    parent(bo, r)
    bl = []
    forge = box("forge", (1.8, 1.4, 1.4), (-1.8, 1.4, 1.0), m, 0.08)
    mouth = arch_prism("mouth", 0.8, 0.4, 2.0)
    mouth.location = (-1.8, 0.7, 0.6)
    L.transform_apply(mouth)
    L.boolean(forge, mouth)
    bl.append(forge)
    chim = lathe("chimney", [(0.5, 0), (0.35, 0.5), (0.3, 3.0), (0.38, 3.2), (0, 3.2)], steps=16)
    chim.location = (-1.8, 1.6, 1.7)
    L.transform_apply(chim)
    chim.data.materials.append(m)
    bl.append(chim)
    anvil = lathe("anvil_foot", [(0.3, 0), (0.18, 0.2), (0.12, 0.5), (0.0, 0.5)], steps=12)
    anvil.location = (0.6, 0.0, 0.3)
    L.transform_apply(anvil)
    anvil.data.materials.append(mi)
    bl.append(anvil)
    bl.append(box("anvil_top", (0.9, 0.35, 0.22), (0.6, 0, 0.9), mi, 0.03))
    horn = tube("horn", [((1.05, 0, 0.92), 0.13), ((1.35, 0, 0.92), 0.03)], seg=10, sub=2)
    horn.data.materials.append(mi)
    bl.append(horn)
    bl.append(box("bench", (2.2, 0.9, 0.12), (1.6, 1.8, 1.0), mw, 0.02))
    for x in (0.7, 2.5):
        for y in (1.45, 2.15):
            bl.append(box("leg", (0.1, 0.1, 0.9), (x, y, 0.5), mw, 0.01))
    for k in range(4):   # tools on the bench
        bl.append(box("tool", (0.06, 0.4, 0.05), (1.0 + k * 0.4, 1.8, 1.1), mi, 0.01, rot=(0, 0, random.uniform(-0.5, 0.5))))
    mast = tube("mast", [((2.6, -1.8, 0.3), 0.12), ((2.6, -1.8, 4.0), 0.1)], seg=10, sub=1)
    boom = tube("boom", [((2.6, -1.8, 3.8), 0.08), ((0.6, -1.2, 3.6), 0.06)], seg=8, sub=1)
    hook = tube("chain", [((0.6, -1.2, 3.6), 0.02), ((0.6, -1.2, 2.2), 0.02)], seg=6, sub=1)
    for o in (mast, boom, hook):
        o.data.materials.append(mi)
        bl.append(o)
    build = join("Build", bl)
    L.bake(build, size=1024)
    parent(build, r)
    fire = uv_sphere("GlowForge", 0.35, (-1.8, 0.8, 0.75), scale=(1.2, 0.6, 0.7), seg=14, rings=10)
    fire.data.materials.append(glow("ws_fire", "#ff8a2a", 10.0))
    parent(fire, build)
    L.export_tree("workshop", r)


def build_scales():
    """Giant scales of gold (decoration of the vault)."""
    r = root("scales")
    mg = mat_gold("scales_gold")
    m = gold_stone("scales_stone")
    parts = [box("base", (1.6, 1.6, 0.6), (0, 0, 0.3), m, 0.05)]
    post = tube("post", [((0, 0, 0.6), 0.16), ((0, 0, 4.5), 0.1)], seg=12, sub=1)
    beam = tube("beam", [((-2.2, 0, 4.4), 0.08), ((2.2, 0, 4.6), 0.08)], seg=10, sub=1)
    parts += [post, beam]
    for o in (post, beam):
        o.data.materials.append(mg)
    for sx, z in ((-1, 4.4), (1, 4.6)):
        for k in range(3):
            a = k / 3 * math.tau
            c = tube("cord", [((sx * 2.2, 0, z), 0.015), ((sx * 2.2 + math.cos(a) * 0.6, math.sin(a) * 0.6, z - 2.0), 0.015)], seg=5, sub=1)
            c.data.materials.append(mg)
            parts.append(c)
        pan = lathe("pan", [(0.75, 0.15), (0.6, 0.0), (0.0, 0.0)], steps=24)
        pan.location = (sx * 2.2, 0, z - 2.05)
        L.transform_apply(pan)
        pan.data.materials.append(mg)
        parts.append(pan)
    ob = join("Scales", parts)
    L.bake(ob, size=512)
    parent(ob, r)
    L.export_tree("scales", r)


# ---------------------------------------------------------------------- IV. the circle of Envy
def mat_marble(name, base="#f2efe8", vein="#9a96a8"):
    """White marble with thin grey-violet veins, polished."""
    t = NT(name)
    n = t.noise(t.vec(), 2.5, detail=8, rough=0.6, distortion=1.5)
    veins = t.ramp(n, [(0.47, 0.0), (0.5, 1.0), (0.53, 0.0)])
    soft = t.noise(t.vec(), 9, detail=4)
    col = t.ramp(soft, [(0.2, C("#e2ded6")), (0.8, C(base))])
    col = t.mix(t.math("MULTIPLY", veins, 0.7), col, C(vein))
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.2)), 0.4), col, C("#8a8680"))
    return t.finish(col, rough=0.18)


def mat_blossom(name, a="#ffb3cf", b="#fff0f6", c="#e8679a"):
    """A canopy of blossom: pink and white specks over a soft pink."""
    t = NT(name)
    sp = t.voronoi(t.vec(), 60, out="Distance")
    dots = t.ramp(sp, [(0.0, 1.0), (0.25, 0.0)])
    n = t.noise(t.vec(), 6, detail=5)
    col = t.ramp(n, [(0.3, C(c)), (0.7, C(a))])
    col = t.mix(dots, col, C(b))
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.4)), 0.3), col, C("#b05080"))
    return t.finish(col, rough=0.7, normal=t.bump(t.math("ADD", dots, n), 0.6, 0.03))


def build_pavilion():
    """A white marble gazebo: a round stepped floor, eight columns and a shallow dome with a gilded tip."""
    r = root("pavilion")
    m = mat_marble("pav_marble")
    mg = mat_gold("pav_gold")
    parts = [lathe("floor", [(3.6, 0), (3.6, 0.25), (3.2, 0.25), (3.2, 0.5), (0, 0.5)], steps=40)]
    parts[0].data.materials.append(m)
    for k in range(8):
        a = k / 8 * math.tau
        c = L.column("col", 3.6, 0.18, m)
        c.location = (math.cos(a) * 2.7, math.sin(a) * 2.7, 0.45)
        L.transform_apply(c)
        parts.append(c)
    ring = lathe("ring", [(3.2, 4.4), (3.2, 4.8), (2.4, 4.8), (2.4, 4.4)], steps=40)
    ring.data.materials.append(m)
    dome = lathe("dome", [(3.1, 4.8), (2.9, 5.5), (2.2, 6.2), (1.2, 6.7), (0.15, 6.85), (0, 6.85)], steps=40)
    L.shade_smooth(dome, 60)
    dome.data.materials.append(m)
    tip = lathe("tip", [(0.25, 6.8), (0.2, 7.2), (0.06, 7.8), (0, 7.9)], steps=16)
    tip.data.materials.append(mg)
    ob = join("Pavilion", parts + [ring, dome, tip])
    L.bake(ob, size=1024)
    parent(ob, r)
    L.export_tree("pavilion", r)


def build_blossom_tree():
    """A blossoming tree: a pale bending trunk and a cloud of pink blossom (canopy clumps)."""
    r = root("blossom_tree")
    mb = L.mat_driftwood("blossom_bark", base="#8a7a72", dark="#3a302c", light="#c0b4aa", moss=0.0)
    mp = mat_blossom("blossom_pink")
    paths = [[((0, 0, 0), 0.22), ((0.2, 0.1, 1.2), 0.18), ((0.0, 0.3, 2.4), 0.14), ((-0.3, 0.2, 3.2), 0.08)]]
    tops = [Vector((-0.3, 0.2, 3.2))]
    for k in range(4):
        a = k / 4 * math.tau + 0.3
        b0 = Vector((0.05, 0.25, 2.0 + 0.2 * k))
        tip = b0 + Vector((math.cos(a) * 1.4, math.sin(a) * 1.4, 1.0 + 0.2 * k))
        paths.append([(b0, 0.1), ((b0 + tip) / 2 + Vector((0, 0, 0.2)), 0.07), (tip, 0.03)])
        tops.append(tip)
    trunk = sweep("trunk", paths, seg=10, sub=3)
    trunk.data.materials.append(mb)
    clumps = []
    for i, tpos in enumerate(tops):
        for j in range(3):
            c = L.blob("clump", random.uniform(0.6, 1.0), seed=400 + i * 3 + j, squash=(1, 1, 0.7), rough=0.08, sub=4)
            L.shade_smooth(c, 80)
            off = tpos + Vector((random.uniform(-0.6, 0.6), random.uniform(-0.6, 0.6), random.uniform(-0.1, 0.5)))
            for v in c.data.vertices:
                v.co += off
            c.data.materials.append(mp)
            clumps.append(c)
    ob = join("BlossomTree", [trunk] + clumps)
    L.bake(ob, size=1024, metal=False, ao_strength=0.25)
    parent(ob, r)
    L.export_tree("blossom_tree", r)


def build_flowers():
    """Clumps of flowers (Flowers0..3): stems, leaves and bright petal rings."""
    r = root("flowers")
    ms = L.mat_plain("flower_stem", "#4a8a2a", rough=0.6)
    palette = [("#ff5a8a", "#ffe04a"), ("#ffffff", "#ffd040"), ("#8a6aff", "#fff0a0"), ("#ffb03a", "#7a3a10")]
    for k in range(4):
        petal_c, eye_c = palette[k]
        mp = L.mat_plain(f"petal{k}", petal_c, rough=0.5)
        me = L.mat_plain(f"eye{k}", eye_c, rough=0.5)
        parts = []
        for i in range(9):
            a = random.uniform(0, math.tau)
            d = random.uniform(0, 0.45)
            base = Vector((math.cos(a) * d, math.sin(a) * d, 0))
            h = random.uniform(0.25, 0.5)
            top = base + Vector((random.uniform(-0.05, 0.05), random.uniform(-0.05, 0.05), h))
            st = tube("stem", [(base, 0.012), (top, 0.008)], seg=5, sub=1)
            st.data.materials.append(ms)
            parts.append(st)
            for j in range(5):
                pa = j / 5 * math.tau
                pt = uv_sphere("petal", 0.035, top + Vector((math.cos(pa) * 0.04, math.sin(pa) * 0.04, 0)), scale=(1.4, 1.4, 0.3), seg=8, rings=5)
                pt.data.materials.append(mp)
                parts.append(pt)
            e = uv_sphere("eye", 0.02, top + Vector((0, 0, 0.008)), seg=8, rings=5)
            e.data.materials.append(me)
            parts.append(e)
        ob = join(f"Flowers{k}", parts)
        L.bake(ob, size=256, metal=False)
        parent(ob, r)
    L.export_tree("flowers", r)


def build_marble_arch():
    """A triumphal arch of white marble with a gilded band."""
    r = root("marble_arch")
    m = mat_marble("arch_marble")
    mg = mat_gold("arch_gold")
    w = wall("arch", 6.0, 6.0, 1.2, m, holes=[(0, 0, 3.0, 3.2, False)], seed=410, broken=0.0)
    parts = [w, box("cap", (6.4, 1.5, 0.4), (0, 0, 6.2), m, 0.05), box("band", (6.05, 1.25, 0.25), (0, 0, 5.2), mg, 0.02)]
    ob = join("MarbleArch", parts)
    L.bake(ob, size=1024)
    parent(ob, r)
    L.export_tree("marble_arch", r)


def build_spring():
    """The Spring: a round marble basin on steps, a column of light rising from glowing water (GlowWater)."""
    r = root("spring")
    m = mat_marble("spring_marble")
    mg = mat_gold("spring_gold")
    parts = [lathe("steps", [(3.0, 0), (3.0, 0.3), (2.5, 0.3), (2.5, 0.6), (0, 0.6)], steps=40),
             lathe("basin", [(2.0, 0.6), (2.2, 0.9), (2.2, 1.3), (2.0, 1.3), (1.9, 0.95), (0, 0.95)], steps=40),
             lathe("pillar", [(0.35, 0.95), (0.25, 1.4), (0.2, 2.2), (0.5, 2.5), (0.55, 2.6), (0, 2.6)], steps=24)]
    parts[0].data.materials.append(m)
    parts[1].data.materials.append(m)
    parts[2].data.materials.append(mg)
    ob = join("Spring", parts)
    L.bake(ob, size=1024)
    parent(ob, r)
    water = lathe("GlowWater", [(1.95, 1.15), (0, 1.15)], steps=40)
    bowl = lathe("bowl", [(0.48, 2.6), (0, 2.55)], steps=24)
    w = join("GlowWater", [water, bowl])
    w.data.materials.append(glow("spring_water", "#c8a0ff", 4.0))
    parent(w, ob)
    L.export_tree("spring", r)


def build_giant_mirror():
    """A giant oval mirror on a marble stand (the Reflection rises before it): Frame and Glass."""
    r = root("giant_mirror")
    m = mat_marble("mirror_marble")
    mg = mat_gold("mirror_gold", rough=0.2)
    # an oval ring of gold standing upright, facing -Y
    bm = bmesh.new()
    pts = []
    for i in range(48):
        a = i / 48 * math.tau
        pts.append(Vector((math.cos(a) * 3.0, 0, 5.0 + math.sin(a) * 4.2)))
    frame = sweep("frame", [[(p, 0.28) for p in pts + pts[:1]]], seg=10, sub=1)
    frame.data.materials.append(mg)
    stand = box("stand", (4.0, 1.6, 1.0), (0, 0, 0.5), m, 0.08)
    legs = [tube("leg", [((sx * 1.6, 0, 0.9), 0.25), ((sx * 2.4, 0, 2.0), 0.22)], seg=10, sub=1) for sx in (-1, 1)]
    for l_ in legs:
        l_.data.materials.append(mg)
    crown = lathe("crown", [(0.6, 0), (0.4, 0.5), (0.1, 1.2), (0, 1.3)], steps=16)
    crown.location = (0, 0, 9.3)
    L.transform_apply(crown)
    crown.data.materials.append(mg)
    ob = join("Frame", [frame, stand, crown] + legs)
    L.bake(ob, size=1024)
    parent(ob, r)
    bm = bmesh.new()
    ring = [bm.verts.new((math.cos(i / 48 * math.tau) * 2.85, -0.02, 5.0 + math.sin(i / 48 * math.tau) * 4.05)) for i in range(48)]
    bm.faces.new(ring)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    glass = L.from_bmesh("Glass", bm, smooth=False)
    glass.data.materials.append(L.mat_plain("mirror_glass", "#c8c8d8", rough=0.05, metal=1.0))
    parent(glass, ob)
    L.export_tree("giant_mirror", r)

ASSETS = {
    "palm": build_palm, "beach_rocks": build_beach_rocks, "shelter": build_shelter, "driftwood": build_driftwood,
    "seaweed": build_seaweed, "morsel": build_morsel, "gull": build_gull,
    "house0": lambda: build_house(0), "house1": lambda: build_house(1), "tower": build_tower, "wall": build_wall,
    "column": build_column, "rubble": build_rubble, "planks": build_planks, "scrap": build_scrap,
    "deadtree": build_deadtree, "lamp": build_lamp, "beacon": build_beacon, "rift": build_rift,
    "volcano": build_volcano, "spires": build_spires, "burnt_house": build_burnt_house, "burnt_wall": build_burnt_wall,
    "vent": build_vent, "totem": build_totem, "ribs": build_ribs, "burnt_tree": build_burnt_tree,
    "gold_wall": build_gold_wall, "gold_tower": build_gold_tower, "gold_house": build_gold_house, "gold_gate": build_gold_gate,
    "gold_pile": build_gold_pile, "crystal": build_crystal, "pylon": build_pylon, "obelisk": build_obelisk,
    "gold_column": build_gold_column, "throne": build_throne, "workshop": build_workshop, "scales": build_scales,
    "pavilion": build_pavilion, "blossom_tree": build_blossom_tree, "flowers": build_flowers, "marble_arch": build_marble_arch,
    "spring": build_spring, "giant_mirror": build_giant_mirror,
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    L.OUT = argv[0] if argv else os.path.abspath("Assets/Crab/Models/World")
    names = argv[1:] or list(ASSETS)
    os.makedirs(L.OUT, exist_ok=True)
    for n in names:
        L.reset()
        random.seed(n)
        try:
            ASSETS[n]()
        except Exception as e:   # one broken asset must not stop the others
            import traceback
            traceback.print_exc()
            print("FAILED", n, e)
    import shutil
    shutil.rmtree(os.path.join(L.OUT, "_tex"), ignore_errors=True)


if __name__ == "__main__":
    main()
