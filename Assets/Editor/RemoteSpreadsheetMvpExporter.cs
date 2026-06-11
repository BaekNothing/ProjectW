using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ProjectW.IngameCore.CaseReview;
using UnityEditor;
using UnityEngine;

namespace ProjectW.Editor
{
public static class RemoteSpreadsheetMvpExporter
{
    public const string OutputFolder = "Library/RemoteSpreadsheetMvpExport";

    [MenuItem("Tools/ProjectW/Case Review/Export Current MVP Spreadsheet CSV")]
    public static void ExportCurrentMvpData()
    {
        Directory.CreateDirectory(OutputFolder);
        var state = CaseReviewGame.Init(new GameConfig(), 1);
        Write("_manifest", BuildManifest());
        Write("localized_text", BuildLocalizedText());
        Write("cards", BuildCards(state));
        Write("characters", BuildCharacters(state));
        Write("work_definitions", BuildWork(state));
        Write("scenarios", BuildScenarios());
        AssetDatabase.Refresh();
        Debug.Log($"ProjectW MVP spreadsheet CSV exported to {Path.GetFullPath(OutputFolder)}");
    }

    private static string BuildManifest()
    {
        var headers = new[] { "datasetId", "sheetName", "enabled", "schemaVersion", "required", "notes" };
        var rows = new[]
        {
            Row("localized_text", "localized_text", "TRUE", "2", "TRUE", "Replacement localized text snapshot"),
            Row("work_definitions", "work_definitions", "TRUE", "2", "TRUE", "Replacement Day 1 work snapshot"),
            Row("cards", "cards", "TRUE", "2", "TRUE", "Replacement card snapshot"),
            Row("characters", "characters", "TRUE", "2", "TRUE", "Replacement personnel snapshot"),
            Row("scenarios", "scenarios", "TRUE", "2", "TRUE", "Replacement scenario snapshot")
        };
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildLocalizedText()
    {
        var entries = Resources.LoadAll<LocalizedTextTable>("CaseReviewData")
            .Where(table => table != null)
            .SelectMany(table => table.Entries)
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
        return LocalizedTextCsv.ToCsv(entries);
    }

    private static string BuildCards(GameState state)
    {
        var headers = new[]
        {
            "cardId", "title", "visibleSummary", "hiddenIntent", "requiredScope", "tags",
            "outcomeModifier", "riskModifier", "reviewCostModifier", "criticalChancePercent",
            "criticalMultiplier", "memoryHooks", "growthHooks", "bossReactionTags"
        };
        var rows = state.Staff
            .SelectMany(person => person.Deck)
            .GroupBy(card => card.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(card => card.Id, StringComparer.OrdinalIgnoreCase)
            .Select(card => Row(
                card.Id,
                card.Title,
                card.Summary,
                "",
                AffinityScope.Surface.ToString(),
                Pipe(card.Tags),
                Number(card.OutcomeModifier),
                Number(card.RiskModifier),
                Number(card.ReviewCostModifier),
                Number(card.CriticalChancePercent),
                Number(card.CriticalMultiplier),
                "",
                "",
                ""));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildCharacters(GameState state)
    {
        var headers = new[]
        {
            "personnelId", "displayName", "cloneLineageId", "background", "interests", "personality",
            "workStyle", "initialInformationScope", "aptitudesJson", "physicalEnergy", "mentalStress",
            "loadAssigned", "fatigue", "stagnation", "trustToManager", "retentionRisk", "hasLeft",
            "daysSinceJoined", "optLow", "optHigh", "maxLoad", "connectionLimit", "cloneVersion",
            "regenerationCount", "regeneratedFromId", "startingDeckIds", "perksJson", "relationshipsJson",
            "memoriesJson", "traitSamplesJson"
        };
        var rows = state.Staff
            .OrderBy(person => person.Id, StringComparer.OrdinalIgnoreCase)
            .Select(person => Row(
                person.Id,
                person.Name,
                person.CloneLineageId,
                person.Background,
                Pipe(person.Interests),
                person.Personality,
                person.WorkStyle,
                person.InformationScope.ToString(),
                Json(person.Aptitudes),
                Number(person.PhysicalEnergy),
                Number(person.MentalStress),
                Number(person.LoadAssigned),
                Number(person.Fatigue),
                Number(person.Stagnation),
                Number(person.TrustToManager),
                Number(person.RetentionRisk),
                Bool(person.HasLeft),
                Number(person.DaysSinceJoined),
                Number(person.OptLow),
                Number(person.OptHigh),
                Number(person.MaxLoad),
                Number(person.ConnectionLimit),
                Number(person.CloneVersion),
                Number(person.RegenerationCount),
                person.RegeneratedFromId,
                Pipe(person.Deck.Select(card => card.Id)),
                Json(person.Perks),
                Json(person.Relationships),
                Json(person.Memories),
                Json(person.TraitSamples)));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildWork(GameState state)
    {
        var headers = new[]
        {
            "eventId", "workId", "title", "kind", "subsystem", "importance", "volume", "urgency",
            "severity", "ttlSec", "status", "latentRisk", "mismatchScore", "assignedPersonnel",
            "physicalCost", "mentalCost", "baseSuccessChance", "requiredAptitudes",
            "recommendedPersonnelCount", "minPersonnelCount", "maxPersonnelCount", "concurrentLimit",
            "concurrentSlotCost", "splitPenalty", "soloPenalty", "tags", "perkTags", "cardHooks",
            "bossReactionTags", "memoryHooks", "visibleSummary", "hiddenFacts", "perkInteractionInfo",
            "truthFramesJson", "logsJson"
        };
        var rows = state.Queue
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(item => Row(
                item.Id,
                item.DefinitionId,
                item.Title,
                item.Kind,
                item.Subsystem,
                Number(item.Importance),
                Number(item.Volume),
                Number(item.Urgency),
                Number(item.Severity),
                Number(item.TtlSec),
                item.Status.ToString(),
                Number(item.LatentRisk),
                Number(item.MismatchScore),
                Pipe(item.AssignedPersonnel),
                Number(item.PhysicalCost),
                Number(item.MentalCost),
                Number(item.BaseSuccessChance),
                Json(item.RequiredAptitudes),
                Number(item.RecommendedPersonnelCount),
                Number(item.MinPersonnelCount),
                Number(item.MaxPersonnelCount),
                Number(item.ConcurrentLimit),
                Number(item.ConcurrentSlotCost),
                Number(item.SplitPenalty),
                Number(item.SoloPenalty),
                Pipe(item.Tags),
                Pipe(item.PerkTags),
                Pipe(item.CardHooks),
                Pipe(item.BossReactionTags),
                Pipe(item.MemoryHooks),
                item.VisibleSummary,
                Pipe(item.HiddenFacts),
                item.PerkInteractionInfo,
                Json(state.TruthFrames.Where(frame => frame.EventId.Equals(item.Id, StringComparison.OrdinalIgnoreCase)).ToList()),
                Json(state.Logs.Where(log => log.EventId.Equals(item.Id, StringComparison.OrdinalIgnoreCase)).ToList())));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildScenarios()
    {
        var headers = new[]
        {
            "eventId", "timing", "priority", "playbackStateKey", "triggerMode",
            "allowedExplicitLocationsJson", "triggerConditionsJson", "textTableId", "linesJson",
            "oneShot", "cooldownDays", "allowReplayInDebug"
        };
        var rows = Resources.LoadAll<ScenarioEventDefinition>("CaseReviewData/Scenarios/Events")
            .Where(scenario => scenario != null)
            .OrderBy(scenario => scenario.EventId, StringComparer.OrdinalIgnoreCase)
            .Select(scenario => Row(
                scenario.EventId,
                scenario.Timing.ToString(),
                Number(scenario.Priority),
                scenario.PlaybackStateKey,
                scenario.TriggerMode.ToString(),
                Json(scenario.AllowedExplicitLocations),
                Json(scenario.TriggerConditions),
                scenario.TextTable != null ? scenario.TextTable.TableId : "",
                Json(scenario.Lines.Select(SanitizeLine).ToList()),
                Bool(scenario.ReplayPolicy?.OneShot ?? true),
                Number(scenario.ReplayPolicy?.CooldownDays ?? 0),
                Bool(scenario.ReplayPolicy?.AllowReplayInDebug ?? false)));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static ScenarioScriptLine SanitizeLine(ScenarioScriptLine source)
    {
        return new ScenarioScriptLine
        {
            LineId = source.LineId,
            Kind = source.Kind,
            SpeakerId = source.SpeakerId,
            PortraitIds = new List<string>(source.PortraitIds ?? new List<string>()),
            TextKey = source.TextKey,
            ExpressionKey = source.ExpressionKey,
            PoseKey = source.PoseKey,
            VoiceToneKey = source.VoiceToneKey,
            StageCommands = (source.StageCommands ?? new List<ScenarioStageCommand>())
                .Select(command => new ScenarioStageCommand
                {
                    CommandType = command.CommandType,
                    TargetId = command.TargetId,
                    Value = command.Value,
                    DurationSec = command.DurationSec,
                    Intensity = command.Intensity
                })
                .ToList(),
            Choices = source.Choices ?? new List<ScenarioChoice>(),
            Effects = source.Effects ?? new List<ScenarioStateEffect>()
        };
    }

    private static IReadOnlyList<string> Row(params string[] values)
    {
        return values;
    }

    private static string Json(object value)
    {
        return JsonConvert.SerializeObject(value, Formatting.None);
    }

    private static string Pipe(IEnumerable<string> values)
    {
        return string.Join("|", (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Number(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Bool(bool value)
    {
        return value ? "TRUE" : "FALSE";
    }

    private static void Write(string datasetId, string csv)
    {
        File.WriteAllText(Path.Combine(OutputFolder, datasetId + ".csv"), csv, new System.Text.UTF8Encoding(false));
    }
}
}
