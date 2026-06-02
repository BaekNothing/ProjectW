using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
public sealed class CharacterDataWorkshop : MonoBehaviour
{
    [SerializeField] private string outputFolder = "Assets/Resources/CaseReviewData/Samples";
    [SerializeField] private CharacterBaseDefinition selectedCharacterBase;
    [SerializeField] private CharacterRuntimeData selectedCharacterRuntime;
    [SerializeField] private ActionCardDefinition selectedCard;
    [SerializeField] private PerkDefinition selectedPerk;
    [SerializeField] private RenderResourceDefinition selectedRenderResources;

    public string OutputFolder => outputFolder;
    public CharacterBaseDefinition SelectedCharacterBase => selectedCharacterBase;
    public CharacterRuntimeData SelectedCharacterRuntime => selectedCharacterRuntime;
    public ActionCardDefinition SelectedCard => selectedCard;
    public PerkDefinition SelectedPerk => selectedPerk;
    public RenderResourceDefinition SelectedRenderResources => selectedRenderResources;
}
}
