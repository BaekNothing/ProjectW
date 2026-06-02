using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
public interface ICharacterBaseData
{
    string PersonnelId { get; }
    string DisplayName { get; }
    string CloneLineageId { get; }
    AffinityScope InitialInformationScope { get; }
    IReadOnlyList<ActionCardDefinition> StartingDeck { get; }
    IReadOnlyList<PerkDefinition> StartingPerks { get; }
    Personnel CreateRuntimeModel();
}

public interface IRenderableData
{
    RenderResourceDefinition RenderResources { get; }
}

public interface ICharacterRuntimeData
{
    string PersonnelId { get; }
    CharacterBaseDefinition BaseDefinition { get; }
    IReadOnlyList<ActionCardDefinition> Deck { get; }
    IReadOnlyList<PerkDefinition> Perks { get; }
    IReadOnlyList<CharacterRelationshipRecord> Relationships { get; }
    IReadOnlyList<CharacterMemoryRecord> Memories { get; }
    Personnel CreateRuntimeModel();
}

public interface IActionCardDefinition
{
    string CardId { get; }
    ActionCard ToRuntimeCard(string ownerPersonnelId);
}

public interface IPerkDefinition
{
    string PerkId { get; }
    PersonnelPerk ToRuntimePerk();
}

public interface ICharacterMemoryStore
{
    IReadOnlyList<CharacterMemoryRecord> Memories { get; }
    void AddMemory(CharacterMemoryRecord memory);
    IEnumerable<CharacterMemoryRecord> FindMemoriesForTarget(string targetId);
}

public interface ICharacterRelationshipStore
{
    IReadOnlyList<CharacterRelationshipRecord> Relationships { get; }
    CharacterRelationshipRecord GetOrCreateRelationship(string targetPersonnelId);
    PersonnelRelationship ToRuntimeRelationship(CharacterRelationshipRecord record);
}

[Serializable]
public sealed class CharacterRelationshipRecord
{
    public string TargetPersonnelId = "";
    public RenderResourceDefinition RenderResources;
    [Range(-100, 100)] public int Trust;
    [Range(-100, 100)] public int Affinity;
    [Range(-100, 100)] public int Debt;
    [Range(-100, 100)] public int Resentment;
    [Range(-100, 100)] public int Reliability;
    [TextArea(2, 5)] public string Note = "";
}

[Serializable]
public sealed class CharacterMemoryRecord
{
    public string MemoryId = "";
    public string OwnerPersonnelId = "";
    public string TargetId = "";
    public RenderResourceDefinition RenderResources;
    public CharacterMemoryType MemoryType;
    public MemoryValence Valence;
    [Range(0, 100)] public int Intensity;
    [Range(0, 100)] public int Decay;
    public List<string> Tags = new();
    public string SourceEventId = "";
    public int DayCreated;
    public AffinityScope VisibleScope = AffinityScope.Working;
    [TextArea(2, 5)] public string Note = "";
}

[Serializable]
public sealed class TraitSampleRecord
{
    public string TraitSampleId = "";
    public string SourceEventId = "";
    public RenderResourceDefinition RenderResources;
    public List<string> Tags = new();
    [Range(0, 100)] public int Strength;
    public bool ClonePersistent;
    [TextArea(2, 5)] public string Note = "";
}

[Serializable]
public sealed class CharacterAptitudes
{
    public int Observation;
    public int Dexterity;
    public int Boldness;
    public int Intuition;
    public int Logic;

    public Dictionary<string, int> ToDictionary()
    {
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["observation"] = Observation,
            ["dexterity"] = Dexterity,
            ["boldness"] = Boldness,
            ["intuition"] = Intuition,
            ["logic"] = Logic
        };
    }
}

[Serializable]
public sealed class CharacterOperationalDefaults
{
    public int PhysicalEnergy = 100;
    public int MentalStress;
    public int LoadAssigned;
    public int Fatigue;
    public int Stagnation;
    public int TrustToManager;
    public int RetentionRisk;
    public int OptLow;
    public int OptHigh;
    public int MaxLoad;
    public int ConnectionLimit = 3;
}

[Serializable]
public sealed class AptitudeModifier
{
    public string Key = "";
    public int Value;
}

public enum CharacterMemoryType
{
    Work,
    Social,
    Manager,
    Clone
}

public enum MemoryValence
{
    Neutral,
    Positive,
    Negative,
    Mixed
}
}
