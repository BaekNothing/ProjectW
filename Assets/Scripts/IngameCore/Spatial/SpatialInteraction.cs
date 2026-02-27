using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameCore.Spatial
{
    [Flags]
    public enum InteractableCategory
    {
        None = 0,
        Character = 1 << 0,
        Furniture = 1 << 1,
        Prop = 1 << 2,
        Tool = 1 << 3,
        Any = ~0
    }

    public readonly struct InteractableEntity
    {
        public string EntityId { get; }
        public Vector2 Position { get; }
        public float InteractionRadius { get; }
        public int InteractionPriority { get; }
        public InteractableCategory Category { get; }

        public InteractableEntity(string entityId, Vector2 position, float interactionRadius, int interactionPriority, InteractableCategory category)
        {
            EntityId = entityId;
            Position = position;
            InteractionRadius = Mathf.Max(0f, interactionRadius);
            InteractionPriority = interactionPriority;
            Category = category;
        }
    }

    public static class SpatialQueryService
    {
        public static List<InteractableEntity> FindCandidates(Vector2 sourcePosition, float queryRadius, InteractableCategory categoryMask, IReadOnlyList<InteractableEntity> entities)
        {
            var candidates = new List<(InteractableEntity entity, float distanceSq)>();
            var queryRadiusSq = Mathf.Max(0f, queryRadius) * Mathf.Max(0f, queryRadius);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if ((entity.Category & categoryMask) == 0)
                {
                    continue;
                }

                var distanceSq = (entity.Position - sourcePosition).sqrMagnitude;
                var maxRange = queryRadius + entity.InteractionRadius;
                if (distanceSq > maxRange * maxRange || distanceSq > queryRadiusSq + (entity.InteractionRadius * entity.InteractionRadius) + (2 * queryRadius * entity.InteractionRadius))
                {
                    continue;
                }

                candidates.Add((entity, distanceSq));
            }

            candidates.Sort((a, b) =>
            {
                var distanceComparison = a.distanceSq.CompareTo(b.distanceSq);
                if (distanceComparison != 0)
                {
                    return distanceComparison;
                }

                var priorityComparison = b.entity.InteractionPriority.CompareTo(a.entity.InteractionPriority);
                if (priorityComparison != 0)
                {
                    return priorityComparison;
                }

                return string.CompareOrdinal(a.entity.EntityId, b.entity.EntityId);
            });

            var sorted = new List<InteractableEntity>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                sorted.Add(candidates[i].entity);
            }

            return sorted;
        }
    }
}
