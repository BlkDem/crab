"""
Items the crab creates from matter (level 1): stick, stone, crowbar, knife, crude pistol.

Run headless:
  blender -b -P Tools/blender/make_items.py -- <out_dir> [asset ...]

The grip is at the origin and the item points forward (Blender -Y, Unity +Z), so the game
puts the origin into the crab's claw. Empties: Muzzle (where a shot leaves the pistol),
Tip (the end of a melee weapon).
"""
import math
import os
import random
import sys
import bmesh
import bpy
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402
from lib import NT, C, join, set_origin, parent, uv_sphere, tube, sweep  # noqa: E402


def empty(name, loc, par):
    e = bpy.data.objects.new(name, None)
    L.link(e)
    e.location = loc
    parent(e, par)
    return e


def mat_iron(name, base="#4d4a47", rust="#7a3a18", amount=0.55):
    return L.mat_metal(name, base=base, patina=rust, patina_amt=amount, rough=0.45, metal_max=0.9)


def build_stick():
    """A crooked branch with a broken-off twig, bark worn at the grip."""
    pts = [((0, 0.08, 0), 0.022), ((0.004, -0.1, 0.006), 0.02), ((-0.01, -0.28, 0.012), 0.018),
           ((0.006, -0.45, 0.004), 0.015), ((0.0, -0.58, -0.006), 0.011)]
    twig = [((0.004, -0.3, 0.012), 0.009), ((0.04, -0.36, 0.03), 0.006), ((0.07, -0.4, 0.05), 0.003)]
    ob = sweep("Stick", [pts, twig], seg=10, sub=4)
    ob.data.materials.append(L.mat_driftwood("stick", base="#7a5232", dark="#2e1a0c", light="#a8805a", moss=0.0))
    L.bake(ob, size=512)
    empty("Tip", (0, -0.58, 0), ob)
    L.export_tree("stick", ob)


def build_stone():
    ob = L.rock_obj("Stone", 0.065, 7, (1.0, 0.85, 0.75), 0.3, L.mat_rock("stone", moss=0.0, scale=12), sub=4, detail=0.08)
    L.bake(ob, size=512, metal=False)
    L.export_tree("stone", ob)


def build_crowbar():
    """A scrap-iron crowbar: a hexagonal bar with a curled, split claw end."""
    pts = [((0, 0.06, 0), 0.014), ((0, -0.2, 0), 0.014), ((0, -0.42, 0.0), 0.013), ((0, -0.5, 0.02), 0.013),
           ((0, -0.53, 0.06), 0.012), ((0, -0.51, 0.1), 0.01), ((0, -0.47, 0.11), 0.007)]
    bar = sweep("Crowbar", [pts], seg=6, sub=4)
    # the flat chisel at the grip end
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.03, v.co.y * 0.05 + 0.08, v.co.z * (0.008 if v.co.y > 0 else 0.022)))
    chisel = L.from_bmesh("chisel", bm, smooth=False)
    m = mat_iron("crowbar")
    bar.data.materials.append(m)
    chisel.data.materials.append(m)
    ob = join("Crowbar", [bar, chisel])
    L.bake(ob, size=512)
    empty("Tip", (0, -0.53, 0.06), ob)
    L.export_tree("crowbar", ob)


def build_knife():
    """A rough knife hammered out of a scrap plate, its handle wrapped in rag strips."""
    bm = bmesh.new()
    # blade outline (in the XY plane, pointing -Y), extruded thin, with a bevelled edge
    outline = [(0.0, -0.04), (0.018, -0.045), (0.022, -0.12), (0.016, -0.2), (0.0, -0.26), (-0.006, -0.2),
               (-0.01, -0.12), (-0.012, -0.045)]
    top = [bm.verts.new((x, y, 0.003)) for x, y in outline]
    bot = [bm.verts.new((x, y, -0.003)) for x, y in outline]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    n = len(outline)
    for i in range(n):
        bm.faces.new((top[i], top[(i + 1) % n], bot[(i + 1) % n], bot[i]))
    for v in top + bot:   # sharpen the cutting side
        if v.co.x > 0.012 and v.co.y < -0.06:
            v.co.z *= 0.2
    blade = L.from_bmesh("blade", bm, smooth=False)
    L.mod(blade, "BEVEL", width=0.0015, segments=1)
    blade.data.materials.append(mat_iron("knife_blade", base="#8a8682", rust="#6a3416", amount=0.4))
    handle = tube("handle", [((0, 0.07, 0), 0.013), ((0, 0.02, 0), 0.015), ((0, -0.04, 0), 0.014)], seg=10, sub=2)
    handle.data.materials.append(L.mat_canvas("knife_rag", base="#6e6252", dark="#3a3024", moss=0.0))
    ob = join("Knife", [blade, handle])
    L.bake(ob, size=512)
    empty("Tip", (0, -0.26, 0), ob)
    L.export_tree("knife", ob)


def build_pistol():
    """A home-made pistol: a pipe barrel strapped with wire to a wooden grip, a nail trigger."""
    m_pipe = mat_iron("pistol_pipe", base="#5a5753", rust="#7a3a18", amount=0.5)
    m_wood = L.mat_wood("pistol_wood", base="#6a4428", dark="#2e1a0c", light="#946a40", rings=30, along="Y")
    m_wire = L.mat_metal("pistol_wire", base="#9a7a4a", patina="#4a3a22", patina_amt=0.3, rough=0.4)
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.018, depth=0.26, location=(0, -0.11, 0.06), rotation=(math.pi / 2, 0, 0))
    barrel = bpy.context.active_object
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.011, depth=0.27, location=(0, -0.11, 0.06), rotation=(math.pi / 2, 0, 0))
    bore = bpy.context.active_object
    L.boolean(barrel, bore)
    barrel.data.materials.append(m_pipe)
    # the grip block slanting down and back, and the stock plate under the barrel
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.03, v.co.y * 0.05, v.co.z * 0.13))
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0.02, -0.01)) @ Matrix.Rotation(-0.35, 4, "X"), verts=bm.verts)
    grip = L.from_bmesh("grip", bm, smooth=False)
    L.mod(grip, "BEVEL", width=0.006, segments=2)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -0.05, 0.035))
    stock = bpy.context.active_object
    stock.scale = (0.032, 0.18, 0.022)
    L.mod(stock, "BEVEL", width=0.004, segments=2)
    for o in (grip, stock):
        o.data.materials.append(m_wood)
    wraps = []
    for y in (-0.02, -0.1, -0.17):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.024, minor_radius=0.0025, location=(0, y, 0.048), rotation=(math.pi / 2, 0, 0))
        w = bpy.context.active_object
        w.scale = (1.0, 1.0, 1.35)
        w.data.materials.append(m_wire)
        wraps.append(w)
    trig = tube("trigger", [((0, 0.0, 0.015), 0.003), ((0, 0.005, -0.01), 0.003), ((0, 0.015, -0.025), 0.0025)], seg=6, sub=2)
    trig.data.materials.append(m_wire)
    ob = join("Pistol", [barrel, grip, stock, trig] + wraps)
    L.bake(ob, size=512)
    empty("Muzzle", (0, -0.24, 0.06), ob)
    L.export_tree("pistol", ob)


ASSETS = {"stick": build_stick, "stone": build_stone, "crowbar": build_crowbar, "knife": build_knife, "pistol": build_pistol}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    L.OUT = argv[0] if argv else os.path.abspath("Assets/Crab/Models/Items")
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
