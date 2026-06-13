using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.IngameCore.CaseReview
{
public static class ScenarioEffectApplier
{
    public static void Apply(
        GameState state,
        IReadOnlyList<ScenarioStateEffect> effects,
        string sourceEventId = "")
    {
        if (state == null || effects == null)
        {
            return;
        }

        foreach (var effect in effects.Where(effect => effect != null))
        {
            ApplyOne(state, effect, sourceEventId);
        }
    }

    public static void ApplyActiveModifiersToWork(GameState state, EventCase item)
    {
        if (state == null || item == null)
        {
            return;
        }

        foreach (var modifier in state.EnvironmentModifiers.Where(modifier =>
            modifier != null
            && modifier.RemainingDays > 0
            && modifier.ApplyToFutureWork
            && MatchesWork(item, modifier.TargetScope, modifier.TargetFilter)))
        {
            ApplyWorkDelta(item, modifier.EffectKey, modifier.Delta);
        }

        state.GlobalLatentRisk = Clamp(
            state.Queue.Where(work => work.Status != CaseStatus.Closed).Sum(work => work.LatentRisk),
            0,
            200);
    }

    public static void AdvanceDay(GameState state)
    {
        if (state?.EnvironmentModifiers == null)
        {
            return;
        }

        foreach (var modifier in state.EnvironmentModifiers)
        {
            modifier.RemainingDays = Math.Max(0, modifier.RemainingDays - 1);
        }

        state.EnvironmentModifiers.RemoveAll(modifier => modifier.RemainingDays <= 0);
    }

    private static void ApplyOne(GameState state, ScenarioStateEffect effect, string sourceEventId)
    {
        switch (effect.Key)
        {
            case ScenarioEffectKey.WorkLatentRiskDelta:
                ApplyWorkEffect(state, effect);
                RegisterModifierIfNeeded(state, effect, sourceEventId, ScenarioEffectKey.WorkLatentRiskDelta);
                break;
            case ScenarioEffectKey.GlobalLatentRiskDelta:
                state.GlobalLatentRisk = Clamp(state.GlobalLatentRisk + effect.Delta, 0, 200);
                RegisterModifierIfNeeded(state, effect, sourceEventId, ScenarioEffectKey.GlobalLatentRiskDelta);
                break;
            case ScenarioEffectKey.AddEnvironmentModifier:
                RegisterModifier(state, effect, sourceEventId, ScenarioEffectKey.WorkLatentRiskDelta);
                break;
            case ScenarioEffectKey.RemoveEnvironmentModifier:
                RemoveModifier(state, effect);
                break;
            case ScenarioEffectKey.ReplacementPressureDelta:
                state.ReplacementPressure = Clamp(state.ReplacementPressure + effect.Delta, 0, 200);
                break;
            case ScenarioEffectKey.AddTag:
                AddStateTag(state, effect.Value);
                break;
        }
    }

    private static void ApplyWorkEffect(GameState state, ScenarioStateEffect effect)
    {
        foreach (var item in state.Queue.Where(item => MatchesWork(item, effect.TargetScope, effect.TargetFilter)))
        {
            ApplyWorkDelta(item, effect.Key, effect.Delta);
        }

        state.GlobalLatentRisk = Clamp(
            state.Queue.Where(item => item.Status != CaseStatus.Closed).Sum(item => item.LatentRisk),
            0,
            200);
    }

    private static bool MatchesWork(EventCase item, ScenarioEffectTargetScope scope, string targetFilter)
    {
        if (item == null)
        {
            return false;
        }

        if (scope == ScenarioEffectTargetScope.SingleWork)
        {
            return item.Id.Equals(targetFilter, StringComparison.OrdinalIgnoreCase);
        }

        if (item.Status == CaseStatus.Closed)
        {
            return false;
        }

        if (scope == ScenarioEffectTargetScope.MatchingOpenWork)
        {
            var filters = ParseFilter(targetFilter);
            return filters.Count == 0
                || filters.Contains(item.Subsystem)
                || item.Tags.Any(filters.Contains);
        }

        return scope is ScenarioEffectTargetScope.AllOpenWork or ScenarioEffectTargetScope.Environment;
    }

    private static void ApplyWorkDelta(EventCase item, ScenarioEffectKey key, int delta)
    {
        if (key == ScenarioEffectKey.WorkLatentRiskDelta)
        {
            item.LatentRisk = Clamp(item.LatentRisk + delta, 0, 100);
        }
    }

    private static void RegisterModifierIfNeeded(
        GameState state,
        ScenarioStateEffect effect,
        string sourceEventId,
        ScenarioEffectKey modifierEffectKey)
    {
        if (effect.DurationDays <= 0)
        {
            return;
        }

        RegisterModifier(state, effect, sourceEventId, modifierEffectKey);
    }

    private static void RegisterModifier(
        GameState state,
        ScenarioStateEffect effect,
        string sourceEventId,
        ScenarioEffectKey modifierEffectKey)
    {
        if (effect.DurationDays <= 0)
        {
            return;
        }

        var id = string.IsNullOrWhiteSpace(effect.SubjectId)
            ? $"{sourceEventId}:{modifierEffectKey}:{effect.TargetFilter}"
            : effect.SubjectId;
        state.EnvironmentModifiers.RemoveAll(modifier =>
            modifier.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        state.EnvironmentModifiers.Add(new EnvironmentModifier
        {
            Id = id,
            SourceEventId = sourceEventId ?? "",
            EffectKey = modifierEffectKey,
            TargetScope = effect.TargetScope,
            TargetFilter = effect.TargetFilter ?? "",
            Delta = effect.Delta,
            RemainingDays = effect.DurationDays,
            ApplyToFutureWork = effect.ApplyToFutureWork
        });
    }

    private static void RemoveModifier(GameState state, ScenarioStateEffect effect)
    {
        var targetId = string.IsNullOrWhiteSpace(effect.SubjectId) ? effect.Value : effect.SubjectId;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        state.EnvironmentModifiers.RemoveAll(modifier =>
            modifier.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddStateTag(GameState state, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var id = $"tag:{value}";
        if (state.EnvironmentModifiers.Any(modifier => modifier.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        state.EnvironmentModifiers.Add(new EnvironmentModifier
        {
            Id = id,
            SourceEventId = value,
            EffectKey = ScenarioEffectKey.AddTag,
            TargetScope = ScenarioEffectTargetScope.Environment,
            TargetFilter = value,
            RemainingDays = int.MaxValue
        });
    }

    private static HashSet<string> ParseFilter(string value)
    {
        return new HashSet<string>(
            (value ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0),
            StringComparer.OrdinalIgnoreCase);
    }

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
}
}
