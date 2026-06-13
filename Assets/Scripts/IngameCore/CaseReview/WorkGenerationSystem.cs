using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.IngameCore.CaseReview
{
public interface IWorkGenerationService
{
    List<EventCase> Generate(GameState state, WorkGenerationRequest request);
}

public sealed class WorkGenerationRequest
{
    public List<WorkDefinition> Definitions { get; set; } = new();
    public int Count { get; set; } = 1;
    public int Difficulty { get; set; }
    public int SeedOffset { get; set; }
}

public sealed class WorkGenerationContext
{
    public int Seed { get; set; }
    public int Day { get; set; } = 1;
    public int Difficulty { get; set; }
    public BossArchetype BossArchetype { get; set; } = BossArchetype.CompetentOperator;
    public int GlobalLatentRisk { get; set; }
    public int TalentShortage { get; set; }
    public int ReplacementPressure { get; set; }
    public int PreviousFailures { get; set; }
    public int UnreviewedReports { get; set; }
    public int CloneBayPressure { get; set; }

    public int ConditionPressure =>
        Math.Max(0, GlobalLatentRisk)
        + Math.Max(0, TalentShortage)
        + Math.Max(0, ReplacementPressure)
        + Math.Max(0, PreviousFailures * 10)
        + Math.Max(0, UnreviewedReports * 8)
        + Math.Max(0, CloneBayPressure);

    public static WorkGenerationContext FromState(GameState state, int difficulty, int seedOffset = 0)
    {
        return new WorkGenerationContext
        {
            Seed = state.Seed + seedOffset,
            Day = state.Day,
            Difficulty = difficulty,
            BossArchetype = state.BossArchetype,
            GlobalLatentRisk = state.GlobalLatentRisk,
            TalentShortage = state.TalentShortage,
            ReplacementPressure = state.ReplacementPressure,
            PreviousFailures = state.Queue.Count(e => e.AutoResolved && e.OutcomeScore > 0 && e.OutcomeScore < 60),
            UnreviewedReports = state.Queue.Count(e => e.AutoResolved && !e.ReportReviewed),
            CloneBayPressure = state.Staff.Count(e => e.HasLeft) * 15
        };
    }
}

public sealed class DefaultWorkGenerationService : IWorkGenerationService
{
    public List<EventCase> Generate(GameState state, WorkGenerationRequest request)
    {
        var context = WorkGenerationContext.FromState(state, request.Difficulty, request.SeedOffset);
        return WorkGenerationSystem.Generate(request.Definitions, context, request.Count);
    }
}

public static class WorkGenerationSystem
{
    public static List<EventCase> Generate(IReadOnlyList<WorkDefinition> definitions, WorkGenerationContext context, int count)
    {
        var result = new List<EventCase>();
        if (definitions == null || definitions.Count == 0 || count <= 0)
        {
            return result;
        }

        var candidates = definitions
            .Where(definition => definition != null)
            .Select(definition => new WeightedWork(definition, definition.EvaluateSpawnWeight(context)))
            .Where(candidate => candidate.Weight > 0)
            .ToList();

        var rng = new DeterministicRng(context.Seed + context.Day * 997 + context.Difficulty * 131);
        for (var i = 0; i < count && candidates.Count > 0; i++)
        {
            var selected = Pick(candidates, rng);
            result.Add(selected.Definition.CreateInstance(context, i + 1));
            candidates.Remove(selected);
        }

        return result;
    }

    public static string PrefixFor(string kind)
    {
        return (kind ?? "").ToLowerInvariant() switch
        {
            "incident" => "E",
            "complaint" => "C",
            "routine" => "R",
            "audit" => "A",
            "hiring" => "H",
            "clone" => "CL",
            "boss" => "B",
            "ai" => "AI",
            _ => "W"
        };
    }

    private static WeightedWork Pick(IReadOnlyList<WeightedWork> candidates, DeterministicRng rng)
    {
        var total = candidates.Sum(candidate => candidate.Weight);
        var roll = rng.Next(1, total + 1);
        var cursor = 0;
        foreach (var candidate in candidates)
        {
            cursor += candidate.Weight;
            if (roll <= cursor)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private readonly struct WeightedWork
    {
        public WeightedWork(WorkDefinition definition, int weight)
        {
            Definition = definition;
            Weight = weight;
        }

        public WorkDefinition Definition { get; }
        public int Weight { get; }
    }

    private sealed class DeterministicRng
    {
        private int state;

        public DeterministicRng(int seed)
        {
            state = seed == 0 ? 1 : seed;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            unchecked
            {
                state = state * 1103515245 + 12345;
            }

            var range = Math.Max(1, maxExclusive - minInclusive);
            return minInclusive + Math.Abs(state % range);
        }
    }
}

public static class WorkOutcomeEventSystem
{
    public static List<EventCase> Generate(
        EventCase source,
        IReadOnlyList<WorkDefinition> definitions,
        WorkGenerationContext context)
    {
        var result = new List<EventCase>();
        if (source == null || definitions == null || context == null)
        {
            return result;
        }

        var sourceDefinition = definitions.FirstOrDefault(definition =>
            definition != null
            && definition.WorkId.Equals(source.DefinitionId, StringComparison.OrdinalIgnoreCase));
        if (sourceDefinition == null)
        {
            return result;
        }

        var targets = definitions
            .Where(definition => definition != null)
            .GroupBy(definition => definition.WorkId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var sequence = 1;
        foreach (var link in sourceDefinition.OutcomeEvents.Where(link => link != null && link.Matches(source)))
        {
            if (!targets.TryGetValue(link.TargetWorkId, out var target))
            {
                continue;
            }

            if (!PassesChance(source, link, context))
            {
                continue;
            }

            var generated = target.CreateInstance(context, sequence++);
            generated.ProjectId = string.IsNullOrWhiteSpace(source.ProjectId)
                ? generated.ProjectId
                : source.ProjectId;
            generated.ParentEventId = source.Id;
            generated.RootEventId = string.IsNullOrWhiteSpace(source.RootEventId)
                ? source.Id
                : source.RootEventId;
            generated.TriggerReason = string.IsNullOrWhiteSpace(link.Reason)
                ? $"{link.Relation}: {source.Id} outcome {source.OutcomeScore}"
                : link.Reason;
            result.Add(generated);
        }

        return result;
    }

    private static bool PassesChance(EventCase source, WorkOutcomeEventLink link, WorkGenerationContext context)
    {
        var chance = Math.Max(0, Math.Min(100, link.ChancePercent));
        if (chance == 0)
        {
            return false;
        }

        if (chance == 100)
        {
            return true;
        }

        unchecked
        {
            var hash = context.Seed;
            hash = hash * 397 ^ context.Day;
            hash = hash * 397 ^ StableHash(source.Id);
            hash = hash * 397 ^ StableHash(link.TargetWorkId);
            return (uint)hash % 100 < chance;
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value ?? "")
            {
                hash = hash * 31 + char.ToUpperInvariant(character);
            }

            return hash;
        }
    }
}
}
