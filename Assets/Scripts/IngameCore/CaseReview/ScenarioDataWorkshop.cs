using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
public sealed class ScenarioDataWorkshop : MonoBehaviour
{
    [SerializeField] private string outputFolder = "Assets/Resources/CaseReviewData/Scenarios";
    [SerializeField] private ScenarioEventDefinition selectedScenarioEvent;
    [SerializeField] private LocalizedTextTable selectedTextTable;
    [SerializeField] private RenderResourceDefinition selectedRenderResources;

    public string OutputFolder => outputFolder;
    public ScenarioEventDefinition SelectedScenarioEvent => selectedScenarioEvent;
    public LocalizedTextTable SelectedTextTable => selectedTextTable;
    public RenderResourceDefinition SelectedRenderResources => selectedRenderResources;
}
}
