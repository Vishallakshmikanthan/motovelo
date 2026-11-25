using System.Collections.Generic;
using UnityEngine;

namespace Motovelo
{
    public enum MotoveloScreenState
    {
        ShowroomHub,
        DriverSelect,
        TrackSelect,
        CarSetup,
        GridIntro,
        Countdown,
        Racing,
        Paused,
        Results
    }

    public enum RaceSessionMode
    {
        QuickRace,
        Championship,
        TimeTrial
    }

    public class MotoveloGameManager : MonoBehaviour
    {
        public static MotoveloGameManager Instance { get; private set; }

        [Header("Scene Prefabs (Loaded from Resources/Models)")]
        public GameObject carChassisPrefab;
        public GameObject wheelPrefab;
        public GameObject driverStandingPrefab;
        public GameObject grandstandPrefab;
        public GameObject pitBuildingPrefab;
        public GameObject startGantryPrefab;

        [Header("Subsystems")]
        public Camera mainCamera;
        public Light directionalSun;
        public MonopostoCameraRig cameraRig;
        public ProceduralV6Audio audioSynth;
        public MotoveloUIManager uiManager;

        [Header("Player Selection & Settings")]
        public MotoveloScreenState state = MotoveloScreenState.ShowroomHub;
        public RaceSessionMode sessionMode = RaceSessionMode.QuickRace;
        public int selectedTeamIdx;
        public int selectedTeamDriverIdx;
        public int customHelmetIdx;
        public int customSuitIdx;
        public int selectedCircuitIdx;
        public int selectedCompoundIdx;
        public int selectedLaps = 3;
        public int aiDifficulty = 1; // 0 Rookie, 1 Pro, 2 Legend
        public float wingsSetup = -15f;
        public float steerSensitivity = 1.0f;
        public bool drivingAssist = true;
        public bool tiltSteering;
        public bool autoGear = true;
        public bool fullLeaderboard;
        public bool showroomZoomed;
        public int championshipPoints;
        public int championshipRound;

        // Showroom Objects
        private GameObject _showroomRoot;
        private Transform _showroomCarAnchor;
        private Transform _showroomDriverAnchor;
        private FormulaCarVisuals _showroomCar;
        private FormulaCarVisuals _showroomReflectionCar;
        private GameObject _showroomDriver;

        // Race Objects
        public SplineCircuit activeTrack;
        public readonly List<FormulaCarPhysics> cars = new List<FormulaCarPhysics>();
        private readonly List<FormulaAICar> _aiList = new List<FormulaAICar>();
        public FormulaCarPhysics playerCar;

        public float raceTime;
        public float stateTimer;
        public int litStartPods;
        public int yellowFlagSector;
        public float fastestLapTime = float.PositiveInfinity;
        public string fastestLapDriver = "";
        public string fastestLapTeamId = "redbull";
        public float fastestLapBannerTimer;
        private float _pitStopTimer;

        public TeamDef CurrentTeam => MotoveloDatabase.Teams[Mathf.Clamp(selectedTeamIdx, 0, MotoveloDatabase.Teams.Length - 1)];
        public DriverDef CurrentDriver
        {
            get
            {
                var list = MotoveloDatabase.GetTeamDrivers(CurrentTeam.id);
                if (list.Count == 0) return MotoveloDatabase.Drivers[0];
                return list[Mathf.Abs(selectedTeamDriverIdx) % list.Count];
            }
        }
        public string CurrentHelmetId => MotoveloDatabase.HelmetIds[Mathf.Abs(customHelmetIdx) % MotoveloDatabase.HelmetIds.Length];
        public string CurrentSuitId   => MotoveloDatabase.SuitIds[Mathf.Abs(customSuitIdx) % MotoveloDatabase.SuitIds.Length];
        public CompoundDef CurrentCompound => MotoveloDatabase.Compounds[Mathf.Clamp(selectedCompoundIdx, 0, MotoveloDatabase.Compounds.Length - 1)];
        public CircuitJsonEntry CurrentCircuit => MotoveloDatabase.Circuits[Mathf.Clamp(selectedCircuitIdx, 0, Mathf.Max(0, MotoveloDatabase.Circuits.Length - 1))];

        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Time.fixedDeltaTime = 1f / 60f;

            LoadPrefabsIfNeeded();
            LoadSave();
        }

        private void Start()
        {
            try
            {
                if (mainCamera == null) mainCamera = Camera.main;
                if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();

                if (cameraRig == null)
                {
                    cameraRig = gameObject.AddComponent<MonopostoCameraRig>();
                    cameraRig.targetCamera = mainCamera;
                }
                else if (cameraRig.targetCamera == null)
                {
                    cameraRig.targetCamera = mainCamera;
                }

                if (audioSynth == null)
                {
                    var audGo = new GameObject("ProceduralV6Audio");
                    audGo.transform.SetParent(transform, false);
                    audGo.AddComponent<AudioSource>();
                    audioSynth = audGo.AddComponent<ProceduralV6Audio>();
                }

                BuildShowroomEnvironment();

                if (uiManager == null)
                {
                    uiManager = gameObject.AddComponent<MotoveloUIManager>();
                    uiManager.BuildUI(this);
                }

                SyncDriverSuitHelmetDefaults();
                SetScreenState(MotoveloScreenState.ShowroomHub);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[MotoveloGameManager] Startup exception: " + ex);
            }
        }

        private void LoadPrefabsIfNeeded()
        {
            if (carChassisPrefab == null)     carChassisPrefab     = Resources.Load<GameObject>("Models/formula_car");
            if (wheelPrefab == null)          wheelPrefab          = Resources.Load<GameObject>("Models/formula_wheel");
            if (driverStandingPrefab == null) driverStandingPrefab = Resources.Load<GameObject>("Models/driver_standing");
            if (grandstandPrefab == null)     grandstandPrefab     = Resources.Load<GameObject>("Models/grandstand");
            if (pitBuildingPrefab == null)    pitBuildingPrefab    = Resources.Load<GameObject>("Models/pit_building");
            if (startGantryPrefab == null)    startGantryPrefab    = Resources.Load<GameObject>("Models/start_gantry");
        }

        private void LoadSave()
        {
            selectedTeamIdx = PlayerPrefs.GetInt("mv_team", 0) % MotoveloDatabase.Teams.Length;
            selectedCircuitIdx = PlayerPrefs.GetInt("mv_circuit", 0);
            championshipPoints = PlayerPrefs.GetInt("mv_pts", 0);
            drivingAssist = PlayerPrefs.GetInt("mv_assist", 1) == 1;
            tiltSteering = PlayerPrefs.GetInt("mv_tilt", 0) == 1;
            steerSensitivity = PlayerPrefs.GetFloat("mv_sens", 1.0f);
        }

        public void SavePrefs()
        {
            PlayerPrefs.SetInt("mv_team", selectedTeamIdx);
            PlayerPrefs.SetInt("mv_circuit", selectedCircuitIdx);
            PlayerPrefs.SetInt("mv_pts", championshipPoints);
            PlayerPrefs.SetInt("mv_assist", drivingAssist ? 1 : 0);
            PlayerPrefs.SetInt("mv_tilt", tiltSteering ? 1 : 0);
            PlayerPrefs.SetFloat("mv_sens", steerSensitivity);
            PlayerPrefs.Save();
        }

        private void BuildShowroomEnvironment()
        {
            _showroomRoot = new GameObject("ShowroomStudio");

            // Glossy Monoposto Studio Floor (semi-transparent over inverted mirror reflection!)
            var floor = MotoveloDatabase.CreatePlaneObject("GlossyFloor", _showroomRoot.transform);
            floor.transform.localScale = new Vector3(8f, 1f, 8f);
            var floorMat = MotoveloDatabase.CreateUrpTransparent(new Color(0.86f, 0.88f, 0.91f, 0.82f), 0.08f, 0.88f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            var carAnchorGo = new GameObject("ShowroomCarAnchor");
            carAnchorGo.transform.SetParent(_showroomRoot.transform, false);
            carAnchorGo.transform.rotation = Quaternion.identity;
            _showroomCarAnchor = carAnchorGo.transform;

            var drvAnchorGo = new GameObject("ShowroomDriverAnchor");
            drvAnchorGo.transform.SetParent(_showroomRoot.transform, false);
            drvAnchorGo.transform.position = new Vector3(0f, 0f, 18f);
            drvAnchorGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _showroomDriverAnchor = drvAnchorGo.transform;
        }

        public void SyncDriverSuitHelmetDefaults()
        {
            var drv = CurrentDriver;
            customHelmetIdx = System.Array.IndexOf(MotoveloDatabase.HelmetIds, drv.helmetId);
            if (customHelmetIdx < 0) customHelmetIdx = 0;
            customSuitIdx = System.Array.IndexOf(MotoveloDatabase.SuitIds, drv.suitId);
            if (customSuitIdx < 0) customSuitIdx = 0;
            RefreshShowroomModels();
        }

        public void RefreshShowroomModels()
        {
            if (_showroomCarAnchor == null || carChassisPrefab == null || wheelPrefab == null) return;

            if (_showroomCar != null) Destroy(_showroomCar.gameObject);
            if (_showroomReflectionCar != null) Destroy(_showroomReflectionCar.gameObject);

            var carGo = new GameObject("HeroCar");
            carGo.transform.SetParent(_showroomCarAnchor, false);
            _showroomCar = carGo.AddComponent<FormulaCarVisuals>();
            _showroomCar.Assemble(carChassisPrefab, wheelPrefab, CurrentTeam, CurrentHelmetId, CurrentSuitId, CurrentCompound);

            // Real Mirror Reflection under glossy floor!
            var reflGo = new GameObject("HeroCarReflection");
            reflGo.transform.SetParent(_showroomCarAnchor, false);
            reflGo.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            reflGo.transform.localScale = new Vector3(1f, -1f, 1f);
            _showroomReflectionCar = reflGo.AddComponent<FormulaCarVisuals>();
            _showroomReflectionCar.Assemble(carChassisPrefab, wheelPrefab, CurrentTeam, CurrentHelmetId, CurrentSuitId, CurrentCompound);

            if (driverStandingPrefab != null)
            {
                if (_showroomDriver != null) Destroy(_showroomDriver);
                _showroomDriver = Instantiate(driverStandingPrefab, _showroomDriverAnchor);
                _showroomDriver.transform.localPosition = Vector3.zero;
                _showroomDriver.transform.localRotation = Quaternion.identity;

                var suitTex   = MotoveloDatabase.LoadTex($"Textures/suits/{CurrentSuitId}");
                var helmetTex = MotoveloDatabase.LoadTex($"Textures/helmets/{CurrentHelmetId}");
                var suitMat   = MotoveloDatabase.CreateUrpLit(Color.white, suitTex, 0.05f, 0.42f);
                var helmetMat = MotoveloDatabase.CreateUrpLit(Color.white, helmetTex, 0.25f, 0.86f);
                var visorMat  = MotoveloDatabase.CreateUrpLit(new Color(0.02f, 0.05f, 0.14f), null, 0.95f, 0.96f);
                var bootMat   = MotoveloDatabase.CreateUrpLit(new Color(0.06f, 0.06f, 0.08f), null, 0.15f, 0.55f);

                foreach (var r in _showroomDriver.GetComponentsInChildren<Renderer>())
                {
                    string mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mn.Contains("DriverSuitMat")) r.sharedMaterial = suitMat;
                    else if (mn.Contains("DriverHelmetMat")) r.sharedMaterial = helmetMat;
                    else if (mn.Contains("Visor")) r.sharedMaterial = visorMat;
                    else r.sharedMaterial = bootMat;
                }
            }

            SavePrefs();
            uiManager?.SyncAllLabels();
        }

        public void CycleTeam(int delta)
        {
            int n = MotoveloDatabase.Teams.Length;
            selectedTeamIdx = ((selectedTeamIdx + delta) % n + n) % n;
            selectedTeamDriverIdx = 0;
            audioSynth?.TriggerClick();
            SyncDriverSuitHelmetDefaults();
        }

        public void CycleDriver(int delta)
        {
            var list = MotoveloDatabase.GetTeamDrivers(CurrentTeam.id);
            int n = Mathf.Max(1, list.Count);
            selectedTeamDriverIdx = ((selectedTeamDriverIdx + delta) % n + n) % n;
            audioSynth?.TriggerClick();
            SyncDriverSuitHelmetDefaults();
        }

        public void CycleHelmet()
        {
            customHelmetIdx = (customHelmetIdx + 1) % MotoveloDatabase.HelmetIds.Length;
            audioSynth?.TriggerClick();
            RefreshShowroomModels();
        }

        public void CycleSuit()
        {
            customSuitIdx = (customSuitIdx + 1) % MotoveloDatabase.SuitIds.Length;
            audioSynth?.TriggerClick();
            RefreshShowroomModels();
        }

        public void SelectCircuit(int idx)
        {
            selectedCircuitIdx = Mathf.Clamp(idx, 0, MotoveloDatabase.Circuits.Length - 1);
            audioSynth?.TriggerClick();
            SavePrefs();
            uiManager?.SyncAllLabels();
        }

        public void SelectCompound(int idx)
        {
            selectedCompoundIdx = Mathf.Clamp(idx, 0, MotoveloDatabase.Compounds.Length - 1);
            audioSynth?.TriggerClick();
            RefreshShowroomModels();
        }

        public void SetScreenState(MotoveloScreenState next)
        {
            state = next;
            stateTimer = 0f;

            bool inShowroom = next == MotoveloScreenState.ShowroomHub ||
                              next == MotoveloScreenState.DriverSelect ||
                              next == MotoveloScreenState.TrackSelect ||
                              next == MotoveloScreenState.CarSetup;

            if (_showroomRoot != null) _showroomRoot.SetActive(inShowroom);
            if (activeTrack != null) activeTrack.gameObject.SetActive(!inShowroom);
            foreach (var c in cars)
                if (c != null) c.gameObject.SetActive(!inShowroom);

            if (inShowroom && mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = next == MotoveloScreenState.DriverSelect
                    ? new Color(0.06f, 0.07f, 0.09f)
                    : new Color(0.76f, 0.79f, 0.83f);
                RenderSettings.fog = false;
                audioSynth.engineActive = false;
            }

            if (next == MotoveloScreenState.Paused || next == MotoveloScreenState.Results)
                audioSynth.engineActive = false;

            uiManager?.OnStateChanged(next);
        }

        public void LaunchSession(RaceSessionMode mode)
        {
            audioSynth?.TriggerClick();
            sessionMode = mode;

            if (mode == RaceSessionMode.Championship && MotoveloDatabase.Circuits.Length > 0)
                selectedCircuitIdx = championshipRound % MotoveloDatabase.Circuits.Length;

            // Clear old track & cars
            if (activeTrack != null) Destroy(activeTrack.gameObject);
            foreach (var c in cars)
                if (c != null) Destroy(c.gameObject);
            cars.Clear();
            _aiList.Clear();

            var cDef = CurrentCircuit;
            var trackGo = new GameObject($"Circuit_{cDef.id}");
            activeTrack = trackGo.AddComponent<SplineCircuit>();
            activeTrack.BuildFromDefinition(cDef, grandstandPrefab, pitBuildingPrefab, startGantryPrefab);

            // Sky & Atmosphere for circuit
            ColorUtility.TryParseHtmlString(cDef.skyHorizon, out Color skyHor);
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = skyHor;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = skyHor;
            RenderSettings.fogDensity = 0.0011f;

            // Spawn 22-car 2026 F1 Grid (or solo car in TimeTrial)
            var pTeam = CurrentTeam;
            var pDrv = CurrentDriver;
            var gridDrivers = new List<(bool isPlayer, DriverDef drv, TeamDef team)>();

            if (mode == RaceSessionMode.TimeTrial)
            {
                gridDrivers.Add((true, pDrv, pTeam));
            }
            else
            {
                var rivals = new List<DriverDef>();
                foreach (var d in MotoveloDatabase.Drivers)
                    if (d.id != pDrv.id) rivals.Add(d);

                int playerSlot = 7; // Start P8 for thrilling pack overtakes!
                for (int i = 0; i < 22; i++)
                {
                    if (i == playerSlot)
                        gridDrivers.Add((true, pDrv, pTeam));
                    else if (rivals.Count > 0)
                    {
                        var r = rivals[0];
                        rivals.RemoveAt(0);
                        gridDrivers.Add((false, r, MotoveloDatabase.GetTeam(r.teamId)));
                    }
                }
            }

            for (int i = 0; i < gridDrivers.Count; i++)
            {
                var entry = gridDrivers[i];
                var comp = entry.isPlayer ? CurrentCompound : MotoveloDatabase.Compounds[i % 3];
                string hId = entry.isPlayer ? CurrentHelmetId : entry.drv.helmetId;
                string sId = entry.isPlayer ? CurrentSuitId : entry.drv.suitId;

                var carGo = new GameObject($"F1Car_{i:D2}_{entry.drv.code}");
                var vis = carGo.AddComponent<FormulaCarVisuals>();
                vis.Assemble(carChassisPrefab, wheelPrefab, entry.team, hId, sId, comp);

                var phys = carGo.AddComponent<FormulaCarPhysics>();
                phys.carId = $"car_{i}";
                phys.isPlayer = entry.isPlayer;
                phys.team = entry.team;
                phys.driver = entry.drv;
                phys.compound = comp;
                phys.visuals = vis;
                phys.wingsSetup = entry.isPlayer ? wingsSetup : 0f;
                phys.steerSensitivity = steerSensitivity;
                phys.autoGear = autoGear;
                phys.drivingAssist = drivingAssist;

                float distBehind = 10f + i * 7.8f;
                var smp = activeTrack.GetSampleAtDistance(activeTrack.totalLength - distBehind);
                float sideOff = (i % 2 == 0) ? -2.9f : 2.9f;
                Vector3 startPos = smp.pos + smp.right * sideOff;
                float startYaw = Mathf.Atan2(smp.tangent.x, smp.tangent.z);

                phys.ResetOnGrid(startPos, startYaw, activeTrack);
                cars.Add(phys);

                if (entry.isPlayer)
                {
                    playerCar = phys;
                }
                else
                {
                    var ai = carGo.AddComponent<FormulaAICar>();
                    ai.Configure(i, aiDifficulty);
                    _aiList.Add(ai);
                }
            }

            raceTime = 0f;
            litStartPods = 0;
            yellowFlagSector = 0;
            fastestLapTime = float.PositiveInfinity;
            fastestLapBannerTimer = 0f;
            _pitStopTimer = 0f;
            activeTrack.SetStartLights(0);
            cameraRig.ResetSnap();

            SetScreenState(MotoveloScreenState.GridIntro);
        }

        public void SkipGridIntroToCountdown()
        {
            if (state == MotoveloScreenState.GridIntro)
            {
                litStartPods = 0;
                activeTrack?.SetStartLights(0);
                SetScreenState(MotoveloScreenState.Countdown);
            }
        }

        public void RequestPitStop()
        {
            if (playerCar == null || state != MotoveloScreenState.Racing) return;
            playerCar.pitRequested = !playerCar.pitRequested;
            audioSynth?.TriggerClick();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            stateTimer += dt;

            // Showroom Camera Updates
            if (state == MotoveloScreenState.ShowroomHub || state == MotoveloScreenState.TrackSelect || state == MotoveloScreenState.CarSetup)
            {
                cameraRig.UpdateShowroomCamera(_showroomCarAnchor, false, showroomZoomed, dt);
                return;
            }
            if (state == MotoveloScreenState.DriverSelect)
            {
                if (_showroomDriver != null)
                    _showroomDriver.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.9f) * 16f, 0f);
                cameraRig.UpdateShowroomCamera(_showroomDriverAnchor, true, showroomZoomed, dt);
                return;
            }

            // Grid Intro Fly-Over (2.5s or tap to skip!)
            if (state == MotoveloScreenState.GridIntro)
            {
                if (playerCar != null && mainCamera != null)
                {
                    Vector3 fwd = new Vector3(Mathf.Sin(playerCar.yaw), 0f, Mathf.Cos(playerCar.yaw));
                    Vector3 right = new Vector3(Mathf.Cos(playerCar.yaw), 0f, -Mathf.Sin(playerCar.yaw));
                    Vector3 camPos = playerCar.transform.position + fwd * (8.5f - stateTimer * 2.4f) + right * 4.0f + Vector3.up * 2.2f;
                    mainCamera.transform.position = camPos;
                    mainCamera.transform.LookAt(playerCar.transform.position + Vector3.up * 0.8f - fwd * 6f);
                }
                if (stateTimer >= 2.5f)
                    SkipGridIntroToCountdown();
                return;
            }

            // Read Player Keyboard + Touch + Tilt Gyro Inputs
            if ((state == MotoveloScreenState.Countdown || state == MotoveloScreenState.Racing) && playerCar != null)
            {
                uiManager.ReadPlayerControls(
                    out float touchSteer,
                    out float touchGas,
                    out float touchBrake,
                    out bool holdErs,
                    out bool holdDrs,
                    out bool holdRear);

                float keySteer = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                               - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)  ? 1f : 0f);
                float gyroSteer = tiltSteering ? Mathf.Clamp(Input.acceleration.x * 2.4f, -1f, 1f) : 0f;

                playerCar.inputSteer = Mathf.Clamp(touchSteer + keySteer + gyroSteer, -1f, 1f);
                playerCar.inputThrottle = Mathf.Clamp01(touchGas + (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f));
                playerCar.inputBrake = Mathf.Clamp01(touchBrake + (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
                playerCar.inputErs = holdErs || Input.GetKey(KeyCode.Space);
                playerCar.inputDrs = holdDrs || Input.GetKey(KeyCode.F);
                cameraRig.lookBack = holdRear || Input.GetKey(KeyCode.R);

                if (Input.GetKeyDown(KeyCode.C)) { cameraRig.CycleCamera(); audioSynth.TriggerClick(); }
                if (Input.GetKeyDown(KeyCode.T)) { fullLeaderboard = !fullLeaderboard; audioSynth.TriggerClick(); }
                if (Input.GetKeyDown(KeyCode.P)) RequestPitStop();
                if (Input.GetKeyDown(KeyCode.E)) playerCar.inputShiftUp = true;
                if (Input.GetKeyDown(KeyCode.Q)) playerCar.inputShiftDown = true;
                if (Input.GetKeyDown(KeyCode.Escape)) SetScreenState(MotoveloScreenState.Paused);

                cameraRig.UpdateRaceCamera(playerCar, activeTrack, dt);

                audioSynth.engineActive = true;
                audioSynth.rpm = playerCar.rpm;
                audioSynth.throttle = playerCar.inputThrottle;
                audioSynth.slip = playerCar.slipAmount;
                audioSynth.kerb = playerCar.kerbRumble;

                uiManager.RefreshHUD();
            }
        }

        private void FixedUpdate()
        {
            if (state != MotoveloScreenState.Countdown && state != MotoveloScreenState.Racing)
                return;
            if (activeTrack == null || playerCar == null)
                return;

            float dt = Time.fixedDeltaTime;

            if (state == MotoveloScreenState.Countdown)
            {
                int pods = Mathf.Min(5, Mathf.FloorToInt(stateTimer / 0.72f) + 1);
                if (stateTimer < 3.6f)
                {
                    if (pods > litStartPods)
                    {
                        litStartPods = pods;
                        activeTrack.SetStartLights(litStartPods);
                        audioSynth?.TriggerBeep(false);
                    }
                }
                else if (stateTimer >= 4.2f)
                {
                    litStartPods = 0;
                    activeTrack.SetStartLights(0);
                    audioSynth?.TriggerBeep(true);
                    SetScreenState(MotoveloScreenState.Racing);
                }
            }
            else if (state == MotoveloScreenState.Racing)
            {
                raceTime += dt;
                if (fastestLapBannerTimer > 0f) fastestLapBannerTimer -= dt;
                UpdateSlipstreamAndYellowFlags();
                UpdatePlayerPitStop(dt);
            }

            bool canMove = state == MotoveloScreenState.Racing;

            // Update AI inputs
            int aiIdx = 0;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (!c.isPlayer && aiIdx < _aiList.Count)
                {
                    if (canMove)
                        _aiList[aiIdx].UpdateAIInputs(c, cars, activeTrack, dt);
                    else
                    {
                        c.inputThrottle = 0.4f;
                        c.inputBrake = 0f;
                        c.inputSteer = 0f;
                    }
                    aiIdx++;
                }

                c.StepPhysics(dt, activeTrack, raceTime, selectedLaps, canMove && !c.inPit,
                    out bool shifted, out bool crossedLap, out _);

                if (crossedLap && c.lastLapTime < fastestLapTime)
                {
                    fastestLapTime = c.lastLapTime;
                    fastestLapDriver = c.driver.name;
                    fastestLapTeamId = c.team.id;
                    fastestLapBannerTimer = 4.5f;
                }

                if (c.isPlayer && shifted)
                    audioSynth?.TriggerShiftPop();
            }

            if (canMove)
                ResolveCarToCarCollisions();

            if (playerCar.finished && state == MotoveloScreenState.Racing)
            {
                var standings = GetStandings();
                int pRank = standings.IndexOf(playerCar) + 1;
                if (sessionMode == RaceSessionMode.Championship)
                {
                    int pts = pRank >= 1 && pRank <= MotoveloDatabase.F1Points.Length ? MotoveloDatabase.F1Points[pRank - 1] : 0;
                    championshipPoints += pts;
                    championshipRound = (championshipRound + 1) % Mathf.Max(1, MotoveloDatabase.Circuits.Length);
                    SavePrefs();
                }
                SetScreenState(MotoveloScreenState.Results);
            }
        }

        private void UpdateSlipstreamAndYellowFlags()
        {
            yellowFlagSector = 0;
            int n = cars.Count;
            for (int i = 0; i < n; i++)
            {
                var c = cars[i];
                c.slipstream = 0f;
                if (raceTime > 6f && !c.isPlayer && c.speed < 13f)
                    yellowFlagSector = c.projection.t < 0.33f ? 1 : (c.projection.t < 0.66f ? 2 : 3);

                Vector3 fwd = new Vector3(Mathf.Sin(c.yaw), 0f, Mathf.Cos(c.yaw));
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    Vector3 d = cars[j].transform.position - c.transform.position;
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist > 4f && dist < 36f)
                    {
                        float dot = Vector3.Dot(d / dist, fwd);
                        if (dot > 0.94f)
                        {
                            float tow = 1f - (dist - 4f) / 32f;
                            if (tow > c.slipstream) c.slipstream = tow;
                        }
                    }
                }
            }
        }

        private void UpdatePlayerPitStop(float dt)
        {
            if (playerCar.pitRequested && !playerCar.inPit && (playerCar.projection.t > 0.94f || playerCar.projection.t < 0.04f))
            {
                playerCar.inPit = true;
                playerCar.pitRequested = false;
                _pitStopTimer = 2.2f;
            }
            if (playerCar.inPit)
            {
                _pitStopTimer -= dt;
                playerCar.speed *= Mathf.Exp(-6f * dt);
                if (_pitStopTimer <= 0f)
                {
                    playerCar.inPit = false;
                    playerCar.pitStops++;
                    playerCar.tyreHealth = 100f;
                    playerCar.tyreTemp = 88f;
                    int nextComp = (selectedCompoundIdx + 1) % 3;
                    playerCar.compound = MotoveloDatabase.Compounds[nextComp];
                    audioSynth?.TriggerBeep(true);
                }
            }
        }

        private void ResolveCarToCarCollisions()
        {
            int n = cars.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var a = cars[i];
                    var b = cars[j];
                    Vector3 d = b.transform.position - a.transform.position;
                    d.y = 0f;
                    float dist = d.magnitude;
                    const float minDist = 2.65f;
                    if (dist > 0.001f && dist < minDist)
                    {
                        Vector3 nrm = d / dist;
                        float push = (minDist - dist) * 0.52f;
                        a.transform.position -= nrm * push;
                        b.transform.position += nrm * push;
                        a.speed *= 0.95f;
                        b.speed *= 0.95f;
                    }
                }
            }
        }

        public List<FormulaCarPhysics> GetStandings()
        {
            var sorted = new List<FormulaCarPhysics>(cars);
            sorted.Sort((a, b) =>
            {
                if (a.finished && b.finished) return a.finishTime.CompareTo(b.finishTime);
                if (a.finished) return -1;
                if (b.finished) return 1;
                return b.totalProgress.CompareTo(a.totalProgress);
            });
            return sorted;
        }
    }
}
