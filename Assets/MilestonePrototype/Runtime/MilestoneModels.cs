using System;
using System.Collections.Generic;

namespace ProjectW.MilestonePrototype
{
    public enum WorkRole { Tech, Analysis, Management, Adaptation }
    public enum TaskKind { Milestone, SideMission, Recovery, Regeneration }
    public enum TaskState { Locked, Available, Active, Complete, Failed }
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
        public int RequiredWork;
        public int Progress;
        public int Deadline;
        public bool Required;
        public string PrerequisiteId;
        public int AssignedCharacter = -1;
        public TaskState State;
        public int DelayDays;
        public string GroupId;
        public RiskLevel Risk;
        public ImportanceLevel Importance;
        public List<TaskRecord> Records = new List<TaskRecord>();

        public float Completion => RequiredWork <= 0 ? 1f : Math.Min(1f, (float)Progress / RequiredWork);
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
