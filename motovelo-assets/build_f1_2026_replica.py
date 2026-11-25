import bpy
import bmesh
import math
import os

OUT_MODEL_DIRS = [
    r"c:\Users\Lenovo\Downloads\motovelo\motovelo-assets\models",
    r"c:\Users\Lenovo\Downloads\motovelo\motovelo-web\public\models",
]
for d in OUT_MODEL_DIRS:
    os.makedirs(d, exist_ok=True)

SCRATCH = r"C:\Users\Lenovo\.gemini\antigravity\brain\b8ec1045-a040-4b99-811a-a712134026e0\scratch"
RB_LIVERY = r"c:\Users\Lenovo\Downloads\motovelo\ref\extracted\Untitled534_20261018170519.png"
VER_HELMET = r"c:\Users\Lenovo\Downloads\motovelo\ref\extracted\Base_2026_20261013145349.png"
SOFT_TYRE = r"c:\Users\Lenovo\Downloads\motovelo\motovelo-web\public\textures\tyres\soft.png"


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_tex_mat(name, img_path=None, base_color=(1, 1, 1), metallic=0.25, roughness=0.22, clearcoat=0.6, alpha_clip=False):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*base_color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if "Coat Weight" in bsdf.inputs:
        bsdf.inputs["Coat Weight"].default_value = clearcoat
    if img_path and os.path.exists(img_path):
        tex_node = nodes.new("ShaderNodeTexImage")
        tex_node.image = bpy.data.images.load(img_path, check_existing=True)
        links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])
        if alpha_clip:
            mat.blend_method = 'CLIP' if hasattr(mat, 'blend_method') else 'OPAQUE'
            links.new(tex_node.outputs["Alpha"], bsdf.inputs["Alpha"])
    return mat


def create_smooth_loft(name, rings_xyz_uv, mat, cap_ends=True):
    """
    rings_xyz_uv: list of rings; each ring is a list of ((x, y, z), (u, v)) tuples of equal length M.
    Builds a smooth quad mesh with exact per-vertex UVs.
    """
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.verify()
    m = len(rings_xyz_uv[0])
    n = len(rings_xyz_uv)

    v_grid = []
    uv_grid = []
    for ring in rings_xyz_uv:
        v_row = [bm.verts.new(co) for (co, _uv) in ring]
        uv_row = [uv for (_co, uv) in ring]
        v_grid.append(v_row)
        uv_grid.append(uv_row)

    bm.verts.ensure_lookup_table()

    for i in range(n - 1):
        for j in range(m):
            jn = (j + 1) % m
            f = bm.faces.new((v_grid[i][j], v_grid[i][jn], v_grid[i + 1][jn], v_grid[i + 1][j]))
            uvs = (uv_grid[i][j], uv_grid[i][jn], uv_grid[i + 1][jn], uv_grid[i + 1][j])
            for loop, uv in zip(f.loops, uvs):
                loop[uv_layer].uv = uv

    if cap_ends and m >= 3:
        f0 = bm.faces.new(list(reversed(v_grid[0])))
        for loop, uv in zip(f0.loops, reversed(uv_grid[0])):
            loop[uv_layer].uv = uv
        f1 = bm.faces.new(v_grid[-1])
        for loop, uv in zip(f1.loops, uv_grid[-1]):
            loop[uv_layer].uv = uv

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def add_quad_panel(name, corners_xyz_uv, thickness, mat):
    """Create a clean 3D wing/endplate panel with exact front/back planar UVs."""
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.verify()
    # Extrude along normal
    pts = [bm.verts.new(c[0]) for c in corners_xyz_uv]
    uvs = [c[1] for c in corners_xyz_uv]
    f_top = bm.faces.new(pts)
    for loop, uv in zip(f_top.loops, uvs):
        loop[uv_layer].uv = uv
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    nrm = f_top.normal.copy()
    pts_bot = [bm.verts.new((v.co.x - nrm.x * thickness, v.co.y - nrm.y * thickness, v.co.z - nrm.z * thickness)) for v in pts]
    f_bot = bm.faces.new(list(reversed(pts_bot)))
    for loop, uv in zip(f_bot.loops, reversed(uvs)):
        loop[uv_layer].uv = uv
    k = len(pts)
    for i in range(k):
        inext = (i + 1) % k
        fs = bm.faces.new((pts[i], pts_bot[i], pts_bot[inext], pts[inext]))
        for loop, uv in zip(fs.loops, (uvs[i], uvs[i], uvs[inext], uvs[inext])):
            loop[uv_layer].uv = uv
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def add_carbon_part(name, prim, loc, scale, mat, rot=(0, 0, 0), bevel=0.0):
    if prim == "cube":
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.5, depth=1.0, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def build_f1_2026_replica(with_preview_textures=False):
    """
    Sculpts an authentic 2026 Formula 1 Replica Car (-Y forward, +Z up, +X right).
    Every body panel is smoothly contoured with 12-point cross-sections and UV-mapped
    directly onto the exact islands of 2026 LIVERY-TEMPLATE.PNG!
    """
    clear_scene()

    m_livery = make_tex_mat("CarLivery", RB_LIVERY if with_preview_textures else None, metallic=0.28, roughness=0.20, clearcoat=0.85)
    m_carbon = make_tex_mat("Carbon", None, base_color=(0.035, 0.038, 0.044), metallic=0.22, roughness=0.38, clearcoat=0.3)
    m_helmet = make_tex_mat("DriverHelmetMat", VER_HELMET if with_preview_textures else None, metallic=0.25, roughness=0.15, clearcoat=0.9)
    m_visor = make_tex_mat("Visor", None, base_color=(0.02, 0.05, 0.14), metallic=0.96, roughness=0.04, clearcoat=1.0)
    m_suit = make_tex_mat("DriverSuitMat", None, base_color=(0.92, 0.93, 0.96), metallic=0.05, roughness=0.65, clearcoat=0.0)
    m_mirror = make_tex_mat("MirrorGlass", None, base_color=(0.78, 0.84, 0.90), metallic=0.98, roughness=0.03)
    m_display = make_tex_mat("SteeringDisplay", None, base_color=(0.04, 0.07, 0.06), metallic=0.0, roughness=0.2)
    m_rain = make_tex_mat("RainLight", None, base_color=(1.0, 0.04, 0.04), metallic=0.0, roughness=0.2)

    # =========================================================================
    # 1. SCULPTED 2026 MONOCOQUE & DROOP NOSE CONE
    # Cross-section profile (12 points around perimeter: top-center -> right chine -> right side -> keel -> left side -> left chine)
    # UV island: u in [0.35..0.65], v in [0.03..0.64]
    # =========================================================================
    nose_stations = [
        # (y, half_w, z_bot, z_mid, z_top, v_uv)
        (-2.74, 0.055, 0.155, 0.175, 0.195, 0.032),
        (-2.55, 0.105, 0.160, 0.200, 0.245, 0.080),
        (-2.25, 0.155, 0.175, 0.245, 0.315, 0.145),
        (-1.90, 0.205, 0.195, 0.300, 0.395, 0.215),
        (-1.50, 0.255, 0.205, 0.355, 0.475, 0.285),
        (-1.10, 0.305, 0.195, 0.400, 0.545, 0.355),
        (-0.70, 0.335, 0.180, 0.425, 0.595, 0.430),
        (-0.30, 0.350, 0.165, 0.430, 0.615, 0.500),
        (0.10,  0.340, 0.165, 0.430, 0.625, 0.570),
        (0.42,  0.315, 0.170, 0.430, 0.640, 0.635),
    ]
    # Perimeter normalized angles (from top clockwise looking from back):
    # 12 points around cross-section with exact lateral u-offset from 0.50
    nose_rings = []
    profile_pts = [
        (0.00, 1.00, 0.000),   # Top center
        (0.48, 0.97, 0.038),   # Top-right deck
        (0.86, 0.86, 0.070),   # Right upper chine
        (1.00, 0.55, 0.108),   # Right flank upper (CARLYLE / 1Password!)
        (0.96, 0.18, 0.142),   # Right flank lower
        (0.55, 0.00, 0.145),   # Bottom-right keel
        (-0.55, 0.00, -0.145), # Bottom-left keel
        (-0.96, 0.18, -0.142), # Left flank lower
        (-1.00, 0.55, -0.108), # Left flank upper (CARLYLE / 1Password!)
        (-0.86, 0.86, -0.070), # Left upper chine
        (-0.48, 0.97, -0.038), # Top-left deck
    ]
    for (y, hw, zb, _zm, zt, v_uv) in nose_stations:
        ring = []
        dz = zt - zb
        # Scale UV width narrower near nose tip so yellow bull tip fills nose cleanly!
        uv_w_scale = 0.42 + 0.58 * min(1.0, (hw / 0.32))
        for (px, pz, du) in profile_pts:
            x = px * hw
            z = zb + pz * dz
            u = 0.50 + du * uv_w_scale
            ring.append(((x, y, z), (u, v_uv)))
        nose_rings.append(ring)
    create_smooth_loft("MonocoqueNose", nose_rings, m_livery)

    # =========================================================================
    # 2. SCULPTED ENGINE COVER, ROLL-HOOP AIRBOX & SHARK FIN
    # UV island: u in [0.338..0.662], v in [0.680..0.985]
    # Top spine is u=0.50 (charging Red Bulls meet cleanly at spine!), flanks sweep down to u=0.338 (L) / 0.662 (R).
    # =========================================================================
    eng_stations = [
        # (y, hw_bot, hw_mid, z_bot, z_spine, v_uv)
        (0.24, 0.25, 0.19, 0.52, 0.95, 0.682),  # Yellow Airbox Intake front!
        (0.48, 0.28, 0.20, 0.44, 0.94, 0.728),  # Bull head & horns!
        (0.82, 0.27, 0.16, 0.36, 0.88, 0.795),  # Bull torso + flank Red Bull logo!
        (1.22, 0.23, 0.11, 0.28, 0.80, 0.868),  # Bull hind legs + coke-bottle taper!
        (1.62, 0.16, 0.06, 0.24, 0.68, 0.935),  # Rear taper!
        (2.02, 0.08, 0.03, 0.24, 0.48, 0.985),  # Gearbox tail!
    ]
    eng_profile = [
        (0.00,  1.00,  0.500),  # Spine top
        (0.38,  0.86,  0.542),  # Right upper arch
        (0.78,  0.58,  0.598),  # Right mid flank
        (1.00,  0.22,  0.655),  # Right lower flank (Red Bull wordmark!)
        (0.65,  0.00,  0.660),  # Right bottom tuck
        (-0.65, 0.00,  0.340),  # Left bottom tuck
        (-1.00, 0.22,  0.345),  # Left lower flank (Red Bull wordmark!)
        (-0.78, 0.58,  0.402),  # Left mid flank
        (-0.38, 0.86,  0.458),  # Left upper arch
    ]
    eng_rings = []
    for (y, hwb, hwm, zb, zs, v_uv) in eng_stations:
        ring = []
        dz = zs - zb
        for (px, pz, u_val) in eng_profile:
            w = hwm if pz > 0.5 else hwb
            ring.append(((px * w, y, zb + pz * dz), (u_val, v_uv)))
        eng_rings.append(ring)
    create_smooth_loft("EngineCover", eng_rings, m_livery)

    # Dorsal Shark Fin blade along spine
    add_quad_panel("SharkFin", [
        ((-0.008, 0.58, 0.92), (0.485, 0.74)),
        ((-0.008, 1.68, 0.68), (0.485, 0.94)),
        ((-0.008, 1.68, 0.83), (0.498, 0.94)),
        ((-0.008, 0.58, 0.95), (0.498, 0.74)),
    ], -0.016, m_livery)

    # Airbox dark carbon scoop + Broadcast T-Cam
    add_carbon_part("AirboxScoop", "cube", (0, 0.22, 0.86), (0.15, 0.06, 0.13), m_carbon, bevel=0.02)
    add_carbon_part("TCamStem", "cube", (0, 0.34, 1.00), (0.032, 0.06, 0.11), m_carbon, bevel=0.005)
    add_carbon_part("TCamWing", "cube", (0, 0.34, 1.06), (0.32, 0.048, 0.028), m_carbon, bevel=0.006)

    # =========================================================================
    # 3. DEEP-UNDERCUT SCULPTED 2026 SIDEPODS (LEFT & RIGHT)
    # Left Sidepod (-X) -> Right UV island u in [0.695..0.848], v in [0.41..0.89] (ORACLE reads L->R!)
    # Right Sidepod (+X) -> Left UV island u in [0.305..0.152], v in [0.41..0.89] (ORACLE reads L->R!)
    # =========================================================================
    sp_stations = [
        # (y, x_inner, x_outer, z_bot, z_top, v_uv)
        (-0.68, 0.28, 0.68, 0.25, 0.54, 0.410),  # High overbite front intake!
        (-0.36, 0.28, 0.74, 0.19, 0.57, 0.475),  # Wide sculpted shoulder (start of ORACLE!)
        (0.10,  0.26, 0.72, 0.18, 0.55, 0.565),  # Mid sidepod (R-A-C!)
        (0.62,  0.22, 0.62, 0.18, 0.50, 0.670),  # Downwash ramp (L-E!)
        (1.18,  0.16, 0.44, 0.19, 0.43, 0.785),  # Coke-bottle undercut!
        (1.74,  0.10, 0.24, 0.21, 0.35, 0.890),  # Rear taper!
    ]
    for side, s in (("L", -1), ("R", 1)):
        # Left (-X): front (-Y) is on the right when viewed from -X -> use Left UV island u in [0.305..0.152]!
        # Right (+X): front (-Y) is on the left when viewed from +X -> use Right UV island u in [0.695..0.848]!
        u_top = 0.305 if s < 0 else 0.695
        u_bot = 0.152 if s < 0 else 0.848

        sp_rings = []
        sp_prof = [
            (0.00, 0.96, 0.00),  # Inner top join
            (0.45, 1.00, 0.18),  # Top deck
            (0.88, 0.92, 0.35),  # Upper outer shoulder (top of ORACLE letters!)
            (1.00, 0.62, 0.55),  # Mid outer flank (center of ORACLE letters!)
            (0.92, 0.26, 0.78),  # Lower undercut curve (bottom of ORACLE letters!)
            (0.52, 0.04, 0.95),  # Deep floor undercut!
            (0.05, 0.08, 1.00),  # Inner bottom join
        ]
        for (y, xi, xo, zb, zt, v_uv) in sp_stations:
            ring = []
            for (fx, fz, tu) in sp_prof:
                x = s * (xi + fx * (xo - xi))
                z = zb + fz * (zt - zb)
                u = u_top + tu * (u_bot - u_top)
                ring.append(((x, y, z), (u, v_uv)))
            sp_rings.append(ring)
        create_smooth_loft(f"Sidepod_{side}", sp_rings, m_livery)
        # Dark radiator intake mouth
        add_carbon_part(f"RadiatorMouth_{side}", "cube", (s * 0.49, -0.69, 0.41), (0.34, 0.05, 0.19), m_carbon, bevel=0.015)

    # =========================================================================
    # 4. GROUND-EFFECT VENTURI FLOOR, EDGE WINGS & REAR DIFFUSER
    # =========================================================================
    add_carbon_part("VenturiFloor", "cube", (0, 0.12, 0.105), (1.54, 3.42, 0.042), m_carbon, bevel=0.012)
    for s in (-1, 1):
        add_carbon_part(f"FloorEdgeWing_{s}", "cube", (s * 0.79, 0.15, 0.125), (0.06, 2.85, 0.035), m_carbon, rot=(0, math.radians(s * 12), 0), bevel=0.006)
    add_carbon_part("RearDiffuser", "cube", (0, 1.92, 0.19), (1.04, 0.58, 0.20), m_carbon, rot=(math.radians(-15), 0, 0), bevel=0.01)
    for x in (-0.38, -0.19, 0.0, 0.19, 0.38):
        add_carbon_part(f"DiffStrake_{x:.2f}", "cube", (x, 1.98, 0.18), (0.018, 0.48, 0.17), m_carbon)

    # =========================================================================
    # 5. SWEPT MULTI-ELEMENT 2026 FRONT WING + SCULPTED ENDPLATES
    # Flaps mapped to u in [0.01..0.14], v in [0.66..0.98] (Esso & AT&T!)
    # Endplates mapped to u in [0.87..0.99], v in [0.42..0.69] (Red Bull can!)
    # =========================================================================
    for idx, (y_c, y_s, z_c, z_s, chord, v0, v1) in enumerate([
        (-2.64, -2.44, 0.115, 0.135, 0.22, 0.67, 0.75),
        (-2.48, -2.31, 0.150, 0.175, 0.16, 0.76, 0.84),
        (-2.35, -2.20, 0.190, 0.220, 0.13, 0.86, 0.95),
    ]):
        fw_rings = []
        spans = [-0.95, -0.62, -0.22, 0.0, 0.22, 0.62, 0.95]
        for sx in spans:
            t_span = abs(sx) / 0.95
            yy = y_c + (y_s - y_c) * (t_span ** 1.4)
            zz = z_c + (z_s - z_c) * t_span
            u_val = 0.02 + (1.0 - t_span) * 0.11
            ring = [
                ((sx, yy - chord * 0.5, zz - 0.012), (u_val, v0)),
                ((sx, yy + chord * 0.5, zz + 0.022), (u_val, v1)),
                ((sx, yy + chord * 0.5, zz + 0.004), (u_val, v1)),
                ((sx, yy - chord * 0.5, zz - 0.026), (u_val, v0)),
            ]
            fw_rings.append(ring)
        create_smooth_loft(f"FrontWingElem_{idx}", fw_rings, m_livery)

    for side, s in (("L", -1), ("R", 1)):
        add_quad_panel(f"FrontEndplate_{side}", [
            ((s * 0.96, -2.58, 0.11), (0.88, 0.56)),
            ((s * 0.96, -2.08, 0.12), (0.98, 0.56)),
            ((s * 0.99, -2.14, 0.37), (0.98, 0.69)),
            ((s * 0.97, -2.52, 0.32), (0.88, 0.69)),
        ], s * 0.022, m_livery)
        add_carbon_part(f"NosePylon_{side}", "cube", (s * 0.09, -2.48, 0.18), (0.022, 0.22, 0.11), m_carbon)

    # =========================================================================
    # 6. SCULPTED SWAN-NECK REAR WING + DRS FLAP + MOBIL 1 ENDPLATES
    # Mainplane -> u in [0.70..0.84], v in [0.03..0.23] (ORACLE / Gate.io)
    # DRS Flap  -> u in [0.66..0.88], v in [0.27..0.38] (Red Bull / VISA)
    # Endplates -> u in [0.885..0.99], v in [0.27..0.40] (Mobil 1!)
    # =========================================================================
    add_quad_panel("RearWingMain", [
        ((-0.52, 1.98, 0.81), (0.71, 0.04)),
        ((0.52,  1.98, 0.81), (0.71, 0.22)),
        ((0.52,  2.22, 0.85), (0.83, 0.22)),
        ((-0.52, 2.22, 0.85), (0.83, 0.04)),
    ], 0.028, m_livery)

    add_quad_panel("RearWingDRS", [
        ((-0.51, 2.12, 0.91), (0.665, 0.275)),
        ((0.51,  2.12, 0.91), (0.875, 0.275)),
        ((0.51,  2.26, 0.99), (0.875, 0.380)),
        ((-0.51, 2.26, 0.99), (0.665, 0.380)),
    ], 0.022, m_livery)

    for side, s in (("L", -1), ("R", 1)):
        # Mobil 1 reads left-to-right on BOTH endplates!
        u_front = 0.992 if s < 0 else 0.885
        u_rear  = 0.885 if s < 0 else 0.992
        add_quad_panel(f"RearEndplate_{side}", [
            ((s * 0.53, 1.94, 0.56), (u_front, 0.16)),
            ((s * 0.53, 2.30, 0.58), (u_rear,  0.16)),
            ((s * 0.53, 2.32, 1.02), (u_rear,  0.40)),
            ((s * 0.53, 1.96, 1.00), (u_front, 0.40)),
        ], s * 0.024, m_livery)
        add_carbon_part(f"SwanNeckPylon_{side}", "cube", (s * 0.075, 1.96, 0.69), (0.022, 0.24, 0.44), m_carbon, bevel=0.005)

    add_carbon_part("BeamWing", "cube", (0, 2.02, 0.44), (0.94, 0.18, 0.028), m_carbon, rot=(math.radians(12), 0, 0), bevel=0.005)
    add_carbon_part("RainLightBox", "cube", (0, 2.12, 0.34), (0.065, 0.055, 0.11), m_rain, bevel=0.008)

    # =========================================================================
    # 7. CURVED TITANIUM HALO ARCH (UV-MAPPED TO TAG HEUER / ORACLE / AT&T ISLAND!)
    # Island at u in [0.05..0.41], v in [0.06..0.24]
    # =========================================================================
    halo_pts = [
        # (x, y, z, radius, u, v) sweeping from left cockpit mount -> front center -> right cockpit mount
        (-0.26,  0.18, 0.72, 0.026, 0.10, 0.10),
        (-0.25, -0.06, 0.78, 0.027, 0.14, 0.14),
        (-0.20, -0.28, 0.83, 0.028, 0.18, 0.17),
        (-0.10, -0.43, 0.85, 0.030, 0.21, 0.19),
        (0.00,  -0.48, 0.855, 0.032, 0.233, 0.195), # Center TAG Heuer shield!
        (0.10,  -0.43, 0.85, 0.030, 0.26, 0.19),
        (0.20,  -0.28, 0.83, 0.028, 0.29, 0.17),
        (0.25,  -0.06, 0.78, 0.027, 0.33, 0.14),
        (0.26,   0.18, 0.72, 0.026, 0.36, 0.10),
    ]
    halo_rings = []
    n_circ = 10
    for (hx, hy, hz, hr, hu, hv) in halo_pts:
        ring = []
        for k in range(n_circ):
            ang = (k / n_circ) * 2 * math.pi
            # Elliptical tube cross-section; top/rear of Halo maps to TAG Heuer / ORACLE / AT&T!
            ox = math.cos(ang) * hr * 0.95
            oz = math.sin(ang) * hr * 1.15
            ring.append(((hx + ox, hy, hz + oz), (hu + ox * 0.35, hv - math.cos(ang) * 0.015)))
        halo_rings.append(ring)
    create_smooth_loft("HaloArch", halo_rings, m_livery)
    add_carbon_part("HaloCenterPillar", "cyl", (0, -0.53, 0.73), (0.042, 0.042, 0.28), m_carbon, rot=(math.radians(28), 0, 0))

    # =========================================================================
    # 8. COCKPIT TUB, STEERING WHEEL + LIVE LCD, DRIVER HELMET & VISA MIRRORS
    # =========================================================================
    add_carbon_part("CockpitOpening", "cube", (0, -0.12, 0.56), (0.44, 0.76, 0.14), m_carbon, bevel=0.02)
    add_carbon_part("HeadrestSurround", "cube", (0, 0.16, 0.66), (0.42, 0.30, 0.08), m_carbon, bevel=0.025)

    # Carbon F1 Steering Wheel + Live LCD Display
    add_carbon_part("SteeringWheelBody", "cube", (0, -0.44, 0.65), (0.27, 0.032, 0.14), m_carbon, rot=(math.radians(-16), 0, 0), bevel=0.008)
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0, -0.42, 0.655), rotation=(math.radians(74), 0, math.radians(180)))
    disp = bpy.context.active_object
    disp.name = "SteeringDisplay"
    disp.scale = (0.16, 0.085, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    disp.data.materials.append(m_display)

    for s in (-1, 1):
        add_carbon_part(f"DriverGlove_{s}", "cube", (s * 0.13, -0.39, 0.635), (0.06, 0.085, 0.07), m_suit, bevel=0.015)
        add_carbon_part(f"DriverArm_{s}", "cyl", (s * 0.165, -0.22, 0.59), (0.08, 0.08, 0.30), m_suit, rot=(math.radians(78), 0, math.radians(s * 12)))

    # Sculpted Driver Helmet + Visor
    bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=18, radius=0.146, location=(0, 0.04, 0.705))
    helmet = bpy.context.active_object
    helmet.name = "DriverHelmet"
    helmet.scale = (0.94, 1.06, 0.98)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    helmet.data.materials.append(m_helmet)
    for p in helmet.data.polygons:
        p.use_smooth = True
    bm = bmesh.new()
    bm.from_mesh(helmet.data)
    uv_layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for loop in f.loops:
            co = loop.vert.co
            ang = math.atan2(co.x, -co.y)
            u = 0.5 + (ang / math.pi) * 0.45
            v = 0.30 + ((co.z + 0.145) / 0.29) * 0.64
            loop[uv_layer].uv = (max(0.02, min(0.98, u)), max(0.26, min(0.98, v)))
    bm.to_mesh(helmet.data)
    bm.free()
    add_carbon_part("DriverVisor", "cube", (0, -0.095, 0.72), (0.215, 0.085, 0.068), m_visor, bevel=0.02)

    # Aero Rear-View Mirrors (mapped to VISA island u=0.84..0.90, v=0.72..0.80)
    for side, s in (("L", -1), ("R", 1)):
        add_carbon_part(f"MirrorStalk_{side}", "cube", (s * 0.43, -0.44, 0.65), (0.16, 0.032, 0.016), m_carbon)
        add_quad_panel(f"MirrorShell_{side}", [
            ((s * 0.46, -0.48, 0.66), (0.855, 0.73)),
            ((s * 0.58, -0.46, 0.66), (0.885, 0.73)),
            ((s * 0.58, -0.46, 0.715), (0.885, 0.79)),
            ((s * 0.46, -0.48, 0.715), (0.855, 0.79)),
        ], -0.055, m_livery)
        add_carbon_part(f"MirrorGlass_{side}", "cube", (s * 0.52, -0.422, 0.687), (0.11, 0.008, 0.044), m_mirror)

    # =========================================================================
    # 9. DOUBLE-WISHBONE CARBON SUSPENSION + FRONT BRAKE DUCT WINGLETS
    # =========================================================================
    for y_pos, name_p in ((-1.75, "Front"), (1.65, "Rear")):
        for side, s in (("L", -1), ("R", 1)):
            add_carbon_part(f"SuspU1_{name_p}_{side}", "cube", (s * 0.54, y_pos - 0.08, 0.41), (0.64, 0.038, 0.018), m_carbon, rot=(0, 0, math.radians(s * -8)))
            add_carbon_part(f"SuspU2_{name_p}_{side}", "cube", (s * 0.54, y_pos + 0.08, 0.41), (0.64, 0.038, 0.018), m_carbon, rot=(0, 0, math.radians(s * 8)))
            add_carbon_part(f"SuspL1_{name_p}_{side}", "cube", (s * 0.54, y_pos - 0.07, 0.25), (0.64, 0.044, 0.018), m_carbon, rot=(0, 0, math.radians(s * -6)))
            add_carbon_part(f"SuspL2_{name_p}_{side}", "cube", (s * 0.54, y_pos + 0.07, 0.25), (0.64, 0.044, 0.018), m_carbon, rot=(0, 0, math.radians(s * 6)))
            add_carbon_part(f"Pushrod_{name_p}_{side}", "cube", (s * 0.52, y_pos + 0.04, 0.34), (0.65, 0.025, 0.020), m_carbon, rot=(0, math.radians(s * -15), 0))
            add_carbon_part(f"BrakeDuct_{name_p}_{side}", "cube", (s * 0.76, y_pos, 0.36), (0.05, 0.22, 0.26), m_carbon, bevel=0.012)


def build_wheel_mesh(loc=(0, 0, 0), is_rear=False, with_preview_textures=False):
    m_rubber = make_tex_mat("TireRubber", None, base_color=(0.042, 0.044, 0.048), metallic=0.02, roughness=0.78, clearcoat=0.05)
    m_sidewall = make_tex_mat("TireSidewall", SOFT_TYRE if with_preview_textures else None, metallic=0.05, roughness=0.55, alpha_clip=True)
    m_cover = make_tex_mat("WheelCoverLivery", RB_LIVERY if with_preview_textures else None, metallic=0.35, roughness=0.25)

    w_width = 0.405 if is_rear else 0.345
    radius = 0.360

    # Smooth bevelled slick tire profile using torus-crowned cylinder
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=radius, depth=w_width, location=loc, rotation=(0, math.radians(90), 0))
    tire = bpy.context.active_object
    tire.name = f"TireSlick_{loc[0]:.2f}_{loc[1]:.2f}"
    mod = tire.modifiers.new("Bevel", "BEVEL")
    mod.width = 0.048
    mod.segments = 4
    bpy.ops.object.modifier_apply(modifier=mod.name)
    tire.data.materials.append(m_rubber)
    for p in tire.data.polygons:
        p.use_smooth = True

    objs = [tire]
    for s in (-1, 1):
        # Pirelli P-Zero Sidewall ring (UV 0..1)
        bpy.ops.mesh.primitive_circle_add(vertices=40, radius=0.342, fill_type='NGON', location=(loc[0] + s * (w_width * 0.5 + 0.002), loc[1], loc[2]), rotation=(0, math.radians(90 * s), 0))
        disc = bpy.context.active_object
        disc.data.materials.append(m_sidewall)
        bm = bmesh.new()
        bm.from_mesh(disc.data)
        uv_l = bm.loops.layers.uv.verify()
        for f in bm.faces:
            for loop in f.loops:
                co = loop.vert.co
                loop[uv_l].uv = (0.5 + co.x / 0.684, 0.5 + co.y / 0.684)
        bm.to_mesh(disc.data)
        bm.free()
        objs.append(disc)

        # 2026 Aero Wheel Cover mapped directly to Gate.io wheel cover island (u=0.081+-0.051, v=0.386+-0.051)!
        bpy.ops.mesh.primitive_circle_add(vertices=36, radius=0.228, fill_type='NGON', location=(loc[0] + s * (w_width * 0.5 + 0.005), loc[1], loc[2]), rotation=(0, math.radians(90 * s), 0))
        cov = bpy.context.active_object
        cov.data.materials.append(m_cover)
        bm = bmesh.new()
        bm.from_mesh(cov.data)
        uv_l = bm.loops.layers.uv.verify()
        for f in bm.faces:
            for loop in f.loops:
                co = loop.vert.co
                u = 0.081 + (co.x / 0.456) * 0.102
                v = 0.386 + (co.y / 0.456) * 0.102
                loop[uv_l].uv = (u, v)
        bm.to_mesh(cov.data)
        bm.free()
        objs.append(cov)
    return objs


def export_production_assets():
    # 1. Export standalone chassis (formula_car.glb & formula_car.fbx) without wheels so wheels articulate!
    build_f1_2026_replica(with_preview_textures=False)
    for d in OUT_MODEL_DIRS:
        glb_p = os.path.join(d, "formula_car.glb")
        bpy.ops.export_scene.gltf(filepath=glb_p, export_format="GLB", export_yup=True, export_apply=True, export_materials="EXPORT")
    fbx_dir = r"c:\Users\Lenovo\Downloads\motovelo\motovelo-assets\fbx"
    os.makedirs(fbx_dir, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(fbx_dir, "formula_car.fbx"), axis_forward='Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS')

    # 2. Export standalone articulated wheel (formula_wheel.glb & formula_wheel.fbx)
    clear_scene()
    build_wheel_mesh(loc=(0, 0, 0), is_rear=False, with_preview_textures=False)
    for d in OUT_MODEL_DIRS:
        bpy.ops.export_scene.gltf(filepath=os.path.join(d, "formula_wheel.glb"), export_format="GLB", export_yup=True, export_apply=True, export_materials="EXPORT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(fbx_dir, "formula_wheel.fbx"), axis_forward='Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS')


def render_beauty_verification():
    """Render 3 studio angles (Side Profile like Monoposto Car Select, 3/4 Hero, and T-Cam View!) to visually verify UVs!"""
    build_f1_2026_replica(with_preview_textures=True)
    # Add all 4 wheels at exact axles for the studio beauty render!
    build_wheel_mesh((-0.88, -1.75, 0.36), is_rear=False, with_preview_textures=True)
    build_wheel_mesh((0.88,  -1.75, 0.36), is_rear=False, with_preview_textures=True)
    build_wheel_mesh((-0.91,  1.65, 0.36), is_rear=True,  with_preview_textures=True)
    build_wheel_mesh((0.91,   1.65, 0.36), is_rear=True,  with_preview_textures=True)

    # Glossy Monoposto Studio Floor
    bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0, 0, 0))
    floor = bpy.context.active_object
    fmat = make_tex_mat("StudioFloor", None, base_color=(0.78, 0.80, 0.84), metallic=0.1, roughness=0.12)
    floor.data.materials.append(fmat)

    # Studio Lights
    for l_loc, energy in [((-5, -4, 6), 1400), ((5, 4, 6), 1100), ((0, -6, 3.5), 750), ((-6, 2, 2.5), 650)]:
        bpy.ops.object.light_add(type='AREA', location=l_loc)
        lt = bpy.context.active_object
        lt.data.energy = energy
        lt.data.size = 5.0

    world = bpy.data.worlds.new("StudioWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.72, 0.75, 0.79, 1.0)
    bg.inputs[1].default_value = 1.1

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 640

    bpy.ops.object.camera_add()
    cam = bpy.context.active_object
    scene.camera = cam

    views = [
        # 1. Exact Monoposto Car Selection Left Side Profile (facing left, full car in frame)!
        ("side_profile", (6.8, -0.15, 0.86), (math.radians(86), 0, math.radians(90)), 50),
        # 2. 3/4 Front-Left Showroom Hero!
        ("hero_34", (-4.8, -4.8, 1.55), (math.radians(74), 0, math.radians(-44)), 46),
        # 3. Exact Monoposto T-Cam View over Helmet & Halo!
        ("tcam_view", (0.0, 0.42, 1.25), (math.radians(78), 0, math.radians(180)), 24),
    ]
    for tag, pos, rot, lens in views:
        cam.location = pos
        cam.rotation_euler = rot
        cam.data.lens = lens
        out_p = os.path.join(SCRATCH, f"f1_2026_{tag}.png")
        scene.render.filepath = out_p
        bpy.ops.render.render(write_still=True)
        print("Rendered", out_p)


if __name__ == "__main__":
    export_production_assets()
    render_beauty_verification()
    print("2026 F1 REPLICA EXPORT + STUDIO RENDERS COMPLETE!")
