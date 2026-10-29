"""
A level's .blend shows copies of the game's models: after models change, import them anew,
changing nothing in the layout.

Run headless:
  blender -b -P Tools/blender/update_assets.py -- <name> [World/cypress ...]
"""
import os
import sys
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level as LV  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
name = argv[0] if argv else "limbo"
path = os.path.join(LV.BLENDS, name + ".blend")
bpy.ops.wm.open_mainfile(filepath=path)
LV.refresh_assets(argv[1:] or None)
bpy.ops.wm.save_mainfile(filepath=path, compress=True)
