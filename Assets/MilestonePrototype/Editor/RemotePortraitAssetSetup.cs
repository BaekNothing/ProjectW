using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Editor
{
    public static class RemotePortraitAssetSetup
    {
        public const string AssetRoot = "Assets/MilestonePrototype/RemoteAssets/Portraits/Crew";
        public const string GroupName = "ProjectW Remote Portraits";

        private static readonly (string FileName, string Address, bool Alpha)[] Assets =
        {
            ("crew-han-tech.png", CrewPortraitCatalog.HanTech, false),
            ("crew-yoon-analysis.png", CrewPortraitCatalog.YoonAnalysis, false),
            ("crew-mi-management.png", CrewPortraitCatalog.MiManagement, false),
            ("crew-kang-adaptation.png", CrewPortraitCatalog.KangAdaptation, false),
            ("Conditions/crew-han-tech-condition-0.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(0), false),
            ("Conditions/crew-yoon-analysis-condition-0.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(1), false),
            ("Conditions/crew-mi-management-condition-0.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(2), false),
            ("Conditions/crew-kang-adaptation-condition-0.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(3), false),
            ("Conditions/crew-han-tech-condition-1.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(4), false),
            ("Conditions/crew-yoon-analysis-condition-1.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(5), false),
            ("Conditions/crew-mi-management-condition-1.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(6), false),
            ("Conditions/crew-kang-adaptation-condition-1.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(7), false),
            ("Conditions/crew-han-tech-condition-2.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(8), false),
            ("Conditions/crew-yoon-analysis-condition-2.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(9), false),
            ("Conditions/crew-mi-management-condition-2.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(10), false),
            ("Conditions/crew-kang-adaptation-condition-2.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(11), false),
            ("Conditions/crew-han-tech-condition-3.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(12), false),
            ("Conditions/crew-yoon-analysis-condition-3.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(13), false),
            ("Conditions/crew-mi-management-condition-3.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(14), false),
            ("Conditions/crew-kang-adaptation-condition-3.png", CrewPortraitCatalog.ExpectedConditionAddressForAsset(15), false)
        };

        [MenuItem("ProjectW/Remote Content/3. Configure Portraits")]
        public static void ConfigureAddressables()
        {
            AssetDatabase.Refresh();
            foreach (var asset in Assets)
            {
                string path = Path.Combine(AssetRoot, asset.FileName).Replace('\\', '/');
                if (!File.Exists(path)) throw new InvalidOperationException($"Missing portrait source: {path}");
                ConfigureTexture(path, asset.Alpha);
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            AddressableAssetGroup group = settings.FindGroup(GroupName) ?? settings.CreateGroup(
                GroupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            BundledAssetGroupSchema bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundle.UseAssetBundleCache = true;
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;

            // Historical modular files stay in source control, but no longer ship in this group.
            var stale = new System.Collections.Generic.List<AddressableAssetEntry>();
            foreach (var entry in group.entries)
                if (entry.address.StartsWith("portraits/crew/modular/", StringComparison.Ordinal))
                    stale.Add(entry);
            foreach (var entry in stale) settings.RemoveAssetEntry(entry.guid);

            foreach (var asset in Assets)
            {
                string path = Path.Combine(AssetRoot, asset.FileName).Replace('\\', '/');
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
                entry.address = asset.Address;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureTexture(string path, bool alpha)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alpha;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }
}
