"""
The crab's later forms (levels 2-6): sculpted models (Tripo and others, Models/), brought to the
rig contract of Crab.Game.CrabRig and to the game's size.

Run headless:
  blender -b -P Tools/blender/import_forms.py -- [<out_dir>] [form2 form3 ...]

Each form: the main mesh alone (a rig that came with it is dropped: the rigs are made the same way
for all, by autorig.py), loose bits floating over the shell taken off (the companions are the
game's own), the mesh cut down to FACES, the pictures to 2048, the rig, grips in the claws, and the
root (Body) scaled like the hero's. Forward is -Y, L is +X.
"""
import os
import sys
import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402
import autorig  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SRC = os.path.join(ROOT, "Models")
SCALE = 3.4        # the root's scale, as the hero's
HERO_WIDTH = 1.34  # the hero (level 1) across the legs, m
FACES = 24000

# level → source, and what the rig can't see for itself
FORMS = {
    "form2": dict(src="crab_l2.glb", level=2, shell_scale=0.5, claw_angle=38),   # Краб-воин (legs tucked under big claws)
    "form3": dict(src="crab mech 3d model level 3.glb", level=3, shell_scale=0.62),  # Бронекраб (a ball: the legs hardly leave it)
    "form4": dict(src="crab_level4_alternate.glb", level=4),                    # Краб-маг
    "form5": dict(src="cf06558e766ff967baea3e16e76e4e90.glb", level=5),         # Краб-повелитель
    "form6": dict(src="fantasy titan 3d model level 6.glb", level=6),           # Краб-титан
}


def main_mesh(path):
    L.reset()
    bpy.ops.import_scene.gltf(filepath=path)
    ob = max((o for o in bpy.data.objects if o.type == "MESH"), key=lambda o: len(o.data.vertices))
    mw = ob.matrix_world.copy()
    ob.parent = None
    ob.matrix_world = mw
    for o in list(bpy.data.objects):
        if o is not ob:
            bpy.data.objects.remove(o)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    ob.modifiers.clear()
    for g in list(ob.vertex_groups):
        ob.vertex_groups.remove(g)
    if ob.data.shape_keys:
        ob.shape_key_clear()
    L.select_only(ob)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob


def drop_floaters(ob):
    """Assemblies of loose pieces that hang in the air over the shell, touching it nowhere (drones
    in the picture)."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    zs = [v.co.z for v in bm.verts]
    z0, h = min(zs), max(zs) - min(zs)
    sets = L.assemblies(bm, 0.02 * h)
    body = sets[0]
    gone = [v for p in sets[1:] if len(p) < 0.25 * len(body) and min(v.co.z for v in p) > z0 + 0.45 * h for v in p]
    if gone:
        bmesh.ops.delete(bm, geom=gone, context="VERTS")
        bm.to_mesh(ob.data)
    bm.free()
    print("FLOATERS removed verts", len(gone))


def cut_down(ob, faces):
    if len(ob.data.polygons) > faces:
        m = ob.modifiers.new("dec", "DECIMATE")
        m.ratio = faces / len(ob.data.polygons)
        L.select_only(ob)
        bpy.ops.object.modifier_apply(modifier="dec")
    L.select_only(ob)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0004)
    bpy.ops.object.mode_set(mode="OBJECT")
    for im in bpy.data.images:
        if im.size[0] > 2048:
            im.scale(2048, 2048)


def to_size(ob, width):
    """`width` across, on the ground, the body's middle at the origin."""
    co = [v.co for v in ob.data.vertices]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    k = width / (mx.x - mn.x)
    mid = autorig.middle([c for c in co if 0.35 < (c.z - mn.z) / (mx.z - mn.z) < 0.75])
    ob.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation(Vector((-mid.x, -mid.y, -mn.z))))
    return k


def grips(arm, limbs):
    """Empties in the claws, near two thirds out, parented to the claws' bones."""
    out = []
    for lb in limbs:
        if lb.kind != "claw":
            continue
        g = bpy.data.objects.new("Grip" + lb.side, None)
        L.link(g)
        g.parent = arm
        g.parent_type = "BONE"
        g.parent_bone = lb.name + "_0"
        bpy.context.view_layer.update()
        at = lb.joints[0].lerp(lb.joints[-1], 0.62)
        at.z = max(at.z, 0.85 * lb.joints[0].z)   # a claw that reaches the ground holds things off it
        g.matrix_world = Matrix.Translation(at)
        out.append(g)
    return out


def build(name, cfg):
    ob = main_mesh(os.path.join(SRC, cfg["src"]))
    drop_floaters(ob)
    cut_down(ob, cfg.get("faces", FACES))
    width = HERO_WIDTH * (1 + 0.06 * (cfg["level"] - 1))
    to_size(ob, width / SCALE)
    ob.name = ob.data.name = "Crab"
    arm, limbs, _ = autorig.rig(ob, claw_angle=cfg.get("claw_angle", 28), shell_scale=cfg.get("shell_scale", 1.0))
    gs = grips(arm, limbs)
    arm.scale = (SCALE,) * 3
    bpy.context.view_layer.update()
    for i, m in enumerate(ob.data.materials):
        m.name = f"{name}_{i}"
    L.export(name, [arm, ob] + gs)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    L.OUT = os.path.abspath(argv[0]) if argv and not argv[0].startswith("form") else os.path.join(ROOT, "Assets/Crab/Models/Hero")
    names = [a for a in argv if a.startswith("form")] or list(FORMS)
    os.makedirs(L.OUT, exist_ok=True)
    for n in names:
        build(n, FORMS[n])


if __name__ == "__main__":
    main()
