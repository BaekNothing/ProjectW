using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.MilestonePrototype
{
    public sealed class MilestoneSimulation
    {
        public const int TeamSize = 4;
        private readonly Random random;
        private readonly TaskSystemBalance balance;
        private readonly string[] crewPortraits;
        private readonly string[] crewMemos;
        private readonly string[][] crewPerks;
        private readonly int[][] crewCompetencies;
        private readonly RandomTaskWordPool randomTaskWords;
        private readonly WorkTask[] baseTasks;
        private readonly CodexEntry[] baseCodex;
        private int nextRandomWorkId;

        public int Day { get; private set; } = 1;
        public int CampaignEndDay { get; }
        public int MidpointReviewDay { get; }
        public int Resources { get; private set; }
        public float ParallelMaximumRemainingDays => balance.ParallelMaximumRemainingDays;
        public int RegenerationResourceCost => balance.RegenerationResourceCost;
        public float InterruptionAndResumptionCostDays =>
            balance.InterruptionCostDays + balance.ResumptionCostDays;
        public bool IsWon => Groups.Where(group => group.Required).All(group => group.State == WorkState.Complete);
        public bool IsLost => Groups.Any(group => group.Required && group.State == WorkState.Failed) ||
                              Day > CampaignEndDay ||
                              Crew.All(member => member.InjuryDays > 0);
        public List<WorkTask> Tasks { get; } = new List<WorkTask>();
        public List<CrewMember> Crew { get; } = new List<CrewMember>();
        public List<WorkGroup> Groups { get; } = new List<WorkGroup>();
        public List<MailEvent> Mail { get; } = new List<MailEvent>();
        public List<CodexEntry> Codex { get; } = new List<CodexEntry>();
        public List<string> DiscoveredTaskWordIds { get; } = new List<string>();
        public List<AssignmentRule> AssignmentRules { get; } = new List<AssignmentRule>();
        public List<string> SystemLog { get; } = new List<string>();
        public DayReport LastReport { get; private set; } = new DayReport();
        public bool MidpointReviewIssued { get; private set; }
        public bool CompetencyAutoAssignment { get; private set; }

        public bool IsWorkVisible(WorkGroup group) => group != null &&
            !group.AwaitingAcceptance && (group.RevealDay <= 0 || Day >= group.RevealDay);

        public void SetCompetencyAutoAssignment(bool enabled) =>
            CompetencyAutoAssignment = enabled;

        public MilestoneSimulation(int seed = 731) : this(TaskSystemDataLoader.Load(), seed)
        {
        }

        public MilestoneSimulation(TaskSystemData data, int seed = 731)
        {
            TaskSystemDataLoader.Validate(data);
            random = new Random(seed);
            balance = data.Balance;
            randomTaskWords = data.RandomTaskWords;
            baseTasks = data.Tasks;
            baseCodex = data.Codex ?? new CodexEntry[0];
            crewPortraits = new string[data.Crew.Length];
            crewMemos = new string[data.Crew.Length];
            crewPerks = new string[data.Crew.Length][];
            crewCompetencies = new int[data.Crew.Length][];
            for (int i = 0; i < data.Crew.Length; i++)
            {
                crewPortraits[i] = data.Crew[i].PortraitLabel;
                crewMemos[i] = data.Crew[i].Memo;
                crewPerks[i] = data.Crew[i].Perks;
                crewCompetencies[i] = data.Crew[i].Competencies;
            }
            CampaignEndDay = data.CampaignEndDay;
            MidpointReviewDay = data.MidpointReviewDay;
            Resources = data.StartingResources;
            Groups.AddRange(data.Works);
            Tasks.AddRange(data.Tasks);
            Crew.AddRange(data.Crew);
            if (data.Mail != null) Mail.AddRange(data.Mail);
            Codex.AddRange(baseCodex);
            NormalizeLoadedData();
            RefreshStates();
            LastReport.Lines.Add("첫 번째 개척 기지가 가동되었습니다.");
            Log("캠페인을 시작했습니다.");
        }

        public bool Assign(string taskId, int crewIndex)
        {
            WorkTask task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            bool assigned = AssignPrimary(task, crewIndex);
            if (assigned && crewIndex >= 0) LearnAssignmentRule(task, crewIndex);
            return assigned;
        }

        private bool AssignPrimary(WorkTask task, int crewIndex)
        {
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
                IsOngoingAssignment(candidate) &&
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
            if (!Tasks.Any(candidate => IsOngoingAssignment(candidate) &&
                                       candidate.AssignedCharacter == crewIndex && !candidate.IsParallelAssignment))
                return false;
            if (Tasks.Any(candidate => candidate != task && candidate.AssignedCharacter == crewIndex &&
                                       candidate.IsParallelAssignment && IsOngoingAssignment(candidate)))
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

            if (task.AssignedCharacter >= 0 &&
                (task.AssignedCharacter != crewIndex || day > Day))
                Detach(task, true);
            task.ScheduledDay = day;
            task.ScheduledWorker = crewIndex;
            AddRecord(task, Crew[crewIndex].Name, RecordKind.Note, $"D{day:00} 작업 시작 예약");
            Log($"{task.Name} 시작 예약: {Crew[crewIndex].Name}, DAY {day}");
            RefreshStates();
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
            if (targetWork != null && mail.ActivatesWork && targetWork.AwaitingAcceptance)
            {
                int acceptanceDelay = Math.Max(0, Day - mail.ArrivalDay);
                targetWork.SoftDeadline += acceptanceDelay;
                targetWork.HardDeadline += acceptanceDelay;
                targetWork.AwaitingAcceptance = false;
                foreach (WorkTask task in Tasks.Where(candidate => candidate.GroupId == targetWork.Id))
                    task.Deadline = targetWork.HardDeadline;
                RefreshStates();
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
            string relationship = $"담당자 신뢰도는 {member.Trust}%입니다. {TrustDescription(member.Trust)} ";
            if (member.InjuryDays > 0)
                return relationship + $"지금은 부상 회복 중입니다. 복귀까지 {member.InjuryDays}일 남았습니다.";
            if (member.RestScheduled)
                return relationship + "오늘은 휴식이 예정되어 있습니다. 회복 후 다시 보고드리겠습니다.";
            if (member.Fatigue >= 80)
                return relationship + $"피로도가 {member.Fatigue}%라 많이 지쳤습니다. 휴식이 필요합니다.";
            if (member.Fatigue >= 55)
                return relationship + $"피로도 {member.Fatigue}%입니다. 계속할 수 있지만 무리가 쌓이고 있습니다.";
            return relationship + $"괜찮습니다. 현재 피로도는 {member.Fatigue}%이고 바로 대응할 수 있습니다.";
        }

        public static string TrustDescription(int trust)
        {
            if (trust >= 75) return "당신의 판단을 깊이 신뢰하고 있습니다.";
            if (trust >= 55) return "협력할 만한 담당자로 보고 있습니다.";
            if (trust >= 35) return "아직 당신의 판단과 능력을 지켜보고 있습니다.";
            return "당신의 판단을 경계하고 있습니다.";
        }

        public string BuildWorkerWorkReply(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count) return string.Empty;
            WorkTask primary = Tasks.FirstOrDefault(task =>
                IsOngoingAssignment(task) && task.AssignedCharacter == crewIndex && !task.IsParallelAssignment);
            WorkTask parallel = Tasks.FirstOrDefault(task =>
                IsOngoingAssignment(task) && task.AssignedCharacter == crewIndex && task.IsParallelAssignment);
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
            ApplyLearnedAssignments(report);
            ApplyCompetencyAssignments(report);
            var pausedCrew = new bool[Crew.Count];
            var pausedConditions = new string[Crew.Count];
            for (int crewIndex = 0; crewIndex < Crew.Count; crewIndex++)
            {
                CrewMember member = Crew[crewIndex];
                pausedCrew[crewIndex] = member.InjuryDays > 0 || member.RestScheduled;
                pausedConditions[crewIndex] = member.Condition;
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
                ProcessTask(task, report, pausedCrew[task.AssignedCharacter],
                    pausedConditions[task.AssignedCharacter]);

            TriggerRandomWork(report);
            Day++;
            RefreshStates();
            ApplyDeadlineResults(report);
            ApplyMidpointReview(report);
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
                CanRunInParallel = task.RemainingWork <= balance.ParallelMaximumRemainingDays + .001f,
                CompetencyMultiplier = hasWorker ? CompetencyOutputMultiplier(Crew[crewIndex], task) : 1f
            };
        }

        public TaskScheduleEstimate EstimateSchedule(string taskId, int crewIndex)
        {
            WorkTask task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task == null || crewIndex < 0 || crewIndex >= Crew.Count) return null;

            float expectedOutput = ExpectedDailyOutput(task, crewIndex);
            float handoverWork = task.AssignedCharacter >= 0 &&
                                 task.AssignedCharacter != crewIndex &&
                                 task.Progress > 0f &&
                                 task.State != TaskState.Complete
                ? InterruptionAndResumptionCostDays
                : 0f;
            float estimatedWork = task.RemainingWork + handoverWork;
            int duration = task.State == TaskState.Complete
                ? 0
                : CeilPositive(estimatedWork / expectedOutput);
            int startDay = Day;
            bool rolling = false;
            bool dependencyDelay = ApplyDependencyStart(task, ref startDay, ref rolling, 0);

            bool workerDelay = false;
            WorkTask current = Tasks.FirstOrDefault(candidate =>
                candidate != task &&
                candidate.AssignedCharacter == crewIndex &&
                !candidate.IsParallelAssignment &&
                candidate.State != TaskState.Complete &&
                candidate.State != TaskState.Failed);
            if (current != null)
            {
                workerDelay = true;
                int currentCompletion = EstimateAssignedCompletion(current, 0);
                if (currentCompletion > 0)
                    startDay = Math.Max(startDay, currentCompletion + 1);
                else
                {
                    startDay = Math.Max(startDay, Day + 1);
                    rolling = true;
                }
            }

            CrewMember worker = Crew[crewIndex];
            if (worker.InjuryDays > 0)
            {
                workerDelay = true;
                startDay = Math.Max(startDay, Day + worker.InjuryDays);
            }
            if (worker.RestScheduled)
            {
                workerDelay = true;
                startDay = Math.Max(startDay, Day + 1);
            }

            string reason = rolling
                ? "막고 있는 일정의 담당자 또는 예상 완료일이 없어 내일로 순연"
                : dependencyDelay && workerDelay
                    ? "선행 일정과 담당자 기존 작업이 모두 끝난 뒤 시작"
                    : dependencyDelay
                        ? "선행 일정이 끝난 뒤 시작"
                        : workerDelay
                            ? "담당자의 기존 일정이 끝난 뒤 시작"
                            : "담당자가 비어 있어 즉시 시작";
            return new TaskScheduleEstimate
            {
                WorkerIndex = crewIndex,
                ExpectedDailyOutput = expectedOutput,
                EstimatedWork = estimatedWork,
                DurationDays = duration,
                StartDay = task.State == TaskState.Complete ? task.CompletedDay : startDay,
                CompletionDay = task.State == TaskState.Complete
                    ? task.CompletedDay
                    : startDay + Math.Max(0, duration - 1),
                RollingStart = rolling,
                StartReason = reason
            };
        }

        public TaskScheduleEstimate EstimatePreviewSchedule(string taskId)
        {
            WorkTask task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task == null) return null;
            if (task.State == TaskState.Complete)
                return new TaskScheduleEstimate
                {
                    WorkerIndex = task.LastWorker,
                    ExpectedDailyOutput = 1f,
                    EstimatedWork = 0f,
                    DurationDays = 0,
                    StartDay = task.StartedDay,
                    CompletionDay = task.CompletedDay,
                    StartReason = "완료된 실제 일정"
                };
            if (task.AssignedCharacter >= 0 && task.AssignedCharacter < Crew.Count)
                return EstimateSchedule(task.Id, task.AssignedCharacter);

            int workerIndex = task.ScheduledWorker >= 0 && task.ScheduledWorker < Crew.Count
                ? task.ScheduledWorker
                : -1;
            float output = workerIndex >= 0 ? ExpectedDailyOutput(task, workerIndex) : 1f;
            int startDay = task.ScheduledDay > 0 ? Math.Max(Day, task.ScheduledDay) : Day;
            bool rolling = false;
            bool dependencyDelay = ApplyPreviewDependencyStart(task, ref startDay, ref rolling, 0);
            if (workerIndex >= 0)
            {
                WorkTask current = Tasks.FirstOrDefault(candidate =>
                    candidate.AssignedCharacter == workerIndex &&
                    !candidate.IsParallelAssignment &&
                    candidate.State != TaskState.Complete &&
                    candidate.State != TaskState.Failed);
                if (current != null)
                {
                    int completion = EstimatePreviewCompletion(current, 1);
                    if (completion > 0) startDay = Math.Max(startDay, completion + 1);
                    else
                    {
                        startDay = Math.Max(startDay, Day + 1);
                        rolling = true;
                    }
                }
            }

            int duration = CeilPositive(task.RemainingWork / output);
            return new TaskScheduleEstimate
            {
                WorkerIndex = workerIndex,
                ExpectedDailyOutput = output,
                EstimatedWork = task.RemainingWork,
                DurationDays = duration,
                StartDay = startDay,
                CompletionDay = startDay + Math.Max(0, duration - 1),
                RollingStart = rolling,
                StartReason = workerIndex >= 0
                    ? dependencyDelay
                        ? "예약 담당자와 선행 일정 기준 미리보기"
                        : "예약 담당자의 시작일 기준 미리보기"
                    : dependencyDelay
                        ? "미배정 · 1일 산출량 기준으로 선행 일정 뒤에 배치"
                        : "미배정 · 1일 산출량 기준 미리보기"
            };
        }

        private float ExpectedDailyOutput(WorkTask task, int crewIndex)
        {
            int lowChance;
            int highChance;
            OutputChances(Crew[crewIndex].Fatigue, out lowChance, out highChance);
            float regularChance = 100 - lowChance - highChance;
            float expectedMultiplier =
                lowChance * balance.LowOutputMultiplier / 100f +
                regularChance / 100f +
                highChance * balance.HighOutputMultiplier / 100f;
            float dailyOutput = Crew[crewIndex].DailyOutput > 0f ? Crew[crewIndex].DailyOutput : 1f;
            float output = dailyOutput * balance.PrimaryProgressDays *
                           CompetencyOutputMultiplier(Crew[crewIndex], task) * expectedMultiplier;
            return output > .001f ? output : .001f;
        }

        public static float CompetencyOutputMultiplier(CrewMember member, WorkTask task)
        {
            if (member?.Competencies == null || task?.RequiredCompetencies == null ||
                task.RequiredCompetencies.Length == 0) return 1f;
            int total = 0;
            bool allBelowStandard = true;
            foreach (int competency in task.RequiredCompetencies)
            {
                int score = competency >= 0 && competency < member.Competencies.Length
                    ? member.Competencies[competency]
                    : 0;
                total += score;
                if (score >= 4) allBelowStandard = false;
            }
            if (allBelowStandard) return .5f;
            return Math.Max(.5f, total / (4f * task.RequiredCompetencies.Length));
        }

        public void OutputChances(int fatigue, out int lowChance, out int highChance)
        {
            int clampedFatigue = Math.Max(0, Math.Min(100, fatigue));
            if (clampedFatigue <= 50)
            {
                lowChance = InterpolateChance(
                    balance.FreshLowOutputChance, balance.LowOutputChance, clampedFatigue, 50);
                highChance = InterpolateChance(
                    balance.FreshHighOutputChance, balance.HighOutputChance, clampedFatigue, 50);
                return;
            }

            int exhaustedWeight = clampedFatigue - 50;
            lowChance = InterpolateChance(
                balance.LowOutputChance, balance.ExhaustedLowOutputChance, exhaustedWeight, 50);
            highChance = InterpolateChance(
                balance.HighOutputChance, balance.ExhaustedHighOutputChance, exhaustedWeight, 50);
        }

        private static int InterpolateChance(int start, int end, int weight, int range)
        {
            return (start * (range - weight) + end * weight + range / 2) / range;
        }

        private bool ApplyDependencyStart(WorkTask task, ref int startDay, ref bool rolling, int depth)
        {
            if (depth > Tasks.Count) return false;
            bool delayed = false;
            if (!string.IsNullOrEmpty(task.PrerequisiteId))
            {
                WorkTask blocker = Tasks.FirstOrDefault(candidate => candidate.Id == task.PrerequisiteId);
                if (blocker != null && blocker.State != TaskState.Complete)
                {
                    delayed = true;
                    ApplyBlockerCompletion(blocker, ref startDay, ref rolling, depth + 1);
                }
            }

            WorkGroup group = ParentWork(task);
            if (group?.PredecessorIds == null) return delayed;
            foreach (string predecessorId in group.PredecessorIds)
            {
                WorkGroup predecessorGroup = Groups.FirstOrDefault(candidate => candidate.Id == predecessorId);
                if (predecessorGroup == null || predecessorGroup.State == WorkState.Complete) continue;
                delayed = true;
                foreach (WorkTask blocker in Tasks.Where(candidate =>
                             candidate.GroupId == predecessorId && candidate.Required &&
                             candidate.State != TaskState.Complete))
                    ApplyBlockerCompletion(blocker, ref startDay, ref rolling, depth + 1);
            }
            return delayed;
        }

        private void ApplyBlockerCompletion(WorkTask blocker, ref int startDay, ref bool rolling, int depth)
        {
            int completion = EstimatePreviewCompletion(blocker, depth);
            if (completion > 0)
                startDay = Math.Max(startDay, completion + 1);
            else
            {
                startDay = Math.Max(startDay, Day + 1);
                rolling = true;
            }
        }

        private bool ApplyPreviewDependencyStart(
            WorkTask task, ref int startDay, ref bool rolling, int depth)
        {
            if (depth > Tasks.Count)
            {
                rolling = true;
                startDay = Math.Max(startDay, Day + 1);
                return false;
            }

            bool delayed = false;
            if (!string.IsNullOrEmpty(task.PrerequisiteId))
            {
                WorkTask blocker = Tasks.FirstOrDefault(candidate => candidate.Id == task.PrerequisiteId);
                if (blocker != null && blocker.State != TaskState.Complete)
                {
                    delayed = true;
                    int completion = EstimatePreviewCompletion(blocker, depth + 1);
                    if (completion > 0) startDay = Math.Max(startDay, completion + 1);
                    else
                    {
                        rolling = true;
                        startDay = Math.Max(startDay, Day + 1);
                    }
                }
            }

            WorkGroup group = ParentWork(task);
            if (group?.PredecessorIds == null) return delayed;
            foreach (string predecessorId in group.PredecessorIds)
            {
                WorkGroup predecessorGroup = Groups.FirstOrDefault(candidate => candidate.Id == predecessorId);
                if (predecessorGroup == null || predecessorGroup.State == WorkState.Complete) continue;
                delayed = true;
                foreach (WorkTask blocker in Tasks.Where(candidate =>
                             candidate.GroupId == predecessorId && candidate.Required &&
                             candidate.State != TaskState.Complete))
                {
                    int completion = EstimatePreviewCompletion(blocker, depth + 1);
                    if (completion > 0) startDay = Math.Max(startDay, completion + 1);
                    else
                    {
                        rolling = true;
                        startDay = Math.Max(startDay, Day + 1);
                    }
                }
            }
            return delayed;
        }

        private int EstimatePreviewCompletion(WorkTask task, int depth)
        {
            if (task.State == TaskState.Complete) return task.CompletedDay;
            if (depth > Tasks.Count) return 0;
            if (task.AssignedCharacter >= 0 && task.AssignedCharacter < Crew.Count)
                return EstimateAssignedCompletion(task, depth + 1);

            int workerIndex = task.ScheduledWorker >= 0 && task.ScheduledWorker < Crew.Count
                ? task.ScheduledWorker
                : -1;
            float output = workerIndex >= 0 ? ExpectedDailyOutput(task, workerIndex) : 1f;
            int startDay = task.ScheduledDay > 0 ? Math.Max(Day, task.ScheduledDay) : Day;
            bool rolling = false;
            ApplyPreviewDependencyStart(task, ref startDay, ref rolling, depth + 1);
            if (rolling) return 0;
            int duration = CeilPositive(task.RemainingWork / output);
            return startDay + Math.Max(0, duration - 1);
        }

        private int EstimateAssignedCompletion(WorkTask task, int depth)
        {
            if (task.State == TaskState.Complete) return task.CompletedDay;
            if (task.AssignedCharacter < 0 || task.AssignedCharacter >= Crew.Count ||
                depth > Tasks.Count)
                return 0;

            int startDay = Day;
            bool rolling = false;
            ApplyDependencyStart(task, ref startDay, ref rolling, depth + 1);
            if (rolling) return 0;
            float output = ExpectedDailyOutput(task, task.AssignedCharacter);
            int duration = CeilPositive(task.RemainingWork / output);
            return startDay + Math.Max(0, duration - 1);
        }

        private static int CeilPositive(float value)
        {
            if (value <= 0f) return 0;
            int whole = (int)value;
            return value - whole > .001f ? whole + 1 : Math.Max(1, whole);
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
            Log = SystemLog.ToArray(),
            AssignmentRules = AssignmentRules.ToArray(),
            DiscoveredTaskWordIds = DiscoveredTaskWordIds.ToArray(),
            MidpointReviewIssued = MidpointReviewIssued,
            CompetencyAutoAssignment = CompetencyAutoAssignment
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
            Crew.Clear();
            int restoredCrewCount = Math.Min(snapshot.Crew.Length, TeamSize);
            var restoredCrew = new CrewMember[restoredCrewCount];
            for (int i = 0; i < restoredCrewCount; i++) restoredCrew[i] = snapshot.Crew[i];
            Crew.AddRange(restoredCrew);
            Mail.Clear(); Mail.AddRange(snapshot.Mail);
            AssignmentRules.Clear();
            if (snapshot.AssignmentRules != null) AssignmentRules.AddRange(snapshot.AssignmentRules);
            DiscoveredTaskWordIds.Clear();
            if (snapshot.DiscoveredTaskWordIds != null)
            {
                foreach (string wordId in snapshot.DiscoveredTaskWordIds)
                    UnlockTaskWord(wordId, null);
            }
            MidpointReviewIssued = snapshot.MidpointReviewIssued;
            CompetencyAutoAssignment = snapshot.CompetencyAutoAssignment;
            SystemLog.Clear();
            if (snapshot.Log != null) SystemLog.AddRange(snapshot.Log);
            MigrateLegacyGeneratedSideMissions();
            NormalizeLoadedData();
            RefreshStates();
            return true;
        }

        private void ProcessTask(WorkTask task, DayReport report, bool pausedByCondition,
            string pausedCondition)
        {
            if (task.AssignedCharacter < 0) return;
            CrewMember member = Crew[task.AssignedCharacter];
            if (pausedByCondition)
            {
                task.LastOutput = 0f;
                task.LastOutcome = TaskOutcome.None;
                report.Lines.Add($"{member.Name}: {task.Name} 보류 · {pausedCondition}");
                return;
            }

            if (task.StartedDay <= 0) task.StartedDay = Day;
            task.State = TaskState.Active;
            bool matched = member.Specialty == task.RequiredRole;
            float baseOutput = member.DailyOutput > 0f ? member.DailyOutput : 1f;
            baseOutput *= task.IsParallelAssignment
                ? balance.ParallelProgressDays
                : balance.PrimaryProgressDays;
            baseOutput *= CompetencyOutputMultiplier(member, task);
            int lowOutputChance;
            int highOutputChance;
            OutputChances(member.Fatigue, out lowOutputChance, out highOutputChance);
            int outputRoll = random.Next(100);
            TaskOutcome outcome = outputRoll < lowOutputChance
                ? TaskOutcome.Failure
                : outputRoll >= 100 - highOutputChance
                    ? TaskOutcome.GreatSuccess
                    : TaskOutcome.Success;
            float outputMultiplier = outcome == TaskOutcome.Failure
                ? balance.LowOutputMultiplier
                : outcome == TaskOutcome.GreatSuccess ? balance.HighOutputMultiplier : 1f;
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
            task.LastOutcome = outcome;
            member.Fatigue = Math.Min(100, member.Fatigue + fatigue);
            member.Experience++;
            task.LastWorker = task.AssignedCharacter;
            string outcomeName = OutcomeName(outcome);
            report.Lines.Add($"{member.Name}: {task.Name} {outcomeName} +{progress:0.#}일 (피로 +{fatigue})");
            AddRecord(task, member.Name, RecordKind.Output, $"{outcomeName} · +{progress:0.#}일 진행");

            int accidentChance = member.Fatigue >= 80
                ? balance.HighFatigueAccidentChance
                : member.Fatigue >= 55 ? balance.MediumFatigueAccidentChance : 0;
            if (!matched) accidentChance += balance.MismatchAccidentChance;
            if (random.Next(100) < accidentChance)
            {
                member.InjuryDays = random.Next(2, 5);
                task.Progress = Math.Max(0f, task.Progress - .5f);
                report.Lines.Add(
                    $"사고: {member.Name}가 {member.InjuryDays}일 부상, 담당 유지 · 작업 일부 손실.");
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
            task.ScheduledDay = 0;
            task.ScheduledWorker = -1;
            report.Lines.Add($"완료: {task.Name}");
            AddRecord(task, member.Name, RecordKind.Output, "작업 완료");
            UnlockTaskWords(task, report);
            RefreshStates();
            WorkGroup group = ParentWork(task);
            if (group != null)
                TryGrantWorkReward(group, report);
        }

        private void UnlockTaskWords(WorkTask task, DayReport report)
        {
            UnlockTaskWord(task.GeneratedAdjectiveId, report);
            UnlockTaskWord(task.GeneratedTargetId, report);
            UnlockTaskWord(task.GeneratedActionId, report);
        }

        private void UnlockTaskWord(string wordId, DayReport report)
        {
            if (string.IsNullOrEmpty(wordId)) return;
            for (int i = 0; i < DiscoveredTaskWordIds.Count; i++)
            {
                if (DiscoveredTaskWordIds[i] == wordId) return;
            }

            CodexEntry entry = CreateTaskWordCodexEntry(wordId);
            if (entry == null) return;
            DiscoveredTaskWordIds.Add(wordId);
            for (int i = 0; i < Codex.Count; i++)
            {
                if (Codex[i].Id == entry.Id) return;
            }
            Codex.AddRange(new[] { entry });
            if (report != null) report.Lines.Add($"도감 해금: {entry.Name}");
        }

        private CodexEntry CreateTaskWordCodexEntry(string wordId)
        {
            foreach (RandomTaskAdjective word in randomTaskWords.Adjectives)
            {
                if (word.Id != wordId) continue;
                return new CodexEntry
                {
                    Id = $"task-word-{word.Id}",
                    Category = "임무 단어 · 형용사",
                    Name = word.Text,
                    Description = $"역할: 위험도와 난이도 결정\n위험도: {RiskDescription(word.Risk)}\n" +
                                  $"난이도 기여: +{word.Difficulty}\n담당 적성은 대상과 행동의 조합으로 결정됩니다."
                };
            }
            foreach (RandomTaskTarget word in randomTaskWords.Targets)
            {
                if (word.Id != wordId) continue;
                return new CodexEntry
                {
                    Id = $"task-word-{word.Id}",
                    Category = "임무 단어 · 대상",
                    Name = word.Text,
                    Description = $"추천 적성 역할: {RoleDescription(word.Role)}\n" +
                                  $"난이도 기여: +{word.Difficulty}\n같은 적성 역할의 행동과 조합됩니다."
                };
            }
            foreach (RandomTaskAction word in randomTaskWords.Actions)
            {
                if (word.Id != wordId) continue;
                return new CodexEntry
                {
                    Id = $"task-word-{word.Id}",
                    Category = "임무 단어 · 행동",
                    Name = word.Text,
                    Description = $"추천 적성 역할: {RoleDescription(word.Role)}\n" +
                                  $"난이도 기여: +{word.Difficulty}\n임무 담당자의 핵심 적성을 결정합니다."
                };
            }
            return null;
        }

        private static string RoleDescription(WorkRole role) =>
            role == WorkRole.Tech ? "기술" :
            role == WorkRole.Analysis ? "분석" :
            role == WorkRole.Management ? "관리" : "적응";

        private static string RiskDescription(RiskLevel risk) =>
            risk == RiskLevel.High ? "높음" : risk == RiskLevel.Medium ? "보통" : "낮음";

        public static string OutcomeName(TaskOutcome outcome) => outcome == TaskOutcome.Failure
            ? "실패"
            : outcome == TaskOutcome.GreatSuccess ? "대성공" : outcome == TaskOutcome.Success ? "성공" : "없음";

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
                if (group.AwaitingAcceptance || group.RevealDay > Day) continue;
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
                if (group.AwaitingAcceptance || group.RevealDay > Day)
                {
                    group.State = WorkState.Locked;
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
            int generatedWorkCount = Groups.Count(group => group.Id != null &&
                group.Id.StartsWith("random-work-") && group.State != WorkState.Complete &&
                group.State != WorkState.Failed);
            int availableSlots = Math.Max(0, balance.RandomWorkLimit - generatedWorkCount);
            if (availableSlots <= 0) return;

            int remainingSideMissions = Tasks.Count(task =>
            {
                if (task.Kind != TaskKind.SideMission || task.State == TaskState.Complete ||
                    task.State == TaskState.Failed) return false;
                WorkGroup parent = ParentWork(task);
                return parent != null && parent.State != WorkState.Complete &&
                       parent.State != WorkState.Failed;
            });
            int batchSize;
            if (remainingSideMissions == 0)
            {
                batchSize = Math.Min(availableSlots, random.Next(1, 4));
            }
            else
            {
                int overdue = Groups.Count(group => group.SoftDeadlineMissed &&
                                                    group.State != WorkState.Complete);
                int exhausted = Crew.Count(member => member.Fatigue >= 55);
                int rawChance = balance.BaseSideMissionChance + overdue * 16 + exhausted * 8;
                int scaledChanceBasisPoints = Math.Min(100, rawChance) *
                                              balance.RandomWorkChanceScalePercent;
                if (random.Next(10000) >= scaledChanceBasisPoints) return;
                batchSize = 1;
            }

            for (int i = 0; i < batchSize; i++) CreateRandomWork(report);
        }

        private void CreateRandomWork(DayReport report)
        {
            int id = ++nextRandomWorkId;
            int taskCount = random.Next(2, 5);
            int softDays = random.Next(balance.RandomWorkMinSoftDays,
                balance.RandomWorkMaxSoftDays + 1) + taskCount - 1;
            int reward = random.Next(balance.RandomWorkMinReward, balance.RandomWorkMaxReward + 1);
            RandomTaskTarget missionTarget =
                randomTaskWords.Targets[random.Next(randomTaskWords.Targets.Length)];
            RandomTaskAdjective missionAdjective =
                randomTaskWords.Adjectives[random.Next(randomTaskWords.Adjectives.Length)];
            var work = new WorkGroup
            {
                Id = $"random-work-{id}",
                Name = $"{missionAdjective.Text} {missionTarget.Text} 개척 임무",
                SoftDeadline = Day + softDays,
                HardDeadline = Day + softDays + balance.RandomWorkHardDeadlineDays,
                Required = false,
                PredecessorIds = Array.Empty<string>(),
                State = WorkState.Locked,
                AwaitingAcceptance = true,
                RewardCredits = reward,
                SoftPenaltyCredits = balance.RandomWorkSoftPenalty,
                HardPenaltyCredits = balance.RandomWorkHardPenalty
            };
            WorkTask predecessor = random.Next(100) < balance.RandomWorkDependencyChance
                ? Tasks.Where(candidate => candidate.State != TaskState.Failed)
                    .OrderBy(candidate => candidate.Id).FirstOrDefault()
                : null;
            string previousTaskId = predecessor?.Id;
            WorkTask firstTask = null;
            RiskLevel missionRisk = RiskLevel.Low;
            for (int taskIndex = 1; taskIndex <= taskCount; taskIndex++)
            {
                RandomTaskTarget target =
                    randomTaskWords.Targets[random.Next(randomTaskWords.Targets.Length)];
                RandomTaskAction action = SelectRandomAction(target.Role);
                RandomTaskAdjective adjective =
                    randomTaskWords.Adjectives[random.Next(randomTaskWords.Adjectives.Length)];
                var task = new WorkTask
                {
                    Id = $"random-task-{id}-{taskIndex}",
                    Name = $"{adjective.Text} {target.Text} {action.Text}",
                    Kind = TaskKind.SideMission,
                    RequiredRole = action.Role,
                    RequiredCompetencies = MergeCompetencies(
                        target.RequiredCompetencies, action.RequiredCompetencies),
                    RequiredWork = random.Next(
                        balance.RandomWorkMinRequiredDays,
                        balance.RandomWorkMaxRequiredDays + 1),
                    Required = true,
                    PrerequisiteId = previousTaskId,
                    Deadline = work.HardDeadline,
                    State = TaskState.Locked,
                    GroupId = work.Id,
                    Risk = adjective.Risk,
                    Importance = adjective.Risk == RiskLevel.High
                        ? ImportanceLevel.High
                        : ImportanceLevel.Medium,
                    Difficulty = Math.Max(1, Math.Min(5,
                        adjective.Difficulty + target.Difficulty + action.Difficulty)),
                    GeneratedAdjectiveId = adjective.Id,
                    GeneratedTargetId = target.Id,
                    GeneratedActionId = action.Id
                };
                if (firstTask == null) firstTask = task;
                if ((int)task.Risk > (int)missionRisk) missionRisk = task.Risk;
                Tasks.Add(task);
                previousTaskId = task.Id;
            }
            Groups.Add(work);
            Mail.Add(new MailEvent
            {
                Id = $"side-mission-offer-{id}",
                ArrivalDay = Day + 1,
                From = "외행성 개척 관제국",
                Subject = $"사이드 미션 제안: {work.Name}",
                Body = $"{taskCount}개 하위 일감으로 구성된 개척 임무입니다. 성공 보상은 자원 {reward}입니다.",
                Instruction = $"수락 시 작업이 활성화됩니다. 실패 페널티: 자원 {work.HardPenaltyCredits}",
                TargetTaskId = firstTask.Id,
                TargetWorkId = work.Id,
                Risk = missionRisk,
                ActivatesWork = true
            });
            report.Lines.Add($"다음 날 아침 사이드 미션 제안 메일 예정: {work.Name} ({taskCount}개 일감)");
        }

        private RandomTaskAction SelectRandomAction(WorkRole role)
        {
            int compatibleCount = 0;
            foreach (RandomTaskAction candidate in randomTaskWords.Actions)
            {
                if (candidate.Role == role) compatibleCount++;
            }

            int selectedIndex = random.Next(compatibleCount);
            foreach (RandomTaskAction candidate in randomTaskWords.Actions)
            {
                if (candidate.Role != role) continue;
                if (selectedIndex-- == 0) return candidate;
            }

            throw new InvalidOperationException("Validated random task action was not found.");
        }

        private static int[] MergeCompetencies(int[] first, int[] second)
        {
            var merged = new int[3];
            int count = 0;
            AddCompetencies(first, merged, ref count);
            AddCompetencies(second, merged, ref count);
            var result = new int[count];
            for (int i = 0; i < count; i++) result[i] = merged[i];
            return result;
        }

        private static void AddCompetencies(int[] source, int[] destination, ref int count)
        {
            if (source == null) return;
            foreach (int competency in source)
            {
                bool exists = false;
                for (int i = 0; i < count; i++)
                    if (destination[i] == competency) exists = true;
                if (!exists && count < destination.Length) destination[count++] = competency;
            }
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
                bool assigned = worker >= 0 && worker < Crew.Count && AssignPrimary(task, worker);
                report.Lines.Add(assigned
                    ? $"작업 시작: {Crew[worker].Name} → {task.Name}"
                    : $"시작 예약 불발: {task.Name}");
                task.ScheduledDay = 0;
                task.ScheduledWorker = -1;
            }
        }

        private void LearnAssignmentRule(WorkTask task, int crewIndex)
        {
            AssignmentRule rule = AssignmentRules.FirstOrDefault(candidate => candidate.Matches(task));
            if (rule == null)
            {
                AssignmentRules.Add(new AssignmentRule
                {
                    Kind = task.Kind,
                    RequiredRole = task.RequiredRole,
                    Difficulty = task.Difficulty,
                    Risk = task.Risk,
                    Importance = task.Importance,
                    CrewName = Crew[crewIndex].Name,
                    UpdateCount = 1
                });
                return;
            }

            rule.CrewName = Crew[crewIndex].Name;
            rule.UpdateCount++;
        }

        private void ApplyLearnedAssignments(DayReport report)
        {
            foreach (WorkTask task in Tasks.Where(candidate =>
                         candidate.AssignedCharacter < 0 &&
                         candidate.ScheduledDay <= 0 &&
                         candidate.State == TaskState.Available).ToList())
            {
                AssignmentRule rule = AssignmentRules.FirstOrDefault(candidate => candidate.Matches(task));
                if (rule == null || !AutomaticDependenciesComplete(task)) continue;
                int worker = Crew.FindIndex(member => member.Name == rule.CrewName);
                if (worker < 0 || !Crew[worker].Available ||
                    Tasks.Any(candidate => candidate.AssignedCharacter == worker &&
                                           !candidate.IsParallelAssignment &&
                                           IsOngoingAssignment(candidate)))
                    continue;
                if (!AssignPrimary(task, worker)) continue;
                report.Lines.Add($"자동 배정: {Crew[worker].Name} → {task.Name}");
            }
        }

        private void ApplyCompetencyAssignments(DayReport report)
        {
            if (!CompetencyAutoAssignment) return;
            foreach (WorkTask task in Tasks)
            {
                if (task.AssignedCharacter >= 0 || task.ScheduledDay > 0 ||
                    task.State != TaskState.Available || !AutomaticDependenciesComplete(task)) continue;
                int bestWorker = -1;
                float bestMultiplier = -1f;
                int bestFatigue = int.MaxValue;
                for (int worker = 0; worker < Crew.Count; worker++)
                {
                    CrewMember member = Crew[worker];
                    if (!member.Available || Tasks.Any(candidate =>
                            candidate.AssignedCharacter == worker && !candidate.IsParallelAssignment &&
                            IsOngoingAssignment(candidate)))
                        continue;
                    float multiplier = CompetencyOutputMultiplier(member, task);
                    if (multiplier < bestMultiplier ||
                        Math.Abs(multiplier - bestMultiplier) < .001f && member.Fatigue >= bestFatigue)
                        continue;
                    bestWorker = worker;
                    bestMultiplier = multiplier;
                    bestFatigue = member.Fatigue;
                }
                if (bestWorker < 0 || !AssignPrimary(task, bestWorker)) continue;
                report.Lines.Add(
                    $"역량 자동 배정: {Crew[bestWorker].Name} → {task.Name} (×{bestMultiplier:0.##})");
            }
        }

        private bool AutomaticDependenciesComplete(WorkTask task)
        {
            WorkGroup group = ParentWork(task);
            if (group == null || group.State == WorkState.Locked || group.State == WorkState.Failed)
                return false;
            if (group.PredecessorIds != null)
            {
                foreach (string predecessorId in group.PredecessorIds)
                {
                    WorkGroup predecessor = Groups.FirstOrDefault(candidate => candidate.Id == predecessorId);
                    if (predecessor == null || predecessor.State != WorkState.Complete) return false;
                }
            }
            if (string.IsNullOrEmpty(task.PrerequisiteId)) return true;
            WorkTask prerequisite = Tasks.FirstOrDefault(candidate => candidate.Id == task.PrerequisiteId);
            return prerequisite != null && prerequisite.State == TaskState.Complete;
        }

        private void ApplyMidpointReview(DayReport report)
        {
            if (MidpointReviewIssued || Day < MidpointReviewDay) return;
            MidpointReviewIssued = true;
            int completed = Groups.Count(group => group.State == WorkState.Complete);
            int overdue = Groups.Count(group => group.SoftDeadlineMissed &&
                                                group.State != WorkState.Complete);
            int failed = Groups.Count(group => group.State == WorkState.Failed);
            report.Lines.Add(
                $"중간평가 (DAY {MidpointReviewDay}): 완료 {completed}/{Groups.Count}, 지연 {overdue}, 실패 {failed}");
        }

        private void NormalizeLoadedData()
        {
            foreach (WorkGroup group in Groups)
                if (group.PredecessorIds == null) group.PredecessorIds = Array.Empty<string>();
            foreach (WorkTask task in Tasks)
            {
                task.AssignedCharacter = task.AssignedCharacter < -1 ? -1 : task.AssignedCharacter;
                task.ScheduledWorker = task.ScheduledWorker < -1 ? -1 : task.ScheduledWorker;
                if (task.AssignedCharacter >= Crew.Count)
                {
                    task.AssignedCharacter = -1;
                    task.IsParallelAssignment = false;
                }
                if (task.ScheduledWorker >= Crew.Count)
                {
                    task.ScheduledDay = 0;
                    task.ScheduledWorker = -1;
                }
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
                BackfillGeneratedWordIds(task);
                BackfillRequiredCompetencies(task);
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
                if (member.Competencies == null || member.Competencies.Length != CrewMember.CompetencyCount)
                    member.Competencies = i < crewCompetencies.Length && crewCompetencies[i] != null
                        ? (int[])crewCompetencies[i].Clone()
                        : new int[CrewMember.CompetencyCount];
                member.RestScheduled = false;
            }
            int highestRandomWorkId = Groups.Where(group => group.Id != null &&
                                                            group.Id.StartsWith("random-work-"))
                .Select(group => int.TryParse(group.Id.Substring(12), out int value) ? value : 0)
                .DefaultIfEmpty(0).Max();
            nextRandomWorkId = highestRandomWorkId;
        }

        private void MigrateLegacyGeneratedSideMissions()
        {
            foreach (WorkGroup group in Groups)
            {
                if (group.Id == null || !group.Id.StartsWith("random-work-") ||
                    group.State == WorkState.Complete || group.State == WorkState.Failed) continue;
                List<WorkTask> children = Tasks.Where(task => task.GroupId == group.Id).ToList();
                if (children.Count != 1 || children[0].Kind != TaskKind.SideMission) continue;

                WorkTask previous = children[0];
                group.SoftDeadline += 2;
                group.HardDeadline += 2;
                previous.Deadline = group.HardDeadline;
                RiskLevel missionRisk = previous.Risk;
                string idSuffix = group.Id.Substring(12);
                for (int taskIndex = 2; taskIndex <= 3; taskIndex++)
                {
                    RandomTaskTarget target =
                        randomTaskWords.Targets[random.Next(randomTaskWords.Targets.Length)];
                    RandomTaskAction action = SelectRandomAction(target.Role);
                    RandomTaskAdjective adjective =
                        randomTaskWords.Adjectives[random.Next(randomTaskWords.Adjectives.Length)];
                    var task = new WorkTask
                    {
                        Id = $"random-task-{idSuffix}-{taskIndex}",
                        Name = $"{adjective.Text} {target.Text} {action.Text}",
                        Kind = TaskKind.SideMission,
                        RequiredRole = action.Role,
                        RequiredCompetencies = MergeCompetencies(
                            target.RequiredCompetencies, action.RequiredCompetencies),
                        RequiredWork = random.Next(balance.RandomWorkMinRequiredDays,
                            balance.RandomWorkMaxRequiredDays + 1),
                        Required = true,
                        PrerequisiteId = previous.Id,
                        Deadline = group.HardDeadline,
                        State = TaskState.Locked,
                        GroupId = group.Id,
                        Risk = adjective.Risk,
                        Importance = adjective.Risk == RiskLevel.High
                            ? ImportanceLevel.High
                            : ImportanceLevel.Medium,
                        Difficulty = Math.Max(1, Math.Min(5,
                            adjective.Difficulty + target.Difficulty + action.Difficulty)),
                        GeneratedAdjectiveId = adjective.Id,
                        GeneratedTargetId = target.Id,
                        GeneratedActionId = action.Id
                    };
                    if ((int)task.Risk > (int)missionRisk) missionRisk = task.Risk;
                    Tasks.Add(task);
                    previous = task;
                }

                MailEvent offer = Mail.FirstOrDefault(mail => mail.TargetWorkId == group.Id &&
                                                              mail.ActivatesWork);
                if (offer == null) continue;
                offer.Subject = $"사이드 미션 제안: {group.Name}";
                offer.Body = $"3개 하위 일감으로 재구성된 개척 임무입니다. 성공 보상은 자원 {group.RewardCredits}입니다.";
                offer.TargetTaskId = children[0].Id;
                offer.Risk = missionRisk;
            }
        }

        private void BackfillGeneratedWordIds(WorkTask task)
        {
            if (task == null || task.Kind != TaskKind.SideMission ||
                !string.IsNullOrEmpty(task.GeneratedAdjectiveId) ||
                string.IsNullOrEmpty(task.Name)) return;

            foreach (RandomTaskAdjective adjective in randomTaskWords.Adjectives)
            {
                foreach (RandomTaskTarget target in randomTaskWords.Targets)
                {
                    foreach (RandomTaskAction action in randomTaskWords.Actions)
                    {
                        if (target.Role != action.Role ||
                            task.Name != $"{adjective.Text} {target.Text} {action.Text}") continue;
                        task.GeneratedAdjectiveId = adjective.Id;
                        task.GeneratedTargetId = target.Id;
                        task.GeneratedActionId = action.Id;
                        return;
                    }
                }
            }
        }

        private void BackfillRequiredCompetencies(WorkTask task)
        {
            if (task.RequiredCompetencies != null && task.RequiredCompetencies.Length > 0) return;
            foreach (WorkTask definition in baseTasks)
            {
                if (definition.Id != task.Id || definition.RequiredCompetencies == null) continue;
                task.RequiredCompetencies = (int[])definition.RequiredCompetencies.Clone();
                return;
            }
            RandomTaskTarget target = null;
            RandomTaskAction action = null;
            foreach (RandomTaskTarget candidate in randomTaskWords.Targets)
                if (candidate.Id == task.GeneratedTargetId) target = candidate;
            foreach (RandomTaskAction candidate in randomTaskWords.Actions)
                if (candidate.Id == task.GeneratedActionId) action = candidate;
            task.RequiredCompetencies = target != null && action != null
                ? MergeCompetencies(target.RequiredCompetencies, action.RequiredCompetencies)
                : new[] { (int)task.RequiredRole };
        }

        private WorkGroup ParentWork(WorkTask task) =>
            Groups.FirstOrDefault(group => group.Id == task.GroupId);

        private static bool IsOngoingAssignment(WorkTask task) => task != null &&
            task.State != TaskState.Complete && task.State != TaskState.Failed;

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
