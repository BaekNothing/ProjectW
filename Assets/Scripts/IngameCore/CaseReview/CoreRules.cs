using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.IngameCore.CaseReview
{
public sealed class CaseReviewRules
{
    private static readonly CaseReviewRules DefaultInstance = new();

    public static CaseReviewRules Default => DefaultInstance;

    public ICardDrawService CardDrawService { get; set; } = new DefaultCardDrawService();
    public IReviewCostPolicy ReviewCostPolicy { get; set; } = new DefaultReviewCostPolicy();
    public IReplacementPressurePolicy ReplacementPressurePolicy { get; set; } = new DefaultReplacementPressurePolicy();
    public IBossPolicy BossPolicy { get; set; } = new DefaultBossPolicy();
    public IWorkGenerationService WorkGenerationService { get; set; } = new DefaultWorkGenerationService();
    public IMeritTokenPolicy MeritTokenPolicy { get; set; } = new DefaultMeritTokenPolicy();
    public IApprovalPolicy ApprovalPolicy { get; set; } = new DefaultApprovalPolicy();
    public ICharacterInjuryPolicy CharacterInjuryPolicy { get; set; } = new DefaultCharacterInjuryPolicy();
}

public interface ICardDrawService
{
    List<ActionCard> DrawMorningCards(GameState state);
}

public interface IReviewCostPolicy
{
    ReviewCostEntry Assess(GameState state, ReviewActionType actionType, string subjectId, string sourceType);
}

public interface IReplacementPressurePolicy
{
    int AfterPlanConfirmed(GameState state, int currentPressure);
    int AfterManualReview(GameState state, ReviewCostEntry cost, int currentPressure);
    int AfterApproval(GameState state, EventCase item, int currentPressure);
}

public interface IBossPolicy
{
    int ReplacementPressureModifier(BossArchetype archetype);
    int ReviewCostModifier(BossArchetype archetype, ReviewActionType actionType);
}

public interface IMeritTokenPolicy
{
    int AwardForResolvedWork(GameState state, EventCase item);
    int AwardForReportReview(GameState state, EventCase item);
}

public interface IApprovalPolicy
{
    int RequiredTokens(ApprovalRequestKind kind);
    ApprovalDecision Evaluate(GameState state, ApprovalRequest request, int submittedTokens);
}

public interface ICharacterInjuryPolicy
{
    List<PersonnelInjury> RollAfterWork(GameState state, EventCase item, IReadOnlyList<Personnel> team, int outcomeScore);
}

public sealed class DefaultCardDrawService : ICardDrawService
{
    public List<ActionCard> DrawMorningCards(GameState state)
    {
        var cards = new List<ActionCard>();
        var cardLimit = Math.Max(0, state.Config?.MorningCardLimit ?? 3);
        foreach (var person in state.Staff
            .Where(s => !s.HasLeft)
            .OrderBy(s => StableHash($"{state.Seed}:{state.Day}:{s.Id}:morning-card"))
            .ThenBy(s => s.Id)
            .Take(cardLimit))
        {
            EnsureDefaultDeck(person);
            if (person.Deck.Count == 0)
            {
                continue;
            }

            var index = Math.Abs(StableHash($"{state.Seed}:{state.Day}:{person.Id}")) % person.Deck.Count;
            cards.Add(CloneCard(person.Deck[index], person.Id));
        }

        return cards;
    }

    private static void EnsureDefaultDeck(Personnel person)
    {
        if (person.Deck.Count > 0)
        {
            return;
        }

        person.Deck.Add(new ActionCard
        {
            Id = $"{person.Id}:steady-work",
            OwnerPersonnelId = person.Id,
            Title = "Steady Work",
            Summary = "Handles assigned work without unusual upside or drama.",
            Tags = new List<string> { "work", "baseline" },
            CriticalChancePercent = 12,
            CriticalMultiplier = 1.5f
        });

        person.Deck.Add(new ActionCard
        {
            Id = $"{person.Id}:shortcut",
            OwnerPersonnelId = person.Id,
            Title = "Shortcut",
            Summary = "Improves visible speed while raising hidden risk.",
            Tags = new List<string> { "speed", "risk" },
            OutcomeModifier = 4,
            RiskModifier = 8,
            CriticalChancePercent = 18,
            CriticalMultiplier = 1.75f
        });

        person.Deck.Add(new ActionCard
        {
            Id = $"{person.Id}:paper-trail",
            OwnerPersonnelId = person.Id,
            Title = "Paper Trail",
            Summary = "Leaves better evidence but costs more review attention.",
            Tags = new List<string> { "paperwork", "review" },
            RiskModifier = -5,
            ReviewCostModifier = 2,
            CriticalChancePercent = 16,
            CriticalMultiplier = 2f
        });
    }

    private static ActionCard CloneCard(ActionCard source, string ownerId)
    {
        return new ActionCard
        {
            Id = source.Id,
            OwnerPersonnelId = string.IsNullOrWhiteSpace(source.OwnerPersonnelId) ? ownerId : source.OwnerPersonnelId,
            TargetEventId = source.TargetEventId,
            Title = source.Title,
            Summary = source.Summary,
            Tags = new List<string>(source.Tags ?? new List<string>()),
            OutcomeModifier = source.OutcomeModifier,
            RiskModifier = source.RiskModifier,
            ReviewCostModifier = source.ReviewCostModifier,
            CriticalChancePercent = source.CriticalChancePercent,
            CriticalMultiplier = source.CriticalMultiplier,
            CriticalTriggered = source.CriticalTriggered,
            CriticalRoll = source.CriticalRoll
        };
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
            {
                hash = hash * 31 + ch;
            }

            return hash;
        }
    }
}

public sealed class DefaultCharacterInjuryPolicy : ICharacterInjuryPolicy
{
    public List<PersonnelInjury> RollAfterWork(GameState state, EventCase item, IReadOnlyList<Personnel> team, int outcomeScore)
    {
        var injuries = new List<PersonnelInjury>();
        if (state is null || item is null || team is null || team.Count == 0)
        {
            return injuries;
        }

        var chance = item.InjuryChancePercent > 0
            ? Math.Min(100, item.InjuryChancePercent)
            : IsDangerousWork(item)
                ? InjuryChancePercent(item, outcomeScore)
                : 0;
        if (chance <= 0)
        {
            return injuries;
        }

        foreach (var person in team.Where(member => member != null && !member.HasLeft))
        {
            var roll = RollPercent($"{state.Seed}:{state.Day}:{item.Id}:{person.Id}:injury");
            if (roll > chance)
            {
                continue;
            }

            var disabilityThreshold = Math.Max(8, chance / 3);
            var kind = item.InjuryChancePercent > 0
                ? item.InjuryKind
                : roll <= disabilityThreshold
                    ? PersonnelInjuryKind.Disability
                    : PersonnelInjuryKind.CriticalInjury;
            injuries.Add(CreateInjury(state, item, person, kind, chance, roll));
        }

        return injuries;
    }

    private static bool IsDangerousWork(EventCase item)
    {
        return item.Severity >= 70
            || item.LatentRisk >= 60
            || item.PhysicalCost >= 12
            || ContainsAny(item.Tags, "injury", "injury-risk", "disability-risk", "danger", "hazard", "radiation", "outdoor", "repair", "critical")
            || ContainsAny(item.CardHooks, "risk", "danger", "hazard");
    }

    private static int InjuryChancePercent(EventCase item, int outcomeScore)
    {
        var chance =
            Math.Max(0, item.Severity - 60) / 2
            + Math.Max(0, item.LatentRisk - 45) / 2
            + Math.Max(0, item.Urgency - 70) / 4
            + Math.Max(0, item.PhysicalCost - 8);

        if (outcomeScore < 55)
        {
            chance += 12;
        }

        if (outcomeScore < 40)
        {
            chance += 12;
        }

        return Math.Min(70, chance);
    }

    private static PersonnelInjury CreateInjury(
        GameState state,
        EventCase item,
        Personnel person,
        PersonnelInjuryKind kind,
        int chance,
        int roll)
    {
        var severity = item.InjurySeverity > 0
            ? Math.Min(100, item.InjurySeverity)
            : kind == PersonnelInjuryKind.Disability
                ? Math.Min(100, 55 + chance)
                : Math.Min(100, 40 + chance);
        var affectedAptitude = string.IsNullOrWhiteSpace(item.InjuryAffectedAptitude)
            ? SelectAffectedAptitude(item)
            : item.InjuryAffectedAptitude;
        return new PersonnelInjury
        {
            Id = $"inj.{state.Day:00}.{item.Id}.{person.Id}.{(person.Injuries?.Count ?? 0) + 1}",
            PersonnelId = person.Id,
            SourceEventId = item.Id,
            DayAcquired = state.Day,
            Kind = kind,
            Label = kind == PersonnelInjuryKind.Disability ? "Disability" : "Critical injury",
            Severity = severity,
            Permanent = kind == PersonnelInjuryKind.Disability,
            AffectedAptitude = affectedAptitude,
            AptitudePenalty = item.InjuryAptitudePenalty > 0
                ? item.InjuryAptitudePenalty
                : kind == PersonnelInjuryKind.Disability ? 2 : 1,
            MaxLoadPenalty = item.InjuryMaxLoadPenalty > 0
                ? item.InjuryMaxLoadPenalty
                : kind == PersonnelInjuryKind.Disability ? 1 : 0,
            Note = $"Rolled {roll} against {chance}% after {item.Id}."
        };
    }

    private static string SelectAffectedAptitude(EventCase item)
    {
        if (item.RequiredAptitudes != null && item.RequiredAptitudes.Count > 0)
        {
            return item.RequiredAptitudes.OrderByDescending(pair => pair.Value).First().Key;
        }

        return item.Subsystem switch
        {
            "O2" => "dexterity",
            "HAB" => "intuition",
            "FOOD" => "logic",
            _ => "observation"
        };
    }

    private static bool ContainsAny(IEnumerable<string> values, params string[] needles)
    {
        return (values ?? Enumerable.Empty<string>()).Any(value =>
            needles.Any(needle => (value ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    private static int RollPercent(string value)
    {
        return Math.Abs(StableHash(value)) % 100 + 1;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value ?? "")
            {
                hash = hash * 31 + ch;
            }

            return hash;
        }
    }
}

public sealed class DefaultReviewCostPolicy : IReviewCostPolicy
{
    public ReviewCostEntry Assess(GameState state, ReviewActionType actionType, string subjectId, string sourceType)
    {
        var baseCost = actionType switch
        {
            ReviewActionType.Plan => (time: 1, resource: 0, focus: 1, trust: 0),
            ReviewActionType.Summary => (time: 1, resource: 0, focus: 1, trust: 0),
            ReviewActionType.Log => (time: 4, resource: 1, focus: 2, trust: 0),
            ReviewActionType.Check => (time: 3, resource: 1, focus: 2, trust: 0),
            ReviewActionType.Report => (time: 3, resource: 0, focus: 2, trust: 0),
            ReviewActionType.Review => (time: 2, resource: 0, focus: 1, trust: 0),
            ReviewActionType.Interview => (time: 6, resource: 1, focus: 3, trust: -1),
            ReviewActionType.BossMemo => (time: 5, resource: 1, focus: 3, trust: 1),
            _ => (time: 1, resource: 0, focus: 1, trust: 0)
        };

        var bossModifier = CaseReviewRules.Default.BossPolicy.ReviewCostModifier(state.BossArchetype, actionType);
        return new ReviewCostEntry
        {
            Day = state.Day,
            AtSec = state.TotalElapsedSec,
            ActionType = actionType,
            SubjectId = subjectId ?? "",
            SourceType = sourceType ?? "",
            TimeCost = Math.Max(0, baseCost.time + bossModifier),
            ResourceCost = Math.Max(0, baseCost.resource),
            FocusCost = Math.Max(0, baseCost.focus),
            TrustCost = baseCost.trust,
            Reason = actionType.ToString()
        };
    }
}

public sealed class DefaultReplacementPressurePolicy : IReplacementPressurePolicy
{
    public int AfterPlanConfirmed(GameState state, int currentPressure)
    {
        var entries = state.MorningPlan?.Entries ?? new List<WorkPlanEntry>();
        if (entries.Count == 0)
        {
            return currentPressure;
        }

        var adjusted = entries.Count(e => e.Adjusted);
        var delta = adjusted == 0 ? 6 : Math.Max(0, 3 - adjusted);
        delta += CaseReviewRules.Default.BossPolicy.ReplacementPressureModifier(state.BossArchetype);
        return Clamp(currentPressure + delta, 0, 100);
    }

    public int AfterManualReview(GameState state, ReviewCostEntry cost, int currentPressure)
    {
        if (cost.ActionType is ReviewActionType.Log or ReviewActionType.Check or ReviewActionType.Report or ReviewActionType.Review)
        {
            return Clamp(currentPressure - 1, 0, 100);
        }

        return currentPressure;
    }

    public int AfterApproval(GameState state, EventCase item, int currentPressure)
    {
        var delta = item.ApprovedFromSummaryOnly ? 8 : -2;
        delta += CaseReviewRules.Default.BossPolicy.ReplacementPressureModifier(state.BossArchetype);
        return Clamp(currentPressure + delta, 0, 100);
    }

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
}

public sealed class DefaultBossPolicy : IBossPolicy
{
    public int ReplacementPressureModifier(BossArchetype archetype)
    {
        return archetype switch
        {
            BossArchetype.RationalAI => 2,
            BossArchetype.TechHipster => 3,
            BossArchetype.PsychoAI => 4,
            BossArchetype.OrdinaryHuman => -1,
            _ => 0
        };
    }

    public int ReviewCostModifier(BossArchetype archetype, ReviewActionType actionType)
    {
        if (archetype == BossArchetype.PsychoAI && actionType is ReviewActionType.Report or ReviewActionType.BossMemo)
        {
            return 2;
        }

        if (archetype == BossArchetype.RationalAI && actionType is ReviewActionType.Log or ReviewActionType.Check)
        {
            return -1;
        }

        return 0;
    }
}

public sealed class DefaultMeritTokenPolicy : IMeritTokenPolicy
{
    public int AwardForResolvedWork(GameState state, EventCase item)
    {
        if (item is null)
        {
            return 0;
        }

        var tokens = 0;
        if (item.OutcomeScore >= 75)
        {
            tokens += 1;
        }

        if (item.OutcomeScore >= 75 && item.Urgency + item.Severity >= 135)
        {
            tokens += 1;
        }

        if (item.OutcomeScore < 55 || item.LatentRisk >= 70)
        {
            tokens += 1;
        }

        return Math.Max(0, tokens);
    }

    public int AwardForReportReview(GameState state, EventCase item)
    {
        if (item is null)
        {
            return 0;
        }

        return item.LatentRisk >= 45 || item.MismatchScore >= 2 || item.OutcomeScore < 60 ? 1 : 0;
    }
}

public sealed class DefaultApprovalPolicy : IApprovalPolicy
{
    public int RequiredTokens(ApprovalRequestKind kind)
    {
        return kind switch
        {
            ApprovalRequestKind.ReportCorrection => 1,
            ApprovalRequestKind.SpecialExpense => 2,
            ApprovalRequestKind.Regeneration => 3,
            ApprovalRequestKind.AuditDefense => 4,
            _ => 1
        };
    }

    public ApprovalDecision Evaluate(GameState state, ApprovalRequest request, int submittedTokens)
    {
        var required = request is not null && request.RequiredTokens > 0
            ? request.RequiredTokens
            : RequiredTokens(request?.Kind ?? ApprovalRequestKind.ReportCorrection);
        var burden = HiddenBurden(state);
        var status = submittedTokens >= required + burden
            ? ApprovalStatus.Approved
            : submittedTokens >= required && burden <= 1
                ? ApprovalStatus.ConditionalApproved
                : ApprovalStatus.Rejected;

        return new ApprovalDecision
        {
            Status = status,
            Hint = HintFor(state)
        };
    }

    private static int HiddenBurden(GameState state)
    {
        if (state is null)
        {
            return 0;
        }

        var burden = 0;
        if (state.ReplacementPressure >= 70) burden++;
        if (state.GlobalLatentRisk >= 120) burden++;
        if (state.Overload >= 70) burden++;
        return burden;
    }

    private static string HintFor(GameState state)
    {
        if (state is null)
        {
            return "review desk unavailable";
        }

        if (state.ReplacementPressure >= state.GlobalLatentRisk / 2 && state.ReplacementPressure >= state.Overload)
        {
            return "AI review hold";
        }

        if (state.GlobalLatentRisk >= state.Overload)
        {
            return "audit line transfer";
        }

        return "operation capacity shortage";
    }
}
}
