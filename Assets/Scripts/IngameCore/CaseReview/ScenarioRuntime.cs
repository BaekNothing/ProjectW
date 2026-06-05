using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.IngameCore.CaseReview
{
public sealed class ScenarioPlaybackStore
{
    private readonly Dictionary<string, ScenarioPlaybackRecord> records = new(StringComparer.OrdinalIgnoreCase);

    public ScenarioPlaybackRecord GetOrCreate(string playbackStateKey)
    {
        var key = string.IsNullOrWhiteSpace(playbackStateKey) ? "scenario.unknown" : playbackStateKey;
        if (!records.TryGetValue(key, out var record))
        {
            record = new ScenarioPlaybackRecord { PlaybackStateKey = key };
            records[key] = record;
        }

        return record;
    }

    public IReadOnlyCollection<ScenarioPlaybackRecord> Records => records.Values;
}

[Serializable]
public sealed class ScenarioPlaybackRecord
{
    public string PlaybackStateKey = "";
    public ScenarioPlaybackStatus Status = ScenarioPlaybackStatus.None;
    public bool Seen;
    public bool Completed;
    public bool Skipped;
    public int LastPlayedDay = -1;
    public int CooldownUntilDay;
    public string SelectedBranchId = "";
}

public sealed class ScenarioScheduler
{
    private readonly IScenarioEventProvider provider;
    private readonly ScenarioPlaybackStore playbackStore;

    public ScenarioScheduler(IScenarioEventProvider provider, ScenarioPlaybackStore playbackStore)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.playbackStore = playbackStore ?? throw new ArgumentNullException(nameof(playbackStore));
    }

    public List<IScenarioEventDefinition> GetLoopBoundaryCandidates(ScenarioTiming timing, ScenarioEventContext context)
    {
        return GetCandidates(timing, context, ScenarioTriggerMode.LoopBoundary, ScenarioExplicitLocation.None);
    }

    public List<IScenarioEventDefinition> GetExplicitCandidates(
        ScenarioTiming timing,
        ScenarioEventContext context,
        ScenarioExplicitLocation location)
    {
        return GetCandidates(timing, context, ScenarioTriggerMode.Explicit, location);
    }

    public void MarkQueued(IScenarioEventDefinition definition)
    {
        playbackStore.GetOrCreate(definition.PlaybackStateKey).Status = ScenarioPlaybackStatus.Queued;
    }

    public void MarkPlaying(IScenarioEventDefinition definition, int day)
    {
        var record = playbackStore.GetOrCreate(definition.PlaybackStateKey);
        record.Status = ScenarioPlaybackStatus.Playing;
        record.Seen = true;
        record.LastPlayedDay = day;
    }

    public void MarkCompleted(IScenarioEventDefinition definition, int day, string branchId = "")
    {
        var record = playbackStore.GetOrCreate(definition.PlaybackStateKey);
        record.Status = ScenarioPlaybackStatus.Completed;
        record.Seen = true;
        record.Completed = true;
        record.Skipped = false;
        record.LastPlayedDay = day;
        record.SelectedBranchId = branchId ?? "";
        record.CooldownUntilDay = Math.Max(record.CooldownUntilDay, day + Math.Max(0, definition.ReplayPolicy?.CooldownDays ?? 0));
    }

    public void MarkSkipped(IScenarioEventDefinition definition, int day)
    {
        var record = playbackStore.GetOrCreate(definition.PlaybackStateKey);
        record.Status = ScenarioPlaybackStatus.Skipped;
        record.Seen = true;
        record.Skipped = true;
        record.LastPlayedDay = day;
        record.CooldownUntilDay = Math.Max(record.CooldownUntilDay, day + Math.Max(0, definition.ReplayPolicy?.CooldownDays ?? 0));
    }

    private List<IScenarioEventDefinition> GetCandidates(
        ScenarioTiming timing,
        ScenarioEventContext context,
        ScenarioTriggerMode requestedMode,
        ScenarioExplicitLocation location)
    {
        return provider.GetEvents(timing, context)
            .Where(definition => MatchesMode(definition, requestedMode, location))
            .Where(definition => IsReplayAllowed(definition, context))
            .Where(definition => ScenarioConditionEvaluator.MatchesAll(definition.TriggerConditions, context))
            .OrderByDescending(definition => definition.Priority)
            .ThenBy(definition => definition.EventId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesMode(
        IScenarioEventDefinition definition,
        ScenarioTriggerMode requestedMode,
        ScenarioExplicitLocation location)
    {
        var mode = definition.TriggerMode;
        var modeAllowed = mode == ScenarioTriggerMode.Both || mode == requestedMode;
        if (!modeAllowed)
        {
            return false;
        }

        if (requestedMode != ScenarioTriggerMode.Explicit)
        {
            return true;
        }

        return definition.AllowedExplicitLocations.Count == 0
            || definition.AllowedExplicitLocations.Contains(location);
    }

    private bool IsReplayAllowed(IScenarioEventDefinition definition, ScenarioEventContext context)
    {
        var policy = definition.ReplayPolicy;
        var record = playbackStore.GetOrCreate(definition.PlaybackStateKey);
        if (policy is null)
        {
            return true;
        }

        if (policy.OneShot && record.Completed && !policy.AllowReplayInDebug)
        {
            return false;
        }

        if (record.CooldownUntilDay > context.Day && !policy.AllowReplayInDebug)
        {
            return false;
        }

        return true;
    }
}

public static class ScenarioConditionEvaluator
{
    public static bool MatchesAll(IReadOnlyList<ScenarioCondition> conditions, ScenarioEventContext context)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return false;
        }

        return conditions.All(condition => Matches(condition, context));
    }

    public static bool Matches(ScenarioCondition condition, ScenarioEventContext context)
    {
        return condition.Key switch
        {
            ScenarioConditionKey.Tag => CompareTag(condition, context),
            ScenarioConditionKey.ReplacementPressure => CompareInt(context.ReplacementPressure, condition),
            ScenarioConditionKey.CapitalBalance => CompareInt(context.CapitalBalance, condition),
            ScenarioConditionKey.BossArchetype => CompareString(context.BossArchetype.ToString(), condition),
            ScenarioConditionKey.Slot => CompareString(context.Slot.ToString(), condition),
            _ => false
        };
    }

    private static bool CompareTag(ScenarioCondition condition, ScenarioEventContext context)
    {
        var target = string.IsNullOrWhiteSpace(condition.Value) ? condition.SubjectId : condition.Value;
        var exists = context.Tags.Any(tag => tag.Equals(target, StringComparison.OrdinalIgnoreCase));
        return condition.Comparison switch
        {
            ScenarioComparison.Exists => exists,
            ScenarioComparison.Equals => exists,
            ScenarioComparison.NotEquals => !exists,
            _ => exists
        };
    }

    private static bool CompareString(string actual, ScenarioCondition condition)
    {
        return condition.Comparison switch
        {
            ScenarioComparison.Exists => !string.IsNullOrWhiteSpace(actual),
            ScenarioComparison.Equals => actual.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
            ScenarioComparison.NotEquals => !actual.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool CompareInt(int actual, ScenarioCondition condition)
    {
        return condition.Comparison switch
        {
            ScenarioComparison.Exists => true,
            ScenarioComparison.Equals => actual == condition.Threshold,
            ScenarioComparison.NotEquals => actual != condition.Threshold,
            ScenarioComparison.GreaterOrEqual => actual >= condition.Threshold,
            ScenarioComparison.LessOrEqual => actual <= condition.Threshold,
            _ => false
        };
    }
}

public sealed class ScenarioPlaybackSession
{
    private readonly IScenarioEventDefinition definition;
    private readonly string languageKey;
    private readonly string countryCode;
    private int lineIndex;

    public ScenarioPlaybackSession(IScenarioEventDefinition definition, string languageKey = "ko", string countryCode = "KR")
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.languageKey = languageKey;
        this.countryCode = countryCode;
        LoadCurrentLine();
    }

    public ScenarioResolvedLine CurrentLine { get; private set; }
    public string VisibleText { get; private set; } = "";
    public bool IsLineComplete { get; private set; }
    public bool IsEventComplete { get; private set; }
    public bool AutoPlayEnabled { get; private set; }

    public void SetAutoPlay(bool enabled)
    {
        AutoPlayEnabled = enabled;
    }

    public void AdvanceTypewriter(int characterCount)
    {
        if (IsEventComplete || IsLineComplete)
        {
            return;
        }

        var targetLength = Math.Min(CurrentLine.Text.Length, VisibleText.Length + Math.Max(0, characterCount));
        VisibleText = CurrentLine.Text[..targetLength];
        IsLineComplete = VisibleText.Length >= CurrentLine.Text.Length;
    }

    public void Click()
    {
        if (IsEventComplete)
        {
            return;
        }

        if (!IsLineComplete)
        {
            CompleteCurrentLine();
            return;
        }

        MoveNextLine();
    }

    public void Skip()
    {
        if (IsEventComplete)
        {
            return;
        }

        CompleteCurrentLine();
        IsEventComplete = true;
    }

    public void TickAutoPlay()
    {
        if (!AutoPlayEnabled || IsEventComplete)
        {
            return;
        }

        if (!IsLineComplete)
        {
            CompleteCurrentLine();
            return;
        }

        if (CurrentLine.Source?.Choices?.Count > 0)
        {
            return;
        }

        MoveNextLine();
    }

    private void CompleteCurrentLine()
    {
        VisibleText = CurrentLine.Text;
        IsLineComplete = true;
    }

    private void MoveNextLine()
    {
        lineIndex++;
        if (lineIndex >= definition.Lines.Count)
        {
            IsEventComplete = true;
            return;
        }

        LoadCurrentLine();
    }

    private void LoadCurrentLine()
    {
        CurrentLine = definition.ResolveLine(lineIndex, languageKey, countryCode);
        VisibleText = "";
        IsLineComplete = string.IsNullOrEmpty(CurrentLine.Text);
    }
}
}
