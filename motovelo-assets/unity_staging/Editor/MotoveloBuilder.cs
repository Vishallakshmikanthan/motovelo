using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Motovelo.Editor
{
    public static class MotoveloBuilder
    {
        [MenuItem("Motovelo/1. Configure Assets & Scene Baseline")]
        public static void SetupSceneAndVisualBaseline()
        {
            ConfigureImportedAssets();
            CreateShaderPreservingMaterialTemplates();
            ConfigurePlayerAndBuildSettings();
            BuildMainScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MotoveloBuilder] Scene, URP Visual Baseline, Material Templates, and PlayerSettings configured cleanly.");
        }

        private static void ConfigureImportedAssets()
        {
            // 1. Models: Bake axis conversion so Blender FBX (+Z forward, +Y up) imports cleanly without child -90deg rotations
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Resources/Models" });
            foreach (string guid in modelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                bool dirty = false;
                if (!importer.bakeAxisConversion) { importer.bakeAxisConversion = true; dirty = true; }
                if (!importer.isReadable) { importer.isReadable = true; dirty = true; }
                if (importer.importCameras) { importer.importCameras = false; dirty = true; }
                if (importer.importLights) { importer.importLights = false; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }

            // 2. Textures: Crisp liveries (aniso 8, 2048px) and clean alpha transparency on logos, tyres, portraits
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Textures" });
            foreach (string guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool isLivery = path.Contains("/liveries/");
                bool isUiTex = path.Contains("/logos/") || path.Contains("/drivers/") || path.Contains("/tracks/");
                bool dirty = false;

                if (!importer.alphaIsTransparency && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    importer.alphaIsTransparency = true;
                    dirty = true;
                }

                bool wantMips = !isUiTex;
                if (importer.mipmapEnabled != wantMips)
                {
                    importer.mipmapEnabled = wantMips;
                    dirty = true;
                }

                int wantAniso = isLivery ? 8 : (wantMips ? 4 : 1);
                if (importer.anisoLevel != wantAniso)
                {
                    importer.anisoLevel = wantAniso;
                    dirty = true;
                }

                int maxSz = isLivery ? 2048 : 1024;
                if (importer.maxTextureSize != maxSz)
                {
                    importer.maxTextureSize = maxSz;
                    dirty = true;
                }

                if (dirty) importer.SaveAndReimport();
            }
        }

        private static void CreateShaderPreservingMaterialTemplates()
        {
            const string dir = "Assets/Resources/Materials";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Resources", "Materials");

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[MotoveloBuilder] Could not find Universal Render Pipeline/Lit shader!");
                return;
            }

            // Reference texture so _BaseMap variant is also preserved in Android IL2CPP builds
            Texture2D sampleTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/liveries/redbull.png");

            // 1. Opaque Lit Template
            SaveOrUpdateMaterial($"{dir}/UrpOpaqueTemplate.mat", urpLit, mat =>
            {
                mat.SetColor("_BaseColor", Color.white);
                if (sampleTex != null) mat.SetTexture("_BaseMap", sampleTex);
                mat.SetFloat("_Metallic", 0.25f);
                mat.SetFloat("_Smoothness", 0.78f);
                mat.SetFloat("_Surface", 0f);
            });

            // 2. AlphaClip Lit Template (for Pirelli Tyre Sidewalls)
            SaveOrUpdateMaterial($"{dir}/UrpAlphaClipTemplate.mat", urpLit, mat =>
            {
                mat.SetColor("_BaseColor", Color.white);
                if (sampleTex != null) mat.SetTexture("_BaseMap", sampleTex);
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.2f);
                mat.EnableKeyword("_ALPHATEST_ON");
            });

            // 3. Transparent Lit Template (for Glossy Monoposto Showroom Mirror Floor)
            SaveOrUpdateMaterial($"{dir}/UrpTransparentTemplate.mat", urpLit, mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.86f, 0.88f, 0.91f, 0.82f));
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            });

            // 4. Emissive Lit Template (for FIA Rain Light & Start Gantry Pods)
            SaveOrUpdateMaterial($"{dir}/UrpEmissiveTemplate.mat", urpLit, mat =>
            {
                mat.SetColor("_BaseColor", new Color(1f, 0.08f, 0.08f, 1f));
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(2.8f, 0.12f, 0.12f, 1f));
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });

            // 5. Procedural Atmospheric Skybox Template (prevents shader stripping on WebGL & Android!)
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                SaveOrUpdateMaterial($"{dir}/MotoveloSkybox.mat", skyShader, mat =>
                {
                    if (mat.HasProperty("_SunDisk")) mat.SetInt("_SunDisk", 2);
                    if (mat.HasProperty("_SunSize")) mat.SetFloat("_SunSize", 0.036f);
                    if (mat.HasProperty("_SunSizeConvergence")) mat.SetFloat("_SunSizeConvergence", 6f);
                    if (mat.HasProperty("_AtmosphereThickness")) mat.SetFloat("_AtmosphereThickness", 0.92f);
                    if (mat.HasProperty("_SkyTint")) mat.SetColor("_SkyTint", new Color(0.32f, 0.54f, 0.86f, 1f));
                    if (mat.HasProperty("_GroundColor")) mat.SetColor("_GroundColor", new Color(0.36f, 0.38f, 0.42f, 1f));
                    if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1.15f);
                });
            }
        }

        private static void SaveOrUpdateMaterial(string assetPath, Shader shader, Action<Material> configure)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader);
                configure(mat);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                mat.shader = shader;
                configure(mat);
                EditorUtility.SetDirty(mat);
            }
        }

        private static void ConfigurePlayerAndBuildSettings()
        {
            PlayerSettings.companyName = "Motovelo";
            PlayerSettings.productName = "MOTOVELO";
            PlayerSettings.bundleVersion = "2.1.0";

            // Orientation: Landscape Auto-Rotation (Left & Right) for mobile racing
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Android Specific Settings — Match Monoposto.apk (UnityPlayerActivity + OpenGLES3 + No Stripping!)
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.motovelo.racing");
            PlayerSettings.Android.bundleVersionCode = 21;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.resizableWindow = false;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Force universal OpenGL ES 3.0 on Android (prevents Unity 6 Vulkan + GameActivity + NativeRenderPass blank screen on Adreno/Mali)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // Disable engine & aggressive managed stripping so all runtime URP, UI, and InputSystem components remain intact
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);

            // Set Active Input Handling = Both (2) and disable Android predictive back / GameActivity quirks
            var psObj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (psObj != null && psObj.Length > 0)
            {
                var so = new SerializedObject(psObj[0]);
                var propInput = so.FindProperty("activeInputHandler");
                if (propInput != null) propInput.intValue = 2;
                var propEntry = so.FindProperty("androidApplicationEntry");
                if (propEntry != null) propEntry.intValue = 1; // 1 = Activity (UnityPlayerActivity)
                var propPredBack = so.FindProperty("androidPredictiveBackSupport");
                if (propPredBack != null) propPredBack.intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Unify Mobile & PC URP Pipeline Assets so Android uses the exact verified URP pipeline without NativeRenderPass stalls
            var pcRp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            var mobRp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            var mobRend = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/Mobile_Renderer.asset");
            if (mobRend != null)
            {
                var rso = new SerializedObject(mobRend);
                var nrp = rso.FindProperty("m_UseNativeRenderPass");
                if (nrp != null) { nrp.intValue = 0; rso.ApplyModifiedPropertiesWithoutUndo(); }
                EditorUtility.SetDirty(mobRend);
            }
            if (pcRp != null)
            {
                pcRp.renderScale = 1.0f;
                pcRp.msaaSampleCount = 4; // Crisp 4x MSAA anti-aliasing!
                pcRp.shadowDistance = 145f; // Long crisp shadows down F1 straights!
                EditorUtility.SetDirty(pcRp);
                GraphicsSettings.defaultRenderPipeline = pcRp;

                int qCount = QualitySettings.names.Length;
                int origQ = QualitySettings.GetQualityLevel();
                for (int q = 0; q < qCount; q++)
                {
                    QualitySettings.SetQualityLevel(q, false);
                    QualitySettings.renderPipeline = pcRp;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                }
                QualitySettings.SetQualityLevel(origQ, false);
            }
            if (mobRp != null)
            {
                mobRp.renderScale = 1.0f;
                mobRp.msaaSampleCount = 2;
                mobRp.shadowDistance = 120f;
                EditorUtility.SetDirty(mobRp);
            }
        }

        private static void BuildMainScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Trilight Ambient & Subtle Atmospheric Fog (Visual Baseline)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            ColorUtility.TryParseHtmlString("#D4DFEE", out Color skyAmb);
            ColorUtility.TryParseHtmlString("#BCC5D3", out Color eqAmb);
            ColorUtility.TryParseHtmlString("#6F7785", out Color gndAmb);
            RenderSettings.ambientSkyColor = skyAmb;
            RenderSettings.ambientEquatorColor = eqAmb;
            RenderSettings.ambientGroundColor = gndAmb;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0011f;
            ColorUtility.TryParseHtmlString("#D5DFEC", out Color fogCol);
            RenderSettings.fogColor = fogCol;

            // 2. Warm Key Directional Sun Light + Soft Rim Fill
            var sunGo = new GameObject("Directional Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            ColorUtility.TryParseHtmlString("#FFF2DC", out Color sunCol);
            sun.color = sunCol;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sunGo.transform.rotation = Quaternion.Euler(40f, -36f, 0f);

            var rimGo = new GameObject("Studio Rim Fill");
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            ColorUtility.TryParseHtmlString("#DCE8FF", out Color rimCol);
            rim.color = rimCol;
            rim.intensity = 0.42f;
            rim.shadows = LightShadows.None;
            rimGo.transform.rotation = Quaternion.Euler(28f, 142f, 0f);

            // 3. Main Camera with URP Post-Processing & FXAA
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            ColorUtility.TryParseHtmlString("#D6DEE8", out Color camBg);
            cam.backgroundColor = camBg;
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 950f;
            cam.fieldOfView = 42f;
            camGo.transform.position = new Vector3(6.2f, 1.18f, -0.6f);
            camGo.transform.LookAt(new Vector3(0f, 0.44f, 0f));

            var camData = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // 4. Global URP Post-Processing Volume (ACES Tonemapping + Subtle Bloom + Vignette + Crisp Contrast)
            const string profilePath = "Assets/Settings/MotoveloPostProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.components.Clear();

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.overrideState = true;
            tonemap.mode.value = TonemappingMode.ACES;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.0f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.32f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.65f;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.18f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.45f;

            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.overrideState = true;
            colorAdj.postExposure.value = 0.06f;
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 9f;
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = 8f;

            EditorUtility.SetDirty(profile);

            var volGo = new GameObject("Global Post Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            vol.sharedProfile = profile;

            // 5. Motovelo Game Manager Root with Serialized Prefab & Subsystem References
            var gmGo = new GameObject("MotoveloGameManager");
            var gm = gmGo.AddComponent<MotoveloGameManager>();
            gm.mainCamera = cam;
            gm.directionalSun = sun;
            gm.carChassisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/formula_car.fbx");
            gm.wheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/formula_wheel.fbx");
            gm.driverStandingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/driver_standing.fbx");
            gm.grandstandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/grandstand.fbx");
            gm.pitBuildingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/pit_building.fbx");
            gm.startGantryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/start_gantry.fbx");

            var rig = gmGo.AddComponent<MonopostoCameraRig>();
            rig.targetCamera = cam;
            gm.cameraRig = rig;

            var audGo = new GameObject("ProceduralV6Audio");
            audGo.transform.SetParent(gmGo.transform, false);
            var audSrc = audGo.AddComponent<AudioSource>();
            audSrc.playOnAwake = true;
            audSrc.spatialBlend = 0f;
            gm.audioSynth = audGo.AddComponent<ProceduralV6Audio>();

            const string scenePath = "Assets/Scenes/Main.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
        }

        [MenuItem("Motovelo/2. Build Android APK")]
        public static void BuildAndroidApk()
        {
            SetupSceneAndVisualBaseline();

            string outApk = @"c:\Users\Lenovo\Downloads\motovelo\MOTOVELO.apk";
            string outDir = Path.GetDirectoryName(outApk);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outApk,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.Log("[MotoveloBuilder] Starting Android IL2CPP ARM64 APK build -> " + outApk);
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[MotoveloBuilder] APK BUILD SUCCEEDED! Size: {summary.totalSize / (1024 * 1024)} MB -> {outApk}");
            }
            else
            {
                throw new Exception($"[MotoveloBuilder] APK BUILD FAILED: {summary.result} (Errors: {summary.totalErrors})");
            }
        }

        [MenuItem("Motovelo/3. Build Standalone Windows Player Smoke Test")]
        public static void BuildWindowsSmokeTest()
        {
            SetupSceneAndVisualBaseline();
            string outExe = @"c:\Users\Lenovo\Downloads\motovelo\MotoveloUnity\Builds\WinSmoke\MOTOVELO.exe";
            string outDir = Path.GetDirectoryName(outExe);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows smoke test build failed: " + report.summary.result);
        }

        [MenuItem("Motovelo/4. Build WebGL (Browser)")]
        public static void BuildWebGL()
        {
            SetupSceneAndVisualBaseline();

            // WebGL browser-friendly settings (Decompression Fallback ensures instant play on any static/local server)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);

            string outDir = @"c:\Users\Lenovo\Downloads\motovelo\MOTOVELO-WebGL";
            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            Debug.Log("[MotoveloBuilder] Starting WebGL browser build -> " + outDir);
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[MotoveloBuilder] WEBGL BUILD SUCCEEDED! Size: {summary.totalSize / (1024 * 1024)} MB -> {outDir}");
            }
            else
            {
                throw new Exception($"[MotoveloBuilder] WEBGL BUILD FAILED: {summary.result} (Errors: {summary.totalErrors})");
            }
        }

        [MenuItem("Motovelo/5. Build Both Android APK + WebGL")]
        public static void BuildAndroidAndWebGL()
        {
            BuildAndroidApk();
            BuildWebGL();
        }
    }
}
