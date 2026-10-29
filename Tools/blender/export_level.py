"""
A level's .blend (Levels/<name>.blend, the source: edit it by hand) to what the game builds the
scene from (Assets/Crab/Models/Levels/<Name>/): ground.glb — the land — and layout.json — the
props, the game's places and the sun (level.py).

The land's vertex colours multiply its tiling colour: the shade of everything standing on it
(ambient occlusion, baked by Cycles), the land's own paint if it has one (the colour layer
"Paint": paths, scorch, damp — painted by the level maker, and by hand in Vertex Paint), broad
patches that break the tiling up, and the darker wet band along the water (below the level of
each `sea` or `water` marker + WET, within its `size`).

Run headless:
  blender -b -P Tools/blender/export_level.py -- <name>        (e.g. prologue)
or in the open .blend: the Crab tab in the 3D view's sidebar (crab_panel, kept in the file).
"""
import os
import sys
import bpy
from mathutils import Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level as LV  # noqa: E402

AO_DISTANCE = 2.5
AO_SAMPLES = 96
PATCH = 0.045          # the broad patches: their size (1/m) and how much they lighten or darken
PATCH_AMOUNT = 0.09
WET = 0.35             # how far above the sea the sand is wet (m)
WET_TINT = (0.70, 0.70, 0.74)


def bake_ao(ground):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = AO_SAMPLES
    sc.world.light_settings.distance = AO_DISTANCE
    me = ground.data
    attr = me.color_attributes.get("AO") or me.color_attributes.new("AO", "BYTE_COLOR", "POINT")
    me.color_attributes.active_color = attr
    bpy.ops.object.select_all(action="DESELECT")
    ground.select_set(True)
    bpy.context.view_layer.objects.active = ground
    bpy.ops.object.bake(type="AO", target="VERTEX_COLORS")
    return attr


def tint(ground, attr):
    """Over the shade: the paint, broad patches and the wet band by the water."""
    waters = [o for o in bpy.data.objects if o.get("role") in ("sea", "water")]
    paint = ground.data.color_attributes.get("Paint")
    mw = ground.matrix_world
    for i, (v, c) in enumerate(zip(ground.data.vertices, attr.data)):
        p = mw @ v.co
        k = 1 + PATCH_AMOUNT * noise.noise(Vector((p.x * PATCH, p.y * PATCH, 1.7)))
        k *= 1 + 0.03 * noise.noise(Vector((p.x * PATCH * 4, p.y * PATCH * 4, 5.1)))
        wet = 0.0
        for w in waters:
            half = w.get("size", 1e6) / 2
            if abs(p.x - w.location.x) < half and abs(p.y - w.location.y) < half:
                wet = max(wet, min(1.0, max(0.0, (w.location.z + WET - p.z) / WET)))
        pc = paint.data[i].color if paint else (1, 1, 1, 1)
        rgb = [c.color[j] * pc[j] * k * (1 - wet + wet * WET_TINT[j]) for j in range(3)]
        c.color = (min(1, rgb[0]), min(1, rgb[1]), min(1, rgb[2]), 1)


def export(name):
    """Export the open .blend as the level `name`."""
    out = os.path.join(LV.MODELS, "Levels", name.capitalize())
    os.makedirs(out, exist_ok=True)
    ground = bpy.data.objects["Ground"]
    tint(ground, bake_ao(ground))
    bpy.ops.object.select_all(action="DESELECT")
    ground.select_set(True)
    bpy.context.view_layer.objects.active = ground
    glb = os.path.join(out, "ground.glb")
    bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=True, export_yup=True,
                              export_apply=True, export_texcoords=True, export_normals=True,
                              export_materials="EXPORT", export_image_format="JPEG", export_jpeg_quality=90,
                              export_vertex_color="ACTIVE")
    print("WROTE", glb, os.path.getsize(glb))
    LV.write_layout(os.path.join(out, "layout.json"), bpy.context.scene, f"Levels/{name.capitalize()}/ground")
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    name = argv[0] if argv else "prologue"
    bpy.ops.wm.open_mainfile(filepath=os.path.join(LV.BLENDS, name + ".blend"))
    export(name)


if __name__ == "__main__":
    main()
