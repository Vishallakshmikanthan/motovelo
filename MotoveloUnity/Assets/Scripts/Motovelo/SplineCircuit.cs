using System.Collections.Generic;
using UnityEngine;

namespace Motovelo
{
    public struct TrackSample
    {
        public Vector3 pos;
        public Vector3 tangent;
        public Vector3 right;
        public Vector3 up;
        public float s;
        public float t;
        public float curvature;
        public float signedCurvature;
        public float slopeRad;
        public float bankRad;
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
        public Vector3 up;
        public float curvature;
        public float slopeRad;
        public float bankRad;
    }

    public struct CircuitZone
    {
        public float detectS;
        public float startS;
        public float endS;
    }

    public class SplineCircuit : MonoBehaviour
    {
        public TrackSample[] samples;
        public float totalLength;
        public float trackWidth = 15f;
        public float halfWidth = 7.5f;
        public List<Vector3> broadcastCameras = new List<Vector3>();
        public List<Renderer> startLightRenderers = new List<Renderer>();
        public List<CircuitZone> drsZones = new List<CircuitZone>();
        public List<CircuitZone> ersHarvestZones = new List<CircuitZone>();
        public bool showRacingLine = true;

        // Physical FIA Pit Lane & Waiting 3D Pit Crew Properties
        public float pitSide = 1f;            // +1 right of main straight, -1 left of main straight
        public float pitLaneOffset = 16.3f;   // Lateral offset (signed) of Pit Lane driving centerline
        public float pitBoxSignedS = 32f;     // Player's garage pit box distance past s = 0
        public PitStopCrewController playerPitCrew;

        private GameObject _racingLineRoot;

        private static float GetTargetElevationMeters(string circuitId)
        {
            switch (circuitId)
            {
                case "heritage-circuit": return 42f; // Spa-Francorchamps (Eau Rouge / Raidillon!)
                case "summit-ring":      return 44f; // Red Bull Ring (Steep Alpine climb!)
                case "lone-star":        return 36f; // COTA Austin (Turn 1 hill!)
                case "cypress-hills":    return 29f; // Suzuka (S-Curves & Dunlop crest!)
                case "royal-gardens":    return 27f; // Monaco (Beau Rivage / Casino climb!)
                case "torque-valley":    return 25f; // Imola (Acque Minerali!)
                case "iberian-circuit":  return 24f; // Barcelona (Turn 3-4 climb!)
                case "danube-park":      return 23f; // Hungaroring
                case "dune-coast":       return 19f; // Zandvoort coastal dunes
                case "caspian-city":     return 20f; // Baku castle climb
                case "jade-river":       return 17f; // Shanghai Turn 1-2 rise & back-straight dip
                case "oakshire":         return 16f; // Silverstone
                case "green-park":       return 15f; // Monza Lesmo/Ascari grade
                case "desert-ring":      return 18f; // Bahrain Sakhir downhill T9-10
                default:                 return 16f;
            }
        }

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
            drsZones.Clear();
            ersHarvestZones.Clear();

            trackWidth = Mathf.Max(14.0f, def.trackWidth);
            halfWidth = trackWidth * 0.5f;

            // 1. Parse & scale GPS coordinates (~1.52x for realistic 5.2-6.8 km Grand Prix circuit scale)
            int ctrlCount = def.points.Length / 3;
            var rawCtrl = new Vector3[ctrlCount];
            const float gpsScale = 1.52f;
            for (int i = 0; i < ctrlCount; i++)
                rawCtrl[i] = new Vector3(def.points[i * 3] * gpsScale, def.points[i * 3 + 1], def.points[i * 3 + 2] * gpsScale);

            // 2-pass polyline smoothing so GeoJSON vertices flow like real FIA Grade-1 tarmac
            for (int pass = 0; pass < 2; pass++)
            {
                var nextCtrl = new Vector3[ctrlCount];
                for (int i = 0; i < ctrlCount; i++)
                {
                    Vector3 prevP = rawCtrl[(i - 1 + ctrlCount) % ctrlCount];
                    Vector3 currP = rawCtrl[i];
                    Vector3 nextP = rawCtrl[(i + 1) % ctrlCount];
                    nextCtrl[i] = prevP * 0.22f + currP * 0.56f + nextP * 0.22f;
                }
                rawCtrl = nextCtrl;
            }

            // Auto-align index 0 onto the longest straight so Start Gantry, Pit Complex & 22-Car Grid sit dead straight
            int win = Mathf.Clamp(ctrlCount / 7, 6, 28);
            int bestStartIdx = 0;
            float bestStraightScore = float.MaxValue;
            for (int i = 0; i < ctrlCount; i++)
            {
                float turnSum = 0f;
                float segLenSum = 0f;
                for (int k = -win; k <= 3; k++)
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

            // Inject authentic circuit-specific 3D elevation profile (steep climbs, crests & plunges; gently graded pit straight!)
            float elevAmp = GetTargetElevationMeters(def.id);
            float seedPhase = Mathf.Abs(def.id.GetHashCode() % 100) * 0.0628f;
            float minY = float.MaxValue;
            for (int i = 0; i < N; i++)
            {
                float u = (float)i / N; // 0..1 around lap; pit straight is around u in [0.90..1.0] U [0.0..0.09]
                float distFromStart = Mathf.Min(u, 1f - u);
                float pitEnvelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distFromStart - 0.05f) / 0.12f));

                float h1 = Mathf.Sin(u * Mathf.PI * 2f + seedPhase) * 0.52f;
                float h2 = Mathf.Sin(u * Mathf.PI * 4f + seedPhase * 1.7f) * 0.31f;
                float h3 = Mathf.Cos(u * Mathf.PI * 6f - seedPhase * 0.8f) * 0.17f;
                // Dramatic Raidillon/Turn-1 crest around u ~ 0.18..0.28
                float crest = Mathf.Exp(-Mathf.Pow((u - 0.23f) / 0.065f, 2f)) * 0.45f;
                float y = (h1 + h2 + h3 + crest) * elevAmp * 0.55f * pitEnvelope;
                raw[i].y = y;
                if (y < minY) minY = y;
            }
            for (int i = 0; i < N; i++)
                raw[i].y -= minY - 0.5f; // Keep entire circuit above ground plane y = 0

            // Compute tangents, signed curvature, slope, and progressive corner banking!
            samples = new TrackSample[N];
            float cum = 0f;
            Vector3 center = Vector3.zero;
            float maxBankDeg = def.id == "dune-coast" ? 16.5f : 9.5f;

            var rawBank = new float[N];
            var signedCurvs = new float[N];
            for (int i = 0; i < N; i++)
            {
                Vector3 pPrev = raw[(i - 2 + N) % N];
                Vector3 p = raw[i];
                Vector3 pNext = raw[(i + 2) % N];
                Vector3 v1 = p - pPrev; v1.y = 0f;
                Vector3 v2 = pNext - p; v2.y = 0f;
                float seg = Mathf.Max(0.8f, (v1.magnitude + v2.magnitude) * 0.5f);
                v1.Normalize(); v2.Normalize();
                float ang = Vector3.Angle(v1, v2) * Mathf.Deg2Rad;
                float crossY = Vector3.Cross(v1, v2).y; // + in right turn, - in left turn
                float sCurv = (ang / seg) * Mathf.Sign(crossY);
                signedCurvs[i] = sCurv;

                float distFromStart = Mathf.Min((float)i / N, 1f - (float)i / N);
                float bankEnv = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distFromStart - 0.03f) / 0.05f));
                rawBank[i] = Mathf.Clamp(sCurv * 340f, -1f, 1f) * maxBankDeg * Mathf.Deg2Rad * bankEnv;
            }

            // Smooth banking transitions over +-10 samples so corner entry/exit camber rolls silkily
            var smoothBank = new float[N];
            for (int i = 0; i < N; i++)
            {
                float sum = 0f;
                for (int k = -10; k <= 10; k++)
                    sum += rawBank[(i + k + N) % N];
                smoothBank[i] = sum / 21f;
            }

            for (int i = 0; i < N; i++)
            {
                Vector3 p = raw[i];
                Vector3 pPrev = raw[(i - 1 + N) % N];
                Vector3 pNext = raw[(i + 1 + N) % N];
                if (i > 0) cum += Vector3.Distance(p, pPrev);

                Vector3 tan3D = (pNext - pPrev).normalized;
                if (tan3D.sqrMagnitude < 0.001f) tan3D = Vector3.forward;
                Vector3 tanFlat = new Vector3(tan3D.x, 0f, tan3D.z).normalized;
                if (tanFlat.sqrMagnitude < 0.001f) tanFlat = Vector3.forward;

                float slopeRad = Mathf.Asin(Mathf.Clamp(tan3D.y, -0.45f, 0.45f));
                float bankRad = smoothBank[i];

                Vector3 flatRight = Vector3.Cross(Vector3.up, tanFlat).normalized;
                // Positive bankRad (right turn, crossY > 0): outer left side rises, inner right side dips -> rotate around tan3D by -bankRad!
                Quaternion bankRot = Quaternion.AngleAxis(-bankRad * Mathf.Rad2Deg, tan3D);
                Vector3 bankedRight = (bankRot * flatRight).normalized;
                Vector3 bankedUp = Vector3.Cross(tan3D, bankedRight).normalized;

                samples[i] = new TrackSample
                {
                    pos = p,
                    tangent = tan3D,
                    right = bankedRight,
                    up = bankedUp,
                    s = cum,
                    t = (float)i / N,
                    curvature = Mathf.Abs(signedCurvs[i]),
                    signedCurvature = signedCurvs[i],
                    slopeRad = slopeRad,
                    bankRad = bankRad
                };
                center += p;
            }
            totalLength = cum + Vector3.Distance(raw[N - 1], raw[0]);
            center /= N;

            // Detect Official FIA DRS Zones (2-3 longest low-curvature straights) & ERS Harvest Braking Zones!
            DetectDrsAndErsZones();

            // Determine clear side (+1 right or -1 left) for the Physical 3D Pit Lane & Garages along the Main Straight
            DeterminePitSide();

            // 1. Sculpted Rolling Terrain Base + 360° Distant Mountain/Stadium Bowl Ring
            ColorUtility.TryParseHtmlString(def.groundColor, out Color grdCol);
            var groundGo = MotoveloDatabase.CreatePlaneObject("GroundTerrain", transform);
            groundGo.transform.position = new Vector3(center.x, -1.2f, center.z);
            groundGo.transform.localScale = new Vector3(680f, 1f, 680f);
            var grassTex = CreateGrassTerrainTexture(grdCol);
            groundGo.GetComponent<Renderer>().sharedMaterial = MotoveloDatabase.CreateUrpLit(Color.white, grassTex, 0.02f, 0.14f);

            BuildPerimeterBackdropBowl(center, grdCol);

            // 2. High-Detail Asphalt Road Ribbon (with rubbered-in racing groove & crisp white edge lines) + Track Skirt Embankment
            var asphaltTex = CreateHighDetailAsphaltTexture();
            var asphaltMat = MotoveloDatabase.CreateUrpLit(Color.white, asphaltTex, 0.08f, 0.38f);
            CreateRibbonMesh("AsphaltRoad", -halfWidth, halfWidth, 0.03f, 0.065f, asphaltMat);

            // Solid earth/concrete embankment under elevated & banked track sections so road never floats in mid-air!
            var embankMat = MotoveloDatabase.CreateUrpLit(grdCol * 0.82f, null, 0.03f, 0.15f);
            CreateEmbankmentSkirtMesh("TrackEmbankmentLeft", -halfWidth - 5.2f, -1.3f, false, embankMat);
            CreateEmbankmentSkirtMesh("TrackEmbankmentRight", halfWidth + 5.2f, -1.3f, true, embankMat);

            // 3. Serrated Red & White FIA Kerbs
            var kerbTex = CreateKerbTexture();
            var kerbMat = MotoveloDatabase.CreateUrpLit(Color.white, kerbTex, 0.05f, 0.48f);
            CreateRibbonMesh("KerbLeft", -halfWidth - 1.55f, -halfWidth + 0.05f, 0.055f, 0.26f, kerbMat);
            CreateRibbonMesh("KerbRight", halfWidth - 0.05f, halfWidth + 1.55f, 0.055f, 0.26f, kerbMat);

            // 4. Painted Tarmac Runoff Aprons
            if (!ColorUtility.TryParseHtmlString(def.runoffColor, out Color runCol))
                runCol = new Color(0.22f, 0.26f, 0.32f);
            var runoffMat = MotoveloDatabase.CreateUrpLit(runCol, null, 0.05f, 0.24f);
            CreateRibbonMesh("RunoffLeft", -halfWidth - 5.2f, -halfWidth - 1.45f, 0.01f, 0.05f, runoffMat);
            CreateRibbonMesh("RunoffRight", halfWidth + 1.45f, halfWidth + 5.2f, 0.01f, 0.05f, runoffMat);

            // 5. Real Sponsor Barriers + Continuous 3.6m FIA Debris Catch-Fencing (Closed Stadium Circuit Feel!)
            var aramcoTex = MotoveloDatabase.LoadTex("Textures/sponsors/aramco");
            var f1Tex = MotoveloDatabase.LoadTex("Textures/sponsors/f1madrid");
            if (aramcoTex != null) aramcoTex.wrapMode = TextureWrapMode.Repeat;
            if (f1Tex != null) f1Tex.wrapMode = TextureWrapMode.Repeat;
            var wallMatL = MotoveloDatabase.CreateUrpLit(Color.white, aramcoTex, 0.14f, 0.58f);
            var wallMatR = MotoveloDatabase.CreateUrpLit(Color.white, f1Tex, 0.14f, 0.58f);
            CreateWallMesh("BarrierLeft", -halfWidth - 4.8f, 0f, 1.05f, false, -0.09f, wallMatL);
            CreateWallMesh("BarrierRight", halfWidth + 4.8f, 0f, 1.05f, true, 0.09f, wallMatR);

            var fenceTex = CreateCatchFenceTexture();
            var fenceMat = MotoveloDatabase.CreateUrpLit(Color.white, fenceTex, 0.65f, 0.45f, true);
            CreateWallMesh("CatchFenceLeft", -halfWidth - 4.85f, 1.02f, 2.85f, false, 0.35f, fenceMat);
            CreateWallMesh("CatchFenceRight", halfWidth + 4.85f, 1.02f, 2.85f, true, 0.35f, fenceMat);

            // 6. Start/Finish Checkered Line & Start Gantry
            var startSmp = samples[0];
            Quaternion startRot = Quaternion.LookRotation(startSmp.tangent, startSmp.up);
            var startQuad = MotoveloDatabase.CreateQuadObject("StartFinishLine", transform);
            startQuad.transform.position = startSmp.pos + startSmp.up * 0.065f;
            startQuad.transform.rotation = startRot * Quaternion.Euler(90f, 0f, 0f);
            startQuad.transform.localScale = new Vector3(trackWidth - 0.6f, 3.4f, 1f);
            startQuad.GetComponent<Renderer>().sharedMaterial = MotoveloDatabase.CreateUrpLit(Color.white, CreateCheckeredTexture(), 0.05f, 0.5f);

            if (gantryPrefab != null)
            {
                SpawnGantryBridge(gantryPrefab, startSmp, "Textures/sponsors/louisvuitton", true);
                // Add 2 extra track-spanning sponsor bridges (Rolex & DHL) on clear straights!
                string[] bridgeSponsors = { "Textures/sponsors/rolex", "Textures/sponsors/dhl" };
                float[] bridgeFracs = { 0.36f, 0.71f };
                for (int b = 0; b < 2; b++)
                {
                    var bSmp = samples[Mathf.RoundToInt(N * bridgeFracs[b]) % N];
                    if (bSmp.curvature < 0.010f)
                        SpawnGantryBridge(gantryPrefab, bSmp, bridgeSponsors[b], false);
                }
            }

            // 7.Marked Official FIA DRS Zones & ERS Harvest Zones on Track!
            SpawnDrsAndErsZoneVisuals();

            // 8. Physical 3D Pit Lane, Concrete Pit Wall, Pit Complex, Grandstands, Lush Tree Clusters & Floodlights!
            var treePrefab = Resources.Load<GameObject>("Models/tree_cluster");
            var lightPrefab = Resources.Load<GameObject>("Models/light_tower");

            BuildPhysicalPitLaneAndGarages(pitPrefab, asphaltMat);

            if (grandstandPrefab != null)
            {
                string[] gsSponsors = { "Textures/sponsors/rolex", "Textures/sponsors/dhl", "Textures/sponsors/f1madrid", "Textures/sponsors/aramco" };
                int placedStands = 0;
                for (int step = 0; step < 24 && placedStands < 10; step++)
                {
                    int idx = (Mathf.RoundToInt(N * (step / 24f)) + 14) % N;
                    var gsSmp = samples[idx];
                    float signedS = GetSignedS(gsSmp.s);
                    float gyaw = Mathf.Atan2(gsSmp.tangent.x, gsSmp.tangent.z) * Mathf.Rad2Deg;
                    Vector3 flatR = Vector3.Cross(Vector3.up, new Vector3(gsSmp.tangent.x, 0f, gsSmp.tangent.z).normalized).normalized;

                    foreach (float side in (step % 2 == 0 ? new[] { -1f, 1f } : new[] { 1f, -1f }))
                    {
                        // Never overlap the Pit Lane / Pit Garages on pitSide!
                        if (Mathf.Abs(side - pitSide) < 0.1f && signedS > -245f && signedS < 250f) continue;

                        Vector3 cand = gsSmp.pos + flatR * (side * (halfWidth + 17.5f));
                        if (IsClearOfAllTrackSegments(cand, halfWidth + 11.5f))
                        {
                            var gs = Instantiate(grandstandPrefab, transform);
                            gs.transform.position = new Vector3(cand.x, gsSmp.pos.y - 0.35f, cand.z);
                            gs.transform.rotation = Quaternion.Euler(0f, gyaw + (side < 0 ? 90f : -90f), 0f);
                            ApplyRichSceneMaterials(gs, gsSponsors[placedStands % gsSponsors.Length]);
                            placedStands++;
                            break;
                        }
                    }
                }
            }

            // Spawn lush Parkland Tree Clusters & Stadium Floodlight Masts strictly OUTSIDE all track loops!
            if (treePrefab != null)
            {
                for (int i = 0; i < N; i += 18)
                {
                    var ts = samples[i];
                    Vector3 flatR = Vector3.Cross(Vector3.up, new Vector3(ts.tangent.x, 0f, ts.tangent.z).normalized).normalized;
                    for (int sIdx = 0; sIdx < 2; sIdx++)
                    {
                        float side = sIdx == 0 ? -1f : 1f;
                        if (Mathf.Abs(side - pitSide) < 0.1f && GetSignedS(ts.s) > -245f && GetSignedS(ts.s) < 250f) continue;
                        float distOut = halfWidth + 15.5f + ((i * 7 + sIdx * 13) % 16);
                        Vector3 cand = ts.pos + flatR * (side * distOut);
                        if (IsClearOfAllTrackSegments(cand, halfWidth + 11.0f))
                        {
                            var tr = Instantiate(treePrefab, transform);
                            tr.transform.position = new Vector3(cand.x, ts.pos.y - 0.6f, cand.z);
                            tr.transform.rotation = Quaternion.Euler(0f, (i * 37f) % 360f, 0f);
                            float sc = 0.88f + ((i * 11) % 10) * 0.045f;
                            tr.transform.localScale = new Vector3(sc, sc, sc);
                            ApplyRichSceneMaterials(tr, null);
                        }
                    }
                }
            }

            if (lightPrefab != null)
            {
                for (int i = 10; i < N; i += 48)
                {
                    var ls = samples[i];
                    float lyaw = Mathf.Atan2(ls.tangent.x, ls.tangent.z) * Mathf.Rad2Deg;
                    Vector3 flatR = Vector3.Cross(Vector3.up, new Vector3(ls.tangent.x, 0f, ls.tangent.z).normalized).normalized;
                    float side = (i / 48) % 2 == 0 ? -1f : 1f;
                    if (Mathf.Abs(side - pitSide) < 0.1f && GetSignedS(ls.s) > -235f && GetSignedS(ls.s) < 240f) side = -pitSide;
                    Vector3 cand = ls.pos + flatR * (side * (halfWidth + 8.2f));
                    if (IsClearOfAllTrackSegments(cand, halfWidth + 6.5f))
                    {
                        var lt = Instantiate(lightPrefab, transform);
                        lt.transform.position = new Vector3(cand.x, ls.pos.y - 0.2f, cand.z);
                        lt.transform.rotation = Quaternion.Euler(0f, lyaw + (side < 0 ? 90f : -90f), 0f);
                        ApplyRichSceneMaterials(lt, null);
                    }
                }
            }

            // 9. Dynamic 3D Racing Line Chevrons + 150/100/50m Braking Marker Boards
            _racingLineRoot = new GameObject("RacingLineChevrons");
            _racingLineRoot.transform.SetParent(transform, false);

            var matGreen = MotoveloDatabase.CreateUrpEmissive(new Color(0.10f, 0.88f, 0.32f), new Color(0.16f, 1.30f, 0.44f), 0.6f);
            var matAmber = MotoveloDatabase.CreateUrpEmissive(new Color(0.95f, 0.78f, 0.08f), new Color(1.35f, 1.05f, 0.10f), 0.6f);
            var matRed   = MotoveloDatabase.CreateUrpEmissive(new Color(0.95f, 0.14f, 0.16f), new Color(1.55f, 0.18f, 0.20f), 0.6f);

            var mat150 = MotoveloDatabase.CreateUrpLit(Color.white, CreateSignBoardTex("150m", new Color(0.08f, 0.09f, 0.11f), Color.white), 0.05f, 0.5f);
            var mat100 = MotoveloDatabase.CreateUrpLit(Color.white, CreateSignBoardTex("100m", new Color(0.08f, 0.09f, 0.11f), Color.white), 0.05f, 0.5f);
            var mat50  = MotoveloDatabase.CreateUrpLit(Color.white, CreateSignBoardTex("50m",  new Color(0.08f, 0.09f, 0.11f), Color.white), 0.05f, 0.5f);

            float lastBoardS = -300f;
            for (int i = 6; i < N; i += 8)
            {
                var smp = samples[i];
                float maxCurv = 0f;
                for (int look = 0; look < 26; look++)
                    maxCurv = Mathf.Max(maxCurv, samples[(i + look) % N].curvature);

                var cQuad = MotoveloDatabase.CreateQuadObject("RaceChevron", _racingLineRoot.transform);
                Quaternion rRot = Quaternion.LookRotation(smp.tangent, smp.up);
                cQuad.transform.position = smp.pos + smp.up * 0.065f;
                cQuad.transform.rotation = rRot * Quaternion.Euler(90f, 0f, 0f);
                cQuad.transform.localScale = new Vector3(1.35f, 3.0f, 1f);
                cQuad.GetComponent<Renderer>().sharedMaterial = maxCurv > 0.018f ? matRed : (maxCurv > 0.009f ? matAmber : matGreen);

                if (smp.curvature > 0.019f && smp.s - lastBoardS > 250f)
                {
                    lastBoardS = smp.s;
                    SpawnTracksideBoard(smp.s - 125f, -1f, mat150, 1.7f, 1.0f);
                    SpawnTracksideBoard(smp.s - 85f,  -1f, mat100, 1.7f, 1.0f);
                    SpawnTracksideBoard(smp.s - 45f,  -1f, mat50,  1.7f, 1.0f);
                }
            }
            _racingLineRoot.SetActive(showRacingLine);

            // 10. Elevated Trackside TV Broadcast Cameras
            for (int i = 0; i < 18; i++)
            {
                var cs = samples[(i * N / 18) % N];
                float side = i % 2 == 0 ? 1f : -1f;
                broadcastCameras.Add(cs.pos + cs.right * (side * (halfWidth + 8.5f)) + Vector3.up * 4.8f);
            }
        }

        private void DetectDrsAndErsZones()
        {
            int n = samples.Length;
            // 1. Always make the main Pit Straight (around s = 0) DRS Zone #1!
            drsZones.Add(new CircuitZone
            {
                detectS = WrapS(totalLength - 290f),
                startS = WrapS(totalLength - 140f),
                endS = Mathf.Min(totalLength * 0.16f, 520f)
            });

            // 2. Find up to 2 additional long low-curvature straights around the lap (s in [0.20..0.84])
            int i = Mathf.RoundToInt(n * 0.18f);
            int limit = Mathf.RoundToInt(n * 0.86f);
            while (i < limit && drsZones.Count < 3)
            {
                if (samples[i].curvature < 0.0052f)
                {
                    int startIdx = i;
                    while (i < limit && samples[i].curvature < 0.0062f) i++;
                    float len = samples[i - 1].s - samples[startIdx].s;
                    if (len > 260f)
                    {
                        float zStart = samples[startIdx].s + 45f;
                        float zEnd = samples[i - 1].s - 55f;
                        if (zEnd - zStart > 160f)
                        {
                            drsZones.Add(new CircuitZone
                            {
                                detectS = WrapS(samples[startIdx].s - 95f),
                                startS = zStart,
                                endS = zEnd
                            });
                        }
                    }
                }
                else i++;
            }

            // 3. Detect heavy braking zones as MGU-K ERS Harvest Zones!
            float lastErsS = -300f;
            for (int k = 25; k < n - 20; k++)
            {
                if (samples[k].curvature > 0.017f && samples[k].s - lastErsS > 320f)
                {
                    lastErsS = samples[k].s;
                    ersHarvestZones.Add(new CircuitZone
                    {
                        detectS = Mathf.Max(10f, samples[k].s - 150f),
                        startS = Mathf.Max(10f, samples[k].s - 135f),
                        endS = Mathf.Max(20f, samples[k].s - 15f)
                    });
                }
            }
        }

        private void SpawnDrsAndErsZoneVisuals()
        {
            var drsLineMat = MotoveloDatabase.CreateUrpEmissive(new Color(0.08f, 0.96f, 0.32f), new Color(0.20f, 2.2f, 0.55f), 0.7f);
            var detLineMat = MotoveloDatabase.CreateUrpEmissive(new Color(0.92f, 0.96f, 1.0f), new Color(1.1f, 1.2f, 1.3f), 0.6f);
            var drsSignMat = MotoveloDatabase.CreateUrpEmissive(new Color(0.06f, 0.78f, 0.26f), new Color(0.12f, 1.35f, 0.42f), 0.6f);
            drsSignMat.mainTexture = CreateSignBoardTex("DRS", new Color(0.03f, 0.42f, 0.14f), Color.white);
            if (drsSignMat.HasProperty("_BaseMap")) drsSignMat.SetTexture("_BaseMap", drsSignMat.mainTexture);

            var ersSignMat = MotoveloDatabase.CreateUrpEmissive(new Color(0.12f, 0.72f, 0.98f), new Color(0.18f, 1.15f, 1.85f), 0.6f);
            ersSignMat.mainTexture = CreateSignBoardTex("ERS", new Color(0.05f, 0.24f, 0.48f), new Color(0.4f, 0.95f, 1f));
            if (ersSignMat.HasProperty("_BaseMap")) ersSignMat.SetTexture("_BaseMap", ersSignMat.mainTexture);

            foreach (var z in drsZones)
            {
                // Detection Line
                var detSmp = GetSampleAtDistance(z.detectS);
                var dLine = MotoveloDatabase.CreateQuadObject("DrsDetectionLine", transform);
                dLine.transform.position = detSmp.pos + detSmp.up * 0.06f;
                dLine.transform.rotation = Quaternion.LookRotation(detSmp.tangent, detSmp.up) * Quaternion.Euler(90f, 0f, 0f);
                dLine.transform.localScale = new Vector3(trackWidth, 0.85f, 1f);
                dLine.GetComponent<Renderer>().sharedMaterial = detLineMat;

                // Activation Bright Green Line + Overhead Glowing DRS Arch + Trackside DRS Boards
                var actSmp = GetSampleAtDistance(z.startS);
                var aLine = MotoveloDatabase.CreateQuadObject("DrsActivationLine", transform);
                aLine.transform.position = actSmp.pos + actSmp.up * 0.068f;
                aLine.transform.rotation = Quaternion.LookRotation(actSmp.tangent, actSmp.up) * Quaternion.Euler(90f, 0f, 0f);
                aLine.transform.localScale = new Vector3(trackWidth, 1.35f, 1f);
                aLine.GetComponent<Renderer>().sharedMaterial = drsLineMat;

                SpawnTracksideBoard(z.startS, -1f, drsSignMat, 2.4f, 1.25f);
                SpawnTracksideBoard(z.startS,  1f, drsSignMat, 2.4f, 1.25f);

                // Glowing Green Verge LED Strip along entire DRS Zone!
                float len = z.endS > z.startS ? (z.endS - z.startS) : (totalLength - z.startS + z.endS);
                for (float d = 0f; d < len; d += 22f)
                {
                    var zs = GetSampleAtDistance(z.startS + d);
                    for (int sSide = -1; sSide <= 1; sSide += 2)
                    {
                        var strip = MotoveloDatabase.CreateQuadObject("DrsLedStrip", transform);
                        strip.transform.position = zs.pos + zs.right * (sSide * (halfWidth + 1.75f)) + zs.up * 0.07f;
                        strip.transform.rotation = Quaternion.LookRotation(zs.tangent, zs.up) * Quaternion.Euler(90f, 0f, 0f);
                        strip.transform.localScale = new Vector3(0.55f, 12.0f, 1f);
                        strip.GetComponent<Renderer>().sharedMaterial = drsLineMat;
                    }
                }
            }

            foreach (var ez in ersHarvestZones)
            {
                SpawnTracksideBoard(ez.startS, 1f, ersSignMat, 2.2f, 1.15f);
                for (float s = ez.startS; s < ez.endS; s += 20f)
                {
                    var es = GetSampleAtDistance(s);
                    var eStrip = MotoveloDatabase.CreateQuadObject("ErsHarvestStrip", transform);
                    eStrip.transform.position = es.pos + es.right * (halfWidth + 1.75f) + es.up * 0.07f;
                    eStrip.transform.rotation = Quaternion.LookRotation(es.tangent, es.up) * Quaternion.Euler(90f, 0f, 0f);
                    eStrip.transform.localScale = new Vector3(0.55f, 10.0f, 1f);
                    eStrip.GetComponent<Renderer>().sharedMaterial = ersSignMat;
                }
            }
        }

        public bool IsInDrsZone(float s)
        {
            float ws = WrapS(s);
            for (int i = 0; i < drsZones.Count; i++)
            {
                var z = drsZones[i];
                if (z.startS <= z.endS)
                {
                    if (ws >= z.startS && ws <= z.endS) return true;
                }
                else
                {
                    if (ws >= z.startS || ws <= z.endS) return true;
                }
            }
            return false;
        }

        public bool IsInErsHarvestZone(float s)
        {
            float ws = WrapS(s);
            for (int i = 0; i < ersHarvestZones.Count; i++)
            {
                var z = ersHarvestZones[i];
                if (ws >= z.startS && ws <= z.endS) return true;
            }
            return false;
        }

        private float WrapS(float s)
        {
            if (totalLength <= 1f) return 0f;
            return ((s % totalLength) + totalLength) % totalLength;
        }

        public float GetSignedS(float s)
        {
            if (totalLength <= 1f) return 0f;
            float w = WrapS(s);
            return w > totalLength * 0.5f ? w - totalLength : w;
        }

        public float GetPitOuterWallExpansion(float s)
        {
            float sd = GetSignedS(s);
            if (sd < -235f || sd > 270f) return 0f;
            if (sd < -195f) return Mathf.SmoothStep(0f, 11.5f, (sd + 235f) / 40f);
            if (sd > 230f)  return Mathf.SmoothStep(11.5f, 0f, (sd - 230f) / 40f);
            return 11.5f;
        }

        private void DeterminePitSide()
        {
            pitSide = 1f;
            var ps = GetSampleAtDistance(120f);
            Vector3 flatR = Vector3.Cross(Vector3.up, new Vector3(ps.tangent.x, 0f, ps.tangent.z).normalized).normalized;
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 cand = ps.pos + flatR * (side * (halfWidth + 22.0f));
                if (IsClearOfAllTrackSegments(cand, halfWidth + 12.0f))
                {
                    pitSide = side;
                    break;
                }
            }
            pitLaneOffset = pitSide * (halfWidth + 8.6f);
            pitBoxSignedS = 138f;
        }

        private void BuildPhysicalPitLaneAndGarages(GameObject pitPrefab, Material asphaltMat)
        {
            // 1. Paved 3D Pit Lane Road & Garage Apron Ribbon (signedS from -230m through 0m to +265m)
            const int steps = 110;
            var verts = new Vector3[(steps + 1) * 2];
            var uvs = new Vector2[(steps + 1) * 2];
            var tris = new int[steps * 6];
            float sStart = -230f, sEnd = 265f;

            for (int i = 0; i <= steps; i++)
            {
                float sd = Mathf.Lerp(sStart, sEnd, (float)i / steps);
                var smp = GetSampleAtDistance(sd);
                float innerOff = pitSide * (halfWidth + 0.9f);
                float outerOff = pitSide * (halfWidth + 4.8f + GetPitOuterWallExpansion(smp.s));

                float leftOff  = Mathf.Min(innerOff, outerOff);
                float rightOff = Mathf.Max(innerOff, outerOff);

                verts[i * 2]     = smp.pos + smp.right * leftOff  + smp.up * 0.025f;
                verts[i * 2 + 1] = smp.pos + smp.right * rightOff + smp.up * 0.025f;
                uvs[i * 2]       = new Vector2(0f, (sd - sStart) * 0.06f);
                uvs[i * 2 + 1]   = new Vector2(1f, (sd - sStart) * 0.06f);

                if (i < steps)
                {
                    int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                    int ti = i * 6;
                    tris[ti] = a; tris[ti + 1] = c; tris[ti + 2] = b;
                    tris[ti + 3] = b; tris[ti + 4] = c; tris[ti + 5] = d;
                }
            }

            var pitMesh = new Mesh { name = "PitLaneTarmac" };
            pitMesh.vertices = verts;
            pitMesh.uv = uvs;
            pitMesh.triangles = tris;
            pitMesh.RecalculateNormals();
            var pitRoadGo = new GameObject("PitLaneTarmac");
            pitRoadGo.transform.SetParent(transform, false);
            pitRoadGo.AddComponent<MeshFilter>().sharedMesh = pitMesh;
            pitRoadGo.AddComponent<MeshRenderer>().sharedMaterial = asphaltMat;

            // 2. Concrete FIA Pit Wall Segments:
            // Segment A [-120m .. +14m] (Grid Wall) + OPEN STARTING-REGION PIT ENTRY [+14m .. +82m] + Segment B [+82m .. +198m] (Garage Wall)!
            var pitWallMat = MotoveloDatabase.CreateUrpLit(new Color(0.82f, 0.84f, 0.88f), MotoveloDatabase.LoadTex("Textures/sponsors/rolex"), 0.15f, 0.55f);
            BuildPitWallSegment("ConcretePitWall_Grid",   -120f,  14f, pitWallMat);
            BuildPitWallSegment("ConcretePitWall_Garage",   82f, 198f, pitWallMat);

            // 3. Glowing PIT ENTRY Chevrons (both at Starting Region +16m..+80m AND Lap-End -215m..-125m), PIT Signs, & 80 KPH Limit Lines!
            var pitEntryGlow = MotoveloDatabase.CreateUrpEmissive(new Color(1f, 0.72f, 0.06f), new Color(2.2f, 1.45f, 0.08f), 0.7f);
            var whiteGlow    = MotoveloDatabase.CreateUrpEmissive(Color.white, new Color(1.5f, 1.5f, 1.6f), 0.7f);
            var exitGreenMat = MotoveloDatabase.CreateUrpEmissive(new Color(0.08f, 0.96f, 0.34f), new Color(0.2f, 2.8f, 0.65f), 0.8f);

            var pitSignMat = MotoveloDatabase.CreateUrpEmissive(new Color(0.95f, 0.68f, 0.05f), new Color(1.6f, 1.1f, 0.1f), 0.7f);
            pitSignMat.mainTexture = CreateSignBoardTex("PIT", new Color(0.14f, 0.10f, 0.02f), new Color(1f, 0.82f, 0.12f));
            if (pitSignMat.HasProperty("_BaseMap")) pitSignMat.SetTexture("_BaseMap", pitSignMat.mainTexture);

            var limSignMat = MotoveloDatabase.CreateUrpEmissive(Color.white, new Color(1.2f, 1.2f, 1.2f), 0.7f);
            limSignMat.mainTexture = CreateSignBoardTex("80", new Color(0.82f, 0.08f, 0.12f), Color.white);
            if (limSignMat.HasProperty("_BaseMap")) limSignMat.SetTexture("_BaseMap", limSignMat.mainTexture);

            foreach (var mouth in new[] { new Vector2(16f, 78f), new Vector2(-215f, -124f) })
            {
                for (int k = 0; k < 6; k++)
                {
                    float frac = k / 5f;
                    float sd = Mathf.Lerp(mouth.x, mouth.y, frac);
                    float lat = pitSide * Mathf.Lerp(halfWidth - 1.0f, halfWidth + 7.8f, frac);
                    var es = GetSampleAtDistance(sd);
                    var chev = MotoveloDatabase.CreateQuadObject($"PitEntryChevron_{mouth.x}_{k}", transform);
                    chev.transform.position = es.pos + es.right * lat + es.up * 0.068f;
                    chev.transform.rotation = Quaternion.LookRotation(es.tangent, es.up) * Quaternion.Euler(90f, pitSide * 14f, 0f);
                    chev.transform.localScale = new Vector3(2.2f, 4.0f, 1f);
                    chev.GetComponent<Renderer>().sharedMaterial = pitEntryGlow;
                }
            }

            foreach (float noseSd in new[] { 82f, -120f })
            {
                var noseSmp = GetSampleAtDistance(noseSd);
                var pitBoard = MotoveloDatabase.CreateCubeObject($"PitEntrySign_{noseSd}", transform);
                pitBoard.transform.position = noseSmp.pos + noseSmp.right * (pitSide * (halfWidth + 3.1f)) + Vector3.up * 2.1f;
                pitBoard.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(noseSmp.tangent.x, noseSmp.tangent.z) * Mathf.Rad2Deg + 180f, 0f);
                pitBoard.transform.localScale = new Vector3(2.4f, 1.2f, 0.18f);
                pitBoard.GetComponent<Renderer>().sharedMaterial = pitSignMat;

                var limLine = MotoveloDatabase.CreateQuadObject($"PitSpeedLimitLine_{noseSd}", transform);
                limLine.transform.position = noseSmp.pos + noseSmp.right * pitLaneOffset + noseSmp.up * 0.068f;
                limLine.transform.rotation = Quaternion.LookRotation(noseSmp.tangent, noseSmp.up) * Quaternion.Euler(90f, 0f, 0f);
                limLine.transform.localScale = new Vector3(9.5f, 1.1f, 1f);
                limLine.GetComponent<Renderer>().sharedMaterial = whiteGlow;

                var limBoard = MotoveloDatabase.CreateCubeObject($"PitLimit80Sign_{noseSd}", transform);
                limBoard.transform.position = noseSmp.pos + noseSmp.right * (pitSide * (halfWidth + 13.6f)) + Vector3.up * 2.0f;
                limBoard.transform.rotation = pitBoard.transform.rotation;
                limBoard.transform.localScale = new Vector3(1.8f, 1.2f, 0.16f);
                limBoard.GetComponent<Renderer>().sharedMaterial = limSignMat;
            }

            // Pit Lane Fast-Lane Center Guide Line (-115m .. +196m)
            for (float sd = -112f; sd < 194f; sd += 14f)
            {
                var ls = GetSampleAtDistance(sd);
                var dash = MotoveloDatabase.CreateQuadObject("PitLaneGuide", transform);
                dash.transform.position = ls.pos + ls.right * (pitSide * (halfWidth + 6.2f)) + ls.up * 0.055f;
                dash.transform.rotation = Quaternion.LookRotation(ls.tangent, ls.up) * Quaternion.Euler(90f, 0f, 0f);
                dash.transform.localScale = new Vector3(0.22f, 6.5f, 1f);
                dash.GetComponent<Renderer>().sharedMaterial = whiteGlow;
            }

            // PIT EXIT Green Light (+198m) & FIA Solid White Pit Exit Merge Line (+198m .. +260m)!
            var exitSmp = GetSampleAtDistance(198f);
            var exitBeacon = MotoveloDatabase.CreateCubeObject("PitExitGreenLight", transform);
            exitBeacon.transform.position = exitSmp.pos + exitSmp.right * (pitSide * (halfWidth + 3.1f)) + Vector3.up * 1.85f;
            exitBeacon.transform.rotation = Quaternion.LookRotation(exitSmp.tangent, Vector3.up);
            exitBeacon.transform.localScale = new Vector3(0.55f, 0.85f, 0.35f);
            exitBeacon.GetComponent<Renderer>().sharedMaterial = exitGreenMat;

            for (float sd = 198f; sd < 260f; sd += 8f)
            {
                var ms = GetSampleAtDistance(sd);
                float mergeLat = pitSide * Mathf.Lerp(halfWidth + 2.8f, halfWidth - 0.2f, (sd - 198f) / 62f);
                var wLine = MotoveloDatabase.CreateQuadObject("PitExitWhiteLine", transform);
                wLine.transform.position = ms.pos + ms.right * mergeLat + ms.up * 0.062f;
                wLine.transform.rotation = Quaternion.LookRotation(ms.tangent, ms.up) * Quaternion.Euler(90f, 0f, 0f);
                wLine.transform.localScale = new Vector3(0.28f, 8.2f, 1f);
                wLine.GetComponent<Renderer>().sharedMaterial = whiteGlow;
            }

            // 4. 3-Story F1 Pit Garages & Paddock Club positioned directly behind the Pit Apron at pitBoxSignedS (+138m)!
            if (pitPrefab != null)
            {
                var boxSmp = GetSampleAtDistance(pitBoxSignedS);
                float pyaw = Mathf.Atan2(boxSmp.tangent.x, boxSmp.tangent.z) * Mathf.Rad2Deg;
                Vector3 flatR = Vector3.Cross(Vector3.up, new Vector3(boxSmp.tangent.x, 0f, boxSmp.tangent.z).normalized).normalized;
                var pit = Instantiate(pitPrefab, transform);
                pit.name = "FIA_PitGarages";
                pit.transform.position = boxSmp.pos + flatR * (pitSide * (halfWidth + 21.6f)) + Vector3.down * 0.15f;
                pit.transform.rotation = Quaternion.Euler(0f, pyaw + (pitSide > 0 ? -90f : 90f), 0f);
                ApplyRichSceneMaterials(pit, "Textures/sponsors/aramco");
            }
        }

        private void BuildPitWallSegment(string wallName, float sdFrom, float sdTo, Material mat)
        {
            const int wallSteps = 32;
            int svCount = (wallSteps + 1) * 2;
            var wVerts = new Vector3[svCount * 2];
            var wUvs = new Vector2[svCount * 2];
            var wTris = new int[wallSteps * 12];
            for (int i = 0; i <= wallSteps; i++)
            {
                float sd = Mathf.Lerp(sdFrom, sdTo, (float)i / wallSteps);
                var smp = GetSampleAtDistance(sd);
                Vector3 bPos = smp.pos + smp.right * (pitSide * (halfWidth + 3.1f));
                Vector3 tPos = bPos + Vector3.up * 1.15f;

                wVerts[i * 2] = bPos; wVerts[i * 2 + 1] = tPos;
                wUvs[i * 2] = new Vector2(sd * 0.08f, 0f); wUvs[i * 2 + 1] = new Vector2(sd * 0.08f, 1f);

                int bi = svCount + i * 2;
                wVerts[bi] = bPos; wVerts[bi + 1] = tPos;
                wUvs[bi] = new Vector2(-sd * 0.08f, 0f); wUvs[bi + 1] = new Vector2(-sd * 0.08f, 1f);

                if (i < wallSteps)
                {
                    int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                    int ba = sideVertCountFix(svCount, a), bb = sideVertCountFix(svCount, b), bc = sideVertCountFix(svCount, c), bd = sideVertCountFix(svCount, d);
                    int ti = i * 12;
                    wTris[ti] = a; wTris[ti + 1] = b; wTris[ti + 2] = c;
                    wTris[ti + 3] = b; wTris[ti + 4] = d; wTris[ti + 5] = c;
                    wTris[ti + 6] = ba; wTris[ti + 7] = bc; wTris[ti + 8] = bb;
                    wTris[ti + 9] = bb; wTris[ti + 10] = bc; wTris[ti + 11] = bd;
                }
            }
            var pwMesh = new Mesh { name = wallName };
            pwMesh.vertices = wVerts; pwMesh.uv = wUvs; pwMesh.triangles = wTris;
            pwMesh.RecalculateNormals();
            var pwGo = new GameObject(wallName);
            pwGo.transform.SetParent(transform, false);
            pwGo.AddComponent<MeshFilter>().sharedMesh = pwMesh;
            pwGo.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static int sideVertCountFix(int svCount, int idx) => svCount + idx;

        public void SpawnPlayerPitCrew(TeamDef team, string helmetId, CompoundDef nextCompound, GameObject wheelPrefab)
        {
            if (playerPitCrew != null) Destroy(playerPitCrew.gameObject);

            var crewPrefab = Resources.Load<GameObject>("Models/pit_crew_member");
            var boomPrefab = Resources.Load<GameObject>("Models/pit_boom");
            var boxSmp = GetSampleAtDistance(pitBoxSignedS);
            Vector3 boxWorldPos = boxSmp.pos + boxSmp.right * pitLaneOffset + boxSmp.up * 0.03f;
            Quaternion boxRot = Quaternion.LookRotation(new Vector3(boxSmp.tangent.x, 0f, boxSmp.tangent.z).normalized, Vector3.up);

            var crewGo = new GameObject("PlayerPitCrewBox");
            crewGo.transform.SetParent(transform, false);
            playerPitCrew = crewGo.AddComponent<PitStopCrewController>();
            playerPitCrew.BuildPitBox(boxWorldPos, boxRot, pitSide, team, helmetId, nextCompound, crewPrefab, boomPrefab, wheelPrefab);
        }

        private bool IsClearOfAllTrackSegments(Vector3 worldCandidate, float minClearanceXZ)
        {
            float minD2 = minClearanceXZ * minClearanceXZ;
            for (int i = 0; i < samples.Length; i += 2)
            {
                float dx = worldCandidate.x - samples[i].pos.x;
                float dz = worldCandidate.z - samples[i].pos.z;
                if (dx * dx + dz * dz < minD2)
                    return false;
            }
            return true;
        }

        private void SpawnGantryBridge(GameObject gantryPrefab, TrackSample smp, string sponsorRes, bool isStartGantry)
        {
            float gyaw = Mathf.Atan2(smp.tangent.x, smp.tangent.z) * Mathf.Rad2Deg;
            var gantry = Instantiate(gantryPrefab, transform);
            gantry.name = isStartGantry ? "StartGantry" : "SponsorBridge";
            gantry.transform.position = smp.pos;
            gantry.transform.rotation = Quaternion.Euler(0f, gyaw, 0f);
            float gScale = (trackWidth + 6.8f) / 21.6f;
            gantry.transform.localScale = new Vector3(gScale, 1.05f, 1f);
            var spTex = MotoveloDatabase.LoadTex(sponsorRes);

            foreach (var r in gantry.GetComponentsInChildren<Renderer>())
            {
                string mName = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (mName.Contains("GantryBanner") && spTex != null)
                {
                    r.material = MotoveloDatabase.CreateUrpLit(Color.white, spTex, 0.12f, 0.58f);
                }
                else if (mName.Contains("StartLight_") || r.gameObject.name.Contains("Bulb"))
                {
                    if (!isStartGantry)
                    {
                        r.gameObject.SetActive(false);
                    }
                    else
                    {
                        var bulbMat = MotoveloDatabase.CreateUrpEmissive(new Color(0.18f, 0.02f, 0.02f), Color.black, 0.8f);
                        r.material = bulbMat;
                        startLightRenderers.Add(r);
                    }
                }
                else if (!isStartGantry && r.gameObject.name.Contains("StartPod"))
                {
                    r.gameObject.SetActive(false);
                }
                else
                {
                    r.material = MotoveloDatabase.CreateUrpLit(new Color(0.15f, 0.17f, 0.21f), null, 0.78f, 0.68f);
                }
            }
        }

        private void ApplyRichSceneMaterials(GameObject root, string bannerSponsorPath)
        {
            Texture2D bannerTex = !string.IsNullOrEmpty(bannerSponsorPath) ? MotoveloDatabase.LoadTex(bannerSponsorPath) : null;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                string mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (mn.Contains("GantryBanner") && bannerTex != null)
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(Color.white, bannerTex, 0.12f, 0.55f);
                else if (mn.Contains("Glass"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.07f, 0.19f, 0.34f), null, 0.92f, 0.94f);
                else if (mn.Contains("Light") || mn.Contains("Glow"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpEmissive(new Color(1f, 0.97f, 0.88f), new Color(2.6f, 2.5f, 2.2f), 0.85f);
                else if (mn.Contains("SeatsCrimson") || mn.Contains("Red"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.84f, 0.10f, 0.14f), null, 0.12f, 0.52f);
                else if (mn.Contains("SeatsCobalt"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.12f, 0.36f, 0.80f), null, 0.12f, 0.52f);
                else if (mn.Contains("SeatsAmber"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.92f, 0.72f, 0.12f), null, 0.12f, 0.52f);
                else if (mn.Contains("Canopy") || mn.Contains("White"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.93f, 0.94f, 0.96f), null, 0.18f, 0.68f);
                else if (mn.Contains("Steel") || mn.Contains("Dark") || mn.Contains("Mast"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.15f, 0.17f, 0.21f), null, 0.75f, 0.65f);
                else if (mn.Contains("FoliageDark"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.11f, 0.30f, 0.12f), null, 0.02f, 0.25f);
                else if (mn.Contains("FoliageMid"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.16f, 0.39f, 0.15f), null, 0.02f, 0.28f);
                else if (mn.Contains("FoliageFresh"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.22f, 0.46f, 0.17f), null, 0.02f, 0.30f);
                else if (mn.Contains("TreeBark"))
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(new Color(0.27f, 0.19f, 0.12f), null, 0.02f, 0.18f);
                else
                {
                    Color c = r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor")
                        ? r.sharedMaterial.GetColor("_BaseColor")
                        : new Color(0.72f, 0.74f, 0.77f);
                    r.sharedMaterial = MotoveloDatabase.CreateUrpLit(c, null, 0.10f, 0.42f);
                }
            }
        }

        private void BuildPerimeterBackdropBowl(Vector3 center, Color groundTint)
        {
            float maxR = 260f;
            for (int i = 0; i < samples.Length; i += 4)
            {
                float d = Vector2.Distance(new Vector2(samples[i].pos.x, samples[i].pos.z), new Vector2(center.x, center.z));
                if (d + 180f > maxR) maxR = d + 180f;
            }

            const int segs = 72;
            var verts = new Vector3[(segs + 1) * 2];
            var uvs = new Vector2[(segs + 1) * 2];
            var tris = new int[segs * 6];
            for (int i = 0; i <= segs; i++)
            {
                float a = ((float)i / segs) * Mathf.PI * 2f;
                float cx = center.x + Mathf.Cos(a) * maxR;
                float cz = center.z + Mathf.Sin(a) * maxR;
                float ridgeH = 52f + Mathf.Sin(a * 5f) * 24f + Mathf.Cos(a * 11f) * 14f;
                verts[i * 2] = new Vector3(cx, -4f, cz);
                verts[i * 2 + 1] = new Vector3(cx * 1.06f, ridgeH, cz * 1.06f);
                uvs[i * 2] = new Vector2((float)i / segs, 0f);
                uvs[i * 2 + 1] = new Vector2((float)i / segs, 1f);
                if (i < segs)
                {
                    int v0 = i * 2, v1 = v0 + 1, v2 = v0 + 2, v3 = v0 + 3;
                    int ti = i * 6;
                    tris[ti] = v0; tris[ti + 1] = v1; tris[ti + 2] = v2;
                    tris[ti + 3] = v1; tris[ti + 4] = v3; tris[ti + 5] = v2;
                }
            }
            var mesh = new Mesh { name = "BackdropHorizonMountains" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            var go = new GameObject("BackdropHorizonMountains");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Color hillCol = Color.Lerp(groundTint, new Color(0.28f, 0.36f, 0.46f), 0.55f);
            go.AddComponent<MeshRenderer>().sharedMaterial = MotoveloDatabase.CreateUrpLit(hillCol, null, 0.02f, 0.12f);
        }

        public void SetRacingLineVisible(bool visible)
        {
            showRacingLine = visible;
            if (_racingLineRoot != null) _racingLineRoot.SetActive(visible);
        }

        public void SetStartLights(int litPods)
        {
            for (int i = 0; i < startLightRenderers.Count; i++)
            {
                int podIdx = i / 2;
                bool lit = podIdx < litPods;
                var mat = startLightRenderers[i].material;
                Color col = lit ? new Color(1f, 0.06f, 0.10f) : new Color(0.14f, 0.02f, 0.02f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", lit ? new Color(3.8f, 0.18f, 0.22f) : Color.black);
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
            Vector3 diff = worldPos - smp.pos;
            float lat = diff.x * smp.right.x + diff.z * smp.right.z;

            return new TrackProjection
            {
                index = bestIdx,
                s = smp.s,
                t = smp.t,
                lateralOffset = lat,
                point = smp.pos + smp.right * lat,
                tangent = smp.tangent,
                right = smp.right,
                up = smp.up,
                curvature = smp.curvature,
                slopeRad = smp.slopeRad,
                bankRad = smp.bankRad
            };
        }

        public TrackSample GetSampleAtDistance(float s)
        {
            int n = samples.Length;
            float wrapped = WrapS(s);
            // Exact binary search on cumulative arc-length s (millimeter-accurate across straights & hairpins!)
            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (samples[mid].s <= wrapped) lo = mid;
                else hi = mid - 1;
            }
            return samples[lo];
        }

        private void SpawnTracksideBoard(float distS, float side, Material mat, float w, float h)
        {
            var bs = GetSampleAtDistance(distS);
            float byaw = Mathf.Atan2(bs.tangent.x, bs.tangent.z) * Mathf.Rad2Deg;
            var cube = MotoveloDatabase.CreateCubeObject("TrackSignBoard", transform);
            cube.transform.position = bs.pos + bs.right * (side * (halfWidth + 3.9f)) + bs.up * (0.6f + h * 0.5f);
            cube.transform.rotation = Quaternion.Euler(0f, byaw + 180f, 0f);
            cube.transform.localScale = new Vector3(w, h, 0.14f);
            cube.GetComponent<Renderer>().sharedMaterial = mat;
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
                verts[i * 2]     = smp.pos + smp.right * leftOff  + smp.up * yOff;
                verts[i * 2 + 1] = smp.pos + smp.right * rightOff + smp.up * yOff;
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

        private void CreateEmbankmentSkirtMesh(string meshName, float edgeOff, float bottomY, bool flip, Material mat)
        {
            int n = samples.Length;
            var verts = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[(n + 1) * 2];
            var tris = new int[n * 6];

            for (int i = 0; i <= n; i++)
            {
                var smp = samples[i % n];
                Vector3 top = smp.pos + smp.right * edgeOff;
                Vector3 bot = new Vector3(top.x + smp.right.x * Mathf.Sign(edgeOff) * 6.5f, bottomY, top.z + smp.right.z * Mathf.Sign(edgeOff) * 6.5f);
                verts[i * 2] = bot;
                verts[i * 2 + 1] = top;
                uvs[i * 2] = new Vector2(0f, 0f);
                uvs[i * 2 + 1] = new Vector2(0f, 1f);
                if (i < n)
                {
                    int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                    int ti = i * 6;
                    if (flip)
                    {
                        tris[ti] = a; tris[ti + 1] = c; tris[ti + 2] = b;
                        tris[ti + 3] = b; tris[ti + 4] = c; tris[ti + 5] = d;
                    }
                    else
                    {
                        tris[ti] = a; tris[ti + 1] = b; tris[ti + 2] = c;
                        tris[ti + 3] = b; tris[ti + 4] = d; tris[ti + 5] = c;
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

        private void CreateWallMesh(string meshName, float offset, float yBase, float height, bool flip, float uRepeat, Material mat)
        {
            int n = samples.Length;
            // Double-sided wall mesh (front + back vertices with proper normals) so barriers & FIA catch-fencing are 100% visible from every camera angle!
            int sideVertCount = (n + 1) * 2;
            var verts = new Vector3[sideVertCount * 2];
            var uvs = new Vector2[sideVertCount * 2];
            var tris = new int[n * 12];

            for (int i = 0; i <= n; i++)
            {
                var smp = samples[i % n];
                float s = i == n ? totalLength : smp.s;
                Vector3 smoothRight = (
                    samples[(i - 4 + n) % n].right +
                    samples[(i - 2 + n) % n].right +
                    smp.right +
                    samples[(i + 2) % n].right +
                    samples[(i + 4) % n].right).normalized;
                if (smoothRight.sqrMagnitude < 0.01f) smoothRight = smp.right;

                float effOffset = offset;
                if (Mathf.Sign(offset) == pitSide)
                    effOffset += pitSide * GetPitOuterWallExpansion(smp.s);

                Vector3 basePos = smp.pos + smoothRight * effOffset + Vector3.up * yBase;
                Vector3 topPos = basePos + Vector3.up * height;

                // Inner track-facing side
                verts[i * 2]     = basePos;
                verts[i * 2 + 1] = topPos;
                uvs[i * 2]       = new Vector2(s * uRepeat, 0f);
                uvs[i * 2 + 1]   = new Vector2(s * uRepeat, 1f);

                // Outer reverse side
                int backIdx = sideVertCount + i * 2;
                verts[backIdx]     = basePos;
                verts[backIdx + 1] = topPos;
                uvs[backIdx]       = new Vector2(-s * uRepeat, 0f);
                uvs[backIdx + 1]   = new Vector2(-s * uRepeat, 1f);

                if (i < n)
                {
                    int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                    int ba = sideVertCount + a, bb = sideVertCount + b, bc = sideVertCount + c, bd = sideVertCount + d;
                    int ti = i * 12;
                    tris[ti]     = a;  tris[ti + 1]  = b;  tris[ti + 2]  = c;
                    tris[ti + 3] = b;  tris[ti + 4]  = d;  tris[ti + 5]  = c;
                    tris[ti + 6] = ba; tris[ti + 7]  = bc; tris[ti + 8]  = bb;
                    tris[ti + 9] = bb; tris[ti + 10] = bc; tris[ti + 11] = bd;
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
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
            }
            return result;
        }

        private static Texture2D CreateHighDetailAsphaltTexture()
        {
            const int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 16;
            tex.filterMode = FilterMode.Trilinear;
            var cols = new Color32[w * h];
            uint rng = 9876543u;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    rng = rng * 1664525u + 1013904223u;
                    float grain = ((rng & 0xFFu) / 255f - 0.5f) * 0.045f;
                    float u = (float)x / w;
                    // Crisp white FIA track limit edge lines!
                    if ((u >= 0.018f && u <= 0.042f) || (u >= 0.958f && u <= 0.982f))
                    {
                        cols[y * w + x] = new Color(0.92f + grain, 0.93f + grain, 0.95f + grain, 1f);
                        continue;
                    }
                    // Dark rubbered-in F1 racing groove around center/quarter lines
                    float groove = Mathf.Exp(-Mathf.Pow((u - 0.50f) / 0.22f, 2f)) * 0.045f;
                    float baseV = 0.195f - groove + grain;
                    cols[y * w + x] = new Color(baseV, baseV * 1.02f, baseV * 1.07f, 1f);
                }
            }
            tex.SetPixels32(cols);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D CreateGrassTerrainTexture(Color tint)
        {
            const int sz = 128;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 8;
            var cols = new Color32[sz * sz];
            uint rng = 2468135u;
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    rng = rng * 1664525u + 1013904223u;
                    float n = ((rng & 0xFFu) / 255f - 0.5f) * 0.08f;
                    float stripe = ((x / 16) % 2 == 0) ? 1.04f : 0.96f; // mowed stadium grass stripes!
                    Color c = tint * stripe + new Color(n, n, n * 0.5f, 0f);
                    c.a = 1f;
                    cols[y * sz + x] = c;
                }
            }
            tex.SetPixels32(cols);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D CreateCatchFenceTexture()
        {
            const int sz = 128;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 8;
            var cols = new Color32[sz * sz];
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    bool post = x < 5 || x > sz - 5;
                    bool topRail = y > sz - 6 || (y > sz / 2 - 2 && y < sz / 2 + 2);
                    bool wire = ((x + y) % 10 == 0) || ((x - y + 256) % 10 == 0);
                    if (post || topRail)
                        cols[y * sz + x] = new Color32(42, 48, 56, 255);
                    else if (wire)
                        cols[y * sz + x] = new Color32(150, 160, 172, 220);
                    else
                        cols[y * sz + x] = new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(cols);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D CreateKerbTexture()
        {
            var tex = new Texture2D(64, 128, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 16;
            for (int y = 0; y < 128; y++)
            {
                bool red = (y / 32) % 2 == 0;
                float bevelShade = (y % 32 < 3) ? 0.72f : 1.0f;
                Color c = (red ? new Color(0.88f, 0.10f, 0.14f) : new Color(0.95f, 0.96f, 0.98f)) * bevelShade;
                c.a = 1f;
                for (int x = 0; x < 64; x++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply(true);
            return tex;
        }

        private static Texture2D CreateCheckeredTexture()
        {
            var tex = new Texture2D(128, 32, TextureFormat.RGBA32, true);
            tex.anisoLevel = 8;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 128; x++)
                    tex.SetPixel(x, y, ((x / 8 + y / 8) % 2 == 0) ? Color.white : new Color(0.06f, 0.06f, 0.08f));
            tex.Apply(true);
            return tex;
        }

        private static Texture2D CreateSignBoardTex(string label, Color bg, Color fg)
        {
            var tex = new Texture2D(128, 64, TextureFormat.RGBA32, true);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    bool border = x < 4 || x >= 124 || y < 4 || y >= 60;
                    tex.SetPixel(x, y, border ? fg : bg);
                }
            }
            // Crisp 5x7 bitmap glyphs centered on board
            DrawSimpleText(tex, label, fg);
            tex.Apply(true);
            return tex;
        }

        private static void DrawSimpleText(Texture2D tex, string text, Color col)
        {
            int scale = 4;
            int charW = 6 * scale;
            int startX = Mathf.Max(8, (tex.width - text.Length * charW) / 2);
            int startY = (tex.height - 7 * scale) / 2;
            for (int idx = 0; idx < text.Length; idx++)
            {
                uint[] rows = GetGlyph(text[idx]);
                for (int ry = 0; ry < 7; ry++)
                {
                    uint bits = rows[6 - ry];
                    for (int rx = 0; rx < 5; rx++)
                    {
                        if (((bits >> (4 - rx)) & 1u) != 0)
                        {
                            for (int sy = 0; sy < scale; sy++)
                                for (int sx = 0; sx < scale; sx++)
                                    tex.SetPixel(startX + idx * charW + rx * scale + sx, startY + ry * scale + sy, col);
                        }
                    }
                }
            }
        }

        private static uint[] GetGlyph(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case '1': return new uint[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E };
                case '5': return new uint[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E };
                case '8': return new uint[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E };
                case '0': return new uint[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E };
                case 'P': return new uint[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 };
                case 'I': return new uint[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
                case 'T': return new uint[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
                case 'M': return new uint[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 };
                case 'D': return new uint[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E };
                case 'R': return new uint[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 };
                case 'S': return new uint[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E };
                case 'E': return new uint[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
                default:  return new uint[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            }
        }
    }
}
