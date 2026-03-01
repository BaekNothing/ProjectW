using System.IO;
using UnityEditor;
using UnityEditor.Compilation;

namespace ProjectW.Editor
{
    [InitializeOnLoad]
    public static class ProjectWManualRefreshBridge
    {
        private const string AutoRefreshPrefKey = "kAutoRefresh";
        private const string CodexSignalPath = "Temp/codex_refresh.signal";

        static ProjectWManualRefreshBridge()
        {
            DisableAutoRefreshByDefault();
            EditorApplication.update += PollCodexRefreshSignal;
        }

        [MenuItem("Tools/ProjectW/Refresh/Force Refresh %#r")]
        public static void ForceRefresh()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CompilationPipeline.RequestScriptCompilation();
        }

        [MenuItem("Tools/ProjectW/Refresh/Enable Auto Refresh")]
        public static void EnableAutoRefresh()
        {
            EditorPrefs.SetInt(AutoRefreshPrefKey, 1);
        }

        [MenuItem("Tools/ProjectW/Refresh/Disable Auto Refresh (Default)")]
        public static void DisableAutoRefreshByDefault()
        {
            EditorPrefs.SetInt(AutoRefreshPrefKey, 0);
        }

        private static void PollCodexRefreshSignal()
        {
            if (!File.Exists(CodexSignalPath))
            {
                return;
            }

            File.Delete(CodexSignalPath);
            ForceRefresh();
        }
    }
}
