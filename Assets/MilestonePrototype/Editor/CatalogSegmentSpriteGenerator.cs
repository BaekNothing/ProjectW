using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Editor
{
    public static class CatalogSegmentSpriteGenerator
    {
        private const int Size = 600;
        private const int Samples = 4;
        private const string OutputPath =
            "Assets/MilestonePrototype/Resources/StudioUI/catalog-segment.png";

        [MenuItem("ProjectW/Studio UI/Generate Catalog Segment Sprite")]
        public static void Generate()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];
            float center = Size * .5f;
            const float innerRadius = 170f;
            const float outerRadius = 290f;
            const float edgeGapDegrees = 1.4f;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    float px = x + (sx + .5f) / Samples - center;
                    float py = y + (sy + .5f) / Samples - center;
                    float radius = Mathf.Sqrt(px * px + py * py);
                    float angle = Mathf.Atan2(py, px) * Mathf.Rad2Deg;
                    if (radius >= innerRadius + 2f && radius <= outerRadius - 2f &&
                        angle >= 60f + edgeGapDegrees && angle <= 120f - edgeGapDegrees)
                        inside++;
                }

                byte alpha = (byte)Mathf.RoundToInt(255f * inside / (Samples * Samples));
                pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? string.Empty);
            File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Generated catalog segment sprite: " + OutputPath);
        }

        public static void GenerateFromCommandLine()
        {
            Generate();
        }
    }
}
