"""
The crab after the Spark (the hero from the prologue on): a sculpted model from Tripo, skinned,
brought to the rig contract of Crab.Game.CrabRig.

Run headless:
  blender -b -P Tools/blender/import_hero.py -- [<out_dir>] [<source .zip or .fbx>]

The source (Tools/blender/src/crab_tripo.zip) is an FBX with an auto-rig: 52 bones named bone_N,
one mesh, four PBR maps. The rig has faults that are mended here:
  - the front right leg has no bones of its own: its skin hangs on the big claw's carpus and palm
    and on the right eye's tip (so it would wave with the claw and the eye) — it gets a chain of
    three new bones and its skin is re-weighted along them;
  - each eye is split over two chains, one not under the other — an eye's stalk and ball are
    merged onto one bone;
  - the big claw is one bone from the wrist to the finger tips — its movable (upper) finger is
    cut off onto a bone of its own.
Then the bones the game moves are renamed after the contract (make_hero.py):
  Body > ... > Eye{L,R}, Leg{L,R}{0..3}_0 > _1 > _2 (hip, knee, ankle),
  Claw{L,R}_0 > _1 > _2 (palm) > _3 (movable finger); Grip{L,R} empties in the palms.
The other bones stay (bone_N): coxae under the shell, leg tips, a stump on the left side.
L is +X (the crab's left, the small claw), R is -X (the big claw). Forward is -Y.

The armature object is Body; like the old hero it carries SCALE and stands on its origin.
"""
import math
import os
import sys
import tempfile
import zipfile
import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import intersect_point_line

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "src", "crab_tripo.zip")
SCALE = 3.4     # the root's scale, as the old hero's (the armor sits in the body's space)
WIDTH = 0.69    # the shell across, in the game (m), as the old hero's

# Tripo's chains → the contract: (hip, knee, ankle) per leg, front to back
LEGS = {
    "LegL0": ("bone_2", "bone_3", "bone_4"),
    "LegL1": ("bone_7", "bone_8", "bone_9"),
    "LegL2": ("bone_14", "bone_15", "bone_16"),
    "LegL3": ("bone_19", "bone_20", "bone_21"),
    # LegR0 is new (see LEG_R0)
    "LegR1": ("bone_27", "bone_28", "bone_29"),
    "LegR2": ("bone_32", "bone_33", "bone_34"),
    "LegR3": ("bone_37", "bone_38", "bone_39"),
}
CLAWS = {
    "ClawL": ("bone_41", "bone_42", "bone_43", "bone_44"),
    "ClawR": ("bone_23", "bone_24", "bone_25", None),   # the finger is cut out below
}
EYES = {"EyeL": ("bone_47", ("bone_48", "bone_49")),
        "EyeR": ("tripo::Head_0", ("bone_45", "bone_46", "bone_51"))}
ROOT = "tripo::Root"

# the front right leg, in the source's units: hip at the shell's edge, knee, ankle, tip
LEG_R0 = [Vector(v) for v in ((-0.285, -0.035, 0.300), (-0.350, -0.170, 0.190), (-0.345, -0.225, 0.135), (-0.215, -0.320, 0.010))]
LEG_R0_RADIUS = 0.075   # the leg's skin is within this of its line
# bones whose skin the front right leg was given
LEG_R0_FROM = {"bone_22", "bone_23", "bone_24", "bone_25", "bone_51"}
EYE_R_BELOW = 0.39   # bone_51's skin under this height is the leg's, above — the eye's


def source_fbx(path):
    if path.lower().endswith(".fbx"):
        return path
    d = tempfile.mkdtemp(prefix="crab_tripo_")
    with zipfile.ZipFile(path) as z:
        z.extractall(d)
    return next(os.path.join(d, n) for n in os.listdir(d) if n.lower().endswith(".fbx"))


def polyline_dist(p, pts):
    """Distance from p to a polyline, and the index + fraction of the closest segment."""
    best = (1e9, 0, 0.0)
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        q, t = intersect_point_line(p, a, b)
        t = min(1.0, max(0.0, t))
        d = (p - a.lerp(b, t)).length
        if d < best[0]:
            best = (d, i, t)
    return best


def groups_of(ob):
    return {g.index: g.name for g in ob.vertex_groups}


def weights(ob, v):
    names = groups_of(ob)
    return {names[g.group]: g.weight for g in v.groups}


def set_weights(ob, v, w):
    """Replace a vertex's weights with w (name → weight), normalised."""
    for g in list(v.groups):
        ob.vertex_groups[g.group].remove([v.index])
    s = sum(w.values())
    for n, x in w.items():
        if x > 1e-4:
            vg = ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)
            vg.add([v.index], x / s, "REPLACE")


def mend_weights(ob, arm):
    me = ob.data
    # the eyes: one bone each
    for eye, (main, rest) in EYES.items():
        for v in me.vertices:
            w = weights(ob, v)
            if not any(n in w for n in rest):
                continue
            if main == "tripo::Head_0" and "bone_51" in w and v.co.z < EYE_R_BELOW:
                continue   # the leg's, handled below
            moved = sum(w.pop(n, 0) for n in rest)
            w[main] = w.get(main, 0) + moved
            set_weights(ob, v, w)

    # the front right leg: skin within its radius, of the bones it was hung on, nearer to it than
    # to the claw's arm (the leg's skin lies within 0.07 of its line, the claw's beyond 0.08)
    claw = [arm.data.bones[n].head_local for n in ("bone_23", "bone_24", "bone_25")] + [arm.data.bones["bone_25"].tail_local]
    n_leg = 0
    for v in me.vertices:
        w = weights(ob, v)
        if not w:
            continue
        dom = max(w, key=w.get)
        if dom not in LEG_R0_FROM:
            continue
        d, i, t = polyline_dist(v.co, LEG_R0)
        if d > LEG_R0_RADIUS or d > polyline_dist(v.co, claw)[0]:
            if "bone_51" in w:   # the claw's own skin, left on the eye's tip by mistake
                w["bone_24"] = w.get("bone_24", 0) + w.pop("bone_51")
                set_weights(ob, v, w)
            continue
        # along the chain: a soft blend over each joint
        nw = {}
        names = ("LegR0_0", "LegR0_1", "LegR0_2")
        k = min(i, 2)
        nw[names[k]] = 1.0
        if t < 0.25 and k > 0:
            a = 0.5 + 2 * t   # 0.5 at the joint → 1 a quarter in
            nw = {names[k]: a, names[k - 1]: 1 - a}
        elif t > 0.75 and k < 2:
            a = 0.5 + 2 * (1 - t)
            nw = {names[k]: a, names[k + 1]: 1 - a}
        if i == 0 and t < 0.3:   # into the body at the hip
            nw[ROOT] = 0.6 * (1 - t / 0.3)
        set_weights(ob, v, nw)
        n_leg += 1
    print("LEG R0 verts", n_leg)


def claw_r_finger(ob):
    """The big claw's upper (movable) finger: the palm's skin past the knuckle's seam and above the gap.
    Seen from the front (x, z) the two cut lines are seam A→B and gap B→C."""
    a, b, c = (Vector(p) for p in FINGER_CUT)
    inside = Vector((-0.10, 0.23))   # a point on the finger

    def side(p, u, v):
        return (v - u).cross(p - u)

    n = 0
    for v in ob.data.vertices:
        w = weights(ob, v)
        if w.get("bone_25", 0) < 0.3:
            continue
        p = Vector((v.co.x, v.co.z))
        if side(p, a, b) * side(inside, a, b) > 0 and side(p, b, c) * side(inside, b, c) > 0:
            w["ClawR_3"] = w.get("ClawR_3", 0) + w.pop("bone_25")
            set_weights(ob, v, w)
            n += 1
    print("CLAW R finger verts", n)


# the big claw's movable finger: the bone (knuckle → tip), and the cut in the front view (x, z)
FINGER_R = (Vector((-0.160, -0.310, 0.250)), Vector((-0.035, -0.310, 0.145)))
FINGER_CUT = ((-0.1775, 0.2825), (-0.1375, 0.2125), (-0.040, 0.136))


def edit_bones(arm):
    L.select_only(arm)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    root = eb[ROOT]
    prev = root
    for i in range(3):
        b = eb.new(f"LegR0_{i}")
        b.head, b.tail = LEG_R0[i], LEG_R0[i + 1]
        b.parent = prev
        b.use_connect = i > 0
        prev = b
    f = eb.new("ClawR_3")
    f.head, f.tail = FINGER_R
    f.parent = eb["bone_25"]
    for _, rest in EYES.values():
        for n in rest:
            if n in eb:
                eb.remove(eb[n])
    bpy.ops.object.mode_set(mode="OBJECT")


def rename(arm, ob):
    names = {}
    for leg, chain in LEGS.items():
        for i, n in enumerate(chain):
            names[n] = f"{leg}_{i}"
    for claw, chain in CLAWS.items():
        for i, n in enumerate(chain):
            if n:
                names[n] = f"{claw}_{i}"
    for eye, (main, _) in EYES.items():
        names[main] = eye
    names[ROOT] = "Root"
    names["bone_11"] = "Stump_0"
    names["bone_12"] = "Stump_1"
    for old, new in names.items():
        arm.data.bones[old].name = new   # vertex groups follow the bones
        vg = ob.vertex_groups.get(old)
        if vg:
            vg.name = new


def grips(arm, k):
    """Empties between the fingers, parented to the palms' bones (placed while the root is unscaled)."""
    out = []
    for side, at in GRIPS.items():
        g = bpy.data.objects.new("Grip" + side, None)
        L.link(g)
        g.parent = arm
        g.parent_type = "BONE"
        g.parent_bone = f"Claw{side}_2"
        bpy.context.view_layer.update()
        g.matrix_world = Matrix.Translation(at * k)
        out.append(g)
    return out


# where held items sit, in the source's units
GRIPS = {"L": Vector((0.075, -0.335, 0.255)), "R": Vector((-0.115, -0.345, 0.205))}


def build(src):
    L.reset()
    bpy.ops.import_scene.fbx(filepath=source_fbx(src))
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    ob = next(o for o in bpy.data.objects if o.type == "MESH")
    arm.name = arm.data.name = "Body"
    ob.name = ob.data.name = "Crab"

    edit_bones(arm)
    mend_weights(ob, arm)
    claw_r_finger(ob)
    for _, rest in EYES.values():
        for n in rest:
            vg = ob.vertex_groups.get(n)
            if vg:
                ob.vertex_groups.remove(vg)
    rename(arm, ob)

    # to the game's size: the shell (the root bone's skin) WIDTH across
    xs = [v.co.x for v in ob.data.vertices if weights(ob, v).get("Root", 0) > 0.8]
    k = WIDTH / (max(xs) - min(xs))
    print("SHELL", round(max(xs) - min(xs), 3), "scale", round(k, 3))
    ob.parent = None
    for o in (arm, ob):
        o.scale = (k / SCALE,) * 3
    L.select_only(arm, ob)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.parent = arm
    ob.matrix_parent_inverse.identity()
    gs = grips(arm, k / SCALE)
    arm.scale = (SCALE,) * 3
    bpy.context.view_layer.update()

    mat = ob.data.materials[0]
    mat.name = "hero0"
    L.export("crab", [arm, ob] + gs)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    L.OUT = argv[0] if argv else os.path.abspath("Assets/Crab/Models/Hero")
    os.makedirs(L.OUT, exist_ok=True)
    build(argv[1] if len(argv) > 1 else SRC)


if __name__ == "__main__":
    main()
