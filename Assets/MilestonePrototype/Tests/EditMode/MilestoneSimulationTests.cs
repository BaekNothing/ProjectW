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
        [Test]
        public void CrewProfilesLoadPortraitMemoAndPerksFromExternalData()
        {
            var game = new MilestoneSimulation(1);

            Assert.That(game.Crew, Has.Count.EqualTo(MilestoneSimulation.TeamSize));
            Assert.That(game.Crew[0].PortraitLabel, Is.Not.Empty);
            Assert.That(game.Crew[0].Memo, Is.Not.Empty);
            Assert.That(game.Crew[0].Perks, Is.Not.Null.And.Not.Empty);
            Assert.That(game.Crew[0].Trust, Is.InRange(0, 100));
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
        public void RestoreTrimsLegacyRosterAndClearsRemovedWorkerAssignments()
        {
            var original = new MilestoneSimulation(1);
            CampaignSnapshot snapshot = original.CreateSnapshot();
            var legacyCrew = new CrewMember[6];
            for (int i = 0; i < snapshot.Crew.Length; i++) legacyCrew[i] = snapshot.Crew[i];
            legacyCrew[4] = new CrewMember { Name = "legacy-4" };
            legacyCrew[5] = new CrewMember { Name = "legacy-5" };
            snapshot.Crew = legacyCrew;
            snapshot.Tasks[0].AssignedCharacter = 5;
            snapshot.Tasks[0].ScheduledDay = 3;
            snapshot.Tasks[0].ScheduledWorker = 4;

            var restored = new MilestoneSimulation(2);
            Assert.That(restored.Restore(snapshot), Is.True);

            Assert.That(restored.Crew, Has.Count.EqualTo(MilestoneSimulation.TeamSize));
            Assert.That(restored.Tasks[0].AssignedCharacter, Is.EqualTo(-1));
            Assert.That(restored.Tasks[0].ScheduledWorker, Is.EqualTo(-1));
            Assert.That(restored.Tasks[0].ScheduledDay, Is.Zero);
        }

        [Test]
        public void MatchingSpecialtyAdvancesWorkAndAddsFatigue()
        {
            var game = new MilestoneSimulation(1);
            Assert.That(game.Assign("survey", 1), Is.True);
            game.AdvanceDay();
            Assert.That(game.Tasks[0].Progress, Is.InRange(.7f, 1.3f));
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(9));
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
            data.Balance.LowOutputChance = 0;
            data.Balance.HighOutputChance = 0;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");

            game.Assign(survey.Id, 1);
            game.AdvanceDay();
            game.AdvanceDay();
            Assert.That(survey.State, Is.EqualTo(TaskState.Active));
            Assert.That(survey.Progress, Is.EqualTo(2f));

            game.AdvanceDay();

            Assert.That(survey.Progress, Is.EqualTo(survey.EffectiveRequiredWork));
            Assert.That(survey.State, Is.EqualTo(TaskState.Complete));
            Assert.That(survey.StartedDay, Is.EqualTo(1));
            Assert.That(survey.CompletedDay, Is.EqualTo(3));
            Assert.That(survey.AssignedCharacter, Is.EqualTo(-1));
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
            Assert.That(MilestonePrototypeController.TaskActualDurationDays(survey, game.Day),
                Is.EqualTo(1));
        }

        [Test]
        public void IncompletePrerequisiteCapsSuccessorAtThirtyPercent()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.LowOutputChance = 0;
            data.Balance.HighOutputChance = 0;
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);
            WorkTask habitat = game.Tasks.Find(task => task.Id == "habitat");

            Assert.That(game.Assign(habitat.Id, 0), Is.True);
            game.AdvanceDay();
            game.AdvanceDay();

            Assert.That(habitat.Progress, Is.EqualTo(habitat.EffectiveRequiredWork * .3f).Within(.001f));
            Assert.That(habitat.State, Is.EqualTo(TaskState.Active));
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
            var game = new MilestoneSimulation(data, 1);

            game.Assign("survey", 1);
            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").LastOutput, Is.EqualTo(1.4f).Within(.001f));
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
            data.Balance.RandomWorkDependencyChance = 0;
            var game = new MilestoneSimulation(data, 1);

            game.AdvanceDay();

            WorkGroup generated = game.Groups.Find(work => work.Id.StartsWith("random-work-"));
            Assert.That(generated, Is.Not.Null);
            Assert.That(generated.SoftDeadline, Is.LessThan(generated.HardDeadline));
            Assert.That(generated.RewardCredits, Is.GreaterThan(0));
            Assert.That(generated.HardPenaltyCredits, Is.GreaterThan(generated.SoftPenaltyCredits));
            Assert.That(game.Tasks.Exists(task => task.GroupId == generated.Id), Is.True);
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
            var game = new MilestoneSimulation(1);

            TaskScheduleEstimate estimate = game.EstimateSchedule("survey", 1);

            Assert.That(estimate.StartDay, Is.EqualTo(game.Day));
            Assert.That(estimate.ExpectedDailyOutput, Is.EqualTo(1f).Within(.001f));
            Assert.That(estimate.DurationDays, Is.EqualTo(3));
            Assert.That(estimate.CompletionDay, Is.EqualTo(3));
            Assert.That(estimate.RollingStart, Is.False);
        }

        [Test]
        public void AssignedBlockerAndBusyWorkerPushSuccessorAfterExpectedCompletion()
        {
            var game = new MilestoneSimulation(1);
            Assert.That(game.Assign("survey", 1), Is.True);

            TaskScheduleEstimate estimate = game.EstimateSchedule("habitat", 1);

            Assert.That(estimate.StartDay, Is.EqualTo(4));
            Assert.That(estimate.CompletionDay, Is.EqualTo(7));
            Assert.That(estimate.RollingStart, Is.False);
        }

        [Test]
        public void UnassignedBlockerUsesBaselinePreviewAndMovesWithCurrentDay()
        {
            var game = new MilestoneSimulation(1);

            TaskScheduleEstimate today = game.EstimateSchedule("habitat", 0);
            game.AdvanceDay();
            TaskScheduleEstimate nextDay = game.EstimateSchedule("habitat", 0);

            Assert.That(today.StartDay, Is.EqualTo(4));
            Assert.That(today.RollingStart, Is.False);
            Assert.That(nextDay.StartDay, Is.EqualTo(5));
            Assert.That(nextDay.RollingStart, Is.False);
        }

        [Test]
        public void FasterWorkerProducesShorterExpectedDuration()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Crew[1].DailyOutput = 2f;
            var game = new MilestoneSimulation(data, 1);

            TaskScheduleEstimate estimate = game.EstimateSchedule("survey", 1);

            Assert.That(estimate.ExpectedDailyOutput, Is.EqualTo(2f).Within(.001f));
            Assert.That(estimate.DurationDays, Is.EqualTo(2));
            Assert.That(estimate.CompletionDay, Is.EqualTo(2));
        }

        [Test]
        public void UnassignedTasksPreviewAtOneWorkPerDayInDependencyOrder()
        {
            var game = new MilestoneSimulation(1);

            TaskScheduleEstimate survey = game.EstimatePreviewSchedule("survey");
            TaskScheduleEstimate power = game.EstimatePreviewSchedule("power");
            TaskScheduleEstimate habitat = game.EstimatePreviewSchedule("habitat");
            TaskScheduleEstimate safety = game.EstimatePreviewSchedule("safety");

            Assert.That(survey.ExpectedDailyOutput, Is.EqualTo(1f));
            Assert.That(survey.StartDay, Is.EqualTo(1));
            Assert.That(survey.CompletionDay, Is.EqualTo(3));
            Assert.That(power.StartDay, Is.EqualTo(4));
            Assert.That(power.CompletionDay, Is.EqualTo(7));
            Assert.That(habitat.StartDay, Is.EqualTo(4));
            Assert.That(safety.StartDay, Is.EqualTo(8));
        }

        [Test]
        public void WorkPredecessorPreviewStartsAfterLatestRequiredTask()
        {
            var game = new MilestoneSimulation(1);

            TaskScheduleEstimate launch = game.EstimatePreviewSchedule("launch");

            Assert.That(launch.StartDay, Is.EqualTo(8));
            Assert.That(launch.CompletionDay, Is.EqualTo(10));
        }

        [Test]
        public void AssignedPredecessorOutputMovesUnassignedSuccessorPreview()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Crew[1].DailyOutput = 2f;
            var game = new MilestoneSimulation(data, 1);
            game.Assign("survey", 1);

            TaskScheduleEstimate habitat = game.EstimatePreviewSchedule("habitat");

            Assert.That(habitat.StartDay, Is.EqualTo(3));
            Assert.That(habitat.CompletionDay, Is.EqualTo(6));
        }

        [Test]
        public void WorkerCannotHaveTwoPrimaryReservationsOnSameDay()
        {
            var game = new MilestoneSimulation(1);

            Assert.That(game.Schedule("survey", 0, 3), Is.True);
            Assert.That(game.Schedule("habitat", 0, 3), Is.False);
            Assert.That(game.Schedule("habitat", 1, 3), Is.True);
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
            var game = new MilestoneSimulation(1);
            game.Assign("survey", 1);
            game.AdvanceDay();
            game.AdvanceDay();
            game.AdvanceDay();
            Assert.That(game.Tasks.Find(t => t.Id == "power").State, Is.EqualTo(TaskState.Available));
            Assert.That(game.Tasks.Find(t => t.Id == "habitat").State, Is.EqualTo(TaskState.Available));
        }

        [Test]
        public void RegenerationConsumesResourcesAndResetsFatigue()
        {
            var game = new MilestoneSimulation(1);
            game.Assign("survey", 1);
            game.AdvanceDay();
            int before = game.Resources;
            Assert.That(game.Regenerate(1), Is.True);
            Assert.That(game.Crew[1].Fatigue, Is.Zero);
            Assert.That(game.Resources, Is.EqualTo(before - 3));
        }

        [Test]
        public void RestConsumesNextDayInsteadOfRecoveringImmediately()
        {
            var game = new MilestoneSimulation(1);
            game.Assign("survey", 1);
            game.AdvanceDay();
            int fatigue = game.Crew[1].Fatigue;
            Assert.That(game.Rest(1), Is.True);
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(fatigue));
            Assert.That(game.Rest(1), Is.False);
            game.AdvanceDay();
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(0));
        }

        [Test]
        public void StatusTextKeepsOnlyTheLatestTwoEventsOnSeparateLines()
        {
            var report = new DayReport();
            report.Lines.Add("first");
            report.Lines.Add("second");
            report.Lines.Add("third");

            string status = MilestonePrototypeController.FormatStatus(report, false, false);

            Assert.That(status, Is.EqualTo("second\nthird"));
        }

        [Test]
        public void MessengerStatusQuestionAddsQuestionAndWorkerReply()
        {
            var game = new MilestoneSimulation(1);
            CrewMember worker = game.Crew[0];

            Assert.That(game.AskWorker(0, "status"), Is.True);

            Assert.That(worker.History[worker.History.Count - 2], Does.Contain("[나]"));
            Assert.That(worker.History[worker.History.Count - 1], Does.Contain(worker.Name));
            Assert.That(worker.History[worker.History.Count - 1], Does.Contain("피로도"));
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
        public void StatusTextUsesTerminalCampaignStateInsteadOfEventLines()
        {
            var report = new DayReport();
            report.Lines.Add("event");

            Assert.That(MilestonePrototypeController.FormatStatus(report, true, false), Is.EqualTo("마일스톤 완료 — 캠페인 승리"));
            Assert.That(MilestonePrototypeController.FormatStatus(report, false, true), Is.EqualTo("운영 붕괴 — 캠페인 실패"));
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
        public void BaseVersionMatchesScrollbarAotBaseline()
        {
            Assert.That(PatchBootstrapper.BaseVersion, Is.EqualTo(4));
        }

        [Test]
        public void ScrollbarWidthIsTwiceTheCurrentSkinWidth()
        {
            Assert.That(MilestonePrototypeController.DoubledScrollbarWidth(16f), Is.EqualTo(32f));
            Assert.That(MilestonePrototypeController.DoubledScrollbarWidth(0f),
                Is.EqualTo(MilestonePrototypeController.DefaultScrollbarWidth * 2f));
        }

        [Test]
        public void ResolvingMailAppliesItsRuleOnlyOnce()
        {
            var game = new MilestoneSimulation(1);
            WorkGroup foundation = game.Groups.Find(group => group.Id == "foundation");
            int softDeadline = foundation.SoftDeadline;
            int hardDeadline = foundation.HardDeadline;

            Assert.That(game.ResolveMail("mail-1"), Is.True);
            Assert.That(foundation.SoftDeadline, Is.EqualTo(softDeadline - 1));
            Assert.That(foundation.HardDeadline, Is.EqualTo(hardDeadline - 1));
            WorkTask survey = game.Tasks.Find(t => t.Id == "survey");
            Assert.That(survey.Importance, Is.EqualTo(ImportanceLevel.High));
            Assert.That(survey.Records, Has.Count.EqualTo(1));
            Assert.That(game.ResolveMail("mail-1"), Is.False);
            Assert.That(foundation.HardDeadline, Is.EqualTo(hardDeadline - 1));
        }

        [Test]
        public void FutureMailCannotBeResolvedEarly()
        {
            var game = new MilestoneSimulation(1);
            Assert.That(game.ResolveMail("mail-2"), Is.False);
        }

        [Test]
        public void SnapshotRestoresCampaignState()
        {
            var original = new MilestoneSimulation(1);
            original.Assign("survey", 1);
            original.AdvanceDay();
            original.ResolveMail("mail-1");

            var restored = new MilestoneSimulation(99);
            Assert.That(restored.Restore(original.CreateSnapshot()), Is.True);
            Assert.That(restored.Day, Is.EqualTo(original.Day));
            Assert.That(restored.Resources, Is.EqualTo(original.Resources));
            Assert.That(restored.Tasks.Find(t => t.Id == "survey").Progress,
                Is.EqualTo(original.Tasks.Find(t => t.Id == "survey").Progress));
            Assert.That(restored.Groups.Find(group => group.Id == "foundation").HardDeadline,
                Is.EqualTo(original.Groups.Find(group => group.Id == "foundation").HardDeadline));
            Assert.That(restored.Mail.Find(m => m.Id == "mail-1").Resolved, Is.True);
        }

        [Test]
        public void WorkPrerequisiteLocksLaunchUntilFoundationCompletes()
        {
            var game = new MilestoneSimulation(1);

            Assert.That(game.Groups.Find(group => group.Id == "launch").State, Is.EqualTo(WorkState.Locked));
            Assert.That(game.Tasks.Find(task => task.Id == "launch").State, Is.EqualTo(TaskState.Locked));
            Assert.That(game.Assign("launch", 0), Is.False);
        }

        [Test]
        public void WorkerCanHoldOnlyOnePrimaryTask()
        {
            var game = new MilestoneSimulation(1);
            CompleteSurvey(game);
            Assert.That(game.Assign("power", 0), Is.True);
            Assert.That(game.Assign("habitat", 0), Is.True);

            Assert.That(game.Tasks.Find(task => task.Id == "power").AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == "habitat").AssignedCharacter, Is.EqualTo(0));
        }

        [Test]
        public void SmallTaskCanRunInParallelForAdditionalFatigue()
        {
            var game = new MilestoneSimulation(1);
            CompleteSurvey(game);
            Assert.That(game.Assign("power", 0), Is.True);
            WorkTask safety = game.Tasks.Find(task => task.Id == "safety");
            safety.PrerequisiteId = null;
            safety.State = TaskState.Available;

            Assert.That(game.AssignParallel("safety", 0), Is.True);
            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "power").Progress, Is.EqualTo(1f));
            Assert.That(safety.State, Is.EqualTo(TaskState.Complete));
            Assert.That(game.Crew[0].Fatigue, Is.EqualTo(36));
        }

        [Test]
        public void InterruptingFourDayTaskAddsOneDayContextCost()
        {
            var game = new MilestoneSimulation(1);
            CompleteSurvey(game);
            WorkTask habitat = game.Tasks.Find(task => task.Id == "habitat");
            Assert.That(game.Assign(habitat.Id, 0), Is.True);
            game.AdvanceDay();

            Assert.That(game.Assign(habitat.Id, -1), Is.True);
            Assert.That(habitat.Progress, Is.EqualTo(1f));
            Assert.That(habitat.ContextCostDays, Is.EqualTo(1f));
            Assert.That(habitat.EffectiveRequiredWork, Is.EqualTo(5f));
            Assert.That(habitat.SplitCount, Is.EqualTo(1));
        }

        [Test]
        public void HandoverChargesExactlyOneSplit()
        {
            var game = new MilestoneSimulation(1);
            CompleteSurvey(game);
            WorkTask habitat = game.Tasks.Find(task => task.Id == "habitat");
            game.Assign(habitat.Id, 0);
            game.AdvanceDay();

            Assert.That(game.Assign(habitat.Id, 3), Is.True);

            Assert.That(habitat.AssignedCharacter, Is.EqualTo(3));
            Assert.That(habitat.ContextCostDays, Is.EqualTo(1f));
            Assert.That(habitat.SplitCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingHardDeadlineFailsRequiredWork()
        {
            var game = new MilestoneSimulation(1);
            while (game.Day <= 20) game.AdvanceDay();

            Assert.That(game.Groups.Find(group => group.Id == "foundation").State, Is.EqualTo(WorkState.Failed));
            Assert.That(game.IsLost, Is.True);
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
            data.Balance.HighFatigueAccidentChance = 0;
            data.Balance.MediumFatigueAccidentChance = 0;
            data.Balance.MismatchAccidentChance = 0;
            var game = new MilestoneSimulation(data, 1);
            CompleteSurvey(game);
            WorkTask habitat = game.Tasks.Find(task => task.Id == "habitat");
            game.Assign(habitat.Id, 1);
            game.AdvanceDay();

            TaskCostPreview preview = game.BuildCostPreview(habitat, 0);

            Assert.That(preview.RemainingDays, Is.EqualTo(habitat.RemainingWork));
            Assert.That(preview.AdditionalContextDays, Is.EqualTo(1f));
            Assert.That(preview.PrimaryFatigue, Is.EqualTo(9));
            Assert.That(preview.ParallelFatigue, Is.EqualTo(21));
            Assert.That(preview.CanRunInParallel, Is.False);
        }

        [Test]
        public void ContentDragMovesScrollOppositeToPointer()
        {
            Vector2 result = MilestonePrototypeController.CalculateDragScroll(
                new Vector2(100, 80), new Vector2(50, 50), new Vector2(20, 10));

            Assert.That(result, Is.EqualTo(new Vector2(130, 120)));
        }

        [Test]
        public void ContentDragDoesNotCreateNegativeScroll()
        {
            Vector2 result = MilestonePrototypeController.CalculateDragScroll(
                new Vector2(5, 7), Vector2.zero, new Vector2(30, 40));

            Assert.That(result, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void OperationsReportCountsDynamicRiskAndLoad()
        {
            var game = new MilestoneSimulation(1);
            game.Assign("survey", 0);
            for (int i = 0; i < 4; i++) game.AdvanceDay();

            OperationsReport report = game.BuildReport();
            Assert.That(report.Active + report.Complete + report.Available + report.Locked, Is.EqualTo(game.Tasks.Count));
            Assert.That(report.HighRisk, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void WindowClampKeepsRectInsideWorkArea()
        {
            Rect result = MilestonePrototypeController.ClampWindowRect(new Rect(-20, 500, 900, 700), 800, 600);
            Assert.That(result.x, Is.EqualTo(0));
            Assert.That(result.y, Is.EqualTo(0));
            Assert.That(result.width, Is.EqualTo(800));
            Assert.That(result.height, Is.EqualTo(600));
        }

        [Test]
        public void ExpandedHitRectDoublesButtonHitAreaWithoutMovingCenter()
        {
            Rect visual = new Rect(100, 20, 25, 20);

            Rect result = MilestonePrototypeController.ExpandHitRect(visual);

            Assert.That(result.size, Is.EqualTo(new Vector2(50, 40)));
            Assert.That(result.center, Is.EqualTo(visual.center));
        }

        [Test]
        public void WindowDragHitAreaUsesDoubleTitleHeight()
        {
            Rect result = MilestonePrototypeController.WindowDragHitRect(710);

            Assert.That(result, Is.EqualTo(new Rect(0, 0, 615, 52)));
        }

        [Test]
        public void NextDayButtonStaysAtBottomRightAboveTaskbar()
        {
            Rect result = MilestonePrototypeController.NextDayButtonRect(1280, 720);

            Assert.That(result.xMax, Is.EqualTo(1270));
            Assert.That(result.yMax, Is.EqualTo(670));
            Assert.That(result.width, Is.EqualTo(170));
            Assert.That(result.height, Is.EqualTo(48));
        }

        [Test]
        public void UiScaleMagnifiesFontsPanelsAndSpacingByOnePointEight()
        {
            Assert.That(MilestonePrototypeController.DefaultUiMagnification, Is.EqualTo(1.8f));
            Assert.That(MilestonePrototypeController.CalculateUiScale(1280), Is.EqualTo(1.8f).Within(.001f));
            Assert.That(MilestonePrototypeController.CalculateUiScale(1920), Is.EqualTo(2.7f).Within(.001f));
        }

        [Test]
        public void UiScaleSupportsOptionsAndDefaultsOldSavesToOnePointEight()
        {
            Assert.That(MilestonePrototypeController.CalculateUiScale(1280, 1f),
                Is.EqualTo(1f).Within(.001f));
            Assert.That(MilestonePrototypeController.CalculateUiScale(1280, 1.4f),
                Is.EqualTo(1.4f).Within(.001f));
            Assert.That(MilestonePrototypeController.CalculateUiScale(1280, 2.2f),
                Is.EqualTo(2.2f).Within(.001f));
            Assert.That(MilestonePrototypeController.NormalizeUiMagnification(0f),
                Is.EqualTo(1.8f));
        }

        [Test]
        public void DesktopSettingsRoundTripSelectedUiMagnification()
        {
            string key = $"projectw.desktop.test.{Guid.NewGuid():N}";
            var storage = new MemoryStringStorage();
            ProjectWSaveStore.Configure(storage);
            try
            {
                var snapshot = new DesktopSnapshot
                {
                    SchemaVersion = ProjectWSaveStore.DesktopSchema,
                    UiMagnification = 1.4f,
                    Windows = Array.Empty<WindowSnapshot>()
                };

                Assert.That(ProjectWSaveStore.SaveDesktop(key, snapshot), Is.True);
                Assert.That(ProjectWSaveStore.TryLoadDesktop(key, out DesktopSnapshot loaded), Is.True);
                Assert.That(loaded.UiMagnification, Is.EqualTo(1.4f));
            }
            finally
            {
                ProjectWSaveStore.Delete(key);
            }
        }

        [Test]
        public void MagnifiedLandscapeDesktopKeepsStatusAndReportClearOfNextDayButton()
        {
            float scale = MilestonePrototypeController.CalculateUiScale(1280);
            float width = 1280 / scale;
            float height = 720 / scale;
            Rect nextDay = MilestonePrototypeController.NextDayButtonRect(width, height);
            Rect report = MilestonePrototypeController.DesktopReportRect(width, height);
            Rect status = MilestonePrototypeController.DesktopStatusRect(width, height);

            Assert.That(report.Overlaps(nextDay), Is.False);
            Assert.That(status.Overlaps(nextDay), Is.False);
            Assert.That(report.yMax, Is.LessThanOrEqualTo(status.y));
        }

        [Test]
        public void OptionsIconRemainsInTwoRowGridAtMaximumScale()
        {
            float width = 1280 / MilestonePrototypeController.CalculateUiScale(1280, 2.2f);
            Rect options = MilestonePrototypeController.DesktopIconRect(8, width);
            Rect report = MilestonePrototypeController.DesktopReportRect(width, 720 / 2.2f);

            Assert.That(options.xMax, Is.LessThanOrEqualTo(width));
            Assert.That(options.yMax, Is.LessThan(report.y));
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
                game.ResolveMail("mail-1");
                Assert.That(ProjectWSaveStore.SaveCampaign(key, game.CreateSnapshot()), Is.True);
                Assert.That(ProjectWSaveStore.TryLoadCampaign(key, out CampaignSnapshot loaded), Is.True);
                Assert.That(loaded.Mail[0].Resolved, Is.True);

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

        private static void CompleteSurvey(MilestoneSimulation game)
        {
            Assert.That(game.Assign("survey", 1), Is.True);
            for (int i = 0; i < 8 && game.Tasks.Find(task => task.Id == "survey").State != TaskState.Complete; i++)
                game.AdvanceDay();
            Assert.That(game.Tasks.Find(task => task.Id == "survey").State, Is.EqualTo(TaskState.Complete));
        }
    }
}
