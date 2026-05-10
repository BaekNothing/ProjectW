using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ProjectW.IngameCore.CaseReview
{
[Serializable]
public sealed class GameConfig
{
    public int MorningSeconds { get; set; } = 90;
    public int NoonSeconds { get; set; } = 210;
    public int EveningSeconds { get; set; } = 120;
    public bool UseTimePressure { get; set; }
    public int QueueSoftCap { get; set; } = 6;
    public int QueueHardCap { get; set; } = 10;
    public int RedirectBudgetPerDay { get; set; } = 2;
    public int AuditBudgetPerDay { get; set; } = 1;
    public int InterviewBudgetPerDay { get; set; } = 1;
    [IgnoreDataMember] public CaseReviewSeedData InitialData { get; set; }
}

[Serializable]
public sealed class CaseReviewSeedData
{
    public List<Personnel> Staff { get; set; } = new();
    public List<EventCase> Queue { get; set; } = new();
    public List<TruthFrame> TruthFrames { get; set; } = new();
    public List<VisibleLog> Logs { get; set; } = new();
}

[Serializable]
public sealed class GameState
{
    public GameConfig Config { get; set; } = new();
    public int Seed { get; set; }
    public int RngState { get; set; }
    public int Day { get; set; } = 1;
    public Slot Slot { get; set; } = Slot.Morning;
    public int TimeRemainingSec { get; set; }
    public int TotalElapsedSec { get; set; }
    public int Overload { get; set; }
    public int GlobalLatentRisk { get; set; } = 15;
    public int TalentShortage { get; set; }
    public int RedirectBudget { get; set; }
    public int AuditBudget { get; set; }
    public int InterviewBudget { get; set; }
    public string KpiMode { get; set; } = "BALANCED";
    public string OpenEventId { get; set; } = "";
    public WorkPlan MorningPlan { get; set; } = new();
    public List<EventCase> Queue { get; set; } = new();
    public List<Personnel> Staff { get; set; } = new();
    public List<TruthFrame> TruthFrames { get; set; } = new();
    public List<VisibleLog> Logs { get; set; } = new();
    public List<DailyReportDocument> Reports { get; set; } = new();
    public List<string> CommandTape { get; set; } = new();
}

[Serializable]
public sealed class WorkPlan
{
    public int Day { get; set; } = 1;
    public bool Confirmed { get; set; }
    public List<WorkPlanEntry> Entries { get; set; } = new();
}

[Serializable]
public sealed class WorkPlanEntry
{
    public string EventId { get; set; } = "";
    public string Reason { get; set; } = "";
    public List<string> PlannedPersonnel { get; set; } = new();
    public bool Adjusted { get; set; }
}

[Serializable]
public enum Slot
{
    Morning,
    Noon,
    Evening
}

[Serializable]
public sealed class EventCase
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subsystem { get; set; } = "";
    public int Urgency { get; set; }
    public int Severity { get; set; }
    public int TtlSec { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Open;
    public int LatentRisk { get; set; }
    public int MismatchScore { get; set; }
    public bool SummaryRead { get; set; }
    public bool ApprovedFromSummaryOnly { get; set; }
    public List<string> AssignedPersonnel { get; set; } = new();
    public int HoldCount { get; set; }
    public bool Redirected { get; set; }
    public int OutcomeScore { get; set; }
    public string ResultSummary { get; set; } = "";
    public bool AutoResolved { get; set; }
    public bool ReportReviewed { get; set; }
    public int PhysicalCost { get; set; }
    public int MentalCost { get; set; }
    public int BaseSuccessChance { get; set; } = 50;
    public Dictionary<string, int> RequiredAptitudes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> PerkTags { get; set; } = new();
    public string PerkInteractionInfo { get; set; } = "";
}

[Serializable]
public enum CaseStatus
{
    Open,
    Held,
    Closed,
    Escalated
}

[Serializable]
public sealed class Personnel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Background { get; set; } = "";
    public List<string> Interests { get; set; } = new();
    public string Personality { get; set; } = "";
    public string WorkStyle { get; set; } = "";
    public int PhysicalEnergy { get; set; } = 100;
    public int MentalStress { get; set; }
    public int LoadAssigned { get; set; }
    public int Fatigue { get; set; }
    public int Stagnation { get; set; }
    public int TrustToManager { get; set; }
    public int RetentionRisk { get; set; }
    public bool HasLeft { get; set; }
    public int DaysSinceJoined { get; set; }
    public int OptLow { get; set; }
    public int OptHigh { get; set; }
    public int MaxLoad { get; set; }
    public int ConnectionLimit { get; set; } = 3;
    public Dictionary<string, int> Aptitudes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PersonnelPerk> Perks { get; set; } = new();
    public List<PersonnelRelationship> Relationships { get; set; } = new();
}

[Serializable]
public sealed class PersonnelPerk
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> TriggerTags { get; set; } = new();
    public Dictionary<string, int> AptitudeModifiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int OutcomeModifier { get; set; }
    public int PhysicalCostModifier { get; set; }
    public int MentalCostModifier { get; set; }
    public string Note { get; set; } = "";
}

[Serializable]
public sealed class PersonnelRelationship
{
    public string TargetId { get; set; } = "";
    public int Trust { get; set; }
    public int Affinity { get; set; }
    public string Note { get; set; } = "";
}

[Serializable]
public sealed class TruthFrame
{
    public string Id { get; set; } = "";
    public string EventId { get; set; } = "";
    public int Tick { get; set; }
    public string ActorId { get; set; } = "";
    public string ActionCode { get; set; } = "";
    public string FactBlob { get; set; } = "";
}

[Serializable]
public sealed class VisibleLog
{
    public string Id { get; set; } = "";
    public string EventId { get; set; } = "";
    public string SourceType { get; set; } = "";
    public int VisibleAtSec { get; set; }
    public string Text { get; set; } = "";
    public bool Omitted { get; set; }
    public bool Distorted { get; set; }
    public bool Delayed { get; set; }
    public bool Announced { get; set; }
    public bool Read { get; set; }
}

[Serializable]
public sealed class DailyReportDocument
{
    public int Day { get; set; }
    public string Title { get; set; } = "";
    public string Generator { get; set; } = "template";
    public string Body { get; set; } = "";
}

[Serializable]
public sealed class DispatchResult
{
    public bool Success { get; set; }
    public string Code { get; set; } = "";
    public int TimeCostSec { get; set; }
    public List<string> Lines { get; set; } = new();
    public Dictionary<string, string> StateDiff { get; set; } = new();
}

[Serializable]
public sealed class TickResult
{
    public List<string> Lines { get; set; } = new();
    public bool SlotChanged { get; set; }
}

[Serializable]
public sealed class ReplayReport
{
    public int Seed { get; set; }
    public int CommandCount { get; set; }
    public string Snapshot { get; set; } = "";
    public string SnapshotHash { get; set; } = "";
    public List<string> Transcript { get; set; } = new();
}

}
