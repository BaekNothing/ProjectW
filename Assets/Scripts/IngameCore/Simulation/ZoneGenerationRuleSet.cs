using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameCore.Simulation
{
    [Serializable]
    public sealed class ItemTemplateRule
    {
        public string TemplateId = "template.default";
        public string DisplayName = "Generated Item";
        [Range(0f, 100f)] public float Weight = 1f;
        public string[] Tags = Array.Empty<string>();
        public string SpriteResourcePath;
    }

    [Serializable]
    public sealed class ZoneTypeRule
    {
        public string ZoneKey = "workzone";
        public string ZoneTag = "zone.mission";
        [Min(1)] public int MinItemCount = 4;
        [Min(1)] public int MaxItemCount = 9;
        [Range(0f, 1f)] public float PersonalRatioMin = 0.2f;
        [Range(0f, 1f)] public float PersonalRatioMax = 0.5f;
        public string[] RequiredTags = Array.Empty<string>();
        public ItemTemplateRule[] ItemTemplates = Array.Empty<ItemTemplateRule>();
    }

    [CreateAssetMenu(fileName = "ZoneGenerationRuleSet", menuName = "ProjectW/Generation/Zone Rule Set")]
    public sealed class ZoneGenerationRuleSet : ScriptableObject
    {
        [SerializeField] private string ruleSetId = "zone-rules.default";
        [SerializeField] private ZoneTypeRule[] zoneRules = Array.Empty<ZoneTypeRule>();

        public string RuleSetId => string.IsNullOrWhiteSpace(ruleSetId) ? name : ruleSetId.Trim();
        public IReadOnlyList<ZoneTypeRule> ZoneRules => zoneRules ?? Array.Empty<ZoneTypeRule>();

        public bool TryGetRuleForZone(string zoneKey, out ZoneTypeRule rule)
        {
            rule = null;
            if (string.IsNullOrWhiteSpace(zoneKey) || zoneRules == null)
            {
                return false;
            }

            var normalized = zoneKey.Trim();
            for (var i = 0; i < zoneRules.Length; i++)
            {
                var candidate = zoneRules[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.ZoneKey))
                {
                    continue;
                }

                if (string.Equals(candidate.ZoneKey.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    rule = candidate;
                    return true;
                }
            }

            return false;
        }

        public string ResolveSpriteResourcePath(string itemTag)
        {
            if (string.IsNullOrWhiteSpace(itemTag) || zoneRules == null)
            {
                return null;
            }

            var normalizedTag = itemTag.Trim();
            for (var i = 0; i < zoneRules.Length; i++)
            {
                var zone = zoneRules[i];
                if (zone?.ItemTemplates == null)
                {
                    continue;
                }

                for (var j = 0; j < zone.ItemTemplates.Length; j++)
                {
                    var template = zone.ItemTemplates[j];
                    if (template == null || string.IsNullOrWhiteSpace(template.SpriteResourcePath))
                    {
                        continue;
                    }

                    var tags = template.Tags;
                    if (tags == null)
                    {
                        continue;
                    }

                    for (var k = 0; k < tags.Length; k++)
                    {
                        if (string.Equals(tags[k], normalizedTag, StringComparison.OrdinalIgnoreCase))
                        {
                            return template.SpriteResourcePath.Trim();
                        }
                    }
                }
            }

            return null;
        }
    }
}
