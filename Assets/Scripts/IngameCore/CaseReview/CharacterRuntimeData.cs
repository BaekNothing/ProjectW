using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
[CreateAssetMenu(menuName = "ProjectW/Case Review/Character Runtime Data", fileName = "CharacterRuntimeData")]
public sealed class CharacterRuntimeData : ScriptableObject, ICharacterRuntimeData, ICharacterMutationTarget, ICharacterMemoryStore, ICharacterRelationshipStore, IRenderableData
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

    public CharacterMutationResult AddCard(ActionCardDefinition card)
    {
        if (card == null)
        {
            return CharacterMutationResult.Ignored("CARD_NULL", "Card definition is null.");
        }

        deck ??= new List<ActionCardDefinition>();
        if (deck.Any(existing => MatchesCard(existing, card.CardId)))
        {
            return CharacterMutationResult.Ignored("CARD_ALREADY_EXISTS", card.CardId);
        }

        deck.Add(card);
        return CharacterMutationResult.Applied("CARD_ADDED", card.CardId);
    }

    public CharacterMutationResult RemoveCard(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return CharacterMutationResult.Ignored("CARD_ID_EMPTY", "Card id is empty.");
        }

        deck ??= new List<ActionCardDefinition>();
        var removed = deck.RemoveAll(card => MatchesCard(card, cardId));
        return removed > 0
            ? CharacterMutationResult.Applied("CARD_REMOVED", cardId)
            : CharacterMutationResult.Ignored("CARD_NOT_FOUND", cardId);
    }

    public CharacterMutationResult AddPerk(PerkDefinition perk)
    {
        if (perk == null)
        {
            return CharacterMutationResult.Ignored("PERK_NULL", "Perk definition is null.");
        }

        perks ??= new List<PerkDefinition>();
        if (perks.Any(existing => MatchesPerk(existing, perk.PerkId)))
        {
            return CharacterMutationResult.Ignored("PERK_ALREADY_EXISTS", perk.PerkId);
        }

        perks.Add(perk);
        return CharacterMutationResult.Applied("PERK_ADDED", perk.PerkId);
    }

    public CharacterMutationResult RemovePerk(string perkId)
    {
        if (string.IsNullOrWhiteSpace(perkId))
        {
            return CharacterMutationResult.Ignored("PERK_ID_EMPTY", "Perk id is empty.");
        }

        perks ??= new List<PerkDefinition>();
        var removed = perks.RemoveAll(perk => MatchesPerk(perk, perkId));
        return removed > 0
            ? CharacterMutationResult.Applied("PERK_REMOVED", perkId)
            : CharacterMutationResult.Ignored("PERK_NOT_FOUND", perkId);
    }

    public CharacterMutationResult AddTraitSample(TraitSampleRecord traitSample)
    {
        if (traitSample == null)
        {
            return CharacterMutationResult.Ignored("TRAIT_NULL", "Trait sample is null.");
        }

        traitSamples ??= new List<TraitSampleRecord>();
        if (!string.IsNullOrWhiteSpace(traitSample.TraitSampleId)
            && traitSamples.Any(existing => MatchesTraitSample(existing, traitSample.TraitSampleId)))
        {
            return CharacterMutationResult.Ignored("TRAIT_ALREADY_EXISTS", traitSample.TraitSampleId);
        }

        traitSample.Strength = ClampPercent(traitSample.Strength);
        traitSamples.Add(traitSample);
        return CharacterMutationResult.Applied("TRAIT_ADDED", traitSample.TraitSampleId);
    }

    public CharacterMutationResult RemoveTraitSample(string traitSampleId)
    {
        if (string.IsNullOrWhiteSpace(traitSampleId))
        {
            return CharacterMutationResult.Ignored("TRAIT_ID_EMPTY", "Trait sample id is empty.");
        }

        traitSamples ??= new List<TraitSampleRecord>();
        var removed = traitSamples.RemoveAll(trait => MatchesTraitSample(trait, traitSampleId));
        return removed > 0
            ? CharacterMutationResult.Applied("TRAIT_REMOVED", traitSampleId)
            : CharacterMutationResult.Ignored("TRAIT_NOT_FOUND", traitSampleId);
    }

    public CharacterMutationResult AdjustTraitSampleStrength(string traitSampleId, int delta)
    {
        if (string.IsNullOrWhiteSpace(traitSampleId))
        {
            return CharacterMutationResult.Ignored("TRAIT_ID_EMPTY", "Trait sample id is empty.");
        }

        traitSamples ??= new List<TraitSampleRecord>();
        var trait = traitSamples.FirstOrDefault(item => MatchesTraitSample(item, traitSampleId));
        if (trait == null)
        {
            return CharacterMutationResult.Ignored("TRAIT_NOT_FOUND", traitSampleId);
        }

        trait.Strength = ClampPercent(trait.Strength + delta);
        return CharacterMutationResult.Applied("TRAIT_STRENGTH_ADJUSTED", traitSampleId);
    }

    public CharacterMutationResult AddMemoryRecord(CharacterMemoryRecord memory)
    {
        if (memory == null)
        {
            return CharacterMutationResult.Ignored("MEMORY_NULL", "Memory record is null.");
        }

        memories ??= new List<CharacterMemoryRecord>();
        if (!string.IsNullOrWhiteSpace(memory.MemoryId)
            && memories.Any(existing => MatchesMemory(existing, memory.MemoryId)))
        {
            return CharacterMutationResult.Ignored("MEMORY_ALREADY_EXISTS", memory.MemoryId);
        }

        memory.Intensity = ClampPercent(memory.Intensity);
        memory.Decay = ClampPercent(memory.Decay);
        memories.Add(memory);
        return CharacterMutationResult.Applied("MEMORY_ADDED", memory.MemoryId);
    }

    public CharacterMutationResult RemoveMemory(string memoryId)
    {
        if (string.IsNullOrWhiteSpace(memoryId))
        {
            return CharacterMutationResult.Ignored("MEMORY_ID_EMPTY", "Memory id is empty.");
        }

        memories ??= new List<CharacterMemoryRecord>();
        var removed = memories.RemoveAll(memory => MatchesMemory(memory, memoryId));
        return removed > 0
            ? CharacterMutationResult.Applied("MEMORY_REMOVED", memoryId)
            : CharacterMutationResult.Ignored("MEMORY_NOT_FOUND", memoryId);
    }

    public CharacterMutationResult SetMemoryStat(string memoryId, CharacterMemoryStatKey stat, int value)
    {
        var memory = FindMemory(memoryId);
        if (memory == null)
        {
            return CharacterMutationResult.Ignored("MEMORY_NOT_FOUND", memoryId);
        }

        switch (stat)
        {
            case CharacterMemoryStatKey.Intensity:
                memory.Intensity = ClampPercent(value);
                break;
            case CharacterMemoryStatKey.Decay:
                memory.Decay = ClampPercent(value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }

        return CharacterMutationResult.Applied("MEMORY_STAT_SET", $"{memoryId}:{stat}");
    }

    public CharacterMutationResult AdjustMemoryStat(string memoryId, CharacterMemoryStatKey stat, int delta)
    {
        var memory = FindMemory(memoryId);
        if (memory == null)
        {
            return CharacterMutationResult.Ignored("MEMORY_NOT_FOUND", memoryId);
        }

        var current = stat switch
        {
            CharacterMemoryStatKey.Intensity => memory.Intensity,
            CharacterMemoryStatKey.Decay => memory.Decay,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null)
        };
        return SetMemoryStat(memoryId, stat, current + delta);
    }

    public CharacterMutationResult RemoveRelationship(string targetPersonnelId)
    {
        if (string.IsNullOrWhiteSpace(targetPersonnelId))
        {
            return CharacterMutationResult.Ignored("RELATIONSHIP_TARGET_EMPTY", "Relationship target id is empty.");
        }

        relationships ??= new List<CharacterRelationshipRecord>();
        var removed = relationships.RemoveAll(record => MatchesRelationship(record, targetPersonnelId));
        return removed > 0
            ? CharacterMutationResult.Applied("RELATIONSHIP_REMOVED", targetPersonnelId)
            : CharacterMutationResult.Ignored("RELATIONSHIP_NOT_FOUND", targetPersonnelId);
    }

    public CharacterMutationResult SetRelationshipStat(string targetPersonnelId, CharacterRelationshipStatKey stat, int value)
    {
        if (string.IsNullOrWhiteSpace(targetPersonnelId))
        {
            return CharacterMutationResult.Ignored("RELATIONSHIP_TARGET_EMPTY", "Relationship target id is empty.");
        }

        var relationship = GetOrCreateRelationship(targetPersonnelId);
        var clamped = Mathf.Clamp(value, -100, 100);
        switch (stat)
        {
            case CharacterRelationshipStatKey.Trust:
                relationship.Trust = clamped;
                break;
            case CharacterRelationshipStatKey.Affinity:
                relationship.Affinity = clamped;
                break;
            case CharacterRelationshipStatKey.Debt:
                relationship.Debt = clamped;
                break;
            case CharacterRelationshipStatKey.Resentment:
                relationship.Resentment = clamped;
                break;
            case CharacterRelationshipStatKey.Reliability:
                relationship.Reliability = clamped;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }

        return CharacterMutationResult.Applied("RELATIONSHIP_STAT_SET", $"{targetPersonnelId}:{stat}");
    }

    public CharacterMutationResult AdjustRelationshipStat(string targetPersonnelId, CharacterRelationshipStatKey stat, int delta)
    {
        if (string.IsNullOrWhiteSpace(targetPersonnelId))
        {
            return CharacterMutationResult.Ignored("RELATIONSHIP_TARGET_EMPTY", "Relationship target id is empty.");
        }

        var relationship = GetOrCreateRelationship(targetPersonnelId);
        var current = stat switch
        {
            CharacterRelationshipStatKey.Trust => relationship.Trust,
            CharacterRelationshipStatKey.Affinity => relationship.Affinity,
            CharacterRelationshipStatKey.Debt => relationship.Debt,
            CharacterRelationshipStatKey.Resentment => relationship.Resentment,
            CharacterRelationshipStatKey.Reliability => relationship.Reliability,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null)
        };
        return SetRelationshipStat(targetPersonnelId, stat, current + delta);
    }

    public CharacterMutationResult SetStat(CharacterStatKey stat, int value)
    {
        currentState ??= new CharacterOperationalDefaults();
        switch (stat)
        {
            case CharacterStatKey.PhysicalEnergy:
                currentState.PhysicalEnergy = ClampPercent(value);
                break;
            case CharacterStatKey.MentalStress:
                currentState.MentalStress = ClampPercent(value);
                break;
            case CharacterStatKey.LoadAssigned:
                currentState.LoadAssigned = ClampNonNegative(value);
                break;
            case CharacterStatKey.Fatigue:
                currentState.Fatigue = ClampPercent(value);
                break;
            case CharacterStatKey.Stagnation:
                currentState.Stagnation = ClampPercent(value);
                break;
            case CharacterStatKey.TrustToManager:
                currentState.TrustToManager = Mathf.Clamp(value, -100, 100);
                break;
            case CharacterStatKey.RetentionRisk:
                currentState.RetentionRisk = ClampPercent(value);
                break;
            case CharacterStatKey.OptLow:
                currentState.OptLow = ClampNonNegative(value);
                break;
            case CharacterStatKey.OptHigh:
                currentState.OptHigh = ClampNonNegative(value);
                break;
            case CharacterStatKey.MaxLoad:
                currentState.MaxLoad = ClampNonNegative(value);
                break;
            case CharacterStatKey.ConnectionLimit:
                currentState.ConnectionLimit = ClampNonNegative(value);
                break;
            case CharacterStatKey.DaysSinceJoined:
                daysSinceJoined = ClampNonNegative(value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }

        return CharacterMutationResult.Applied("STAT_SET", stat.ToString());
    }

    public CharacterMutationResult AdjustStat(CharacterStatKey stat, int delta)
    {
        return SetStat(stat, GetStat(stat) + delta);
    }

    public int GetStat(CharacterStatKey stat)
    {
        currentState ??= new CharacterOperationalDefaults();
        return stat switch
        {
            CharacterStatKey.PhysicalEnergy => currentState.PhysicalEnergy,
            CharacterStatKey.MentalStress => currentState.MentalStress,
            CharacterStatKey.LoadAssigned => currentState.LoadAssigned,
            CharacterStatKey.Fatigue => currentState.Fatigue,
            CharacterStatKey.Stagnation => currentState.Stagnation,
            CharacterStatKey.TrustToManager => currentState.TrustToManager,
            CharacterStatKey.RetentionRisk => currentState.RetentionRisk,
            CharacterStatKey.OptLow => currentState.OptLow,
            CharacterStatKey.OptHigh => currentState.OptHigh,
            CharacterStatKey.MaxLoad => currentState.MaxLoad,
            CharacterStatKey.ConnectionLimit => currentState.ConnectionLimit,
            CharacterStatKey.DaysSinceJoined => daysSinceJoined,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null)
        };
    }

    public Personnel CreateRuntimeModel()
    {
        currentState ??= new CharacterOperationalDefaults();
        deck ??= new List<ActionCardDefinition>();
        perks ??= new List<PerkDefinition>();
        relationships ??= new List<CharacterRelationshipRecord>();
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
        AddMemoryRecord(memory);
    }

    public IEnumerable<CharacterMemoryRecord> FindMemoriesForTarget(string targetId)
    {
        memories ??= new List<CharacterMemoryRecord>();
        return memories.Where(memory => memory != null && memory.TargetId.Equals(targetId ?? "", StringComparison.OrdinalIgnoreCase));
    }

    public CharacterRelationshipRecord GetOrCreateRelationship(string targetPersonnelId)
    {
        relationships ??= new List<CharacterRelationshipRecord>();
        var existing = relationships.FirstOrDefault(r => MatchesRelationship(r, targetPersonnelId));
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

    private static bool MatchesCard(ActionCardDefinition card, string cardId)
    {
        return card != null && card.CardId.Equals(cardId ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesPerk(PerkDefinition perk, string perkId)
    {
        return perk != null && perk.PerkId.Equals(perkId ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesTraitSample(TraitSampleRecord trait, string traitSampleId)
    {
        return trait != null && trait.TraitSampleId.Equals(traitSampleId ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesMemory(CharacterMemoryRecord memory, string memoryId)
    {
        return memory != null && memory.MemoryId.Equals(memoryId ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesRelationship(CharacterRelationshipRecord relationship, string targetPersonnelId)
    {
        return relationship != null && relationship.TargetPersonnelId.Equals(targetPersonnelId ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private CharacterMemoryRecord FindMemory(string memoryId)
    {
        if (string.IsNullOrWhiteSpace(memoryId))
        {
            return null;
        }

        memories ??= new List<CharacterMemoryRecord>();
        return memories.FirstOrDefault(memory => MatchesMemory(memory, memoryId));
    }

    private static int ClampPercent(int value)
    {
        return Mathf.Clamp(value, 0, 100);
    }

    private static int ClampNonNegative(int value)
    {
        return Mathf.Max(0, value);
    }
}
}
