"""
The worlds of the first stage: the beach of the prologue and Limbo, the dead world.

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


def ruin_stone(name, base="#7c807d", dark="#2e3331", light="#a3a7a2", moss=0.25):
    return L.mat_stone(name, base=base, dark=dark, light=light, block=2.6, stretch=(1, 1, 1.9), mortar=0.035, moss=moss, dirt=0.85)


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


ASSETS = {
    "palm": build_palm, "beach_rocks": build_beach_rocks, "shelter": build_shelter, "driftwood": build_driftwood,
    "seaweed": build_seaweed, "morsel": build_morsel, "gull": build_gull,
    "house0": lambda: build_house(0), "house1": lambda: build_house(1), "tower": build_tower, "wall": build_wall,
    "column": build_column, "rubble": build_rubble, "planks": build_planks, "scrap": build_scrap,
    "deadtree": build_deadtree, "lamp": build_lamp, "beacon": build_beacon, "rift": build_rift,
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
