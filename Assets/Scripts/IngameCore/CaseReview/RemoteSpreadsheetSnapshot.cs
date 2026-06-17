using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
public sealed class RemoteSpreadsheetSnapshot
{
    public CaseReviewSeedData InitialData { get; set; } = new();
    public List<LocalizedTextEntry> LocalizedTextEntries { get; set; } = new();
    public List<IScenarioEventDefinition> Scenarios { get; set; } = new();

    public GameConfig CreateGameConfig()
    {
        return new GameConfig { InitialData = InitialData };
    }
}

public sealed class RemoteScenarioEventDefinition : IScenarioEventDefinition
{
    public string EventId { get; set; } = "";
    public ScenarioTiming Timing { get; set; }
    public int Priority { get; set; }
    public string PlaybackStateKey { get; set; } = "";
    public ScenarioTriggerMode TriggerMode { get; set; }
    public IReadOnlyList<ScenarioExplicitLocation> AllowedExplicitLocations { get; set; } = Array.Empty<ScenarioExplicitLocation>();
    public IReadOnlyList<ScenarioCondition> TriggerConditions { get; set; } = Array.Empty<ScenarioCondition>();
    public IReadOnlyList<ScenarioStateEffect> EntryCosts { get; set; } = Array.Empty<ScenarioStateEffect>();
    public IReadOnlyList<ScenarioStateEffect> ExitEffects { get; set; } = Array.Empty<ScenarioStateEffect>();
    public ScenarioReplayPolicy ReplayPolicy { get; set; } = new();
    public LocalizedTextTable TextTable { get; set; }
    public IReadOnlyList<ScenarioScriptLine> Lines { get; set; } = Array.Empty<ScenarioScriptLine>();

    public ScenarioResolvedLine ResolveLine(int index, string languageKey, string countryCode = "")
    {
        if (index < 0 || index >= Lines.Count)
        {
            return new ScenarioResolvedLine(null, "");
        }

        var line = Lines[index];
        var text = TextTable != null ? TextTable.GetText(line.TextKey, languageKey, countryCode) : line.TextKey;
        return new ScenarioResolvedLine(line, text);
    }
}

public static class RemoteSpreadsheetSnapshotParser
{
    public static readonly string[] RequiredDatasetIds =
    {
        "localized_text",
        "work_definitions",
        "truth_actions",
        "work_details",
        "work_outcome_events",
        "cards",
        "perks",
        "characters",
        "character_details",
        "scenarios",
        "scenario_details"
    };

    public static RemoteSpreadsheetSnapshot Parse(IReadOnlyDictionary<string, string> datasets)
    {
        foreach (var datasetId in RequiredDatasetIds)
        {
            if (!datasets.TryGetValue(datasetId, out var csv) || string.IsNullOrWhiteSpace(csv))
            {
                throw new FormatException($"Required replacement dataset '{datasetId}' is missing.");
            }
        }

        var localizedEntries = LocalizedTextCsv.FromCsv(datasets["localized_text"]);
        var textTable = ScriptableObject.CreateInstance<LocalizedTextTable>();
        textTable.ReplaceEntries(localizedEntries);
        var cards = ParseCards(datasets["cards"]);
        var perks = ParsePerks(datasets["perks"]);
        var characterDetails = ParseCharacterDetails(datasets["character_details"]);
        var staff = ParseCharacters(datasets["characters"], characterDetails, cards, perks);
        var truthActions = ParseTruthActions(datasets["truth_actions"]);
        var workDetails = ParseWorkDetails(datasets["work_details"]);
        var outcomeEvents = ParseWorkOutcomeEvents(datasets["work_outcome_events"]);
        var initialData = ParseWorkData(datasets["work_definitions"], workDetails, outcomeEvents);
        initialData.Staff = staff;
        initialData.TruthActions = truthActions;
        var scenarioDetails = ParseScenarioDetails(datasets["scenario_details"]);
        var scenarios = ParseScenarios(datasets["scenarios"], textTable, scenarioDetails);
        var knownScenarioIds = new HashSet<string>(
            scenarios.Select(scenario => scenario.EventId),
            StringComparer.OrdinalIgnoreCase);
        var orphanScenarioId = scenarioDetails.Keys.FirstOrDefault(id => !knownScenarioIds.Contains(id));
        if (!string.IsNullOrWhiteSpace(orphanScenarioId))
        {
            throw new FormatException($"Scenario details reference missing scenario '{orphanScenarioId}'.");
        }

        if (staff.Count == 0)
        {
            throw new FormatException("Replacement snapshot must contain at least one character.");
        }

        if (initialData.Queue.Count == 0)
        {
            throw new FormatException("Replacement snapshot must contain at least one work item.");
        }

        if (scenarios.Count == 0)
        {
            throw new FormatException("Replacement snapshot must contain at least one scenario.");
        }

        return new RemoteSpreadsheetSnapshot
        {
            InitialData = initialData,
            LocalizedTextEntries = localizedEntries,
            Scenarios = scenarios.Cast<IScenarioEventDefinition>().ToList()
        };
    }

    private static Dictionary<string, ActionCard> ParseCards(string csv)
    {
        var table = CsvTable.Read(csv);
        var cards = new Dictionary<string, ActionCard>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var id = row.Required("cardId");
            if (cards.ContainsKey(id))
            {
                throw new FormatException($"Duplicate cardId '{id}'.");
            }

            cards[id] = new ActionCard
            {
                Id = id,
                Title = row.Required("title"),
                Summary = row.Value("visibleSummary"),
                Tags = ParsePipeList(row.Value("tags")),
                OutcomeModifier = row.Int("outcomeModifier"),
                RiskModifier = row.Int("riskModifier"),
                ReviewCostModifier = row.Int("reviewCostModifier"),
                CriticalChancePercent = row.Int("criticalChancePercent", 10),
                CriticalMultiplier = row.Float("criticalMultiplier", 1.5f)
            };
        }

        return cards;
    }

    private static Dictionary<string, PersonnelPerk> ParsePerks(string csv)
    {
        var table = CsvTable.Read(csv);
        var perks = new Dictionary<string, PersonnelPerk>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var id = row.Required("perkId");
            if (perks.ContainsKey(id))
            {
                throw new FormatException($"Duplicate perkId '{id}'.");
            }

            perks[id] = new PersonnelPerk
            {
                Id = id,
                Name = row.Required("title"),
                TriggerTags = ParsePipeList(row.Value("triggerTags")),
                AptitudeModifiers = ParseIntMap(row.Value("aptitudeModifiersJson")),
                OutcomeModifier = row.Int("outcomeModifier"),
                PhysicalCostModifier = row.Int("physicalCostModifier"),
                MentalCostModifier = row.Int("mentalCostModifier"),
                ClonePersistent = row.Bool("clonePersistent"),
                Note = row.Value("note")
            };
        }

        return perks;
    }

    private static List<Personnel> ParseCharacters(
        string csv,
        IReadOnlyDictionary<string, CharacterDetailData> details,
        IReadOnlyDictionary<string, ActionCard> cards,
        IReadOnlyDictionary<string, PersonnelPerk> perks)
    {
        var table = CsvTable.Read(csv);
        var staff = new List<Personnel>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var id = row.Required("personnelId");
            if (!ids.Add(id))
            {
                throw new FormatException($"Duplicate personnelId '{id}'.");
            }

            var detail = details.TryGetValue(id, out var parsedDetail) ? parsedDetail : new CharacterDetailData();
            var person = new Personnel
            {
                Id = id,
                Name = row.Required("displayName"),
                CloneLineageId = row.Value("cloneLineageId"),
                Background = row.Value("background"),
                Interests = ParsePipeList(row.Value("interests")),
                Personality = row.Value("personality"),
                WorkStyle = row.Value("workStyle"),
                InformationScope = row.Enum("initialInformationScope", AffinityScope.Surface),
                Aptitudes = detail.Aptitudes.Count > 0 ? detail.Aptitudes : ParseIntMap(row.Value("aptitudesJson")),
                PhysicalEnergy = row.Int("basePhysicalEnergy", row.Int("physicalEnergy", 100)),
                MentalStress = row.Int("baseMentalStress", row.Int("mentalStress")),
                LoadAssigned = row.Int("baseLoadAssigned", row.Int("loadAssigned")),
                Fatigue = row.Int("baseFatigue", row.Int("fatigue")),
                Stagnation = row.Int("baseStagnation", row.Int("stagnation")),
                TrustToManager = row.Int("baseTrustToManager", row.Int("trustToManager")),
                RetentionRisk = row.Int("baseRetentionRisk", row.Int("retentionRisk")),
                OptLow = row.Int("optLow"),
                OptHigh = row.Int("optHigh"),
                MaxLoad = row.Int("maxLoad"),
                ConnectionLimit = row.Int("connectionLimit", 3),
            };

            foreach (var cardId in ParsePipeList(row.Value("startingDeckIds")))
            {
                if (!cards.TryGetValue(cardId, out var source))
                {
                    throw new FormatException($"Character '{id}' references missing card '{cardId}'.");
                }

                person.Deck.Add(CloneCard(source, id));
            }

            foreach (var perkId in ParsePipeList(row.Value("startingPerkIds")))
            {
                if (!perks.TryGetValue(perkId, out var source))
                {
                    throw new FormatException($"Character '{id}' references missing perk '{perkId}'.");
                }

                person.Perks.Add(ClonePerk(source));
            }

            staff.Add(person);
        }

        return staff;
    }

    private static Dictionary<string, CharacterDetailData> ParseCharacterDetails(string csv)
    {
        var table = CsvTable.Read(csv);
        var result = new Dictionary<string, CharacterDetailData>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var personnelId = row.Required("personnelId");
            if (!result.TryGetValue(personnelId, out var details))
            {
                details = new CharacterDetailData();
                result[personnelId] = details;
            }

            if (details.HasCompressedRow)
            {
                throw new FormatException($"Duplicate character detail row for personnelId '{personnelId}'.");
            }

            details.HasCompressedRow = true;
            details.Aptitudes = ParseAptitudeColumns(row);
        }

        return result;
    }

    private static Dictionary<string, int> ParseAptitudeColumns(CsvRow row)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "observation", "dexterity", "boldness", "intuition", "logic" })
        {
            if (!string.IsNullOrWhiteSpace(row.Value(key)))
            {
                result[key] = row.Int(key);
            }
        }

        return result;
    }

    private static Dictionary<string, WorkDetailData> ParseWorkDetails(string csv)
    {
        var table = CsvTable.Read(csv);
        var result = new Dictionary<string, WorkDetailData>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var eventId = row.Required("eventId");
            if (!result.TryGetValue(eventId, out var details))
            {
                details = new WorkDetailData();
                result[eventId] = details;
            }

            if (details.HasCompressedRow)
            {
                throw new FormatException($"Duplicate work detail row for eventId '{eventId}'.");
            }

            details.HasCompressedRow = true;
            details.RequiredAptitudes = ParseAptitudeColumns(row);
            details.TruthFrames = ParseJsonList<TruthFrame>(row.Value("truthFramesJson"));
            foreach (var frame in details.TruthFrames)
            {
                frame.EventId = eventId;
            }

        }

        return result;
    }

    private static List<TruthActionDefinition> ParseTruthActions(string csv)
    {
        var table = CsvTable.Read(csv);
        var result = new List<TruthActionDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var actionCode = row.Required("actionCode");
            if (!ids.Add(actionCode))
            {
                throw new FormatException($"Duplicate truth actionCode '{actionCode}'.");
            }

            result.Add(new TruthActionDefinition
            {
                ActionCode = actionCode,
                SourceType = NormalizeSourceType(row.Value("sourceType")),
                VisibleText = row.Required("visibleText"),
                DistortedByMismatch = row.Bool("distortedByMismatch"),
                DelayedByDefault = row.Bool("delayedByDefault"),
                Notes = row.Value("notes")
            });
        }

        return result;
    }

    private static string NormalizeSourceType(string value)
    {
        var sourceType = string.IsNullOrWhiteSpace(value) ? "work" : value.Trim().ToLowerInvariant();
        return sourceType is "summary" or "work" or "equip" or "rel" ? sourceType : "work";
    }

    private static Dictionary<string, List<WorkOutcomeEventLink>> ParseWorkOutcomeEvents(string csv)
    {
        var table = CsvTable.Read(csv);
        var result = new Dictionary<string, List<WorkOutcomeEventLink>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var sourceWorkId = row.Required("sourceWorkId");
            var targetWorkId = row.Required("targetWorkId");
            if (!result.TryGetValue(sourceWorkId, out var links))
            {
                links = new List<WorkOutcomeEventLink>();
                result[sourceWorkId] = links;
            }

            links.Add(WorkOutcomeEventLink.CreateRuntime(
                targetWorkId,
                row.Int("minOutcomeScore"),
                row.Int("maxOutcomeScore", 100),
                row.Int("minLatentRisk"),
                row.Int("chancePercent", 100),
                row.Enum("relation", WorkOutcomeRelation.Consequence),
                row.Value("reason")));
        }

        return result;
    }

    private static CaseReviewSeedData ParseWorkData(
        string csv,
        IReadOnlyDictionary<string, WorkDetailData> details,
        IReadOnlyDictionary<string, List<WorkOutcomeEventLink>> outcomeEvents)
    {
        var table = CsvTable.Read(csv);
        var data = new CaseReviewSeedData();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definitionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var id = row.Required("eventId");
            if (!ids.Add(id))
            {
                throw new FormatException($"Duplicate eventId '{id}'.");
            }

            var detail = details.TryGetValue(id, out var parsedDetail) ? parsedDetail : new WorkDetailData();
            var workId = row.Value("workId");
            if (string.IsNullOrWhiteSpace(workId))
            {
                workId = id;
            }

            var item = new EventCase
            {
                Id = id,
                DefinitionId = workId,
                ProjectId = row.Value("projectId"),
                Tier = row.Enum("tier", WorkTier.Sub),
                ParentEventId = row.Value("parentEventId"),
                RootEventId = row.Value("rootEventId"),
                TriggerReason = row.Value("triggerReason"),
                Kind = row.Required("kind"),
                Title = row.Required("title"),
                Subsystem = row.Required("subsystem"),
                Importance = row.Int("importance"),
                Volume = row.Int("volume"),
                Urgency = row.Int("urgency"),
                Severity = row.Int("severity"),
                TtlSec = row.Int("ttlSec"),
                Status = row.Enum("status", CaseStatus.Open),
                LatentRisk = row.Int("latentRisk"),
                MismatchScore = row.Int("mismatchScore"),
                AssignedPersonnel = ParsePipeList(row.Value("assignedPersonnel")),
                PhysicalCost = row.Int("physicalCost"),
                MentalCost = row.Int("mentalCost"),
                BaseSuccessChance = row.Int("baseSuccessChance", 50),
                RequiredAptitudes = detail.RequiredAptitudes.Count > 0
                    ? detail.RequiredAptitudes
                    : ParseIntMap(row.Value("requiredAptitudes")),
                RecommendedPersonnelCount = row.Int("recommendedPersonnelCount", 1),
                MinPersonnelCount = row.Int("minPersonnelCount", 1),
                MaxPersonnelCount = row.Int("maxPersonnelCount", 2),
                ConcurrentLimit = row.Int("concurrentLimit", 1),
                ConcurrentSlotCost = row.Int("concurrentSlotCost", 1),
                SplitPenalty = row.Int("splitPenalty"),
                SoloPenalty = row.Int("soloPenalty"),
                Tags = ParsePipeList(row.Value("tags")),
                PerkTags = ParsePipeList(row.Value("perkTags")),
                CardHooks = ParsePipeList(row.Value("cardHooks")),
                BossReactionTags = ParsePipeList(row.Value("bossReactionTags")),
                MemoryHooks = ParsePipeList(row.Value("memoryHooks")),
                VisibleSummary = row.Value("visibleSummary"),
                HiddenFacts = ParsePipeList(row.Value("hiddenFacts")),
                InjuryChancePercent = row.Int("injuryChancePercent"),
                InjuryKind = row.Enum("injuryKind", PersonnelInjuryKind.CriticalInjury),
                InjurySeverity = row.Int("injurySeverity", 50),
                InjuryAffectedAptitude = row.Value("injuryAffectedAptitude"),
                InjuryAptitudePenalty = row.Int("injuryAptitudePenalty", 1),
                InjuryMaxLoadPenalty = row.Int("injuryMaxLoadPenalty"),
                PermanentDisabilityPerkId = row.Value("permanentDisabilityPerkId"),
                PerkInteractionInfo = row.Value("perkInteractionInfo")
            };

            if (!definitionIds.Add(workId))
            {
                throw new FormatException($"Duplicate workId '{workId}'.");
            }

            var links = outcomeEvents.TryGetValue(workId, out var parsedLinks)
                ? parsedLinks
                : new List<WorkOutcomeEventLink>();
            data.WorkDefinitions.Add(WorkDefinition.CreateRuntime(item, links));

            if (row.Bool("initiallyQueued", true))
            {
                data.Queue.Add(item);
            }

            data.TruthFrames.AddRange(detail.TruthFrames.Count > 0
                ? detail.TruthFrames
                : ParseJsonList<TruthFrame>(row.Value("truthFramesJson")));
        }

        var missingSource = outcomeEvents.Keys.FirstOrDefault(key => !definitionIds.Contains(key));
        if (!string.IsNullOrWhiteSpace(missingSource))
        {
            throw new FormatException($"Work outcome event sourceWorkId '{missingSource}' has no work definition row.");
        }

        var missingTarget = outcomeEvents
            .SelectMany(pair => pair.Value)
            .Select(link => link.TargetWorkId)
            .FirstOrDefault(targetWorkId => !definitionIds.Contains(targetWorkId));
        if (!string.IsNullOrWhiteSpace(missingTarget))
        {
            throw new FormatException($"Work outcome event targetWorkId '{missingTarget}' has no work definition row.");
        }

        return data;
    }

    private static List<RemoteScenarioEventDefinition> ParseScenarios(
        string csv,
        LocalizedTextTable textTable,
        IReadOnlyDictionary<string, ScenarioDetailData> scenarioDetails)
    {
        var table = CsvTable.Read(csv);
        var scenarios = new List<RemoteScenarioEventDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var id = row.Required("eventId");
            if (!ids.Add(id))
            {
                throw new FormatException($"Duplicate scenario eventId '{id}'.");
            }

            var details = scenarioDetails.TryGetValue(id, out var parsedDetails)
                ? parsedDetails
                : new ScenarioDetailData();
            var lines = details.Lines.Count > 0
                ? details.Lines
                : ParseJsonList<ScenarioScriptLine>(row.Value("linesJson"));
            if (lines.Count == 0)
            {
                throw new FormatException($"Scenario '{id}' must contain at least one detail LINE row.");
            }

            scenarios.Add(new RemoteScenarioEventDefinition
            {
                EventId = id,
                Timing = row.Enum("timing", ScenarioTiming.Morning),
                Priority = row.Int("priority"),
                PlaybackStateKey = string.IsNullOrWhiteSpace(row.Value("playbackStateKey")) ? id : row.Value("playbackStateKey"),
                TriggerMode = row.Enum("triggerMode", ScenarioTriggerMode.LoopBoundary),
                AllowedExplicitLocations = details.AllowedExplicitLocations.Count > 0
                    ? details.AllowedExplicitLocations
                    : ParseJsonList<ScenarioExplicitLocation>(row.Value("allowedExplicitLocationsJson")),
                TriggerConditions = details.TriggerConditions.Count > 0
                    ? details.TriggerConditions
                    : ParseJsonList<ScenarioCondition>(row.Value("triggerConditionsJson")),
                EntryCosts = details.EntryCosts.Count > 0
                    ? details.EntryCosts
                    : ParseJsonList<ScenarioStateEffect>(row.Value("entryCostsJson")),
                ExitEffects = details.ExitEffects.Count > 0
                    ? details.ExitEffects
                    : ParseJsonList<ScenarioStateEffect>(row.Value("exitEffectsJson")),
                TextTable = textTable,
                Lines = lines,
                ReplayPolicy = new ScenarioReplayPolicy
                {
                    OneShot = row.Bool("oneShot", true),
                    CooldownDays = row.Int("cooldownDays"),
                    AllowReplayInDebug = row.Bool("allowReplayInDebug")
                }
            });
        }

        return scenarios;
    }

    private static Dictionary<string, ScenarioDetailData> ParseScenarioDetails(string csv)
    {
        var table = CsvTable.Read(csv);
        var detailsByScenario = new Dictionary<string, ScenarioDetailData>(StringComparer.OrdinalIgnoreCase);
        var linesById = new Dictionary<string, Dictionary<string, ScenarioScriptLine>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in table.Rows)
        {
            var scenarioId = row.Required("scenarioId");
            var rowType = row.Required("rowType");
            if (!detailsByScenario.TryGetValue(scenarioId, out var scenarioDetails))
            {
                scenarioDetails = new ScenarioDetailData();
                detailsByScenario[scenarioId] = scenarioDetails;
                linesById[scenarioId] = new Dictionary<string, ScenarioScriptLine>(StringComparer.OrdinalIgnoreCase);
            }

            if (rowType.Equals("SCENARIO", StringComparison.OrdinalIgnoreCase))
            {
                if (scenarioDetails.HasScenarioRow)
                {
                    throw new FormatException($"Duplicate scenario detail row for scenarioId '{scenarioId}'.");
                }

                scenarioDetails.HasScenarioRow = true;
                scenarioDetails.AllowedExplicitLocations.AddRange(ParseEnumPipeList<ScenarioExplicitLocation>(row.Value("allowedExplicitLocations")));
                scenarioDetails.TriggerConditions.AddRange(ParseJsonList<ScenarioCondition>(row.Value("triggerConditionsJson")));
                scenarioDetails.EntryCosts.AddRange(ParseJsonList<ScenarioStateEffect>(row.Value("entryCostsJson")));
                scenarioDetails.ExitEffects.AddRange(ParseJsonList<ScenarioStateEffect>(row.Value("exitEffectsJson")));
                continue;
            }

            if (rowType.Equals("LINE", StringComparison.OrdinalIgnoreCase))
            {
                var lineId = row.Required("rowId");
                if (linesById[scenarioId].ContainsKey(lineId))
                {
                    throw new FormatException($"Scenario '{scenarioId}' contains duplicate line rowId '{lineId}'.");
                }

                var line = new ScenarioScriptLine
                {
                    LineId = lineId,
                    Kind = row.Enum("kind", ScenarioLineKind.Dialogue),
                    SpeakerId = row.Value("speakerId"),
                    PortraitIds = ParsePipeList(row.Value("portraitIds")),
                    TextKey = row.Value("textKey"),
                    ExpressionKey = row.Value("expressionKey"),
                    PoseKey = row.Value("poseKey"),
                    VoiceToneKey = row.Value("voiceToneKey"),
                    StageCommands = ParseJsonList<ScenarioStageCommand>(row.Value("stageCommandsJson")),
                    Effects = ParseJsonList<ScenarioStateEffect>(row.Value("effectsJson"))
                };
                scenarioDetails.Lines.Add(line);
                linesById[scenarioId][lineId] = line;
                continue;
            }

            if (rowType.Equals("CHOICE", StringComparison.OrdinalIgnoreCase))
            {
                var parentLineId = row.Required("parentLineId");
                if (!linesById[scenarioId].TryGetValue(parentLineId, out var parentLine))
                {
                    throw new FormatException(
                        $"Scenario '{scenarioId}' choice '{row.Value("rowId")}' must appear after parent line '{parentLineId}'.");
                }

                var choiceId = row.Required("rowId");
                if (parentLine.Choices.Any(choice => choice.ChoiceId.Equals(choiceId, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new FormatException($"Scenario '{scenarioId}' line '{parentLineId}' contains duplicate choice rowId '{choiceId}'.");
                }

                parentLine.Choices.Add(new ScenarioChoice
                {
                    ChoiceId = choiceId,
                    LabelTextKey = row.Required("choiceLabelTextKey"),
                    NextLineId = row.Value("jumpToLineId"),
                    VisibleConditions = ParseJsonList<ScenarioCondition>(row.Value("visibleConditionsJson")),
                    Costs = ParseJsonList<ScenarioStateEffect>(row.Value("costsJson")),
                    Effects = ParseJsonList<ScenarioStateEffect>(row.Value("effectsJson"))
                });
                continue;
            }

            throw new FormatException(
                $"Scenario detail row '{scenarioId}/{row.Value("rowId")}' has unsupported rowType '{rowType}'. Use SCENARIO, LINE, or CHOICE.");
        }

        foreach (var scenario in detailsByScenario)
        {
            if (scenario.Value.Lines.Count == 0)
            {
                throw new FormatException($"Scenario '{scenario.Key}' must contain at least one LINE row.");
            }

            foreach (var choice in scenario.Value.Lines.SelectMany(line => line.Choices))
            {
                if (!string.IsNullOrWhiteSpace(choice.NextLineId)
                    && !linesById[scenario.Key].ContainsKey(choice.NextLineId))
                {
                    throw new FormatException(
                        $"Scenario '{scenario.Key}' choice '{choice.ChoiceId}' jumps to missing line '{choice.NextLineId}'.");
                }
            }
        }

        return detailsByScenario;
    }

    private static ActionCard CloneCard(ActionCard source, string ownerId)
    {
        return new ActionCard
        {
            Id = source.Id,
            OwnerPersonnelId = ownerId,
            Title = source.Title,
            Summary = source.Summary,
            Tags = new List<string>(source.Tags),
            OutcomeModifier = source.OutcomeModifier,
            RiskModifier = source.RiskModifier,
            ReviewCostModifier = source.ReviewCostModifier,
            CriticalChancePercent = source.CriticalChancePercent,
            CriticalMultiplier = source.CriticalMultiplier
        };
    }

    private static PersonnelPerk ClonePerk(PersonnelPerk source)
    {
        return new PersonnelPerk
        {
            Id = source.Id,
            Name = source.Name,
            TriggerTags = new List<string>(source.TriggerTags ?? new List<string>()),
            AptitudeModifiers = new Dictionary<string, int>(
                source.AptitudeModifiers ?? new Dictionary<string, int>(),
                StringComparer.OrdinalIgnoreCase),
            OutcomeModifier = source.OutcomeModifier,
            PhysicalCostModifier = source.PhysicalCostModifier,
            MentalCostModifier = source.MentalCostModifier,
            ClonePersistent = source.ClonePersistent,
            Note = source.Note
        };
    }

    private static List<string> ParsePipeList(string value)
    {
        return (value ?? "")
            .Split('|')
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static List<TEnum> ParseEnumPipeList<TEnum>(string value)
        where TEnum : struct
    {
        var result = new List<TEnum>();
        foreach (var item in ParsePipeList(value))
        {
            if (!Enum.TryParse<TEnum>(item, true, out var parsed))
            {
                throw new FormatException($"Unsupported {typeof(TEnum).Name} value '{item}'.");
            }

            result.Add(parsed);
        }

        return result;
    }

    private static Dictionary<string, int> ParseIntMap(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var parsed = JsonConvert.DeserializeObject<Dictionary<string, int>>(value)
            ?? new Dictionary<string, int>();
        return new Dictionary<string, int>(parsed, StringComparer.OrdinalIgnoreCase);
    }

    private static List<T> ParseJsonList<T>(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? new List<T>()
            : JsonConvert.DeserializeObject<List<T>>(value) ?? new List<T>();
    }
}

public sealed class ScenarioDetailData
{
    public bool HasScenarioRow { get; set; }
    public List<ScenarioExplicitLocation> AllowedExplicitLocations { get; set; } = new();
    public List<ScenarioCondition> TriggerConditions { get; set; } = new();
    public List<ScenarioStateEffect> EntryCosts { get; set; } = new();
    public List<ScenarioStateEffect> ExitEffects { get; set; } = new();
    public List<ScenarioScriptLine> Lines { get; set; } = new();
}

public sealed class CharacterDetailData
{
    public bool HasCompressedRow { get; set; }
    public Dictionary<string, int> Aptitudes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorkDetailData
{
    public bool HasCompressedRow { get; set; }
    public Dictionary<string, int> RequiredAptitudes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<TruthFrame> TruthFrames { get; set; } = new();
}

public sealed class CsvTable
{
    private CsvTable(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<CsvRow> Rows { get; }

    public static CsvTable Read(string csv)
    {
        var rows = SpreadsheetCsv.ParseRows(csv);
        if (rows.Count == 0)
        {
            throw new FormatException("CSV is empty.");
        }

        var headers = rows[0].Select(header => header.Trim()).ToList();
        var dataRows = rows.Skip(1)
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .Select(row => new CsvRow(headers, row))
            .ToList();
        return new CsvTable(headers, dataRows);
    }
}

public sealed class CsvRow
{
    private readonly Dictionary<string, string> values;

    public CsvRow(IReadOnlyList<string> headers, IReadOnlyList<string> cells)
    {
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            values[headers[index]] = index < cells.Count ? cells[index] ?? "" : "";
        }
    }

    public string Value(string name)
    {
        return values.TryGetValue(name, out var value) ? value : "";
    }

    public string Required(string name)
    {
        var value = Value(name).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"Required value '{name}' is empty.");
        }

        return value;
    }

    public int Int(string name, int fallback = 0)
    {
        return int.TryParse(Value(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    public float Float(string name, float fallback = 0f)
    {
        return float.TryParse(Value(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    public bool Bool(string name, bool fallback = false)
    {
        var value = Value(name).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value == "1";
    }

    public T Enum<T>(string name, T fallback) where T : struct
    {
        return System.Enum.TryParse(Value(name), true, out T value) ? value : fallback;
    }
}
}
