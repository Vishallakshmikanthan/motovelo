using System.Collections.Generic;
using UnityEngine;

namespace Motovelo
{
    public class FormulaAICar : MonoBehaviour
    {
        private float _preferredLine;
        private float _skillFactor;
        private float _currentAvoid;
        private float _aggression;
        private int _plannedPitLap;

        public void Configure(int gridIndex, int difficulty)
        {
            float baseSkill = difficulty == 0 ? 0.90f : (difficulty == 1 ? 0.97f : 1.03f);
            float frontRunnerBonus = ((21 - Mathf.Min(21, gridIndex)) / 21f) * 0.055f;
            _skillFactor = baseSkill + frontRunnerBonus;
            _aggression = 0.85f + (gridIndex % 5) * 0.05f;
            _preferredLine = (gridIndex % 2 == 0 ? -1f : 1f) * (1.6f + (gridIndex % 3) * 0.65f);
            // Stagger AI mandatory FIA tyre-compound stops across Lap 1 end & Lap 2 so pit lane feels alive!
            _plannedPitLap = (gridIndex % 2 == 0) ? 1 : 2;
        }

        public void UpdateAIInputs(FormulaCarPhysics self, List<FormulaCarPhysics> allCars, SplineCircuit track, float dt, int totalLaps = 3)
        {
            if (self.pitPhase != PitLanePhase.OnTrack)
                return; // PitLane autopilot handles EnteringPitLane / ServicingInBox / ExitingPitLane!

            float signedS = track.GetSignedS(self.projection.s);
            bool wantMandatoryPit = totalLaps >= 3 && !self.MandatoryCompoundSatisfied && self.lap >= _plannedPitLap && self.maxSectorReached >= 2;
            bool wantWearPit = self.tyreHealth < 38f && self.maxSectorReached >= 2;
            bool diveIntoPitsNow = (wantMandatoryPit || wantWearPit) && signedS >= -265f && signedS <= -115f;

            // 1. Dynamic Lookahead Target (steers smoothly into Pit Entry Mouth when pitting!)
            float lookDist = diveIntoPitsNow ? 24f : Mathf.Clamp(22f + self.speed * 0.52f, 22f, 68f);
            var targetSmp = track.GetSampleAtDistance(self.projection.s + lookDist);

            float apexShift = Mathf.Clamp(targetSmp.signedCurvature * 220f, -1f, 1f) * (track.halfWidth * 0.58f);
            float startBlend = self.lap == 1 ? Mathf.Clamp01(self.projection.s / 180f) : 1f;
            float baseLine = diveIntoPitsNow
                ? track.pitSide * (track.halfWidth + 5.8f)
                : Mathf.Lerp(_preferredLine, apexShift, startBlend);

            // 2. Curvature Braking Lookahead
            float maxCurv = targetSmp.curvature * 0.9f;
            float[] lookDists = { 32f, 68f, 108f, 148f };
            for (int k = 0; k < lookDists.Length; k++)
            {
                float d = lookDists[k];
                var s = track.GetSampleAtDistance(self.projection.s + d);
                float cornerV = Mathf.Sqrt(38.5f / (s.curvature + 0.0018f)) * _skillFactor;
                float brkDistNeeded = Mathf.Max(0f, (self.speed * self.speed - cornerV * cornerV) / (2f * 42f));
                if (brkDistNeeded + 18f >= d)
                    maxCurv = Mathf.Max(maxCurv, s.curvature);
            }

            // 3. Smart Slipstream & Overtake Lane Selection
            float targetAvoid = 0f;
            float trafficThrottleCap = 1.0f;
            float trafficBrake = 0f;

            if (!diveIntoPitsNow)
            {
                Vector3 fwd = new Vector3(Mathf.Sin(self.yaw), 0f, Mathf.Cos(self.yaw));
                for (int i = 0; i < allCars.Count; i++)
                {
                    var other = allCars[i];
                    if (other == self || other.inPit) continue;
                    Vector3 delta = other.transform.position - self.transform.position;
                    delta.y = 0f;
                    float dist = delta.magnitude;
                    if (dist > 0.5f && dist < 24f)
                    {
                        float dotFwd = Vector3.Dot(delta / dist, fwd);
                        if (dotFwd > 0.68f)
                        {
                            float latDiff = other.projection.lateralOffset - self.projection.lateralOffset;
                            if (Mathf.Abs(latDiff) < 2.9f && self.speed > other.speed - 1.5f)
                            {
                                float passSide = other.projection.lateralOffset > 0.5f ? -1f : (other.projection.lateralOffset < -0.5f ? 1f : (self.GetInstanceID() % 2 == 0 ? 1f : -1f));
                                targetAvoid = passSide * 4.4f;

                                float closingSpeed = self.speed - other.speed;
                                if (dist < 4.2f && Mathf.Abs(latDiff) < 1.8f && closingSpeed > 4.5f && self.speed > 25f)
                                {
                                    trafficThrottleCap = 0.55f;
                                    trafficBrake = Mathf.Clamp01((closingSpeed - 4.5f) / 18f) * 0.35f;
                                }
                            }
                        }
                    }
                }
            }

            _currentAvoid = Mathf.Lerp(_currentAvoid, targetAvoid, Mathf.Min(1f, dt * 5.2f));
            float desiredOff = diveIntoPitsNow
                ? baseLine
                : Mathf.Clamp(baseLine + _currentAvoid, -track.halfWidth + 1.8f, track.halfWidth - 1.8f);

            Vector3 aim = targetSmp.pos + targetSmp.right * desiredOff;
            float desiredYaw = Mathf.Atan2(aim.x - self.transform.position.x, aim.z - self.transform.position.z);
            float yawErrDeg = Mathf.DeltaAngle(self.yaw * Mathf.Rad2Deg, desiredYaw * Mathf.Rad2Deg);
            self.inputSteer = Mathf.Clamp((yawErrDeg * Mathf.Deg2Rad) * 2.35f, -1f, 1f);

            // Brake cleanly down to 80 KPH when diving into Pit Entry!
            float targetCornerSpeed = diveIntoPitsNow
                ? 22.0f
                : Mathf.Clamp(Mathf.Sqrt(39.0f / (maxCurv + 0.0018f)) * _skillFactor, 26f, 105f);

            if (self.speed > targetCornerSpeed + 3.5f)
            {
                self.inputThrottle = 0f;
                self.inputBrake = Mathf.Max(trafficBrake, Mathf.Clamp01((self.speed - targetCornerSpeed) / 18f));
            }
            else if (self.speed > targetCornerSpeed)
            {
                self.inputThrottle = Mathf.Min(trafficThrottleCap, 0.68f);
                self.inputBrake = trafficBrake;
            }
            else
            {
                self.inputThrottle = trafficThrottleCap;
                self.inputBrake = trafficBrake;
            }

            self.inputDrs = !diveIntoPitsNow && self.inDrsZone && self.inputBrake < 0.05f && Mathf.Abs(self.inputSteer) < 0.35f;
            bool overtakingOrLaunching = Mathf.Abs(_currentAvoid) > 1.2f || self.slipstream > 0.18f || (self.lap == 1 && self.projection.s < 320f);
            self.inputErs = !diveIntoPitsNow && self.inputBrake < 0.05f && self.speed > 35f && self.ersCharge > 0.25f && overtakingOrLaunching && _aggression > 0.82f;
        }
    }
}
