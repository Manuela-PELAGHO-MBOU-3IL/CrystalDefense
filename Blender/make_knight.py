# Builds a low-poly knight in Blender, saves knight.blend and exports knight.obj for the engine.
import bpy, bmesh, math, os, zlib, struct
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))
COLS, ROWS, CELL = 4, 2, 64
PALETTE = {  # name: (column, row from the top, color)
    "steel_light": (0, 0, (205, 210, 222)),
    "steel":       (1, 0, (150, 156, 168)),
    "steel_dark":  (2, 0, (80, 86, 98)),
    "blue":        (3, 0, (38, 72, 175)),
    "gold":        (0, 1, (232, 186, 60)),
    "leather":     (1, 1, (105, 66, 38)),
    "red":         (2, 1, (205, 40, 45)),
    "black":       (3, 1, (18, 18, 24)),
}

def write_png(path):
    cells = {(c, r): col for c, r, col in PALETTE.values()}
    w, h = COLS * CELL, ROWS * CELL
    raw = b"".join(b"\x00" + b"".join(bytes(cells[(x // CELL, y // CELL)]) for x in range(w)) for y in range(h))
    chunk = lambda t, d: struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))

palette_path = os.path.join(OUT, "knight_palette.png")
write_png(palette_path)

# --- empty scene ---
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# --- one material for the whole knight: the palette image (like in the game engine) ---
mat = bpy.data.materials.new("KnightPalette")
mat.use_nodes = True
nodes = mat.node_tree.nodes
bsdf = nodes["Principled BSDF"]
bsdf.inputs["Roughness"].default_value = 0.45
tex = nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(palette_path)
tex.interpolation = "Closest"
mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

collection = bpy.data.collections.new("Knight")
scene.collection.children.link(collection)

def finish(obj, name, color):
    """Names the part, gives it the palette material and points all its UVs at its color cell."""
    obj.name = name
    for c in obj.users_collection:
        c.objects.unlink(obj)
    collection.objects.link(obj)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    c, r, _ = PALETTE[color]
    u, v = (c + 0.5) / COLS, 1.0 - (r + 0.5) / ROWS
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    for loop_uv in obj.data.uv_layers.active.data:
        loop_uv.uv = (u, v)
    for p in obj.data.polygons:
        p.use_smooth = False   # flat shading = low-poly look
    obj["palette_color"] = color   # visible in the Object properties, to know which cell a part uses
    return obj

def box(name, color, loc, size, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    return finish(o, name, color)

def cyl(name, color, loc, r, depth, verts=8, rot=(0, 0, 0), r2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r if r2 is None else r2,
                                    depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.active_object, name, color)

def ball(name, color, loc, r, scale=(1, 1, 1), segments=8, rings=5):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=r, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, name, color)

# Blender: Z is up and the knight looks along -Y (towards the Front view, numpad 1).
# His right hand (sword) is on -X, his left hand (shield) on +X. Feet at z = 0, height about 1.65.
for side, x in (("R", -0.11), ("L", 0.11)):
    box(f"Leg.{side}", "steel_dark", (x, 0, 0.25), (0.15, 0.17, 0.36))
    box(f"Boot.{side}", "leather", (x, -0.03, 0.045), (0.17, 0.25, 0.09))
    box(f"Knee.{side}", "steel_light", (x, -0.09, 0.28), (0.11, 0.04, 0.08))
cyl("Faulds", "steel", (0, 0, 0.42), 0.25, 0.14, r2=0.22)
cyl("Belt", "leather", (0, 0, 0.495), 0.23, 0.05)
box("Belt.Buckle", "gold", (0, -0.23, 0.495), (0.07, 0.02, 0.05))
cyl("Chest", "steel", (0, 0, 0.72), 0.22, 0.42, r2=0.27)
box("Tabard", "blue", (0, -0.235, 0.67), (0.26, 0.02, 0.32))
box("Tabard.CrossV", "gold", (0, -0.25, 0.69), (0.045, 0.012, 0.22))
box("Tabard.CrossH", "gold", (0, -0.252, 0.72), (0.16, 0.016, 0.045))
cyl("Gorget", "steel_dark", (0, 0, 0.96), 0.13, 0.07)
for side, x in (("R", -1), ("L", 1)):
    ball(f"Pauldron.{side}", "steel_light", (0.28 * x, 0, 0.9), 0.12, scale=(1.0, 1.0, 0.75))
    cyl(f"Arm.{side}", "steel", (0.3 * x, 0, 0.7), 0.06, 0.34)
    box(f"Gauntlet.{side}", "steel_dark", (0.31 * x, -0.02, 0.5), (0.1, 0.11, 0.1))
# helmet
cyl("Helmet", "steel_light", (0, 0, 1.08), 0.15, 0.22)
ball("Helmet.Dome", "steel_light", (0, 0, 1.19), 0.15, scale=(1, 1, 0.75), rings=4)
box("Visor.Slit", "black", (0, -0.148, 1.11), (0.2, 0.02, 0.025))
box("Visor.Breath", "black", (0, -0.148, 1.04), (0.025, 0.02, 0.08))
box("Helmet.Ridge", "gold", (0, -0.01, 1.27), (0.03, 0.3, 0.04))
cyl("Plume", "red", (0, 0.06, 1.38), 0.055, 0.26, verts=6, rot=(math.radians(-25), 0, 0), r2=0.015)
# sword in the right hand, pointing up
sx, sy = -0.31, -0.12
cyl("Sword.Grip", "leather", (sx, sy, 0.5), 0.022, 0.14, verts=6)
ball("Sword.Pommel", "gold", (sx, sy, 0.42), 0.032, segments=6, rings=4)
box("Sword.Guard", "gold", (sx, sy, 0.585), (0.16, 0.035, 0.03))
box("Sword.Blade", "steel_light", (sx, sy, 0.88), (0.055, 0.014, 0.56))
cyl("Sword.Tip", "steel_light", (sx, sy, 1.19), 0.04, 0.07, verts=4, r2=0.0, rot=(0, 0, math.radians(45)))
# shield on the left arm, turned a little to the front
shield_rot = (0, 0, math.radians(-25))
box("Shield", "blue", (0.4, -0.06, 0.66), (0.035, 0.36, 0.46), rot=shield_rot)
box("Shield.Rim.Top", "gold", (0.405, -0.06, 0.885), (0.04, 0.37, 0.03), rot=shield_rot)
box("Shield.Rim.Bottom", "gold", (0.405, -0.06, 0.435), (0.04, 0.37, 0.03), rot=shield_rot)
ball("Shield.Boss", "gold", (0.43, -0.075, 0.66), 0.06, scale=(0.6, 1, 1), segments=8, rings=4)

# apply the transforms, so that the exported vertices are exactly what we see
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.ops.object.select_all(action="DESELECT")

# --- export for the engine: one .obj with all parts, triangulated, no materials, no normals ---
# Blender looks along -Y with Z up; the engine looks along -Z with Y up.
# forward_axis='Z' + up_axis='Y' turns the knight by 180°, so that he looks along -Z in the game.
bpy.ops.wm.obj_export(
    filepath=os.path.join(OUT, "knight.obj"),
    export_selected_objects=False,
    forward_axis="Z", up_axis="Y",
    global_scale=1.0,
    apply_modifiers=True,
    export_uv=True,
    export_normals=False,          # the engine computes flat normals = low-poly look
    export_materials=False,        # the engine ignores .mtl files anyway
    export_triangulated_mesh=True,
)

# --- a camera and a light, so that the .blend opens with a nice view, then save it ---
bpy.ops.object.light_add(type="SUN", location=(2, -3, 4))
sun = bpy.context.active_object
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
sun.data.energy = 3.0
bpy.ops.object.camera_add(location=(1.9, -3.2, 1.5))
cam = bpy.context.active_object
cam.rotation_euler = (Vector((0, 0, 0.75)) - cam.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = cam
world = bpy.data.worlds.new("World"); scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.05, 0.06, 0.09, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.5

bpy.ops.file.pack_all()   # the palette image is stored inside the .blend
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "knight.blend"))
print("parts:", len(collection.objects))
