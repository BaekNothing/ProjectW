using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectW.Bootstrap;
using ProjectW.Contracts;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class MilestoneSimulationTests
    {
        private const string PlanningWorkId = "test-planning-work";
        private const string FollowupWorkId = "test-followup-work";
        private const string RootTaskId = "test-root";
        private const string DependentTaskAId = "test-dependent-a";
        private const string DependentTaskBId = "test-dependent-b";
        private const string OptionalTaskId = "test-optional";
        private const string FollowupTaskId = "test-followup";

        [Test]
        public void StakeholderMailCreatesCatalogTasksWithoutAutomaticStaffing()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            var game = new MilestoneSimulation(data, 1);

            Assert.That(game.OpenStakeholderRequest("mail-stakeholder-landing"), Is.True);
            List<WorkTask> tasks = game.Tasks.FindAll(task => task.StakeholderRequestId == "landing-brief");

            Assert.That(tasks.Count, Is.EqualTo(6));
            Assert.That(tasks.ConvertAll(task => task.Name),
                Is.EquivalentTo(new[] { "회원과 권한", "상품과 주문", "결제와 환불",
                    "알림과 소통", "조회와 보고", "관리자와 운영" }));
            Assert.That(tasks.FindAll(task => task.ScopeIncluded).Count, Is.EqualTo(3));
            Assert.That(game.StakeholderPlanTotalWork("landing-brief"), Is.EqualTo(12f));
            game.AdvanceDay();
            Assert.That(tasks, Has.All.Matches<WorkTask>(task => task.AssignedCharacter < 0));
        }

        [Test]
        public void StakeholderRequestStaysSeparateUntilPlanningStarts()
        {
            var game = new MilestoneSimulation(TaskSystemDataLoader.Load(), 1);

            Assert.That(game.OpenStakeholderRequest("mail-stakeholder-landing"), Is.True);
            StakeholderRequestState request = game.StakeholderState("landing-brief");
            WorkGroup work = game.Groups.Find(group => group.Id == request.WorkId);

            Assert.That(request.PlanningStarted, Is.False);
            Assert.That(game.IsWorkVisible(work), Is.False);
            Assert.That(game.AssignStakeholderTask(
                "stakeholder-landing-brief-order-refund", 1), Is.False);

            Assert.That(game.StartStakeholderPlanning(request.Id), Is.True);
            Assert.That(request.PlanningStarted, Is.True);
            Assert.That(game.IsWorkVisible(work), Is.False,
                "Planning alone must not register the request in the work queue.");
        }

        [Test]
        public void StakeholderAssignmentAllowsMismatchAndMarksForcedParallelWork()
        {
            var game = new MilestoneSimulation(TaskSystemDataLoader.Load(), 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            game.StartStakeholderPlanning("landing-brief");
            WorkTask analysis = game.Tasks.Find(task => task.Id == "stakeholder-landing-brief-order-refund");
            WorkTask operations = game.Tasks.Find(task => task.Id == "stakeholder-landing-brief-payment-refund");

            Assert.That(game.AssignStakeholderTask(analysis.Id, 0), Is.True,
                "A mismatched worker remains assignable at an efficiency and dissatisfaction cost.");
            Assert.That(game.AssignmentDissatisfactionGain(analysis.Id, 0, false), Is.GreaterThan(0));
            Assert.That(game.AssignStakeholderTask(operations.Id, 0), Is.True);
            Assert.That(analysis.PlannedCharacter, Is.EqualTo(0));
            Assert.That(operations.PlannedCharacter, Is.EqualTo(0));
            Assert.That(operations.AssignedCharacter, Is.EqualTo(-1),
                "A planned worker must not consume the live queue before approval.");
            Assert.That(game.AssignmentDissatisfactionGain(operations.Id, 0, true),
                Is.GreaterThanOrEqualTo(game.AssignmentDissatisfactionGain(operations.Id, 0, false) + 5));
        }

        [Test]
        public void AcceptedStakeholderPlanEntersQueueOnlyAfterReview()
        {
            var game = new MilestoneSimulation(TaskSystemDataLoader.Load(), 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            game.StartStakeholderPlanning("landing-brief");
            FillStakeholderPlan(game, true);
            StakeholderRequestState request = game.StakeholderState("landing-brief");
            WorkGroup work = game.Groups.Find(group => group.Id == request.WorkId);
            List<WorkTask> tasks = game.Tasks.FindAll(task =>
                task.StakeholderRequestId == request.Id);

            Assert.That(game.StakeholderPlanProjectedFit(request.Id), Is.EqualTo(100));
            Assert.That(game.StakeholderPlanProjectedCompletionDay(request.Id),
                Is.LessThanOrEqualTo(work.SoftDeadline));
            Assert.That(tasks.FindAll(task => task.ScopeIncluded), Has.All.Matches<WorkTask>(task =>
                task.PlannedCharacter >= 0 && task.AssignedCharacter < 0));
            Assert.That(game.IsWorkVisible(work), Is.False);

            Assert.That(game.SubmitStakeholderPlan(request.Id), Is.True);

            Assert.That(request.PlanConfirmed, Is.False);
            Assert.That(request.PendingPlanReviewDay, Is.InRange(game.Day + 1, game.Day + 3));
            Assert.That(game.NextEventDay, Is.EqualTo(request.PendingPlanReviewDay));
            Assert.That(game.NextEventSummary, Is.EqualTo("계획 검토"));
            Assert.That(game.IsWorkVisible(work), Is.False);
            Assert.That(tasks.FindAll(task => task.ScopeIncluded), Has.All.Matches<WorkTask>(task =>
                task.PlannedCharacter >= 0 && task.AssignedCharacter < 0));
            Assert.That(game.AssignStakeholderTask(tasks[0].Id, 3), Is.False);
            Assert.That(game.RequestStakeholderClarification(request.Id, "call"), Is.False);
            Assert.That(game.SubmitStakeholderPlan(request.Id), Is.False);

            AdvanceUntilStakeholderPlanReviewed(game, request);

            Assert.That(request.PlanConfirmed, Is.True);
            Assert.That(game.IsWorkVisible(work), Is.True);
            Assert.That(tasks.FindAll(task => task.ScopeIncluded), Has.All.Matches<WorkTask>(task =>
                task.PlannedCharacter < 0 && task.AssignedCharacter >= 0));
            Assert.That(game.Mail, Has.Some.Matches<MailEvent>(mail =>
                mail.Id == "stakeholder-plan-review-landing-brief-1" &&
                mail.Subject.StartsWith("계획 승인")));
        }

        [Test]
        public void PlanBeyondSoftDeadlineIsRejectedEvenWhenContentMatches()
        {
            var game = new MilestoneSimulation(TaskSystemDataLoader.Load(), 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            game.StartStakeholderPlanning("landing-brief");
            FillStakeholderPlan(game, true);
            game.Crew[0].DailyOutput = .1f;
            game.Crew[1].DailyOutput = .1f;
            game.Crew[2].DailyOutput = .1f;
            StakeholderRequestState request = game.StakeholderState("landing-brief");
            WorkGroup work = game.Groups.Find(group => group.Id == request.WorkId);

            Assert.That(game.StakeholderPlanProjectedCompletionDay(request.Id),
                Is.GreaterThan(work.SoftDeadline));
            Assert.That(game.SubmitStakeholderPlan(request.Id), Is.True);

            Assert.That(request.PlanRejectionCount, Is.EqualTo(0));
            Assert.That(request.PendingPlanReviewDay, Is.GreaterThan(game.Day));
            AdvanceUntilStakeholderPlanReviewed(game, request);

            Assert.That(request.PlanConfirmed, Is.False);
            Assert.That(request.PlanRejectionCount, Is.EqualTo(1));
            Assert.That(request.LastPlanReviewMessage, Does.Contain("소프트 마감"));
            Assert.That(game.IsWorkVisible(work), Is.False);
        }

        [Test]
        public void ThirdRejectedStakeholderPlanFailsRequest()
        {
            var game = new MilestoneSimulation(TaskSystemDataLoader.Load(), 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            game.StartStakeholderPlanning("landing-brief");
            FillStakeholderPlan(game, false);
            StakeholderRequestState request = game.StakeholderState("landing-brief");
            WorkGroup work = game.Groups.Find(group => group.Id == request.WorkId);

            Assert.That(game.StakeholderPlanProjectedFit(request.Id), Is.LessThan(70));
            Assert.That(game.SubmitStakeholderPlan(request.Id), Is.True);
            AdvanceUntilStakeholderPlanReviewed(game, request);
            Assert.That(game.SubmitStakeholderPlan(request.Id), Is.True);
            AdvanceUntilStakeholderPlanReviewed(game, request);
            Assert.That(game.SubmitStakeholderPlan(request.Id), Is.True);
            AdvanceUntilStakeholderPlanReviewed(game, request);

            Assert.That(request.PlanRejectionCount, Is.EqualTo(3));
            Assert.That(request.PlanFailed, Is.True);
            Assert.That(work.State, Is.EqualTo(WorkState.Failed));
            Assert.That(game.Tasks.FindAll(task => task.StakeholderRequestId == request.Id),
                Has.All.Matches<WorkTask>(task => task.State == TaskState.Failed));
        }

        [Test]
        public void CatalogScopeAndOptionsRecalculateEffortAndClearOnDisable()
        {
            var game = new MilestoneSimulation(TaskSystemDataLoader.Load(), 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            game.StartStakeholderPlanning("landing-brief");
            WorkTask admin = game.Tasks.Find(task =>
                task.Id == "stakeholder-landing-brief-admin-operation");

            Assert.That(admin.ScopeIncluded, Is.False);
            Assert.That(game.SetStakeholderTaskIncluded(admin.Id, true), Is.True);
            Assert.That(game.SetStakeholderOption(admin.Id, "audit-log", true), Is.True);
            Assert.That(admin.RequiredWork, Is.EqualTo(7f));
            Assert.That(admin.SelectedOptionIds, Does.Contain("audit-log"));

            Assert.That(game.SetStakeholderTaskIncluded(admin.Id, false), Is.True);
            Assert.That(admin.RequiredWork, Is.Zero);
            Assert.That(admin.SelectedOptionIds, Is.Empty);
            Assert.That(admin.PlannedCharacter, Is.EqualTo(-1));
        }

        [TestCase(2, "F")]
        [TestCase(3, "D")]
        [TestCase(4, "C")]
        [TestCase(5, "B")]
        [TestCase(6, "A")]
        [TestCase(7, "S")]
        public void CompetencyGradesUseFdcBasOrder(int value, string expected)
        {
            Assert.That(CrewMember.CompetencyGrade(value), Is.EqualTo(expected));
        }

        [Test]
        public void MismatchedWorkAccumulatesDissatisfactionAndLowersEfficiency()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            data.Balance.FreshLowOutputChance = 0;
            data.Balance.FreshHighOutputChance = 0;
            TaskSystemData otherData = TaskSystemDataLoader.Load();
            DisableAccidents(otherData);
            otherData.Balance.FreshLowOutputChance = 0;
            otherData.Balance.FreshHighOutputChance = 0;
            var matched = new MilestoneSimulation(data, 5);
            var mismatched = new MilestoneSimulation(otherData, 5);

            Assert.That(matched.Assign("survey", 1), Is.True);
            Assert.That(mismatched.Assign("survey", 0), Is.True);
            matched.AdvanceDay();
            mismatched.AdvanceDay();

            Assert.That(mismatched.Crew[0].Dissatisfaction, Is.GreaterThan(0));
            Assert.That(mismatched.Tasks.Find(task => task.Id == "survey").LastOutput,
                Is.LessThan(matched.Tasks.Find(task => task.Id == "survey").LastOutput));
        }

        [Test]
        public void ClarificationRaisesClarityOnDueDayWithoutMovingDeadline()
        {
            var game = new MilestoneSimulation(TaskSystemDataLoader.Load(), 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            StakeholderRequestState request = game.StakeholderState("landing-brief");
            WorkGroup work = game.Groups.Find(group => group.Id == request.WorkId);
            int hardDeadline = work.HardDeadline;

            Assert.That(game.RequestStakeholderClarification(request.Id, "call"), Is.True);
            game.AdvanceDay();
            Assert.That(request.Clarity, Is.EqualTo(25));
            game.AdvanceDay();

            Assert.That(request.Clarity, Is.EqualTo(50));
            Assert.That(request.PendingClarificationId, Is.Empty);
            Assert.That(work.HardDeadline, Is.EqualTo(hardDeadline));
        }

        [Test]
        public void NextEventAdvanceStopsWhenClarificationArrives()
        {
            var game = new MilestoneSimulation(TaskSystemDataLoader.Load(), 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            StakeholderRequestState request = game.StakeholderState("landing-brief");

            Assert.That(game.RequestStakeholderClarification(request.Id, "call"), Is.True);
            Assert.That(game.NextEventDay, Is.EqualTo(3));
            Assert.That(game.NextEventSummary, Is.EqualTo("추가 정보"));

            Assert.That(game.AdvanceToNextEvent(), Is.EqualTo(2));
            Assert.That(game.Day, Is.EqualTo(3));
            Assert.That(request.PendingClarificationId, Is.Empty);
            Assert.That(request.Clarity, Is.EqualTo(50));
        }

        [Test]
        public void CompletedStakeholderPlanScoresWeightedIntentFitOnce()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            game.StartStakeholderPlanning("landing-brief");
            FillStakeholderPlan(game, true);
            Assert.That(game.ConfirmStakeholderPlan("landing-brief"), Is.True);
            AdvanceUntilStakeholderPlanReviewed(game, game.StakeholderState("landing-brief"));
            game.Crew[0].DailyOutput = 100f;
            game.Crew[1].DailyOutput = 100f;
            game.Crew[2].DailyOutput = 100f;
            game.Crew[3].DailyOutput = 100f;
            game.Crew[4].DailyOutput = 100f;

            game.AdvanceDay();
            StakeholderRequestState result = game.StakeholderState("landing-brief");

            Assert.That(result.FitScore, Is.EqualTo(100));
            Assert.That(game.CampaignFitScore, Is.EqualTo(100));
            Assert.That(game.Mail.FindAll(mail => mail.Id.StartsWith("stakeholder-result-")).Count,
                Is.EqualTo(1));
            game.AdvanceDay();
            Assert.That(game.Mail.FindAll(mail => mail.Id.StartsWith("stakeholder-result-")).Count,
                Is.EqualTo(1));
        }

        [Test]
        public void StakeholderRequestPlanSurvivesCampaignRestore()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            var game = new MilestoneSimulation(data, 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            game.StartStakeholderPlanning("landing-brief");
            game.SetStakeholderOption("stakeholder-landing-brief-order-refund", "partial-cancel", true);
            game.AssignStakeholderTask("stakeholder-landing-brief-order-refund", 1);
            game.RequestStakeholderClarification("landing-brief", "meeting");

            CampaignSnapshot snapshot = game.CreateSnapshot();
            snapshot.Mail = new MailEvent[0];
            var restored = new MilestoneSimulation(data, 2);
            Assert.That(restored.Restore(snapshot), Is.True);
            StakeholderRequestState state = restored.StakeholderState("landing-brief");
            WorkTask task = restored.Tasks.Find(candidate =>
                candidate.Id == "stakeholder-landing-brief-order-refund");

            Assert.That(state, Is.Not.Null);
            Assert.That(state.PendingClarificationId, Is.EqualTo("meeting"));
            Assert.That(task.SelectedOptionIds, Does.Contain("partial-cancel"));
            Assert.That(task.SelectedTaskBlockId, Is.EqualTo("order-refund"));
            Assert.That(task.PlannedCharacter, Is.EqualTo(1));
            Assert.That(task.AssignedCharacter, Is.EqualTo(-1));
            Assert.That(restored.Mail.Exists(mail => mail.Id == "mail-stakeholder-landing"), Is.True,
                "A patch must add the request entry mail to an older campaign snapshot.");
        }

        [Test]
        public void PendingStakeholderPlanReviewSurvivesCampaignRestore()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            var game = new MilestoneSimulation(data, 1);
            game.OpenStakeholderRequest("mail-stakeholder-landing");
            game.StartStakeholderPlanning("landing-brief");
            FillStakeholderPlan(game, true);
            Assert.That(game.SubmitStakeholderPlan("landing-brief"), Is.True);
            int reviewDay = game.StakeholderState("landing-brief").PendingPlanReviewDay;

            var restored = new MilestoneSimulation(data, 2);
            Assert.That(restored.Restore(game.CreateSnapshot()), Is.True);
            StakeholderRequestState request = restored.StakeholderState("landing-brief");

            Assert.That(request.PendingPlanReviewDay, Is.EqualTo(reviewDay));
            Assert.That(restored.NextEventDay, Is.EqualTo(reviewDay));
            AdvanceUntilStakeholderPlanReviewed(restored, request);
            Assert.That(request.PlanConfirmed, Is.True);
        }

        [TestCase(1, 1, 10, 1, 4)]
        [TestCase(92, 1, 12, 31, 4)]
        [TestCase(93, 1, 1, 1, 1)]
        [TestCase(151, 1, 2, 28, 1)]
        [TestCase(152, 1, 3, 1, 1)]
        [TestCase(274, 1, 7, 1, 3)]
        [TestCase(366, 2, 10, 1, 4)]
        public void GameCalendarUsesOctoberStartAndNonLeapYear(
            int gameDay, int expectedYear, int expectedMonth, int expectedDay, int expectedQuarter)
        {
            GameCalendar.GetDate(gameDay, out int year, out int month, out int dayOfMonth);

            Assert.That(year, Is.EqualTo(expectedYear));
            Assert.That(month, Is.EqualTo(expectedMonth));
            Assert.That(dayOfMonth, Is.EqualTo(expectedDay));
            Assert.That(GameCalendar.Quarter(month), Is.EqualTo(expectedQuarter));
        }

        [Test]
        public void GameCalendarFormatKeepsDayAndShowsDateAndQuarter()
        {
            Assert.That(GameCalendar.FormatDay(1), Is.EqualTo("DAY 01 · 10월 1일 · 4분기"));
            Assert.That(GameCalendar.FormatDay(366), Is.EqualTo("DAY 366 · 2년차 10월 1일 · 4분기"));
        }

        [Test]
        public void CompletedWorkLeavesTaskPanelAfterFourteenElapsedDays()
        {
            var work = new WorkGroup { Id = "done", State = WorkState.Complete };
            var tasks = new List<WorkTask>
            {
                new WorkTask
                {
                    GroupId = work.Id, Required = true, State = TaskState.Complete, CompletedDay = 20
                },
                new WorkTask
                {
                    GroupId = work.Id, Required = true, State = TaskState.Complete, CompletedDay = 22
                },
                new WorkTask
                {
                    GroupId = work.Id, Required = false, State = TaskState.Available
                }
            };

            Assert.That(MilestoneRules.WorkCompletionDay(work, tasks), Is.EqualTo(22));
            Assert.That(MilestoneRules.ShouldShowWorkInTaskPanel(work, tasks, 35), Is.True);
            Assert.That(MilestoneRules.ShouldShowWorkInTaskPanel(work, tasks, 36), Is.False);
        }

        [Test]
        public void IncompleteWorkAlwaysRemainsInTaskPanel()
        {
            var work = new WorkGroup { Id = "active", State = WorkState.InProgress };
            var tasks = new List<WorkTask>();

            Assert.That(MilestoneRules.ShouldShowWorkInTaskPanel(work, tasks, 999), Is.True);
        }

        [Test]
        public void MilestoneHistoryMeasuresInclusiveElapsedDaysFromFirstRecordedActivity()
        {
            var work = new WorkGroup { State = WorkState.InProgress, HardDeadline = 20 };
            var tasks = new List<WorkTask>
            {
                new WorkTask
                {
                    StartedDay = 5,
                    Records = new List<TaskRecord>
                    {
                        new TaskRecord { Day = 4, Actor = "민서", Kind = RecordKind.Note, Text = "배정" },
                        new TaskRecord { Day = 6, Actor = "민서", Kind = RecordKind.Output, Text = "성과" }
                    }
                }
            };

            Assert.That(MilestoneRules.HasWorkHistory(work, tasks), Is.True);
            Assert.That(MilestoneRules.WorkHistoryStartDay(tasks), Is.EqualTo(4));
            Assert.That(MilestoneRules.WorkHistoryEndDay(work, tasks, 8), Is.EqualTo(8));
            Assert.That(MilestoneRules.WorkHistoryElapsedDays(work, tasks, 8), Is.EqualTo(5));
        }

        [Test]
        public void MilestoneHistoryKeepsUniqueHistoricalParticipants()
        {
            var crew = new List<CrewMember>
            {
                new CrewMember { Name = "민서" },
                new CrewMember { Name = "도윤" }
            };
            var tasks = new List<WorkTask>
            {
                new WorkTask
                {
                    AssignedCharacter = 1,
                    LastWorker = 0,
                    Records = new List<TaskRecord>
                    {
                        new TaskRecord { Day = 1, Actor = "민서", Kind = RecordKind.Output },
                        new TaskRecord { Day = 2, Actor = "민서", Kind = RecordKind.Issue },
                        new TaskRecord { Day = 2, Actor = "SYSTEM", Kind = RecordKind.Note }
                    }
                }
            };

            Assert.That(MilestoneRules.WorkHistoryParticipants(tasks, crew),
                Is.EqualTo("민서, 도윤"));
        }

        [Test]
        public void MilestoneHistoryExcludesUntouchedPlans()
        {
            var work = new WorkGroup { State = WorkState.Available };
            var tasks = new List<WorkTask> { new WorkTask { State = TaskState.Available } };

            Assert.That(MilestoneRules.HasWorkHistory(work, tasks), Is.False);
        }

        [Test]
        public void IncidentPerkMultipliersStackMultiplicativelyAtEachCheckpoint()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 1;
            PerkDefinition first = Array.Find(data.PerkDefinitions, perk => perk.Name == "정밀 정비");
            PerkDefinition second = Array.Find(data.PerkDefinitions, perk => perk.Name == "안전 우선");
            first.IncidentHalfMultiplier = 3f;
            second.IncidentHalfMultiplier = 2f;
            var game = new MilestoneSimulation(data, 1);
            CrewMember member = game.Crew[0];

            IncidentChancePreview chance = game.BuildIncidentChance(member, 50);

            Assert.That(chance.TotalMultiplier, Is.EqualTo(6f).Within(.001f));
            Assert.That(chance.ChanceBasisPoints, Is.EqualTo(600));
            Assert.That(chance.Formula, Does.Contain("정밀 정비 3").And.Contain("안전 우선 2"));
        }

        [Test]
        public void TaskConsumesZeroHalfAndCompleteIncidentCheckpointsOnlyOnce()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 0;
            data.Balance.RandomWorkLimit = 3;
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            game.Crew[1].DailyOutput = survey.EffectiveRequiredWork;
            Assert.That(game.Assign(survey.Id, 1), Is.True);

            game.AdvanceDay();

            Assert.That(survey.State, Is.EqualTo(TaskState.Complete));
            Assert.That(survey.IncidentCheckpointMask, Is.EqualTo(7));
            CampaignSnapshot snapshot = game.CreateSnapshot();
            var restored = new MilestoneSimulation(data, 2);
            Assert.That(restored.Restore(snapshot), Is.True);
            Assert.That(restored.Tasks.Find(task => task.Id == survey.Id).IncidentCheckpointMask,
                Is.EqualTo(7));
        }

        [Test]
        public void GeneratedIncidentRecordsWorkerTaskCheckpointAndMultiplierFormula()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 100;
            data.Balance.RandomWorkLimit = 1;
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            Assert.That(game.Assign(survey.Id, 1), Is.True);

            game.AdvanceDay();

            MailEvent offer = game.PendingIncidentOffer;
            Assert.That(offer, Is.Not.Null);
            Assert.That(offer.Body, Does.Contain(game.Crew[1].Name)
                .And.Contain(survey.Name).And.Contain("0% 시점")
                .And.Contain("현재 배율").And.Contain("기본 100%"));
            Assert.That(survey.Records.Exists(record => record.Text.Contains("돌발상황 발생")), Is.True);
        }

        [Test]
        public void CriticalEventChoiceChainAppliesEffectsAndAdvancesNodes()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.CriticalEvents = CriticalEventFixture();
            var game = new MilestoneSimulation(data, 7);
            int initialResources = game.Resources;

            Assert.That(game.HasActiveCriticalEvent, Is.True);
            Assert.That(game.ActiveCriticalNodeId, Is.EqualTo("first"));
            Assert.That(game.Mail.Exists(mail => mail.Subject.StartsWith("[!중요!]")), Is.True);
            Assert.That(game.ChooseCriticalEvent("commit"), Is.True);
            Assert.That(game.ActiveCriticalNodeId, Is.EqualTo("second"));
            Assert.That(game.HasPendingCriticalChoice, Is.False);
            Assert.That(game.ActiveCriticalNodeArrivalDay, Is.InRange(3, 4));
            Assert.That(game.Resources, Is.EqualTo(initialResources - 2));
            foreach (CrewMember member in game.Crew)
                Assert.That(member.Fatigue, Is.EqualTo(5));
            Assert.That(game.TaskSuccessChanceModifier, Is.EqualTo(6));

            while (game.Day < game.ActiveCriticalNodeArrivalDay) game.AdvanceDay();
            Assert.That(game.HasPendingCriticalChoice, Is.True);
            Assert.That(game.ChooseCriticalEvent("finish"), Is.True);
            Assert.That(game.HasActiveCriticalEvent, Is.False);
            game.AdvanceDay();
            Assert.That(game.Day, Is.InRange(4, 5));
        }

        [Test]
        public void CriticalEventCanBeAnsweredAnyTimeDuringItsSevenDayWindow()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.CriticalEvents = CriticalEventFixture();
            var game = new MilestoneSimulation(data, 7);

            Assert.That(game.Day, Is.EqualTo(1));
            Assert.That(game.CriticalResponseDeadlineDay, Is.EqualTo(7));
            Assert.That(game.MustResolveCriticalChoice, Is.False);
            Assert.That(game.Mail.Find(mail => mail.IsCritical).Instruction, Does.Contain("DAY 7"));

            for (int day = 1; day < 7; day++) game.AdvanceDay();

            Assert.That(game.Day, Is.EqualTo(7));
            Assert.That(game.MustResolveCriticalChoice, Is.True);

            Assert.That(game.ChooseCriticalEvent("commit"), Is.True);
            Assert.That(game.MustResolveCriticalChoice, Is.False);
            game.AdvanceDay();
            Assert.That(game.Day, Is.EqualTo(8));
        }

        [Test]
        public void OnlyOneDueCriticalEventCanBeActiveAtATime()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            CriticalEventDefinition[] definitions = CriticalEventFixture();
            CriticalEventDefinition second = definitions[0];
            definitions[0] = new CriticalEventDefinition
            {
                Id = "earlier",
                StartDay = 1,
                FirstNodeId = "first",
                Nodes = second.Nodes
            };
            definitions = new[] { definitions[0], second };
            data.CriticalEvents = definitions;
            var game = new MilestoneSimulation(data, 1);

            Assert.That(game.ActiveCriticalEventId, Is.EqualTo("earlier"));
            Assert.That(game.Mail.FindAll(mail => mail.IsCritical), Has.Count.EqualTo(1));
        }

        [Test]
        public void DebugForceStartsEarliestCriticalEventBeforeItsScheduledDay()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            CriticalEventDefinition[] definitions = CriticalEventFixture();
            definitions[0].StartDay = 20;
            data.CriticalEvents = definitions;
            var game = new MilestoneSimulation(data, 1);

            Assert.That(game.HasActiveCriticalEvent, Is.False);
            Assert.That(game.CanForceCriticalEvent, Is.True);
            Assert.That(game.ForceCriticalEvent(), Is.True);

            Assert.That(game.ActiveCriticalEventId, Is.EqualTo("test-chain"));
            Assert.That(game.ActiveCriticalNodeId, Is.EqualTo("first"));
            Assert.That(game.CanForceCriticalEvent, Is.False);
            Assert.That(game.HasPendingCriticalChoice, Is.True);
            Assert.That(game.Mail.Exists(mail => mail.IsCritical && !mail.Resolved), Is.True);
        }

        private static CriticalEventDefinition[] CriticalEventFixture()
        {
            return new[]
            {
                new CriticalEventDefinition
                {
                    Id = "test-chain",
                    StartDay = 1,
                    FirstNodeId = "first",
                    Nodes = new[]
                    {
                        new CriticalEventNode
                        {
                            Id = "first", From = "TEST", Subject = "First", Body = "Body",
                            Choices = new[]
                            {
                                new CriticalEventChoice
                                {
                                    Id = "commit", Text = "Commit", Outcomes = new[]
                                    {
                                        new CriticalEventOutcome
                                        {
                                            Weight = 100, Text = "Applied", ResourceDelta = -2,
                                            CrewIndex = -1, FatigueDelta = 5,
                                            SuccessChanceDelta = 6, NextNodeId = "second"
                                        }
                                    }
                                }
                            }
                        },
                        new CriticalEventNode
                        {
                            Id = "second", From = "TEST", Subject = "Second", Body = "Body",
                            Choices = new[]
                            {
                                new CriticalEventChoice
                                {
                                    Id = "finish", Text = "Finish", Outcomes = new[]
                                    {
                                        new CriticalEventOutcome { Weight = 100, Text = "Done" }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        [Test]
        public void EndlessSessionHasNoVictoryState()
        {
            var game = new MilestoneSimulation(1);

            foreach (WorkGroup group in game.Groups) group.Required = false;

            Assert.That(game.IsWon, Is.False);
            Assert.That(game.IsLost, Is.False);
        }

        [Test]
        public void FormerCampaignEndIsOnlyAPlanningBaseline()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.CampaignEndDay = 2;
            data.MidpointReviewDay = 1;
            data.StartingResources = 100;
            data.Balance.PayrollIntervalDays = 100;
            foreach (WorkGroup group in data.Works) group.Required = false;
            var game = new MilestoneSimulation(data, 1);

            game.AdvanceDay();
            game.AdvanceDay();
            game.AdvanceDay();

            Assert.That(game.Day, Is.GreaterThan(data.CampaignEndDay));
            Assert.That(game.IsLost, Is.False);
            Assert.That(game.PlanningHorizonDay, Is.EqualTo(game.Day + 30));
        }

        [Test]
        public void ResourceDepletionIsTheOnlySessionEndingRule()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.StartingResources = 1;
            data.Balance.PayrollIntervalDays = 1;
            data.Balance.BaseSalary = 1;
            foreach (WorkGroup group in data.Works) group.Required = false;
            var game = new MilestoneSimulation(data, 1);

            game.AdvanceDay();

            Assert.That(game.Resources, Is.Zero);
            Assert.That(game.IsLost, Is.True);
            int stoppedDay = game.Day;
            game.AdvanceDay();
            Assert.That(game.Day, Is.EqualTo(stoppedDay));
        }

        [Test]
        public void CrewProfilesLoadPortraitMemoAndPerksFromExternalData()
        {
            var game = new MilestoneSimulation(1);

            Assert.That(game.Crew, Has.Count.EqualTo(MilestoneSimulation.TeamSize));
            Assert.That(game.CampaignEndDay, Is.EqualTo(90));
            Assert.That(game.MidpointReviewDay, Is.EqualTo(45));
            Assert.That(game.Crew[0].PortraitLabel, Is.Not.Empty);
            Assert.That(game.Crew[0].PortraitAddress, Is.EqualTo("portraits/crew/crew-han-tech"));
            Assert.That(game.Crew[0].Personality, Is.Not.Empty);
            Assert.That(game.Crew[0].Memo, Is.Not.Empty);
            Assert.That(game.Crew[0].Perks, Is.Not.Null.And.Not.Empty);
            Assert.That(game.Crew[0].Competencies, Has.Length.EqualTo(CrewMember.CompetencyCount));
            Assert.That(game.Crew[0].Competencies, Has.All.InRange(0, CrewMember.MaximumCompetency));
            Assert.That(game.Crew[0].Trust, Is.InRange(0, 100));
        }

        [Test]
        public void CodexShipsAPlayerGuideAndDeveloperMiniSpecification()
        {
            var game = new MilestoneSimulation(1);
            string[] requiredCategories =
            {
                "시작과 목표", "일과 작업", "작업자", "자동화",
                "자원과 운영", "상호작용", "시스템과 데이터"
            };

            foreach (string category in requiredCategories)
                Assert.That(game.Codex.Exists(entry => entry.Category == category), Is.True, category);
            Assert.That(game.Codex, Has.Count.GreaterThanOrEqualTo(25));
            Assert.That(game.Codex.TrueForAll(entry =>
                !string.IsNullOrWhiteSpace(entry.Id) &&
                !string.IsNullOrWhiteSpace(entry.Category) &&
                !string.IsNullOrWhiteSpace(entry.Name) &&
                entry.Description != null && entry.Description.Length >= 100), Is.True);
            Assert.That(game.Codex.Exists(entry => entry.Description.Contains("DailyOutput")), Is.True);
            Assert.That(game.Codex.Exists(entry => entry.Description.Contains("아직 구현되지")), Is.True);
        }

        [Test]
        public void CodexRecordsEveryInitialCrewPersonalityAndPerkOnce()
        {
            var game = new MilestoneSimulation(2);

            foreach (CrewMember member in game.Crew)
            {
                Assert.That(game.Codex.Exists(entry =>
                    entry.Category == "작업자 특성 · 성격" && entry.Name == member.Personality), Is.True);
                foreach (string perk in member.Perks)
                    Assert.That(game.Codex.Exists(entry =>
                        entry.Category == "작업자 특성 · 퍽" && entry.Name == perk), Is.True);
            }
            for (int i = 0; i < game.DiscoveredCrewTraitIds.Count; i++)
                for (int previous = 0; previous < i; previous++)
                    Assert.That(game.DiscoveredCrewTraitIds[i],
                        Is.Not.EqualTo(game.DiscoveredCrewTraitIds[previous]));
        }

        [Test]
        public void RegeneratedCrewTraitDiscoveryPersistsAfterTraitIsLostAndSaveIsRestored()
        {
            var game = new MilestoneSimulation(3);
            game.Crew[0].Perks = new[] { "새로운 현장 퍽" };

            Assert.That(game.Regenerate(0, false, true), Is.True);
            Assert.That(game.Codex.Exists(entry =>
                entry.Category == "작업자 특성 · 퍽" && entry.Name == "새로운 현장 퍽"), Is.True);
            game.Crew[0].Perks = new[] { "교체된 퍽" };

            var restored = new MilestoneSimulation(4);
            Assert.That(restored.Restore(game.CreateSnapshot()), Is.True);
            Assert.That(restored.Codex.Exists(entry =>
                entry.Category == "작업자 특성 · 퍽" && entry.Name == "새로운 현장 퍽"), Is.True);
            Assert.That(restored.Codex.Exists(entry =>
                entry.Category == "작업자 특성 · 퍽" && entry.Name == "교체된 퍽"), Is.True);
        }

        [Test]
        public void ManualAssignmentCreatesAndUpdatesLearnedRule()
        {
            var game = new MilestoneSimulation(1);

            Assert.That(game.Assign("survey", 1), Is.True);
            Assert.That(game.AssignmentRules, Has.Count.EqualTo(1));
            Assert.That(game.AssignmentRules[0].CrewName, Is.EqualTo(game.Crew[1].Name));
            Assert.That(game.AssignmentRules[0].UpdateCount, Is.EqualTo(1));

            Assert.That(game.Assign("survey", 0), Is.True);
            Assert.That(game.AssignmentRules, Has.Count.EqualTo(1));
            Assert.That(game.AssignmentRules[0].CrewName, Is.EqualTo(game.Crew[0].Name));
            Assert.That(game.AssignmentRules[0].UpdateCount, Is.EqualTo(2));
        }

        [Test]
        public void LearnedRuleAutomaticallyAssignsMatchingAvailableTask()
        {
            var game = new MilestoneSimulation(1);
            WorkTask source = game.Tasks.Find(task => task.Id == "survey");
            Assert.That(game.Assign(source.Id, 1), Is.True);
            Assert.That(game.Assign(source.Id, -1), Is.True);
            source.Progress = source.EffectiveRequiredWork;
            source.State = TaskState.Complete;
            var repeated = new WorkTask
            {
                Id = "survey-repeat",
                Name = "repeat",
                Kind = source.Kind,
                RequiredRole = source.RequiredRole,
                Difficulty = source.Difficulty,
                Risk = source.Risk,
                Importance = source.Importance,
                RequiredWork = 3f,
                Required = true,
                GroupId = source.GroupId,
                State = TaskState.Available
            };
            game.Tasks.Add(repeated);

            DayReport report = game.AdvanceDay();

            Assert.That(repeated.AssignedCharacter, Is.EqualTo(1));
            Assert.That(report.Lines, Has.Some.Contains("자동 배정"));
            Assert.That(game.AssignmentRules[0].UpdateCount, Is.EqualTo(1));
        }

        [Test]
        public void WeeklyCalendarIdentifiesSaturdayAndSundayColumns()
        {
            Assert.That(MilestoneSimulation.IsWeekendDay(5), Is.False);
            Assert.That(MilestoneSimulation.IsWeekendDay(6), Is.True);
            Assert.That(MilestoneSimulation.IsWeekendDay(7), Is.True);
            Assert.That(MilestoneSimulation.IsWeekendDay(8), Is.False);
            Assert.That(MilestoneSimulation.WeekdayName((Weekday)((13 - 1) % 7)), Is.EqualTo("토"));
        }

        [Test]
        public void AutomaticAssignmentPreviewUsesLearnedRuleBeforeCompetencySelection()
        {
            var game = new MilestoneSimulation(5);
            Assert.That(game.Assign("survey", 1), Is.True);
            Assert.That(game.Assign("survey", -1), Is.True);
            game.SetCompetencyAutoAssignment(true);

            int preview = game.PreviewAutomaticAssignee("survey", out string source);

            Assert.That(preview, Is.EqualTo(1));
            Assert.That(source, Is.EqualTo("학습 규칙"));
            game.AdvanceDay();
            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.EqualTo(preview));
        }

        [Test]
        public void OrganizationPreviewMatchesNextAutomaticAssignment()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 6);
            game.SetCompetencyAutoAssignment(true);

            int preview = game.PreviewAutomaticAssignee("survey", out string source);

            Assert.That(preview, Is.GreaterThanOrEqualTo(0));
            Assert.That(source, Does.StartWith("조직"));
            game.AdvanceDay();
            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.EqualTo(preview));
        }

        [Test]
        public void OrganizationAssignmentUsesDefaultRoleDepthChart()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.EqualTo(1));
            Assert.That(game.LastReport.Lines, Has.Some.Contains("조직 자동 배정"));
        }

        [Test]
        public void BalancedOrganizationKeepsRoleDepthPriorityOverSmallFatigueDifference()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            data.Crew[1].Fatigue = 30;
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.EqualTo(1));
        }

        [Test]
        public void CompetencyAutoAssignmentLeavesSuccessorsUnassignedUntilPredecessorCompletes()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == RootTaskId).AssignedCharacter, Is.GreaterThanOrEqualTo(0));
            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskAId).AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskBId).AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == OptionalTaskId).AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == FollowupTaskId).AssignedCharacter, Is.EqualTo(-1));
        }

        [Test]
        public void CompetencyAutoAssignmentReconsidersSuccessorsOnCycleAfterCompletion()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            DisableAccidents(data);
            data.Balance.BaseSideMissionChance = 0;
            Array.Find(data.Tasks, task => task.Id == RootTaskId).RequiredWork = .5f;
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();
            Assert.That(game.Tasks.Find(task => task.Id == RootTaskId).State, Is.EqualTo(TaskState.Complete));
            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskAId).AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskBId).AssignedCharacter, Is.EqualTo(-1));

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskAId).AssignedCharacter, Is.GreaterThanOrEqualTo(0));
            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskBId).AssignedCharacter, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void LearnedAutoAssignmentAlsoWaitsForTaskPredecessor()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);
            WorkTask dependent = game.Tasks.Find(task => task.Id == DependentTaskAId);
            game.AssignmentRules.Add(new AssignmentRule
            {
                Kind = dependent.Kind,
                RequiredRole = dependent.RequiredRole,
                Difficulty = dependent.Difficulty,
                Risk = dependent.Risk,
                Importance = dependent.Importance,
                CrewName = game.Crew[0].Name
            });

            game.AdvanceDay();

            Assert.That(dependent.AssignedCharacter, Is.EqualTo(-1));
        }

        [Test]
        public void ReservationTakesPriorityOverCompetencyAutoAssignment()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            Assert.That(game.Schedule("survey", 0, game.Day), Is.True);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.EqualTo(0));
        }

        [Test]
        public void LearnedRuleTakesPriorityOverCompetencyAutoAssignment()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            Assert.That(game.Assign("survey", 0), Is.True);
            Assert.That(game.Assign("survey", -1), Is.True);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.EqualTo(0));
        }

        [Test]
        public void LearnedRulesAndMidpointReviewSurviveSnapshot()
        {
            var original = new MilestoneSimulation(1);
            Assert.That(original.Assign("survey", 1), Is.True);
            original.SetCompetencyAutoAssignment(true);
            CampaignSnapshot snapshot = original.CreateSnapshot();
            snapshot.Day = 44;

            var restored = new MilestoneSimulation(2);
            Assert.That(restored.Restore(snapshot), Is.True);
            DayReport report = restored.AdvanceDay();

            Assert.That(restored.AssignmentRules, Has.Count.EqualTo(1));
            Assert.That(restored.CompetencyAutoAssignment, Is.True);
            Assert.That(restored.MidpointReviewIssued, Is.True);
            Assert.That(report.Lines, Has.Some.Contains("중간평가"));
            Assert.That(restored.CreateSnapshot().MidpointReviewIssued, Is.True);
        }

        [Test]
        public void TrustDescriptionExplainsHowWorkerViewsResponsibleOfficer()
        {
            Assert.That(MilestoneSimulation.TrustDescription(80), Does.Contain("깊이 신뢰"));
            Assert.That(MilestoneSimulation.TrustDescription(60), Does.Contain("협력"));
            Assert.That(MilestoneSimulation.TrustDescription(45), Does.Contain("지켜보고"));
            Assert.That(MilestoneSimulation.TrustDescription(20), Does.Contain("경계"));
        }

        [Test]
        public void RestoreExpandsLegacyFourPersonRosterToTwelve()
        {
            var original = new MilestoneSimulation(1);
            CampaignSnapshot snapshot = original.CreateSnapshot();
            var legacyCrew = new CrewMember[4];
            for (int i = 0; i < legacyCrew.Length; i++) legacyCrew[i] = snapshot.Crew[i];
            legacyCrew[0].Dissatisfaction = 37;
            snapshot.Crew = legacyCrew;

            var restored = new MilestoneSimulation(2);
            Assert.That(restored.Restore(snapshot), Is.True);

            Assert.That(restored.Crew, Has.Count.EqualTo(MilestoneSimulation.TeamSize));
            Assert.That(restored.Crew[0].Dissatisfaction, Is.EqualTo(37));
            Assert.That(restored.Crew[4].Name, Is.EqualTo("박설비관"));
        }

        [Test]
        public void MatchingSpecialtyAdvancesWorkAndAddsFatigue()
        {
            var game = new MilestoneSimulation(1);
            Assert.That(game.Assign("survey", 1), Is.True);
            game.AdvanceDay();
            Assert.That(game.Tasks[0].Progress, Is.EqualTo(1.5f).Within(.001f));
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(4));
        }

        [Test]
        public void AllRequiredCompetenciesBelowStandardProduceHalfOutput()
        {
            var member = new CrewMember { Competencies = new[] { 3, 2, 1, 3, 2, 1 } };
            var task = new WorkTask { RequiredCompetencies = new[] { 0, 1, 3 } };

            Assert.That(MilestoneSimulation.CompetencyOutputMultiplier(member, task), Is.EqualTo(.5f));
        }

        [Test]
        public void ExcellentCompetencyCoversAnotherRequiredCompetencyShortfall()
        {
            var member = new CrewMember { Competencies = new[] { 7, 1, 4, 4, 4, 4 } };
            var task = new WorkTask { RequiredCompetencies = new[] { 0, 1 } };

            Assert.That(MilestoneSimulation.CompetencyOutputMultiplier(member, task), Is.EqualTo(1f));
        }

        [TestCase(100, 0, TaskOutcome.Failure, .75f)]
        [TestCase(0, 0, TaskOutcome.Success, 1.5f)]
        [TestCase(0, 100, TaskOutcome.GreatSuccess, 2.25f)]
        public void ConditionOutcomeMultipliesCompetencyBasedOutput(
            int lowChance, int highChance, TaskOutcome expectedOutcome, float expectedOutput)
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.FreshLowOutputChance = lowChance;
            data.Balance.LowOutputChance = lowChance;
            data.Balance.ExhaustedLowOutputChance = lowChance;
            data.Balance.FreshHighOutputChance = highChance;
            data.Balance.HighOutputChance = highChance;
            data.Balance.ExhaustedHighOutputChance = highChance;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);

            game.Assign("survey", 1);
            game.AdvanceDay();

            WorkTask task = game.Tasks.Find(candidate => candidate.Id == "survey");
            Assert.That(task.LastOutcome, Is.EqualTo(expectedOutcome));
            Assert.That(task.LastOutput, Is.EqualTo(expectedOutput).Within(.001f));
        }

        [Test]
        public void AssignmentWaitsUntilDailyCycleBeforeTaskStarts()
        {
            var game = new MilestoneSimulation(1);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");

            Assert.That(game.Assign(survey.Id, 1), Is.True);
            Assert.That(survey.StartedDay, Is.Zero);
            Assert.That(survey.State, Is.EqualTo(TaskState.Available));

            game.AdvanceDay();

            Assert.That(survey.StartedDay, Is.EqualTo(1));
            Assert.That(survey.State, Is.EqualTo(TaskState.Active));
        }

        [Test]
        public void UnchangedAssigneeWorksUntilOutputCompletesTaskAndRecordsDates()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            data.Balance.LowOutputChance = 0;
            data.Balance.HighOutputChance = 0;
            data.Balance.FreshLowOutputChance = 0;
            data.Balance.FreshHighOutputChance = 0;
            data.Balance.ExhaustedLowOutputChance = 0;
            data.Balance.ExhaustedHighOutputChance = 0;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);
            WorkTask survey = game.Tasks.Find(task => task.Id == RootTaskId);

            game.Assign(survey.Id, 1);
            game.AdvanceDay();
            Assert.That(survey.State, Is.EqualTo(TaskState.Active));
            Assert.That(survey.Progress, Is.EqualTo(1.5f));

            game.AdvanceDay();

            Assert.That(survey.Progress, Is.EqualTo(survey.EffectiveRequiredWork));
            Assert.That(survey.State, Is.EqualTo(TaskState.Complete));
            Assert.That(survey.StartedDay, Is.EqualTo(1));
            Assert.That(survey.CompletedDay, Is.EqualTo(2));
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
        }

        [Test]
        public void CompletedTaskTimelineDoesNotFollowCurrentDay()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 0;
            data.Balance.LowOutputChance = 0;
            data.Balance.HighOutputChance = 0;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            WorkTask surveyDefinition = Array.Find(data.Tasks, task => task.Id == "survey");
            surveyDefinition.RequiredWork = .5f;
            var game = new MilestoneSimulation(data, 1);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");

            game.Assign(survey.Id, 1);
            game.AdvanceDay();
            int started = survey.StartedDay;
            int completed = survey.CompletedDay;
            game.AdvanceDay();
            game.AdvanceDay();

            Assert.That(survey.StartedDay, Is.EqualTo(started));
            Assert.That(survey.CompletedDay, Is.EqualTo(completed));
            Assert.That(MilestoneRules.TaskActualDurationDays(survey, game.Day),
                Is.EqualTo(1));
        }

        [Test]
        public void IncompletePrerequisiteCapsSuccessorAtThirtyPercent()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            data.Balance.LowOutputChance = 0;
            data.Balance.HighOutputChance = 0;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);
            WorkTask dependent = game.Tasks.Find(task => task.Id == DependentTaskBId);

            Assert.That(game.Assign(dependent.Id, 0), Is.True);
            game.AdvanceDay();
            game.AdvanceDay();

            Assert.That(dependent.Progress, Is.EqualTo(dependent.EffectiveRequiredWork * .3f).Within(.001f));
            Assert.That(dependent.State, Is.EqualTo(TaskState.Active));
        }

        [Test]
        public void WorkerDailyOutputUsesTwentyPercentLowBand()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.LowOutputChance = 100;
            data.Balance.HighOutputChance = 0;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            data.Crew[1].DailyOutput = 2f;
            data.Crew[1].Fatigue = 50;
            var game = new MilestoneSimulation(data, 1);

            game.Assign("survey", 1);
            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").LastOutput, Is.EqualTo(1.5f).Within(.001f));
        }

        [Test]
        public void FatigueControlsOutputChanceAnchors()
        {
            var game = new MilestoneSimulation(1);

            game.OutputChances(0, out int freshLow, out int freshHigh);
            game.OutputChances(50, out int midpointLow, out int midpointHigh);
            game.OutputChances(100, out int exhaustedLow, out int exhaustedHigh);

            Assert.That((freshLow, 100 - freshLow - freshHigh, freshHigh), Is.EqualTo((5, 60, 35)));
            Assert.That((midpointLow, 100 - midpointLow - midpointHigh, midpointHigh), Is.EqualTo((20, 60, 20)));
            Assert.That((exhaustedLow, 100 - exhaustedLow - exhaustedHigh, exhaustedHigh), Is.EqualTo((100, 0, 0)));
        }

        [Test]
        public void FullyFatiguedWorkerKeepsAssignmentButAutomaticallyRests()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Crew[1].Fatigue = 100;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");

            Assert.That(game.Assign(survey.Id, 1), Is.True);
            game.AdvanceDay();

            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(survey.LastOutput, Is.EqualTo(0f).Within(.001f));
            Assert.That(game.Crew[1].Fatigue, Is.LessThan(100));
            Assert.That(game.LastReport.Lines, Has.Some.Contains("피로 한계"));
        }

        [Test]
        public void WorkCompletionPaysConfiguredCreditRewardOnce()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.LowOutputChance = 0;
            data.Balance.HighOutputChance = 0;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            WorkGroup foundation = Array.Find(data.Works, work => work.Id == "foundation");
            foundation.RewardCredits = 6;
            foreach (WorkTask task in data.Tasks)
                if (task.GroupId == foundation.Id && task.Required)
                {
                    task.RequiredWork = .1f;
                    task.PrerequisiteId = null;
                }
            var game = new MilestoneSimulation(data, 1);
            int before = game.Resources;

            foreach (WorkTask task in game.Tasks.FindAll(item => item.GroupId == foundation.Id && item.Required))
            {
                game.Assign(task.Id, 0);
                game.AdvanceDay();
            }

            Assert.That(foundation.State, Is.EqualTo(WorkState.Complete));
            Assert.That(game.Resources, Is.EqualTo(before + 6));
            Assert.That(foundation.RewardClaimed, Is.True);
        }

        [Test]
        public void SoftAndHardDeadlinePenaltiesReduceCreditsOnce()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 0;
            WorkGroup foundation = Array.Find(data.Works, work => work.Id == "foundation");
            foundation.SoftDeadline = 1;
            foundation.HardDeadline = 2;
            foundation.SoftPenaltyCredits = 2;
            foundation.HardPenaltyCredits = 7;
            var game = new MilestoneSimulation(data, 1);

            game.AdvanceDay();
            Assert.That(game.Resources, Is.EqualTo(data.StartingResources - 2));
            game.AdvanceDay();

            Assert.That(game.Resources, Is.EqualTo(data.StartingResources - 9));
            Assert.That(foundation.SoftPenaltyApplied, Is.True);
            Assert.That(foundation.HardPenaltyApplied, Is.True);
        }

        [Test]
        public void DailyCycleCanGenerateRandomWorkWithDeadlinesAndCredits()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 100;
            data.Balance.RandomWorkChanceScalePercent = 100;
            data.Balance.RandomWorkDependencyChance = 0;
            data.RandomTaskWords = new RandomTaskWordPool
            {
                Adjectives = new[]
                {
                    new RandomTaskAdjective
                    {
                        Id = "adjective-unstable",
                        Text = "불안정한",
                        Risk = RiskLevel.High,
                        Difficulty = 2
                    }
                },
                Targets = new[]
                {
                    new RandomTaskTarget
                    {
                        Id = "target-bedrock",
                        Text = "암반",
                        Role = WorkRole.Analysis,
                        RequiredCompetencies = new[] { 1 },
                        Difficulty = 1
                    }
                },
                Actions = new[]
                {
                    new RandomTaskAction
                    {
                        Id = "action-survey",
                        Text = "탐사",
                        Role = WorkRole.Analysis,
                        RequiredCompetencies = new[] { 1 },
                        Difficulty = 1
                    }
                }
            };
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);
            Assert.That(game.Assign("survey", 1), Is.True);

            game.AdvanceDay();

            WorkGroup generated = game.Groups.Find(work => work.Id.StartsWith("random-work-"));
            List<WorkTask> generatedTasks = game.Tasks.FindAll(task => task.GroupId == generated.Id);
            WorkTask generatedTask = generatedTasks[0];
            Assert.That(generated, Is.Not.Null);
            Assert.That(generatedTasks, Has.Count.InRange(2, 4));
            for (int taskIndex = 1; taskIndex < generatedTasks.Count; taskIndex++)
                Assert.That(generatedTasks[taskIndex].PrerequisiteId,
                    Is.EqualTo(generatedTasks[taskIndex - 1].Id));
            Assert.That(generated.AwaitingAcceptance, Is.True);
            generated.Urgent = true;
            Assert.That(game.IsWorkVisible(generated), Is.False);
            Assert.That(generatedTask.State, Is.EqualTo(TaskState.Locked));
            MailEvent offer = game.Mail.Find(mail => mail.TargetWorkId == generated.Id);
            Assert.That(offer, Is.Not.Null);
            Assert.That(offer.ArrivalDay, Is.EqualTo(game.Day - 1));
            Assert.That(offer.ActivatesWork, Is.True);
            Assert.That(offer.IsProposal, Is.False);
            Assert.That(game.HasPendingIncidentDecision, Is.True);
            Assert.That(game.CanAdvanceDay, Is.False);
            Assert.That(game.AcceptIncidentWork(offer.Id), Is.True);
            Assert.That(game.HasPendingIncidentDecision, Is.False);
            Assert.That(game.CanAdvanceDay, Is.True);
            Assert.That(generated.AwaitingAcceptance, Is.False);
            Assert.That(game.IsWorkVisible(generated), Is.True);
            Assert.That(generatedTask.State, Is.EqualTo(TaskState.Available));
            Assert.That(generated.SoftDeadline, Is.LessThan(generated.HardDeadline));
            Assert.That(generated.RewardCredits, Is.GreaterThan(0));
            Assert.That(generated.HardPenaltyCredits, Is.GreaterThan(generated.SoftPenaltyCredits));
            Assert.That(generatedTask.Name, Is.EqualTo("불안정한 암반 탐사"));
            Assert.That(generatedTask.RequiredRole, Is.EqualTo(WorkRole.Analysis));
            Assert.That(generatedTask.RequiredCompetencies, Is.EqualTo(new[] { 1 }));
            Assert.That(generatedTask.Risk, Is.EqualTo(RiskLevel.High));
            Assert.That(generatedTask.Difficulty, Is.EqualTo(4));
            Assert.That(generatedTask.RequiredWork, Is.InRange(1f, 2f));

            CampaignSnapshot legacySnapshot = game.CreateSnapshot();
            generated.AwaitingAcceptance = true;
            offer.ActivatesWork = true;
            offer.IsProposal = false;
            offer.IsBossRequest = false;
            offer.ProposalStage = ProposalStage.None;
            offer.Resolved = false;
            generatedTask.GeneratedAdjectiveId = null;
            generatedTask.GeneratedTargetId = null;
            generatedTask.GeneratedActionId = null;
            var migrated = new MilestoneSimulation(data, 98);
            Assert.That(migrated.Restore(legacySnapshot), Is.True);
            WorkTask migratedTask = migrated.Tasks.Find(task => task.Id == generatedTask.Id);
            WorkGroup migratedWork = migrated.Groups.Find(work => work.Id == generated.Id);
            MailEvent migratedOffer = migrated.Mail.Find(mail => mail.Id == offer.Id);
            Assert.That(migratedWork.AwaitingAcceptance, Is.True);
            Assert.That(migratedOffer.ActivatesWork, Is.True);
            Assert.That(migratedOffer.IsBossRequest, Is.True);
            Assert.That(migrated.IsWorkVisible(migratedWork), Is.False);
            Assert.That(migratedTask.GeneratedAdjectiveId, Is.EqualTo("adjective-unstable"));
            Assert.That(migratedTask.GeneratedTargetId, Is.EqualTo("target-bedrock"));
            Assert.That(migratedTask.GeneratedActionId, Is.EqualTo("action-survey"));

            Assert.That(migrated.ResolveMail(migratedOffer.Id), Is.True);
            Assert.That(migrated.IsWorkVisible(migratedWork), Is.True);

            data.Balance.BaseSideMissionChance = 0;
            migrated.Crew[1].DailyOutput = 5f;
            Assert.That(migrated.Assign(migratedTask.Id, 1), Is.True);
            migrated.AdvanceDay();

            Assert.That(migratedTask.State, Is.EqualTo(TaskState.Complete));
            Assert.That(migrated.DiscoveredTaskWordIds, Is.EquivalentTo(new[]
            {
                "adjective-unstable", "target-bedrock", "action-survey"
            }));
            Assert.That(migrated.Codex.Exists(entry =>
                entry.Name == "암반" && entry.Description.Contains("추천 적성 역할: 분석")), Is.True);
            Assert.That(migrated.Codex.Exists(entry =>
                entry.Name == "불안정한" && entry.Description.Contains("역할: 위험도와 난이도 결정")), Is.True);

            var restored = new MilestoneSimulation(data, 99);
            Assert.That(restored.Restore(migrated.CreateSnapshot()), Is.True);
            Assert.That(restored.DiscoveredTaskWordIds, Is.EquivalentTo(migrated.DiscoveredTaskWordIds));
            Assert.That(restored.Codex.Exists(entry => entry.Name == "탐사"), Is.True);
        }

        [Test]
        public void ProposalSystemDoesNotGenerateCandidatesOrMail()
        {
            var game = new MilestoneSimulation(TaskSystemDataLoader.Load(), 24);

            Assert.That(game.ReadyMadeProposals, Is.Empty);
            Assert.That(game.NextProposalBatchDay, Is.Zero);
            Assert.That(game.Mail.Exists(mail => mail.IsProposal), Is.False);
            for (int day = 0; day < 30; day++) game.AdvanceDay();
            Assert.That(game.ReadyMadeProposals, Is.Empty);
            Assert.That(game.Mail.Exists(mail => mail.IsProposal), Is.False);
        }

        [Test]
        public void IncidentDoesNotRollWithoutAWorkedTaskCheckpoint()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 100;
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);

            game.AdvanceDay();

            Assert.That(game.Groups.Exists(work => work.Id.StartsWith("random-work-")), Is.False);
        }

        [Test]
        public void RestoreExpandsLegacySingleTaskSideMissionIntoHierarchy()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 100;
            data.Balance.RandomWorkChanceScalePercent = 100;
            var original = new MilestoneSimulation(data, 1);
            original.SetCompetencyAutoAssignment(false);
            Assert.That(original.Assign("survey", 1), Is.True);
            original.AdvanceDay();
            WorkGroup legacyWork = original.Groups.Find(group => group.Id.StartsWith("random-work-"));
            WorkTask legacyTask = original.Tasks.Find(task => task.GroupId == legacyWork.Id);
            int oldSoftDeadline = legacyWork.SoftDeadline;
            int oldHardDeadline = legacyWork.HardDeadline;
            MailEvent legacyOffer = original.Mail.Find(mail => mail.TargetWorkId == legacyWork.Id);
            legacyOffer.IsProposal = false;
            legacyOffer.IsBossRequest = false;
            legacyOffer.ProposalStage = ProposalStage.None;
            CampaignSnapshot snapshot = original.CreateSnapshot();
            snapshot.Tasks = System.Array.FindAll(snapshot.Tasks, task =>
                task.GroupId != legacyWork.Id || task.Id == legacyTask.Id);
            var restored = new MilestoneSimulation(data, 2);

            Assert.That(restored.Restore(snapshot), Is.True);

            WorkGroup migratedWork = restored.Groups.Find(group => group.Id == legacyWork.Id);
            List<WorkTask> migratedTasks = restored.Tasks.FindAll(task => task.GroupId == legacyWork.Id);
            Assert.That(migratedTasks, Has.Count.EqualTo(3));
            Assert.That(migratedTasks[1].PrerequisiteId, Is.EqualTo(migratedTasks[0].Id));
            Assert.That(migratedTasks[2].PrerequisiteId, Is.EqualTo(migratedTasks[1].Id));
            Assert.That(migratedWork.SoftDeadline, Is.EqualTo(oldSoftDeadline + 2));
            Assert.That(migratedWork.HardDeadline, Is.EqualTo(oldHardDeadline + 2));
            Assert.That(restored.Mail.Find(mail => mail.TargetWorkId == legacyWork.Id).Body,
                Does.Contain("3개 하위 일감"));
        }

        [Test]
        public void CompletedTaskRetainsAssigneeWithoutConsumingWorkerCapacity()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            data.Balance.RandomWorkLimit = 0;
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            WorkTask survey = game.Tasks.Find(task => task.Id == RootTaskId);
            Assert.That(game.Assign(survey.Id, 1), Is.True);

            while (survey.State != TaskState.Complete) game.AdvanceDay();

            Assert.That(survey.State, Is.EqualTo(TaskState.Complete));
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(game.Assign(DependentTaskBId, 1), Is.True);
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskBId).AssignedCharacter,
                Is.EqualTo(1));
        }

        [Test]
        public void ZeroRemainingSideMissionsDoNotForceAnInboundRequest()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 0;
            data.Balance.RandomWorkChanceScalePercent = 0;
            data.Balance.RandomWorkLimit = 3;
            data.Balance.RandomWorkDependencyChance = 0;
            var game = new MilestoneSimulation(data, 1);

            game.AdvanceDay();

            List<WorkGroup> generatedWorks = game.Groups.FindAll(group =>
                group.Id.StartsWith("random-work-"));
            int offerCount = game.Mail.FindAll(mail => mail.TargetWorkId != null &&
                mail.TargetWorkId.StartsWith("random-work-") &&
                mail.ArrivalDay == game.Day).Count;
            Assert.That(generatedWorks, Is.Empty);
            Assert.That(offerCount, Is.EqualTo(generatedWorks.Count));
            foreach (WorkGroup work in generatedWorks)
            {
                List<WorkTask> childTasks = game.Tasks.FindAll(task => task.GroupId == work.Id);
                Assert.That(childTasks, Has.Count.InRange(2, 4));
                Assert.That(work.AwaitingAcceptance, Is.True);
                Assert.That(game.IsWorkVisible(work), Is.False);
                Assert.That(childTasks[0].State, Is.EqualTo(TaskState.Locked));
                Assert.That(game.Mail.FindAll(mail => mail.TargetWorkId == work.Id), Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void DecliningIncidentWorkConsumesResourcesAndNeverAdmitsItToGantt()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 100;
            data.Balance.RandomWorkChanceScalePercent = 100;
            data.Balance.RandomWorkLimit = 3;
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);
            Assert.That(game.Assign("survey", 1), Is.True);

            game.AdvanceDay();

            MailEvent offer = game.PendingIncidentOffer;
            WorkGroup work = game.Groups.Find(group => group.Id == offer.TargetWorkId);
            int resources = game.Resources;
            Assert.That(game.DeclineIncidentWork(offer.Id), Is.True);
            Assert.That(game.Resources,
                Is.EqualTo(Math.Max(0, resources - work.HardPenaltyCredits)));
            Assert.That(work.State, Is.EqualTo(WorkState.Failed));
            Assert.That(work.AwaitingAcceptance, Is.True);
            Assert.That(game.IsWorkVisible(work), Is.False);
            Assert.That(game.HasPendingIncidentDecision, Is.False);
        }

        [Test]
        public void ScheduledTaskAutomaticallyAssignsAndStartsOnReservedDay()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 0;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");

            Assert.That(game.Schedule(survey.Id, 1, 2), Is.True);
            game.AdvanceDay();
            Assert.That(survey.Progress, Is.Zero);
            Assert.That(survey.ScheduledDay, Is.EqualTo(2));

            game.AdvanceDay();

            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(survey.Progress, Is.GreaterThan(0f));
            Assert.That(survey.ScheduledDay, Is.Zero);
            Assert.That(survey.ScheduledWorker, Is.EqualTo(-1));
        }

        [Test]
        public void StartedTaskContinuesWithoutRepeatedAssignment()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            Array.Find(data.Tasks, task => task.Id == "survey").RequiredWork = 4f;
            var game = new MilestoneSimulation(data, 1);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            Assert.That(game.Assign(survey.Id, 1), Is.True);

            game.AdvanceDay();
            float firstDayProgress = survey.Progress;
            game.AdvanceDay();

            Assert.That(survey.Progress, Is.GreaterThan(firstDayProgress));
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
        }

        [Test]
        public void ReschedulingActiveTaskHoldsItUntilNewStartDay()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            game.Assign(survey.Id, 1);
            game.AdvanceDay();
            float heldProgress = survey.Progress;

            Assert.That(game.Schedule(survey.Id, 1, 3), Is.True);
            Assert.That(survey.AssignedCharacter, Is.EqualTo(-1));
            Assert.That(survey.ContextCostDays, Is.EqualTo(1f));
            game.AdvanceDay();
            Assert.That(survey.Progress, Is.EqualTo(heldProgress));
            game.AdvanceDay();

            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(survey.Progress, Is.GreaterThan(heldProgress));
        }

        [Test]
        public void IdleWorkerScheduleStartsTodayAndDividesWorkByExpectedOutput()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);

            TaskScheduleEstimate estimate = game.EstimateSchedule(RootTaskId, 1);

            Assert.That(estimate.StartDay, Is.EqualTo(game.Day));
            Assert.That(estimate.ExpectedDailyOutput, Is.EqualTo(1.725f).Within(.001f));
            Assert.That(estimate.DurationDays, Is.EqualTo(2));
            Assert.That(estimate.CompletionDay, Is.EqualTo(2));
            Assert.That(estimate.RollingStart, Is.False);
        }

        [Test]
        public void AssignedBlockerAndBusyWorkerPushSuccessorAfterExpectedCompletion()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);
            Assert.That(game.Assign(RootTaskId, 1), Is.True);

            TaskScheduleEstimate estimate = game.EstimateSchedule(DependentTaskBId, 1);

            Assert.That(estimate.StartDay, Is.EqualTo(3));
            Assert.That(estimate.CompletionDay, Is.EqualTo(8));
            Assert.That(estimate.RollingStart, Is.False);
        }

        [Test]
        public void UnassignedBlockerUsesBaselinePreviewAndMovesWithCurrentDay()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);

            TaskScheduleEstimate today = game.EstimateSchedule(DependentTaskBId, 0);
            game.AdvanceDay();
            TaskScheduleEstimate nextDay = game.EstimateSchedule(DependentTaskBId, 0);

            Assert.That(today.StartDay, Is.EqualTo(4));
            Assert.That(today.RollingStart, Is.False);
            Assert.That(nextDay.StartDay, Is.EqualTo(5));
            Assert.That(nextDay.RollingStart, Is.False);
        }

        [Test]
        public void FasterWorkerProducesShorterExpectedDuration()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            data.Crew[1].DailyOutput = 2f;
            var game = new MilestoneSimulation(data, 1);

            TaskScheduleEstimate estimate = game.EstimateSchedule(RootTaskId, 1);

            Assert.That(estimate.ExpectedDailyOutput, Is.EqualTo(3.45f).Within(.001f));
            Assert.That(estimate.DurationDays, Is.EqualTo(1));
            Assert.That(estimate.CompletionDay, Is.EqualTo(1));
        }

        [Test]
        public void UnassignedTasksPreviewAtOneWorkPerDayInDependencyOrder()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);

            TaskScheduleEstimate survey = game.EstimatePreviewSchedule(RootTaskId);
            TaskScheduleEstimate dependentA = game.EstimatePreviewSchedule(DependentTaskAId);
            TaskScheduleEstimate dependentB = game.EstimatePreviewSchedule(DependentTaskBId);
            TaskScheduleEstimate optional = game.EstimatePreviewSchedule(OptionalTaskId);

            Assert.That(survey.ExpectedDailyOutput, Is.EqualTo(1f));
            Assert.That(survey.StartDay, Is.EqualTo(1));
            Assert.That(survey.CompletionDay, Is.EqualTo(3));
            Assert.That(dependentA.StartDay, Is.EqualTo(4));
            Assert.That(dependentA.CompletionDay, Is.EqualTo(7));
            Assert.That(dependentB.StartDay, Is.EqualTo(4));
            Assert.That(optional.StartDay, Is.EqualTo(8));
        }

        [Test]
        public void WorkPredecessorPreviewStartsAfterLatestRequiredTask()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);

            TaskScheduleEstimate followup = game.EstimatePreviewSchedule(FollowupTaskId);

            Assert.That(followup.StartDay, Is.EqualTo(8));
            Assert.That(followup.CompletionDay, Is.EqualTo(10));
        }

        [Test]
        public void AssignedPredecessorOutputMovesUnassignedSuccessorPreview()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            data.Crew[1].DailyOutput = 2f;
            var game = new MilestoneSimulation(data, 1);
            game.Assign(RootTaskId, 1);

            TaskScheduleEstimate dependent = game.EstimatePreviewSchedule(DependentTaskBId);

            Assert.That(dependent.StartDay, Is.EqualTo(2));
            Assert.That(dependent.CompletionDay, Is.EqualTo(5));
        }

        [Test]
        public void WorkerCannotHaveTwoPrimaryReservationsOnSameDay()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);

            Assert.That(game.Schedule(RootTaskId, 0, 3), Is.True);
            Assert.That(game.Schedule(DependentTaskBId, 0, 3), Is.False);
            Assert.That(game.Schedule(DependentTaskBId, 1, 3), Is.True);
        }

        [Test]
        public void TaskScheduleSurvivesCampaignSnapshot()
        {
            var original = new MilestoneSimulation(1);
            Assert.That(original.Schedule("survey", 1, 4), Is.True);

            var restored = new MilestoneSimulation(99);
            Assert.That(restored.Restore(original.CreateSnapshot()), Is.True);
            WorkTask survey = restored.Tasks.Find(task => task.Id == "survey");

            Assert.That(survey.ScheduledDay, Is.EqualTo(4));
            Assert.That(survey.ScheduledWorker, Is.EqualTo(1));
        }

        [Test]
        public void CompletingPrerequisiteUnlocksFollowingTasks()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);
            game.Assign(RootTaskId, 1);
            game.AdvanceDay();
            game.AdvanceDay();
            game.AdvanceDay();
            Assert.That(game.Tasks.Find(t => t.Id == DependentTaskAId).State, Is.EqualTo(TaskState.Available));
            Assert.That(game.Tasks.Find(t => t.Id == DependentTaskBId).State, Is.EqualTo(TaskState.Available));
        }

        [Test]
        public void RegenerationConsumesResourcesAndResetsFatigue()
        {
            var game = new MilestoneSimulation(1);
            game.Assign("survey", 1);
            game.AdvanceDay();
            game.Crew[1].Experience = 25;
            int before = game.Resources;
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            Assert.That(game.Regenerate(1), Is.True);
            Assert.That(game.Crew[1].Fatigue, Is.Zero);
            Assert.That(game.Crew[1].Experience, Is.Zero);
            Assert.That(game.CurrentBaseSalary(game.Crew[1]), Is.EqualTo(1));
            Assert.That(game.Resources, Is.EqualTo(before - 3));
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(survey.ContextCostDays, Is.Zero);
        }

        [Test]
        public void RegenerationInheritanceTradesUpfrontResourcesForRetainedAbilityAndPerks()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.StartingResources = 30;
            data.Balance.RegenerationPersonalityRetentionWeight = 100;
            var game = new MilestoneSimulation(data, 1);
            CrewMember member = game.Crew[0];
            member.Skill = 7;
            member.Competencies = new[] { 7, 7, 7, 7, 7, 7 };
            member.Perks = new[] { "현장 성장 퍽" };
            member.Experience = 40;
            string personality = member.Personality;

            Assert.That(game.RegenerationCost(true, true), Is.EqualTo(12));
            Assert.That(game.Regenerate(0, true, true), Is.True);

            Assert.That(game.Resources, Is.EqualTo(18));
            Assert.That(member.Skill, Is.EqualTo(7));
            Assert.That(member.Competencies, Is.All.EqualTo(7));
            Assert.That(member.Perks, Is.EqualTo(new[] { "현장 성장 퍽" }));
            Assert.That(member.Experience, Is.Zero);
            Assert.That(game.CurrentBaseSalary(member), Is.EqualTo(1));
            Assert.That(member.Personality, Is.EqualTo(personality));
        }

        [Test]
        public void RegenerationWithoutInheritanceRestoresBaselineAndRerollsPersonality()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.RegenerationPersonalityRetentionWeight = 0;
            int baselineSkill = data.Crew[0].Skill;
            int[] baselineCompetencies = (int[])data.Crew[0].Competencies.Clone();
            string[] baselinePerks = (string[])data.Crew[0].Perks.Clone();
            var game = new MilestoneSimulation(data, 1);
            CrewMember member = game.Crew[0];
            string previousPersonality = member.Personality;
            member.Skill = 1;
            member.Competencies = new[] { 1, 1, 1, 1, 1, 1 };
            member.Perks = new[] { "임시 퍽" };

            Assert.That(game.Regenerate(0, false, false), Is.True);

            Assert.That(member.Skill, Is.EqualTo(baselineSkill));
            Assert.That(member.Competencies, Is.EqualTo(baselineCompetencies));
            Assert.That(member.Perks, Is.EqualTo(baselinePerks));
            Assert.That(member.Personality, Is.Not.EqualTo(previousPersonality));
        }

        [Test]
        public void CareerRaisesBaseSalaryAndPayrollContinuouslyConsumesResources()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.StartingResources = 100;
            data.Balance.RandomWorkLimit = 0;
            data.Balance.PayrollIntervalDays = 2;
            data.Balance.BaseSalary = 1;
            data.Balance.ExperiencePerSalaryIncrease = 2;
            data.Balance.SalaryIncrease = 1;
            ForceSuccessOutcome(data);
            var game = new MilestoneSimulation(data, 1);
            Assert.That(game.Assign("survey", 0), Is.True);

            game.AdvanceDay();
            Assert.That(game.Resources, Is.EqualTo(100));
            game.AdvanceDay();

            Assert.That(game.Crew[0].Experience, Is.EqualTo(2));
            Assert.That(game.CurrentBaseSalary(game.Crew[0]), Is.EqualTo(2));
            int expectedPayroll = MilestoneSimulation.TeamSize + 1;
            Assert.That(game.TotalPayroll(), Is.EqualTo(expectedPayroll));
            Assert.That(game.Resources, Is.EqualTo(100 - expectedPayroll));
            Assert.That(game.LastReport.Lines,
                Has.Some.Contains($"기본급 합계 -{expectedPayroll}자원"));
        }

        [Test]
        public void RestConsumesNextDayInsteadOfRecoveringImmediately()
        {
            var game = new MilestoneSimulation(1);
            game.Assign("survey", 1);
            game.AdvanceDay();
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            float progress = survey.Progress;
            int fatigue = game.Crew[1].Fatigue;
            Assert.That(game.Rest(1), Is.True);
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(fatigue));
            Assert.That(game.Rest(1), Is.False);
            game.AdvanceDay();
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(0));
            Assert.That(survey.Progress, Is.EqualTo(progress));
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(survey.ContextCostDays, Is.Zero);
        }

        [Test]
        public void InjuryPausesWorkWithoutRemovingItsOwner()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.HighFatigueAccidentChance = 100;
            data.Balance.MediumFatigueAccidentChance = 100;
            data.Balance.MismatchAccidentChance = 0;
            data.Balance.BaseSideMissionChance = 0;
            data.Crew[1].Fatigue = 80;
            var game = new MilestoneSimulation(data, 1);
            game.AdjustCrewFatigueLimit(1, 100);
            Assert.That(game.Assign("survey", 1), Is.True);

            game.AdvanceDay();
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            float accidentProgress = survey.Progress;
            Assert.That(game.Crew[1].InjuryDays, Is.GreaterThan(0));
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(survey.ContextCostDays, Is.Zero);

            game.AdvanceDay();

            Assert.That(survey.Progress, Is.EqualTo(accidentProgress));
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(survey.ContextCostDays, Is.Zero);
            Assert.That(game.LastReport.Lines, Has.Some.Contains("보류"));
        }

        [Test]
        public void MessengerStatusQuestionAddsOneCombinedConversationItem()
        {
            var game = new MilestoneSimulation(1);
            CrewMember worker = game.Crew[0];
            int historyCount = worker.History.Count;

            Assert.That(game.AskWorker(0, "status"), Is.True);

            Assert.That(worker.History, Has.Count.EqualTo(historyCount + 1));
            Assert.That(worker.History[worker.History.Count - 1], Does.Contain(worker.Name));
            Assert.That(worker.History[worker.History.Count - 1], Does.Contain("[나]"));
            Assert.That(worker.History[worker.History.Count - 1], Does.Contain("피로도"));
        }

        [Test]
        public void MessengerRepliesExpressTheSameSituationByPersonality()
        {
            var game = new MilestoneSimulation(1);
            game.Crew[0].Fatigue = 60;
            game.Crew[1].Fatigue = 60;

            string principled = game.BuildWorkerStatusReply(0);
            string analytical = game.BuildWorkerStatusReply(1);

            Assert.That(principled, Does.Contain("절차에 따라"));
            Assert.That(analytical, Does.Contain("수치와 정황"));
            Assert.That(principled, Does.Contain("피로도 60%"));
            Assert.That(analytical, Does.Contain("피로도 60%"));
            Assert.That(principled, Is.Not.EqualTo(analytical));
        }

        [Test]
        public void MessengerWorkQuestionReportsAssignedTaskProgress()
        {
            var game = new MilestoneSimulation(1);
            WorkTask task = game.Tasks.Find(candidate => candidate.Id == "survey");
            Assert.That(game.Assign(task.Id, 1), Is.True);

            string reply = game.BuildWorkerWorkReply(1);

            Assert.That(reply, Does.Contain(task.Name));
            Assert.That(reply, Does.Contain("진척도"));
        }

        [Test]
        public void PatchChannelCacheBusterPreservesExistingQueryString()
        {
            Assert.That(PatchBootstrapper.AddCacheBuster("https://example.test/channel.json", 123),
                Is.EqualTo("https://example.test/channel.json?projectw_nocache=123"));
            Assert.That(PatchBootstrapper.AddCacheBuster("https://example.test/channel.json?branch=dev", 456),
                Is.EqualTo("https://example.test/channel.json?branch=dev&projectw_nocache=456"));
        }

        [Test]
        public void RequestDiagnosticIncludesHttpCodeSizeAndError()
        {
            string result = PatchBootstrapper.FormatRequestResult("GET", "https://example.test/channel.json", 403, "Forbidden", 27);

            Assert.That(result, Does.Contain("HTTP 403"));
            Assert.That(result, Does.Contain("bytes=27"));
            Assert.That(result, Does.Contain("Forbidden"));
            Assert.That(result, Does.Contain("https://example.test/channel.json"));
        }

        [Test]
        public void HotUpdateManifestFileNameIncludesDllExtension()
        {
            Assert.That(PatchBootstrapper.GetHotUpdateFileName("ProjectW.HotUpdate"),
                Is.EqualTo("ProjectW.HotUpdate.dll.bytes"));
        }

        [Test]
        public void BaseVersionMatchesCurrentAotBaseline()
        {
            Assert.That(PatchBootstrapper.BaseVersion, Is.EqualTo(17));
        }

        [Test]
        public void GameplayDataOverrideRoundTripsAndCanBeRemoved()
        {
            var storage = new MemoryStringStorage();
            TaskSystemDataLoader.Configure(null, storage);
            try
            {
                TaskSystemData edited = TaskSystemDataLoader.Load();
                int original = edited.StartingResources;
                edited.StartingResources = original + 7;
                TaskSystemDataLoader.SaveOverride(edited);
                Assert.That(TaskSystemDataLoader.Load().StartingResources, Is.EqualTo(original + 7));
                TaskSystemDataLoader.DeleteOverride();
                Assert.That(TaskSystemDataLoader.Load().StartingResources, Is.EqualTo(original));
            }
            finally { TaskSystemDataLoader.Configure(null, null); }
        }

        [Test]
        public void InvalidGameplayDataCannotReplaceLastValidOverride()
        {
            var storage = new MemoryStringStorage();
            TaskSystemDataLoader.Configure(null, storage);
            try
            {
                TaskSystemData valid = TaskSystemDataLoader.Load();
                TaskSystemDataLoader.SaveOverride(valid);
                valid.SchemaVersion = 999;
                Assert.Throws<InvalidOperationException>(() => TaskSystemDataLoader.SaveOverride(valid));
                Assert.That(TaskSystemDataLoader.Load().SchemaVersion, Is.EqualTo(TaskSystemDataLoader.SupportedSchema));
            }
            finally { TaskSystemDataLoader.Configure(null, null); }
        }

        [Test]
        public void CategoryTransferCopiesAndAppliesWholeCharacterCategory()
        {
            TaskSystemData source = TaskSystemDataLoader.Load();
            string json = TaskSystemDataCategoryTransfer.Copy(source, TaskSystemDataCategoryTransfer.Characters);
            TaskSystemData transfer = TaskSystemDataLoader.ParseUnchecked(json);
            transfer.Crew[0].Name = "Clipboard Crew";
            json = JsonUtility.ToJson(transfer, true);

            Assert.That(TaskSystemDataCategoryTransfer.TryApply(source,
                TaskSystemDataCategoryTransfer.Characters, json, out TaskSystemData updated, out string error), Is.True, error);
            Assert.That(updated.Crew[0].Name, Is.EqualTo("Clipboard Crew"));
            Assert.That(source.Crew[0].Name, Is.Not.EqualTo("Clipboard Crew"));
            Assert.That(updated.Balance.BaseSideMissionChance, Is.EqualTo(source.Balance.BaseSideMissionChance));
        }

        [Test]
        public void CategoryTransferRejectsWrongCategoryWithoutChangingWorkingCopy()
        {
            TaskSystemData source = TaskSystemDataLoader.Load();
            string mailJson = TaskSystemDataCategoryTransfer.Copy(source, TaskSystemDataCategoryTransfer.Mail);

            Assert.That(TaskSystemDataCategoryTransfer.TryApply(source,
                TaskSystemDataCategoryTransfer.Characters, mailJson, out TaskSystemData updated, out string error), Is.False);
            Assert.That(error, Does.Contain("Expected category 'Characters'"));
            Assert.That(updated, Is.SameAs(source));
        }

        [Test]
        public void CategoryTransferRejectsInvalidWholeDocumentRelationship()
        {
            TaskSystemData source = TaskSystemDataLoader.Load();
            TaskSystemData transfer = TaskSystemDataLoader.ParseUnchecked(
                TaskSystemDataCategoryTransfer.Copy(source, TaskSystemDataCategoryTransfer.CriticalEvents));
            transfer.CriticalEvents[0].FirstNodeId = "missing-node";
            string json = JsonUtility.ToJson(transfer, true);

            Assert.That(TaskSystemDataCategoryTransfer.TryApply(source,
                TaskSystemDataCategoryTransfer.CriticalEvents, json, out TaskSystemData updated, out string error), Is.False);
            Assert.That(error, Does.Contain("first node"));
            Assert.That(updated, Is.SameAs(source));
        }

        [Test]
        public void ResolvingMailAppliesItsRuleOnlyOnce()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Mail = new[]
            {
                new MailEvent
                {
                    Id = "mail-1", ArrivalDay = 1, From = "현장 운영실",
                    Subject = "일정 조정", Instruction = "마감을 하루 앞당깁니다.",
                    TargetWorkId = "foundation", DeadlineDelta = -1
                }
            };
            data.StakeholderRequests = new StakeholderRequestDefinition[0];
            var game = new MilestoneSimulation(data, 1);
            WorkGroup foundation = game.Groups.Find(group => group.Id == "foundation");
            int softDeadline = foundation.SoftDeadline;
            int hardDeadline = foundation.HardDeadline;

            MailEvent weekly = game.Mail.Find(mail => mail.IsWeeklyFieldReport);
            Assert.That(weekly, Is.Not.Null);
            Assert.That(weekly.Subject, Is.EqualTo("주간현장 현황공유"));
            Assert.That(game.Mail.Exists(mail => mail.Id == "mail-1"), Is.False);
            Assert.That(game.ResolveMail("mail-1"), Is.False);
            Assert.That(game.DecideWeeklyFieldItem(weekly.Id, "mail-1", true), Is.True);
            Assert.That(foundation.SoftDeadline, Is.EqualTo(softDeadline - 1));
            Assert.That(foundation.HardDeadline, Is.EqualTo(hardDeadline - 1));
            WorkTask survey = game.Tasks.Find(t => t.Id == "survey");
            Assert.That(survey.Importance, Is.EqualTo(ImportanceLevel.High));
            Assert.That(survey.Records, Has.Count.EqualTo(1));
            Assert.That(game.DecideWeeklyFieldItem(weekly.Id, "mail-1", true), Is.False);
            Assert.That(foundation.HardDeadline, Is.EqualTo(hardDeadline - 1));
        }

        [Test]
        public void WeeklyFieldIncidentCanBeIgnoredWithoutChangingSchedule()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Mail = new[]
            {
                new MailEvent
                {
                    Id = "mail-1", ArrivalDay = 1, From = "현장 운영실",
                    Subject = "일정 조정", Instruction = "마감을 하루 앞당깁니다.",
                    TargetWorkId = "foundation", DeadlineDelta = -1
                }
            };
            data.StakeholderRequests = new StakeholderRequestDefinition[0];
            var game = new MilestoneSimulation(data, 1);
            WorkGroup foundation = game.Groups.Find(group => group.Id == "foundation");
            int softDeadline = foundation.SoftDeadline;
            int hardDeadline = foundation.HardDeadline;
            MailEvent weekly = game.Mail.Find(mail => mail.IsWeeklyFieldReport);

            Assert.That(game.DecideWeeklyFieldItem(weekly.Id, "mail-1", false), Is.True);
            Assert.That(foundation.SoftDeadline, Is.EqualTo(softDeadline));
            Assert.That(foundation.HardDeadline, Is.EqualTo(hardDeadline));
            Assert.That(weekly.Resolved, Is.True);
        }

        [Test]
        public void ScheduleIncidentsWaitForMondayDigestWhileMissionMailStaysSeparate()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 100;
            data.Balance.RandomWorkChanceScalePercent = 100;
            data.Balance.RandomWorkLimit = 1;
            data.Mail = new[]
            {
                new MailEvent
                {
                    Id = "mail-4", ArrivalDay = 6, From = "현장 운영실",
                    Subject = "일정 조정", Instruction = "마감을 하루 앞당깁니다.",
                    TargetWorkId = "foundation", DeadlineDelta = -1
                }
            };
            data.StakeholderRequests = new StakeholderRequestDefinition[0];
            var game = new MilestoneSimulation(data, 1);

            while (game.Day < 8) game.AdvanceDay();

            MailEvent weekly = game.Mail.Find(mail => mail.Id == "weekly-field-8");
            Assert.That(weekly, Is.Not.Null);
            Assert.That(weekly.WeeklyFieldItems, Has.Length.EqualTo(1));
            Assert.That(weekly.WeeklyFieldItems[0].SourceMailId, Is.EqualTo("mail-4"));
            Assert.That(game.Mail.Exists(mail => mail.Id == "mail-4"), Is.False);
            Assert.That(game.Mail.Exists(mail => mail.IsBossRequest && !mail.IsWeeklyFieldReport), Is.True);
        }

        [Test]
        public void FutureMailCannotBeResolvedEarly()
        {
            var game = new MilestoneSimulation(1);
            Assert.That(game.ResolveMail("mail-2"), Is.False);
        }

        [Test]
        public void UnreadMailCountOnlyIncludesVisibleInboxMail()
        {
            var game = new MilestoneSimulation(1);
            game.Mail.Clear();
            game.Mail.Add(new MailEvent { Id = "visible", ArrivalDay = game.Day, Read = false });
            game.Mail.Add(new MailEvent { Id = "future", ArrivalDay = game.Day + 1, Read = false });
            game.Mail.Add(new MailEvent { Id = "read", ArrivalDay = game.Day, Read = true });

            Assert.That(game.UnreadMailCount(), Is.EqualTo(1));
            game.MarkMailRead("visible");
            Assert.That(game.UnreadMailCount(), Is.Zero);
        }

        [Test]
        public void SnapshotRestoresCampaignState()
        {
            var original = new MilestoneSimulation(1);
            original.Assign("survey", 1);
            original.AdvanceDay();
            MailEvent request = original.Mail.Find(mail => mail.Id == "mail-stakeholder-landing");
            request.Read = true;
            request.Resolved = true;

            var restored = new MilestoneSimulation(99);
            Assert.That(restored.Restore(original.CreateSnapshot()), Is.True);
            Assert.That(restored.Day, Is.EqualTo(original.Day));
            Assert.That(restored.Resources, Is.EqualTo(original.Resources));
            Assert.That(restored.Tasks.Find(t => t.Id == "survey").Progress,
                Is.EqualTo(original.Tasks.Find(t => t.Id == "survey").Progress));
            Assert.That(restored.Groups.Find(group => group.Id == "foundation").HardDeadline,
                Is.EqualTo(original.Groups.Find(group => group.Id == "foundation").HardDeadline));
            Assert.That(restored.Mail.Find(m => m.Id == request.Id).Resolved, Is.True);
        }

        [Test]
        public void LegacySnapshotBackfillsTaskCompetencyRequirements()
        {
            var original = new MilestoneSimulation(1);
            CampaignSnapshot snapshot = original.CreateSnapshot();
            foreach (WorkTask task in snapshot.Tasks) task.RequiredCompetencies = null;
            var restored = new MilestoneSimulation(2);

            Assert.That(restored.Restore(snapshot), Is.True);
            foreach (WorkTask task in restored.Tasks)
                Assert.That(task.RequiredCompetencies, Has.Length.InRange(1, 3));
        }

        [Test]
        public void WorkPredecessorLocksFollowupUntilPlanningWorkCompletes()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);

            Assert.That(game.Groups.Find(group => group.Id == FollowupWorkId).State, Is.EqualTo(WorkState.Locked));
            Assert.That(game.Tasks.Find(task => task.Id == FollowupTaskId).State, Is.EqualTo(TaskState.Locked));
            Assert.That(game.Assign(FollowupTaskId, 0), Is.False);
        }

        [Test]
        public void FollowupWorkBecomesVisibleOnRevealDay()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            data.Balance.BaseSideMissionChance = 0;
            var original = new MilestoneSimulation(data, 1);
            CampaignSnapshot snapshot = original.CreateSnapshot();
            snapshot.Day = 59;
            foreach (WorkTask task in snapshot.Tasks)
                if (task.GroupId == PlanningWorkId) task.State = TaskState.Complete;
            System.Array.Find(snapshot.Groups, group => group.Id == PlanningWorkId).State =
                WorkState.Complete;
            var game = new MilestoneSimulation(data, 2);
            Assert.That(game.Restore(snapshot), Is.True);
            WorkGroup finalWork = game.Groups.Find(group => group.Id == FollowupWorkId);

            Assert.That(game.IsWorkVisible(finalWork), Is.False);
            Assert.That(game.Tasks.Find(task => task.Id == FollowupTaskId).State, Is.EqualTo(TaskState.Locked));
            game.AdvanceDay();

            Assert.That(game.Day, Is.EqualTo(60));
            Assert.That(game.IsWorkVisible(finalWork), Is.True);
            Assert.That(game.Tasks.Find(task => task.Id == FollowupTaskId).State,
                Is.EqualTo(TaskState.Available));
        }

        [Test]
        public void FailedAutomaticallyAddedSideMissionCostsResourcesWithoutGameOver()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 100;
            data.Balance.RandomWorkChanceScalePercent = 100;
            data.Balance.RandomWorkLimit = 1;
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);
            Assert.That(game.Assign("survey", 1), Is.True);
            game.AdvanceDay();
            WorkGroup sideMission = game.Groups.Find(group => group.Id.StartsWith("random-work-"));
            MailEvent offer = game.Mail.Find(mail => mail.TargetWorkId == sideMission.Id);
            Assert.That(game.ResolveMail(offer.Id), Is.True);
            data.Balance.BaseSideMissionChance = 0;
            sideMission.SoftDeadline = game.Day;
            sideMission.HardDeadline = game.Day;
            int resourcesBeforeFailure = game.Resources;
            int offersBeforeFailure = game.Mail.FindAll(mail =>
                mail.TargetWorkId != null && mail.TargetWorkId.StartsWith("random-work-")).Count;

            game.AdvanceDay();

            Assert.That(sideMission.State, Is.EqualTo(WorkState.Failed));
            Assert.That(game.Resources, Is.EqualTo(resourcesBeforeFailure -
                sideMission.SoftPenaltyCredits - sideMission.HardPenaltyCredits));
            Assert.That(game.IsLost, Is.False);
            Assert.That(game.Mail.FindAll(mail =>
                    mail.TargetWorkId != null && mail.TargetWorkId.StartsWith("random-work-")),
                Has.Count.EqualTo(offersBeforeFailure));
        }

        [Test]
        public void WorkerCanHoldOnlyOnePrimaryTask()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);
            CompleteRootTask(game);
            Assert.That(game.Assign(DependentTaskAId, 0), Is.True);
            Assert.That(game.Assign(DependentTaskBId, 0), Is.True);

            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskAId).AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskBId).AssignedCharacter, Is.EqualTo(0));
        }

        [Test]
        public void SmallTaskCanRunInParallelForAdditionalFatigue()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            data.Balance.RandomWorkLimit = 0;
            foreach (CrewMember member in data.Crew) member.DailyOutput = 1f;
            ForceSuccessOutcome(data);
            var game = new MilestoneSimulation(data, 1);
            CompleteRootTask(game);
            Assert.That(game.Assign(DependentTaskAId, 0), Is.True);
            WorkTask optional = game.Tasks.Find(task => task.Id == OptionalTaskId);
            optional.PrerequisiteId = null;
            optional.RequiredWork = .5f;
            optional.State = TaskState.Available;

            Assert.That(game.AssignParallel(OptionalTaskId, 0), Is.True);
            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == DependentTaskAId).Progress, Is.EqualTo(1.5f));
            Assert.That(optional.State, Is.EqualTo(TaskState.Complete));
            Assert.That(game.Crew[0].Fatigue, Is.EqualTo(17));
        }

        [Test]
        public void InterruptingTaskAddsOneDayContextCost()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            data.Balance.RandomWorkLimit = 0;
            foreach (CrewMember member in data.Crew) member.DailyOutput = 1f;
            ForceSuccessOutcome(data);
            var game = new MilestoneSimulation(data, 1);
            CompleteRootTask(game);
            WorkTask dependent = game.Tasks.Find(task => task.Id == DependentTaskBId);
            Assert.That(game.Assign(dependent.Id, 0), Is.True);
            game.AdvanceDay();

            Assert.That(game.Assign(dependent.Id, -1), Is.True);
            Assert.That(dependent.Progress, Is.EqualTo(1.25f));
            Assert.That(dependent.ContextCostDays, Is.EqualTo(1f));
            Assert.That(dependent.EffectiveRequiredWork, Is.EqualTo(dependent.RequiredWork + 1f));
            Assert.That(dependent.SplitCount, Is.EqualTo(1));
        }

        [Test]
        public void HandoverChargesExactlyOneSplit()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 1);
            CompleteRootTask(game);
            WorkTask dependent = game.Tasks.Find(task => task.Id == DependentTaskBId);
            game.Assign(dependent.Id, 0);
            game.AdvanceDay();

            Assert.That(game.Assign(dependent.Id, 3), Is.True);

            Assert.That(dependent.AssignedCharacter, Is.EqualTo(3));
            Assert.That(dependent.ContextCostDays, Is.EqualTo(1f));
            Assert.That(dependent.SplitCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingHardDeadlineFailsRequiredWorkWithoutEndingTheRun()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.StartingResources = 100;
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);
            int hardDeadline = game.Groups.Find(group => group.Id == "foundation").HardDeadline;
            while (game.Day <= hardDeadline) game.AdvanceDay();

            Assert.That(game.Groups.Find(group => group.Id == "foundation").State, Is.EqualTo(WorkState.Failed));
            Assert.That(game.IsLost, Is.False);
        }

        [Test]
        public void GameplayDefinitionsLoadFromExternalJsonResource()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();

            Assert.That(data.Works, Is.Not.Empty);
            Assert.That(data.Tasks, Is.Not.Empty);
            Assert.That(data.Balance.InterruptionCostDays, Is.EqualTo(.5f));
            Assert.That(data.Balance.ResumptionCostDays, Is.EqualTo(.5f));
        }

        [Test]
        public void TaskCostPreviewShowsHandoverBeforeReassignment()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);
            CompleteRootTask(game);
            WorkTask dependent = game.Tasks.Find(task => task.Id == DependentTaskBId);
            game.Assign(dependent.Id, 1);
            game.AdvanceDay();

            TaskCostPreview preview = game.BuildCostPreview(dependent, 0);

            Assert.That(preview.RemainingDays, Is.EqualTo(dependent.RemainingWork));
            Assert.That(preview.AdditionalContextDays, Is.EqualTo(1f));
            Assert.That(preview.PrimaryFatigue, Is.EqualTo(4));
            Assert.That(preview.ParallelFatigue, Is.EqualTo(9));
            Assert.That(preview.CanRunInParallel, Is.True);
        }

        [Test]
        public void PrioritySchedulePlacesDependentTasksAfterTheirForecastPredecessor()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            var game = new MilestoneSimulation(data, 41);
            WorkTask survey = game.Tasks.Find(task => task.Id == RootTaskId);
            WorkTask dependent = game.Tasks.Find(task => task.Id == DependentTaskBId);

            TaskScheduleEstimate[] schedule = game.BuildPrioritySchedule();
            TaskScheduleEstimate surveyPlan = Array.Find(schedule, item => item.TaskId == survey.Id);
            TaskScheduleEstimate dependentPlan = Array.Find(schedule, item => item.TaskId == dependent.Id);

            Assert.That(surveyPlan, Is.Not.Null);
            Assert.That(dependentPlan, Is.Not.Null);
            Assert.That(dependentPlan.StartDay, Is.GreaterThan(surveyPlan.CompletionDay));
        }

        [Test]
        public void OperationsReportCountsDynamicRiskAndLoad()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            Array.Find(data.Tasks, task => task.Id == "survey").Risk = RiskLevel.High;
            var game = new MilestoneSimulation(data, 1);
            game.Assign("survey", 0);
            for (int i = 0; i < 4; i++) game.AdvanceDay();

            OperationsReport report = game.BuildReport();
            Assert.That(report.Active + report.Complete + report.Available + report.Locked, Is.EqualTo(game.Tasks.Count));
            Assert.That(report.HighRisk, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void CampaignPlayerPrefsRoundTripsAndRejectsCorruptJson()
        {
            string key = $"projectw.test.{Guid.NewGuid():N}";
            var storage = new MemoryStringStorage();
            ProjectWSaveStore.Configure(storage);
            try
            {
                var game = new MilestoneSimulation(1);
                MailEvent request = game.Mail.Find(mail => mail.Id == "mail-stakeholder-landing");
                request.Read = true;
                request.Resolved = true;
                Assert.That(ProjectWSaveStore.SaveCampaign(key, game.CreateSnapshot()), Is.True);
                Assert.That(ProjectWSaveStore.TryLoadCampaign(key, out CampaignSnapshot loaded), Is.True);
                Assert.That(Array.Find(loaded.Mail, mail => mail.Id == request.Id).Resolved, Is.True);

                storage.SetString(key, "{not-json");
                Assert.That(ProjectWSaveStore.TryLoadCampaign(key, out _), Is.False);
            }
            finally
            {
                ProjectWSaveStore.Delete(key);
            }
        }

        private sealed class MemoryStringStorage : IStringStorage
        {
            private readonly Dictionary<string, string> values = new Dictionary<string, string>();

            public bool TryGetString(string key, out string value) => values.TryGetValue(key, out value);
            public void SetString(string key, string value) => values[key] = value;
            public void DeleteKey(string key) => values.Remove(key);
        }

        [Test]
        public void UnscheduledCheckupConsumesResourceStopsWorkAndUpdatesOnlyAfterMondayDownload()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            ForceSuccessOutcome(data);
            var game = new MilestoneSimulation(data, 11);
            Assert.That(game.Assign("survey", 1), Is.True);
            int resources = game.Resources;

            Assert.That(game.SendForMedicalCheckup(1), Is.True);
            Assert.That(game.Resources, Is.EqualTo(resources - data.Balance.UnscheduledCheckupResourceCost));
            game.AdvanceDay();
            Assert.That(game.Tasks.Find(task => task.Id == "survey").Progress, Is.Zero);

            while (game.Day < 8) game.AdvanceDay();
            MailEvent resultMail = game.Mail.Find(mail => mail.IsMedicalReport && mail.ArrivalDay == 8);
            Assert.That(resultMail, Is.Not.Null);
            Assert.That(game.Crew[1].MedicalFileUpdatedDay, Is.Zero);
            Assert.That(game.ResolveMail(resultMail.Id), Is.True);
            Assert.That(game.Crew[1].MedicalFileUpdatedDay, Is.EqualTo(8));
            Assert.That(game.Crew[1].MedicalFileMental, Is.EqualTo(70));
        }

        [Test]
        public void RegularFridayCheckupIsFreeAndProducesHalfDayOutput()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            ForceSuccessOutcome(data);
            foreach (WorkGroup work in data.Works)
            {
                work.SoftDeadline = 180;
                work.HardDeadline = 200;
            }
            foreach (WorkTask task in data.Tasks) task.Deadline = 200;
            var game = new MilestoneSimulation(data, 12);
            CampaignSnapshot snapshot = game.CreateSnapshot();
            snapshot.Day = 61;
            Assert.That(game.Restore(snapshot), Is.True);
            Assert.That(game.CurrentWeekday, Is.EqualTo(Weekday.Friday));
            Assert.That(game.IsRegularCheckupDay(5), Is.False);
            Assert.That(game.IsRegularCheckupDay(61), Is.True);
            Assert.That(game.IsRegularCheckupDay(117), Is.True);
            Assert.That(game.Assign("survey", 1), Is.True);
            float fullOutput = game.Crew[1].DailyOutput *
                               MilestoneSimulation.CompetencyOutputMultiplier(
                                   game.Crew[1], game.Tasks.Find(task => task.Id == "survey"));
            int resources = game.Resources;

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").LastOutput,
                Is.EqualTo(fullOutput * .5f).Within(.001f));
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(2));
            Assert.That(game.Resources, Is.EqualTo(resources));
            Assert.That(game.PendingMedicalResults, Has.Length.EqualTo(game.Crew.Count));
        }

        [Test]
        public void FiveWorkdaysAndWeekendCostTwoFatigueWhileAThirdRestDayRecoversAboutEight()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            ForceSuccessOutcome(data);
            data.Balance.RandomWorkLimit = 0;
            WorkTask surveyData = Array.Find(data.Tasks, task => task.Id == "survey");
            surveyData.RequiredWork = 100f;
            var game = new MilestoneSimulation(data, 31);
            game.SetCompetencyAutoAssignment(false);
            game.Crew[1].Fatigue = 50;
            Assert.That(game.Assign("survey", 1), Is.True);

            for (int day = 0; day < 7; day++) game.AdvanceDay();

            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(52));
            Assert.That(game.Rest(1), Is.True);
            game.AdvanceDay();
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(43));
        }

        [Test]
        public void WeekendRestPausesNormalWorkAndAllOutWorkContinues()
        {
            TaskSystemData restingData = TaskSystemDataLoader.Load();
            DisableAccidents(restingData);
            ForceSuccessOutcome(restingData);
            var resting = new MilestoneSimulation(restingData, 13);
            CampaignSnapshot restingSnapshot = resting.CreateSnapshot();
            restingSnapshot.Day = 6;
            Assert.That(resting.Restore(restingSnapshot), Is.True);
            resting.Crew[1].Fatigue = 50;
            resting.Crew[1].Mental = 50;
            Assert.That(resting.Assign("survey", 1), Is.True);
            resting.AdvanceDay();
            Assert.That(resting.Tasks.Find(task => task.Id == "survey").Progress, Is.Zero);
            Assert.That(resting.Crew[1].Fatigue, Is.EqualTo(41));
            Assert.That(resting.Crew[1].Mental, Is.EqualTo(58));

            TaskSystemData allOutData = TaskSystemDataLoader.Load();
            DisableAccidents(allOutData);
            ForceSuccessOutcome(allOutData);
            var allOut = new MilestoneSimulation(allOutData, 13);
            CampaignSnapshot allOutSnapshot = allOut.CreateSnapshot();
            allOutSnapshot.Day = 6;
            Assert.That(allOut.Restore(allOutSnapshot), Is.True);
            Assert.That(allOut.Assign("survey", 1), Is.True);
            Assert.That(allOut.ConfigureWorkFocus("foundation", false, true), Is.True);
            allOut.AdvanceDay();
            Assert.That(allOut.Tasks.Find(task => task.Id == "survey").Progress, Is.GreaterThan(0f));
        }

        [Test]
        public void UrgentWorkPullsItsBestWorkerFromLowerPriorityWork()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UsePlanningTestFixture(data);
            DisableAccidents(data);
            ForceSuccessOutcome(data);
            WorkGroup urgentWork = Array.Find(data.Works, work => work.Id == FollowupWorkId);
            urgentWork.PredecessorIds = new string[0];
            urgentWork.RevealDay = 0;
            int bestWorker = 1;
            Array.Find(data.Tasks, task => task.Id == FollowupTaskId).RequiredRole =
                data.Crew[bestWorker].Specialty;
            var game = new MilestoneSimulation(data, 17);
            game.SetCompetencyAutoAssignment(false);
            WorkTask urgentTask = game.Tasks.Find(task => task.Id == FollowupTaskId);
            WorkTask lowerTask = game.Tasks.Find(task => task.Id == RootTaskId);
            foreach (CrewMember member in game.Crew)
                foreach (int competency in urgentTask.RequiredCompetencies)
                    member.Competencies[competency] = CrewMember.MinimumCompetency;
            foreach (int competency in urgentTask.RequiredCompetencies)
                game.Crew[bestWorker].Competencies[competency] = CrewMember.MaximumCompetency;
            Assert.That(game.Assign(lowerTask.Id, bestWorker), Is.True);
            while (game.MoveWorkPriority(FollowupWorkId, -1)) { }
            Assert.That(game.ConfigureWorkFocus(FollowupWorkId, true, false), Is.True);
            Assert.That(game.Groups.Find(group => group.Id == FollowupWorkId).Priority,
                Is.LessThan(game.Groups.Find(group => group.Id == PlanningWorkId).Priority));
            Assert.That(urgentTask.State, Is.EqualTo(TaskState.Available));
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();

            Assert.That(game.LastReport.Lines, Has.Some.Contains("우선순위 선점"));
            Assert.That(urgentTask.AssignedCharacter, Is.EqualTo(bestWorker));
        }

        [Test]
        public void AllOutWorkSkipsFridayCheckupForItsAssignedWorker()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            ForceSuccessOutcome(data);
            foreach (WorkGroup work in data.Works)
            {
                work.SoftDeadline = 180;
                work.HardDeadline = 200;
            }
            foreach (WorkTask task in data.Tasks) task.Deadline = 200;
            var game = new MilestoneSimulation(data, 18);
            game.SetCompetencyAutoAssignment(false);
            CampaignSnapshot snapshot = game.CreateSnapshot();
            snapshot.Day = 61;
            Assert.That(game.Restore(snapshot), Is.True);
            Assert.That(game.Assign("survey", 1), Is.True);
            Assert.That(game.ConfigureWorkFocus("foundation", false, true), Is.True);
            float expected = game.Crew[1].DailyOutput *
                MilestoneSimulation.CompetencyOutputMultiplier(game.Crew[1],
                    game.Tasks.Find(task => task.Id == "survey"));

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").LastOutput,
                Is.EqualTo(expected).Within(.001f));
            Assert.That(game.PendingMedicalResults, Has.Length.EqualTo(game.Crew.Count - 1));
            Assert.That(Array.Exists(game.PendingMedicalResults, result => result.CrewIndex == 1), Is.False);
        }

        [Test]
        public void ContinuityStyleUsesPlayerRoleDepthChartForAutomaticStaffing()
        {
            var game = new MilestoneSimulation(31);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            int selectedWorker = 3;
            game.SetOperationsStyle(OperationsStyle.Continuity);
            while (Array.IndexOf(game.RoleDepthOrder(survey.RequiredRole), selectedWorker) > 0)
                Assert.That(game.MoveRoleDepth(survey.RequiredRole, selectedWorker, -1), Is.True);

            game.AdvanceDay();

            Assert.That(survey.AssignedCharacter, Is.EqualTo(selectedWorker));
            Assert.That(game.LastReport.Lines, Has.Some.Contains("조직 자동 배정"));
        }

        [Test]
        public void FatiguePolicyRestsWorkerWithoutLosingCurrentTaskContext()
        {
            var game = new MilestoneSimulation(32);
            game.SetCompetencyAutoAssignment(false);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            Assert.That(game.Assign(survey.Id, 0), Is.True);
            game.Crew[0].Fatigue = 40;
            game.AdjustCrewFatigueLimit(0, -30);
            float progress = survey.Progress;

            game.AdvanceDay();

            Assert.That(survey.AssignedCharacter, Is.EqualTo(0));
            Assert.That(survey.Progress, Is.EqualTo(progress));
            Assert.That(game.Crew[0].Fatigue, Is.LessThan(40));
            Assert.That(game.LastReport.Lines, Has.Some.Contains("피로 한계"));
        }

        [Test]
        public void RecoveryAndEmergencyModesRestrictNormalAutomaticStaffing()
        {
            var game = new MilestoneSimulation(33);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            game.SetCrewOperatingMode(0, CrewOperatingMode.Recovery);
            game.SetCrewOperatingMode(1, CrewOperatingMode.Emergency);
            while (Array.IndexOf(game.RoleDepthOrder(survey.RequiredRole), 0) > 0)
                game.MoveRoleDepth(survey.RequiredRole, 0, -1);
            while (Array.IndexOf(game.RoleDepthOrder(survey.RequiredRole), 1) > 1)
                game.MoveRoleDepth(survey.RequiredRole, 1, -1);

            game.AdvanceDay();

            Assert.That(survey.AssignedCharacter, Is.Not.EqualTo(0));
            Assert.That(survey.AssignedCharacter, Is.Not.EqualTo(1));
        }

        [Test]
        public void StaffingPlanSurvivesCampaignSnapshotAndLegacySnapshotGetsDefaults()
        {
            var original = new MilestoneSimulation(34);
            original.SetOperationsStyle(OperationsStyle.Growth);
            original.SetCrewOperatingMode(2, CrewOperatingMode.Support);
            original.AdjustCrewFatigueLimit(2, 15);
            WorkRole role = WorkRole.Management;
            while (Array.IndexOf(original.RoleDepthOrder(role), 2) > 0)
                original.MoveRoleDepth(role, 2, -1);

            CampaignSnapshot snapshot = original.CreateSnapshot();
            var restored = new MilestoneSimulation(35);
            Assert.That(restored.Restore(snapshot), Is.True);
            Assert.That(restored.StaffingPlan.Style, Is.EqualTo(OperationsStyle.Growth));
            Assert.That(restored.CrewPolicy(2).Mode, Is.EqualTo(CrewOperatingMode.Support));
            Assert.That(restored.CrewPolicy(2).FatigueLimit, Is.EqualTo(85));
            Assert.That(restored.RoleDepthOrder(role)[0], Is.EqualTo(2));

            snapshot.StaffingPlan = null;
            var legacy = new MilestoneSimulation(36);
            Assert.That(legacy.Restore(snapshot), Is.True);
            Assert.That(legacy.StaffingPlan.Style, Is.EqualTo(OperationsStyle.Balanced));
            Assert.That(legacy.CrewPolicy(0).FatigueLimit, Is.EqualTo(70));
        }

        [Test]
        public void OrganizationRunsThirtyDaysWithoutManualTaskAssignment()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 37);

            for (int day = 0; day < 30 && !game.IsLost; day++) game.AdvanceDay();

            Assert.That(game.Day, Is.EqualTo(31));
            Assert.That(game.Tasks.Exists(task => task.Progress > 0f || task.State == TaskState.Complete), Is.True);
        }

        private static void CompleteRootTask(MilestoneSimulation game)
        {
            Assert.That(game.Assign(RootTaskId, 1), Is.True);
            for (int i = 0; i < 8 && game.Tasks.Find(task => task.Id == RootTaskId).State != TaskState.Complete; i++)
                game.AdvanceDay();
            Assert.That(game.Tasks.Find(task => task.Id == RootTaskId).State, Is.EqualTo(TaskState.Complete));
        }

        private static void FillStakeholderPlan(MilestoneSimulation game, bool matchingIntent)
        {
            string member = "stakeholder-landing-brief-member-permission";
            string order = "stakeholder-landing-brief-order-refund";
            string payment = "stakeholder-landing-brief-payment-refund";
            string reporting = "stakeholder-landing-brief-reporting";
            string admin = "stakeholder-landing-brief-admin-operation";
            if (matchingIntent)
            {
                Assert.That(game.SetStakeholderOption(member, "multi-role", true), Is.True);
                Assert.That(game.SetStakeholderOption(order, "partial-cancel", true), Is.True);
                Assert.That(game.SetStakeholderOption(payment, "partial-refund", true), Is.True);
                Assert.That(game.SetStakeholderOption(payment, "mixed-payment", true), Is.True);
                Assert.That(game.SetStakeholderOption(payment, "duplicate-guard", true), Is.True);
                Assert.That(game.SetStakeholderTaskIncluded(reporting, true), Is.True);
                Assert.That(game.SetStakeholderOption(reporting, "excel-export", true), Is.True);
                Assert.That(game.SetStakeholderTaskIncluded(admin, true), Is.True);
                Assert.That(game.SetStakeholderOption(admin, "audit-log", true), Is.True);
            }
            Assert.That(game.AssignStakeholderTask(member, 1), Is.True);
            Assert.That(game.AssignStakeholderTask(order, 1), Is.True);
            Assert.That(game.AssignStakeholderTask(payment, 2), Is.True);
            if (matchingIntent)
            {
                Assert.That(game.AssignStakeholderTask(reporting, 3), Is.True);
                Assert.That(game.AssignStakeholderTask(admin, 4), Is.True);
            }
        }

        private static void AdvanceUntilStakeholderPlanReviewed(
            MilestoneSimulation game, StakeholderRequestState request)
        {
            int reviewDay = request.PendingPlanReviewDay;
            Assert.That(reviewDay, Is.GreaterThan(game.Day));
            while (request.PendingPlanReviewDay > 0)
                game.AdvanceDay();
            Assert.That(game.Day, Is.EqualTo(reviewDay));
        }

        private static void UsePlanningTestFixture(TaskSystemData data)
        {
            data.Works = new[]
            {
                new WorkGroup
                {
                    Id = PlanningWorkId,
                    Name = "Planning test work",
                    SoftDeadline = 45,
                    HardDeadline = 60,
                    Required = true,
                    RewardCredits = 5,
                    SoftPenaltyCredits = 2,
                    HardPenaltyCredits = 7,
                    PredecessorIds = new string[0]
                },
                new WorkGroup
                {
                    Id = FollowupWorkId,
                    Name = "Follow-up test work",
                    RevealDay = 60,
                    SoftDeadline = 81,
                    HardDeadline = 90,
                    Required = true,
                    RewardCredits = 8,
                    SoftPenaltyCredits = 3,
                    HardPenaltyCredits = 10,
                    PredecessorIds = new[] { PlanningWorkId }
                }
            };
            data.Tasks = new[]
            {
                PlanningTask(RootTaskId, "Root task", WorkRole.Analysis,
                    new[] { 1, 3 }, 3f, string.Empty, PlanningWorkId,
                    RiskLevel.Medium, ImportanceLevel.High, 2, true),
                PlanningTask(DependentTaskAId, "Dependent task A", WorkRole.Tech,
                    new[] { 0, 2 }, 4f, RootTaskId, PlanningWorkId,
                    RiskLevel.High, ImportanceLevel.High, 3, true),
                PlanningTask(DependentTaskBId, "Dependent task B", WorkRole.Tech,
                    new[] { 0, 4, 2 }, 4f, RootTaskId, PlanningWorkId,
                    RiskLevel.Medium, ImportanceLevel.High, 2, true),
                PlanningTask(OptionalTaskId, "Optional task", WorkRole.Analysis,
                    new[] { 0, 4, 1 }, 1f, DependentTaskAId, PlanningWorkId,
                    RiskLevel.Low, ImportanceLevel.Medium, 1, false),
                PlanningTask(FollowupTaskId, "Follow-up task", WorkRole.Adaptation,
                    new[] { 0, 3, 5 }, 3f, string.Empty, FollowupWorkId,
                    RiskLevel.High, ImportanceLevel.High, 3, true)
            };
        }

        private static WorkTask PlanningTask(
            string id, string name, WorkRole role, int[] competencies, float work,
            string prerequisiteId, string groupId, RiskLevel risk,
            ImportanceLevel importance, int difficulty, bool required)
        {
            return new WorkTask
            {
                Id = id,
                Name = name,
                Kind = TaskKind.Milestone,
                RequiredRole = role,
                RequiredCompetencies = competencies,
                RequiredWork = work,
                Required = required,
                PrerequisiteId = prerequisiteId,
                AssignedCharacter = -1,
                GroupId = groupId,
                Risk = risk,
                Importance = importance,
                Difficulty = difficulty
            };
        }

        private static void DisableAccidents(TaskSystemData data)
        {
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
        }

        private static void ForceSuccessOutcome(TaskSystemData data)
        {
            data.Balance.FreshLowOutputChance = 0;
            data.Balance.LowOutputChance = 0;
            data.Balance.ExhaustedLowOutputChance = 0;
            data.Balance.FreshHighOutputChance = 0;
            data.Balance.HighOutputChance = 0;
            data.Balance.ExhaustedHighOutputChance = 0;
        }
    }
}
