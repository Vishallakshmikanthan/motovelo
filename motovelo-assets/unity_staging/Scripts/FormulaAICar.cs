using System.Collections.Generic;
using UnityEngine;

namespace Motovelo
{
    public class FormulaAICar : MonoBehaviour
    {
        private float _laneOffset;
        private float _skillFactor;
        private float _currentAvoid;

        public void Configure(int gridIndex, int difficulty)
        {
            float baseSkill = difficulty == 0 ? 0.80f : (difficulty == 1 ? 0.91f : 0.99f);
            float spread = ((21 - Mathf.Min(21, gridIndex)) / 21f) * 0.07f;
            _skillFactor = baseSkill + spread;
            _laneOffset = (gridIndex % 2 == 0 ? -1f : 1f) * (1.2f + (gridIndex % 4) * 0.85f);
        }

        public void UpdateAIInputs(FormulaCarPhysics self, List<FormulaCarPhysics> allCars, SplineCircuit track, float dt)
        {
            float lookDist = Mathf.Clamp(self.speed * 0.58f, 18f, 58f);
            var targetSmp = track.GetSampleAtDistance(self.projection.s + lookDist);

            float maxCurv = targetSmp.curvature;
            float[] lookDists = { 30f, 65f, 110f, 155f };
            foreach (float d in lookDists)
            {
                var s = track.GetSampleAtDistance(self.projection.s + d);
                float w = d <= 65f ? 1.0f : (d <= 110f ? 0.85f : 0.65f);
                maxCurv = Mathf.Max(maxCurv, s.curvature * w);
            }

            float targetAvoid = 0f;
            float emergencyBrake = 0f;
            Vector3 fwd = new Vector3(Mathf.Sin(self.yaw), 0f, Mathf.Cos(self.yaw));

            for (int i = 0; i < allCars.Count; i++)
            {
                var other = allCars[i];
                if (other == self) continue;
                Vector3 delta = other.transform.position - self.transform.position;
                delta.y = 0f;
                float dist = delta.magnitude;
                if (dist > 0.1f && dist < 18f)
                {
                    float dotFwd = Vector3.Dot(delta / dist, fwd);
                    if (dotFwd > 0.60f)
                    {
                        float latDiff = other.projection.lateralOffset - self.projection.lateralOffset;
                        if (Mathf.Abs(latDiff) < 3.4f)
                        {
                            targetAvoid = other.projection.lateralOffset > 0f ? -4.2f : 4.2f;
                            if (dist < 6.2f && other.speed < self.speed + 2f)
                                emergencyBrake = 0.55f;
                        }
                    }
                }
            }

            _currentAvoid = Mathf.Lerp(_currentAvoid, targetAvoid, Mathf.Min(1f, dt * 4.5f));
            float desiredOff = Mathf.Clamp(_laneOffset + _currentAvoid, -track.halfWidth + 2.0f, track.halfWidth - 2.0f);

            Vector3 aim = targetSmp.pos + targetSmp.right * desiredOff;
            float desiredYaw = Mathf.Atan2(aim.x - self.transform.position.x, aim.z - self.transform.position.z);
            float yawErrDeg = Mathf.DeltaAngle(self.yaw * Mathf.Rad2Deg, desiredYaw * Mathf.Rad2Deg);

            self.inputSteer = Mathf.Clamp((yawErrDeg * Mathf.Deg2Rad) * 2.5f, -1f, 1f);

            float cornerSpeed = Mathf.Clamp(Mathf.Sqrt(26.5f / (maxCurv + 0.0024f)) * _skillFactor, 22f, 102f);
            if (self.speed > cornerSpeed + 4.5f)
            {
                self.inputThrottle = 0f;
                self.inputBrake = Mathf.Max(emergencyBrake, Mathf.Clamp01((self.speed - cornerSpeed) / 16f));
            }
            else if (self.speed > cornerSpeed)
            {
                self.inputThrottle = 0.42f;
                self.inputBrake = emergencyBrake;
            }
            else
            {
                self.inputThrottle = 1.0f;
                self.inputBrake = emergencyBrake;
            }

            bool onStraight = maxCurv < 0.0055f && self.speed > 48f;
            self.inputDrs = onStraight && self.inputBrake < 0.05f;
            self.inputErs = onStraight && self.ersCharge > 0.35f && (self.slipstream > 0.2f || Mathf.Abs(_currentAvoid) > 1.0f);
        }
    }
}
