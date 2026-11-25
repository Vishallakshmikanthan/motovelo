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

            // Use car's true 3D sloped & banked basis so hills, crests & banked corners feel completely smooth & natural!
            Vector3 fwd = car.transform.forward;
            Vector3 right = car.transform.right;
            Vector3 up = car.transform.up;
            float speedNorm = Mathf.Clamp01(car.speed / 96f);

            float tNow = Time.time * 48f;
            float shakeAmp = car.kerbRumble * 0.012f + (speedNorm > 0.82f ? (speedNorm - 0.82f) * 0.006f : 0f);
            Vector3 shake = right * (Mathf.Sin(tNow * 1.3f) * shakeAmp) + up * (Mathf.Cos(tNow * 1.9f) * shakeAmp);

            Vector3 desiredPos;
            Vector3 desiredLook;
            Vector3 desiredUp = Vector3.Slerp(Vector3.up, up, mode == RaceCamMode.Chase || mode == RaceCamMode.ChaseClose ? 0.55f : 0.85f);
            float targetFov = 68f;
            float targetRoll = 0f;

            if (car.pitPhase == PitLanePhase.ServicingInBox)
            {
                // Iconic F1 TV Broadcast Pit-Box Camera watching the 10-man crew lift the car, fire wheel guns & fit fresh tyres!
                float pSide = track != null ? track.pitSide : 1f;
                desiredPos = car.transform.position + fwd * 4.5f - right * (pSide * 2.85f) + Vector3.up * 1.72f;
                desiredLook = car.transform.position + Vector3.up * 0.62f;
                desiredUp = Vector3.up;
                targetFov = 54f;
                _smoothPos = Vector3.Lerp(_smoothPos, desiredPos, Mathf.Min(1f, dt * 8f));
                _smoothLook = Vector3.Lerp(_smoothLook, desiredLook, Mathf.Min(1f, dt * 10f));
                targetCamera.transform.position = _smoothPos;
                targetCamera.transform.LookAt(_smoothLook, Vector3.up);
                targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Mathf.Min(1f, dt * 8f));
                return;
            }

            if (lookBack)
            {
                desiredPos = car.transform.position - fwd * 0.35f + up * 1.24f;
                desiredLook = car.transform.position - fwd * 22f + up * 0.65f;
                targetFov = 74f + speedNorm * 8f;
            }
            else
            {
                switch (mode)
                {
                    case RaceCamMode.TCam:
                    {
                        float gShift = Mathf.Clamp(-car.lateralG * 0.014f, -0.06f, 0.06f);
                        desiredPos = car.transform.position - fwd * 0.36f + right * gShift + up * 1.24f + shake;
                        desiredLook = car.transform.position + fwd * 14f + right * (-car.steerAngle * 1.8f) + up * 0.38f;
                        targetFov = 66f + speedNorm * 13f + (car.drsActive ? 2.5f : 0f) + (car.ersActive ? 2.5f : 0f);
                        targetRoll = Mathf.Clamp(-car.lateralG * 0.35f, -2.0f, 2.0f);
                        break;
                    }
                    case RaceCamMode.Cockpit:
                    {
                        desiredPos = car.transform.position + fwd * 0.02f + up * 0.81f + shake;
                        desiredLook = car.transform.position + fwd * 16f + right * (-car.steerAngle * 2.0f) + up * 0.58f;
                        targetFov = 66f + speedNorm * 10f + (car.ersActive ? 2.5f : 0f);
                        targetRoll = Mathf.Clamp(-car.lateralG * 0.40f, -2.4f, 2.4f);
                        break;
                    }
                    case RaceCamMode.Chase:
                    {
                        desiredPos = car.transform.position - fwd * (5.9f + speedNorm * 0.85f) + up * 2.05f + shake;
                        desiredLook = car.transform.position + fwd * 10f + up * 0.80f;
                        targetFov = 62f + speedNorm * 13f + (car.drsActive ? 2.5f : 0f) + (car.ersActive ? 3f : 0f);
                        break;
                    }
                    case RaceCamMode.ChaseClose:
                    {
                        desiredPos = car.transform.position - fwd * (4.1f + speedNorm * 0.55f) + up * 1.44f + shake;
                        desiredLook = car.transform.position + fwd * 11f + up * 0.70f;
                        targetFov = 65f + speedNorm * 11f;
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
                        desiredUp = Vector3.up;
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
                _smoothPos = Vector3.Lerp(_smoothPos, desiredPos, Mathf.Min(1f, dt * 14f));
                _smoothLook = Vector3.Lerp(_smoothLook, desiredLook, Mathf.Min(1f, dt * 18f));
            }

            targetCamera.transform.position = _smoothPos;
            Vector3 lookDir = _smoothLook - _smoothPos;
            if (lookDir.sqrMagnitude > 0.001f)
                targetCamera.transform.rotation = Quaternion.LookRotation(lookDir.normalized, desiredUp);

            _smoothRoll = Mathf.Lerp(_smoothRoll, targetRoll, Mathf.Min(1f, dt * 9f));
            if (Mathf.Abs(_smoothRoll) > 0.02f)
                targetCamera.transform.Rotate(0f, 0f, _smoothRoll, Space.Self);

            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Mathf.Min(1f, dt * 8f));
        }
    }
}
