"""
The first circle, Limbo, laid out once into Levels/limbo.blend (after Dante: the grey valley of
the virtuous souls before the Noble Castle of seven walls). From then on the .blend is the
source: edit it by hand and export it with export_level.py. Won't overwrite an existing .blend
(pass --force to start over).

Run headless:
  blender -b -P Tools/blender/make_level_limbo.py [-- --force]

South to north (game z), the way the crab goes:
  1 the grey shore   — the start by a dead sea; crags, a dead tree, a field of steles;
  2 the ruins (west) — arches and an aqueduct on the hills, the round temple on the top; a rift;
  3 the valley       — misty, cypress groves, the paths the processions of souls walk;
  4 the dead lake    — black water, broken arches on its shore; a rift;
  5 the poet's cliff — a colossal statue looking over the valley, a ramp up to it; a beacon;
  6 the ravine       — the bridge across it; a rift at its foot;
  7 the gate         — the Noble Castle's first wall, statues, the Guardian's ground before the
                       gate, the way out through it; the castle itself far off in the fog.
The paths are painted lighter into the land's Paint layer. Everything is laid out by level.Placer:
the great structures first, then the game's places (each nudged to where it has room, the space
round it kept clear), then the scenery by purpose — rows of steles, an avenue of columns, a
portico, arches round the lake, groves, crags — each piece only where its real footprint fits.
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

NAME = "limbo"
AREA = (-110, -96, 220, 300)   # game x, z, width, depth
STEP = 0.75
TILE = 0.2                     # the ground repeats every 5 m

# the places (game x, z)
START = (0, -74)
RUINS = (-62, 0)
TEMPLE = (-66, 8)
LAKE = (55, 5)
CLIFF = (84, 54)
RAMP = ((56, 34), (77, 49))
RAVINE_Z = 104
BRIDGE_X = 20
ARENA = (20, 134)
GATE = (20, 158)
PATHS = [
    [(0, -76), (4, -60), (-4, -42), (-8, -26), (0, -8), (6, 10), (14, 32), (10, 56), (14, 74), (20, 86)],   # the way
    [(-8, -26), (-26, -18), (-44, -10), (-58, -2), (-66, 6)],                                           # to the ruins
    [(0, -8), (18, -2), (34, 4)],                                                                        # to the lake
    [(14, 32), (34, 30), (56, 34), (77, 49)],                                                            # to the poet
    [(20, 120), (20, 134), (20, 152)],                                                                   # the gate
]


def perlin01(a, b, s=0.37):
    return 0.5 + 0.5 * noise.noise(Vector((a, b, s)))


def smooth(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def dist(x, z, p):
    return math.hypot(x - p[0], z - p[1])


def seg_dist(x, z, a, b):
    ax, az = a
    bx, bz = b
    dx, dz = bx - ax, bz - az
    t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)))
    return math.hypot(x - ax - t * dx, z - az - t * dz), t


def path_dist(x, z):
    return min(seg_dist(x, z, p[i], p[i + 1])[0] for p in PATHS for i in range(len(p) - 1))


def height(x, z):
    h = (perlin01(x * 0.025 + 7, z * 0.025 + 3) - 0.5) * 2.4
    h += (perlin01(x * 0.09, z * 0.09) - 0.5) * 0.6 + (perlin01(x * 0.3, z * 0.3) - 0.5) * 0.12
    # the dead sea in the south
    if z < -80:
        h -= (-80 - z) * 0.3
    # the valley's walls east and west, crags heaped on them
    rim = max(0.0, abs(x) - 80)
    h += rim * 0.6 + rim * perlin01(x * 0.05, z * 0.05, 1.3) * 0.8
    # the rocky foreground (south-west) and the ruins' hills (west)
    h += 9 * math.exp(-(dist(x, z, (-88, -62)) / 22) ** 2)
    h += 7.5 * math.exp(-(dist(x, z, RUINS) / 24) ** 2)
    # the lake's bowl
    h = h + (-2.6 - h) * smooth(22, 13, dist(x, z, LAKE))
    # the poet's cliff and the ramp up to it
    h = max(h, 13 * smooth(15, 8, dist(x, z, CLIFF)))
    d, t = seg_dist(x, z, *RAMP)
    h = max(h, h + (1 + 11.5 * t - h) * smooth(6, 3, d))
    # the gate's ground: level from the ravine to the wall
    gate = smooth(4, 0, max(abs(x - BRIDGE_X) - 34, z < 116 and 116 - z or 0, z - 162))
    h = h + (1.0 - h) * gate
    # beyond the wall the land climbs toward the castle
    if z > 162:
        h += (z - 162) * 0.12
    # the ravine across the valley, rough-bottomed; the bridge's ends level with each other
    h = h + (-10 + noise.noise(Vector((x * 0.1, z * 0.1, 4.2))) * 1.5 - h) * smooth(10, 6, abs(z - RAVINE_Z))
    for end in ((BRIDGE_X, RAVINE_Z - 17), (BRIDGE_X, RAVINE_Z + 17)):
        h = h + (0.8 - h) * smooth(9, 5, dist(x, z, end))
    # the paths sink a little into the ground
    h -= 0.12 * smooth(2.5, 0.5, path_dist(x, z))
    return h


def at(x, z, sink=0.05):
    return from_unity(x, height(x, z) - sink, z)


def ash_images(n=1024):
    """Dead ground: ash-grey dust with dry dark grass and grit, tiling."""
    rng = np.random.default_rng(11)
    hgt = sum(LV.periodic_noise(n, c, rng) * w for c, w in ((4, 0.5), (16, 0.3), (64, 0.2), (256, 0.15)))
    grass = LV.periodic_noise(n, 128, rng) * LV.periodic_noise(n, 32, rng)
    grass = np.clip((grass - 0.25) * 4, 0, 1)
    grit = rng.random((n, n)) ** 8
    tone = (hgt - hgt.mean()) / (hgt.std() + 1e-6)
    dust = np.array([0.34, 0.33, 0.30])
    dry = np.array([0.24, 0.24, 0.19])
    col = dust[None, None, :] * (1 + 0.08 * tone[..., None])
    col = col * (1 - grass[..., None]) + dry[None, None, :] * grass[..., None]
    col -= 0.1 * grit[..., None]
    col = np.clip(col, 0, 1)
    return LV.image("ash_color", col, "sRGB"), LV.image("ash_normal", LV.normal_map(hgt + grass * 0.4, 5), "Non-Color")


def paint(ground):
    """The land's own paint: the paths lighter and dustier, the rest a little darker."""
    me = ground.data
    attr = me.color_attributes.new("Paint", "BYTE_COLOR", "POINT")
    for v, c in zip(me.vertices, attr.data):
        x, z = -v.co.x, -v.co.y
        p = smooth(2.8, 0.6, path_dist(x, z))
        k = 0.82 + 0.18 * p
        c.color = (k * (1 + 0.04 * p), k * (1 + 0.03 * p), k, 1)


def lake_dist(x, z):
    return dist(x, z, LAKE)


def clear(pts, group):
    """Where a thing of `group` may stand (game points of its footprint): off the paths (avenue columns
    may flank them closer), out of the water and the sea, out of the ravine (but its own crags)."""
    keep = {"avenue": 1.6, "crag": 3.0, "ravine": 0.0, "shore": 2.0}.get(group, 3.2)
    for x, z in pts:
        if keep and path_dist(x, z) < keep:
            return False
        if z < -80 or abs(x) > 108:
            return False
        if group != "shore" and lake_dist(x, z) < 15.5:
            return False
        if group != "ravine" and abs(z - RAVINE_Z) < 11:
            return False
    return True


P = LV.Placer(clear, merge=("crag", "castle", "ravine"))


def along(ax, az, bx, bz):
    """The Blender yaw that turns a model's X axis along the game direction a → b."""
    return math.degrees(math.atan2(-(bz - az), -(bx - ax)))


def nudge(path, x, z, yaw=0.0, scale=1.0, sink=0.05, group="spot", margin=0.3, **props):
    """A game spot as close to (x, z) as there is room for it (spiral out up to 9 m)."""
    for r in [0] + [1.5 * k for k in range(1, 7)]:
        for a in range(0, 360, 30 if r else 360):
            px, pz = x + r * math.cos(math.radians(a)), z + r * math.sin(math.radians(a))
            ob = P.place(path, at(px, pz, sink), yaw, scale, group=group, margin=margin, **props)
            if ob:
                return ob, px, pz
    ob = P.place(path, at(x, z, sink), yaw, scale, group=group, force=True, **props)
    print("CROWDED", props.get("name", path), x, z)
    return ob, x, z


def gate_front():
    """The game z of the gatehouse's south face."""
    _, _, _, hy = LV.footprint("World/castle_gate")
    return GATE[1] - hy


def structures(rng):
    """The great things first: they stand where they must, and the rest keeps out of their way."""
    # the castle's first wall across the north: the gatehouse, runs of wall out from it both ways
    # (as far as the valley's floor goes), a tower at every other joint
    _, _, gx, _ = LV.footprint("World/castle_gate")
    _, _, wx, _ = LV.footprint("World/castle_wall/CastleWall")
    P.place("World/castle_gate", at(*GATE, 0.2), 180, 1.0, group="castle", force=True, collider="mesh", snap=1)
    step = 2 * wx - 0.4
    for side in (-1, 1):
        j = 0
        while True:
            x = GATE[0] + side * (gx - 0.5 + wx + j * step)
            if abs(x + side * wx) > 86:
                break
            P.place("World/castle_wall/CastleWall", at(x, GATE[1] + 1.5, 0.3), 0, 1.0, group="castle", force=True, collider="mesh", snap=1)
            if j % 2 == 1:
                P.place("World/castle_wall/CastleTower", at(x + side * wx, GATE[1] + 2.5, 0.3), 0, 1.0, group="castle", force=True, collider="mesh", snap=1)
            j += 1
    front = gate_front()
    for side in (-1, 1):
        P.place("World/gate_statue", at(GATE[0] + side * 6.0, front - 3.5, 0.1), 180, 1.0, group="castle", force=True, collider="mesh", snap=1)
    # the Guardian's ground: a ring of broken columns, open to the south (the bridge) and the north (the gate)
    for i in range(14):
        a = i / 14 * math.tau
        if abs(math.sin(a)) > 0.8:
            continue
        x, z = ARENA[0] + math.cos(a) * 19, ARENA[1] + math.sin(a) * 19
        P.place("World/column", at(x, z, 0.1), -rng.uniform(0, 360), rng.uniform(1.0, 1.2), group="arena", force=True, collider="mesh", snap=1)
    # the bridge over the ravine (its origin is the deck's top: not set down on the ravine's floor)
    P.place("World/bridge", from_unity(BRIDGE_X, 0.8, RAVINE_Z), 0, 1.0, group="ravine", force=True, collider="mesh", snap=0)
    # the poet on the cliff, looking down over the valley (south-west)
    P.place("World/poet", at(CLIFF[0] + 1, CLIFF[1] + 1, 0.2), 135, 1.0, group="cliff", force=True, into="Game", name="Poet", role="poet", collider="mesh", snap=1)
    # the round temple on the ruins' hill, the aqueduct striding over the hill
    P.place("World/tholos", at(*TEMPLE, 0.3), -20, 1.0, group="temple", force=True, collider="mesh", snap=1)
    yaw = along(0, 0, 19.5, 6.5)
    for k in range(4):
        x, z = -88 + k * 19.6, -26 + k * 6.55
        P.place("World/aqueduct", at(x, z, 1.2), yaw, 1.0, group="aqueduct", force=True, collider="mesh", snap=0)
    # the castle far off, in the fog
    P.place("World/castle_far", from_unity(30, -25, 430), 180, 1.0, group="far", force=True, collider="none", snap=0)


def game(rng):
    """The game's places, each where there is room for it; the space round each kept clear."""
    LV.marker("Start", at(*START, 0), 0, role="start")
    P.keep_clear(at(*START), 7)
    LV.marker("Sea", from_unity(0, -1.2, -40), 0, role="sea", size=500.0)
    LV.marker("Lake", from_unity(LAKE[0], -1.4, LAKE[1]), 0, role="water", size=48.0)
    for (x, z, yaw, bid) in ((4, -66, 0, "shore"), (-40, -16, 60, "ruins"), (4, 22, 0, "valley"), (32, -4, 80, "lake"),
                             (76, 47, -120, "poet"), (14, 84, 0, "bridge"), (22, 118, 0, "gate")):
        _, px, pz = nudge("World/beacon", x, z, -yaw, into="Game", name=f"Beacon_{bid}", role="beacon", id=bid, collider="mesh", snap=1)
        P.keep_clear(at(px, pz), 3.5)
    for (x, z, yaw, rid, crawl) in ((-50, -18, 70, "west", 0.2), (38, 16, -70, "east", 0.5), (8, 76, 180, "north", 0.5)):
        _, px, pz = nudge("World/rift", x, z, -yaw, 1.1, into="Game", name=f"Rift_{rid}", role="rift", id=rid, crawlers=crawl,
                          budget=8, max_alive=3, every=6.0, collider="mesh", snap=1)
        P.keep_clear(at(px, pz), 6)   # the demons come out in front of it
    for (x, z) in ((-3, -60), (5, -56)):
        LV.marker("Shade", at(x, z, 0), 180, role="first_shade")
        P.keep_clear(at(x, z), 7)   # the first meeting in the open
    # matter: rubble teaches stones, planks are organic, scrap teaches the crowbar and the knife
    sources = [("rubble", "mineral", (-4, -50), (-44, -4), (-56, 16), (12, 40), (-8, 60)),
               ("planks", "organic", (8, -46), (-20, -22), (24, 10), (-12, 46)),
               ("scrap", "metal", (-6, -36), (28, 24), (-30, 8), (48, 32), (2, 52), (8, 70))]
    for model, kind, *spots in sources:
        for (x, z) in spots:
            _, px, pz = nudge(f"World/{model}", x, z, -rng.uniform(0, 360), into="Game", name=model.capitalize(),
                              role="matter", kind=kind, amount=7 if model == "scrap" else 6, collider="mesh", snap=1)
            P.keep_clear(at(px, pz), 1.5)
    # the Guardian's ground: matter that comes back for the fight, the Guardian, the way out before the gate
    for (x, z, model, kind) in ((8, 128, "scrap", "metal"), (32, 128, "scrap", "metal"), (7, 142, "scrap", "metal"), (33, 142, "scrap", "metal"),
                                (20, 121, "rubble", "mineral"), (5, 135, "rubble", "mineral"), (35, 135, "rubble", "mineral")):
        nudge(f"World/{model}", x, z, -rng.uniform(0, 360), into="Game", name=model.capitalize(), role="matter", kind=kind,
              amount=5 if model == "scrap" else 4, regrow=22.0 if model == "scrap" else 25.0, collider="mesh", snap=1)
    LV.marker("Arena", at(*ARENA, 0), 0, role="arena")
    LV.marker("Guardian", at(ARENA[0], ARENA[1] + 3, 0), 180, role="guardian")
    P.keep_clear(at(*ARENA), 21)
    front = gate_front()
    P.place("World/rift", at(GATE[0], front - 2.5), 180, 1.1, group="castle", force=True, into="Game", name="Exit", role="exit",
            id="exit", leads_to="Wrath", delay=6.0, collider="mesh", snap=1)
    # the processions: pale souls walking the paths, slowly, forever
    for pid, pts in (("A", PATHS[0][1:8]), ("B", PATHS[1]), ("C", PATHS[3])):
        for k, (x, z) in enumerate(pts):
            LV.marker(f"Procession_{pid}{k}", at(x, z, 0), 0, role="procession", path=pid, order=k, count=6)


def scenery(rng):
    """What fills the land, laid out by purpose, each piece only where it has room."""
    def cypress(x, z, s):
        return P.place(f"World/cypress/Cypress{rng.randrange(3)}", at(x, z, 0.2), -rng.uniform(0, 360), s, group="tree", margin=0.1, collider="box", snap=1)

    # the graveyard by the shore: steles in rows facing the sea, some fallen out of line or gone
    for row in range(8):
        z = -68 + row * 4.2
        for col in range(-8, 9):
            if rng.random() < 0.3:
                continue
            x = col * 3.4 + rng.uniform(-0.5, 0.5)
            P.place(f"World/stele/Stele{rng.choice((0, 0, 1, 1, 2))}", at(x, z + rng.uniform(-0.4, 0.4), 0.1), 180 + rng.uniform(-10, 10), rng.uniform(0.9, 1.1),
                    group="stele", margin=0.5, collider="mesh", snap=1)
    # the dead tree by the shore, a stone at its foot
    P.place("World/deadtree", at(-14, -72), -40, 1.3, group="tree", collider="mesh", snap=1)
    # the avenue to the temple: columns flanking the path up the ruins' hill, many fallen or gone
    route = PATHS[1][2:]
    for a, b in zip(route, route[1:]):
        n = int(math.hypot(b[0] - a[0], b[1] - a[1]) / 5)
        dx, dz = (b[0] - a[0]) / max(1, n), (b[1] - a[1]) / max(1, n)
        nx, nz = -dz, dx
        ln = math.hypot(nx, nz) or 1
        for k in range(n):
            for side in (-1, 1):
                if rng.random() < 0.35:
                    continue
                x, z = a[0] + dx * k + side * nx / ln * 3.6, a[1] + dz * k + side * nz / ln * 3.6
                P.place("World/column", at(x, z, 0.1), -rng.uniform(0, 360), rng.uniform(0.85, 1.05), group="avenue", collider="mesh", snap=1)
    # the portico: a row of arches along the hill below the aqueduct, whole and fallen
    yaw = along(0, 0, 19.5, 6.5)
    for line in ((-82, -40), (-74, 14)):
        for k in range(5):
            x, z = line[0] + k * 8.6 * 0.95, line[1] + k * 8.6 * 0.32
            if rng.random() < 0.2:
                continue
            P.place(f"World/ruin_arch/Arch{k % 2}", at(x, z, 0.2), yaw, 1.0, group="arch", collider="mesh", snap=1)
    # the dead lake: broken arches standing in the shallows round it, open toward the water
    for t in (200, 235, 270, 305, 340, 20, 60):
        a = math.radians(t)
        x, z = LAKE[0] + math.cos(a) * 18.5, LAKE[1] + math.sin(a) * 18.5
        P.place(f"World/ruin_arch/Arch{rng.randrange(2)}", at(x, z, 0.6), along(0, 0, -math.sin(a), math.cos(a)), 1.0, group="shore", collider="mesh", snap=1)
    # cypress groves in the valley, single cypresses along the paths
    for gx, gz in ((-22, 8), (30, -20), (-36, 34), (38, 52), (-16, 66), (54, 74), (-50, 60), (-32, -50), (30, -50), (-60, 36), (60, -30)):
        for _ in range(rng.randint(7, 12)):
            cypress(gx + rng.gauss(0, 6), gz + rng.gauss(0, 6), rng.uniform(0.8, 1.25))
    for _ in range(140):
        x, z = rng.uniform(-75, 75), rng.uniform(-60, 92)
        if path_dist(x, z) < 10:
            cypress(x, z, rng.uniform(0.7, 1.1))
    # dead trees, here and there
    for _ in range(40):
        P.place("World/deadtree", at(rng.uniform(-80, 80), rng.uniform(-70, 92)), -rng.uniform(0, 360), rng.uniform(0.8, 1.2), group="tree", collider="mesh", snap=1)
    # crags: heaped on the rims, round the poet's cliff (the ramp kept open), down in the ravine, a few boulders
    for _ in range(120):
        side = rng.choice((-1, 1))
        P.place(f"World/crag/Crag{rng.choice((1, 2, 3, 3))}", at(side * rng.uniform(80, 106), rng.uniform(-90, 190), 0.6),
                -rng.uniform(0, 360), rng.uniform(0.8, 1.6), group="crag", collider="mesh", snap=1)
    for _ in range(30):
        P.place(f"World/crag/Crag{rng.choice((1, 2, 3))}", at(rng.uniform(-105, -62), rng.uniform(-90, -42), 0.6),
                -rng.uniform(0, 360), rng.uniform(0.8, 1.5), group="crag", collider="mesh", snap=1)
    for a in range(12):
        x, z = CLIFF[0] + math.cos(a * 0.52) * 13, CLIFF[1] + math.sin(a * 0.52) * 13
        if seg_dist(x, z, *RAMP)[0] > 7:
            P.place(f"World/crag/Crag{rng.choice((2, 3))}", at(x, z, 1.5), -rng.uniform(0, 360), rng.uniform(1.0, 1.4), group="crag", collider="mesh", snap=1)
    for _ in range(60):
        P.place(f"World/crag/Crag{rng.choice((0, 1, 2))}", at(rng.uniform(-105, 105), RAVINE_Z + rng.uniform(-7, 7), 0.5),
                -rng.uniform(0, 360), rng.uniform(0.8, 1.4), group="ravine", collider="mesh", snap=1)
    for _ in range(50):
        P.place("World/crag/Crag0", at(rng.uniform(-70, 70), rng.uniform(-60, 92), 0.3), -rng.uniform(0, 360), rng.uniform(0.6, 1.2),
                group="boulder", collider="mesh", snap=1)


def sun():
    # a low, cold, overcast light from the south-east: long shadows across the valley
    ld = bpy.data.lights.new("Sun", "SUN")
    ld.color = (0.78, 0.83, 0.88)
    ld.energy = 0.75 * LV.SUN
    ld.angle = math.radians(6)
    ob = bpy.data.objects.new("Sun", ld)
    u = Vector((-0.55, -0.42, 0.72)).normalized()   # where the light runs, in game axes
    ob.rotation_euler = Vector((-u.x, -u.z, u.y)).to_track_quat("-Z", "Y").to_euler()
    ob.location = (0, 0, 60)
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
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.32, 0.34, 0.35, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
    sc.world = w
    rng = random.Random(NAME)
    col, nrm = ash_images()
    ground = LV.ground_mesh(AREA, STEP, height, TILE, LV.tiled_material("Ash", col, nrm, 0.95))
    paint(ground)
    structures(rng)
    game(rng)
    scenery(rng)
    sun()
    LV.exclude_assets()
    LV.embed_panel()
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print("WROTE", path)


if __name__ == "__main__":
    main()
