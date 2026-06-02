using System;
using System.Collections.Generic;
using System.IO;
using ProjectW.IngameCore.CaseReview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectW.Editor
{
[CustomEditor(typeof(CharacterDataWorkshop))]
public sealed class CharacterDataWorkshopEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Character Data Factory", EditorStyles.boldLabel);

        var workshop = (CharacterDataWorkshop)target;
        if (GUILayout.Button("Generate / Refresh Sample Data"))
        {
            CharacterDataWorkshopGenerator.GenerateSamples(workshop.OutputFolder);
        }

        if (GUILayout.Button("Create Blank Character Base"))
        {
            CharacterDataWorkshopGenerator.CreateBlank<CharacterBaseDefinition>(workshop.OutputFolder, "Characters", "CharacterBase");
        }

        if (GUILayout.Button("Create Blank Character Runtime"))
        {
            CharacterDataWorkshopGenerator.CreateBlank<CharacterRuntimeData>(workshop.OutputFolder, "Characters", "CharacterRuntimeData");
        }

        if (GUILayout.Button("Create Blank Card"))
        {
            CharacterDataWorkshopGenerator.CreateBlank<ActionCardDefinition>(workshop.OutputFolder, "Cards", "ActionCard");
        }

        if (GUILayout.Button("Create Blank Perk"))
        {
            CharacterDataWorkshopGenerator.CreateBlank<PerkDefinition>(workshop.OutputFolder, "Perks", "Perk");
        }

        if (GUILayout.Button("Create Blank Render Resources"))
        {
            CharacterDataWorkshopGenerator.CreateBlank<RenderResourceDefinition>(workshop.OutputFolder, "Render", "RenderResources");
        }

        if (GUILayout.Button("Ping Output Folder"))
        {
            CharacterDataWorkshopGenerator.PingFolder(workshop.OutputFolder);
        }
    }
}

public static class CharacterDataWorkshopMenu
{
    private const string WorkshopScenePath = "Assets/Scenes/CharacterDataWorkshop.unity";

    [MenuItem("Tools/ProjectW/Case Review/Open Character Data Workshop Scene")]
    public static void OpenWorkshopScene()
    {
        CharacterDataWorkshopGenerator.EnsureWorkshopScene(WorkshopScenePath);
        EditorSceneManager.OpenScene(WorkshopScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/ProjectW/Case Review/Create or Refresh Sample Data")]
    public static void GenerateSamples()
    {
        CharacterDataWorkshopGenerator.GenerateSamples("Assets/Resources/CaseReviewData/Samples");
    }
}

public static class CharacterDataWorkshopGenerator
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

        var workshop = new GameObject("Character Data Workshop");
        workshop.AddComponent<CharacterDataWorkshop>();

        var cameraObject = new GameObject("Workshop Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
        camera.transform.position = new Vector3(0f, 0f, -8f);

        var lightObject = new GameObject("Workshop Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.8f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
    }

    public static void GenerateSamples(string root)
    {
        EnsureSampleFolders(root);

        var cautiousRender = CreateOrLoad<RenderResourceDefinition>($"{root}/Render/RR_CautiousPlanner.asset");
        Set(cautiousRender, "resourceId", "render.cautious-planner");
        Set(cautiousRender, "displayLabel", "Cautious Planner");
        Set(cautiousRender, "accentColor", new Color(0.28f, 0.55f, 0.88f));
        Set(cautiousRender, "uiVariant", "paper-blue");

        var shortcutRender = CreateOrLoad<RenderResourceDefinition>($"{root}/Render/RR_ShortcutOperator.asset");
        Set(shortcutRender, "resourceId", "render.shortcut-operator");
        Set(shortcutRender, "displayLabel", "Shortcut Operator");
        Set(shortcutRender, "accentColor", new Color(0.87f, 0.47f, 0.22f));
        Set(shortcutRender, "uiVariant", "warning-orange");

        var quietRender = CreateOrLoad<RenderResourceDefinition>($"{root}/Render/RR_QuietAuditor.asset");
        Set(quietRender, "resourceId", "render.quiet-auditor");
        Set(quietRender, "displayLabel", "Quiet Auditor");
        Set(quietRender, "accentColor", new Color(0.38f, 0.72f, 0.58f));
        Set(quietRender, "uiVariant", "ledger-green");

        var overdocument = CreateCard(
            $"{root}/Cards/Card_Overdocument.asset",
            "card.overdocument",
            "Overdocument",
            cautiousRender,
            "Writes a careful plan with extra review checkpoints.",
            "Slows the queue, but leaves fewer invisible traps.",
            AffinityScope.Working,
            new[] { "paper", "review", "procedure" },
            outcome: 8,
            risk: -6,
            reviewCost: 3);

        var shortcutPatch = CreateCard(
            $"{root}/Cards/Card_ShortcutPatch.asset",
            "card.shortcut-patch",
            "Shortcut Patch",
            shortcutRender,
            "Applies a fast local workaround before all signatures arrive.",
            "Looks efficient unless someone reads the missing approval chain.",
            AffinityScope.Surface,
            new[] { "repair", "shortcut", "risk" },
            outcome: 12,
            risk: 9,
            reviewCost: -1);

        var silentAudit = CreateCard(
            $"{root}/Cards/Card_SilentAudit.asset",
            "card.silent-audit",
            "Silent Audit",
            quietRender,
            "Checks one suspicious record without advertising the check.",
            "Can expose a false calm before the daily report hardens.",
            AffinityScope.Trusted,
            new[] { "audit", "records", "mismatch" },
            outcome: 5,
            risk: -10,
            reviewCost: 4);

        var damageControl = CreateCard(
            $"{root}/Cards/Card_DamageControl.asset",
            "card.damage-control",
            "Damage Control",
            cautiousRender,
            "Turns an ugly incident into a tolerable report draft.",
            "The report reads better than the day felt.",
            AffinityScope.Working,
            new[] { "report", "manager", "spin" },
            outcome: 6,
            risk: 2,
            reviewCost: 2);

        var proceduralist = CreatePerk(
            $"{root}/Perks/Perk_ProcedureLoyalist.asset",
            "perk.procedure-loyalist",
            "Procedure Loyalist",
            cautiousRender,
            new[] { "procedure", "review", "paper" },
            outcome: 4,
            physical: 0,
            mental: -2);

        var improviser = CreatePerk(
            $"{root}/Perks/Perk_PanicImproviser.asset",
            "perk.panic-improviser",
            "Panic Improviser",
            shortcutRender,
            new[] { "repair", "emergency", "shortcut" },
            outcome: 7,
            physical: 2,
            mental: 4);

        var auditor = CreatePerk(
            $"{root}/Perks/Perk_PatternAuditor.asset",
            "perk.pattern-auditor",
            "Pattern Auditor",
            quietRender,
            new[] { "audit", "records", "mismatch" },
            outcome: 5,
            physical: 0,
            mental: 1);

        var planner = CreateCharacterBase(
            $"{root}/Characters/Base_CautiousPlanner.asset",
            "CL-S01",
            "Cautious Planner",
            "LINE-SAFE",
            cautiousRender,
            new[] { overdocument, damageControl },
            new[] { proceduralist },
            observation: 7,
            dexterity: 3,
            boldness: 2,
            intuition: 5,
            logic: 8);

        var operatorClone = CreateCharacterBase(
            $"{root}/Characters/Base_ShortcutOperator.asset",
            "CL-F02",
            "Shortcut Operator",
            "LINE-FAST",
            shortcutRender,
            new[] { shortcutPatch, damageControl },
            new[] { improviser },
            observation: 4,
            dexterity: 8,
            boldness: 7,
            intuition: 5,
            logic: 4);

        var quietAuditor = CreateCharacterBase(
            $"{root}/Characters/Base_QuietAuditor.asset",
            "CL-A03",
            "Quiet Auditor",
            "LINE-AUDIT",
            quietRender,
            new[] { silentAudit, overdocument },
            new[] { auditor },
            observation: 9,
            dexterity: 3,
            boldness: 3,
            intuition: 7,
            logic: 7);

        CreateRuntime($"{root}/Characters/Runtime_CautiousPlanner.asset", planner, "P-S01", new[] { overdocument, damageControl }, new[] { proceduralist });
        CreateRuntime($"{root}/Characters/Runtime_ShortcutOperator.asset", operatorClone, "P-F02", new[] { shortcutPatch, damageControl }, new[] { improviser });
        CreateRuntime($"{root}/Characters/Runtime_QuietAuditor.asset", quietAuditor, "P-A03", new[] { silentAudit, overdocument }, new[] { auditor });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PingFolder(root);
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
        EnsureFolder($"{root}/Render");
        EnsureFolder($"{root}/Cards");
        EnsureFolder($"{root}/Perks");
        EnsureFolder($"{root}/Characters");
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

    private static ActionCardDefinition CreateCard(
        string path,
        string id,
        string title,
        RenderResourceDefinition render,
        string visibleSummary,
        string hiddenIntent,
        AffinityScope scope,
        IReadOnlyList<string> tags,
        int outcome,
        int risk,
        int reviewCost)
    {
        var card = CreateOrLoad<ActionCardDefinition>(path);
        Set(card, "cardId", id);
        Set(card, "title", title);
        Set(card, "renderResources", render);
        Set(card, "visibleSummary", visibleSummary);
        Set(card, "hiddenIntent", hiddenIntent);
        Set(card, "requiredScope", scope);
        Set(card, "tags", tags);
        Set(card, "outcomeModifier", outcome);
        Set(card, "riskModifier", risk);
        Set(card, "reviewCostModifier", reviewCost);
        return card;
    }

    private static PerkDefinition CreatePerk(
        string path,
        string id,
        string title,
        RenderResourceDefinition render,
        IReadOnlyList<string> triggerTags,
        int outcome,
        int physical,
        int mental)
    {
        var perk = CreateOrLoad<PerkDefinition>(path);
        Set(perk, "perkId", id);
        Set(perk, "title", title);
        Set(perk, "renderResources", render);
        Set(perk, "triggerTags", triggerTags);
        Set(perk, "outcomeModifier", outcome);
        Set(perk, "physicalCostModifier", physical);
        Set(perk, "mentalCostModifier", mental);
        Set(perk, "clonePersistent", true);
        return perk;
    }

    private static CharacterBaseDefinition CreateCharacterBase(
        string path,
        string id,
        string name,
        string lineage,
        RenderResourceDefinition render,
        IReadOnlyList<ActionCardDefinition> deck,
        IReadOnlyList<PerkDefinition> perks,
        int observation,
        int dexterity,
        int boldness,
        int intuition,
        int logic)
    {
        var character = CreateOrLoad<CharacterBaseDefinition>(path);
        Set(character, "personnelId", id);
        Set(character, "displayName", name);
        Set(character, "cloneLineageId", lineage);
        Set(character, "renderResources", render);
        Set(character, "background", "Sample clone profile for the character data workshop.");
        Set(character, "personality", "Readable, replaceable, and just specific enough to make management awkward.");
        Set(character, "workStyle", "Designed as a sample data block.");
        Set(character, "initialInformationScope", AffinityScope.Working);
        Set(character, "startingDeck", deck);
        Set(character, "startingPerks", perks);
        SetAptitudes(character, observation, dexterity, boldness, intuition, logic);
        return character;
    }

    private static CharacterRuntimeData CreateRuntime(
        string path,
        CharacterBaseDefinition baseDefinition,
        string personnelId,
        IReadOnlyList<ActionCardDefinition> deck,
        IReadOnlyList<PerkDefinition> perks)
    {
        var runtime = CreateOrLoad<CharacterRuntimeData>(path);
        Set(runtime, "baseDefinition", baseDefinition);
        Set(runtime, "personnelIdOverride", personnelId);
        Set(runtime, "informationScopeOverride", AffinityScope.Working);
        Set(runtime, "deck", deck);
        Set(runtime, "perks", perks);
        return runtime;
    }

    private static void SetAptitudes(CharacterBaseDefinition character, int observation, int dexterity, int boldness, int intuition, int logic)
    {
        var serialized = new SerializedObject(character);
        var aptitudes = serialized.FindProperty("aptitudes");
        aptitudes.FindPropertyRelative("Observation").intValue = observation;
        aptitudes.FindPropertyRelative("Dexterity").intValue = dexterity;
        aptitudes.FindPropertyRelative("Boldness").intValue = boldness;
        aptitudes.FindPropertyRelative("Intuition").intValue = intuition;
        aptitudes.FindPropertyRelative("Logic").intValue = logic;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
    }

    private static void Set<T>(UnityEngine.Object asset, string fieldName, T value)
    {
        var serialized = new SerializedObject(asset);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            throw new InvalidOperationException($"Missing serialized field '{fieldName}' on {asset.GetType().Name}.");
        }

        SetProperty(property, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void SetProperty<T>(SerializedProperty property, T value)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.String:
                property.stringValue = value?.ToString() ?? "";
                return;
            case SerializedPropertyType.Integer:
                property.intValue = Convert.ToInt32(value);
                return;
            case SerializedPropertyType.Boolean:
                property.boolValue = Convert.ToBoolean(value);
                return;
            case SerializedPropertyType.Enum:
                property.enumValueIndex = Convert.ToInt32(value);
                return;
            case SerializedPropertyType.Color:
                property.colorValue = value is Color color ? color : Color.white;
                return;
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = value as UnityEngine.Object;
                return;
            case SerializedPropertyType.Generic when property.isArray && value is IEnumerable<string> strings:
                SetStringArray(property, strings);
                return;
            case SerializedPropertyType.Generic when property.isArray && value is System.Collections.IEnumerable objects:
                SetObjectArray(property, objects);
                return;
            default:
                throw new NotSupportedException($"Cannot set property '{property.name}' of type {property.propertyType}.");
        }
    }

    private static void SetStringArray(SerializedProperty property, IEnumerable<string> values)
    {
        property.ClearArray();
        var index = 0;
        foreach (var value in values)
        {
            property.InsertArrayElementAtIndex(index);
            property.GetArrayElementAtIndex(index).stringValue = value;
            index++;
        }
    }

    private static void SetObjectArray(SerializedProperty property, System.Collections.IEnumerable values)
    {
        property.ClearArray();
        var index = 0;
        foreach (var value in values)
        {
            property.InsertArrayElementAtIndex(index);
            property.GetArrayElementAtIndex(index).objectReferenceValue = value as UnityEngine.Object;
            index++;
        }
    }
}
}
