using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
[CreateAssetMenu(menuName = "ProjectW/Case Review/Card Definition", fileName = "ActionCardDefinition")]
public sealed class ActionCardDefinition : ScriptableObject, IActionCardDefinition, IRenderableData
{
    [SerializeField] private string cardId = "";
    [SerializeField] private string title = "";
    [SerializeField] private RenderResourceDefinition renderResources;
    [TextArea(2, 5)] [SerializeField] private string visibleSummary = "";
    [TextArea(2, 5)] [SerializeField] private string hiddenIntent = "";
    [SerializeField] private AffinityScope requiredScope = AffinityScope.Surface;
    [SerializeField] private List<string> tags = new();
    [SerializeField] private int outcomeModifier;
    [SerializeField] private int riskModifier;
    [SerializeField] private int reviewCostModifier;
    [SerializeField] private int criticalChancePercent = 10;
    [SerializeField] private float criticalMultiplier = 1.5f;
    [SerializeField] private List<string> memoryHooks = new();
    [SerializeField] private List<string> growthHooks = new();
    [SerializeField] private List<string> bossReactionTags = new();

    public string CardId => cardId;
    public string Title => title;
    public RenderResourceDefinition RenderResources => renderResources;
    public string VisibleSummary => visibleSummary;
    public string HiddenIntent => hiddenIntent;
    public AffinityScope RequiredScope => requiredScope;
    public IReadOnlyList<string> Tags => tags;
    public IReadOnlyList<string> MemoryHooks => memoryHooks;
    public IReadOnlyList<string> GrowthHooks => growthHooks;
    public IReadOnlyList<string> BossReactionTags => bossReactionTags;

    public ActionCard ToRuntimeCard(string ownerPersonnelId)
    {
        return new ActionCard
        {
            Id = cardId,
            OwnerPersonnelId = ownerPersonnelId ?? "",
            Title = title,
            Summary = visibleSummary,
            Tags = new List<string>(tags),
            OutcomeModifier = outcomeModifier,
            RiskModifier = riskModifier,
            ReviewCostModifier = reviewCostModifier,
            CriticalChancePercent = Mathf.Clamp(criticalChancePercent, 0, 100),
            CriticalMultiplier = Mathf.Max(1f, criticalMultiplier)
        };
    }
}
}
