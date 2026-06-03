#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Uses the project TraceVirtualOffset override (no Sampling.hlsl) and clears harmless
/// package-shader conversion warnings that Unity still compiles from immutable URP assets.
/// </summary>
[InitializeOnLoad]
static class ProbeVolumeTraceVirtualOffsetFix
{
    const string ProjectShaderPath = "Assets/Settings/Rendering/TraceVirtualOffset.urtshader";
    const string PackageShaderPath =
        "Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/VirtualOffset/TraceVirtualOffset.urtshader";
    const string GlobalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings.asset";

    static ProbeVolumeTraceVirtualOffsetFix()
    {
        EditorApplication.delayCall += Apply;
    }

    static void Apply()
    {
        AssignProjectShaderToGlobalSettings();
        ClearShaderMessages(ProjectShaderPath);
        ClearShaderMessages(PackageShaderPath);
    }

    static void AssignProjectShaderToGlobalSettings()
    {
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(ProjectShaderPath);
        if (subAssets == null || subAssets.Length == 0)
            return;

        var compute = subAssets.OfType<ComputeShader>().FirstOrDefault();
        var rayTracing = subAssets.FirstOrDefault(a => a != null && a.GetType().Name == "RayTracingShader");

        if (compute == null && rayTracing == null)
            return;

        var globalSettings = AssetDatabase.LoadMainAssetAtPath(GlobalSettingsPath);
        if (globalSettings == null)
            return;

        var serialized = new SerializedObject(globalSettings);
        var iterator = serialized.GetIterator();
        var enterChildren = true;
        var updated = false;

        while (iterator.Next(enterChildren))
        {
            enterChildren = true;
            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            if (iterator.name == "traceVirtualOffsetCS" && compute != null &&
                iterator.objectReferenceValue != compute)
            {
                iterator.objectReferenceValue = compute;
                updated = true;
            }
            else if (iterator.name == "traceVirtualOffsetRT" && rayTracing != null &&
                     iterator.objectReferenceValue != rayTracing)
            {
                iterator.objectReferenceValue = rayTracing;
                updated = true;
            }
        }

        if (updated)
            serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ClearShaderMessages(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            return;

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset == null)
                continue;

            if (asset is Shader shader)
            {
                ShaderUtil.ClearShaderMessages(shader);
                continue;
            }

            ClearMessagesViaReflection(asset, "ClearShaderMessages");
            ClearMessagesViaReflection(asset, "ClearComputeShaderMessages");
        }
    }

    static void ClearMessagesViaReflection(UnityEngine.Object asset, string methodName)
    {
        var method = typeof(ShaderUtil).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
            null,
            new[] { asset.GetType() },
            null);

        method?.Invoke(null, new object[] { asset });
    }
}
#endif
