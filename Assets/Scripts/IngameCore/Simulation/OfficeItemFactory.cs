using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace ProjectW.IngameCore.Simulation
{
    public static class OfficeItemFactory
    {
        private static readonly string[] BaseTags =
        {
            "desk", "computer", "bed", "pillow", "blanket", "table", "tray", "cup"
        };

        private static readonly (string Name, string[] Tags)[] Templates =
        {
            ("Dev Desk", new[] { "desk" }),
            ("Workstation PC", new[] { "computer" }),
            ("Shared Table", new[] { "table" }),
            ("Meal Tray", new[] { "tray" }),
            ("Mug", new[] { "cup" }),
            ("Nap Bed", new[] { "bed" }),
            ("Book Pile", new[] { "pillow" }),
            ("Coat Stack", new[] { "blanket" }),
            ("Laptop Cart", new[] { "computer", "desk" }),
            ("Bench", new[] { "table" }),
            ("Sofa", new[] { "bed", "blanket" }),
            ("Cushion", new[] { "pillow" }),
            ("Dust Heap", new[] { "pillow" }),
            ("Cabinet", new[] { "desk" }),
            ("Water Cup", new[] { "cup" }),
            ("Food Tray", new[] { "tray" })
        };

        private static readonly Dictionary<string, string> DefaultZoneByTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "desk", "workzone" },
            { "computer", "workzone" },
            { "table", "eatzone" },
            { "tray", "eatzone" },
            { "cup", "eatzone" },
            { "bed", "sleepzone" },
            { "pillow", "sleepzone" },
            { "blanket", "sleepzone" }
        };

        public static List<WorldItem> GenerateOfficeItems(Random random, int desiredCount, IReadOnlyList<string> candidateOwners)
        {
            return GenerateOfficeItems(random, desiredCount, candidateOwners, null);
        }

        public static List<WorldItem> GenerateOfficeItems(
            Random random,
            int desiredCount,
            IReadOnlyList<string> candidateOwners,
            ZoneGenerationRuleSet zoneRuleSet)
        {
            var rng = random ?? new Random(1);
            if (zoneRuleSet != null && zoneRuleSet.ZoneRules.Count > 0)
            {
                return GenerateFromZoneRules(rng, desiredCount, candidateOwners, zoneRuleSet);
            }

            var count = Math.Max(8, desiredCount);
            var items = new List<WorldItem>(count);

            // Ensure baseline tags always exist so all jobs can be satisfiable.
            for (var i = 0; i < BaseTags.Length; i++)
            {
                items.Add(BuildTaggedItem(rng, i, BaseTags[i], candidateOwners));
            }

            while (items.Count < count)
            {
                var template = Templates[rng.Next(0, Templates.Length)];
                var policy = rng.NextDouble() < 0.45 ? ItemUsagePolicy.Personal : ItemUsagePolicy.Public;
                var owner = policy == ItemUsagePolicy.Personal
                    ? PickOwner(rng, candidateOwners)
                    : string.Empty;
                var item = new WorldItem(
                    $"office-{items.Count + 1:00}",
                    template.Name,
                    policy,
                    owner,
                    template.Tags,
                    ResolveZoneFromTags(template.Tags));
                items.Add(item);
            }

            return items;
        }

        private static List<WorldItem> GenerateFromZoneRules(
            Random random,
            int desiredCount,
            IReadOnlyList<string> candidateOwners,
            ZoneGenerationRuleSet zoneRuleSet)
        {
            var items = new List<WorldItem>(Math.Max(8, desiredCount));
            var index = 0;
            var rules = zoneRuleSet.ZoneRules;
            for (var i = 0; i < rules.Count; i++)
            {
                var zoneRule = rules[i];
                if (zoneRule == null || string.IsNullOrWhiteSpace(zoneRule.ZoneKey))
                {
                    continue;
                }

                var zoneKey = zoneRule.ZoneKey.Trim();
                var minCount = Mathf.Max(1, zoneRule.MinItemCount);
                var maxCount = Mathf.Max(minCount, zoneRule.MaxItemCount);
                var targetCount = random.Next(minCount, maxCount + 1);
                var requiredCount = 0;
                if (zoneRule.RequiredTags != null)
                {
                    for (var j = 0; j < zoneRule.RequiredTags.Length; j++)
                    {
                        if (!string.IsNullOrWhiteSpace(zoneRule.RequiredTags[j]))
                        {
                            requiredCount += 1;
                        }
                    }
                }
                targetCount = Math.Max(targetCount, requiredCount);
                var personalRatio = Mathf.Lerp(
                    Mathf.Clamp01(zoneRule.PersonalRatioMin),
                    Mathf.Clamp01(zoneRule.PersonalRatioMax),
                    (float)random.NextDouble());
                var personalQuota = Mathf.Clamp(Mathf.RoundToInt(targetCount * personalRatio), 0, targetCount);
                var createdCount = 0;
                var personalAssigned = 0;

                // Required tags are generated first so every zone stays satisfiable.
                if (zoneRule.RequiredTags != null)
                {
                    for (var j = 0; j < zoneRule.RequiredTags.Length; j++)
                    {
                        var requiredTag = zoneRule.RequiredTags[j];
                        if (string.IsNullOrWhiteSpace(requiredTag))
                        {
                            continue;
                        }

                        var template = PickTemplateForTag(zoneRule, requiredTag.Trim(), random);
                        var policy = ResolvePolicyForQuota(random, targetCount, personalQuota, createdCount, personalAssigned);
                        var item = BuildRuleItem(random, zoneKey, template, requiredTag.Trim(), policy, candidateOwners, ref index);
                        items.Add(item);
                        createdCount += 1;
                        if (policy == ItemUsagePolicy.Personal)
                        {
                            personalAssigned += 1;
                        }
                    }
                }

                while (CountByZone(items, zoneKey) < targetCount)
                {
                    var template = PickWeightedTemplate(zoneRule, random);
                    var fallbackTag = ResolvePrimaryTag(template?.Tags, "generic");
                    var policy = ResolvePolicyForQuota(random, targetCount, personalQuota, createdCount, personalAssigned);
                    var item = BuildRuleItem(random, zoneKey, template, fallbackTag, policy, candidateOwners, ref index);
                    items.Add(item);
                    createdCount += 1;
                    if (policy == ItemUsagePolicy.Personal)
                    {
                        personalAssigned += 1;
                    }
                }
            }

            if (items.Count == 0)
            {
                items.AddRange(GenerateOfficeItems(random, desiredCount, candidateOwners));
            }

            return items;
        }

        private static WorldItem BuildRuleItem(
            Random random,
            string zoneKey,
            ItemTemplateRule template,
            string fallbackTag,
            ItemUsagePolicy policy,
            IReadOnlyList<string> candidateOwners,
            ref int index)
        {
            var tags = (template?.Tags != null && template.Tags.Length > 0)
                ? template.Tags
                : new[] { fallbackTag };
            var owner = policy == ItemUsagePolicy.Personal
                ? PickOwner(random, candidateOwners)
                : string.Empty;
            index += 1;
            var displayName = string.IsNullOrWhiteSpace(template?.DisplayName)
                ? $"{zoneKey} {ResolvePrimaryTag(tags, fallbackTag)}"
                : template.DisplayName.Trim();
            return new WorldItem(
                $"{zoneKey}-{index:000}",
                displayName,
                policy,
                owner,
                tags,
                zoneKey);
        }

        private static ItemUsagePolicy ResolvePolicyForQuota(
            Random random,
            int targetCount,
            int personalQuota,
            int createdCount,
            int personalAssigned)
        {
            if (personalAssigned >= personalQuota)
            {
                return ItemUsagePolicy.Public;
            }

            var remainingSlots = Math.Max(1, targetCount - createdCount);
            var remainingPersonal = Math.Max(0, personalQuota - personalAssigned);
            if (remainingSlots <= remainingPersonal)
            {
                return ItemUsagePolicy.Personal;
            }

            var ratio = (double)remainingPersonal / remainingSlots;
            return random.NextDouble() < ratio ? ItemUsagePolicy.Personal : ItemUsagePolicy.Public;
        }

        private static WorldItem BuildTaggedItem(Random random, int index, string tag, IReadOnlyList<string> candidateOwners)
        {
            var policy = random.NextDouble() < 0.5 ? ItemUsagePolicy.Personal : ItemUsagePolicy.Public;
            var owner = policy == ItemUsagePolicy.Personal
                ? PickOwner(random, candidateOwners)
                : string.Empty;
            return new WorldItem($"office-base-{index:00}", $"Office {tag}", policy, owner, new[] { tag }, ResolveZoneFromTags(new[] { tag }));
        }

        private static ItemTemplateRule PickTemplateForTag(ZoneTypeRule rule, string requiredTag, Random random)
        {
            if (rule?.ItemTemplates == null || rule.ItemTemplates.Length == 0)
            {
                return null;
            }

            var matches = new List<ItemTemplateRule>();
            for (var i = 0; i < rule.ItemTemplates.Length; i++)
            {
                var template = rule.ItemTemplates[i];
                if (template?.Tags == null)
                {
                    continue;
                }

                for (var j = 0; j < template.Tags.Length; j++)
                {
                    if (string.Equals(template.Tags[j], requiredTag, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(template);
                        break;
                    }
                }
            }

            if (matches.Count == 0)
            {
                return null;
            }

            return matches[random.Next(0, matches.Count)];
        }

        private static ItemTemplateRule PickWeightedTemplate(ZoneTypeRule rule, Random random)
        {
            if (rule?.ItemTemplates == null || rule.ItemTemplates.Length == 0)
            {
                return null;
            }

            var total = 0f;
            for (var i = 0; i < rule.ItemTemplates.Length; i++)
            {
                total += Mathf.Max(0f, rule.ItemTemplates[i]?.Weight ?? 0f);
            }

            if (total <= 0.0001f)
            {
                return rule.ItemTemplates[random.Next(0, rule.ItemTemplates.Length)];
            }

            var roll = (float)random.NextDouble() * total;
            var sum = 0f;
            for (var i = 0; i < rule.ItemTemplates.Length; i++)
            {
                sum += Mathf.Max(0f, rule.ItemTemplates[i]?.Weight ?? 0f);
                if (roll <= sum)
                {
                    return rule.ItemTemplates[i];
                }
            }

            return rule.ItemTemplates[rule.ItemTemplates.Length - 1];
        }

        private static int CountByZone(List<WorldItem> items, string zoneKey)
        {
            var count = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i]?.ZoneKey, zoneKey, StringComparison.OrdinalIgnoreCase))
                {
                    count += 1;
                }
            }

            return count;
        }

        private static string ResolvePrimaryTag(IReadOnlyList<string> tags, string fallback)
        {
            if (tags == null || tags.Count == 0 || string.IsNullOrWhiteSpace(tags[0]))
            {
                return string.IsNullOrWhiteSpace(fallback) ? "generic" : fallback.Trim();
            }

            return tags[0].Trim();
        }

        private static string ResolveZoneFromTags(IReadOnlyList<string> tags)
        {
            if (tags != null)
            {
                for (var i = 0; i < tags.Count; i++)
                {
                    var tag = tags[i];
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    if (DefaultZoneByTag.TryGetValue(tag.Trim(), out var zone))
                    {
                        return zone;
                    }
                }
            }

            return "unknown";
        }

        private static string PickOwner(Random random, IReadOnlyList<string> candidateOwners)
        {
            if (candidateOwners == null || candidateOwners.Count == 0)
            {
                return string.Empty;
            }

            return candidateOwners[random.Next(0, candidateOwners.Count)] ?? string.Empty;
        }
    }
}
