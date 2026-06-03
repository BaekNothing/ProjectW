using System.IO;
using System.Text;
using ProjectW.IngameCore.CaseReview;
using UnityEditor;
using UnityEngine;

namespace ProjectW.Editor
{
[CustomEditor(typeof(LocalizedTextTable))]
public sealed class LocalizedTextTableEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("CSV Spreadsheet Tools", EditorStyles.boldLabel);

        var table = (LocalizedTextTable)target;
        if (GUILayout.Button("Export CSV"))
        {
            LocalizedTextTableCsvEditorUtility.ExportWithPanel(table);
        }

        if (GUILayout.Button("Import CSV"))
        {
            LocalizedTextTableCsvEditorUtility.ImportWithPanel(table);
        }
    }
}

public static class LocalizedTextTableCsvEditorUtility
{
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(true);

    public static void ExportWithPanel(LocalizedTextTable table)
    {
        if (table == null)
        {
            return;
        }

        var defaultName = $"{Sanitize(table.TableId, table.name)}.csv";
        var path = EditorUtility.SaveFilePanel("Export localized text CSV", Application.dataPath, defaultName, "csv");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Export(table, path);
    }

    public static void ImportWithPanel(LocalizedTextTable table)
    {
        if (table == null)
        {
            return;
        }

        var path = EditorUtility.OpenFilePanel("Import localized text CSV", Application.dataPath, "csv");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Import(table, path);
    }

    public static void Export(LocalizedTextTable table, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, LocalizedTextCsv.ToCsv(table), Utf8WithBom);
        AssetDatabase.Refresh();
        Debug.Log($"Exported localized text CSV: {path}");
    }

    public static void Import(LocalizedTextTable table, string path)
    {
        var csv = File.ReadAllText(path, Encoding.UTF8);
        ApplyEntries(table, LocalizedTextCsv.FromCsv(csv));
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Imported localized text CSV: {path}");
    }

    public static void ExportAll(string assetFolder, string csvFolder)
    {
        Directory.CreateDirectory(csvFolder);
        foreach (var guid in AssetDatabase.FindAssets("t:LocalizedTextTable", new[] { assetFolder }))
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var table = AssetDatabase.LoadAssetAtPath<LocalizedTextTable>(assetPath);
            if (table == null)
            {
                continue;
            }

            Export(table, Path.Combine(csvFolder, $"{Sanitize(table.TableId, table.name)}.csv"));
        }
    }

    private static void ApplyEntries(LocalizedTextTable table, System.Collections.Generic.List<LocalizedTextEntry> entries)
    {
        var serialized = new SerializedObject(table);
        var property = serialized.FindProperty("entries");
        property.ClearArray();
        for (var i = 0; i < entries.Count; i++)
        {
            property.InsertArrayElementAtIndex(i);
            var entryProperty = property.GetArrayElementAtIndex(i);
            entryProperty.FindPropertyRelative("Key").stringValue = entries[i].Key;
            var valuesProperty = entryProperty.FindPropertyRelative("Values");
            valuesProperty.ClearArray();
            for (var j = 0; j < entries[i].Values.Count; j++)
            {
                valuesProperty.InsertArrayElementAtIndex(j);
                var valueProperty = valuesProperty.GetArrayElementAtIndex(j);
                valueProperty.FindPropertyRelative("LanguageKey").stringValue = entries[i].Values[j].LanguageKey;
                valueProperty.FindPropertyRelative("CountryCode").stringValue = entries[i].Values[j].CountryCode;
                valueProperty.FindPropertyRelative("Text").stringValue = entries[i].Values[j].Text;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string Sanitize(string value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value;
        foreach (var ch in Path.GetInvalidFileNameChars())
        {
            source = source.Replace(ch, '_');
        }

        return string.IsNullOrWhiteSpace(source) ? "LocalizedTextTable" : source;
    }
}
}
