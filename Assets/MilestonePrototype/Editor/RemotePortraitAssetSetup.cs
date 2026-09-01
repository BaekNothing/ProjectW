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

        private static readonly (string FileName, string Address)[] Assets =
        {
            ("crew-han-tech.png", CrewPortraitCatalog.HanTech),
            ("crew-yoon-analysis.png", CrewPortraitCatalog.YoonAnalysis),
            ("crew-mi-management.png", CrewPortraitCatalog.MiManagement),
            ("crew-kang-adaptation.png", CrewPortraitCatalog.KangAdaptation)
        };

        [MenuItem("ProjectW/Remote Content/3. Configure Portraits")]
        public static void ConfigureAddressables()
        {
            AssetDatabase.Refresh();
            foreach (var asset in Assets)
            {
                string path = Path.Combine(AssetRoot, asset.FileName).Replace('\\', '/');
                if (!File.Exists(path)) throw new InvalidOperationException($"Missing portrait source: {path}");
                ConfigureTexture(path);
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

            foreach (var asset in Assets)
            {
                string path = Path.Combine(AssetRoot, asset.FileName).Replace('\\', '/');
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
                entry.address = asset.Address;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }
}
