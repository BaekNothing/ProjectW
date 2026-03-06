using System;
using System.Collections.Generic;
using ProjectW.IngameCore.StateMachine;

namespace ProjectW.IngameCore.Simulation
{
    public sealed class EventLogCollector
    {
        private TickLogRecord currentTick;
        private readonly List<TickLogRecord> tickLogs = new List<TickLogRecord>();

        public IReadOnlyList<TickLogRecord> TickLogs => tickLogs;

        public void BeginTick(string sessionId, int tickIndex, string loopState, int seed)
        {
            currentTick = new TickLogRecord
            {
                SessionId = sessionId,
                TickIndex = tickIndex,
                LoopState = loopState,
                Seed = seed,
                DecisionTrace = new DecisionTraceLog()
            };
        }

        public void RecordStateTransition(CoreLoopState fromState, CoreLoopState toState, string errorCode = null)
        {
            EnsureTick();
            currentTick.StateTransition = $"{fromState}->{toState}";
            if (!string.IsNullOrEmpty(errorCode))
            {
                currentTick.ErrorCodes.Add(errorCode);
            }
        }

        public void RecordInterventionResult(string interventionId, bool applied)
        {
            EnsureTick();
            if (string.IsNullOrEmpty(interventionId))
            {
                return;
            }

            if (applied)
            {
                currentTick.AppliedInterventionIds.Add(interventionId);
            }
            else
            {
                currentTick.RejectedInterventionIds.Add(interventionId);
            }
        }

        public void RecordNeuronDecision(CharacterNeuronIntent intent)
        {
            EnsureTick();
            if (currentTick.DecisionTrace == null)
            {
                currentTick.DecisionTrace = new DecisionTraceLog();
            }

            if (currentTick.DecisionTrace.CandidateActions.Count == 0)
            {
                currentTick.DecisionTrace.CandidateActions.Add(CharacterNeuronIntent.Work.ToString());
                currentTick.DecisionTrace.CandidateActions.Add(CharacterNeuronIntent.Eat.ToString());
                currentTick.DecisionTrace.CandidateActions.Add(CharacterNeuronIntent.Sleep.ToString());
                currentTick.DecisionTrace.CandidateScores.Add(0f);
                currentTick.DecisionTrace.CandidateScores.Add(0f);
                currentTick.DecisionTrace.CandidateScores.Add(0f);
            }

            currentTick.SelectedActionId = intent.ToString();
        }

        public void RecordJobDecision(JobDecisionTrace trace)
        {
            EnsureTick();
            if (trace.CandidateActions == null)
            {
                return;
            }

            currentTick.DecisionTrace = new DecisionTraceLog
            {
                DecisionTraceId = trace.DecisionTraceId,
                CandidateActions = new List<string>(trace.CandidateActions),
                CandidateScores = new List<float>(trace.CandidateScores),
                BlockedActions = new List<string>(trace.BlockedActions)
            };

            if (!string.IsNullOrEmpty(trace.SelectedActionId))
            {
                currentTick.SelectedActionId = trace.SelectedActionId;
            }
        }


        public void RecordKnowledgeTransfer(
            string fromAgentId,
            string toAgentId,
            string knowledgeKey,
            KnowledgeSourceType sourceType,
            bool success,
            float probability,
            float beforeConfidence,
            float afterConfidence,
            bool distorted,
            string reason)
        {
            EnsureTick();
            var message = $"knowledge_transfer from={NormalizeLogValue(fromAgentId)} to={NormalizeLogValue(toAgentId)} key={NormalizeLogValue(knowledgeKey)} source={sourceType} success={success} distorted={distorted} probability={probability:0.00} confidence={beforeConfidence:0.00}->{afterConfidence:0.00} reason={NormalizeLogValue(reason)}";
            currentTick.KnowledgeEvents.Add(message);
        }

        public void RecordKnowledgeFailure(
            string fromAgentId,
            string toAgentId,
            string knowledgeKey,
            KnowledgeSourceType sourceType,
            float probability,
            string reason)
        {
            EnsureTick();
            var message = $"knowledge_failure from={NormalizeLogValue(fromAgentId)} to={NormalizeLogValue(toAgentId)} key={NormalizeLogValue(knowledgeKey)} source={sourceType} probability={probability:0.00} reason={NormalizeLogValue(reason)}";
            currentTick.KnowledgeEvents.Add(message);
        }
        public TickLogRecord EndTick()
        {
            EnsureTick();
            tickLogs.Add(currentTick);
            var finalized = currentTick;
            currentTick = null;
            return finalized;
        }

        private static string NormalizeLogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "n/a" : value.Trim();
        }

        private void EnsureTick()
        {
            if (currentTick == null)
            {
                throw new InvalidOperationException("BeginTick must be called before recording events.");
            }
        }
    }

    public sealed class ReplayVerifier
    {
        public ReplayVerificationResult Verify(IReadOnlyList<TickLogRecord> expected, IReadOnlyList<TickLogRecord> actual)
        {
            var errorCodes = new List<string>();
            var differences = new List<string>();
            var max = Math.Max(expected?.Count ?? 0, actual?.Count ?? 0);

            for (var i = 0; i < max; i++)
            {
                var left = i < (expected?.Count ?? 0) ? expected[i] : null;
                var right = i < (actual?.Count ?? 0) ? actual[i] : null;

                if (!CoreFieldsMatch(left, right, out var difference))
                {
                    errorCodes.Add("E-RPL-001");
                    if (!string.IsNullOrWhiteSpace(difference))
                    {
                        differences.Add($"tick[{i}]: {difference}");
                    }
                }
            }

            return new ReplayVerificationResult(errorCodes.Count == 0, errorCodes, differences);
        }

        private static bool CoreFieldsMatch(TickLogRecord left, TickLogRecord right, out string difference)
        {
            difference = string.Empty;
            if (left == null || right == null)
            {
                difference = "missing_tick_record";
                return false;
            }

            if (left.TickIndex != right.TickIndex)
            {
                difference = $"tick_index({left.TickIndex}!={right.TickIndex})";
                return false;
            }

            if (!string.Equals(left.LoopState, right.LoopState, StringComparison.Ordinal))
            {
                difference = $"loop_state({left.LoopState}!={right.LoopState})";
                return false;
            }

            if (!string.Equals(left.StateTransition, right.StateTransition, StringComparison.Ordinal))
            {
                difference = $"state_transition({left.StateTransition}!={right.StateTransition})";
                return false;
            }

            if (!string.Equals(left.SelectedActionId, right.SelectedActionId, StringComparison.Ordinal))
            {
                difference = $"selected_action({left.SelectedActionId}!={right.SelectedActionId})";
                return false;
            }

            if (left.Seed != right.Seed)
            {
                difference = $"seed({left.Seed}!={right.Seed})";
                return false;
            }

            if (!SetEquals(left.AppliedInterventionIds, right.AppliedInterventionIds))
            {
                difference = "applied_interventions_mismatch";
                return false;
            }

            if (!SetEquals(left.RejectedInterventionIds, right.RejectedInterventionIds))
            {
                difference = "rejected_interventions_mismatch";
                return false;
            }

            return true;
        }

        private static bool SetEquals(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            var leftSet = new HashSet<string>(left ?? Array.Empty<string>(), StringComparer.Ordinal);
            var rightSet = new HashSet<string>(right ?? Array.Empty<string>(), StringComparer.Ordinal);
            return leftSet.SetEquals(rightSet);
        }
    }
}
