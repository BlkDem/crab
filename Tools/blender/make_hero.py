"""
The hero of "Краб против ада": a small orange crab (level 1, "Обычный краб").

Run headless:
  blender -b -P Tools/blender/make_hero.py -- <out_dir>

A hierarchy of rigid parts whose origins sit at the joints, so the game bends them
(procedural gait, gestures). All parts share one baked texture set.

Part names (the contract with Crab.Game.CrabRig):
  Body > Eye{L,R} (stalk + eyeball; the pupil looks along -Y),
         Leg{L,R}{0..3}_0 > _1 > _2 (hip, knee, ankle),
         Claw{L,R}_0 > _1 > _2 (palm with the fixed finger) > _3 (movable finger)
  Grip{L,R}: empties inside the palms where held items attach.
L is +X (the crab's left), R is -X. Built at the aquarium crab's scale (the material noise is
tuned for it) and scaled up on export: the root carries SCALE.
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
from lib import NT, C, UP, FWD, TAU, from_bmesh, join, set_origin, parent, uv_sphere, tube, smoothstep, decimate_to  # noqa: E402

SCALE = 3.4   # about 0.65 m across the shell in the game


# ----------------------------------------------------------------------------- materials
def mat_shell(name):
    """Bright orange shell, as on the concept art: darker rust mottling, warm highlights on the
    ridges, cream belly, a granular surface with the H-shaped grooves."""
    t = NT(name)
    x, y, z = t.sep(t.coord)
    nz = t.sep(t.geo("Normal"))[2]
    mott = t.noise(t.vec(), 30, detail=6, rough=0.6)
    col = t.ramp(mott, [(0.32, C("#9a3410")), (0.55, C("#d9581a")), (0.78, C("#f07a2a"))])
    speck = t.voronoi(t.vec(), 150, out="Distance")
    col = t.mix(t.ramp(speck, [(0.05, 0.55), (0.12, 0.0)]), col, C("#5a1806"))
    top = t.ramp(nz, [(0.55, 0.0), (0.95, 1.0)])
    col = t.mix(t.math("MULTIPLY", top, 0.35), col, C("#ffb066"))
    belly = t.ramp(nz, [(-0.35, 1.0), (0.05, 0.0)])
    col = t.mix(belly, col, C("#f3d6a4"))
    gx = t.math("ABSOLUTE", x)
    arc = t.math("ABSOLUTE", t.math("SUBTRACT", y, t.math("MULTIPLY", t.math("MULTIPLY", gx, gx), 9.0)))
    groove = t.ramp(arc, [(0.0, 1.0), (0.006, 0.0)])
    grooves = t.math("MULTIPLY", groove, t.math("SUBTRACT", 1.0, belly))
    col = t.mix(t.math("MULTIPLY", grooves, 0.55), col, C("#4a1404"))
    gran = t.voronoi(t.vec(), 240, out="Distance")
    h = t.math("SUBTRACT", t.math("MULTIPLY", gran, 0.5), t.math("MULTIPLY", grooves, 0.8))
    return t.finish(col, rough=t.ramp(belly, [(0.0, 0.32), (1.0, 0.5)]), normal=t.bump(h, 0.45, 0.004))


def mat_leg(name, length):
    t = NT(name)
    v = t.math("DIVIDE", t.sep(t.n("ShaderNodeUVMap", uv_map="Grain").outputs["UV"])[1], length)
    nz = t.sep(t.geo("Normal"))[2]
    col = t.ramp(t.noise(t.vec(), 80, detail=4), [(0.3, C("#b8441a")), (0.7, C("#e2662a"))])
    col = t.mix(t.ramp(nz, [(-0.8, 0.5), (-0.2, 0.0)]), col, C("#f2c890"))
    ends = t.ramp(v, [(0.0, 1.0), (0.12, 0.0), (0.88, 0.0), (1.0, 1.0)])
    col = t.mix(t.math("MULTIPLY", ends, 0.35), col, C("#6a2008"))
    return t.finish(col, rough=0.38, normal=t.bump(t.noise(t.vec(), 380, detail=2), 0.2, 0.002))


def mat_tip(name, length):
    t = NT(name)
    v = t.math("DIVIDE", t.sep(t.n("ShaderNodeUVMap", uv_map="Grain").outputs["UV"])[1], length)
    return t.finish(t.ramp(v, [(0.0, C("#d0602a")), (0.6, C("#7a2a0e")), (1.0, C("#2a0c04"))]), rough=0.3)


def mat_claw(name, along=False, length=1.0):
    t = NT(name)
    col = t.ramp(t.noise(t.vec(), 50, detail=5), [(0.3, C("#c2380e")), (0.55, C("#ec5a1c")), (0.8, C("#ff8a3a"))])
    col = t.mix(t.ramp(t.voronoi(t.vec(), 180, out="Distance"), [(0.04, 0.45), (0.1, 0.0)]), col, C("#5a1404"))
    if along:
        v = t.math("DIVIDE", t.sep(t.n("ShaderNodeUVMap", uv_map="Grain").outputs["UV"])[1], length)
        col = t.mix(t.ramp(v, [(0.5, 0.0), (0.78, 1.0)]), col, C("#fff0d6"))
    return t.finish(col, rough=0.28, normal=t.bump(t.voronoi(t.vec(), 280, out="Distance"), 0.3, 0.003))


def mat_stalk(name):
    t = NT(name)
    return t.finish(t.ramp(t.noise(t.vec(), 110), [(0.3, C("#c2501c")), (0.7, C("#e8742e"))]), rough=0.4)


# ----------------------------------------------------------------------------- the aquarium crab's shapes
def carapace(H):
    """A sesarmid crab's shell: nearly square from above, flat on top with steep sides and a
    crisp rim, raised gastric and branchial regions with the H-shaped groove between them,
    eye sockets at the front corners, the front margin bent down between them, two small
    teeth behind each eye; a flat belly."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=96, v_segments=48, radius=1.0)
    p = 4.0
    for v in bm.verts:
        x, y, z = v.co
        r = math.hypot(x, y)
        if r > 1e-6:
            sq = r / ((abs(x) ** p + abs(y) ** p) ** (1 / p))   # circle -> squircle
            x, y = x * sq, y * sq
        ax = abs(x)
        X = x * 0.1 * (1 - 0.1 * max(0.0, y))                 # a little narrower at the back
        Y = y * 0.084
        if y < -0.5:   # eye sockets
            Y += 0.012 * math.exp(-((ax - 0.7) / 0.13) ** 2) * smoothstep(-0.5, -0.97, y)
        eq = math.exp(-(z / 0.18) ** 2)
        if ax > 0.85 and -0.75 < y < -0.15:   # teeth on the front of the side margin
            tooth = max(0.0, math.sin((y + 0.75) / 0.6 * TAU)) ** 4
            X += math.copysign(0.0055 * tooth * eq, x)
        if z >= 0:
            top = z ** 0.4
            g = math.exp(-((x / 0.3) ** 2 + ((y + 0.22) / 0.32) ** 2))                 # gastric
            br = math.exp(-(((ax - 0.55) / 0.3) ** 2 + ((y - 0.2) / 0.42) ** 2))       # branchial
            groove = math.exp(-((y - 0.08 - 0.55 * x * x) / 0.05) ** 2) * (1 - 0.6 * ax)
            mid = math.exp(-(x / 0.04) ** 2) * smoothstep(0.05, -0.45, y) * smoothstep(-0.8, -0.5, y)
            Z = 0.028 * top + top * (0.0045 * g + 0.004 * br - 0.0028 * groove - 0.0015 * mid)
            Z -= 0.007 * smoothstep(-0.72, -1.0, y) * smoothstep(0.6, 0.25, ax) * top  # bent-down front
        else:
            Z = 0.015 * z
        v.co = Vector((X, Y, Z + H))
    ob = from_bmesh("Body", bm)
    decimate_to(ob, 9000)
    return ob


def leg_chain(side, name, base, out_dir, slopes, lengths, radii, mats, flat=0.55):
    """Three segments (hip -> knee -> ankle -> tip). out_dir: horizontal direction of the leg;
    slopes: elevation of each segment (radians, + is up)."""
    obs, p = [], base.copy()
    flat_axis = out_dir.cross(UP).normalized()
    for i, (sl, L, (r0, r1), m) in enumerate(zip(slopes, lengths, radii, mats)):
        d = (out_dir * math.cos(sl) + UP * math.sin(sl)).normalized()
        q = p + d * L
        if i < 2:
            pts = [(p, r0 * 0.85), (p + d * (L * 0.12), r0), (p + d * (L * 0.5), (r0 + r1) * 0.56), (p + d * (L * 0.9), r1), (q, r1 * 0.8)]
        else:   # the claw-like tip curves down a little
            pts = [(p, r0 * 0.85), (p + d * (L * 0.3), r0), (p + d * (L * 0.7) - UP * (L * 0.04), r0 * 0.55), (q, r1)]
        ob = tube(f"{name}_{i}", pts, seg=8, sub=2, flat=flat, flat_axis=flat_axis)
        knob = uv_sphere("k", r0 * 0.9, p, seg=8, rings=5)
        ob = join(f"{name}_{i}", [ob, knob])
        ob.data.materials.clear()
        ob.data.materials.append(m)
        set_origin(ob, p)
        if obs:
            parent(ob, obs[-1])
        obs.append(ob)
        p = q
    return obs


def claw(side, name, base, mats, s=1.0):
    """Cheliped: merus -> carpus -> palm with the fixed finger -> movable finger. Held folded
    in front of the face, palms turned in, fingers pointing forward and in."""
    sx = 1 if side == "L" else -1
    m_arm, m_palm, m_finger = mats
    p0 = base.copy()
    p1 = p0 + Vector((sx * 0.02, -0.03, -0.008)) * s          # merus: out and forward
    p2 = p1 + Vector((-sx * 0.009, -0.018, 0.002)) * s           # carpus (wrist): turns forward-in
    mside = (p1 - p0).cross(UP).normalized()
    merus = tube(f"{name}_0", [(p0, 0.0072 * s), (p0 + (p1 - p0) * 0.35, 0.0092 * s), (p0 + (p1 - p0) * 0.75, 0.0094 * s), (p1, 0.0078 * s)],
                 seg=10, sub=2, flat=0.62, flat_axis=UP)
    merus = join(f"{name}_0", [merus, uv_sphere("k", 0.0072 * s, p0, seg=10, rings=6)])
    carpus = tube(f"{name}_1", [(p1, 0.0074 * s), (p1 + (p2 - p1) * 0.45, 0.0098 * s), (p2, 0.0082 * s)], seg=10, sub=2,
                  flat=0.75, flat_axis=UP)
    carpus = join(f"{name}_1", [carpus, uv_sphere("k", 0.0076 * s, p1, seg=10, rings=6)])
    # palm: an inflated oval, taller than wide, narrowing toward the fingers
    pd = Vector((-sx * 0.55, -1, -0.1)).normalized()
    pc = p2 + pd * 0.02 * s
    palm = uv_sphere("palm", 1.0, (0, 0, 0), seg=22, rings=14)
    for v in palm.data.vertices:
        co = v.co
        fl = 1 - 0.22 * max(0.0, -co.y)                    # narrower toward the fingers (-Y)
        v.co = Vector((co.x * 0.0115 * fl, co.y * 0.022, co.z * 0.016 * fl + 0.002 * co.x * co.x)) * s
    rot = FWD.rotation_difference(pd).to_matrix().to_4x4()
    palm.data.transform(Matrix.Translation(pc) @ rot)
    f0 = pc + pd * 0.016 * s + Vector((0, 0, -0.0065)) * s
    fd = (pd + Vector((sx * 0.2, 0, -0.06))).normalized()
    fixed = tube("fixed", [(f0, 0.0058 * s), (f0 + fd * 0.013 * s, 0.0048 * s), (f0 + fd * 0.024 * s + Vector((0, 0, 0.0028)) * s, 0.0028 * s),
                           (f0 + fd * 0.032 * s + Vector((0, 0, 0.0058)) * s, 0.0007 * s)], seg=10, sub=2, flat=0.62, flat_axis=fd.cross(UP))
    fixed.data.materials.append(m_finger)
    palm.data.materials.append(m_palm)
    palm_ob = join(f"{name}_2", [palm, fixed, uv_sphere("k", 0.0078 * s, p2, seg=10, rings=6)])
    # movable finger, hinged on top of the palm's front
    h = pc + pd * 0.015 * s + Vector((0, 0, 0.0062)) * s
    dd = (pd + Vector((sx * 0.2, 0, 0.03))).normalized()
    dactyl = tube(f"{name}_3", [(h, 0.0054 * s), (h + dd * 0.014 * s, 0.0047 * s), (h + dd * 0.026 * s - Vector((0, 0, 0.0035)) * s, 0.0027 * s),
                               (h + dd * 0.034 * s - Vector((0, 0, 0.0078)) * s, 0.0007 * s)], seg=10, sub=2, flat=0.62, flat_axis=dd.cross(UP))
    dactyl = join(f"{name}_3", [dactyl, uv_sphere("k", 0.0048 * s, h, seg=8, rings=6)])
    for ob, m in ((merus, m_arm), (carpus, m_arm), (dactyl, m_finger)):
        ob.data.materials.clear()
        ob.data.materials.append(m)
    set_origin(merus, p0)
    set_origin(carpus, p1)
    set_origin(palm_ob, p2)
    set_origin(dactyl, h)
    parent(carpus, merus)
    parent(palm_ob, carpus)
    parent(dactyl, palm_ob)
    return [merus, carpus, palm_ob, dactyl]


# ----------------------------------------------------------------------------- the hero
def eye(side, sx, H, m_stalk, m_white, m_pupil, m_shine):
    """A big cartoon eye on a long stalk: white eyeball, a black pupil looking forward and a
    highlight — the crab's face, which stays the same at every level of the game."""
    b = Vector((sx * 0.05, -0.068, H + 0.012))
    tip = b + Vector((sx * 0.008, -0.004, 0.034))
    st = tube("stalk", [(b, 0.0062), (b + (tip - b) * 0.5 + Vector((sx * 0.002, 0, 0)), 0.0052), (tip, 0.0056)], seg=10, sub=3)
    st.data.materials.append(m_stalk)
    c = tip + Vector((0, -0.002, 0.013))
    ball = uv_sphere("ball", 0.018, c, scale=(1, 0.95, 1.08), seg=28, rings=18)
    ball.data.materials.append(m_white)
    look = Vector((sx * 0.12, -1, 0.05)).normalized()
    pupil = uv_sphere("pupil", 0.0095, c + look * 0.0136, scale=(1, 0.55, 1.15), seg=18, rings=12)
    rot = FWD.rotation_difference(look).to_matrix().to_4x4()
    pupil.data.transform(Matrix.Translation(c + look * 0.0136) @ rot @ Matrix.Translation(-(c + look * 0.0136)))
    pupil.data.materials.append(m_pupil)
    shine = uv_sphere("shine", 0.003, c + look * 0.0172 + Vector((sx * -0.003, 0, 0.0055)), seg=10, rings=8)
    shine.data.materials.append(m_shine)
    ob = join("Eye" + side, [st, ball, pupil, shine])
    set_origin(ob, b)
    return ob


def build_hero():
    H = 0.062
    body = carapace(H)
    m_shell = mat_shell("hero_shell")
    body.data.materials.append(m_shell)
    L.shade_smooth(body, 70)
    # mouthparts under the front edge
    mouth = []
    for sx in (-1, 1):
        mp = uv_sphere("mx", 1.0, (0, 0, 0), seg=14, rings=10)
        for v in mp.data.vertices:
            v.co = Vector((v.co.x * 0.012, v.co.y * 0.013, v.co.z * 0.0045))
        mp.data.transform(Matrix.Translation((sx * 0.013, -0.074, H - 0.004)) @ Matrix.Rotation(-0.7, 4, "X"))
        mp.data.materials.append(m_shell)
        mouth.append(mp)
    body = join("Body", [body] + mouth)
    set_origin(body, (0, 0, 0))
    parts = [body]

    m_stalk = mat_stalk("hero_stalk")
    m_white = L.mat_plain("hero_eye_white", C("#fbf6ee"), rough=0.12)
    m_pupil = L.mat_plain("hero_eye_pupil", C("#0b0806"), rough=0.05)
    m_shine = L.mat_plain("hero_eye_shine", C("#ffffff"), rough=0.05)
    for side, sx in (("L", 1), ("R", -1)):
        e = eye(side, sx, H, m_stalk, m_white, m_pupil, m_shine)
        parent(e, body)
        parts.append(e)

    m_leg, m_leg2, m_tip = mat_leg("hero_leg", 0.07), mat_leg("hero_leg2", 0.066), mat_tip("hero_tip", 0.045)
    for side, sx in (("L", 1), ("R", -1)):
        for i, (y, ang, k) in enumerate(((-0.03, 0.62, 1.0), (-0.004, 0.2, 1.06), (0.024, -0.2, 1.04), (0.046, -0.62, 0.92))):
            base = Vector((sx * 0.09 * (1 - 0.1 * max(0, y) / 0.05), y, H - 0.002))
            out_dir = Vector((sx * math.cos(ang), -math.sin(ang), 0)).normalized()
            obs = leg_chain(side, f"Leg{side}{i}", base, out_dir,
                            slopes=(math.radians(28), math.radians(-48), math.radians(-72)),
                            lengths=(0.07 * k, 0.064 * k, 0.042 * k),
                            radii=((0.011, 0.0098), (0.009, 0.0074), (0.0064, 0.0009)),
                            mats=(m_leg, m_leg2, m_tip))
            parent(obs[0], body)
            parts += obs

    m_arm, m_palm, m_finger = mat_claw("hero_claw_arm"), mat_claw("hero_claw_palm"), mat_claw("hero_claw_finger", along=True, length=0.06)
    grips = []
    for side, sx in (("L", 1), ("R", -1)):
        obs = claw(side, "Claw" + side, Vector((sx * 0.07, -0.06, H - 0.004)), (m_arm, m_palm, m_finger), s=1.75)
        parent(obs[0], body)
        parts += obs
        # where a held item sits: between the fingers, at the palm's front
        g = bpy.data.objects.new("Grip" + side, None)
        L.link(g)
        palm = obs[2]
        g.location = palm.matrix_world.translation + (palm.matrix_world.to_3x3() @ Vector((0, 0, 0)))
        bb = [palm.matrix_world @ Vector(c) for c in palm.bound_box]
        g.location = sum(bb, Vector()) / 8 + Vector((0, -0.012, 0.0))
        parent(g, palm)
        grips.append(g)

    for ob in parts:
        if ob is not body:
            L.shade_smooth(ob, 60)
    L.bake_group(parts, "hero", size=2048, ao_dist=0.02)
    body.scale = (SCALE, SCALE, SCALE)
    L.export_tree("hero", body)


ASSETS = {"hero": build_hero}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    L.OUT = argv[0] if argv else os.path.abspath("Assets/Crab/Models/Hero")
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
