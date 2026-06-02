using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
[CreateAssetMenu(menuName = "ProjectW/Case Review/Perk Definition", fileName = "PerkDefinition")]
public sealed class PerkDefinition : ScriptableObject, IPerkDefinition, IRenderableData
{
    [SerializeField] private string perkId = "";
    [SerializeField] private string title = "";
    [SerializeField] private RenderResourceDefinition renderResources;
    [SerializeField] private List<string> triggerTags = new();
    [SerializeField] private List<AptitudeModifier> aptitudeModifiers = new();
    [SerializeField] private int outcomeModifier;
    [SerializeField] private int riskModifier;
    [SerializeField] private int physicalCostModifier;
    [SerializeField] private int mentalCostModifier;
    [SerializeField] private int reviewCostModifier;
    [SerializeField] private int memoryModifier;
    [SerializeField] private bool clonePersistent;
    [TextArea(2, 5)] [SerializeField] private string note = "";

    public string PerkId => perkId;
    public string Title => title;
    public RenderResourceDefinition RenderResources => renderResources;
    public IReadOnlyList<string> TriggerTags => triggerTags;
    public bool ClonePersistent => clonePersistent;

    public PersonnelPerk ToRuntimePerk()
    {
        return new PersonnelPerk
        {
            Id = perkId,
            Name = title,
            TriggerTags = new List<string>(triggerTags),
            AptitudeModifiers = aptitudeModifiers.ToDictionary(m => m.Key, m => m.Value, StringComparer.OrdinalIgnoreCase),
            OutcomeModifier = outcomeModifier,
            PhysicalCostModifier = physicalCostModifier,
            MentalCostModifier = mentalCostModifier,
            Note = note
        };
    }
}
}
