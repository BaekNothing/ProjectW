using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
[CreateAssetMenu(menuName = "ProjectW/Case Review/Scenario Event Definition", fileName = "ScenarioEvent")]
public sealed class ScenarioEventDefinition : ScriptableObject, IScenarioEventDefinition, IRenderableData
{
    [SerializeField] private string eventId = "";
    [SerializeField] private ScenarioTiming timing = ScenarioTiming.Morning;
    [SerializeField] private int priority;
    [SerializeField] private string playbackStateKey = "";
    [SerializeField] private ScenarioTriggerMode triggerMode = ScenarioTriggerMode.LoopBoundary;
    [SerializeField] private List<ScenarioExplicitLocation> allowedExplicitLocations = new();
    [SerializeField] private RenderResourceDefinition renderResources;
    [SerializeField] private LocalizedTextTable textTable;
    [SerializeField] private List<ScenarioCondition> triggerConditions = new();
    [SerializeField] private List<ScenarioStateEffect> entryCosts = new();
    [SerializeField] private List<ScenarioScriptLine> lines = new();
    [SerializeField] private List<ScenarioStateEffect> exitEffects = new();
    [SerializeField] private ScenarioReplayPolicy replayPolicy = new();

    public string EventId => eventId;
    public ScenarioTiming Timing => timing;
    public int Priority => priority;
    public string PlaybackStateKey => string.IsNullOrWhiteSpace(playbackStateKey) ? eventId : playbackStateKey;
    public ScenarioTriggerMode TriggerMode => triggerMode;
    public IReadOnlyList<ScenarioExplicitLocation> AllowedExplicitLocations => allowedExplicitLocations;
    public RenderResourceDefinition RenderResources => renderResources;
    public LocalizedTextTable TextTable => textTable;
    public IReadOnlyList<ScenarioCondition> TriggerConditions => triggerConditions;
    public IReadOnlyList<ScenarioStateEffect> EntryCosts => entryCosts;
    public IReadOnlyList<ScenarioScriptLine> Lines => lines;
    public IReadOnlyList<ScenarioStateEffect> ExitEffects => exitEffects;
    public ScenarioReplayPolicy ReplayPolicy => replayPolicy;

    public ScenarioResolvedLine ResolveLine(int index, string languageKey, string countryCode = "")
    {
        if (index < 0 || index >= lines.Count)
        {
            return new ScenarioResolvedLine(null, "");
        }

        var line = lines[index];
        var text = textTable != null ? textTable.GetText(line.TextKey, languageKey, countryCode) : line.TextKey;
        return new ScenarioResolvedLine(line, text);
    }
}

[System.Serializable]
public sealed class ScenarioReplayPolicy
{
    public bool OneShot = true;
    public int CooldownDays;
    public bool AllowReplayInDebug;
}
}
