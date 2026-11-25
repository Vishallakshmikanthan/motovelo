using System;
using System.Collections.Generic;
using UnityEngine;

namespace Motovelo
{
    [Serializable]
    public class CircuitJsonEntry
    {
        public string id;
        public string name;
        public string country;
        public string flagCode;
        public string description;
        public int lengthM;
        public float trackWidth;
        public string skyTop;
        public string skyHorizon;
        public string groundColor;
        public string runoffColor;
        public bool hasWater;
        public float[] points;
    }

    [Serializable]
    public class CircuitJsonRoot
    {
        public CircuitJsonEntry[] circuits;
    }

    public class TeamDef
    {
        public string id;
        public string name;
        public string shortCode;
        public Color primaryColor;
        public float power;
        public float aero;
        public float grip;

        public TeamDef(string id, string name, string shortCode, string hex, float power, float aero, float grip)
        {
            this.id = id;
            this.name = name;
            this.shortCode = shortCode;
            ColorUtility.TryParseHtmlString(hex, out this.primaryColor);
            this.power = power;
            this.aero = aero;
            this.grip = grip;
        }
    }

    public class DriverDef
    {
        public string id;
        public string name;
        public string code;
        public int number;
        public string country;
        public string teamId;
        public string helmetId;
        public string suitId;

        public DriverDef(string id, string name, string code, int number, string country, string teamId, string helmetId, string suitId)
        {
            this.id = id;
            this.name = name;
            this.code = code;
            this.number = number;
            this.country = country;
            this.teamId = teamId;
            this.helmetId = helmetId;
            this.suitId = suitId;
        }
    }

    public class CompoundDef
    {
        public string id;
        public string label;
        public string shortLetter;
        public Color color;
        public float gripMult;
        public float wearRate;

        public CompoundDef(string id, string label, string shortLetter, string hex, float gripMult, float wearRate)
        {
            this.id = id;
            this.label = label;
            this.shortLetter = shortLetter;
            ColorUtility.TryParseHtmlString(hex, out this.color);
            this.gripMult = gripMult;
            this.wearRate = wearRate;
        }
    }

    public static class MotoveloDatabase
    {
        public static readonly TeamDef[] Teams = new TeamDef[]
        {
            new TeamDef("redbull",     "Red Bull Racing",   "RBR", "#101E4A", 1.04f, 1.04f, 1.03f),
            new TeamDef("ferrari",     "Scuderia Ferrari",  "FER", "#DC1428", 1.04f, 1.03f, 1.03f),
            new TeamDef("mclaren",     "McLaren Formula 1", "MCL", "#FF7800", 1.03f, 1.04f, 1.04f),
            new TeamDef("mercedes",    "Mercedes-AMG F1",   "MER", "#00D2BE", 1.03f, 1.02f, 1.02f),
            new TeamDef("astonmartin", "Aston Martin F1",   "AST", "#00594F", 1.01f, 1.01f, 1.01f),
            new TeamDef("alpine",      "Alpine F1 Team",    "ALP", "#0078D4", 1.00f, 1.00f, 1.00f),
            new TeamDef("williams",    "Williams Racing",   "WIL", "#0046C8", 1.00f, 1.00f, 1.00f),
            new TeamDef("vcarb",       "Racing Bulls",      "VCB", "#2B56E8", 1.00f, 1.00f, 1.00f),
            new TeamDef("audi",        "Audi Revolut F1",   "AUD", "#D21828", 0.99f, 0.99f, 1.00f),
            new TeamDef("haas",        "Haas F1 Team",      "HAS", "#E2202C", 0.99f, 0.99f, 0.99f),
            new TeamDef("cadillac",    "Cadillac F1 Team",  "CAD", "#C9A227", 0.99f, 0.99f, 0.99f),
        };

        public static readonly DriverDef[] Drivers = new DriverDef[]
        {
            new DriverDef("verstappen", "Max Verstappen",    "VER", 3,  "Netherlands", "redbull",     "verstappen", "redbull"),
            new DriverDef("hadjar",     "Isack Hadjar",      "HAD", 6,  "France",      "redbull",     "hadjar",     "redbull"),
            new DriverDef("leclerc",    "Charles Leclerc",   "LEC", 16, "Monaco",      "ferrari",     "leclerc",    "ferrari"),
            new DriverDef("hamilton",   "Lewis Hamilton",    "HAM", 44, "UK",          "ferrari",     "hamilton",   "ferrari"),
            new DriverDef("piastri",    "Oscar Piastri",     "PIA", 81, "Australia",   "mclaren",     "piastri",    "mclaren"),
            new DriverDef("norris",     "Lando Norris",      "NOR", 1,  "UK",          "mclaren",     "norris",     "mclaren"),
            new DriverDef("antonelli",  "Kimi Antonelli",    "ANT", 12, "Italy",       "mercedes",    "antonelli",  "mercedes"),
            new DriverDef("russell",    "George Russell",    "RUS", 63, "UK",          "mercedes",    "russell",    "mercedes"),
            new DriverDef("alonso",     "Fernando Alonso",   "ALO", 14, "Spain",       "astonmartin", "alonso",     "astonmartin"),
            new DriverDef("stroll",     "Lance Stroll",      "STR", 18, "Canada",      "astonmartin", "stroll",     "astonmartin"),
            new DriverDef("gasly",      "Pierre Gasly",      "GAS", 10, "France",      "alpine",      "alpine",     "alpine"),
            new DriverDef("colapinto",  "Franco Colapinto",  "COL", 43, "Argentina",   "alpine",      "alpine",     "alpine"),
            new DriverDef("albon",      "Alex Albon",        "ALB", 23, "Thailand",    "williams",    "williams",   "williams"),
            new DriverDef("sainz",      "Carlos Sainz",      "SAI", 55, "Spain",       "williams",    "williams",   "williams"),
            new DriverDef("lawson",     "Liam Lawson",       "LAW", 30, "New Zealand", "vcarb",       "vcarb",      "vcarb"),
            new DriverDef("lindblad",   "Arvid Lindblad",    "ARV", 41, "UK",          "vcarb",       "vcarb",      "vcarb"),
            new DriverDef("hulkenberg", "Nico Hulkenberg",   "HUL", 27, "Germany",     "audi",        "haas",       "audi"),
            new DriverDef("bortoleto",  "Gabriel Bortoleto", "BOR", 5,  "Brazil",      "audi",        "piastri",    "audi"),
            new DriverDef("ocon",       "Esteban Ocon",      "OCO", 31, "France",      "haas",        "haas",       "haas"),
            new DriverDef("bearman",    "Ollie Bearman",     "BEA", 87, "UK",          "haas",        "haas",       "haas"),
            new DriverDef("bottas",     "Valtteri Bottas",   "BOT", 77, "Finland",     "cadillac",    "russell",    "cadillac"),
            new DriverDef("perez",      "Sergio Perez",      "PER", 11, "Mexico",      "cadillac",    "verstappen", "cadillac"),
        };

        public static readonly string[] HelmetIds = new string[]
        {
            "verstappen", "verstappen_cota", "hadjar", "leclerc", "hamilton",
            "russell", "antonelli", "norris", "piastri", "alonso",
            "stroll", "williams", "alpine", "haas", "vcarb"
        };

        public static readonly string[] SuitIds = new string[]
        {
            "redbull", "ferrari", "mclaren", "mercedes", "astonmartin",
            "alpine", "williams", "vcarb", "audi", "haas", "cadillac"
        };

        public static readonly CompoundDef[] Compounds = new CompoundDef[]
        {
            new CompoundDef("soft",  "SOFT",  "S", "#FF2432", 1.06f, 1.40f),
            new CompoundDef("mid",   "MED",   "M", "#F5CC16", 1.01f, 1.00f),
            new CompoundDef("hard",  "HARD",  "H", "#E8ECF2", 0.97f, 0.65f),
            new CompoundDef("inter", "INTER", "I", "#22D44C", 0.93f, 0.85f),
            new CompoundDef("wet",   "WET",   "W", "#1B8CFF", 0.89f, 0.75f),
        };

        public static readonly int[] F1Points = new int[] { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };

        private static CircuitJsonEntry[] _circuits;
        public static CircuitJsonEntry[] Circuits
        {
            get
            {
                if (_circuits == null)
                {
                    var ta = Resources.Load<TextAsset>("Data/circuits_2026");
                    if (ta != null)
                    {
                        var root = JsonUtility.FromJson<CircuitJsonRoot>(ta.text);
                        _circuits = root != null && root.circuits != null ? root.circuits : Array.Empty<CircuitJsonEntry>();
                    }
                    else
                    {
                        _circuits = Array.Empty<CircuitJsonEntry>();
                    }
                }
                return _circuits;
            }
        }

        public static TeamDef GetTeam(string id)
        {
            foreach (var t in Teams)
                if (t.id == id) return t;
            return Teams[0];
        }

        public static List<DriverDef> GetTeamDrivers(string teamId)
        {
            var list = new List<DriverDef>();
            foreach (var d in Drivers)
                if (d.teamId == teamId) list.Add(d);
            return list;
        }

        private static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();
        public static Texture2D LoadTex(string resourcePath)
        {
            if (_texCache.TryGetValue(resourcePath, out var cached) && cached != null)
                return cached;
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null) _texCache[resourcePath] = tex;
            return tex;
        }

        public static Material CreateUrpLit(Color color, Texture2D mainTex = null, float metallic = 0.2f, float smoothness = 0.7f, bool alphaClip = false)
        {
            string resName = alphaClip ? "Materials/UrpAlphaClipTemplate" : "Materials/UrpOpaqueTemplate";
            Material tmpl = Resources.Load<Material>(resName);
            Material mat;
            if (tmpl != null)
            {
                mat = new Material(tmpl);
            }
            else
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                mat = new Material(sh);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
            mat.mainTexture = mainTex;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (alphaClip && mat.HasProperty("_AlphaClip"))
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.2f);
                mat.EnableKeyword("_ALPHATEST_ON");
            }
            return mat;
        }

        public static Material CreateUrpTransparent(Color color, float metallic = 0.08f, float smoothness = 0.88f)
        {
            Material tmpl = Resources.Load<Material>("Materials/UrpTransparentTemplate");
            Material mat = tmpl != null ? new Material(tmpl) : CreateUrpLit(color, null, metallic, smoothness, false);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            mat.renderQueue = 3000;
            return mat;
        }

        public static Material CreateUrpEmissive(Color baseColor, Color emissionHdr, float smoothness = 0.8f)
        {
            Material tmpl = Resources.Load<Material>("Materials/UrpEmissiveTemplate");
            Material mat = tmpl != null ? new Material(tmpl) : CreateUrpLit(baseColor, null, 0.05f, smoothness, false);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            mat.mainTexture = null;
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionHdr);
            return mat;
        }

        private static Mesh _unitQuadMesh;
        private static Mesh _unitPlaneMesh;
        private static Mesh _unitCubeMesh;

        public static GameObject CreatePlaneObject(string name, Transform parent)
        {
            if (_unitPlaneMesh == null)
            {
                _unitPlaneMesh = new Mesh { name = "MotoveloPlane10x10" };
                _unitPlaneMesh.vertices = new[]
                {
                    new Vector3(-5f, 0f, -5f),
                    new Vector3( 5f, 0f, -5f),
                    new Vector3(-5f, 0f,  5f),
                    new Vector3( 5f, 0f,  5f)
                };
                _unitPlaneMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                _unitPlaneMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                _unitPlaneMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            }
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _unitPlaneMesh;
            go.AddComponent<MeshRenderer>();
            return go;
        }

        public static GameObject CreateQuadObject(string name, Transform parent)
        {
            if (_unitQuadMesh == null)
            {
                _unitQuadMesh = new Mesh { name = "MotoveloQuad" };
                _unitQuadMesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3( 0.5f, -0.5f, 0f),
                    new Vector3(-0.5f,  0.5f, 0f),
                    new Vector3( 0.5f,  0.5f, 0f)
                };
                _unitQuadMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                _unitQuadMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                _unitQuadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            }
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _unitQuadMesh;
            go.AddComponent<MeshRenderer>();
            return go;
        }

        public static GameObject CreateCubeObject(string name, Transform parent)
        {
            if (_unitCubeMesh == null)
            {
                _unitCubeMesh = new Mesh { name = "MotoveloCube" };
                Vector3 p0 = new Vector3(-0.5f, -0.5f,  0.5f);
                Vector3 p1 = new Vector3( 0.5f, -0.5f,  0.5f);
                Vector3 p2 = new Vector3( 0.5f, -0.5f, -0.5f);
                Vector3 p3 = new Vector3(-0.5f, -0.5f, -0.5f);
                Vector3 p4 = new Vector3(-0.5f,  0.5f,  0.5f);
                Vector3 p5 = new Vector3( 0.5f,  0.5f,  0.5f);
                Vector3 p6 = new Vector3( 0.5f,  0.5f, -0.5f);
                Vector3 p7 = new Vector3(-0.5f,  0.5f, -0.5f);
                _unitCubeMesh.vertices = new[]
                {
                    p0, p1, p5, p4, // Front (+Z)
                    p2, p3, p7, p6, // Back (-Z)
                    p3, p0, p4, p7, // Left (-X)
                    p1, p2, p6, p5, // Right (+X)
                    p4, p5, p6, p7, // Top (+Y)
                    p3, p2, p1, p0  // Bottom (-Y)
                };
                var uvs = new Vector2[24];
                var tris = new int[36];
                for (int f = 0; f < 6; f++)
                {
                    int v = f * 4;
                    uvs[v] = new Vector2(0, 0); uvs[v + 1] = new Vector2(1, 0);
                    uvs[v + 2] = new Vector2(1, 1); uvs[v + 3] = new Vector2(0, 1);
                    int t = f * 6;
                    tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                    tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
                }
                _unitCubeMesh.uv = uvs;
                _unitCubeMesh.triangles = tris;
                _unitCubeMesh.RecalculateNormals();
            }
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _unitCubeMesh;
            go.AddComponent<MeshRenderer>();
            return go;
        }
    }
}
