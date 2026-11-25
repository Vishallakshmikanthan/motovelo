using UnityEngine;

namespace Motovelo
{
    public enum RaceCamMode
    {
        TCam = 0,
        Cockpit = 1,
        Chase = 2,
        ChaseClose = 3,
        Broadcast = 4
    }

    public class MonopostoCameraRig : MonoBehaviour
    {
        public Camera targetCamera;
        public RaceCamMode mode = RaceCamMode.TCam;
        public bool lookBack;

        private Vector3 _smoothPos;
        private Vector3 _smoothLook;
        private float _smoothRoll;
        private bool _initialized;

        public void CycleCamera()
        {
            mode = (RaceCamMode)(((int)mode + 1) % 5);
            _initialized = false;
        }

        public void ResetSnap()
        {
            _initialized = false;
            _smoothRoll = 0f;
        }

        public void UpdateShowroomCamera(Transform focusTarget, bool driverFocus, bool zoomed, float dt)
        {
            if (targetCamera == null || focusTarget == null) return;

            Vector3 desiredPos;
            Vector3 desiredLook;
            float targetFov;

            if (driverFocus)
            {
                float dist = zoomed ? 2.15f : 3.20f;
                float yPos = zoomed ? 1.50f : 1.08f;
                float yLook = zoomed ? 1.44f : 0.96f;
                desiredPos = focusTarget.position + new Vector3(0f, yPos, dist);
                desiredLook = focusTarget.position + new Vector3(0f, yLook, 0f);
                targetFov = 40f;
            }
            else
            {
                float sway = Mathf.Sin(Time.time * 0.50f);
                float distX = zoomed ? 4.25f : 5.45f;
                float distZ = (zoomed ? 1.85f : 2.35f) + sway * 0.95f;
                float height = zoomed ? 0.98f : 1.14f;
                // Sleek Monoposto 3/4 side-profile hero view (+Z Nose, +X Sidepod, Rear Wing & Mirror Floor!)
                desiredPos = focusTarget.position + new Vector3(distX, height, distZ);
                desiredLook = focusTarget.position + new Vector3(0f, 0.44f, 0.08f);
                targetFov = 38f;
            }

            targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, desiredPos, Mathf.Min(1f, dt * 6.5f));
            targetCamera.transform.LookAt(desiredLook);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Mathf.Min(1f, dt * 6.5f));
        }

        public void UpdateRaceCamera(FormulaCarPhysics car, SplineCircuit track, float dt)
        {
            if (targetCamera == null || car == null) return;

            Vector3 fwd = new Vector3(Mathf.Sin(car.yaw), 0f, Mathf.Cos(car.yaw));
            Vector3 right = new Vector3(Mathf.Cos(car.yaw), 0f, -Mathf.Sin(car.yaw));
            float speedNorm = Mathf.Clamp01(car.speed / 95f);

            float tNow = Time.time * 54f;
            float shakeAmp = car.kerbRumble * 0.016f + (speedNorm > 0.78f ? (speedNorm - 0.78f) * 0.010f : 0f);
            Vector3 shake = right * (Mathf.Sin(tNow * 1.3f) * shakeAmp) + Vector3.up * (Mathf.Cos(tNow * 1.9f) * shakeAmp);

            Vector3 desiredPos;
            Vector3 desiredLook;
            float targetFov = 68f;
            float targetRoll = 0f;

            if (lookBack)
            {
                desiredPos = car.transform.position - fwd * 0.35f + Vector3.up * 1.24f;
                desiredLook = car.transform.position - fwd * 22f + Vector3.up * 0.65f;
                targetFov = 74f + speedNorm * 8f;
            }
            else
            {
                switch (mode)
                {
                    case RaceCamMode.TCam:
                    {
                        float gShift = Mathf.Clamp(-car.lateralG * 0.018f, -0.08f, 0.08f);
                        desiredPos = car.transform.position - fwd * 0.36f + right * gShift + Vector3.up * 1.24f + shake;
                        desiredLook = car.transform.position + fwd * 13f + right * (-car.steerAngle * 2.2f) + Vector3.up * (0.36f - car.pitch * 3.0f);
                        targetFov = 67f + speedNorm * 14f + (car.drsActive ? 3f : 0f) + (car.ersActive ? 3f : 0f);
                        targetRoll = Mathf.Clamp(-car.lateralG * 0.45f, -2.5f, 2.5f);
                        break;
                    }
                    case RaceCamMode.Cockpit:
                    {
                        desiredPos = car.transform.position + fwd * 0.02f + Vector3.up * 0.81f + shake;
                        desiredLook = car.transform.position + fwd * 15f + right * (-car.steerAngle * 2.4f) + Vector3.up * (0.56f - car.pitch * 2.5f);
                        targetFov = 67f + speedNorm * 11f + (car.ersActive ? 3f : 0f);
                        targetRoll = Mathf.Clamp(-car.lateralG * 0.55f, -3.0f, 3.0f);
                        break;
                    }
                    case RaceCamMode.Chase:
                    {
                        desiredPos = car.transform.position - fwd * (6.1f + speedNorm * 1.1f) + Vector3.up * 2.15f + shake;
                        desiredLook = car.transform.position + fwd * 10f + Vector3.up * 0.82f;
                        targetFov = 63f + speedNorm * 15f + (car.ersActive ? 4f : 0f);
                        break;
                    }
                    case RaceCamMode.ChaseClose:
                    {
                        desiredPos = car.transform.position - fwd * (4.1f + speedNorm * 0.65f) + Vector3.up * 1.44f + shake;
                        desiredLook = car.transform.position + fwd * 11f + Vector3.up * 0.70f;
                        targetFov = 66f + speedNorm * 13f;
                        break;
                    }
                    default:
                    {
                        Vector3 nearest = car.transform.position + Vector3.up * 5f;
                        float bestD2 = float.MaxValue;
                        if (track != null && track.broadcastCameras.Count > 0)
                        {
                            for (int i = 0; i < track.broadcastCameras.Count; i++)
                            {
                                float d2 = (track.broadcastCameras[i] - car.transform.position).sqrMagnitude;
                                if (d2 < bestD2) { bestD2 = d2; nearest = track.broadcastCameras[i]; }
                            }
                        }
                        desiredPos = nearest;
                        desiredLook = car.transform.position + Vector3.up * 0.6f;
                        targetFov = 44f;
                        break;
                    }
                }
            }

            if (!_initialized || lookBack || mode == RaceCamMode.TCam || mode == RaceCamMode.Cockpit)
            {
                _smoothPos = desiredPos;
                _smoothLook = desiredLook;
                _initialized = true;
            }
            else if (mode == RaceCamMode.Broadcast)
            {
                _smoothPos = desiredPos;
                _smoothLook = Vector3.Lerp(_smoothLook, desiredLook, Mathf.Min(1f, dt * 14f));
            }
            else
            {
                _smoothPos = Vector3.Lerp(_smoothPos, desiredPos, Mathf.Min(1f, dt * 12f));
                _smoothLook = Vector3.Lerp(_smoothLook, desiredLook, Mathf.Min(1f, dt * 16f));
            }

            targetCamera.transform.position = _smoothPos;
            targetCamera.transform.LookAt(_smoothLook);
            _smoothRoll = Mathf.Lerp(_smoothRoll, targetRoll, Mathf.Min(1f, dt * 9f));
            if (Mathf.Abs(_smoothRoll) > 0.02f)
                targetCamera.transform.Rotate(0f, 0f, _smoothRoll, Space.Self);

            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Mathf.Min(1f, dt * 8f));
        }
    }
}
