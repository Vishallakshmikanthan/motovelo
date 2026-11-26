using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Motovelo
{
    public class MotoveloUIManager : MonoBehaviour
    {
        private MotoveloGameManager _gm;
        private Canvas _canvas;
        private Font _font;
        private Sprite _roundedSprite;
        private Sprite _circleSprite;
        private Sprite _ringSprite;

        // Screen Panels
        private GameObject _panelShowroom;
        private GameObject _panelDriver;
        private GameObject _panelTrack;
        private GameObject _panelSetup;
        private GameObject _panelGridIntro;
        private GameObject _panelHud;
        private GameObject _panelPause;
        private GameObject _panelResults;

        // Showroom UI refs
        private RawImage _teamLogoImg;
        private Text _teamNameTxt;
        private Text _driverTagTxt;
        private Text _trackPillTxt;
        private Text _champPtsTxt;

        // Driver Select UI refs
        private RawImage _drvPortraitImg;
        private Text _drvNameTxt;
        private Text _drvSuitHelmTxt;

        // Track Select UI refs
        private Transform _trackListContent;
        private readonly List<Image> _trackCardBorders = new List<Image>();

        // Car Setup UI refs
        private Text _setupCompoundTxt;
        private Text _setupWingsTxt;
        private Text _setupLapsTxt;
        private Text _setupDiffTxt;
        private Text _setupAssistTxt;
        private Text _setupTiltTxt;

        // Grid Intro UI refs
        private RawImage _gridPortraitImg;
        private RawImage _gridLogoImg;
        private Text _gridDriverTxt;
        private Text _gridTeamTxt;
        private Text _gridPosTxt;

        // HUD UI refs (LibreCorsa Broadcast Style)
        private struct TowerRowUI
        {
            public GameObject root;
            public Image bg;
            public Text posTxt;
            public Image teamBar;
            public Text codeTxt;
            public Image tyreBg;
            public Text tyreTxt;
            public Text gapTxt;
        }
        private readonly List<TowerRowUI> _towerRows = new List<TowerRowUI>();
        private Image _aheadBarImg;
        private Text _aheadCodeTxt;
        private Text _aheadGapTxt;
        private Text _towerHeaderTxt;
        private Image _towerHeaderBg;
        private Text _hudLapTxt;
        private RectTransform _minimapWrapRt;
        private RawImage _minimapImg;
        private Texture2D _minimapTex;
        private Color32[] _minimapClear;
        private readonly Image[] _startLightLenses = new Image[5];
        private GameObject _startLightsWrap;
        private Text _startLightsBannerTxt;
        private float _lightsOutBannerTimer;
        private bool _wasInCountdown;
        private GameObject _flBannerWrap;
        private Text _flBannerTxt;
        private GameObject _pitStatusBannerWrap;
        private Image _pitStatusBannerBg;
        private Text _pitStatusBannerTxt;
        private Text _nextTyreBtnTxt;
        private Text _hudSpeedTxt;
        private Text _hudRpmTxt;
        private Image _speedRingFill;
        private Text _hudGearTxt;
        private Text _hudGearSubTxt;
        private Text _hudPosBigTxt;
        private Text _hudPosTotalTxt;
        private Text _hudTimeTxt;
        private Text _hudBestTxt;
        private readonly Image[] _sectorBars = new Image[3];
        private readonly Text[] _sectorLabels = new Text[3];
        private Text _weatherTempTxt;
        private Text _weatherTrackTxt;
        private Text _battPctTxt;
        private Image _ersBarFill;
        private Image _overtakePillBg;
        private Text _overtakePillTxt;
        private Image _drsBadgeBg;
        private Text _drsBadgeTxt;
        private Text _sfxBtnTxt;
        private Image _pitBtnBg;
        private Text _assistPillTxt;
        private readonly Image[] _tyreRings = new Image[4];
        private readonly Text[] _tyrePcts = new Text[4];
        private readonly Image[] _carTyreRects = new Image[4];
        private Image _cardCompoundBg;
        private Text _cardCompoundTxt;
        private Text _damageStatusTxt;
        private readonly Image[] _rpmDots = new Image[11];

        // Multi-touch buttons
        private TouchHoldButton _btnLeft;
        private TouchHoldButton _btnRight;
        private TouchHoldButton _btnGas;
        private TouchHoldButton _btnBrake;
        private TouchHoldButton _btnErs;
        private TouchHoldButton _btnDrs;
        private TouchHoldButton _btnRear;

        // Results UI refs
        private Text _resTitleTxt;
        private Text _resBodyTxt;

        private int _hudFrameCount;

        public void BuildUI(MotoveloGameManager gm)
        {
            _gm = gm;
            _font = Resources.Load<Font>("Fonts/MotoveloFont");
            if (_font == null)
            {
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            }
            if (_font == null)
            {
                try { _font = Font.CreateDynamicFontFromOSFont(new[] { "Roboto", "sans-serif", "Arial" }, 18); } catch { }
            }

            _roundedSprite = CreateRoundedRectSprite(64, 14);
            _circleSprite = CreateCircleSprite(64);
            _ringSprite = CreateRingSprite(128, 12);

            EnsureEventSystem();

            var canvasGo = new GameObject("MotoveloCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildShowroomPanel(canvasGo.transform);
            BuildDriverPanel(canvasGo.transform);
            BuildTrackPanel(canvasGo.transform);
            BuildSetupPanel(canvasGo.transform);
            BuildGridIntroPanel(canvasGo.transform);
            BuildHudPanel(canvasGo.transform);
            BuildPausePanel(canvasGo.transform);
            BuildResultsPanel(canvasGo.transform);

            SyncAllLabels();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            // Static direct reference so IL2CPP never strips InputSystemUIInputModule on Android!
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // =====================================================================
        // 1. MINIMALISTIC SHOWROOM HUB (1-Tap Play + Live 3D Replica Car!)
        // =====================================================================
        private void BuildShowroomPanel(Transform root)
        {
            _panelShowroom = CreateFullScreenPanel(root, "PanelShowroom", Color.clear);

            // Top sleek bar
            var topBar = CreatePanel(_panelShowroom.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -88), new Vector2(0, 0), new Color(0.05f, 0.06f, 0.08f, 0.82f));
            CreateLabel(topBar.transform, "MOTOVELO", 34, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 0), new Vector2(0, 1), new Vector2(36, 0), new Vector2(320, 0));
            _trackPillTxt = CreateLabel(topBar.transform, "JADE RIVER • CHINA", 20, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 0.94f), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(-220, 0), new Vector2(220, 0));
            _champPtsTxt = CreateLabel(topBar.transform, "CHAMPIONSHIP: 0 PTS", 20, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.98f, 0.76f, 0.12f), new Vector2(1, 0), new Vector2(1, 1), new Vector2(-360, 0), new Vector2(-36, 0));

            // Top-Left Monoposto Crimson Team Banner + Team Logo
            var bannerWrap = CreatePanel(_panelShowroom.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -210), new Vector2(560, -112), new Color(0.76f, 0.06f, 0.11f, 0.95f), true);
            var logoBox = CreatePanel(bannerWrap.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(10, 10), new Vector2(88, -10), new Color(0.06f, 0.07f, 0.10f, 1f), true);
            var logoGo = new GameObject("TeamLogo", typeof(RectTransform), typeof(RawImage));
            logoGo.transform.SetParent(logoBox.transform, false);
            StretchFull(logoGo.GetComponent<RectTransform>(), 4);
            _teamLogoImg = logoGo.GetComponent<RawImage>();

            _teamNameTxt = CreateLabel(bannerWrap.transform, "Red Bull Racing", 28, FontStyle.Bold, TextAnchor.LowerLeft, Color.white, new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(106, 0), new Vector2(-20, -8));
            _driverTagTxt = CreateLabel(bannerWrap.transform, "#3 Max Verstappen", 18, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.92f, 0.94f, 0.98f, 0.88f), new Vector2(0, 0), new Vector2(1, 0.48f), new Vector2(106, 8), new Vector2(-20, 0));

            // Top-Right Clean Zoom Button
            CreateActionButton(_panelShowroom.transform, "INSPECT", new Vector2(1, 1), new Vector2(-196, -168), new Vector2(160, 52), new Color(0.10f, 0.12f, 0.16f, 0.88f), () =>
            {
                _gm.showroomZoomed = !_gm.showroomZoomed;
                _gm.audioSynth?.TriggerClick();
            });

            // Left / Right Circular Crimson Team Switchers
            CreateCircleButton(_panelShowroom.transform, "<", new Vector2(0, 0.5f), new Vector2(68, 0), 76, new Color(0.78f, 0.06f, 0.12f, 0.94f), () => _gm.CycleTeam(-1));
            CreateCircleButton(_panelShowroom.transform, ">", new Vector2(1, 0.5f), new Vector2(-68, 0), 76, new Color(0.78f, 0.06f, 0.12f, 0.94f), () => _gm.CycleTeam(1));

            // Minimalistic Bottom Action Dock (4 secondary + 1 bold RACE NOW CTA!)
            var botBar = CreatePanel(_panelShowroom.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 118), new Color(0.05f, 0.06f, 0.08f, 0.88f));

            CreateActionButton(botBar.transform, "DRIVER & SUIT", new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(228, 68), new Color(0.14f, 0.16f, 0.20f, 0.95f), () => _gm.SetScreenState(MotoveloScreenState.DriverSelect));
            CreateActionButton(botBar.transform, "CIRCUITS (20)", new Vector2(0, 0.5f), new Vector2(394, 0), new Vector2(228, 68), new Color(0.14f, 0.16f, 0.20f, 0.95f), () => _gm.SetScreenState(MotoveloScreenState.TrackSelect));
            CreateActionButton(botBar.transform, "SETUP & TYRES", new Vector2(0, 0.5f), new Vector2(638, 0), new Vector2(228, 68), new Color(0.14f, 0.16f, 0.20f, 0.95f), () => _gm.SetScreenState(MotoveloScreenState.CarSetup));
            CreateActionButton(botBar.transform, "CHAMPIONSHIP", new Vector2(0, 0.5f), new Vector2(882, 0), new Vector2(228, 68), new Color(0.14f, 0.16f, 0.20f, 0.95f), () => _gm.LaunchSession(RaceSessionMode.Championship));

            CreateActionButton(botBar.transform, "QUICK RACE  >", new Vector2(1, 0.5f), new Vector2(-190, 0), new Vector2(310, 74), new Color(0.84f, 0.07f, 0.12f, 1f), () => _gm.LaunchSession(RaceSessionMode.QuickRace));
        }

        // =====================================================================
        // 2. 3D DRIVER, HELMET & SUIT SELECTION
        // =====================================================================
        private void BuildDriverPanel(Transform root)
        {
            _panelDriver = CreateFullScreenPanel(root, "PanelDriver", Color.clear);

            CreateActionButton(_panelDriver.transform, "<  BACK", new Vector2(0, 1), new Vector2(116, -54), new Vector2(160, 54), new Color(0.14f, 0.16f, 0.20f, 0.92f), () => _gm.SetScreenState(MotoveloScreenState.ShowroomHub));
            CreateLabel(_panelDriver.transform, "DRIVER & GEAR SELECTION", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(215, -81), new Vector2(680, -27));

            var card = CreatePanel(_panelDriver.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -245), new Vector2(540, -110), new Color(0.76f, 0.06f, 0.11f, 0.95f), true);
            var pBox = CreatePanel(card.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(10, 10), new Vector2(125, -10), new Color(0.08f, 0.09f, 0.12f, 1f), true);
            var pGo = new GameObject("DrvPortrait", typeof(RectTransform), typeof(RawImage));
            pGo.transform.SetParent(pBox.transform, false);
            StretchFull(pGo.GetComponent<RectTransform>(), 2);
            _drvPortraitImg = pGo.GetComponent<RawImage>();

            _drvNameTxt = CreateLabel(card.transform, "Max Verstappen", 28, FontStyle.Bold, TextAnchor.LowerLeft, Color.white, new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(142, 0), new Vector2(-16, -10));
            _drvSuitHelmTxt = CreateLabel(card.transform, "HELMET: VERSTAPPEN • SUIT: REDBULL", 16, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.95f, 0.95f, 0.98f, 0.85f), new Vector2(0, 0), new Vector2(1, 0.45f), new Vector2(142, 10), new Vector2(-16, 0));

            CreateCircleButton(_panelDriver.transform, "<", new Vector2(0, 0.5f), new Vector2(68, 0), 76, new Color(0.78f, 0.06f, 0.12f, 0.94f), () => _gm.CycleDriver(-1));
            CreateCircleButton(_panelDriver.transform, ">", new Vector2(1, 0.5f), new Vector2(-68, 0), 76, new Color(0.78f, 0.06f, 0.12f, 0.94f), () => _gm.CycleDriver(1));

            var botBar = CreatePanel(_panelDriver.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 114), new Color(0.05f, 0.06f, 0.08f, 0.90f));
            CreateActionButton(botBar.transform, "LOAD HELMET", new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(220, 64), new Color(0.14f, 0.16f, 0.20f, 0.95f), () => _gm.CycleHelmet());
            CreateActionButton(botBar.transform, "LOAD SUIT", new Vector2(0, 0.5f), new Vector2(390, 0), new Vector2(220, 64), new Color(0.14f, 0.16f, 0.20f, 0.95f), () => _gm.CycleSuit());
            CreateActionButton(botBar.transform, "ZOOM HELMET", new Vector2(0, 0.5f), new Vector2(630, 0), new Vector2(220, 64), new Color(0.14f, 0.16f, 0.20f, 0.95f), () => { _gm.showroomZoomed = !_gm.showroomZoomed; });
            CreateActionButton(botBar.transform, "CONTINUE  >", new Vector2(1, 0.5f), new Vector2(-180, 0), new Vector2(280, 70), new Color(0.84f, 0.07f, 0.12f, 1f), () => _gm.SetScreenState(MotoveloScreenState.ShowroomHub));
        }

        // =====================================================================
        // 3. REAL F1 CIRCUIT CAROUSEL (20 bacinger/f1-circuits GPS Tracks!)
        // =====================================================================
        private void BuildTrackPanel(Transform root)
        {
            _panelTrack = CreateFullScreenPanel(root, "PanelTrack", new Color(0.05f, 0.06f, 0.09f, 0.96f));

            CreateActionButton(_panelTrack.transform, "<  BACK", new Vector2(0, 1), new Vector2(116, -54), new Vector2(160, 54), new Color(0.16f, 0.18f, 0.22f, 0.95f), () => _gm.SetScreenState(MotoveloScreenState.ShowroomHub));
            CreateLabel(_panelTrack.transform, "SELECT GRAND PRIX CIRCUIT (REAL F1 GPS LAYOUTS)", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(215, -81), new Vector2(960, -27));

            // Horizontal ScrollView
            var scrollGo = new GameObject("TrackScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scrollGo.transform.SetParent(_panelTrack.transform, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0.16f);
            srt.anchorMax = new Vector2(1, 0.86f);
            srt.offsetMin = new Vector2(36, 0);
            srt.offsetMax = new Vector2(-36, 0);
            scrollGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            scrollGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _trackListContent = contentGo.transform;
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0);
            crt.anchorMax = new Vector2(0, 1);
            crt.pivot = new Vector2(0, 0.5f);

            var hlg = contentGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24f;
            hlg.padding = new RectOffset(16, 16, 16, 16);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.content = crt;
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Elastic;

            _trackCardBorders.Clear();
            var circuits = MotoveloDatabase.Circuits;
            for (int i = 0; i < circuits.Length; i++)
            {
                int captured = i;
                var c = circuits[i];
                var cardBorder = CreatePanel(contentGo.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, new Color(0.84f, 0.08f, 0.14f, 1f), true);
                cardBorder.GetComponent<RectTransform>().sizeDelta = new Vector2(290, 560);
                _trackCardBorders.Add(cardBorder.GetComponent<Image>());

                var inner = CreatePanel(cardBorder.transform, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5), new Color(0.09f, 0.10f, 0.14f, 1f), true);
                var btn = cardBorder.AddComponent<Button>();
                btn.onClick.AddListener(() => _gm.SelectCircuit(captured));

                var photoGo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage));
                photoGo.transform.SetParent(inner.transform, false);
                var prt = photoGo.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0, 0.28f);
                prt.anchorMax = new Vector2(1, 1);
                prt.offsetMin = Vector2.zero;
                prt.offsetMax = Vector2.zero;
                photoGo.GetComponent<RawImage>().texture = MotoveloDatabase.LoadTex($"Textures/tracks/{c.id}");

                var footer = CreatePanel(inner.transform, new Vector2(0, 0), new Vector2(1, 0.30f), Vector2.zero, Vector2.zero, new Color(0.76f, 0.06f, 0.11f, 0.96f));
                CreateLabel(footer.transform, c.name, 22, FontStyle.Bold, TextAnchor.LowerRight, Color.white, new Vector2(0, 0.52f), new Vector2(1, 1), new Vector2(12, 0), new Vector2(-14, -4));
                CreateLabel(footer.transform, $"{c.country} • {c.lengthM}M", 15, FontStyle.Bold, TextAnchor.UpperRight, new Color(0.95f, 0.95f, 0.98f, 0.88f), new Vector2(0, 0), new Vector2(1, 0.52f), new Vector2(12, 4), new Vector2(-14, 0));
            }

            CreateActionButton(_panelTrack.transform, "RACE THIS CIRCUIT  >", new Vector2(1, 0), new Vector2(-200, 64), new Vector2(330, 70), new Color(0.84f, 0.07f, 0.12f, 1f), () => _gm.LaunchSession(RaceSessionMode.QuickRace));
        }

        // =====================================================================
        // 4. CLEAN CAR SETUP, TYRES & DRIVING ASSISTS
        // =====================================================================
        private void BuildSetupPanel(Transform root)
        {
            _panelSetup = CreateFullScreenPanel(root, "PanelSetup", new Color(0.05f, 0.06f, 0.09f, 0.94f));

            CreateActionButton(_panelSetup.transform, "<  BACK", new Vector2(0, 1), new Vector2(116, -54), new Vector2(160, 54), new Color(0.16f, 0.18f, 0.22f, 0.95f), () => _gm.SetScreenState(MotoveloScreenState.ShowroomHub));
            CreateLabel(_panelSetup.transform, "CAR SETUP, PIRELLI TYRES & CONTROLS", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(215, -81), new Vector2(880, -27));

            // 5 Pirelli Tyre Compound Cards
            CreateLabel(_panelSetup.transform, "SELECT TYRE COMPOUND", 18, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.90f, 0.22f, 0.26f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -165), new Vector2(0, -125));
            for (int i = 0; i < MotoveloDatabase.Compounds.Length; i++)
            {
                int idx = i;
                var comp = MotoveloDatabase.Compounds[i];
                float xPos = (i - 2) * 240f;
                var cCard = CreatePanel(_panelSetup.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(xPos - 105, -405), new Vector2(xPos + 105, -180), comp.color * 0.85f, true);
                var btn = cCard.AddComponent<Button>();
                btn.onClick.AddListener(() => _gm.SelectCompound(idx));

                var tImgGo = new GameObject("TyreImg", typeof(RectTransform), typeof(RawImage));
                tImgGo.transform.SetParent(cCard.transform, false);
                var trt = tImgGo.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0.5f, 0.55f);
                trt.anchorMax = new Vector2(0.5f, 0.55f);
                trt.sizeDelta = new Vector2(138, 138);
                tImgGo.GetComponent<RawImage>().texture = MotoveloDatabase.LoadTex($"Textures/tyres/{comp.id}");

                CreateLabel(cCard.transform, comp.label, 22, FontStyle.Bold, TextAnchor.LowerCenter, Color.white, new Vector2(0, 0), new Vector2(1, 0.30f), new Vector2(0, 10), Vector2.zero);
            }
            _setupCompoundTxt = CreateLabel(_panelSetup.transform, "ACTIVE: SOFT", 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -455), new Vector2(0, -415));

            // Clean Toggle Rows (No ugly web sliders!)
            float rowY = -540f;
            _setupWingsTxt = BuildOptionStepper(_panelSetup.transform, "WINGS AERO BALANCE", rowY, () =>
            {
                float[] presets = { -35f, -15f, 0f, 20f, 40f };
                int cur = System.Array.IndexOf(presets, _gm.wingsSetup);
                _gm.wingsSetup = presets[(cur + 1 + presets.Length) % presets.Length];
                SyncAllLabels();
            });
            _setupLapsTxt = BuildOptionStepper(_panelSetup.transform, "RACE DISTANCE", rowY - 82f, () =>
            {
                int[] laps = { 2, 3, 5, 10, 25 };
                int cur = System.Array.IndexOf(laps, _gm.selectedLaps);
                _gm.selectedLaps = laps[(cur + 1) % laps.Length];
                SyncAllLabels();
            });
            _setupDiffTxt = BuildOptionStepper(_panelSetup.transform, "AI RIVAL DIFFICULTY", rowY - 164f, () =>
            {
                _gm.aiDifficulty = (_gm.aiDifficulty + 1) % 3;
                SyncAllLabels();
            });
            _setupAssistTxt = BuildOptionStepper(_panelSetup.transform, "DRIVING & BRAKING ASSIST", rowY - 246f, () =>
            {
                _gm.drivingAssist = !_gm.drivingAssist;
                _gm.SavePrefs();
                SyncAllLabels();
            });
            _setupTiltTxt = BuildOptionStepper(_panelSetup.transform, "STEERING MODE", rowY - 328f, () =>
            {
                _gm.tiltSteering = !_gm.tiltSteering;
                _gm.SavePrefs();
                SyncAllLabels();
            });

            CreateActionButton(_panelSetup.transform, "START GRAND PRIX  >", new Vector2(1, 0), new Vector2(-200, 64), new Vector2(330, 70), new Color(0.84f, 0.07f, 0.12f, 1f), () => _gm.LaunchSession(RaceSessionMode.QuickRace));
        }

        private Text BuildOptionStepper(Transform parent, string title, float yTop, UnityEngine.Events.UnityAction onClick)
        {
            var row = CreatePanel(parent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-460, yTop - 64), new Vector2(460, yTop), new Color(0.11f, 0.13f, 0.17f, 0.95f), true);
            CreateLabel(row.transform, title, 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.82f, 0.86f, 0.92f), new Vector2(0, 0), new Vector2(0.6f, 1), new Vector2(24, 0), Vector2.zero);
            var btnGo = CreateActionButton(row.transform, "PRO", new Vector2(1, 0.5f), new Vector2(-145, 0), new Vector2(250, 48), new Color(0.78f, 0.06f, 0.12f, 0.95f), onClick);
            return btnGo.GetComponentInChildren<Text>();
        }

        // =====================================================================
        // 5. MONOPOSTO STARTING GRID BROADCAST INTRO
        // =====================================================================
        private void BuildGridIntroPanel(Transform root)
        {
            _panelGridIntro = CreateFullScreenPanel(root, "PanelGridIntro", new Color(0, 0, 0, 0.18f));
            var skipBtn = _panelGridIntro.AddComponent<Button>();
            skipBtn.onClick.AddListener(() => _gm.SkipGridIntroToCountdown());

            CreateLabel(_panelGridIntro.transform, "STARTING GRID", 54, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -160), new Vector2(0, -70));

            var card = CreatePanel(_panelGridIntro.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-560, 70), new Vector2(-60, 560), new Color(0.06f, 0.07f, 0.10f, 0.94f), true);
            var portGo = new GameObject("GridPortrait", typeof(RectTransform), typeof(RawImage));
            portGo.transform.SetParent(card.transform, false);
            var prt = portGo.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0, 0.28f);
            prt.anchorMax = new Vector2(1, 1);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            _gridPortraitImg = portGo.GetComponent<RawImage>();

            var bar = CreatePanel(card.transform, new Vector2(0, 0), new Vector2(1, 0.28f), Vector2.zero, Vector2.zero, new Color(0.04f, 0.05f, 0.07f, 0.98f));
            _gridPosTxt = CreateLabel(bar.transform, "P8", 42, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.76f, 0.12f), new Vector2(0, 0), new Vector2(0.24f, 1), Vector2.zero, Vector2.zero);

            var glGo = new GameObject("GridLogo", typeof(RectTransform), typeof(RawImage));
            glGo.transform.SetParent(bar.transform, false);
            var glrt = glGo.GetComponent<RectTransform>();
            glrt.anchorMin = new Vector2(0.25f, 0.22f);
            glrt.anchorMax = new Vector2(0.39f, 0.78f);
            glrt.offsetMin = Vector2.zero;
            glrt.offsetMax = Vector2.zero;
            _gridLogoImg = glGo.GetComponent<RawImage>();

            _gridDriverTxt = CreateLabel(bar.transform, "Max Verstappen", 26, FontStyle.BoldAndItalic, TextAnchor.LowerRight, Color.white, new Vector2(0.40f, 0.45f), new Vector2(1, 1), Vector2.zero, new Vector2(-18, -4));
            _gridTeamTxt = CreateLabel(bar.transform, "Red Bull Racing", 18, FontStyle.Bold, TextAnchor.UpperRight, new Color(0.80f, 0.84f, 0.90f), new Vector2(0.40f, 0), new Vector2(1, 0.45f), Vector2.zero, new Vector2(-18, 0));
        }

        // =====================================================================
        // 6. LIBRECORSA-INSPIRED BROADCAST HUD & ERGONOMIC TOUCH CONTROLS
        // =====================================================================
        private void BuildHudPanel(Transform root)
        {
            _panelHud = CreateFullScreenPanel(root, "PanelHud", Color.clear);

            Color cardBg = new Color(0.05f, 0.06f, 0.09f, 0.88f);
            Color cardBorder = new Color(1f, 1f, 1f, 0.14f);
            Color dividerCol = new Color(1f, 1f, 1f, 0.10f);

            // -----------------------------------------------------------------
            // TOP-LEFT: LIBRECORSA LEADERBOARD CARD (LAP + ROWS + AHEAD FOOTER)
            // -----------------------------------------------------------------
            var towerOuter = CreatePanel(_panelHud.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -296), new Vector2(310, -20), cardBorder, true);
            var tower = CreatePanel(towerOuter.transform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f), cardBg, true);

            var tHdr = CreatePanel(tower.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -34), Vector2.zero, new Color(0.08f, 0.10f, 0.14f, 0.92f));
            _towerHeaderBg = tHdr.GetComponent<Image>();
            _hudLapTxt = CreateLabel(tHdr.transform, "LAP 1/3", 17, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            _towerHeaderTxt = _hudLapTxt;
            CreatePanel(tower.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -35), new Vector2(0, -34), dividerCol);

            _towerRows.Clear();
            const int maxTowerRows = 14;
            for (int i = 0; i < maxTowerRows; i++)
            {
                float yTop = -38f - i * 32f;
                var rowGo = CreatePanel(tower.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, yTop - 30f), new Vector2(-4, yTop), Color.clear, true);
                var rowBg = rowGo.GetComponent<Image>();
                var posTxt = CreateLabel(rowGo.transform, $"{i + 1}", 14, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.75f, 0.78f, 0.85f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 0), new Vector2(28, 0));
                var barGo = CreatePanel(rowGo.transform, new Vector2(0, 0.18f), new Vector2(0, 0.82f), new Vector2(34, 0), new Vector2(38, 0), Color.white);
                var codeTxt = CreateLabel(rowGo.transform, "VER", 15, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 0), new Vector2(0, 1), new Vector2(46, 0), new Vector2(124, 0));

                var tyreGo = CreatePanel(rowGo.transform, new Vector2(0.58f, 0.5f), new Vector2(0.58f, 0.5f), new Vector2(-10, -10), new Vector2(10, 10), new Color(0.88f, 0.12f, 0.18f, 1f));
                var tyreImg = tyreGo.GetComponent<Image>();
                tyreImg.sprite = _circleSprite;
                var tyreTxt = CreateLabel(tyreGo.transform, "S", 11, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var gapTxt = CreateLabel(rowGo.transform, "+0.000", 14, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.88f, 0.90f, 0.95f), new Vector2(0.64f, 0), new Vector2(1, 1), Vector2.zero, new Vector2(-8, 0));

                rowGo.SetActive(i < 5);
                _towerRows.Add(new TowerRowUI
                {
                    root = rowGo,
                    bg = rowBg,
                    posTxt = posTxt,
                    teamBar = barGo.GetComponent<Image>(),
                    codeTxt = codeTxt,
                    tyreBg = tyreImg,
                    tyreTxt = tyreTxt,
                    gapTxt = gapTxt
                });
            }

            // Bottom AHEAD footer inside leaderboard card
            CreatePanel(tower.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 34), new Vector2(0, 35), dividerCol);
            CreateLabel(tower.transform, "AHEAD", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.65f, 0.69f, 0.76f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 2), new Vector2(76, 32));
            var aheadBar = CreatePanel(tower.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(82, 8), new Vector2(86, 26), Color.white);
            _aheadBarImg = aheadBar.GetComponent<Image>();
            _aheadCodeTxt = CreateLabel(tower.transform, "---", 14, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 0), new Vector2(0, 0), new Vector2(94, 2), new Vector2(165, 32));
            _aheadGapTxt = CreateLabel(tower.transform, "-.---", 14, FontStyle.Bold, TextAnchor.MiddleRight, Color.white, new Vector2(0.55f, 0), new Vector2(1, 0), new Vector2(0, 2), new Vector2(-12, 32));

            // -----------------------------------------------------------------
            // TOP-CENTER: LIBRECORSA START LIGHTS / LIGHTS OUT & STATUS BANNERS
            // -----------------------------------------------------------------
            var lightsOuter = CreatePanel(_panelHud.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-240, -142), new Vector2(240, -20), cardBorder, true);
            _startLightsWrap = lightsOuter;
            var lightsInner = CreatePanel(lightsOuter.transform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f), new Color(0.05f, 0.06f, 0.09f, 0.92f), true);
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 34f;
                var pod = CreatePanel(lightsInner.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x - 11, -34), new Vector2(x + 11, -12), new Color(0.18f, 0.04f, 0.05f, 1f));
                var img = pod.GetComponent<Image>();
                img.sprite = _circleSprite;
                _startLightLenses[i] = img;
            }
            _startLightsBannerTxt = CreateLabel(lightsInner.transform, "LIGHTS OUT", 44, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 0), new Vector2(1, 0.72f), new Vector2(12, 6), new Vector2(-12, -4));

            // Fastest Lap Purple Banner
            _flBannerWrap = CreatePanel(_panelHud.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-230, -82), new Vector2(230, -22), new Color(0.54f, 0.08f, 0.78f, 0.94f), true);
            _flBannerTxt = CreateLabel(_flBannerWrap.transform, "FASTEST LAP", 17, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));

            // Live FIA Pit Lane, Pit Crew Stopwatch & Stewards Regulation Banner
            _pitStatusBannerWrap = CreatePanel(_panelHud.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-340, -152), new Vector2(340, -92), new Color(0.06f, 0.08f, 0.12f, 0.94f), true);
            _pitStatusBannerBg = _pitStatusBannerWrap.GetComponent<Image>();
            _pitStatusBannerTxt = CreateLabel(_pitStatusBannerWrap.transform, "", 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-14, 0));
            _pitStatusBannerWrap.SetActive(false);

            // -----------------------------------------------------------------
            // TOP-RIGHT: LIBRECORSA TIMING & WEATHER CARDS + ACTION PILLS
            // -----------------------------------------------------------------
            var timingOuter = CreatePanel(_panelHud.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-346, -158), new Vector2(-24, -20), cardBorder, true);
            var timing = CreatePanel(timingOuter.transform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f), cardBg, true);

            // Sector Strip (S1 | S2 | S3)
            for (int s = 0; s < 3; s++)
            {
                float x0 = s / 3f;
                float x1 = (s + 1) / 3f;
                var sBar = CreatePanel(timing.transform, new Vector2(x0, 1), new Vector2(x1, 1), new Vector2(4, -4), new Vector2(-4, -1), new Color(1f, 1f, 1f, 0.14f));
                _sectorBars[s] = sBar.GetComponent<Image>();
                _sectorLabels[s] = CreateLabel(timing.transform, $"S{s + 1}", 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.75f, 0.79f, 0.86f), new Vector2(x0, 1), new Vector2(x1, 1), new Vector2(0, -28), new Vector2(0, -4));
            }
            CreatePanel(timing.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30), new Vector2(0, -29), dividerCol);

            // Big Position + Lap Time
            _hudPosBigTxt = CreateLabel(timing.transform, "1", 38, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 0.28f), new Vector2(0.24f, 0.78f), new Vector2(14, 0), Vector2.zero);
            _hudPosTotalTxt = CreateLabel(timing.transform, "/22", 16, FontStyle.Bold, TextAnchor.LowerLeft, new Color(0.65f, 0.69f, 0.76f), new Vector2(0.18f, 0.34f), new Vector2(0.38f, 0.62f), Vector2.zero, Vector2.zero);
            _hudTimeTxt = CreateLabel(timing.transform, "0:00.000", 30, FontStyle.Bold, TextAnchor.MiddleRight, Color.white, new Vector2(0.36f, 0.28f), new Vector2(1, 0.78f), Vector2.zero, new Vector2(-14, 0));

            CreatePanel(timing.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 36), new Vector2(0, 37), dividerCol);
            CreateLabel(timing.transform, "BEST", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.62f, 0.66f, 0.74f), new Vector2(0, 0), new Vector2(0.35f, 0), new Vector2(14, 4), new Vector2(0, 34));
            _hudBestTxt = CreateLabel(timing.transform, "-:--.---", 16, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.82f, 0.86f, 0.92f), new Vector2(0.35f, 0), new Vector2(1, 0), Vector2.zero, new Vector2(-14, 34));

            // Weather / Track Condition Card below Timing Card
            var weatherOuter = CreatePanel(_panelHud.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-346, -226), new Vector2(-24, -166), cardBorder, true);
            var weatherCard = CreatePanel(weatherOuter.transform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f), cardBg, true);
            _weatherTempTxt = CreateLabel(weatherCard.transform, "AIR 27°   TRACK 41°   DRY", 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.86f, 0.90f, 0.95f), new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(14, 0), new Vector2(-14, -4));
            _weatherTrackTxt = CreateLabel(weatherCard.transform, "TYRE 100%  •  88°C", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.64f, 0.69f, 0.76f), new Vector2(0, 0), new Vector2(1, 0.48f), new Vector2(14, 4), new Vector2(-14, 0));

            // Compact Action Pills below Weather Card (SFX, TOWER, CAM, PAUSE)
            var sfxGo = CreateActionButton(_panelHud.transform, "SFX ON", new Vector2(1, 1), new Vector2(-306, -254), new Vector2(78, 34), cardBg, () => { if (_gm.audioSynth != null) { _gm.audioSynth.audioEnabled = !_gm.audioSynth.audioEnabled; if (_sfxBtnTxt != null) _sfxBtnTxt.text = _gm.audioSynth.audioEnabled ? "SFX ON" : "MUTED"; } });
            _sfxBtnTxt = sfxGo.GetComponentInChildren<Text>();
            if (_sfxBtnTxt != null) _sfxBtnTxt.fontSize = 12;

            var towerBtn = CreateActionButton(_panelHud.transform, "TOWER", new Vector2(1, 1), new Vector2(-222, -254), new Vector2(78, 34), cardBg, () => { _gm.fullLeaderboard = !_gm.fullLeaderboard; _gm.audioSynth?.TriggerClick(); });
            var towerBtnTxt = towerBtn.GetComponentInChildren<Text>();
            if (towerBtnTxt != null) towerBtnTxt.fontSize = 12;

            var camBtn = CreateActionButton(_panelHud.transform, "CAM", new Vector2(1, 1), new Vector2(-138, -254), new Vector2(78, 34), cardBg, () => { _gm.cameraRig.CycleCamera(); _gm.audioSynth?.TriggerClick(); });
            var camBtnTxt = camBtn.GetComponentInChildren<Text>();
            if (camBtnTxt != null) camBtnTxt.fontSize = 12;

            var pauseBtn = CreateActionButton(_panelHud.transform, "II", new Vector2(1, 1), new Vector2(-58, -254), new Vector2(68, 34), cardBg, () => _gm.SetScreenState(MotoveloScreenState.Paused));
            var pauseBtnTxt = pauseBtn.GetComponentInChildren<Text>();
            if (pauseBtnTxt != null) pauseBtnTxt.fontSize = 14;

            // -----------------------------------------------------------------
            // BOTTOM-LEFT: LIBRECORSA CIRCUIT MINIMAP CARD
            // -----------------------------------------------------------------
            var mapOuter = CreatePanel(_panelHud.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 210), new Vector2(224, 370), cardBorder, true);
            _minimapWrapRt = mapOuter.GetComponent<RectTransform>();
            var mapInner = CreatePanel(mapOuter.transform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f), new Color(0.05f, 0.06f, 0.09f, 0.84f), true);
            var mapGo = new GameObject("MinimapRaw", typeof(RectTransform), typeof(RawImage));
            mapGo.transform.SetParent(mapInner.transform, false);
            StretchFull(mapGo.GetComponent<RectTransform>(), 8);
            _minimapImg = mapGo.GetComponent<RawImage>();
            _minimapTex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            _minimapClear = new Color32[128 * 128];
            _minimapImg.texture = _minimapTex;

            // -----------------------------------------------------------------
            // BOTTOM-CENTER: LIBRECORSA TELEMETRY POD (BATT + GEAR + KM/H)
            // -----------------------------------------------------------------
            var podWrap = CreatePanel(_panelHud.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-235, 16), new Vector2(235, 174), Color.clear);

            // Center Gear & Mode Card
            var centerOuter = CreatePanel(podWrap.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(-118, 6), new Vector2(118, -6), cardBorder, true);
            var centerCard = CreatePanel(centerOuter.transform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f), cardBg, true);

            // 11 RPM Shift Dots across top of Center Pod
            for (int i = 0; i < 11; i++)
            {
                float dx = -80f + i * 16f;
                var dot = CreatePanel(centerCard.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(dx - 4.5f, -18), new Vector2(dx + 4.5f, -9), new Color(1, 1, 1, 0.16f));
                dot.GetComponent<Image>().sprite = _circleSprite;
                _rpmDots[i] = dot.GetComponent<Image>();
            }

            // Acid-Lime Gear Square
            var gearBox = CreatePanel(centerCard.transform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(-34, -26), new Vector2(34, 34), new Color(0.83f, 1.0f, 0.0f, 1f), true);
            _hudGearTxt = CreateLabel(gearBox.transform, "N", 38, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter, new Color(0.05f, 0.06f, 0.08f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _hudGearSubTxt = CreateLabel(centerCard.transform, "NEUTRAL", 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.70f, 0.74f, 0.80f), new Vector2(0, 0.24f), new Vector2(1, 0.38f), Vector2.zero, Vector2.zero);

            // Bottom Mode Pills inside Center Pod: OVERTAKE + X ARMED (DRS)
            var ovtPill = CreatePanel(centerCard.transform, new Vector2(0.06f, 0.06f), new Vector2(0.49f, 0.23f), Vector2.zero, Vector2.zero, new Color(0.14f, 0.17f, 0.22f, 0.95f), true);
            _overtakePillBg = ovtPill.GetComponent<Image>();
            _overtakePillTxt = CreateLabel(ovtPill.transform, "OVERTAKE", 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.85f, 0.90f, 0.96f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var drsPill = CreatePanel(centerCard.transform, new Vector2(0.51f, 0.06f), new Vector2(0.94f, 0.23f), Vector2.zero, Vector2.zero, new Color(0.14f, 0.17f, 0.22f, 0.95f), true);
            _drsBadgeBg = drsPill.GetComponent<Image>();
            _drsBadgeTxt = CreateLabel(drsPill.transform, "DRS", 10, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Left Circular Gauge: BATT (ERS)
            var battBg = CreatePanel(podWrap.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, -68), new Vector2(136, 68), new Color(0.05f, 0.06f, 0.09f, 0.94f));
            battBg.GetComponent<Image>().sprite = _circleSprite;
            var battTrack = CreatePanel(battBg.transform, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4), new Color(1f, 1f, 1f, 0.12f));
            battTrack.GetComponent<Image>().sprite = _ringSprite;
            var battFillGo = CreatePanel(battBg.transform, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4), new Color(0.0f, 0.88f, 1.0f, 1f));
            _ersBarFill = battFillGo.GetComponent<Image>();
            _ersBarFill.sprite = _ringSprite;
            _ersBarFill.type = Image.Type.Filled;
            _ersBarFill.fillMethod = Image.FillMethod.Radial360;
            _ersBarFill.fillOrigin = (int)Image.Origin360.Bottom;
            _ersBarFill.fillClockwise = true;
            CreateLabel(battBg.transform, "BATT", 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.68f, 0.74f, 0.82f), new Vector2(0, 0.62f), new Vector2(1, 0.82f), Vector2.zero, Vector2.zero);
            _battPctTxt = CreateLabel(battBg.transform, "100%", 28, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 0.25f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);

            // Right Circular Gauge: KM/H + RPM
            var spdBg = CreatePanel(podWrap.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-136, -68), new Vector2(0, 68), new Color(0.05f, 0.06f, 0.09f, 0.94f));
            spdBg.GetComponent<Image>().sprite = _circleSprite;
            var spdTrack = CreatePanel(spdBg.transform, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4), new Color(1f, 1f, 1f, 0.12f));
            spdTrack.GetComponent<Image>().sprite = _ringSprite;
            var spdFillGo = CreatePanel(spdBg.transform, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4), Color.white);
            _speedRingFill = spdFillGo.GetComponent<Image>();
            _speedRingFill.sprite = _ringSprite;
            _speedRingFill.type = Image.Type.Filled;
            _speedRingFill.fillMethod = Image.FillMethod.Radial360;
            _speedRingFill.fillOrigin = (int)Image.Origin360.Bottom;
            _speedRingFill.fillClockwise = true;
            CreateLabel(spdBg.transform, "KM/H", 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.68f, 0.74f, 0.82f), new Vector2(0, 0.64f), new Vector2(1, 0.84f), Vector2.zero, Vector2.zero);
            _hudSpeedTxt = CreateLabel(spdBg.transform, "0", 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 0.30f), new Vector2(1, 0.68f), Vector2.zero, Vector2.zero);
            _hudRpmTxt = CreateLabel(spdBg.transform, "4800 rpm", 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.68f, 0.74f, 0.82f), new Vector2(0, 0.14f), new Vector2(1, 0.32f), Vector2.zero, Vector2.zero);

            // -----------------------------------------------------------------
            // BOTTOM-RIGHT: LIBRECORSA ASSIST PILL + TYRE/DAMAGE SILHOUETTE CARD
            // -----------------------------------------------------------------
            var assistOuter = CreatePanel(_panelHud.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-326, 206), new Vector2(-24, 240), cardBorder, true);
            var assistCard = CreatePanel(assistOuter.transform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f), cardBg, true);
            _assistPillTxt = CreateLabel(assistCard.transform, "ELEC  •  ASSIST ON  •  AERO -15", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.86f, 0.90f, 0.96f), new Vector2(0, 0), new Vector2(0.68f, 1), new Vector2(12, 0), Vector2.zero);

            var nextTyreGo = CreateActionButton(assistCard.transform, "NEXT: MED", new Vector2(1, 0.5f), new Vector2(-52, 0), new Vector2(96, 26), new Color(0.16f, 0.19f, 0.25f, 0.95f), () => _gm.CycleNextPitCompound());
            _pitBtnBg = nextTyreGo.GetComponent<Image>();
            _nextTyreBtnTxt = nextTyreGo.GetComponentInChildren<Text>();
            if (_nextTyreBtnTxt != null) _nextTyreBtnTxt.fontSize = 11;

            var tyreOuter = CreatePanel(_panelHud.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-326, 20), new Vector2(-24, 198), cardBorder, true);
            var tyreCard = CreatePanel(tyreOuter.transform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f), cardBg, true);

            // Top-down F1 Car Silhouette in Center of Card
            var silColor = new Color(0.50f, 0.55f, 0.64f, 0.85f);
            CreatePanel(tyreCard.transform, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(-22, 34), new Vector2(22, 42), silColor, true); // Front wing
            CreatePanel(tyreCard.transform, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(-6, -34), new Vector2(6, 40), silColor, true);   // Nose/spine
            CreatePanel(tyreCard.transform, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(-16, -24), new Vector2(16, 12), silColor, true); // Sidepods
            CreatePanel(tyreCard.transform, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(-20, -40), new Vector2(20, -33), silColor, true); // Rear wing

            // 4 Wheel Rects on Silhouette + 4 Circular Tyre Health Rings (FL, FR, RL, RR)
            Vector2[] wheelAnchors = {
                new Vector2(-24, 18), new Vector2(24, 18),
                new Vector2(-24, -22), new Vector2(24, -22)
            };
            Vector2[] ringOffsets = {
                new Vector2(-92, 34), new Vector2(92, 34),
                new Vector2(-92, -26), new Vector2(92, -26)
            };
            for (int w = 0; w < 4; w++)
            {
                var wr = CreatePanel(tyreCard.transform, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), wheelAnchors[w] - new Vector2(5, 9), wheelAnchors[w] + new Vector2(5, 9), new Color(0.15f, 0.92f, 0.45f, 1f), true);
                _carTyreRects[w] = wr.GetComponent<Image>();

                var tRingTrack = CreatePanel(tyreCard.transform, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), ringOffsets[w] - new Vector2(26, 26), ringOffsets[w] + new Vector2(26, 26), new Color(1f, 1f, 1f, 0.14f));
                tRingTrack.GetComponent<Image>().sprite = _ringSprite;
                var tRingFill = CreatePanel(tRingTrack.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.15f, 0.92f, 0.45f, 1f));
                var rImg = tRingFill.GetComponent<Image>();
                rImg.sprite = _ringSprite;
                rImg.type = Image.Type.Filled;
                rImg.fillMethod = Image.FillMethod.Radial360;
                rImg.fillOrigin = (int)Image.Origin360.Top;
                rImg.fillClockwise = true;
                _tyreRings[w] = rImg;
                _tyrePcts[w] = CreateLabel(tRingTrack.transform, "100%", 12, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            // Bottom row of Tyre Card: Compound Badge + Status
            CreatePanel(tyreCard.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(0, 31), dividerCol);
            var cBadge = CreatePanel(tyreCard.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 5), new Vector2(32, 25), new Color(0.88f, 0.12f, 0.18f, 1f));
            _cardCompoundBg = cBadge.GetComponent<Image>();
            _cardCompoundBg.sprite = _circleSprite;
            _cardCompoundTxt = CreateLabel(cBadge.transform, "S", 12, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _damageStatusTxt = CreateLabel(tyreCard.transform, "NO DAMAGE", 12, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.75f, 0.80f, 0.86f), new Vector2(0.25f, 0), new Vector2(1, 0), Vector2.zero, new Vector2(-14, 30));

            // -----------------------------------------------------------------
            // ERGONOMIC TRANSLUCENT TOUCH CONTROLS (PRESERVED!)
            // -----------------------------------------------------------------
            _btnLeft  = CreateHoldControl(_panelHud.transform, "<",     new Vector2(0, 0), new Vector2(102, 102), new Vector2(132, 132), 48, new Color(1, 1, 1, 0.16f), true);
            _btnRight = CreateHoldControl(_panelHud.transform, ">",     new Vector2(0, 0), new Vector2(256, 102), new Vector2(132, 132), 48, new Color(1, 1, 1, 0.16f), true);

            _btnBrake = CreateHoldControl(_panelHud.transform, "BRAKE", new Vector2(1, 0), new Vector2(-510, 96), new Vector2(124, 106), 17, new Color(0.90f, 0.16f, 0.20f, 0.26f), false);
            _btnGas   = CreateHoldControl(_panelHud.transform, "GAS",   new Vector2(1, 0), new Vector2(-380, 114), new Vector2(118, 144), 19, new Color(0.18f, 0.90f, 0.42f, 0.26f), false);

            _btnErs   = CreateHoldControl(_panelHud.transform, "ERS",   new Vector2(0.5f, 0), new Vector2(-286, 84), new Vector2(76, 76), 16, new Color(0.0f, 0.72f, 0.95f, 0.36f), true);
            _btnDrs   = CreateHoldControl(_panelHud.transform, "DRS",   new Vector2(0.5f, 0), new Vector2(286, 84),  new Vector2(76, 76), 16, new Color(0.16f, 0.88f, 0.40f, 0.36f), true);
            _btnRear  = CreateHoldControl(_panelHud.transform, "REAR",  new Vector2(0.5f, 0), new Vector2(-286, 174), new Vector2(68, 40), 13, new Color(1f, 1f, 1f, 0.18f), false);
        }

        // =====================================================================
        // 7. PAUSE & CLASSIFICATION RESULTS
        // =====================================================================
        private void BuildPausePanel(Transform root)
        {
            _panelPause = CreateFullScreenPanel(root, "PanelPause", new Color(0.03f, 0.04f, 0.06f, 0.86f));
            var card = CreatePanel(_panelPause.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240, -210), new Vector2(240, 210), new Color(0.09f, 0.11f, 0.15f, 0.98f), true);
            CreateLabel(card.transform, "SESSION PAUSED", 28, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -76), new Vector2(0, -16));
            CreateActionButton(card.transform, "RESUME RACE",   new Vector2(0.5f, 0.5f), new Vector2(0, 65),  new Vector2(360, 62), new Color(0.84f, 0.07f, 0.12f, 1f), () => _gm.SetScreenState(MotoveloScreenState.Racing));
            CreateActionButton(card.transform, "RESTART",       new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(360, 58), new Color(0.16f, 0.18f, 0.24f, 1f), () => _gm.LaunchSession(_gm.sessionMode));
            CreateActionButton(card.transform, "SHOWROOM MENU", new Vector2(0.5f, 0.5f), new Vector2(0, -85), new Vector2(360, 58), new Color(0.16f, 0.18f, 0.24f, 1f), () => _gm.SetScreenState(MotoveloScreenState.ShowroomHub));
        }

        private void BuildResultsPanel(Transform root)
        {
            _panelResults = CreateFullScreenPanel(root, "PanelResults", new Color(0.03f, 0.04f, 0.06f, 0.90f));
            var card = CreatePanel(_panelResults.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-460, -360), new Vector2(460, 360), new Color(0.08f, 0.10f, 0.14f, 0.98f), true);
            _resTitleTxt = CreateLabel(card.transform, "P1 — GRAND PRIX CLASSIFICATION", 32, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(32, -82), new Vector2(-32, -18));
            _resBodyTxt = CreateLabel(card.transform, "", 19, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.90f, 0.92f, 0.96f), new Vector2(0, 0.18f), new Vector2(1, 1), new Vector2(32, 0), new Vector2(-32, -92));
            _resBodyTxt.lineSpacing = 1.24f;

            CreateActionButton(card.transform, "RACE AGAIN",    new Vector2(1, 0), new Vector2(-410, 56), new Vector2(240, 64), new Color(0.16f, 0.18f, 0.24f, 1f), () => _gm.LaunchSession(_gm.sessionMode));
            CreateActionButton(card.transform, "CONTINUE  >",   new Vector2(1, 0), new Vector2(-145, 56), new Vector2(240, 64), new Color(0.84f, 0.07f, 0.12f, 1f), () => _gm.SetScreenState(MotoveloScreenState.ShowroomHub));
        }

        // =====================================================================
        // STATE SYNC & LIVE HUD UPDATES
        // =====================================================================
        public void OnStateChanged(MotoveloScreenState next)
        {
            _panelShowroom.SetActive(next == MotoveloScreenState.ShowroomHub);
            _panelDriver.SetActive(next == MotoveloScreenState.DriverSelect);
            _panelTrack.SetActive(next == MotoveloScreenState.TrackSelect);
            _panelSetup.SetActive(next == MotoveloScreenState.CarSetup);
            _panelGridIntro.SetActive(next == MotoveloScreenState.GridIntro);
            _panelHud.SetActive(next == MotoveloScreenState.Countdown || next == MotoveloScreenState.Racing || next == MotoveloScreenState.Paused);
            _panelPause.SetActive(next == MotoveloScreenState.Paused);
            _panelResults.SetActive(next == MotoveloScreenState.Results);

            if (next == MotoveloScreenState.GridIntro)
            {
                var d = _gm.CurrentDriver;
                var t = _gm.CurrentTeam;
                _gridPortraitImg.texture = MotoveloDatabase.LoadTex($"Textures/drivers/{d.id}");
                _gridLogoImg.texture = MotoveloDatabase.LoadTex($"Textures/logos/{t.id}");
                _gridDriverTxt.text = d.name;
                _gridTeamTxt.text = t.name;
                _gridPosTxt.text = _gm.sessionMode == RaceSessionMode.TimeTrial ? "P1" : "P8";
            }
            else if (next == MotoveloScreenState.Results)
            {
                PopulateResults();
            }

            // Hide touch steer buttons when tilt steering is active for clean screen!
            if (_btnLeft != null)  _btnLeft.gameObject.SetActive(!_gm.tiltSteering);
            if (_btnRight != null) _btnRight.gameObject.SetActive(!_gm.tiltSteering);
            if (_minimapWrapRt != null)
            {
                if (_gm.tiltSteering)
                {
                    _minimapWrapRt.offsetMin = new Vector2(24, 20);
                    _minimapWrapRt.offsetMax = new Vector2(224, 180);
                }
                else
                {
                    _minimapWrapRt.offsetMin = new Vector2(24, 198);
                    _minimapWrapRt.offsetMax = new Vector2(224, 358);
                }
            }

            SyncAllLabels();
        }

        public void SyncAllLabels()
        {
            if (_gm == null) return;
            var t = _gm.CurrentTeam;
            var d = _gm.CurrentDriver;
            var c = _gm.CurrentCircuit;

            if (_teamLogoImg != null) _teamLogoImg.texture = MotoveloDatabase.LoadTex($"Textures/logos/{t.id}");
            if (_teamNameTxt != null) _teamNameTxt.text = t.name;
            if (_driverTagTxt != null) _driverTagTxt.text = $"#{d.number} {d.name}  •  {_gm.CurrentCompound.label} TYRES";
            if (_trackPillTxt != null && c != null) _trackPillTxt.text = $"{c.name}  •  {c.country}";
            if (_champPtsTxt != null) _champPtsTxt.text = $"CHAMPIONSHIP: {_gm.championshipPoints} PTS";

            if (_drvPortraitImg != null) _drvPortraitImg.texture = MotoveloDatabase.LoadTex($"Textures/drivers/{d.id}");
            if (_drvNameTxt != null) _drvNameTxt.text = $"#{d.number}  {d.name}";
            if (_drvSuitHelmTxt != null) _drvSuitHelmTxt.text = $"HELMET: {_gm.CurrentHelmetId.ToUpper()}   •   SUIT: {_gm.CurrentSuitId.ToUpper()}";

            for (int i = 0; i < _trackCardBorders.Count; i++)
            {
                _trackCardBorders[i].color = i == _gm.selectedCircuitIdx
                    ? new Color(1f, 0.16f, 0.22f, 1f)
                    : new Color(1f, 1f, 1f, 0.15f);
            }

            if (_setupCompoundTxt != null) _setupCompoundTxt.text = $"ACTIVE COMPOUND: {_gm.CurrentCompound.label}";
            if (_setupWingsTxt != null) _setupWingsTxt.text = $"{_gm.wingsSetup:+0;-0;0} ({(_gm.wingsSetup < 0 ? "HIGH SPEED" : "HIGH GRIP")})";
            if (_setupLapsTxt != null) _setupLapsTxt.text = $"{_gm.selectedLaps} LAPS";
            if (_setupDiffTxt != null) _setupDiffTxt.text = _gm.aiDifficulty == 0 ? "ROOKIE" : (_gm.aiDifficulty == 1 ? "PRO" : "LEGEND");
            if (_setupAssistTxt != null) _setupAssistTxt.text = _gm.drivingAssist ? "ON (SMOOTH)" : "OFF (PRO)";
            if (_setupTiltTxt != null) _setupTiltTxt.text = _gm.tiltSteering ? "TILT GYRO" : "TOUCH ARROWS";
        }

        public void ReadPlayerControls(out float steer, out float gas, out float brake, out bool ers, out bool drs, out bool rear)
        {
            steer = (_btnRight != null && _btnRight.isHeld ? 1f : 0f) - (_btnLeft != null && _btnLeft.isHeld ? 1f : 0f);
            gas   = _btnGas != null && _btnGas.isHeld ? 1f : 0f;
            brake = _btnBrake != null && _btnBrake.isHeld ? 1f : 0f;
            ers   = _btnErs != null && _btnErs.isHeld;
            drs   = _btnDrs != null && _btnDrs.isHeld;
            rear  = _btnRear != null && _btnRear.isHeld;
        }

        public void RefreshHUD()
        {
            var car = _gm.playerCar;
            if (car == null) return;

            bool inCountdown = _gm.state == MotoveloScreenState.Countdown;
            if (_wasInCountdown && !inCountdown && _gm.state == MotoveloScreenState.Racing)
                _lightsOutBannerTimer = 1.4f;
            _wasInCountdown = inCountdown;
            if (_lightsOutBannerTimer > 0f) _lightsOutBannerTimer -= Time.deltaTime;

            bool showLights = inCountdown || _lightsOutBannerTimer > 0f;
            _startLightsWrap.SetActive(showLights);
            if (showLights)
            {
                for (int i = 0; i < 5; i++)
                {
                    _startLightLenses[i].color = (inCountdown && i < _gm.litStartPods)
                        ? new Color(1f, 0.12f, 0.16f, 1f)
                        : new Color(0.16f, 0.18f, 0.22f, 0.9f);
                }
                if (_startLightsBannerTxt != null)
                {
                    if (!inCountdown) _startLightsBannerTxt.text = "LIGHTS OUT";
                    else if (_gm.litStartPods == 0) _startLightsBannerTxt.text = "GRID READY";
                    else _startLightsBannerTxt.text = $"LIGHTS {_gm.litStartPods}/5";
                }
            }

            _flBannerWrap.SetActive(!showLights && _gm.fastestLapBannerTimer > 0f);
            if (_gm.fastestLapBannerTimer > 0f)
                _flBannerTxt.text = $"FASTEST LAP  •  {_gm.fastestLapDriver}  {FormatTime(_gm.fastestLapTime)}";

            // Center Telemetry Pod: Speed, Gear, RPM, Batt, Overtake, DRS
            int kph = Mathf.RoundToInt(car.speed * 3.6f);
            bool isNeutral = inCountdown || car.speed < 0.4f;
            if (_hudSpeedTxt != null) _hudSpeedTxt.text = kph.ToString();
            if (_hudRpmTxt != null) _hudRpmTxt.text = $"{Mathf.RoundToInt(car.rpm)} rpm";
            if (_speedRingFill != null)
            {
                _speedRingFill.fillAmount = Mathf.Clamp01(kph / 340f);
                _speedRingFill.color = kph > 290 ? new Color(0.83f, 1.0f, 0.0f, 1f) : Color.white;
            }
            if (_hudGearTxt != null) _hudGearTxt.text = isNeutral ? "N" : car.gear.ToString();
            if (_hudGearSubTxt != null) _hudGearSubTxt.text = isNeutral ? "NEUTRAL" : $"GEAR {car.gear}";

            if (_hudTimeTxt != null) _hudTimeTxt.text = FormatTime(_gm.raceTime - car.lapStartTime);
            if (_hudBestTxt != null) _hudBestTxt.text = FormatTime(car.bestLapTime);

            // BATT (ERS) Circular Gauge + Overtake Pill
            if (_battPctTxt != null) _battPctTxt.text = $"{Mathf.RoundToInt(car.ersCharge * 100f)}%";
            _ersBarFill.fillAmount = car.ersCharge;
            _ersBarFill.color = car.ersActive
                ? new Color(0.20f, 1.0f, 0.65f, 1f)
                : (car.ersHarvesting ? new Color(0.75f, 0.38f, 1.0f, 1f) : new Color(0.0f, 0.88f, 1.0f, 1f));

            if (_overtakePillBg != null)
            {
                _overtakePillBg.color = car.ersActive
                    ? new Color(0.0f, 0.82f, 0.95f, 0.95f)
                    : (car.ersCharge > 0.1f ? new Color(0.16f, 0.20f, 0.28f, 0.95f) : new Color(0.12f, 0.13f, 0.17f, 0.8f));
                if (_overtakePillTxt != null)
                    _overtakePillTxt.color = car.ersActive ? new Color(0.04f, 0.05f, 0.08f, 1f) : new Color(0.85f, 0.90f, 0.96f, 1f);
            }

            if (car.drsActive)
            {
                _drsBadgeBg.color = new Color(0.10f, 0.88f, 0.45f, 1f);
                if (_drsBadgeTxt != null) { _drsBadgeTxt.text = "DRS OPEN"; _drsBadgeTxt.color = new Color(0.04f, 0.05f, 0.08f, 1f); }
            }
            else if (car.inDrsZone)
            {
                float pulse = 0.68f + Mathf.Sin(Time.time * 9f) * 0.25f;
                _drsBadgeBg.color = new Color(0.0f, 0.78f, 0.88f, pulse);
                if (_drsBadgeTxt != null) { _drsBadgeTxt.text = "X ARMED"; _drsBadgeTxt.color = Color.white; }
            }
            else
            {
                _drsBadgeBg.color = new Color(0.14f, 0.17f, 0.22f, 0.95f);
                if (_drsBadgeTxt != null) { _drsBadgeTxt.text = "DRS"; _drsBadgeTxt.color = new Color(0.70f, 0.75f, 0.82f, 1f); }
            }

            // Assist Pill & Next Pit Compound Strategy
            var nextComp = MotoveloDatabase.Compounds[Mathf.Clamp(car.nextPitCompoundIdx, 0, MotoveloDatabase.Compounds.Length - 1)];
            if (_nextTyreBtnTxt != null) _nextTyreBtnTxt.text = $"NEXT: {nextComp.label}";
            if (_pitBtnBg != null) _pitBtnBg.color = new Color(nextComp.color.r * 0.65f, nextComp.color.g * 0.65f, nextComp.color.b * 0.65f, 0.92f);
            if (_assistPillTxt != null)
            {
                string elecState = car.ersActive ? "DEPLOY" : (car.ersHarvesting ? "REGEN" : "ELEC");
                string assistTag = _gm.drivingAssist ? "TC ON" : "TC OFF";
                _assistPillTxt.text = $"{elecState}  •  {assistTag}  •  AERO {_gm.wingsSetup:+0;-0;0}";
            }

            // Live FIA Pit Crew Stopwatch, Pit Limiter, Pit Entry Approach & Stewards Regulation Banner!
            if (_pitStatusBannerWrap != null && _gm.activeTrack != null)
            {
                float signedS = _gm.activeTrack.GetSignedS(car.projection.s);
                string sideWord = _gm.activeTrack.pitSide > 0f ? "RIGHT" : "LEFT";

                if (car.pitPhase == PitLanePhase.ServicingInBox)
                {
                    _pitStatusBannerWrap.SetActive(true);
                    _pitStatusBannerBg.color = new Color(0.08f, 0.58f, 0.24f, 0.95f);
                    string action = car.pitServiceTimer < 1.25f ? $"REMOVING TYRES -> FITTING {nextComp.label}" : $"{car.compound.label} FITTED — DROPPING JACKS";
                    _pitStatusBannerTxt.text = $"PIT STOP  •  {car.pitServiceTimer:F2}s  •  {action}";
                }
                else if (car.pitPhase == PitLanePhase.EnteringPitLane)
                {
                    _pitStatusBannerWrap.SetActive(true);
                    _pitStatusBannerBg.color = new Color(0.88f, 0.58f, 0.04f, 0.95f);
                    _pitStatusBannerTxt.text = $"PIT LANE LIMITER 80 KPH  •  CREW READY WITH {nextComp.label} TYRES (TAP NEXT TO CHANGE)";
                }
                else if (car.pitPhase == PitLanePhase.ExitingPitLane)
                {
                    _pitStatusBannerWrap.SetActive(true);
                    _pitStatusBannerBg.color = new Color(0.10f, 0.72f, 0.30f, 0.95f);
                    _pitStatusBannerTxt.text = $"PIT EXIT  •  80 KPH LIMITER  •  STAY OUTSIDE WHITE LINE";
                }
                else if (car.fiaNoticeTimer > 0f && !string.IsNullOrEmpty(car.fiaNotice))
                {
                    _pitStatusBannerWrap.SetActive(true);
                    bool isPenalty = car.fiaNotice.Contains("PENALTY") || car.fiaNotice.Contains("WARNING");
                    _pitStatusBannerBg.color = isPenalty ? new Color(0.84f, 0.10f, 0.14f, 0.95f) : new Color(0.12f, 0.16f, 0.24f, 0.94f);
                    _pitStatusBannerTxt.text = car.fiaNotice;
                }
                else if (!inCountdown && ((signedS >= -360f && signedS <= -120f) || (signedS >= -40f && signedS <= 78f)))
                {
                    _pitStatusBannerWrap.SetActive(true);
                    bool needMandatory = _gm.selectedLaps >= 3 && !car.MandatoryCompoundSatisfied && car.lap >= 2;
                    _pitStatusBannerBg.color = needMandatory || car.tyreHealth < 48f
                        ? new Color(0.90f, 0.54f, 0.05f, 0.94f)
                        : new Color(0.10f, 0.14f, 0.20f, 0.88f);
                    string regTag = needMandatory ? "MANDATORY 2-COMPOUND RULE: " : "";
                    _pitStatusBannerTxt.text = $"{regTag}PIT LANE OPEN ON {sideWord} — STEER {sideWord} ACROSS AMBER ARROWS TO CHANGE TYRES";
                }
                else
                {
                    _pitStatusBannerWrap.SetActive(false);
                }
            }

            float rpmFrac = Mathf.Clamp01((car.rpm - 5000f) / 8200f);
            int lit = Mathf.RoundToInt(rpmFrac * 11f);
            for (int i = 0; i < 11; i++)
            {
                if (i < lit)
                    _rpmDots[i].color = i < 4 ? new Color(0.15f, 0.95f, 0.35f) : (i < 8 ? new Color(1f, 0.18f, 0.22f) : new Color(0.22f, 0.65f, 1f));
                else
                    _rpmDots[i].color = new Color(1f, 1f, 1f, 0.15f);
            }

            _hudFrameCount++;
            if (_hudFrameCount == 1 || _hudFrameCount % 6 == 0)
            {
                // Header: LAP 1/3 or YELLOW FLAG
                if (_gm.yellowFlagSector > 0)
                {
                    _towerHeaderBg.color = new Color(0.98f, 0.78f, 0.08f, 0.96f);
                    _hudLapTxt.color = Color.black;
                    _hudLapTxt.text = $"YELLOW FLAG S{_gm.yellowFlagSector}  •  LAP {car.lap}/{_gm.selectedLaps}";
                }
                else
                {
                    _towerHeaderBg.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
                    _hudLapTxt.color = Color.white;
                    _hudLapTxt.text = $"LAP {car.lap}/{_gm.selectedLaps}";
                }

                // Sector Strip
                for (int s = 0; s < 3; s++)
                {
                    bool completed = car.sectorTimes[s] > 0f;
                    bool current = (s == car.maxSectorReached);
                    _sectorBars[s].color = current
                        ? new Color(0.83f, 1.0f, 0.0f, 1f)
                        : (completed ? new Color(0.15f, 0.92f, 0.45f, 0.9f) : new Color(1f, 1f, 1f, 0.14f));
                    _sectorLabels[s].text = completed ? $"S{s + 1} {FormatSec(car.sectorTimes[s])}" : $"S{s + 1}";
                    _sectorLabels[s].color = current ? Color.white : (completed ? new Color(0.35f, 1f, 0.55f) : new Color(0.68f, 0.72f, 0.80f));
                }

                // Weather & Tyre Temp Sub-Card
                if (_weatherTempTxt != null) _weatherTempTxt.text = $"AIR 27°   TRACK 41°   DRY";
                if (_weatherTrackTxt != null) _weatherTrackTxt.text = $"{car.compound.label} {Mathf.RoundToInt(car.tyreHealth)}%  •  {Mathf.RoundToInt(car.tyreTemp)}°C";

                // Bottom-Right Tyre Rings & Car Damage Card
                float healthFrac = Mathf.Clamp01(car.tyreHealth / 100f);
                Color tyreRingCol = healthFrac > 0.55f
                    ? new Color(0.15f, 0.92f, 0.45f, 1f)
                    : (healthFrac > 0.28f ? new Color(0.98f, 0.74f, 0.12f, 1f) : new Color(0.95f, 0.16f, 0.22f, 1f));
                int healthPct = Mathf.RoundToInt(car.tyreHealth);
                for (int w = 0; w < 4; w++)
                {
                    if (_tyreRings[w] != null) { _tyreRings[w].fillAmount = healthFrac; _tyreRings[w].color = tyreRingCol; }
                    if (_tyrePcts[w] != null) _tyrePcts[w].text = $"{healthPct}%";
                    if (_carTyreRects[w] != null) _carTyreRects[w].color = tyreRingCol;
                }
                if (_cardCompoundBg != null) _cardCompoundBg.color = car.compound.color;
                if (_cardCompoundTxt != null)
                {
                    _cardCompoundTxt.text = car.compound.shortLetter;
                    _cardCompoundTxt.color = (car.compound.shortLetter == "M" || car.compound.shortLetter == "H") ? Color.black : Color.white;
                }
                if (_damageStatusTxt != null)
                {
                    if (car.timePenaltySeconds > 0f)
                    {
                        _damageStatusTxt.text = $"PENALTY +{car.timePenaltySeconds:F0}s";
                        _damageStatusTxt.color = new Color(1f, 0.25f, 0.30f, 1f);
                    }
                    else if (healthPct < 35)
                    {
                        _damageStatusTxt.text = "TYRE WEAR CRITICAL";
                        _damageStatusTxt.color = new Color(0.98f, 0.74f, 0.12f, 1f);
                    }
                    else
                    {
                        _damageStatusTxt.text = "NO DAMAGE";
                        _damageStatusTxt.color = new Color(0.75f, 0.80f, 0.86f, 1f);
                    }
                }

                // Standings & Leaderboard Rows
                var standings = _gm.GetStandings();
                int pIdx = standings.IndexOf(car);
                if (_hudPosBigTxt != null) _hudPosBigTxt.text = $"{pIdx + 1}";
                if (_hudPosTotalTxt != null) _hudPosTotalTxt.text = $"/{standings.Count}";

                int maxRows = _gm.fullLeaderboard ? Mathf.Min(_towerRows.Count, standings.Count) : Mathf.Min(5, standings.Count);
                int start = _gm.fullLeaderboard ? 0 : Mathf.Clamp(pIdx - 2, 0, Mathf.Max(0, standings.Count - maxRows));
                float leadProg = standings.Count > 0 ? standings[0].totalProgress : 0f;

                // Resize tower height dynamically when expanded
                if (_towerHeaderBg != null && _towerHeaderBg.transform.parent != null && _towerHeaderBg.transform.parent.parent != null)
                {
                    var outerRt = _towerHeaderBg.transform.parent.parent.GetComponent<RectTransform>();
                    if (outerRt != null)
                    {
                        float totalH = 38f + maxRows * 32f + 40f;
                        outerRt.offsetMin = new Vector2(24f, -20f - totalH);
                    }
                }

                Color limeHighlight = new Color(0.78f, 0.96f, 0.0f, 0.98f);
                Color darkText = new Color(0.05f, 0.06f, 0.08f, 1f);
                for (int r = 0; r < _towerRows.Count; r++)
                {
                    int sIdx = start + r;
                    bool active = r < maxRows && sIdx < standings.Count;
                    var row = _towerRows[r];
                    row.root.SetActive(active);
                    if (!active) continue;

                    var c = standings[sIdx];
                    string penTag = c.timePenaltySeconds > 0f ? $" +{c.timePenaltySeconds:F0}s" : "";
                    string gap = c.inPit ? "PIT" : (sIdx == 0 ? $"LEADER{penTag}" : $"+{Mathf.Max(0f, (leadProg - c.totalProgress) * 58f + c.timePenaltySeconds):F3}{penTag}");

                    row.bg.color = c.isPlayer ? limeHighlight : Color.clear;
                    row.posTxt.text = $"{sIdx + 1}";
                    row.posTxt.color = c.isPlayer ? darkText : new Color(0.75f, 0.78f, 0.85f);
                    row.teamBar.color = c.team.primaryColor;
                    row.codeTxt.text = c.driver.code;
                    row.codeTxt.color = c.isPlayer ? darkText : Color.white;
                    row.tyreBg.color = c.compound.color;
                    row.tyreTxt.text = c.compound.shortLetter;
                    row.tyreTxt.color = (c.compound.shortLetter == "M" || c.compound.shortLetter == "H") ? Color.black : Color.white;
                    row.gapTxt.text = gap;
                    row.gapTxt.color = c.isPlayer ? darkText : new Color(0.88f, 0.90f, 0.95f);
                }

                // AHEAD Footer
                if (_aheadBarImg != null && _aheadCodeTxt != null && _aheadGapTxt != null)
                {
                    if (pIdx > 0 && pIdx < standings.Count)
                    {
                        var aheadCar = standings[pIdx - 1];
                        float aheadGap = Mathf.Max(0f, (aheadCar.totalProgress - car.totalProgress) * 58f);
                        _aheadBarImg.color = aheadCar.team.primaryColor;
                        _aheadCodeTxt.text = aheadCar.driver.code;
                        _aheadGapTxt.text = $"{aheadGap:F3}";
                    }
                    else
                    {
                        _aheadBarImg.color = car.team.primaryColor;
                        _aheadCodeTxt.text = car.driver.code;
                        _aheadGapTxt.text = "LEADER";
                    }
                }

                DrawMinimapTexture();
            }
        }

        private void PopulateResults()
        {
            var standings = _gm.GetStandings();
            int pRank = standings.IndexOf(_gm.playerCar) + 1;
            _resTitleTxt.text = pRank == 1 ? "P1 — GRAND PRIX VICTORY!" : $"P{pRank} — RACE CLASSIFICATION";

            var sb = new StringBuilder();
            int show = Mathf.Min(12, standings.Count);
            float leadProg = standings.Count > 0 ? standings[0].totalProgress : 0f;
            for (int i = 0; i < show; i++)
            {
                var c = standings[i];
                int pts = i < MotoveloDatabase.F1Points.Length ? MotoveloDatabase.F1Points[i] : 0;
                string gap = i == 0 ? FormatTime(c.finishTime > 0 ? c.finishTime : _gm.raceTime) : $"+{(i * 1.1f + Mathf.Abs(leadProg - c.totalProgress) * 18f):F3}s";
                string hl = c.isPlayer ? "<color=#FF4858>" : "";
                string hlEnd = c.isPlayer ? "</color>" : "";
                sb.AppendLine($"{hl}P{i + 1:D2}   {c.driver.name,-20}  ({c.team.shortCode})    {gap,-12}   +{pts} PTS{hlEnd}");
            }
            _resBodyTxt.supportRichText = true;
            _resBodyTxt.text = sb.ToString();
        }

        private void DrawMinimapTexture()
        {
            var trk = _gm.activeTrack;
            if (trk == null || trk.samples == null || trk.samples.Length == 0) return;

            _minimapTex.SetPixels32(_minimapClear);

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var s in trk.samples)
            {
                if (s.pos.x < minX) minX = s.pos.x;
                if (s.pos.x > maxX) maxX = s.pos.x;
                if (s.pos.z < minZ) minZ = s.pos.z;
                if (s.pos.z > maxZ) maxZ = s.pos.z;
            }
            const int W = 128;
            const float pad = 14f;
            float scale = Mathf.Min((W - pad * 2f) / Mathf.Max(1f, maxX - minX), (W - pad * 2f) / Mathf.Max(1f, maxZ - minZ));
            float ox = (W - (maxX - minX) * scale) * 0.5f;
            float oz = (W - (maxZ - minZ) * scale) * 0.5f;

            for (int i = 0; i < trk.samples.Length; i += 2)
            {
                int px = Mathf.Clamp(Mathf.RoundToInt(ox + (trk.samples[i].pos.x - minX) * scale), 1, W - 2);
                int py = Mathf.Clamp(Mathf.RoundToInt(oz + (trk.samples[i].pos.z - minZ) * scale), 1, W - 2);
                float t = (float)i / Mathf.Max(1, trk.samples.Length);
                Color32 secCol = t < 0.333f
                    ? new Color32(45, 220, 130, 235)
                    : (t < 0.666f ? new Color32(0, 200, 255, 235) : new Color32(255, 85, 130, 235));
                PlotDot(px, py, 1, secCol);
            }

            foreach (var c in _gm.cars)
            {
                int cx = Mathf.Clamp(Mathf.RoundToInt(ox + (c.transform.position.x - minX) * scale), 2, W - 3);
                int cy = Mathf.Clamp(Mathf.RoundToInt(oz + (c.transform.position.z - minZ) * scale), 2, W - 3);
                Color32 col = c.isPlayer ? new Color32(210, 255, 0, 255) : (Color32)c.team.primaryColor;
                PlotDot(cx, cy, c.isPlayer ? 3 : 2, col);
            }

            _minimapTex.Apply(false);
        }

        private void PlotDot(int cx, int cy, int r, Color32 col)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r)
                        _minimapTex.SetPixel(cx + dx, cy + dy, col);
        }

        // =====================================================================
        // CLEAN PROCEDURAL VECTOR UI HELPERS
        // =====================================================================
        private GameObject CreateFullScreenPanel(Transform parent, string pName, Color bg)
        {
            var go = new GameObject(pName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            StretchFull(go.GetComponent<RectTransform>(), 0);
            var img = go.GetComponent<Image>();
            img.color = bg;
            img.raycastTarget = bg.a > 0.01f;
            return go;
        }

        private GameObject CreatePanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color col, bool rounded = false)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.GetComponent<Image>();
            img.color = col;
            if (rounded)
            {
                img.sprite = _roundedSprite;
                img.type = Image.Type.Sliced;
            }
            return go;
        }

        private Text CreateLabel(Transform parent, string text, int size, FontStyle style, TextAnchor align, Color col, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = col;
            t.raycastTarget = false;
            return t;
        }

        private GameObject CreateActionButton(Transform parent, string label, Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = _roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            CreateLabel(go.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return go;
        }

        private GameObject CreateCircleButton(Transform parent, string symbol, Vector2 anchor, Vector2 anchoredPos, float diameter, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"CircleBtn_{symbol}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(diameter, diameter);
            var img = go.GetComponent<Image>();
            img.sprite = _circleSprite;
            img.color = bg;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            CreateLabel(go.transform, symbol, 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return go;
        }

        private TouchHoldButton CreateHoldControl(Transform parent, string label, Vector2 anchor, Vector2 anchoredPos, Vector2 size, int fontSize, Color idleCol, bool circular)
        {
            var go = new GameObject($"Hold_{label}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = circular ? _circleSprite : _roundedSprite;
            if (!circular) img.type = Image.Type.Sliced;
            img.color = idleCol;

            var hold = go.AddComponent<TouchHoldButton>();
            hold.targetGraphic = img;
            hold.idleColor = idleCol;
            hold.pressedColor = new Color(Mathf.Min(1f, idleCol.r + 0.25f), Mathf.Min(1f, idleCol.g + 0.25f), Mathf.Min(1f, idleCol.b + 0.25f), Mathf.Min(0.85f, idleCol.a + 0.35f));

            CreateLabel(go.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.88f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return hold;
        }

        private static void StretchFull(RectTransform rt, float pad)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        private static Sprite CreateRoundedRectSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (size - 1 - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (size - 1 - radius)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = (size - 2) * 0.5f;
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float alpha = Mathf.Clamp01(r + 0.6f - dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRingSprite(int size, int thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float outerR = (size - 2) * 0.5f;
            float innerR = outerR - thickness;
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float outerAlpha = Mathf.Clamp01(outerR + 0.6f - dist);
                    float innerAlpha = Mathf.Clamp01(dist - innerR + 0.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, outerAlpha * innerAlpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static string FormatTime(float sec)
        {
            if (float.IsInfinity(sec) || float.IsNaN(sec) || sec < 0f) return "--:--.---";
            int mins = Mathf.FloorToInt(sec / 60f);
            float rem = sec - mins * 60f;
            int whole = Mathf.FloorToInt(rem);
            int ms = Mathf.FloorToInt((rem - whole) * 1000f);
            return $"{mins:D2}:{whole:D2}.{ms:D3}";
        }

        private static string FormatSec(float sec)
        {
            if (sec < 0f || float.IsInfinity(sec)) return "--.-";
            return $"{sec:F1}s";
        }
    }
}
