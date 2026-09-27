using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ProjectW.MilestonePrototype.Editor
{
    // Every WebGL entry point (including Build Settings) passes this gate.
    public sealed class SheetContentBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string executable = Environment.GetEnvironmentVariable("PROJECTW_PYTHON");
            if (string.IsNullOrWhiteSpace(executable)) executable = "python";
            var start = new ProcessStartInfo(executable, "-X utf8 \"" + Path.Combine(root, "tools/sheet_content.py") + "\"")
            {
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(150000)) { process.Kill(); throw new BuildFailedException("Google Sheets import timed out."); }
                if (process.ExitCode != 0) throw new BuildFailedException("Google Sheets import failed: " + stderr.Result);
                UnityEngine.Debug.Log(stdout.Result);
            }
            AssetDatabase.ImportAsset("Assets/MilestonePrototype/Resources/sheet-content.json", ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
