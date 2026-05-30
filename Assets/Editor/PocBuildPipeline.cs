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

    // 스플래시 v4: 고요 → 빅뱅 → 행성 형성 (10초, 30 PNG)
    // 사양: docs/superpowers/specs/2026-05-21-splash-v4-design.md (§3.4 자산 경로)
    // 해상도 256: PoC + 픽셀아트 톤 + iOS 업스케일 시 픽셀감 보존 + 메모리 효율 고려.
    private const string SplashV4Dir = "Assets/AppIcon/splash_v4";
    // phase 별 frame 수 (index 0..3 = phase 1..4)
    private static readonly int[] SplashV4PhaseCounts = new[] { 12, 6, 8, 4 };

    // 스플래시 BGM (v2, 10초). loop=false 단발 재생.
    private const string SplashBgmPath = "Assets/Audio/splash_bgm_v2.wav";

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

    // Earth 타일셋 (TilemapRenderer 의 grass + 5종 오토타일 시트 슬롯에 주입)
    // grass=32×32 단일, 나머지는 128×128 autotile 시트 (렌더러가 코드로 슬라이스).
    private const string TilesetDir = "Assets/world/tiles";
    private static readonly string[] TilesetPaths = {
        TilesetDir + "/grass_v0_32.png",
        TilesetDir + "/grass_v1_32.png",
        TilesetDir + "/grass_v2_32.png",
        TilesetDir + "/grass_v3_32.png",
        TilesetDir + "/path_auto_128.png",
        TilesetDir + "/road_auto_128.png",
        TilesetDir + "/water_auto_128.png",
        TilesetDir + "/forest_auto_128.png",
        TilesetDir + "/building_auto_128.png",
    };

    // Earth 오브젝트(나무/덤불/그루터기) — TilemapRenderer 의 object 슬롯에 주입. 투명·하단앵커.
    private const string ObjectsDir = "Assets/world/objects";
    private static readonly string[] ObjectPaths = {
        ObjectsDir + "/tree_pine_a_48x64.png",
        ObjectsDir + "/tree_pine_b_48x64.png",
        ObjectsDir + "/bush_32x32.png",
        ObjectsDir + "/stump_32x32.png",
    };

    private static void EnsureTilesetTextureSettings()
    {
        foreach (var path in TilesetPaths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 32;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureObjectTextureSettings()
    {
        foreach (var path in ObjectPaths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 32;
            importer.SaveAndReimport();
        }
    }

    private const string BundleId = "com.gusxodnjs.terrapoc";
    private const string BuildOutput = "build/ios";

    [MenuItem("TERRA PoC/1. Setup Hello Scene")]
    public static void SetupHelloScene()
    {
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 카메라: TilemapRenderer 가 SpriteRenderer 기반 → Orthographic.
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 4.5f; // ≈27m 가시 (Pikmin 줌)
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x82, 0xcf, 0x1c, 0xff); // grass 톤 (이음새 은폐)
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
        }

        EnsureTilesetTextureSettings();
        EnsureObjectTextureSettings();

        var map = new GameObject("MapRoot");
        var tilemap = map.AddComponent<TilemapRenderer>();
        var tmSo = new SerializedObject(tilemap);
        var gv = tmSo.FindProperty("grassVariants");
        gv.arraySize = 4;
        gv.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/grass_v0_32.png");
        gv.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/grass_v1_32.png");
        gv.GetArrayElementAtIndex(2).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/grass_v2_32.png");
        gv.GetArrayElementAtIndex(3).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/grass_v3_32.png");
        tmSo.FindProperty("pathSheet").objectReferenceValue     = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/path_auto_128.png");
        tmSo.FindProperty("roadSheet").objectReferenceValue     = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/road_auto_128.png");
        tmSo.FindProperty("waterSheet").objectReferenceValue    = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/water_auto_128.png");
        tmSo.FindProperty("forestSheet").objectReferenceValue   = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/forest_auto_128.png");
        tmSo.FindProperty("buildingSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/building_auto_128.png");
        tmSo.FindProperty("treePineA").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(ObjectsDir + "/tree_pine_a_48x64.png");
        tmSo.FindProperty("treePineB").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(ObjectsDir + "/tree_pine_b_48x64.png");
        tmSo.FindProperty("bushTex").objectReferenceValue   = AssetDatabase.LoadAssetAtPath<Texture2D>(ObjectsDir + "/bush_32x32.png");
        tmSo.FindProperty("stumpTex").objectReferenceValue  = AssetDatabase.LoadAssetAtPath<Texture2D>(ObjectsDir + "/stump_32x32.png");
        tmSo.ApplyModifiedPropertiesWithoutUndo();
        int tilesetLoaded = 0;
        foreach (var path in TilesetPaths)
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) tilesetLoaded++;
        Debug.Log("[POC] TilemapRenderer wired: tileset=" + tilesetLoaded + "/9");
        int objectsLoaded = 0;
        foreach (var path in ObjectPaths)
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) objectsLoaded++;
        Debug.Log("[POC] objects wired: " + objectsLoaded + "/4");

        var gps = new GameObject("GpsRoot");
        var gpsCheck = gps.AddComponent<GpsCheck>();
        var so = new SerializedObject(gpsCheck);
        var prop = so.FindProperty("tilemap"); // 필드명 mapView→tilemap 로 변경됨
        if (prop != null)
        {
            prop.objectReferenceValue = tilemap;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[POC] GpsCheck.tilemap SerializedProperty not found — GPS→Tilemap wiring skipped.");
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

        // v4 phase 별 frame 로드 (Assets/AppIcon/splash_v4/phase{p}_f{nn}.png)
        // - phase 1..4 = 12+6+8+4 = 30 PNG
        // - SplashScreen 의 phase{1..4}Frames 필드에 직접 주입.
        var phaseArrays = new Texture2D[4][];
        int totalLoaded = 0;
        int totalMissing = 0;
        for (int p = 0; p < SplashV4PhaseCounts.Length; p++)
        {
            int count = SplashV4PhaseCounts[p];
            var arr = new Texture2D[count];
            int loaded = 0;
            for (int f = 0; f < count; f++)
            {
                string framePath = $"{SplashV4Dir}/phase{p + 1}_f{f:D2}.png";
                AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceUpdate);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(framePath);
                if (tex != null)
                {
                    arr[f] = tex;
                    loaded++;
                }
                else
                {
                    Debug.LogWarning("[POC] Splash v4 frame missing: " + framePath);
                    totalMissing++;
                }
            }
            phaseArrays[p] = arr;
            totalLoaded += loaded;
            Debug.Log($"[POC] Splash v4 phase{p + 1}: {loaded}/{count} frames loaded");
        }

        splash.phase1Frames = phaseArrays[0];
        splash.phase2Frames = phaseArrays[1];
        splash.phase3Frames = phaseArrays[2];
        splash.phase4Frames = phaseArrays[3];
        // 시나리오 v2: Splash → PlanetIntroScene
        splash.nextScene = "PlanetIntroScene";
        Debug.Log($"[POC] Splash v4 frames total: {totalLoaded}/30 (missing={totalMissing}) nextScene={splash.nextScene}");

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

        // AVAudioSession 카테고리를 native plugin (Assets/Plugins/iOS/IOSAudioSession.mm)
        // 에서 직접 Playback 으로 설정하므로, Unity 가 다른 앱 오디오를 자동으로
        // 죽이지 않도록 false. 사용자가 음악 앱을 재생 중이어도 게임 BGM 이
        // 그 위에 깔린다 (MixWithOthers). 무음 스위치 ON 우회는 native plugin 담당.
        PlayerSettings.muteOtherAudioSources = false;

        AssetDatabase.SaveAssets();
        Debug.Log("[POC] iOS Player Settings configured (bundle=" + BundleId + ", muteOtherAudioSources=false)");
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
