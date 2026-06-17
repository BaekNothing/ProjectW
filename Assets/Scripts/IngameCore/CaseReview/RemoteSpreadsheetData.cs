using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectW.IngameCore.CaseReview
{
public sealed class RemoteSpreadsheetSyncResult
{
    public bool Success;
    public string Message = "";
    public int DatasetCount;
    public int CharacterCount;
    public int WorkCount;
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
    public static RemoteSpreadsheetSnapshot ActiveSnapshot { get; private set; }

    private static readonly Dictionary<string, string[]> RequiredHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["localized_text"] = new[] { "Key" },
        ["work_definitions"] = new[]
        {
            "eventId",
            "workId",
            "title",
            "kind",
            "subsystem",
            "projectId",
            "tier"
        },
        ["truth_actions"] = new[] { "actionCode", "sourceType", "visibleText" },
        ["work_details"] = new[] { "eventId", "observation", "dexterity", "boldness", "intuition", "logic", "truthFramesJson" },
        ["work_outcome_events"] = new[] { "sourceWorkId", "targetWorkId", "minOutcomeScore", "maxOutcomeScore", "minLatentRisk", "chancePercent", "relation" },
        ["cards"] = new[] { "cardId", "title" },
        ["perks"] = new[] { "perkId", "title", "triggerTags", "outcomeModifier", "physicalCostModifier", "mentalCostModifier", "clonePersistent" },
        ["characters"] = new[] { "personnelId", "displayName", "startingDeckIds", "startingPerkIds" },
        ["character_details"] = new[] { "personnelId", "observation", "dexterity", "boldness", "intuition", "logic" },
        ["scenarios"] = new[]
        {
            "eventId",
            "timing"
        },
        ["scenario_details"] = new[]
        {
            "scenarioId",
            "rowType",
            "rowId",
            "parentLineId",
            "textKey",
            "choiceLabelTextKey",
            "jumpToLineId",
            "allowedExplicitLocations",
            "triggerConditionsJson",
            "entryCostsJson",
            "exitEffectsJson",
            "stageCommandsJson",
            "effectsJson",
            "visibleConditionsJson",
            "costsJson"
        }
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

    public static bool TryLoadCachedSnapshot(out string summary, out string error)
    {
        summary = "";
        error = "";
        var datasets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var datasetId in RemoteSpreadsheetSnapshotParser.RequiredDatasetIds)
        {
            var path = CachePath(datasetId);
            if (!File.Exists(path))
            {
                return false;
            }

            datasets[datasetId] = File.ReadAllText(path, Encoding.UTF8);
        }

        try
        {
            ActivateSnapshot(RemoteSpreadsheetSnapshotParser.Parse(datasets));
            summary = $"{ActiveSnapshot.InitialData.Staff.Count} STAFF / {ActiveSnapshot.InitialData.Queue.Count} WORK";
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

        var enabledEntries = manifest
            .Where(item => item.Enabled)
            .ToDictionary(item => item.DatasetId, StringComparer.OrdinalIgnoreCase);
        foreach (var datasetId in RemoteSpreadsheetSnapshotParser.RequiredDatasetIds)
        {
            if (!enabledEntries.ContainsKey(datasetId))
            {
                completed?.Invoke(Failure($"Replacement manifest must enable required dataset '{datasetId}'."));
                yield break;
            }
        }

        foreach (var entry in enabledEntries.Values)
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
                completed?.Invoke(Failure($"{entry.DatasetId} download failed: {requestError}"));
                yield break;
            }

            try
            {
                ValidateDataset(entry.DatasetId, csv);
                stagedCsv[entry.DatasetId] = csv;
            }
            catch (Exception exception)
            {
                completed?.Invoke(Failure($"{entry.DatasetId} validation failed: {exception.Message}"));
                yield break;
            }
        }

        RemoteSpreadsheetSnapshot snapshot;
        try
        {
            snapshot = RemoteSpreadsheetSnapshotParser.Parse(stagedCsv);
        }
        catch (Exception exception)
        {
            completed?.Invoke(Failure($"Replacement snapshot validation failed: {exception.Message}"));
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
            ActivateSnapshot(snapshot);
            completed?.Invoke(new RemoteSpreadsheetSyncResult
            {
                Success = true,
                DatasetCount = stagedCsv.Count,
                CharacterCount = snapshot.InitialData.Staff.Count,
                WorkCount = snapshot.InitialData.Queue.Count,
                CompletedAtUtc = DateTime.UtcNow,
                Message = $"Replaced all data with {snapshot.InitialData.Staff.Count} staff, {snapshot.InitialData.Queue.Count} work items, and {snapshot.Scenarios.Count} scenarios."
            });
        }
        catch (Exception exception)
        {
            completed?.Invoke(Failure($"Cache write failed: {exception.Message}"));
        }
    }

    private static void ActivateSnapshot(RemoteSpreadsheetSnapshot snapshot)
    {
        ActiveSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        CharacterLocalProgressStore.ApplyTo(ActiveSnapshot.InitialData.Staff);
        LocalizedTextRuntimeOverrides.Replace(snapshot.LocalizedTextEntries);
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

public sealed class CharacterProgressSnapshot
{
    public List<CharacterProgressRecord> Characters { get; set; } = new();
}

public sealed class CharacterProgressRecord
{
    public string PersonnelId { get; set; } = "";
    public int PhysicalEnergy { get; set; }
    public int MentalStress { get; set; }
    public int LoadAssigned { get; set; }
    public int Fatigue { get; set; }
    public int Stagnation { get; set; }
    public int TrustToManager { get; set; }
    public int RetentionRisk { get; set; }
    public bool HasLeft { get; set; }
    public int DaysSinceJoined { get; set; }
    public int CloneVersion { get; set; }
    public int RegenerationCount { get; set; }
    public string RegeneratedFromId { get; set; } = "";
    public AffinityScope InformationScope { get; set; } = AffinityScope.Surface;
    public List<ActionCard> Deck { get; set; } = new();
    public List<PersonnelPerk> Perks { get; set; } = new();
    public List<PersonnelRelationship> Relationships { get; set; } = new();
    public List<PersonnelMemory> Memories { get; set; } = new();
    public List<PersonnelTraitSample> TraitSamples { get; set; } = new();
    public List<PersonnelInjury> Injuries { get; set; } = new();
}

public static class CharacterLocalProgressStore
{
    private const string FileName = "character_progress.json";

    public static void SaveFrom(IEnumerable<Personnel> staff)
    {
        Directory.CreateDirectory(RemoteDataFolderPath());
        var snapshot = new CharacterProgressSnapshot
        {
            Characters = (staff ?? Enumerable.Empty<Personnel>())
                .Where(person => !string.IsNullOrWhiteSpace(person.Id))
                .Select(ToRecord)
                .ToList()
        };
        File.WriteAllText(ProgressPath(), JsonConvert.SerializeObject(snapshot, Formatting.Indented), new UTF8Encoding(false));
    }

    public static void ApplyTo(IEnumerable<Personnel> staff)
    {
        if (!File.Exists(ProgressPath()))
        {
            return;
        }

        CharacterProgressSnapshot snapshot;
        try
        {
            snapshot = JsonConvert.DeserializeObject<CharacterProgressSnapshot>(File.ReadAllText(ProgressPath(), Encoding.UTF8))
                ?? new CharacterProgressSnapshot();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Character local progress ignored: {exception.Message}");
            return;
        }

        var progressById = snapshot.Characters
            .Where(record => !string.IsNullOrWhiteSpace(record.PersonnelId))
            .ToDictionary(record => record.PersonnelId, StringComparer.OrdinalIgnoreCase);
        foreach (var person in staff ?? Enumerable.Empty<Personnel>())
        {
            if (person == null || !progressById.TryGetValue(person.Id, out var progress))
            {
                continue;
            }

            ApplyRecord(person, progress);
        }
    }

    public static string ProgressPath()
    {
        return Path.Combine(RemoteDataFolderPath(), FileName);
    }

    private static string RemoteDataFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, "remote-data");
    }

    private static CharacterProgressRecord ToRecord(Personnel person)
    {
        return new CharacterProgressRecord
        {
            PersonnelId = person.Id,
            PhysicalEnergy = person.PhysicalEnergy,
            MentalStress = person.MentalStress,
            LoadAssigned = person.LoadAssigned,
            Fatigue = person.Fatigue,
            Stagnation = person.Stagnation,
            TrustToManager = person.TrustToManager,
            RetentionRisk = person.RetentionRisk,
            HasLeft = person.HasLeft,
            DaysSinceJoined = person.DaysSinceJoined,
            CloneVersion = person.CloneVersion,
            RegenerationCount = person.RegenerationCount,
            RegeneratedFromId = person.RegeneratedFromId,
            InformationScope = person.InformationScope,
            Deck = person.Deck ?? new List<ActionCard>(),
            Perks = person.Perks ?? new List<PersonnelPerk>(),
            Relationships = person.Relationships ?? new List<PersonnelRelationship>(),
            Memories = person.Memories ?? new List<PersonnelMemory>(),
            TraitSamples = person.TraitSamples ?? new List<PersonnelTraitSample>(),
            Injuries = person.Injuries ?? new List<PersonnelInjury>()
        };
    }

    private static void ApplyRecord(Personnel person, CharacterProgressRecord progress)
    {
        person.PhysicalEnergy = progress.PhysicalEnergy;
        person.MentalStress = progress.MentalStress;
        person.LoadAssigned = progress.LoadAssigned;
        person.Fatigue = progress.Fatigue;
        person.Stagnation = progress.Stagnation;
        person.TrustToManager = progress.TrustToManager;
        person.RetentionRisk = progress.RetentionRisk;
        person.HasLeft = progress.HasLeft;
        person.DaysSinceJoined = progress.DaysSinceJoined;
        person.CloneVersion = Math.Max(1, progress.CloneVersion);
        person.RegenerationCount = progress.RegenerationCount;
        person.RegeneratedFromId = progress.RegeneratedFromId ?? "";
        person.InformationScope = progress.InformationScope;
        person.Deck = progress.Deck ?? new List<ActionCard>();
        person.Perks = progress.Perks ?? new List<PersonnelPerk>();
        person.Relationships = progress.Relationships ?? new List<PersonnelRelationship>();
        person.Memories = progress.Memories ?? new List<PersonnelMemory>();
        person.TraitSamples = progress.TraitSamples ?? new List<PersonnelTraitSample>();
        person.Injuries = progress.Injuries ?? new List<PersonnelInjury>();
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

        var source = csv ?? "";
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < source.Length && source[index + 1] == '"')
                    {
                        cell.Append('"');
                        index++;
                        continue;
                    }

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

    public static string ToCsv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(Escape)));
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        value ??= "";
        var escaped = value.Replace("\"", "\"\"");
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? $"\"{escaped}\"" : escaped;
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
