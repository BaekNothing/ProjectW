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
            ("Modular/body-01-tech.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(0), false),
            ("Modular/body-02-analysis.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(1), false),
            ("Modular/body-03-management.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(2), false),
            ("Modular/body-04-adaptation.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(3), false),
            ("Modular/face-base.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(4), true),
            ("Modular/eyes-01-focused.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(5), true),
            ("Modular/eyes-02-friendly.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(6), true),
            ("Modular/eyes-03-decisive.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(7), true),
            ("Modular/eyes-04-calm.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(8), true),
            ("Modular/brow-01-straight.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(9), true),
            ("Modular/brow-02-soft-arch.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(10), true),
            ("Modular/brow-03-bold.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(11), true),
            ("Modular/brow-04-calm.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(12), true),
            ("Modular/mouth-01-neutral.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(13), true),
            ("Modular/mouth-02-smile.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(14), true),
            ("Modular/mouth-03-determined.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(15), true),
            ("Modular/mouth-04-concerned.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(16), true),
            ("Modular/hair-01-asym-bob.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(17), true),
            ("Modular/hair-02-low-bun.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(18), true),
            ("Modular/hair-03-tousled.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(19), true),
            ("Modular/hair-04-side-part.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(20), true),
            ("Modular/dark-00-none.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(21), true),
            ("Modular/dark-01-fatigue.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(22), true),
            ("Modular/dark-02-overwork.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(23), true),
            ("Modular/dark-03-illness.png", CrewPortraitCatalog.ExpectedModularAddressForAsset(24), true)
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
