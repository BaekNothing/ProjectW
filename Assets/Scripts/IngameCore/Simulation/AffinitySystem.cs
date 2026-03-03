using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    public enum AffinityEventType
    {
        PersonalItemViolated,
        ItemConflict,
        WorkCompletedQuickly,
        WorkOverloaded,
        HelpReceived,
        HelpProvided
    }

    public readonly struct AffinityEvent
    {
        public readonly string SourceAgentId;
        public readonly string TargetAgentId;
        public readonly AffinityEventType EventType;
        public readonly float Delta;

        public AffinityEvent(string sourceAgentId, string targetAgentId, AffinityEventType eventType, float delta)
        {
            SourceAgentId = sourceAgentId;
            TargetAgentId = targetAgentId;
            EventType = eventType;
            Delta = delta;
        }
    }

    public sealed class AffinitySystem
    {
        private readonly Dictionary<string, float> _scores = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public float GetAffinity(string sourceAgentId, string targetAgentId)
        {
            if (!TryBuildKey(sourceAgentId, targetAgentId, out var key))
            {
                return 0f;
            }

            return _scores.TryGetValue(key, out var score) ? score : 0f;
        }

        public float ApplyEvent(AffinityEvent affinityEvent)
        {
            if (!TryBuildKey(affinityEvent.SourceAgentId, affinityEvent.TargetAgentId, out var key))
            {
                return 0f;
            }

            _scores.TryGetValue(key, out var current);
            var next = Math.Clamp(current + affinityEvent.Delta, -100f, 100f);
            _scores[key] = next;
            return next;
        }

        public List<AffinityEvent> BuildItemUsageEvents(string userAgentId, WorldItem item, string observerAgentId, bool escalatedConflict)
        {
            var events = new List<AffinityEvent>();
            if (item == null || item.UsagePolicy != ItemUsagePolicy.Personal)
            {
                return events;
            }

            if (string.IsNullOrWhiteSpace(item.OwnerAgentId)
                || string.Equals(item.OwnerAgentId, userAgentId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(observerAgentId)
                || !string.Equals(item.OwnerAgentId, observerAgentId, StringComparison.OrdinalIgnoreCase))
            {
                return events;
            }

            events.Add(new AffinityEvent(observerAgentId, userAgentId, AffinityEventType.PersonalItemViolated, -8f));
            if (escalatedConflict)
            {
                events.Add(new AffinityEvent(observerAgentId, userAgentId, AffinityEventType.ItemConflict, -6f));
                events.Add(new AffinityEvent(userAgentId, observerAgentId, AffinityEventType.ItemConflict, -6f));
            }

            return events;
        }

        public List<AffinityEvent> BuildWorkOutcomeEvents(string workerId, bool completedQuickly, bool overloaded, string helperId)
        {
            var events = new List<AffinityEvent>();
            if (completedQuickly)
            {
                events.Add(new AffinityEvent(workerId, workerId, AffinityEventType.WorkCompletedQuickly, 3f));
            }

            if (overloaded)
            {
                events.Add(new AffinityEvent(workerId, workerId, AffinityEventType.WorkOverloaded, -4f));
            }

            if (!string.IsNullOrWhiteSpace(helperId) && !string.Equals(helperId, workerId, StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AffinityEvent(workerId, helperId, AffinityEventType.HelpReceived, 5f));
                events.Add(new AffinityEvent(helperId, workerId, AffinityEventType.HelpProvided, 2f));
            }

            return events;
        }

        private static bool TryBuildKey(string sourceAgentId, string targetAgentId, out string key)
        {
            key = string.Empty;
            if (string.IsNullOrWhiteSpace(sourceAgentId) || string.IsNullOrWhiteSpace(targetAgentId))
            {
                return false;
            }

            key = sourceAgentId.Trim() + "=>" + targetAgentId.Trim();
            return true;
        }
    }
}
