import bpy
import bmesh
import math
import os

OUT_DIRS = [
    r"c:\Users\Lenovo\Downloads\motovelo\motovelo-assets\models",
    r"c:\Users\Lenovo\Downloads\motovelo\motovelo-web\public\models",
]
for d in OUT_DIRS:
    os.makedirs(d, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_mat(name, color, metallic=0.0, roughness=0.4, emission=None, emission_strength=0.0):
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


def add_box(name, loc, scale, mat, rot=(0, 0, 0), bevel=0.0):
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
    return obj


def add_cyl(name, loc, radius, depth, mat, rot=(0, 0, 0), verts=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return obj


def add_tapered_loft(name, sections, mat):
    """Create a sculpted lofted mesh from a list of (y_pos, half_w_bot, half_w_top, z_bot, z_top)."""
    bm = bmesh.new()
    rings = []
    for (y, wb, wt, zb, zt) in sections:
        v0 = bm.verts.new((-wb, y, zb))
        v1 = bm.verts.new((wb, y, zb))
        v2 = bm.verts.new((wt, y, zt))
        v3 = bm.verts.new((-wt, y, zt))
        rings.append((v0, v1, v2, v3))
    bm.verts.ensure_lookup_table()
    # Cap front and back
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    # Connect rings
    for i in range(len(rings) - 1):
        a = rings[i]
        b = rings[i + 1]
        for j in range(4):
            jn = (j + 1) % 4
            bm.faces.new((a[j], a[jn], b[jn], b[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    return obj


def export_glb(filename):
    for d in OUT_DIRS:
        path = os.path.join(d, filename)
        bpy.ops.export_scene.gltf(
            filepath=path,
            export_format="GLB",
            export_yup=True,
            export_apply=True,
            export_materials="EXPORT",
        )
        print(f"Exported {path} ({os.path.getsize(path)} bytes)")


def build_formula_car():
    """
    Blender coords: -Y is forward (maps to +Z in glTF Y-up), +Z is up (maps to +Y in glTF), +X is right.
    Total car length ~5.3m, width ~2.0m, wheelbase ~3.4m (front axle y=-1.75, rear axle y=+1.65).
    """
    clear_scene()
    m_primary = make_mat("BodyPrimary", (0.85, 0.06, 0.08), metallic=0.55, roughness=0.22)
    m_secondary = make_mat("BodySecondary", (0.08, 0.09, 0.11), metallic=0.35, roughness=0.32)
    m_carbon = make_mat("Carbon", (0.04, 0.045, 0.05), metallic=0.15, roughness=0.45)
    m_accent = make_mat("Accent", (1.0, 0.72, 0.05), metallic=0.4, roughness=0.25)
    m_helmet = make_mat("Helmet", (0.95, 0.95, 0.98), metallic=0.3, roughness=0.18)
    m_visor = make_mat("Visor", (0.02, 0.05, 0.12), metallic=0.95, roughness=0.05)
    m_rain = make_mat("RainLight", (1.0, 0.05, 0.05), metallic=0.0, roughness=0.2, emission=(1.0, 0.05, 0.02), emission_strength=6.0)

    # 1. Sculpted Central Monocoque & Nose Cone (-Y is front)
    nose_sections = [
        (-2.65, 0.10, 0.08, 0.16, 0.22),
        (-2.20, 0.18, 0.15, 0.18, 0.34),
        (-1.60, 0.28, 0.24, 0.18, 0.50),
        (-0.90, 0.36, 0.34, 0.16, 0.62),
        (-0.20, 0.38, 0.36, 0.16, 0.64),
        (0.55,  0.34, 0.28, 0.16, 0.88),
        (1.30,  0.26, 0.14, 0.18, 0.74),
        (1.95,  0.16, 0.08, 0.20, 0.44),
    ]
    add_tapered_loft("Monocoque", nose_sections, m_primary)

    # 2. Carbon Floor / Plank & Rear Diffuser
    add_box("FloorPlank", (0, 0.05, 0.11), (1.42, 3.20, 0.05), m_carbon, bevel=0.01)
    add_box("FloorEdgeL", (-0.72, 0.05, 0.14), (0.05, 2.90, 0.06), m_accent)
    add_box("FloorEdgeR", (0.72, 0.05, 0.14), (0.05, 2.90, 0.06), m_accent)
    add_box("Diffuser", (0, 1.85, 0.18), (0.96, 0.55, 0.18), m_carbon, rot=(math.radians(-14), 0, 0))
    for x in (-0.34, -0.17, 0.17, 0.34):
        add_box(f"DiffuserStrake_{x:.2f}", (x, 1.92, 0.18), (0.02, 0.45, 0.16), m_carbon)

    # 3. Sculpted Sidepods (Left & Right)
    for side, s in (("L", -1), ("R", 1)):
        sp_sections = [
            (-0.65, 0.16, 0.18, 0.22, 0.54),
            (-0.25, 0.28, 0.26, 0.18, 0.58),
            (0.40,  0.27, 0.24, 0.18, 0.56),
            (1.10,  0.20, 0.14, 0.18, 0.46),
            (1.70,  0.10, 0.06, 0.20, 0.34),
        ]
        sp = add_tapered_loft(f"Sidepod_{side}", sp_sections, m_primary)
        sp.location.x = s * 0.48
        # Sidepod intake darkness
        add_box(f"Intake_{side}", (s * 0.48, -0.66, 0.40), (0.28, 0.06, 0.22), m_carbon)
        # Accent stripe along sidepod
        add_box(f"SideStripe_{side}", (s * 0.69, 0.25, 0.44), (0.03, 1.45, 0.06), m_accent)

    # 4. Airbox / Roll Hoop & Shark Fin & T-Cam
    add_box("Airbox", (0, 0.38, 0.86), (0.24, 0.32, 0.22), m_secondary, bevel=0.03)
    add_box("AirboxIntake", (0, 0.22, 0.88), (0.16, 0.04, 0.14), m_carbon)
    add_box("SharkFin", (0, 1.15, 0.78), (0.03, 1.15, 0.28), m_secondary)
    add_box("TCamPillar", (0, 0.36, 1.01), (0.04, 0.06, 0.10), m_carbon)
    add_box("TCamBar", (0, 0.36, 1.07), (0.34, 0.05, 0.035), m_accent)

    # 5. Cockpit Opening, Steering Wheel, Driver Helmet & HALO
    add_box("CockpitTub", (0, -0.18, 0.56), (0.44, 0.78, 0.16), m_carbon)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=14, radius=0.145, location=(0, 0.02, 0.69))
    helmet = bpy.context.active_object
    helmet.name = "DriverHelmet"
    helmet.data.materials.append(m_helmet)
    bpy.ops.object.shade_smooth()
    add_box("DriverVisor", (0, -0.10, 0.71), (0.22, 0.10, 0.08), m_visor, bevel=0.02)

    # Halo safety hoop (central pillar + swept arms)
    add_cyl("HaloPillar", (0, -0.50, 0.73), 0.022, 0.28, m_carbon, rot=(math.radians(32), 0, 0), verts=12)
    add_box("HaloFrontArc", (0, -0.40, 0.84), (0.34, 0.12, 0.045), m_carbon, bevel=0.012)
    for side, s in (("L", -1), ("R", 1)):
        add_box(f"HaloArm_{side}", (s * 0.22, -0.08, 0.80), (0.045, 0.68, 0.045), m_carbon, rot=(math.radians(-10), 0, math.radians(s * 14)), bevel=0.01)

    # 6. Multi-Element Front Wing
    add_box("FrontWingMain", (0, -2.52, 0.14), (1.92, 0.42, 0.04), m_carbon, rot=(math.radians(4), 0, 0), bevel=0.008)
    add_box("FrontWingFlap1", (0, -2.38, 0.19), (1.84, 0.22, 0.03), m_primary, rot=(math.radians(14), 0, 0), bevel=0.006)
    add_box("FrontWingFlap2", (0, -2.28, 0.23), (1.76, 0.14, 0.025), m_accent, rot=(math.radians(20), 0, 0), bevel=0.005)
    for side, s in (("L", -1), ("R", 1)):
        add_box(f"FrontEndplate_{side}", (s * 0.96, -2.44, 0.24), (0.04, 0.56, 0.24), m_primary, bevel=0.008)
        add_box(f"NosePylon_{side}", (s * 0.11, -2.42, 0.19), (0.025, 0.26, 0.12), m_carbon)

    # 7. Rear Wing + DRS Flap + Endplates + Rain Light
    add_box("RearWingMain", (0, 2.08, 0.86), (1.06, 0.30, 0.04), m_carbon, rot=(math.radians(8), 0, 0), bevel=0.008)
    add_box("RearWingDRS", (0, 2.16, 0.96), (1.04, 0.18, 0.035), m_primary, rot=(math.radians(22), 0, 0), bevel=0.006)
    add_box("RearWingBeam", (0, 1.98, 0.44), (0.88, 0.18, 0.035), m_carbon, rot=(math.radians(10), 0, 0))
    add_box("RearWingPillar", (0, 1.95, 0.68), (0.04, 0.26, 0.46), m_carbon)
    for side, s in (("L", -1), ("R", 1)):
        add_box(f"RearEndplate_{side}", (s * 0.54, 2.08, 0.78), (0.035, 0.48, 0.48), m_primary, bevel=0.008)
    add_box("RainLightBox", (0, 2.08, 0.34), (0.07, 0.06, 0.12), m_rain)

    # 8. Double-Wishbone Suspension Arms (Front y=-1.75, Rear y=+1.65)
    for y_pos, name_p in ((-1.75, "Front"), (1.65, "Rear")):
        for side, s in (("L", -1), ("R", 1)):
            add_box(f"SuspUpper_{name_p}_{side}", (s * 0.52, y_pos, 0.42), (0.68, 0.045, 0.025), m_carbon)
            add_box(f"SuspLower_{name_p}_{side}", (s * 0.52, y_pos, 0.26), (0.68, 0.055, 0.025), m_carbon)
            add_box(f"SuspPushrod_{name_p}_{side}", (s * 0.50, y_pos + 0.06, 0.35), (0.68, 0.03, 0.025), m_carbon, rot=(0, math.radians(s * -16), 0))

    export_glb("formula_car.glb")


def build_formula_wheel():
    """Single Formula wheel centered at origin, axle along X-axis, radius ~0.36m, width ~0.36m."""
    clear_scene()
    m_rubber = make_mat("TireRubber", (0.04, 0.042, 0.045), metallic=0.0, roughness=0.82)
    m_rim = make_mat("WheelRim", (0.14, 0.15, 0.17), metallic=0.88, roughness=0.22)
    m_stripe = make_mat("TireStripe", (0.95, 0.12, 0.10), metallic=0.1, roughness=0.35)
    m_nut = make_mat("WheelNut", (0.92, 0.72, 0.10), metallic=0.9, roughness=0.2)

    # Main slick tire cylinder along X
    add_cyl("TireMain", (0, 0, 0), 0.36, 0.36, m_rubber, rot=(0, math.radians(90), 0), verts=32)
    # Recessed rim barrel
    add_cyl("RimBarrel", (0, 0, 0), 0.23, 0.37, m_rim, rot=(0, math.radians(90), 0), verts=24)
    # Pirelli-style sidewall compound stripes (both sides)
    for s in (-1, 1):
        add_cyl(f"SidewallStripe_{s}", (s * 0.181, 0, 0), 0.285, 0.008, m_stripe, rot=(0, math.radians(90), 0), verts=32)
        add_cyl(f"SidewallInnerMask_{s}", (s * 0.183, 0, 0), 0.255, 0.008, m_rubber, rot=(0, math.radians(90), 0), verts=32)
        add_cyl(f"CenterNut_{s}", (s * 0.19, 0, 0), 0.055, 0.04, m_nut, rot=(0, math.radians(90), 0), verts=12)
        # 5 Spokes
        for k in range(5):
            ang = k * (2 * math.pi / 5)
            add_box(f"Spoke_{s}_{k}", (s * 0.175, math.cos(ang) * 0.11, math.sin(ang) * 0.11), (0.03, 0.22, 0.032), m_rim, rot=(ang, 0, 0))

    export_glb("formula_wheel.glb")


def build_grandstand():
    """Motorsport grandstand facing -Y (glTF +Z), width 28m, depth 10m, height 10m."""
    clear_scene()
    m_concrete = make_mat("Concrete", (0.48, 0.50, 0.53), metallic=0.05, roughness=0.85)
    m_steel = make_mat("Steel", (0.14, 0.16, 0.20), metallic=0.75, roughness=0.35)
    m_seats_red = make_mat("SeatsRed", (0.78, 0.10, 0.12), metallic=0.1, roughness=0.55)
    m_seats_dark = make_mat("SeatsDark", (0.12, 0.14, 0.18), metallic=0.1, roughness=0.55)
    m_roof = make_mat("Roof", (0.90, 0.92, 0.95), metallic=0.25, roughness=0.4)

    # Base foundation
    add_box("Base", (0, 0, 0.6), (28.0, 9.0, 1.2), m_concrete)
    # 8 stepped seating tiers
    for i in range(8):
        y = -3.4 + i * 0.95
        z = 1.4 + i * 0.65
        mat = m_seats_red if i % 2 == 0 else m_seats_dark
        add_box(f"Tier_{i}", (0, y, z), (27.4, 0.92, 0.55), mat)
    # Back wall
    add_box("BackWall", (0, 4.4, 4.8), (28.0, 0.4, 8.8), m_concrete)
    # Steel columns
    for x in (-13.2, -6.6, 0.0, 6.6, 13.2):
        add_box(f"Col_{x:.1f}", (x, 4.0, 5.2), (0.45, 0.45, 9.8), m_steel)
    # Cantilevered roof
    add_box("RoofCanopy", (0, -0.5, 10.1), (29.0, 11.2, 0.35), m_roof, rot=(math.radians(-6), 0, 0))
    add_box("RoofTrim", (0, -5.9, 9.5), (29.0, 0.35, 0.75), m_seats_red)

    export_glb("grandstand.glb")


def build_pit_building():
    """Modern F1 Pit & Paddock Building: width 44m, depth 12m, height 10m."""
    clear_scene()
    m_wall = make_mat("PitWall", (0.88, 0.90, 0.92), metallic=0.1, roughness=0.5)
    m_dark = make_mat("PitDark", (0.10, 0.11, 0.14), metallic=0.4, roughness=0.4)
    m_glass = make_mat("PitGlass", (0.12, 0.28, 0.45), metallic=0.85, roughness=0.12)
    m_red = make_mat("PitAccent", (0.88, 0.08, 0.10), metallic=0.25, roughness=0.3)
    m_light = make_mat("PitLight", (1.0, 0.95, 0.85), metallic=0.0, roughness=0.2, emission=(1.0, 0.96, 0.88), emission_strength=3.0)

    # Main structure
    add_box("MainBlock", (0, 1.5, 4.5), (44.0, 10.0, 9.0), m_wall)
    # Lower garage bay recesses (6 bays)
    for i in range(6):
        x = -17.5 + i * 7.0
        add_box(f"Bay_{i}", (x, -3.55, 2.0), (5.6, 0.4, 3.8), m_dark)
        add_box(f"BayLight_{i}", (x, -3.65, 3.7), (4.6, 0.2, 0.18), m_light)
    # Upper Paddock Club glass facade
    add_box("UpperGlass", (0, -3.55, 6.7), (42.5, 0.3, 3.2), m_glass)
    # Overhanging pit awning & red fascia
    add_box("Awning", (0, -4.8, 4.5), (44.4, 3.0, 0.35), m_dark)
    add_box("RedFascia", (0, -6.2, 4.6), (44.6, 0.25, 0.65), m_red)
    add_box("RoofFascia", (0, -3.6, 9.2), (44.6, 0.4, 0.8), m_red)

    export_glb("pit_building.glb")


def build_start_gantry():
    """Overhead Start/Finish Gantry spanning 20m track width with 5 F1 light pods."""
    clear_scene()
    m_truss = make_mat("GantrySteel", (0.12, 0.14, 0.17), metallic=0.75, roughness=0.32)
    m_banner = make_mat("GantryBanner", (0.85, 0.08, 0.10), metallic=0.2, roughness=0.35)
    m_pod = make_mat("LightPod", (0.05, 0.05, 0.06), metallic=0.3, roughness=0.4)

    # Left & right towers (track width ~16m, towers at +-10.5m)
    for s in (-1, 1):
        add_box(f"Tower_{s}", (s * 10.8, 0, 4.5), (1.1, 1.2, 9.0), m_truss)
        add_box(f"TowerBase_{s}", (s * 10.8, 0, 0.4), (1.8, 2.0, 0.8), m_truss)
    # Overhead bridge
    add_box("Bridge", (0, 0, 8.2), (22.6, 1.4, 1.6), m_truss)
    add_box("BannerFront", (0, -0.76, 8.3), (20.4, 0.12, 1.3), m_banner)
    add_box("BannerBack", (0, 0.76, 8.3), (20.4, 0.12, 1.3), m_banner)

    # 5 F1 Start Light Pods
    for i in range(5):
        x = -3.2 + i * 1.6
        add_box(f"StartPod_{i}", (x, -0.95, 6.7), (0.65, 0.35, 1.3), m_pod)
        m_bulb = make_mat(f"StartLight_{i}", (0.15, 0.02, 0.02), metallic=0.1, roughness=0.2)
        add_cyl(f"BulbTop_{i}", (x, -1.14, 6.95), 0.16, 0.08, m_bulb, rot=(math.radians(90), 0, 0), verts=16)
        add_cyl(f"BulbBot_{i}", (x, -1.14, 6.45), 0.16, 0.08, m_bulb, rot=(math.radians(90), 0, 0), verts=16)

    export_glb("start_gantry.glb")


if __name__ == "__main__":
    build_formula_car()
    build_formula_wheel()
    build_grandstand()
    build_pit_building()
    build_start_gantry()
    print("ALL MOTOVELO BLENDER ASSETS GENERATED SUCCESSFULLY!")
