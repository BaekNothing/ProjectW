using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
[CreateAssetMenu(menuName = "ProjectW/Case Review/Character Base", fileName = "CharacterBase")]
public sealed class CharacterBaseDefinition : ScriptableObject, ICharacterBaseData, IRenderableData
{
    [SerializeField] private string personnelId = "";
    [SerializeField] private string displayName = "";
    [SerializeField] private string cloneLineageId = "";
    [SerializeField] private RenderResourceDefinition renderResources;
    [TextArea(2, 5)] [SerializeField] private string background = "";
    [SerializeField] private List<string> interests = new();
    [SerializeField] private string personality = "";
    [SerializeField] private string workStyle = "";
    [SerializeField] private AffinityScope initialInformationScope = AffinityScope.Surface;
    [SerializeField] private CharacterAptitudes aptitudes = new();
    [SerializeField] private CharacterOperationalDefaults operationalDefaults = new();
    [SerializeField] private List<ActionCardDefinition> startingDeck = new();
    [SerializeField] private List<PerkDefinition> startingPerks = new();

    public string PersonnelId => personnelId;
    public string DisplayName => displayName;
    public string CloneLineageId => cloneLineageId;
    public RenderResourceDefinition RenderResources => renderResources;
    public AffinityScope InitialInformationScope => initialInformationScope;
    public IReadOnlyList<ActionCardDefinition> StartingDeck => startingDeck;
    public IReadOnlyList<PerkDefinition> StartingPerks => startingPerks;

    public Personnel CreateRuntimeModel()
    {
        return new Personnel
        {
            Id = personnelId,
            Name = displayName,
            CloneLineageId = cloneLineageId,
            Background = background,
            Interests = new List<string>(interests),
            Personality = personality,
            WorkStyle = workStyle,
            InformationScope = initialInformationScope,
            Aptitudes = aptitudes.ToDictionary(),
            PhysicalEnergy = operationalDefaults.PhysicalEnergy,
            MentalStress = operationalDefaults.MentalStress,
            LoadAssigned = operationalDefaults.LoadAssigned,
            Fatigue = operationalDefaults.Fatigue,
            Stagnation = operationalDefaults.Stagnation,
            TrustToManager = operationalDefaults.TrustToManager,
            RetentionRisk = operationalDefaults.RetentionRisk,
            OptLow = operationalDefaults.OptLow,
            OptHigh = operationalDefaults.OptHigh,
            MaxLoad = operationalDefaults.MaxLoad,
            ConnectionLimit = operationalDefaults.ConnectionLimit,
            Deck = startingDeck.Where(card => card != null).Select(card => card.ToRuntimeCard(personnelId)).ToList(),
            Perks = startingPerks.Where(perk => perk != null).Select(perk => perk.ToRuntimePerk()).ToList()
        };
    }
}
}
