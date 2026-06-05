using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.CaseReview
{
public interface ILocalizedTextSource
{
    string DefaultLanguageKey { get; }
    string DefaultCountryCode { get; }
    string GetText(string key, string languageKey, string countryCode = "");
    bool TryGetText(string key, string languageKey, string countryCode, out string text);
}

public interface IScenarioEventDefinition
{
    string EventId { get; }
    ScenarioTiming Timing { get; }
    int Priority { get; }
    string PlaybackStateKey { get; }
    ScenarioTriggerMode TriggerMode { get; }
    IReadOnlyList<ScenarioExplicitLocation> AllowedExplicitLocations { get; }
    IReadOnlyList<ScenarioCondition> TriggerConditions { get; }
    ScenarioReplayPolicy ReplayPolicy { get; }
    LocalizedTextTable TextTable { get; }
    IReadOnlyList<ScenarioScriptLine> Lines { get; }
    ScenarioResolvedLine ResolveLine(int index, string languageKey, string countryCode = "");
}

public interface IScenarioEventProvider
{
    IEnumerable<IScenarioEventDefinition> GetEvents(ScenarioTiming timing, ScenarioEventContext context);
}

[Serializable]
public sealed class ScenarioEventContext
{
    public int Day;
    public int Week;
    public int Month;
    public int Quarter;
    public int Year;
    public Slot Slot = Slot.Morning;
    public BossArchetype BossArchetype = BossArchetype.CompetentOperator;
    public int ReplacementPressure;
    public int CapitalBalance;
    public List<string> Tags = new();
}

[Serializable]
public sealed class ScenarioScriptLine
{
    public string LineId = "";
    public ScenarioLineKind Kind = ScenarioLineKind.Dialogue;
    public string SpeakerId = "";
    public List<string> PortraitIds = new();
    public string TextKey = "";
    public string ExpressionKey = "";
    public string PoseKey = "";
    public string VoiceToneKey = "";
    public RenderResourceDefinition CenterImage;
    public List<ScenarioStageCommand> StageCommands = new();
    public List<ScenarioChoice> Choices = new();
    public List<ScenarioStateEffect> Effects = new();
}

[Serializable]
public sealed class ScenarioStageCommand
{
    public ScenarioStageCommandType CommandType;
    public string TargetId = "";
    public string Value = "";
    public RenderResourceDefinition RenderResources;
    public float DurationSec;
    public float Intensity;
}

[Serializable]
public sealed class ScenarioChoice
{
    public string ChoiceId = "";
    public string LabelTextKey = "";
    public List<ScenarioCondition> VisibleConditions = new();
    public List<ScenarioStateEffect> Costs = new();
    public List<ScenarioStateEffect> Effects = new();
    public string NextLineId = "";
}

[Serializable]
public sealed class ScenarioCondition
{
    public ScenarioConditionKey Key;
    public string SubjectId = "";
    public string Value = "";
    public int Threshold;
    public ScenarioComparison Comparison = ScenarioComparison.GreaterOrEqual;
}

[Serializable]
public sealed class ScenarioStateEffect
{
    public ScenarioEffectKey Key;
    public string SubjectId = "";
    public string Value = "";
    public int Delta;
}

[Serializable]
public readonly struct ScenarioResolvedLine
{
    public ScenarioResolvedLine(ScenarioScriptLine source, string text)
    {
        Source = source;
        Text = text ?? "";
    }

    public ScenarioScriptLine Source { get; }
    public string Text { get; }
}

public enum ScenarioTiming
{
    Morning,
    Afternoon,
    Night,
    WeeklyAudit,
    MonthlyEvaluation,
    QuarterlyEvaluation,
    YearlySettlement
}

public enum ScenarioTriggerMode
{
    LoopBoundary,
    Explicit,
    Both
}

public enum ScenarioExplicitLocation
{
    None,
    CharacterOuting,
    Consultation,
    BossCall,
    AuditBriefing,
    SpecialVisit
}

public enum ScenarioPlaybackStatus
{
    None,
    Queued,
    Playing,
    Completed,
    Skipped,
    Blocked
}

public enum ScenarioLineKind
{
    Dialogue,
    Narration,
    Stage,
    Choice,
    Effect,
    StateEffect
}

public enum ScenarioStageCommandType
{
    AddSpeaker,
    RemoveSpeaker,
    MoveSpeaker,
    FocusSpeaker,
    SetExpression,
    SetPose,
    ShowCenterImage,
    HideCenterImage,
    Shake,
    Collapse,
    ShowSpeedLines,
    ShowEffect,
    DimOthers,
    ClearStage,
    CompleteEffects,
    SetPanelPosition,
    SetAutoPlayable,
    SetTypewriterSpeed
}

public enum ScenarioConditionKey
{
    Tag,
    Relationship,
    Memory,
    ReplacementPressure,
    CapitalBalance,
    BossArchetype,
    Slot
}

public enum ScenarioComparison
{
    Exists,
    Equals,
    NotEquals,
    GreaterOrEqual,
    LessOrEqual
}

public enum ScenarioEffectKey
{
    TimeCost,
    FocusCost,
    CapitalDelta,
    TrustDelta,
    RelationshipDelta,
    ReplacementPressureDelta,
    AddTag,
    AddMemory,
    AlertFlag,
    AuditCandidate
}
}
