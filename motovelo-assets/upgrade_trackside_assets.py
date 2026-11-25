import bpy
import bmesh
import math
import os

UNITY_MODELS = r"c:\Users\Lenovo\Downloads\motovelo\MotoveloUnity\Assets\Resources\Models"
STAGING_MODELS = r"c:\Users\Lenovo\Downloads\motovelo\motovelo-assets\unity_staging\Models"
FBX_DIR = r"c:\Users\Lenovo\Downloads\motovelo\motovelo-assets\fbx"
GLB_DIRS = [
    r"c:\Users\Lenovo\Downloads\motovelo\motovelo-assets\models",
    r"c:\Users\Lenovo\Downloads\motovelo\motovelo-web\public\models",
]
for d in [UNITY_MODELS, STAGING_MODELS, FBX_DIR, *GLB_DIRS]:
    os.makedirs(d, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, metallic=0.1, roughness=0.45, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def set_planar_uv(obj):
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    uv_l = bm.loops.layers.uv.verify()
    xs = [v.co.x for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    min_x, max_x = min(xs), max(xs)
    min_z, max_z = min(zs), max(zs)
    dx = max(1e-4, max_x - min_x)
    dz = max(1e-4, max_z - min_z)
    for f in bm.faces:
        for loop in f.loops:
            co = loop.vert.co
            loop[uv_l].uv = ((co.x - min_x) / dx, (co.z - min_z) / dz)
    bm.to_mesh(mesh)
    bm.free()


def add_box(name, loc, scale, mat, rot=(0, 0, 0), bevel=0.0, planar_uv=False):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0.0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    if planar_uv:
        set_planar_uv(obj)
    return obj


def add_cyl(name, loc, radius, depth, mat, rot=(0, 0, 0), verts=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def add_cone(name, loc, r1, r2, depth, mat, verts=12):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def export_all(base_name):
    for d in [UNITY_MODELS, STAGING_MODELS, FBX_DIR]:
        fbx_p = os.path.join(d, f"{base_name}.fbx")
        bpy.ops.export_scene.fbx(filepath=fbx_p, axis_forward='Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS')
    for d in GLB_DIRS:
        glb_p = os.path.join(d, f"{base_name}.glb")
        bpy.ops.export_scene.gltf(filepath=glb_p, export_format="GLB", export_yup=True, export_apply=True, export_materials="EXPORT")
    print(f"Exported {base_name} (.fbx + .glb)")


def build_grandstand():
    """AAA FIA Stadium Grandstand facing -Y (front toward track), 34m wide x 13m deep x 13.5m high."""
    clear_scene()
    m_conc   = make_mat("StadiumConcrete", (0.74, 0.76, 0.79), metallic=0.05, roughness=0.72)
    m_dark   = make_mat("StadiumSteel",    (0.14, 0.16, 0.20), metallic=0.78, roughness=0.32)
    m_roof   = make_mat("CanopyWhite",     (0.94, 0.95, 0.97), metallic=0.18, roughness=0.28)
    m_glass  = make_mat("VipGlass",        (0.08, 0.20, 0.34), metallic=0.90, roughness=0.08)
    m_red    = make_mat("SeatsCrimson",    (0.82, 0.10, 0.14), metallic=0.08, roughness=0.52)
    m_blue   = make_mat("SeatsCobalt",     (0.12, 0.34, 0.78), metallic=0.08, roughness=0.52)
    m_gold   = make_mat("SeatsAmber",      (0.92, 0.72, 0.12), metallic=0.08, roughness=0.52)
    m_aisle  = make_mat("StairAisle",      (0.42, 0.44, 0.48), metallic=0.05, roughness=0.80)
    m_banner = make_mat("GantryBanner",    (1.0, 1.0, 1.0),    metallic=0.10, roughness=0.35)

    # Structural podium & concourse
    add_box("PodiumBase", (0, 0.8, 0.9), (34.0, 11.5, 1.8), m_conc, bevel=0.06)
    add_box("FrontWall",  (0, -4.95, 1.35), (34.2, 0.35, 1.1), m_dark)
    add_box("FrontBanner", (0, -5.16, 1.35), (32.8, 0.08, 0.92), m_banner, planar_uv=True)

    # 12 Steep Stadium Seating Tiers + Colorful Crowd Blocks + Stair Aisles
    seat_mats = [m_red, m_blue, m_gold]
    for t in range(12):
        y = -4.2 + t * 0.78
        z = 1.95 + t * 0.56
        add_box(f"SeatTier_{t}", (0, y, z), (33.2, 0.74, 0.46), seat_mats[t % 3], bevel=0.02)
        # Spectator crowd silhouettes along each row
        for seg in (-12.5, -5.5, 1.5, 8.5, 13.5):
            add_box(f"CrowdRow_{t}_{seg:.1f}", (seg, y - 0.05, z + 0.42), (3.8, 0.32, 0.44), seat_mats[(t + int(abs(seg))) % 3], bevel=0.06)

    for ax in (-10.0, 0.0, 10.0):
        add_box(f"Stairway_{ax:.0f}", (ax, 0.1, 5.1), (1.15, 9.6, 0.35), m_aisle, rot=(math.radians(35), 0, 0))

    # Upper VIP Hospitality Skybox Suite
    add_box("VipSuiteBox", (0, 4.9, 9.6), (33.6, 2.8, 2.6), m_conc, bevel=0.05)
    add_box("VipSuiteGlass", (0, 3.45, 9.6), (32.4, 0.18, 1.95), m_glass)

    # Cantilevered Space-Frame Roof Trusses + Curved Stadium Canopy
    for x in (-15.5, -9.3, -3.1, 3.1, 9.3, 15.5):
        add_cyl(f"RearMast_{x:.1f}", (x, 5.8, 6.8), 0.28, 13.6, m_dark, verts=12)
        add_box(f"CantileverArm_{x:.1f}", (x, 0.2, 12.5), (0.32, 12.8, 0.55), m_dark, rot=(math.radians(-5), 0, 0))
        add_cyl(f"TensionRod_{x:.1f}", (x, 2.6, 13.3), 0.07, 7.5, m_dark, rot=(math.radians(68), 0, 0), verts=8)

    add_box("CanopyMain", (0, -0.1, 12.85), (35.2, 13.4, 0.28), m_roof, rot=(math.radians(-5), 0, 0), bevel=0.04)
    add_box("CanopyFascia", (0, -6.75, 12.25), (35.2, 0.26, 0.82), m_red)
    export_all("grandstand")


def build_pit_building():
    """3-Story FIA Paddock Club, 10 Team Garages & Angled Race Control Tower (56m wide)."""
    clear_scene()
    m_white  = make_mat("PaddockWhite", (0.92, 0.93, 0.95), metallic=0.12, roughness=0.34)
    m_carbon = make_mat("PaddockDark",  (0.11, 0.13, 0.16), metallic=0.55, roughness=0.35)
    m_glass  = make_mat("PaddockGlass", (0.07, 0.19, 0.32), metallic=0.92, roughness=0.06)
    m_red    = make_mat("PaddockRed",   (0.84, 0.07, 0.11), metallic=0.25, roughness=0.30)
    m_glow   = make_mat("GarageLight",  (1.0, 0.97, 0.90),  metallic=0.0,  roughness=0.15, emission=(1.0, 0.97, 0.90), emission_strength=3.5)
    m_banner = make_mat("GantryBanner", (1.0, 1.0, 1.0),    metallic=0.10, roughness=0.35)

    # Ground Floor Garage Block + 10 Open Garage Bays
    add_box("GarageCore", (0, 2.2, 2.4), (56.0, 10.5, 4.8), m_white, bevel=0.05)
    for i in range(10):
        gx = -24.75 + i * 5.5
        add_box(f"GarageBayRecess_{i}", (gx, -3.08, 1.95), (4.5, 0.45, 3.7), m_carbon)
        add_box(f"GarageLightStrip_{i}", (gx, -3.18, 3.62), (3.9, 0.18, 0.16), m_glow)
        add_box(f"GaragePillar_{i}", (gx - 2.75, -3.15, 2.4), (0.55, 0.55, 4.8), m_white)

    # 2nd Floor VIP Paddock Club Cantilevered Glass Lounge
    add_box("ClubSlabBot", (0, 0.6, 5.0), (57.2, 13.8, 0.45), m_carbon)
    add_box("ClubGlassWall", (0, -5.8, 6.8), (55.8, 0.25, 3.2), m_glass)
    add_box("ClubBackCore", (0, 2.4, 6.8), (56.0, 9.8, 3.2), m_white)
    add_box("ClubBalconyRail", (0, -6.25, 5.65), (57.0, 0.12, 0.95), m_glass)
    add_box("PitLaneBanner", (0, -6.32, 4.85), (54.0, 0.10, 0.85), m_banner, planar_uv=True)

    # 3rd Floor Rooftop Hospitality Wing & Louvered Pergola
    add_box("RoofDeck", (0, 0.4, 8.65), (57.6, 14.2, 0.48), m_white)
    add_box("RoofFasciaRed", (0, -6.72, 8.85), (57.8, 0.22, 0.65), m_red)
    for px in (-20.0, -7.0, 7.0, 20.0):
        add_box(f"PergolaCanopy_{px:.0f}", (px, -1.0, 11.1), (10.2, 8.5, 0.24), m_white, bevel=0.03)
        for sx in (-4.4, 4.4):
            add_cyl(f"PergolaCol_{px:.0f}_{sx:.1f}", (px + sx, -4.4, 9.9), 0.16, 2.4, m_carbon, verts=10)

    # Iconic Angled FIA Race Control Tower on Pit Entry End
    add_box("RaceControlShaft", (-21.0, 0.5, 12.6), (11.5, 8.2, 7.6), m_white, bevel=0.06)
    add_box("RaceControlPod",   (-21.0, -3.2, 14.2), (12.8, 6.4, 3.8), m_carbon, rot=(math.radians(-9), 0, 0), bevel=0.06)
    add_box("RaceControlGlass", (-21.0, -6.35, 14.3), (12.0, 0.22, 2.6), m_glass, rot=(math.radians(-9), 0, 0))
    add_cyl("CommsMast", (-21.0, 0.5, 18.2), 0.12, 4.2, m_carbon, verts=10)
    export_all("pit_building")


def build_start_gantry():
    """FIA Space-Frame Start/Finish Gantry + 5 Twin-Lens Red Start Light Pods."""
    clear_scene()
    m_truss  = make_mat("GantrySteel",  (0.14, 0.16, 0.20), metallic=0.82, roughness=0.28)
    m_banner = make_mat("GantryBanner", (1.0, 1.0, 1.0),    metallic=0.10, roughness=0.32)
    m_pod    = make_mat("LightPod",     (0.04, 0.04, 0.05), metallic=0.35, roughness=0.35)
    m_visor  = make_mat("PodHood",      (0.08, 0.08, 0.10), metallic=0.50, roughness=0.30)

    for s in (-1, 1):
        # Lattice twin-column side towers
        for dy in (-0.55, 0.55):
            add_cyl(f"Leg_{s}_{dy:.2f}", (s * 10.8, dy, 4.6), 0.22, 9.2, m_truss, verts=12)
        for hz in (1.5, 3.2, 4.9, 6.6, 8.3):
            add_box(f"Rung_{s}_{hz:.1f}", (s * 10.8, 0, hz), (0.35, 1.3, 0.16), m_truss)
        add_box(f"Plinth_{s}", (s * 10.8, 0, 0.35), (1.6, 2.0, 0.7), m_truss, bevel=0.04)

    # Overhead Space-Frame Bridge + Sponsor Boards (Front & Back)
    for dy in (-0.65, 0.65):
        for dz in (7.65, 9.15):
            add_cyl(f"Chord_{dy:.2f}_{dz:.2f}", (0, dy, dz), 0.14, 22.8, m_truss, rot=(0, math.radians(90), 0), verts=10)
    add_box("BannerFront", (0, -0.78, 8.40), (20.6, 0.10, 1.45), m_banner, planar_uv=True)
    add_box("BannerBack",  (0,  0.78, 8.40), (20.6, 0.10, 1.45), m_banner, planar_uv=True)

    # 5 Official FIA Start Light Pods
    for i in range(5):
        x = -3.2 + i * 1.6
        add_box(f"StartPod_{i}", (x, -0.92, 6.68), (0.62, 0.32, 1.36), m_pod, bevel=0.03)
        m_bulb = make_mat(f"StartLight_{i}", (0.18, 0.02, 0.02), metallic=0.05, roughness=0.2)
        for bz, tag in ((6.96, "Top"), (6.38, "Bot")):
            add_cyl(f"Bulb{tag}_{i}", (x, -1.10, bz), 0.165, 0.08, m_bulb, rot=(math.radians(90), 0, 0), verts=18)
            add_cyl(f"Hood{tag}_{i}", (x, -1.14, bz + 0.06), 0.18, 0.14, m_visor, rot=(math.radians(90), 0, 0), verts=14)
    export_all("start_gantry")


def build_tree_cluster():
    """Lush Trackside Parkland Tree Cluster (Cypress + Pine + Broadleaf canopies)."""
    clear_scene()
    m_trunk = make_mat("TreeBark",  (0.26, 0.18, 0.11), metallic=0.0, roughness=0.92)
    m_leaf1 = make_mat("FoliageDark",  (0.11, 0.29, 0.12), metallic=0.0, roughness=0.78)
    m_leaf2 = make_mat("FoliageMid",   (0.16, 0.38, 0.15), metallic=0.0, roughness=0.74)
    m_leaf3 = make_mat("FoliageFresh", (0.22, 0.45, 0.17), metallic=0.0, roughness=0.72)

    specs = [
        (0.0,  0.0,  1.15, m_leaf1, "pine"),
        (-3.4, 1.8,  0.95, m_leaf2, "broad"),
        (3.6,  1.5,  1.05, m_leaf3, "cypress"),
        (-1.6, -2.8, 0.88, m_leaf3, "pine"),
        (2.4,  -2.4, 0.92, m_leaf1, "broad"),
    ]
    for idx, (tx, ty, sc, lmat, kind) in enumerate(specs):
        add_cyl(f"Trunk_{idx}", (tx, ty, 1.4 * sc), 0.24 * sc, 2.8 * sc, m_trunk, verts=8)
        if kind == "pine":
            add_cone(f"Pine1_{idx}", (tx, ty, 3.6 * sc), 2.1 * sc, 0.2 * sc, 3.2 * sc, lmat, verts=10)
            add_cone(f"Pine2_{idx}", (tx, ty, 5.2 * sc), 1.6 * sc, 0.15 * sc, 2.8 * sc, lmat, verts=10)
            add_cone(f"Pine3_{idx}", (tx, ty, 6.6 * sc), 1.1 * sc, 0.08 * sc, 2.2 * sc, lmat, verts=10)
        elif kind == "cypress":
            add_cone(f"Cyp_{idx}", (tx, ty, 5.0 * sc), 1.15 * sc, 0.22 * sc, 6.4 * sc, lmat, verts=12)
        else:
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=2.1 * sc, location=(tx, ty, 4.3 * sc))
            sp = bpy.context.active_object
            sp.scale = (1.08, 1.02, 0.88)
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
            sp.data.materials.append(lmat)
            for p in sp.data.polygons:
                p.use_smooth = True
    export_all("tree_cluster")


def build_light_tower():
    """Tall FIA Stadium Floodlight Mast (18m tall) facing -Y toward track."""
    clear_scene()
    m_mast = make_mat("MastSteel", (0.22, 0.25, 0.29), metallic=0.80, roughness=0.32)
    m_head = make_mat("LampFrame", (0.10, 0.11, 0.13), metallic=0.60, roughness=0.38)
    m_led  = make_mat("FloodGlow", (1.0, 0.98, 0.92),  metallic=0.0,  roughness=0.1, emission=(1.0, 0.98, 0.92), emission_strength=5.0)

    add_box("MastPlinth", (0, 0, 0.5), (1.4, 1.4, 1.0), m_mast, bevel=0.05)
    add_cone("MastPole",  (0, 0, 9.2), 0.38, 0.18, 17.2, m_mast, verts=12)
    add_box("HeadMount",  (0, -0.5, 17.6), (4.2, 0.6, 1.6), m_head, rot=(math.radians(24), 0, 0), bevel=0.04)
    add_box("FloodPanel", (0, -0.82, 17.48), (3.85, 0.12, 1.28), m_led, rot=(math.radians(24), 0, 0))
    export_all("light_tower")


def build_pit_crew_member():
    """Action-ready F1 Pit Crew Mechanic (1.58m athletic crouched stance, facing -Y / Unity +Z) with pneumatic wheel gun."""
    clear_scene()
    m_suit   = make_mat("DriverSuitMat",   (1.0, 1.0, 1.0),    metallic=0.05, roughness=0.58)
    m_helmet = make_mat("DriverHelmetMat", (1.0, 1.0, 1.0),    metallic=0.28, roughness=0.18)
    m_visor  = make_mat("Visor",           (0.02, 0.06, 0.14), metallic=0.95, roughness=0.05)
    m_dark   = make_mat("BootDark",        (0.07, 0.08, 0.10), metallic=0.45, roughness=0.40)

    # Athletic crouched legs & boots
    for s in (-1, 1):
        add_box(f"Boot_{s}",  (s * 0.14, -0.05, 0.06), (0.10, 0.24, 0.12), m_dark, bevel=0.02)
        add_cyl(f"Shin_{s}",  (s * 0.14,  0.02, 0.28), 0.068, 0.38, m_suit, rot=(math.radians(-16), 0, 0), verts=12)
        add_cyl(f"Thigh_{s}", (s * 0.13,  0.10, 0.58), 0.084, 0.38, m_suit, rot=(math.radians(24), 0, 0), verts=12)

    add_box("Pelvis", (0, 0.14, 0.76), (0.34, 0.22, 0.16), m_suit, bevel=0.03)
    torso = add_box("Torso", (0, 0.04, 1.06), (0.38, 0.22, 0.48), m_suit, rot=(math.radians(18), 0, 0), bevel=0.04, planar_uv=True)

    # Forward-reaching mechanic arms + pneumatic impact wheel gun
    for s in (-1, 1):
        add_cyl(f"UpperArm_{s}", (s * 0.23, -0.06, 1.14), 0.058, 0.28, m_suit, rot=(math.radians(52), 0, math.radians(s * -10)), verts=12)
        add_cyl(f"ForeArm_{s}",  (s * 0.16, -0.26, 0.98), 0.050, 0.28, m_suit, rot=(math.radians(78), 0, math.radians(s * 16)), verts=12)
        add_box(f"Glove_{s}",    (s * 0.09, -0.40, 0.92), (0.07, 0.09, 0.07), m_dark, bevel=0.015)

    add_cyl("WheelGunBody", (0, -0.46, 0.92), 0.048, 0.28, m_dark, rot=(math.radians(90), 0, 0), verts=12)
    add_cyl("WheelGunNose", (0, -0.62, 0.92), 0.028, 0.14, m_dark, rot=(math.radians(90), 0, 0), verts=12)

    # Crew Helmet + Visor
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=14, radius=0.142, location=(0, -0.06, 1.38))
    hlm = bpy.context.active_object
    hlm.name = "CrewHelmet"
    hlm.data.materials.append(m_helmet)
    for p in hlm.data.polygons:
        p.use_smooth = True
    set_planar_uv(hlm)
    add_box("CrewVisor", (0, -0.19, 1.38), (0.20, 0.08, 0.065), m_visor, bevel=0.015)
    export_all("pit_crew_member")


def build_pit_boom():
    """Overhead FIA Pit Box Pneumatic Gantry Rig, Air Hoses & Stop/Go Light Pod."""
    clear_scene()
    m_steel = make_mat("StadiumSteel", (0.14, 0.16, 0.20), metallic=0.80, roughness=0.30)
    m_red   = make_mat("PaddockRed",   (0.86, 0.08, 0.12), metallic=0.30, roughness=0.32)
    m_glow  = make_mat("PitBoxLight",  (0.10, 0.95, 0.32), metallic=0.0,  roughness=0.15, emission=(0.15, 2.2, 0.45), emission_strength=3.0)

    # Garage-side vertical mast (x = +3.2m off pit box center) + cantilevered overhead boom spanning over car
    add_box("BoomPlinth", (3.2, 0, 0.25), (0.9, 1.2, 0.5), m_steel, bevel=0.03)
    add_cyl("BoomMast",   (3.2, 0, 2.1),  0.18, 4.0, m_steel, verts=12)
    add_box("BoomArm",    (0.8, 0, 3.85), (5.2, 0.42, 0.28), m_red, bevel=0.03)
    add_box("SubArmF",    (0.0, -1.75, 3.65), (2.4, 0.16, 0.16), m_steel)
    add_box("SubArmR",    (0.0,  1.65, 3.65), (2.4, 0.16, 0.16), m_steel)
    add_box("CrossSpine", (0.0,  0.0,  3.72), (0.20, 3.8, 0.18), m_steel)

    # 4 Overhead Pneumatic Air Hose Drops right above the 4 wheel stations
    for wx in (-1.05, 1.05):
        for wy in (-1.75, 1.65):
            add_cyl(f"AirHose_{wx:.1f}_{wy:.1f}", (wx, wy, 2.75), 0.025, 1.8, m_steel, verts=8)

    # Overhead Driver Stop/Go Light Board
    add_box("LightHousing", (0.0, -2.35, 3.25), (0.75, 0.22, 0.42), m_steel, bevel=0.02)
    add_box("PitStatusLed", (0.0, -2.18, 3.25), (0.62, 0.06, 0.28), m_glow)
    export_all("pit_boom")


if __name__ == "__main__":
    build_grandstand()
    build_pit_building()
    build_start_gantry()
    build_tree_cluster()
    build_light_tower()
    build_pit_crew_member()
    build_pit_boom()
    print("ALL HIGH-DETAIL FIA TRACKSIDE + PIT CREW MODELS EXPORTED!")
