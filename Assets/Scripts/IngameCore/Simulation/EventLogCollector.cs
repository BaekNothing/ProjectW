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

        public TickLogRecord EndTick()
        {
            EnsureTick();
            tickLogs.Add(currentTick);
            var finalized = currentTick;
            currentTick = null;
            return finalized;
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
            var max = Math.Max(expected?.Count ?? 0, actual?.Count ?? 0);

            for (var i = 0; i < max; i++)
            {
                var left = i < (expected?.Count ?? 0) ? expected[i] : null;
                var right = i < (actual?.Count ?? 0) ? actual[i] : null;

                if (!CoreFieldsMatch(left, right))
                {
                    errorCodes.Add("E-RPL-001");
                }
            }

            return new ReplayVerificationResult(errorCodes.Count == 0, errorCodes);
        }

        private static bool CoreFieldsMatch(TickLogRecord left, TickLogRecord right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return left.TickIndex == right.TickIndex
                   && string.Equals(left.LoopState, right.LoopState, StringComparison.Ordinal)
                   && string.Equals(left.StateTransition, right.StateTransition, StringComparison.Ordinal)
                   && string.Equals(left.SelectedActionId, right.SelectedActionId, StringComparison.Ordinal)
                   && left.Seed == right.Seed;
        }
    }
}
