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
        [Range(0f, 100f)] public float Weight = 1f;
        [Range(0f, 1f)] public float Priority = 0.5f;
        [Min(1)] public int MinWorkUnits = 1;
        [Min(1)] public int MaxWorkUnits = 25;
        public string[] RequiredTags = Array.Empty<string>();
    }

    [CreateAssetMenu(fileName = "TaskGenerationRuleSet", menuName = "ProjectW/Generation/Task Rule Set")]
    public sealed class TaskGenerationRuleSet : ScriptableObject
    {
        [SerializeField] private string ruleSetId = "task-rules.default";
        [SerializeField, Range(0f, 1f)] private float workUnitJitterRatio = 0.2f;
        [SerializeField] private TaskTemplateRule[] templates = Array.Empty<TaskTemplateRule>();

        public string RuleSetId => string.IsNullOrWhiteSpace(ruleSetId) ? name : ruleSetId.Trim();
        public float WorkUnitJitterRatio => Mathf.Clamp01(workUnitJitterRatio);
        public IReadOnlyList<TaskTemplateRule> Templates => templates ?? Array.Empty<TaskTemplateRule>();

        public bool HasTemplates()
        {
            return templates != null && templates.Length > 0;
        }
    }
}
