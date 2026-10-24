"""
Enemies of the first circle (Limbo): the Shade, the Scavenger and the boss, the Guardian of Limbo.

Run headless:
  blender -b -P Tools/blender/make_enemies.py -- <out_dir> [asset ...]

Rigid parts with origins at the joints, one shared baked texture set per creature. Glow* parts
keep a plain emissive material (not baked), so the game can pulse them.

Part names (the contract with Crab.Game enemies):
  shade:    Body > ArmL, ArmR, GlowEyes
  crawler:  Body > Jaw, GlowEyes, Leg{L,R}{0..2}_0 > _1
  guardian: Body > Heart (glow), Head > GlowVisor, ArmL_0 > ArmL_1 > Lantern > GlowLantern,
            ArmR_0 > ArmR_1 > Blade
Origin = ground under the creature; forward = Blender -Y (Unity +Z).
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
from lib import NT, C, UP, join, set_origin, parent, uv_sphere, tube, lathe  # noqa: E402


def glow(name, color, strength=6.0):
    return L.mat_plain(name, color, rough=0.4, emission=(color, strength))


def mat_robe(name, base="#3b4341", dark="#121615"):
    t = NT(name)
    folds = t.noise(t.vec(scale=(3, 3, 0.4)), 4, detail=4, distortion=0.4)
    stains = t.noise(t.vec(), 2.5, detail=5)
    col = t.ramp(t.math("ADD", t.math("MULTIPLY", folds, 0.6), t.math("MULTIPLY", stains, 0.4)), [(0.3, C(dark)), (0.7, C(base))])
    z = t.sep(t.coord)[2]
    # the hem fades into dark rot, the shoulders catch a pale ghostly dust
    col = t.mix(t.ramp(z, [(0.0, 0.85), (0.5, 0.0)]), col, C("#060807"))
    col = t.mix(t.math("MULTIPLY", t.ramp(t.sep(t.geo("Normal"))[2], [(0.4, 0.0), (0.9, 1.0)]), 0.3), col, C("#7a8c86"))
    weave = t.noise(t.vec(), 160, detail=2)
    return t.finish(col, rough=0.92, normal=t.bump(t.math("ADD", folds, t.math("MULTIPLY", weave, 0.2)), 0.5, 0.02))


def mat_bone(name, base="#cfc3a2", dark="#4a4030", rot="#3e4a36"):
    t = NT(name)
    n = t.noise(t.vec(), 7, detail=6, rough=0.6)
    pores = t.voronoi(t.vec(), 60, out="Distance")
    col = t.ramp(n, [(0.3, C(dark)), (0.55, C(base)), (0.8, C("#e8dfc6"))])
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.15)), 0.9), col, C("#14100a"))
    col = t.mix(t.ramp(t.noise(t.vec(), 3, detail=4), [(0.55, 0.0), (0.7, 0.6)]), col, C(rot))
    return t.finish(col, rough=0.6, normal=t.bump(t.math("ADD", n, t.math("MULTIPLY", pores, 0.3)), 0.5, 0.01))


def ragged_hem(ob, below, amount, seed):
    """Tear the bottom of a robe: lift and wobble the vertices below `below`."""
    for v in ob.data.vertices:
        if v.co.z < below:
            k = 1 - v.co.z / below
            a = math.atan2(v.co.y, v.co.x)
            n = noise.noise(Vector((math.cos(a) * 3 + seed, math.sin(a) * 3, 0.5)))
            v.co.z += max(0.0, n) * amount * k
            v.co.x *= 1 + 0.08 * n * k
            v.co.y *= 1 + 0.08 * n * k


# ----------------------------------------------------------------------------- the Shade
def build_shade():
    m_robe = mat_robe("shade_robe")
    m_bone = mat_bone("shade_bone", base="#b9b4a4", dark="#3a3830", rot="#2f3a33")
    robe = lathe("robe", [(0.48, 0.05), (0.44, 0.3), (0.36, 0.7), (0.3, 1.05), (0.33, 1.28), (0.24, 1.38), (0.12, 1.44), (0.0, 1.46)], steps=40)
    L.mod(robe, "SUBSURF", levels=1)
    L.apply_all(robe)
    ragged_hem(robe, 0.35, 0.28, 1.0)
    # the hood: a shell open at the front, and the darkness inside it
    hood = uv_sphere("hood", 1.0, (0, 0, 0), seg=28, rings=18)
    for v in hood.data.vertices:
        v.co = Vector((v.co.x * 0.22, v.co.y * 0.24, v.co.z * 0.28)) + Vector((0, 0.02, 1.6))
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -0.22, 1.57))
    cut = bpy.context.active_object
    cut.scale = (0.26, 0.22, 0.34)
    L.boolean(hood, cut)
    L.mod(hood, "SOLIDIFY", thickness=0.03, offset=-1)
    L.apply_all(hood)
    face = uv_sphere("face", 1.0, (0, 0.04, 1.58), scale=(0.17, 0.17, 0.21), seg=18, rings=12)
    face.data.materials.append(L.mat_plain("shade_void", "#020303", rough=1.0))
    for o in (robe, hood):
        o.data.materials.append(m_robe)
    body = join("Body", [robe, hood, face])
    set_origin(body, (0, 0, 0))
    parts = [body]
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 0.3, 0.0, 1.3))
        el = sh + Vector((sx * 0.08, -0.16, -0.3))
        wr = el + Vector((sx * 0.0, -0.2, -0.16))
        sleeve = tube("sleeve", [(sh, 0.085), (el, 0.1), (wr, 0.14)], seg=14, sub=3)
        sleeve.data.materials.append(m_robe)
        fingers = []
        for k, ang in enumerate((-0.4, 0.0, 0.4)):
            d = Vector((sx * 0.25 * math.sin(ang), -1, -0.6 + 0.1 * k)).normalized()
            p0 = wr + Vector((sx * 0.03 * (k - 1), -0.02, -0.04))
            fingers.append(tube("finger", [(p0, 0.016), (p0 + d * 0.09, 0.012), (p0 + d * 0.17 + Vector((0, 0, -0.04)), 0.004)], seg=6, sub=2))
        for f in fingers:
            f.data.materials.append(m_bone)
        arm = join("Arm" + side, [sleeve] + fingers)
        set_origin(arm, sh)
        parent(arm, body)
        parts.append(arm)
    for o in parts:
        L.shade_smooth(o, 60)
    L.bake_group(parts, "shade", size=1024, ao_dist=0.25)
    m_eye = glow("shade_eyes", "#b8ffd6", 9.0)
    eyes = [uv_sphere("e", 0.022, (sx * 0.06, -0.12, 1.6), scale=(1.4, 0.6, 0.7), seg=10, rings=8) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(m_eye)
    ge = join("GlowEyes", eyes)
    parent(ge, body)
    L.export_tree("shade", body)


# ----------------------------------------------------------------------------- the Scavenger
def build_crawler():
    m_bone = mat_bone("crawler_bone")
    # a long ribcage body: ribs as ridges across the back
    rib = uv_sphere("ribs", 1.0, (0, 0, 0), seg=40, rings=24)
    for v in rib.data.vertices:
        x, y, z = v.co
        r = 1 + 0.12 * max(0.0, math.sin(y * 9.0)) * (1 if z > -0.3 else 0.3)
        v.co = Vector((x * 0.24 * r, y * 0.46, z * 0.17 * r + 0.42))
    # vertebrae along the top of the back, from the neck to the tail
    spine = [uv_sphere("vert", 0.05 - 0.002 * i, (0, -0.32 + i * 0.085, 0.6 - 0.004 * (i - 4) ** 2), seg=10, rings=6) for i in range(9)]
    tail = tube("tail", [((0, 0.42, 0.5), 0.05), ((0, 0.62, 0.42), 0.035), ((0, 0.8, 0.3), 0.02), ((0, 0.92, 0.32), 0.006)], seg=8, sub=3)
    skull = uv_sphere("skull", 1.0, (0, 0, 0), seg=28, rings=18)
    for v in skull.data.vertices:
        x, y, z = v.co
        v.co = Vector((x * 0.15 * (1 - 0.25 * max(0.0, -y)), y * 0.19, z * 0.13)) + Vector((0, -0.52, 0.46))
    for o in [rib, tail, skull] + spine:
        o.data.materials.append(m_bone)
    body = join("Body", [rib, tail, skull] + spine)
    set_origin(body, (0, 0, 0))
    parts = [body]
    jaw = tube("jaw", [((-0.1, -0.5, 0.39), 0.025), ((-0.07, -0.64, 0.37), 0.022), ((0.0, -0.7, 0.36), 0.02), ((0.07, -0.64, 0.37), 0.022), ((0.1, -0.5, 0.39), 0.025)], seg=8, sub=3)
    teeth = [tube("tooth", [(Vector((sx * 0.05, -0.64 - 0.02 * k, 0.385)), 0.012), (Vector((sx * 0.05, -0.645 - 0.02 * k, 0.43)), 0.001)], seg=6, sub=1)
             for sx in (-1, 1) for k in range(2)]
    for o in [jaw] + teeth:
        o.data.materials.append(m_bone)
    jaw = join("Jaw", [jaw] + teeth)
    set_origin(jaw, (0, -0.48, 0.41))
    parent(jaw, body)
    parts.append(jaw)
    for side, sx in (("L", 1), ("R", -1)):
        for i, y in enumerate((-0.25, 0.0, 0.25)):
            hip = Vector((sx * 0.2, y, 0.42))
            out = Vector((sx * 1.0, -0.35 * (1 - i), 0)).normalized()
            knee = hip + out * 0.32 + UP * 0.22
            foot = knee + out * 0.22 - UP * 0.64
            up = tube("u", [(hip, 0.035), (hip + (knee - hip) * 0.5, 0.04), (knee, 0.03)], seg=8, sub=2)
            up = join(f"Leg{side}{i}_0", [up, uv_sphere("k", 0.04, hip, seg=8, rings=6)])
            lo = tube("l", [(knee, 0.03), (knee + (foot - knee) * 0.6, 0.022), (foot, 0.004)], seg=8, sub=2)
            lo = join(f"Leg{side}{i}_1", [lo, uv_sphere("k", 0.034, knee, seg=8, rings=6)])
            for o in (up, lo):
                o.data.materials.clear()
                o.data.materials.append(m_bone)
            set_origin(up, hip)
            set_origin(lo, knee)
            parent(up, body)
            parent(lo, up)
            parts += [up, lo]
    for o in parts:
        L.shade_smooth(o, 55)
    L.bake_group(parts, "crawler", size=1024, ao_dist=0.12)
    m_eye = glow("crawler_eyes", "#9dff7a", 8.0)
    eyes = [uv_sphere("e", 0.024, (sx * 0.065, -0.64, 0.5), seg=10, rings=8) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(m_eye)
    ge = join("GlowEyes", eyes)
    parent(ge, body)
    L.export_tree("crawler", body)


# ----------------------------------------------------------------------------- the Guardian of Limbo
def build_guardian():
    m_armor = L.mat_stone("guardian_armor", base="#5a6062", dark="#23282a", light="#868c8c", block=2.2, stretch=(1, 1, 1.3),
                          mortar=0.025, moss=0.18, dirt=0.8, rough=0.8)
    m_robe = mat_robe("guardian_robe", base="#2f3634", dark="#0b0e0d")
    m_iron = L.mat_metal("guardian_iron", base="#3c3a38", patina="#5a3a20", patina_amt=0.6, rough=0.55, metal_max=0.8)
    m_blade = L.mat_rock("guardian_blade", base="#4c5254", dark="#1d2123", light="#7a8082", moss=0.0, scale=1.2)

    skirt = lathe("skirt", [(1.7, 0.15), (1.55, 0.8), (1.3, 1.6), (1.05, 2.3), (0.92, 2.8), (0.8, 3.3), (0.0, 3.35)], steps=48)
    L.mod(skirt, "SUBSURF", levels=1)
    L.apply_all(skirt)
    ragged_hem(skirt, 1.0, 0.7, 3.0)
    skirt.data.materials.append(m_robe)
    chest = L.blob("chest", 1.0, seed=4, squash=(1.25, 0.8, 1.15), rough=0.12, sub=4)
    for v in chest.data.vertices:
        v.co += Vector((0, 0, 3.7))
    chest.data.materials.append(m_armor)
    belt = bpy.data.objects.new("belt", bpy.data.meshes.new("belt"))
    bpy.ops.mesh.primitive_torus_add(major_radius=0.86, minor_radius=0.2, location=(0, 0, 2.8))
    belt = bpy.context.active_object
    belt.scale = (1.08, 0.9, 1.0)
    belt.data.materials.append(m_iron)
    pads = []
    for sx in (-1, 1):
        p = L.blob("pad", 0.62, seed=5 + sx, squash=(1.1, 1.0, 0.55), rough=0.15, sub=4)
        for v in p.data.vertices:
            v.co += Vector((sx * 1.35, 0, 4.35))
        p.data.materials.append(m_armor)
        pads.append(p)
        for k in range(3):   # spikes along the pauldron
            b = Vector((sx * (1.15 + 0.22 * k), 0.1 - 0.15 * k, 4.6))
            sp = tube("spike", [(b, 0.09), (b + Vector((sx * 0.25, 0.05, 0.5)), 0.01)], seg=8, sub=2)
            sp.data.materials.append(m_iron)
            pads.append(sp)
    body = join("Body", [skirt, chest, belt] + pads)
    set_origin(body, (0, 0, 0))
    parts = [body]

    helm = L.blob("helm", 1.0, seed=9, squash=(0.42, 0.5, 0.62), rough=0.08, sub=4)
    for v in helm.data.vertices:
        v.co += Vector((0, 0, 5.15))
    helm.data.materials.append(m_armor)
    horns = []
    for sx in (-1, 1):
        b = Vector((sx * 0.32, 0.05, 5.45))
        h = tube("horn", [(b, 0.12), (b + Vector((sx * 0.35, 0.15, 0.35)), 0.08), (b + Vector((sx * 0.5, 0.45, 0.85)), 0.04), (b + Vector((sx * 0.42, 0.75, 1.15)), 0.005)], seg=10, sub=3)
        h.data.materials.append(m_iron)
        horns.append(h)
    head = join("Head", [helm] + horns)
    set_origin(head, (0, 0, 4.7))
    parent(head, body)
    parts.append(head)

    arm_parts = {}
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 1.4, 0, 4.2))
        el = sh + Vector((sx * 0.35, -0.35, -1.2))
        wr = el + Vector((sx * -0.05, -0.85, -0.45))
        up = tube("up", [(sh, 0.32), (sh + (el - sh) * 0.5, 0.36), (el, 0.28)], seg=14, sub=2)
        up.data.materials.append(m_armor)
        up = join(f"Arm{side}_0", [up])
        fo = tube("fo", [(el, 0.28), (el + (wr - el) * 0.5, 0.3), (wr, 0.24)], seg=14, sub=2)
        fist = L.blob("fist", 0.3, seed=11 + sx, squash=(1, 1.1, 0.9), rough=0.1, sub=3)
        for v in fist.data.vertices:
            v.co += wr + Vector((0, -0.15, 0))
        for o in (fo, fist):
            o.data.materials.append(m_armor)
        fo = join(f"Arm{side}_1", [fo, fist])
        set_origin(up, sh)
        set_origin(fo, el)
        parent(up, body)
        parent(fo, up)
        parts += [up, fo]
        arm_parts[side] = (fo, wr + Vector((0, -0.15, 0)))

    # the lantern hangs from the left fist on a chain-ring
    fo, hand = arm_parts["L"]
    lc = hand + Vector((0, -0.1, -0.75))
    cage = []
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.42, depth=0.12, location=lc + Vector((0, 0, 0.45)))
    cage.append(bpy.context.active_object)
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.36, depth=0.1, location=lc + Vector((0, 0, -0.45)))
    cage.append(bpy.context.active_object)
    for k in range(8):
        a = k / 8 * math.tau
        p = lc + Vector((math.cos(a) * 0.38, math.sin(a) * 0.38, 0))
        cage.append(tube("bar", [(p + Vector((0, 0, -0.45)), 0.035), (p + Vector((0, 0, 0.45)), 0.035)], seg=6, sub=1))
    cage.append(tube("chain", [(hand, 0.05), (hand + Vector((0, -0.05, -0.15)), 0.05), (lc + Vector((0, 0, 0.5)), 0.04)], seg=8, sub=2))
    for o in cage:
        o.data.materials.clear()
        o.data.materials.append(m_iron)
    lantern = join("Lantern", cage)
    set_origin(lantern, hand)
    parent(lantern, fo)
    parts.append(lantern)

    # the blade: a huge chipped stone sword held point-down in the right fist
    fo, hand = arm_parts["R"]
    bm = bmesh.new()
    prof = [(0.0, 0.0), (0.34, 0.2), (0.3, 2.6), (0.0, 3.6), (-0.3, 2.6), (-0.34, 0.2)]
    top = [bm.verts.new((x, 0.09, -z)) for x, z in prof]
    bot = [bm.verts.new((x, -0.09, -z)) for x, z in prof]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    n = len(prof)
    for i in range(n):
        bm.faces.new((top[i], top[(i + 1) % n], bot[(i + 1) % n], bot[i]))
    blade = L.from_bmesh("blade", bm, smooth=False)
    L.mod(blade, "BEVEL", width=0.04, segments=2)
    L.mod(blade, "SUBSURF", levels=2)
    L.apply_all(blade)
    L.displace(blade, 0.06, 0.4, 13)
    L.apply_all(blade)
    hilt = tube("hilt", [(Vector((-0.55, 0, 0.12)), 0.08), (Vector((0, 0, 0.18)), 0.1), (Vector((0.55, 0, 0.12)), 0.08)], seg=8, sub=2)
    grip = tube("grip", [(Vector((0, 0, 0.18)), 0.09), (Vector((0, 0, 0.75)), 0.08)], seg=8, sub=1)
    blade.data.materials.append(m_blade)
    for o in (hilt, grip):
        o.data.materials.append(m_iron)
    sword = join("Blade", [blade, hilt, grip])
    sword.data.transform(Matrix.Translation(hand - Vector((0, 0, 0.5))))
    set_origin(sword, hand)
    parent(sword, fo)
    parts.append(sword)

    for o in parts:
        L.shade_smooth(o, 50)
    L.bake_group(parts, "guardian", size=2048, ao_dist=0.6)

    heart = uv_sphere("Heart", 0.3, (0, -0.86, 3.75), scale=(1, 0.6, 1.1), seg=20, rings=14)
    heart.data.materials.append(glow("guardian_heart", "#7dffb2", 10.0))
    parent(heart, body)
    visor = uv_sphere("GlowVisor", 1.0, (0, -0.44, 5.12), scale=(0.3, 0.06, 0.035), seg=16, rings=8)
    visor.data.materials.append(glow("guardian_visor", "#b8ffd6", 12.0))
    parent(visor, head)
    core = uv_sphere("GlowLantern", 0.26, arm_parts["L"][1] + Vector((0, -0.1, -0.75)), seg=18, rings=12)
    core.data.materials.append(glow("guardian_lantern", "#d8ffe0", 14.0))
    parent(core, lantern)
    L.export_tree("guardian", body)


# ----------------------------------------------------------------------------- the second circle: Wrath
def mat_demon(name, base="#8a1c0c", dark="#2a0604", vein="#ff7a1a", veins=0.5, scale=6.0):
    """Demon hide: dark crimson, scaled, cracked with glowing veins of fire (baked in the colour)."""
    t = NT(name)
    n = t.noise(t.vec(), scale, detail=6, rough=0.6)
    scales = t.voronoi(t.vec(), scale * 5, out="Distance")
    col = t.ramp(t.math("ADD", t.math("MULTIPLY", n, 0.7), t.math("MULTIPLY", scales, 0.3)), [(0.25, C(dark)), (0.6, C(base)), (0.85, C("#c4401c"))])
    cr = t.voronoi(t.vec(), scale * 1.4, feature="DISTANCE_TO_EDGE")
    vein_m = t.math("MULTIPLY", t.ramp(cr, [(0.0, 1.0), (0.03, 0.0)]), veins)
    col = t.mix(vein_m, col, C(vein))
    col = t.mix(t.math("MULTIPLY", t.math("SUBTRACT", 1.0, t.ao(0.15)), 0.7), col, C("#100302"))
    return t.finish(col, rough=0.55, normal=t.bump(t.math("ADD", scales, t.math("MULTIPLY", n, 0.5)), 0.6, 0.01))


def mat_horn(name):
    t = NT(name)
    z = t.sep(t.coord)[2]
    col = t.ramp(t.noise(t.vec(scale=(1, 1, 4)), 20, detail=4), [(0.3, C("#1a1410")), (0.7, C("#4a3a2a"))])
    return t.finish(col, rough=0.35)


def limb(name, pts, radius_pairs, mat, origin):
    """A tapered limb along pts [(co, r)], origin at `origin`."""
    ob = tube(name, pts, seg=12, sub=3)
    ob.data.materials.append(mat)
    set_origin(ob, origin)
    return ob


def build_imp():
    m = mat_demon("imp_hide")
    mh = mat_horn("imp_horn")
    torso = uv_sphere("torso", 1.0, (0, 0, 0), seg=24, rings=16)
    for v in torso.data.vertices:
        x, y, z = v.co
        v.co = Vector((x * 0.24 * (1 + 0.25 * max(0, z)), y * 0.18, z * 0.3)) + Vector((0, 0.02, 0.95))
    pelvis = uv_sphere("pelvis", 1.0, (0, 0.02, 0.66), scale=(0.18, 0.14, 0.12), seg=16, rings=10)
    tail = tube("tail", [((0, 0.12, 0.65), 0.06), ((0, 0.4, 0.5), 0.04), ((0, 0.62, 0.62), 0.025), ((0, 0.72, 0.8), 0.006)], seg=8, sub=3)
    for o in (torso, pelvis, tail):
        o.data.materials.append(m)
    body = join("Body", [torso, pelvis, tail])
    set_origin(body, (0, 0, 0))
    parts = [body]
    skull = uv_sphere("skull", 1.0, (0, 0, 0), seg=20, rings=14)
    for v in skull.data.vertices:
        x, y, z = v.co
        v.co = Vector((x * 0.13, y * 0.15 * (1 + 0.3 * max(0, -y)), z * 0.13)) + Vector((0, -0.04, 1.36))
    skull.data.materials.append(m)
    horns = []
    for sx in (-1, 1):
        b = Vector((sx * 0.08, 0.0, 1.45))
        h = tube("horn", [(b, 0.035), (b + Vector((sx * 0.1, 0.05, 0.1)), 0.025), (b + Vector((sx * 0.12, 0.14, 0.22)), 0.004)], seg=8, sub=3)
        h.data.materials.append(mh)
        horns.append(h)
    jaw = tube("jaw", [((-0.08, -0.12, 1.28), 0.025), ((0, -0.2, 1.27), 0.03), ((0.08, -0.12, 1.28), 0.025)], seg=8, sub=2)
    jaw.data.materials.append(m)
    head = join("Head", [skull, jaw] + horns)
    set_origin(head, (0, 0, 1.22))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 0.26, 0, 1.12))
        el = sh + Vector((sx * 0.1, -0.1, -0.28))
        wr = el + Vector((0, -0.18, -0.2))
        arm = tube("arm", [(sh, 0.06), (el, 0.05), (wr, 0.045)], seg=10, sub=3)
        arm.data.materials.append(m)
        claws = []
        for k in range(3):
            d = Vector((sx * 0.06 * (k - 1), -1, -0.5)).normalized()
            p0 = wr + Vector((sx * 0.03 * (k - 1), -0.02, 0))
            c = tube("claw", [(p0, 0.016), (p0 + d * 0.1, 0.012), (p0 + d * 0.17 + Vector((0, 0, -0.04)), 0.002)], seg=6, sub=2)
            c.data.materials.append(mh)
            claws.append(c)
        a = join("Arm" + side, [arm] + claws)
        set_origin(a, sh)
        parent(a, body)
        parts.append(a)
        hip = Vector((sx * 0.12, 0.02, 0.66))
        knee = hip + Vector((sx * 0.03, -0.12, -0.3))
        ank = knee + Vector((0, 0.14, -0.26))
        toe = ank + Vector((0, -0.12, -0.08))
        leg = tube("leg", [(hip, 0.07), (knee, 0.055), (ank, 0.04), (toe, 0.03)], seg=10, sub=3)
        leg.data.materials.append(m)
        leg = join("Leg" + side, [leg])
        set_origin(leg, hip)
        parent(leg, body)
        parts.append(leg)
    for o in parts:
        L.shade_smooth(o, 55)
    L.bake_group(parts, "imp", size=1024, ao_dist=0.1)
    eyes = [uv_sphere("e", 0.022, (sx * 0.05, -0.17, 1.4), scale=(1.3, 0.6, 0.8), seg=10, rings=8) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(glow("imp_eyes", "#ffcf3a", 10.0))
    ge = join("GlowEyes", eyes)
    parent(ge, body)
    L.export_tree("imp", body)


def build_brute():
    m = mat_demon("brute_hide", base="#5a1a10", dark="#1a0603", veins=0.8, scale=3.0)
    mh = mat_horn("brute_horn")
    torso = L.blob("torso", 1.0, seed=21, squash=(0.95, 0.7, 0.9), rough=0.12, sub=4)
    for v in torso.data.vertices:
        v.co = Vector((v.co.x * 1.0, v.co.y, v.co.z)) + Vector((0, 0.1, 2.0))
    belly = L.blob("belly", 0.7, seed=22, squash=(1.0, 0.9, 0.8), rough=0.1, sub=4)
    for v in belly.data.vertices:
        v.co += Vector((0, 0.0, 1.3))
    for o in (torso, belly):
        o.data.materials.append(m)
    body = join("Body", [torso, belly])
    set_origin(body, (0, 0, 0))
    parts = [body]
    skull = L.blob("skull", 0.38, seed=23, squash=(1.0, 1.1, 0.9), rough=0.1, sub=3)
    for v in skull.data.vertices:
        v.co += Vector((0, -0.45, 2.75))
    skull.data.materials.append(m)
    horns = []
    for sx in (-1, 1):
        b = Vector((sx * 0.25, -0.4, 2.95))
        h = tube("horn", [(b, 0.11), (b + Vector((sx * 0.35, 0.05, 0.05)), 0.08), (b + Vector((sx * 0.45, -0.1, 0.35)), 0.012)], seg=10, sub=3)
        h.data.materials.append(mh)
        horns.append(h)
    head = join("Head", [skull] + horns)
    set_origin(head, (0, -0.3, 2.6))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 1.05, 0, 2.45))
        el = sh + Vector((sx * 0.3, -0.2, -0.8))
        wr = el + Vector((0, -0.35, -0.6))
        arm = tube("arm", [(sh, 0.3), (el, 0.26), (wr, 0.24)], seg=14, sub=3)
        fist = L.blob("fist", 0.38, seed=24 + sx, squash=(1, 1.1, 0.95), rough=0.12, sub=3)
        for v in fist.data.vertices:
            v.co += wr + Vector((0, -0.1, -0.15))
        for o in (arm, fist):
            o.data.materials.append(m)
        a = join("Arm" + side, [arm, fist])
        set_origin(a, sh)
        parent(a, body)
        parts.append(a)
        hip = Vector((sx * 0.45, 0.1, 1.05))
        knee = hip + Vector((sx * 0.1, -0.15, -0.5))
        foot = knee + Vector((0, 0.05, -0.55))
        leg = tube("leg", [(hip, 0.3), (knee, 0.27), (foot, 0.3)], seg=12, sub=2)
        leg.data.materials.append(m)
        leg = join("Leg" + side, [leg])
        set_origin(leg, hip)
        parent(leg, body)
        parts.append(leg)
    for o in parts:
        L.shade_smooth(o, 50)
    L.bake_group(parts, "brute", size=1024, ao_dist=0.3)
    core = uv_sphere("GlowCore", 0.22, (0, -0.62, 2.0), scale=(1.2, 0.5, 1.3), seg=16, rings=10)
    core.data.materials.append(glow("brute_core", "#ff8a1a", 10.0))
    parent(core, body)
    eyes = [uv_sphere("e", 0.05, (sx * 0.13, -0.78, 2.8), seg=10, rings=8) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(glow("brute_eyes", "#ffd040", 10.0))
    ge = join("GlowEyes", eyes)
    parent(ge, head)
    L.export_tree("brute", body)


def build_burrower():
    m = mat_demon("burrower_hide", base="#a0562a", dark="#3a1a0a", vein="#ffb040", veins=0.25, scale=5.0)
    mh = mat_horn("burrower_fang")
    segs = []
    for i in range(7):
        y = -0.4 + i * 0.28
        r = 0.32 - 0.03 * i
        sg = uv_sphere("seg", 1.0, (0, y, r * 0.9), scale=(r, 0.2, r * 0.85), seg=18, rings=12)
        sg.data.materials.append(m)
        segs.append(sg)
        for sx in (-1, 1):   # spiny ridges
            sp = tube("spine", [(Vector((sx * r * 0.5, y, r * 1.6)), 0.04), (Vector((sx * r * 0.7, y + 0.06, r * 2.1)), 0.004)], seg=6, sub=1)
            sp.data.materials.append(mh)
            segs.append(sp)
    head = uv_sphere("head", 1.0, (0, -0.68, 0.34), scale=(0.34, 0.32, 0.3), seg=20, rings=14)
    head.data.materials.append(m)
    body = join("Body", segs + [head])
    set_origin(body, (0, 0, 0))
    parts = [body]
    for side, sx in (("L", 1), ("R", -1)):
        b = Vector((sx * 0.16, -0.92, 0.3))
        j = tube("mandible", [(b, 0.07), (b + Vector((sx * 0.12, -0.18, 0.02)), 0.05), (b + Vector((sx * 0.02, -0.38, 0.0)), 0.006)], seg=8, sub=3)
        j.data.materials.append(mh)
        j = join("Jaw" + side, [j])
        set_origin(j, b)
        parent(j, body)
        parts.append(j)
    for o in parts:
        L.shade_smooth(o, 55)
    L.bake_group(parts, "burrower", size=1024, ao_dist=0.12)
    eyes = [uv_sphere("e", 0.03, (sx * 0.14 + dx, -0.9, 0.45), seg=8, rings=6) for sx in (-1, 1) for dx in (0.0, 0.05 * sx)]
    for e in eyes:
        e.data.materials.append(glow("burrower_eyes", "#ffe08a", 9.0))
    ge = join("GlowEyes", eyes)
    parent(ge, body)
    L.export_tree("burrower", body)


def mat_magma_rock(name):
    """Black volcanic rock split by cracks full of magma (bright in the colour)."""
    t = NT(name)
    n = t.noise(t.vec(), 1.5, detail=6, rough=0.7, distortion=0.4)
    col = t.ramp(n, [(0.25, C("#0e0a09")), (0.55, C("#2a2220")), (0.85, C("#4a3a34"))])
    cr = t.voronoi(t.vec(), 1.6, feature="DISTANCE_TO_EDGE")
    crack = t.ramp(cr, [(0.0, 1.0), (0.04, 0.0)])
    col = t.mix(crack, col, C("#ff6a10"))
    col = t.mix(t.math("MULTIPLY", t.ramp(cr, [(0.0, 1.0), (0.08, 0.0)]), 0.5), col, C("#a02a08"))
    return t.finish(col, rough=0.8, normal=t.bump(t.math("ADD", n, t.math("MULTIPLY", crack, -0.6)), 1.0, 0.05))


def build_colossus():
    m = mat_magma_rock("colossus_rock")
    mh = mat_horn("colossus_horn")

    def chunk(name, r, loc, squash, seed):
        b = L.blob(name, r, seed=seed, squash=squash, rough=0.22, sub=4)
        for v in b.data.vertices:
            v.co += Vector(loc)
        b.data.materials.append(m)
        return b
    torso = [chunk("chest", 2.2, (0, 0, 8.2), (1.2, 0.85, 1.0), 31), chunk("waist", 1.6, (0, 0.2, 6.2), (1.0, 0.8, 0.8), 32),
             chunk("hip", 1.7, (0, 0.1, 5.0), (1.2, 0.9, 0.6), 33)]
    for k in range(5):
        torso.append(chunk("plate", 0.8, (math.cos(k) * 1.6, -0.9 + 0.2 * k, 7 + k * 0.5), (1, 0.6, 0.8), 40 + k))
    body = join("Body", torso)
    set_origin(body, (0, 0, 0))
    parts = [body]
    skull = chunk("skull", 1.0, (0, -0.6, 10.9), (0.9, 1.0, 0.9), 50)
    horns = []
    for sx in (-1, 1):
        b = Vector((sx * 0.7, -0.4, 11.4))
        h = tube("horn", [(b, 0.32), (b + Vector((sx * 0.8, 0.3, 0.6)), 0.22), (b + Vector((sx * 1.0, 0.9, 1.7)), 0.04)], seg=10, sub=3)
        h.data.materials.append(mh)
        horns.append(h)
    head = join("Head", [skull] + horns)
    set_origin(head, (0, -0.3, 10.2))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 2.7, 0, 9.0))
        el = sh + Vector((sx * 0.8, -0.6, -2.4))
        wr = el + Vector((0, -1.0, -1.8))
        up = [chunk("u", 1.1, sh + Vector((sx * 0.3, 0, -0.6)), (0.9, 0.9, 1.3), 60 + sx), chunk("u2", 0.9, (sh + el) / 2, (0.9, 0.9, 1.3), 62 + sx)]
        a0 = join(f"Arm{side}_0", up)
        fo = [chunk("f", 0.85, (el + wr) / 2, (0.9, 0.9, 1.3), 64 + sx), chunk("fist", 1.1, wr + Vector((0, -0.3, -0.4)), (1, 1.1, 0.9), 66 + sx)]
        a1 = join(f"Arm{side}_1", fo)
        set_origin(a0, sh)
        set_origin(a1, el)
        parent(a0, body)
        parent(a1, a0)
        parts += [a0, a1]
        hip = Vector((sx * 1.3, 0.2, 4.6))
        leg = join(f"Leg{side}", [chunk("thigh", 1.2, hip + Vector((sx * 0.2, 0, -1.4)), (0.9, 0.9, 1.4), 70 + sx),
                                  chunk("shin", 1.1, hip + Vector((sx * 0.3, -0.2, -3.4)), (0.9, 1.0, 1.2), 72 + sx)])
        set_origin(leg, hip)
        parent(leg, body)
        parts.append(leg)
    for o in parts:
        L.shade_smooth(o, 40)
    L.bake_group(parts, "colossus", size=2048, ao_dist=1.0)
    g = glow("colossus_core", "#ffb04a", 12.0)
    for name, loc, par in (("CoreChest", (0, -1.95, 8.3), body), ("CoreL", (2.7, -0.9, 9.5), parts[2]), ("CoreR", (-2.7, -0.9, 9.5), parts[5])):
        c = uv_sphere(name, 0.55, loc, scale=(1, 0.6, 1), seg=18, rings=12)
        c.data.materials.append(g)
        parent(c, par)
    eyes = [uv_sphere("e", 0.16, (sx * 0.38, -1.45, 11.0), scale=(1.4, 0.6, 0.7), seg=12, rings=8) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(glow("colossus_eyes", "#ffe070", 14.0))
    ge = join("GlowEyes", eyes)
    parent(ge, head)
    L.export_tree("colossus", body)



# ---------------------------------------------------------------------- III. the circle of Greed
def mat_gold(name, rough=0.3, patina=0.25):
    return L.mat_metal(name, base="#f0b840", patina="#6a4612", patina_amt=patina, rough=rough, metal_max=0.5)


def build_collector():
    """A collector of Greed: a hunched, pot-bellied demon of sickly olive hide, dragging a sack
    of gold on its back. Body > Head > GlowEyes, ArmL, ArmR, LegL, LegR, Sack."""
    m = mat_demon("collector_hide", base="#6a5a22", dark="#201a06", vein="#ffd040", veins=0.35, scale=5.0)
    mh = mat_horn("collector_horn")
    ms = L.mat_canvas("collector_sack", base="#7a6440", dark="#3a2a14", moss=0.0)
    mg = mat_gold("collector_gold")
    torso = L.blob("torso", 0.36, seed=61, squash=(1.0, 0.9, 0.85), rough=0.1, sub=4)
    for v in torso.data.vertices:
        v.co += Vector((0, -0.02, 0.62))
    belly = L.blob("belly", 0.3, seed=62, squash=(1.0, 0.95, 0.9), rough=0.08, sub=4)
    for v in belly.data.vertices:
        v.co += Vector((0, -0.12, 0.5))
    for o in (torso, belly):
        o.data.materials.append(m)
    body = join("Body", [torso, belly])
    set_origin(body, (0, 0, 0))
    parts = [body]
    skull = L.blob("skull", 0.17, seed=63, squash=(1.0, 1.15, 0.9), rough=0.1, sub=3)
    for v in skull.data.vertices:
        v.co += Vector((0, -0.28, 0.9))
    nose = tube("nose", [((0, -0.42, 0.92), 0.05), ((0, -0.5, 0.86), 0.02)], seg=8, sub=2)
    ears = [tube("ear", [((sx * 0.13, -0.24, 0.95), 0.04), ((sx * 0.3, -0.2, 1.02), 0.008)], seg=8, sub=2, flat=0.4, flat_axis=(0, 1, 0)) for sx in (-1, 1)]
    for o in [skull, nose] + ears:
        o.data.materials.append(m)
    horns = []
    for sx in (-1, 1):
        b = Vector((sx * 0.07, -0.22, 1.03))
        h = tube("horn", [(b, 0.025), (b + Vector((sx * 0.03, 0.05, 0.08)), 0.004)], seg=6, sub=2)
        h.data.materials.append(mh)
        horns.append(h)
    head = join("Head", [skull, nose] + ears + horns)
    set_origin(head, (0, -0.2, 0.85))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 0.3, -0.05, 0.75))
        arm = tube("arm", [(sh, 0.06), (sh + Vector((sx * 0.06, -0.12, -0.22)), 0.05), (sh + Vector((sx * 0.04, -0.2, -0.42)), 0.045)], seg=10, sub=3)
        arm.data.materials.append(m)
        a = join("Arm" + side, [arm])
        set_origin(a, sh)
        parent(a, body)
        parts.append(a)
        hip = Vector((sx * 0.14, 0.0, 0.36))
        leg = tube("leg", [(hip, 0.08), (hip + Vector((0, -0.08, -0.18)), 0.06), (hip + Vector((0, 0.04, -0.34)), 0.05), (hip + Vector((0, -0.08, -0.36)), 0.045)], seg=10, sub=3)
        leg.data.materials.append(m)
        leg = join("Leg" + side, [leg])
        set_origin(leg, hip)
        parent(leg, body)
        parts.append(leg)
    sack = L.blob("sackbody", 0.32, seed=64, squash=(1.0, 0.9, 1.15), rough=0.15, sub=4)
    for v in sack.data.vertices:
        v.co += Vector((0, 0.32, 0.85))
    sack.data.materials.append(ms)
    neck = tube("neck", [((0, 0.32, 1.18), 0.1), ((0, 0.3, 1.28), 0.06)], seg=10, sub=2)
    neck.data.materials.append(ms)
    coins = []
    for k in range(6):
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.04, depth=0.01, location=(random.uniform(-0.06, 0.06), 0.3 + random.uniform(-0.05, 0.05), 1.3 + k * 0.005), rotation=(random.uniform(-0.5, 0.5), random.uniform(-0.5, 0.5), 0))
        c = bpy.context.active_object
        c.data.materials.append(mg)
        coins.append(c)
    sk = join("Sack", [sack, neck] + coins)
    set_origin(sk, (0, 0.15, 0.95))
    parent(sk, body)
    parts.append(sk)
    for o in parts:
        L.shade_smooth(o, 50)
    L.bake_group(parts, "collector", size=1024, ao_dist=0.06)
    eyes = [uv_sphere("e", 0.025, (sx * 0.06, -0.42, 0.97), seg=10, rings=8) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(glow("collector_eyes", "#ffe060", 10.0))
    ge = join("GlowEyes", eyes)
    parent(ge, head)
    L.export_tree("collector", body)


def build_goldguard():
    """A guard of the treasury: a 2.2 m knight in gilded plate over a dark demon body, a tower
    shield on the left arm, a halberd in the right. Body > Head > GlowVisor, ArmL (shield),
    ArmR (halberd), LegL, LegR."""
    mg = mat_gold("guard_gold", rough=0.28)
    md = L.mat_metal("guard_dark", base="#3a3026", patina="#1a120a", patina_amt=0.3, rough=0.4, metal_max=0.6)
    mh = mat_horn("guard_horn")
    chest = lathe("chest", [(0.0, 1.0), (0.32, 1.02), (0.4, 1.25), (0.44, 1.55), (0.36, 1.75), (0.15, 1.82), (0.0, 1.82)], steps=24)
    for v in chest.data.vertices:
        v.co.y *= 0.75
    chest.data.materials.append(mg)
    skirt = lathe("skirt", [(0.0, 1.05), (0.34, 1.05), (0.42, 0.75), (0.0, 0.75)], steps=16)
    skirt.data.materials.append(md)
    pauld = []
    for sx in (-1, 1):
        p = uv_sphere("pauldron", 0.2, (sx * 0.45, 0, 1.7), scale=(1.1, 1, 0.75), seg=16, rings=10)
        p.data.materials.append(mg)
        pauld.append(p)
    body = join("Body", [chest, skirt] + pauld)
    set_origin(body, (0, 0, 0))
    parts = [body]
    helm = lathe("helm", [(0.0, 1.82), (0.17, 1.84), (0.2, 2.0), (0.17, 2.15), (0.0, 2.2)], steps=20)
    helm.data.materials.append(mg)
    crest = tube("crest", [((0, 0.05, 2.15), 0.04), ((0, 0.12, 2.3), 0.03), ((0, 0.25, 2.33), 0.01)], seg=8, sub=2)
    crest.data.materials.append(mg)
    horns = []
    for sx in (-1, 1):
        b = Vector((sx * 0.16, 0, 2.08))
        h = tube("horn", [(b, 0.04), (b + Vector((sx * 0.15, 0.02, 0.1)), 0.025), (b + Vector((sx * 0.18, 0.08, 0.25)), 0.004)], seg=8, sub=2)
        h.data.materials.append(mh)
        horns.append(h)
    head = join("Head", [helm, crest] + horns)
    set_origin(head, (0, 0, 1.85))
    parent(head, body)
    parts.append(head)
    # left arm with the tower shield, held before the body
    shL = Vector((0.45, 0, 1.6))
    armL = tube("arm", [(shL, 0.09), (shL + Vector((0.05, -0.15, -0.3)), 0.08), (shL + Vector((0.0, -0.35, -0.45)), 0.07)], seg=10, sub=2)
    armL.data.materials.append(md)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.1, -0.5, 1.05))
    shield = bpy.context.active_object
    shield.scale = (0.7, 0.08, 1.3)
    L.apply_all(shield)
    L.transform_apply(shield)
    for v in shield.data.vertices:
        v.co.y -= 0.12 * (1 - (v.co.x - 0.1) ** 2 / 0.12)   # curved forward
    L.mod(shield, "BEVEL", width=0.03, segments=2)
    shield.data.materials.append(mg)
    boss = uv_sphere("boss", 0.12, (0.1, -0.62, 1.15), scale=(1, 0.5, 1), seg=14, rings=8)
    boss.data.materials.append(md)
    aL = join("ArmL", [armL, shield, boss])
    set_origin(aL, shL)
    parent(aL, body)
    parts.append(aL)
    shR = Vector((-0.45, 0, 1.6))
    armR = tube("arm", [(shR, 0.09), (shR + Vector((-0.05, -0.1, -0.3)), 0.08), (shR + Vector((-0.05, -0.25, -0.5)), 0.07)], seg=10, sub=2)
    armR.data.materials.append(md)
    hand = shR + Vector((-0.05, -0.28, -0.52))
    shaft = tube("shaft", [(hand + Vector((0, 0, -0.9)), 0.03), (hand + Vector((0, 0, 1.4)), 0.03)], seg=8, sub=1)
    shaft.data.materials.append(md)
    bpy.ops.mesh.primitive_cube_add(size=1, location=hand + Vector((0, -0.12, 1.25)))
    blade = bpy.context.active_object
    blade.scale = (0.03, 0.3, 0.4)
    L.apply_all(blade)
    L.transform_apply(blade)
    for v in blade.data.vertices:
        if v.co.y < hand.y - 0.2:
            v.co.z += 0.12 * (1 if v.co.z > hand.z + 1.25 else -1)
    blade.data.materials.append(mg)
    spike = tube("spike", [(hand + Vector((0, 0, 1.4)), 0.03), (hand + Vector((0, 0, 1.75)), 0.003)], seg=6, sub=1)
    spike.data.materials.append(mg)
    aR = join("ArmR", [armR, shaft, blade, spike])
    set_origin(aR, shR)
    parent(aR, body)
    parts.append(aR)
    for side, sx in (("L", 1), ("R", -1)):
        hip = Vector((sx * 0.18, 0, 0.9))
        leg = tube("leg", [(hip, 0.11), (hip + Vector((0, -0.05, -0.45)), 0.1), (hip + Vector((0, 0.02, -0.85)), 0.09)], seg=10, sub=2)
        leg.data.materials.append(md)
        greave = tube("greave", [(hip + Vector((0, -0.06, -0.45)), 0.115), (hip + Vector((0, 0.0, -0.82)), 0.1)], seg=12, sub=1)
        greave.data.materials.append(mg)
        boot = uv_sphere("boot", 0.12, hip + Vector((0, -0.08, -0.85)), scale=(0.9, 1.5, 0.6), seg=12, rings=8)
        boot.data.materials.append(mg)
        lg = join("Leg" + side, [leg, greave, boot])
        set_origin(lg, hip)
        parent(lg, body)
        parts.append(lg)
    for o in parts:
        L.shade_smooth(o, 40)
    L.bake_group(parts, "goldguard", size=1024, ao_dist=0.08)
    visor = uv_sphere("GlowVisor", 0.05, (0, -0.17, 2.0), scale=(2.4, 0.5, 0.5), seg=12, rings=6)
    visor.data.materials.append(glow("guard_visor", "#ff3a2a", 10.0))
    parent(visor, head)
    L.export_tree("goldguard", body)


def build_usurer():
    """The Usurer King: a bloated demon, seated (about 7 m), in a red robe trimmed with gold, a
    crown with horns, a gem on his chest (his weak point). Body > Head > GlowEyes, Gem,
    ArmL_0 > ArmL_1 (a purse), ArmR_0 > ArmR_1 > Scepter."""
    m = mat_demon("usurer_hide", base="#7a5a20", dark="#2a1a06", vein="#ffcf40", veins=0.25, scale=3.0)
    mr = L.mat_canvas("usurer_robe", base="#6a1018", dark="#2a0408", moss=0.0)
    mg = mat_gold("usurer_gold", rough=0.25)
    mh = mat_horn("usurer_horn")
    belly = L.blob("belly", 2.0, seed=71, squash=(1.1, 0.95, 0.9), rough=0.06, sub=4)
    for v in belly.data.vertices:
        v.co += Vector((0, -0.3, 3.0))
    chest = L.blob("chest", 1.7, seed=72, squash=(1.15, 0.85, 0.8), rough=0.06, sub=4)
    for v in chest.data.vertices:
        v.co += Vector((0, 0.1, 4.6))
    for o in (belly, chest):
        o.data.materials.append(m)
    robe = lathe("robe", [(0.0, 0.0), (2.6, 0.0), (2.5, 1.0), (2.3, 2.4), (0.0, 2.4)], steps=32)
    for v in robe.data.vertices:
        v.co.y = v.co.y * 0.9 - 0.4
    robe.data.materials.append(mr)
    bpy.ops.mesh.primitive_torus_add(major_radius=2.0, minor_radius=0.18, location=(0, -0.3, 2.3))
    belt = bpy.context.active_object
    belt.scale = (1.1, 0.95, 1)
    L.apply_all(belt)
    belt.data.materials.append(mg)
    legs = []
    for sx in (-1, 1):   # seated: thighs forward, shins down
        k = Vector((sx * 1.0, -2.2, 1.6))
        legs.append(tube("leg", [(Vector((sx * 1.0, -0.5, 1.8)), 0.75), (k, 0.6), (k + Vector((0, -0.1, -1.55)), 0.5)], seg=14, sub=2))
        legs[-1].data.materials.append(mr)
        shoe = uv_sphere("shoe", 0.5, k + Vector((0, -0.35, -1.55)), scale=(0.9, 1.6, 0.5), seg=14, rings=8)
        shoe.data.materials.append(mg)
        legs.append(shoe)
    chain = []
    for i in range(14):   # a gold chain across the belly
        a = math.pi * (0.15 + 0.7 * i / 13)
        p = Vector((math.cos(a) * 1.9, -0.3 - math.sin(a) * 1.75, 3.6 - math.sin(a) * 0.5))
        bpy.ops.mesh.primitive_torus_add(major_radius=0.12, minor_radius=0.04, location=p, rotation=(0, math.pi / 2 * (i % 2), a))
        c = bpy.context.active_object
        c.data.materials.append(mg)
        chain.append(c)
    body = join("Body", [belly, chest, robe, belt] + legs + chain)
    set_origin(body, (0, 0, 0))
    parts = [body]
    skull = L.blob("skull", 0.85, seed=73, squash=(1.0, 1.0, 0.95), rough=0.08, sub=4)
    for v in skull.data.vertices:
        v.co += Vector((0, -0.4, 6.2))
    jowls = L.blob("jowls", 0.8, seed=74, squash=(1.2, 0.9, 0.6), rough=0.05, sub=3)
    for v in jowls.data.vertices:
        v.co += Vector((0, -0.6, 5.7))
    nose = tube("nose", [((0, -1.15, 6.25), 0.18), ((0, -1.35, 6.0), 0.1)], seg=10, sub=2)
    for o in (skull, jowls, nose):
        o.data.materials.append(m)
    crown = lathe("crown", [(0.75, 0), (0.78, 0.35), (0.7, 0.35), (0.67, 0.0)], steps=24)
    crown.location = (0, -0.35, 6.85)
    L.transform_apply(crown)
    crown.data.materials.append(mg)
    pts = []
    for k in range(8):
        a = k / 8 * math.tau
        pts.append(tube("point", [((math.cos(a) * 0.72, -0.35 + math.sin(a) * 0.72, 7.15), 0.1), ((math.cos(a) * 0.8, -0.35 + math.sin(a) * 0.8, 7.55), 0.01)], seg=6, sub=1))
        pts[-1].data.materials.append(mg)
    horns = []
    for sx in (-1, 1):
        b = Vector((sx * 0.7, -0.3, 6.6))
        h = tube("horn", [(b, 0.18), (b + Vector((sx * 0.6, 0.1, 0.1)), 0.13), (b + Vector((sx * 0.85, -0.1, 0.7)), 0.02)], seg=10, sub=3)
        h.data.materials.append(mh)
        horns.append(h)
    head = join("Head", [skull, jowls, nose, crown] + pts + horns)
    set_origin(head, (0, -0.2, 5.6))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 1.9, 0.1, 5.2))
        el = sh + Vector((sx * 0.5, -0.6, -1.4))
        wr = el + Vector((-sx * 0.2, -1.3, 0.1))
        up = tube("upper", [(sh, 0.6), (el, 0.5)], seg=14, sub=2)
        up.data.materials.append(m)
        cuff = tube("cuff", [(sh + (el - sh) * 0.2, 0.68), (sh + (el - sh) * 0.55, 0.62)], seg=14, sub=1)
        cuff.data.materials.append(mr)
        a0 = join(f"Arm{side}_0", [up, cuff])
        fo = tube("fore", [(el, 0.45), (wr, 0.38)], seg=14, sub=2)
        fo.data.materials.append(m)
        hand = L.blob("hand", 0.42, seed=75 + sx, squash=(1, 1.1, 0.8), rough=0.06, sub=3)
        for v in hand.data.vertices:
            v.co += wr + Vector((0, -0.3, 0))
        hand.data.materials.append(m)
        rings = []
        for k in range(2):
            bpy.ops.mesh.primitive_torus_add(major_radius=0.14, minor_radius=0.045, location=wr + Vector((sx * 0.15 * (k - 0.5), -0.6, 0.05)), rotation=(math.pi / 2, 0, 0))
            rr = bpy.context.active_object
            rr.data.materials.append(mg)
            rings.append(rr)
        extra = []
        if side == "L":
            purse = L.blob("purse", 0.5, seed=77, squash=(1, 0.9, 1.1), rough=0.1, sub=3)
            for v in purse.data.vertices:
                v.co += wr + Vector((0, -0.45, -0.55))
            purse.data.materials.append(mr)
            extra.append(purse)
        a1 = join(f"Arm{side}_1", [fo, hand] + rings + extra)
        set_origin(a0, sh)
        set_origin(a1, el)
        parent(a0, body)
        parent(a1, a0)
        parts += [a0, a1]
        if side == "R":
            sc = tube("rod", [(wr + Vector((0, -0.3, -1.0)), 0.08), (wr + Vector((0, -0.3, 2.0)), 0.08)], seg=10, sub=1)
            sc.data.materials.append(mg)
            top = uv_sphere("orb", 0.3, wr + Vector((0, -0.3, 2.25)), seg=16, rings=10)
            top.data.materials.append(mg)
            scep = join("Scepter", [sc, top])
            set_origin(scep, wr)
            parent(scep, a1)
            parts.append(scep)
    for o in parts:
        L.shade_smooth(o, 45)
    L.bake_group(parts, "usurer", size=2048, ao_dist=0.6)
    gem = uv_sphere("Gem", 0.5, (0, -1.6, 4.6), scale=(1, 0.55, 1.25), seg=8, rings=6)
    gem.data.materials.append(glow("usurer_gem", "#ff2a44", 9.0))
    parent(gem, body)
    setting = lathe("setting", [(0.6, -0.65), (0.75, 0), (0.6, 0.65)], steps=16)
    eyes = [uv_sphere("e", 0.12, (sx * 0.32, -1.18, 6.4), scale=(1.3, 0.6, 0.7), seg=10, rings=8) for sx in (-1, 1)]
    bpy.data.objects.remove(setting)
    for e in eyes:
        e.data.materials.append(glow("usurer_eyes", "#ffd040", 12.0))
    ge = join("GlowEyes", eyes)
    parent(ge, head)
    L.export_tree("usurer", body)


# ---------------------------------------------------------------------- IV. the circle of Envy
def build_dweller():
    """A dweller of paradise (about 1.6 m, floating): a long pearl-white robe that ends in a wisp,
    long sleeves, a smooth faceless head with two soft eyes, a golden halo (GlowHalo).
    Body > Head > GlowEyes, GlowHalo, ArmL, ArmR."""
    mr = L.mat_plain("dweller_robe", "#f4f0fa", rough=0.6)
    mt = L.mat_plain("dweller_trim", "#e6c46a", rough=0.35, metal=0.4)
    ms = L.mat_plain("dweller_skin", "#f8e8e0", rough=0.5)
    robe = lathe("robe", [(0.0, 0.05), (0.08, 0.05), (0.32, 0.3), (0.38, 0.6), (0.3, 1.0), (0.24, 1.25), (0.16, 1.35), (0.0, 1.38)], steps=24)
    for v in robe.data.vertices:   # the hem drifts in a wisp
        if v.co.z < 0.4:
            v.co.x += math.sin(v.co.y * 9) * 0.03
    robe.data.materials.append(mr)
    sash = lathe("sash", [(0.33, 0.78), (0.35, 0.85), (0.33, 0.92)], steps=24)
    sash.data.materials.append(mt)
    body = join("Body", [robe, sash])
    set_origin(body, (0, 0, 0))
    parts = [body]
    hd = uv_sphere("headball", 0.16, (0, 0, 1.55), scale=(0.9, 0.95, 1.1), seg=20, rings=14)
    hd.data.materials.append(ms)
    hood = lathe("hood", [(0.0, 1.78), (0.15, 1.74), (0.2, 1.6), (0.19, 1.42), (0.15, 1.38)], steps=20)
    for v in hood.data.vertices:
        if v.co.y < -0.05 and v.co.z < 1.72:   # open at the face
            v.co.y *= 0.4
    hood.data.materials.append(mr)
    head = join("Head", [hd, hood])
    set_origin(head, (0, 0, 1.38))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 0.2, 0, 1.25))
        sl = tube("sleeve", [(sh, 0.07), (sh + Vector((sx * 0.08, -0.1, -0.35)), 0.09), (sh + Vector((sx * 0.06, -0.2, -0.6)), 0.12)], seg=10, sub=2)
        sl.data.materials.append(mr)
        hand = uv_sphere("hand", 0.05, sh + Vector((sx * 0.06, -0.24, -0.66)), seg=10, rings=8)
        hand.data.materials.append(ms)
        a = join("Arm" + side, [sl, hand])
        set_origin(a, sh)
        parent(a, body)
        parts.append(a)
    for o in parts:
        L.shade_smooth(o, 50)
    L.bake_group(parts, "dweller", size=512, ao_dist=0.05)
    eyes = [uv_sphere("e", 0.028, (sx * 0.06, -0.135, 1.57), scale=(1, 0.5, 1.3), seg=10, rings=8) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(glow("dweller_eyes", "#9ae0ff", 6.0))
    ge = join("GlowEyes", eyes)
    parent(ge, head)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.2, minor_radius=0.018, location=(0, 0, 1.92))
    halo = bpy.context.active_object
    halo.name = "GlowHalo"
    halo.data.materials.append(glow("dweller_halo", "#ffd870", 8.0))
    set_origin(halo, (0, 0, 1.92))
    parent(halo, head)
    L.export_tree("dweller", body)


def build_envious():
    """An Envious one: the dweller's shape gone wrong — thin, hunched, grey-green hide torn by
    thorns, the robe in rags, a face of mirror shards with green eyes. Body > Head > GlowEyes,
    ArmL, ArmR, LegL, LegR."""
    m = mat_demon("envious_hide", base="#4a5a3a", dark="#141a0e", vein="#8aff5a", veins=0.45, scale=6.0)
    mr = L.mat_canvas("envious_rags", base="#c8c4b8", dark="#3a3a30", moss=0.5)
    mm = L.mat_plain("envious_shard", "#c8d0d8", rough=0.08, metal=0.9)
    mh = mat_horn("envious_thorn")
    torso = L.blob("torso", 0.28, seed=91, squash=(0.9, 0.7, 1.3), rough=0.12, sub=4)
    for v in torso.data.vertices:
        v.co += Vector((0, 0.04, 1.05))
    rags = lathe("rags", [(0.12, 0.98), (0.2, 0.9), (0.27, 0.62), (0.3, 0.42)], steps=24)
    for v in rags.data.vertices:   # a torn, fringed hem
        if v.co.z < 0.5:
            v.co.z += 0.16 * abs(math.sin(math.atan2(v.co.y, v.co.x) * 7))
    L.mod(rags, "SOLIDIFY", thickness=0.015)
    L.apply_all(rags)
    rags.data.materials.append(mr)
    torso.data.materials.append(m)
    thorns = []
    for k in range(7):
        a = random.uniform(0, math.tau)
        b = Vector((math.cos(a) * 0.22, 0.08 + math.sin(a) * 0.15, random.uniform(0.95, 1.3)))
        th = tube("thorn", [(b, 0.03), (b + Vector((math.cos(a) * 0.14, 0.1, 0.08)), 0.003)], seg=6, sub=1)
        th.data.materials.append(mh)
        thorns.append(th)
    body = join("Body", [torso, rags] + thorns)
    set_origin(body, (0, 0, 0))
    parts = [body]
    skull = L.blob("skull", 0.15, seed=92, squash=(0.85, 1.0, 1.15), rough=0.12, sub=3)
    for v in skull.data.vertices:
        v.co += Vector((0, -0.08, 1.45))
    skull.data.materials.append(m)
    shards = []
    for k in range(6):
        bpy.ops.mesh.primitive_cone_add(vertices=3, radius1=0.06, depth=0.16, location=(random.uniform(-0.08, 0.08), -0.21, 1.45 + random.uniform(-0.08, 0.1)),
                                        rotation=(math.pi / 2 + random.uniform(-0.5, 0.5), random.uniform(-0.6, 0.6), random.uniform(0, 3)))
        sh = bpy.context.active_object
        sh.data.materials.append(mm)
        shards.append(sh)
    head = join("Head", [skull] + shards)
    set_origin(head, (0, -0.05, 1.38))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 0.24, 0.02, 1.28))
        arm = tube("arm", [(sh, 0.045), (sh + Vector((sx * 0.08, -0.12, -0.35)), 0.035), (sh + Vector((sx * 0.06, -0.3, -0.6)), 0.03)], seg=8, sub=2)
        arm.data.materials.append(m)
        claws = []
        wr = sh + Vector((sx * 0.06, -0.3, -0.6))
        for k in range(3):
            d = Vector((sx * 0.05 * (k - 1), -1, -0.6)).normalized()
            c = tube("claw", [(wr, 0.014), (wr + d * 0.16, 0.002)], seg=6, sub=1)
            c.data.materials.append(mh)
            claws.append(c)
        a = join("Arm" + side, [arm] + claws)
        set_origin(a, sh)
        parent(a, body)
        parts.append(a)
        hip = Vector((sx * 0.1, 0.03, 0.62))
        leg = tube("leg", [(hip, 0.06), (hip + Vector((0, -0.1, -0.3)), 0.045), (hip + Vector((0, 0.08, -0.55)), 0.035), (hip + Vector((0, -0.06, -0.6)), 0.03)], seg=8, sub=2)
        leg.data.materials.append(m)
        lg = join("Leg" + side, [leg])
        set_origin(lg, hip)
        parent(lg, body)
        parts.append(lg)
    for o in parts:
        L.shade_smooth(o, 45)
    L.bake_group(parts, "envious", size=1024, ao_dist=0.06)
    eyes = [uv_sphere("e", 0.025, (sx * 0.05, -0.23, 1.48), seg=10, rings=8) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(glow("envious_eyes", "#8aff4a", 12.0))
    ge = join("GlowEyes", eyes)
    parent(ge, head)
    L.export_tree("envious", body)


# ---------------------------------------------------------------------- V. the circle of Despondency
def mat_smoke(name, base="#1a1c24", light="#3a3e4c"):
    """Dark smoke-cloth: soft streaks, no shine."""
    t = NT(name)
    n = t.noise(t.vec(scale=(1, 1, 3)), 4, detail=6, distortion=1.0)
    col = t.ramp(n, [(0.3, C(base)), (0.7, C(light))])
    return t.finish(col, rough=0.9)


def build_wraith():
    """A Wraith of despondency (about 1.9 m): a hunched hooded shape of smoke, no legs — the hem
    trails into wisps; very long thin arms; two pale eyes. Body > Head > GlowEyes, ArmL, ArmR."""
    m = mat_smoke("wraith_smoke")
    mh = mat_smoke("wraith_hand", base="#2a2e38", light="#4a4e5a")
    robe = lathe("robe", [(0.0, 0.0), (0.12, 0.05), (0.4, 0.4), (0.42, 0.9), (0.32, 1.4), (0.2, 1.6), (0.0, 1.62)], steps=20)
    for v in robe.data.vertices:
        if v.co.z < 0.5:
            v.co.z += 0.2 * abs(math.sin(math.atan2(v.co.y, v.co.x) * 6))
        v.co.y += 0.15 * (v.co.z / 1.6) ** 2   # hunched
    robe.data.materials.append(m)
    body = join("Body", [robe])
    set_origin(body, (0, 0, 0))
    parts = [body]
    hood = lathe("hood", [(0.0, 1.95), (0.18, 1.9), (0.24, 1.72), (0.22, 1.55), (0.15, 1.5)], steps=16)
    for v in hood.data.vertices:
        if v.co.y < -0.06 and v.co.z < 1.85:
            v.co.y *= 0.35
        v.co.y += 0.12
    hood.data.materials.append(m)
    head = join("Head", [hood])
    set_origin(head, (0, 0.1, 1.55))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 0.28, 0.12, 1.42))
        arm = tube("arm", [(sh, 0.06), (sh + Vector((sx * 0.12, -0.3, -0.45)), 0.04), (sh + Vector((sx * 0.1, -0.55, -0.95)), 0.03)], seg=8, sub=2)
        arm.data.materials.append(mh)
        fingers = []
        tip = sh + Vector((sx * 0.1, -0.55, -0.95))
        for k in range(4):
            d = Vector((sx * 0.04 * (k - 1.5), -0.5, -1)).normalized()
            f = tube("finger", [(tip, 0.012), (tip + d * 0.22, 0.003)], seg=5, sub=1)
            f.data.materials.append(mh)
            fingers.append(f)
        a = join("Arm" + side, [arm] + fingers)
        set_origin(a, sh)
        parent(a, body)
        parts.append(a)
    for o in parts:
        L.shade_smooth(o, 50)
    L.bake_group(parts, "wraith", size=512, ao_dist=0.08)
    eyes = [uv_sphere("e", 0.03, (sx * 0.07, -0.02, 1.72), scale=(1.4, 0.5, 0.6), seg=10, rings=8) for sx in (-1, 1)]
    for e in eyes:
        e.data.materials.append(glow("wraith_eyes", "#c8dcff", 10.0))
    ge = join("GlowEyes", eyes)
    parent(ge, head)
    L.export_tree("wraith", body)


def build_devourer():
    """The Devourer of Light (about 11 m): a heaving mass of darkness, a maw of pale teeth that
    opens (Maw), six long arms (Arm0..5) and, deep inside, a violet heart (Heart).
    Body > Maw, Heart, Arm0..5."""
    m = mat_smoke("devourer_dark", base="#0c0c12", light="#24222e")
    mt = L.mat_plain("devourer_teeth", "#d8d0c0", rough=0.4)
    mass = []
    for k in range(9):
        b = L.blob("mass", random.uniform(2.2, 3.4), seed=600 + k, squash=(1, 0.9, 1.1), rough=0.15, sub=4)
        off = Vector((random.uniform(-2.5, 2.5), random.uniform(0, 2.5), 3 + k * 0.7 + random.uniform(-1, 1)))
        for v in b.data.vertices:
            v.co += off
        b.data.materials.append(m)
        mass.append(b)
    body = join("Body", mass)
    set_origin(body, (0, 0, 0))
    parts = [body]
    # the maw: a lower jaw ring of teeth that swings open
    jaw = lathe("jaw", [(1.8, 0), (2.0, 0.3), (1.7, 0.5), (0.0, 0.3)], steps=24)
    for v in jaw.data.vertices:
        v.co.y *= 0.6
    jaw.data.materials.append(m)
    teeth = []
    for k in range(14):
        a = math.pi * (0.1 + 0.8 * k / 13)
        p = Vector((math.cos(a) * 1.7, -math.sin(a) * 1.0, 0.45))
        t_ = tube("tooth", [(p, 0.14), (p + Vector((0, 0, 0.7)), 0.01)], seg=6, sub=1)
        t_.data.materials.append(mt)
        teeth.append(t_)
    maw = join("Maw", [jaw] + teeth)
    for v in maw.data.vertices:
        v.co += Vector((0, -2.6, 6.2))
    set_origin(maw, (0, -1.6, 6.6))
    parent(maw, body)
    parts.append(maw)
    upper = []
    for k in range(14):
        a = math.pi * (0.1 + 0.8 * k / 13)
        p = Vector((math.cos(a) * 1.7, -2.6 - math.sin(a) * 1.0, 7.4))
        t_ = tube("tooth", [(p, 0.14), (p + Vector((0, 0, -0.7)), 0.01)], seg=6, sub=1)
        t_.data.materials.append(mt)
        upper.append(t_)
    ut = join("UpperTeeth", upper)
    parent(ut, body)
    parts.append(ut)
    for i in range(6):
        sx = 1 if i % 2 == 0 else -1
        base = Vector((sx * (2.4 + (i // 2) * 0.4), -0.5, 9 - (i // 2) * 2.2))
        pts = [(base, 0.45)]
        for j in range(1, 6):
            pts.append((base + Vector((sx * j * 0.9, -j * 0.8, -j * 0.9 + math.sin(j) * 0.4)), 0.45 - j * 0.07))
        arm = tube(f"arm{i}", pts, seg=8, sub=3)
        arm.data.materials.append(m)
        a = join(f"Arm{i}", [arm])
        set_origin(a, base)
        parent(a, body)
        parts.append(a)
    for o in parts:
        L.shade_smooth(o, 50)
    L.bake_group(parts, "devourer", size=1024, ao_dist=0.8)
    heart = uv_sphere("Heart", 1.0, (0, -1.4, 6.8), scale=(1, 0.8, 1.1), seg=16, rings=12)
    heart.data.materials.append(glow("devourer_heart", "#b07aff", 10.0))
    set_origin(heart, (0, -1.4, 6.8))
    parent(heart, body)
    eyes = [uv_sphere("e", 0.22, (sx * 1.2, -2.5, 8.6), scale=(1.5, 0.5, 0.6), seg=10, rings=8) for sx in (-1, -0.4, 0.4, 1)]
    for e in eyes:
        e.data.materials.append(glow("devourer_eyes", "#d8c8ff", 8.0))
    ge = join("GlowEyes", eyes)
    parent(ge, body)
    L.export_tree("devourer", body)


# ---------------------------------------------------------------------- VI. the circle of Betrayal
def build_legionary():
    """A soldier of the legion (2 m): a demon in dark iron plate, a round shield on the left arm,
    a spear in the right, a red visor. Body > Head > GlowVisor, ArmL, ArmR, LegL, LegR."""
    mi = L.mat_metal("legion_iron", base="#3a3a3e", patina="#6a2a1a", patina_amt=0.4, rough=0.45, metal_max=0.6)
    mr = L.mat_canvas("legion_cloth", base="#6a1a14", dark="#2a0806", moss=0.0)
    md = mat_demon("legion_hide", base="#4a1a10", dark="#1a0604", veins=0.3)
    chest = lathe("chest", [(0.0, 1.0), (0.3, 1.0), (0.38, 1.3), (0.4, 1.55), (0.3, 1.72), (0.0, 1.72)], steps=16)
    for v in chest.data.vertices:
        v.co.y *= 0.75
    chest.data.materials.append(mi)
    skirt = lathe("skirt", [(0.0, 1.02), (0.32, 1.02), (0.4, 0.7), (0.0, 0.7)], steps=12)
    skirt.data.materials.append(mr)
    body = join("Body", [chest, skirt])
    set_origin(body, (0, 0, 0))
    parts = [body]
    helm = lathe("helm", [(0.0, 1.72), (0.16, 1.74), (0.19, 1.9), (0.15, 2.04), (0.0, 2.08)], steps=12)
    helm.data.materials.append(mi)
    crest = tube("crest", [((0, 0.15, 1.95), 0.03), ((0, 0.0, 2.15), 0.05), ((0, -0.12, 2.05), 0.02)], seg=8, sub=2, flat=0.3, flat_axis=(1, 0, 0))
    crest.data.materials.append(mr)
    head = join("Head", [helm, crest])
    set_origin(head, (0, 0, 1.74))
    parent(head, body)
    parts.append(head)
    shL = Vector((0.42, 0, 1.55))
    armL = tube("arm", [(shL, 0.08), (shL + Vector((0.05, -0.15, -0.3)), 0.07)], seg=8, sub=1)
    armL.data.materials.append(md)
    shield = lathe("shield", [(0.0, 0.05), (0.45, 0.0), (0.42, -0.05), (0.0, -0.02)], steps=20)
    shield.rotation_euler = (math.pi / 2, 0, 0)
    shield.location = (0.45, -0.38, 1.15)
    L.transform_apply(shield)
    shield.data.materials.append(mi)
    aL = join("ArmL", [armL, shield])
    set_origin(aL, shL)
    parent(aL, body)
    parts.append(aL)
    shR = Vector((-0.42, 0, 1.55))
    armR = tube("arm", [(shR, 0.08), (shR + Vector((-0.05, -0.2, -0.35)), 0.07)], seg=8, sub=1)
    armR.data.materials.append(md)
    hand = shR + Vector((-0.05, -0.22, -0.38))
    spear = tube("spear", [(hand + Vector((0, 0.6, -0.4)), 0.025), (hand + Vector((0, -1.4, 0.6)), 0.025)], seg=6, sub=1)
    spear.data.materials.append(md)
    tip = tube("tip", [(hand + Vector((0, -1.4, 0.6)), 0.06), (hand + Vector((0, -1.75, 0.78)), 0.004)], seg=6, sub=1)
    tip.data.materials.append(mi)
    aR = join("ArmR", [armR, spear, tip])
    set_origin(aR, shR)
    parent(aR, body)
    parts.append(aR)
    for side, sx in (("L", 1), ("R", -1)):
        hip = Vector((sx * 0.17, 0, 0.9))
        leg = tube("leg", [(hip, 0.1), (hip + Vector((0, -0.05, -0.45)), 0.09), (hip + Vector((0, 0.02, -0.85)), 0.08)], seg=8, sub=1)
        leg.data.materials.append(md)
        boot = uv_sphere("boot", 0.11, hip + Vector((0, -0.07, -0.86)), scale=(0.9, 1.5, 0.6), seg=10, rings=6)
        boot.data.materials.append(mi)
        lg = join("Leg" + side, [leg, boot])
        set_origin(lg, hip)
        parent(lg, body)
        parts.append(lg)
    for o in parts:
        L.shade_smooth(o, 40)
    L.bake_group(parts, "legionary", size=512, ao_dist=0.08)
    visor = uv_sphere("GlowVisor", 0.05, (0, -0.16, 1.88), scale=(2.4, 0.5, 0.4), seg=10, rings=6)
    visor.data.materials.append(glow("legion_visor", "#ff3a2a", 10.0))
    parent(visor, head)
    L.export_tree("legionary", body)


def build_guide():
    """The Guide (about 1.5 m): a lanky clockwork figure in a ragged cloak, a round lantern held
    up in its left hand, one big lens of an eye. Body > Head > GlowEye, ArmL > GlowLantern, ArmR, LegL, LegR."""
    mb = L.mat_metal("guide_brass", base="#a8803a", patina="#3a6a5a", patina_amt=0.5, rough=0.4, metal_max=0.6)
    mc = L.mat_canvas("guide_cloak", base="#5a5048", dark="#2a241e", moss=0.0)
    torso = lathe("torso", [(0.0, 0.75), (0.16, 0.78), (0.2, 1.05), (0.15, 1.2), (0.0, 1.22)], steps=12)
    torso.data.materials.append(mb)
    cloak = lathe("cloak", [(0.12, 1.25), (0.28, 1.15), (0.32, 0.8), (0.3, 0.55)], steps=16)
    for v in cloak.data.vertices:
        if v.co.z < 0.65:
            v.co.z += 0.1 * abs(math.sin(math.atan2(v.co.y, v.co.x) * 5))
        if v.co.y < 0:
            v.co.y *= 0.5   # open at the front
    L.mod(cloak, "SOLIDIFY", thickness=0.012)
    L.apply_all(cloak)
    cloak.data.materials.append(mc)
    body = join("Body", [torso, cloak])
    set_origin(body, (0, 0, 0))
    parts = [body]
    hd = uv_sphere("headball", 0.13, (0, 0, 1.36), scale=(1, 1, 1.1), seg=16, rings=10)
    hd.data.materials.append(mb)
    hood = lathe("hood", [(0.0, 1.55), (0.15, 1.5), (0.18, 1.36), (0.15, 1.25)], steps=12)
    for v in hood.data.vertices:
        if v.co.y < -0.05:
            v.co.y *= 0.3
    hood.data.materials.append(mc)
    head = join("Head", [hd, hood])
    set_origin(head, (0, 0, 1.24))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 0.2, 0, 1.15))
        arm = tube("arm", [(sh, 0.03), (sh + Vector((sx * 0.05, -0.12, -0.3)), 0.025), (sh + Vector((sx * 0.04, -0.2, -0.55)), 0.022)], seg=6, sub=1)
        arm.data.materials.append(mb)
        extra = []
        if side == "L":
            cage = lathe("cage", [(0.0, -0.15), (0.08, -0.1), (0.09, 0.05), (0.04, 0.1), (0.0, 0.12)], steps=8)
            cage.location = sh + Vector((sx * 0.04, -0.2, -0.72))
            L.transform_apply(cage)
            cage.data.materials.append(mb)
            extra.append(cage)
        a = join("Arm" + side, [arm] + extra)
        set_origin(a, sh)
        parent(a, body)
        parts.append(a)
        hip = Vector((sx * 0.08, 0, 0.75))
        leg = tube("leg", [(hip, 0.035), (hip + Vector((0, -0.05, -0.38)), 0.03), (hip + Vector((0, 0.03, -0.72)), 0.028)], seg=6, sub=1)
        leg.data.materials.append(mb)
        lg = join("Leg" + side, [leg])
        set_origin(lg, hip)
        parent(lg, body)
        parts.append(lg)
    for o in parts:
        L.shade_smooth(o, 40)
    L.bake_group(parts, "guide", size=512, ao_dist=0.05)
    eye = uv_sphere("GlowEye", 0.05, (0, -0.125, 1.38), scale=(1, 0.4, 1), seg=10, rings=8)
    eye.data.materials.append(glow("guide_eye", "#8ae0ff", 8.0))
    parent(eye, head)
    lamp = uv_sphere("GlowLantern", 0.065, (0.24, -0.2, 0.43), seg=10, rings=8)
    lamp.data.materials.append(glow("guide_lamp", "#ffd890", 10.0))
    parent(lamp, parts[2])
    L.export_tree("guide", body)


def build_janus():
    """The Two-Faced (about 10 m): a mechanical idol — a stepped iron base, a barrel chest, two
    long arms ending in saw discs (ArmL > SawL, ArmR > SawR), and a head with two faces: the
    smiling front (FaceFront, glowing blue) and the true demonic face at the back (FaceBack,
    glowing red). Body > Head > FaceFront, FaceBack, ArmL, ArmR."""
    m = L.mat_metal("janus_iron", base="#4a4a50", patina="#6a3a1c", patina_amt=0.45, rough=0.45, metal_max=0.6)
    mg = L.mat_metal("janus_brass", base="#a8803a", patina="#3a5a4a", patina_amt=0.4, rough=0.4, metal_max=0.6)
    parts = []
    base = lathe("base", [(3.2, 0), (3.2, 0.8), (2.6, 0.8), (2.6, 1.6), (2.0, 1.6), (2.0, 2.2), (0, 2.2)], steps=24)
    base.data.materials.append(m)
    chest = lathe("chest", [(1.6, 2.2), (2.1, 3.0), (2.3, 4.8), (2.0, 6.2), (1.2, 6.8), (0, 6.9)], steps=24)
    chest.data.materials.append(m)
    rings = []
    for z in (3.4, 4.6, 5.8):
        bpy.ops.mesh.primitive_torus_add(major_radius=2.3 - abs(z - 4.6) * 0.15, minor_radius=0.12, location=(0, 0, z))
        t_ = bpy.context.active_object
        t_.data.materials.append(mg)
        rings.append(t_)
    body = join("Body", [base, chest] + rings)
    set_origin(body, (0, 0, 0))
    parts.append(body)
    skull = lathe("skull", [(0.0, 6.8), (1.2, 6.9), (1.4, 7.8), (1.3, 8.8), (0.8, 9.4), (0.0, 9.5)], steps=24)
    skull.data.materials.append(m)
    crown = []
    for k in range(8):
        a = k / 8 * math.tau
        crown.append(tube("spike", [((math.cos(a) * 0.9, math.sin(a) * 0.9, 9.2), 0.15), ((math.cos(a) * 1.1, math.sin(a) * 1.1, 10.2), 0.02)], seg=6, sub=1))
        crown[-1].data.materials.append(mg)
    head = join("Head", [skull] + crown)
    set_origin(head, (0, 0, 6.8))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 2.3, 0, 5.8))
        arm = tube("arm", [(sh, 0.5), (sh + Vector((sx * 1.2, -0.6, -1.6)), 0.4), (sh + Vector((sx * 1.6, -1.8, -3.0)), 0.35)], seg=10, sub=2)
        arm.data.materials.append(m)
        a = join("Arm" + side, [arm])
        set_origin(a, sh)
        parent(a, body)
        parts.append(a)
        saw_c = sh + Vector((sx * 1.6, -2.2, -3.2))
        bm = bmesh.new()
        pts = []
        for i in range(48):
            ang = i / 48 * math.tau
            rr = 1.1 if i % 2 == 0 else 0.92
            pts.append((math.cos(ang) * rr, math.sin(ang) * rr))
        fv = [bm.verts.new((x, -0.05, y)) for x, y in pts]
        bv = [bm.verts.new((x, 0.05, y)) for x, y in pts]
        bm.faces.new(fv)
        bm.faces.new(list(reversed(bv)))
        for i in range(len(pts)):
            bm.faces.new((fv[(i + 1) % len(pts)], fv[i], bv[i], bv[(i + 1) % len(pts)]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        saw = L.from_bmesh("Saw" + side, bm, smooth=False)
        saw.rotation_euler = (0, 0, math.pi / 2)
        saw.location = saw_c
        L.transform_apply(saw)
        saw.data.materials.append(mg)
        set_origin(saw, saw_c)
        parent(saw, a)
        parts.append(saw)
    for o in parts:
        L.shade_smooth(o, 35)
    L.bake_group(parts, "janus", size=1024, ao_dist=0.5)
    # the two faces: masks on the front and the back of the head
    front = uv_sphere("FaceFront", 1.0, (0, -1.25, 8.1), scale=(0.95, 0.35, 1.05), seg=16, rings=10)
    front.data.materials.append(glow("janus_front", "#8ad0ff", 3.0))
    set_origin(front, (0, -1.25, 8.1))
    parent(front, head)
    back = uv_sphere("FaceBack", 1.0, (0, 1.25, 8.1), scale=(0.95, 0.35, 1.05), seg=16, rings=10)
    back.data.materials.append(glow("janus_back", "#ff3a2a", 4.0))
    set_origin(back, (0, 1.25, 8.1))
    parent(back, head)
    L.export_tree("janus", body)

ASSETS = {"shade": build_shade, "crawler": build_crawler, "guardian": build_guardian,
          "imp": build_imp, "brute": build_brute, "burrower": build_burrower, "colossus": build_colossus,
          "collector": build_collector, "goldguard": build_goldguard, "usurer": build_usurer,
          "dweller": build_dweller, "envious": build_envious, "wraith": build_wraith, "devourer": build_devourer,
          "legionary": build_legionary, "guide": build_guide, "janus": build_janus}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    L.OUT = argv[0] if argv else os.path.abspath("Assets/Crab/Models/Enemies")
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
