using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Simulation
{
    [Serializable]
    public sealed class DecisionTraceLog
    {
        public string DecisionTraceId;
        public List<string> CandidateActions = new List<string>();
        public List<float> CandidateScores = new List<float>();
        public List<string> BlockedActions = new List<string>();
    }

    [Serializable]
    public sealed class TickLogRecord
    {
        public string SessionId;
        public int TickIndex;
        public string LoopState;
        public string StateTransition;
        public List<string> AppliedInterventionIds = new List<string>();
        public List<string> RejectedInterventionIds = new List<string>();
        public string SelectedActionId;
        public int Seed;
        public List<string> ErrorCodes = new List<string>();
        public DecisionTraceLog DecisionTrace;
    }

    public readonly struct ReplayVerificationResult
    {
        public bool IsMatch { get; }
        public IReadOnlyList<string> ErrorCodes { get; }

        public ReplayVerificationResult(bool isMatch, IReadOnlyList<string> errorCodes)
        {
            IsMatch = isMatch;
            ErrorCodes = errorCodes;
        }
    }
}
