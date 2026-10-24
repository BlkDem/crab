"""
Shared Blender helpers for "Краб против ада": scene reset with GPU baking, bmesh and modifier
utilities, procedural Cycles materials (stone, rock, wood, metal...), baking procedural
materials down to PBR textures (albedo with AO, normal, roughness/metallic) and GLB export.

Taken from the Living Aquarium's generators (tools/blender/make_decor.py, make_critters.py).

Coordinate convention after glTF export (Unity via glTFast): +Y up, the model's front faces
Blender -Y; glTFast mirrors X on import, the game code accounts for it. Units are metres.
"""
import bpy
import bmesh
import math
import os
import random
import sys
import numpy as np
import addon_utils
from mathutils import Vector, Matrix, Euler, Quaternion, noise

TAU = math.pi * 2
OUT = None
TEX = 1024
UP = Vector((0, 0, 1))
FWD = Vector((0, -1, 0))    # a model's forward in Blender space

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    addon_utils.enable("cycles", default_set=True, persistent=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 24
    sc.cycles.use_denoising = False
    sc.render.bake.margin = 6
    sc.cycles.device = "GPU" if _gpu() else "CPU"


_GPU = None  # detected backend, re-applied after every factory reset


def _gpu():
    """Enable a GPU backend for Cycles if the machine has one (baking is much faster)."""
    global _GPU
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for kind in ([_GPU] if _GPU else ("OPTIX", "CUDA", "HIP", "ONEAPI", "METAL")):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            if any(d.type == kind for d in prefs.devices):
                for d in prefs.devices:
                    d.use = d.type == kind
                if _GPU is None:
                    print("Cycles device:", kind)
                _GPU = kind
                return True
        except Exception:
            continue
    _GPU = _GPU or ""
    return False


def link(ob):
    bpy.context.scene.collection.objects.link(ob)
    return ob


def from_bmesh(name, bm, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    return link(bpy.data.objects.new(name, me))


def select_only(*obs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]


def apply_all(ob):
    select_only(ob)
    for m in list(ob.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except RuntimeError:
            ob.modifiers.remove(m)  # disabled (e.g. zero levels): nothing to apply


def join(name, obs):
    obs = [o for o in obs if o]
    select_only(*obs)
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    return ob


def transform_apply(ob):
    select_only(ob)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def set_origin(ob, world_point):
    """Move the object's origin to world_point without moving the geometry."""
    mw = ob.matrix_world.copy()
    local = mw.inverted() @ Vector(world_point)
    ob.data.transform(Matrix.Translation(-local))
    ob.matrix_world = mw @ Matrix.Translation(local)


def mod(ob, kind, **kw):
    m = ob.modifiers.new(kind.lower(), kind)
    for k, v in kw.items():
        setattr(m, k, v)
    return m


def boolean(ob, cutter, op="DIFFERENCE"):
    """Apply a boolean with the exact solver. Cutters must be closed, non-self-intersecting
    meshes (overlapping pieces confuse the solver); an emptied result is a hard error."""
    m = mod(ob, "BOOLEAN", operation=op, object=cutter, solver="EXACT")
    select_only(ob)
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    if not ob.data.polygons:
        raise RuntimeError(f"boolean {op} left {ob.name} empty")


def prim(kind, name, **kw):
    getattr(bpy.ops.mesh, "primitive_" + kind + "_add")(**kw)
    ob = bpy.context.active_object
    ob.name = name
    return ob


def shade_smooth(ob, angle=50):
    select_only(ob)
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(angle))
    except Exception:
        bpy.ops.object.shade_smooth()


def displace(ob, strength, scale, seed=0, kind="VORONOI", levels=0):
    """Organic surface break-up with a procedural texture."""
    if levels:
        mod(ob, "SUBSURF", levels=levels, render_levels=levels)
    tex = bpy.data.textures.new(f"d{seed}", kind if kind != "NOISE" else "CLOUDS")
    if kind == "VORONOI":
        tex.noise_scale = scale
        tex.distance_metric = "DISTANCE"
    else:
        tex.noise_scale = scale
        tex.noise_depth = 2
    m = mod(ob, "DISPLACE", texture=tex, strength=strength, texture_coords="OBJECT", mid_level=0.5)
    return m


class NT:
    """Tiny helper around a material node tree."""

    def __init__(self, name):
        self.mat = bpy.data.materials.new(name)
        self.mat.use_nodes = True
        self.t = self.mat.node_tree
        self.t.nodes.clear()
        self.out = self.n("ShaderNodeOutputMaterial")
        self.bsdf = self.n("ShaderNodeBsdfPrincipled")
        self.l(self.bsdf.outputs["BSDF"], self.out.inputs["Surface"])
        self.coord = self.n("ShaderNodeTexCoord").outputs["Object"]

    def n(self, kind, **kw):
        nd = self.t.nodes.new(kind)
        for k, v in kw.items():
            setattr(nd, k, v)
        return nd

    def l(self, a, b):
        self.t.links.new(a, b)

    def inp(self, nd, key, val):
        s = nd.inputs[key]
        if hasattr(val, "is_linked") or hasattr(val, "links"):
            self.l(val, s)
        else:
            if isinstance(val, (tuple, list)) and len(val) == 3 and len(s.default_value) == 4:
                val = (*val, 1.0)
            s.default_value = val

    # --- building blocks
    def vec(self, scale=(1, 1, 1), loc=(0, 0, 0)):
        m = self.n("ShaderNodeMapping")
        self.l(self.coord, m.inputs["Vector"])
        m.inputs["Scale"].default_value = scale
        m.inputs["Location"].default_value = loc
        return m.outputs["Vector"]

    def noise(self, vec, scale, detail=4, rough=0.55, distortion=0.0, out="Fac"):
        nd = self.n("ShaderNodeTexNoise")
        self.l(vec, nd.inputs["Vector"])
        nd.inputs["Scale"].default_value = scale
        nd.inputs["Detail"].default_value = detail
        nd.inputs["Roughness"].default_value = rough
        nd.inputs["Distortion"].default_value = distortion
        return nd.outputs[out]

    def voronoi(self, vec, scale, feature="F1", rand=1.0, out="Distance"):
        nd = self.n("ShaderNodeTexVoronoi", feature=feature)
        self.l(vec, nd.inputs["Vector"])
        nd.inputs["Scale"].default_value = scale
        nd.inputs["Randomness"].default_value = rand
        return nd.outputs[out]

    def ramp(self, fac, stops):
        """stops: [(pos, (r,g,b)) or (pos, value)]"""
        nd = self.n("ShaderNodeValToRGB")
        self.l(fac, nd.inputs["Fac"])
        els = nd.color_ramp.elements
        while len(els) < len(stops):
            els.new(0.5)
        for e, (p, c) in zip(els, stops):
            e.position = p
            if not isinstance(c, (tuple, list)):
                c = (c, c, c)
            e.color = (*c, 1.0)
        return nd.outputs["Color"]

    def mix(self, fac, a, b, blend="MIX"):
        nd = self.n("ShaderNodeMix", data_type="RGBA", blend_type=blend)
        for key, v in (("Factor_Float", fac), ("A_Color", a), ("B_Color", b)):
            s = next(i for i in nd.inputs if i.identifier == key)
            if isinstance(v, (tuple, list)):
                s.default_value = (*v, 1.0) if len(v) == 3 else v
            elif isinstance(v, (int, float)):
                s.default_value = v
            else:
                self.l(v, s)
        return next(o for o in nd.outputs if o.identifier == "Result_Color")

    def math(self, op, a, b=0.0, clamp=False):
        nd = self.n("ShaderNodeMath", operation=op, use_clamp=clamp)
        for s, v in zip(nd.inputs, (a, b)):
            if isinstance(v, (int, float)):
                s.default_value = v
            else:
                self.l(v, s)
        return nd.outputs[0]

    def sep(self, vec):
        nd = self.n("ShaderNodeSeparateXYZ")
        self.l(vec, nd.inputs["Vector"])
        return nd.outputs

    def comb(self, x, y, z):
        nd = self.n("ShaderNodeCombineXYZ")
        for sock, v in zip(nd.inputs, (x, y, z)):
            if isinstance(v, (int, float)):
                sock.default_value = v
            else:
                self.l(v, sock)
        return nd.outputs[0]

    def geo(self, out):
        return self.n("ShaderNodeNewGeometry").outputs[out]

    def ao(self, dist=0.15):
        nd = self.n("ShaderNodeAmbientOcclusion", samples=16)
        nd.inputs["Distance"].default_value = dist
        return nd.outputs["AO"]

    def bump(self, height, strength=0.6, dist=0.02):
        nd = self.n("ShaderNodeBump")
        self.l(height, nd.inputs["Height"])
        nd.inputs["Strength"].default_value = strength
        nd.inputs["Distance"].default_value = dist
        return nd.outputs["Normal"]

    def finish(self, color, rough=0.8, metal=0.0, normal=None, emission=None, alpha=None):
        b = self.bsdf
        self.inp(b, "Base Color", color)
        self.inp(b, "Roughness", rough)
        self.inp(b, "Metallic", metal)
        if normal is not None:
            self.l(normal, b.inputs["Normal"])
        if emission is not None:
            col, strength = emission
            self.inp(b, "Emission Color", col)
            b.inputs["Emission Strength"].default_value = strength
        if alpha is not None:
            self.inp(b, "Alpha", alpha)
        return self.mat


def C(hexstr):
    h = hexstr.lstrip("#")
    return tuple((int(h[i:i + 2], 16) / 255) ** 2.2 for i in (0, 2, 4))  # sRGB -> linear


def moss_mask(t, amount=0.5, up=0.55, scale=6.0):
    """Moss/algae on upward-facing surfaces, broken up by noise."""
    nz = t.sep(t.geo("Normal"))[2]
    upw = t.math("MULTIPLY", t.math("SUBTRACT", nz, up), 4.0, clamp=True)
    n = t.noise(t.vec(), scale, detail=6, rough=0.6)
    patch = t.ramp(n, [(0.58 - amount * 0.3, 0.0), (0.66 - amount * 0.28, 1.0)])
    return t.math("MULTIPLY", upw, patch)


def mat_stone(name, base="#8d8a83", dark="#4e4b45", light="#b9b4a8", block=6.0, stretch=(1, 1, 1.6),
              mortar=0.06, moss=0.35, dirt=0.6, rough=0.85):
    """Rubble masonry: irregular stones (voronoi cells) with recessed mortar joints."""
    t = NT(name)
    v = t.vec(scale=stretch)
    cell_rand = t.voronoi(v, block, out="Color")
    edge = t.voronoi(v, block, feature="DISTANCE_TO_EDGE")
    joint = t.ramp(edge, [(0.0, 0.0), (mortar, 1.0)])          # 0 in the joints, 1 on the stone
    joint_f = t.math("ADD", joint, 0.0)
    grain = t.noise(t.vec(), 28, detail=8, rough=0.7)
    tint = t.ramp(t.math("ADD", t.sep(cell_rand)[0], t.math("MULTIPLY", grain, 0.35)),
                  [(0.15, C(dark)), (0.55, C(base)), (0.95, C(light))])
    col = t.mix(joint_f, C("#2c2a26"), tint)
    # weathering: darker in crevices
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.25)), dirt), col, C("#1d1b17"))
    if moss > 0:
        m = moss_mask(t, moss)
        mcol = t.ramp(t.noise(t.vec(), 40, detail=6), [(0.3, C("#2f4a1c")), (0.7, C("#5d7d2a"))])
        col = t.mix(m, col, mcol)
    height = t.math("ADD", t.math("MULTIPLY", joint_f, 0.8), t.math("MULTIPLY", grain, 0.2))
    return t.finish(col, rough=rough, normal=t.bump(height, 0.8, 0.03))


def mat_rock(name, base="#7b746a", dark="#3f3a34", light="#a59c8f", moss=0.3, scale=3.0):
    t = NT(name)
    n1 = t.noise(t.vec(), scale, detail=6, rough=0.6, distortion=0.4)
    strata = t.noise(t.vec(scale=(1, 1, 6)), scale * 0.6, detail=3)
    # a few fissures, not a full cell network: voronoi edges masked by noise
    cr = t.voronoi(t.vec(), scale * 1.1, feature="DISTANCE_TO_EDGE")
    fissure = t.ramp(cr, [(0.0, 0.0), (0.018, 1.0)])
    keep = t.ramp(t.noise(t.vec(), scale * 1.5, detail=2), [(0.5, 1.0), (0.58, 0.0)])
    crack = t.math("MAXIMUM", fissure, keep)
    col = t.ramp(t.math("ADD", t.math("MULTIPLY", n1, 0.7), t.math("MULTIPLY", strata, 0.3)),
                 [(0.25, C(dark)), (0.5, C(base)), (0.8, C(light))])
    col = t.mix(t.math("SUBTRACT", 1.0, crack), col, C("#26221d"))
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.3)), 0.7), col, C("#1a1714"))
    if moss > 0:
        mcol = t.ramp(t.noise(t.vec(), 45, detail=6), [(0.3, C("#2d4619")), (0.7, C("#62852b"))])
        col = t.mix(moss_mask(t, moss, up=0.6), col, mcol)
    height = t.math("ADD", t.math("MULTIPLY", crack, 0.4), t.math("MULTIPLY", n1, 0.6))
    return t.finish(col, rough=0.9, normal=t.bump(height, 0.9, 0.05))


def mat_wood(name, base="#6b4a2e", dark="#3a2716", light="#9a7048", rings=14.0, along="Z", moss=0.0, planks=0.0, rough=0.82,
             seam_axis=None, distortion=1.5, staves=0):
    t = NT(name)
    stretch = {"X": (0.15, 1, 1), "Y": (1, 0.15, 1), "Z": (1, 1, 0.15)}

    def grains(sc):
        return (t.noise(t.vec(scale=sc), rings, detail=8, rough=0.6, distortion=distortion),
                t.noise(t.vec(scale=sc), rings * 6, detail=4))
    if along == "XY":
        # boards on a box: grain runs along X on faces looking along Y, along Y on the ends
        (gx, fx), (gy, fy) = grains(stretch["X"]), grains(stretch["Y"])
        w = t.ramp(t.math("ABSOLUTE", t.sep(t.geo("Normal"))[0]), [(0.5, 0.0), (0.6, 1.0)])

        def blend(a, b):
            return t.math("ADD", t.math("MULTIPLY", a, t.math("SUBTRACT", 1.0, w)), t.math("MULTIPLY", b, w))
        grain, fine = blend(gx, gy), blend(fx, fy)
        along = "X"
    else:
        grain, fine = grains(stretch[along])
    col = t.ramp(t.math("ADD", t.math("MULTIPLY", grain, 0.7), t.math("MULTIPLY", fine, 0.3)),
                 [(0.25, C(dark)), (0.55, C(base)), (0.85, C(light))])
    height = t.math("ADD", t.math("MULTIPLY", grain, 0.6), t.math("MULTIPLY", fine, 0.4))
    if planks > 0:
        # plank seams across the "along" axis
        ax = {"X": 0, "Y": 1, "Z": 2}[seam_axis] if seam_axis else {"X": 1, "Y": 0, "Z": 0}[along]
        p = t.sep(t.vec())[ax]
        seam = t.ramp(t.math("FRACT", t.math("MULTIPLY", p, planks)), [(0.0, 0.0), (0.04, 1.0), (0.96, 1.0), (1.0, 0.0)])
        col = t.mix(t.math("SUBTRACT", 1.0, seam), col, C("#1a120a"))
        height = t.math("MULTIPLY", height, seam)
    if staves:
        # barrel staves: seams at regular angles around the object's Z axis
        x, y, _ = t.sep(t.coord)
        f = t.math("FRACT", t.math("ADD", t.math("MULTIPLY", t.math("ARCTAN2", y, x), staves / TAU), 0.5))
        seam = t.ramp(f, [(0.0, 0.0), (0.05, 1.0), (0.95, 1.0), (1.0, 0.0)])
        col = t.mix(t.math("SUBTRACT", 1.0, seam), col, C("#1a120a"))
        height = t.math("MULTIPLY", height, seam)
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.2)), 0.7), col, C("#120c07"))
    if moss > 0:
        mcol = t.ramp(t.noise(t.vec(), 45, detail=6), [(0.3, C("#2b4317")), (0.7, C("#5b7a27"))])
        col = t.mix(moss_mask(t, moss), col, mcol)
    return t.finish(col, rough=rough, normal=t.bump(height, 0.5, 0.02))


def mat_metal(name, base="#b08a3e", patina="#3f7d6a", patina_amt=0.35, rough=0.35, metal_max=1.0):
    """Metal with patina/rust collecting in cavities. metal_max < 1 keeps it readable without
    strong environment reflections (the aquarium has little to reflect)."""
    t = NT(name)
    n = t.noise(t.vec(), 9, detail=6, rough=0.6)
    cav = t.math("SUBTRACT", 1.0, t.ao(0.12))
    p = t.math("ADD", t.math("MULTIPLY", cav, 1.5), t.math("MULTIPLY", n, patina_amt))
    pm = t.ramp(p, [(0.45, 0.0), (0.75, 1.0)])
    col = t.mix(pm, C(base), C(patina))
    metal = t.ramp(p, [(0.45, metal_max), (0.75, 0.0)])
    rgh = t.ramp(p, [(0.45, rough), (0.75, 0.85)])
    return t.finish(col, rough=rgh, metal=metal, normal=t.bump(n, 0.25, 0.01))


def mat_plain(name, color, rough=0.5, metal=0.0, emission=None, alpha=None):
    t = NT(name)
    return t.finish(C(color) if isinstance(color, str) else color, rough=rough, metal=metal,
                    emission=(C(emission[0]), emission[1]) if emission else None, alpha=alpha)


def uv_unwrap(ob, margin=0.004):
    me = ob.data
    if len(me.uv_layers):   # keep existing layers (materials may read them): unwrap into a new one
        uv = me.uv_layers.new(name="BakeUV")
        me.uv_layers.active = uv
        uv.active_render = True
    select_only(ob)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=margin, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def _img(name, size, non_color=False):
    img = bpy.data.images.new(name, size, size, alpha=False, float_buffer=False)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    return img


def _set_target(ob, img):
    for slot in ob.material_slots:
        nt = slot.material.node_tree
        nd = nt.nodes.get("__bake__") or nt.nodes.new("ShaderNodeTexImage")
        nd.name = "__bake__"
        nd.image = img
        nt.nodes.active = nd


def _pixels(img):
    a = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def _save(img, arr, path, non_color=False):
    img.pixels.foreach_set(arr.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    out = bpy.data.images.load(path)
    if non_color:
        out.colorspace_settings.name = "Non-Color"
    return out


def _metal_emit_swap(ob, on):
    """Bake helper: route each material's Metallic input into an emission shader."""
    for slot in ob.material_slots:
        nt = slot.material.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        if on:
            em = nt.nodes.new("ShaderNodeEmission")
            em.name = "__em__"
            src = bsdf.inputs["Metallic"]
            if src.is_linked:
                nt.links.new(src.links[0].from_socket, em.inputs["Color"])
            else:
                v = src.default_value
                em.inputs["Color"].default_value = (v, v, v, 1)
            nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
        else:
            nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
            em = nt.nodes.get("__em__")
            if em:
                nt.nodes.remove(em)


def bake(ob, size=TEX, ao_strength=0.55, metal=True):
    """Bake the object's procedural materials into one PBR material with textures."""
    key = ob.name
    tmp = os.path.join(OUT, "_tex")
    os.makedirs(tmp, exist_ok=True)
    apply_all(ob)
    first = next(s.material for s in ob.material_slots if s.material)
    for slot in ob.material_slots:
        if slot.material is None:
            slot.material = first  # e.g. faces left by a boolean cutter
    uv_unwrap(ob)
    select_only(ob)
    bk = bpy.context.scene.render.bake

    img = _img(key + "_c", size)
    _set_target(ob, img)
    bk.use_pass_direct = bk.use_pass_indirect = False
    bk.use_pass_color = True
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_clear=True, margin=6)
    col = _pixels(img).copy()

    ao_img = _img(key + "_ao", size, True)
    _set_target(ob, ao_img)
    bpy.ops.object.bake(type="AO", use_clear=True, margin=6)
    ao = _pixels(ao_img)[..., :1]
    col[..., :3] *= (1 - ao_strength) + ao_strength * ao

    nrm_img = _img(key + "_n", size, True)
    _set_target(ob, nrm_img)
    bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", use_clear=True, margin=6)
    nrm = _pixels(nrm_img).copy()

    r_img = _img(key + "_r", size, True)
    _set_target(ob, r_img)
    bpy.ops.object.bake(type="ROUGHNESS", use_clear=True, margin=6)
    rough = _pixels(r_img).copy()

    met = None
    if metal:
        m_img = _img(key + "_m", size, True)
        _set_target(ob, m_img)
        _metal_emit_swap(ob, True)
        bpy.ops.object.bake(type="EMIT", use_clear=True, margin=6)
        _metal_emit_swap(ob, False)
        met = _pixels(m_img).copy()
        if met[..., 0].max() < 0.02:
            met = None

    # glTF wants metallic/roughness in one texture (G = roughness, B = metallic)
    orm = np.ones_like(rough)
    orm[..., 0] = ao[..., 0]
    orm[..., 1] = rough[..., 0]
    orm[..., 2] = met[..., 0] if met is not None else 0.0

    c_img = _save(img, col, os.path.join(tmp, key + "_c.png"))
    n_img = _save(nrm_img, nrm, os.path.join(tmp, key + "_n.png"), True)
    o_img = _save(r_img, orm, os.path.join(tmp, key + "_orm.png"), True)

    m = bpy.data.materials.new(key)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tc = nt.nodes.new("ShaderNodeTexImage"); tc.image = c_img
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = n_img
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    to = nt.nodes.new("ShaderNodeTexImage"); to.image = o_img
    sp = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(to.outputs["Color"], sp.inputs["Color"])
    nt.links.new(sp.outputs["Green"], bsdf.inputs["Roughness"])
    nt.links.new(sp.outputs["Blue"], bsdf.inputs["Metallic"])
    ob.data.materials.clear()
    ob.data.materials.append(m)
    me = ob.data
    for layer in [l for l in me.uv_layers if l.name != me.uv_layers.active.name]:
        me.uv_layers.remove(layer)   # the game only needs the baked layout
    return ob


# Textures go into the GLB as JPEG (quality 90) from the circle of Violence on: several times
# smaller than PNG, and the repository's LFS space is limited. Set CRAB_PNG=1 to export PNG.
IMAGE_FORMAT = "AUTO" if os.environ.get("CRAB_PNG") else "JPEG"


def export(name, obs):
    select_only(*obs)
    path = os.path.join(OUT, name + ".glb")
    kw = dict(filepath=path, export_format="GLB", use_selection=True, export_yup=True,
              export_apply=True, export_texcoords=True, export_normals=True,
              export_materials="EXPORT", export_image_format=IMAGE_FORMAT)
    if IMAGE_FORMAT == "JPEG":
        kw["export_jpeg_quality"] = 90
    bpy.ops.export_scene.gltf(**kw)
    print("WROTE", name, os.path.getsize(path))


def blob(name, r=1.0, seed=0, squash=(1, 1, 0.7), rough=0.25, sub=5, flat_bottom=0.15):
    """A natural rock: subdivided ico-sphere pushed around by layered noise, faceted a bit."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    off = Vector((seed * 3.1, seed * 1.7, seed * 2.3))
    for v in bm.verts:
        n = v.co.normalized()
        d = 1 + noise.noise(n * 1.6 + off) * rough + noise.noise(n * 4.5 + off) * rough * 0.3
        # chiselled facets
        d += abs(noise.noise(n * 2.5 + off * 2)) * -rough * 0.5
        v.co = Vector((n.x * squash[0], n.y * squash[1], n.z * squash[2])) * r * d
        if v.co.z < -r * flat_bottom:
            v.co.z = -r * flat_bottom + (v.co.z + r * flat_bottom) * 0.15
    ob = from_bmesh(name, bm)
    return ob


def decimate(ob, ratio):
    mod(ob, "DECIMATE", ratio=ratio)
    apply_all(ob)


def rock_obj(name, r, seed, squash, rough, mat, sub=5, detail=0.06):
    ob = blob(name, r, seed=seed, squash=squash, rough=rough, sub=sub)
    displace(ob, detail * r, 0.45 * r, seed)
    apply_all(ob)
    ob.data.materials.append(mat)
    return ob


def column(name, h, r, mat, broken=False, seed=0):
    """Fluted column with base and capital."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=r, depth=h, location=(0, 0, h / 2 + 0.12))
    c = bpy.context.active_object
    c.name = name
    bm = bmesh.new()
    bm.from_mesh(c.data)
    bmesh.ops.subdivide_edges(bm, edges=[e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > 1e-4], cuts=12)
    rng = random.Random(seed)
    for v in bm.verts:
        a = math.atan2(v.co.y, v.co.x)
        k = 1 - 0.05 * max(0.0, math.cos(a * 20)) ** 0.5   # flutes
        z = (v.co.z + h / 2) / h
        k *= 1 - 0.06 * z                                 # entasis taper
        v.co.x *= k
        v.co.y *= k
        if broken and z > 0.97:                           # jagged break
            v.co.z -= abs(noise.noise(Vector((v.co.x * 9, v.co.y * 9, seed)))) * 0.25
    bm.to_mesh(c.data)
    bm.free()
    c.data.materials.append(mat)
    parts = [c]
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.06))
    b = bpy.context.active_object
    b.scale = (r * 2.6, r * 2.6, 0.12)
    mod(b, "BEVEL", width=0.02, segments=3)
    b.data.materials.append(mat)
    parts.append(b)
    bpy.ops.mesh.primitive_torus_add(major_radius=r * 1.05, minor_radius=r * 0.18, location=(0, 0, 0.14))
    torus = bpy.context.active_object
    torus.data.materials.append(mat)
    parts.append(torus)
    if not broken:
        bpy.ops.mesh.primitive_cone_add(vertices=32, radius1=r * 0.95, radius2=r * 1.4, depth=0.14, location=(0, 0, h + 0.19))
        cap = bpy.context.active_object
        cap.data.materials.append(mat)
        parts.append(cap)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, h + 0.31))
        ab = bpy.context.active_object
        ab.scale = (r * 3.0, r * 3.0, 0.1)
        mod(ab, "BEVEL", width=0.015, segments=2)
        ab.data.materials.append(mat)
        parts.append(ab)
    for p in parts:
        apply_all(p)
        transform_apply(p)  # origin at the column's foot
    return join(name, parts)


def tube_curve(name, paths, res=5, bevel_res=4):
    """paths: list of [(co, radius), ...] -> one mesh object of round tapered tubes."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = 1.0
    cu.bevel_resolution = bevel_res
    cu.resolution_u = res
    cu.use_fill_caps = True
    for pts in paths:
        sp = cu.splines.new("NURBS")
        sp.points.add(len(pts) - 1)
        for p, (co, r) in zip(sp.points, pts):
            p.co = (*co, 1.0)
            p.radius = r
        sp.order_u = 3
        sp.use_endpoint_u = True
    ob = link(bpy.data.objects.new(name, cu))
    select_only(ob)
    bpy.ops.object.convert(target="MESH")
    return bpy.context.view_layer.objects.active


def catmull(pts, sub):
    """Catmull-Rom resampling of [(Vector, radius), ...] with `sub` steps per segment."""
    out = []
    n = len(pts)
    for i in range(n - 1):
        p0, p1, p2, p3 = (pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, n - 1)])
        for k in range(sub):
            t = k / sub
            t2, t3 = t * t, t * t * t
            w = (-0.5 * t3 + t2 - 0.5 * t, 1.5 * t3 - 2.5 * t2 + 1, -1.5 * t3 + 2 * t2 + 0.5 * t, 0.5 * t3 - 0.5 * t2)
            co = p0[0] * w[0] + p1[0] * w[1] + p2[0] * w[2] + p3[0] * w[3]
            r = p1[1] + (p2[1] - p1[1]) * t
            out.append((co, r))
    out.append(pts[-1])
    return out


def sweep(name, paths, seg=12, sub=4):
    """Round tubes along paths [(Vector, radius), ...] with a "Grain" UV layer: u around the
    tube, v = distance along it, so a material can run fibres along every branch."""
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("Grain")
    for pts in paths:
        P = catmull([(Vector(c), r) for c, r in pts], sub)
        rings, along, prev = [], 0.0, None
        for i, (co, r) in enumerate(P):
            t = (P[min(i + 1, len(P) - 1)][0] - P[max(i - 1, 0)][0]).normalized()
            nrm = t.orthogonal().normalized() if prev is None else (prev - t * prev.dot(t)).normalized()
            prev = nrm
            bi = t.cross(nrm)
            if i:
                along += (P[i][0] - P[i - 1][0]).length
            rings.append(([bm.verts.new(co + (nrm * math.cos(TAU * j / seg) + bi * math.sin(TAU * j / seg)) * r)
                           for j in range(seg)], along))
        for (a, va), (b, vb) in zip(rings, rings[1:]):
            for j in range(seg):
                j2 = (j + 1) % seg
                f = bm.faces.new((a[j], a[j2], b[j2], b[j]))
                for loop, uv in zip(f.loops, ((j / seg, va), ((j + 1) / seg, va), ((j + 1) / seg, vb), (j / seg, vb))):
                    loop[uvl].uv = uv
        for ring, v in (rings[0], rings[-1]):   # caps
            c = bm.verts.new(sum((x.co for x in ring), Vector()) / seg)
            for j in range(seg):
                f = bm.faces.new((ring[j], ring[(j + 1) % seg], c))
                for loop in f.loops:
                    loop[uvl].uv = (0.5, v)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return from_bmesh(name, bm)


def mat_driftwood(name, base="#8a5a3a", dark="#341f12", light="#c09a72", moss=0.2):
    """Weathered driftwood: deep fibres running along each branch (read from the Grain UV)."""
    t = NT(name)
    uv = t.n("ShaderNodeUVMap", uv_map="Grain").outputs["UV"]
    m = t.n("ShaderNodeMapping")
    t.l(uv, m.inputs["Vector"])
    m.inputs["Scale"].default_value = (14, 0.9, 1)
    fib = t.noise(m.outputs["Vector"], 3.0, detail=8, rough=0.65, distortion=0.6)
    grooves = t.ramp(fib, [(0.35, 0.0), (0.55, 1.0)])
    fine = t.noise(t.vec(), 90, detail=3)
    col = t.ramp(t.math("ADD", t.math("MULTIPLY", fib, 0.8), t.math("MULTIPLY", fine, 0.2)), [(0.3, C(dark)), (0.55, C(base)), (0.8, C(light))])
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.12)), 0.8), col, C("#140b05"))
    if moss > 0:
        mcol = t.ramp(t.noise(t.vec(), 45, detail=6), [(0.3, C("#2b4317")), (0.7, C("#5b7a27"))])
        col = t.mix(moss_mask(t, moss), col, mcol)
    return t.finish(col, rough=0.85, normal=t.bump(t.math("ADD", grooves, t.math("MULTIPLY", fine, 0.15)), 0.9, 0.02))


def mat_basalt(name, moss=0.2):
    """Dark craggy volcanic rock with pale mineral flecks."""
    t = NT(name)
    n = t.noise(t.vec(), 4, detail=8, rough=0.7, distortion=0.5)
    fleck = t.ramp(t.voronoi(t.vec(), 70, out="Distance"), [(0.0, 1.0), (0.08, 0.0)])
    pits = t.ramp(t.noise(t.vec(), 25, detail=4), [(0.35, 1.0), (0.45, 0.0)])
    col = t.ramp(n, [(0.25, C("#17181a")), (0.55, C("#35373a")), (0.85, C("#5f6266"))])
    col = t.mix(t.math("MULTIPLY", fleck, 0.6), col, C("#b8b2a6"))
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.25)), 0.85), col, C("#08090a"))
    if moss > 0:
        mcol = t.ramp(t.noise(t.vec(), 45, detail=6), [(0.3, C("#2d4619")), (0.7, C("#62852b"))])
        col = t.mix(moss_mask(t, moss, up=0.65), col, mcol)
    height = t.math("ADD", n, t.math("MULTIPLY", pits, -0.3))
    return t.finish(col, rough=0.8, normal=t.bump(height, 1.0, 0.04))


def craggy(name, r, seed, squash, mat):
    """Angular rock: a blob broken up by cell-shaped facets."""
    ob = blob(name, r, seed=seed, squash=squash, rough=0.3, sub=5)
    displace(ob, 0.16 * r, 0.55 * r, seed)
    displace(ob, 0.05 * r, 0.18 * r, seed + 1)
    apply_all(ob)
    ob.data.materials.append(mat)
    return ob


def lathe(name, prof, steps=48):
    """Surface of revolution around Z from a (radius, z) profile."""
    bm = bmesh.new()
    verts = [bm.verts.new((r, 0, z)) for r, z in prof]
    for a, b in zip(verts, verts[1:]):
        bm.edges.new((a, b))
    bmesh.ops.spin(bm, geom=bm.verts[:] + bm.edges[:], cent=(0, 0, 0), axis=(0, 0, 1), steps=steps, angle=TAU)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return from_bmesh(name, bm)


def mat_leather(name, base="#4a3322", dark="#22160d"):
    t = NT(name)
    n = t.noise(t.vec(), 30, detail=6)
    col = t.ramp(n, [(0.3, C(dark)), (0.7, C(base))])
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.08)), 0.7), col, C("#0d0905"))
    return t.finish(col, rough=0.6, normal=t.bump(n, 0.3, 0.005))


def mat_canvas(name, base="#8a8774", dark="#4f4d40", moss=0.35):
    """Weathered rubberised canvas: fine weave, stains, algae towards the feet."""
    t = NT(name)
    weave = t.noise(t.vec(scale=(1, 1, 6)), 220, detail=2)
    stains = t.noise(t.vec(), 6, detail=5, distortion=0.6)
    col = t.ramp(t.math("ADD", t.math("MULTIPLY", stains, 0.8), t.math("MULTIPLY", weave, 0.2)), [(0.3, C(dark)), (0.7, C(base))])
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.1)), 0.8), col, C("#1d1c16"))
    if moss > 0:
        z = t.sep(t.coord)[2]
        mcol = t.ramp(t.noise(t.vec(), 45, detail=6), [(0.3, C("#2d4619")), (0.7, C("#62852b"))])
        low = t.ramp(t.math("ADD", z, t.math("MULTIPLY", stains, 0.25)), [(0.1, 0.9), (0.45, 0.0)])
        col = t.mix(t.math("MULTIPLY", t.math("ADD", low, moss_mask(t, moss, up=0.6)), moss * 1.4), col, mcol)
    return t.finish(col, rough=0.85, normal=t.bump(t.math("ADD", t.math("MULTIPLY", weave, 0.3), stains), 0.4, 0.01))


def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def decimate_to(ob, target):
    """Collapse the mesh down to about `target` triangles. The smallest faces go first, so the
    hidden inner whorls of a shell and the inside of a body lose detail before the outside."""
    apply_all(ob)
    n = tris(ob)
    if n > target:
        mod(ob, "DECIMATE", ratio=target / n)
        apply_all(ob)


def parent(child, par):
    child.parent = par
    child.matrix_parent_inverse = par.matrix_world.inverted()


def uv_sphere(name, r, loc, scale=(1, 1, 1), seg=24, rings=14):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    for v in bm.verts:
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])) + Vector(loc)
    return from_bmesh(name, bm)


def tube(name, pts, seg=10, sub=3, flat=1.0, flat_axis=None):
    """sweep (round tapered tube with a Grain UV: v = length along) squashed across flat_axis."""
    ob = sweep(name, [pts], seg=seg, sub=sub)
    if flat != 1.0 and flat_axis is not None:
        a = Vector(flat_axis).normalized()
        o = Vector(pts[0][0])
        for v in ob.data.vertices:
            rel = v.co - o
            v.co -= a * rel.dot(a) * (1 - flat)
    return ob


def bake_group(obs, key, size=1024, ao_strength=0.6, ao_dist=0.03):
    """Bake every part's procedural material into ONE texture set shared by all the parts."""
    tmp = os.path.join(OUT, "_tex")
    os.makedirs(tmp, exist_ok=True)
    sc = bpy.context.scene
    if sc.world is None:
        sc.world = bpy.data.worlds.new("w")
    sc.world.light_settings.distance = ao_dist   # AO between the parts, not the whole room
    for ob in obs:
        apply_all(ob)
        me = ob.data
        first = next(m for m in me.materials if m)
        for i, m in enumerate(me.materials):
            if m is None:
                me.materials[i] = first   # e.g. a joint knob joined without a material
        uv = me.uv_layers.new(name="BakeUV")
        me.uv_layers.active = uv
        uv.active_render = True
    # one shared UV layout: unwrap all parts together in multi-object edit mode
    select_only(*obs)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.004, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    select_only(*obs)
    bk = sc.render.bake

    def target(img):
        for ob in obs:
            _set_target(ob, img)

    img = _img(key + "_c", size)
    target(img)
    bk.use_pass_direct = bk.use_pass_indirect = False
    bk.use_pass_color = True
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_clear=True, margin=4)
    col = _pixels(img).copy()

    ao_img = _img(key + "_ao", size, True)
    target(ao_img)
    bpy.ops.object.bake(type="AO", use_clear=True, margin=4)
    ao = _pixels(ao_img)[..., :1]
    col[..., :3] *= (1 - ao_strength) + ao_strength * ao

    nrm_img = _img(key + "_n", size, True)
    target(nrm_img)
    bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", use_clear=True, margin=4)
    nrm = _pixels(nrm_img).copy()

    r_img = _img(key + "_r", size, True)
    target(r_img)
    bpy.ops.object.bake(type="ROUGHNESS", use_clear=True, margin=4)
    rough = _pixels(r_img).copy()
    orm = rough.copy()
    orm[..., 0] = ao[..., 0]
    orm[..., 2] = 0.0

    c_img = _save(img, col, os.path.join(tmp, key + "_c.png"))
    n_img = _save(nrm_img, nrm, os.path.join(tmp, key + "_n.png"), True)
    o_img = _save(r_img, orm, os.path.join(tmp, key + "_orm.png"), True)

    m = bpy.data.materials.new(key)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tc = nt.nodes.new("ShaderNodeTexImage"); tc.image = c_img
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = n_img
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    to = nt.nodes.new("ShaderNodeTexImage"); to.image = o_img
    sp = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(to.outputs["Color"], sp.inputs["Color"])
    nt.links.new(sp.outputs["Green"], bsdf.inputs["Roughness"])
    bsdf.inputs["Metallic"].default_value = 0.0
    for ob in obs:
        me = ob.data
        me.materials.clear()
        me.materials.append(m)
        for layer in [l for l in me.uv_layers if l.name != "BakeUV"]:
            me.uv_layers.remove(layer)


def export_tree(name, root):
    """Export a part hierarchy (root and all its children) as one GLB."""
    obs = [root] + list(root.children_recursive)
    export(name, obs)
    print("TRIS", name, sum(len(p.vertices) - 2 for o in obs if o.type == "MESH" for p in o.data.polygons))
