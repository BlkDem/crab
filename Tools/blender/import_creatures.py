"""
Sculpted models of the first circle (Models/lvl1, from Tripo) brought into the game:
  creatures, rigged here to what their code moves —
    shade    (the Shade of Limbo, 2.5 m): the hooded ghost on its own Mixamo skeleton with its clips (Призрак Лимба.glb);
             GlowEyes on the head bone, in the hood;
    crawler  (the Scavenger, 1.5 m long): Body > Jaw, Tail, Leg{L,R}{0,1}_0 > _1 (0 front, 1 back); GlowEyes;
    guardian (the Guardian of Limbo, 5.6 m): Body > Head, Arm{L,R}_0 > _1; Heart, Lantern, GlowLantern;
  and things that only stand: poet (the colossus on the cliff), gate_statue, castle_far (in the fog),
  the ruins (arches, aqueduct, temple, column, steles, the castle's wall, towers and gate, the bridge),
  the dead tree, the cypress, and the heaps of matter (rubble, planks, scrap).
A creature's bones are placed from its shape: the arms where the shroud's sides reach out past the
robe, the legs over the feet (where it touches the ground), the jaw under the skull's front. Each
vertex goes to the bone of its part, the rest to Body.

Run headless:
  blender -b -P Tools/blender/import_creatures.py -- [name ...]
Forward is -Y, L is +X; ground at z = 0.
"""
import math
import os
import sys
import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SRC = os.path.join(ROOT, "Models", "lvl1")
SRC2 = os.path.join(ROOT, "Models", "lvl2")
MODELS = os.path.join(ROOT, "Assets", "Crab", "Models")


def load(file, height=None, length=None, width=None, faces=None):
    """The model alone, its transforms applied, scaled to its size, standing on the origin."""
    L.reset()
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, file))
    ob = max((o for o in bpy.data.objects if o.type == "MESH"), key=lambda o: len(o.data.vertices))
    mw = ob.matrix_world.copy()
    ob.parent = None
    ob.matrix_world = mw
    for o in list(bpy.data.objects):
        if o is not ob:
            bpy.data.objects.remove(o)
    L.select_only(ob)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if faces and len(ob.data.polygons) > faces:
        m = ob.modifiers.new("dec", "DECIMATE")
        m.ratio = faces / len(ob.data.polygons)
        bpy.ops.object.modifier_apply(modifier="dec")
    co = [v.co for v in ob.data.vertices]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    k = height / (mx.z - mn.z) if height else length / (mx.y - mn.y) if length else width / (mx.x - mn.x)
    ob.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation(Vector((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z))))
    ob.data.update()
    return ob


def armature(bones):
    """bones: [(name, head, tail, parent)]."""
    ad = bpy.data.armatures.new("Body")
    arm = bpy.data.objects.new("Body", ad)
    L.link(arm)
    L.select_only(arm)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, par in bones:
        b = ad.edit_bones.new(name)
        b.head, b.tail = Vector(head), Vector(tail)
        if par:
            b.parent = ad.edit_bones[par]
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def skin(ob, arm, weights):
    """weights: per vertex {bone: w}."""
    groups = {}
    for i, w in enumerate(weights):
        s = sum(w.values())
        for k, x in w.items():
            if x / s > 0.01:
                g = groups.get(k) or groups.setdefault(k, ob.vertex_groups.new(name=k))
                g.add([i], x / s, "REPLACE")
    m = ob.modifiers.new("Armature", "ARMATURE")
    m.object = arm
    ob.parent = arm


def auto_skin(ob, arm, keep_body=None):
    """Blender's bone-heat weights (the skin follows the bones it is nearest inside); keep_body(co)
    → a share pulled back to Body (parts that must not swing)."""
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    if not any(len(v.groups) for v in ob.data.vertices):
        print("BONE HEAT FAILED: envelopes")
        bpy.ops.object.parent_set(type="ARMATURE_ENVELOPE")
    if keep_body:
        body = ob.vertex_groups.get("Body") or ob.vertex_groups.new(name="Body")
        names = {g.index: g.name for g in ob.vertex_groups}
        for v in ob.data.vertices:
            k = keep_body(v.co)
            if k <= 0:
                continue
            for g in v.groups:
                if names[g.group] != "Body":
                    g.weight *= 1 - k
            body.add([v.index], k, "ADD")
    for v in ob.data.vertices:   # anything the heat missed goes with the body
        if not v.groups:
            ob.vertex_groups["Body"].add([v.index], 1.0, "REPLACE")


def eyes(arm, bone, points, r, color="#d8fff0"):
    """Two glowing motes, parented to a bone (GlowEyes: the game scales them)."""
    parts = [L.uv_sphere(f"e{i}", r, p, seg=10, rings=6) for i, p in enumerate(points)]
    e = L.join("GlowEyes", parts)
    e.data.materials.append(L.mat_plain("eyes_glow", color, rough=0.3, emission=(color, 8)))
    # the origin between the eyes (the game scales them about it)
    c = sum((Vector(p) for p in points), Vector()) / len(points)
    e.data.transform(Matrix.Translation(-c))
    e.parent = arm
    e.parent_type = "BONE"
    e.parent_bone = bone
    bpy.context.view_layer.update()
    e.matrix_world = Matrix.Translation(c)
    return e


def smooth(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def export(name, folder, obs):
    L.OUT = os.path.join(MODELS, folder)
    for im in bpy.data.images:
        if im.size[0] > 2048:
            im.scale(2048, 2048)
    for i, m in enumerate(bpy.data.materials):
        m.name = f"{name}_{i}"
    L.export(name, obs)


# ----------------------------------------------------------------------------- creatures
# The Shade's clips: Tripo's names, and the names the game plays them by (Shade, Pilgrim, SceneBuilder.ShadeAnimator).
SHADE_CLIPS = {"idle": "Idle", "look_around": "LookAround", "walk": "Walk", "run": "Run", "angry_01": "Reach",
               "hit_to_head": "HitHead", "hit_to_body_02": "HitBody", "hit_to_stomach": "HitStomach", "fall": "Fall"}


def use_action(arm, action):
    data = arm.animation_data
    data.action = action
    if getattr(data, "action_slot", None) is None and len(getattr(action, "slots", [])) > 0:
        data.action_slot = action.slots[0]


def in_place(arm, bone, action):
    """Takes a clip's travel out: the hips keep their sway and bob, not the distance (the game moves the creature)."""
    scene = bpy.context.scene
    first, last = (int(round(f)) for f in action.frame_range)
    use_action(arm, action)
    scene.frame_set(first)
    start = bone.head.copy()
    scene.frame_set(last)
    step = (bone.head - start) / (last - first)
    step.z = 0.0
    to_bone = bone.bone.matrix_local.to_3x3().inverted()
    keys = []
    for f in range(first, last + 1):
        scene.frame_set(f)
        keys.append((f, bone.location + to_bone @ (-step * (f - first))))
    for f, location in keys:
        bone.location = location
        bone.keyframe_insert("location", frame=f)


def build_shade():
    """The Shade of Limbo: Tripo's hooded ghost (Призрак Лимба.glb) on its own Mixamo skeleton, with its clips —
    it drifts on its walk, reaches up and swoops at the crab, flinches when hit and falls. 2.5 m tall; the walk and
    the run keep their stride but not their travel; GlowEyes ride the head bone, inside the hood."""
    H = 2.5
    L.reset()
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, "Призрак Лимба.glb"))
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    ob = next(o for o in bpy.data.objects if o.type == "MESH" and o.parent == arm)
    for o in list(bpy.data.objects):
        if o not in (arm, ob):
            bpy.data.objects.remove(o)
    for a in list(bpy.data.actions):
        if a.name not in SHADE_CLIPS:
            bpy.data.actions.remove(a)

    # sized in the rest pose, by the armature, so every clip's bone moves scale with it
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    zs = [(ob.matrix_world @ v.co).z for v in ob.data.vertices]
    k = H / (max(zs) - min(zs))
    arm.scale = arm.scale * k
    bpy.context.view_layer.update()

    hips = arm.pose.bones["mixamorig:Hips"]
    for a in list(bpy.data.actions):
        a.use_fake_user = True
        if a.name in ("walk", "run"):
            in_place(arm, hips, a)
        a.name = SHADE_CLIPS[a.name]
    use_action(arm, bpy.data.actions["Idle"])
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

    # the eyes inside the hood, a little behind its rim, a touch above the head joint
    head = arm.matrix_world @ arm.pose.bones["mixamorig:Head"].head
    world = [ob.matrix_world @ v.co for v in ob.data.vertices]
    rim = min(c.y for c in world if abs(c.x) < 0.04 * H and abs(c.z - head.z) < 0.04 * H)
    ez, ey_ = head.z + 0.012 * H, rim + 0.035 * H
    ey = eyes(arm, "mixamorig:Head", [(0.018 * H, ey_, ez), (-0.018 * H, ey_, ez)], 0.006 * H)
    arm.name = "Shade"
    ob.name = ob.data.name = "Shroud"
    export("shade", "Enemies", [arm, ob, ey])


def build_crawler():
    ob = load("Падальщик.glb", length=1.5)
    co = [v.co for v in ob.data.vertices]
    Lg = 1.5
    hip_z = 0.3 * Lg
    # the feet: where it stands, one cluster per quarter (front -Y / back +Y, left +X / right -X)
    legs = {}
    for name, sx, sy in (("LegL0", 1, -1), ("LegR0", -1, -1), ("LegL1", 1, 1), ("LegR1", -1, 1)):
        low = [c for c in co if c.z < 0.03 * Lg and c.x * sx > 0 and c.y * sy > 0]
        foot = sum(low, Vector()) / len(low)
        hip = Vector((foot.x * 0.45, foot.y * 0.85, hip_z))
        knee = Vector((foot.x * 0.85, (hip.y + foot.y) / 2 + (0.06 if sy < 0 else -0.06) * Lg, 0.17 * Lg))
        legs[name] = (hip, knee, Vector((foot.x, foot.y, 0.0)))
    ys = [c.y for c in co]
    front, back = min(ys), max(ys)
    skull = [c for c in co if c.y < front + 0.16 * Lg]
    sz = sum(c.z for c in skull) / len(skull)
    jaw_hinge = Vector((0, front + 0.14 * Lg, sz - 0.01 * Lg))
    jaw_tip = Vector((0, front, sz - 0.05 * Lg))
    bones = [("Body", (0, 0, 0), (0, 0, hip_z), None), ("Jaw", jaw_hinge, jaw_tip, "Body"),
             ("Tail", (0, back - 0.22 * Lg, 0.21 * Lg), (0, back, 0.2 * Lg), "Body")]
    for name, (hip, knee, foot) in legs.items():
        bones += [(name + "_0", hip, knee, "Body"), (name + "_1", knee, foot, name + "_0")]
    arm = armature(bones)
    auto_skin(ob, arm)
    ey = eyes(arm, "Body", [(0.03 * Lg, front + 0.07 * Lg, sz + 0.035 * Lg), (-0.03 * Lg, front + 0.07 * Lg, sz + 0.035 * Lg)], 0.008 * Lg, "#ffe2b0")
    ob.name = ob.data.name = "Bones"
    export("crawler", "Enemies", [arm, ob, ey])


def build_guardian():
    """The Guardian of Limbo (5.6 m): Body > Head, ArmL_0 > ArmL_1 (the lantern's arm), ArmR_0 > ArmR_1
    (the sword's); Lantern and GlowLantern in the lantern, Heart (a glowing core the game hits) in the
    chest. The arms are named by what they hold (the game's ArmL holds the lantern), not by side."""
    ob = load("страж.glb", height=5.6)
    H = 5.6
    co = [v.co for v in ob.data.vertices]
    # which side holds the lantern: the lantern hangs lower and wider than the sword's hand
    low = [c for c in co if 0.27 * H < c.z < 0.37 * H and abs(c.x) > 0.24 * H]
    lx = -1 if sum(1 for c in low if c.x < 0) > sum(1 for c in low if c.x > 0) else 1
    arms = {"ArmL": lx, "ArmR": -lx}
    sh, el, hd = 0.79 * H, 0.61 * H, 0.45 * H
    bones = [("Body", (0, 0, 0), (0, 0, 0.6 * H), None), ("Head", (0, 0, 0.84 * H), (0, 0, 0.98 * H), "Body")]
    for name, sx in arms.items():
        bones += [(name + "_0", (sx * 0.16 * H, 0, sh), (sx * 0.21 * H, 0, el), "Body"),
                  (name + "_1", (sx * 0.21 * H, 0, el), (sx * 0.23 * H, -0.02 * H, hd), name + "_0")]
    arm = armature(bones)
    weights = []
    for c in co:
        w = {"Body": 1.0}
        x, z = c.x, c.z
        for name, sx in arms.items():
            out = x * sx
            if z < 0.47 * H:        # the hand and what it holds (lantern, sword), out past the robe
                k = smooth(0.18 * H, 0.205 * H, out)
                if k > 0:
                    w = {"Body": 1 - k, name + "_1": k}
            elif z < 0.8 * H:       # forearm, then the upper arm, out past the chest
                edge = 0.15 * H - 0.02 * H * smooth(0.62 * H, 0.78 * H, z)
                k = smooth(edge - 0.01 * H, edge + 0.015 * H, out) * (1 - smooth(0.78 * H, 0.81 * H, z))
                if k > 0:
                    f = smooth(el - 0.03 * H, el + 0.03 * H, z)
                    w = {"Body": 1 - k, name + "_1": k * (1 - f), name + "_0": k * f}
        if z > 0.82 * H and abs(x) < 0.12 * H:
            k = smooth(0.82 * H, 0.86 * H, z)
            w = {"Body": 1 - k, "Head": k}
        weights.append(w)
    skin(ob, arm, weights)
    # the heart: the green crystal on the chest, glowing a little out of it
    chest = [c for c in co if 0.68 * H < c.z < 0.76 * H and abs(c.x) < 0.06 * H]
    fy = min(c.y for c in chest)
    heart = eyes(arm, "Body", [(0, fy + 0.05, 0.72 * H)], 0.028 * H, "#6dff9e")
    heart.name = heart.data.name = "Heart"
    lantern_pts = [c for c in co if 0.25 * H < c.z < 0.42 * H and c.x * lx > 0.22 * H]
    lc = sum(lantern_pts, Vector()) / len(lantern_pts)
    core = eyes(arm, "ArmL_1", [tuple(lc)], 0.025 * H, "#c8ffd8")
    core.name = core.data.name = "GlowLantern"
    lant = bpy.data.objects.new("Lantern", None)
    L.link(lant)
    lant.parent = arm
    lant.parent_type = "BONE"
    lant.parent_bone = "ArmL_1"
    bpy.context.view_layer.update()
    lant.matrix_world = Matrix.Translation(lc)
    ob.name = ob.data.name = "Armour"
    print("GUARDIAN lantern side", lx, "lantern", tuple(round(v, 2) for v in lc))
    export("guardian", "Enemies", [arm, ob, heart, core, lant])


# ----------------------------------------------------------------------------- things that stand
def build_poet():
    ob = load("Статуя поэта.glb", height=11.0)
    ob.name = ob.data.name = "Poet"
    export("poet", "World", [ob])


def build_gate_statue():
    ob = load("Статуи врат.glb", height=6.5)
    ob.name = ob.data.name = "GateStatue"
    export("gate_statue", "World", [ob])


def build_castle_far():
    ob = load("Замок для фона.glb", width=300.0, faces=60000)
    ob.name = ob.data.name = "CastleFar"
    export("castle_far", "World", [ob])


# ----------------------------------------------------------------------------- the ruins of Limbo
def piece(file, name, size, axis="z", rot=0, faces=20000, stretch=1.0, src=SRC):
    """One sculpted model into the open scene: its main mesh alone, turned `rot` degrees about Z (its
    long side to the one the game expects), cut down to `faces`, `size` m along `axis`, stretched
    `stretch` in height, centred on the ground."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(src, file))
    new = [o for o in bpy.data.objects if o not in before]
    ob = max((o for o in new if o.type == "MESH"), key=lambda o: len(o.data.vertices))
    mw = ob.matrix_world.copy()
    ob.parent = None
    ob.matrix_world = mw
    for o in new:
        if o is not ob:
            bpy.data.objects.remove(o)
    L.select_only(ob)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(ob.data.polygons) > faces:
        m = ob.modifiers.new("dec", "DECIMATE")
        m.ratio = faces / len(ob.data.polygons)
        bpy.ops.object.modifier_apply(modifier="dec")
    ob.data.transform(Matrix.Rotation(math.radians(rot), 4, "Z"))
    co = [v.co for v in ob.data.vertices]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    k = size / (mx - mn)["xyz".index(axis)]
    ob.data.transform(Matrix.Diagonal((k, k, k * stretch, 1)) @ Matrix.Translation(Vector((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z))))
    ob.data.update()
    ob.name = ob.data.name = name
    return ob


def shrink_images(size):
    for im in bpy.data.images:
        if im.size[0] > size:
            im.scale(size, size)


def stand(asset, children, tex=2048, folder="World"):
    """Export the pieces as one asset: a root named after it, the pieces its children (the variants)."""
    r = bpy.data.objects.new(asset, None)
    L.link(r)
    for c in children:
        c.parent = r
    shrink_images(tex)
    export(asset, folder, [r] + children)


def on_top(ob):
    """The origin on the walkable top (an island, a ledge): the height most of its middle stands at."""
    co = [v.co for v in ob.data.vertices]
    wx = max(c.x for c in co) - min(c.x for c in co)
    wy = max(c.y for c in co) - min(c.y for c in co)
    mid = sorted(c.z for c in co if abs(c.x) < 0.25 * wx and abs(c.y) < 0.25 * wy)
    top = mid[int(len(mid) * 0.97)] if mid else max(c.z for c in co)
    ob.data.transform(Matrix.Translation((0, 0, -top)))
    print("TOP", ob.name, round(top, 2))


# ----------------------------------------------------------------------------- the second circle (Models/lvl2)
LUST = {
    # asset: (file, size m, axis, faces, origin on top, folder)
    "lust_island_large": ("island_large.glb", 24, "x", 20000, True, "World"),
    "lust_island_medium": ("island_medium.glb", 18, "x", 18000, True, "World"),
    "lust_islet": ("islet_small.glb", 4.5, "x", 6000, True, "World"),
    "lust_ledge": ("ruina_ledge.glb", 10, "x", 12000, True, "World"),
    "lust_wind_rock": ("wind_rock.glb", 3.2, "z", 6000, False, "World"),
    "lust_spire": ("ruined_spire.glb", 22, "z", 16000, False, "World"),
    "lust_bridge": ("broken_bridge.glb", 12, "x", 12000, False, "World"),
    "lust_throne": ("minos_throne.glb", 12, "z", 20000, False, "World"),
    "lust_page": ("minos_page.glb", 0.8, "x", 3000, False, "World"),
    "lust_palace_hall": ("palace_hall.glb", 9, "z", 20000, False, "World"),
    "lust_ziggurat": ("ziggurat_ruin.glb", 18, "z", 20000, False, "World"),
    "lust_lovers_statue": ("lovers_statue.glb", 5, "x", 12000, False, "World"),
    "minos_book": ("minos_book.glb", 3, "x", 6000, False, "World"),
    "paolo_francesca": ("paolo_francesca.glb", 2.4, "z", 14000, False, "World"),
    "minos_judge": ("minos_judge.glb", 14, "z", 24000, False, "Enemies"),
    "minos_demon": ("minos_demon.glb", 12, "z", 24000, False, "Enemies"),
    "wind_demon": ("wind_demon.glb", 2.6, "z", 16000, False, "Enemies"),
    "entwined": ("entwined.glb", 2.6, "z", 16000, False, "Enemies"),
}


def close_holes(ob, sides=24):
    """Land is walked on and things are set down on it: the generated mesh's pinholes and unwelded
    seams (a ray, a foot, slips through them) welded and filled."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.004)
    filled = bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=sides)["faces"]
    bm.to_mesh(ob.data)
    bm.free()
    print("HOLES", ob.name, len(filled))


def build_lust(asset):
    file, size, axis, faces, top, folder = LUST[asset]
    L.reset()
    ob = piece(file, asset, size, axis=axis, faces=faces, src=SRC2)
    if top:
        close_holes(ob)
        on_top(ob)
    stand(asset, [ob], tex=2048 if size > 5 else 1024, folder=folder)


def variant(ob, name, scale):
    """Another child showing the same mesh at another size."""
    v = bpy.data.objects.new(name, ob.data)
    L.link(v)
    v.scale = (scale,) * 3
    return v


def build_ruin_arch():
    L.reset()
    stand("ruin_arch", [piece("Арка руин.glb", "Arch0", 7.0, faces=12000), piece("Рухнувшая арка.glb", "Arch1", 7.0, faces=12000)])


def cut(ob, co, no):
    """Cut the mesh by a plane, dropping what lies on the normal's side; the cut closed over."""
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    r = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no, clear_outer=True)
    edges = [e for e in r["geom_cut"] if isinstance(e, bmesh.types.BMEdge) and e.is_boundary]
    if edges:
        bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    bm.to_mesh(ob.data)
    bm.free()


def build_aqueduct():
    """Tripo's aqueduct stands on a slab with a solid block along one side: both cut away, the arcades left."""
    L.reset()
    ob = piece("Акведук.glb", "Aqueduct", 20.0, axis="x", rot=90, faces=20000)
    cut(ob, (0, -3.0, 0), (0, -1, 0))
    cut(ob, (0, 0, 2.6), (0, 0, -1))
    zmin = min(v.co.z for v in ob.data.vertices)
    ob.data.transform(Matrix.Translation((0, 0, -zmin)))
    stand("aqueduct", [ob])


def build_tholos():
    L.reset()
    stand("tholos", [piece("Ротонда на холме.glb", "Tholos", 10.0, axis="x", faces=24000)])


def build_column():
    L.reset()
    stand("column", [piece("Колонна.glb", "Column", 3.6, faces=6000)], tex=1024)


def build_stele():
    L.reset()
    a = piece("стела1.glb", "Stele0", 2.2, faces=4000)
    b = piece("стела2.glb", "Stele1", 2.0, faces=4000)
    stand("stele", [a, b, variant(a, "Stele2", 0.65)], tex=1024)


def build_castle_wall():
    L.reset()
    w = piece("стена2.glb", "CastleWall", 16.0, axis="x", rot=90, faces=16000, stretch=2.3)
    t = piece("башня.glb", "CastleTower", 26.0, faces=20000)
    stand("castle_wall", [w, t])


def build_castle_gate():
    L.reset()
    stand("castle_gate", [piece("врата.glb", "CastleGate", 22.0, axis="x", faces=30000)])


def build_bridge():
    """The deck's top at the origin (the level sets the bridge by its deck), the deck along Y."""
    L.reset()
    ob = piece("Мост через ущелье.glb", "Bridge", 34.0, axis="y", rot=90, faces=24000)
    co = [v.co for v in ob.data.vertices]
    w = max(c.x for c in co) - min(c.x for c in co)
    mid = [c.z for c in co if abs(c.x) < 0.15 * w and abs(c.y) < 6]
    deck = max(mid)
    ob.data.transform(Matrix.Translation((0, 0, -deck)))
    print("BRIDGE deck", round(deck, 2), "width", round(w, 2))
    stand("bridge", [ob])


def build_deadtree():
    L.reset()
    stand("deadtree", [piece("мертвое дерево.glb", "DeadTree", 6.0, faces=14000)], tex=1024)


def build_cypress():
    """Tripo's cypress is bright and cheerful: its colour darkened and greyed to Limbo's."""
    import numpy as np
    L.reset()
    c = piece("кипарис.glb", "Cypress0", 9.5, faces=6000)
    shrink_images(1024)
    for m in c.data.materials:
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image and n.image.colorspace_settings.name == "sRGB":
                px = np.array(n.image.pixels[:]).reshape(-1, 4)
                grey = px[:, :3].mean(1, keepdims=True)
                px[:, :3] = (px[:, :3] * 0.45 + grey * 0.55) * np.array([0.42, 0.5, 0.45])
                n.image.pixels.foreach_set(px.astype(np.float32).ravel())
                n.image.pack()
    stand("cypress", [c, variant(c, "Cypress1", 0.8), variant(c, "Cypress2", 1.2)], tex=1024)


def build_matter(file, asset, size):
    L.reset()
    stand(asset, [piece(file, asset.capitalize(), size, axis="x", faces=6000)], tex=1024)


BUILD = {"shade": build_shade, "crawler": build_crawler, "guardian": build_guardian, "poet": build_poet,
         "gate_statue": build_gate_statue, "castle_far": build_castle_far,
         "ruin_arch": build_ruin_arch, "aqueduct": build_aqueduct, "tholos": build_tholos, "column": build_column,
         "stele": build_stele, "castle_wall": build_castle_wall, "castle_gate": build_castle_gate, "bridge": build_bridge,
         "deadtree": build_deadtree, "cypress": build_cypress,
         "rubble": lambda: build_matter("обломки.glb", "rubble", 2.0),
         "planks": lambda: build_matter("доски.glb", "planks", 2.2),
         "scrap": lambda: build_matter("металлолом.glb", "scrap", 1.8)}
BUILD.update({a: (lambda a=a: build_lust(a)) for a in LUST})


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for n in argv or list(BUILD):
        BUILD[n]()


if __name__ == "__main__":
    main()
