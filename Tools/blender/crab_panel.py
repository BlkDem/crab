"""
The Crab tab in the 3D view's sidebar (N) of a level's .blend: export the level to the game in one
click. Kept inside each level's .blend as a text block that registers itself when the file opens
(Blender asks to allow the file's scripts: allow them, or run this text once by hand). The code
lives in Tools/blender (found next to the .blend: Levels/../Tools/blender).
"""
import os
import sys
import bpy


def tools():
    return os.path.normpath(os.path.join(os.path.dirname(bpy.data.filepath), "..", "Tools", "blender"))


class CRAB_OT_export(bpy.types.Operator):
    bl_idname = "crab.export_level"
    bl_label = "Экспорт в игру"
    bl_description = "Запечь тени земли и выгрузить уровень в Assets/Crab/Models/Levels (потом в Unity: Crab → Build Scene)"

    def execute(self, context):
        if tools() not in sys.path:
            sys.path.insert(0, tools())
        import importlib
        import level
        import export_level
        importlib.reload(level)
        importlib.reload(export_level)
        name = os.path.splitext(os.path.basename(bpy.data.filepath))[0]
        out = export_level.export(name)
        self.report({"INFO"}, f"Уровень выгружен: {out}. Теперь в Unity: Crab → Build Scene → {name.capitalize()}")
        return {"FINISHED"}


class CRAB_PT_level(bpy.types.Panel):
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Crab"
    bl_label = "Уровень"

    def draw(self, context):
        col = self.layout.column()
        col.operator("crab.export_level", icon="EXPORT")
        ob = context.object
        if ob is not None:
            box = col.box()
            box.label(text=ob.name)
            for k in ("role", "asset", "collider", "snap"):
                if k in ob.keys():
                    box.prop(ob, f'["{k}"]', text=k)


def register():
    for c in (CRAB_OT_export, CRAB_PT_level):
        try:
            bpy.utils.register_class(c)
        except ValueError:   # already there (the text run twice)
            bpy.utils.unregister_class(c)
            bpy.utils.register_class(c)


register()
