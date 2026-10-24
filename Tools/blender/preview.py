"""
Turntable-style preview of a GLB: three views side by side.
  blender -b -P Tools/blender/preview.py -- <model.glb> <out.png> [distance] [height]
"""
import math
import os
import sys
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
obs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
lo = Vector((min(min((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in obs) for i in range(3)))
hi = Vector((max(max((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in obs) for i in range(3)))
center, size = (lo + hi) / 2, (hi - lo).length
dist = float(argv[2]) if len(argv) > 2 else size * 1.6
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 640, 480
w = bpy.data.worlds.new("w"); sc.world = w
w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.2, 0.24, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 3.5
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 50
import numpy as np
tiles = []
for i, (az, el) in enumerate(((-30, 25), (-100, 15), (160, 45))):
    a, e = math.radians(az), math.radians(el)
    d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    cam.location = center + d * dist
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = out + f".{i}.png"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    tiles.append(path)
imgs = [bpy.data.images.load(p) for p in tiles]
W, Hh = imgs[0].size
canvas = np.zeros((Hh, W * 3, 4), dtype=np.float32)
for k, im in enumerate(imgs):
    a = np.array(im.pixels[:], dtype=np.float32).reshape(Hh, W, 4)
    canvas[:, k * W:(k + 1) * W] = a
res = bpy.data.images.new("res", W * 3, Hh)
res.pixels = canvas.ravel()
res.filepath_raw = out
res.file_format = "PNG"
res.save()
for p in tiles:
    os.remove(p)
print("PREVIEW", out)
