using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore
{
    public enum SessionTerminationReason
    {
        None,
        ObjectiveComplete,
        EmergencyExtract,
        TotalWipe
    }

    public readonly struct SessionEndResult
    {
        public bool IsEnd { get; }
        public SessionTerminationReason EndReason { get; }
        public string EndReasonCode { get; }
        public IReadOnlyList<SessionTerminationReason> SuppressedReasons { get; }

        public SessionEndResult(
            bool isEnd,
            SessionTerminationReason endReason,
            IReadOnlyList<SessionTerminationReason> suppressedReasons)
        {
            IsEnd = isEnd;
            EndReason = endReason;
            EndReasonCode = endReason == SessionTerminationReason.None ? string.Empty : endReason.ToString();
            SuppressedReasons = suppressedReasons ?? Array.Empty<SessionTerminationReason>();
        }
    }

    public static class SessionEndResolver
    {
        private static readonly SessionTerminationReason[] PriorityOrder =
        {
            SessionTerminationReason.TotalWipe,
            SessionTerminationReason.EmergencyExtract,
            SessionTerminationReason.ObjectiveComplete
        };

        public static SessionEndResult ResolveSessionEnd(bool totalWipe, bool emergencyExtract, bool objectiveComplete)
        {
            var activeReasons = new HashSet<SessionTerminationReason>();
            if (totalWipe)
            {
                activeReasons.Add(SessionTerminationReason.TotalWipe);
            }

            if (emergencyExtract)
            {
                activeReasons.Add(SessionTerminationReason.EmergencyExtract);
            }

            if (objectiveComplete)
            {
                activeReasons.Add(SessionTerminationReason.ObjectiveComplete);
            }

            if (activeReasons.Count == 0)
            {
                return new SessionEndResult(false, SessionTerminationReason.None, Array.Empty<SessionTerminationReason>());
            }

            SessionTerminationReason selected = SessionTerminationReason.None;
            var suppressed = new List<SessionTerminationReason>();
            foreach (var reason in PriorityOrder)
            {
                if (!activeReasons.Contains(reason))
                {
                    continue;
                }

                if (selected == SessionTerminationReason.None)
                {
                    selected = reason;
                    continue;
                }

                suppressed.Add(reason);
            }

            return new SessionEndResult(true, selected, suppressed);
        }
    }
}
