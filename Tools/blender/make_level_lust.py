"""
The second circle, Lust, laid out once into Levels/lust.blend (after Dante, canto V: the souls of
the lustful whirled for ever by the infernal hurricane, "la bufera infernal, che mai non resta").
From then on the .blend is the source: edit it by hand and export it with export_level.py. Won't
overwrite an existing .blend (pass --force to start over).

Run headless:
  blender -b -P Tools/blender/make_level_lust.py [-- --force]

The wind blows north (the Gale's prevailing 0), and so does the way, south to north (game z):
  1 the gate     — a narrow ledge along a cliff wall over the abyss; across it, on his own rock,
                   Minos the judge, the souls walking to him in a line;
  2 the ruina    — the broken plateau where the storm begins: wind-carved rocks to shelter behind,
                   its north edge torn off into the abyss;
  3 the valley   — the islands adrift in the storm: gust over to them, rise on the updrafts to the
                   high ones — Semiramis' tower (her ziggurat, a page) and Dido's island (her
                   Carthaginian hall, a page), gliding back down to the way;
  4 the city     — two islands of ruins, broken arch bridges between them; the lovers' statue, Paolo
                   and Francesca (the last page);
  5 the throne   — up an updraft onto the great island where Minos' throne stands; the way out.
The land of zones 1–2 is a heightfield (Ground); every island is a placed model with `land` = 1 (its
origin on its walkable top: import_creatures.on_top) — the game makes them land too. Going up is only
by an updraft; every other hop is checked here against what the crab can jump (rays at the real
models), so an edit of the layout that breaks the way fails loudly.
"""
import math
import os
import random
import sys
import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib as L  # noqa: E402
import level as LV  # noqa: E402
from level import from_unity  # noqa: E402

NAME = "lust"
AREA = (-50, -118, 100, 116)   # the heightfield (zones 1–2): game x, z, width, depth
STEP = 1.0
TILE = 0.2
ABYSS = -16                    # below this the crab has fallen (Abyss)
SHEER = 14                     # how steeply the land's edges fall (m down per m out)

# what the crab can jump (CrabController: run 5.6, jump 5.2, gravity 18, glide -3 with jump held;
# a gust carries some 4 m further): a hop's longest stretch with nothing underfoot, by its drop
RUN, GLIDE = 5.6, 3.0


def reach(drop, gust=False):
    flat = RUN * (0.46 + max(0.0, drop) / GLIDE) * 0.85   # rise and fall to the same height, then gliding down
    return flat + (4.0 if gust else 0.0)


# ----------------------------------------------------------------------------- the islands
# kind: asset, walkable half sizes (along its own X, along its own Y; Blender) at scale 1
KINDS = {"large": ("World/lust_island_large", 10.5, 10.5, True),
         "medium": ("World/lust_island_medium", 8.3, 2.6, False),
         "islet": ("World/lust_islet", 2.0, 2.0, False),
         "ledge": ("World/lust_ledge", 4.5, 3.4, False)}


class Isle:
    def __init__(self, name, kind, x, top, z, yaw=0.0, scale=1.0):
        self.name, self.kind, self.x, self.top, self.z, self.yaw, self.scale = name, kind, x, top, z, yaw, scale
        _, hx, hy, self.round = KINDS[kind]
        self.hx, self.hy = hx * scale, hy * scale

    @property
    def c(self):
        return (self.x, self.z)

    def has(self, x, z, margin=0.0):
        """(game x, z) on its walkable top, `margin` in from the edge."""
        if self.round:
            return math.hypot(x - self.x, z - self.z) < self.hx - margin
        # its own axes: Blender X is game -x turned by yaw
        a = math.radians(self.yaw)
        bx, by = -(x - self.x), -(z - self.z)
        u = bx * math.cos(a) + by * math.sin(a)
        v = -bx * math.sin(a) + by * math.cos(a)
        return abs(u) < self.hx - margin and abs(v) < self.hy - margin

    def at(self, dx=0.0, dz=0.0, up=0.0):
        return from_unity(self.x + dx, self.top + up, self.z + dz)


ISLES = [
    Isle("IsletA", "islet", 0, -1, 3.6),                     # the first gust over from the ruina
    Isle("ValleyIsland", "large", 2, -2, 18, 30),
    Isle("TowerIsland", "large", 26, 9, 23, 140, 0.7),       # Semiramis: up the first updraft
    Isle("FallenTerrace", "ledge", 14, 3, 36),               # her terrace, fallen: the way back down
    Isle("LongIsland", "medium", 0, -3, 40, 90),
    Isle("IsletB", "islet", 0, -4, 54, 15),
    Isle("AbyssIsland", "large", 0, -5.5, 73, 200),          # over a gap only a gust carries
    Isle("IsletC", "islet", 19.5, 4, 80, 40),                # up the second updraft
    Isle("DidoIsland", "medium", 21, 2, 94.5, 90),
    Isle("IsletD", "islet", 12.5, -1.5, 106.5, 70),              # from Dido's island back down
    Isle("CityIsland", "large", 0, -5.5, 119, 75),
]
BY = {i.name: i for i in ISLES}

# the bridges (World/lust_bridge: a humped arch along its own Y, 33 m; the deck's ends ±14 m out
# at 3.1 m over its origin, its middle line at its own x = -2.2)
BRIDGE_END, BRIDGE_DECK_UP, BRIDGE_DECK_X = 14.0, 3.1, -2.2


def bridge_across(a, toward, lean):
    """A bridge from island a's edge toward game direction (degrees from north, `toward`); the island
    on its far end (same height, same kind) is made and returned: the deck's ends 1.5 m into both."""
    d = (math.sin(math.radians(toward)), math.cos(math.radians(toward)))
    inside = a.hx - 1.5
    mid = (a.x + d[0] * (inside + BRIDGE_END), a.z + d[1] * (inside + BRIDGE_END))
    far = Isle(f"{a.name}_far", "large", mid[0] + d[0] * (BRIDGE_END + inside), a.top, mid[1] + d[1] * (BRIDGE_END + inside), lean)
    return mid, d, far


B1_MID, B1_DIR, C2 = None, None, None   # laid out in islands()
CITY2 = None
ARENA = None


# ----------------------------------------------------------------------------- the land of zones 1–2
JUDGE_ROCK = (-24, -78)
START = (0, -104)
WALL_FOOT = 8.2      # east of the ledge (game x): where the wall has its full height, things heaped on it begin
RUINA_SOUTH = -54    # south of the ruina (game z): where they must end


def perlin(a, b, s=0.37):
    return noise.noise(Vector((a, b, s)))


def smooth(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def ground_out(x, z):
    """How far (m) outside the walkable land of zones 1–2 (negative inside)."""
    path = max(abs(x - 0.6 * math.sin(z * 0.07)) - 3.6, -114 - z, z + 50)
    start = math.hypot(x - START[0], z - START[1]) - 7
    ragged = 0.0 if abs(x) < 8 else 2.5 * (0.5 + 0.5 * perlin(x * 0.12, 3.3))
    plateau = max(abs(x) - 27 - 2.5 * perlin(z * 0.08, 7.1), -53 - z, z + 4.5 + ragged)
    return min(path, start, plateau)


def height(x, z):
    top = 0.18 * perlin(x * 0.2, z * 0.2) + 0.1 * perlin(x * 0.7, z * 0.7, 2.2)
    if abs(x) < 7 and z > -14:   # flat where the first gust is jumped from
        top *= 0.3
    d = ground_out(x, z)
    h = top if d <= 0 else top - d * SHEER
    # Minos' rock across the abyss from the ledge
    dj = math.hypot(x - JUDGE_ROCK[0], z - JUDGE_ROCK[1]) - 8
    h = max(h, 1.5 + 0.2 * perlin(x * 0.3, z * 0.3) - max(0.0, dj) * SHEER)
    # the cliff wall east of the ledge, rising high, its face broken
    wall = smooth(4.4, 7.5, x - 0.6 * math.sin(z * 0.07)) * smooth(-44, -56, z)
    h += (15 + 5 * perlin(x * 0.08, z * 0.08, 5.5) + 2 * perlin(x * 0.4, z * 0.4, 1.1) - h) * wall
    return max(h, -40.0)


def on_ground(x, z, margin=0.0):
    return ground_out(x, z) < -margin and height(x, z) < 1.0


def land_at(x, z, margin=0.0):
    """The walkable top under (game x, z): its height, or None (the abyss)."""
    for i in all_isles():
        if i.has(x, z, margin):
            return i.top
    if on_ground(x, z, margin):
        return height(x, z)
    return None


def all_isles():
    return ISLES + [x for x in (CITY2, ARENA) if x]


def at(x, z, up=0.0):
    h = land_at(x, z)
    return from_unity(x, (h if h is not None else 0) + up, z)


# ----------------------------------------------------------------------------- looks
def slate_images(n=1024):
    """The ruina's stone: black slate with violet veins and grit, tiling."""
    rng = np.random.default_rng(22)
    hgt = sum(LV.periodic_noise(n, c, rng) * w for c, w in ((4, 0.5), (16, 0.35), (64, 0.2), (256, 0.12)))
    veins = np.abs(LV.periodic_noise(n, 24, rng) - 0.5)
    veins = np.clip(1 - veins * 26, 0, 1) ** 3
    grit = rng.random((n, n)) ** 10
    tone = (hgt - hgt.mean()) / (hgt.std() + 1e-6)
    stone = np.array([0.15, 0.14, 0.17])
    vein = np.array([0.42, 0.28, 0.58])
    col = stone[None, None, :] * (1 + 0.12 * tone[..., None])
    col = col * (1 - 0.5 * veins[..., None]) + vein[None, None, :] * 0.5 * veins[..., None]
    col -= 0.06 * grit[..., None]
    col = np.clip(col, 0, 1)
    return LV.image("slate_color", col, "sRGB"), LV.image("slate_normal", LV.normal_map(hgt - veins * 0.2, 6), "Non-Color")


def paint(ground):
    """The ledge's path worn lighter; the plateau darker toward its torn edge."""
    me = ground.data
    attr = me.color_attributes.new("Paint", "BYTE_COLOR", "POINT")
    for v, c in zip(me.vertices, attr.data):
        x, z = -v.co.x, -v.co.y
        worn = smooth(2.5, 0.5, abs(x - 0.6 * math.sin(z * 0.07))) * (1 if z < -50 else smooth(-30, -50, z))
        edge = smooth(-16, -6, z) * 0.15
        k = 0.85 + 0.15 * worn - edge
        c.color = (k * (1 + 0.05 * worn), k, k * (1 + 0.04 * worn), 1)


# ----------------------------------------------------------------------------- laying out
TREE = None   # the land as it really is (the models' own tops have dips and bumps), once the islands stand


def real_top(x, z):
    hit = TREE.ray_cast(from_unity(x, 40, z), Vector((0, 0, -1)), 80)[0] if TREE else None
    return hit.z if hit else None


def flat(pts, dip=0.4, bump=1.2):
    """The real land under every point, level with the walkable top there: no hole, no dip deeper than
    `dip` (a thing set down on the land sinks into it), no bump higher than `bump` (it pokes through)."""
    for x, z in pts:
        top = land_at(x, z)
        y = real_top(x, z)
        if top is None or y is None or top - y > dip or y - top > bump:
            return False
    return True


def clear(pts, group):
    """Where a thing may stand: every point of its footprint on a walkable top and the real land under
    it level (far things anywhere)."""
    if group in ("far", "land"):
        return True
    margin = 0.4 if group in ("rock", "spot") else 0.8
    if not all(land_at(x, z, margin) is not None for x, z in pts):
        return False
    # the models' tops have cracks: the whole footprint, every half metre (pts: its outline and middle)
    x0, x1 = min(x for x, _ in pts), max(x for x, _ in pts)
    z0, z1 = min(z for _, z in pts), max(z for _, z in pts)
    grid = [(x0 + i * 0.5, z0 + j * 0.5) for i in range(int((x1 - x0) / 0.5) + 1) for j in range(int((z1 - z0) / 0.5) + 1)]
    return flat(pts + grid, 0.8 if group == "rock" else 0.6)   # rocks may sit deeper


P = LV.Placer(clear, merge=("land", "far"))


def place_near(path, x, z, yaw=0.0, scale=1.0, keep=0.0, **props):
    """A game place as close to (game x, z) as there is room and level land for it (out to 7.5 m)."""
    for r in (0, 1.0, 2.0, 3.0, 4.5, 6.0, 7.5):
        for a in range(0, 360, 30 if r else 360):
            px, pz = x + r * math.cos(math.radians(a)), z + r * math.sin(math.radians(a))
            ob = P.place(path, at(px, pz), yaw, scale, group="spot", margin=0.3, into="Game", **props)
            if ob:
                if keep:
                    P.keep_clear(at(px, pz), keep)
                return ob
    raise SystemExit(f"no room for {props.get('name', path)} near {x:.1f}, {z:.1f}")


def free_spot(x, z, r=1.0):
    """Level land round (game x, z), out of the places kept clear: where a marker may go."""
    for d in (0, 0.8, 1.6, 2.4, 3.2, 4.0):
        for a in range(0, 360, 30 if d else 360):
            px, pz = x + d * math.cos(math.radians(a)), z + d * math.sin(math.radians(a))
            ring = [(px, pz)] + [(px + r * math.cos(b), pz + r * math.sin(b)) for b in (0, 1.57, 3.14, 4.71)]
            if flat(ring, 0.3, 0.3) and all(land_at(qx, qz, 0.5) is not None for qx, qz in ring):
                return px, pz
    raise SystemExit(f"no level land near {x:.1f}, {z:.1f}")


def islands(rng):
    global B1_MID, B1_DIR, C2, CITY2, ARENA
    # the city: an arch bridge north from the abyss island to the first city island, a second one
    # north-east to the second; the throne's island beyond that, up an updraft
    b1, d1, _ = bridge_across(BY["AbyssIsland"], 0, 0)
    c1 = BY["CityIsland"]
    assert abs(b1[1] + BRIDGE_END + c1.hx - 1.5 - c1.z) < 0.5, "the first bridge doesn't reach the city island"
    b2, d2, CITY2 = bridge_across(c1, 22, 160)
    CITY2.name = "LoversIsland"
    up = (CITY2.x, CITY2.z + CITY2.hx + 2.6)
    ARENA = Isle("ThroneIsland", "large", up[0], CITY2.top + 9.5, up[1] + 2.6 + 10.5 * 1.4, 10, 1.4)
    B1_MID, B1_DIR, C2 = b1, d1, b2
    for i in all_isles():
        asset = KINDS[i.kind][0]
        # the land itself isn't in the Placer's way: things stand on it
        LV.instance(asset, from_unity(i.x, i.top, i.z), i.yaw, i.scale, name=i.name, land=1, snap=0, collider="mesh")
    for k, (mid, d, top) in enumerate(((b1, d1, BY["AbyssIsland"].top), (b2, d2, c1.top))):
        a = math.atan2(d[0], d[1])   # its own -Y (game +z at yaw 0) along d: the yaw (Blender) is -a
        yaw = -math.degrees(a)
        off = Matrix.Rotation(math.radians(yaw), 3, "Z") @ Vector((BRIDGE_DECK_X, 0, 0))
        where = from_unity(mid[0], top - BRIDGE_DECK_UP, mid[1]) - off
        LV.instance("World/lust_bridge", where, yaw, 1.0, name=f"Bridge{k + 1}", snap=0, collider="mesh")
        for end in (-1, 1):   # the way onto it kept clear
            P.keep_clear(from_unity(mid[0] + end * d[0] * (BRIDGE_END + 1), 0, mid[1] + end * d[1] * (BRIDGE_END + 1)), 3.5)
    return up


def updraft(name, x, z, base, top, r=2.4):
    LV.marker(name, from_unity(x, base, z), 0, role="updraft", radius=r, height=float(top - base))
    P.keep_clear(from_unity(x, 0, z), r + 1.0)


def structures(up):
    tower, dido, c1, c2, arena = BY["TowerIsland"], BY["DidoIsland"], BY["CityIsland"], CITY2, ARENA
    # Minos on his rock, looking east over the ledge the souls come along
    P.place("Enemies/minos_judge", from_unity(JUDGE_ROCK[0] - 1, height(*JUDGE_ROCK), JUDGE_ROCK[1]), -90, 1.0, group="far", force=True,
            into="Game", name="MinosJudge", role="judge", collider="mesh", snap=1)
    # Semiramis' ziggurat on her tower island, Dido's hall at the north end of hers
    P.place("World/lust_ziggurat", tower.at(2.5, 2.0), 160, 0.45, group="ruin", force=True, collider="mesh", snap=1)
    P.place("World/lust_palace_hall", dido.at(0, 5.5), 0, 0.9, group="ruin", force=True, collider="mesh", snap=1)
    # the city: a broken spire on each island, the toppled lovers on the second
    P.place("World/lust_spire", c1.at(-6, 3), 25, 0.8, group="ruin", force=True, collider="mesh", snap=1)
    P.place("World/lust_spire", c2.at(6, 4), -40, 0.65, group="ruin", force=True, collider="mesh", snap=1)
    P.place("World/lust_lovers_statue", c2.at(-3.5, 2.5), 35, 1.0, group="ruin", force=True, collider="mesh", snap=1)
    P.place("World/paolo_francesca", c2.at(-1, -1, 0.6), 200, 1.0, group="far", force=True, collider="none", snap=0)
    # the throne at the far side of the great island, facing the way up
    P.place("World/lust_throne", arena.at(0, 10), 180, 1.0, group="ruin", force=True, collider="mesh", snap=1)


def game(rng, up):
    tower, dido, c1, c2, arena = BY["TowerIsland"], BY["DidoIsland"], BY["CityIsland"], CITY2, ARENA
    valley, abyss_i = BY["ValleyIsland"], BY["AbyssIsland"]
    LV.marker("Start", at(*START), 0, role="start")
    P.keep_clear(at(*START), 3)
    LV.marker("Gale", from_unity(0, 10, 0), 0, role="gale", prevailing=0.0)
    LV.marker("Abyss", from_unity(0, ABYSS, 0), 0, role="abyss", depth=float(ABYSS))
    # the updrafts: by the valley island up to Semiramis' tower; by the abyss island up to Dido's
    # islet; at the second city island's north edge up to the throne
    updraft("UpdraftTower", valley.x + valley.hx + 2.7, 22, ABYSS + 1, tower.top + 4)
    updraft("UpdraftDido", abyss_i.x + abyss_i.hx + 2.7, 77, ABYSS + 1, BY["IsletC"].top + 4)
    updraft("UpdraftThrone", up[0], up[1], ABYSS + 1, arena.top + 4.5)
    # the beacons, one to each stretch of the way
    for bid, (x, z), yaw in (("gate", (-3.4, -107), 30), ("ruina", (8, -22), 20), ("valley", (valley.x + 4, valley.z - 5), 0),
                             ("abyss", (abyss_i.x - 4, abyss_i.z - 3), 0), ("city", (c1.x + 4, c1.z - 4), 0),
                             ("throne", (c2.x - 7, c2.z - 3), 0)):
        place_near("World/beacon", x, z, -yaw, keep=3.0, name=f"Beacon_{bid}", role="beacon", id="lust_" + bid, collider="mesh", snap=1)
    # the three pages of Minos' book: Semiramis, Dido, Francesca
    for pid, isle, dx, dz in (("semiramis", tower, -3.5, -2.5), ("dido", dido, 0, -2.5), ("francesca", c2, 1.5, -3)):
        px, pz = free_spot(isle.x + dx, isle.z + dz)
        LV.marker(f"Page_{pid}", from_unity(px, isle.top + 1.2, pz), 0, role="page", id=pid)
        P.keep_clear(from_unity(px, 0, pz), 2.5)
    # the way out before the throne
    place_near("World/rift", arena.x, arena.z + 3, 180, 1.2, name="Exit", role="exit", id="exit", leads_to="Wrath", delay=6.0,
               collider="mesh", snap=1)
    LV.marker("Arena", arena.at(), 0, role="arena")
    P.keep_clear(arena.at(0, -2), 9)
    # matter: rubble on the ruina, scrap on the islands
    for model, kind, (x, z) in (("rubble", "mineral", (-12, -32)), ("rubble", "mineral", (15, -42)), ("scrap", "metal", (valley.x - 5, valley.z + 3)),
                                ("rubble", "mineral", (c1.x - 6, c1.z - 4)), ("scrap", "metal", (c2.x + 4, c2.z - 5))):
        place_near(f"World/{model}", x, z, -rng.uniform(0, 360), keep=1.5, name=model.capitalize(), role="matter", kind=kind, amount=6,
                   collider="mesh", snap=1)
    # the souls walking the ledge to their judge, slowly, for ever
    for k, z in enumerate(range(-101, -59, 6)):
        LV.marker(f"Procession_A{k}", at(-1.2 + 0.6 * math.sin(z * 0.07), z), 0, role="procession", path="A", order=k, count=8)
    # the way's ends kept clear: the jumps' run-ups and landings, the bridges' ends
    for x, z in ((0, -7.5), (0, 7), (0, 30), (0, 34), (0, 46), (0, 52), (0, 57), (0, 65), (19.5, 82), (21, 88), (20, 101), (12.5, 106.5)):
        P.keep_clear(from_unity(x, 0, z), 2.0)
    # the way through the circle, point by point, past all three pages (the task's mark follows it);
    # kind: how a point is reached from the one before
    way = [("walk", 0, -5.5, None), ("gust", 0, 3.6, None), ("calm", 1, 10, None),
           ("updraft", valley.x + valley.hx + 2.7, 22, valley.top), ("ride", 22, 27, None), ("down", 14, 34, None), ("down", 1, 38, None),
           ("walk", 0, 47, None), ("calm", 0, 54, None), ("gust", 0, 65, None),
           ("updraft", abyss_i.x + abyss_i.hx + 2.7, 77, abyss_i.top), ("ride", 19.5, 81.5, None), ("calm", 20.5, 88, None), ("walk", 20, 102, None),
           ("down", 12.5, 106.5, None), ("down", 3, 114, None), ("walk", c2.x, c2.z, c2.top),
           ("updraft", up[0], up[1], c2.top), ("ride", arena.x, arena.z, arena.top)]
    for k, (kind, x, z, top) in enumerate(way):
        y = land_at(x, z) if top is None else top
        assert y is not None, f"the way's point {k} over the abyss: {x}, {z}"
        LV.marker(f"Way{k:02d}", from_unity(x, y + 1, z), 0, role="route", seq=k, kind=kind)


def scenery(rng):
    """Shelter from the wind (the crab hides on a rock's north side), crags on the edges, far islands."""
    def rock(x, z, s=None):
        return P.place("World/lust_wind_rock", at(x, z, -0.1), -rng.uniform(0, 360), s or rng.uniform(0.9, 1.4), group="rock", margin=0.6,
                       collider="mesh", snap=1)

    def crag(x, z, s):
        return P.place(f"World/crag/Crag{rng.choice((0, 1, 2))}", at(x, z, -0.3), -rng.uniform(0, 360), s, group="rock", margin=0.6,
                       collider="mesh", snap=1)
    # the ruina: a scatter of wind-carved rocks, heavier toward its torn edge where the gusts bite
    n = 0
    for _ in range(400):
        if n >= 26:
            break
        x, z = rng.uniform(-24, 24), rng.uniform(-50, -12)
        if abs(x) < 3 and z > -18:
            continue
        n += rock(x, z) is not None
    for _ in range(80):
        crag(rng.uniform(-27, 27), rng.uniform(-50, -10), rng.uniform(0.7, 1.3))
    # a few on each island to hide behind
    for isle, count in (("ValleyIsland", 4), ("AbyssIsland", 4), ("CityIsland", 3), ("LoversIsland", 3), ("ThroneIsland", 6), ("LongIsland", 1)):
        i = CITY2 if isle == "LoversIsland" else ARENA if isle == "ThroneIsland" else BY[isle]
        placed = 0
        for _ in range(200):
            if placed >= count:
                break
            a, r = rng.uniform(0, math.tau), rng.uniform(0.2, 0.85) * i.hx
            x, z = i.x + r * math.cos(a), i.z + r * math.sin(a)
            placed += rock(x, z) is not None
    # crags heaped on the cliff wall, behind the ledge: the biggest are 50 m across, so each is pushed
    # back by its real footprint, clear of the ledge (they'd roof it over and bar it) and of the ruina
    for _ in range(18):
        x, z = rng.uniform(9, 16), rng.uniform(-112, -56)
        path, yaw, s = f"World/crag/Crag{rng.choice((1, 2, 3))}", -rng.uniform(0, 360), rng.uniform(0.8, 1.2)
        box = P.rect(path, from_unity(0, 0, 0), yaw, s, 0)
        x = max(x, WALL_FOOT - min(-bx for bx, _ in box))
        z = min(z, RUINA_SOUTH - max(-by for _, by in box))
        # set by the lowest of the wall's top under it: its feet mustn't hang over the broken top
        base = min(height(-bx, -by) for bx, by in LV.samples(P.rect(path, from_unity(x, 0, z), yaw, s, 0)))
        P.place(path, from_unity(x, base - 0.8, z), yaw, s, group="far", force=True, collider="mesh", snap=0)
    # far islands adrift all round, ruins on some, none in reach
    far = [((-60, -30), -8, 1.3, "spire"), ((70, -10), 6, 1.1, "palace"), ((-75, 40), 14, 1.2, ""), ((65, 50), -12, 1.0, "ziggurat"),
           ((-55, 95), 2, 1.4, "spire"), ((80, 120), 10, 1.2, ""), ((-70, 160), -6, 1.1, "palace"), ((55, 190), 18, 1.3, "spire"),
           ((-30, 230), 24, 1.6, ""), ((30, -80), -20, 0.9, ""), ((-45, -95), 10, 0.8, ""), ((0, 270), 30, 1.8, "spire")]
    for k, ((x, z), y, s, ruin) in enumerate(far):
        kind = "World/lust_island_large" if k % 3 else "World/lust_island_medium"
        P.place(kind, from_unity(x, y, z), rng.uniform(0, 360), s, group="far", force=True, name=f"far_island{k}", snap=0, collider="none")
        if ruin:
            path = {"spire": "World/lust_spire", "palace": "World/lust_palace_hall", "ziggurat": "World/lust_ziggurat"}[ruin]
            P.place(path, from_unity(x + 1, y - 0.2, z), rng.uniform(0, 360), s * (0.6 if ruin == "ziggurat" else 1.0), group="far", force=True,
                    name=f"far_{ruin}{k}", snap=0, collider="none")
    for k in range(18):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(45, 110)
        P.place("World/lust_islet", from_unity(r * math.cos(a), rng.uniform(-14, 22), 60 + r * math.sin(a) * 1.4), rng.uniform(0, 360),
                rng.uniform(0.8, 2.2), group="far", force=True, name=f"far_islet{k}", snap=0, collider="none")


# ----------------------------------------------------------------------------- the way, checked
def land_tree():
    """The land the crab can stand on (the ground and every model with a collider), for rays."""
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    for inst in dg.object_instances:
        ob = inst.object
        if ob.type != "MESH":
            continue
        owner = inst.parent.original if inst.is_instance and inst.parent else ob.original
        if owner.get("collider", "mesh") == "none" or owner.name.startswith("far_"):
            continue
        if not inst.is_instance and any(c.name.startswith("asset:") for c in owner.users_collection):
            continue   # the assets themselves, at the origin
        me = ob.data.copy()
        me.transform(inst.matrix_world)
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    return BVHTree.FromBMesh(bm)


def check_way(tree, up):
    """Every hop of the way: its longest stretch with nothing underfoot within what the crab jumps."""
    def g(x, z):
        return (x, land_at(x, z), z)
    v, t, ld, li, dd, ab, cc = (BY[n] for n in ("ValleyIsland", "TowerIsland", "LongIsland", "IsletB", "DidoIsland", "AbyssIsland", "CityIsland"))
    hops = [("gust", g(0, -5.5), g(0, 3.6)), ("calm", g(0, 4.5), g(1, 10)), ("calm", g(0, 26), g(0, 34)),
            ("down", g(22, 27), g(14, 34)), ("down", g(10, 36), g(1, 38)),
            ("calm", g(0, 47), g(0, 54)), ("gust", g(0, 55.5), g(0, 65)),
            ("calm", g(19.5, 81.5), g(20.5, 88)), ("down", g(20, 102), g(12.5, 106.5)), ("down", g(12, 108), g(3, 114)),
            ("walk", g(0, 81), g(0, 114)), ("walk", g(cc.x + 7.5 * math.sin(math.radians(22)), cc.z + 7.5 * math.cos(math.radians(22))),
                                                   g(CITY2.x - 7.5 * math.sin(math.radians(22)), CITY2.z - 7.5 * math.cos(math.radians(22))))]
    bad = []
    for kind, a, b in hops:
        assert a[1] is not None and b[1] is not None, f"a hop from or to the abyss: {a} → {b}"
        lo, hi = min(a[1], b[1]), max(a[1], b[1])
        n = int(math.hypot(b[0] - a[0], b[2] - a[2]) / 0.25)
        gap = run = 0.0
        for k in range(n + 1):
            f = k / n
            x, z = a[0] + (b[0] - a[0]) * f, a[2] + (b[2] - a[2]) * f
            hit = tree.ray_cast(from_unity(x, hi + 14, z), Vector((0, 0, -1)), 40)[0]
            y = hit.z if hit else -1e9
            ok = lo - 1.5 < y < hi + (11 if kind == "walk" else 1.6)
            run = 0.0 if ok else run + 0.25
            gap = max(gap, run)
        drop = a[1] - b[1]
        limit = 0.8 if kind == "walk" else reach(drop, gust=kind == "gust")
        if kind != "walk" and b[1] > a[1] + 1.4:
            bad.append(f"{kind} {a}→{b}: {b[1] - a[1]:.1f} m up, no updraft")
        print(f"HOP {kind:5s} {a[0]:6.1f},{a[2]:6.1f} → {b[0]:6.1f},{b[2]:6.1f}  gap {gap:4.1f} m  drop {drop:4.1f}  can {limit:4.1f}")
        if gap > limit:
            bad.append(f"{kind} {a}→{b}: gap {gap:.1f} m > {limit:.1f}")
    # the updrafts lift above what they lead to, and stand next to where they're jumped into from
    for ob in bpy.data.collections["Game"].objects:
        if ob.get("role") == "updraft":
            x, z = -ob.location.x, -ob.location.y
            top = ob.location.z + ob["height"]
            near = max((i.top for i in all_isles() if math.hypot(x - i.x, z - i.z) < i.hx * 1.0 + ob["radius"] + 9), default=-99)
            print(f"UPDRAFT {ob.name}: top {top:.1f}, highest island by it {near:.1f}")
            if top < near + 2:
                bad.append(f"{ob.name} doesn't lift above {near}")
    if bad:
        raise SystemExit("THE WAY IS BROKEN:\n  " + "\n  ".join(bad))
    print("THE WAY HOLDS")


def sun():
    # a cold violet light high from the south, through the storm's murk
    ld = bpy.data.lights.new("Sun", "SUN")
    ld.color = (0.8, 0.65, 1.0)
    ld.energy = 1.3 * LV.SUN
    ld.angle = math.radians(8)
    ob = bpy.data.objects.new("Sun", ld)
    u = Vector((0.296, -0.5, -0.814)).normalized()   # where the light runs, in game axes
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
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.2, 0.13, 0.28, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    sc.world = w
    rng = random.Random(NAME)
    col, nrm = slate_images()
    ground = LV.ground_mesh(AREA, STEP, height, TILE, LV.tiled_material("Slate", col, nrm, 0.85))
    paint(ground)
    global TREE
    up = islands(rng)
    TREE = land_tree()   # the islands and the ground as they really are, for what stands on them
    structures(up)
    game(rng, up)
    scenery(rng)
    check_way(land_tree(), up)
    sun()
    LV.exclude_assets()
    LV.embed_panel()
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print("WROTE", path)


if __name__ == "__main__":
    main()
