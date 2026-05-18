using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PocBuildPipeline
{
    private const string SceneDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/HelloScene.unity";
    private const string SplashScenePath = "Assets/Scenes/SplashScene.unity";
    private const string PlanetIntroScenePath = "Assets/Scenes/PlanetIntroScene.unity";

    // 스플래시 v2: 빅뱅 12프레임 (무 → 폭발 → 응집 → 황폐 행성)
    // 사양: docs/splash_v2_bigbang.md
    // 해상도 256: PoC + 픽셀아트 톤 + iOS 업스케일 시 픽셀감 보존 + 메모리 효율 고려.
    private const string FramePathPrefix = "Assets/AppIcon/splash_anim_v2_bigbang_256_f";
    private const int FrameCount = 12;

    // 스플래시 BGM (v1, 8초). loop=false 단발 재생.
    private const string SplashBgmPath = "Assets/Audio/splash_bgm_v1.wav";

    // 행성 카드 (시나리오 v2 §2 / §8)
    private static readonly string[] PlanetCardPaths = new[]
    {
        "Assets/world/planet_card_volcano_256.png",
        "Assets/world/planet_card_ice_256.png",
        "Assets/world/planet_card_desert_256.png",
    };

    // 플레이어 아바타 (화면 중앙 고정 마커, IMGUI)
    private static readonly string[] PlayerIdlePaths = new[]
    {
        "Assets/characters/walker_front_idle_frame0_64x64.png",
        "Assets/characters/walker_front_idle_frame1_64x64.png",
        "Assets/characters/walker_front_idle_frame2_64x64.png",
        "Assets/characters/walker_front_idle_frame3_64x64.png",
    };
    private const string PlayerRingPath = "Assets/characters/player_accuracy_ring_64x64.png";
    private const string PlayerShadowPath = "Assets/characters/player_shadow_32x16.png";

    private const string BundleId = "com.gusxodnjs.terrapoc";
    private const string BuildOutput = "build/ios";

    [MenuItem("TERRA PoC/1. Setup Hello Scene")]
    public static void SetupHelloScene()
    {
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 카메라: MapView 가 SpriteRenderer 기반이므로 Orthographic 필수.
        // ortho size = 1.5 → 화면 높이 3 unit ≈ 3 타일 (zoom 17 기준 약 ±200m 가시영역)
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 1.5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x08, 0x0d, 0x1f, 0xff);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
        }

        var map = new GameObject("MapRoot");
        var mapView = map.AddComponent<MapView>();

        var gps = new GameObject("GpsRoot");
        var gpsCheck = gps.AddComponent<GpsCheck>();

        // GpsCheck.mapView SerializeField 에 MapView 참조 직접 주입.
        // 이 와이어링이 빠지면 GPS 갱신이 MapView.SetCenter 를 호출하지 못해
        // 지도가 서울시청(initialLat/Lon) 고정으로 보임.
        var so = new SerializedObject(gpsCheck);
        var prop = so.FindProperty("mapView");
        if (prop != null)
        {
            prop.objectReferenceValue = mapView;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[POC] GpsCheck.mapView SerializedProperty not found — GPS→MapView wiring skipped.");
        }

        var discovery = new GameObject("DiscoveryRoot");
        discovery.AddComponent<DiscoveryDetection>();

        // PlayerAvatar — 화면 중앙 고정 마커. IMGUI 로 ring + shadow + idle 4프레임 그리기.
        // 자산은 모두 Assets/characters/ 에 있는 PNG 를 GUI.DrawTexture 로 직접 사용.
        // (런타임에 AssetDatabase 불가하므로 SerializeField 로 주입.)
        EnsurePlayerTextureSettings();

        var player = new GameObject("PlayerRoot");
        var avatar = player.AddComponent<PlayerAvatar>();
        var idleTex = new Texture2D[PlayerIdlePaths.Length];
        int idleLoaded = 0;
        for (int i = 0; i < PlayerIdlePaths.Length; i++)
        {
            idleTex[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerIdlePaths[i]);
            if (idleTex[i] != null) idleLoaded++;
            else Debug.LogWarning("[POC] Player idle frame not loaded: " + PlayerIdlePaths[i]);
        }
        avatar.idleFrames = idleTex;
        avatar.accuracyRingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerRingPath);
        avatar.shadowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerShadowPath);
        Debug.Log("[POC] PlayerAvatar wired: idle=" + idleLoaded + "/" + PlayerIdlePaths.Length +
                  ", ring=" + (avatar.accuracyRingTex != null) +
                  ", shadow=" + (avatar.shadowTex != null));

        EditorSceneManager.SaveScene(scene, ScenePath);

        UpdateBuildScenes();
        Debug.Log("[POC] Scene saved: " + ScenePath);
    }

    [MenuItem("TERRA PoC/1b. Setup Splash Scene")]
    public static void SetupSplashScene()
    {
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x08, 0x0d, 0x1f, 0xff);
        }

        var splashRoot = new GameObject("SplashRoot");
        var splash = splashRoot.AddComponent<SplashScreen>();

        // v2 빅뱅 12프레임 로드
        var frames = new List<Texture2D>(FrameCount);
        int missing = 0;
        for (int i = 0; i < FrameCount; i++)
        {
            string framePath = $"{FramePathPrefix}{i:D2}.png";
            AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceUpdate);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(framePath);
            if (tex != null)
            {
                frames.Add(tex);
            }
            else
            {
                Debug.LogWarning("[POC] Splash frame missing: " + framePath);
                missing++;
            }
        }

        splash.frames = frames.ToArray();
        // 시나리오 v2: Splash → PlanetIntroScene
        splash.nextScene = "PlanetIntroScene";
        Debug.Log($"[POC] Splash frames loaded: {frames.Count}/{FrameCount} (missing={missing}) nextScene={splash.nextScene}");

        // BGM 주입 (null 안전 — 자산 누락 시 무음으로 진행).
        // AudioClip 으로 로드해 직렬화 — 런타임에선 AssetDatabase 비활성.
        AssetDatabase.ImportAsset(SplashBgmPath, ImportAssetOptions.ForceUpdate);
        var bgm = AssetDatabase.LoadAssetAtPath<AudioClip>(SplashBgmPath);
        if (bgm != null)
        {
            splash.bgm = bgm;
            Debug.Log("[POC] Splash BGM loaded: " + SplashBgmPath);
        }
        else
        {
            Debug.LogWarning("[POC] Splash BGM missing: " + SplashBgmPath);
        }

        EditorSceneManager.SaveScene(scene, SplashScenePath);

        UpdateBuildScenes();
        Debug.Log("[POC] Splash scene saved: " + SplashScenePath);
    }

    [MenuItem("TERRA PoC/1c. Setup Planet Intro Scene")]
    public static void SetupPlanetIntroScene()
    {
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

        // 카드 PNG 의 텍스처 import 설정을 Sprite (2D and UI) + Point filter + PPU 64 로 보장.
        EnsurePlanetCardTextureSettings();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0a, 0x10, 0x22, 0xff);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        var introRoot = new GameObject("PlanetIntroRoot");
        var intro = introRoot.AddComponent<PlanetIntroScene>();

        // 카드 Sprite 주입 — 런타임에서는 AssetDatabase 가 동작하지 않으므로
        // scene serialization 으로 직접 참조해야 빌드에 포함된다.
        // 순서: 0=Volcano, 1=Ice, 2=Desert (PlanetType enum 순)
        var sprites = new Sprite[3];
        int loaded = 0;
        for (int i = 0; i < PlanetCardPaths.Length; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlanetCardPaths[i]);
            sprites[i] = sprite;
            if (sprite != null) loaded++;
            else Debug.LogWarning("[POC] Planet card sprite not loaded: " + PlanetCardPaths[i]);
        }
        intro.cardSpritesByType = sprites;
        Debug.Log("[POC] PlanetIntroScene cards injected: " + loaded + "/" + PlanetCardPaths.Length);

        EditorSceneManager.SaveScene(scene, PlanetIntroScenePath);

        UpdateBuildScenes();
        Debug.Log("[POC] PlanetIntroScene saved: " + PlanetIntroScenePath);
    }

    /// <summary>
    /// EditorBuildSettings.scenes 를 Splash → PlanetIntro → Hello 순서로 정합.
    /// 각 setup 메뉴가 호출 시점에 일관된 순서를 보장.
    /// 존재하지 않는 씬은 건너뛴다 (1c 미실행 상태에서도 1/1b 가 동작하도록).
    /// </summary>
    private static void UpdateBuildScenes()
    {
        var ordered = new List<EditorBuildSettingsScene>();
        if (File.Exists(SplashScenePath)) ordered.Add(new EditorBuildSettingsScene(SplashScenePath, true));
        if (File.Exists(PlanetIntroScenePath)) ordered.Add(new EditorBuildSettingsScene(PlanetIntroScenePath, true));
        if (File.Exists(ScenePath)) ordered.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = ordered.ToArray();

        var names = new List<string>();
        foreach (var s in ordered) names.Add(Path.GetFileNameWithoutExtension(s.path));
        Debug.Log("[POC] EditorBuildSettings.scenes: " + string.Join(" → ", names));
    }

    /// <summary>
    /// 플레이어 아바타 PNG 텍스처 import 설정 — Default (GUI.DrawTexture 용),
    /// Point filter, no mipmap, Alpha is Transparency.
    /// IMGUI 로 GUI.DrawTexture 호출하므로 Sprite 가 아닌 Default 텍스처 타입.
    /// </summary>
    private static void EnsurePlayerTextureSettings()
    {
        var paths = new List<string>(PlayerIdlePaths);
        paths.Add(PlayerRingPath);
        paths.Add(PlayerShadowPath);

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[POC] Player asset missing: " + path);
                continue;
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
            if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; dirty = true; }
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
            if (dirty)
            {
                ti.SaveAndReimport();
                Debug.Log("[POC] Player texture import settings updated: " + path);
            }
        }
    }

    /// <summary>
    /// 카드 PNG 텍스처 import 설정 — Sprite (2D and UI), Point filter, PPU 64, Alpha is Transparency.
    /// PixelLab 보고 권장 설정 그대로.
    /// </summary>
    private static void EnsurePlanetCardTextureSettings()
    {
        foreach (var path in PlanetCardPaths)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[POC] Planet card missing: " + path);
                continue;
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
            if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; dirty = true; }
            if (ti.spritePixelsPerUnit != 64f) { ti.spritePixelsPerUnit = 64f; dirty = true; }
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
            if (dirty)
            {
                ti.SaveAndReimport();
                Debug.Log("[POC] Planet card import settings updated: " + path);
            }
        }
    }

    [MenuItem("TERRA PoC/2. Configure iOS Player Settings")]
    public static void ConfigureIOSPlayerSettings()
    {
        PlayerSettings.companyName = "TERRA";
        PlayerSettings.productName = "TerraPoC";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
        PlayerSettings.iOS.targetOSVersionString = "13.0";
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        PlayerSettings.iOS.locationUsageDescription = "산책 중 주변 종을 발견하려면 위치 정보가 필요합니다.";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        AssetDatabase.SaveAssets();
        Debug.Log("[POC] iOS Player Settings configured (bundle=" + BundleId + ")");
    }

    [MenuItem("TERRA PoC/3. Build iOS Xcode Project")]
    public static void BuildIOS()
    {
        if (!Directory.Exists(BuildOutput)) Directory.CreateDirectory(BuildOutput);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

        // EditorBuildSettings 의 순서를 그대로 따라가도록 모음.
        var scenes = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.enabled) scenes.Add(s.path);
        }
        if (scenes.Count == 0)
        {
            Debug.LogError("[POC] No scenes registered. Run TERRA PoC/1, 1b, 1c first.");
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = BuildOutput,
            target = BuildTarget.iOS,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[POC] Build result: " + report.summary.result + " | output: " + BuildOutput +
                  " | duration: " + report.summary.totalTime + " | size: " + report.summary.totalSize +
                  " | scenes: " + string.Join(",", scenes));

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            PocPostProcessBuild.OnPostProcessBuild(BuildTarget.iOS, BuildOutput);
        }
    }

    [MenuItem("TERRA PoC/9. Do All (Setup + Configure + Build)")]
    public static void DoAll()
    {
        SetupHelloScene();
        SetupSplashScene();
        SetupPlanetIntroScene();
        ConfigureIOSPlayerSettings();
        BuildIOS();
    }
}
