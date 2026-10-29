"""
Levels made in Blender: the helpers shared by the level makers (make_level_*.py, run once to lay a
level out) and export_level.py (run after every edit of the .blend).

A level's .blend (Levels/<name>.blend) is the source of the level; edit it by hand freely:
  Ground   — the land, one mesh (its own baked material; a collider in the game);
  Props    — scenery: empties instancing an asset collection; custom property `asset` (e.g.
             "World/palm" or "World/beach_rocks/Rock2"), `collider` ("mesh", "box", "none"),
             `snap` (1: the game sets it down on the ground under it);
  Game     — the places the game's logic needs: empties (or prop-like instances) with a custom
             property `role` ("start", "food", "shelter", "gull", "wave", "matter", "rift", "sea"...)
             and the role's own properties;
  a Sun lamp — the sun's direction, colour and strength;
  Assets   — (excluded) the assets the instances show, imported from Assets/Crab/Models.
Blender is Z-up with forward -Y; the game (Unity) is Y-up with forward +Z and X mirrored:
  unity = (-x, z, -y), unity yaw = -(blender Z rotation).
"""
import json
import math
import os
import tempfile
import bpy
import numpy as np
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
MODELS = os.path.join(ROOT, "Assets", "Crab", "Models")
BLENDS = os.path.join(ROOT, "Levels")


def to_unity(v):
    return [-v[0], v[2], -v[1]]


def from_unity(x, y, z):
    """A game position (x, height, z) in Blender's space."""
    return Vector((-x, -z, y))


def collection(name, parent=None):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in (parent or bpy.context.scene.collection).children:
        (parent or bpy.context.scene.collection).children.link(c)
    return c


def asset(path):
    """The collection showing an asset ("World/palm", or "World/beach_rocks/Rock2" for one child of a
    model), imported once from its GLB into the Assets collection."""
    name = "asset:" + path
    if name in bpy.data.collections:
        return bpy.data.collections[name]
    parts = path.split("/")
    glb = os.path.join(MODELS, *parts[:2]) + ".glb"
    lib = collection("Assets")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=glb)
    new = [o for o in bpy.data.objects if o not in before]
    keep = new
    if len(parts) > 2:   # one named child (its subtree), moved to the origin
        top = next(o for o in new if o.name.split(".")[0] == parts[2])
        keep = [top] + list(top.children_recursive)
        top.parent = None
        top.location = (0, 0, 0)
        for o in new:
            if o not in keep:
                bpy.data.objects.remove(o)
    c = bpy.data.collections.new(name)
    lib.children.link(c)
    for o in keep:
        for uc in list(o.users_collection):
            uc.objects.unlink(o)
        c.objects.link(o)
    return c


def exclude_assets():
    """The Assets collection stays out of the view (its instances show it)."""
    lc = bpy.context.view_layer.layer_collection.children.get("Assets")
    if lc:
        lc.exclude = True


def instance(path, where, yaw=0.0, scale=1.0, into="Props", name=None, **props):
    """An empty showing an asset, at a Blender position, turned `yaw` degrees about Z."""
    e = bpy.data.objects.new(name or path.split("/")[-1], None)
    e.instance_type = "COLLECTION"
    e.instance_collection = asset(path)
    e.location = where
    e.rotation_euler = (0, 0, math.radians(yaw))
    e.scale = (scale,) * 3
    e["asset"] = path
    for k, v in props.items():
        e[k] = v
    collection(into).objects.link(e)
    return e


def marker(name, where, yaw=0.0, into="Game", **props):
    """A plain empty for the game's logic."""
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "ARROWS"
    e.empty_display_size = 0.8
    e.location = where
    e.rotation_euler = (0, 0, math.radians(yaw))
    for k, v in props.items():
        e[k] = v
    collection(into).objects.link(e)
    return e


def describe(ob):
    """One object of the layout, in the game's terms."""
    loc, rot, scl = ob.matrix_world.decompose()
    fwd = rot @ Vector((0, -1, 0))
    yaw = math.degrees(math.atan2(-fwd.x, -fwd.y))   # the game's yaw from the forward on the ground
    d = {"name": ob.name, "pos": [round(x, 4) for x in to_unity(loc)], "yaw": round(yaw, 2), "scale": round(scl.x, 4)}
    for k in ob.keys():
        if k.startswith("_") or k == "cycles":
            continue
        v = ob[k]
        d[k] = v if isinstance(v, (int, float, str)) else list(v)
    return d


SUN = 2.5   # a sun lamp's strength in Blender per unit of the game's light intensity


def sun_of(scene):
    sun = next((o for o in scene.objects if o.type == "LIGHT" and o.data.type == "SUN"), None)
    if not sun:
        return None
    d = sun.matrix_world.to_quaternion() @ Vector((0, 0, -1))
    c = sun.data.color
    return {"dir": [round(x, 4) for x in to_unity(d)], "color": [round(c[0], 3), round(c[1], 3), round(c[2], 3)],
            "intensity": round(sun.data.energy / SUN, 3)}


def write_layout(path, scene, ground_glb):
    objs = []
    for cname in ("Props", "Game"):
        c = bpy.data.collections.get(cname)
        if not c:
            continue
        for ob in c.all_objects:
            if ob.type == "EMPTY":
                d = describe(ob)
                d["group"] = cname
                objs.append(d)
    data = {"ground": ground_glb, "sun": sun_of(scene), "objects": objs}
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    print("LAYOUT", path, len(objs), "objects")


def embed_panel():
    """The Crab tab (crab_panel.py) inside the .blend: it registers itself when the file opens."""
    src = os.path.join(os.path.dirname(os.path.abspath(__file__)), "crab_panel.py")
    t = bpy.data.texts.get("crab_panel.py") or bpy.data.texts.new("crab_panel.py")
    t.clear()
    t.write(open(src, encoding="utf-8").read())
    t.use_module = True


def periodic_noise(n, cells, rng):
    """Tileable value noise n×n with `cells` lattice cells a side (smooth, wraps around)."""
    g = rng.random((cells, cells))
    u = np.arange(n) * cells / n
    i0 = np.floor(u).astype(int)
    f = u - i0
    f = f * f * (3 - 2 * f)
    i1 = (i0 + 1) % cells
    a = g[i0][:, i0] * (1 - f)[None, :] + g[i0][:, i1] * f[None, :]
    b = g[i1][:, i0] * (1 - f)[None, :] + g[i1][:, i1] * f[None, :]
    return a * (1 - f)[:, None] + b * f[:, None]


def image(name, rgb, colorspace):
    """A picture from an n×n×3 array, packed into the .blend (it carries its own pictures)."""
    n = rgb.shape[0]
    im = bpy.data.images.new(name, n, n, alpha=False)
    im.colorspace_settings.name = colorspace   # before the pixels: setting it makes a new image anew
    px = np.ones((n, n, 4), dtype=np.float32)
    px[..., :3] = rgb[::-1]   # Blender's rows go bottom-up
    im.pixels.foreach_set(px.ravel())
    im.update()
    im.filepath_raw = os.path.join(tempfile.gettempdir(), name + ".png")
    im.file_format = "PNG"
    im.save()
    im.pack()
    return im


def normal_map(hgt, strength=6):
    """A tangent normal map (0..1) from a tileable height."""
    dx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * strength
    dy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * strength
    nrm = np.dstack((-dx, -dy, np.ones_like(dx)))
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    return nrm * 0.5 + 0.5


def ground_mesh(area, step, height, tile, material):
    """The land: a grid over `area` (game x, z, width, depth) at `height(x, z)` (game coordinates),
    UVs in metres × tile, an AO colour layer for the export, the material."""
    x0, z0, w, d = area
    nx, nz = int(w / step) + 1, int(d / step) + 1
    verts = [from_unity(x0 + i * step, height(x0 + i * step, z0 + j * step), z0 + j * step) for j in range(nz) for i in range(nx)]
    faces = [(j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i) for j in range(nz - 1) for i in range(nx - 1)]
    me = bpy.data.meshes.new("Ground")
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.update()
    if me.polygons[0].normal.z < 0:   # the land faces up
        me.flip_normals()
    uv = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            v = verts[me.loops[li].vertex_index]
            uv.data[li].uv = (-v.x * tile, -v.y * tile)
    me.color_attributes.new("AO", "BYTE_COLOR", "POINT")
    ob = bpy.data.objects.new("Ground", me)
    collection("Ground").objects.link(ob)
    ob["collider"] = "mesh"
    me.materials.append(material)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def tiled_material(name, color_img, normal_img, rough=0.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage")
    tc.image = color_img
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = normal_img
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = rough
    return m
