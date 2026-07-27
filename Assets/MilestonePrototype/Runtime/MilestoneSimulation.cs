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
        public List<WorkGroup> Groups { get; } = new List<WorkGroup>();
        public List<MailEvent> Mail { get; } = new List<MailEvent>();
        public List<CodexEntry> Codex { get; } = new List<CodexEntry>();
        public List<string> SystemLog { get; } = new List<string>();
        public DayReport LastReport { get; private set; } = new DayReport();

        public MilestoneSimulation(int seed = 731)
        {
            random = new Random(seed);
            CreateCrew();
            CreateMilestone();
            CreateMail();
            CreateCodex();
            RefreshLocks();
            LastReport.Lines.Add("첫 번째 개척 기지가 가동되었습니다.");
            Log("캠페인을 시작했습니다.");
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
            if (crewIndex >= 0)
            {
                AddRecord(task, Crew[crewIndex].Name, RecordKind.Note, "작업에 배정됨");
                Crew[crewIndex].History.Add($"DAY {Day}: {task.Name} 배정");
                Log($"{Crew[crewIndex].Name} → {task.Name}");
            }
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
            Crew[crewIndex].History.Add($"DAY {Day}: 휴식 예약");
            Log($"{Crew[crewIndex].Name} 휴식 예약");
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
            member.History.Add($"DAY {Day}: 재생 시술");
            Log($"{member.Name} 재생 시술, 자원 -3");
            return true;
        }

        public bool ResolveMail(string mailId)
        {
            MailEvent mail = Mail.FirstOrDefault(item => item.Id == mailId && item.ArrivalDay <= Day);
            if (mail == null || mail.Resolved) return false;
            mail.Read = true;
            WorkTask target = Tasks.FirstOrDefault(task => task.Id == mail.TargetTaskId);
            if (target != null && mail.DeadlineDelta != 0)
            {
                target.Deadline = Math.Max(Day, target.Deadline + mail.DeadlineDelta);
                target.Importance = ImportanceLevel.High;
                target.Risk = RiskLevel.High;
                AddRecord(target, mail.From, RecordKind.Issue, mail.Instruction);
            }
            Resources = Math.Max(0, Resources + mail.ResourceDelta);
            mail.Resolved = true;
            Log($"메일 처리: {mail.Subject}");
            return true;
        }

        public void MarkMailRead(string mailId)
        {
            MailEvent mail = Mail.FirstOrDefault(item => item.Id == mailId && item.ArrivalDay <= Day);
            if (mail != null) mail.Read = true;
        }

        public DayReport AdvanceDay()
        {
            var report = new DayReport();
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
            foreach (string line in report.Lines) Log(line);
            return report;
        }

        public OperationsReport BuildReport() => new OperationsReport
        {
            Complete = Tasks.Count(t => t.State == TaskState.Complete),
            Active = Tasks.Count(t => t.State == TaskState.Active),
            Available = Tasks.Count(t => t.State == TaskState.Available),
            Locked = Tasks.Count(t => t.State == TaskState.Locked),
            Delayed = Tasks.Count(t => t.DelayDays > 0 && t.State != TaskState.Complete),
            HighRisk = Tasks.Count(t => EffectiveRisk(t) == RiskLevel.High && t.State != TaskState.Complete),
            OverloadedCrew = Crew.Count(c => c.Fatigue >= 55 || c.InjuryDays > 0)
        };

        public RiskLevel EffectiveRisk(WorkTask task)
        {
            if (task.DelayDays > 0 || task.Deadline - Day <= 2 && task.State != TaskState.Complete) return RiskLevel.High;
            if (task.AssignedCharacter >= 0 && Crew[task.AssignedCharacter].Fatigue >= 55) return RiskLevel.High;
            return task.Risk;
        }

        public CampaignSnapshot CreateSnapshot() => new CampaignSnapshot
        {
            SchemaVersion = ProjectWSaveStore.CampaignSchema, Day = Day, Resources = Resources,
            Tasks = Tasks.ToArray(), Crew = Crew.ToArray(), Mail = Mail.ToArray(), Log = SystemLog.ToArray()
        };

        public bool Restore(CampaignSnapshot snapshot)
        {
            if (snapshot == null || snapshot.SchemaVersion != ProjectWSaveStore.CampaignSchema ||
                snapshot.Tasks == null || snapshot.Crew == null || snapshot.Mail == null) return false;
            Day = Math.Max(1, snapshot.Day);
            Resources = Math.Max(0, snapshot.Resources);
            Tasks.Clear(); Tasks.AddRange(snapshot.Tasks);
            Crew.Clear(); Crew.AddRange(snapshot.Crew);
            Mail.Clear(); Mail.AddRange(snapshot.Mail);
            SystemLog.Clear(); if (snapshot.Log != null) SystemLog.AddRange(snapshot.Log);
            return true;
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
            AddRecord(task, member.Name, RecordKind.Output, $"+{work} 진행");

            int accidentChance = member.Fatigue >= 80 ? 28 : member.Fatigue >= 55 ? 10 : 0;
            if (!matched) accidentChance += 6;
            if (random.Next(100) < accidentChance)
            {
                member.InjuryDays = random.Next(2, 5);
                task.Progress = Math.Max(0, task.Progress - 2);
                task.AssignedCharacter = -1;
                task.State = TaskState.Available;
                report.Lines.Add($"사고: {member.Name}은(는) {member.InjuryDays}일 부상. 작업 일부 손실.");
                AddRecord(task, member.Name, RecordKind.Issue, $"{member.InjuryDays}일 부상");
            }
            else if (task.Progress >= task.RequiredWork)
            {
                task.State = TaskState.Complete;
                task.AssignedCharacter = -1;
                Resources += task.Kind == TaskKind.SideMission ? 2 : 1;
                report.Lines.Add($"완료: {task.Name}");
                AddRecord(task, member.Name, RecordKind.Output, "작업 완료");
            }
        }

        private void TriggerSideMission(DayReport report)
        {
            int overdue = Tasks.Count(t => t.Kind == TaskKind.Milestone && t.DelayDays > 0 && t.State != TaskState.Complete);
            int exhausted = Crew.Count(c => c.Fatigue >= 55);
            int chance = 8 + overdue * 16 + exhausted * 8;
            if (Tasks.Count(t => t.Kind == TaskKind.SideMission && t.State != TaskState.Complete) >= 3 || random.Next(100) >= chance) return;
            WorkTask mission = exhausted > 0
                ? NewSide("과로 인력 건강 점검", WorkRole.Management, 8, 3)
                : overdue > 0 ? NewSide("지연 일정 해명 보고", WorkRole.Management, 10, 2)
                : NewSide("예고 없는 장비 점검", WorkRole.Tech, 9, 3);
            Tasks.Add(mission);
            report.Lines.Add($"사이드미션 발생: {mission.Name} (D-{mission.Deadline - Day})");
        }

        private WorkTask NewSide(string name, WorkRole role, int work, int days) => new WorkTask
        {
            Id = $"side-{++nextSideMissionId}", Name = name, Kind = TaskKind.SideMission, RequiredRole = role,
            RequiredWork = work, Deadline = Day + days, State = TaskState.Available, GroupId = "incident",
            Risk = RiskLevel.High, Importance = ImportanceLevel.Medium
        };

        private void RefreshLocks()
        {
            foreach (WorkTask task in Tasks.Where(t => t.State == TaskState.Locked))
                if (string.IsNullOrEmpty(task.PrerequisiteId) || Tasks.Any(t => t.Id == task.PrerequisiteId && t.State == TaskState.Complete))
                    task.State = TaskState.Available;
        }

        private void CreateCrew()
        {
            Crew.Add(new CrewMember { Name = "윤 기술관", Specialty = WorkRole.Tech, Skill = 4 });
            Crew.Add(new CrewMember { Name = "서 분석관", Specialty = WorkRole.Analysis, Skill = 4 });
            Crew.Add(new CrewMember { Name = "민 관리자", Specialty = WorkRole.Management, Skill = 4 });
            Crew.Add(new CrewMember { Name = "강 적응관", Specialty = WorkRole.Adaptation, Skill = 4 });
            Crew.Add(new CrewMember { Name = "한 정비관", Specialty = WorkRole.Tech, Skill = 3 });
            Crew.Add(new CrewMember { Name = "문 조정관", Specialty = WorkRole.Management, Skill = 3 });
        }

        private void CreateMilestone()
        {
            Groups.Add(new WorkGroup { Id = "foundation", Name = "정착 기반", SoftDeadline = 15, HardDeadline = 20 });
            Groups.Add(new WorkGroup { Id = "launch", Name = "최종 가동", SoftDeadline = 27, HardDeadline = 30 });
            Groups.Add(new WorkGroup { Id = "incident", Name = "돌발 대응", SoftDeadline = 30, HardDeadline = 30 });
            Tasks.Add(Task("survey", "착륙 지점 조사", WorkRole.Analysis, 18, 7, true, "foundation", null, RiskLevel.Medium));
            Tasks.Add(Task("power", "발전 설비 설치", WorkRole.Tech, 24, 15, true, "foundation", "survey", RiskLevel.High));
            Tasks.Add(Task("habitat", "거주 구역 건설", WorkRole.Tech, 22, 18, true, "foundation", "survey", RiskLevel.Medium));
            Tasks.Add(Task("safety", "안전 검증", WorkRole.Analysis, 12, 20, false, "foundation", "power", RiskLevel.Low));
            Tasks.Add(Task("launch", "최종 가동 시험", WorkRole.Adaptation, 20, 27, true, "launch", "habitat", RiskLevel.High));
        }

        private static WorkTask Task(string id, string name, WorkRole role, int work, int deadline, bool required,
            string group, string prerequisite, RiskLevel risk) => new WorkTask
        {
            Id = id, Name = name, Kind = TaskKind.Milestone, RequiredRole = role, RequiredWork = work,
            Deadline = deadline, Required = required, GroupId = group, PrerequisiteId = prerequisite,
            State = string.IsNullOrEmpty(prerequisite) ? TaskState.Available : TaskState.Locked,
            Risk = risk, Importance = required ? ImportanceLevel.High : ImportanceLevel.Medium
        };

        private void CreateMail()
        {
            Mail.Add(new MailEvent { Id = "mail-1", ArrivalDay = 1, From = "개척 본부", Subject = "착륙 지점 조사 우선 요청", Body = "후속 설비 작업을 위해 조사 일정을 앞당겨 주십시오.", Instruction = "조사 마감이 하루 앞당겨지고 중요도가 상승합니다.", TargetTaskId = "survey", DeadlineDelta = -1, Risk = RiskLevel.Medium });
            Mail.Add(new MailEvent { Id = "mail-2", ArrivalDay = 4, From = "보급 통제실", Subject = "추가 보급 승인", Body = "초기 운영 보고가 승인되었습니다.", Instruction = "자원 2를 수령합니다.", ResourceDelta = 2, Risk = RiskLevel.Low });
            Mail.Add(new MailEvent { Id = "mail-3", ArrivalDay = 10, From = "안전 위원회", Subject = "발전 설비 안전 검토", Body = "일정 압박으로 사고 위험이 증가했습니다.", Instruction = "발전 설비 마감이 하루 앞당겨집니다.", TargetTaskId = "power", DeadlineDelta = -1, Risk = RiskLevel.High });
        }

        private void CreateCodex()
        {
            Codex.Add(new CodexEntry { Id = "role", Category = "운영", Name = "역할 적합도", Description = "대원의 전문 역할과 작업 요구 역할이 같으면 작업량 보너스를 얻고 피로가 덜 쌓입니다." });
            Codex.Add(new CodexEntry { Id = "fatigue", Category = "대원", Name = "피로와 부상", Description = "피로가 높을수록 사고 가능성이 증가합니다. 휴식은 다음 날 피로를 회복합니다." });
            Codex.Add(new CodexEntry { Id = "regen", Category = "대원", Name = "재생 시술", Description = "자원 3을 사용해 피로와 부상을 즉시 제거하지만 경험 일부를 잃습니다." });
            Codex.Add(new CodexEntry { Id = "deadline", Category = "작업", Name = "마감과 위험", Description = "마감이 임박하거나 지연된 작업, 과로한 대원이 맡은 작업은 고위험으로 표시됩니다." });
            Codex.Add(new CodexEntry { Id = "victory", Category = "캠페인", Name = "승리 조건", Description = "DAY 30 안에 모든 필수 마일스톤을 완료하면 승리합니다." });
        }

        private void AddRecord(WorkTask task, string actor, RecordKind kind, string text)
        {
            if (task.Records == null) task.Records = new List<TaskRecord>();
            task.Records.Add(new TaskRecord { Day = Day, Actor = actor, Kind = kind, Text = text });
        }

        private void Log(string text)
        {
            SystemLog.Add($"DAY {Day:00}  {text}");
            if (SystemLog.Count > 100) SystemLog.RemoveAt(0);
        }
    }
}
