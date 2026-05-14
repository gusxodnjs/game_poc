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
    private const string IconAssetPath = "Assets/AppIcon/icon_planet_v5_master_1024.png";
    private const string BundleId = "com.gusxodnjs.terrapoc";
    private const string BuildOutput = "build/ios";

    [MenuItem("TERRA PoC/1. Setup Hello Scene")]
    public static void SetupHelloScene()
    {
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = new GameObject("HelloRoot");
        root.AddComponent<HelloWorld>();
        var gps = new GameObject("GpsRoot");
        gps.AddComponent<GpsCheck>();
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
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

        AssetDatabase.ImportAsset(IconAssetPath, ImportAssetOptions.ForceUpdate);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
        if (tex != null)
        {
            splash.backgroundIcon = tex;
        }
        else
        {
            Debug.LogWarning("[POC] Splash icon not loaded: " + IconAssetPath);
        }

        EditorSceneManager.SaveScene(scene, SplashScenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SplashScenePath, true),
            new EditorBuildSettingsScene(ScenePath, true),
        };
        Debug.Log("[POC] Splash scene saved: " + SplashScenePath);
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
        AssetDatabase.SaveAssets();
        Debug.Log("[POC] iOS Player Settings configured (bundle=" + BundleId + ")");
    }

    [MenuItem("TERRA PoC/3. Build iOS Xcode Project")]
    public static void BuildIOS()
    {
        if (!Directory.Exists(BuildOutput)) Directory.CreateDirectory(BuildOutput);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { SplashScenePath, ScenePath },
            locationPathName = BuildOutput,
            target = BuildTarget.iOS,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[POC] Build result: " + report.summary.result + " | output: " + BuildOutput +
                  " | duration: " + report.summary.totalTime + " | size: " + report.summary.totalSize);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            PocPostProcessBuild.OnPostProcessBuild(BuildTarget.iOS, BuildOutput);
        }
    }

    public static void DoAll()
    {
        SetupHelloScene();
        SetupSplashScene();
        ConfigureIOSPlayerSettings();
        BuildIOS();
    }
}
