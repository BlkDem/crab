"""
Sculpted models of the first circle (Models/lvl1, from Tripo) brought into the game:
  creatures, rigged here to what their code moves —
    shade    (the Shade of Limbo, 2.5 m): Body > ArmL, ArmR; GlowEyes in the hood;
    crawler  (the Scavenger, 1.5 m long): Body > Jaw, Tail, Leg{L,R}{0,1}_0 > _1 (0 front, 1 back); GlowEyes;
  and things that only stand: poet (the colossus on the cliff), gate_statue, castle_far (in the fog).
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
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SRC = os.path.join(ROOT, "Models", "lvl1")
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
def build_shade():
    ob = load("Тень Лимба.glb", height=2.5)
    co = [v.co for v in ob.data.vertices]
    H = 2.5
    # the arms hang at the shroud's sides: out past the robe between hip and shoulder
    sides = {}
    for s, sx in (("L", 1), ("R", -1)):
        arm_pts = [c for c in co if c.x * sx > 0.22 * H and 0.3 * H < c.z < 0.74 * H]
        hand = min(arm_pts, key=lambda c: c.z)
        sides[s] = (Vector((sx * 0.13 * H, 0.0, 0.73 * H)), Vector((hand.x, hand.y, hand.z)))
    face = [c for c in co if c.z > 0.84 * H and abs(c.x) < 0.05 * H]
    fy = min(c.y for c in face)
    arm = armature([("Body", (0, 0, 0.05 * H), (0, 0, 0.8 * H), None)] +
                   [(f"Arm{s}", sh, hd, "Body") for s, (sh, hd) in sides.items()])
    # only what reaches out past the shroud is arm (the edge closes in toward the shoulder);
    # the shroud and its rags stay with the body
    def arm_part(c):
        edge = 0.2 * H - 0.07 * H * smooth(0.58 * H, 0.74 * H, c.z)
        return smooth(edge - 0.01 * H, edge + 0.015 * H, abs(c.x)) * smooth(0.3 * H, 0.35 * H, c.z)

    auto_skin(ob, arm, lambda c: 1 - arm_part(c))
    ey = eyes(arm, "Body", [(0.022 * H, fy + 0.14, 0.87 * H), (-0.022 * H, fy + 0.14, 0.87 * H)], 0.006 * H)
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


BUILD = {"shade": build_shade, "crawler": build_crawler, "poet": build_poet,
         "gate_statue": build_gate_statue, "castle_far": build_castle_far}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for n in argv or list(BUILD):
        BUILD[n]()


if __name__ == "__main__":
    main()
