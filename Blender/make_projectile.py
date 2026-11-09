# Builds the projectile (a small low-poly energy orb) in Blender and exports it for the engine.
import bpy, os
OUT = os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.18, location=(0, 0, 0))
orb = bpy.context.active_object
orb.name = "Projectile.Orb"
# all UVs in the middle of the texture: the color comes from projectile.png (one plain color)
uv = orb.data.uv_layers.new(name="UVMap")
for loop_uv in uv.data:
    loop_uv.uv = (0.5, 0.5)
bpy.ops.wm.obj_export(filepath=os.path.join(OUT, "projectile.obj"), forward_axis="Z", up_axis="Y",
                      export_uv=True, export_normals=False, export_materials=False, export_triangulated_mesh=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "projectile.blend"))
