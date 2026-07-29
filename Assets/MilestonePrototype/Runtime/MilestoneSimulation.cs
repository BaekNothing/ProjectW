using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.MilestonePrototype
{
    public sealed class MilestoneSimulation
    {
        private readonly Random random;
        private readonly TaskSystemBalance balance;
        private readonly string[] crewPortraits;
        private readonly string[] crewMemos;
        private readonly string[][] crewPerks;
        private int nextRandomWorkId;

        public int Day { get; private set; } = 1;
        public int CampaignEndDay { get; }
        public int Resources { get; private set; }
        public float ParallelMaximumRemainingDays => balance.ParallelMaximumRemainingDays;
        public int RegenerationResourceCost => balance.RegenerationResourceCost;
        public float InterruptionAndResumptionCostDays =>
            balance.InterruptionCostDays + balance.ResumptionCostDays;
        public bool IsWon => Groups.Where(group => group.Required).All(group => group.State == WorkState.Complete);
        public bool IsLost => Groups.Any(group => group.Required && group.State == WorkState.Failed) ||
                              Day > CampaignEndDay ||
                              (!Crew.Any(member => member.Available) &&
                               Crew.All(member => member.InjuryDays > 0 || member.Fatigue >= 100));
        public List<WorkTask> Tasks { get; } = new List<WorkTask>();
        public List<CrewMember> Crew { get; } = new List<CrewMember>();
        public List<WorkGroup> Groups { get; } = new List<WorkGroup>();
        public List<MailEvent> Mail { get; } = new List<MailEvent>();
        public List<CodexEntry> Codex { get; } = new List<CodexEntry>();
        public List<string> SystemLog { get; } = new List<string>();
        public DayReport LastReport { get; private set; } = new DayReport();

        public MilestoneSimulation(int seed = 731) : this(TaskSystemDataLoader.Load(), seed)
        {
        }

        public MilestoneSimulation(TaskSystemData data, int seed = 731)
        {
            TaskSystemDataLoader.Validate(data);
            random = new Random(seed);
            balance = data.Balance;
            crewPortraits = new string[data.Crew.Length];
            crewMemos = new string[data.Crew.Length];
            crewPerks = new string[data.Crew.Length][];
            for (int i = 0; i < data.Crew.Length; i++)
            {
                crewPortraits[i] = data.Crew[i].PortraitLabel;
                crewMemos[i] = data.Crew[i].Memo;
                crewPerks[i] = data.Crew[i].Perks;
            }
            CampaignEndDay = data.CampaignEndDay;
            Resources = data.StartingResources;
            Groups.AddRange(data.Works);
            Tasks.AddRange(data.Tasks);
            Crew.AddRange(data.Crew);
            if (data.Mail != null) Mail.AddRange(data.Mail);
            if (data.Codex != null) Codex.AddRange(data.Codex);
            NormalizeLoadedData();
            RefreshStates();
            LastReport.Lines.Add("첫 번째 개척 기지가 가동되었습니다.");
            Log("캠페인을 시작했습니다.");
        }

        public bool Assign(string taskId, int crewIndex)
        {
            WorkTask task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task == null || task.State != TaskState.Available && task.State != TaskState.Active) return false;
            if (crewIndex < -1 || crewIndex >= Crew.Count) return false;

            if (crewIndex < 0)
            {
                if (task.AssignedCharacter >= 0) Detach(task, true);
                RefreshStates();
                return true;
            }

            if (!Crew[crewIndex].Available) return false;
            if (task.AssignedCharacter == crewIndex && !task.IsParallelAssignment) return true;

            WorkTask current = Tasks.FirstOrDefault(candidate =>
                candidate.AssignedCharacter == crewIndex && !candidate.IsParallelAssignment);
            if (current != null && current != task) Detach(current, true);
            if (task.AssignedCharacter >= 0) Detach(task, true);

            Crew[crewIndex].RestScheduled = false;
            task.AssignedCharacter = crewIndex;
            task.IsParallelAssignment = false;
            task.State = task.StartedDay > 0 ? TaskState.Active : TaskState.Available;
            AddAssignmentRecord(task, crewIndex, "주 작업 배정");
            RefreshStates();
            return true;
        }

        public bool AssignParallel(string taskId, int crewIndex)
        {
            WorkTask task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task == null || task.State != TaskState.Available && task.State != TaskState.Active) return false;
            if (crewIndex < 0 || crewIndex >= Crew.Count || !Crew[crewIndex].Available) return false;
            if (task.RemainingWork > balance.ParallelMaximumRemainingDays + .001f) return false;
            if (!Tasks.Any(candidate => candidate.AssignedCharacter == crewIndex && !candidate.IsParallelAssignment))
                return false;
            if (Tasks.Any(candidate => candidate != task && candidate.AssignedCharacter == crewIndex &&
                                       candidate.IsParallelAssignment))
                return false;
            if (task.AssignedCharacter == crewIndex && !task.IsParallelAssignment) return false;
            if (task.AssignedCharacter >= 0) Detach(task, true);

            task.AssignedCharacter = crewIndex;
            task.IsParallelAssignment = true;
            task.State = task.StartedDay > 0 ? TaskState.Active : TaskState.Available;
            AddAssignmentRecord(task, crewIndex, "병행 작업 배정");
            RefreshStates();
            return true;
        }

        public bool Schedule(string taskId, int crewIndex, int day)
        {
            WorkTask task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task == null || task.State == TaskState.Complete || task.State == TaskState.Failed)
                return false;
            if (crewIndex < 0 || crewIndex >= Crew.Count || day < Day) return false;
            if (Tasks.Any(candidate => candidate != task &&
                                      candidate.ScheduledDay == day &&
                                      candidate.ScheduledWorker == crewIndex))
                return false;

            task.ScheduledDay = day;
            task.ScheduledWorker = crewIndex;
            AddRecord(task, Crew[crewIndex].Name, RecordKind.Note, $"D{day:00} 작업 예약");
            Log($"{task.Name} 예약: {Crew[crewIndex].Name}, DAY {day}");
            return true;
        }

        public bool CancelSchedule(string taskId)
        {
            WorkTask task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task == null || task.ScheduledDay <= 0) return false;
            task.ScheduledDay = 0;
            task.ScheduledWorker = -1;
            AddRecord(task, "SYSTEM", RecordKind.Note, "작업 예약 취소");
            return true;
        }

        public bool Rest(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count ||
                Crew[crewIndex].InjuryDays > 0 || Crew[crewIndex].RestScheduled) return false;
            foreach (WorkTask task in Tasks.Where(candidate => candidate.AssignedCharacter == crewIndex).ToList())
                Detach(task, true);
            Crew[crewIndex].RestScheduled = true;
            Crew[crewIndex].History.Add($"DAY {Day}: 휴식 예약");
            Log($"{Crew[crewIndex].Name} 휴식 예약");
            RefreshStates();
            return true;
        }

        public bool Regenerate(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count ||
                Resources < balance.RegenerationResourceCost) return false;
            CrewMember member = Crew[crewIndex];
            Resources -= balance.RegenerationResourceCost;
            member.Fatigue = 0;
            member.InjuryDays = 0;
            member.RestScheduled = false;
            member.Experience = Math.Max(0, member.Experience - 2);
            member.RegenerationCount++;
            foreach (WorkTask task in Tasks.Where(candidate => candidate.AssignedCharacter == crewIndex).ToList())
                Detach(task, true);
            member.History.Add($"DAY {Day}: 재생 시술");
            Log($"{member.Name} 재생 시술, 자원 -{balance.RegenerationResourceCost}");
            RefreshStates();
            return true;
        }

        public bool ResolveMail(string mailId)
        {
            MailEvent mail = Mail.FirstOrDefault(item => item.Id == mailId && item.ArrivalDay <= Day);
            if (mail == null || mail.Resolved) return false;
            mail.Read = true;

            WorkGroup targetWork = Groups.FirstOrDefault(group => group.Id == mail.TargetWorkId);
            if (targetWork == null && !string.IsNullOrWhiteSpace(mail.TargetTaskId))
            {
                WorkTask targetTask = Tasks.FirstOrDefault(task => task.Id == mail.TargetTaskId);
                targetWork = targetTask == null ? null : Groups.FirstOrDefault(group => group.Id == targetTask.GroupId);
            }
            if (targetWork != null && mail.DeadlineDelta != 0)
            {
                targetWork.SoftDeadline = Math.Max(Day, targetWork.SoftDeadline + mail.DeadlineDelta);
                targetWork.HardDeadline = Math.Max(targetWork.SoftDeadline, targetWork.HardDeadline + mail.DeadlineDelta);
                foreach (WorkTask task in Tasks.Where(candidate => candidate.GroupId == targetWork.Id))
                {
                    task.Deadline = targetWork.HardDeadline;
                    task.Importance = ImportanceLevel.High;
                    task.Risk = RiskLevel.High;
                    AddRecord(task, mail.From, RecordKind.Issue, mail.Instruction);
                }
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

        public bool AskWorker(int crewIndex, string question)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count || string.IsNullOrWhiteSpace(question))
                return false;

            CrewMember member = Crew[crewIndex];
            string answer;
            if (question == "status")
            {
                answer = BuildWorkerStatusReply(crewIndex);
                question = "현재 상태는 어때요?";
            }
            else if (question == "work")
            {
                answer = BuildWorkerWorkReply(crewIndex);
                question = "작업 현황을 알려주세요.";
            }
            else
            {
                return false;
            }

            member.History.Add($"DAY {Day} [나] {question}");
            member.History.Add($"DAY {Day} [{member.Name}] {answer}");
            Log($"{member.Name}에게 메신저 질문");
            return true;
        }

        public string BuildWorkerStatusReply(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count) return string.Empty;
            CrewMember member = Crew[crewIndex];
            if (member.InjuryDays > 0)
                return $"지금은 부상 회복 중입니다. 복귀까지 {member.InjuryDays}일 남았습니다.";
            if (member.RestScheduled)
                return "오늘은 휴식이 예정되어 있습니다. 회복 후 다시 보고드리겠습니다.";
            if (member.Fatigue >= 80)
                return $"피로도가 {member.Fatigue}%라 많이 지쳤습니다. 휴식이 필요합니다.";
            if (member.Fatigue >= 55)
                return $"피로도 {member.Fatigue}%입니다. 계속할 수 있지만 무리가 쌓이고 있습니다.";
            return $"괜찮습니다. 현재 피로도는 {member.Fatigue}%이고 바로 대응할 수 있습니다.";
        }

        public string BuildWorkerWorkReply(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count) return string.Empty;
            WorkTask primary = Tasks.FirstOrDefault(task =>
                task.AssignedCharacter == crewIndex && !task.IsParallelAssignment);
            WorkTask parallel = Tasks.FirstOrDefault(task =>
                task.AssignedCharacter == crewIndex && task.IsParallelAssignment);
            if (primary == null && parallel == null)
                return "현재 맡은 작업은 없습니다. 새 지시를 기다리고 있습니다.";

            string reply = primary == null
                ? "주 작업은 없습니다."
                : $"{primary.Name} 작업을 진행 중입니다. 진척도는 {primary.Completion * 100:0}%입니다.";
            if (parallel != null)
                reply += $" 병행 작업은 {parallel.Name}, 진척도 {parallel.Completion * 100:0}%입니다.";
            return reply;
        }

        public DayReport AdvanceDay()
        {
            var report = new DayReport();
            if (IsWon || IsLost) return report;

            ApplyScheduledAssignments(report);
            foreach (CrewMember member in Crew)
            {
                if (member.InjuryDays > 0) member.InjuryDays--;
                if (!member.RestScheduled) continue;
                member.Fatigue = Math.Max(0, member.Fatigue - balance.RestRecovery);
                member.RestScheduled = false;
                report.Lines.Add($"{member.Name}: 휴식으로 피로 회복");
            }

            foreach (WorkTask task in Tasks.Where(candidate =>
                         candidate.AssignedCharacter >= 0 &&
                         candidate.State != TaskState.Complete &&
                         candidate.State != TaskState.Failed)
                         .OrderBy(candidate => candidate.IsParallelAssignment).ToList())
                ProcessTask(task, report);

            TriggerRandomWork(report);
            Day++;
            RefreshStates();
            ApplyDeadlineResults(report);
            RefreshStates();
            LastReport = report;
            if (report.Lines.Count == 0) report.Lines.Add("특이사항 없이 하루가 지났습니다.");
            foreach (string line in report.Lines) Log(line);
            return report;
        }

        public OperationsReport BuildReport() => new OperationsReport
        {
            Complete = Tasks.Count(task => task.State == TaskState.Complete),
            Active = Tasks.Count(task => task.State == TaskState.Active),
            Available = Tasks.Count(task => task.State == TaskState.Available),
            Locked = Tasks.Count(task => task.State == TaskState.Locked),
            Delayed = Tasks.Count(task => ParentWork(task)?.SoftDeadlineMissed == true &&
                                          task.State != TaskState.Complete),
            HighRisk = Tasks.Count(task => EffectiveRisk(task) == RiskLevel.High &&
                                           task.State != TaskState.Complete),
            OverloadedCrew = Crew.Count(member => member.Fatigue >= 55 || member.InjuryDays > 0)
        };

        public RiskLevel EffectiveRisk(WorkTask task)
        {
            WorkGroup work = ParentWork(task);
            if (work?.SoftDeadlineMissed == true ||
                work != null && work.HardDeadline - Day <= 2 && task.State != TaskState.Complete)
                return RiskLevel.High;
            if (task.AssignedCharacter >= 0 && Crew[task.AssignedCharacter].Fatigue >= 55)
                return RiskLevel.High;
            return task.Risk;
        }

        public TaskCostPreview BuildCostPreview(WorkTask task, int crewIndex = -1)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            bool hasWorker = crewIndex >= 0 && crewIndex < Crew.Count;
            bool matched = hasWorker && Crew[crewIndex].Specialty == task.RequiredRole;
            int primaryFatigue = matched ? balance.MatchingFatigue : balance.MismatchedFatigue;
            if (ParentWork(task)?.SoftDeadlineMissed == true)
                primaryFatigue += balance.SoftDeadlineFatigue;
            bool changesActiveOwner = task.AssignedCharacter >= 0 &&
                                      task.AssignedCharacter != crewIndex &&
                                      task.Progress > 0f;
            return new TaskCostPreview
            {
                RemainingDays = task.RemainingWork,
                AdditionalContextDays = changesActiveOwner
                    ? InterruptionAndResumptionCostDays
                    : 0f,
                PrimaryFatigue = primaryFatigue,
                ParallelFatigue = primaryFatigue + balance.ParallelFatigue,
                CanRunInParallel = task.RemainingWork <= balance.ParallelMaximumRemainingDays + .001f
            };
        }

        public CampaignSnapshot CreateSnapshot() => new CampaignSnapshot
        {
            SchemaVersion = ProjectWSaveStore.CampaignSchema,
            Day = Day,
            Resources = Resources,
            Tasks = Tasks.ToArray(),
            Groups = Groups.ToArray(),
            Crew = Crew.ToArray(),
            Mail = Mail.ToArray(),
            Log = SystemLog.ToArray()
        };

        public bool Restore(CampaignSnapshot snapshot)
        {
            if (snapshot == null || snapshot.SchemaVersion != ProjectWSaveStore.CampaignSchema ||
                snapshot.Tasks == null || snapshot.Groups == null ||
                snapshot.Crew == null || snapshot.Mail == null) return false;
            Day = Math.Max(1, snapshot.Day);
            Resources = Math.Max(0, snapshot.Resources);
            Tasks.Clear(); Tasks.AddRange(snapshot.Tasks);
            Groups.Clear(); Groups.AddRange(snapshot.Groups);
            Crew.Clear(); Crew.AddRange(snapshot.Crew);
            Mail.Clear(); Mail.AddRange(snapshot.Mail);
            SystemLog.Clear();
            if (snapshot.Log != null) SystemLog.AddRange(snapshot.Log);
            NormalizeLoadedData();
            RefreshStates();
            return true;
        }

        private void ProcessTask(WorkTask task, DayReport report)
        {
            if (task.AssignedCharacter < 0) return;
            CrewMember member = Crew[task.AssignedCharacter];
            if (!member.Available)
            {
                Detach(task, true);
                return;
            }

            if (task.StartedDay <= 0) task.StartedDay = Day;
            task.State = TaskState.Active;
            bool matched = member.Specialty == task.RequiredRole;
            float baseOutput = member.DailyOutput > 0f ? member.DailyOutput : 1f;
            baseOutput *= task.IsParallelAssignment
                ? balance.ParallelProgressDays
                : balance.PrimaryProgressDays;
            int outputRoll = random.Next(100);
            float outputMultiplier = outputRoll < balance.LowOutputChance
                ? balance.LowOutputMultiplier
                : outputRoll >= 100 - balance.HighOutputChance
                    ? balance.HighOutputMultiplier
                    : 1f;
            float progress = baseOutput * outputMultiplier;
            WorkTask prerequisite = string.IsNullOrEmpty(task.PrerequisiteId)
                ? null
                : Tasks.FirstOrDefault(candidate => candidate.Id == task.PrerequisiteId);
            if (prerequisite != null && prerequisite.State != TaskState.Complete)
            {
                float progressLimit = task.EffectiveRequiredWork * balance.PrerequisiteProgressLimit;
                progress = Math.Max(0f, Math.Min(progress, progressLimit - task.Progress));
            }
            int fatigue = matched ? balance.MatchingFatigue : balance.MismatchedFatigue;
            if (task.IsParallelAssignment) fatigue += balance.ParallelFatigue;
            if (ParentWork(task)?.SoftDeadlineMissed == true) fatigue += balance.SoftDeadlineFatigue;

            task.Progress = Math.Min(task.EffectiveRequiredWork, task.Progress + progress);
            task.LastOutput = progress;
            member.Fatigue = Math.Min(100, member.Fatigue + fatigue);
            member.Experience++;
            task.LastWorker = task.AssignedCharacter;
            report.Lines.Add($"{member.Name}: {task.Name} +{progress:0.#}일 (피로 +{fatigue})");
            AddRecord(task, member.Name, RecordKind.Output, $"+{progress:0.#}일 진행");

            int accidentChance = member.Fatigue >= 80
                ? balance.HighFatigueAccidentChance
                : member.Fatigue >= 55 ? balance.MediumFatigueAccidentChance : 0;
            if (!matched) accidentChance += balance.MismatchAccidentChance;
            if (random.Next(100) < accidentChance)
            {
                member.InjuryDays = random.Next(2, 5);
                task.Progress = Math.Max(0f, task.Progress - .5f);
                Detach(task, true);
                report.Lines.Add($"사고: {member.Name}가 {member.InjuryDays}일 부상, 작업 일부 손실.");
                AddRecord(task, member.Name, RecordKind.Issue, $"{member.InjuryDays}일 부상");
            }
            else if (task.Progress + .001f >= task.EffectiveRequiredWork)
            {
                CompleteTask(task, member, report);
            }
        }

        private void CompleteTask(WorkTask task, CrewMember member, DayReport report)
        {
            task.Progress = task.EffectiveRequiredWork;
            task.CompletedDay = Day;
            task.State = TaskState.Complete;
            task.AssignedCharacter = -1;
            task.IsParallelAssignment = false;
            task.ScheduledDay = 0;
            task.ScheduledWorker = -1;
            report.Lines.Add($"완료: {task.Name}");
            AddRecord(task, member.Name, RecordKind.Output, "작업 완료");
            RefreshStates();
            WorkGroup group = ParentWork(task);
            if (group != null)
                TryGrantWorkReward(group, report);
        }

        private void Detach(WorkTask task, bool interrupted)
        {
            if (interrupted && task.Progress > 0f && task.State != TaskState.Complete &&
                task.State != TaskState.Failed)
            {
                task.ContextCostDays += balance.InterruptionCostDays + balance.ResumptionCostDays;
                task.SplitCount++;
                AddRecord(task, task.AssignedCharacter >= 0 ? Crew[task.AssignedCharacter].Name : "SYSTEM",
                    RecordKind.Issue,
                    $"작업 중단: 문맥 비용 +{balance.InterruptionCostDays + balance.ResumptionCostDays:0.#}일");
            }
            if (task.AssignedCharacter >= 0) task.LastWorker = task.AssignedCharacter;
            task.AssignedCharacter = -1;
            task.IsParallelAssignment = false;
            if (task.State != TaskState.Complete && task.State != TaskState.Failed)
                task.State = TaskState.Available;
        }

        private void ApplyDeadlineResults(DayReport report)
        {
            foreach (WorkGroup group in Groups)
            {
                if (group.State == WorkState.Complete || group.State == WorkState.Failed) continue;
                if (Day > group.SoftDeadline && !group.SoftDeadlineMissed)
                {
                    group.SoftDeadlineMissed = true;
                    if (!group.SoftPenaltyApplied)
                    {
                        group.SoftPenaltyApplied = true;
                        Resources = Math.Max(0, Resources - group.SoftPenaltyCredits);
                    }
                    report.Lines.Add($"소프트 마감 초과: {group.Name}");
                }
                if (Day <= group.HardDeadline) continue;
                group.State = WorkState.Failed;
                if (!group.HardPenaltyApplied)
                {
                    group.HardPenaltyApplied = true;
                    Resources = Math.Max(0, Resources - group.HardPenaltyCredits);
                }
                foreach (WorkTask task in Tasks.Where(candidate =>
                             candidate.GroupId == group.Id && candidate.State != TaskState.Complete))
                {
                    task.State = TaskState.Failed;
                    task.AssignedCharacter = -1;
                    task.IsParallelAssignment = false;
                    task.ScheduledDay = 0;
                    task.ScheduledWorker = -1;
                }
                report.Lines.Add($"하드 마감 실패: {group.Name}");
            }
        }

        private void RefreshStates()
        {
            foreach (WorkGroup group in Groups)
            {
                if (group.State == WorkState.Failed) continue;
                List<WorkTask> workTasks = Tasks.Where(task => task.GroupId == group.Id).ToList();
                bool complete = workTasks.Where(task => task.Required)
                    .All(task => task.State == TaskState.Complete);
                if (complete && workTasks.Any(task => task.Required))
                {
                    group.State = WorkState.Complete;
                    continue;
                }
                bool predecessorsComplete = group.PredecessorIds == null ||
                    group.PredecessorIds.All(id => Groups.Any(candidate =>
                        candidate.Id == id && candidate.State == WorkState.Complete));
                if (!predecessorsComplete)
                {
                    group.State = WorkState.Locked;
                    continue;
                }
                group.State = workTasks.Any(task => task.StartedDay > 0 || task.Progress > 0f)
                    ? WorkState.InProgress
                    : WorkState.Available;
            }

            foreach (WorkTask task in Tasks)
            {
                if (task.State == TaskState.Complete || task.State == TaskState.Failed) continue;
                WorkGroup group = ParentWork(task);
                bool workAvailable = group != null &&
                                     group.State != WorkState.Locked &&
                                     group.State != WorkState.Failed;
                if (!workAvailable)
                {
                    task.State = TaskState.Locked;
                    continue;
                }
                task.State = task.AssignedCharacter >= 0 && task.StartedDay > 0
                    ? TaskState.Active
                    : TaskState.Available;
            }
        }

        private void TriggerRandomWork(DayReport report)
        {
            int overdue = Groups.Count(group => group.SoftDeadlineMissed &&
                                                group.State != WorkState.Complete);
            int exhausted = Crew.Count(member => member.Fatigue >= 55);
            int chance = balance.BaseSideMissionChance + overdue * 16 + exhausted * 8;
            if (Groups.Count(group => group.Id != null && group.Id.StartsWith("random-work-") &&
                                      group.State != WorkState.Complete && group.State != WorkState.Failed) >=
                    balance.RandomWorkLimit ||
                random.Next(100) >= chance) return;

            int id = ++nextRandomWorkId;
            int softDays = random.Next(balance.RandomWorkMinSoftDays, balance.RandomWorkMaxSoftDays + 1);
            int reward = random.Next(balance.RandomWorkMinReward, balance.RandomWorkMaxReward + 1);
            var work = new WorkGroup
            {
                Id = $"random-work-{id}",
                Name = $"긴급 업무 {id}",
                SoftDeadline = Day + softDays,
                HardDeadline = Day + softDays + balance.RandomWorkHardDeadlineDays,
                Required = false,
                PredecessorIds = Array.Empty<string>(),
                State = WorkState.Available,
                RewardCredits = reward,
                SoftPenaltyCredits = balance.RandomWorkSoftPenalty,
                HardPenaltyCredits = balance.RandomWorkHardPenalty
            };
            WorkTask predecessor = random.Next(100) < balance.RandomWorkDependencyChance
                ? Tasks.Where(candidate => candidate.State != TaskState.Failed)
                    .OrderBy(candidate => candidate.Id).FirstOrDefault()
                : null;
            var mission = new WorkTask
            {
                Id = $"random-task-{id}",
                Name = exhausted > 0 ? "과로 인력 건강 점검" :
                    overdue > 0 ? "지연 일정 해명 보고" : "예고 없는 장비 점검",
                Kind = TaskKind.SideMission,
                RequiredRole = overdue > 0 || exhausted > 0 ? WorkRole.Management : WorkRole.Tech,
                RequiredWork = random.Next(2, 6),
                Required = true,
                PrerequisiteId = predecessor?.Id,
                Deadline = work.HardDeadline,
                State = TaskState.Available,
                GroupId = work.Id,
                Risk = RiskLevel.High,
                Importance = ImportanceLevel.Medium
            };
            Groups.Add(work);
            Tasks.Add(mission);
            report.Lines.Add($"사이드 업무 발생: {mission.Name}");
        }

        private void TryGrantWorkReward(WorkGroup group, DayReport report)
        {
            if (group.State != WorkState.Complete || group.RewardClaimed) return;
            group.RewardClaimed = true;
            Resources += group.RewardCredits;
            report.Lines.Add($"보상: {group.Name} credit +{group.RewardCredits}");
        }

        private void ApplyScheduledAssignments(DayReport report)
        {
            foreach (WorkTask task in Tasks.Where(candidate =>
                         candidate.ScheduledDay > 0 && candidate.ScheduledDay <= Day).ToList())
            {
                int worker = task.ScheduledWorker;
                bool assigned = worker >= 0 && worker < Crew.Count && Assign(task.Id, worker);
                report.Lines.Add(assigned
                    ? $"예약 시작: {Crew[worker].Name} → {task.Name}"
                    : $"예약 불발: {task.Name}");
                task.ScheduledDay = 0;
                task.ScheduledWorker = -1;
            }
        }

        private void NormalizeLoadedData()
        {
            foreach (WorkGroup group in Groups)
                if (group.PredecessorIds == null) group.PredecessorIds = Array.Empty<string>();
            foreach (WorkTask task in Tasks)
            {
                task.AssignedCharacter = task.AssignedCharacter < -1 ? -1 : task.AssignedCharacter;
                task.ScheduledWorker = task.ScheduledWorker < -1 ? -1 : task.ScheduledWorker;
                if (task.ScheduledDay <= 0 || task.ScheduledWorker < 0)
                {
                    task.ScheduledDay = 0;
                    task.ScheduledWorker = -1;
                }
                if (task.Progress > 0f && task.StartedDay <= 0)
                {
                    int referenceDay = task.State == TaskState.Complete
                        ? Math.Max(1, Day - 1)
                        : Day;
                    int elapsedDays = (int)task.Progress;
                    if (task.Progress - elapsedDays > .001f) elapsedDays++;
                    task.StartedDay = Math.Max(1,
                        referenceDay - elapsedDays + 1);
                }
                if (task.State == TaskState.Complete && task.CompletedDay <= 0)
                    task.CompletedDay = Math.Max(task.StartedDay, Day - 1);
                task.Records = task.Records ?? new List<TaskRecord>();
                WorkGroup group = ParentWork(task);
                if (group != null) task.Deadline = group.HardDeadline;
            }
            for (int i = 0; i < Crew.Count; i++)
            {
                CrewMember member = Crew[i];
                member.History = member.History ?? new List<string>();
                if (i < crewPortraits.Length && string.IsNullOrEmpty(member.PortraitLabel))
                    member.PortraitLabel = crewPortraits[i];
                if (i < crewMemos.Length && string.IsNullOrEmpty(member.Memo))
                    member.Memo = crewMemos[i];
                if (member.Perks == null)
                    member.Perks = i < crewPerks.Length && crewPerks[i] != null
                        ? crewPerks[i]
                        : Array.Empty<string>();
                member.RestScheduled = false;
            }
            int highestRandomWorkId = Groups.Where(group => group.Id != null &&
                                                            group.Id.StartsWith("random-work-"))
                .Select(group => int.TryParse(group.Id.Substring(12), out int value) ? value : 0)
                .DefaultIfEmpty(0).Max();
            nextRandomWorkId = highestRandomWorkId;
        }

        private WorkGroup ParentWork(WorkTask task) =>
            Groups.FirstOrDefault(group => group.Id == task.GroupId);

        private void AddAssignmentRecord(WorkTask task, int crewIndex, string text)
        {
            CrewMember member = Crew[crewIndex];
            AddRecord(task, member.Name, RecordKind.Note, text);
            member.History.Add($"DAY {Day}: {task.Name} 배정");
            Log($"{member.Name} → {task.Name}");
        }

        private void AddRecord(WorkTask task, string actor, RecordKind kind, string text)
        {
            task.Records = task.Records ?? new List<TaskRecord>();
            task.Records.Add(new TaskRecord { Day = Day, Actor = actor, Kind = kind, Text = text });
        }

        private void Log(string text)
        {
            SystemLog.Add($"DAY {Day:00}  {text}");
            if (SystemLog.Count > 100) SystemLog.RemoveAt(0);
        }
    }
}
