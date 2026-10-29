"""
Put the current Crab tab (crab_panel.py) into a level's .blend, changing nothing else in it.

Run headless:
  blender -b -P Tools/blender/update_panel.py -- <name>        (e.g. prologue)
"""
import os
import sys
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level as LV  # noqa: E402

name = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "prologue"
path = os.path.join(LV.BLENDS, name + ".blend")
bpy.ops.wm.open_mainfile(filepath=path)
LV.embed_panel()
bpy.ops.wm.save_mainfile(filepath=path, compress=True)
print("PANEL", path)
