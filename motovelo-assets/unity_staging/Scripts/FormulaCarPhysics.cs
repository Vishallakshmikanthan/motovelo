using UnityEngine;

namespace Motovelo
{
    public enum PitLanePhase
    {
        OnTrack = 0,
        EnteringPitLane = 1,
        ServicingInBox = 2,
        ExitingPitLane = 3
    }

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
        public bool drivingAssist = true;

        // Raw Inputs
        public float inputThrottle;
        public float inputBrake;
        public float inputSteer;
        public bool inputErs;
        public bool inputDrs;
        public bool inputShiftUp;
        public bool inputShiftDown;

        // Smoothed Powertrain & Kinematic State
        public float yaw;
        public float speed; // m/s
        public float lateralVel;
        public float smoothThrottle;
        public float smoothBrake;
        public float smoothSteer;
        public float steerAngle;
        public float wheelSpin;
        public int gear = 1;
        public float rpm = 4800f;
        public float shiftTimer;
        public float ersCharge = 1f;
        public bool ersActive;
        public bool ersHarvesting;
        public bool inDrsZone;
        public bool inErsHarvestZone;
        public bool drsLatchedOpen;
        public bool drsActive;
        public float slipstream;
        public float slipAmount;
        public float kerbRumble;
        public float lateralG;
        public float pitch;
        public float roll;

        // Physical FIA Pit Stop & Regulation State
        public PitLanePhase pitPhase = PitLanePhase.OnTrack;
        public bool pitLimiterActive;
        public float pitServiceTimer;
        public float lastPitStopDuration = 2.4f;
        public int nextPitCompoundIdx = 1;
        public int usedCompoundsMask;
        public int pitStops;
        public bool inPit => pitPhase != PitLanePhase.OnTrack;
        public float timePenaltySeconds;
        public string fiaNotice = "";
        public float fiaNoticeTimer;
        private bool _checkedPitLimitLine;
        private bool _swappedTyresThisStop;

        // Telemetry
        public float tyreTemp = 88f;
        public float tyreHealth = 100f;

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

        private bool _prevDrsInput;

        private static readonly float[] GearTopSpeed = { 0f, 35.0f, 47.2f, 57.8f, 67.0f, 75.0f, 82.5f, 89.2f, 97.8f };
        private const float PitSpeedLimitMps = 22.22f; // Official FIA 80.0 KPH Pit Lane Speed Limit!

        public bool MandatoryCompoundSatisfied
        {
            get
            {
                // Count distinct dry/wet compounds fitted during the race (>= 2 satisfies FIA Sporting Regs!)
                int bits = usedCompoundsMask;
                int count = 0;
                while (bits != 0) { count += bits & 1; bits >>= 1; }
                return count >= 2;
            }
        }

        public void ResetOnGrid(Vector3 worldPos, float startYawRad, SplineCircuit track)
        {
            projection = track.Project(worldPos, -1);
            trackHintIdx = projection.index;
            worldPos.y = projection.point.y;

            transform.position = worldPos;
            yaw = startYawRad;
            SyncWorldTransform(projection);

            speed = 0f;
            lateralVel = 0f;
            smoothThrottle = 0f;
            smoothBrake = 0f;
            smoothSteer = 0f;
            steerAngle = 0f;
            gear = 1;
            rpm = 4800f;
            shiftTimer = 0f;
            ersCharge = 1f;
            ersActive = false;
            ersHarvesting = false;
            inDrsZone = false;
            inErsHarvestZone = false;
            drsLatchedOpen = false;
            drsActive = false;
            slipstream = 0f;
            slipAmount = 0f;
            kerbRumble = 0f;
            pitch = 0f;
            roll = 0f;

            pitPhase = PitLanePhase.OnTrack;
            pitLimiterActive = false;
            pitServiceTimer = 0f;
            pitStops = 0;
            timePenaltySeconds = 0f;
            fiaNotice = "";
            fiaNoticeTimer = 0f;
            _checkedPitLimitLine = false;
            _swappedTyresThisStop = false;

            int startCompIdx = System.Array.IndexOf(MotoveloDatabase.Compounds, compound);
            if (startCompIdx < 0) startCompIdx = 0;
            usedCompoundsMask = 1 << startCompIdx;
            nextPitCompoundIdx = startCompIdx == 0 ? 1 : 0; // Default next tyre to alternate slick compound!

            tyreTemp = 88f;
            tyreHealth = 100f;

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

            if (visuals != null)
            {
                visuals.SetJackLift(0f);
                visuals.SyncPose(0f, 0f, 0f, 0f, false, false);
            }
        }

        public void RaiseFiaNotice(string msg, float duration = 4.2f)
        {
            fiaNotice = msg;
            fiaNoticeTimer = duration;
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

            if (fiaNoticeTimer > 0f) fiaNoticeTimer = Mathf.Max(0f, fiaNoticeTimer - dt);

            if (!canMove)
            {
                smoothThrottle = Mathf.MoveTowards(smoothThrottle, inputThrottle, dt * 7.5f);
                float idleTarget = 4600f + smoothThrottle * 6800f;
                rpm = Mathf.Lerp(rpm, idleTarget, Mathf.Min(1f, dt * 10f));
                if (visuals != null) visuals.SyncPose(0f, 0f, 0f, wheelSpin, false, false);
                return;
            }

            projection = track.Project(transform.position, trackHintIdx);
            trackHintIdx = projection.index;
            float signedS = track.GetSignedS(projection.s);

            // =================================================================
            // PHYSICAL FIA PIT LANE STATE MACHINE & REGULATIONS
            // =================================================================
            if (pitPhase == PitLanePhase.ServicingInBox)
            {
                pitServiceTimer += dt;
                speed = 0f;
                lateralVel = 0f;
                drsLatchedOpen = false;
                drsActive = false;
                ersActive = false;
                pitLimiterActive = true;
                gear = 1;
                rpm = Mathf.Lerp(rpm, pitServiceTimer > 2.0f ? 9800f : 5200f, dt * 10f);

                // At 1.25s (50% through the 2.5s stop) — Crew fits the fresh Pirelli tyres!
                if (!_swappedTyresThisStop && pitServiceTimer >= 1.25f)
                {
                    _swappedTyresThisStop = true;
                    int safeIdx = Mathf.Clamp(nextPitCompoundIdx, 0, MotoveloDatabase.Compounds.Length - 1);
                    compound = MotoveloDatabase.Compounds[safeIdx];
                    usedCompoundsMask |= (1 << safeIdx);
                    tyreHealth = 100f;
                    tyreTemp = 90f;
                    visuals?.SwapTyreCompound(compound);
                }

                // At 2.50s — Jacks drop, Green Light "GO! GO! GO!"
                if (pitServiceTimer >= 2.50f)
                {
                    pitStops++;
                    lastPitStopDuration = 2.38f + (Mathf.Abs(carId.GetHashCode() + pitStops) % 25) * 0.01f;
                    pitPhase = PitLanePhase.ExitingPitLane;
                    visuals?.SetJackLift(0f);
                    if (isPlayer)
                        RaiseFiaNotice($"GREEN LIGHT — GO! GO! ({lastPitStopDuration:F2}s STOP • {compound.label} FITTED)", 3.6f);
                }

                if (visuals != null)
                    visuals.SyncPose(0f, 0f, 0f, wheelSpin, false, false);
                return;
            }

            // Detect physical entry into EITHER Pit Entry Mouth:
            // (A) Starting-Region Pit Entry Mouth (+12m..+84m right past Start Gantry!) OR (B) Lap-End Pit Entry Mouth (-225m..-118m)!
            bool inStartingPitMouth = signedS >= 12f && signedS <= 84f;
            bool inLapEndPitMouth = signedS >= -225f && signedS <= -118f;
            if (pitPhase == PitLanePhase.OnTrack && (inStartingPitMouth || inLapEndPitMouth))
            {
                float signedLat = projection.lateralOffset * track.pitSide;
                if (signedLat > track.halfWidth + 2.8f)
                {
                    pitPhase = PitLanePhase.EnteringPitLane;
                    _checkedPitLimitLine = false;
                    _swappedTyresThisStop = false;
                    drsLatchedOpen = false;
                    if (isPlayer)
                        RaiseFiaNotice("PIT ENTRY — BRAKE FOR 80 KPH SPEED LIMIT LINE!", 3.2f);
                }
            }

            float effInputThrottle = inputThrottle;
            float effInputBrake = inputBrake;
            float effInputSteer = inputSteer;

            if (pitPhase == PitLanePhase.EnteringPitLane)
            {
                // Check FIA 80 KPH Pit Speed Limit Line (at -118m if entered at lap end, or +82m if entered in starting region!)
                bool crossedLimit = (signedS >= -118f && signedS < 0f) || signedS >= 82f;
                if (!_checkedPitLimitLine && crossedLimit)
                {
                    _checkedPitLimitLine = true;
                    pitLimiterActive = true;
                    if (isPlayer && speed * 3.6f > 112f)
                    {
                        timePenaltySeconds += 3.0f;
                        RaiseFiaNotice($"FIA STEWARDS: +3.0s PENALTY — SPEEDING IN PIT LANE ({Mathf.RoundToInt(speed * 3.6f)} KPH > 80 KPH)!", 4.8f);
                    }
                    else if (isPlayer)
                    {
                        RaiseFiaNotice("PIT LIMITER 80 KPH ACTIVE — SELECT NEXT TYRE COMPOUND", 3.5f);
                    }
                }

                // Smooth Pit-Lane Autopilot into Team Pit Box at pitBoxSignedS (+138m)
                float distToBox = track.pitBoxSignedS - signedS;
                var aimSmp = track.GetSampleAtDistance(projection.s + Mathf.Clamp(distToBox, 6f, 20f));
                Vector3 aimWorld = aimSmp.pos + aimSmp.right * track.pitLaneOffset;
                float desYaw = Mathf.Atan2(aimWorld.x - transform.position.x, aimWorld.z - transform.position.z);
                float yawErr = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, desYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                effInputSteer = Mathf.Clamp(yawErr * 2.6f, -1f, 1f);

                if (pitLimiterActive)
                {
                    float targetSpd = distToBox < 26f ? Mathf.Clamp(distToBox * 0.85f, 3.5f, PitSpeedLimitMps) : PitSpeedLimitMps;
                    effInputThrottle = speed < targetSpd - 0.5f ? 0.65f : 0f;
                    effInputBrake = speed > targetSpd + 1.2f ? Mathf.Clamp01((speed - targetSpd) / 14f) : 0f;
                }

                if (distToBox <= 1.45f && signedS > 80f)
                {
                    // Snap cleanly onto the painted Pit Box stop marks!
                    var boxSmp = track.GetSampleAtDistance(track.pitBoxSignedS);
                    transform.position = boxSmp.pos + boxSmp.right * track.pitLaneOffset;
                    yaw = Mathf.Atan2(boxSmp.tangent.x, boxSmp.tangent.z);
                    SyncWorldTransform(projection);
                    speed = 0f;
                    pitPhase = PitLanePhase.ServicingInBox;
                    pitServiceTimer = 0f;
                    return;
                }
            }
            else if (pitPhase == PitLanePhase.ExitingPitLane)
            {
                pitLimiterActive = true;
                var aimSmp = track.GetSampleAtDistance(projection.s + 22f);
                Vector3 aimWorld = aimSmp.pos + aimSmp.right * track.pitLaneOffset;
                float desYaw = Mathf.Atan2(aimWorld.x - transform.position.x, aimWorld.z - transform.position.z);
                float yawErr = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, desYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                effInputSteer = Mathf.Clamp(yawErr * 2.5f, -1f, 1f);

                effInputThrottle = speed < PitSpeedLimitMps - 0.4f ? 0.75f : 0.1f;
                effInputBrake = speed > PitSpeedLimitMps + 1.0f ? 0.4f : 0f;

                if (signedS >= 198f)
                {
                    pitPhase = PitLanePhase.OnTrack;
                    pitLimiterActive = false;
                    if (isPlayer)
                        RaiseFiaNotice("PIT EXIT CLEAR — LIMITER OFF, STAY OUTSIDE WHITE LINE!", 3.2f);
                }
            }
            else
            {
                pitLimiterActive = false;
                // FIA Pit Exit White Line Rule check (198m..255m)
                if (isPlayer && pitStops > 0 && signedS > 199f && signedS < 252f && maxSectorReached == 0)
                {
                    float signedLat = projection.lateralOffset * track.pitSide;
                    if (signedLat > track.halfWidth - 0.5f && signedLat < track.halfWidth + 0.5f && Mathf.Abs(smoothSteer) > 0.35f)
                        RaiseFiaNotice("FIA WARNING: DO NOT CROSS SOLID WHITE PIT EXIT LINE!", 2.8f);
                }
            }

            inDrsZone = pitPhase == PitLanePhase.OnTrack && track.IsInDrsZone(projection.s);
            inErsHarvestZone = pitPhase == PitLanePhase.OnTrack && track.IsInErsHarvestZone(projection.s);

            float absLat = Mathf.Abs(projection.lateralOffset);
            bool onKerb = pitPhase == PitLanePhase.OnTrack && absLat > track.halfWidth - 0.65f && absLat <= track.halfWidth + 1.55f;
            bool offTrack = pitPhase == PitLanePhase.OnTrack && absLat > track.halfWidth + 1.55f && !(projection.lateralOffset * track.pitSide > 0f && signedS > -215f && signedS < 215f);
            kerbRumble = onKerb ? Mathf.Clamp01(speed / 42f) : (offTrack ? Mathf.Clamp01(speed / 55f) * 0.5f : 0f);

            float surfaceGrip = offTrack ? 0.76f : (onKerb ? 0.96f : 1.0f);
            float surfaceDrag = offTrack ? 0.18f : 0f;

            float throtRate = effInputThrottle > smoothThrottle ? 8.5f : 14.0f;
            float brakeRate = effInputBrake > smoothBrake ? 12.0f : 18.0f;
            smoothThrottle = Mathf.MoveTowards(smoothThrottle, Mathf.Clamp01(effInputThrottle), dt * throtRate);
            smoothBrake = Mathf.MoveTowards(smoothBrake, Mathf.Clamp01(effInputBrake), dt * brakeRate);

            // MGU-K ERS Deployment & Braking Harvest
            ersHarvesting = false;
            if (inputErs && pitPhase == PitLanePhase.OnTrack && ersCharge > 0.02f && smoothThrottle > 0.25f && speed > 12f)
            {
                ersActive = true;
                ersCharge = Mathf.Max(0f, ersCharge - dt * 0.135f);
            }
            else
            {
                ersActive = false;
                if (smoothBrake > 0.15f && speed > 18f)
                {
                    ersHarvesting = true;
                    float harvestRate = inErsHarvestZone ? 0.34f : 0.18f;
                    ersCharge = Mathf.Min(1f, ersCharge + dt * harvestRate);
                }
                else
                {
                    ersCharge = Mathf.Min(1f, ersCharge + dt * 0.032f);
                }
            }

            // FIA DRS Latch Logic
            bool drsPressedEdge = inputDrs && !_prevDrsInput;
            _prevDrsInput = inputDrs;

            if (!inDrsZone || smoothBrake > 0.08f || speed < 28f || Mathf.Abs(smoothSteer) > 0.62f)
                drsLatchedOpen = false;
            else if (drsPressedEdge || inputDrs || (isPlayer && drivingAssist && smoothThrottle > 0.75f && Mathf.Abs(smoothSteer) < 0.22f))
                drsLatchedOpen = true;

            drsActive = drsLatchedOpen && inDrsZone;

            // 8-Speed Seamless Gearbox
            int prevGear = gear;
            shiftTimer = Mathf.Max(0f, shiftTimer - dt);
            if (!autoGear && isPlayer && pitPhase == PitLanePhase.OnTrack)
            {
                if (inputShiftUp && gear < 8 && shiftTimer <= 0f) { gear++; shiftTimer = 0.10f; }
                if (inputShiftDown && gear > 1 && shiftTimer <= 0f) { gear--; shiftTimer = 0.10f; }
            }
            else if (shiftTimer <= 0f)
            {
                float upSpd = GearTopSpeed[gear] * 0.90f;
                float dnSpd = gear > 1 ? GearTopSpeed[gear - 1] * 0.76f : -1f;
                if (speed > upSpd && gear < 8) { gear++; shiftTimer = 0.12f; }
                else if (speed < dnSpd && gear > 1) { gear--; shiftTimer = 0.11f; }
            }
            if (gear != prevGear) shifted = true;
            inputShiftUp = false;
            inputShiftDown = false;

            float wingNorm = wingsSetup / 50f;
            float speedSetupMult = 1f - wingNorm * 0.04f;
            float gripSetupMult = (1f + wingNorm * 0.08f) * compound.gripMult * (0.85f + 0.15f * (tyreHealth / 100f));

            float speedNorm = Mathf.Clamp01(speed / 96f);
            float downforce = (1.18f + 2.05f * speedNorm * speedNorm) * team.aero;
            float ersBoost = ersActive ? 1.22f : 1.0f;
            float drsDrag = drsActive ? 0.73f : 1.0f;
            float towDrag = 1f - slipstream * 0.20f;

            float topCap = pitLimiterActive
                ? PitSpeedLimitMps
                : GearTopSpeed[8] * team.power * speedSetupMult * (drsActive ? 1.055f : 1f) * (ersActive ? 1.035f : 1f) * (1f + slipstream * 0.04f);

            float tractionLaunch = Mathf.Lerp(0.72f, 1.0f, Mathf.Clamp01(speed / 24f));
            float accelBase = (30.5f - gear * 1.45f) * team.power * ersBoost * tractionLaunch;
            float limiter = speed < topCap ? 1.0f : 0.02f;

            float slopeGravity = pitPhase == PitLanePhase.OnTrack ? -9.81f * Mathf.Sin(projection.slopeRad) : 0f;

            float fwdAccel = smoothThrottle * accelBase * limiter + slopeGravity;
            float brkDecel = smoothBrake * (46f + 24f * speedNorm) * surfaceGrip;
            float aeroDrag = 0.00148f * speed * speed * drsDrag * towDrag + 0.048f * speed + surfaceDrag * speed;

            float prevSpeed = speed;
            speed = Mathf.Max(0f, speed + (fwdAccel - brkDecel - aeroDrag) * dt);
            if (pitLimiterActive && speed > PitSpeedLimitMps + 0.5f)
                speed = Mathf.MoveTowards(speed, PitSpeedLimitMps, dt * 32f);

            float steerRate = Mathf.Abs(effInputSteer) > Mathf.Abs(smoothSteer) ? 9.5f : 14.5f;
            smoothSteer = Mathf.Lerp(smoothSteer, effInputSteer, Mathf.Min(1f, dt * steerRate));

            float maxLock = (0.58f / (1f + speed * 0.022f)) * steerSensitivity;
            float targetSteer = -smoothSteer * maxLock;
            steerAngle = Mathf.Lerp(steerAngle, targetSteer, Mathf.Min(1f, dt * 15f));

            float bankBonus = 1f + Mathf.Abs(projection.bankRad) * 1.15f;
            float maxLatAccel = 32.0f * downforce * team.grip * gripSetupMult * surfaceGrip * bankBonus;
            float desiredYawRate = speed > 0.5f ? (speed / 3.40f) * Mathf.Tan(-steerAngle) * 1.32f : 0f;
            float latAccelNeeded = Mathf.Abs(desiredYawRate * speed);
            float gripRatio = latAccelNeeded > maxLatAccel ? maxLatAccel / latAccelNeeded : 1f;

            float actualYawRate = desiredYawRate * gripRatio;
            yaw += actualYawRate * dt;
            lateralG = (actualYawRate * speed) / 9.81f;

            if (isPlayer && pitPhase == PitLanePhase.OnTrack && Mathf.Abs(effInputSteer) < 0.04f && speed > 20f)
            {
                float trackYaw = Mathf.Atan2(projection.tangent.x, projection.tangent.z);
                float dYaw = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, trackYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                if (Mathf.Abs(dYaw) < (drivingAssist ? 0.20f : 0.10f))
                    yaw += dYaw * Mathf.Min(1f, dt * (drivingAssist ? 1.8f : 0.9f));
            }

            float slip = Mathf.Max(0f, 1f - gripRatio);
            slipAmount = Mathf.Clamp01(slip * 1.8f + (smoothBrake > 0.85f && speed > 32f ? 0.25f : 0f));
            lateralVel += (desiredYawRate - actualYawRate) * speed * 0.08f * dt;
            lateralVel *= Mathf.Exp(-12f * dt);

            tyreTemp = Mathf.Lerp(tyreTemp, 80f + speedNorm * 20f + slipAmount * 12f, Mathf.Min(1f, dt * 0.8f));
            tyreHealth = Mathf.Max(12f, tyreHealth - dt * (0.065f + slipAmount * 0.28f) * compound.wearRate);

            Vector3 fwdFlat = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 rightFlat = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));

            Vector3 pos = transform.position;
            pos += (fwdFlat * speed + rightFlat * lateralVel) * dt;

            var postProj = track.Project(pos, trackHintIdx);
            trackHintIdx = postProj.index;
            float postSignedS = track.GetSignedS(postProj.s);

            // Outer Barrier Collision (widens smoothly around Pit Lane on pitSide!)
            bool onPitSide = Mathf.Sign(postProj.lateralOffset) == track.pitSide;
            float outerLimit = track.halfWidth + 4.3f + (onPitSide ? track.GetPitOuterWallExpansion(postProj.s) : 0f);
            if (Mathf.Abs(postProj.lateralOffset) > outerLimit)
            {
                float sign = Mathf.Sign(postProj.lateralOffset);
                float excess = Mathf.Abs(postProj.lateralOffset) - outerLimit;
                pos -= new Vector3(postProj.right.x, 0f, postProj.right.z) * (sign * (excess + 0.06f));
                speed *= Mathf.Clamp01(1f - 0.55f * dt);
                float tYaw = Mathf.Atan2(postProj.tangent.x, postProj.tangent.z);
                float dYaw = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, tYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                yaw += dYaw * Mathf.Min(1f, dt * 8f);
                postProj = track.Project(pos, trackHintIdx);
            }

            // Concrete FIA Pit Wall Separation (Grid Wall [-119m..+13m] and Garage Wall [+83m..+197m] at track.halfWidth + 3.1m)
            bool alongPitWall = (postSignedS > -119f && postSignedS < 13f) || (postSignedS > 83f && postSignedS < 197f);
            if (onPitSide && alongPitWall)
            {
                float signedLat = postProj.lateralOffset * track.pitSide;
                float wallLat = track.halfWidth + 3.1f;
                if (pitPhase == PitLanePhase.OnTrack && signedLat > wallLat - 0.85f)
                {
                    float push = signedLat - (wallLat - 0.85f);
                    pos -= new Vector3(postProj.right.x, 0f, postProj.right.z) * (track.pitSide * push);
                    speed *= Mathf.Clamp01(1f - 0.5f * dt);
                }
                else if (pitPhase != PitLanePhase.OnTrack && signedLat < wallLat + 0.85f)
                {
                    float push = (wallLat + 0.85f) - signedLat;
                    pos += new Vector3(postProj.right.x, 0f, postProj.right.z) * (track.pitSide * push);
                }
            }

            pos.y = Mathf.Lerp(pos.y, postProj.point.y, Mathf.Min(1f, dt * 26f));
            transform.position = pos;
            SyncWorldTransform(postProj);

            float lowSpd = gear == 1 ? 0f : GearTopSpeed[gear - 1] * 0.68f;
            float highSpd = GearTopSpeed[gear];
            float ratio = Mathf.Clamp((speed - lowSpd) / Mathf.Max(1f, highSpd - lowSpd), 0f, 1.04f);
            float targetRpm = 5200f + ratio * 7900f + (speed < 3f ? smoothThrottle * 3500f : 0f);
            rpm = Mathf.Lerp(rpm, targetRpm, Mathf.Min(1f, dt * 16f));

            float longAccel = (speed - prevSpeed) / Mathf.Max(0.001f, dt);
            float targetPitch = Mathf.Clamp(-longAccel * 0.00055f, -0.022f, 0.022f);
            float targetRoll = Mathf.Clamp(actualYawRate * speed * 0.0012f, -0.038f, 0.038f);
            pitch = Mathf.Lerp(pitch, targetPitch, Mathf.Min(1f, dt * 8f));
            roll  = Mathf.Lerp(roll,  targetRoll,  Mathf.Min(1f, dt * 8f));

            wheelSpin += (speed / 0.36f) * dt;

            // Sector & Lap Tracking (works seamlessly whether crossing Finish Line on Main Straight OR in Pit Box!)
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
                    if (!finished)
                    {
                        finished = true;
                        // Enforce Official FIA 2-Compound Mandatory Dry Tyre Regulation in races >= 3 laps!
                        if (totalLaps >= 3 && !MandatoryCompoundSatisfied)
                        {
                            timePenaltySeconds += 10.0f;
                            if (isPlayer)
                                RaiseFiaNotice("FIA STEWARDS: +10.0s PENALTY — MANDATORY 2ND TYRE COMPOUND NOT USED!", 6.0f);
                        }
                        finishTime = raceTime + timePenaltySeconds;
                    }
                }
                else
                {
                    lap++;
                }
            }

            float lapFrac = (maxSectorReached == 0 && t > 0.85f) ? t - 1f : t;
            totalProgress = (finished ? totalLaps + 1 : lap - 1) + lapFrac;

            if (visuals != null)
                visuals.SyncPose(pitch, roll, steerAngle, wheelSpin, drsActive, ersHarvesting);
        }

        private void SyncWorldTransform(TrackProjection proj)
        {
            Vector3 flatFwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 upNormal = Vector3.Slerp(Vector3.up, proj.up, pitPhase == PitLanePhase.OnTrack ? 0.88f : 0.25f).normalized;
            Vector3 fwd3D = Vector3.ProjectOnPlane(flatFwd, upNormal).normalized;
            if (fwd3D.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(fwd3D, upNormal);
            else
                transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
        }
    }
}
