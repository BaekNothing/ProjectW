using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
[CreateAssetMenu(menuName = "ProjectW/Case Review/Work Definition", fileName = "WorkDefinition")]
public sealed class WorkDefinition : ScriptableObject, IRenderableData
{
    [SerializeField] private string workId = "";
    [SerializeField] private string projectId = "";
    [SerializeField] private WorkTier tier = WorkTier.Sub;
    [SerializeField] private string title = "";
    [SerializeField] private RenderResourceDefinition renderResources;
    [SerializeField] private string kind = "routine";
    [SerializeField] private string subsystem = "GENERAL";
    [SerializeField] private int importance = 30;
    [SerializeField] private int volume = 10;
    [SerializeField] private int risk = 10;
    [SerializeField] private int latentRisk = 10;
    [SerializeField] private int urgency = 30;
    [SerializeField] private int ttlSec = 180;
    [SerializeField] private int baseSuccessChance = 55;
    [SerializeField] private int mismatchScore;
    [SerializeField] private int physicalCost = 5;
    [SerializeField] private int mentalCost = 5;
    [SerializeField] private List<WorkAptitudeRequirement> requiredAptitudes = new();
    [SerializeField] private int recommendedPersonnelCount = 1;
    [SerializeField] private int minPersonnelCount = 1;
    [SerializeField] private int maxPersonnelCount = 2;
    [SerializeField] private int concurrentLimit = 1;
    [SerializeField] private int concurrentSlotCost = 1;
    [SerializeField] private int splitPenalty;
    [SerializeField] private int soloPenalty;
    [SerializeField] private List<string> tags = new();
    [SerializeField] private List<string> perkTags = new();
    [SerializeField] private List<string> cardHooks = new();
    [SerializeField] private List<string> bossReactionTags = new();
    [SerializeField] private List<string> memoryHooks = new();
    [TextArea(2, 5)] [SerializeField] private string visibleSummary = "";
    [SerializeField] private List<string> hiddenFacts = new();
    [SerializeField] private int injuryChancePercent;
    [SerializeField] private PersonnelInjuryKind injuryKind = PersonnelInjuryKind.CriticalInjury;
    [SerializeField] private int injurySeverity = 50;
    [SerializeField] private string injuryAffectedAptitude = "";
    [SerializeField] private int injuryAptitudePenalty = 1;
    [SerializeField] private int injuryMaxLoadPenalty;
    [SerializeField] private string permanentDisabilityPerkId = "";
    [TextArea(2, 5)] [SerializeField] private string perkInteractionInfo = "";
    [SerializeField] private WorkSpawnProfile spawnProfile = new();
    [SerializeField] private List<WorkOutcomeEventLink> outcomeEvents = new();

    public string WorkId => workId;
    public string ProjectId => projectId;
    public WorkTier Tier => tier;
    public string Title => title;
    public RenderResourceDefinition RenderResources => renderResources;
    public string Kind => kind;
    public string Subsystem => subsystem;
    public int Importance => importance;
    public int Volume => volume;
    public int Risk => risk;
    public int LatentRisk => latentRisk;
    public int Urgency => urgency;
    public int BaseSpawnWeight => spawnProfile.BaseSpawnWeight;
    public IReadOnlyList<string> Tags => tags;
    public WorkSpawnProfile SpawnProfile => spawnProfile;
    public IReadOnlyList<WorkOutcomeEventLink> OutcomeEvents => outcomeEvents;

    public static WorkDefinition CreateRuntime(EventCase source, IEnumerable<WorkOutcomeEventLink> links = null)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var definition = CreateInstance<WorkDefinition>();
        definition.workId = string.IsNullOrWhiteSpace(source.DefinitionId) ? source.Id : source.DefinitionId;
        definition.projectId = source.ProjectId;
        definition.tier = source.Tier;
        definition.title = source.Title;
        definition.kind = source.Kind;
        definition.subsystem = source.Subsystem;
        definition.importance = source.Importance;
        definition.volume = source.Volume;
        definition.risk = Math.Max(0, (source.Severity - source.Importance) * 2);
        definition.latentRisk = source.LatentRisk;
        definition.urgency = source.Urgency;
        definition.ttlSec = source.TtlSec;
        definition.baseSuccessChance = source.BaseSuccessChance;
        definition.mismatchScore = source.MismatchScore;
        definition.physicalCost = source.PhysicalCost;
        definition.mentalCost = source.MentalCost;
        definition.requiredAptitudes = source.RequiredAptitudes
            .Select(pair => new WorkAptitudeRequirement { Key = pair.Key, Value = pair.Value })
            .ToList();
        definition.recommendedPersonnelCount = source.RecommendedPersonnelCount;
        definition.minPersonnelCount = source.MinPersonnelCount;
        definition.maxPersonnelCount = source.MaxPersonnelCount;
        definition.concurrentLimit = source.ConcurrentLimit;
        definition.concurrentSlotCost = source.ConcurrentSlotCost;
        definition.splitPenalty = source.SplitPenalty;
        definition.soloPenalty = source.SoloPenalty;
        definition.tags = new List<string>(source.Tags ?? new List<string>());
        definition.perkTags = new List<string>(source.PerkTags ?? new List<string>());
        definition.cardHooks = new List<string>(source.CardHooks ?? new List<string>());
        definition.bossReactionTags = new List<string>(source.BossReactionTags ?? new List<string>());
        definition.memoryHooks = new List<string>(source.MemoryHooks ?? new List<string>());
        definition.visibleSummary = source.VisibleSummary;
        definition.hiddenFacts = new List<string>(source.HiddenFacts ?? new List<string>());
        definition.injuryChancePercent = source.InjuryChancePercent;
        definition.injuryKind = source.InjuryKind;
        definition.injurySeverity = source.InjurySeverity;
        definition.injuryAffectedAptitude = source.InjuryAffectedAptitude;
        definition.injuryAptitudePenalty = source.InjuryAptitudePenalty;
        definition.injuryMaxLoadPenalty = source.InjuryMaxLoadPenalty;
        definition.permanentDisabilityPerkId = source.PermanentDisabilityPerkId;
        definition.perkInteractionInfo = source.PerkInteractionInfo;
        definition.outcomeEvents = (links ?? Array.Empty<WorkOutcomeEventLink>()).ToList();
        return definition;
    }

    public int EvaluateSpawnWeight(WorkGenerationContext context)
    {
        return spawnProfile.Evaluate(context);
    }

    public EventCase CreateInstance(WorkGenerationContext context, int sequence)
    {
        var difficulty = Math.Max(0, context?.Difficulty ?? 0);
        var conditionPressure = Math.Max(0, context?.ConditionPressure ?? 0);
        var riskBonus = difficulty * 2 + conditionPressure / 10;
        var volumeBonus = difficulty + Math.Max(0, context?.TalentShortage ?? 0) / 25;
        var generatedId = $"{WorkGenerationSystem.PrefixFor(kind)}-{Math.Max(1, context?.Day ?? 1):D2}{sequence:D2}";

        return new EventCase
        {
            Id = generatedId,
            DefinitionId = workId,
            ProjectId = string.IsNullOrWhiteSpace(projectId) ? workId : projectId,
            Tier = tier,
            Kind = kind,
            Title = title,
            Subsystem = subsystem,
            Importance = Clamp(importance + difficulty, 0, 100),
            Volume = Clamp(volume + volumeBonus, 0, 100),
            Urgency = Clamp(urgency + difficulty * 2, 0, 100),
            Severity = Clamp(importance + risk / 2 + difficulty, 0, 100),
            TtlSec = ttlSec,
            LatentRisk = Clamp(latentRisk + riskBonus, 0, 100),
            MismatchScore = Clamp(mismatchScore + difficulty / 3, 0, 10),
            PhysicalCost = Math.Max(0, physicalCost + volumeBonus),
            MentalCost = Math.Max(0, mentalCost + difficulty),
            BaseSuccessChance = Clamp(baseSuccessChance - difficulty * 2, 5, 95),
            RequiredAptitudes = requiredAptitudes.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase),
            RecommendedPersonnelCount = Math.Max(1, recommendedPersonnelCount),
            MinPersonnelCount = Math.Max(0, minPersonnelCount),
            MaxPersonnelCount = Math.Max(Math.Max(1, recommendedPersonnelCount), maxPersonnelCount),
            ConcurrentLimit = Math.Max(1, concurrentLimit),
            ConcurrentSlotCost = Math.Max(1, concurrentSlotCost),
            SplitPenalty = splitPenalty,
            SoloPenalty = soloPenalty,
            Tags = new List<string>(tags),
            PerkTags = perkTags.Count > 0 ? new List<string>(perkTags) : new List<string>(tags),
            CardHooks = new List<string>(cardHooks),
            BossReactionTags = new List<string>(bossReactionTags),
            MemoryHooks = new List<string>(memoryHooks),
            VisibleSummary = visibleSummary,
            HiddenFacts = new List<string>(hiddenFacts),
            InjuryChancePercent = injuryChancePercent,
            InjuryKind = injuryKind,
            InjurySeverity = injurySeverity,
            InjuryAffectedAptitude = injuryAffectedAptitude,
            InjuryAptitudePenalty = injuryAptitudePenalty,
            InjuryMaxLoadPenalty = injuryMaxLoadPenalty,
            PermanentDisabilityPerkId = permanentDisabilityPerkId,
            PerkInteractionInfo = perkInteractionInfo
        };
    }

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
}

[Serializable]
public sealed class WorkOutcomeEventLink
{
    [SerializeField] private string targetWorkId = "";
    [SerializeField] private int minOutcomeScore;
    [SerializeField] private int maxOutcomeScore = 100;
    [SerializeField] private int minLatentRisk;
    [Range(0, 100)] [SerializeField] private int chancePercent = 100;
    [SerializeField] private WorkOutcomeRelation relation = WorkOutcomeRelation.Consequence;
    [TextArea(1, 3)] [SerializeField] private string reason = "";

    public string TargetWorkId => targetWorkId;
    public int MinOutcomeScore => minOutcomeScore;
    public int MaxOutcomeScore => maxOutcomeScore;
    public int MinLatentRisk => minLatentRisk;
    public int ChancePercent => chancePercent;
    public WorkOutcomeRelation Relation => relation;
    public string Reason => reason;

    public static WorkOutcomeEventLink CreateRuntime(
        string targetWorkId,
        int minOutcomeScore,
        int maxOutcomeScore,
        int minLatentRisk,
        int chancePercent,
        WorkOutcomeRelation relation,
        string reason)
    {
        return new WorkOutcomeEventLink
        {
            targetWorkId = targetWorkId ?? "",
            minOutcomeScore = minOutcomeScore,
            maxOutcomeScore = maxOutcomeScore,
            minLatentRisk = minLatentRisk,
            chancePercent = Math.Max(0, Math.Min(100, chancePercent)),
            relation = relation,
            reason = reason ?? ""
        };
    }

    public bool Matches(EventCase source)
    {
        return source != null
            && source.OutcomeScore >= minOutcomeScore
            && source.OutcomeScore <= maxOutcomeScore
            && source.LatentRisk >= minLatentRisk;
    }
}

[Serializable]
public enum WorkOutcomeRelation
{
    Trigger,
    Transition,
    Consequence
}

[Serializable]
public sealed class WorkAptitudeRequirement
{
    public string Key = "";
    [Range(0, 10)] public int Value;
}

[Serializable]
public sealed class WorkSpawnProfile
{
    [SerializeField] private int baseSpawnWeight = 10;
    [SerializeField] private int minDay = 1;
    [SerializeField] private int maxDay = 999;
    [SerializeField] private int minDifficulty;
    [SerializeField] private int maxDifficulty = 999;
    [SerializeField] private List<WorkDifficultyWeight> difficultyWeights = new();
    [SerializeField] private List<WorkBossWeight> bossWeights = new();
    [SerializeField] private List<WorkConditionWeight> conditionWeights = new();
    [SerializeField] private int cooldownDays;

    public int BaseSpawnWeight => baseSpawnWeight;
    public int CooldownDays => cooldownDays;

    public int Evaluate(WorkGenerationContext context)
    {
        if (context == null)
        {
            return Math.Max(0, baseSpawnWeight);
        }

        if (context.Day < minDay || context.Day > maxDay)
        {
            return 0;
        }

        if (context.Difficulty < minDifficulty || context.Difficulty > maxDifficulty)
        {
            return 0;
        }

        var weight = Math.Max(0, baseSpawnWeight);
        weight += difficultyWeights.Where(w => w.Applies(context.Difficulty)).Sum(w => w.WeightDelta);
        weight += bossWeights.Where(w => w.Boss == context.BossArchetype).Sum(w => w.WeightDelta);
        weight += conditionWeights.Where(w => w.Applies(context)).Sum(w => w.WeightDelta);
        return Math.Max(0, weight);
    }
}

[Serializable]
public sealed class WorkDifficultyWeight
{
    public int MinDifficulty;
    public int MaxDifficulty = 999;
    public int WeightDelta;

    public bool Applies(int difficulty) => difficulty >= MinDifficulty && difficulty <= MaxDifficulty;
}

[Serializable]
public sealed class WorkBossWeight
{
    public BossArchetype Boss = BossArchetype.CompetentOperator;
    public int WeightDelta;
}

[Serializable]
public sealed class WorkConditionWeight
{
    public WorkConditionKey Key;
    public int Threshold;
    public int WeightDelta;

    public bool Applies(WorkGenerationContext context)
    {
        var value = Key switch
        {
            WorkConditionKey.GlobalLatentRisk => context.GlobalLatentRisk,
            WorkConditionKey.TalentShortage => context.TalentShortage,
            WorkConditionKey.ReplacementPressure => context.ReplacementPressure,
            WorkConditionKey.PreviousFailures => context.PreviousFailures,
            WorkConditionKey.UnreviewedReports => context.UnreviewedReports,
            WorkConditionKey.CloneBayPressure => context.CloneBayPressure,
            _ => 0
        };

        return value >= Threshold;
    }
}

public enum WorkConditionKey
{
    GlobalLatentRisk,
    TalentShortage,
    ReplacementPressure,
    PreviousFailures,
    UnreviewedReports,
    CloneBayPressure
}
}
