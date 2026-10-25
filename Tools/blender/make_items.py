"""
Items the crab creates from matter (level 1): stick, stone, crowbar, knife, crude pistol.

Run headless:
  blender -b -P Tools/blender/make_items.py -- <out_dir> [asset ...]

The grip is at the origin and the item points forward (Blender -Y, Unity +Z), so the game
puts the origin into the crab's claw. Empties: Muzzle (where a shot leaves the pistol),
Tip (the end of a melee weapon).
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
from lib import NT, C, join, set_origin, parent, uv_sphere, tube, sweep, lathe  # noqa: E402


def empty(name, loc, par):
    e = bpy.data.objects.new(name, None)
    L.link(e)
    e.location = loc
    parent(e, par)
    return e


def mat_iron(name, base="#4d4a47", rust="#7a3a18", amount=0.55):
    return L.mat_metal(name, base=base, patina=rust, patina_amt=amount, rough=0.45, metal_max=0.9)


def build_stick():
    """A crooked branch with a broken-off twig, bark worn at the grip."""
    pts = [((0, 0.08, 0), 0.022), ((0.004, -0.1, 0.006), 0.02), ((-0.01, -0.28, 0.012), 0.018),
           ((0.006, -0.45, 0.004), 0.015), ((0.0, -0.58, -0.006), 0.011)]
    twig = [((0.004, -0.3, 0.012), 0.009), ((0.04, -0.36, 0.03), 0.006), ((0.07, -0.4, 0.05), 0.003)]
    ob = sweep("Stick", [pts, twig], seg=10, sub=4)
    ob.data.materials.append(L.mat_driftwood("stick", base="#7a5232", dark="#2e1a0c", light="#a8805a", moss=0.0))
    L.bake(ob, size=512)
    empty("Tip", (0, -0.58, 0), ob)
    L.export_tree("stick", ob)


def build_stone():
    ob = L.rock_obj("Stone", 0.065, 7, (1.0, 0.85, 0.75), 0.3, L.mat_rock("stone", moss=0.0, scale=12), sub=4, detail=0.08)
    L.bake(ob, size=512, metal=False)
    L.export_tree("stone", ob)


def build_crowbar():
    """A scrap-iron crowbar: a hexagonal bar with a curled, split claw end."""
    pts = [((0, 0.06, 0), 0.014), ((0, -0.2, 0), 0.014), ((0, -0.42, 0.0), 0.013), ((0, -0.5, 0.02), 0.013),
           ((0, -0.53, 0.06), 0.012), ((0, -0.51, 0.1), 0.01), ((0, -0.47, 0.11), 0.007)]
    bar = sweep("Crowbar", [pts], seg=6, sub=4)
    # the flat chisel at the grip end
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.03, v.co.y * 0.05 + 0.08, v.co.z * (0.008 if v.co.y > 0 else 0.022)))
    chisel = L.from_bmesh("chisel", bm, smooth=False)
    m = mat_iron("crowbar")
    bar.data.materials.append(m)
    chisel.data.materials.append(m)
    ob = join("Crowbar", [bar, chisel])
    L.bake(ob, size=512)
    empty("Tip", (0, -0.53, 0.06), ob)
    L.export_tree("crowbar", ob)


def build_knife():
    """A rough knife hammered out of a scrap plate, its handle wrapped in rag strips."""
    bm = bmesh.new()
    # blade outline (in the XY plane, pointing -Y), extruded thin, with a bevelled edge
    outline = [(0.0, -0.04), (0.018, -0.045), (0.022, -0.12), (0.016, -0.2), (0.0, -0.26), (-0.006, -0.2),
               (-0.01, -0.12), (-0.012, -0.045)]
    top = [bm.verts.new((x, y, 0.003)) for x, y in outline]
    bot = [bm.verts.new((x, y, -0.003)) for x, y in outline]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    n = len(outline)
    for i in range(n):
        bm.faces.new((top[i], top[(i + 1) % n], bot[(i + 1) % n], bot[i]))
    for v in top + bot:   # sharpen the cutting side
        if v.co.x > 0.012 and v.co.y < -0.06:
            v.co.z *= 0.2
    blade = L.from_bmesh("blade", bm, smooth=False)
    L.mod(blade, "BEVEL", width=0.0015, segments=1)
    blade.data.materials.append(mat_iron("knife_blade", base="#8a8682", rust="#6a3416", amount=0.4))
    handle = tube("handle", [((0, 0.07, 0), 0.013), ((0, 0.02, 0), 0.015), ((0, -0.04, 0), 0.014)], seg=10, sub=2)
    handle.data.materials.append(L.mat_canvas("knife_rag", base="#6e6252", dark="#3a3024", moss=0.0))
    ob = join("Knife", [blade, handle])
    L.bake(ob, size=512)
    empty("Tip", (0, -0.26, 0), ob)
    L.export_tree("knife", ob)


def build_pistol():
    """A home-made pistol: a pipe barrel strapped with wire to a wooden grip, a nail trigger."""
    m_pipe = mat_iron("pistol_pipe", base="#5a5753", rust="#7a3a18", amount=0.5)
    m_wood = L.mat_wood("pistol_wood", base="#6a4428", dark="#2e1a0c", light="#946a40", rings=30, along="Y")
    m_wire = L.mat_metal("pistol_wire", base="#9a7a4a", patina="#4a3a22", patina_amt=0.3, rough=0.4)
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.018, depth=0.26, location=(0, -0.11, 0.06), rotation=(math.pi / 2, 0, 0))
    barrel = bpy.context.active_object
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.011, depth=0.27, location=(0, -0.11, 0.06), rotation=(math.pi / 2, 0, 0))
    bore = bpy.context.active_object
    L.boolean(barrel, bore)
    barrel.data.materials.append(m_pipe)
    # the grip block slanting down and back, and the stock plate under the barrel
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.03, v.co.y * 0.05, v.co.z * 0.13))
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0.02, -0.01)) @ Matrix.Rotation(-0.35, 4, "X"), verts=bm.verts)
    grip = L.from_bmesh("grip", bm, smooth=False)
    L.mod(grip, "BEVEL", width=0.006, segments=2)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -0.05, 0.035))
    stock = bpy.context.active_object
    stock.scale = (0.032, 0.18, 0.022)
    L.mod(stock, "BEVEL", width=0.004, segments=2)
    for o in (grip, stock):
        o.data.materials.append(m_wood)
    wraps = []
    for y in (-0.02, -0.1, -0.17):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.024, minor_radius=0.0025, location=(0, y, 0.048), rotation=(math.pi / 2, 0, 0))
        w = bpy.context.active_object
        w.scale = (1.0, 1.0, 1.35)
        w.data.materials.append(m_wire)
        wraps.append(w)
    trig = tube("trigger", [((0, 0.0, 0.015), 0.003), ((0, 0.005, -0.01), 0.003), ((0, 0.015, -0.025), 0.0025)], seg=6, sub=2)
    trig.data.materials.append(m_wire)
    ob = join("Pistol", [barrel, grip, stock, trig] + wraps)
    L.bake(ob, size=512)
    empty("Muzzle", (0, -0.24, 0.06), ob)
    L.export_tree("pistol", ob)


def build_machinegun():
    """Three scrap pipes bound into a barrel cluster on an iron body, an ammo box hanging under it."""
    m_pipe = mat_iron("mg_pipe", base="#4a4846", rust="#6a3416", amount=0.45)
    m_body = mat_iron("mg_body", base="#3a3836", rust="#7a3a18", amount=0.6)
    m_wood = L.mat_wood("mg_wood", base="#5a3a22", dark="#2a180a", light="#7a5838", rings=30, along="Y")
    parts = []
    for k in range(3):
        a = k / 3 * math.tau
        off = Vector((math.cos(a) * 0.022, 0, 0.07 + math.sin(a) * 0.022))
        bpy.ops.mesh.primitive_cylinder_add(vertices=14, radius=0.013, depth=0.36, location=Vector((0, -0.2, 0)) + off, rotation=(math.pi / 2, 0, 0))
        b = bpy.context.active_object
        b.data.materials.append(m_pipe)
        parts.append(b)
    for y in (-0.08, -0.25, -0.36):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.042, minor_radius=0.006, location=(0, y, 0.07), rotation=(math.pi / 2, 0, 0))
        r = bpy.context.active_object
        r.data.materials.append(m_body)
        parts.append(r)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0.02, 0.06))
    box = bpy.context.active_object
    box.scale = (0.06, 0.16, 0.07)
    L.mod(box, "BEVEL", width=0.008, segments=2)
    box.data.materials.append(m_body)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.0, -0.02, -0.02))
    ammo = bpy.context.active_object
    ammo.scale = (0.07, 0.08, 0.07)
    L.mod(ammo, "BEVEL", width=0.006, segments=2)
    ammo.data.materials.append(m_body)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.03, v.co.y * 0.045, v.co.z * 0.12))
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0.08, -0.02)) @ Matrix.Rotation(-0.3, 4, "X"), verts=bm.verts)
    grip = L.from_bmesh("grip", bm, smooth=False)
    L.mod(grip, "BEVEL", width=0.006, segments=2)
    grip.data.materials.append(m_wood)
    ob = join("MachineGun", parts + [box, ammo, grip])
    L.bake(ob, size=512)
    empty("Muzzle", (0, -0.39, 0.07), ob)
    L.export_tree("machinegun", ob)


def build_armor():
    """Scrap armour for the crab (level 2, "Краб-воин"): riveted plates following the shell,
    built in the hero model's own units so it sits on the Body as it is."""
    H = 0.062
    m = L.mat_metal("armor_scrap", base="#6a6660", patina="#7a3a18", patina_amt=0.55, rough=0.45, metal_max=0.85)
    m2 = L.mat_metal("armor_bolt", base="#3a3836", patina="#5a3a20", patina_amt=0.3, rough=0.4)
    plates = []
    for i, (y0, y1) in enumerate(((-0.07, -0.025), (-0.02, 0.03), (0.035, 0.08))):
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=10, y_segments=4, size=0.5)
        for v in bm.verts:
            x = v.co.x * 0.2 * (1 - 0.15 * i)
            y = y0 + (v.co.y + 0.5) * (y1 - y0)
            z = H + 0.034 - 0.9 * x * x - 0.6 * (y + 0.01) ** 2
            v.co = Vector((x, y, z))
        pl = L.from_bmesh(f"plate{i}", bm)
        L.mod(pl, "SOLIDIFY", thickness=0.004, offset=1)
        L.apply_all(pl)
        pl.data.materials.append(m)
        plates.append(pl)
        for sx in (-1, 1):
            for yy in (y0 + 0.008, y1 - 0.008):
                x = sx * 0.07 * (1 - 0.15 * i)
                bolt = uv_sphere("bolt", 0.0035, (x, yy, H + 0.038 - 0.9 * x * x - 0.6 * (yy + 0.01) ** 2), seg=8, rings=6)
                bolt.data.materials.append(m2)
                plates.append(bolt)
    for sx in (-1, 1):   # shoulder spikes
        b = Vector((sx * 0.07, -0.05, H + 0.03))
        sp = tube("spike", [(b, 0.006), (b + Vector((sx * 0.012, -0.005, 0.02)), 0.001)], seg=8, sub=1)
        sp.data.materials.append(m2)
        plates.append(sp)
    ob = join("Armor", plates)
    L.bake(ob, size=512)
    L.export_tree("armor", ob)


def build_drone():
    """SCOUT, the first companion: a small round drone with a ring of four rotors and a blue eye."""
    m_shell = L.mat_metal("drone_shell", base="#8a8e94", patina="#3a3e44", patina_amt=0.25, rough=0.3, metal_max=0.9)
    m_dark = L.mat_metal("drone_dark", base="#2a2c30", patina="#1a1a1c", patina_amt=0.2, rough=0.4)
    body = uv_sphere("Body", 0.12, (0, 0, 0), scale=(1, 1, 0.85), seg=28, rings=18)
    body.data.materials.append(m_shell)
    cap = uv_sphere("cap", 0.07, (0, 0.02, 0.09), scale=(1, 1, 0.5), seg=18, rings=10)
    cap.data.materials.append(m_dark)
    ant = tube("antenna", [((0.03, 0.05, 0.1), 0.004), ((0.05, 0.08, 0.2), 0.002)], seg=6, sub=1)
    ant.data.materials.append(m_dark)
    gun = tube("gun", [((0, -0.08, -0.06), 0.012), ((0, -0.17, -0.07), 0.01)], seg=8, sub=1)
    gun.data.materials.append(m_dark)
    body = join("Body", [body, cap, ant, gun])
    set_origin(body, (0, 0, 0))
    parts = [body]
    bpy.ops.mesh.primitive_torus_add(major_radius=0.21, minor_radius=0.012, location=(0, 0, 0))
    ring = bpy.context.active_object
    ring.name = "Ring"
    ring.data.materials.append(m_dark)
    struts = [ring]
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        p = Vector((math.cos(a) * 0.21, math.sin(a) * 0.21, 0))
        st = tube("strut", [(p * 0.5, 0.008), (p, 0.008)], seg=6, sub=1)
        st.data.materials.append(m_dark)
        struts.append(st)
        bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.055, depth=0.006, location=p + Vector((0, 0, 0.02)))
        r = bpy.context.active_object
        r.data.materials.append(m_shell)
        struts.append(r)
    ring = join("Ring", struts)
    set_origin(ring, (0, 0, 0))
    parent(ring, body)
    parts.append(ring)
    L.bake_group(parts, "drone", size=512, ao_dist=0.03)
    eye = uv_sphere("GlowEye", 0.04, (0, -0.105, 0.0), scale=(1, 0.5, 1), seg=14, rings=10)
    eye.data.materials.append(L.mat_plain("drone_eye", "#4ab8ff", rough=0.2, emission=("#4ab8ff", 12.0)))
    parent(eye, body)
    L.export_tree("drone", body)



# ---------------------------------------------------------------------- level 3 (circle of Greed)
def mat_steel(name):
    return L.mat_metal(name, base="#9aa0a8", patina="#3a3c40", patina_amt=0.3, rough=0.35, metal_max=0.45)


def mat_goldtrim(name):
    return L.mat_metal(name, base="#f0b840", patina="#6a4612", patina_amt=0.2, rough=0.3, metal_max=0.5)


def build_heavy_armor():
    """Heavy armour (level 3, "Бронекраб"): a full steel carapace over the shell with gold trim,
    shoulder guards and a glowing crystal insert (GlowInsert) — in the hero model's units."""
    H = 0.062
    m = mat_steel("harmor_steel")
    mg = mat_goldtrim("harmor_gold")
    parts = []
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=16, y_segments=10, size=0.5)
    for v in bm.verts:
        x = v.co.x * 0.24
        y = -0.085 + (v.co.y + 0.5) * 0.18
        z = H + 0.04 - 0.75 * x * x - 0.55 * (y + 0.005) ** 2
        v.co = Vector((x, y, z))
    shell = L.from_bmesh("carapace", bm)
    L.mod(shell, "SOLIDIFY", thickness=0.007, offset=1)
    L.apply_all(shell)
    shell.data.materials.append(m)
    parts.append(shell)
    for y in (-0.05, 0.0, 0.05):   # ridges in gold
        pts = []
        for i in range(9):
            x = -0.11 + i * 0.0275
            pts.append((Vector((x, y, H + 0.048 - 0.75 * x * x - 0.55 * (y + 0.005) ** 2)), 0.0035))
        rid = tube("ridge", pts, seg=6, sub=1)
        rid.data.materials.append(mg)
        parts.append(rid)
    for sx in (-1, 1):
        g = uv_sphere("guard", 0.035, (sx * 0.1, -0.045, H + 0.03), scale=(1.2, 1, 0.6), seg=14, rings=8)
        g.data.materials.append(m)
        parts.append(g)
        rim = tube("rim", [(Vector((sx * 0.07, -0.07, H + 0.03)), 0.004), (Vector((sx * 0.13, -0.05, H + 0.025)), 0.004), (Vector((sx * 0.12, -0.01, H + 0.02)), 0.004)], seg=6, sub=2)
        rim.data.materials.append(mg)
        parts.append(rim)
        sp = tube("spike", [(Vector((sx * 0.105, -0.045, H + 0.05)), 0.008), (Vector((sx * 0.12, -0.05, H + 0.08)), 0.001)], seg=8, sub=1)
        sp.data.materials.append(mg)
        parts.append(sp)
    ob = join("HeavyArmor", parts)
    L.bake(ob, size=512)
    gem = uv_sphere("GlowInsert", 0.012, (0, -0.02, H + 0.05), scale=(1.4, 1, 0.5), seg=12, rings=8)
    gem.data.materials.append(L.mat_plain("harmor_insert", "#4ad8ff", rough=0.2, emission=("#4ad8ff", 8.0)))
    parent(gem, ob)
    L.export_tree("heavy_armor", ob)


def build_energy_rifle():
    """The energy rifle: a steel body, a gold-ringed barrel and a crystal coil that glows (GlowCoil)."""
    m = mat_steel("rifle_steel")
    mg = mat_goldtrim("rifle_gold")
    md = L.mat_metal("rifle_dark", base="#2a2c30", patina="#1a1a1c", patina_amt=0.2, rough=0.4, metal_max=0.7)
    parts = []
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -0.02, 0.06))
    body = bpy.context.active_object
    body.scale = (0.05, 0.22, 0.08)
    L.apply_all(body)
    L.transform_apply(body)   # the root of the item: it must carry no scale (the game resets it)
    L.mod(body, "BEVEL", width=0.01, segments=3)
    body.data.materials.append(m)
    parts.append(body)
    bar = tube("barrel", [((0, -0.12, 0.08), 0.018), ((0, -0.42, 0.08), 0.014)], seg=14, sub=1)
    bar.data.materials.append(md)
    parts.append(bar)
    for y in (-0.2, -0.28, -0.36):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.024, minor_radius=0.005, location=(0, y, 0.08), rotation=(math.pi / 2, 0, 0))
        r = bpy.context.active_object
        r.data.materials.append(mg)
        parts.append(r)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.035, v.co.y * 0.05, v.co.z * 0.12))
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0.06, -0.02)) @ Matrix.Rotation(-0.3, 4, "X"), verts=bm.verts)
    grip = L.from_bmesh("grip", bm, smooth=False)
    L.mod(grip, "BEVEL", width=0.006, segments=2)
    grip.data.materials.append(md)
    parts.append(grip)
    stock = tube("stock", [((0, 0.09, 0.06), 0.03), ((0, 0.2, 0.04), 0.035)], seg=10, sub=1, flat=0.6, flat_axis=(1, 0, 0))
    stock.data.materials.append(md)
    parts.append(stock)
    ob = join("EnergyRifle", parts)
    L.bake(ob, size=512)
    coil = tube("GlowCoil", [((0, -0.04, 0.115), 0.012), ((0, -0.14, 0.115), 0.012)], seg=6, sub=1)
    coil.data.materials.append(L.mat_plain("rifle_coil", "#4ad8ff", rough=0.2, emission=("#4ad8ff", 10.0)))
    parent(coil, ob)
    empty("Muzzle", (0, -0.43, 0.08), ob)
    L.export_tree("energy_rifle", ob)


def build_tank():
    """TANK, the heavy robot (about 1.3 m): a squat steel body with yellow-black stripes, a round
    head with one eye (GlowEye), big fists and stubby legs. Body > Head > GlowEye, ArmL, ArmR,
    LegL, LegR."""
    m = mat_steel("tank_steel")
    my = L.mat_plain("tank_yellow", "#e0b020", rough=0.5)
    md = L.mat_metal("tank_dark", base="#2a2c30", patina="#1a1a1c", patina_amt=0.2, rough=0.45, metal_max=0.7)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.8))
    torso = bpy.context.active_object
    torso.scale = (0.7, 0.55, 0.6)
    L.apply_all(torso)
    L.transform_apply(torso)   # the root part must carry no scale
    L.mod(torso, "BEVEL", width=0.08, segments=3)
    torso.data.materials.append(my)
    stripe = []
    for k in range(4):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(-0.24 + k * 0.16, -0.285, 0.62), rotation=(0, 0.6, 0))
        st = bpy.context.active_object
        st.scale = (0.06, 0.02, 0.22)
        st.data.materials.append(md)
        stripe.append(st)
    vent = []
    for k in range(3):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0.28, 0.75 + k * 0.1))
        v = bpy.context.active_object
        v.scale = (0.4, 0.02, 0.04)
        v.data.materials.append(md)
        vent.append(v)
    body = join("Body", [torso] + stripe + vent)
    set_origin(body, (0, 0, 0))
    parts = [body]
    head = uv_sphere("dome", 0.22, (0, -0.05, 1.22), scale=(1, 1, 0.8), seg=20, rings=12)
    head.data.materials.append(m)
    visor = tube("visor", [((-0.15, -0.22, 1.22), 0.06), ((0.15, -0.22, 1.22), 0.06)], seg=10, sub=1)
    visor.data.materials.append(md)
    head = join("Head", [head, visor])
    set_origin(head, (0, -0.05, 1.1))
    parent(head, body)
    parts.append(head)
    for side, sx in (("L", 1), ("R", -1)):
        sh = Vector((sx * 0.42, 0, 0.95))
        arm = tube("arm", [(sh, 0.12), (sh + Vector((sx * 0.1, -0.1, -0.3)), 0.1), (sh + Vector((sx * 0.08, -0.2, -0.45)), 0.1)], seg=10, sub=2)
        arm.data.materials.append(md)
        fist = L.blob("fist", 0.18, seed=80 + sx, squash=(1, 1.1, 0.9), rough=0.04, sub=3)
        for v in fist.data.vertices:
            v.co += sh + Vector((sx * 0.08, -0.25, -0.6))
        fist.data.materials.append(m)
        a = join("Arm" + side, [arm, fist])
        set_origin(a, sh)
        parent(a, body)
        parts.append(a)
        hip = Vector((sx * 0.22, 0, 0.45))
        leg = tube("leg", [(hip, 0.12), (hip + Vector((0, 0, -0.38)), 0.11)], seg=10, sub=1)
        leg.data.materials.append(md)
        foot = uv_sphere("foot", 0.16, hip + Vector((0, -0.06, -0.38)), scale=(1, 1.4, 0.55), seg=12, rings=8)
        foot.data.materials.append(m)
        lg = join("Leg" + side, [leg, foot])
        set_origin(lg, hip)
        parent(lg, body)
        parts.append(lg)
    for o in parts:
        L.shade_smooth(o, 35)
    L.bake_group(parts, "tank", size=1024, ao_dist=0.05)
    eye = uv_sphere("GlowEye", 0.06, (0, -0.285, 1.22), scale=(1, 0.5, 0.8), seg=12, rings=8)
    eye.data.materials.append(L.mat_plain("tank_eye", "#ff9a2a", rough=0.2, emission=("#ff9a2a", 10.0)))
    parent(eye, head)
    L.export_tree("tank", body)


# ---------------------------------------------------------------------- level 4 (circle of Envy)
def build_energy_blade():
    """The energy blade: a gilded hilt with a crystal, the blade itself pure light (GlowBlade)."""
    mg = mat_goldtrim("blade_gold")
    md = L.mat_metal("blade_dark", base="#2a2c30", patina="#1a1a1c", patina_amt=0.2, rough=0.4, metal_max=0.6)
    grip = tube("grip", [((0, 0.06, 0), 0.016), ((0, -0.05, 0), 0.016)], seg=10, sub=1)
    grip.data.materials.append(md)
    guard = tube("guard", [((-0.05, -0.055, 0), 0.01), ((0, -0.06, 0), 0.014), ((0.05, -0.055, 0), 0.01)], seg=8, sub=2)
    guard.data.materials.append(mg)
    pommel = uv_sphere("pommel", 0.02, (0, 0.07, 0), seg=10, rings=8)
    pommel.data.materials.append(mg)
    ob = join("EnergyBlade", [grip, guard, pommel])
    L.bake(ob, size=256)
    bm = bmesh.new()
    pts = [(-0.018, -0.065), (0.018, -0.065), (0.012, -0.5), (0.0, -0.56), (-0.012, -0.5)]
    f = [bm.verts.new((x, y, -0.004)) for x, y in pts]
    b = [bm.verts.new((x, y, 0.004)) for x, y in pts]
    bm.faces.new(f)
    bm.faces.new(list(reversed(b)))
    for i in range(len(pts)):
        bm.faces.new((f[(i + 1) % len(pts)], f[i], b[i], b[(i + 1) % len(pts)]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    blade = L.from_bmesh("GlowBlade", bm, smooth=False)
    blade.data.materials.append(L.mat_plain("blade_light", "#c8a0ff", rough=0.2, emission=("#c8a0ff", 10.0)))
    parent(blade, ob)
    empty("Tip", (0, -0.56, 0), ob)
    L.export_tree("energy_blade", ob)


def build_gravity_gun():
    """The gravity gun: a short dark body with three gold prongs around a violet core (GlowCore)."""
    md = L.mat_metal("grav_dark", base="#3a3a44", patina="#1a1a20", patina_amt=0.2, rough=0.35, metal_max=0.6)
    mg = mat_goldtrim("grav_gold")
    body = lathe("body", [(0.0, 0.12), (0.045, 0.12), (0.055, 0.0), (0.05, -0.12), (0.0, -0.12)], steps=20)
    for v in body.data.vertices:
        v.co = Vector((v.co.x, -v.co.z, v.co.y + 0.06))
    body.data.materials.append(md)
    parts = [body]
    for k in range(3):
        a = k / 3 * math.tau + math.pi / 2
        o = Vector((math.cos(a) * 0.05, 0, math.sin(a) * 0.05 + 0.06))
        pr = tube("prong", [(o + Vector((0, -0.08, 0)), 0.012), (o * 1.0 + Vector((0, -0.2, 0)) + (o - Vector((0, 0, 0.06))) * 0.6, 0.008)], seg=6, sub=1)
        pr.data.materials.append(mg)
        parts.append(pr)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.032, v.co.y * 0.045, v.co.z * 0.11))
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0.06, -0.02)) @ Matrix.Rotation(-0.3, 4, "X"), verts=bm.verts)
    grip = L.from_bmesh("grip", bm, smooth=False)
    grip.data.materials.append(md)
    parts.append(grip)
    ob = join("GravityGun", parts)
    L.transform_apply(ob)
    L.bake(ob, size=512)
    core = uv_sphere("GlowCore", 0.03, (0, -0.17, 0.06), seg=12, rings=8)
    core.data.materials.append(L.mat_plain("grav_core", "#b07aff", rough=0.2, emission=("#b07aff", 12.0)))
    parent(core, ob)
    empty("Muzzle", (0, -0.2, 0.06), ob)
    L.export_tree("gravity_gun", ob)


def build_spark_core():
    """The Spark's core (level 4, the crab-mage): a crown of violet crystals on the shell with a
    ring of glowing runes (GlowRunes) — in the hero model's units, on the Body."""
    H = 0.062
    mb = mat_goldtrim("core_gold")
    base = lathe("base", [(0.03, H + 0.035), (0.034, H + 0.042), (0.026, H + 0.05), (0.0, H + 0.05)], steps=16)
    base.data.materials.append(mb)
    ob = join("SparkCoreBase", [base])
    L.bake(ob, size=256)
    shards = []
    for k in range(5):
        a = k / 5 * math.tau
        tilt = 0.35 if k else 0.0
        ln = 0.05 if k else 0.07
        bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.008, radius2=0.0, depth=ln, location=(0, 0, ln / 2))
        sh = bpy.context.active_object
        sh.rotation_euler = (math.cos(a) * tilt, math.sin(a) * tilt, 0)
        sh.location = (math.cos(a) * 0.012 * (1 if k else 0), -0.01 + math.sin(a) * 0.012 * (1 if k else 0), H + 0.045)
        L.transform_apply(sh)
        shards.append(sh)
    cr = join("GlowCrystals", shards)
    cr.data.materials.append(L.mat_plain("core_crystal", "#c8a0ff", rough=0.15, emission=("#b07aff", 6.0)))
    parent(cr, ob)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.09, minor_radius=0.0025, location=(0, 0, H + 0.03))
    runes = bpy.context.active_object
    runes.name = "GlowRunes"
    runes.data.materials.append(L.mat_plain("core_runes", "#c8a0ff", rough=0.3, emission=("#c8a0ff", 5.0)))
    parent(runes, ob)
    L.export_tree("spark_core", ob)


# ---------------------------------------------------------------------- level 5 (circle of Despondency)
def build_cyber_frame():
    """The cyber-frame (level 5, the cyber-crab): a steel spine on the shell, two jointed
    mechanical limbs rising from the back with small claws, an antenna and a scanner lens over
    the right eye (GlowLens, GlowLights) — in the hero model's units, on the Body."""
    H = 0.062
    m = mat_steel("cyber_steel")
    md = L.mat_metal("cyber_dark", base="#2a2c30", patina="#1a1a1c", patina_amt=0.2, rough=0.4, metal_max=0.6)
    parts = []
    spine = tube("spine", [((0, 0.06, H + 0.03), 0.012), ((0, 0.0, H + 0.045), 0.012), ((0, -0.05, H + 0.04), 0.01)], seg=8, sub=2)
    spine.data.materials.append(m)
    parts.append(spine)
    for sx in (-1, 1):
        b = Vector((sx * 0.03, 0.05, H + 0.04))
        k1 = b + Vector((sx * 0.04, 0.03, 0.06))
        k2 = k1 + Vector((sx * 0.02, -0.06, 0.03))
        arm = tube("limb", [(b, 0.008), (k1, 0.007), (k2, 0.006)], seg=6, sub=1)
        arm.data.materials.append(md)
        parts.append(arm)
        for jnt in (b, k1):
            j = uv_sphere("joint", 0.01, jnt, seg=8, rings=6)
            j.data.materials.append(m)
            parts.append(j)
        for c in (-1, 1):
            cl = tube("claw", [(k2, 0.004), (k2 + Vector((c * 0.008, -0.02, -0.006)), 0.001)], seg=5, sub=1)
            cl.data.materials.append(m)
            parts.append(cl)
    ant = tube("antenna", [((-0.02, 0.06, H + 0.04), 0.002), ((-0.025, 0.08, H + 0.11), 0.0012)], seg=5, sub=1)
    ant.data.materials.append(md)
    parts.append(ant)
    ob = join("CyberFrame", parts)
    L.bake(ob, size=256)
    lens = uv_sphere("GlowLens", 0.008, (-0.032, -0.058, H + 0.06), scale=(1, 0.4, 1), seg=10, rings=6)
    lens.data.materials.append(L.mat_plain("cyber_lens", "#5adcff", rough=0.2, emission=("#5adcff", 10.0)))
    parent(lens, ob)
    lights = [uv_sphere("l", 0.003, (sx * 0.03, 0.05 - k * 0.03, H + 0.05), seg=6, rings=4) for sx in (-1, 1) for k in range(2)]
    gl = join("GlowLights", lights)
    gl.data.materials.append(L.mat_plain("cyber_lights", "#5adcff", rough=0.2, emission=("#5adcff", 8.0)))
    parent(gl, ob)
    L.export_tree("cyber_frame", ob)


def build_hunter():
    """HUNTER, the long-range drone: a slim dark hull with swept fins and a long barrel, a red
    targeting eye (GlowEye). Body (with Muzzle)."""
    md = L.mat_metal("hunter_dark", base="#2a2c34", patina="#14141a", patina_amt=0.2, rough=0.35, metal_max=0.6)
    mr = L.mat_plain("hunter_red", "#8a1a1a", rough=0.5)
    hull = uv_sphere("hull", 0.12, (0, 0, 0), scale=(0.8, 2.0, 0.6), seg=20, rings=12)
    hull.data.materials.append(md)
    barrel = tube("barrel", [((0, -0.2, -0.03), 0.018), ((0, -0.55, -0.03), 0.014)], seg=10, sub=1)
    barrel.data.materials.append(md)
    fins = []
    for sx in (-1, 1):
        f = tube("fin", [((sx * 0.06, 0.05, 0), 0.04), ((sx * 0.28, 0.2, 0.02), 0.01)], seg=6, sub=1, flat=0.2, flat_axis=(0, 0, 1))
        f.data.materials.append(mr)
        fins.append(f)
    ob = join("Body", [hull, barrel] + fins)
    L.bake(ob, size=256)
    eye = uv_sphere("GlowEye", 0.03, (0, -0.2, 0.04), scale=(1, 0.5, 1), seg=10, rings=8)
    eye.data.materials.append(L.mat_plain("hunter_eye", "#ff3a2a", rough=0.2, emission=("#ff3a2a", 12.0)))
    parent(eye, ob)
    empty("Muzzle", (0, -0.56, -0.03), ob)
    L.export_tree("hunter", ob)


# ---------------------------------------------------------------------- level 6 (circle of Betrayal)
def build_engineer():
    """ENGINEER: a small walking robot (about 0.9 m) on two legs, a round green-striped body and a
    welding arm (Arm). Body > Arm, LegL, LegR, GlowEye."""
    m = mat_steel("eng_steel")
    mg = L.mat_plain("eng_green", "#2a9a6a", rough=0.5)
    md = L.mat_metal("eng_dark", base="#2a2c30", patina="#1a1a1c", patina_amt=0.2, rough=0.45, metal_max=0.6)
    hull = uv_sphere("hull", 0.32, (0, 0, 0.62), scale=(1, 0.9, 0.85), seg=20, rings=12)
    hull.data.materials.append(m)
    band = lathe("band", [(0.33, 0.55), (0.34, 0.62), (0.33, 0.69)], steps=20)
    band.data.materials.append(mg)
    tank = tube("tank", [((0, 0.25, 0.5), 0.1), ((0, 0.28, 0.85), 0.1)], seg=10, sub=1)
    tank.data.materials.append(mg)
    body = join("Body", [hull, band, tank])
    set_origin(body, (0, 0, 0))
    parts = [body]
    sh = Vector((0.28, -0.05, 0.7))
    arm = tube("arm", [(sh, 0.04), (sh + Vector((0.05, -0.2, -0.05)), 0.035), (sh + Vector((0.0, -0.38, 0.05)), 0.03)], seg=8, sub=1)
    arm.data.materials.append(md)
    torch = tube("torch", [(sh + Vector((0.0, -0.38, 0.05)), 0.025), (sh + Vector((0.0, -0.5, 0.08)), 0.01)], seg=6, sub=1)
    torch.data.materials.append(mg)
    a = join("Arm", [arm, torch])
    set_origin(a, sh)
    parent(a, body)
    parts.append(a)
    for side, sx in (("L", 1), ("R", -1)):
        hip = Vector((sx * 0.14, 0, 0.36))
        leg = tube("leg", [(hip, 0.05), (hip + Vector((0, -0.05, -0.2)), 0.04), (hip + Vector((0, 0.02, -0.34)), 0.04)], seg=8, sub=1)
        leg.data.materials.append(md)
        foot = uv_sphere("foot", 0.07, hip + Vector((0, -0.04, -0.34)), scale=(1, 1.5, 0.5), seg=10, rings=6)
        foot.data.materials.append(m)
        lg = join("Leg" + side, [leg, foot])
        set_origin(lg, hip)
        parent(lg, body)
        parts.append(lg)
    for o in parts:
        L.shade_smooth(o, 35)
    L.bake_group(parts, "engineer", size=512, ao_dist=0.04)
    eye = uv_sphere("GlowEye", 0.06, (0, -0.29, 0.72), scale=(1.4, 0.4, 0.7), seg=10, rings=6)
    eye.data.materials.append(L.mat_plain("eng_eye", "#5affb0", rough=0.2, emission=("#5affb0", 10.0)))
    parent(eye, body)
    L.export_tree("engineer", body)


def build_mage_orb():
    """MAG: a sphere of violet crystal (glowing) in two turning brass rings (RingA, RingB)."""
    mb = mat_goldtrim("mage_brass")
    core = uv_sphere("GlowCore", 0.16, (0, 0, 0), seg=20, rings=14)
    core.data.materials.append(L.mat_plain("mage_core", "#b07aff", rough=0.15, emission=("#b07aff", 8.0)))
    ob = core
    ob.name = "Body"
    for nm, r_ in (("RingA", 0.26), ("RingB", 0.32)):
        bpy.ops.mesh.primitive_torus_add(major_radius=r_, minor_radius=0.015, location=(0, 0, 0))
        t_ = bpy.context.active_object
        t_.name = nm
        t_.data.materials.append(mb)
        L.bake(t_, size=128)
        t_.name = nm
        parent(t_, ob)
    L.export_tree("mage_orb", ob)


def build_turret():
    """The ENGINEER's turret: a tripod and a turning head (Head) with a short barrel (Muzzle)."""
    m = mat_steel("tur_steel")
    mg = L.mat_plain("tur_green", "#2a9a6a", rough=0.5)
    legs = []
    for k in range(3):
        a = k / 3 * math.tau
        legs.append(tube("leg", [((0, 0, 0.45), 0.03), ((math.cos(a) * 0.35, math.sin(a) * 0.35, 0.0), 0.025)], seg=6, sub=1))
    for l_ in legs:
        l_.data.materials.append(m)
    base = join("Base", legs)
    L.bake(base, size=128)
    head = box_item("headbox", (0.24, 0.3, 0.18), (0, 0, 0.55), mg)
    barrel = tube("barrel", [((0, -0.15, 0.56), 0.025), ((0, -0.42, 0.56), 0.02)], seg=8, sub=1)
    barrel.data.materials.append(m)
    hd = join("Head", [head, barrel])
    L.bake(hd, size=128)
    set_origin(hd, (0, 0, 0.5))
    parent(hd, base)
    empty("Muzzle", (0, -0.43, 0.56), hd)
    L.export_tree("turret", base)


def box_item(name, size, loc, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    b = bpy.context.active_object
    b.name = name
    b.scale = size
    L.apply_all(b)
    L.transform_apply(b)
    L.mod(b, "BEVEL", width=0.01, segments=2)
    L.apply_all(b)
    b.data.materials.append(mat)
    return b


# ---------------------------------------------------------------------- level 7 (circle of Violence)
def mat_paint(name, base="#3d6a9a", dark="#22384f", wear_to="#a8adb4"):
    """Painted armour: the colour darker in the cavities, chipped to bare steel on the sharp edges."""
    t = L.NT(name)
    n = t.noise(t.vec(), 14, detail=6, rough=0.6)
    # pointiness is about 0.5 on flat faces: only clear edges, broken up by the noise, wear through
    edge = t.ramp(t.math("ADD", t.geo("Pointiness"), t.math("MULTIPLY", t.math("SUBTRACT", n, 0.5), 0.1)), [(0.6, 0.0), (0.66, 1.0)])
    cav = t.math("SUBTRACT", 1.0, t.ao(0.1))
    col = t.mix(t.ramp(cav, [(0.1, 0.0), (0.6, 1.0)]), C(base), C(dark))
    col = t.mix(edge, col, C(wear_to))
    metal = t.ramp(edge, [(0.0, 0.1), (1.0, 0.6)])
    rgh = t.ramp(edge, [(0.0, 0.55), (1.0, 0.3)])
    return t.finish(col, rough=rgh, metal=metal, normal=t.bump(n, 0.15, 0.01))


def mat_hazard(name, freq=40.0):
    """Diagonal hazard stripes, orange and near black, a little worn."""
    t = L.NT(name)
    x, y, z = t.sep(t.vec())
    w = t.math("SINE", t.math("MULTIPLY", t.math("ADD", t.math("ADD", x, y), z), freq))
    n = t.noise(t.vec(), 18, detail=4)
    band = t.ramp(t.math("ADD", w, t.math("MULTIPLY", n, 0.3)), [(0.48, 0.0), (0.52, 1.0)])
    col = t.mix(band, C("#1c1c1e"), C("#ff8a1e"))
    return t.finish(col, rough=0.5, metal=0.1, normal=t.bump(n, 0.1, 0.01))


def taper_box(name, size, loc, mat, top=1.0, front=1.0, bevel=0.025):
    """A bevelled box whose top is scaled by `top` (a chest broader than the waist) and whose front
    face leans by `front` (scale of the front half at the top)."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
    b = bpy.context.active_object
    b.name = name
    for v in b.data.vertices:
        if v.co.z > 0:
            v.co.x *= top
            v.co.y *= top
            if v.co.y < 0:
                v.co.y *= front
    b.scale = size
    L.apply_all(b)
    L.transform_apply(b)
    b.location = loc
    L.transform_apply(b)
    L.mod(b, "BEVEL", width=bevel, segments=3)
    L.apply_all(b)
    b.data.materials.append(mat)
    return b


def build_battlebot():
    """A battle robot of the crab's army (about 1.25 m): a small walker. A tapered armoured chest
    with a dark visor helmet, round pauldrons with hazard bands, a twin cannon in a housing on the
    right shoulder (Gun, its origin at the muzzles), a fist on the left, a backpack with exhausts and
    an antenna; reverse-jointed legs with knee guards and three-toed feet (LegL, LegR). Body >
    Gun, LegL, LegR, GlowEye, GlowVents."""
    pb = mat_paint("bb_paint")
    pd = mat_paint("bb_paint_dark", base="#2a3a4c", dark="#151d27")
    hz = mat_hazard("bb_hazard")
    st = mat_steel("bb_steel")
    md = L.mat_metal("bb_dark", base="#2a2c30", patina="#1a1a1c", patina_amt=0.2, rough=0.45, metal_max=0.6)
    body_parts = []
    # pelvis, waist, chest
    body_parts.append(taper_box("pelvis", (0.36, 0.26, 0.14), (0, 0.01, 0.56), pd, top=1.05, bevel=0.02))
    waist = tube("waist", [((0, 0.02, 0.6), 0.1), ((0, 0.02, 0.7), 0.09)], seg=12, sub=1)
    waist.data.materials.append(md)
    body_parts.append(waist)
    body_parts.append(taper_box("chest", (0.44, 0.34, 0.36), (0, 0.0, 0.86), pb, top=1.28, front=0.9, bevel=0.035))
    body_parts.append(taper_box("chestplate", (0.3, 0.06, 0.22), (0, -0.22, 0.88), pd, top=1.15, bevel=0.02))
    for sx in (-1, 1):   # vents in the chest plate
        for k in range(3):
            body_parts.append(box_item("slot", (0.07, 0.02, 0.012), (sx * 0.07, -0.255, 0.84 + k * 0.035), md))
    # helmet: a rounded dome with a visor band
    helm = uv_sphere("helm", 0.15, (0, -0.04, 1.14), scale=(1.0, 1.05, 0.78), seg=20, rings=12)
    helm.data.materials.append(pb)
    body_parts.append(helm)
    jaw = taper_box("jaw", (0.2, 0.12, 0.08), (0, -0.11, 1.06), pd, top=1.1, bevel=0.02)
    body_parts.append(jaw)
    visor_frame = taper_box("visorframe", (0.26, 0.05, 0.08), (0, -0.165, 1.15), md, bevel=0.015)
    body_parts.append(visor_frame)
    ant = tube("antenna", [((0.09, 0.05, 1.22), 0.008), ((0.13, 0.12, 1.42), 0.004)], seg=6, sub=1)
    ant.data.materials.append(st)
    body_parts.append(ant)
    body_parts.append(uv_sphere("antip", 0.012, (0.13, 0.12, 1.42), seg=8, rings=6))
    body_parts[-1].data.materials.append(hz)
    # pauldrons with hazard bands
    for sx in (-1, 1):
        pl = uv_sphere("pauldron", 0.13, (sx * 0.33, 0.0, 1.0), scale=(1.0, 1.15, 0.75), seg=18, rings=10)
        pl.data.materials.append(pb)
        body_parts.append(pl)
        bpy.ops.mesh.primitive_torus_add(major_radius=0.12, minor_radius=0.022, location=(sx * 0.33, 0.0, 0.95))
        band = bpy.context.active_object
        band.name = "band"
        band.scale = (1.0, 1.15, 1.0)
        L.apply_all(band)
        L.transform_apply(band)
        band.data.materials.append(hz)
        body_parts.append(band)
    # the left arm: upper arm, forearm guard, fist
    sh = Vector((-0.36, 0.0, 0.92))
    arm = tube("arm", [(sh, 0.05), (sh + Vector((-0.03, -0.02, -0.18)), 0.045)], seg=10, sub=1)
    arm.data.materials.append(md)
    body_parts.append(arm)
    body_parts.append(taper_box("forearm", (0.12, 0.2, 0.12), sh + Vector((-0.03, -0.1, -0.24)), pb, top=1.1, bevel=0.02))
    fist = uv_sphere("fist", 0.065, sh + Vector((-0.03, -0.22, -0.26)), scale=(1, 1.1, 0.95), seg=12, rings=8)
    fist.data.materials.append(st)
    body_parts.append(fist)
    # backpack and exhausts
    body_parts.append(taper_box("pack", (0.32, 0.16, 0.3), (0, 0.24, 0.9), pd, top=0.9, bevel=0.03))
    for sx in (-1, 1):
        ex = tube("exhaust", [((sx * 0.09, 0.3, 0.98), 0.035), ((sx * 0.1, 0.33, 1.12), 0.03)], seg=10, sub=1)
        ex.data.materials.append(md)
        body_parts.append(ex)
    body = join("Body", body_parts)
    set_origin(body, (0, 0, 0))
    parts = [body]
    # the twin cannon on the right shoulder, its origin at the muzzles
    gp = [taper_box("housing", (0.16, 0.3, 0.14), (0.36, -0.08, 1.12), pd, bevel=0.025)]
    for dx in (-0.035, 0.035):
        br = tube("barrel", [((0.36 + dx, -0.2, 1.12), 0.028), ((0.36 + dx, -0.5, 1.12), 0.024)], seg=10, sub=1)
        br.data.materials.append(md)
        gp.append(br)
        for k in range(3):
            bpy.ops.mesh.primitive_torus_add(major_radius=0.03, minor_radius=0.007, location=(0.36 + dx, -0.26 - k * 0.07, 1.12), rotation=(math.pi / 2, 0, 0))
            rg = bpy.context.active_object
            rg.data.materials.append(st)
            gp.append(rg)
        mz = tube("muzzle", [((0.36 + dx, -0.5, 1.12), 0.032), ((0.36 + dx, -0.55, 1.12), 0.03)], seg=10, sub=1)
        mz.data.materials.append(st)
        gp.append(mz)
    g = join("Gun", gp)
    set_origin(g, (0.36, -0.55, 1.12))
    parent(g, body)
    parts.append(g)
    # reverse-jointed legs: thigh forward to the knee, shin back down to the ankle, a three-toed foot
    for side, sx in (("L", 1), ("R", -1)):
        hip = Vector((sx * 0.15, 0.02, 0.54))
        knee = hip + Vector((sx * 0.02, -0.12, -0.2))
        ankle = hip + Vector((sx * 0.02, 0.08, -0.44))
        lp = []
        th = tube("thigh", [(hip, 0.065), (knee, 0.055)], seg=10, sub=1)
        th.data.materials.append(md)
        lp.append(th)
        lp.append(taper_box("thighplate", (0.11, 0.08, 0.16), (hip + knee) / 2 + Vector((0, -0.05, 0)), pb, top=1.1, bevel=0.02))
        kg = uv_sphere("kneeguard", 0.06, knee + Vector((0, -0.035, 0.01)), scale=(1, 0.8, 1.1), seg=12, rings=8)
        kg.data.materials.append(hz)
        lp.append(kg)
        sh_ = tube("shin", [(knee, 0.05), (ankle, 0.04)], seg=10, sub=1)
        sh_.data.materials.append(st)
        lp.append(sh_)
        piston = tube("piston", [(knee + Vector((0, 0.04, -0.02)), 0.015), (ankle + Vector((0, 0.04, 0.06)), 0.012)], seg=6, sub=1)
        piston.data.materials.append(md)
        lp.append(piston)
        for a in (-0.5, 0.0, 0.5):   # three toes and a heel spur
            d = Vector((math.sin(a) * 0.1, -math.cos(a) * 0.13, -0.08))
            toe = tube("toe", [(ankle, 0.03), (ankle + d, 0.022)], seg=8, sub=1)
            toe.data.materials.append(pd)
            lp.append(toe)
        heel = tube("heel", [(ankle, 0.026), (ankle + Vector((0, 0.09, -0.08)), 0.02)], seg=8, sub=1)
        heel.data.materials.append(pd)
        lp.append(heel)
        lg = join("Leg" + side, lp)
        set_origin(lg, hip)
        parent(lg, body)
        parts.append(lg)
    for o in parts:
        L.shade_smooth(o, 32)
    L.bake_group(parts, "battlebot", size=512, ao_dist=0.04)
    eye = taper_box("GlowEye", (0.22, 0.02, 0.035), (0, -0.19, 1.15), L.mat_plain("bb_eye", "#5ac8ff", rough=0.2, emission=("#5ac8ff", 10.0)), bevel=0.008)
    parent(eye, body)
    vents = []
    for sx in (-1, 1):
        v = uv_sphere("vent", 0.026, (sx * 0.1, 0.33, 1.125), scale=(1, 1, 0.3), seg=10, rings=6)
        vents.append(v)
    gv = join("GlowVents", vents)
    gv.data.materials.append(L.mat_plain("bb_vent", "#ff9a40", rough=0.3, emission=("#ff8a30", 6.0)))
    parent(gv, body)
    L.export_tree("battlebot", body)


# ---------------------------------------------------------------------- level 9 (circle of Power)
def build_matter_lord():
    """The Lord of Matter (level 9): what goes on the crab's own shell — the plates of a battle robot
    over the back and the claws' bases, two wings of light (GlowWings), a ring of the AI turning over
    it (Halo > GlowRing). In the hero model's units, on the Body; the face is left as it is."""
    H = 0.062
    m = mat_steel("lord_steel")
    mg = mat_goldtrim("lord_gold")
    parts = []
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=12, y_segments=8, size=0.5)
    for v in bm.verts:
        x = v.co.x * 0.26
        y = 0.0 + (v.co.y + 0.5) * 0.12
        v.co = Vector((x, y, H + 0.05 - 0.7 * x * x - 0.4 * (y - 0.05) ** 2))
    back = L.from_bmesh("backplate", bm)
    L.mod(back, "SOLIDIFY", thickness=0.008, offset=1)
    L.apply_all(back)
    back.data.materials.append(m)
    parts.append(back)
    for sx in (-1, 1):
        parts.append(uv_sphere("shoulder", 0.04, (sx * 0.13, -0.04, H + 0.03), scale=(1.2, 1, 0.7), seg=12, rings=8))
        parts[-1].data.materials.append(mg)
        parts.append(tube("vent", [((sx * 0.05, 0.11, H + 0.05), 0.01), ((sx * 0.06, 0.14, H + 0.09), 0.008)], seg=6, sub=1))
        parts[-1].data.materials.append(m)
    ob = join("LordPlates", parts)
    L.bake(ob, size=256)
    wings = []
    for sx in (-1, 1):
        # a fan of six feathers of light, long in the middle, each a thin curved blade
        root = Vector((sx * 0.06, 0.08, H + 0.08))
        for k in range(6):
            a = math.radians(10 + k * 17)
            length = 0.2 + 0.24 * math.sin(math.pi * (k + 0.6) / 6.4)
            d = Vector((sx * math.cos(a), 0.35 * math.sin(a) * 0.4, math.sin(a)))
            d.normalize()
            side = Vector((0, 1, 0)).cross(d).normalized()   # across the feather, in the wing's plane
            bm = bmesh.new()
            rows = 6
            left, right = [], []
            for r_ in range(rows + 1):
                f = r_ / rows
                w = 0.028 * math.sin(math.pi * min(1.0, f * 1.1 + 0.08)) + 0.004
                c = root + d * (length * f) + Vector((0, 0.05 * f * f, -0.02 * f * f))   # sweeps back and droops
                left.append(bm.verts.new(c - side * w))
                right.append(bm.verts.new(c + side * w))
            for r_ in range(rows):
                q = [left[r_], right[r_], right[r_ + 1], left[r_ + 1]]
                bm.faces.new(q if sx > 0 else list(reversed(q)))
            w_ = L.from_bmesh("feather", bm, smooth=True)
            L.mod(w_, "SOLIDIFY", thickness=0.003)
            L.apply_all(w_)
            wings.append(w_)
    gw = join("GlowWings", wings)
    gw.data.materials.append(L.mat_plain("lord_wings", "#fff0c8", rough=0.2, emission=("#ffe8a8", 6.0)))
    parent(gw, ob)
    halo = bpy.data.objects.new("Halo", None)
    L.link(halo)
    halo.location = (0, 0, H + 0.13)
    parent(halo, ob)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.07, minor_radius=0.0025, location=(0, 0, H + 0.13))
    ring = bpy.context.active_object
    ring.name = "GlowRing"
    ring.data.materials.append(L.mat_plain("lord_ring", "#5adcff", rough=0.2, emission=("#5adcff", 8.0)))
    set_origin(ring, (0, 0, H + 0.13))
    parent(ring, halo)
    L.export_tree("matter_lord", ob)

ASSETS = {"stick": build_stick, "stone": build_stone, "crowbar": build_crowbar, "knife": build_knife, "pistol": build_pistol,
          "machinegun": build_machinegun, "armor": build_armor, "drone": build_drone,
          "heavy_armor": build_heavy_armor, "energy_rifle": build_energy_rifle, "tank": build_tank,
          "energy_blade": build_energy_blade, "gravity_gun": build_gravity_gun, "spark_core": build_spark_core,
          "cyber_frame": build_cyber_frame, "hunter": build_hunter,
          "engineer": build_engineer, "mage_orb": build_mage_orb, "turret": build_turret,
          "battlebot": build_battlebot, "matter_lord": build_matter_lord}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    L.OUT = argv[0] if argv else os.path.abspath("Assets/Crab/Models/Items")
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
