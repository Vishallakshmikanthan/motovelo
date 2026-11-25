using System.Collections.Generic;
using UnityEngine;

namespace Motovelo
{
    public class PitStopCrewController : MonoBehaviour
    {
        private readonly List<Transform> _wheelGunners = new List<Transform>();
        private readonly List<Transform> _tyreCarriers = new List<Transform>();
        private readonly List<GameObject> _spareWheelProps = new List<GameObject>();
        private readonly List<Renderer> _sparkRenderers = new List<Renderer>();
        private Transform _frontJackman;
        private Transform _rearJackman;
        private Material _boomLedMat;
        private Material _sparkMat;

        // Wheel station local coords relative to Pit Box Center (+Z forward along pit lane, +X right)
        private static readonly Vector3[] WheelTargets =
        {
            new Vector3(-0.95f, 0f,  1.75f), // FL
            new Vector3( 0.95f, 0f,  1.75f), // FR
            new Vector3(-0.98f, 0f, -1.65f), // RL
            new Vector3( 0.98f, 0f, -1.65f)  // RR
        };

        public void BuildPitBox(
            Vector3 worldPos,
            Quaternion worldRot,
            float garageSideSign,
            TeamDef team,
            string helmetId,
            CompoundDef nextCompound,
            GameObject crewPrefab,
            GameObject boomPrefab,
            GameObject wheelPrefab)
        {
            transform.position = worldPos;
            transform.rotation = worldRot;

            foreach (Transform child in transform)
                Destroy(child.gameObject);

            _wheelGunners.Clear();
            _tyreCarriers.Clear();
            _spareWheelProps.Clear();
            _sparkRenderers.Clear();

            // Glowing Painted FIA Team Pit Box Slot Markings on Pit Apron!
            var boxGlowMat = MotoveloDatabase.CreateUrpEmissive(team.primaryColor, team.primaryColor * 1.8f + new Color(0.1f, 0.8f, 0.3f), 0.7f);
            var yellowLineMat = MotoveloDatabase.CreateUrpEmissive(new Color(1f, 0.85f, 0.1f), new Color(1.6f, 1.3f, 0.12f), 0.7f);

            for (int s = -1; s <= 1; s += 2)
            {
                var sideStripe = MotoveloDatabase.CreateQuadObject($"BoxSideStripe_{s}", transform);
                sideStripe.transform.localPosition = new Vector3(s * 1.42f, 0.045f, 0f);
                sideStripe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                sideStripe.transform.localScale = new Vector3(0.22f, 5.6f, 1f);
                sideStripe.GetComponent<Renderer>().sharedMaterial = yellowLineMat;
            }

            var stopLine = MotoveloDatabase.CreateQuadObject("FrontStopMark", transform);
            stopLine.transform.localPosition = new Vector3(0f, 0.048f, 2.55f);
            stopLine.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            stopLine.transform.localScale = new Vector3(3.0f, 0.32f, 1f);
            stopLine.GetComponent<Renderer>().sharedMaterial = boxGlowMat;

            // Overhead Pneumatic Pit Boom Gantry
            if (boomPrefab != null)
            {
                var boom = Instantiate(boomPrefab, transform);
                boom.transform.localPosition = Vector3.zero;
                // Flip X if garage is on -X side so mast always stands on the garage side!
                boom.transform.localScale = new Vector3(garageSideSign >= 0f ? 1f : -1f, 1f, 1f);
                boom.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                _boomLedMat = MotoveloDatabase.CreateUrpEmissive(new Color(0.95f, 0.72f, 0.06f), new Color(2.0f, 1.4f, 0.1f), 0.8f);
                var teamRed = MotoveloDatabase.CreateUrpLit(team.primaryColor, null, 0.35f, 0.68f);
                var darkSteel = MotoveloDatabase.CreateUrpLit(new Color(0.14f, 0.16f, 0.20f), null, 0.78f, 0.65f);

                foreach (var r in boom.GetComponentsInChildren<Renderer>())
                {
                    string mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mn.Contains("PitBoxLight")) r.sharedMaterial = _boomLedMat;
                    else if (mn.Contains("PaddockRed")) r.sharedMaterial = teamRed;
                    else r.sharedMaterial = darkSteel;
                }
            }

            if (crewPrefab == null) return;

            var suitTex   = MotoveloDatabase.LoadTex($"Textures/suits/{team.id}");
            var helmetTex = MotoveloDatabase.LoadTex($"Textures/helmets/{helmetId}");
            var suitMat   = MotoveloDatabase.CreateUrpLit(Color.white, suitTex, 0.06f, 0.42f);
            var helmetMat = MotoveloDatabase.CreateUrpLit(Color.white, helmetTex, 0.28f, 0.85f);
            var visorMat  = MotoveloDatabase.CreateUrpLit(new Color(0.02f, 0.05f, 0.14f), null, 0.95f, 0.95f);
            var bootMat   = MotoveloDatabase.CreateUrpLit(new Color(0.08f, 0.09f, 0.11f), null, 0.35f, 0.50f);
            _sparkMat     = MotoveloDatabase.CreateUrpEmissive(new Color(0.3f, 0.85f, 1f), Color.black, 0.9f);

            Transform SpawnMechanic(string roleName, Vector3 localPos, float yawDeg)
            {
                var m = Instantiate(crewPrefab, transform);
                m.name = roleName;
                m.transform.localPosition = localPos;
                // crewPrefab faces +Z in Unity after 180 flip
                m.transform.localRotation = Quaternion.Euler(0f, yawDeg + 180f, 0f);
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                {
                    string mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mn.Contains("DriverSuitMat")) r.sharedMaterial = suitMat;
                    else if (mn.Contains("DriverHelmetMat")) r.sharedMaterial = helmetMat;
                    else if (mn.Contains("Visor")) r.sharedMaterial = visorMat;
                    else r.sharedMaterial = bootMat;
                }
                return m.transform;
            }

            // 1. 4 Wheel Gunners + 4 Tyre Carriers + 4 Fresh Pirelli Spare Wheels waiting at each corner!
            for (int i = 0; i < 4; i++)
            {
                Vector3 wt = WheelTargets[i];
                float side = Mathf.Sign(wt.x);
                float faceCarYaw = side < 0f ? 90f : -90f;

                var gunner = SpawnMechanic($"WheelGunner_{i}", new Vector3(side * 2.15f, 0f, wt.z), faceCarYaw);
                _wheelGunners.Add(gunner);

                // Spark emitter at wheel gun tip!
                var spark = MotoveloDatabase.CreateCubeObject($"GunSpark_{i}", transform);
                spark.transform.localPosition = new Vector3(side * 1.05f, 0.36f, wt.z);
                spark.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
                var sr = spark.GetComponent<Renderer>();
                sr.sharedMaterial = _sparkMat;
                spark.SetActive(false);
                _sparkRenderers.Add(sr);

                var carrier = SpawnMechanic($"TyreCarrier_{i}", new Vector3(side * 2.45f, 0f, wt.z - 0.62f), faceCarYaw);
                _tyreCarriers.Add(carrier);

                if (wheelPrefab != null)
                {
                    var spWheel = Instantiate(wheelPrefab, carrier);
                    spWheel.name = $"FreshTyre_{i}";
                    spWheel.transform.localPosition = new Vector3(0f, 0.72f, -0.52f);
                    spWheel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    spWheel.transform.localScale = new Vector3(0.92f, 0.92f, 0.92f);
                    _spareWheelProps.Add(spWheel);
                }
            }

            UpdateSpareWheelCompoundVisuals(nextCompound);

            // 2. Front Jackman & Rear Jackman (stand ready just clear of the lane, step in when car arrives!)
            _frontJackman = SpawnMechanic("FrontJackman", new Vector3(garageSideSign * 1.95f, 0f, 3.25f), 180f);
            _rearJackman  = SpawnMechanic("RearJackman",  new Vector3(garageSideSign * 1.95f, 0f, -3.15f), 0f);
        }

        public void UpdateSpareWheelCompoundVisuals(CompoundDef comp)
        {
            if (comp == null) return;
            var tyreTex = MotoveloDatabase.LoadTex($"Textures/tyres/{comp.id}");
            var sideMat = MotoveloDatabase.CreateUrpLit(Color.white, tyreTex, 0.06f, 0.48f, true);
            var rubMat  = MotoveloDatabase.CreateUrpLit(new Color(0.045f, 0.048f, 0.054f), null, 0.04f, 0.28f);

            foreach (var w in _spareWheelProps)
            {
                if (w == null) continue;
                foreach (var r in w.GetComponentsInChildren<Renderer>())
                {
                    string mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mn.Contains("TireSidewall")) r.sharedMaterial = sideMat;
                    else r.sharedMaterial = rubMat;
                }
            }
        }

        /// <summary>
        /// Drives the live 12-man pit crew animation!
        /// phaseNorm: 0 when waiting idle; 0..1 during active 2.5s pit stop!
        /// Returns vertical jack lift height in meters (+0.15m while car is up on jacks).
        /// </summary>
        public float AnimateCrew(bool activeStop, float phaseNorm, float garageSideSign)
        {
            if (!activeStop)
            {
                // Waiting ready stance clear of the 3.2m pit box lane!
                for (int i = 0; i < _wheelGunners.Count; i++)
                {
                    float side = Mathf.Sign(WheelTargets[i].x);
                    _wheelGunners[i].localPosition = Vector3.Lerp(_wheelGunners[i].localPosition, new Vector3(side * 2.15f, 0f, WheelTargets[i].z), 0.15f);
                    _tyreCarriers[i].localPosition = Vector3.Lerp(_tyreCarriers[i].localPosition, new Vector3(side * 2.45f, 0f, WheelTargets[i].z - 0.62f), 0.15f);
                    if (i < _sparkRenderers.Count) _sparkRenderers[i].gameObject.SetActive(false);
                    if (i < _spareWheelProps.Count && _spareWheelProps[i] != null) _spareWheelProps[i].SetActive(true);
                }
                if (_frontJackman != null)
                    _frontJackman.localPosition = Vector3.Lerp(_frontJackman.localPosition, new Vector3(garageSideSign * 1.95f, 0f, 3.25f), 0.15f);
                if (_rearJackman != null)
                    _rearJackman.localPosition = Vector3.Lerp(_rearJackman.localPosition, new Vector3(garageSideSign * 1.95f, 0f, -3.15f), 0.15f);

                if (_boomLedMat != null && _boomLedMat.HasProperty("_EmissionColor"))
                {
                    _boomLedMat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.08f));
                    _boomLedMat.SetColor("_EmissionColor", new Color(2.0f, 1.5f, 0.1f));
                }
                return 0f;
            }

            // Active 2.5s F1 Pit Stop Choreography:
            // 0.00..0.18: Crew rushes in & Jackmen lift car +0.15m
            // 0.18..0.48: Wheel gunners fire sparks (loosening nuts) & carriers pull old wheels
            // 0.50:       New Pirelli tyre compound applied!
            // 0.50..0.82: Carriers fit fresh wheels & gunners fire sparks (tightening nuts)
            // 0.82..1.00: Crew steps clear, Jacks drop car, Overhead Light flashes GREEN "GO! GO! GO!"
            bool crewIn = phaseNorm < 0.84f;
            float inBlend = crewIn ? Mathf.SmoothStep(0f, 1f, phaseNorm / 0.16f) : Mathf.SmoothStep(1f, 0f, (phaseNorm - 0.84f) / 0.16f);

            bool gunFiring = (phaseNorm > 0.16f && phaseNorm < 0.44f) || (phaseNorm > 0.54f && phaseNorm < 0.80f);
            bool blink = Mathf.FloorToInt(Time.time * 28f) % 2 == 0;

            for (int i = 0; i < _wheelGunners.Count; i++)
            {
                float side = Mathf.Sign(WheelTargets[i].x);
                float gunX = Mathf.Lerp(side * 2.15f, side * 1.32f, inBlend);
                float carX = Mathf.Lerp(side * 2.45f, side * 1.48f, inBlend);
                float carZ = Mathf.Lerp(WheelTargets[i].z - 0.62f, WheelTargets[i].z - 0.18f, inBlend);

                _wheelGunners[i].localPosition = new Vector3(gunX, 0f, WheelTargets[i].z);
                _tyreCarriers[i].localPosition = new Vector3(carX, 0f, carZ);

                if (i < _spareWheelProps.Count && _spareWheelProps[i] != null)
                    _spareWheelProps[i].SetActive(phaseNorm < 0.52f);

                if (i < _sparkRenderers.Count)
                {
                    bool showSpark = gunFiring && blink;
                    _sparkRenderers[i].gameObject.SetActive(showSpark);
                    if (showSpark)
                        _sparkRenderers[i].transform.localRotation = Quaternion.Euler(Time.time * 420f, Time.time * 310f, 0f);
                }
            }

            if (_sparkMat != null && _sparkMat.HasProperty("_EmissionColor"))
                _sparkMat.SetColor("_EmissionColor", new Color(0.4f, 2.4f, 3.6f));

            if (_frontJackman != null)
                _frontJackman.localPosition = new Vector3(Mathf.Lerp(garageSideSign * 1.95f, 0f, inBlend), 0f, Mathf.Lerp(3.25f, 2.82f, inBlend));
            if (_rearJackman != null)
                _rearJackman.localPosition = new Vector3(Mathf.Lerp(garageSideSign * 1.95f, 0f, inBlend), 0f, Mathf.Lerp(-3.15f, -2.68f, inBlend));

            if (_boomLedMat != null && _boomLedMat.HasProperty("_EmissionColor"))
            {
                bool greenGo = phaseNorm >= 0.84f;
                _boomLedMat.SetColor("_BaseColor", greenGo ? new Color(0.08f, 1f, 0.32f) : new Color(1f, 0.10f, 0.10f));
                _boomLedMat.SetColor("_EmissionColor", greenGo ? new Color(0.2f, 3.4f, 0.7f) : new Color(2.8f, 0.12f, 0.12f));
            }

            float jackLift = 0f;
            if (phaseNorm > 0.08f && phaseNorm < 0.86f)
            {
                float up = Mathf.Clamp01((phaseNorm - 0.08f) / 0.10f);
                float dn = Mathf.Clamp01((0.86f - phaseNorm) / 0.08f);
                jackLift = Mathf.Min(up, dn) * 0.15f;
            }
            return jackLift;
        }
    }
}
