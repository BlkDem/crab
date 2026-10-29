"""
An automatic rig for a sculpted crab (one mesh, no bones, often with the legs fused into armour) to
the contract of Crab.Game.CrabRig.

Around the body's middle the limbs keep to sectors of angle: the claws in front, four legs a side
behind them. The legs' directions come from where the mesh touches the ground (the feet); where the
feet can't be told apart they are spread evenly. Each leg is two bones (hip at the shell's edge,
knee halfway out), each claw one (from its root to its far end). A vertex goes to the limb of its
sector by how far it reaches past the shell's edge, blended softly between neighbouring sectors and
over the knee; what is inside the shell's edge (and up on it: crystals, turrets) stays with Root.
Forward is -Y, L is +X (the crab's left). Ground at z = 0.
"""
import math
import bpy
from mathutils import Vector


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def angle_of(d):
    """Angle about the body from forward (-Y), positive to the crab's left (+X), in degrees."""
    return math.degrees(math.atan2(d.x, -d.y))


def ang_diff(a, b):
    return (a - b + 180) % 360 - 180


def middle(pts):
    """The body's middle on the ground: the medians (a gun held far out doesn't pull it)."""
    xs = sorted(c.x for c in pts)
    ys = sorted(c.y for c in pts)
    return Vector((xs[len(xs) // 2], ys[len(ys) // 2], 0))


def flat(c, centre):
    return Vector((c.x - centre.x, c.y - centre.y, 0))


class Limb:
    def __init__(self, name, kind, side, angle):
        self.name, self.kind, self.side, self.angle = name, kind, side, angle
        self.joints = []   # hip (root), [knee], tip
        self.reach = 0.0


def feet(co, hgt, size):
    """Where the mesh meets the ground: clusters of low vertices."""
    low = [c for c in co if c.z < 0.05 * hgt]
    cell = 0.035 * size
    grid = {}
    for c in low:
        grid.setdefault((int(c.x // cell), int(c.y // cell)), []).append(c)
    seen, out = set(), []
    for k in grid:
        if k in seen:
            continue
        st, cl = [k], []
        seen.add(k)
        while st:
            a = st.pop()
            cl += grid[a]
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    b = (a[0] + dx, a[1] + dy)
                    if b in grid and b not in seen:
                        seen.add(b)
                        st.append(b)
        if len(cl) >= 3:
            out.append(sum(cl, Vector()) / len(cl))
    return out


def plan(co, debug=print, claw_angle=28, shell_scale=1.0):
    """The body's middle, the shell's edge, and the limbs with their sectors."""
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    size = max(mx.x - mn.x, mx.y - mn.y)
    hgt = mx.z - mn.z
    body = [c for c in co if 0.35 * hgt < c.z - mn.z < 0.75 * hgt]
    centre = middle(body)
    # the shell's edge: how far the high part (the shell, not the low limbs) reaches out
    top = sorted(flat(c, centre).length for c in co if c.z - mn.z > 0.55 * hgt)
    shell = min(top[int(len(top) * 0.8)] if top else 0.3 * size, 0.36 * size) * shell_scale

    fs = [f for f in feet(co, hgt, size) if flat(f, centre).length > 0.7 * shell]
    limbs = []
    for side, sgn in (("L", 1), ("R", -1)):
        angs = sorted(abs(angle_of(flat(f, centre))) for f in fs if angle_of(flat(f, centre)) * sgn > 0)
        # four legs from 50° to 170°: the feet if four or more are seen there, otherwise spread evenly
        legs = [a for a in angs if 48 < a < 170]
        if len(legs) >= 4:
            legs = [legs[round(i * (len(legs) - 1) / 3)] for i in range(4)]
        else:
            legs = [62, 92, 122, 150]
        limbs.append(Limb("Claw" + side, "claw", side, sgn * claw_angle))
        for i, a in enumerate(legs):
            limbs.append(Limb(f"Leg{side}{i}", "leg", side, sgn * a))
    debug(f"size {size:.3f} h {hgt:.3f} centre {tuple(round(x, 3) for x in centre)} shell {shell:.3f} feet {len(fs)}")
    return centre, shell, size, hgt, mn, limbs


def place(co, centre, shell, mn, limbs):
    """Each limb's joints from the vertices of its sector."""
    for L in limbs:
        dirv = Vector((math.sin(math.radians(L.angle)), -math.cos(math.radians(L.angle)), 0))
        sect = [c for c in co if abs(ang_diff(angle_of(flat(c, centre)), L.angle)) < 10 and flat(c, centre).length > shell]
        if not sect:
            sect = [centre + dirv * shell * 1.6]
        far = max(sect, key=lambda c: flat(c, centre).length)
        L.reach = max(flat(far, centre).length, shell * 1.2)
        hip = centre + dirv * shell * 0.9
        # the hip's height: the middle of the limb's skin where it leaves the shell
        ring = [c for c in sect if flat(c, centre).length < shell * 1.15] or sect
        hip.z = sum(c.z for c in ring) / len(ring)
        if L.kind == "leg":
            # the knee: near the top of the leg's arch halfway out; the foot on the ground, farthest out
            r = (shell + L.reach) / 2
            mid = [c for c in sect if abs(flat(c, centre).length - r) < 0.15 * (L.reach - shell)] or sect
            knee = centre + dirv * r
            knee.z = max(c.z for c in mid) * 0.6 + sum(c.z for c in mid) / len(mid) * 0.4
            foot = centre + dirv * L.reach
            foot.z = mn.z
            L.joints = [hip, knee, foot]
        else:
            L.joints = [hip, far.copy()]


def sector_weights(co, centre, shell, hgt, mn, limbs, spread=14.0):
    """Per vertex: {bone: weight}."""
    out = []
    angs = [L.angle for L in limbs]
    for c in co:
        d = flat(c, centre)
        rho = d.length
        a = angle_of(d)
        # how much of the vertex is limb: past the shell's edge, and not up on the shell. Legs keep low;
        # high things near the edge are the shell's (spikes, turrets), farther out a claw may be raised
        edge = smooth(0.78 * shell, 1.05 * shell, rho)
        high = smooth(0.62 * hgt, 0.8 * hgt, c.z - mn.z)
        part = {"leg": edge * (1 - high), "claw": edge * (1 - high * (1 - smooth(1.2 * shell, 1.5 * shell, rho)))}
        if max(part.values()) < 1e-3:
            out.append({"Root": 1.0})
            continue
        # a claw takes its sector whole (it swings far: a blend with its neighbours would tear), up to
        # halfway to the first leg; the legs share theirs softly
        side = "L" if a > 0 else "R"
        claw = next((i for i, L in enumerate(limbs) if L.kind == "claw" and L.side == side), None)
        first = next((L for L in limbs if L.kind == "leg" and L.side == side), None)
        bound = (abs(limbs[claw].angle) + abs(first.angle)) / 2 if claw is not None and first else 0
        clawk = 1 - smooth(bound - 4, bound + 4, abs(a)) if claw is not None else 0
        ks = [0.0 if L.kind == "claw" else math.exp(-(ang_diff(a, x) / spread) ** 2) for L, x in zip(limbs, angs)]
        s = sum(ks)
        if s < 1e-6:   # between sectors, far from all: the nearest leg
            k = min((i for i, L in enumerate(limbs) if L.kind == "leg"), key=lambda i: abs(ang_diff(a, angs[i])))
            ks[k], s = 1.0, 1.0
        ks = [k / s * (1 - clawk) for k in ks]
        if claw is not None:
            ks[claw] = clawk
        w = {}
        for L, k in zip(limbs, ks):
            k = k * part[L.kind]
            if k < 0.02:
                continue
            if L.kind == "leg":
                # over the knee: the outer half of the leg goes to its second bone
                f = smooth(0.42, 0.62, (rho - shell) / max(1e-6, L.reach - shell))
                w[L.name + "_0"] = w.get(L.name + "_0", 0) + k * (1 - f)
                w[L.name + "_1"] = w.get(L.name + "_1", 0) + k * f
            else:
                w[L.name + "_0"] = w.get(L.name + "_0", 0) + k
        rest = 1 - sum(w.values())
        if rest > 0.02:
            w["Root"] = rest
        out.append(w)
    return out


def build_armature(limbs, centre, hgt, name="Body"):
    arm_d = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, arm_d)
    bpy.context.scene.collection.objects.link(arm)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_d.edit_bones
    root = eb.new("Root")
    root.head = Vector((centre.x, centre.y, 0.0))
    root.tail = Vector((centre.x, centre.y, 0.5 * hgt))
    for L in limbs:
        prev = root
        for i in range(len(L.joints) - 1):
            b = eb.new(f"{L.name}_{i}")
            b.head, b.tail = L.joints[i], L.joints[i + 1]
            b.parent = prev
            b.use_connect = i > 0
            prev = b
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def rig(ob, debug=print, claw_angle=28, shell_scale=1.0):
    """Bones and skin for a crab mesh (transforms applied). Returns the armature, the limbs and
    (centre, shell edge, size, height)."""
    co = [ob.matrix_world @ v.co for v in ob.data.vertices]
    centre, shell, size, hgt, mn, limbs = plan(co, debug, claw_angle, shell_scale)
    place(co, centre, shell, mn, limbs)
    for L in limbs:
        debug(f"  {L.name:7s} {L.angle:+6.1f} reach {L.reach:.3f} joints {[tuple(round(x, 3) for x in j) for j in L.joints]}")
    w = sector_weights(co, centre, shell, hgt, mn, limbs)
    for g in list(ob.vertex_groups):
        ob.vertex_groups.remove(g)
    groups = {}
    for i, wv in enumerate(w):
        s = sum(wv.values())
        for k, x in wv.items():
            g = groups.get(k) or groups.setdefault(k, ob.vertex_groups.new(name=k))
            g.add([i], x / s, "REPLACE")
    arm = build_armature(limbs, centre, hgt)
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    ob.parent = arm
    return arm, limbs, (centre, shell, size, hgt)
