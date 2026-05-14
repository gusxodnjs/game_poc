using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class PocPostProcessBuild
{
    private const string DisplayName = "작은정복자들";
    private const string IconSourceDir = "Assets/AppIcon";

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        var plistPath = Path.Combine(buildPath, "Info.plist");
        if (File.Exists(plistPath))
        {
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("CFBundleDisplayName", DisplayName);
            plist.WriteToFile(plistPath);
            Debug.Log("[POC] CFBundleDisplayName=" + DisplayName);
        }

        var appiconDir = Path.Combine(buildPath, "Unity-iPhone", "Images.xcassets", "AppIcon.appiconset");
        if (Directory.Exists(appiconDir))
        {
            foreach (var size in new[] { 120, 180 })
            {
                var srcPath = Path.Combine(IconSourceDir, $"Icon-iPhone-{size}.png");
                var dstPath = Path.Combine(appiconDir, $"Icon-iPhone-{size}.png");
                if (File.Exists(srcPath))
                {
                    File.Copy(srcPath, dstPath, true);
                    Debug.Log("[POC] AppIcon copied: " + dstPath);
                }
            }
        }
    }
}
