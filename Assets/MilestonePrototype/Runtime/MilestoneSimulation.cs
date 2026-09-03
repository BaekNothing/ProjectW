using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectW.MilestonePrototype
{
    public sealed class MilestoneSimulation
    {
        private enum PerkEffect { Fatigue, RestRecovery, WeekendRecovery }

        public const int CriticalResponseWindowDays = 7;
        public const int TeamSize = 4;
        private readonly Random random;
        private readonly Random proposalRandom;
        private readonly TaskSystemBalance balance;
        private readonly string[] crewPortraits;
        private readonly string[] crewPortraitAddresses;
        private readonly string[] crewPersonalities;
        private readonly string[] crewMemos;
        private readonly string[][] crewPerks;
        private readonly int[][] crewCompetencies;
        private readonly WorkRole[] crewSpecialties;
        private readonly int[] crewSkills;
        private readonly float[] crewDailyOutputs;
        private readonly RandomTaskWordPool randomTaskWords;
        private readonly WorkTask[] baseTasks;
        private readonly CodexEntry[] baseCodex;
        private readonly CriticalEventDefinition[] criticalEvents;
        private readonly PerkDefinition[] perkDefinitions;
        private int nextRandomWorkId;
        private int nextReadyProposalId;
        private int nextProposalBatchDay;

        public int Day { get; private set; } = 1;
        public int CampaignEndDay { get; }
        public int MidpointReviewDay { get; }
        public int Resources { get; private set; }
        public float ParallelMaximumRemainingDays => balance.ParallelMaximumRemainingDays;
        public int RegenerationResourceCost => balance.RegenerationResourceCost;
        public int RegenerationAbilityInheritanceCost => balance.RegenerationAbilityInheritanceCost;
        public int RegenerationPerkInheritanceCost => balance.RegenerationPerkInheritanceCost;
        public int RegenerationPersonalityRetentionWeight =>
            balance.RegenerationPersonalityRetentionWeight;
        public int InitialBaseSalary => balance.BaseSalary;
        public int PayrollIntervalDays => balance.PayrollIntervalDays;
        public int NextPayrollDay => ((Day - 1) / balance.PayrollIntervalDays + 1) *
                                     balance.PayrollIntervalDays;
        public float InterruptionAndResumptionCostDays =>
            balance.InterruptionCostDays + balance.ResumptionCostDays;
        public int PlanningHorizonDay => Math.Max(CampaignEndDay, Day + 30);
        public bool IsWon => false;
        public bool IsLost => Resources <= 0;
        public List<WorkTask> Tasks { get; } = new List<WorkTask>();
        public List<CrewMember> Crew { get; } = new List<CrewMember>();
        public List<WorkGroup> Groups { get; } = new List<WorkGroup>();
        public List<MailEvent> Mail { get; } = new List<MailEvent>();
        private readonly List<MailEvent> pendingWeeklyFieldItems = new List<MailEvent>();
        public List<CodexEntry> Codex { get; } = new List<CodexEntry>();
        public List<string> DiscoveredTaskWordIds { get; } = new List<string>();
        public List<string> DiscoveredCrewTraitIds { get; } = new List<string>();
        public List<string> DiscoveredCrewTraitSources { get; } = new List<string>();
        public List<AssignmentRule> AssignmentRules { get; } = new List<AssignmentRule>();
        public List<ReadyMadeProposal> ReadyMadeProposals { get; } = new List<ReadyMadeProposal>();
        public int NextProposalBatchDay => nextProposalBatchDay;
        public List<string> SystemLog { get; } = new List<string>();
        public DayReport LastReport { get; private set; } = new DayReport();
        public bool MidpointReviewIssued { get; private set; }
        public bool CompetencyAutoAssignment { get; private set; }
        public Weekday CurrentWeekday => (Weekday)((Day - 1) % 7);
        public int UnscheduledCheckupResourceCost => balance.UnscheduledCheckupResourceCost;
        public MedicalResult[] PendingMedicalResults { get; private set; } = new MedicalResult[0];
        public string ActiveCriticalEventId { get; private set; }
        public string ActiveCriticalNodeId { get; private set; }
        public int ActiveCriticalNodeArrivalDay { get; private set; }
        public int TaskSuccessChanceModifier { get; private set; }
        public bool HasActiveCriticalEvent => !string.IsNullOrEmpty(ActiveCriticalEventId);
        public bool HasPendingCriticalChoice
        {
            get
            {
                foreach (MailEvent mail in Mail)
                    if (mail.IsCritical && mail.CriticalEventId == ActiveCriticalEventId &&
                        mail.CriticalNodeId == ActiveCriticalNodeId && !mail.Resolved)
                        return true;
                return false;
            }
        }
        public int CriticalResponseDeadlineDay =>
            ActiveCriticalNodeArrivalDay + CriticalResponseWindowDays - 1;
        public bool MustResolveCriticalChoice =>
            HasPendingCriticalChoice && Day >= CriticalResponseDeadlineDay;
        public bool CanForceCriticalEvent => !IsLost && !HasActiveCriticalEvent && criticalEvents.Length > 0;

        public bool IsWorkVisible(WorkGroup group) => group != null &&
            !group.AwaitingAcceptance && (group.RevealDay <= 0 || Day >= group.RevealDay) &&
            Tasks.Any(task => task.GroupId == group.Id);

        public MailEvent PendingIncidentOffer => Mail.FirstOrDefault(mail =>
            mail.IsBossRequest && !mail.Resolved && mail.ArrivalDay <= Day &&
            Groups.Any(group => group.Id == mail.TargetWorkId && group.AwaitingAcceptance));

        public bool HasPendingIncidentDecision => PendingIncidentOffer != null;
        public bool CanAdvanceDay => !IsLost && !MustResolveCriticalChoice &&
                                     !HasPendingIncidentDecision;

        public bool AcceptIncidentWork(string mailId) => ResolveMail(mailId);

        public bool DeclineIncidentWork(string mailId)
        {
            MailEvent mail = Mail.FirstOrDefault(item => item.Id == mailId &&
                item.IsBossRequest && !item.Resolved && item.ArrivalDay <= Day);
            WorkGroup work = mail == null
                ? null
                : Groups.FirstOrDefault(group => group.Id == mail.TargetWorkId &&
                    group.AwaitingAcceptance);
            if (mail == null || work == null) return false;
            Resources = Math.Max(0, Resources - work.HardPenaltyCredits);
            work.State = WorkState.Failed;
            foreach (WorkTask task in Tasks.Where(candidate => candidate.GroupId == work.Id))
            {
                task.State = TaskState.Failed;
                task.AssignedCharacter = -1;
                task.IsParallelAssignment = false;
                task.ScheduledDay = 0;
                task.ScheduledWorker = -1;
            }
            mail.Read = true;
            mail.Resolved = true;
            mail.Instruction = $"거절 완료 · 자원 -{work.HardPenaltyCredits}";
            Log($"돌발임무 거절: {work.Name} / 자원 -{work.HardPenaltyCredits}");
            return true;
        }

        public void SetCompetencyAutoAssignment(bool enabled) =>
            CompetencyAutoAssignment = enabled;

        public bool MoveWorkPriority(string workId, int direction)
        {
            List<WorkGroup> ordered = Groups.Where(IsWorkVisible)
                .Where(group => group.State != WorkState.Complete && group.State != WorkState.Failed)
                .OrderBy(group => group.Priority).ThenBy(group => group.Id).ToList();
            int index = ordered.FindIndex(group => group.Id == workId);
            int otherIndex = index + direction;
            if (index < 0 || otherIndex < 0 || otherIndex >= ordered.Count) return false;
            int priority = ordered[index].Priority;
            ordered[index].Priority = ordered[otherIndex].Priority;
            ordered[otherIndex].Priority = priority;
            if (ordered[index].Priority == ordered[otherIndex].Priority)
            {
                ordered[index].Priority = otherIndex;
                ordered[otherIndex].Priority = index;
            }
            NormalizeWorkPriorities();
            return true;
        }

        public bool ConfigureWorkFocus(string workId, bool urgent, bool allOut)
        {
            WorkGroup work = Groups.FirstOrDefault(group => group.Id == workId && IsWorkVisible(group));
            if (work == null || work.State == WorkState.Complete || work.State == WorkState.Failed) return false;
            work.Urgent = urgent;
            work.AllOut = allOut;
            return true;
        }

        public static string WeekdayName(Weekday weekday)
        {
            switch (weekday)
            {
                case Weekday.Monday: return "월";
                case Weekday.Tuesday: return "화";
                case Weekday.Wednesday: return "수";
                case Weekday.Thursday: return "목";
                case Weekday.Friday: return "금";
                case Weekday.Saturday: return "토";
                default: return "일";
            }
        }

        public static bool IsWeekendDay(int day)
        {
            int weekday = (Math.Max(1, day) - 1) % 7;
            return weekday == (int)Weekday.Saturday || weekday == (int)Weekday.Sunday;
        }

        public bool IsRegularCheckupDay(int day) =>
            day >= balance.FirstRegularCheckupDay &&
            (day - balance.FirstRegularCheckupDay) % balance.RegularCheckupIntervalDays == 0 &&
            (Weekday)((day - 1) % 7) == Weekday.Friday;

        public int PreviewAutomaticAssignee(string taskId, out string source)
        {
            source = string.Empty;
            WorkTask task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);
            if (task == null || task.AssignedCharacter >= 0) return -1;
            if (task.ScheduledDay > 0 && task.ScheduledWorker >= 0 && task.ScheduledWorker < Crew.Count)
            {
                source = $"예약 D{task.ScheduledDay:00}";
                return task.ScheduledWorker;
            }
            if (task.State != TaskState.Available || !AutomaticDependenciesComplete(task)) return -1;

            AssignmentRule rule = AssignmentRules.FirstOrDefault(candidate => candidate.Matches(task));
            if (rule != null)
            {
                int learnedWorker = Crew.FindIndex(member => member.Name == rule.CrewName);
                if (WorkerCanTakePrimary(learnedWorker))
                {
                    source = "학습 규칙";
                    return learnedWorker;
                }
            }
            if (!CompetencyAutoAssignment) return -1;

            int bestWorker = -1;
            float bestMultiplier = -1f;
            int bestFatigue = int.MaxValue;
            for (int worker = 0; worker < Crew.Count; worker++)
            {
                if (!WorkerCanTakePrimary(worker)) continue;
                CrewMember member = Crew[worker];
                float multiplier = CompetencyOutputMultiplier(member, task);
                if (multiplier < bestMultiplier ||
                    Math.Abs(multiplier - bestMultiplier) < .001f && member.Fatigue >= bestFatigue)
                    continue;
                bestWorker = worker;
                bestMultiplier = multiplier;
                bestFatigue = member.Fatigue;
            }
            if (bestWorker >= 0) source = $"역량 ×{bestMultiplier:0.##}";
            return bestWorker;
        }

        public static string MedicalGrade(int value) =>
            value >= 70 ? "양호" : value >= 40 ? "주의" : "위험";

        public MilestoneSimulation(int seed = 731) : this(TaskSystemDataLoader.Load(), seed)
        {
        }

        public MilestoneSimulation(TaskSystemData data, int seed = 731)
        {
            TaskSystemDataLoader.Validate(data);
            random = new Random(seed);
            proposalRandom = new Random(seed ^ 1597463007);
            balance = data.Balance;
            randomTaskWords = data.RandomTaskWords;
            baseTasks = data.Tasks;
            baseCodex = data.Codex ?? new CodexEntry[0];
            criticalEvents = data.CriticalEvents ?? new CriticalEventDefinition[0];
            perkDefinitions = data.PerkDefinitions ?? new PerkDefinition[0];
            crewPortraits = new string[data.Crew.Length];
            crewPortraitAddresses = new string[data.Crew.Length];
            crewPersonalities = new string[data.Crew.Length];
            crewMemos = new string[data.Crew.Length];
            crewPerks = new string[data.Crew.Length][];
            crewCompetencies = new int[data.Crew.Length][];
            crewSpecialties = new WorkRole[data.Crew.Length];
            crewSkills = new int[data.Crew.Length];
            crewDailyOutputs = new float[data.Crew.Length];
            for (int i = 0; i < data.Crew.Length; i++)
            {
                crewPortraits[i] = data.Crew[i].PortraitLabel;
                crewPortraitAddresses[i] = data.Crew[i].PortraitAddress;
                crewPersonalities[i] = data.Crew[i].Personality;
                crewMemos[i] = data.Crew[i].Memo;
                crewPerks[i] = data.Crew[i].Perks == null
                    ? Array.Empty<string>()
                    : (string[])data.Crew[i].Perks.Clone();
                crewCompetencies[i] = (int[])data.Crew[i].Competencies.Clone();
                crewSpecialties[i] = data.Crew[i].Specialty;
                crewSkills[i] = data.Crew[i].Skill;
                crewDailyOutputs[i] = data.Crew[i].DailyOutput;
            }
            CampaignEndDay = data.CampaignEndDay;
            MidpointReviewDay = data.MidpointReviewDay;
            Resources = data.StartingResources;
            CompetencyAutoAssignment = true;
            Groups.AddRange(data.Works);
            Tasks.AddRange(data.Tasks);
            Crew.AddRange(data.Crew);
            if (data.Mail != null) Mail.AddRange(data.Mail);
            ExtractWeeklyFieldItemsFromMail();
            Codex.AddRange(baseCodex);
            DiscoverCurrentCrewTraits();
            NormalizeLoadedData();
            GenerateReadyMadeProposalBatch(null);
            RefreshStates();
            LastReport.Lines.Add("첫 번째 개척 기지가 가동되었습니다.");
            Log("캠페인을 시작했습니다.");
            DeliverWeeklyFieldReport();
            TriggerCriticalEvent();
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
            return Regenerate(crewIndex, false, false);
        }

        public int RegenerationCost(bool inheritAbilities, bool inheritPerks)
        {
            return balance.RegenerationResourceCost +
                   (inheritAbilities ? balance.RegenerationAbilityInheritanceCost : 0) +
                   (inheritPerks ? balance.RegenerationPerkInheritanceCost : 0);
        }

        public bool Regenerate(int crewIndex, bool inheritAbilities, bool inheritPerks)
        {
            int cost = RegenerationCost(inheritAbilities, inheritPerks);
            if (crewIndex < 0 || crewIndex >= Crew.Count ||
                Resources < cost) return false;
            CrewMember member = Crew[crewIndex];
            string previousPersonality = member.Personality;
            Resources -= cost;
            if (!inheritAbilities)
            {
                member.Specialty = crewSpecialties[crewIndex];
                member.Skill = crewSkills[crewIndex];
                member.Competencies = (int[])crewCompetencies[crewIndex].Clone();
                member.DailyOutput = crewDailyOutputs[crewIndex];
            }
            if (!inheritPerks)
                member.Perks = (string[])crewPerks[crewIndex].Clone();
            member.Fatigue = 0;
            member.InjuryDays = 0;
            member.RestScheduled = false;
            member.Experience = 0;
            member.Personality = RollRegeneratedPersonality(previousPersonality);
            DiscoverCrewTraits(crewIndex);
            member.RegenerationCount++;
            string inheritance = inheritAbilities && inheritPerks
                ? "능력·퍽 인계"
                : inheritAbilities ? "능력 인계" : inheritPerks ? "퍽 인계" : "인계 없음";
            member.History.Add($"DAY {Day}: 재생 시술 · {inheritance} · 성격 {member.Personality}");
            Log($"{member.Name} 재생 시술, {inheritance} · 경력 0 · 기본급 {CurrentBaseSalary(member)} · 자원 -{cost}");
            RefreshStates();
            return true;
        }

        private string RollRegeneratedPersonality(string previousPersonality)
        {
            if (!string.IsNullOrEmpty(previousPersonality) &&
                random.Next(100) < balance.RegenerationPersonalityRetentionWeight)
                return previousPersonality;
            int alternatives = 0;
            for (int i = 0; i < crewPersonalities.Length; i++)
                if (!string.IsNullOrEmpty(crewPersonalities[i]) &&
                    crewPersonalities[i] != previousPersonality)
                    alternatives++;
            if (alternatives == 0) return previousPersonality;
            int selected = random.Next(alternatives);
            for (int i = 0; i < crewPersonalities.Length; i++)
            {
                string candidate = crewPersonalities[i];
                if (string.IsNullOrEmpty(candidate) || candidate == previousPersonality) continue;
                if (selected == 0) return candidate;
                selected--;
            }
            return previousPersonality;
        }

        public bool ResolveMail(string mailId)
        {
            MailEvent mail = Mail.FirstOrDefault(item => item.Id == mailId && item.ArrivalDay <= Day);
            if (mail == null || mail.Resolved || mail.IsCritical || mail.IsProposal ||
                mail.IsWeeklyFieldReport || IsWeeklyFieldIncidentMail(mail)) return false;
            mail.Read = true;

            if (mail.IsMedicalReport)
            {
                if (mail.MedicalResults != null)
                    foreach (MedicalResult result in mail.MedicalResults)
                    {
                        if (result == null || result.CrewIndex < 0 || result.CrewIndex >= Crew.Count) continue;
                        CrewMember member = Crew[result.CrewIndex];
                        member.MedicalFileUpdatedDay = Day;
                        member.MedicalFileHealth = result.Health;
                        member.MedicalFileFatigue = result.Fatigue;
                        member.MedicalFileMental = result.Mental;
                        member.MedicalFileTrust = result.Trust;
                        member.History.Add($"DAY {Day}: 검진 파일 갱신");
                    }
                mail.Resolved = true;
                Log($"검진 파일 다운로드: {mail.Subject}");
                return true;
            }

            ApplyStandardMailEffect(mail);
            mail.Resolved = true;
            Log($"메일 처리: {mail.Subject}");
            return true;
        }

        private void ApplyStandardMailEffect(MailEvent mail)
        {
            ApplyStandardMailEffect(mail.TargetWorkId, mail.TargetTaskId, mail.DeadlineDelta,
                mail.ResourceDelta, mail.From, mail.Instruction, mail.ActivatesWork, mail.ArrivalDay);
        }

        private void ApplyStandardMailEffect(string targetWorkId, string targetTaskId,
            int deadlineDelta, int resourceDelta, string from, string instruction,
            bool activatesWork, int arrivalDay)
        {
            WorkGroup targetWork = Groups.FirstOrDefault(group => group.Id == targetWorkId);
            if (targetWork == null && !string.IsNullOrWhiteSpace(targetTaskId))
            {
                WorkTask targetTask = Tasks.FirstOrDefault(task => task.Id == targetTaskId);
                targetWork = targetTask == null ? null : Groups.FirstOrDefault(group => group.Id == targetTask.GroupId);
            }
            if (targetWork != null && deadlineDelta != 0)
            {
                targetWork.SoftDeadline = Math.Max(Day, targetWork.SoftDeadline + deadlineDelta);
                targetWork.HardDeadline = Math.Max(targetWork.SoftDeadline, targetWork.HardDeadline + deadlineDelta);
                foreach (WorkTask task in Tasks.Where(candidate => candidate.GroupId == targetWork.Id))
                {
                    task.Deadline = targetWork.HardDeadline;
                    task.Importance = ImportanceLevel.High;
                    task.Risk = RiskLevel.High;
                    AddRecord(task, from, RecordKind.Issue, instruction);
                }
            }
            if (targetWork != null && activatesWork && targetWork.AwaitingAcceptance)
            {
                int acceptanceDelay = Math.Max(0, Day - arrivalDay);
                targetWork.SoftDeadline += acceptanceDelay;
                targetWork.HardDeadline += acceptanceDelay;
                targetWork.AwaitingAcceptance = false;
                foreach (WorkTask task in Tasks.Where(candidate => candidate.GroupId == targetWork.Id))
                    task.Deadline = targetWork.HardDeadline;
                RefreshStates();
            }
            Resources = Math.Max(0, Resources + resourceDelta);
        }

        public bool IsWeeklyFieldIncidentMail(MailEvent mail) => mail != null &&
            mail.DeadlineDelta != 0 && !mail.IsCritical && !mail.IsProposal &&
            !mail.ActivatesWork && !mail.IsBossRequest && !mail.IsWeeklyFieldReport;

        public bool IsMailVisibleInInbox(MailEvent mail) => mail != null && mail.ArrivalDay <= Day;

        public int UnreadMailCount() => Mail.Count(mail => IsMailVisibleInInbox(mail) && !mail.Read);

        public bool DecideWeeklyFieldItem(string reportId, string sourceMailId, bool approve)
        {
            MailEvent report = Mail.FirstOrDefault(mail => mail.Id == reportId &&
                mail.IsWeeklyFieldReport && mail.ArrivalDay <= Day);
            if (report == null || report.WeeklyFieldItems == null) return false;
            WeeklyFieldDecisionItem item = null;
            foreach (WeeklyFieldDecisionItem candidate in report.WeeklyFieldItems)
                if (candidate.SourceMailId == sourceMailId) item = candidate;
            if (item == null || item.Decided) return false;
            item.Decided = true;
            item.Approved = approve;
            if (approve)
                ApplyStandardMailEffect(item.TargetWorkId, item.TargetTaskId, item.DeadlineDelta,
                    item.ResourceDelta, item.From, item.Instruction, false, item.ArrivalDay);
            report.Read = true;
            report.Resolved = true;
            foreach (WeeklyFieldDecisionItem candidate in report.WeeklyFieldItems)
                if (!candidate.Decided) report.Resolved = false;
            Log($"주간 현장 안건 {(approve ? "승인" : "무시")}: {item.Subject}");
            return true;
        }

        private void ExtractWeeklyFieldItemsFromMail()
        {
            List<MailEvent> sources = Mail.Where(IsWeeklyFieldIncidentMail).ToList();
            foreach (MailEvent source in sources)
            {
                pendingWeeklyFieldItems.Add(source);
                Mail.Remove(source);
            }
        }

        private void DeliverWeeklyFieldReport()
        {
            if (CurrentWeekday != Weekday.Monday) return;
            List<MailEvent> pending = pendingWeeklyFieldItems
                .Where(item => item.ArrivalDay <= Day).OrderBy(item => item.ArrivalDay).ToList();
            if (pending.Count == 0) return;

            var items = new WeeklyFieldDecisionItem[pending.Count];
            RiskLevel reportRisk = RiskLevel.Low;
            for (int i = 0; i < pending.Count; i++)
            {
                MailEvent source = pending[i];
                if ((int)source.Risk > (int)reportRisk) reportRisk = source.Risk;
                items[i] = new WeeklyFieldDecisionItem
                {
                    SourceMailId = source.Id,
                    ArrivalDay = source.ArrivalDay,
                    From = source.From,
                    Subject = source.Subject,
                    Body = source.Body,
                    Instruction = source.Instruction,
                    TargetTaskId = source.TargetTaskId,
                    TargetWorkId = source.TargetWorkId,
                    DeadlineDelta = source.DeadlineDelta,
                    ResourceDelta = source.ResourceDelta,
                    Risk = source.Risk
                };
                pendingWeeklyFieldItems.Remove(source);
            }
            Mail.Add(new MailEvent
            {
                Id = $"weekly-field-{Day}",
                ArrivalDay = Day,
                From = "현장 운영실",
                Subject = "주간현장 현황공유",
                Body = $"이번 주 일정 변경 검토 안건 {items.Length}건입니다.",
                Instruction = "각 안건을 승인 또는 무시하세요. 승인한 안건만 즉시 일정에 반영됩니다.",
                Risk = reportRisk,
                IsWeeklyFieldReport = true,
                WeeklyFieldItems = items
            });
        }

        public RandomTaskTarget[] ProposalTargets => randomTaskWords.Targets;
        public RandomTaskAction[] ProposalActions => randomTaskWords.Actions;

        public ProposalEstimate EstimateProposal(string targetId, string[] actionIds)
        {
            RandomTaskTarget target = randomTaskWords.Targets.FirstOrDefault(item => item.Id == targetId);
            if (target == null || actionIds == null || actionIds.Length < 2 || actionIds.Length > 4) return null;
            float totalWork = 0f;
            int difficultyTotal = 0;
            RiskLevel risk = RiskLevel.Low;
            foreach (string actionId in actionIds)
            {
                RandomTaskAction action = randomTaskWords.Actions.FirstOrDefault(item => item.Id == actionId);
                if (action == null) return null;
                int difficulty = Math.Max(1, Math.Min(5, target.Difficulty + action.Difficulty));
                totalWork += 2 + difficulty * .5f;
                difficultyTotal += difficulty;
                if (difficulty >= 5) risk = RiskLevel.High;
                else if (difficulty >= 3 && risk == RiskLevel.Low) risk = RiskLevel.Medium;
            }
            int cost = Math.Max(1, CeilPositive(totalWork / 4f) + difficultyTotal / 6);
            int reward = cost + CeilPositive(totalWork * .75f) + actionIds.Length;
            int softDays = 14 + Math.Max(0, actionIds.Length - 2) * 3;
            return new ProposalEstimate
            {
                TaskCount = actionIds.Length,
                TotalWork = totalWork,
                CostCredits = cost,
                RewardCredits = reward,
                SoftDeadlineDay = Day + softDays,
                HardDeadlineDay = Day + softDays + 7,
                SoftDurationDays = softDays,
                HardDurationDays = softDays + 7,
                Risk = risk
            };
        }

        public bool SubmitReadyMadeProposal(string proposalId)
        {
            ReadyMadeProposal proposal = ReadyMadeProposals.FirstOrDefault(item => item.Id == proposalId);
            if (proposal == null || Day > proposal.ExpiresDay) return false;
            if (!SubmitProposal(proposal.TargetId, proposal.ActionIds, proposal.Pitch)) return false;
            int batchId = proposal.BatchId;
            ReadyMadeProposals.RemoveAll(item => item.BatchId == batchId);
            Log($"레디메이드 제안 선택: {proposal.Id}");
            return true;
        }

        private void GenerateReadyMadeProposalBatch(DayReport report)
        {
            int batchId = Day * 1000 + nextReadyProposalId + 1;
            int count = proposalRandom.Next(3, 5);
            int expiresDay = Day + 7;
            for (int proposalIndex = 0; proposalIndex < count; proposalIndex++)
            {
                RandomTaskTarget target = randomTaskWords.Targets[proposalRandom.Next(randomTaskWords.Targets.Length)];
                int taskCount = proposalRandom.Next(2, 5);
                var actionIds = new string[taskCount];
                for (int taskIndex = 0; taskIndex < taskCount; taskIndex++)
                    actionIds[taskIndex] = randomTaskWords.Actions[proposalRandom.Next(randomTaskWords.Actions.Length)].Id;
                ProposalEstimate estimate = EstimateProposal(target.Id, actionIds);
                ReadyMadeProposals.Add(new ReadyMadeProposal
                {
                    Id = $"ready-proposal-{++nextReadyProposalId}",
                    BatchId = batchId,
                    CreatedDay = Day,
                    ExpiresDay = expiresDay,
                    TargetId = target.Id,
                    ActionIds = actionIds,
                    Pitch = (ProposalPitch)proposalRandom.Next(0, 3),
                    CostCredits = estimate.CostCredits,
                    RewardCredits = estimate.RewardCredits,
                    TotalWork = estimate.TotalWork,
                    Risk = estimate.Risk,
                    SoftDurationDays = estimate.SoftDurationDays,
                    HardDurationDays = estimate.HardDurationDays
                });
            }
            nextProposalBatchDay = Day + proposalRandom.Next(14, 22);
            Mail.Add(new MailEvent
            {
                Id = $"ready-proposal-notice-{batchId}",
                ArrivalDay = Day,
                From = "PM 기획 지원실",
                Subject = $"새 제안 후보 {count}건 도착",
                Body = $"제안서 앱에 바로 제출할 수 있는 후보 {count}건이 들어왔습니다. DAY {expiresDay}까지 하나를 선택할 수 있습니다.",
                Instruction = "제안서 앱에서 후보를 검토하세요.",
                Risk = RiskLevel.Low,
                IsProposal = true
            });
            report?.Lines.Add($"레디메이드 제안 후보 {count}건 도착 · DAY {expiresDay} 소멸");
        }

        private void RefreshReadyMadeProposals(DayReport report)
        {
            ReadyMadeProposals.RemoveAll(item => Day > item.ExpiresDay);
            if (Day >= nextProposalBatchDay) GenerateReadyMadeProposalBatch(report);
        }

        public bool SubmitProposal(string targetId, string[] actionIds, ProposalPitch pitch)
        {
            if (pitch == ProposalPitch.Decline) return false;
            int pending = Groups.Count(group => group.Id != null && group.Id.StartsWith("proposal-work-") &&
                group.AwaitingAcceptance && group.State != WorkState.Failed);
            if (pending >= 3) return false;
            RandomTaskTarget target = randomTaskWords.Targets.FirstOrDefault(item => item.Id == targetId);
            ProposalEstimate estimate = EstimateProposal(targetId, actionIds);
            if (target == null || estimate == null || Resources < estimate.CostCredits) return false;

            int id = ++nextRandomWorkId;
            var work = new WorkGroup
            {
                Id = $"proposal-work-{id}",
                Name = $"{target.Text} 개선 제안",
                SoftDeadline = 0,
                HardDeadline = 0,
                Required = false,
                PredecessorIds = Array.Empty<string>(),
                State = WorkState.Locked,
                AwaitingAcceptance = true,
                ProposalCostCredits = estimate.CostCredits,
                ProposalSoftDurationDays = estimate.SoftDurationDays,
                ProposalHardDurationDays = estimate.HardDurationDays,
                Priority = NextWorkPriority(),
                RewardCredits = estimate.RewardCredits,
                SoftPenaltyCredits = balance.RandomWorkSoftPenalty,
                HardPenaltyCredits = Math.Max(balance.RandomWorkHardPenalty, estimate.CostCredits)
            };
            string previousTaskId = null;
            for (int taskIndex = 0; taskIndex < actionIds.Length; taskIndex++)
            {
                RandomTaskAction action = randomTaskWords.Actions.FirstOrDefault(item => item.Id == actionIds[taskIndex]);
                if (action == null) return false;
                int difficulty = Math.Max(1, Math.Min(5, target.Difficulty + action.Difficulty));
                var task = new WorkTask
                {
                    Id = $"proposal-task-{id}-{taskIndex + 1}",
                    Name = $"{target.Text} {action.Text}",
                    Kind = TaskKind.SideMission,
                    RequiredRole = action.Role,
                    RequiredCompetencies = MergeCompetencies(target.RequiredCompetencies, action.RequiredCompetencies),
                    RequiredWork = 2 + difficulty * .5f,
                    Required = true,
                    PrerequisiteId = previousTaskId,
                    Deadline = work.HardDeadline,
                    State = TaskState.Locked,
                    GroupId = work.Id,
                    Risk = difficulty >= 5 ? RiskLevel.High : difficulty >= 3 ? RiskLevel.Medium : RiskLevel.Low,
                    Importance = difficulty >= 4 ? ImportanceLevel.High : ImportanceLevel.Medium,
                    Difficulty = difficulty,
                    GeneratedTargetId = target.Id,
                    GeneratedActionId = action.Id
                };
                Tasks.Add(task);
                previousTaskId = task.Id;
            }
            BossPreference preference = (BossPreference)random.Next(0, 3);
            bool approved = (int)pitch == (int)preference;
            Groups.Add(work);
            Mail.Add(new MailEvent
            {
                Id = $"proposal-result-{id}",
                ArrivalDay = Day + 1,
                From = "사장실",
                Subject = approved ? $"제안 승인: {work.Name}" : $"제안 보완 요청: {work.Name}",
                Body = approved
                    ? $"제안이 승인되었습니다. 투자비 자원 {estimate.CostCredits}가 집행되고 프로젝트가 작업 목록에 편입됩니다."
                    : $"제안의 방향은 이해했지만 {BossQuestion(preference)}에 대한 보완 답변이 필요합니다.",
                Instruction = approved ? "승인 결과입니다." : "제안서 앱에서 보완 방향을 선택해 다시 제출하세요.",
                TargetWorkId = work.Id,
                Risk = estimate.Risk,
                IsProposal = true,
                ProposalStage = approved ? ProposalStage.Accepted : ProposalStage.Question,
                BossPreference = preference
            });
            RefreshStates();
            Log($"제안서 제출: {work.Name} / 비용 {estimate.CostCredits} / 보상 {estimate.RewardCredits}");
            return true;
        }

        public bool RespondToProposal(string mailId, ProposalPitch pitch)
        {
            MailEvent mail = Mail.FirstOrDefault(item => item.Id == mailId && item.ArrivalDay <= Day);
            if (mail == null || !mail.IsProposal || mail.Resolved || mail.IsCritical) return false;
            WorkGroup work = Groups.FirstOrDefault(group => group.Id == mail.TargetWorkId);
            if (work == null || !work.AwaitingAcceptance) return false;
            mail.Read = true;

            if (pitch == ProposalPitch.Decline)
            {
                work.State = WorkState.Failed;
                foreach (WorkTask task in Tasks.Where(candidate => candidate.GroupId == work.Id))
                    task.State = TaskState.Failed;
                mail.ProposalStage = ProposalStage.Declined;
                mail.Instruction = "PM 의견: 지금 맡을 일이 아닙니다. 일정과 자원에 반영하지 않았습니다.";
                mail.Resolved = true;
                Log($"제안 보류 의견: {work.Name}");
                return true;
            }

            if (mail.ProposalStage != ProposalStage.Question) return false;
            mail.Resolved = true;
            Mail.Add(new MailEvent
            {
                Id = $"proposal-revision-result-{work.Id}-{Day}-{Mail.Count}",
                ArrivalDay = Day + 1,
                From = "사장실",
                Subject = $"보완 제안 승인: {work.Name}",
                Body = $"보완 답변을 확인했습니다. 투자비 자원 {work.ProposalCostCredits}를 집행하고 프로젝트를 편입합니다.",
                Instruction = "승인 결과입니다.",
                TargetWorkId = work.Id,
                Risk = mail.Risk,
                IsProposal = true,
                ProposalStage = ProposalStage.Accepted,
                BossPreference = mail.BossPreference
            });
            Log($"제안 보완 답변 제출: {work.Name}");
            return true;
        }

        private static string BossQuestion(BossPreference preference)
        {
            if (preference == BossPreference.Stability) return "실패 위험과 안전장치는 무엇인가";
            if (preference == BossPreference.Growth) return "이 일이 장기 성과를 얼마나 키우는가";
            return "투입 자원 대비 효과가 충분한가";
        }

        public void MarkMailRead(string mailId)
        {
            MailEvent mail = Mail.FirstOrDefault(item => item.Id == mailId && item.ArrivalDay <= Day);
            if (mail == null) return;
            mail.Read = true;
            if (mail.IsProposal && mail.ProposalStage == ProposalStage.None &&
                string.IsNullOrEmpty(mail.TargetWorkId))
                mail.Resolved = true;
        }

        public CriticalEventNode ActiveCriticalNode()
        {
            foreach (CriticalEventDefinition definition in criticalEvents)
            {
                if (definition.Id != ActiveCriticalEventId || definition.Nodes == null) continue;
                foreach (CriticalEventNode node in definition.Nodes)
                    if (node.Id == ActiveCriticalNodeId) return node;
            }
            return null;
        }

        public bool ChooseCriticalEvent(string choiceId)
        {
            if (!HasPendingCriticalChoice) return false;
            CriticalEventNode node = ActiveCriticalNode();
            CriticalEventChoice choice = null;
            if (node?.Choices != null)
                foreach (CriticalEventChoice candidate in node.Choices)
                    if (candidate.Id == choiceId)
                    {
                        choice = candidate;
                        break;
                    }
            if (choice?.Outcomes == null || choice.Outcomes.Length == 0) return false;
            int totalWeight = 0;
            foreach (CriticalEventOutcome outcome in choice.Outcomes)
                totalWeight += Math.Max(0, outcome.Weight);
            if (totalWeight <= 0) return false;

            int roll = random.Next(totalWeight);
            CriticalEventOutcome selected = choice.Outcomes[choice.Outcomes.Length - 1];
            foreach (CriticalEventOutcome outcome in choice.Outcomes)
            {
                roll -= Math.Max(0, outcome.Weight);
                if (roll < 0)
                {
                    selected = outcome;
                    break;
                }
            }

            ApplyCriticalOutcome(selected);
            MailEvent currentMail = Mail.FirstOrDefault(mail => mail.IsCritical &&
                mail.CriticalEventId == ActiveCriticalEventId &&
                mail.CriticalNodeId == ActiveCriticalNodeId && !mail.Resolved);
            if (currentMail != null)
            {
                currentMail.Read = true;
                currentMail.Resolved = true;
                currentMail.Instruction = $"선택: {choice.Text}\n결과: {selected.Text}";
            }
            Log($"중요 선택: {node.Subject} / {choice.Text} / {selected.Text}");

            if (IsLost || string.IsNullOrEmpty(selected.NextNodeId))
            {
                ActiveCriticalEventId = null;
                ActiveCriticalNodeId = null;
                ActiveCriticalNodeArrivalDay = 0;
            }
            else
            {
                ActiveCriticalNodeId = selected.NextNodeId;
                ActiveCriticalNodeArrivalDay = Day + random.Next(2, 4);
                Log($"중요 이벤트 후속 보고 예정: DAY {ActiveCriticalNodeArrivalDay}");
            }
            return true;
        }

        private void ApplyCriticalOutcome(CriticalEventOutcome outcome)
        {
            Resources = Math.Max(0, Resources + outcome.ResourceDelta);
            if (outcome.CrewIndex >= 0 && outcome.CrewIndex < Crew.Count)
            {
                CrewMember member = Crew[outcome.CrewIndex];
                member.Fatigue = Math.Max(0, Math.Min(100, member.Fatigue + outcome.FatigueDelta));
            }
            else if (outcome.FatigueDelta != 0)
            {
                foreach (CrewMember member in Crew)
                    member.Fatigue = Math.Max(0, Math.Min(100, member.Fatigue + outcome.FatigueDelta));
            }
            TaskSuccessChanceModifier = Math.Max(-50,
                Math.Min(50, TaskSuccessChanceModifier + outcome.SuccessChanceDelta));
        }

        private void TriggerCriticalEvent()
        {
            if (IsLost || HasActiveCriticalEvent) return;
            CriticalEventDefinition definition = null;
            foreach (CriticalEventDefinition candidate in criticalEvents)
            {
                if (candidate.StartDay > Day) continue;
                bool alreadyStarted = false;
                foreach (MailEvent mail in Mail)
                    if (mail.IsCritical && mail.CriticalEventId == candidate.Id)
                    {
                        alreadyStarted = true;
                        break;
                    }
                if (alreadyStarted) continue;
                if (definition == null || candidate.StartDay < definition.StartDay)
                    definition = candidate;
            }
            if (definition == null) return;
            ActiveCriticalEventId = definition.Id;
            ActiveCriticalNodeId = definition.FirstNodeId;
            ActiveCriticalNodeArrivalDay = Day;
            AddCriticalMail();
        }

        public bool ForceCriticalEvent()
        {
            if (!CanForceCriticalEvent) return false;
            CriticalEventDefinition definition = criticalEvents[0];
            foreach (CriticalEventDefinition candidate in criticalEvents)
                if (candidate.StartDay < definition.StartDay)
                    definition = candidate;
            ActiveCriticalEventId = definition.Id;
            ActiveCriticalNodeId = definition.FirstNodeId;
            ActiveCriticalNodeArrivalDay = Day;
            AddCriticalMail();
            Log($"디버그 중요 이벤트 강제 발생: {definition.Id}");
            return HasActiveCriticalEvent;
        }

        private void AddCriticalMail()
        {
            if (HasPendingCriticalChoice || ActiveCriticalNodeArrivalDay > Day) return;
            CriticalEventNode node = ActiveCriticalNode();
            if (node == null)
            {
                ActiveCriticalEventId = null;
                ActiveCriticalNodeId = null;
                ActiveCriticalNodeArrivalDay = 0;
                return;
            }
            Mail.Add(new MailEvent
            {
                Id = $"critical-{ActiveCriticalEventId}-{node.Id}-{Day}-{Mail.Count}",
                ArrivalDay = Day,
                From = node.From,
                Subject = $"[!중요!] {node.Subject}",
                Body = node.Body,
                Instruction = $"DAY {CriticalResponseDeadlineDay}까지 회신해야 합니다.",
                Risk = node.Risk,
                IsCritical = true,
                CriticalEventId = ActiveCriticalEventId,
                CriticalNodeId = node.Id
            });
        }

        private void DeliverScheduledCriticalMail()
        {
            if (!HasActiveCriticalEvent || HasPendingCriticalChoice ||
                ActiveCriticalNodeArrivalDay > Day) return;
            AddCriticalMail();
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

            member.History.Add($"DAY {Day}\n[나] {question}\n[{member.Name}] {answer}");
            Log($"{member.Name}에게 메신저 질문");
            return true;
        }

        public string BuildWorkerStatusReply(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count) return string.Empty;
            CrewMember member = Crew[crewIndex];
            string relationship = $"담당자 신뢰도는 {member.Trust}%입니다. {TrustDescription(member.Trust)} ";
            string reply;
            if (member.InjuryDays > 0)
                reply = $"지금은 부상 회복 중입니다. 복귀까지 {member.InjuryDays}일 남았습니다.";
            else if (member.RestScheduled)
                reply = "오늘은 휴식이 예정되어 있습니다. 회복 후 다시 보고드리겠습니다.";
            else if (member.Fatigue >= 80)
                reply = $"피로도가 {member.Fatigue}%라 많이 지쳤습니다. 휴식이 필요합니다.";
            else if (member.Fatigue >= 55)
                reply = $"피로도 {member.Fatigue}%입니다. 계속할 수 있지만 무리가 쌓이고 있습니다.";
            else
                reply = $"괜찮습니다. 현재 피로도는 {member.Fatigue}%이고 바로 대응할 수 있습니다.";
            return ApplyPersonalityVoice(member, relationship + reply);
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
            CrewMember member = Crew[crewIndex];
            WorkTask primary = Tasks.FirstOrDefault(task =>
                IsOngoingAssignment(task) && task.AssignedCharacter == crewIndex && !task.IsParallelAssignment);
            WorkTask parallel = Tasks.FirstOrDefault(task =>
                IsOngoingAssignment(task) && task.AssignedCharacter == crewIndex && task.IsParallelAssignment);
            if (primary == null && parallel == null)
                return ApplyPersonalityVoice(member, "현재 맡은 작업은 없습니다. 새 지시를 기다리고 있습니다.");

            string reply = primary == null
                ? "주 작업은 없습니다."
                : $"{primary.Name} 작업을 진행 중입니다. 진척도는 {primary.Completion * 100:0}%입니다.";
            if (parallel != null)
                reply += $" 병행 작업은 {parallel.Name}, 진척도 {parallel.Completion * 100:0}%입니다.";
            return ApplyPersonalityVoice(member, reply);
        }

        public static string ApplyPersonalityVoice(CrewMember member, string message)
        {
            if (member == null || string.IsNullOrEmpty(message)) return message ?? string.Empty;
            switch (member.Personality)
            {
                case "원칙적": return "절차에 따라 보고드립니다. " + message;
                case "분석적": return "현재 수치와 정황을 종합하면, " + message;
                case "다정함": return message + " 다른 대원들의 상태도 함께 살펴보겠습니다.";
                case "대담함": return message + " 필요하면 바로 움직이겠습니다.";
                default: return message;
            }
        }

        public DayReport AdvanceDay()
        {
            var report = new DayReport();
            if (IsLost) return report;

            bool weekendRest = CurrentWeekday == Weekday.Saturday || CurrentWeekday == Weekday.Sunday;
            bool regularFridayCheckup = IsRegularCheckupDay(Day);
            ApplyScheduledAssignments(report);
            ApplyPriorityPreemption(report);
            ApplyLearnedAssignments(report);
            ApplyCompetencyAssignments(report);
            var pausedCrew = new bool[Crew.Count];
            var pausedConditions = new string[Crew.Count];
            for (int crewIndex = 0; crewIndex < Crew.Count; crewIndex++)
            {
                CrewMember member = Crew[crewIndex];
                bool allOutAssignment = HasAllOutAssignment(crewIndex);
                bool medicalLeave = member.MedicalLeaveDay == Day;
                pausedCrew[crewIndex] = weekendRest && !allOutAssignment ||
                                        medicalLeave && !allOutAssignment ||
                                        member.InjuryDays > 0 || member.RestScheduled;
                pausedConditions[crewIndex] = member.Condition;
                if (regularFridayCheckup && !allOutAssignment) RecordMedicalResult(crewIndex);
                if (member.InjuryDays > 0) member.InjuryDays--;
                if (weekendRest && !allOutAssignment)
                {
                    int fatigueRecovery = ScalePerkValue(balance.WeekendFatigueRecovery,
                        PerkMultiplier(member, PerkEffect.WeekendRecovery));
                    member.Fatigue = Math.Max(0, member.Fatigue - fatigueRecovery);
                    member.Mental = Math.Min(100, member.Mental + balance.WeekendMentalRecovery);
                    if (member.InjuryDays > 0 && random.Next(100) < balance.WeekendInjuryRecoveryChance)
                        member.InjuryDays = 0;
                    report.Lines.Add($"{member.Name}: 주말 휴식 · 피로 {fatigueRecovery}/멘탈 회복");
                }
                else if (medicalLeave)
                    report.Lines.Add($"{member.Name}: 일정 외 검진으로 당일 작업 중단");
                if (!member.RestScheduled) continue;
                int restRecovery = ScalePerkValue(balance.RestRecovery,
                    PerkMultiplier(member, PerkEffect.RestRecovery));
                member.Fatigue = Math.Max(0, member.Fatigue - restRecovery);
                member.RestScheduled = false;
                report.Lines.Add($"{member.Name}: 휴식으로 피로 {restRecovery} 회복");
            }

            foreach (WorkTask task in Tasks.Where(candidate =>
                         candidate.AssignedCharacter >= 0 &&
                         candidate.State != TaskState.Complete &&
                         candidate.State != TaskState.Failed)
                         .OrderBy(candidate => candidate.IsParallelAssignment).ToList())
                ProcessTask(task, report, pausedCrew[task.AssignedCharacter],
                    pausedConditions[task.AssignedCharacter],
                    !HasAllOutAssignment(task.AssignedCharacter) &&
                    (regularFridayCheckup || Crew[task.AssignedCharacter].MedicalHalfDay == Day) ? .5f : 1f);

            ApplyPayroll(report);
            Day++;
            DeliverWeeklyFieldReport();
            DeliverMedicalResults();
            DeliverProposalResults(report);
            RefreshReadyMadeProposals(report);
            RefreshStates();
            ApplyDeadlineResults(report);
            ApplyMidpointReview(report);
            RefreshStates();
            DeliverScheduledCriticalMail();
            TriggerCriticalEvent();
            LastReport = report;
            if (report.Lines.Count == 0) report.Lines.Add("특이사항 없이 하루가 지났습니다.");
            foreach (string line in report.Lines) Log(line);
            return report;
        }

        private void DeliverProposalResults(DayReport report)
        {
            foreach (MailEvent mail in Mail.Where(item => item.IsProposal &&
                         item.ProposalStage == ProposalStage.Accepted && !item.Resolved &&
                         item.ArrivalDay <= Day).ToList())
            {
                WorkGroup work = Groups.FirstOrDefault(group => group.Id == mail.TargetWorkId);
                if (work == null || !work.AwaitingAcceptance)
                {
                    mail.Resolved = true;
                    continue;
                }
                if (Resources < work.ProposalCostCredits)
                {
                    work.State = WorkState.Failed;
                    foreach (WorkTask task in Tasks.Where(candidate => candidate.GroupId == work.Id))
                        task.State = TaskState.Failed;
                    mail.Subject = $"예산 부족으로 승인 취소: {work.Name}";
                    mail.Body = $"필요 투자비 자원 {work.ProposalCostCredits}를 확보하지 못해 프로젝트 편입이 취소되었습니다.";
                    mail.ProposalStage = ProposalStage.Declined;
                    mail.Resolved = true;
                    report.Lines.Add($"제안 승인 취소: {work.Name} / 예산 부족");
                    continue;
                }
                Resources -= work.ProposalCostCredits;
                work.SoftDeadline = Day + Math.Max(1, work.ProposalSoftDurationDays);
                work.HardDeadline = Day + Math.Max(work.ProposalSoftDurationDays + 1,
                    work.ProposalHardDurationDays);
                work.AwaitingAcceptance = false;
                foreach (WorkTask task in Tasks.Where(candidate => candidate.GroupId == work.Id))
                    task.Deadline = work.HardDeadline;
                mail.Resolved = true;
                report.Lines.Add($"제안 승인 및 편입: {work.Name} / 투자비 -{work.ProposalCostCredits}");
            }
        }

        public bool SendForMedicalCheckup(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count || IsLost) return false;
            bool regular = IsRegularCheckupDay(Day);
            CrewMember member = Crew[crewIndex];
            foreach (MedicalResult result in PendingMedicalResults)
                if (result.CrewIndex == crewIndex && result.ExamDay == Day) return false;
            if (!regular)
            {
                if (Resources < balance.UnscheduledCheckupResourceCost) return false;
                Resources -= balance.UnscheduledCheckupResourceCost;
                member.MedicalLeaveDay = Day;
            }
            else member.MedicalHalfDay = Day;
            RecordMedicalResult(crewIndex);
            member.History.Add($"DAY {Day}: {(regular ? "정기" : "일정 외")} 검진 예약");
            return true;
        }

        public int SendAllForMedicalCheckup()
        {
            int sent = 0;
            for (int i = 0; i < Crew.Count; i++)
                if (SendForMedicalCheckup(i)) sent++;
            return sent;
        }

        private void RecordMedicalResult(int crewIndex)
        {
            foreach (MedicalResult result in PendingMedicalResults)
                if (result.CrewIndex == crewIndex && result.ExamDay == Day) return;
            CrewMember member = Crew[crewIndex];
            var next = new MedicalResult[PendingMedicalResults.Length + 1];
            for (int i = 0; i < PendingMedicalResults.Length; i++) next[i] = PendingMedicalResults[i];
            next[next.Length - 1] = new MedicalResult
            {
                CrewIndex = crewIndex,
                ExamDay = Day,
                DeliveryDay = Day + 7 - ((Day - 1) % 7),
                Health = Math.Max(0, 100 - member.InjuryDays * 20),
                Fatigue = member.Fatigue,
                Mental = member.Mental,
                Trust = member.Trust
            };
            PendingMedicalResults = next;
        }

        private void DeliverMedicalResults()
        {
            int dueCount = 0;
            foreach (MedicalResult result in PendingMedicalResults)
                if (result.DeliveryDay <= Day) dueCount++;
            if (dueCount == 0) return;
            var due = new MedicalResult[dueCount];
            var remaining = new MedicalResult[PendingMedicalResults.Length - dueCount];
            int dueIndex = 0;
            int remainingIndex = 0;
            foreach (MedicalResult result in PendingMedicalResults)
                if (result.DeliveryDay <= Day) due[dueIndex++] = result;
                else remaining[remainingIndex++] = result;
            Mail.Add(new MailEvent
            {
                Id = $"medical-{Day}", ArrivalDay = Day, From = "의료지원실",
                Subject = $"주간 검진 결과 · DAY {Day}",
                Body = $"작업자 {due.Length}명의 검진 결과가 도착했습니다.",
                Instruction = "검진 파일 다운로드를 눌러 작업자 파일을 갱신하세요.",
                Risk = RiskLevel.Low, IsMedicalReport = true, MedicalResults = due
            });
            PendingMedicalResults = remaining;
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

        public int CurrentBaseSalary(CrewMember member)
        {
            if (member == null) return 0;
            return balance.BaseSalary +
                   Math.Max(0, member.Experience) / balance.ExperiencePerSalaryIncrease *
                   balance.SalaryIncrease;
        }

        public int TotalPayroll()
        {
            int total = 0;
            foreach (CrewMember member in Crew) total += CurrentBaseSalary(member);
            return total;
        }

        private void ApplyPayroll(DayReport report)
        {
            if (Day % balance.PayrollIntervalDays != 0) return;
            int payroll = TotalPayroll();
            Resources = Math.Max(0, Resources - payroll);
            report.Lines.Add($"급여 지급: 월 기본급 합계 -{payroll}자원");
            foreach (CrewMember member in Crew)
                member.History.Add($"DAY {Day}: 기본급 {CurrentBaseSalary(member)} 지급");
        }

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

        public TaskScheduleEstimate[] BuildPrioritySchedule()
        {
            var estimates = new TaskScheduleEstimate[Tasks.Count];
            int estimateCount = 0;
            var workerFreeDay = new int[Crew.Count];
            for (int worker = 0; worker < workerFreeDay.Length; worker++)
                workerFreeDay[worker] = Day;

            foreach (WorkTask task in Tasks.Where(candidate => candidate.State == TaskState.Complete))
                estimates[estimateCount++] = new TaskScheduleEstimate
                {
                    TaskId = task.Id,
                    WorkerIndex = task.LastWorker,
                    StartDay = task.StartedDay,
                    CompletionDay = task.CompletedDay,
                    StartReason = "완료된 실제 일정"
                };

            foreach (WorkTask task in Tasks.Where(candidate => candidate.AssignedCharacter >= 0 &&
                         candidate.State != TaskState.Complete && candidate.State != TaskState.Failed))
            {
                int worker = task.AssignedCharacter;
                float output = ExpectedDailyOutput(task, worker);
                int duration = CeilPositive(task.RemainingWork / output);
                int completion = Day + Math.Max(0, duration - 1);
                estimates[estimateCount++] = new TaskScheduleEstimate
                {
                    TaskId = task.Id,
                    WorkerIndex = worker,
                    ExpectedDailyOutput = output,
                    EstimatedWork = task.RemainingWork,
                    DurationDays = duration,
                    StartDay = Day,
                    CompletionDay = completion,
                    StartReason = "현재 담당자의 진행 일정"
                };
                if (!task.IsParallelAssignment)
                    workerFreeDay[worker] = Math.Max(workerFreeDay[worker], completion + 1);
            }

            List<WorkTask> pending = Tasks.Where(task => task.AssignedCharacter < 0 &&
                    task.State != TaskState.Complete && task.State != TaskState.Failed &&
                    IsWorkVisible(ParentWork(task)))
                .OrderBy(task => ParentWork(task)?.Priority ?? int.MaxValue)
                .ThenBy(task => task.Id).ToList();
            int safety = pending.Count + 1;
            while (pending.Count > 0 && safety-- > 0)
            {
                bool plannedAny = false;
                foreach (WorkTask task in pending.ToList())
                {
                    int dependencyDay;
                    if (!TryPriorityDependencyDay(task, estimates, estimateCount, out dependencyDay)) continue;
                    int worker = PriorityScheduleWorker(task, workerFreeDay);
                    float output = worker >= 0 ? ExpectedDailyOutput(task, worker) : 1f;
                    int start = Math.Max(Day, dependencyDay);
                    if (task.ScheduledDay > 0) start = Math.Max(start, task.ScheduledDay);
                    if (worker >= 0) start = Math.Max(start, workerFreeDay[worker]);
                    int duration = CeilPositive(task.RemainingWork / output);
                    int completion = start + Math.Max(0, duration - 1);
                    estimates[estimateCount++] = new TaskScheduleEstimate
                    {
                        TaskId = task.Id,
                        WorkerIndex = worker,
                        ExpectedDailyOutput = output,
                        EstimatedWork = task.RemainingWork,
                        DurationDays = duration,
                        StartDay = start,
                        CompletionDay = completion,
                        StartReason = "작업 우선순위와 인력 가용일 기준 예상"
                    };
                    if (worker >= 0) workerFreeDay[worker] = completion + 1;
                    pending.Remove(task);
                    plannedAny = true;
                }
                if (!plannedAny) break;
            }

            foreach (WorkTask task in pending)
            {
                TaskScheduleEstimate fallback = EstimatePreviewSchedule(task.Id);
                if (fallback == null) continue;
                fallback.TaskId = task.Id;
                estimates[estimateCount++] = fallback;
            }
            return estimates;
        }

        private bool TryPriorityDependencyDay(
            WorkTask task, TaskScheduleEstimate[] estimates, int estimateCount, out int dependencyDay)
        {
            dependencyDay = Day;
            if (!string.IsNullOrEmpty(task.PrerequisiteId))
            {
                WorkTask blocker = Tasks.FirstOrDefault(item => item.Id == task.PrerequisiteId);
                if (blocker != null && blocker.State != TaskState.Complete)
                {
                    TaskScheduleEstimate estimate = FindPriorityEstimate(estimates, estimateCount, blocker.Id);
                    if (estimate == null) return false;
                    dependencyDay = Math.Max(dependencyDay, estimate.CompletionDay + 1);
                }
            }
            WorkGroup group = ParentWork(task);
            if (group?.PredecessorIds == null) return true;
            foreach (string predecessorId in group.PredecessorIds)
            {
                WorkGroup predecessor = Groups.FirstOrDefault(item => item.Id == predecessorId);
                if (predecessor == null || predecessor.State == WorkState.Complete) continue;
                foreach (WorkTask blocker in Tasks.Where(item => item.GroupId == predecessorId &&
                             item.Required && item.State != TaskState.Complete))
                {
                    TaskScheduleEstimate estimate = FindPriorityEstimate(estimates, estimateCount, blocker.Id);
                    if (estimate == null) return false;
                    dependencyDay = Math.Max(dependencyDay, estimate.CompletionDay + 1);
                }
            }
            return true;
        }

        private static TaskScheduleEstimate FindPriorityEstimate(
            TaskScheduleEstimate[] estimates, int count, string taskId)
        {
            for (int index = 0; index < count; index++)
                if (estimates[index]?.TaskId == taskId) return estimates[index];
            return null;
        }

        private int PriorityScheduleWorker(WorkTask task, int[] workerFreeDay)
        {
            if (task.ScheduledWorker >= 0 && task.ScheduledWorker < Crew.Count)
                return task.ScheduledWorker;
            AssignmentRule rule = AssignmentRules.FirstOrDefault(candidate => candidate.Matches(task));
            if (rule != null)
            {
                int learned = Crew.FindIndex(member => member.Name == rule.CrewName);
                if (learned >= 0 && learned < Crew.Count && Crew[learned].Available) return learned;
            }
            if (!CompetencyAutoAssignment) return -1;
            int earliest = int.MaxValue;
            for (int worker = 0; worker < Crew.Count; worker++)
                if (Crew[worker].Available) earliest = Math.Min(earliest, workerFreeDay[worker]);
            int best = -1;
            float bestMultiplier = -1f;
            int bestFatigue = int.MaxValue;
            for (int worker = 0; worker < Crew.Count; worker++)
            {
                if (!Crew[worker].Available || workerFreeDay[worker] != earliest) continue;
                float multiplier = CompetencyOutputMultiplier(Crew[worker], task);
                if (multiplier < bestMultiplier ||
                    Math.Abs(multiplier - bestMultiplier) < .001f && Crew[worker].Fatigue >= bestFatigue)
                    continue;
                best = worker;
                bestMultiplier = multiplier;
                bestFatigue = Crew[worker].Fatigue;
            }
            return best;
        }

        private float ExpectedDailyOutput(WorkTask task, int crewIndex)
        {
            int lowChance;
            int highChance;
            OutputChances(Crew[crewIndex].Fatigue, out lowChance, out highChance);
            ApplySuccessChanceModifier(ref lowChance, highChance);
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

        private void ApplySuccessChanceModifier(ref int lowChance, int highChance)
        {
            lowChance = Math.Max(0, Math.Min(100 - highChance,
                lowChance - TaskSuccessChanceModifier));
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
            PendingWeeklyFieldItems = pendingWeeklyFieldItems.ToArray(),
            Log = SystemLog.ToArray(),
            AssignmentRules = AssignmentRules.ToArray(),
            DiscoveredTaskWordIds = DiscoveredTaskWordIds.ToArray(),
            DiscoveredCrewTraitIds = DiscoveredCrewTraitIds.ToArray(),
            DiscoveredCrewTraitSources = DiscoveredCrewTraitSources.ToArray(),
            MidpointReviewIssued = MidpointReviewIssued,
            CompetencyAutoAssignment = CompetencyAutoAssignment,
            HasAutoAssignmentPreference = true,
            Crunch = false,
            ReadyMadeProposals = ReadyMadeProposals.ToArray(),
            NextProposalBatchDay = nextProposalBatchDay,
            NextReadyProposalId = nextReadyProposalId,
            PendingMedicalResults = PendingMedicalResults,
            ActiveCriticalEventId = ActiveCriticalEventId,
            ActiveCriticalNodeId = ActiveCriticalNodeId,
            ActiveCriticalNodeArrivalDay = ActiveCriticalNodeArrivalDay,
            TaskSuccessChanceModifier = TaskSuccessChanceModifier
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
            pendingWeeklyFieldItems.Clear();
            if (snapshot.PendingWeeklyFieldItems != null)
                pendingWeeklyFieldItems.AddRange(snapshot.PendingWeeklyFieldItems);
            ExtractWeeklyFieldItemsFromMail();
            AssignmentRules.Clear();
            if (snapshot.AssignmentRules != null) AssignmentRules.AddRange(snapshot.AssignmentRules);
            DiscoveredTaskWordIds.Clear();
            if (snapshot.DiscoveredTaskWordIds != null)
            {
                foreach (string wordId in snapshot.DiscoveredTaskWordIds)
                    UnlockTaskWord(wordId, null);
            }
            RestoreCrewTraitDiscoveries(snapshot.DiscoveredCrewTraitIds,
                snapshot.DiscoveredCrewTraitSources);
            DiscoverCurrentCrewTraits();
            MidpointReviewIssued = snapshot.MidpointReviewIssued;
            CompetencyAutoAssignment = snapshot.HasAutoAssignmentPreference
                ? snapshot.CompetencyAutoAssignment
                : true;
            ReadyMadeProposals.Clear();
            if (snapshot.ReadyMadeProposals != null)
                ReadyMadeProposals.AddRange(snapshot.ReadyMadeProposals);
            nextProposalBatchDay = snapshot.NextProposalBatchDay;
            nextReadyProposalId = snapshot.NextReadyProposalId;
            PendingMedicalResults = snapshot.PendingMedicalResults ?? new MedicalResult[0];
            ActiveCriticalEventId = snapshot.ActiveCriticalEventId;
            ActiveCriticalNodeId = snapshot.ActiveCriticalNodeId;
            ActiveCriticalNodeArrivalDay = snapshot.ActiveCriticalNodeArrivalDay;
            TaskSuccessChanceModifier = snapshot.TaskSuccessChanceModifier;
            SystemLog.Clear();
            if (snapshot.Log != null) SystemLog.AddRange(snapshot.Log);
            MigrateLegacyGeneratedSideMissions();
            NormalizeLoadedData();
            if (nextProposalBatchDay <= 0) GenerateReadyMadeProposalBatch(null);
            RefreshStates();
            if (HasActiveCriticalEvent && ActiveCriticalNodeArrivalDay <= 0)
                ActiveCriticalNodeArrivalDay = Day;
            DeliverScheduledCriticalMail();
            DeliverWeeklyFieldReport();
            if (!HasActiveCriticalEvent) TriggerCriticalEvent();
            return true;
        }

        private void ProcessTask(WorkTask task, DayReport report, bool pausedByCondition,
            string pausedCondition, float workdayMultiplier = 1f)
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
            TryTriggerIncidentCheckpoint(task, member, 0, report);
            bool matched = member.Specialty == task.RequiredRole;
            float baseOutput = member.DailyOutput > 0f ? member.DailyOutput : 1f;
            baseOutput *= task.IsParallelAssignment
                ? balance.ParallelProgressDays
                : balance.PrimaryProgressDays;
            baseOutput *= CompetencyOutputMultiplier(member, task);
            int lowOutputChance;
            int highOutputChance;
            OutputChances(member.Fatigue, out lowOutputChance, out highOutputChance);
            ApplySuccessChanceModifier(ref lowOutputChance, highOutputChance);
            int outputRoll = random.Next(100);
            TaskOutcome outcome = outputRoll < lowOutputChance
                ? TaskOutcome.Failure
                : outputRoll >= 100 - highOutputChance
                    ? TaskOutcome.GreatSuccess
                    : TaskOutcome.Success;
            float outputMultiplier = outcome == TaskOutcome.Failure
                ? balance.LowOutputMultiplier
                : outcome == TaskOutcome.GreatSuccess ? balance.HighOutputMultiplier : 1f;
            float progress = baseOutput * outputMultiplier * workdayMultiplier;
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
            if (workdayMultiplier < 1f) fatigue /= 2;
            fatigue = ScalePerkValue(fatigue,
                PerkMultiplier(member, PerkEffect.Fatigue));

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
            if (task.Completion + .001f >= .5f)
                TryTriggerIncidentCheckpoint(task, member, 50, report);
            if (task.State == TaskState.Complete)
                TryTriggerIncidentCheckpoint(task, member, 100, report);
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

        private void DiscoverCurrentCrewTraits()
        {
            for (int crewIndex = 0; crewIndex < Crew.Count; crewIndex++)
                DiscoverCrewTraits(crewIndex);
        }

        private void DiscoverCrewTraits(int crewIndex)
        {
            if (crewIndex < 0 || crewIndex >= Crew.Count) return;
            CrewMember member = Crew[crewIndex];
            DiscoverCrewTrait("personality", member.Personality, member.Name);
            if (member.Perks == null) return;
            foreach (string perk in member.Perks)
                DiscoverCrewTrait("perk", perk, member.Name);
        }

        private void DiscoverCrewTrait(string kind, string traitName, string crewName)
        {
            if (string.IsNullOrWhiteSpace(traitName)) return;
            string discoveryId = kind + ":" + traitName;
            for (int i = 0; i < DiscoveredCrewTraitIds.Count; i++)
                if (DiscoveredCrewTraitIds[i] == discoveryId) return;

            DiscoveredCrewTraitIds.Add(discoveryId);
            DiscoveredCrewTraitSources.Add(crewName ?? string.Empty);
            AddCrewTraitCodexEntry(discoveryId, crewName);
        }

        private void AddCrewTraitCodexEntry(string discoveryId, string crewName)
        {
            if (string.IsNullOrEmpty(discoveryId)) return;
            bool personality = discoveryId.Length > 12 && discoveryId[3] == 's';
            int nameStart = personality ? 12 : 5;
            if (discoveryId.Length <= nameStart) return;
            string traitName = discoveryId.Substring(nameStart);
            string entryId = "crew-trait-" + discoveryId;
            for (int i = 0; i < Codex.Count; i++)
                if (Codex[i].Id == entryId) return;

            string source = string.IsNullOrEmpty(crewName) ? "기록 미상 작업자" : crewName;
            string description = personality
                ? $"{source} 작업자에게서 처음 확인된 성격 특성입니다. 성격은 작업자 상세 정보에 표시되며, 같은 상황을 설명하는 메신저 답변의 말투와 표현 방식에 반영됩니다. 재생 시술 뒤 성격이 바뀌더라도 이미 확인한 특성 기록은 도감에 남습니다. 현재 별도의 수치 보정이나 성공 확률 효과는 명세되지 않았습니다."
                : $"{source} 작업자에게서 처음 확인된 퍽입니다. 퍽은 작업자 상세 정보와 재생 시술의 퍽 인계 대상에 포함됩니다. 이 도감은 발견 사실과 이름을 보존하며, 퍽의 구체적인 작업 보정 수치는 외부 게임 데이터에 명시된 효과만 적용합니다. 작업자가 퍽을 잃거나 재생되더라도 한번 확인한 기록은 도감에 남습니다.";
            Codex.AddRange(new[]
            {
                new CodexEntry
                {
                    Id = entryId,
                    Category = personality ? "작업자 특성 · 성격" : "작업자 특성 · 퍽",
                    Name = traitName,
                    Description = description
                }
            });
        }

        private void RestoreCrewTraitDiscoveries(string[] ids, string[] sources)
        {
            if (ids == null) return;
            for (int i = 0; i < ids.Length; i++)
            {
                string source = sources != null && i < sources.Length ? sources[i] : string.Empty;
                bool known = false;
                for (int existing = 0; existing < DiscoveredCrewTraitIds.Count; existing++)
                    if (DiscoveredCrewTraitIds[existing] == ids[i])
                    {
                        known = true;
                        break;
                    }
                if (known) continue;
                DiscoveredCrewTraitIds.Add(ids[i]);
                DiscoveredCrewTraitSources.Add(source);
                AddCrewTraitCodexEntry(ids[i], source);
            }
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

        public IncidentChancePreview BuildIncidentChance(CrewMember member, int checkpointPercent)
        {
            float multiplier = 1f;
            string formula = $"기본 {balance.BaseSideMissionChance}%";
            if (member != null && member.Perks != null)
            {
                foreach (string perkName in member.Perks)
                {
                    PerkDefinition definition = FindPerkDefinition(perkName);
                    if (definition == null) continue;
                    float factor = checkpointPercent == 0
                        ? definition.IncidentStartMultiplier
                        : checkpointPercent == 50
                            ? definition.IncidentHalfMultiplier
                            : definition.IncidentCompleteMultiplier;
                    if (Math.Abs(factor - 1f) < .001f) continue;
                    multiplier *= factor;
                    formula += $" × {definition.Name} {factor:0.##}";
                }
            }
            int chanceBasisPoints = (int)(balance.BaseSideMissionChance * 100f * multiplier + .5f);
            chanceBasisPoints = Math.Max(0, Math.Min(10000, chanceBasisPoints));
            return new IncidentChancePreview
            {
                CheckpointPercent = checkpointPercent,
                ChanceBasisPoints = chanceBasisPoints,
                TotalMultiplier = multiplier,
                Formula = formula
            };
        }

        public string PerkDescription(string perkName)
        {
            PerkDefinition definition = FindPerkDefinition(perkName);
            return definition?.Description ?? "효과 정의 없음";
        }

        private void TryTriggerIncidentCheckpoint(
            WorkTask sourceTask, CrewMember member, int checkpointPercent, DayReport report)
        {
            int bit = checkpointPercent == 0 ? 1 : checkpointPercent == 50 ? 2 : 4;
            if ((sourceTask.IncidentCheckpointMask & bit) != 0) return;
            sourceTask.IncidentCheckpointMask |= bit;
            if (AvailableRandomWorkSlots() <= 0) return;

            IncidentChancePreview chance = BuildIncidentChance(member, checkpointPercent);
            if (chance.ChanceBasisPoints <= 0) return;
            if (chance.ChanceBasisPoints < 10000 && random.Next(10000) >= chance.ChanceBasisPoints) return;
            string cause = $"{member.Name} 담당자의 {sourceTask.Name} 작업 {checkpointPercent}% 시점에서 발생 · " +
                           $"현재 배율 ×{chance.TotalMultiplier:0.##} ({chance.Formula}) · " +
                           $"최종 확률 {chance.ChanceBasisPoints / 100f:0.##}%";
            AddRecord(sourceTask, member.Name, RecordKind.Issue, "돌발상황 발생 · " + cause);
            CreateRandomWork(report, cause);
        }

        private int AvailableRandomWorkSlots()
        {
            int generatedWorkCount = Groups.Count(group => group.Id != null &&
                group.Id.StartsWith("random-work-") && group.State != WorkState.Complete &&
                group.State != WorkState.Failed);
            return Math.Max(0, balance.RandomWorkLimit - generatedWorkCount);
        }

        private PerkDefinition FindPerkDefinition(string perkName)
        {
            foreach (PerkDefinition definition in perkDefinitions)
                if (definition != null && definition.Name == perkName) return definition;
            return null;
        }

        private float PerkMultiplier(CrewMember member, PerkEffect effect)
        {
            float multiplier = 1f;
            if (member?.Perks == null) return multiplier;
            foreach (string perkName in member.Perks)
            {
                PerkDefinition definition = FindPerkDefinition(perkName);
                if (definition == null) continue;
                multiplier *= effect == PerkEffect.Fatigue
                    ? definition.FatigueCostMultiplier
                    : effect == PerkEffect.RestRecovery
                        ? definition.RestRecoveryMultiplier
                        : definition.WeekendRecoveryMultiplier;
            }
            return multiplier;
        }

        private static int ScalePerkValue(int value, float multiplier) =>
            Math.Max(0, (int)(value * multiplier + .5f));

        private void CreateRandomWork(DayReport report, string cause)
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
                Priority = NextWorkPriority(),
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
                ArrivalDay = Day,
                From = "사장실",
                Subject = $"돌발임무 결정: {work.Name}",
                Body = $"사장실에서 {taskCount}개 실행 단계로 구성된 돌발임무의 즉시 결정을 요청했습니다. " +
                       $"성공 보상은 자원 {reward}입니다.\n\n발생 원인: {cause}",
                Instruction = "즉시 결정 팝업에서 승인 또는 거절을 선택하세요.",
                TargetTaskId = firstTask.Id,
                TargetWorkId = work.Id,
                Risk = missionRisk,
                ActivatesWork = true,
                IsProposal = false,
                IsBossRequest = true,
                ProposalStage = ProposalStage.None
            });
            RefreshStates();
            report.Lines.Add($"돌발임무 결정 요청: {work.Name} ({taskCount}개 실행 단계) · {cause}");
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
                         candidate.State == TaskState.Available)
                         .OrderBy(candidate => ParentWork(candidate)?.Priority ?? int.MaxValue)
                         .ThenBy(candidate => candidate.Id).ToList())
            {
                AssignmentRule rule = AssignmentRules.FirstOrDefault(candidate => candidate.Matches(task));
                if (rule == null || !AutomaticDependenciesComplete(task)) continue;
                int worker = Crew.FindIndex(member => member.Name == rule.CrewName);
                if (!WorkerCanTakePrimary(worker)) continue;
                if (!AssignPrimary(task, worker)) continue;
                report.Lines.Add($"자동 배정: {Crew[worker].Name} → {task.Name}");
            }
        }

        private void ApplyCompetencyAssignments(DayReport report)
        {
            if (!CompetencyAutoAssignment) return;
            foreach (WorkTask task in Tasks.OrderBy(candidate =>
                         ParentWork(candidate)?.Priority ?? int.MaxValue).ThenBy(candidate => candidate.Id))
            {
                if (task.AssignedCharacter >= 0 || task.ScheduledDay > 0 ||
                    task.State != TaskState.Available || !AutomaticDependenciesComplete(task)) continue;
                int bestWorker = -1;
                float bestMultiplier = -1f;
                int bestFatigue = int.MaxValue;
                for (int worker = 0; worker < Crew.Count; worker++)
                {
                    CrewMember member = Crew[worker];
                    if (!WorkerCanTakePrimary(worker)) continue;
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

        private void ApplyPriorityPreemption(DayReport report)
        {
            foreach (WorkGroup work in Groups.Where(group => IsWorkVisible(group) &&
                         group.Urgent &&
                         group.State != WorkState.Complete && group.State != WorkState.Failed)
                         .OrderBy(group => group.Priority).ToList())
            {
                List<WorkTask> eligible = Tasks.Where(task => task.GroupId == work.Id &&
                    task.AssignedCharacter < 0 && task.State == TaskState.Available &&
                    AutomaticDependenciesComplete(task)).ToList();
                foreach (WorkTask target in eligible)
                {
                    int bestWorker = -1;
                    float bestMultiplier = -1f;
                    for (int worker = 0; worker < Crew.Count; worker++)
                    {
                        if (!Crew[worker].Available) continue;
                        float multiplier = CompetencyOutputMultiplier(Crew[worker], target);
                        if (multiplier <= bestMultiplier) continue;
                        WorkTask current = Tasks.FirstOrDefault(task => task.AssignedCharacter == worker &&
                            !task.IsParallelAssignment && IsOngoingAssignment(task));
                        if (current != null && (ParentWork(current)?.Priority ?? int.MaxValue) <= work.Priority)
                            continue;
                        bestWorker = worker;
                        bestMultiplier = multiplier;
                    }
                    if (bestWorker < 0 || WorkerCanTakePrimary(bestWorker)) continue;
                    WorkTask interrupted = Tasks.FirstOrDefault(task => task.AssignedCharacter == bestWorker &&
                        !task.IsParallelAssignment && IsOngoingAssignment(task));
                    if (interrupted == null) break;
                    int interruptedWorker = interrupted.AssignedCharacter;
                    if (!AssignPrimary(interrupted, -1)) break;
                    report.Lines.Add($"우선순위 선점: {Crew[interruptedWorker].Name}의 {interrupted.Name} 중단 → {work.Name}");
                    if (AssignPrimary(target, interruptedWorker))
                        report.Lines.Add($"긴급 자동 배정: {Crew[interruptedWorker].Name} → {target.Name}");
                }
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

        private bool WorkerCanTakePrimary(int worker)
        {
            if (worker < 0 || worker >= Crew.Count || !Crew[worker].Available) return false;
            return !Tasks.Any(candidate => candidate.AssignedCharacter == worker &&
                                           !candidate.IsParallelAssignment &&
                                           IsOngoingAssignment(candidate));
        }

        private bool HasAllOutAssignment(int worker)
        {
            return Tasks.Any(task => task.AssignedCharacter == worker && IsOngoingAssignment(task) &&
                ParentWork(task)?.AllOut == true);
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
            if (Groups.All(group => group.Priority == 0))
                for (int i = 0; i < Groups.Count; i++) Groups[i].Priority = i;
            NormalizeWorkPriorities();
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
                if (task.IncidentCheckpointMask == 0)
                {
                    if (task.StartedDay > 0 || task.Progress > 0f) task.IncidentCheckpointMask |= 1;
                    if (task.Completion + .001f >= .5f) task.IncidentCheckpointMask |= 2;
                    if (task.State == TaskState.Complete) task.IncidentCheckpointMask |= 4;
                }
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
                if (i < crewPortraitAddresses.Length && string.IsNullOrEmpty(member.PortraitAddress))
                    member.PortraitAddress = crewPortraitAddresses[i];
                if (i < crewPersonalities.Length && string.IsNullOrEmpty(member.Personality))
                    member.Personality = crewPersonalities[i];
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
                    (group.Id.StartsWith("random-work-") || group.Id.StartsWith("proposal-work-")))
                .Select(group => group.Id.StartsWith("random-work-")
                    ? int.TryParse(group.Id.Substring(12), out int randomValue) ? randomValue : 0
                    : int.TryParse(group.Id.Substring(14), out int proposalValue) ? proposalValue : 0)
                .DefaultIfEmpty(0).Max();
            nextRandomWorkId = highestRandomWorkId;
        }

        private void NormalizeWorkPriorities()
        {
            List<WorkGroup> ordered = Groups.OrderBy(group => group.Priority)
                .ThenBy(group => group.Id).ToList();
            for (int i = 0; i < ordered.Count; i++) ordered[i].Priority = i;
        }

        private int NextWorkPriority()
        {
            int highest = -1;
            foreach (WorkGroup group in Groups)
                if (group.Priority > highest) highest = group.Priority;
            return highest + 1;
        }

        private void MigrateLegacyGeneratedSideMissions()
        {
            foreach (WorkGroup group in Groups)
            {
                if (group.Id == null || !group.Id.StartsWith("random-work-") ||
                    group.State == WorkState.Complete || group.State == WorkState.Failed) continue;
                MailEvent offer = Mail.FirstOrDefault(mail => mail.TargetWorkId == group.Id);
                if (offer != null && (offer.IsProposal || offer.IsBossRequest)) continue;
                if (offer != null)
                {
                    bool hasStarted = Tasks.Any(task => task.GroupId == group.Id &&
                        (task.StartedDay > 0 || task.Progress > 0f));
                    if (!offer.Resolved && !hasStarted) group.AwaitingAcceptance = true;
                    offer.ActivatesWork = true;
                    offer.IsBossRequest = true;
                    offer.Instruction =
                        $"맡으려면 수락하세요. 실패 시 자원 {group.HardPenaltyCredits}가 차감됩니다.";
                }
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
