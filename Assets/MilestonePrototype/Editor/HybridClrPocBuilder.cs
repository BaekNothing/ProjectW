using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using HybridCLR.Editor.Settings;
using ProjectW.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Editor
{
    public static class HybridClrPocBuilder
    {
        private const string HotAssembly = "ProjectW.HotUpdate";
        private const string EntryType = "ProjectW.HotUpdate.GameEntry";
        private const string Owner = "BaekNothing";
        private const string Repository = "ProjectW";
        private const string StartScene = "Assets/MilestonePrototype/Scenes/MilestonePrototype.unity";
        private const string GameplayDataSource = "Assets/MilestonePrototype/Resources/task-system.json";
        private const string GameplayDataName = "task-system.json";
        private static readonly string[] AotMetadataAssemblies = { "mscorlib", "System", "System.Core" };

        [MenuItem("ProjectW/Hot Update/1. Configure HybridCLR")]
        public static void Configure()
        {
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            settings.enable = true;
            settings.useGlobalIl2cpp = false;
            settings.hybridclrRepoURL = "https://github.com/focus-creative-games/hybridclr";
            settings.il2cppPlusRepoURL = "https://github.com/focus-creative-games/il2cpp_plus";
            settings.hotUpdateAssemblies = new[] { HotAssembly };
            settings.patchAOTAssemblies = AotMetadataAssemblies;
            HybridCLRSettings.Save();

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Unity_4_8);
            AssetDatabase.SaveAssets();
            Debug.Log("ProjectW HybridCLR settings configured.");
        }

        [MenuItem("ProjectW/Hot Update/2. Install HybridCLR Runtime")]
        public static void InstallRuntime()
        {
            Configure();
            var installer = new InstallerController();
            if (!installer.HasInstalledHybridCLR()) installer.InstallDefaultHybridCLR();
            Debug.Log("ProjectW HybridCLR runtime installation complete.");
        }

        [MenuItem("ProjectW/Hot Update/3. Build Patch...")]
        public static void BuildPatchInteractive()
        {
            string input = EditorUtility.DisplayDialogComplex(
                "Build development patch", "Build the next patch using the version in PatchChannels/dev.json?",
                "Build", "Cancel", null) == 0 ? "1" : null;
            if (input != null) BuildPatch(GetNextPatchVersion());
        }

        public static string BuildPatch(string patchVersion)
        {
            if (string.IsNullOrWhiteSpace(patchVersion) || !Regex.IsMatch(patchVersion, @"^\d{8}-\d{3}$"))
                throw new ArgumentException("Patch version must use YYYYMMDD-NNN.", nameof(patchVersion));
            EnsureAndroidTarget();
            Configure();
            EnsureInstalled();
            CompileDllCommand.CompileDll(BuildTarget.Android, true);

            string tag = $"dev-{patchVersion}";
            string output = Path.GetFullPath(Path.Combine("PatchBuild", tag));
            RecreateDirectory(output);

            var files = new List<PatchFileRecord>();
            string hotDllSource = Path.Combine(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(BuildTarget.Android), HotAssembly + ".dll");
            AddFile(hotDllSource, output, HotAssembly + ".dll.bytes", "hotUpdateAssembly", tag, files);
            AddFile(GameplayDataSource, output, GameplayDataName, "gameplayData", tag, files);

            string metadataRoot = SettingsUtil.GetAssembliesPostIl2CppStripDir(BuildTarget.Android);
            foreach (string assembly in AotMetadataAssemblies)
            {
                string source = Path.Combine(metadataRoot, assembly + ".dll");
                if (File.Exists(source)) AddFile(source, output, assembly + ".dll.bytes", "aotMetadata", tag, files);
            }

            var manifest = new PatchManifestRecord
            {
                schemaVersion = 1,
                patchVersion = patchVersion,
                minBaseVersion = PatchBootstrapper.BaseVersion,
                entryAssembly = HotAssembly,
                entryType = EntryType,
                files = files.ToArray()
            };
            File.WriteAllText(Path.Combine(output, "patch-manifest.json"), JsonUtility.ToJson(manifest, true));
            File.WriteAllText(Path.Combine(output, "release-notes.md"),
                $"ProjectW development hot-update patch {tag}.\n\nBase APK version required: {manifest.minBaseVersion}\n");
            Debug.Log($"Patch built: {output}");
            return output;
        }

        [MenuItem("ProjectW/Hot Update/4. Build Base APK")]
        public static void BuildBaseApk()
        {
            EnsureAndroidTarget();
            Configure();
            EnsureInstalled();
            PrebuildCommand.GenerateAll();
            PrepareEmbeddedPatch();

            Directory.CreateDirectory("APK");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { StartScene },
                locationPathName = "APK/ProjectW-HybridCLR.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build failed: {report.summary.result}, errors {report.summary.totalErrors}");
            Debug.Log($"Base APK built: {Path.GetFullPath("APK/ProjectW-HybridCLR.apk")}");
        }

        public static void SetupFromCommandLine()
        {
            Configure();
            InstallRuntime();
        }

        public static void BuildBaseApkFromCommandLine() => BuildBaseApk();

        public static void BuildPatchFromCommandLine()
        {
            string version = Environment.GetEnvironmentVariable("PROJECTW_PATCH_VERSION");
            if (string.IsNullOrWhiteSpace(version))
                throw new BuildFailedException("Set PROJECTW_PATCH_VERSION using YYYYMMDD-NNN.");
            BuildPatch(version);
        }

        private static void PrepareEmbeddedPatch()
        {
            string root = Path.Combine("Assets", "StreamingAssets", "HotUpdate");
            string metadataOutput = Path.Combine(root, "AotMetadata");
            Directory.CreateDirectory(metadataOutput);

            string dllSource = Path.Combine(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(BuildTarget.Android), HotAssembly + ".dll");
            File.Copy(dllSource, Path.Combine(root, HotAssembly + ".dll.bytes"), true);

            string metadataRoot = SettingsUtil.GetAssembliesPostIl2CppStripDir(BuildTarget.Android);
            foreach (string assembly in AotMetadataAssemblies)
            {
                string source = Path.Combine(metadataRoot, assembly + ".dll");
                if (File.Exists(source)) File.Copy(source, Path.Combine(metadataOutput, assembly + ".dll.bytes"), true);
            }
            AssetDatabase.Refresh();
        }

        private static void EnsureInstalled()
        {
            if (!new InstallerController().HasInstalledHybridCLR())
                throw new BuildFailedException("Run ProjectW/Hot Update/2. Install HybridCLR Runtime first.");
        }

        private static void EnsureAndroidTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return;
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Could not switch the active build target to Android.");
        }

        private static string GetNextPatchVersion()
        {
            string json = File.ReadAllText("PatchChannels/dev.json");
            string date = DateTime.Now.ToString("yyyyMMdd");
            Match match = Regex.Match(json, @"dev-(\d{8})-(\d{3})");
            int sequence = match.Success && match.Groups[1].Value == date
                ? int.Parse(match.Groups[2].Value) + 1
                : 1;
            return $"{date}-{sequence:000}";
        }

        private static void AddFile(string source, string output, string name, string role, string tag,
            ICollection<PatchFileRecord> files)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("Patch input was not generated.", source);
            string destination = Path.Combine(output, name);
            File.Copy(source, destination, true);
            var info = new FileInfo(destination);
            files.Add(new PatchFileRecord
            {
                name = name,
                role = role,
                url = $"https://github.com/{Owner}/{Repository}/releases/download/{tag}/{name}",
                size = info.Length,
                sha256 = ComputeSha256(destination)
            });
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void RecreateDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }

        [Serializable]
        private sealed class PatchManifestRecord
        {
            public int schemaVersion;
            public string patchVersion;
            public int minBaseVersion;
            public string entryAssembly;
            public string entryType;
            public PatchFileRecord[] files;
        }

        [Serializable]
        private sealed class PatchFileRecord
        {
            public string name;
            public string role;
            public string url;
            public long size;
            public string sha256;
        }
    }
}
