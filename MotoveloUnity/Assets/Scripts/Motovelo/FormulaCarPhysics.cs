using UnityEngine;

namespace Motovelo
{
    public class FormulaCarPhysics : MonoBehaviour
    {
        public string carId;
        public bool isPlayer;
        public TeamDef team;
        public DriverDef driver;
        public CompoundDef compound;
        public FormulaCarVisuals visuals;

        // Car Setup (-50..+50)
        public float wingsSetup = -15f;
        public float steerSensitivity = 1.0f;
        public bool autoGear = true;
        public bool drivingAssist = true; // Effortless Monoposto-style corner braking + apex assist!

        // Inputs
        public float inputThrottle;
        public float inputBrake;
        public float inputSteer;
        public bool inputErs;
        public bool inputDrs;
        public bool inputShiftUp;
        public bool inputShiftDown;

        // Kinematic & Powertrain State
        public float yaw;
        public float speed; // m/s
        public float lateralVel;
        public float smoothSteer;
        public float steerAngle;
        public float wheelSpin;
        public int gear = 1;
        public float rpm = 5200f;
        public float ersCharge = 1f;
        public bool ersActive;
        public bool drsActive;
        public float slipstream;
        public float slipAmount;
        public float kerbRumble;
        public float lateralG;
        public float pitch;
        public float roll;

        // Telemetry & Pit State
        public float tyreTemp = 86f;
        public float tyreHealth = 100f;
        public int pitStops;
        public bool inPit;
        public bool pitRequested;

        // Race Timing & Progress
        public int trackHintIdx = -1;
        public TrackProjection projection;
        public int lap = 1;
        public int maxSectorReached;
        public float totalProgress;
        public float lapStartTime;
        public float sectorStartTime;
        public float[] sectorTimes = new float[3] { -1f, -1f, -1f };
        public float lastLapTime = float.PositiveInfinity;
        public float bestLapTime = float.PositiveInfinity;
        public bool finished;
        public float finishTime;

        private static readonly float[] GearTopSpeed = { 0f, 32f, 46f, 59f, 71f, 82f, 91f, 98f, 106f };

        public void ResetOnGrid(Vector3 worldPos, float startYawRad, SplineCircuit track)
        {
            transform.position = worldPos;
            yaw = startYawRad;
            transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
            speed = 0f;
            lateralVel = 0f;
            smoothSteer = 0f;
            steerAngle = 0f;
            gear = 1;
            rpm = 5400f;
            ersCharge = 1f;
            ersActive = false;
            drsActive = false;
            slipstream = 0f;
            slipAmount = 0f;
            kerbRumble = 0f;
            tyreTemp = 86f;
            tyreHealth = 100f;
            pitStops = 0;
            inPit = false;
            pitRequested = false;

            lap = 1;
            maxSectorReached = 0;
            totalProgress = 0f;
            lapStartTime = 0f;
            sectorStartTime = 0f;
            sectorTimes[0] = sectorTimes[1] = sectorTimes[2] = -1f;
            lastLapTime = float.PositiveInfinity;
            bestLapTime = float.PositiveInfinity;
            finished = false;
            finishTime = 0f;

            projection = track.Project(worldPos, -1);
            trackHintIdx = projection.index;
            if (visuals != null) visuals.SyncPose(0f, 0f, 0f, 0f, false);
        }

        public void StepPhysics(
            float dt,
            SplineCircuit track,
            float raceTime,
            int totalLaps,
            bool canMove,
            out bool shifted,
            out bool crossedLap,
            out bool newBestLap)
        {
            shifted = false;
            crossedLap = false;
            newBestLap = false;

            if (!canMove)
            {
                float idleTarget = 5200f + inputThrottle * 7600f;
                rpm = Mathf.Lerp(rpm, idleTarget, Mathf.Min(1f, dt * 12f));
                if (visuals != null) visuals.SyncPose(0f, 0f, 0f, wheelSpin, false);
                return;
            }

            projection = track.Project(transform.position, trackHintIdx);
            trackHintIdx = projection.index;

            float absLat = Mathf.Abs(projection.lateralOffset);
            bool onKerb = absLat > track.halfWidth - 0.7f && absLat <= track.halfWidth + 1.5f;
            bool offTrack = absLat > track.halfWidth + 1.5f;
            kerbRumble = onKerb ? Mathf.Clamp01(speed / 35f) : (offTrack ? Mathf.Clamp01(speed / 50f) * 0.6f : 0f);

            float surfaceGrip = offTrack ? 0.68f : (onKerb ? 0.94f : 1.0f);
            float surfaceDrag = offTrack ? 0.32f : 0f;

            float effThrottle = inputThrottle;
            float effBrake = inputBrake;
            float effSteer = inputSteer;

            // Smart Monoposto Driving Assist for effortless mobile play!
            if (isPlayer && drivingAssist && speed > 18f)
            {
                float maxCurvAhead = projection.curvature;
                float[] checkDists = { 28f, 58f, 95f };
                foreach (float cd in checkDists)
                {
                    var ahead = track.GetSampleAtDistance(projection.s + cd);
                    maxCurvAhead = Mathf.Max(maxCurvAhead, ahead.curvature);
                }
                float safeCornerSpd = Mathf.Sqrt(29.5f / (maxCurvAhead + 0.0022f));
                if (speed > safeCornerSpd + 6f && effBrake < 0.2f)
                {
                    effBrake = Mathf.Clamp01((speed - safeCornerSpd) / 24f) * 0.72f;
                    effThrottle *= 0.25f;
                }
            }

            // ERS & DRS
            if (inputErs && ersCharge > 0.03f && effThrottle > 0.2f && speed > 14f)
            {
                ersActive = true;
                ersCharge = Mathf.Max(0f, ersCharge - dt * 0.17f);
            }
            else
            {
                ersActive = false;
                float regen = effBrake > 0.1f ? 0.16f : 0.045f;
                ersCharge = Mathf.Min(1f, ersCharge + dt * regen);
            }

            bool wantDrs = inputDrs || inputErs;
            drsActive = wantDrs && effBrake < 0.05f && speed > 36f && Mathf.Abs(smoothSteer) < 0.55f;

            // 8-Speed Sequential Gearbox
            int prevGear = gear;
            if (!autoGear && isPlayer)
            {
                if (inputShiftUp && gear < 8) gear++;
                if (inputShiftDown && gear > 1) gear--;
            }
            else
            {
                float upSpd = GearTopSpeed[gear] * 0.93f;
                float dnSpd = gear > 1 ? GearTopSpeed[gear - 1] * 0.74f : -1f;
                if (speed > upSpd && gear < 8) gear++;
                else if (speed < dnSpd && gear > 1) gear--;
            }
            if (gear != prevGear) shifted = true;
            inputShiftUp = false;
            inputShiftDown = false;

            float wingNorm = wingsSetup / 50f;
            float speedSetupMult = 1f - wingNorm * 0.045f;
            float gripSetupMult = (1f + wingNorm * 0.09f) * compound.gripMult * (0.85f + 0.15f * (tyreHealth / 100f));

            float speedNorm = Mathf.Clamp01(speed / 100f);
            float downforce = (1.10f + 1.90f * speedNorm * speedNorm) * team.aero;
            float ersBoost = ersActive ? 1.20f : 1.0f;
            float drsDrag = drsActive ? 0.76f : 1.0f;
            float towDrag = 1f - slipstream * 0.22f;

            float gearTop = GearTopSpeed[gear] * team.power * speedSetupMult
                            * (drsActive ? 1.045f : 1f)
                            * (ersActive ? 1.035f : 1f)
                            * (1f + slipstream * 0.045f);

            float accelBase = (34.0f - gear * 2.05f) * team.power * ersBoost;
            float revFactor = speed < gearTop ? 1.0f : 0.06f;

            float fwdAccel = effThrottle * accelBase * revFactor;
            float brkDecel = effBrake * (50f + 26f * speedNorm) * surfaceGrip;
            float aeroDrag = 0.00172f * speed * speed * drsDrag * towDrag + 0.062f * speed + surfaceDrag * speed;

            float prevSpeed = speed;
            speed = Mathf.Max(0f, speed + (fwdAccel - brkDecel - aeroDrag) * dt);

            // Buttery-smooth progressive steering
            float steerRate = Mathf.Abs(effSteer) > Mathf.Abs(smoothSteer) ? 10.0f : 15.5f;
            smoothSteer = Mathf.Lerp(smoothSteer, effSteer, Mathf.Min(1f, dt * steerRate));

            // Positive inputSteer (+1 right) turns clockwise (+yaw in Unity left-handed Y-up!)
            float maxLock = (0.64f / (1f + speed * 0.020f)) * steerSensitivity;
            float targetSteer = -smoothSteer * maxLock; // negative steerAngle convention synced with visuals
            steerAngle = Mathf.Lerp(steerAngle, targetSteer, Mathf.Min(1f, dt * 16f));

            float maxLatAccel = 28.5f * downforce * team.grip * gripSetupMult * surfaceGrip;
            // In Unity left-handed (+X right, +Z forward): yaw = atan2(fwd.x, fwd.z). Turning right (+inputSteer) increases yaw!
            float desiredYawRate = speed > 0.5f ? (speed / 3.35f) * Mathf.Tan(-steerAngle) * 1.36f : 0f;
            float latAccelNeeded = Mathf.Abs(desiredYawRate * speed);
            float gripRatio = latAccelNeeded > maxLatAccel ? maxLatAccel / latAccelNeeded : 1f;

            float actualYawRate = desiredYawRate * gripRatio;
            yaw += actualYawRate * dt;
            lateralG = (actualYawRate * speed) / 9.81f;

            // Gentle track-tangent stabilization when player isn't steering
            if (isPlayer && Mathf.Abs(effSteer) < 0.05f && speed > 16f)
            {
                float trackYaw = Mathf.Atan2(projection.tangent.x, projection.tangent.z);
                float dYaw = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, trackYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                if (Mathf.Abs(dYaw) < (drivingAssist ? 0.28f : 0.14f))
                    yaw += dYaw * Mathf.Min(1f, dt * (drivingAssist ? 2.4f : 1.3f));
            }

            float slip = Mathf.Max(0f, 1f - gripRatio);
            slipAmount = Mathf.Clamp01(slip * 2.2f + (effBrake > 0.78f && speed > 28f ? 0.35f : 0f));
            lateralVel += (desiredYawRate - actualYawRate) * speed * 0.10f * dt;
            lateralVel *= Mathf.Exp(-11f * dt);

            // Tyre temp & wear
            float targetTemp = 76f + speedNorm * 22f + slipAmount * 14f;
            tyreTemp = Mathf.Lerp(tyreTemp, targetTemp, Mathf.Min(1f, dt * 0.8f));
            tyreHealth = Mathf.Max(15f, tyreHealth - dt * (0.055f + slipAmount * 0.32f) * compound.wearRate);

            Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));

            Vector3 pos = transform.position;
            pos += (fwd * speed + right * lateralVel) * dt;
            pos.y = Mathf.Lerp(pos.y, projection.point.y, Mathf.Min(1f, dt * 18f));

            // Smooth Glancing Barrier Deflection
            float barrierLimit = track.halfWidth + 4.8f;
            var postProj = track.Project(pos, trackHintIdx);
            if (Mathf.Abs(postProj.lateralOffset) > barrierLimit)
            {
                float sign = Mathf.Sign(postProj.lateralOffset);
                float excess = Mathf.Abs(postProj.lateralOffset) - barrierLimit;
                pos -= postProj.right * (sign * (excess + 0.10f));
                speed *= 0.78f;
                float tYaw = Mathf.Atan2(postProj.tangent.x, postProj.tangent.z);
                float dYaw = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, tYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                yaw += dYaw * 0.50f;
            }

            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);

            // RPM & Suspension Pitch/Roll
            float lowSpd = gear == 1 ? 0f : GearTopSpeed[gear - 1] * 0.72f;
            float highSpd = GearTopSpeed[gear];
            float ratio = Mathf.Clamp((speed - lowSpd) / Mathf.Max(1f, highSpd - lowSpd), 0f, 1.06f);
            float targetRpm = 5000f + ratio * 8400f + (speed < 2f ? effThrottle * 4200f : 0f);
            rpm = Mathf.Lerp(rpm, targetRpm, Mathf.Min(1f, dt * 18f));

            float longAccel = (speed - prevSpeed) / Mathf.Max(0.001f, dt);
            float targetPitch = Mathf.Clamp(-longAccel * 0.0011f, -0.045f, 0.045f);
            float targetRoll = Mathf.Clamp(actualYawRate * speed * 0.0018f, -0.055f, 0.055f);
            pitch = Mathf.Lerp(pitch, targetPitch, Mathf.Min(1f, dt * 12f));
            roll  = Mathf.Lerp(roll,  targetRoll,  Mathf.Min(1f, dt * 12f));

            wheelSpin += (speed / 0.36f) * dt;

            // Sector & Lap Tracking
            float t = postProj.t;
            if (t > 0.33f && t < 0.60f && maxSectorReached == 0)
            {
                maxSectorReached = 1;
                sectorTimes[0] = raceTime - sectorStartTime;
                sectorStartTime = raceTime;
            }
            else if (t > 0.66f && t < 0.90f && maxSectorReached == 1)
            {
                maxSectorReached = 2;
                sectorTimes[1] = raceTime - sectorStartTime;
                sectorStartTime = raceTime;
            }
            else if (t < 0.15f && maxSectorReached == 2)
            {
                sectorTimes[2] = raceTime - sectorStartTime;
                sectorStartTime = raceTime;
                float lapDur = raceTime - lapStartTime;
                lastLapTime = lapDur;
                if (lapDur > 12f && lapDur < bestLapTime)
                {
                    bestLapTime = lapDur;
                    newBestLap = true;
                }
                lapStartTime = raceTime;
                maxSectorReached = 0;
                crossedLap = true;
                if (lap >= totalLaps)
                {
                    if (!finished) { finished = true; finishTime = raceTime; }
                }
                else
                {
                    lap++;
                }
            }

            float lapFrac = (maxSectorReached == 0 && t > 0.85f) ? t - 1f : t;
            totalProgress = (finished ? totalLaps + 1 : lap - 1) + lapFrac;

            if (visuals != null)
                visuals.SyncPose(pitch, roll, steerAngle, wheelSpin, drsActive);
        }
    }
}
