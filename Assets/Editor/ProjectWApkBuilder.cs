using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ProjectW.Editor
{
public static class ProjectWApkBuilder
{
    private const string ApkFolder = "APK";
    private const string AndroidPackageName = "com.baeknothing.projectw";
    private const int MaxKeptBuilds = 3;

    [MenuItem("Tools/ProjectW/Build/Android APK")]
    public static void BuildApk()
    {
        Directory.CreateDirectory(ApkFolder);

        var productName = SanitizeFileName(PlayerSettings.productName);
        var buildNumber = NextBuildNumber(productName);
        var date = DateTime.Now.ToString("yyyyMMdd");
        var apkPath = Path.Combine(ApkFolder, $"{productName}_{date}_{buildNumber}.apk").Replace("\\", "/");
        var scenes = ExistingBuildScenes();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No buildable scenes found. Add a valid scene to EditorBuildSettings or restore Assets/Scenes/MVP Scene.unity.");
        }

        // Keep Android identity/build-number setup here so manual and batch builds use the same APK naming rule.
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageName);
        PlayerSettings.Android.bundleVersionCode = buildNumber;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

        var targetGroup = BuildTargetGroup.Android;
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, BuildTarget.Android);
        }

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = targetGroup,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"APK build failed: {report.summary.result} ({report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings)");
        }

        PruneOldApks(productName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string[] ExistingBuildScenes()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && File.Exists(scene.path))
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length > 0)
        {
            return scenes;
        }

        return File.Exists("Assets/Scenes/MVP Scene.unity")
            ? new[] { "Assets/Scenes/MVP Scene.unity" }
            : Array.Empty<string>();
    }

    private static int NextBuildNumber(string productName)
    {
        var fromSettings = Math.Max(1, PlayerSettings.Android.bundleVersionCode);
        var fromFiles = Directory.Exists(ApkFolder)
            ? Directory.GetFiles(ApkFolder, $"{productName}_*.apk")
                .Select(Path.GetFileNameWithoutExtension)
                .Select(ParseBuildNumber)
                .DefaultIfEmpty(0)
                .Max()
            : 0;

        return Math.Max(fromSettings, fromFiles) + 1;
    }

    private static int ParseBuildNumber(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return 0;
        }

        var lastUnderscore = fileName.LastIndexOf('_');
        return lastUnderscore >= 0 && int.TryParse(fileName[(lastUnderscore + 1)..], out var buildNumber)
            ? buildNumber
            : 0;
    }

    private static void PruneOldApks(string productName)
    {
        var apks = Directory.GetFiles(ApkFolder, $"{productName}_*.apk")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        foreach (var oldBuild in apks.Skip(MaxKeptBuilds))
        {
            oldBuild.Delete();
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((string.IsNullOrWhiteSpace(value) ? "ProjectW" : value)
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "ProjectW" : cleaned;
    }
}
}
