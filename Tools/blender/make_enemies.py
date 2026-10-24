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


ASSETS = {"shade": build_shade, "crawler": build_crawler, "guardian": build_guardian}


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
