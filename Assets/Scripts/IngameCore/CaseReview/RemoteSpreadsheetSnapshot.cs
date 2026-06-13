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
        "cards",
        "characters",
        "scenarios"
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
        var staff = ParseCharacters(datasets["characters"], cards);
        var initialData = ParseWorkData(datasets["work_definitions"]);
        initialData.Staff = staff;
        var scenarios = ParseScenarios(datasets["scenarios"], textTable);

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

    private static List<Personnel> ParseCharacters(string csv, IReadOnlyDictionary<string, ActionCard> cards)
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
                Aptitudes = ParseIntMap(row.Value("aptitudesJson")),
                PhysicalEnergy = row.Int("physicalEnergy", 100),
                MentalStress = row.Int("mentalStress"),
                LoadAssigned = row.Int("loadAssigned"),
                Fatigue = row.Int("fatigue"),
                Stagnation = row.Int("stagnation"),
                TrustToManager = row.Int("trustToManager"),
                RetentionRisk = row.Int("retentionRisk"),
                HasLeft = row.Bool("hasLeft"),
                DaysSinceJoined = row.Int("daysSinceJoined"),
                OptLow = row.Int("optLow"),
                OptHigh = row.Int("optHigh"),
                MaxLoad = row.Int("maxLoad"),
                ConnectionLimit = row.Int("connectionLimit", 3),
                CloneVersion = row.Int("cloneVersion", 1),
                RegenerationCount = row.Int("regenerationCount"),
                RegeneratedFromId = row.Value("regeneratedFromId"),
                Perks = ParseJsonList<PersonnelPerk>(row.Value("perksJson")),
                Relationships = ParseJsonList<PersonnelRelationship>(row.Value("relationshipsJson")),
                Memories = ParseJsonList<PersonnelMemory>(row.Value("memoriesJson")),
                TraitSamples = ParseJsonList<PersonnelTraitSample>(row.Value("traitSamplesJson"))
            };

            foreach (var cardId in ParsePipeList(row.Value("startingDeckIds")))
            {
                if (!cards.TryGetValue(cardId, out var source))
                {
                    throw new FormatException($"Character '{id}' references missing card '{cardId}'.");
                }

                person.Deck.Add(CloneCard(source, id));
            }

            staff.Add(person);
        }

        return staff;
    }

    private static CaseReviewSeedData ParseWorkData(string csv)
    {
        var table = CsvTable.Read(csv);
        var data = new CaseReviewSeedData();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var id = row.Required("eventId");
            if (!ids.Add(id))
            {
                throw new FormatException($"Duplicate eventId '{id}'.");
            }

            data.Queue.Add(new EventCase
            {
                Id = id,
                DefinitionId = row.Value("workId"),
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
                RequiredAptitudes = ParseIntMap(row.Value("requiredAptitudes")),
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
                PerkInteractionInfo = row.Value("perkInteractionInfo")
            });

            data.TruthFrames.AddRange(ParseJsonList<TruthFrame>(row.Value("truthFramesJson")));
            data.Logs.AddRange(ParseJsonList<VisibleLog>(row.Value("logsJson")));
        }

        return data;
    }

    private static List<RemoteScenarioEventDefinition> ParseScenarios(string csv, LocalizedTextTable textTable)
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

            scenarios.Add(new RemoteScenarioEventDefinition
            {
                EventId = id,
                Timing = row.Enum("timing", ScenarioTiming.Morning),
                Priority = row.Int("priority"),
                PlaybackStateKey = string.IsNullOrWhiteSpace(row.Value("playbackStateKey")) ? id : row.Value("playbackStateKey"),
                TriggerMode = row.Enum("triggerMode", ScenarioTriggerMode.LoopBoundary),
                AllowedExplicitLocations = ParseJsonList<ScenarioExplicitLocation>(row.Value("allowedExplicitLocationsJson")),
                TriggerConditions = ParseJsonList<ScenarioCondition>(row.Value("triggerConditionsJson")),
                EntryCosts = ParseJsonList<ScenarioStateEffect>(row.Value("entryCostsJson")),
                ExitEffects = ParseJsonList<ScenarioStateEffect>(row.Value("exitEffectsJson")),
                TextTable = textTable,
                Lines = ParseJsonList<ScenarioScriptLine>(row.Value("linesJson")),
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

    private static List<string> ParsePipeList(string value)
    {
        return (value ?? "")
            .Split('|')
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
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
