# Builds a detailed knight in Blender (plate armour, red cape, round shield), saves knight.blend
# and exports one .obj per articulated part for the game (scene graph).
# Run with Blender's Python (bpy). Do not run it inside an open Blender scene: it starts from an empty scene.
import bpy, bmesh, math, os, zlib, struct
from mathutils import Vector, Euler

OUT = os.path.dirname(os.path.abspath(__file__))
COLS, ROWS, CELL = 4, 3, 64
PALETTE = {  # name: (column, row from the top, color) - muted, "realistic" colours
    "steel_light": (0, 0, (176, 182, 192)),
    "steel":       (1, 0, (122, 128, 138)),
    "steel_dark":  (2, 0, (62, 66, 74)),
    "chain":       (3, 0, (92, 94, 100)),
    "red":         (0, 1, (128, 22, 26)),
    "red_dark":    (1, 1, (78, 12, 16)),
    "leather":     (2, 1, (78, 50, 32)),
    "leather_dark":(3, 1, (48, 32, 22)),
    "brass":       (0, 2, (168, 128, 58)),
    "black":       (1, 2, (14, 14, 17)),
    "wood":        (2, 2, (92, 62, 40)),
    "skin":        (3, 2, (40, 40, 44)),   # spare cell (dark)
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

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

mat = bpy.data.materials.new("KnightPalette")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
bsdf.inputs["Roughness"].default_value = 0.35
bsdf.inputs["Metallic"].default_value = 0.4
tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(palette_path)
tex.interpolation = "Closest"
mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

collection = bpy.data.collections.new("Knight")
scene.collection.children.link(collection)
BEVELLED = []   # boxes that get rounded edges

def finish(obj, name, color, bevel=False):
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
    obj["palette_color"] = color
    if bevel:
        BEVELLED.append(obj)
    return obj

def box(name, color, loc, size, rot=(0, 0, 0), bevel=True):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    return finish(o, name, color, bevel)

def cyl(name, color, loc, r, depth, verts=12, rot=(0, 0, 0), r2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r if r2 is None else r2,
                                    depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.active_object, name, color)

def ball(name, color, loc, r, scale=(1, 1, 1), segments=12, rings=8, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=r, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, name, color)

def half_ball(name, color, loc, r, scale=(1, 1, 1), rot=(0, 0, 0), keep="top"):
    """A dome: a sphere cut in half (used for pauldrons and the elbow/knee cops)."""
    o = ball(name, color, loc, r, scale, segments=12, rings=8, rot=rot)
    bm = bmesh.new(); bm.from_mesh(o.data)
    doomed = [v for v in bm.verts if (v.co.z < -1e-4 if keep == "top" else v.co.z > 1e-4)]
    bmesh.ops.delete(bm, geom=doomed, context="VERTS")
    bm.to_mesh(o.data); bm.free()
    o.modifiers.new("Solidify", "SOLIDIFY").thickness = 0.015   # closed shell: visible from both sides
    return o

# The knight looks along -Y (Front view), Z is up, feet at z = 0, height about 1.95 (7 heads).
# His right side (sword) is -X, his left side (shield) is +X.
HIP_Z, SHOULDER_Z = 0.85, 1.32       # realistic proportions: legs are about half of the height
CAPE_TOP, CAPE_LENGTH = 1.32, 1.15

# ---------- legs ----------
for side, x in (("R", -0.12), ("L", 0.12)):
    cyl(f"Leg.{side}.Chain", "chain", (x, 0, 0.70), 0.085, 0.30, r2=0.095)
    cyl(f"Leg.{side}.Cuisse", "steel", (x, -0.01, 0.68), 0.09, 0.30, r2=0.1)
    half_ball(f"Leg.{side}.Poleyn", "steel_light", (x, -0.05, 0.48), 0.07, scale=(1, 0.8, 1.1), rot=(math.radians(90), 0, 0))
    cyl(f"Leg.{side}.Greave", "steel", (x, 0, 0.27), 0.068, 0.38, r2=0.082)
    box(f"Leg.{side}.Sabaton", "steel_dark", (x, -0.05, 0.04), (0.13, 0.26, 0.08))
    box(f"Leg.{side}.Sabaton.Tip", "steel", (x, -0.16, 0.035), (0.11, 0.08, 0.06))

# ---------- hips and torso ----------
cyl("Hips.Chain", "chain", (0, 0, 0.87), 0.21, 0.12, r2=0.22)
cyl("Faulds", "steel", (0, 0, 0.93), 0.255, 0.1, r2=0.235)
cyl("Faulds.Lower", "steel_dark", (0, 0, 0.88), 0.27, 0.05, r2=0.255)
box("Tasset.R", "steel", (-0.15, -0.15, 0.78), (0.15, 0.03, 0.22), rot=(math.radians(10), 0, math.radians(15)))
box("Tasset.L", "steel", (0.15, -0.15, 0.78), (0.15, 0.03, 0.22), rot=(math.radians(10), 0, math.radians(-15)))
cyl("Belt", "leather", (0, 0, 0.985), 0.24, 0.045)
box("Belt.Buckle", "brass", (0, -0.24, 0.985), (0.07, 0.025, 0.055))
cyl("Breastplate", "steel_light", (0, 0, 1.17), 0.235, 0.36, verts=14, r2=0.28)
box("Breastplate.Ridge", "steel_light", (0, -0.25, 1.18), (0.035, 0.05, 0.3), rot=(math.radians(-7), 0, 0))
box("Breastplate.Rim", "brass", (0, -0.215, 1.34), (0.34, 0.03, 0.025))
box("Tabard", "red", (0, -0.25, 0.70), (0.26, 0.02, 0.52), bevel=False)
box("Tabard.Trim", "brass", (0, -0.262, 0.45), (0.26, 0.012, 0.025), bevel=False)
cyl("Gorget", "steel", (0, 0, 1.37), 0.14, 0.08, r2=0.12)
cyl("Gorget.Rim", "brass", (0, 0, 1.415), 0.125, 0.02)

# ---------- great helm ----------
cyl("Helm", "steel_light", (0, 0, 1.54), 0.13, 0.26, verts=14, r2=0.135)
ball("Helm.Top", "steel_light", (0, 0, 1.67), 0.135, scale=(1, 1, 0.6), rings=8)
box("Helm.Ridge", "steel", (0, 0, 1.68), (0.025, 0.29, 0.08))
box("Helm.Band", "brass", (0, -0.133, 1.55), (0.2, 0.015, 0.02), bevel=False)
box("Visor.Slit.R", "black", (-0.05, -0.131, 1.585), (0.075, 0.02, 0.018), bevel=False)
box("Visor.Slit.L", "black", (0.05, -0.131, 1.585), (0.075, 0.02, 0.018), bevel=False)
box("Visor.Nose", "steel", (0, -0.137, 1.55), (0.025, 0.02, 0.12))
for i, (dx, dz) in enumerate(((-0.04, 1.48), (0.04, 1.48), (-0.04, 1.45), (0.04, 1.45))):
    box(f"Visor.Breath.{i}", "black", (dx, -0.132, dz), (0.015, 0.02, 0.012), bevel=False)
cyl("Helm.Plume", "red", (0, 0.04, 1.79), 0.03, 0.18, verts=8, rot=(math.radians(-20), 0, 0), r2=0.06)

# ---------- cape (own part, it moves when the knight runs) ----------
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.29, depth=CAPE_LENGTH,
                                    location=(0, 0.02, CAPE_TOP - CAPE_LENGTH / 2), end_fill_type="NOTHING")
cape = bpy.context.active_object
bm = bmesh.new(); bm.from_mesh(cape.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -0.02], context="VERTS")   # keep the back half
for v in bm.verts:
    t = (CAPE_LENGTH / 2 - v.co.z) / CAPE_LENGTH    # 0 at the top, 1 at the bottom
    v.co.x *= 1.0 + 0.3 * t                          # wider at the bottom
    v.co.y = v.co.y * (1.0 + 0.4 * t) + 0.05 * t
bm.to_mesh(cape.data); bm.free()
finish(cape, "Cape", "red_dark")
cape.modifiers.new("Solidify", "SOLIDIFY").thickness = 0.02

# ---------- arms ----------
for side, s in (("R", -1), ("L", 1)):
    x = 0.31 * s
    half_ball(f"Pauldron.{side}.Top", "steel_light", (x, 0, SHOULDER_Z + 0.02), 0.13, scale=(1.05, 1, 0.8), rot=(0, math.radians(18 * s), 0))
    half_ball(f"Pauldron.{side}.Lame", "steel", (x + 0.02 * s, 0, SHOULDER_Z - 0.05), 0.125, scale=(1, 1, 0.7), rot=(0, math.radians(30 * s), 0))
    box(f"Pauldron.{side}.Rim", "brass", (x + 0.06 * s, 0, SHOULDER_Z - 0.1), (0.03, 0.2, 0.02), rot=(0, math.radians(30 * s), 0), bevel=False)
    cyl(f"Arm.{side}.Rerebrace", "steel", (x, 0, 1.15), 0.06, 0.26)
    half_ball(f"Arm.{side}.Couter", "steel_light", (x, 0.04, 1.0), 0.06, rot=(math.radians(-90), 0, 0))
    cyl(f"Arm.{side}.Vambrace", "steel", (x, 0, 0.88), 0.055, 0.23, r2=0.06)
    box(f"Gauntlet.{side}", "steel_dark", (x, -0.02, 0.73), (0.1, 0.11, 0.1))
    box(f"Gauntlet.{side}.Cuff", "steel", (x, -0.02, 0.795), (0.12, 0.13, 0.03))

# ---------- longsword in the right hand, held forward ----------
sx, sz = -0.31, 0.73
along_y = (math.radians(90), 0, 0)
cyl("Sword.Grip", "leather_dark", (sx, -0.06, sz), 0.022, 0.16, verts=8, rot=along_y)
ball("Sword.Pommel", "brass", (sx, 0.035, sz), 0.035, segments=8, rings=6)
box("Sword.Guard", "brass", (sx, -0.155, sz), (0.24, 0.035, 0.035))
box("Sword.Ricasso", "steel", (sx, -0.2, sz), (0.02, 0.06, 0.05), bevel=False)
box("Sword.Blade", "steel_light", (sx, -0.56, sz), (0.012, 0.72, 0.058), bevel=False)
box("Sword.Fuller", "steel", (sx, -0.52, sz), (0.016, 0.55, 0.014), bevel=False)
cyl("Sword.Tip", "steel_light", (sx, -0.955, sz), 0.042, 0.08, verts=4, r2=0.0, rot=(math.radians(90), math.radians(45), 0))

# ---------- round shield on the left arm ----------
shield_rot = (0, math.radians(90), math.radians(-30))   # disc facing outwards, turned a bit to the front
sh = (0.42, -0.07, 0.97)
cyl("Shield", "red", sh, 0.31, 0.035, verts=24, rot=shield_rot)
cyl("Shield.Back", "wood", (sh[0] - 0.02, sh[1] - 0.01, sh[2]), 0.305, 0.03, verts=24, rot=shield_rot)
cyl("Shield.Rim", "steel", (sh[0] + 0.004, sh[1] + 0.002, sh[2]), 0.325, 0.025, verts=24, rot=shield_rot)
ball("Shield.Boss", "steel_light", (sh[0] + 0.03, sh[1] - 0.017, sh[2]), 0.075, scale=(0.5, 1, 1), rot=(0, 0, math.radians(-30)))
for i in range(6):   # brass studs around the shield
    a = math.radians(60 * i)
    local = Vector((0.0, 0.22 * math.cos(a), 0.22 * math.sin(a)))
    local.rotate(Euler((0, 0, math.radians(-30))))
    ball(f"Shield.Stud.{i}", "brass", (sh[0] + 0.025 + local.x, sh[1] + local.y, sh[2] + local.z), 0.02, segments=6, rings=4)

# ---------- finish: apply transforms, round the boxes, smooth shading ----------
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
for o in BEVELLED:
    b = o.modifiers.new("Bevel", "BEVEL")
    b.width = 0.008
    b.segments = 2
    b.limit_method = "ANGLE"
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
bpy.ops.object.select_all(action="DESELECT")

# ---------- export: one .obj per articulated part, with the joint at the origin ----------
PARTS = {
    "body":      ((0.0, 0.0, 0.0),         lambda n: not n.startswith(("Leg.", "Arm.", "Gauntlet.", "Sword", "Shield", "Cape", "Pauldron"))),
    "leg_right": ((-0.12, 0.0, HIP_Z),     lambda n: n.startswith("Leg.R.")),
    "leg_left":  ((0.12, 0.0, HIP_Z),      lambda n: n.startswith("Leg.L.")),
    "arm_right": ((-0.31, 0.0, SHOULDER_Z), lambda n: n.startswith(("Arm.R.", "Gauntlet.R", "Sword", "Pauldron.R"))),
    "arm_left":  ((0.31, 0.0, SHOULDER_Z),  lambda n: n.startswith(("Arm.L.", "Gauntlet.L", "Shield", "Pauldron.L"))),
    "cape":      ((0.0, 0.17, CAPE_TOP),  lambda n: n == "Cape"),
}
os.makedirs(os.path.join(OUT, "Knight"), exist_ok=True)
assigned = set()
for part, (pivot, belongs) in PARTS.items():
    objs = [o for o in collection.objects if belongs(o.name)]
    assigned.update(o.name for o in objs)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
        o.location = (o.location[0] - pivot[0], o.location[1] - pivot[1], o.location[2] - pivot[2])
    # Blender looks along -Y with Z up; the game looks along -Z with Y up:
    # forward_axis='Z' + up_axis='Y' turns the knight by 180 degrees.
    bpy.ops.wm.obj_export(
        filepath=os.path.join(OUT, "Knight", f"knight_{part}.obj"),
        export_selected_objects=True, forward_axis="Z", up_axis="Y",
        apply_modifiers=True, export_uv=True,
        export_normals=True,             # smooth shading (the engine uses the normals of the file)
        export_materials=False, export_triangulated_mesh=True,
    )
    for o in objs:
        o.location = (o.location[0] + pivot[0], o.location[1] + pivot[1], o.location[2] + pivot[2])
    print(part, len(objs), "objects")
missing = {o.name for o in collection.objects} - assigned
assert not missing, missing
bpy.ops.object.select_all(action="DESELECT")

# ---------- light, camera and save ----------
bpy.ops.object.light_add(type="SUN", location=(2, -3, 4))
sun = bpy.context.active_object
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
sun.data.energy = 3.5
bpy.ops.object.camera_add(location=(2.0, -3.4, 1.6))
cam = bpy.context.active_object
cam.rotation_euler = (Vector((0, 0, 1.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = cam
world = bpy.data.worlds.new("World"); scene.world = world
world.color = (0.05, 0.06, 0.08)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "knight.blend"))
print("parts:", len(collection.objects))
