using System;
using System.Collections.Generic;
using System.Linq;
using ProjectW.IngameCore.CaseReview;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    [CreateAssetMenu(menuName = "ProjectW/Case Review Database", fileName = "CaseReviewDatabase")]
    public sealed class CaseReviewDatabase : ScriptableObject
    {
        public List<PersonnelDefinition> staff = new List<PersonnelDefinition>();
        public List<CaseDefinition> cases = new List<CaseDefinition>();
        public List<TruthFrameDefinition> truthFrames = new List<TruthFrameDefinition>();
        public List<VisibleLogDefinition> visibleLogs = new List<VisibleLogDefinition>();

        public CaseReviewSeedData ToSeedData()
        {
            return new CaseReviewSeedData
            {
                Staff = staff.Select(s => s.ToModel()).ToList(),
                Queue = cases.Select(c => c.ToModel()).ToList(),
                TruthFrames = truthFrames.Select(t => t.ToModel()).ToList(),
                Logs = visibleLogs.Select(l => l.ToModel()).ToList()
            };
        }
    }

    [Serializable]
    public sealed class PersonnelDefinition
    {
        public string id = "";
        public string displayName = "";
        public string background = "";
        public List<string> interests = new List<string>();
        public string personality = "";
        public string workStyle = "";
        public int physicalEnergy = 100;
        public int mentalStress;
        public int loadAssigned;
        public int fatigue;
        public int stagnation;
        public int trustToManager;
        public int retentionRisk;
        public int daysSinceJoined;
        public int optLow;
        public int optHigh;
        public int maxLoad;
        public int connectionLimit = 3;
        public AptitudeBlock aptitudes = new AptitudeBlock();
        public List<PersonnelPerkDefinition> perks = new List<PersonnelPerkDefinition>();
        public List<PersonnelRelationshipDefinition> relationships = new List<PersonnelRelationshipDefinition>();

        public Personnel ToModel()
        {
            return new Personnel
            {
                Id = id,
                Name = displayName,
                Background = background,
                Interests = new List<string>(interests),
                Personality = personality,
                WorkStyle = workStyle,
                PhysicalEnergy = physicalEnergy,
                MentalStress = mentalStress,
                LoadAssigned = loadAssigned,
                Fatigue = fatigue,
                Stagnation = stagnation,
                TrustToManager = trustToManager,
                RetentionRisk = retentionRisk,
                DaysSinceJoined = daysSinceJoined,
                OptLow = optLow,
                OptHigh = optHigh,
                MaxLoad = maxLoad,
                ConnectionLimit = connectionLimit,
                Aptitudes = aptitudes.ToDictionary(),
                Perks = perks.Select(p => p.ToModel()).ToList(),
                Relationships = relationships.Select(r => r.ToModel()).ToList()
            };
        }
    }

    [Serializable]
    public sealed class PersonnelPerkDefinition
    {
        public string id = "";
        public string displayName = "";
        public List<string> triggerTags = new List<string>();
        public List<StringIntPair> aptitudeModifiers = new List<StringIntPair>();
        public int outcomeModifier;
        public int physicalCostModifier;
        public int mentalCostModifier;
        [TextArea(2, 5)] public string note = "";

        public PersonnelPerk ToModel()
        {
            return new PersonnelPerk
            {
                Id = id,
                Name = displayName,
                TriggerTags = new List<string>(triggerTags),
                AptitudeModifiers = StringIntPair.ToDictionary(aptitudeModifiers),
                OutcomeModifier = outcomeModifier,
                PhysicalCostModifier = physicalCostModifier,
                MentalCostModifier = mentalCostModifier,
                Note = note
            };
        }
    }

    [Serializable]
    public sealed class PersonnelRelationshipDefinition
    {
        public string targetId = "";
        public int trust;
        public int affinity;
        [TextArea(2, 4)] public string note = "";

        public PersonnelRelationship ToModel()
        {
            return new PersonnelRelationship
            {
                TargetId = targetId,
                Trust = trust,
                Affinity = affinity,
                Note = note
            };
        }
    }

    [Serializable]
    public sealed class CaseDefinition
    {
        public string id = "";
        public string kind = "";
        public string title = "";
        public string subsystem = "";
        public int urgency;
        public int severity;
        public int ttlSec;
        public int latentRisk;
        public int mismatchScore;
        public List<string> assignedPersonnel = new List<string>();
        public int physicalCost;
        public int mentalCost;
        public int baseSuccessChance = 50;
        public AptitudeBlock requiredAptitudes = new AptitudeBlock();
        public List<string> perkTags = new List<string>();
        [TextArea(2, 5)] public string perkInteractionInfo = "";

        public EventCase ToModel()
        {
            return new EventCase
            {
                Id = id,
                Kind = kind,
                Title = title,
                Subsystem = subsystem,
                Urgency = urgency,
                Severity = severity,
                TtlSec = ttlSec,
                LatentRisk = latentRisk,
                MismatchScore = mismatchScore,
                AssignedPersonnel = new List<string>(assignedPersonnel),
                PhysicalCost = physicalCost,
                MentalCost = mentalCost,
                BaseSuccessChance = baseSuccessChance,
                RequiredAptitudes = requiredAptitudes.ToDictionary(),
                PerkTags = new List<string>(perkTags),
                PerkInteractionInfo = perkInteractionInfo
            };
        }
    }

    [Serializable]
    public sealed class TruthFrameDefinition
    {
        public string id = "";
        public string eventId = "";
        public int tick;
        public string actorId = "";
        public string actionCode = "";
        [TextArea(2, 5)] public string factBlob = "";

        public TruthFrame ToModel()
        {
            return new TruthFrame
            {
                Id = id,
                EventId = eventId,
                Tick = tick,
                ActorId = actorId,
                ActionCode = actionCode,
                FactBlob = factBlob
            };
        }
    }

    [Serializable]
    public sealed class VisibleLogDefinition
    {
        public string id = "";
        public string eventId = "";
        public string sourceType = "";
        public int visibleAtSec;
        [TextArea(2, 5)] public string text = "";
        public bool omitted;
        public bool distorted;
        public bool delayed;
        public bool announced;
        public bool read;

        public VisibleLog ToModel()
        {
            return new VisibleLog
            {
                Id = id,
                EventId = eventId,
                SourceType = sourceType,
                VisibleAtSec = visibleAtSec,
                Text = text,
                Omitted = omitted,
                Distorted = distorted,
                Delayed = delayed,
                Announced = announced,
                Read = read
            };
        }
    }

    [Serializable]
    public sealed class AptitudeBlock
    {
        public int observation;
        public int dexterity;
        public int boldness;
        public int intuition;
        public int logic;

        public Dictionary<string, int> ToDictionary()
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["observation"] = observation,
                ["dexterity"] = dexterity,
                ["boldness"] = boldness,
                ["intuition"] = intuition,
                ["logic"] = logic
            };
        }
    }

    [Serializable]
    public sealed class StringIntPair
    {
        public string key = "";
        public int value;

        public static Dictionary<string, int> ToDictionary(IEnumerable<StringIntPair> pairs)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in pairs ?? Array.Empty<StringIntPair>())
            {
                if (!string.IsNullOrWhiteSpace(pair.key))
                {
                    result[pair.key] = pair.value;
                }
            }

            return result;
        }
    }
}
