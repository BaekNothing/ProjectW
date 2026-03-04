using System;
using System.Collections.Generic;

namespace ProjectW.IngameCore.Config
{
    [Serializable]
    public sealed class SessionConfig
    {
        public string SessionId { get; set; }
        public float TickSeconds { get; set; }
        public int MaxDecisionRetry { get; set; }
        public int MaxPersistRetry { get; set; }
        public int PersistRetryBackoffMs { get; set; }
    }

    [Serializable]
    public sealed class StateTransitionRuleRow
    {
        public string FromState { get; set; }
        public string ToState { get; set; }
        public string EntryCondition { get; set; }
        public string ExitCondition { get; set; }
        public string Guard { get; set; }
        public int Priority { get; set; }
        public bool Enabled { get; set; }
    }

    [Serializable]
    public sealed class CharacterProfileRow
    {
        public string CharacterId { get; set; }
        public string TraitWeightsJson { get; set; }
        public float Stress { get; set; }
        public float Health { get; set; }
        public int TraumaLevel { get; set; }
        public bool Enabled { get; set; }
    }

    [Serializable]
    public sealed class InterventionCommandRow
    {
        public string CommandId { get; set; }
        public int IssuedTick { get; set; }
        public int ApplyTick { get; set; }
        public string CommandType { get; set; }
        public string TargetScope { get; set; }
        public string PayloadJson { get; set; }
        public int Priority { get; set; }
        public string SupersedesCommandId { get; set; }
    }

    [Serializable]
    public sealed class TerminationRuleRow
    {
        public string RuleId { get; set; }
        public string ConditionType { get; set; }
        public string ThresholdExpr { get; set; }
        public string ResultCode { get; set; }
        public bool Enabled { get; set; }
        public int Priority { get; set; }
    }

    public sealed class IngameCsvConfigSet
    {
        public SessionConfig SessionConfig { get; set; }
        public IReadOnlyList<StateTransitionRuleRow> StateTransitionRules { get; set; }
        public IReadOnlyList<CharacterProfileRow> CharacterProfiles { get; set; }
        public IReadOnlyList<TerminationRuleRow> TerminationRules { get; set; }
        public IReadOnlyList<InterventionCommandRow> InterventionCommands { get; set; }
    }
}
