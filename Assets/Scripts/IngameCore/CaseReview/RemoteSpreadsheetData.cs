using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectW.IngameCore.CaseReview
{
public sealed class RemoteSpreadsheetSyncResult
{
    public bool Success;
    public string Message = "";
    public int DatasetCount;
    public DateTime CompletedAtUtc;
}

public sealed class RemoteSpreadsheetManifestEntry
{
    public string DatasetId = "";
    public string SheetName = "";
    public bool Enabled;
    public int SchemaVersion;
    public bool Required;
}

public static class RemoteSpreadsheetData
{
    public const string SpreadsheetId = "1AbGMtaZzbHYyKj307znp5Jna7iIBUiG4bSEv9Q30A0s";
    public const string ManifestSheetName = "_manifest";

    private const int RequestTimeoutSeconds = 20;
    private const string CacheFolderName = "remote-data";

    private static readonly Dictionary<string, string[]> RequiredHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["localized_text"] = new[] { "Key" },
        ["work_definitions"] = new[] { "workId", "title", "kind", "subsystem" },
        ["cards"] = new[] { "cardId", "title" },
        ["characters"] = new[] { "personnelId", "displayName" },
        ["scenarios"] = new[] { "eventId", "timing", "linesJson" }
    };

    public static string BuildCsvUrl(string sheetName)
    {
        var escapedSheetName = UnityWebRequest.EscapeURL(sheetName ?? "");
        return $"https://docs.google.com/spreadsheets/d/{SpreadsheetId}/gviz/tq?tqx=out:csv&sheet={escapedSheetName}";
    }

    public static IReadOnlyList<RemoteSpreadsheetManifestEntry> ParseManifest(string csv)
    {
        var rows = SpreadsheetCsv.ParseRows(csv);
        if (rows.Count == 0)
        {
            throw new FormatException("Remote data manifest is empty.");
        }

        var header = rows[0];
        var datasetIndex = HeaderIndex(header, "datasetId");
        var sheetIndex = HeaderIndex(header, "sheetName");
        var enabledIndex = HeaderIndex(header, "enabled");
        var schemaIndex = HeaderIndex(header, "schemaVersion");
        var requiredIndex = HeaderIndex(header, "required");
        var entries = new List<RemoteSpreadsheetManifestEntry>();

        foreach (var row in rows.Skip(1))
        {
            var datasetId = Cell(row, datasetIndex).Trim();
            var sheetName = Cell(row, sheetIndex).Trim();
            if (string.IsNullOrWhiteSpace(datasetId) || string.IsNullOrWhiteSpace(sheetName))
            {
                continue;
            }

            entries.Add(new RemoteSpreadsheetManifestEntry
            {
                DatasetId = datasetId,
                SheetName = sheetName,
                Enabled = ParseBoolean(Cell(row, enabledIndex)),
                SchemaVersion = ParseInteger(Cell(row, schemaIndex), 1),
                Required = ParseBoolean(Cell(row, requiredIndex))
            });
        }

        return entries;
    }

    public static bool TryLoadCachedLocalizedText(out int entryCount, out string error)
    {
        entryCount = 0;
        error = "";
        var path = CachePath("localized_text");
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var entries = LocalizedTextCsv.FromCsv(File.ReadAllText(path, Encoding.UTF8));
            LocalizedTextRuntimeOverrides.Replace(entries);
            entryCount = entries.Count;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static IEnumerator Sync(Action<RemoteSpreadsheetSyncResult> completed)
    {
        var stagedCsv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string manifestCsv = null;
        string requestError = null;

        yield return DownloadCsv(ManifestSheetName, (csv, error) =>
        {
            manifestCsv = csv;
            requestError = error;
        });

        if (!string.IsNullOrWhiteSpace(requestError))
        {
            completed?.Invoke(Failure($"Manifest download failed: {requestError}"));
            yield break;
        }

        IReadOnlyList<RemoteSpreadsheetManifestEntry> manifest;
        try
        {
            manifest = ParseManifest(manifestCsv);
        }
        catch (Exception exception)
        {
            completed?.Invoke(Failure($"Manifest validation failed: {exception.Message}"));
            yield break;
        }

        foreach (var entry in manifest.Where(item => item.Enabled))
        {
            string csv = null;
            requestError = null;
            yield return DownloadCsv(entry.SheetName, (value, error) =>
            {
                csv = value;
                requestError = error;
            });

            if (!string.IsNullOrWhiteSpace(requestError))
            {
                if (entry.Required)
                {
                    completed?.Invoke(Failure($"{entry.DatasetId} download failed: {requestError}"));
                    yield break;
                }

                continue;
            }

            try
            {
                ValidateDataset(entry.DatasetId, csv);
                stagedCsv[entry.DatasetId] = csv;
            }
            catch (Exception exception)
            {
                if (entry.Required)
                {
                    completed?.Invoke(Failure($"{entry.DatasetId} validation failed: {exception.Message}"));
                    yield break;
                }
            }
        }

        if (!stagedCsv.TryGetValue("localized_text", out var localizedTextCsv))
        {
            completed?.Invoke(Failure("The required localized_text dataset was not staged."));
            yield break;
        }

        try
        {
            Directory.CreateDirectory(CacheFolderPath());
            foreach (var pair in stagedCsv)
            {
                File.WriteAllText(CachePath(pair.Key), pair.Value, new UTF8Encoding(false));
            }

            File.WriteAllText(CachePath(ManifestSheetName), manifestCsv, new UTF8Encoding(false));
            var localizedEntries = LocalizedTextCsv.FromCsv(localizedTextCsv);
            LocalizedTextRuntimeOverrides.Replace(localizedEntries);
            completed?.Invoke(new RemoteSpreadsheetSyncResult
            {
                Success = true,
                DatasetCount = stagedCsv.Count,
                CompletedAtUtc = DateTime.UtcNow,
                Message = $"Downloaded {stagedCsv.Count} datasets and applied {localizedEntries.Count} localized text entries."
            });
        }
        catch (Exception exception)
        {
            completed?.Invoke(Failure($"Cache write failed: {exception.Message}"));
        }
    }

    public static void ValidateDataset(string datasetId, string csv)
    {
        var rows = SpreadsheetCsv.ParseRows(csv);
        if (rows.Count == 0)
        {
            throw new FormatException("CSV is empty.");
        }

        if (!RequiredHeaders.TryGetValue(datasetId, out var requiredHeaders))
        {
            return;
        }

        foreach (var header in requiredHeaders)
        {
            HeaderIndex(rows[0], header);
        }

        if (datasetId.Equals("localized_text", StringComparison.OrdinalIgnoreCase))
        {
            LocalizedTextCsv.FromCsv(csv);
        }
    }

    private static IEnumerator DownloadCsv(string sheetName, Action<string, string> completed)
    {
        using var request = UnityWebRequest.Get(BuildCsvUrl(sheetName));
        request.timeout = RequestTimeoutSeconds;
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            completed(null, $"{request.responseCode} {request.error}".Trim());
            yield break;
        }

        completed(request.downloadHandler.text, null);
    }

    private static int HeaderIndex(IReadOnlyList<string> header, string name)
    {
        for (var index = 0; index < header.Count; index++)
        {
            if (string.Equals(header[index]?.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        throw new FormatException($"Required column '{name}' was not found.");
    }

    private static string Cell(IReadOnlyList<string> row, int index)
    {
        return index >= 0 && index < row.Count ? row[index] ?? "" : "";
    }

    private static bool ParseBoolean(string value)
    {
        return string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value?.Trim(), "1", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseInteger(string value, int fallback)
    {
        return int.TryParse(value?.Trim(), out var parsed) ? parsed : fallback;
    }

    private static string CacheFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, CacheFolderName);
    }

    private static string CachePath(string datasetId)
    {
        var safeName = new string((datasetId ?? "dataset")
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_')
            .ToArray());
        return Path.Combine(CacheFolderPath(), safeName + ".csv");
    }

    private static RemoteSpreadsheetSyncResult Failure(string message)
    {
        return new RemoteSpreadsheetSyncResult
        {
            Success = false,
            Message = message ?? "Remote data sync failed.",
            CompletedAtUtc = DateTime.UtcNow
        };
    }
}

public static class SpreadsheetCsv
{
    public static List<List<string>> ParseRows(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        foreach (var character in csv ?? "")
        {
            if (inQuotes)
            {
                if (character == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    cell.Append(character);
                }

                continue;
            }

            if (character == '"')
            {
                inQuotes = true;
                continue;
            }

            if (character == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
                continue;
            }

            if (character == '\n')
            {
                row.Add(TrimCarriageReturn(cell.ToString()));
                cell.Clear();
                rows.Add(row);
                row = new List<string>();
                continue;
            }

            cell.Append(character);
        }

        row.Add(TrimCarriageReturn(cell.ToString()));
        if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
        {
            rows.Add(row);
        }

        return rows;
    }

    private static string TrimCarriageReturn(string value)
    {
        return value.EndsWith("\r", StringComparison.Ordinal) ? value[..^1] : value;
    }
}

public static class LocalizedTextRuntimeOverrides
{
    private static readonly Dictionary<string, LocalizedTextEntry> Entries = new(StringComparer.OrdinalIgnoreCase);

    public static int Count => Entries.Count;

    public static void Replace(IEnumerable<LocalizedTextEntry> entries)
    {
        Entries.Clear();
        foreach (var entry in entries ?? Enumerable.Empty<LocalizedTextEntry>())
        {
            if (!string.IsNullOrWhiteSpace(entry?.Key))
            {
                Entries[entry.Key.Trim()] = entry;
            }
        }
    }

    public static void Clear()
    {
        Entries.Clear();
    }

    public static bool TryGetText(
        string key,
        string languageKey,
        string countryCode,
        string defaultLanguageKey,
        string defaultCountryCode,
        out string text)
    {
        text = "";
        if (string.IsNullOrWhiteSpace(key) || !Entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        var values = entry.Values ?? new List<LocalizedTextValue>();
        var language = Normalize(languageKey, defaultLanguageKey);
        var country = Normalize(countryCode, defaultCountryCode);
        var defaultLanguage = Normalize(defaultLanguageKey, "ko");
        var defaultCountry = Normalize(defaultCountryCode, "KR");
        var localized = Find(values, language, country)
            ?? FindLanguage(values, language)
            ?? Find(values, defaultLanguage, defaultCountry)
            ?? FindLanguage(values, defaultLanguage)
            ?? values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value.Text));

        if (localized == null || string.IsNullOrWhiteSpace(localized.Text))
        {
            return false;
        }

        text = localized.Text;
        return true;
    }

    private static LocalizedTextValue Find(IEnumerable<LocalizedTextValue> values, string language, string country)
    {
        return values.FirstOrDefault(value =>
            string.Equals(value.LanguageKey, language, StringComparison.OrdinalIgnoreCase)
            && string.Equals(value.CountryCode, country, StringComparison.OrdinalIgnoreCase));
    }

    private static LocalizedTextValue FindLanguage(IEnumerable<LocalizedTextValue> values, string language)
    {
        return values.FirstOrDefault(value =>
            string.Equals(value.LanguageKey, language, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(value.Text));
    }

    private static string Normalize(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
}
