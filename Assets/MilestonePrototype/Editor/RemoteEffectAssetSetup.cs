using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Editor
{
    public static class RemoteEffectAssetSetup
    {
        public const string AssetRoot = "Assets/MilestonePrototype/RemoteAssets/Effects";
        public const string GroupName = "ProjectW Remote Effects";
        public const string RadialAddress = "effects/radial-trapezoid";
        public const string SparkleAddress = "effects/sparkle";
        public const string RingAddress = "effects/ring";

        private static readonly (string FileName, string Address, Func<int, int, Color> Pixel)[] Assets =
        {
            ("radial-trapezoid.png", RadialAddress, RadialPixel),
            ("sparkle.png", SparkleAddress, SparklePixel),
            ("ring.png", RingAddress, RingPixel)
        };

        [MenuItem("ProjectW/Remote Content/1. Generate Effect Sources")]
        public static void GenerateEffectSources()
        {
            Directory.CreateDirectory(AssetRoot);
            foreach (var asset in Assets) WritePng(Path.Combine(AssetRoot, asset.FileName), 256, asset.Pixel);
            AssetDatabase.Refresh();
            foreach (var asset in Assets) ConfigureTexture(Path.Combine(AssetRoot, asset.FileName));
            AssetDatabase.SaveAssets();
        }

        [MenuItem("ProjectW/Remote Content/2. Configure Addressables")]
        public static void ConfigureAddressables()
        {
            GenerateEffectSources();
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

        public static void ConfigureFromCommandLine() => ConfigureAddressables();

        private static void WritePng(string path, int size, Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) pixels[y * size + x] = pixel(x, y);
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static Color RadialPixel(int x, int y)
        {
            const float size = 256f;
            float nx = (x + .5f - size * .5f) / (size * .5f);
            float ny = (y + .5f - size * .5f) / (size * .5f);
            float radius = Mathf.Sqrt(nx * nx + ny * ny);
            if (radius < .2f || radius > .96f) return Color.clear;
            float t = Mathf.InverseLerp(.2f, .96f, radius);
            float segment = Mathf.PI * 2f / 16f;
            float angle = Mathf.Repeat(Mathf.Atan2(ny, nx) + segment * .5f, segment) - segment * .5f;
            float halfAngle = Mathf.Lerp(.025f, .115f, t);
            float edge = 1f - Mathf.SmoothStep(.78f, 1f, Mathf.Abs(angle) / halfAngle);
            float outerFade = 1f - Mathf.SmoothStep(.48f, 1f, t);
            float innerFade = Mathf.SmoothStep(0f, .12f, t);
            return new Color(1f, 1f, 1f, edge * outerFade * innerFade);
        }

        private static Color SparklePixel(int x, int y)
        {
            float nx = Mathf.Abs((x - 127.5f) / 127.5f);
            float ny = Mathf.Abs((y - 127.5f) / 127.5f);
            float cross = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Min(nx * 5f + ny, ny * 5f + nx)), 2f);
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny) * 4f);
            return new Color(1f, 1f, 1f, Mathf.Max(cross, core));
        }

        private static Color RingPixel(int x, int y)
        {
            float nx = (x - 127.5f) / 127.5f;
            float ny = (y - 127.5f) / 127.5f;
            float radius = Mathf.Sqrt(nx * nx + ny * ny);
            float alpha = Mathf.Clamp01(1f - Mathf.Abs(radius - .76f) * 32f);
            return new Color(1f, 1f, 1f, alpha);
        }

        private static void ConfigureTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
}
