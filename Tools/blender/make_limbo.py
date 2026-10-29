"""
The first circle's crags (the valley's rims, the poet's cliff, the ravine). The rest of Limbo's
land is sculpted (Tripo, Models/lvl1): import_creatures.py.

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
from make_world import root  # noqa: E402






# ----------------------------------------------------------------------------- trees




# ----------------------------------------------------------------------------- ruins








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












ASSETS = {"crag": build_crag}


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
