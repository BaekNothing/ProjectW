using System;
using System.IO;
using System.Linq;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Editor
{
    public static class WebPreviewBuilder
    {
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new BuildFailedException("Launch Unity with -buildTarget WebGL.");
            const string scenePath = "Assets/WebPreview/WebPreview.unity";
            // Restore shared Android and Addressables configuration even on build failure.
            var paths = Directory.GetFiles("ProjectSettings", "*.asset")
                .Concat(Directory.GetFiles("Assets/AddressableAssetsData", "*.asset", SearchOption.AllDirectories));
            var backup = paths.ToDictionary(path => path, File.ReadAllBytes);
            try
            {
                HybridCLRSettings.Instance.enable = false;
                HybridCLRSettings.Save();
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.template = "APPLICATION:Default";
                PlayerSettings.defaultWebScreenWidth = 1280;
                PlayerSettings.defaultWebScreenHeight = 720;
                PlayerSettings.runInBackground = true;
                PlayerSettings.productName = "Managing is T.R.U.L.Y fun XD";

                var settings = AddressableAssetSettingsDefaultObject.Settings;
                settings.BuildRemoteCatalog = false;
                foreach (var group in settings.groups.Where(group => group != null))
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    if (schema == null) continue;
                    schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                    schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                    EditorUtility.SetDirty(schema);
                }
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult content);
                if (!string.IsNullOrEmpty(content.Error)) throw new BuildFailedException(content.Error);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
                camera.orthographic = true;
                var bootstrap = new GameObject("Web Preview").AddComponent<ProjectW.WebPreview.WebPreviewBootstrap>();
                bootstrap.UiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/WebPreview/Fonts/NotoSansCJKkr-Regular.otf");
                if (bootstrap.UiFont == null) throw new BuildFailedException("Missing Korean UI font.");
                EditorSceneManager.SaveScene(scene, scenePath);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = "Builds/WebGL",
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"WebGL build failed: {report.summary.result}");
                File.Copy("Assets/WebPreview/Fonts/OFL.txt", "Builds/WebGL/OFL.txt", true);
                Debug.Log($"WEB_PREVIEW_BUILD_OK bytes={report.summary.totalSize}");
            }
            finally
            {
                foreach (var pair in backup) File.WriteAllBytes(pair.Key, pair.Value);
                AssetDatabase.Refresh();
            }
        }
    }
}
