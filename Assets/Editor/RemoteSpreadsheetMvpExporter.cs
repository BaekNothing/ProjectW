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
        Write("character_details", BuildCharacterDetails(state));
        Write("work_definitions", BuildWork(state));
        Write("work_details", BuildWorkDetails(state));
        Write("work_outcome_events", BuildWorkOutcomeEvents(state));
        Write("scenarios", BuildScenarios());
        Write("scenario_details", BuildScenarioDetails());
        Write("info", BuildInfo());
        AssetDatabase.Refresh();
        Debug.Log($"ProjectW MVP spreadsheet CSV exported to {Path.GetFullPath(OutputFolder)}");
    }

    private static string BuildManifest()
    {
        var headers = new[] { "datasetId", "sheetName", "enabled", "schemaVersion", "required", "notes" };
        var rows = new[]
        {
            Row("localized_text", "localized_text", "TRUE", "2", "TRUE", "Replacement localized text snapshot"),
            Row("work_definitions", "work_definitions", "TRUE", "3", "TRUE", "Replacement Day 1 work snapshot"),
            Row("work_details", "work_details", "TRUE", "3", "TRUE", "One work event detail row per eventId"),
            Row("work_outcome_events", "work_outcome_events", "TRUE", "1", "TRUE", "Result-linked work generation rules"),
            Row("cards", "cards", "TRUE", "2", "TRUE", "Replacement card snapshot"),
            Row("characters", "characters", "TRUE", "4", "TRUE", "Immutable personnel authoring snapshot"),
            Row("character_details", "character_details", "TRUE", "4", "TRUE", "Immutable character aptitude detail row per personnelId"),
            Row("scenarios", "scenarios", "TRUE", "5", "TRUE", "Scenario metadata without nested JSON"),
            Row("scenario_details", "scenario_details", "TRUE", "4", "TRUE", "One scenario, line, or choice key per row")
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
            "workStyle", "initialInformationScope", "basePhysicalEnergy", "baseMentalStress",
            "baseLoadAssigned", "baseFatigue", "baseStagnation", "baseTrustToManager", "baseRetentionRisk",
            "optLow", "optHigh", "maxLoad", "connectionLimit", "startingDeckIds"
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
                Number(person.PhysicalEnergy),
                Number(person.MentalStress),
                Number(person.LoadAssigned),
                Number(person.Fatigue),
                Number(person.Stagnation),
                Number(person.TrustToManager),
                Number(person.RetentionRisk),
                Number(person.OptLow),
                Number(person.OptHigh),
                Number(person.MaxLoad),
                Number(person.ConnectionLimit),
                Pipe(person.Deck.Select(card => card.Id))));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildCharacterDetails(GameState state)
    {
        var headers = new[]
        {
            "personnelId", "observation", "dexterity", "boldness", "intuition", "logic"
        };
        var rows = state.Staff
            .OrderBy(person => person.Id, StringComparer.OrdinalIgnoreCase)
            .Select(person => Row(
                person.Id,
                Aptitude(person.Aptitudes, "observation"),
                Aptitude(person.Aptitudes, "dexterity"),
                Aptitude(person.Aptitudes, "boldness"),
                Aptitude(person.Aptitudes, "intuition"),
                Aptitude(person.Aptitudes, "logic")));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildWork(GameState state)
    {
        var headers = new[]
        {
            "eventId", "workId", "title", "kind", "subsystem", "importance", "volume", "urgency",
            "severity", "ttlSec", "status", "latentRisk", "mismatchScore", "assignedPersonnel",
            "physicalCost", "mentalCost", "baseSuccessChance",
            "recommendedPersonnelCount", "minPersonnelCount", "maxPersonnelCount", "concurrentLimit",
            "concurrentSlotCost", "splitPenalty", "soloPenalty", "tags", "perkTags", "cardHooks",
            "bossReactionTags", "memoryHooks", "visibleSummary", "hiddenFacts", "perkInteractionInfo",
            "projectId", "tier", "parentEventId", "rootEventId", "triggerReason", "initiallyQueued"
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
                item.ProjectId,
                item.Tier.ToString(),
                item.ParentEventId,
                item.RootEventId,
                item.TriggerReason,
                Bool(true)));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildWorkDetails(GameState state)
    {
        var headers = new[]
        {
            "eventId", "observation", "dexterity", "boldness", "intuition", "logic",
            "truthFramesJson", "logsJson"
        };
        var rows = state.Queue
            .OrderBy(work => work.Id, StringComparer.OrdinalIgnoreCase)
            .Select(work => Row(
                work.Id,
                Aptitude(work.RequiredAptitudes, "observation"),
                Aptitude(work.RequiredAptitudes, "dexterity"),
                Aptitude(work.RequiredAptitudes, "boldness"),
                Aptitude(work.RequiredAptitudes, "intuition"),
                Aptitude(work.RequiredAptitudes, "logic"),
                Json(state.TruthFrames.Where(frame => frame.EventId.Equals(work.Id, StringComparison.OrdinalIgnoreCase)).ToList()),
                Json(state.Logs.Where(log => log.EventId.Equals(work.Id, StringComparison.OrdinalIgnoreCase)).ToList())));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildWorkOutcomeEvents(GameState state)
    {
        var headers = new[]
        {
            "sourceWorkId", "targetWorkId", "minOutcomeScore", "maxOutcomeScore",
            "minLatentRisk", "chancePercent", "relation", "reason", "notes"
        };
        var definitions = (state.WorkDefinitions ?? new List<WorkDefinition>())
            .Concat(Resources.LoadAll<WorkDefinition>("CaseReviewData").Where(definition => definition != null))
            .GroupBy(definition => definition.WorkId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(definition => definition.WorkId, StringComparer.OrdinalIgnoreCase);
        var rows = definitions
            .SelectMany(definition => definition.OutcomeEvents.Select(link => Row(
                definition.WorkId,
                link.TargetWorkId,
                Number(link.MinOutcomeScore),
                Number(link.MaxOutcomeScore),
                Number(link.MinLatentRisk),
                Number(link.ChancePercent),
                link.Relation.ToString(),
                link.Reason,
                "")));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildScenarios()
    {
        var headers = new[]
        {
            "eventId", "timing", "priority", "playbackStateKey", "triggerMode", "textTableId",
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
                scenario.TextTable != null ? scenario.TextTable.TableId : "",
                Bool(scenario.ReplayPolicy?.OneShot ?? true),
                Number(scenario.ReplayPolicy?.CooldownDays ?? 0),
                Bool(scenario.ReplayPolicy?.AllowReplayInDebug ?? false)));
        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static string BuildScenarioDetails()
    {
        var headers = new[]
        {
            "scenarioId", "rowType", "rowId", "parentLineId", "kind", "speakerId",
            "portraitIds", "textKey", "expressionKey", "poseKey", "voiceToneKey",
            "choiceLabelTextKey", "jumpToLineId", "allowedExplicitLocations",
            "triggerConditionsJson", "entryCostsJson", "exitEffectsJson",
            "stageCommandsJson", "effectsJson", "visibleConditionsJson", "costsJson"
        };
        var rows = new List<IReadOnlyList<string>>();
        foreach (var scenario in Resources.LoadAll<ScenarioEventDefinition>("CaseReviewData/Scenarios/Events")
                     .Where(scenario => scenario != null)
                     .OrderBy(scenario => scenario.EventId, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(Row(
                scenario.EventId,
                "SCENARIO",
                scenario.EventId,
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                Pipe(scenario.AllowedExplicitLocations.Select(location => location.ToString())),
                Json(scenario.TriggerConditions),
                Json(scenario.EntryCosts),
                Json(scenario.ExitEffects),
                "",
                "",
                "",
                ""));

            foreach (var line in scenario.Lines)
            {
                rows.Add(Row(
                    scenario.EventId,
                    "LINE",
                    line.LineId,
                    "",
                    line.Kind.ToString(),
                    line.SpeakerId,
                    Pipe(line.PortraitIds),
                    line.TextKey,
                    line.ExpressionKey,
                    line.PoseKey,
                    line.VoiceToneKey,
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    Json(SanitizeStageCommands(line.StageCommands)),
                    Json(line.Effects ?? new List<ScenarioStateEffect>()),
                    "",
                    ""));

                foreach (var choice in line.Choices ?? new List<ScenarioChoice>())
                {
                    rows.Add(Row(
                        scenario.EventId,
                        "CHOICE",
                        choice.ChoiceId,
                        line.LineId,
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        choice.LabelTextKey,
                        choice.NextLineId,
                        "",
                        "",
                        "",
                        "",
                        "",
                        Json(choice.Effects ?? new List<ScenarioStateEffect>()),
                        Json(choice.VisibleConditions ?? new List<ScenarioCondition>()),
                        Json(choice.Costs ?? new List<ScenarioStateEffect>())));
                }
            }
        }

        return SpreadsheetCsv.ToCsv(headers, rows);
    }

    private static List<ScenarioStageCommand> SanitizeStageCommands(IEnumerable<ScenarioStageCommand> commands)
    {
        return (commands ?? Array.Empty<ScenarioStageCommand>())
            .Select(command => new ScenarioStageCommand
            {
                CommandType = command.CommandType,
                TargetId = command.TargetId,
                Value = command.Value,
                DurationSec = command.DurationSec,
                Intensity = command.Intensity
            })
            .ToList();
    }

    private static string Aptitude(IReadOnlyDictionary<string, int> aptitudes, string key)
    {
        return aptitudes != null && aptitudes.TryGetValue(key, out var value) ? Number(value) : "";
    }

    private static string BuildInfo()
    {
        var rows = new List<IReadOnlyList<string>>();
        AddInfo(rows, "_manifest", new[]
        {
            ("datasetId", "Unique runtime dataset key."),
            ("sheetName", "Google Sheet tab downloaded for the dataset."),
            ("enabled", "Whether sync downloads this dataset."),
            ("schemaVersion", "Current sheet schema version."),
            ("required", "Whether a complete snapshot requires the dataset."),
            ("notes", "Short operator-facing dataset note.")
        });
        AddInfo(rows, "info", new[]
        {
            ("sheetName", "Sheet containing the documented column."),
            ("columnName", "Column being documented."),
            ("required", "Whether the column must contain a value."),
            ("description", "Short authoring description."),
            ("example", "Representative value or format.")
        });
        AddInfo(rows, "localized_text", new[]
        {
            ("Key", "Stable localization key."),
            ("<language-country>", "Localized text, for example ko-KR or en-US.")
        });
        AddInfo(rows, "cards", new[]
        {
            ("cardId", "Stable card identifier."), ("title", "Player-facing card title."),
            ("visibleSummary", "Player-facing card summary."), ("hiddenIntent", "Designer-facing hidden intent."),
            ("requiredScope", "Information scope required to use the card."),
            ("tags", "Pipe-separated card tags."),
            ("outcomeModifier", "Modifier applied to work outcome."), ("riskModifier", "Modifier applied to risk."),
            ("reviewCostModifier", "Modifier applied to review cost."),
            ("criticalChancePercent", "Critical success chance from 0 to 100."),
            ("criticalMultiplier", "Critical success result multiplier."),
            ("memoryHooks", "Pipe-separated memory hooks."), ("growthHooks", "Pipe-separated growth hooks."),
            ("bossReactionTags", "Pipe-separated boss reaction tags.")
        });
        AddInfo(rows, "characters", new[]
        {
            ("personnelId", "Stable personnel identifier."), ("displayName", "Player-facing name."),
            ("cloneLineageId", "Shared lineage identifier across regenerated versions."),
            ("background", "Background description."), ("interests", "Pipe-separated interests."),
            ("personality", "Personality summary."), ("workStyle", "Work-style summary."),
            ("initialInformationScope", "Initial information access scope."),
            ("basePhysicalEnergy", "Immutable starting physical energy."), ("baseMentalStress", "Immutable starting mental stress."),
            ("baseLoadAssigned", "Immutable starting assigned load."), ("baseFatigue", "Immutable starting fatigue."),
            ("baseStagnation", "Immutable starting stagnation."), ("baseTrustToManager", "Immutable starting manager trust."),
            ("baseRetentionRisk", "Immutable starting departure risk."), ("optLow", "Low preferred workload."),
            ("optHigh", "High preferred workload."), ("maxLoad", "Maximum workload."),
            ("connectionLimit", "Maximum active relationships."),
            ("startingDeckIds", "Pipe-separated starting card IDs.")
        });
        AddInfo(rows, "character_details", new[]
        {
            ("personnelId", "Personnel owning this single detail row."),
            ("observation", "Observation aptitude value."), ("dexterity", "Dexterity aptitude value."),
            ("boldness", "Boldness aptitude value."), ("intuition", "Intuition aptitude value."),
            ("logic", "Logic aptitude value. Runtime-changing character progress stays local.")
        });
        AddInfo(rows, "work_definitions", new[]
        {
            ("eventId", "Stable runtime work identifier."), ("workId", "Authoring definition identifier."),
            ("title", "Player-facing work title."), ("kind", "Work category."), ("subsystem", "Owning subsystem."),
            ("importance", "Importance score."), ("volume", "Work volume."), ("urgency", "Urgency score."),
            ("severity", "Failure severity."), ("ttlSec", "Time-to-live in seconds."), ("status", "Starting status."),
            ("latentRisk", "Starting hidden risk."), ("mismatchScore", "Starting assignment mismatch."),
            ("assignedPersonnel", "Pipe-separated assigned personnel IDs."),
            ("physicalCost", "Physical execution cost."), ("mentalCost", "Mental execution cost."),
            ("baseSuccessChance", "Base success chance from 0 to 100."),
            ("recommendedPersonnelCount", "Recommended team size."), ("minPersonnelCount", "Minimum team size."),
            ("maxPersonnelCount", "Maximum team size."), ("concurrentLimit", "Concurrent execution limit."),
            ("concurrentSlotCost", "Concurrent slot consumption."), ("splitPenalty", "Penalty for splitting work."),
            ("soloPenalty", "Penalty for solo work."), ("tags", "Pipe-separated work tags."),
            ("perkTags", "Pipe-separated perk interaction tags."), ("cardHooks", "Pipe-separated card hooks."),
            ("bossReactionTags", "Pipe-separated boss reaction tags."), ("memoryHooks", "Pipe-separated memory hooks."),
            ("visibleSummary", "Player-facing summary."), ("hiddenFacts", "Pipe-separated hidden facts."),
            ("perkInteractionInfo", "Readable perk interaction note."), ("projectId", "Owning project ID."),
            ("tier", "Main or Sub project role."), ("parentEventId", "Parent work event ID."),
            ("rootEventId", "Root work event ID."), ("triggerReason", "Reason this work was generated."),
            ("initiallyQueued", "TRUE if this row starts in Day 1 queue; FALSE if it is a generation template only.")
        });
        AddInfo(rows, "work_details", new[]
        {
            ("eventId", "Work event owning this single detail row."),
            ("observation", "Required observation aptitude."), ("dexterity", "Required dexterity aptitude."),
            ("boldness", "Required boldness aptitude."), ("intuition", "Required intuition aptitude."),
            ("logic", "Required logic aptitude."), ("truthFramesJson", "All truth frames for this work."),
            ("logsJson", "All visible logs for this work.")
        });
        AddInfo(rows, "work_outcome_events", new[]
        {
            ("sourceWorkId", "Work definition that can generate the follow-up."),
            ("targetWorkId", "Follow-up work definition to generate."),
            ("minOutcomeScore", "Minimum source outcome score."),
            ("maxOutcomeScore", "Maximum source outcome score."),
            ("minLatentRisk", "Minimum source latent-risk threshold."),
            ("chancePercent", "Deterministic generation chance from 0 to 100."),
            ("relation", "How the generated work relates to the source project."),
            ("reason", "Readable generation reason."), ("notes", "Additional authoring note.")
        });
        AddInfo(rows, "scenarios", new[]
        {
            ("eventId", "Stable scenario identifier."), ("timing", "Loop timing when the scenario is eligible."),
            ("priority", "Candidate ordering priority."), ("playbackStateKey", "Replay-state storage key."),
            ("triggerMode", "LoopBoundary, Explicit, or Both."), ("textTableId", "Localized text table identifier."),
            ("oneShot", "Whether completion blocks normal replay."), ("cooldownDays", "Days before replay is allowed."),
            ("allowReplayInDebug", "Whether debug mode bypasses replay restrictions.")
        });
        AddInfo(rows, "scenario_details", new[]
        {
            ("scenarioId", "Scenario owning this row."), ("rowType", "SCENARIO, LINE, or CHOICE."),
            ("rowId", "Line or choice identifier."), ("parentLineId", "Parent line for choices and choice details."),
            ("kind", "Scenario line kind."), ("speakerId", "Speaking personnel ID."),
            ("portraitIds", "Pipe-separated visible portrait IDs."), ("textKey", "Localized line text key."),
            ("expressionKey", "Portrait expression key."), ("poseKey", "Portrait pose key."),
            ("voiceToneKey", "Voice tone key."), ("choiceLabelTextKey", "Localized choice label key."),
            ("jumpToLineId", "Target line ID; blank continues downward."),
            ("allowedExplicitLocations", "Pipe-separated explicit locations for the SCENARIO row."),
            ("triggerConditionsJson", "All trigger conditions for the SCENARIO row."),
            ("entryCostsJson", "All entry costs for the SCENARIO row."),
            ("exitEffectsJson", "All exit effects for the SCENARIO row."),
            ("stageCommandsJson", "All stage commands for a LINE row."),
            ("effectsJson", "Line effects for LINE rows or choice effects for CHOICE rows."),
            ("visibleConditionsJson", "All visible conditions for a CHOICE row."),
            ("costsJson", "All costs for a CHOICE row.")
        });

        return SpreadsheetCsv.ToCsv(
            new[] { "sheetName", "columnName", "required", "description", "example" },
            rows);
    }

    private static void AddInfo(
        ICollection<IReadOnlyList<string>> rows,
        string sheetName,
        IEnumerable<(string Column, string Description)> columns)
    {
        foreach (var column in columns)
        {
            rows.Add(Row(
                sheetName,
                column.Column,
                IsRequiredInfoColumn(column.Column) ? "YES" : "NO",
                column.Description,
                ""));
        }
    }

    private static bool IsRequiredInfoColumn(string columnName)
    {
        return columnName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            || columnName is "Key" or "sheetName" or "columnName"
            || columnName.Equals("rowType", StringComparison.OrdinalIgnoreCase)
            || columnName.Equals("value", StringComparison.OrdinalIgnoreCase);
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
