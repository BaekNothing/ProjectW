using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectW.MilestonePrototype.Editor
{
    public static class StudioUiPanelSetup
    {
        private const string ThemePath =
            "Assets/MilestonePrototype/Resources/StudioUI/UnityDefaultRuntimeTheme.tss";
        private const string SettingsPath =
            "Assets/MilestonePrototype/Resources/StudioUI/ProjectWPanelSettings.asset";

        [MenuItem("ProjectW/StudioUI/Rebuild Panel Settings")]
        public static void Rebuild()
        {
            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) throw new System.InvalidOperationException("StudioUI runtime theme is missing.");

            PanelSettings existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(SettingsPath);
            if (existing != null) AssetDatabase.DeleteAsset(SettingsPath);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "ProjectW Runtime Panel";
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = .5f;
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("StudioUI PanelSettings rebuilt.");
        }
    }
}
