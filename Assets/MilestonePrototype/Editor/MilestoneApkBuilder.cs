using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ProjectW.MilestonePrototype.Editor
{
    public static class MilestoneApkBuilder
    {
        private const string StartScene = "Assets/MilestonePrototype/Scenes/MilestonePrototype.unity";
        private const string OutputPath = "APK/ProjectW.apk";

        public static void BuildAndroid()
        {
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (enabledScenes.Length != 1 || enabledScenes[0] != StartScene)
                throw new InvalidOperationException($"Android 빌드는 새 시작 씬 하나만 허용합니다: {StartScene}");

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? "APK");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { StartScene },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Android 빌드 실패: {report.summary.result}, 오류 {report.summary.totalErrors}개");
        }
    }
}
