using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.MilestonePrototype
{
    public sealed class MilestoneSimulation
    {
        private readonly Random random;
        private int nextSideMissionId;

        public int Day { get; private set; } = 1;
        public int CampaignEndDay { get; } = 30;
        public int Resources { get; private set; } = 12;
        public bool IsWon => Tasks.Where(t => t.Kind == TaskKind.Milestone && t.Required).All(t => t.State == TaskState.Complete);
        public bool IsLost => Day > CampaignEndDay || (!Crew.Any(c => c.Available) && Crew.All(c => c.InjuryDays > 0 || c.Fatigue >= 100));
        public List<WorkTask> Tasks { get; } = new List<WorkTask>();
        public List<CrewMember> Crew { get; } = new List<CrewMember>();
        public DayReport LastReport { get; private set; } = new DayReport();

        public MilestoneSimulation(int seed = 731)
        {
            random = new Random(seed);
            CreateCrew();
            CreateMilestone();
            RefreshLocks();
            LastReport.Lines.Add("첫 번째 개척 기지 가동 계획이 공개되었습니다.");
        }

        public bool Assign(string taskId, int crewIndex)
        {
            WorkTask task = Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task == null || task.State != TaskState.Available && task.State != TaskState.Active) return false;
            if (crewIndex < -1 || crewIndex >= Crew.Count) return false;
            if (crewIndex >= 0 && !Crew[crewIndex].Available) return false;
            if (crewIndex >= 0) Crew[crewIndex].RestScheduled = false;
            foreach (WorkTask other in Tasks.Where(t => t.AssignedCharacter == crewIndex)) other.AssignedCharacter = -1;
            task.AssignedCharacter = crewIndex;
            task.State = crewIndex >= 0 ? TaskState.Active : TaskState.Available;
            return true;
        }

        public bool Rest(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count || Crew[crewIndex].InjuryDays > 0 || Crew[crewIndex].RestScheduled) return false;
            foreach (WorkTask task in Tasks.Where(t => t.AssignedCharacter == crewIndex))
            {
                task.AssignedCharacter = -1;
                task.State = TaskState.Available;
            }
            Crew[crewIndex].RestScheduled = true;
            return true;
        }

        public bool Regenerate(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count || Resources < 3) return false;
            CrewMember member = Crew[crewIndex];
            Resources -= 3;
            member.Fatigue = 0;
            member.InjuryDays = 0;
            member.RestScheduled = false;
            member.Experience = Math.Max(0, member.Experience - 2);
            member.RegenerationCount++;
            foreach (WorkTask task in Tasks.Where(t => t.AssignedCharacter == crewIndex)) task.AssignedCharacter = -1;
            return true;
        }

        public DayReport AdvanceDay()
        {
            DayReport report = new DayReport();
            if (IsWon || IsLost) return report;

            foreach (CrewMember member in Crew)
            {
                if (member.InjuryDays > 0) member.InjuryDays--;
                if (!member.RestScheduled) continue;
                member.Fatigue = Math.Max(0, member.Fatigue - 18);
                member.RestScheduled = false;
                report.Lines.Add($"{member.Name}: 휴식으로 피로 회복");
            }
            foreach (WorkTask task in Tasks.Where(t => t.State == TaskState.Active).ToList()) ProcessTask(task, report);
            foreach (WorkTask task in Tasks.Where(t => t.State != TaskState.Complete && t.State != TaskState.Locked && Day > t.Deadline)) task.DelayDays++;
            TriggerSideMission(report);
            Day++;
            RefreshLocks();
            LastReport = report;
            if (report.Lines.Count == 0) report.Lines.Add("특이사항 없이 하루가 지났습니다.");
            return report;
        }

        private void ProcessTask(WorkTask task, DayReport report)
        {
            if (task.AssignedCharacter < 0) return;
            CrewMember member = Crew[task.AssignedCharacter];
            if (!member.Available)
            {
                task.AssignedCharacter = -1;
                task.State = TaskState.Available;
                return;
            }

            bool matched = member.Specialty == task.RequiredRole;
            int work = member.Skill + (matched ? 2 : 0) + member.Experience / 3;
            int fatigue = matched ? 9 : 15;
            if (Day > task.Deadline) fatigue += 4;
            task.Progress = Math.Min(task.RequiredWork, task.Progress + work);
            member.Fatigue = Math.Min(100, member.Fatigue + fatigue);
            member.Experience++;
            report.Lines.Add($"{member.Name}: {task.Name} +{work} (피로 +{fatigue})");

            int accidentChance = member.Fatigue >= 80 ? 28 : member.Fatigue >= 55 ? 10 : 0;
            if (!matched) accidentChance += 6;
            if (random.Next(100) < accidentChance)
            {
                member.InjuryDays = random.Next(2, 5);
                task.Progress = Math.Max(0, task.Progress - 2);
                task.AssignedCharacter = -1;
                task.State = TaskState.Available;
                report.Lines.Add($"사고: {member.Name}이(가) {member.InjuryDays}일 부상, 작업 일부 손실.");
            }
            else if (task.Progress >= task.RequiredWork)
            {
                task.State = TaskState.Complete;
                task.AssignedCharacter = -1;
                Resources += task.Kind == TaskKind.SideMission ? 2 : 1;
                report.Lines.Add($"완료: {task.Name}");
            }
        }

        private void TriggerSideMission(DayReport report)
        {
            int overdue = Tasks.Count(t => t.Kind == TaskKind.Milestone && t.DelayDays > 0 && t.State != TaskState.Complete);
            int exhausted = Crew.Count(c => c.Fatigue >= 55);
            int chance = 8 + overdue * 16 + exhausted * 8;
            if (Tasks.Count(t => t.Kind == TaskKind.SideMission && t.State != TaskState.Complete) >= 3 || random.Next(100) >= chance) return;

            WorkTask mission;
            if (exhausted > 0)
                mission = NewSide("과로 인력 건강 점검", WorkRole.Management, 8, 3);
            else if (overdue > 0)
                mission = NewSide("지연 일정 해명 보고", WorkRole.Management, 10, 2);
            else
                mission = NewSide("예고 없는 장비 점검", WorkRole.Tech, 9, 3);
            Tasks.Add(mission);
            report.Lines.Add($"사이드미션 발생: {mission.Name} (D-{mission.Deadline - Day})");
        }

        private WorkTask NewSide(string name, WorkRole role, int work, int days) => new WorkTask
        {
            Id = $"side-{++nextSideMissionId}", Name = name, Kind = TaskKind.SideMission, RequiredRole = role,
            RequiredWork = work, Deadline = Day + days, State = TaskState.Available
        };

        private void RefreshLocks()
        {
            foreach (WorkTask task in Tasks.Where(t => t.State == TaskState.Locked))
            {
                if (string.IsNullOrEmpty(task.PrerequisiteId) || Tasks.Any(t => t.Id == task.PrerequisiteId && t.State == TaskState.Complete))
                    task.State = TaskState.Available;
            }
        }

        private void CreateCrew()
        {
            Crew.Add(new CrewMember { Name = "한 기술자", Specialty = WorkRole.Tech, Skill = 4 });
            Crew.Add(new CrewMember { Name = "윤 분석관", Specialty = WorkRole.Analysis, Skill = 4 });
            Crew.Add(new CrewMember { Name = "서 관리자", Specialty = WorkRole.Management, Skill = 4 });
            Crew.Add(new CrewMember { Name = "강 대응관", Specialty = WorkRole.Adaptation, Skill = 4 });
            Crew.Add(new CrewMember { Name = "임 정비사", Specialty = WorkRole.Tech, Skill = 3 });
            Crew.Add(new CrewMember { Name = "문 조정관", Specialty = WorkRole.Management, Skill = 3 });
        }

        private void CreateMilestone()
        {
            Tasks.Add(new WorkTask { Id = "survey", Name = "착륙 지점 조사", Kind = TaskKind.Milestone, RequiredRole = WorkRole.Analysis, RequiredWork = 18, Deadline = 7, Required = true, State = TaskState.Available });
            Tasks.Add(new WorkTask { Id = "power", Name = "발전 설비 설치", Kind = TaskKind.Milestone, RequiredRole = WorkRole.Tech, RequiredWork = 24, Deadline = 15, Required = true, PrerequisiteId = "survey", State = TaskState.Locked });
            Tasks.Add(new WorkTask { Id = "habitat", Name = "거주 구역 건설", Kind = TaskKind.Milestone, RequiredRole = WorkRole.Tech, RequiredWork = 22, Deadline = 18, Required = true, PrerequisiteId = "survey", State = TaskState.Locked });
            Tasks.Add(new WorkTask { Id = "safety", Name = "안전 검증", Kind = TaskKind.Milestone, RequiredRole = WorkRole.Analysis, RequiredWork = 12, Deadline = 20, Required = false, PrerequisiteId = "power", State = TaskState.Locked });
            Tasks.Add(new WorkTask { Id = "launch", Name = "최종 가동 시험", Kind = TaskKind.Milestone, RequiredRole = WorkRole.Adaptation, RequiredWork = 20, Deadline = 27, Required = true, PrerequisiteId = "habitat", State = TaskState.Locked });
        }
    }
}
