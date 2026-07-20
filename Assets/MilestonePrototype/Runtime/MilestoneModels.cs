using System;
using System.Collections.Generic;

namespace ProjectW.MilestonePrototype
{
    public enum WorkRole { Tech, Analysis, Management, Adaptation }
    public enum TaskKind { Milestone, SideMission, Recovery, Regeneration }
    public enum TaskState { Locked, Available, Active, Complete, Failed }

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

        public bool Available => InjuryDays <= 0 && Fatigue < 100 && !RestScheduled;
        public string Condition => InjuryDays > 0 ? $"부상 {InjuryDays}일" : Fatigue >= 80 ? "소진" : Fatigue >= 55 ? "과로" : Fatigue >= 30 ? "피로" : "정상";
    }

    public sealed class DayReport
    {
        public readonly List<string> Lines = new List<string>();
    }
}
