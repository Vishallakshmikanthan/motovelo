using System.Collections.Generic;
using UnityEngine;

namespace Motovelo
{
    public struct TrackSample
    {
        public Vector3 pos;
        public Vector3 tangent;
        public Vector3 right;
        public float s;
        public float t;
        public float curvature;
    }

    public struct TrackProjection
    {
        public int index;
        public float s;
        public float t;
        public float lateralOffset;
        public Vector3 point;
        public Vector3 tangent;
        public Vector3 right;
        public float curvature;
    }

    public class SplineCircuit : MonoBehaviour
    {
        public TrackSample[] samples;
        public float totalLength;
        public float trackWidth = 16f;
        public float halfWidth = 8f;
        public List<Vector3> broadcastCameras = new List<Vector3>();
        public List<Renderer> startLightRenderers = new List<Renderer>();
        public bool showRacingLine = true;

        private GameObject _racingLineRoot;

        public void BuildFromDefinition(
            CircuitJsonEntry def,
            GameObject grandstandPrefab,
            GameObject pitPrefab,
            GameObject gantryPrefab)
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            startLightRenderers.Clear();
            broadcastCameras.Clear();

            trackWidth = Mathf.Max(13.5f, def.trackWidth);
            halfWidth = trackWidth * 0.5f;

            // Parse & scale control points from bacinger/f1-circuits GPS meters (~1.48x for realistic 5km+ F1 scale)
            int ctrlCount = def.points.Length / 3;
            var rawCtrl = new Vector3[ctrlCount];
            const float gpsScale = 1.48f;
            for (int i = 0; i < ctrlCount; i++)
                rawCtrl[i] = new Vector3(def.points[i * 3] * gpsScale, def.points[i * 3 + 1], def.points[i * 3 + 2] * gpsScale);

            // Gentle 2-pass polyline smoothing so GeoJSON vertices flow like real F1 tarmac
            for (int pass = 0; pass < 2; pass++)
            {
                var nextCtrl = new Vector3[ctrlCount];
                for (int i = 0; i < ctrlCount; i++)
                {
                    Vector3 prevP = rawCtrl[(i - 1 + ctrlCount) % ctrlCount];
                    Vector3 currP = rawCtrl[i];
                    Vector3 nextP = rawCtrl[(i + 1) % ctrlCount];
                    nextCtrl[i] = prevP * 0.2f + currP * 0.6f + nextP * 0.2f;
                }
                rawCtrl = nextCtrl;
            }

            // Auto-align index 0 onto the circuit's longest straight so Start Gantry & 22-car Grid never sit in a hairpin!
            int win = Mathf.Clamp(ctrlCount / 7, 6, 28);
            int bestStartIdx = 0;
            float bestStraightScore = float.MaxValue;
            for (int i = 0; i < ctrlCount; i++)
            {
                float turnSum = 0f;
                float segLenSum = 0f;
                for (int k = -win; k <= 2; k++)
                {
                    Vector3 a = rawCtrl[(i + k - 1 + ctrlCount * 2) % ctrlCount];
                    Vector3 b = rawCtrl[(i + k + ctrlCount * 2) % ctrlCount];
                    Vector3 c = rawCtrl[(i + k + 1 + ctrlCount * 2) % ctrlCount];
                    turnSum += Vector3.Angle(b - a, c - b);
                    segLenSum += Vector3.Distance(a, b);
                }
                float score = turnSum / Mathf.Max(20f, segLenSum);
                if (score < bestStraightScore)
                {
                    bestStraightScore = score;
                    bestStartIdx = i;
                }
            }

            var ctrl = new Vector3[ctrlCount];
            for (int i = 0; i < ctrlCount; i++)
                ctrl[i] = rawCtrl[(bestStartIdx + i) % ctrlCount];

            // Resample closed Catmull-Rom spline to 960 uniform samples
            const int N = 960;
            var raw = EvaluateClosedCatmullRom(ctrl, N);
            samples = new TrackSample[N];
            float cum = 0f;
            Vector3 center = Vector3.zero;

            for (int i = 0; i < N; i++)
            {
                Vector3 p = raw[i];
                Vector3 pPrev = raw[(i - 1 + N) % N];
                Vector3 pNext = raw[(i + 1) % N];
                if (i > 0) cum += Vector3.Distance(p, pPrev);

                Vector3 tan = (pNext - pPrev).normalized;
                if (tan.sqrMagnitude < 0.001f) tan = Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, tan).normalized;

                Vector3 t1 = (p - pPrev).normalized;
                Vector3 t2 = (pNext - p).normalized;
                float ang = Vector3.Angle(t1, t2) * Mathf.Deg2Rad;
                float seg = Mathf.Max(0.5f, Vector3.Distance(p, pPrev));

                samples[i] = new TrackSample
                {
                    pos = p,
                    tangent = tan,
                    right = right,
                    s = cum,
                    t = (float)i / N,
                    curvature = ang / seg
                };
                center += p;
            }
            totalLength = cum + Vector3.Distance(raw[N - 1], raw[0]);
            center /= N;

            // 1. Ground Terrain
            ColorUtility.TryParseHtmlString(def.groundColor, out Color grdCol);
            var groundGo = MotoveloDatabase.CreatePlaneObject("GroundTerrain", transform);
            groundGo.transform.position = new Vector3(center.x, -0.45f, center.z);
            groundGo.transform.localScale = new Vector3(520f, 1f, 520f);
            groundGo.GetComponent<Renderer>().sharedMaterial = MotoveloDatabase.CreateUrpLit(grdCol, null, 0.02f, 0.12f);

            // 2. Asphalt Road Ribbon
            var asphaltTex = CreateAsphaltTexture();
            var asphaltMat = MotoveloDatabase.CreateUrpLit(Color.white, asphaltTex, 0.06f, 0.32f);
            CreateRibbonMesh("AsphaltRoad", -halfWidth, halfWidth, 0.02f, 0.08f, asphaltMat);

            // 3. Red & White Formula Kerbs
            var kerbTex = CreateKerbTexture();
            var kerbMat = MotoveloDatabase.CreateUrpLit(Color.white, kerbTex, 0.05f, 0.45f);
            CreateRibbonMesh("KerbLeft", -halfWidth - 1.45f, -halfWidth + 0.05f, 0.045f, 0.24f, kerbMat);
            CreateRibbonMesh("KerbRight", halfWidth - 0.05f, halfWidth + 1.45f, 0.045f, 0.24f, kerbMat);

            // 4. Tarmac Runoff Aprons
            if (!ColorUtility.TryParseHtmlString(def.runoffColor, out Color runCol))
                runCol = new Color(0.25f, 0.28f, 0.32f);
            var runoffMat = MotoveloDatabase.CreateUrpLit(runCol, null, 0.04f, 0.20f);
            CreateRibbonMesh("RunoffLeft", -halfWidth - 6.2f, -halfWidth - 1.4f, 0.0f, 0.05f, runoffMat);
            CreateRibbonMesh("RunoffRight", halfWidth + 1.4f, halfWidth + 6.2f, 0.0f, 0.05f, runoffMat);

            // 5. Real Sponsor Barriers (Download.zip sponsors — sleek 0.92m F1 height, non-mirrored UVs, hairpin-safe!)
            var aramcoTex = MotoveloDatabase.LoadTex("Textures/sponsors/aramco");
            var f1Tex = MotoveloDatabase.LoadTex("Textures/sponsors/f1madrid");
            if (aramcoTex != null) aramcoTex.wrapMode = TextureWrapMode.Repeat;
            if (f1Tex != null) f1Tex.wrapMode = TextureWrapMode.Repeat;
            var wallMatL = MotoveloDatabase.CreateUrpLit(Color.white, aramcoTex, 0.12f, 0.55f);
            var wallMatR = MotoveloDatabase.CreateUrpLit(Color.white, f1Tex, 0.12f, 0.55f);
            CreateWallMesh("BarrierLeft", -halfWidth - 4.4f, 0f, 0.92f, false, -0.09f, wallMatL);
            CreateWallMesh("BarrierRight", halfWidth + 4.4f, 0f, 0.92f, true, 0.09f, wallMatR);

            // 6. Start/Finish Checkered Line
            var startSmp = samples[0];
            float startYaw = Mathf.Atan2(startSmp.tangent.x, startSmp.tangent.z) * Mathf.Rad2Deg;
            var startQuad = MotoveloDatabase.CreateQuadObject("StartFinishLine", transform);
            startQuad.transform.position = startSmp.pos + Vector3.up * 0.055f;
            startQuad.transform.rotation = Quaternion.Euler(90f, startYaw, 0f);
            startQuad.transform.localScale = new Vector3(trackWidth - 0.8f, 3.0f, 1f);
            startQuad.GetComponent<Renderer>().sharedMaterial = MotoveloDatabase.CreateUrpLit(Color.white, CreateCheckeredTexture(), 0.05f, 0.5f);

            // 7. Start Gantry + Dynamic Lights
            if (gantryPrefab != null)
            {
                var gantry = Instantiate(gantryPrefab, transform);
                gantry.name = "StartGantry";
                gantry.transform.position = startSmp.pos;
                gantry.transform.rotation = Quaternion.Euler(0f, startYaw, 0f);
                float gScale = (trackWidth + 5.8f) / 21.6f;
                gantry.transform.localScale = new Vector3(gScale, 1f, 1f);
                var lvTex = MotoveloDatabase.LoadTex("Textures/sponsors/louisvuitton");

                foreach (var r in gantry.GetComponentsInChildren<Renderer>())
                {
                    string mName = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mName.Contains("GantryBanner") && lvTex != null)
                    {
                        r.material = MotoveloDatabase.CreateUrpLit(Color.white, lvTex, 0.1f, 0.55f);
                    }
                    else if (mName.Contains("StartLight_") || r.gameObject.name.Contains("Bulb"))
                    {
                        var bulbMat = MotoveloDatabase.CreateUrpEmissive(new Color(0.18f, 0.02f, 0.02f), Color.black, 0.8f);
                        r.material = bulbMat;
                        startLightRenderers.Add(r);
                    }
                    else
                    {
                        r.material = MotoveloDatabase.CreateUrpLit(new Color(0.15f, 0.17f, 0.20f), null, 0.7f, 0.65f);
                    }
                }
            }

            // 8. Pit Complex & Grandstands
            if (pitPrefab != null)
            {
                var ps = samples[Mathf.RoundToInt(N * 0.025f)];
                float pyaw = Mathf.Atan2(ps.tangent.x, ps.tangent.z) * Mathf.Rad2Deg;
                var pit = Instantiate(pitPrefab, transform);
                pit.transform.position = ps.pos + ps.right * (halfWidth + 15.5f);
                pit.transform.rotation = Quaternion.Euler(0f, pyaw - 90f, 0f);
                UpgradeImportedMaterials(pit);
            }

            if (grandstandPrefab != null)
            {
                float[] fracs = { 0.015f, 0.07f, 0.22f, 0.38f, 0.56f, 0.74f, 0.90f };
                for (int k = 0; k < fracs.Length; k++)
                {
                    var gsSmp = samples[Mathf.RoundToInt(N * fracs[k]) % N];
                    float gyaw = Mathf.Atan2(gsSmp.tangent.x, gsSmp.tangent.z) * Mathf.Rad2Deg;
                    float side = k % 2 == 0 ? -1f : 1f;
                    var gs = Instantiate(grandstandPrefab, transform);
                    gs.transform.position = gsSmp.pos + gsSmp.right * (side * (halfWidth + 16.5f));
                    gs.transform.rotation = Quaternion.Euler(0f, gyaw + (side < 0 ? 90f : -90f), 0f);
                    UpgradeImportedMaterials(gs);
                }
            }

            // 9. Dynamic 3D Racing Line Chevrons + 150/100/50m Braking Marker Boards
            _racingLineRoot = new GameObject("RacingLineChevrons");
            _racingLineRoot.transform.SetParent(transform, false);

            var matGreen = MotoveloDatabase.CreateUrpEmissive(new Color(0.10f, 0.85f, 0.30f), new Color(0.15f, 1.25f, 0.42f), 0.6f);
            var matAmber = MotoveloDatabase.CreateUrpEmissive(new Color(0.95f, 0.78f, 0.08f), new Color(1.35f, 1.05f, 0.10f), 0.6f);
            var matRed   = MotoveloDatabase.CreateUrpEmissive(new Color(0.95f, 0.14f, 0.16f), new Color(1.55f, 0.18f, 0.20f), 0.6f);

            var mat150 = MotoveloDatabase.CreateUrpLit(Color.white, CreateBrakeBoardTex("150"), 0.05f, 0.5f);
            var mat100 = MotoveloDatabase.CreateUrpLit(Color.white, CreateBrakeBoardTex("100"), 0.05f, 0.5f);
            var mat50  = MotoveloDatabase.CreateUrpLit(Color.white, CreateBrakeBoardTex("50"),  0.05f, 0.5f);

            float lastBoardS = -300f;
            for (int i = 6; i < N; i += 8)
            {
                var smp = samples[i];
                float maxCurv = 0f;
                for (int look = 0; look < 24; look++)
                    maxCurv = Mathf.Max(maxCurv, samples[(i + look) % N].curvature);

                var cQuad = MotoveloDatabase.CreateQuadObject("RaceChevron", _racingLineRoot.transform);
                float yaw = Mathf.Atan2(smp.tangent.x, smp.tangent.z) * Mathf.Rad2Deg;
                cQuad.transform.position = smp.pos + Vector3.up * 0.05f;
                cQuad.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                cQuad.transform.localScale = new Vector3(1.5f, 3.2f, 1f);
                cQuad.GetComponent<Renderer>().sharedMaterial = maxCurv > 0.021f ? matRed : (maxCurv > 0.011f ? matAmber : matGreen);

                if (smp.curvature > 0.024f && smp.s - lastBoardS > 240f)
                {
                    lastBoardS = smp.s;
                    SpawnBrakeBoard(smp.s - 110f, mat150);
                    SpawnBrakeBoard(smp.s - 75f, mat100);
                    SpawnBrakeBoard(smp.s - 40f, mat50);
                }
            }
            _racingLineRoot.SetActive(showRacingLine);

            // 10. Broadcast Cameras
            for (int i = 0; i < 18; i++)
            {
                var cs = samples[(i * N / 18) % N];
                float side = i % 2 == 0 ? 1f : -1f;
                broadcastCameras.Add(cs.pos + cs.right * (side * (halfWidth + 8.5f)) + Vector3.up * 4.5f);
            }
        }

        public void SetRacingLineVisible(bool visible)
        {
            showRacingLine = visible;
            if (_racingLineRoot != null) _racingLineRoot.SetActive(visible);
        }

        public void SetStartLights(int litPods)
        {
            // Each pod has 2 bulbs (top & bottom) -> 10 bulb renderers total
            for (int i = 0; i < startLightRenderers.Count; i++)
            {
                int podIdx = i / 2;
                bool lit = podIdx < litPods;
                var mat = startLightRenderers[i].material;
                Color col = lit ? new Color(1f, 0.08f, 0.12f) : new Color(0.14f, 0.02f, 0.02f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", lit ? new Color(3.5f, 0.2f, 0.25f) : Color.black);
            }
        }

        public TrackProjection Project(Vector3 worldPos, int hintIdx = -1)
        {
            int n = samples.Length;
            int bestIdx = 0;
            float bestD2 = float.MaxValue;

            if (hintIdx >= 0)
            {
                for (int k = -44; k <= 44; k++)
                {
                    int idx = (hintIdx + k + n) % n;
                    Vector3 p = samples[idx].pos;
                    float dx = worldPos.x - p.x;
                    float dz = worldPos.z - p.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < bestD2) { bestD2 = d2; bestIdx = idx; }
                }
            }

            if (bestD2 > 1800f)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = samples[i].pos;
                    float dx = worldPos.x - p.x;
                    float dz = worldPos.z - p.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < bestD2) { bestD2 = d2; bestIdx = i; }
                }
            }

            var smp = samples[bestIdx];
            float lx = worldPos.x - smp.pos.x;
            float lz = worldPos.z - smp.pos.z;
            float lat = lx * smp.right.x + lz * smp.right.z;

            return new TrackProjection
            {
                index = bestIdx,
                s = smp.s,
                t = smp.t,
                lateralOffset = lat,
                point = smp.pos,
                tangent = smp.tangent,
                right = smp.right,
                curvature = smp.curvature
            };
        }

        public TrackSample GetSampleAtDistance(float s)
        {
            int n = samples.Length;
            float wrapped = ((s % totalLength) + totalLength) % totalLength;
            int idx = Mathf.FloorToInt((wrapped / totalLength) * n) % n;
            return samples[idx];
        }

        private void SpawnBrakeBoard(float distS, Material mat)
        {
            var bs = GetSampleAtDistance(distS);
            float byaw = Mathf.Atan2(bs.tangent.x, bs.tangent.z) * Mathf.Rad2Deg;
            var cube = MotoveloDatabase.CreateCubeObject("BrakeBoard", transform);
            cube.transform.position = bs.pos - bs.right * (halfWidth + 4.2f) + Vector3.up * 1.0f;
            cube.transform.rotation = Quaternion.Euler(0f, byaw + 180f, 0f);
            cube.transform.localScale = new Vector3(1.5f, 1.0f, 0.14f);
            cube.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private void UpgradeImportedMaterials(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial != null)
                {
                    Color c = r.sharedMaterial.HasProperty("_BaseColor")
                        ? r.sharedMaterial.GetColor("_BaseColor")
                        : r.sharedMaterial.color;
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(c, null, 0.15f, 0.55f);
                }
            }
        }

        private void CreateRibbonMesh(string meshName, float leftOff, float rightOff, float yOff, float vRepeat, Material mat)
        {
            int n = samples.Length;
            var verts = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[(n + 1) * 2];
            var tris = new int[n * 6];

            for (int i = 0; i <= n; i++)
            {
                var smp = samples[i % n];
                float s = i == n ? totalLength : smp.s;
                verts[i * 2]     = smp.pos + smp.right * leftOff  + Vector3.up * yOff;
                verts[i * 2 + 1] = smp.pos + smp.right * rightOff + Vector3.up * yOff;
                uvs[i * 2]     = new Vector2(0f, s * vRepeat);
                uvs[i * 2 + 1] = new Vector2(1f, s * vRepeat);

                if (i < n)
                {
                    int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                    int ti = i * 6;
                    tris[ti] = a; tris[ti + 1] = c; tris[ti + 2] = b;
                    tris[ti + 3] = b; tris[ti + 4] = c; tris[ti + 5] = d;
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            var go = new GameObject(meshName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private void CreateWallMesh(string meshName, float offset, float yBase, float height, bool flip, float uRepeat, Material mat)
        {
            int n = samples.Length;
            var verts = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[(n + 1) * 2];
            var tris = new int[n * 6];

            for (int i = 0; i <= n; i++)
            {
                var smp = samples[i % n];
                float s = i == n ? totalLength : smp.s;
                // Smooth normal across neighbors so inner hairpin walls never fold across the asphalt!
                Vector3 smoothRight = (
                    samples[(i - 4 + n) % n].right +
                    samples[(i - 2 + n) % n].right +
                    smp.right +
                    samples[(i + 2) % n].right +
                    samples[(i + 4) % n].right).normalized;
                if (smoothRight.sqrMagnitude < 0.01f) smoothRight = smp.right;

                Vector3 basePos = smp.pos + smoothRight * offset + Vector3.up * yBase;
                verts[i * 2]     = basePos;
                verts[i * 2 + 1] = basePos + Vector3.up * height;
                uvs[i * 2]     = new Vector2(s * uRepeat, 0f);
                uvs[i * 2 + 1] = new Vector2(s * uRepeat, 1f);

                if (i < n)
                {
                    int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                    int ti = i * 6;
                    if (flip)
                    {
                        tris[ti] = a; tris[ti + 1] = b; tris[ti + 2] = c;
                        tris[ti + 3] = b; tris[ti + 4] = d; tris[ti + 5] = c;
                    }
                    else
                    {
                        tris[ti] = a; tris[ti + 1] = c; tris[ti + 2] = b;
                        tris[ti + 3] = b; tris[ti + 4] = c; tris[ti + 5] = d;
                    }
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            var go = new GameObject(meshName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static Vector3[] EvaluateClosedCatmullRom(Vector3[] pts, int totalSamples)
        {
            int m = pts.Length;
            var result = new Vector3[totalSamples];
            for (int s = 0; s < totalSamples; s++)
            {
                float u = ((float)s / totalSamples) * m;
                int idx = Mathf.FloorToInt(u) % m;
                float t = u - Mathf.Floor(u);
                Vector3 p0 = pts[(idx - 1 + m) % m];
                Vector3 p1 = pts[idx];
                Vector3 p2 = pts[(idx + 1) % m];
                Vector3 p3 = pts[(idx + 2) % m];

                float t2 = t * t;
                float t3 = t2 * t;
                result[s] = 0.5f * (
                    (2f * p1) +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                );
            }
            return result;
        }

        private static Texture2D CreateAsphaltTexture()
        {
            var tex = new Texture2D(256, 256, TextureFormat.RGB24, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            var rng = new System.Random(42);
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    float n = 0.14f + (float)rng.NextDouble() * 0.06f;
                    // Darker rubbered racing groove in center
                    float centerDist = Mathf.Abs(x - 128f) / 128f;
                    if (centerDist < 0.45f) n *= 0.85f;
                    Color c = new Color(n, n, n + 0.01f);
                    if (x >= 6 && x <= 11 || x >= 244 && x <= 249)
                        c = new Color(0.92f, 0.94f, 0.96f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateKerbTexture()
        {
            var tex = new Texture2D(32, 128, TextureFormat.RGB24, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < 128; y++)
            {
                Color c = y < 64 ? new Color(0.86f, 0.14f, 0.12f) : new Color(0.95f, 0.96f, 0.98f);
                for (int x = 0; x < 32; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateCheckeredTexture()
        {
            var tex = new Texture2D(128, 32, TextureFormat.RGB24, false);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 128; x++)
                    tex.SetPixel(x, y, ((x / 8 + y / 8) % 2 == 0) ? Color.white : new Color(0.08f, 0.08f, 0.09f));
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateBrakeBoardTex(string num)
        {
            var tex = new Texture2D(64, 48, TextureFormat.RGB24, false);
            Color bg = num == "50" ? new Color(0.90f, 0.12f, 0.15f) : (num == "100" ? new Color(0.95f, 0.75f, 0.08f) : Color.white);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 64; x++)
                {
                    bool border = x < 4 || x > 59 || y < 4 || y > 43;
                    tex.SetPixel(x, y, border ? new Color(0.08f, 0.09f, 0.12f) : bg);
                }
            tex.Apply();
            return tex;
        }
    }
}
