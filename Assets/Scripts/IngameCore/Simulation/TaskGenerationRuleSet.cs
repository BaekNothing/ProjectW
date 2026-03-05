using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameCore.Simulation
{
    [Serializable]
    public sealed class TaskTemplateRule
    {
        public string TemplateId = "task.default";
        public string NeedKey = "work";
        public string ZoneKey = "workzone";
        public string ZoneAffinity = "workzone";
        public WorkType WorkType = WorkType.Routine;
        [Range(0f, 100f)] public float Weight = 1f;
        [Range(0f, 1f)] public float Priority = 0.5f;
        [Min(1)] public int MinWorkUnits = 1;
        [Min(1)] public int MaxWorkUnits = 25;
        public string[] RequiredTags = Array.Empty<string>();
        public string[] RequiredTagPool = Array.Empty<string>();
        public Vector2Int BaseWorkUnitsRange = new Vector2Int(4, 10);
    }

    [CreateAssetMenu(fileName = "TaskGenerationRuleSet", menuName = "ProjectW/Generation/Task Rule Set")]
    public sealed class TaskGenerationRuleSet : ScriptableObject
    {
        [SerializeField] private string ruleSetId = "task-rules.default";
        [SerializeField, Range(0f, 1f)] private float workUnitJitterRatio = 0.2f;
        [SerializeField, Range(0.1f, 3f)] private float easyObjectSupplyScale = 1.25f;
        [SerializeField, Range(0.1f, 3f)] private float normalObjectSupplyScale = 1f;
        [SerializeField, Range(0.1f, 3f)] private float riskyObjectSupplyScale = 0.65f;
        [SerializeField, Range(1f, 3f)] private float primaryPriorityWeight = 1.8f;
        [SerializeField, Range(1f, 2f)] private float secondaryPriorityWeight = 1.3f;
        [SerializeField] private TaskTemplateRule[] templates = Array.Empty<TaskTemplateRule>();

        public string RuleSetId => string.IsNullOrWhiteSpace(ruleSetId) ? name : ruleSetId.Trim();
        public float WorkUnitJitterRatio => Mathf.Clamp01(workUnitJitterRatio);
        public float PrimaryPriorityWeight => Mathf.Max(1f, primaryPriorityWeight);
        public float SecondaryPriorityWeight => Mathf.Max(1f, secondaryPriorityWeight);
        public IReadOnlyList<TaskTemplateRule> Templates => templates ?? Array.Empty<TaskTemplateRule>();

        public bool HasTemplates()
        {
            return templates != null && templates.Length > 0;
        }

        public float GetDifficultySupplyScale(SessionDifficulty difficulty)
        {
            switch (difficulty)
            {
                case SessionDifficulty.Easy:
                    return Mathf.Max(0.1f, easyObjectSupplyScale);
                case SessionDifficulty.Risky:
                    return Mathf.Max(0.1f, riskyObjectSupplyScale);
                default:
                    return Mathf.Max(0.1f, normalObjectSupplyScale);
            }
        }

        public float GetPriorityWeight(WorkType type, PriorityPair priorityPair)
        {
            if (type == priorityPair.PrimaryWorkType)
            {
                return PrimaryPriorityWeight;
            }

            if (type == priorityPair.SecondaryWorkType)
            {
                return SecondaryPriorityWeight;
            }

            return 1f;
        }
    }
}
