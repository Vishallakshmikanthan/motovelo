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

        // HUD UI refs
        private Text _towerHeaderTxt;
        private Image _towerHeaderBg;
        private Text _hudLapTxt;
        private Text _towerBodyTxt;
        private RawImage _minimapImg;
        private Texture2D _minimapTex;
        private Color32[] _minimapClear;
        private readonly Image[] _startLightLenses = new Image[5];
        private GameObject _startLightsWrap;
        private GameObject _flBannerWrap;
        private Text _flBannerTxt;
        private Text _hudSpeedTxt;
        private Text _hudGearTxt;
        private Text _hudTimeTxt;
        private Text _hudBestTxt;
        private Text _hudSectorsTxt;
        private Image _ersBarFill;
        private Image _drsBadgeBg;
        private Image _pitBtnBg;
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
        // 6. CLEAN MONOPOSTO BROADCAST HUD & EFFORTLESS TOUCH CONTROLS
        // =====================================================================
        private void BuildHudPanel(Transform root)
        {
            _panelHud = CreateFullScreenPanel(root, "PanelHud", Color.clear);

            // Top-Left F1 Leaderboard Tower
            var tower = CreatePanel(_panelHud.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -460), new Vector2(290, -20), new Color(0.04f, 0.05f, 0.08f, 0.84f), true);
            var tHdr = CreatePanel(tower.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -34), Vector2.zero, new Color(0.82f, 0.06f, 0.11f, 0.96f));
            _towerHeaderBg = tHdr.GetComponent<Image>();
            _towerHeaderTxt = CreateLabel(tHdr.transform, "MOTOVELO GP", 16, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _hudLapTxt = CreateLabel(tower.transform, "LAP 1/3", 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.94f, 0.98f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -62), new Vector2(-10, -36));
            _towerBodyTxt = CreateLabel(tower.transform, "", 16, FontStyle.Bold, TextAnchor.UpperLeft, Color.white, new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 8), new Vector2(-10, -66));
            _towerBodyTxt.lineSpacing = 1.18f;

            // Top-Left Circular Track Radar Minimap
            var mapWrap = CreatePanel(_panelHud.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(306, -212), new Vector2(498, -20), new Color(0.06f, 0.08f, 0.12f, 0.55f), true);
            mapWrap.GetComponent<Image>().sprite = _circleSprite;
            var mapGo = new GameObject("MinimapRaw", typeof(RectTransform), typeof(RawImage));
            mapGo.transform.SetParent(mapWrap.transform, false);
            StretchFull(mapGo.GetComponent<RectTransform>(), 10);
            _minimapImg = mapGo.GetComponent<RawImage>();
            _minimapTex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            _minimapClear = new Color32[128 * 128];
            _minimapImg.texture = _minimapTex;

            // Top-Center 5 Red Start Lights
            _startLightsWrap = CreatePanel(_panelHud.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-245, -180), new Vector2(245, -76), new Color(0.05f, 0.06f, 0.08f, 0.92f), true);
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 94f;
                var pod = CreatePanel(_startLightsWrap.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x - 34, -34), new Vector2(x + 34, 34), new Color(0.18f, 0.03f, 0.04f, 1f));
                var img = pod.GetComponent<Image>();
                img.sprite = _circleSprite;
                _startLightLenses[i] = img;
            }

            // Fastest Lap Purple Banner
            _flBannerWrap = CreatePanel(_panelHud.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-230, -84), new Vector2(230, -20), new Color(0.54f, 0.08f, 0.78f, 0.94f), true);
            _flBannerTxt = CreateLabel(_flBannerWrap.transform, "FASTEST LAP", 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));

            // Top-Right Action Pills (TOWER, CAM, PAUSE — crisp clean text badges, zero emojis!)
            CreateActionButton(_panelHud.transform, "TOWER", new Vector2(1, 1), new Vector2(-340, -46), new Vector2(108, 46), new Color(0.08f, 0.10f, 0.14f, 0.85f), () => { _gm.fullLeaderboard = !_gm.fullLeaderboard; _gm.audioSynth?.TriggerClick(); });
            CreateActionButton(_panelHud.transform, "CAM",   new Vector2(1, 1), new Vector2(-218, -46), new Vector2(108, 46), new Color(0.08f, 0.10f, 0.14f, 0.85f), () => { _gm.cameraRig.CycleCamera(); _gm.audioSynth?.TriggerClick(); });
            CreateActionButton(_panelHud.transform, "II",    new Vector2(1, 1), new Vector2(-96,  -46), new Vector2(88,  46), new Color(0.08f, 0.10f, 0.14f, 0.85f), () => _gm.SetScreenState(MotoveloScreenState.Paused));

            // Top-Right Monoposto Telemetry Cluster
            var cluster = CreatePanel(_panelHud.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-396, -304), new Vector2(-24, -84), new Color(0.05f, 0.06f, 0.09f, 0.84f), true);

            // 11 RPM Shift Dots
            for (int i = 0; i < 11; i++)
            {
                float dx = -140f + i * 28f;
                var dot = CreatePanel(cluster.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(dx - 8, -28), new Vector2(dx + 8, -12), new Color(1, 1, 1, 0.16f));
                dot.GetComponent<Image>().sprite = _circleSprite;
                _rpmDots[i] = dot.GetComponent<Image>();
            }

            _hudGearTxt = CreateLabel(cluster.transform, "N", 62, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.85f), new Vector2(-50, 0), new Vector2(50, 0));
            _hudSpeedTxt = CreateLabel(cluster.transform, "0 KPH", 26, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.35f, 1f, 0.55f), new Vector2(0, 0.44f), new Vector2(0.45f, 0.82f), new Vector2(16, 0), Vector2.zero);

            var drsPill = CreatePanel(cluster.transform, new Vector2(0.58f, 0.50f), new Vector2(0.95f, 0.76f), Vector2.zero, Vector2.zero, new Color(0.16f, 0.18f, 0.22f, 0.9f), true);
            _drsBadgeBg = drsPill.GetComponent<Image>();
            CreateLabel(drsPill.transform, "DRS", 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _hudTimeTxt = CreateLabel(cluster.transform, "LAP  00:00.000", 16, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 0.22f), new Vector2(1, 0.42f), new Vector2(16, 0), new Vector2(-16, 0));
            _hudBestTxt = CreateLabel(cluster.transform, "BEST --:--.---", 15, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.35f, 1f, 0.55f), new Vector2(0, 0.22f), new Vector2(1, 0.42f), new Vector2(16, 0), new Vector2(-16, 0));
            _hudSectorsTxt = CreateLabel(cluster.transform, "S1 --.-   S2 --.-   S3 --.-", 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.75f, 0.80f, 0.88f), new Vector2(0, 0.08f), new Vector2(1, 0.24f), Vector2.zero, Vector2.zero);

            var ersTrack = CreatePanel(cluster.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 8), new Vector2(-16, 16), new Color(1, 1, 1, 0.15f));
            var ersFill = CreatePanel(ersTrack.transform, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, new Color(0.15f, 0.86f, 1f, 1f));
            _ersBarFill = ersFill.GetComponent<Image>();
            _ersBarFill.type = Image.Type.Filled;
            _ersBarFill.fillMethod = Image.FillMethod.Horizontal;

            // Ergonomic Translucent Bottom Touch Controls
            _btnLeft  = CreateHoldControl(_panelHud.transform, "<",     new Vector2(0, 0), new Vector2(118, 118), new Vector2(146, 146), 54, new Color(1, 1, 1, 0.18f), true);
            _btnRight = CreateHoldControl(_panelHud.transform, ">",     new Vector2(0, 0), new Vector2(292, 118), new Vector2(146, 146), 54, new Color(1, 1, 1, 0.18f), true);

            _btnBrake = CreateHoldControl(_panelHud.transform, "BRAKE", new Vector2(1, 0), new Vector2(-304, 112), new Vector2(158, 128), 20, new Color(0.90f, 0.16f, 0.20f, 0.28f), false);
            _btnGas   = CreateHoldControl(_panelHud.transform, "GAS",   new Vector2(1, 0), new Vector2(-118, 134), new Vector2(148, 174), 22, new Color(0.18f, 0.90f, 0.42f, 0.28f), false);

            _btnErs   = CreateHoldControl(_panelHud.transform, "ERS",   new Vector2(0.5f, 0), new Vector2(-180, 96), new Vector2(96, 96), 20, new Color(0.18f, 0.55f, 1.0f, 0.45f), true);
            _btnDrs   = CreateHoldControl(_panelHud.transform, "DRS",   new Vector2(0.5f, 0), new Vector2(180,  96), new Vector2(96, 96), 20, new Color(0.16f, 0.88f, 0.40f, 0.40f), true);
            _btnRear  = CreateHoldControl(_panelHud.transform, "REAR",  new Vector2(0.5f, 0), new Vector2(-60,  86), new Vector2(82, 82), 16, new Color(1f, 1f, 1f, 0.20f), true);

            var pitGo = CreateActionButton(_panelHud.transform, "PIT", new Vector2(0.5f, 0), new Vector2(60, 86), new Vector2(82, 82), new Color(1f, 1f, 1f, 0.20f), () => _gm.RequestPitStop());
            _pitBtnBg = pitGo.GetComponent<Image>();
            _pitBtnBg.sprite = _circleSprite;
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
            _startLightsWrap.SetActive(inCountdown);
            if (inCountdown)
            {
                for (int i = 0; i < 5; i++)
                    _startLightLenses[i].color = i < _gm.litStartPods
                        ? new Color(1f, 0.10f, 0.14f, 1f)
                        : new Color(0.18f, 0.03f, 0.04f, 1f);
            }

            _flBannerWrap.SetActive(_gm.fastestLapBannerTimer > 0f);
            if (_gm.fastestLapBannerTimer > 0f)
                _flBannerTxt.text = $"FASTEST LAP  •  {_gm.fastestLapDriver}  {FormatTime(_gm.fastestLapTime)}";

            int kph = Mathf.RoundToInt(car.speed * 3.6f);
            _hudSpeedTxt.text = $"{kph} KPH";
            _hudGearTxt.text = (inCountdown || car.speed < 0.4f) ? "N" : car.gear.ToString();
            _hudLapTxt.text = $"LAP {car.lap}/{_gm.selectedLaps}   •   {car.compound.shortLetter} {Mathf.RoundToInt(car.tyreHealth)}%";
            _hudTimeTxt.text = $"LAP  {FormatTime(_gm.raceTime - car.lapStartTime)}";
            _hudBestTxt.text = $"BEST {FormatTime(car.bestLapTime)}";

            _ersBarFill.fillAmount = car.ersCharge;
            _drsBadgeBg.color = car.drsActive ? new Color(0.12f, 0.88f, 0.34f, 1f) : new Color(0.16f, 0.18f, 0.22f, 0.9f);
            _pitBtnBg.color = car.pitRequested ? new Color(0.98f, 0.78f, 0.12f, 0.85f) : new Color(1f, 1f, 1f, 0.20f);

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
            if (_hudFrameCount % 6 == 0)
            {
                if (_gm.yellowFlagSector > 0)
                {
                    _towerHeaderBg.color = new Color(0.98f, 0.78f, 0.08f, 0.96f);
                    _towerHeaderTxt.color = Color.black;
                    _towerHeaderTxt.text = $"YELLOW FLAG  S{_gm.yellowFlagSector}";
                }
                else
                {
                    _towerHeaderBg.color = new Color(0.82f, 0.06f, 0.11f, 0.96f);
                    _towerHeaderTxt.color = Color.white;
                    _towerHeaderTxt.text = "MOTOVELO GP";
                }

                _hudSectorsTxt.text = $"S1 {FormatSec(car.sectorTimes[0])}   S2 {FormatSec(car.sectorTimes[1])}   S3 {FormatSec(car.sectorTimes[2])}";

                var standings = _gm.GetStandings();
                int pIdx = standings.IndexOf(car);
                int maxRows = _gm.fullLeaderboard ? Mathf.Min(14, standings.Count) : Mathf.Min(5, standings.Count);
                int start = _gm.fullLeaderboard ? 0 : Mathf.Clamp(pIdx - 2, 0, Mathf.Max(0, standings.Count - maxRows));
                float leadProg = standings.Count > 0 ? standings[0].totalProgress : 0f;

                var sb = new StringBuilder();
                for (int i = start; i < Mathf.Min(standings.Count, start + maxRows); i++)
                {
                    var c = standings[i];
                    string gap = c.inPit ? "PIT" : (i == 0 ? "LEADER" : $"+{Mathf.Max(0f, (leadProg - c.totalProgress) * 58f):F2}s");
                    string prefix = c.isPlayer ? "<color=#38FF6A>> " : "  ";
                    string suffix = c.isPlayer ? "</color>" : "";
                    sb.AppendLine($"{prefix}{i + 1:D2}  {c.driver.code}  [{c.compound.shortLetter}]  {gap}{suffix}");
                }
                _towerBodyTxt.supportRichText = true;
                _towerBodyTxt.text = sb.ToString();

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

            var roadCol = new Color32(200, 210, 225, 220);
            for (int i = 0; i < trk.samples.Length; i += 2)
            {
                int px = Mathf.Clamp(Mathf.RoundToInt(ox + (trk.samples[i].pos.x - minX) * scale), 1, W - 2);
                int py = Mathf.Clamp(Mathf.RoundToInt(oz + (trk.samples[i].pos.z - minZ) * scale), 1, W - 2);
                PlotDot(px, py, 1, roadCol);
            }

            foreach (var c in _gm.cars)
            {
                int cx = Mathf.Clamp(Mathf.RoundToInt(ox + (c.transform.position.x - minX) * scale), 2, W - 3);
                int cy = Mathf.Clamp(Mathf.RoundToInt(oz + (c.transform.position.z - minZ) * scale), 2, W - 3);
                Color32 col = c.isPlayer ? new Color32(40, 255, 90, 255) : (Color32)c.team.primaryColor;
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
