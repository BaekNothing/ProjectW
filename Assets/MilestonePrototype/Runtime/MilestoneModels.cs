using System;
using System.Collections.Generic;

namespace ProjectW.MilestonePrototype
{
    public enum WorkRole { Tech, Analysis, Management, Adaptation }
    public enum TaskKind { Milestone, SideMission, Recovery, Regeneration }
    public enum TaskState { Locked, Available, Active, Complete, Failed }
    public enum WorkState { Locked, Available, InProgress, Complete, Failed }
    public enum RiskLevel { Low, Medium, High }
    public enum ImportanceLevel { Low, Medium, High }
    public enum RecordKind { Output, Note, Issue }

    [Serializable]
    public sealed class WorkTask
    {
        public string Id;
        public string Name;
        public TaskKind Kind;
        public WorkRole RequiredRole;
        public float RequiredWork;
        public float Progress;
        public int Deadline;
        public bool Required;
        public string PrerequisiteId;
        public int AssignedCharacter = -1;
        public bool IsParallelAssignment;
        public TaskState State;
        public int DelayDays;
        public string GroupId;
        public RiskLevel Risk;
        public ImportanceLevel Importance;
        public float ContextCostDays;
        public int SplitCount;
        public int LastWorker = -1;
        public int Difficulty;
        public List<TaskRecord> Records = new List<TaskRecord>();

        public float EffectiveRequiredWork => RequiredWork + ContextCostDays;
        public float RemainingWork => Math.Max(0f, EffectiveRequiredWork - Progress);
        public float Completion => EffectiveRequiredWork <= 0 ? 1f : Math.Min(1f, Progress / EffectiveRequiredWork);
    }

    [Serializable]
    public sealed class CrewMember
    {
        public string Name;
        public WorkRole Specialty;
        public int Skill;
        public int Fatigue;
        public int Experience;
        public int InjuryDays;
        public int RegenerationCount;
        public int Trust;
        public int Pride;
        public int Authority;
        public bool RestScheduled;
        public List<string> History = new List<string>();

        public bool Available => InjuryDays <= 0 && Fatigue < 100 && !RestScheduled;
        public string Condition => InjuryDays > 0 ? $"부상 {InjuryDays}일" : Fatigue >= 80 ? "소진" : Fatigue >= 55 ? "과로" : Fatigue >= 30 ? "피로" : "정상";
    }

    public sealed class DayReport
    {
        public readonly List<string> Lines = new List<string>();
    }

    [Serializable]
    public sealed class TaskRecord
    {
        public int Day;
        public string Actor;
        public RecordKind Kind;
        public string Text;
    }

    [Serializable]
    public sealed class WorkGroup
    {
        public string Id;
        public string Name;
        public int SoftDeadline;
        public int HardDeadline;
        public bool Required = true;
        public string[] PredecessorIds;
        public WorkState State;
        public bool SoftDeadlineMissed;
    }

    [Serializable]
    public sealed class TaskSystemBalance
    {
        public float PrimaryProgressDays = 1f;
        public float ParallelProgressDays = 1f;
        public float ParallelMaximumRemainingDays = 1f;
        public float InterruptionCostDays = .5f;
        public float ResumptionCostDays = .5f;
        public int MatchingFatigue = 9;
        public int MismatchedFatigue = 15;
        public int ParallelFatigue = 12;
        public int SoftDeadlineFatigue = 4;
        public int RestRecovery = 18;
        public int RegenerationResourceCost = 3;
        public int HighFatigueAccidentChance = 28;
        public int MediumFatigueAccidentChance = 10;
        public int MismatchAccidentChance = 6;
        public int SideMissionLimit = 3;
        public int BaseSideMissionChance = 8;
    }

    public sealed class TaskCostPreview
    {
        public float RemainingDays;
        public float AdditionalContextDays;
        public int PrimaryFatigue;
        public int ParallelFatigue;
        public bool CanRunInParallel;
    }

    [Serializable]
    public sealed class TaskSystemData
    {
        public int SchemaVersion;
        public int CampaignEndDay;
        public int StartingResources;
        public TaskSystemBalance Balance;
        public WorkGroup[] Works;
        public WorkTask[] Tasks;
        public CrewMember[] Crew;
        public MailEvent[] Mail;
        public CodexEntry[] Codex;
    }

    [Serializable]
    public sealed class MailEvent
    {
        public string Id;
        public int ArrivalDay;
        public string From;
        public string Subject;
        public string Body;
        public string Instruction;
        public string TargetTaskId;
        public string TargetWorkId;
        public int DeadlineDelta;
        public int ResourceDelta;
        public RiskLevel Risk;
        public bool Read;
        public bool Resolved;
    }

    [Serializable]
    public sealed class CodexEntry
    {
        public string Id;
        public string Category;
        public string Name;
        public string Description;
    }

    public sealed class OperationsReport
    {
        public int Complete;
        public int Active;
        public int Available;
        public int Locked;
        public int Delayed;
        public int HighRisk;
        public int OverloadedCrew;
    }

    [Serializable]
    public sealed class CampaignSnapshot
    {
        public int SchemaVersion = 1;
        public int Day;
        public int Resources;
        public WorkTask[] Tasks;
        public WorkGroup[] Groups;
        public CrewMember[] Crew;
        public MailEvent[] Mail;
        public string[] Log;
    }

    [Serializable]
    public sealed class DesktopSnapshot
    {
        public int SchemaVersion = 1;
        public WindowSnapshot[] Windows;
    }

    [Serializable]
    public sealed class WindowSnapshot
    {
        public string Id;
        public float X;
        public float Y;
        public bool Open;
        public bool Minimized;
        public int Order;
    }
}
