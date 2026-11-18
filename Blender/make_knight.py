# Builds a Renaissance knight in Blender (inspired by a painted knight figurine) with natural shapes and
# baked textures, saves knight.blend and exports one .obj per articulated part for the game (scene graph).
#
#   1. Shapes: simple shapes made smooth and natural with Subdivision Surface, noise folds on the cloth,
#      and a cape draped by a cloth simulation.
#   2. Materials: procedural shader nodes (worn and scratched metal, striped woven cloth, leather, brass).
#   3. UV unwrapping of all parts into one shared atlas, then baking of the materials into two images:
#      knight_color.png (colours) and knight_normal.png (small reliefs, tangent space, OpenGL convention).
#
# Run it with Blender's Python (bpy). Do not run it inside an open Blender scene: it starts from an empty scene.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

OUT = os.path.dirname(os.path.abspath(__file__))
TEXTURE_SIZE = 2048

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"

collection = bpy.data.collections.new("Knight")
scene.collection.children.link(collection)

# =====================================================================================================
# MATERIALS (procedural shader nodes)
# =====================================================================================================
COLORS = {
    "steel_light": (0.24, 0.23, 0.21), "steel": (0.15, 0.145, 0.13), "steel_dark": (0.055, 0.052, 0.048),
    "black": (0.01, 0.01, 0.01), "brass": (0.55, 0.37, 0.11), "gold": (0.75, 0.52, 0.17),
    "bronze": (0.2, 0.12, 0.05), "leather": (0.3, 0.12, 0.04), "leather_dark": (0.05, 0.025, 0.012),
    "red": (0.32, 0.025, 0.018), "red_dark": (0.12, 0.006, 0.006), "yellow": (0.67, 0.36, 0.06),
    "orange": (0.52, 0.16, 0.025), "shield_field": (0.06, 0.03, 0.015), "heraldic_red": (0.55, 0.02, 0.015),
    "chain": (0.12, 0.12, 0.12),
}
METALS = {"steel_light", "steel", "steel_dark", "chain"}
GOLDS = {"brass", "gold", "bronze"}
LEATHERS = {"leather", "leather_dark"}
_materials = {}

class Nodes:
    """Small helper to build shader node trees with less code."""
    def __init__(self, mat):
        self.t = mat.node_tree
        self.t.nodes.clear()
        self.x = -1200
    def new(self, kind, **props):
        n = self.t.nodes.new(kind)
        n.location = (self.x, 0); self.x += 150
        for k, v in props.items():
            setattr(n, k, v)
        return n
    def link(self, a, b):
        self.t.links.new(a, b)
    def value(self, socket, v):
        socket.default_value = v
    def ramp(self, fac_socket, positions_colors):
        r = self.new("ShaderNodeValToRGB")
        els = r.color_ramp.elements
        (p0, c0), (p1, c1) = positions_colors[0], positions_colors[-1]
        els[0].position, els[0].color = p0, (*c0, 1)
        els[1].position, els[1].color = p1, (*c1, 1)
        self.link(fac_socket, r.inputs["Fac"])
        return r
    def mix(self, factor, a, b):
        m = self.new("ShaderNodeMix", data_type="RGBA")
        for socket, value in ((m.inputs[0], factor), (m.inputs[6], a), (m.inputs[7], b)):
            if isinstance(value, (float, int)):
                socket.default_value = value
            elif isinstance(value, tuple):
                socket.default_value = (*value, 1)
            else:
                self.link(value, socket)
        return m.outputs[2]
    def noise(self, coords, scale, detail=6.0, roughness=0.6):
        n = self.new("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = roughness
        self.link(coords, n.inputs["Vector"])
        return n
    def math(self, op, a, b=None):
        m = self.new("ShaderNodeMath", operation=op)
        for socket, value in ((m.inputs[0], a), (m.inputs[1], b)):
            if value is None:
                continue
            if isinstance(value, (float, int)):
                socket.default_value = value
            else:
                self.link(value, socket)
        return m.outputs[0]

def make_material(kind, stripes=None):
    key = kind if stripes is None else f"stripes_{stripes[0]}_{stripes[1]}_{stripes[2]}"
    if key in _materials:
        return _materials[key]
    mat = bpy.data.materials.new(key)
    mat.use_nodes = True
    N = Nodes(mat)
    coords = N.new("ShaderNodeTexCoord").outputs["Object"]
    out = N.new("ShaderNodeOutputMaterial")
    bsdf = N.new("ShaderNodeBsdfPrincipled")
    N.value(bsdf.inputs["Metallic"], 0.0)          # we bake the colour only: no metallic black-out
    N.link(bsdf.outputs["BSDF"], out.inputs["Surface"])
    base = COLORS.get(kind, (0.5, 0.5, 0.5))
    height = None   # height signal for the bump (small reliefs)

    if kind in METALS:
        # mottled patina, thin scratches, brighter worn edges; hammered and scratched surface
        patina = N.noise(coords, 7.0, 8.0, 0.65)
        color = N.mix(N.ramp(patina.outputs["Fac"], [(0.35, tuple(c * 0.55 for c in base)), (0.7, tuple(min(1, c * 1.35) for c in base))]).outputs["Color"], base, base)
        color = N.mix(0.55, base, N.ramp(patina.outputs["Fac"], [(0.3, tuple(c * 0.6 for c in base)), (0.75, tuple(min(1, c * 1.4) for c in base))]).outputs["Color"])
        wave = N.new("ShaderNodeTexWave", wave_type="BANDS")
        wave.inputs["Scale"].default_value = 18.0
        wave.inputs["Distortion"].default_value = 18.0
        wave.inputs["Detail"].default_value = 8.0
        N.link(coords, wave.inputs["Vector"])
        scratches = N.ramp(wave.outputs["Fac"], [(0.0, (1, 1, 1)), (0.04, (0, 0, 0))]).outputs["Color"]
        color = N.mix(N.math("MULTIPLY", scratches, 0.45), color, tuple(min(1, c * 1.8 + 0.05) for c in base))
        geo = N.new("ShaderNodeNewGeometry")
        edges = N.ramp(geo.outputs["Pointiness"], [(0.57, (0, 0, 0)), (0.68, (1, 1, 1))]).outputs["Color"]
        color = N.mix(N.math("MULTIPLY", edges, 0.7), color, tuple(min(1, c * 1.7 + 0.06) for c in base))
        hammered = N.new("ShaderNodeTexVoronoi")
        hammered.inputs["Scale"].default_value = 22.0 if kind != "chain" else 140.0
        N.link(coords, hammered.inputs["Vector"])
        fine = N.noise(coords, 160.0, 4.0, 0.5)
        height = N.math("ADD", N.math("MULTIPLY", hammered.outputs["Distance"], 0.6 if kind != "chain" else 1.6),
                        N.math("SUBTRACT", N.math("MULTIPLY", fine.outputs["Fac"], 0.25), N.math("MULTIPLY", scratches, 0.35)))
        N.value(bsdf.inputs["Roughness"], 0.35)
    elif kind in GOLDS:
        grime = N.noise(coords, 12.0, 6.0, 0.6)
        color = N.mix(grime.outputs["Fac"], tuple(c * 0.55 for c in base), tuple(min(1, c * 1.25) for c in base))
        height = N.math("MULTIPLY", N.noise(coords, 90.0, 4.0).outputs["Fac"], 0.3)
        N.value(bsdf.inputs["Roughness"], 0.3)
    elif kind in LEATHERS:
        grain = N.new("ShaderNodeTexVoronoi")
        grain.inputs["Scale"].default_value = 90.0
        N.link(coords, grain.inputs["Vector"])
        var = N.noise(coords, 9.0, 5.0)
        color = N.mix(var.outputs["Fac"], tuple(c * 0.6 for c in base), tuple(min(1, c * 1.4) for c in base))
        height = N.math("MULTIPLY", grain.outputs["Distance"], 0.5)
        N.value(bsdf.inputs["Roughness"], 0.7)
    else:
        # cloth (plain or striped), painted wood, black: woven texture and colour variation
        if stripes:
            a, b, count = stripes
            sep = N.new("ShaderNodeSeparateXYZ")
            N.link(coords, sep.inputs["Vector"])
            angle = N.math("ARCTAN2", sep.outputs["Y"], sep.outputs["X"])
            band = N.math("FRACT", N.math("MULTIPLY", angle, count / (2 * math.pi)))
            fac = N.math("GREATER_THAN", band, 0.5)
            color = N.mix(fac, COLORS[a], COLORS[b])
        else:
            color = N.mix(0.0, base, base)
        var = N.noise(coords, 6.0, 5.0)
        color = N.mix(N.math("MULTIPLY", var.outputs["Fac"], 0.35), color, (0.0, 0.0, 0.0))
        if kind in ("shield_field", "heraldic_red", "black"):
            height = N.math("MULTIPLY", N.noise(coords, 60.0, 5.0).outputs["Fac"], 0.25)
        else:
            weave_x = N.new("ShaderNodeTexWave", wave_type="BANDS", bands_direction="X")
            weave_y = N.new("ShaderNodeTexWave", wave_type="BANDS", bands_direction="Z")
            for w in (weave_x, weave_y):
                w.inputs["Scale"].default_value = 160.0
                N.link(coords, w.inputs["Vector"])
            height = N.math("MULTIPLY", N.math("ADD", weave_x.outputs["Fac"], weave_y.outputs["Fac"]), 0.25)
        N.value(bsdf.inputs["Roughness"], 0.85)

    if not isinstance(color, bpy.types.NodeSocket):
        pass
    N.link(color, bsdf.inputs["Base Color"])
    if height is not None:
        bump = N.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.6
        bump.inputs["Distance"].default_value = 0.004
        N.link(height, bump.inputs["Height"])
        N.link(bump.outputs["Normal"], bsdf.inputs["Normal"])
    _materials[key] = mat
    return mat

# =====================================================================================================
# SHAPES
# =====================================================================================================
BEVELLED = []

def finish(obj, name, color, bevel=False, stripes=None, smooth=0, folds=0.0):
    """Names the part and gives it its material.
    smooth = Subdivision Surface level (natural rounded shapes), folds = strength of noise folds (cloth)."""
    obj.name = name
    for c in obj.users_collection:
        c.objects.unlink(obj)
    collection.objects.link(obj)
    obj.data.materials.clear()
    obj.data.materials.append(make_material(color, stripes))
    obj["material"] = color if stripes is None else f"stripes {stripes}"
    if smooth:
        sub = obj.modifiers.new("Subdivision", "SUBSURF")
        sub.levels = sub.render_levels = smooth
    if folds > 0:
        tex = bpy.data.textures.get("Folds") or bpy.data.textures.new("Folds", "CLOUDS")
        tex.noise_scale = 0.06
        disp = obj.modifiers.new("Folds", "DISPLACE")
        disp.texture = tex
        disp.strength = folds
        disp.mid_level = 0.5
    if bevel:
        BEVELLED.append(obj)
    return obj

def box(name, color, loc, size, rot=(0, 0, 0), bevel=True):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    return finish(o, name, color, bevel)

def cyl(name, color, loc, r, depth, verts=16, rot=(0, 0, 0), r2=None, stripes=None, smooth=0, folds=0.0):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r if r2 is None else r2,
                                    depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.active_object, name, color, stripes=stripes, smooth=smooth, folds=folds)

def ball(name, color, loc, r, scale=(1, 1, 1), segments=16, rings=10, rot=(0, 0, 0), stripes=None, smooth=0, folds=0.0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=r, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, name, color, stripes=stripes, smooth=smooth, folds=folds)

def rivet(name, loc, r=0.011):
    return ball(name, "brass", loc, r, segments=8, rings=5)

def half_ball(name, color, loc, r, scale=(1, 1, 1), rot=(0, 0, 0)):
    """A dome (half sphere) with a thickness: pauldrons, elbow and knee cops."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=r, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -1e-4], context="VERTS")
    bm.to_mesh(o.data); bm.free()
    o.modifiers.new("Solidify", "SOLIDIFY").thickness = 0.015
    return finish(o, name, color, smooth=1)

def ring_of_rivets(prefix, center, radius, z, count, front_only=False):
    for i in range(count):
        a = 2 * math.pi * (i + 0.5) / count
        p = Vector((center[0] + radius * math.cos(a), center[1] + radius * math.sin(a), z))
        if front_only and p.y > center[1]:
            continue
        rivet(f"{prefix}.{i}", p)

# The knight looks along -Y (Front view), Z is up, feet at z = 0, height about 1.9.
# His right side (sword) is -X, his left side (shield) is +X.
HIP_Z, SHOULDER_Z, SHOULDER_X = 0.85, 1.32, 0.33
CAPE_TOP, CAPE_LENGTH = 1.32, 1.1

# ---------------- legs: puffed striped breeches with folds, knee puffs, tall striped boots ----------------
for side, x in (("R", -0.12), ("L", 0.12)):
    ball(f"Leg.{side}.Breeches", "red", (x, 0, 0.70), 0.105, scale=(1, 1, 1.55), stripes=("red", "yellow", 8), smooth=1, folds=0.012)
    ball(f"Leg.{side}.KneePuff", "orange", (x, 0, 0.505), 0.088, scale=(1, 1, 0.55), stripes=("orange", "yellow", 8), smooth=1, folds=0.01)
    cyl(f"Leg.{side}.BootTop", "yellow", (x, 0, 0.455), 0.084, 0.04, verts=24)
    cyl(f"Leg.{side}.Boot", "red", (x, 0, 0.255), 0.07, 0.38, verts=24, r2=0.08, stripes=("red", "yellow", 6))
    box(f"Leg.{side}.Shoe", "leather", (x, -0.045, 0.04), (0.11, 0.24, 0.08))
    cyl(f"Leg.{side}.Shoe.Tip", "leather", (x, -0.17, 0.035), 0.05, 0.06, verts=10, r2=0.012, rot=(math.radians(90), 0, 0))

# ---------------- hips: chain mail, faulds (rings of plates) and tassets ----------------
cyl("Hips.Chain", "chain", (0, 0, 0.88), 0.2, 0.1, verts=24, r2=0.21)
for i, (z, r) in enumerate(((0.955, 0.245), (0.915, 0.26), (0.875, 0.275))):
    cyl(f"Faulds.{i}", "steel", (0, 0, z), r, 0.045, verts=24, r2=r - 0.012)
ring_of_rivets("Faulds.Rivet", (0, 0), 0.272, 0.875, 14, front_only=True)
for side, s in (("R", -1), ("L", 1)):
    for i in range(4):
        z = 0.83 - i * 0.065
        box(f"Tasset.{side}.{i}", "steel", (0.13 * s, -0.17 - i * 0.008, z), (0.16, 0.025, 0.075),
            rot=(math.radians(8), 0, math.radians(-12 * s)))
        rivet(f"Tasset.{side}.{i}.Rivet", (0.13 * s, -0.19 - i * 0.008, z))

# ---------------- torso: rounded breastplate with brass rivets, belt ----------------
cyl("Belt", "steel_dark", (0, 0, 0.985), 0.235, 0.04, verts=24)
box("Belt.Buckle", "brass", (0, -0.235, 0.985), (0.06, 0.02, 0.05))
cyl("Breastplate", "steel", (0, 0, 1.15), 0.235, 0.34, verts=32, r2=0.29)
ball("Breastplate.Front", "steel_light", (0, -0.075, 1.17), 0.22, scale=(0.95, 0.72, 0.95), segments=24, rings=14)
box("Breastplate.Ridge", "steel_light", (0, -0.25, 1.12), (0.03, 0.04, 0.24), rot=(math.radians(-6), 0, 0))
box("Breastplate.Neckline", "brass", (0, -0.2, 1.31), (0.3, 0.035, 0.02))
ring_of_rivets("Breastplate.Rivet.Top", (0, 0), 0.285, 1.3, 16, front_only=True)
ring_of_rivets("Breastplate.Rivet.Waist", (0, 0), 0.245, 1.0, 14, front_only=True)

# ---------------- neck and close helmet with a rounded pointed visor ----------------
cyl("Gorget.0", "steel", (0, 0, 1.35), 0.15, 0.05, verts=32, r2=0.14)
cyl("Gorget.1", "steel_light", (0, 0, 1.39), 0.135, 0.04, verts=32, r2=0.125)
ring_of_rivets("Gorget.Rivet", (0, 0), 0.148, 1.355, 10, front_only=True)
ball("Helmet.Skull", "steel_light", (0, 0.01, 1.6), 0.13, scale=(1, 1.08, 1.05), smooth=1)
cyl("Helmet.Bevor", "steel", (0, -0.02, 1.47), 0.13, 0.1, verts=32, r2=0.125)
box("Helmet.Comb", "steel", (0, 0.01, 1.73), (0.02, 0.24, 0.05))
box("Helmet.VisorPlate", "steel", (0, -0.11, 1.605), (0.2, 0.05, 0.07))
box("Helmet.Slit.Upper", "black", (0, -0.137, 1.618), (0.17, 0.012, 0.012), bevel=False)
box("Helmet.Slit.Lower", "black", (0, -0.137, 1.592), (0.17, 0.012, 0.012), bevel=False)
ball("Helmet.Beak", "steel_light", (0, -0.115, 1.535), 0.085, scale=(0.95, 1.55, 0.78), smooth=1)
box("Helmet.Beak.Ridge", "steel", (0, -0.2, 1.535), (0.018, 0.1, 0.03))
for i, dx in enumerate((-0.035, 0.0, 0.035)):
    box(f"Helmet.Breath.{i}", "black", (dx, -0.2, 1.505), (0.012, 0.03, 0.025), bevel=False)
ring_of_rivets("Helmet.Rivet", (0, 0.01), 0.136, 1.535, 12)
rivet("Helmet.Pivot.R", (-0.135, -0.03, 1.6), 0.016)
rivet("Helmet.Pivot.L", (0.135, -0.03, 1.6), 0.016)

# ---------------- arms: layered pauldrons, puffed striped sleeves, plate forearms ----------------
for side, s in (("R", -1), ("L", 1)):
    x = SHOULDER_X * s
    half_ball(f"Pauldron.{side}.0", "steel_light", (x, 0, SHOULDER_Z + 0.03), 0.13, scale=(1.05, 1.05, 0.75), rot=(0, math.radians(20 * s), 0))
    half_ball(f"Pauldron.{side}.1", "steel", (x + 0.02 * s, 0, SHOULDER_Z - 0.03), 0.128, scale=(1.0, 1.05, 0.65), rot=(0, math.radians(32 * s), 0))
    half_ball(f"Pauldron.{side}.2", "steel_light", (x + 0.035 * s, 0, SHOULDER_Z - 0.08), 0.122, scale=(0.95, 1.0, 0.6), rot=(0, math.radians(42 * s), 0))
    for i in range(5):
        a = math.radians(-50 + 25 * i)
        rivet(f"Pauldron.{side}.Rivet.{i}", (x + 0.1 * s, 0.1 * math.sin(a), SHOULDER_Z - 0.06 + 0.03 * math.cos(a)))
    ball(f"Arm.{side}.Sleeve", "yellow", (x, 0, 1.13), 0.09, scale=(1, 1, 1.45), stripes=("yellow", "orange", 8), smooth=1, folds=0.012)
    half_ball(f"Arm.{side}.Couter", "steel_light", (x, 0.045, 1.0), 0.06, rot=(math.radians(-90), 0, 0))
    cyl(f"Arm.{side}.Vambrace", "steel", (x, 0, 0.885), 0.055, 0.2, verts=24, r2=0.062)
    for i, z in enumerate((0.8, 0.97)):
        cyl(f"Arm.{side}.Lame.{i}", "steel_light", (x, 0, z), 0.066, 0.025)
    box(f"Gauntlet.{side}", "steel_dark", (x, -0.02, 0.73), (0.1, 0.11, 0.1))
    cyl(f"Gauntlet.{side}.Cuff", "steel", (x, -0.015, 0.785), 0.072, 0.04, verts=16, r2=0.06)

# ---------------- longsword in the right hand, held forward ----------------
sx, sz = -SHOULDER_X, 0.73
along_y = (math.radians(90), 0, 0)
cyl("Sword.Grip", "leather_dark", (sx, -0.06, sz), 0.022, 0.16, verts=10, rot=along_y)
ball("Sword.Pommel", "gold", (sx, 0.035, sz), 0.035, segments=10, rings=6)
box("Sword.Guard", "gold", (sx, -0.155, sz), (0.26, 0.035, 0.035))
box("Sword.Blade", "steel_light", (sx, -0.57, sz), (0.012, 0.74, 0.058), bevel=False)
box("Sword.Fuller", "steel", (sx, -0.53, sz), (0.016, 0.56, 0.014), bevel=False)
cyl("Sword.Tip", "steel_light", (sx, -0.975, sz), 0.042, 0.08, verts=4, r2=0.0, rot=(math.radians(90), math.radians(45), 0))

# ---------------- heater shield with a coat of arms on the left arm ----------------
SH = Vector((0.45, -0.09, 0.98))
SH_M = Matrix.Translation(SH) @ Matrix.Rotation(math.radians(-28), 4, "Z")

def heater_outline(width, height, steps=10):
    w, top = width / 2, height * 0.45
    pts = [(-w, top), (w, top), (w, 0.0)]
    for i in range(1, steps):
        t = i / steps
        pts.append((w * (1 - t) ** 1.4, -height * 0.55 * math.sin(t * math.pi / 2)))
    pts.append((0.0, -height * 0.55))
    for i in range(steps - 1, 0, -1):
        t = i / steps
        pts.append((-w * (1 - t) ** 1.4, -height * 0.55 * math.sin(t * math.pi / 2)))
    pts.append((-w, 0.0))
    return pts

def shield_plate(name, color, outline, x_offset, thickness):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    verts = [bm.verts.new((x_offset, y, z)) for y, z in outline]
    face = bm.faces.new(verts)
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    for v in [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]:
        v.co.x -= thickness
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(mesh); bm.free()
    o = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(o)
    o.matrix_world = SH_M
    return finish(o, name, color)

def shield_box(name, color, local_center, size, angle=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = bpy.context.active_object
    o.matrix_world = SH_M @ Matrix.Translation(Vector(local_center)) @ Matrix.Rotation(math.radians(angle), 4, "X") @ Matrix.Diagonal((*size, 1.0))
    return finish(o, name, color)

outline = heater_outline(0.5, 0.68)
shield_plate("Shield.Rim", "gold", [(y * 1.06, z * 1.05) for y, z in outline], 0.0, 0.04)
shield_plate("Shield.Field", "shield_field", outline, 0.012, 0.012)
shield_plate("Shield.Arms", "heraldic_red", [(y * 0.36, z * 0.36 + 0.02) for y, z in outline], 0.022, 0.01)
shield_box("Shield.Arms.CrossV", "gold", (0.026, 0.0, 0.0), (0.006, 0.02, 0.2))
shield_box("Shield.Arms.CrossH", "gold", (0.026, 0.0, 0.035), (0.006, 0.17, 0.02))
for i, s in enumerate((-1, 1)):   # stylised golden eagle wings around the coat of arms
    for j, (dy, dz, length, angle) in enumerate(((0.13, 0.12, 0.17, 35), (0.15, 0.06, 0.15, 15), (0.14, 0.0, 0.12, -5))):
        shield_box(f"Shield.Wing.{i}.{j}", "gold", (0.02, dy * s, dz), (0.006, length, 0.045), angle * s)
    shield_box(f"Shield.Leaf.{i}", "gold", (0.02, 0.05 * s, -0.17), (0.006, 0.03, 0.12))
shield_box("Shield.Leaf.Center", "gold", (0.02, 0.0, -0.21), (0.006, 0.03, 0.09))
for i, (y, z) in enumerate(outline[::2]):
    ball(f"Shield.Stud.{i}", "gold", SH_M @ Vector((0.012, y * 0.98, z * 0.97)), 0.014, segments=8, rings=5)
ball("Shield.Boss", "brass", SH_M @ Vector((-0.02, 0.0, 0.0)), 0.05, scale=(0.5, 1, 1), smooth=1)

# ---------------- apply transforms, then bevel the boxes and smooth shading ----------------
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
for o in BEVELLED:
    b = o.modifiers.new("Bevel", "BEVEL")
    b.width, b.segments, b.limit_method = 0.008, 2, "ANGLE"
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
bpy.ops.object.select_all(action="DESELECT")

# =====================================================================================================
# CAPE: a piece of cloth draped by a cloth simulation
# =====================================================================================================
bpy.ops.mesh.primitive_grid_add(x_subdivisions=22, y_subdivisions=30, size=1.0)
cape = bpy.context.active_object
pin = cape.vertex_groups.new(name="Pin")
pinned = []
for v in cape.data.vertices:
    u, t = v.co.x, v.co.y + 0.5            # u: -0.5 … 0.5 across, t: 0 (bottom) … 1 (top)
    radius = 0.28 * (1.0 + 0.3 * (1.0 - t))
    angle = u * math.pi * 0.85              # wrap the cloth around the back
    pleat = 0.025 * math.sin(u * 26.0) * (1.0 - t)
    v.co = Vector((math.sin(angle) * radius, 0.05 + math.cos(angle) * radius * 0.75 + pleat,
                   CAPE_TOP - (1.0 - t) * CAPE_LENGTH))
    if t > 0.999:
        pinned.append(v.index)
pin.add(pinned, 1.0, "REPLACE")
# a simple invisible body to drape the cape on
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.27, depth=1.05, location=(0, 0.0, 0.84))
body_proxy = bpy.context.active_object
body_proxy.modifiers.new("Collision", "COLLISION")
cloth = cape.modifiers.new("Cloth", "CLOTH")
cloth.settings.vertex_group_mass = "Pin"
cloth.settings.quality = 6
cloth.settings.mass = 0.3
cloth.settings.tension_stiffness = 12
cloth.settings.compression_stiffness = 12
cloth.settings.bending_stiffness = 0.3
cloth.collision_settings.distance_min = 0.012
scene.frame_start, scene.frame_end = 1, 70
for frame in range(1, 71):
    scene.frame_set(frame)
draped = bpy.data.meshes.new_from_object(cape.evaluated_get(bpy.context.evaluated_depsgraph_get()))
cape.modifiers.clear()
cape.data = draped
bpy.data.objects.remove(body_proxy, do_unlink=True)
scene.frame_set(1)
finish(cape, "Cape", "red_dark")
cape.modifiers.new("Solidify", "SOLIDIFY").thickness = 0.015
for p in cape.data.polygons:
    p.use_smooth = True

print("shapes done:", len(collection.objects), "parts")

# =====================================================================================================
# UV UNWRAPPING (one shared atlas for all parts) AND BAKING
# =====================================================================================================
meshes = [o for o in collection.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.002, scale_to_bounds=False)
bpy.ops.uv.pack_islands(margin=0.002, rotate=True)
bpy.ops.object.mode_set(mode="OBJECT")

def new_image(name, non_color):
    img = bpy.data.images.new(name, TEXTURE_SIZE, TEXTURE_SIZE, alpha=False)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    return img

color_img = new_image("knight_color", False)
normal_img = new_image("knight_normal", True)

def set_bake_target(img):
    for mat in _materials.values():
        nodes = mat.node_tree.nodes
        node = nodes.get("BakeTarget") or nodes.new("ShaderNodeTexImage")
        node.name = "BakeTarget"
        node.image = img
        nodes.active = node

scene.cycles.samples = 4
scene.render.bake.margin = 6
set_bake_target(color_img)
bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, margin=6)
set_bake_target(normal_img)
bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", margin=6)
for img in (color_img, normal_img):
    img.filepath_raw = os.path.join(OUT, f"{img.name}.png")
    img.file_format = "PNG"
    img.save()
print("baked textures")

# The .blend keeps the procedural materials, plus a preview material that shows the baked textures.
preview = bpy.data.materials.new("KnightBaked")
preview.use_nodes = True
pn = preview.node_tree.nodes
pbsdf = pn["Principled BSDF"]
ctex = pn.new("ShaderNodeTexImage"); ctex.image = color_img
ntex = pn.new("ShaderNodeTexImage"); ntex.image = normal_img
nmap = pn.new("ShaderNodeNormalMap")
preview.node_tree.links.new(ctex.outputs["Color"], pbsdf.inputs["Base Color"])
preview.node_tree.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
preview.node_tree.links.new(nmap.outputs["Normal"], pbsdf.inputs["Normal"])
pbsdf.inputs["Roughness"].default_value = 0.45
pbsdf.inputs["Metallic"].default_value = 0.3

# =====================================================================================================
# EXPORT: one .obj per articulated part, with the joint at the origin
# =====================================================================================================
PARTS = {
    "body":      ((0.0, 0.0, 0.0),                lambda n: not n.startswith(("Leg.", "Arm.", "Gauntlet.", "Sword", "Shield", "Cape", "Pauldron"))),
    "leg_right": ((-0.12, 0.0, HIP_Z),            lambda n: n.startswith("Leg.R.")),
    "leg_left":  ((0.12, 0.0, HIP_Z),             lambda n: n.startswith("Leg.L.")),
    "arm_right": ((-SHOULDER_X, 0.0, SHOULDER_Z), lambda n: n.startswith(("Arm.R.", "Gauntlet.R", "Sword", "Pauldron.R"))),
    "arm_left":  ((SHOULDER_X, 0.0, SHOULDER_Z),  lambda n: n.startswith(("Arm.L.", "Gauntlet.L", "Shield", "Pauldron.L"))),
    "cape":      ((0.0, 0.17, CAPE_TOP),          lambda n: n == "Cape"),
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
    bpy.ops.wm.obj_export(filepath=os.path.join(OUT, "Knight", f"knight_{part}.obj"),
                          export_selected_objects=True, forward_axis="Z", up_axis="Y",
                          apply_modifiers=True, export_uv=True, export_normals=True,
                          export_materials=False, export_triangulated_mesh=True)
    for o in objs:
        o.location = (o.location[0] + pivot[0], o.location[1] + pivot[1], o.location[2] + pivot[2])
    print(part, len(objs), "objects")
missing = {o.name for o in collection.objects} - assigned
assert not missing, missing

# the parts now show the baked textures in the .blend (the procedural materials stay in the file)
for o in meshes:
    o.data.materials.clear()
    o.data.materials.append(preview)
    for mat in _materials.values():
        mat.use_fake_user = True

bpy.ops.object.select_all(action="DESELECT")
bpy.ops.object.light_add(type="SUN", location=(2, -3, 4))
sun = bpy.context.active_object
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
sun.data.energy = 3.5
bpy.ops.object.camera_add(location=(1.7, -3.6, 1.6))
cam = bpy.context.active_object
cam.rotation_euler = (Vector((0, 0, 0.95)) - cam.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = cam
world = bpy.data.worlds.new("World"); scene.world = world
world.color = (0.05, 0.06, 0.08)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "knight.blend"))
print("parts:", len(collection.objects))
