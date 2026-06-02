using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
[CreateAssetMenu(menuName = "ProjectW/Case Review/Character Runtime Data", fileName = "CharacterRuntimeData")]
public sealed class CharacterRuntimeData : ScriptableObject, ICharacterRuntimeData, ICharacterMemoryStore, ICharacterRelationshipStore, IRenderableData
{
    [SerializeField] private CharacterBaseDefinition baseDefinition;
    [SerializeField] private string personnelIdOverride = "";
    [SerializeField] private RenderResourceDefinition renderResources;
    [SerializeField] private AffinityScope informationScopeOverride = AffinityScope.Surface;
    [SerializeField] private CharacterOperationalDefaults currentState = new();
    [SerializeField] private List<ActionCardDefinition> deck = new();
    [SerializeField] private List<PerkDefinition> perks = new();
    [SerializeField] private List<CharacterRelationshipRecord> relationships = new();
    [SerializeField] private List<CharacterMemoryRecord> memories = new();
    [SerializeField] private List<TraitSampleRecord> traitSamples = new();
    [SerializeField] private bool hasLeft;
    [SerializeField] private int daysSinceJoined;

    public string PersonnelId => string.IsNullOrWhiteSpace(personnelIdOverride) && baseDefinition != null
        ? baseDefinition.PersonnelId
        : personnelIdOverride;

    public CharacterBaseDefinition BaseDefinition => baseDefinition;
    public RenderResourceDefinition RenderResources => renderResources != null ? renderResources : baseDefinition != null ? baseDefinition.RenderResources : null;
    public IReadOnlyList<ActionCardDefinition> Deck => deck;
    public IReadOnlyList<PerkDefinition> Perks => perks;
    public IReadOnlyList<CharacterRelationshipRecord> Relationships => relationships;
    public IReadOnlyList<CharacterMemoryRecord> Memories => memories;
    public IReadOnlyList<TraitSampleRecord> TraitSamples => traitSamples;

    public Personnel CreateRuntimeModel()
    {
        var model = baseDefinition != null ? baseDefinition.CreateRuntimeModel() : new Personnel { Id = PersonnelId };
        model.Id = PersonnelId;
        model.InformationScope = informationScopeOverride;
        model.PhysicalEnergy = currentState.PhysicalEnergy;
        model.MentalStress = currentState.MentalStress;
        model.LoadAssigned = currentState.LoadAssigned;
        model.Fatigue = currentState.Fatigue;
        model.Stagnation = currentState.Stagnation;
        model.TrustToManager = currentState.TrustToManager;
        model.RetentionRisk = currentState.RetentionRisk;
        model.OptLow = currentState.OptLow;
        model.OptHigh = currentState.OptHigh;
        model.MaxLoad = currentState.MaxLoad;
        model.ConnectionLimit = currentState.ConnectionLimit;
        model.HasLeft = hasLeft;
        model.DaysSinceJoined = daysSinceJoined;
        model.Deck = deck.Where(card => card != null).Select(card => card.ToRuntimeCard(PersonnelId)).ToList();
        model.Perks = perks.Where(perk => perk != null).Select(perk => perk.ToRuntimePerk()).ToList();
        model.Relationships = relationships.Select(ToRuntimeRelationship).ToList();
        return model;
    }

    public void AddMemory(CharacterMemoryRecord memory)
    {
        if (memory == null)
        {
            return;
        }

        memories.Add(memory);
    }

    public IEnumerable<CharacterMemoryRecord> FindMemoriesForTarget(string targetId)
    {
        return memories.Where(memory => memory != null && memory.TargetId.Equals(targetId ?? "", StringComparison.OrdinalIgnoreCase));
    }

    public CharacterRelationshipRecord GetOrCreateRelationship(string targetPersonnelId)
    {
        var existing = relationships.FirstOrDefault(r => r != null && r.TargetPersonnelId.Equals(targetPersonnelId ?? "", StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing;
        }

        var record = new CharacterRelationshipRecord { TargetPersonnelId = targetPersonnelId ?? "" };
        relationships.Add(record);
        return record;
    }

    public PersonnelRelationship ToRuntimeRelationship(CharacterRelationshipRecord record)
    {
        return new PersonnelRelationship
        {
            TargetId = record.TargetPersonnelId,
            Trust = record.Trust,
            Affinity = record.Affinity,
            Note = record.Note
        };
    }
}
}
