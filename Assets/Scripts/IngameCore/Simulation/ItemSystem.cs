using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    public enum ItemUsagePolicy
    {
        Public,
        Personal
    }

    [Serializable]
    public sealed class WorldItem
    {
        private readonly HashSet<string> _tags;

        public string Id { get; }
        public string DisplayName { get; }
        public ItemUsagePolicy UsagePolicy { get; }
        public string OwnerAgentId { get; }
        public string ZoneKey { get; }
        public IReadOnlyCollection<string> Tags => _tags;

        public WorldItem(string id, string displayName, ItemUsagePolicy usagePolicy, string ownerAgentId, IEnumerable<string> tags, string zoneKey = "unknown")
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
            UsagePolicy = usagePolicy;
            OwnerAgentId = string.IsNullOrWhiteSpace(ownerAgentId) ? string.Empty : ownerAgentId.Trim();
            ZoneKey = string.IsNullOrWhiteSpace(zoneKey) ? "unknown" : zoneKey.Trim();
            _tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (tags == null)
            {
                return;
            }

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                _tags.Add(tag.Trim());
            }
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            return _tags.Contains(tag.Trim());
        }

        public bool IsPreferredFor(string agentId)
        {
            if (UsagePolicy == ItemUsagePolicy.Public)
            {
                return true;
            }

            return string.Equals(OwnerAgentId, agentId, StringComparison.OrdinalIgnoreCase);
        }

        public string BuildInspectorSummary()
        {
            var ownerLabel = string.IsNullOrWhiteSpace(OwnerAgentId) ? "None" : OwnerAgentId;
            var tagsLabel = _tags.Count == 0 ? "-" : string.Join(", ", _tags);
            return $"{DisplayName} ({Id}) | zone:{ZoneKey} | policy:{UsagePolicy} | owner:{ownerLabel} | tags:{tagsLabel}";
        }
    }

    [Serializable]
    public sealed class NeedRequirement
    {
        public string NeedKey { get; }
        public string ZoneKey { get; }
        public IReadOnlyList<string> RequiredTags { get; }

        public NeedRequirement(string needKey, string zoneKey, IReadOnlyList<string> requiredTags)
        {
            NeedKey = string.IsNullOrWhiteSpace(needKey) ? "unknown" : needKey.Trim();
            ZoneKey = string.IsNullOrWhiteSpace(zoneKey) ? "unknown" : zoneKey.Trim();
            RequiredTags = requiredTags ?? Array.Empty<string>();
        }

        public bool IsSatisfied(IReadOnlyList<WorldItem> availableItems, string agentId)
        {
            if (RequiredTags.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < RequiredTags.Count; i++)
            {
                var requiredTag = RequiredTags[i];
                if (!HasMatchingItem(availableItems, requiredTag, agentId))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasMatchingItem(IReadOnlyList<WorldItem> availableItems, string requiredTag, string agentId)
        {
            if (availableItems == null)
            {
                return false;
            }

            WorldItem fallback = null;
            for (var i = 0; i < availableItems.Count; i++)
            {
                var item = availableItems[i];
                if (item == null || !item.HasTag(requiredTag))
                {
                    continue;
                }

                if (item.IsPreferredFor(agentId))
                {
                    return true;
                }

                fallback = item;
            }

            return fallback != null;
        }
    }
}
