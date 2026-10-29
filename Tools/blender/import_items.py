"""
Weapons from sculpted sheets (Models/: several weapons in one mesh, from Tripo and others), one
cut out for each of the game's items, to the contract of make_items.py: the grip at the origin,
pointing forward (-Y), up +Z; an empty Muzzle (where a shot leaves) or Tip (a blade's end).

Run headless:
  blender -b -P Tools/blender/import_items.py -- [<out_dir>] [name ...]

An item is the assembly (loose pieces that touch) of its sheet nearest `at`; `forward` is where
its front points on the sheet. It is cut down to FACES, its pictures to 1024, turned, scaled to
`length` along its axis; the grip sits `grip` of the way from its back end, a little below the
middle of its height.
"""
import os
import sys
import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SRC = os.path.join(ROOT, "Models")
FACES = 6000

ITEMS = {
    "pistol": dict(src="weapons.glb", at=(0.375, 0.0, 0.074), forward="+X", length=0.32, grip=0.22, end="Muzzle"),
    "knife": dict(src="weapons.glb", at=(-0.04, 0.0, 0.073), forward="+X", length=0.34, grip=0.14, end="Tip"),
    "machinegun": dict(src="weapon 3d model level 3.glb", at=(0.107, -0.234, 0.807), forward="-Y", length=0.56, grip=0.3, end="Muzzle"),
    "energy_rifle": dict(src="weapon 3d model level 3.glb", at=(-0.132, 0.059, 0.82), forward="-Y", length=0.64, grip=0.3, end="Muzzle"),
    "energy_blade": dict(src="weapon 3d model level 3.glb", at=(-0.133, 0.133, 0.411), forward="-Y", length=0.66, grip=0.1, end="Tip"),
    "gravity_gun": dict(src="neon energy weapons 3d model.glb", at=(-0.299, 0.013, 0.064), forward="-Y", length=0.46, grip=0.3, end="Muzzle"),
}

TURN = {   # the sheet's forward → -Y
    "-Y": Matrix.Identity(4),
    "+Y": Matrix.Rotation(3.14159265, 4, "Z"),
    "+X": Matrix.Rotation(-1.5707963, 4, "Z"),
    "-X": Matrix.Rotation(1.5707963, 4, "Z"),
}


def sheet(path):
    L.reset()
    bpy.ops.import_scene.gltf(filepath=path)
    ob = max((o for o in bpy.data.objects if o.type == "MESH"), key=lambda o: len(o.data.vertices))
    mw = ob.matrix_world.copy()
    ob.parent = None
    ob.matrix_world = mw
    for o in list(bpy.data.objects):
        if o is not ob:
            bpy.data.objects.remove(o)
    ob.modifiers.clear()
    L.select_only(ob)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob


def cut_out(ob, at):
    """Keep only the assembly nearest `at`."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    co = [v.co for v in bm.verts]
    size = max(max(c[i] for c in co) - min(c[i] for c in co) for i in range(3))
    sets = [p for p in L.assemblies(bm, 0.004 * size) if len(p) > 200]
    at = Vector(at)
    keep = min(sets, key=lambda p: (sum((v.co for v in p), Vector()) / len(p) - at).length)
    ids = {v.index for v in keep}
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in ids], context="VERTS")
    bm.to_mesh(ob.data)
    bm.free()


def build(name, cfg):
    ob = sheet(os.path.join(SRC, cfg["src"]))
    cut_out(ob, cfg["at"])
    if len(ob.data.polygons) > FACES:
        m = ob.modifiers.new("dec", "DECIMATE")
        m.ratio = FACES / len(ob.data.polygons)
        L.select_only(ob)
        bpy.ops.object.modifier_apply(modifier="dec")
    for im in bpy.data.images:
        if im.size[0] > 1024:
            im.scale(1024, 1024)
    ob.data.transform(TURN[cfg["forward"]])
    co = [v.co for v in ob.data.vertices]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    k = cfg["length"] / (mx.y - mn.y)
    # the grip: `grip` of the way from the back (+Y) toward the front, a little below the middle
    grip = Vector(((mn.x + mx.x) / 2, mx.y - cfg["grip"] * (mx.y - mn.y), mn.z + 0.4 * (mx.z - mn.z)))
    ob.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation(-grip))
    ob.name = ob.data.name = name
    for i, m in enumerate(ob.data.materials):
        m.name = f"{name}_{i}"
    end = bpy.data.objects.new(cfg["end"], None)
    L.link(end)
    co = [v.co for v in ob.data.vertices]
    front = min(c.y for c in co)
    near = [c for c in co if c.y < front + 0.06 * cfg["length"]]
    end.location = (sum(c.x for c in near) / len(near), front, sum(c.z for c in near) / len(near))
    end.parent = ob
    L.export_tree(name, ob)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = [a for a in argv if a in ITEMS]
    rest = [a for a in argv if a not in ITEMS]
    L.OUT = os.path.abspath(rest[0]) if rest else os.path.join(ROOT, "Assets/Crab/Models/Items")
    os.makedirs(L.OUT, exist_ok=True)
    for n in names or list(ITEMS):
        build(n, ITEMS[n])


if __name__ == "__main__":
    main()
