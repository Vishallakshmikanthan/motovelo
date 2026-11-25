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
            // Rotate 180 deg Y so sculpted MonocoqueNose + FrontWing + Halo face +Z forward!
            chassis.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var liveryTex = MotoveloDatabase.LoadTex($"Textures/liveries/{team.id}");
            var helmetTex = MotoveloDatabase.LoadTex($"Textures/helmets/{helmetId}");
            var suitTex   = MotoveloDatabase.LoadTex($"Textures/suits/{suitId}");
            var tyreTex   = MotoveloDatabase.LoadTex($"Textures/tyres/{compound.id}");

            var liveryMat = MotoveloDatabase.CreateUrpLit(Color.white, liveryTex, 0.28f, 0.80f);
            var carbonMat = MotoveloDatabase.CreateUrpLit(new Color(0.04f, 0.045f, 0.052f), null, 0.22f, 0.62f);
            var helmetMat = MotoveloDatabase.CreateUrpLit(Color.white, helmetTex, 0.25f, 0.86f);
            var suitMat   = MotoveloDatabase.CreateUrpLit(Color.white, suitTex, 0.05f, 0.35f);
            var visorMat  = MotoveloDatabase.CreateUrpLit(new Color(0.02f, 0.05f, 0.14f), null, 0.95f, 0.96f);
            var mirrorMat = MotoveloDatabase.CreateUrpLit(new Color(0.80f, 0.85f, 0.92f), null, 0.98f, 0.96f);
            var rainMat   = MotoveloDatabase.CreateUrpEmissive(new Color(1f, 0.06f, 0.06f), new Color(2.5f, 0.1f, 0.1f), 0.8f);

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
                else if (mName.Contains("RainLight")) r.sharedMaterial = rainMat;
                else r.sharedMaterial = carbonMat;
            }

            drsFlap = WrapInCenteredPivot(rawDrs, "DrsHingePivot");
            driverHelmet = WrapInCenteredPivot(rawHelmet, "HelmetNeckPivot");
            steeringWheel = WrapInCenteredPivot(rawSteer, "SteeringShaftPivot");

            // Build 4 Articulated Wheels
            var rubberMat   = MotoveloDatabase.CreateUrpLit(new Color(0.045f, 0.047f, 0.052f), null, 0.03f, 0.24f);
            var sidewallMat = MotoveloDatabase.CreateUrpLit(Color.white, tyreTex, 0.05f, 0.45f, true);

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

            // Note: FBX exported with +Z forward, +Y up! Front axle z = +1.75, Rear axle z = -1.65
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

        public void SyncPose(float pitchRad, float rollRad, float steerRad, float wheelSpinRad, bool drsOpen)
        {
            if (bodyRoot != null)
                bodyRoot.localRotation = Quaternion.Euler(pitchRad * Mathf.Rad2Deg, 0f, rollRad * Mathf.Rad2Deg);

            float steerDeg = -steerRad * Mathf.Rad2Deg * 1.45f;
            if (frontLeftPivot != null)  frontLeftPivot.localRotation  = Quaternion.Euler(0f, steerDeg, 0f);
            if (frontRightPivot != null) frontRightPivot.localRotation = Quaternion.Euler(0f, steerDeg, 0f);

            float spinDeg = wheelSpinRad * Mathf.Rad2Deg;
            for (int i = 0; i < 4; i++)
            {
                if (wheelMeshes[i] != null)
                    wheelMeshes[i].localRotation = Quaternion.Euler(spinDeg, 0f, 0f);
            }

            if (driverHelmet != null)
                driverHelmet.localRotation = Quaternion.Euler(0f, steerDeg * 0.42f, -rollRad * Mathf.Rad2Deg * 1.3f);

            if (steeringWheel != null)
                steeringWheel.localRotation = Quaternion.Euler(0f, 0f, -steerDeg * 2.4f);

            if (drsFlap != null)
            {
                Quaternion targetRot = Quaternion.Euler(drsOpen ? -20f : 0f, 0f, 0f);
                drsFlap.localRotation = Quaternion.Slerp(drsFlap.localRotation, targetRot, 0.25f);
            }
        }

        private Transform WrapInCenteredPivot(Transform part, string pivotName)
        {
            if (part == null) return null;
            var rend = part.GetComponent<Renderer>();
            if (rend == null) return null;

            Transform parent = part.parent;
            var pivotGo = new GameObject(pivotName);
            pivotGo.transform.SetParent(bodyRoot, false);
            Vector3 localCenter = bodyRoot.InverseTransformPoint(rend.bounds.center);
            pivotGo.transform.localPosition = localCenter;
            pivotGo.transform.localRotation = Quaternion.identity;
            part.SetParent(pivotGo.transform, true);
            return pivotGo.transform;
        }
    }
}
