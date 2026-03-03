using System;
using System.Collections.Generic;

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

        public static List<WorldItem> GenerateOfficeItems(Random random, int desiredCount, IReadOnlyList<string> candidateOwners)
        {
            var rng = random ?? new Random(1);
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
                    template.Tags);
                items.Add(item);
            }

            return items;
        }

        private static WorldItem BuildTaggedItem(Random random, int index, string tag, IReadOnlyList<string> candidateOwners)
        {
            var policy = random.NextDouble() < 0.5 ? ItemUsagePolicy.Personal : ItemUsagePolicy.Public;
            var owner = policy == ItemUsagePolicy.Personal
                ? PickOwner(random, candidateOwners)
                : string.Empty;
            return new WorldItem($"office-base-{index:00}", $"Office {tag}", policy, owner, new[] { tag });
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
