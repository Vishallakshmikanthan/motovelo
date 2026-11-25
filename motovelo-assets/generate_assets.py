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


def set_box_uv(obj, u0, v0, u1, v1):
    """Assign planar/box UVs mapped into [u0..u1] x [v0..v1] (Blender V=0 bottom, V=1 top)."""
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    uv_layer = bm.loops.layers.uv.verify()
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    min_x, max_x = min(xs), max(xs)
    min_y, max_y = min(ys), max(ys)
    min_z, max_z = min(zs), max(zs)
    dx = max(1e-5, max_x - min_x)
    dy = max(1e-5, max_y - min_y)
    dz = max(1e-5, max_z - min_z)

    for f in bm.faces:
        n = f.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in f.loops:
            co = loop.vert.co
            if az >= ax and az >= ay:
                tu = (co.x - min_x) / dx
                tv = (co.y - min_y) / dy
            elif ax >= ay and ax >= az:
                tu = (co.y - min_y) / dy
                tv = (co.z - min_z) / dz
            else:
                tu = (co.x - min_x) / dx
                tv = (co.z - min_z) / dz
            loop[uv_layer].uv = (u0 + tu * (u1 - u0), v0 + tv * (v1 - v0))
    bm.to_mesh(mesh)
    bm.free()


def add_box(name, loc, scale, mat, rot=(0, 0, 0), bevel=0.0, uv_rect=None):
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
    if uv_rect is not None:
        set_box_uv(obj, *uv_rect)
    return obj


def add_cyl(name, loc, radius, depth, mat, rot=(0, 0, 0), verts=24, uv_rect=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    if uv_rect is not None:
        set_box_uv(obj, *uv_rect)
    return obj


def add_tapered_loft_uv(name, sections, mat, u0, v0, u1, v1, side_mode=False):
    """
    Create a sculpted lofted mesh with clean UVs mapped directly onto [u0..u1] x [v0..v1].
    sections: list of (y_pos, half_w_bot, half_w_top, z_bot, z_top).
    """
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.verify()
    rings = []
    n_sec = len(sections)
    for (y, wb, wt, zb, zt) in sections:
        v0v = bm.verts.new((-wb, y, zb))
        v1v = bm.verts.new((wb, y, zb))
        v2v = bm.verts.new((wt, y, zt))
        v3v = bm.verts.new((-wt, y, zt))
        rings.append((v0v, v1v, v2v, v3v))
    bm.verts.ensure_lookup_table()

    # Cap ends
    f_start = bm.faces.new(rings[0])
    f_end = bm.faces.new(list(reversed(rings[-1])))
    for f in (f_start, f_end):
        for loop in f.loops:
            loop[uv_layer].uv = ((u0 + u1) * 0.5, (v0 + v1) * 0.5)

    # Connect rings with smooth UVs along length (v) and perimeter (u)
    u_stops = [0.0, 0.22, 0.5, 0.78, 1.0]
    for i in range(n_sec - 1):
        a = rings[i]
        b = rings[i + 1]
        va = v0 + (i / max(1, n_sec - 1)) * (v1 - v0)
        vb = v0 + ((i + 1) / max(1, n_sec - 1)) * (v1 - v0)
        for j in range(4):
            jn = (j + 1) % 4
            f = bm.faces.new((a[j], a[jn], b[jn], b[j]))
            if side_mode:
                # Map side faces so horizontal text reads cleanly along length (v0->v1)
                # j=0 bottom, j=1 right side, j=2 top, j=3 left side
                ua0 = u0 + (0.15 if j in (1, 3) else 0.0) * (u1 - u0)
                ua1 = u0 + (0.85 if j in (1, 3) else 1.0) * (u1 - u0)
                if j == 1:  # +X outer side
                    coords = [(u0, va), (u1, va), (u1, vb), (u0, vb)]
                elif j == 3:  # -X outer side
                    coords = [(u1, va), (u0, va), (u0, vb), (u1, vb)]
                else:
                    coords = [(ua0, va), (ua1, va), (ua1, vb), (ua0, vb)]
            else:
                # Top-down mapping for nose/monocoque
                if j == 2:  # top face: a[2](+x), a[3](-x), b[3](-x), b[2](+x)
                    coords = [(u1, va), (u0, va), (u0, vb), (u1, vb)]
                elif j == 1:  # +x side
                    coords = [(u1, va), (u0 + 0.75 * (u1 - u0), va), (u0 + 0.75 * (u1 - u0), vb), (u1, vb)]
                elif j == 3:  # -x side
                    coords = [(u0 + 0.25 * (u1 - u0), va), (u0, va), (u0, vb), (u0 + 0.25 * (u1 - u0), vb)]
                else:
                    coords = [(u0 + u_stops[j] * (u1 - u0), va), (u0 + u_stops[j + 1] * (u1 - u0), va),
                              (u0 + u_stops[j + 1] * (u1 - u0), vb), (u0 + u_stops[j] * (u1 - u0), vb)]
            for loop, uv in zip(f.loops, coords):
                loop[uv_layer].uv = uv

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
    2026 F1 Car with UV regions mapped onto 2026 LIVERY-TEMPLATE.PNG & HELMET-TEMPLATE.png!
    Blender coords: -Y is forward (glTF +Z), +Z is up (glTF +Y), +X is right.
    """
    clear_scene()
    # Main livery material (receives 2026 LIVERY-TEMPLATE texture at runtime!)
    m_livery = make_mat("CarLivery", (1.0, 1.0, 1.0), metallic=0.35, roughness=0.22)
    m_carbon = make_mat("Carbon", (0.045, 0.048, 0.055), metallic=0.18, roughness=0.42)
    m_helmet = make_mat("DriverHelmetMat", (1.0, 1.0, 1.0), metallic=0.28, roughness=0.16)
    m_visor = make_mat("Visor", (0.02, 0.06, 0.14), metallic=0.95, roughness=0.05)
    m_suit = make_mat("DriverSuitMat", (1.0, 1.0, 1.0), metallic=0.05, roughness=0.65)
    m_mirror = make_mat("MirrorGlass", (0.75, 0.80, 0.86), metallic=0.96, roughness=0.04)
    m_display = make_mat("SteeringDisplay", (0.05, 0.08, 0.06), metallic=0.0, roughness=0.2, emission=(0.8, 0.9, 0.8), emission_strength=1.2)
    m_rain = make_mat("RainLight", (1.0, 0.05, 0.05), metallic=0.0, roughness=0.2, emission=(1.0, 0.05, 0.02), emission_strength=6.0)

    # 1. Sculpted Central Nose & Monocoque -> UV center-bottom (0.35..0.65, 0.02..0.46)
    nose_sections = [
        (-2.68, 0.09, 0.07, 0.15, 0.21),
        (-2.22, 0.17, 0.14, 0.17, 0.33),
        (-1.62, 0.27, 0.23, 0.17, 0.49),
        (-0.92, 0.35, 0.33, 0.16, 0.61),
        (-0.22, 0.37, 0.35, 0.16, 0.63),
        (0.45,  0.34, 0.29, 0.16, 0.72),
    ]
    add_tapered_loft_uv("MonocoqueNose", nose_sections, m_livery, 0.36, 0.02, 0.64, 0.46, side_mode=False)

    # 2. Engine Cover & Airbox -> UV center-top (0.35..0.65, 0.68..0.98)
    engine_sections = [
        (0.32, 0.33, 0.22, 0.42, 0.90),
        (0.75, 0.30, 0.16, 0.24, 0.86),
        (1.25, 0.25, 0.10, 0.20, 0.74),
        (1.92, 0.15, 0.05, 0.20, 0.42),
    ]
    add_tapered_loft_uv("EngineCover", engine_sections, m_livery, 0.35, 0.69, 0.65, 0.97, side_mode=True)

    # Shark Fin (clean livery banner on sides -> engine cover UV)
    add_box("SharkFin", (0, 1.15, 0.78), (0.028, 1.10, 0.28), m_livery, uv_rect=(0.35, 0.72, 0.48, 0.96))
    add_box("AirboxIntake", (0, 0.24, 0.84), (0.16, 0.05, 0.13), m_carbon)
    add_box("TCamPillar", (0, 0.38, 0.98), (0.04, 0.06, 0.12), m_carbon)
    add_box("TCamBar", (0, 0.38, 1.05), (0.34, 0.05, 0.032), m_livery, uv_rect=(0.45, 0.85, 0.55, 0.92))

    # 3. Sculpted Sidepods (Left -> u 0.14..0.31, Right -> u 0.69..0.86, v 0.41..0.90)
    for side, s, u0, u1 in (("L", -1, 0.15, 0.30), ("R", 1, 0.70, 0.85)):
        sp_sections = [
            (-0.68, 0.15, 0.18, 0.22, 0.54),
            (-0.22, 0.28, 0.26, 0.18, 0.58),
            (0.45,  0.27, 0.24, 0.18, 0.56),
            (1.15,  0.20, 0.13, 0.18, 0.45),
            (1.72,  0.09, 0.05, 0.20, 0.34),
        ]
        sp = add_tapered_loft_uv(f"Sidepod_{side}", sp_sections, m_livery, u0, 0.42, u1, 0.89, side_mode=True)
        sp.location.x = s * 0.47
        add_box(f"Intake_{side}", (s * 0.48, -0.69, 0.40), (0.28, 0.06, 0.21), m_carbon)

    # 4. Carbon Floor / Plank & Rear Diffuser
    add_box("FloorPlank", (0, 0.05, 0.11), (1.46, 3.25, 0.05), m_carbon, bevel=0.01)
    add_box("Diffuser", (0, 1.88, 0.18), (0.98, 0.55, 0.18), m_carbon, rot=(math.radians(-14), 0, 0))
    for x in (-0.34, -0.17, 0.17, 0.34):
        add_box(f"DiffuserStrake_{x:.2f}", (x, 1.94, 0.18), (0.02, 0.45, 0.16), m_carbon)

    # 5. Cockpit Tub, Detailed F1 Steering Wheel + Live LCD Display + Gloved Hands
    add_box("CockpitTub", (0, -0.16, 0.55), (0.44, 0.80, 0.16), m_carbon)
    # Steering column + carbon wheel body (facing +Y toward driver in Blender)
    add_box("SteeringWheelBase", (0, -0.46, 0.65), (0.28, 0.035, 0.145), m_carbon, rot=(math.radians(-16), 0, 0), bevel=0.008)
    add_box("SteeringGripL", (-0.13, -0.45, 0.64), (0.045, 0.045, 0.14), m_carbon, rot=(math.radians(-16), 0, 0), bevel=0.008)
    add_box("SteeringGripR", (0.13, -0.45, 0.64), (0.045, 0.045, 0.14), m_carbon, rot=(math.radians(-16), 0, 0), bevel=0.008)
    # Live LCD telemetry plane (exact UV 0..1)
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0, -0.438, 0.655), rotation=(math.radians(74), 0, math.radians(180)))
    disp = bpy.context.active_object
    disp.name = "SteeringDisplay"
    disp.scale = (0.165, 0.088, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    disp.data.materials.append(m_display)

    # Gloved driver hands & forearms gripping wheel
    for s in (-1, 1):
        add_box(f"DriverGlove_{s}", (s * 0.135, -0.41, 0.635), (0.065, 0.09, 0.075), m_suit, bevel=0.015, uv_rect=(0.45, 0.15, 0.60, 0.35))
        add_cyl(f"DriverArm_{s}", (s * 0.17, -0.24, 0.59), 0.042, 0.32, m_suit, rot=(math.radians(78), 0, math.radians(s * 12)), verts=12, uv_rect=(0.65, 0.65, 0.90, 0.90))

    # Driver Helmet (UV sphere mapped to HELMET-TEMPLATE.png!)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, radius=0.148, location=(0, 0.04, 0.70))
    helmet = bpy.context.active_object
    helmet.name = "DriverHelmet"
    helmet.data.materials.append(m_helmet)
    bpy.ops.object.shade_smooth()
    # Map helmet UVs cleanly onto HELMET-TEMPLATE crown/sides (v: 0.28..0.96)
    bm = bmesh.new()
    bm.from_mesh(helmet.data)
    uv_layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for loop in f.loops:
            co = loop.vert.co
            ang = math.atan2(co.x, -co.y)  # 0 at front (-Y), +-pi at back
            u = 0.5 + (ang / math.pi) * 0.45
            v = 0.30 + ((co.z + 0.148) / 0.296) * 0.64
            loop[uv_layer].uv = (max(0.02, min(0.98, u)), max(0.26, min(0.98, v)))
    bm.to_mesh(helmet.data)
    bm.free()

    add_box("DriverVisor", (0, -0.095, 0.715), (0.225, 0.085, 0.072), m_visor, bevel=0.02)

    # 6. HALO Safety Hoop (mapped to Halo UV region 0.84..0.99, 0.79..0.99!)
    add_cyl("HaloPillar", (0, -0.54, 0.73), 0.022, 0.29, m_livery, rot=(math.radians(34), 0, 0), verts=14, uv_rect=(0.90, 0.92, 0.93, 0.98))
    add_box("HaloFrontArc", (0, -0.43, 0.845), (0.36, 0.11, 0.048), m_livery, bevel=0.014, uv_rect=(0.86, 0.93, 0.97, 0.98))
    for side, s, u_r in (("L", -1, (0.83, 0.79, 0.86, 0.95)), ("R", 1, (0.96, 0.79, 0.99, 0.95))):
        add_box(f"HaloArm_{side}", (s * 0.24, -0.08, 0.80), (0.048, 0.72, 0.048), m_livery, rot=(math.radians(-10), 0, math.radians(s * 14)), bevel=0.012, uv_rect=u_r)

    # Rear-View Mirrors (visible in T-Cam!)
    for side, s in (("L", -1), ("R", 1)):
        add_box(f"MirrorStalk_{side}", (s * 0.42, -0.44, 0.66), (0.16, 0.03, 0.02), m_carbon)
        add_box(f"MirrorHousing_{side}", (s * 0.51, -0.44, 0.69), (0.15, 0.07, 0.065), m_livery, bevel=0.008, uv_rect=(0.45, 0.40, 0.55, 0.45))
        add_box(f"MirrorGlass_{side}", (s * 0.51, -0.40, 0.69), (0.13, 0.01, 0.05), m_mirror)

    # 7. Multi-Element Front Wing (mapped to Front Wing UV 0.05..0.40, 0.04..0.34)
    add_box("FrontWingMain", (0, -2.55, 0.14), (1.94, 0.42, 0.038), m_livery, rot=(math.radians(4), 0, 0), bevel=0.008, uv_rect=(0.06, 0.05, 0.40, 0.18))
    add_box("FrontWingFlap1", (0, -2.40, 0.19), (1.86, 0.22, 0.03), m_livery, rot=(math.radians(14), 0, 0), bevel=0.006, uv_rect=(0.08, 0.16, 0.38, 0.26))
    add_box("FrontWingFlap2", (0, -2.30, 0.23), (1.78, 0.14, 0.025), m_livery, rot=(math.radians(20), 0, 0), bevel=0.005, uv_rect=(0.12, 0.24, 0.34, 0.33))
    for side, s in (("L", -1), ("R", 1)):
        add_box(f"FrontEndplate_{side}", (s * 0.97, -2.45, 0.24), (0.04, 0.56, 0.24), m_livery, bevel=0.008, uv_rect=(0.02, 0.72, 0.13, 0.84))
        add_box(f"NosePylon_{side}", (s * 0.11, -2.44, 0.19), (0.025, 0.26, 0.12), m_carbon)

    # 8. Rear Wing + DRS Flap + Endplates + Rain Light
    add_box("RearWingMain", (0, 2.08, 0.86), (1.06, 0.30, 0.04), m_livery, rot=(math.radians(8), 0, 0), bevel=0.008, uv_rect=(0.66, 0.27, 0.87, 0.38))
    add_box("RearWingDRS", (0, 2.16, 0.96), (1.04, 0.18, 0.035), m_livery, rot=(math.radians(22), 0, 0), bevel=0.006, uv_rect=(0.66, 0.29, 0.87, 0.37))
    add_box("RearWingBeam", (0, 1.98, 0.44), (0.88, 0.18, 0.035), m_carbon, rot=(math.radians(10), 0, 0))
    add_box("RearWingPillar", (0, 1.95, 0.68), (0.04, 0.26, 0.46), m_carbon)
    for side, s, v_r in (("L", -1, (0.87, 0.55, 0.99, 0.68)), ("R", 1, (0.87, 0.41, 0.99, 0.54))):
        add_box(f"RearEndplate_{side}", (s * 0.54, 2.08, 0.78), (0.035, 0.48, 0.48), m_livery, bevel=0.008, uv_rect=v_r)
    add_box("RainLightBox", (0, 2.08, 0.34), (0.07, 0.06, 0.12), m_rain)

    # 9. Double-Wishbone Suspension Arms
    for y_pos, name_p in ((-1.75, "Front"), (1.65, "Rear")):
        for side, s in (("L", -1), ("R", 1)):
            add_box(f"SuspUpper_{name_p}_{side}", (s * 0.52, y_pos, 0.42), (0.68, 0.045, 0.025), m_carbon)
            add_box(f"SuspLower_{name_p}_{side}", (s * 0.52, y_pos, 0.26), (0.68, 0.055, 0.025), m_carbon)
            add_box(f"SuspPushrod_{name_p}_{side}", (s * 0.50, y_pos + 0.06, 0.35), (0.68, 0.03, 0.025), m_carbon, rot=(0, math.radians(s * -16), 0))

    export_glb("formula_car.glb")


def build_formula_wheel():
    """Single Formula wheel with UV-mapped Pirelli sidewall discs for compound textures!"""
    clear_scene()
    m_rubber = make_mat("TireRubber", (0.045, 0.047, 0.052), metallic=0.0, roughness=0.84)
    m_rim = make_mat("WheelRim", (0.14, 0.15, 0.18), metallic=0.88, roughness=0.22)
    m_sidewall = make_mat("TireSidewall", (1.0, 1.0, 1.0), metallic=0.05, roughness=0.55)
    m_nut = make_mat("WheelNut", (0.92, 0.72, 0.10), metallic=0.9, roughness=0.2)

    add_cyl("TireMain", (0, 0, 0), 0.36, 0.36, m_rubber, rot=(0, math.radians(90), 0), verts=32)
    add_cyl("RimBarrel", (0, 0, 0), 0.21, 0.37, m_rim, rot=(0, math.radians(90), 0), verts=24)

    # Planar UV-mapped sidewall discs on both sides (UV 0..1 maps directly to tyres/{compound}.png!)
    for s in (-1, 1):
        bpy.ops.mesh.primitive_circle_add(vertices=32, radius=0.356, fill_type='NGON', location=(s * 0.182, 0, 0), rotation=(0, math.radians(90 * s), 0))
        disc = bpy.context.active_object
        disc.name = f"SidewallDisc_{s}"
        disc.data.materials.append(m_sidewall)
        bm = bmesh.new()
        bm.from_mesh(disc.data)
        uv_layer = bm.loops.layers.uv.verify()
        for f in bm.faces:
            for loop in f.loops:
                co = loop.vert.co
                u = 0.5 + (co.x / 0.712)
                v = 0.5 + (co.y / 0.712)
                loop[uv_layer].uv = (u, v)
        bm.to_mesh(disc.data)
        bm.free()

        add_cyl(f"CenterNut_{s}", (s * 0.19, 0, 0), 0.055, 0.04, m_nut, rot=(0, math.radians(90), 0), verts=12)
        for k in range(5):
            ang = k * (2 * math.pi / 5)
            add_box(f"Spoke_{s}_{k}", (s * 0.175, math.cos(ang) * 0.10, math.sin(ang) * 0.10), (0.03, 0.20, 0.028), m_rim, rot=(ang, 0, 0))

    export_glb("formula_wheel.glb")


def build_driver_standing():
    """
    Full 3D Standing F1 Driver for DRIVER SELECTION screen & Pit Crew!
    Height ~1.82m, facing -Y (glTF +Z).
    UVs mapped directly to SUIT-TEMPLATE.png and HELMET-TEMPLATE.png!
    """
    clear_scene()
    m_suit = make_mat("DriverSuitMat", (1.0, 1.0, 1.0), metallic=0.05, roughness=0.62)
    m_helmet = make_mat("DriverHelmetMat", (1.0, 1.0, 1.0), metallic=0.30, roughness=0.16)
    m_visor = make_mat("Visor", (0.02, 0.06, 0.14), metallic=0.95, roughness=0.04)
    m_boot = make_mat("BootDark", (0.06, 0.06, 0.07), metallic=0.15, roughness=0.45)

    # Torso (front mapped to SUIT-TEMPLATE chest 0.32..0.60, 0.63..0.95)
    torso_sections = [
        (0.0, 0.15, 0.15, 0.90, 0.94),
        (0.0, 0.16, 0.18, 0.94, 1.18),
        (0.0, 0.21, 0.21, 1.18, 1.44),
        (0.0, 0.11, 0.10, 1.44, 1.50),
    ]
    # Build torso box with smooth bevel & clean front chest UV
    torso = add_box("Torso", (0, 0, 1.18), (0.39, 0.21, 0.56), m_suit, bevel=0.045)
    bm = bmesh.new()
    bm.from_mesh(torso.data)
    uv_layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        is_front = f.normal.y < 0.0
        for loop in f.loops:
            co = loop.vert.co
            tx = (co.x + 0.195) / 0.39
            tz = (co.z - 0.90) / 0.56
            if is_front:
                loop[uv_layer].uv = (0.32 + tx * 0.28, 0.63 + tz * 0.31)
            else:
                loop[uv_layer].uv = (0.02 + tx * 0.26, 0.63 + tz * 0.31)
    bm.to_mesh(torso.data)
    bm.free()

    # Neck collar
    add_cyl("Neck", (0, 0, 1.50), 0.075, 0.10, m_boot, verts=16)

    # Arms (angled slightly outward like Monoposto Driver Selection!)
    for side, s in (("L", -1), ("R", 1)):
        add_cyl(f"UpperArm_{side}", (s * 0.26, 0, 1.29), 0.062, 0.30, m_suit, rot=(0, math.radians(s * 18), 0), verts=16, uv_rect=(0.64, 0.62, 0.94, 0.94))
        add_cyl(f"ForeArm_{side}", (s * 0.34, -0.01, 1.04), 0.052, 0.28, m_suit, rot=(0, math.radians(s * 14), 0), verts=16, uv_rect=(0.64, 0.62, 0.94, 0.94))
        add_box(f"Glove_{side}", (s * 0.38, -0.02, 0.87), (0.075, 0.055, 0.15), m_suit, rot=(0, math.radians(s * 10), 0), bevel=0.015, uv_rect=(0.42, 0.16, 0.62, 0.38))

    # Pelvis & Legs (mapped to SUIT-TEMPLATE legs 0.02..0.32, 0.20..0.55)
    add_box("Pelvis", (0, 0, 0.86), (0.35, 0.20, 0.16), m_suit, bevel=0.04, uv_rect=(0.04, 0.46, 0.30, 0.56))
    for side, s, u_r in (("L", -1, (0.02, 0.20, 0.15, 0.54)), ("R", 1, (0.18, 0.20, 0.32, 0.54))):
        add_cyl(f"Thigh_{side}", (s * 0.10, 0, 0.63), 0.088, 0.42, m_suit, rot=(0, math.radians(s * 3), 0), verts=16, uv_rect=u_r)
        add_cyl(f"Shin_{side}", (s * 0.115, 0.01, 0.27), 0.068, 0.38, m_suit, rot=(0, math.radians(s * 2), 0), verts=16, uv_rect=u_r)
        add_box(f"Boot_{side}", (s * 0.125, -0.04, 0.06), (0.095, 0.23, 0.12), m_boot, bevel=0.02)

    # Helmet (UV sphere mapped to HELMET-TEMPLATE.png!)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=18, radius=0.145, location=(0, 0, 1.66))
    helmet = bpy.context.active_object
    helmet.name = "DriverHelmet"
    helmet.data.materials.append(m_helmet)
    bpy.ops.object.shade_smooth()
    bm = bmesh.new()
    bm.from_mesh(helmet.data)
    uv_layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for loop in f.loops:
            co = loop.vert.co
            ang = math.atan2(co.x, -co.y)
            u = 0.5 + (ang / math.pi) * 0.46
            v = 0.28 + ((co.z + 0.145) / 0.29) * 0.66
            loop[uv_layer].uv = (max(0.02, min(0.98, u)), max(0.26, min(0.98, v)))
    bm.to_mesh(helmet.data)
    bm.free()

    add_box("DriverVisor", (0, -0.10, 1.67), (0.21, 0.09, 0.068), m_visor, bevel=0.02)

    export_glb("driver_standing.glb")


def build_grandstand():
    """Motorsport grandstand facing -Y (glTF +Z), width 30m, depth 11m, height 10.5m."""
    clear_scene()
    m_concrete = make_mat("Concrete", (0.50, 0.52, 0.55), metallic=0.05, roughness=0.85)
    m_steel = make_mat("Steel", (0.16, 0.18, 0.22), metallic=0.75, roughness=0.35)
    m_seats_1 = make_mat("SeatsRed", (0.78, 0.12, 0.12), metallic=0.05, roughness=0.6)
    m_seats_2 = make_mat("SeatsBlue", (0.15, 0.35, 0.72), metallic=0.05, roughness=0.6)
    m_seats_3 = make_mat("SeatsGold", (0.85, 0.68, 0.12), metallic=0.05, roughness=0.6)
    m_roof = make_mat("Roof", (0.92, 0.93, 0.95), metallic=0.22, roughness=0.38)

    add_box("Base", (0, 0, 0.7), (30.0, 9.5, 1.4), m_concrete)
    seat_mats = [m_seats_1, m_seats_2, m_seats_3]
    for i in range(9):
        y = -3.6 + i * 0.92
        z = 1.6 + i * 0.64
        add_box(f"Tier_{i}", (0, y, z), (29.2, 0.88, 0.52), seat_mats[i % 3])
    add_box("BackWall", (0, 4.6, 5.2), (30.0, 0.4, 9.2), m_concrete)
    for x in (-14.0, -7.0, 0.0, 7.0, 14.0):
        add_box(f"Col_{x:.1f}", (x, 4.2, 5.5), (0.45, 0.45, 10.2), m_steel)
    add_box("RoofCanopy", (0, -0.5, 10.5), (31.0, 11.6, 0.38), m_roof, rot=(math.radians(-6), 0, 0))
    add_box("RoofTrim", (0, -6.1, 9.9), (31.0, 0.35, 0.75), m_roof)

    export_glb("grandstand.glb")


def build_pit_building():
    """Modern F1 Pit & Paddock Building with angled control tower (Monoposto style!)."""
    clear_scene()
    m_wall = make_mat("PitWall", (0.90, 0.91, 0.93), metallic=0.1, roughness=0.45)
    m_dark = make_mat("PitDark", (0.12, 0.13, 0.16), metallic=0.35, roughness=0.4)
    m_glass = make_mat("PitGlass", (0.10, 0.22, 0.36), metallic=0.88, roughness=0.10)
    m_red = make_mat("PitAccent", (0.85, 0.08, 0.10), metallic=0.25, roughness=0.3)
    m_light = make_mat("PitLight", (1.0, 0.96, 0.88), metallic=0.0, roughness=0.2, emission=(1.0, 0.96, 0.88), emission_strength=3.0)

    add_box("MainBlock", (0, 1.5, 4.5), (48.0, 10.0, 9.0), m_wall)
    for i in range(7):
        x = -21.0 + i * 7.0
        add_box(f"Bay_{i}", (x, -3.55, 2.0), (5.6, 0.4, 3.8), m_dark)
        add_box(f"BayLight_{i}", (x, -3.65, 3.7), (4.6, 0.2, 0.18), m_light)
    add_box("UpperGlass", (0, -3.55, 6.7), (46.5, 0.3, 3.2), m_glass)
    add_box("Awning", (0, -4.8, 4.5), (48.4, 3.2, 0.35), m_dark)
    add_box("RedFascia", (0, -6.3, 4.6), (48.6, 0.25, 0.65), m_red)
    # Angled Race Control Tower (iconic from Monoposto screenshots!)
    add_box("ControlTower", (-12.0, -1.0, 12.2), (14.0, 9.0, 6.5), m_wall, rot=(math.radians(-10), 0, 0))
    add_box("ControlGlass", (-12.0, -5.4, 12.4), (13.2, 0.4, 3.8), m_glass, rot=(math.radians(-10), 0, 0))

    export_glb("pit_building.glb")


def build_start_gantry():
    """Overhead Start/Finish Gantry spanning 22m track width with 5 F1 light pods."""
    clear_scene()
    m_truss = make_mat("GantrySteel", (0.14, 0.16, 0.19), metallic=0.75, roughness=0.32)
    m_banner = make_mat("GantryBanner", (1.0, 1.0, 1.0), metallic=0.15, roughness=0.35)
    m_pod = make_mat("LightPod", (0.05, 0.05, 0.06), metallic=0.3, roughness=0.4)

    for s in (-1, 1):
        add_box(f"Tower_{s}", (s * 10.8, 0, 4.5), (1.1, 1.2, 9.0), m_truss)
        add_box(f"TowerBase_{s}", (s * 10.8, 0, 0.4), (1.8, 2.0, 0.8), m_truss)
    add_box("Bridge", (0, 0, 8.2), (22.6, 1.4, 1.6), m_truss)
    add_box("BannerFront", (0, -0.76, 8.3), (20.4, 0.12, 1.35), m_banner, uv_rect=(0.0, 0.0, 1.0, 1.0))
    add_box("BannerBack", (0, 0.76, 8.3), (20.4, 0.12, 1.35), m_banner, uv_rect=(0.0, 0.0, 1.0, 1.0))

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
    build_driver_standing()
    build_grandstand()
    build_pit_building()
    build_start_gantry()
    print("ALL MOTOVELO MONOPOSTO-GRADE BLENDER ASSETS GENERATED!")
