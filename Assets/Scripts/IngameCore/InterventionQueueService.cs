using System;
using System.Collections.Generic;
using System.Linq;
using ProjectW.IngameCore.Config;

namespace ProjectW.IngameCore
{
    public static class InterventionRejectionReasons
    {
        public const string AlreadyApplied = "already_applied";
        public const string TargetNotFound = "target_not_found";
        public const string SelfSupersede = "self_supersede";
        public const string TickNotReached = "tick_not_reached";
        public const string ConflictOverridden = "conflict_overridden";
        public const string Superseded = "superseded";
    }

    public sealed class InterventionDecision
    {
        public string CommandId { get; }
        public bool Applied { get; }
        public string Reason { get; }

        public InterventionDecision(string commandId, bool applied, string reason)
        {
            CommandId = commandId ?? string.Empty;
            Applied = applied;
            Reason = reason ?? string.Empty;
        }
    }

    public sealed class InterventionApplyResult
    {
        public int AppliedCount { get; }
        public int RejectedCount { get; }
        public IReadOnlyList<string> RejectionReasons { get; }
        public IReadOnlyList<string> AppliedCommandIds { get; }
        public IReadOnlyList<string> RejectedCommandIds { get; }
        public int LastAppliedTick { get; }
        public int PendingCount { get; }
        public string RecentRejectedReason { get; }

        public InterventionApplyResult(
            int appliedCount,
            int rejectedCount,
            IReadOnlyList<string> rejectionReasons,
            IReadOnlyList<string> appliedCommandIds,
            IReadOnlyList<string> rejectedCommandIds,
            int lastAppliedTick,
            int pendingCount,
            string recentRejectedReason)
        {
            AppliedCount = appliedCount;
            RejectedCount = rejectedCount;
            RejectionReasons = rejectionReasons ?? Array.Empty<string>();
            AppliedCommandIds = appliedCommandIds ?? Array.Empty<string>();
            RejectedCommandIds = rejectedCommandIds ?? Array.Empty<string>();
            LastAppliedTick = lastAppliedTick;
            PendingCount = pendingCount;
            RecentRejectedReason = string.IsNullOrWhiteSpace(recentRejectedReason) ? "None" : recentRejectedReason;
        }
    }

    internal enum InterventionStatus
    {
        Pending,
        Applied,
        Rejected,
        Superseded
    }

    public sealed class InterventionQueueService
    {
        private readonly Dictionary<string, InterventionStatus> _statuses = new Dictionary<string, InterventionStatus>(StringComparer.Ordinal);
        private int _lastAppliedTick = -1;

        public InterventionApplyResult ApplyInterventions(int currentTick, IReadOnlyList<InterventionCommandRow> queue)
        {
            if (queue == null || queue.Count == 0)
            {
                return new InterventionApplyResult(0, 0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), _lastAppliedTick, 0, "None");
            }

            foreach (var command in queue)
            {
                if (command == null || string.IsNullOrWhiteSpace(command.CommandId))
                {
                    continue;
                }

                if (!_statuses.ContainsKey(command.CommandId))
                {
                    _statuses[command.CommandId] = InterventionStatus.Pending;
                }
            }

            var decisions = new List<InterventionDecision>();
            var pending = queue.Where(IsPending).ToList();

            foreach (var command in pending)
            {
                var effectiveApplyTick = Math.Max(command.ApplyTick, command.IssuedTick + 1);
                if (currentTick < effectiveApplyTick)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(command.SupersedesCommandId))
                {
                    continue;
                }

                var targetId = command.SupersedesCommandId.Trim();
                if (string.Equals(targetId, command.CommandId, StringComparison.Ordinal))
                {
                    Reject(command.CommandId, InterventionRejectionReasons.SelfSupersede, decisions);
                    continue;
                }

                if (!_statuses.TryGetValue(targetId, out var targetStatus))
                {
                    Reject(command.CommandId, InterventionRejectionReasons.TargetNotFound, decisions);
                    continue;
                }

                if (targetStatus == InterventionStatus.Applied)
                {
                    Reject(command.CommandId, InterventionRejectionReasons.AlreadyApplied, decisions);
                    continue;
                }

                if (targetStatus == InterventionStatus.Pending)
                {
                    _statuses[targetId] = InterventionStatus.Superseded;
                    decisions.Add(new InterventionDecision(targetId, false, InterventionRejectionReasons.Superseded));
                }
            }

            pending = queue.Where(IsPending).ToList();
            var ready = pending.Where(command => currentTick >= Math.Max(command.ApplyTick, command.IssuedTick + 1)).ToList();

            var conflictGroups = ready.GroupBy(command => (command.CommandType ?? string.Empty).Trim() + "|" + (command.TargetScope ?? string.Empty).Trim(), StringComparer.Ordinal);
            foreach (var group in conflictGroups)
            {
                var ordered = group
                    .OrderByDescending(command => command.IssuedTick)
                    .ThenByDescending(command => command.Priority)
                    .ThenBy(command => command.CommandId, StringComparer.Ordinal)
                    .ToList();

                if (ordered.Count == 0)
                {
                    continue;
                }

                var winner = ordered[0];
                _statuses[winner.CommandId] = InterventionStatus.Applied;
                decisions.Add(new InterventionDecision(winner.CommandId, true, string.Empty));
                _lastAppliedTick = Math.Max(_lastAppliedTick, currentTick);

                for (int i = 1; i < ordered.Count; i++)
                {
                    Reject(ordered[i].CommandId, InterventionRejectionReasons.ConflictOverridden, decisions);
                }
            }

            var appliedIds = decisions.Where(decision => decision.Applied).Select(decision => decision.CommandId).Distinct(StringComparer.Ordinal).ToArray();
            var rejected = decisions.Where(decision => !decision.Applied).ToArray();
            var rejectedIds = rejected.Select(decision => decision.CommandId).Distinct(StringComparer.Ordinal).ToArray();
            var reasons = rejected.Select(decision => decision.Reason).Where(reason => !string.IsNullOrWhiteSpace(reason)).ToArray();
            var recentReason = reasons.Length > 0 ? reasons[reasons.Length - 1] : "None";

            return new InterventionApplyResult(
                appliedIds.Length,
                rejectedIds.Length,
                reasons,
                appliedIds,
                rejectedIds,
                _lastAppliedTick,
                queue.Count(IsPending),
                recentReason);
        }

        private bool IsPending(InterventionCommandRow command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.CommandId))
            {
                return false;
            }

            return _statuses.TryGetValue(command.CommandId, out var status) && status == InterventionStatus.Pending;
        }

        private void Reject(string commandId, string reason, ICollection<InterventionDecision> decisions)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return;
            }

            _statuses[commandId] = InterventionStatus.Rejected;
            decisions.Add(new InterventionDecision(commandId, false, reason));
        }
    }
}
