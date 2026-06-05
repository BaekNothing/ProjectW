using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ProjectW.IngameCore.CaseReview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectW.Editor
{
[CustomEditor(typeof(ScenarioDataWorkshop))]
public sealed class ScenarioDataWorkshopEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Scenario Data Factory", EditorStyles.boldLabel);

        var workshop = (ScenarioDataWorkshop)target;
        if (GUILayout.Button("Generate / Refresh Sample Scenario Data"))
        {
            ScenarioDataWorkshopGenerator.GenerateSamples(workshop.OutputFolder);
        }

        if (GUILayout.Button("Create Blank Scenario Event"))
        {
            ScenarioDataWorkshopGenerator.CreateBlank<ScenarioEventDefinition>(workshop.OutputFolder, "Events", "ScenarioEvent");
        }

        if (GUILayout.Button("Create Blank Localized Text Table"))
        {
            ScenarioDataWorkshopGenerator.CreateBlank<LocalizedTextTable>(workshop.OutputFolder, "Text", "LocalizedTextTable");
        }

        using (new EditorGUI.DisabledScope(workshop.SelectedTextTable == null))
        {
            if (GUILayout.Button("Export Selected Text Table CSV"))
            {
                LocalizedTextTableCsvEditorUtility.ExportWithPanel(workshop.SelectedTextTable);
            }

            if (GUILayout.Button("Import CSV Into Selected Text Table"))
            {
                LocalizedTextTableCsvEditorUtility.ImportWithPanel(workshop.SelectedTextTable);
            }
        }

        if (GUILayout.Button("Export All Text Tables CSV"))
        {
            ScenarioDataWorkshopGenerator.ExportAllTextTablesCsv(workshop.OutputFolder);
        }

        if (GUILayout.Button("Create Blank Render Resources"))
        {
            ScenarioDataWorkshopGenerator.CreateBlank<RenderResourceDefinition>(workshop.OutputFolder, "Render", "ScenarioRenderResources");
        }

        if (GUILayout.Button("Ping Output Folder"))
        {
            ScenarioDataWorkshopGenerator.PingFolder(workshop.OutputFolder);
        }
    }
}

public static class ScenarioDataWorkshopMenu
{
    private const string WorkshopScenePath = "Assets/Scenes/ScenarioDataWorkshop.unity";
    private const string DefaultOutputFolder = "Assets/Resources/CaseReviewData/Scenarios";

    [MenuItem("Tools/ProjectW/Case Review/Open Scenario Data Workshop Scene")]
    public static void OpenWorkshopScene()
    {
        ScenarioDataWorkshopGenerator.EnsureWorkshopScene(WorkshopScenePath);
        EditorSceneManager.OpenScene(WorkshopScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/ProjectW/Case Review/Create or Refresh Sample Scenario Data")]
    public static void GenerateSamples()
    {
        ScenarioDataWorkshopGenerator.GenerateSamples(DefaultOutputFolder);
    }

    [MenuItem("Tools/ProjectW/Case Review/Export Scenario Text CSV")]
    public static void ExportScenarioTextCsv()
    {
        ScenarioDataWorkshopGenerator.ExportAllTextTablesCsv(DefaultOutputFolder);
    }
}

public static class ScenarioDataWorkshopGenerator
{
    public static void EnsureWorkshopScene(string scenePath)
    {
        EnsureFolder(Path.GetDirectoryName(scenePath)?.Replace("\\", "/") ?? "Assets/Scenes");

        if (File.Exists(scenePath))
        {
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = Path.GetFileNameWithoutExtension(scenePath);

        var workshop = new GameObject("Scenario Data Workshop");
        workshop.AddComponent<ScenarioDataWorkshop>();

        var cameraObject = new GameObject("Workshop Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
        camera.transform.position = new Vector3(0f, 0f, -8f);

        var lightObject = new GameObject("Workshop Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.75f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -25f, 0f);

        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
    }

    public static void GenerateSamples(string root)
    {
        EnsureSampleFolders(root);

        var render = CreateOrLoad<RenderResourceDefinition>($"{root}/Render/RR_TeaAudit.asset");
        SetField(render, "resourceId", "render.scenario.tea-audit");
        SetField(render, "displayLabel", "Tea Audit Scenario");
        SetField(render, "accentColor", new Color(0.44f, 0.70f, 0.86f));
        SetField(render, "uiVariant", "scenario-blue");

        var text = CreateOrLoad<LocalizedTextTable>($"{root}/Text/Text_TeaAudit.asset");
        SetField(text, "tableId", "text.scenario.tea-audit");
        SetField(text, "defaultLanguageKey", "ko");
        SetField(text, "defaultCountryCode", "KR");
        SetField(text, "entries", new List<LocalizedTextEntry>
        {
            Entry("scenario.tea-audit.line.001", ("ko", "KR", "\uac10\uc0ac\ud300\uc774 \ubcf5\ub3c4 \ub05d\uc5d0\uc11c \ub2f9\uc2e0\uc744 \uae30\ub2e4\ub9ac\uace0 \uc788\ub2e4."), ("en", "US", "The audit team is waiting at the end of the corridor.")),
            Entry("scenario.tea-audit.line.002", ("ko", "KR", "\ucc28\ub098 \ud55c\uc794 \ud558\uc2dc\uc8e0. \uacf5\uc2dd \uae30\ub85d\uc5d0\ub294 \ub0a8\uae30\uc9c0 \uc54a\uaca0\uc2b5\ub2c8\ub2e4."), ("en", "US", "Let's have tea. I will keep it off the formal record.")),
            Entry("scenario.tea-audit.choice.listen", ("ko", "KR", "\ub05d\uae4c\uc9c0 \ub4e3\ub294\ub2e4"), ("en", "US", "Listen to the end")),
            Entry("scenario.tea-audit.choice.alert", ("ko", "KR", "\uc0ac\uc7a5\uc5d0\uac8c \uba3c\uc800 \uc54c\ub9b0\ub2e4"), ("en", "US", "Alert the boss first"))
        });

        var scenario = CreateOrLoad<ScenarioEventDefinition>($"{root}/Events/Scenario_TeaAudit.asset");
        SetField(scenario, "eventId", "scenario.tea-audit");
        SetField(scenario, "playbackStateKey", "scenario.tea-audit");
        SetField(scenario, "timing", ScenarioTiming.Night);
        SetField(scenario, "priority", 10);
        SetField(scenario, "triggerMode", ScenarioTriggerMode.Both);
        SetField(scenario, "allowedExplicitLocations", new List<ScenarioExplicitLocation>
        {
            ScenarioExplicitLocation.AuditBriefing
        });
        SetField(scenario, "renderResources", render);
        SetField(scenario, "textTable", text);
        SetField(scenario, "triggerConditions", new List<ScenarioCondition>
        {
            new ScenarioCondition
            {
                Key = ScenarioConditionKey.Tag,
                SubjectId = "audit",
                Value = "audit",
                Comparison = ScenarioComparison.Exists
            }
        });
        SetField(scenario, "entryCosts", new List<ScenarioStateEffect>
        {
            new ScenarioStateEffect { Key = ScenarioEffectKey.FocusCost, Delta = -1 }
        });
        SetField(scenario, "lines", new List<ScenarioScriptLine>
        {
            new ScenarioScriptLine
            {
                LineId = "L001",
                Kind = ScenarioLineKind.Narration,
                TextKey = "scenario.tea-audit.line.001",
                CenterImage = render,
                StageCommands = new List<ScenarioStageCommand>
                {
                    new ScenarioStageCommand { CommandType = ScenarioStageCommandType.ShowCenterImage, RenderResources = render, DurationSec = 0.2f },
                    new ScenarioStageCommand { CommandType = ScenarioStageCommandType.Shake, TargetId = "screen", Intensity = 0.15f, DurationSec = 0.25f }
                }
            },
            new ScenarioScriptLine
            {
                LineId = "L002",
                Kind = ScenarioLineKind.Dialogue,
                SpeakerId = "P-quiet-auditor",
                TextKey = "scenario.tea-audit.line.002",
                ExpressionKey = "tired",
                PoseKey = "lean",
                StageCommands = new List<ScenarioStageCommand>
                {
                    new ScenarioStageCommand { CommandType = ScenarioStageCommandType.AddSpeaker, TargetId = "P-quiet-auditor", Value = "left" },
                    new ScenarioStageCommand { CommandType = ScenarioStageCommandType.FocusSpeaker, TargetId = "P-quiet-auditor", Intensity = 1f },
                    new ScenarioStageCommand { CommandType = ScenarioStageCommandType.DimOthers, TargetId = "others", Intensity = 0.6f }
                },
                Choices = new List<ScenarioChoice>
                {
                    new ScenarioChoice
                    {
                        ChoiceId = "listen",
                        LabelTextKey = "scenario.tea-audit.choice.listen",
                        Costs = new List<ScenarioStateEffect>
                        {
                            new ScenarioStateEffect { Key = ScenarioEffectKey.FocusCost, Delta = -2 }
                        },
                        Effects = new List<ScenarioStateEffect>
                        {
                            new ScenarioStateEffect { Key = ScenarioEffectKey.RelationshipDelta, SubjectId = "P-quiet-auditor", Delta = 5 }
                        }
                    },
                    new ScenarioChoice
                    {
                        ChoiceId = "alert",
                        LabelTextKey = "scenario.tea-audit.choice.alert",
                        Costs = new List<ScenarioStateEffect>
                        {
                            new ScenarioStateEffect { Key = ScenarioEffectKey.TrustDelta, Delta = -1 }
                        },
                        Effects = new List<ScenarioStateEffect>
                        {
                            new ScenarioStateEffect { Key = ScenarioEffectKey.AlertFlag, SubjectId = "boss", Value = "audit-alert" },
                            new ScenarioStateEffect { Key = ScenarioEffectKey.AuditCandidate, SubjectId = "scenario.tea-audit", Value = "alerted-before-review" }
                        }
                    }
                }
            }
        });
        SetField(scenario, "exitEffects", new List<ScenarioStateEffect>
        {
            new ScenarioStateEffect { Key = ScenarioEffectKey.AddTag, Value = "scenario-seen:tea-audit" }
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PingFolder(root);
    }

    public static void ExportAllTextTablesCsv(string root)
    {
        var assetFolder = $"{root}/Text";
        var csvAssetFolder = $"{root}/TextCsv";
        EnsureFolder(assetFolder);
        EnsureFolder(csvAssetFolder);

        var csvFolder = Path.Combine(Directory.GetCurrentDirectory(), csvAssetFolder);
        LocalizedTextTableCsvEditorUtility.ExportAll(assetFolder, csvFolder);

        AssetDatabase.Refresh();
        PingFolder(csvAssetFolder);
    }

    public static T CreateBlank<T>(string root, string subfolder, string prefix) where T : ScriptableObject
    {
        EnsureFolder($"{root}/{subfolder}");
        var path = AssetDatabase.GenerateUniqueAssetPath($"{root}/{subfolder}/{prefix}.asset");
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        return asset;
    }

    public static void PingFolder(string folder)
    {
        EnsureFolder(folder);
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static void EnsureSampleFolders(string root)
    {
        EnsureFolder(root);
        EnsureFolder($"{root}/Events");
        EnsureFolder($"{root}/Text");
        EnsureFolder($"{root}/Render");
    }

    private static void EnsureFolder(string folder)
    {
        var normalized = folder.Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        var parts = normalized.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static LocalizedTextEntry Entry(string key, params (string language, string country, string text)[] values)
    {
        var entry = new LocalizedTextEntry { Key = key };
        foreach (var value in values)
        {
            entry.Values.Add(new LocalizedTextValue
            {
                LanguageKey = value.language,
                CountryCode = value.country,
                Text = value.text
            });
        }

        return entry;
    }

    private static void SetField<T>(UnityEngine.Object asset, string fieldName, T value)
    {
        var field = asset.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
        {
            throw new InvalidOperationException($"Missing field '{fieldName}' on {asset.GetType().Name}.");
        }

        field.SetValue(asset, value);
        EditorUtility.SetDirty(asset);
    }
}
}
