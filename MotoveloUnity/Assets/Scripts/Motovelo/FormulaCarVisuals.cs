using UnityEngine;

namespace Motovelo
{
    public class FormulaCarVisuals : MonoBehaviour
    {
        public Transform bodyRoot;
        public Transform frontLeftPivot;
        public Transform frontRightPivot;
        public Transform[] wheelMeshes = new Transform[4];
        public Transform drsFlap;
        public Transform driverHelmet;
        public Transform steeringWheel;

        private Material _rainLightMat;
        private Material _drsLedMat;
        private float _jackLift;

        public void Assemble(
            GameObject carChassisPrefab,
            GameObject wheelPrefab,
            TeamDef team,
            string helmetId,
            string suitId,
            CompoundDef compound)
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            var bodyGo = new GameObject("BodyRoot");
            bodyGo.transform.SetParent(transform, false);
            bodyRoot = bodyGo.transform;

            var chassis = Instantiate(carChassisPrefab, bodyRoot);
            chassis.name = "Chassis";
            chassis.transform.localPosition = Vector3.zero;
            chassis.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var liveryTex = MotoveloDatabase.LoadTex($"Textures/liveries/{team.id}");
            var helmetTex = MotoveloDatabase.LoadTex($"Textures/helmets/{helmetId}");
            var suitTex   = MotoveloDatabase.LoadTex($"Textures/suits/{suitId}");
            var tyreTex   = MotoveloDatabase.LoadTex($"Textures/tyres/{compound.id}");

            var liveryMat = MotoveloDatabase.CreateUrpLit(Color.white, liveryTex, 0.32f, 0.84f);
            var carbonMat = MotoveloDatabase.CreateUrpLit(new Color(0.045f, 0.050f, 0.058f), null, 0.28f, 0.68f);
            var helmetMat = MotoveloDatabase.CreateUrpLit(Color.white, helmetTex, 0.28f, 0.88f);
            var suitMat   = MotoveloDatabase.CreateUrpLit(Color.white, suitTex, 0.05f, 0.38f);
            var visorMat  = MotoveloDatabase.CreateUrpLit(new Color(0.02f, 0.05f, 0.14f), null, 0.96f, 0.97f);
            var mirrorMat = MotoveloDatabase.CreateUrpLit(new Color(0.82f, 0.87f, 0.94f), null, 0.98f, 0.97f);
            _rainLightMat = MotoveloDatabase.CreateUrpEmissive(new Color(1f, 0.06f, 0.06f), new Color(2.6f, 0.1f, 0.1f), 0.85f);
            _drsLedMat    = MotoveloDatabase.CreateUrpEmissive(new Color(0.10f, 0.18f, 0.12f), Color.black, 0.85f);

            Transform rawDrs = null;
            Transform rawHelmet = null;
            Transform rawSteer = null;

            foreach (var r in chassis.GetComponentsInChildren<Renderer>())
            {
                string objName = r.gameObject.name;
                if (objName.Contains("RearWingDRS")) rawDrs = r.transform;
                if (objName.Contains("DriverHelmet")) rawHelmet = r.transform;
                if (objName.Contains("SteeringWheelBody")) rawSteer = r.transform;

                string mName = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (mName.Contains("CarLivery")) r.sharedMaterial = liveryMat;
                else if (mName.Contains("DriverHelmetMat")) r.sharedMaterial = helmetMat;
                else if (mName.Contains("DriverSuitMat")) r.sharedMaterial = suitMat;
                else if (mName.Contains("Visor")) r.sharedMaterial = visorMat;
                else if (mName.Contains("MirrorGlass")) r.sharedMaterial = mirrorMat;
                else if (mName.Contains("RainLight")) r.sharedMaterial = _rainLightMat;
                else r.sharedMaterial = carbonMat;
            }

            // Pivot DRS upper flap around its upper-rear hinge so opening flips the leading edge clearly UP & FLAT (-44 deg)!
            drsFlap = WrapInPivotWithOffset(rawDrs, "DrsHingePivot", new Vector3(0f, 0.03f, -0.06f));
            driverHelmet = WrapInPivotWithOffset(rawHelmet, "HelmetNeckPivot", Vector3.zero);
            steeringWheel = WrapInPivotWithOffset(rawSteer, "SteeringShaftPivot", Vector3.zero);

            // Add twin FIA Rear Endplate DRS Status LEDs (glow neon-green when DRS opens!)
            for (int s = -1; s <= 1; s += 2)
            {
                var led = MotoveloDatabase.CreateCubeObject($"DrsWingtipLed_{s}", bodyRoot);
                led.transform.localPosition = new Vector3(s * 0.545f, 0.96f, -2.14f);
                led.transform.localScale = new Vector3(0.025f, 0.045f, 0.16f);
                led.GetComponent<Renderer>().sharedMaterial = _drsLedMat;
            }

            // Build 4 Articulated Wheels
            var rubberMat   = MotoveloDatabase.CreateUrpLit(new Color(0.045f, 0.048f, 0.054f), null, 0.04f, 0.28f);
            var sidewallMat = MotoveloDatabase.CreateUrpLit(Color.white, tyreTex, 0.06f, 0.48f, true);

            Transform SpawnWheel(string wName, Transform parent, Vector3 localPos, bool isRear)
            {
                var w = Instantiate(wheelPrefab, parent);
                w.name = wName;
                w.transform.localPosition = localPos;
                w.transform.localRotation = Quaternion.identity;
                if (isRear) w.transform.localScale = new Vector3(1.16f, 1.03f, 1.03f);

                foreach (var r in w.GetComponentsInChildren<Renderer>())
                {
                    string mName = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mName.Contains("TireSidewall")) r.sharedMaterial = sidewallMat;
                    else if (mName.Contains("WheelCoverLivery")) r.sharedMaterial = liveryMat;
                    else r.sharedMaterial = rubberMat;
                }
                return w.transform;
            }

            var flPivotGo = new GameObject("PivotFL");
            flPivotGo.transform.SetParent(transform, false);
            flPivotGo.transform.localPosition = new Vector3(-0.88f, 0.36f, 1.75f);
            frontLeftPivot = flPivotGo.transform;
            wheelMeshes[0] = SpawnWheel("WheelFL", frontLeftPivot, Vector3.zero, false);

            var frPivotGo = new GameObject("PivotFR");
            frPivotGo.transform.SetParent(transform, false);
            frPivotGo.transform.localPosition = new Vector3(0.88f, 0.36f, 1.75f);
            frontRightPivot = frPivotGo.transform;
            wheelMeshes[1] = SpawnWheel("WheelFR", frontRightPivot, Vector3.zero, false);

            wheelMeshes[2] = SpawnWheel("WheelRL", transform, new Vector3(-0.91f, 0.37f, -1.65f), true);
            wheelMeshes[3] = SpawnWheel("WheelRR", transform, new Vector3(0.91f, 0.37f, -1.65f), true);
        }

        public void SetJackLift(float liftMeters)
        {
            _jackLift = liftMeters;
        }

        public void SwapTyreCompound(CompoundDef newCompound)
        {
            if (newCompound == null) return;
            var tyreTex = MotoveloDatabase.LoadTex($"Textures/tyres/{newCompound.id}");
            var sidewallMat = MotoveloDatabase.CreateUrpLit(Color.white, tyreTex, 0.06f, 0.48f, true);
            for (int i = 0; i < 4; i++)
            {
                if (wheelMeshes[i] == null) continue;
                foreach (var r in wheelMeshes[i].GetComponentsInChildren<Renderer>())
                {
                    string mName = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mName.Contains("TireSidewall"))
                        r.sharedMaterial = sidewallMat;
                }
            }
        }

        public void SyncPose(float pitchRad, float rollRad, float steerRad, float wheelSpinRad, bool drsOpen, bool ersHarvesting = false)
        {
            if (bodyRoot != null)
            {
                bodyRoot.localPosition = new Vector3(0f, _jackLift, 0f);
                bodyRoot.localRotation = Quaternion.Euler(pitchRad * Mathf.Rad2Deg, 0f, rollRad * Mathf.Rad2Deg);
            }

            float steerDeg = -steerRad * Mathf.Rad2Deg * 1.45f;
            if (frontLeftPivot != null)
            {
                frontLeftPivot.localPosition = new Vector3(-0.88f, 0.36f + _jackLift, 1.75f);
                frontLeftPivot.localRotation = Quaternion.Euler(0f, steerDeg, 0f);
            }
            if (frontRightPivot != null)
            {
                frontRightPivot.localPosition = new Vector3(0.88f, 0.36f + _jackLift, 1.75f);
                frontRightPivot.localRotation = Quaternion.Euler(0f, steerDeg, 0f);
            }
            if (wheelMeshes[2] != null) wheelMeshes[2].localPosition = new Vector3(-0.91f, 0.37f + _jackLift, -1.65f);
            if (wheelMeshes[3] != null) wheelMeshes[3].localPosition = new Vector3( 0.91f, 0.37f + _jackLift, -1.65f);

            float spinDeg = wheelSpinRad * Mathf.Rad2Deg;
            for (int i = 0; i < 4; i++)
            {
                if (wheelMeshes[i] != null)
                    wheelMeshes[i].localRotation = Quaternion.Euler(spinDeg, 0f, 0f);
            }

            if (driverHelmet != null)
                driverHelmet.localRotation = Quaternion.Euler(0f, steerDeg * 0.45f, -rollRad * Mathf.Rad2Deg * 1.3f);

            if (steeringWheel != null)
                steeringWheel.localRotation = Quaternion.Euler(0f, 0f, -steerDeg * 2.4f);

            if (drsFlap != null)
            {
                // Rotate +42 deg around trailing hinge so leading edge of upper DRS flap lifts wide open & horizontal!
                Quaternion targetRot = Quaternion.Euler(drsOpen ? 42f : 0f, 0f, 0f);
                drsFlap.localRotation = Quaternion.Slerp(drsFlap.localRotation, targetRot, 0.28f);
            }

            if (_drsLedMat != null && _drsLedMat.HasProperty("_EmissionColor"))
            {
                _drsLedMat.SetColor("_BaseColor", drsOpen ? new Color(0.1f, 1f, 0.35f) : new Color(0.10f, 0.14f, 0.10f));
                _drsLedMat.SetColor("_EmissionColor", drsOpen ? new Color(0.2f, 3.2f, 0.8f) : Color.black);
            }

            if (_rainLightMat != null && _rainLightMat.HasProperty("_EmissionColor"))
            {
                // Authentic FIA MGU-K Harvesting Blink in warm ruby-red (no ACES magenta shift!)
                bool blinkOn = !ersHarvesting || (Mathf.FloorToInt(Time.time * 10f) % 2 == 0);
                _rainLightMat.SetColor("_BaseColor", new Color(0.95f, 0.12f, 0.04f));
                _rainLightMat.SetColor("_EmissionColor", blinkOn ? new Color(2.2f, 0.24f, 0.02f) : new Color(0.18f, 0.02f, 0.01f));
            }
        }

        private Transform WrapInPivotWithOffset(Transform part, string pivotName, Vector3 localOffset)
        {
            if (part == null) return null;
            var rend = part.GetComponent<Renderer>();
            if (rend == null) return null;

            var pivotGo = new GameObject(pivotName);
            pivotGo.transform.SetParent(bodyRoot, false);
            Vector3 localCenter = bodyRoot.InverseTransformPoint(rend.bounds.center) + localOffset;
            pivotGo.transform.localPosition = localCenter;
            pivotGo.transform.localRotation = Quaternion.identity;
            part.SetParent(pivotGo.transform, true);
            return pivotGo.transform;
        }
    }
}
