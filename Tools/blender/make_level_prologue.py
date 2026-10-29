"""
The prologue's beach, laid out once into Levels/prologue.blend. From then on the .blend is the
source of the level: edit it by hand, and export it with export_level.py. This script won't
overwrite an existing .blend (pass --force to start over).

Run headless:
  blender -b -P Tools/blender/make_level_prologue.py [-- --force]

The beach rises gently from the sea (game -Z) to dunes (+Z) with palms along them; rocks and weed
scattered over it. The game's places (level.py, role): the crab's start, the food (three fish
and two clumps of weed; all the weed on the beach may be eaten), the shelter slab from the gull,
the gull's circle, the big wave, two pieces of driftwood (the first matter), the rift that opens
at the end, the sea's level. The sun is a lamp.
"""
import math
import os
import random
import sys
import bpy
import numpy as np
from mathutils import Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402
import level as LV  # noqa: E402
from level import from_unity  # noqa: E402

NAME = "prologue"
AREA = (-60, -40, 120, 100)   # game x, z, width, depth
STEP = 0.5
TILE = 0.25                   # the sand repeats every 4 m


def perlin01(a, b):
    return 0.5 + 0.5 * noise.noise(Vector((a, b, 0.37)))


def height(x, z):
    """The beach's ground in game coordinates (as BeachHeight in SceneBuilder)."""
    h = z * 0.045
    if z < -4:
        h -= (-4 - z) * 0.09
    dunes = min(1.0, max(0.0, (z - 12) / 10))
    h += dunes * perlin01(x * 0.06 + 3, z * 0.06) * 2.5
    h += (perlin01(x * 0.25, z * 0.25) - 0.5) * 0.12
    return h


def at(x, z, sink=0.05):
    return from_unity(x, height(x, z) - sink, z)


def sand_images(n=1024):
    """Sand that tiles: colour and a normal map from the same small ripples and grains."""
    rng = np.random.default_rng(7)
    hgt = sum(LV.periodic_noise(n, c, rng) * w for c, w in ((4, 0.5), (16, 0.3), (64, 0.15), (256, 0.12)))
    # wind ripples across the beach
    yy = np.arange(n)[:, None] / n
    hgt += 0.08 * np.sin(2 * math.pi * (yy * 12 + 0.6 * LV.periodic_noise(n, 4, rng)))
    grains = rng.random((n, n)) ** 6
    base = np.array([0.80, 0.69, 0.50])
    tone = (hgt - hgt.mean()) / (hgt.std() + 1e-6)
    col = base[None, None, :] * (1 + 0.06 * tone[..., None]) - 0.12 * grains[..., None] * np.array([1, 0.95, 0.9])
    col = np.clip(col, 0, 1)
    dx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 6
    dy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 6
    nrm = np.dstack((-dx, -dy, np.ones_like(dx)))
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    return LV.image("sand_color", col, "sRGB"), LV.image("sand_normal", nrm * 0.5 + 0.5, "Non-Color")


def ground():
    x0, z0, w, d = AREA
    nx, nz = int(w / STEP) + 1, int(d / STEP) + 1
    verts, uvs = [], []
    for j in range(nz):
        for i in range(nx):
            x, z = x0 + i * STEP, z0 + j * STEP
            verts.append(from_unity(x, height(x, z), z))
    faces = []
    for j in range(nz - 1):
        for i in range(nx - 1):
            a = j * nx + i
            faces.append((a, a + 1, a + nx + 1, a + nx))
    me = bpy.data.meshes.new("Ground")
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.update()
    if me.polygons[0].normal.z < 0:   # the land faces up
        me.flip_normals()
    uv = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            v = verts[me.loops[li].vertex_index]
            uv.data[li].uv = (-v.x * TILE, -v.y * TILE)
    me.color_attributes.new("AO", "BYTE_COLOR", "POINT")
    ob = bpy.data.objects.new("Ground", me)
    LV.collection("Ground").objects.link(ob)
    ob["collider"] = "mesh"
    col, nrm = sand_images()
    m = bpy.data.materials.new("Sand")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage")
    tc.image = col
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = nrm
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.9
    me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def scenery(rng):
    for (x, z) in ((-16, 16), (-9, 19), (-3, 23), (5, 17), (11, 21), (18, 15), (-22, 22), (24, 24), (0, 30), (-12, 28), (13, 31)):
        x, z = x + rng.uniform(-1, 1), z + rng.uniform(-1, 1)
        LV.instance("World/palm", at(x, z), -rng.uniform(0, 360), rng.uniform(0.85, 1.2), collider="none", snap=1)
    n = 0
    while n < 16:
        x, z = rng.uniform(-30, 30), rng.uniform(-6, 30)
        if abs(x) < 4 and z < 10:
            continue
        LV.instance(f"World/beach_rocks/Rock{rng.randrange(4)}", at(x, z), -rng.uniform(0, 360), rng.uniform(0.5, 1.4), collider="mesh", snap=1)
        n += 1
    for _ in range(10):   # the weed on the beach: may be eaten
        x, z = rng.uniform(-25, 25), rng.uniform(-3, 6)
        LV.instance("World/seaweed", at(x, z, 0), -rng.uniform(0, 360), rng.uniform(0.8, 1.4), into="Game",
                    role="food", title="Водоросли", heal=10, collider="none", snap=1)


def game(rng):
    LV.marker("Start", at(0, 2, 0), -180, role="start")
    for (x, z) in ((-4, -1), (5, -3), (2.5, 6)):
        LV.instance("World/morsel", at(x, z, 0), -rng.uniform(0, 360), 1.6, into="Game", name="Fish",
                    role="food", title="Рыбёшка", heal=15, collider="none", snap=1)
    for (x, z) in ((-8, 3), (9, 1)):
        LV.instance("World/seaweed", at(x, z, 0), -rng.uniform(0, 360), 1.2, into="Game", name="Weed",
                    role="food", title="Водоросли", heal=20, collider="none", snap=1)
    LV.instance("World/shelter", at(8, 4.5), -20, 1.0, into="Game", name="Shelter", role="shelter", collider="mesh", snap=1)
    LV.marker("Gull", at(0, 4, 0), 0, role="gull", height=10.0, scale=1.3)
    LV.marker("Wave", from_unity(0, 0.1, -16), 0, role="wave", reach_z=9.0, speed=5.2, width=90.0)
    LV.instance("World/driftwood", at(-5, 9, 0), -30, 1.2, into="Game", name="Driftwood", role="matter",
                kind="organic", amount=8, first=1, collider="none", snap=1)
    LV.instance("World/driftwood", at(6, 11, 0), 60, 1.0, into="Game", name="Driftwood2", role="matter",
                kind="organic", amount=5, collider="none", snap=1)
    LV.instance("World/rift", at(0, 19), -180, 1.1, into="Game", name="Rift", role="rift",
                id="prologue_exit", leads_to="Limbo", delay=0.5, collider="mesh", snap=1)
    LV.marker("Sea", from_unity(0, -0.25, 0), 0, role="sea", size=400.0)


def sun():
    # the game's sun turned (42°, -30°): its light runs that way
    e = (math.radians(42), math.radians(-30))
    u = Vector((math.cos(e[0]) * math.sin(e[1]), -math.sin(e[0]), math.cos(e[0]) * math.cos(e[1])))
    d = Vector((-u.x, -u.z, u.y))
    ld = bpy.data.lights.new("Sun", "SUN")
    ld.color = (1.0, 0.93, 0.8)
    ld.energy = 1.6 * LV.SUN
    ld.angle = math.radians(2)
    ob = bpy.data.objects.new("Sun", ld)
    ob.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    ob.location = (0, 0, 30)
    bpy.context.scene.collection.objects.link(ob)


def main():
    force = "--force" in sys.argv
    os.makedirs(LV.BLENDS, exist_ok=True)
    path = os.path.join(LV.BLENDS, NAME + ".blend")
    if os.path.exists(path) and not force:
        print("EXISTS", path, "— the .blend is the source now; pass --force to lay it out anew")
        return
    L.reset()
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    w = bpy.data.worlds.new("Sky")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.7, 0.9, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    sc.world = w
    rng = random.Random(NAME)
    ground()
    scenery(rng)
    game(rng)
    sun()
    LV.exclude_assets()
    LV.embed_panel()
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print("WROTE", path)


if __name__ == "__main__":
    main()
