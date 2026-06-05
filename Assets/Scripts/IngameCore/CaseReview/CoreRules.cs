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

public sealed class DefaultCardDrawService : ICardDrawService
{
    public List<ActionCard> DrawMorningCards(GameState state)
    {
        var cards = new List<ActionCard>();
        foreach (var person in state.Staff.Where(s => !s.HasLeft).OrderBy(s => s.Id))
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
            Tags = new List<string> { "work", "baseline" }
        });

        person.Deck.Add(new ActionCard
        {
            Id = $"{person.Id}:shortcut",
            OwnerPersonnelId = person.Id,
            Title = "Shortcut",
            Summary = "Improves visible speed while raising hidden risk.",
            Tags = new List<string> { "speed", "risk" },
            OutcomeModifier = 4,
            RiskModifier = 8
        });

        person.Deck.Add(new ActionCard
        {
            Id = $"{person.Id}:paper-trail",
            OwnerPersonnelId = person.Id,
            Title = "Paper Trail",
            Summary = "Leaves better evidence but costs more review attention.",
            Tags = new List<string> { "paperwork", "review" },
            RiskModifier = -5,
            ReviewCostModifier = 2
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
            ReviewCostModifier = source.ReviewCostModifier
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
}
