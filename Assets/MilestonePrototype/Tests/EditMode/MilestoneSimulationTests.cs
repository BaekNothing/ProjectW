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
            Assert.That(game.Crew[0].PortraitAddress, Is.EqualTo(CrewPortraitCatalog.HanTech));
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
        public void CompetencyAssignmentPreviewMatchesNextAutomaticAssignment()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 6);
            game.SetCompetencyAutoAssignment(true);

            int preview = game.PreviewAutomaticAssignee("survey", out string source);

            Assert.That(preview, Is.GreaterThanOrEqualTo(0));
            Assert.That(source, Does.StartWith("역량"));
            game.AdvanceDay();
            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.EqualTo(preview));
        }

        [Test]
        public void CompetencyAutoAssignmentChoosesBestAvailableWorker()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.EqualTo(1));
            Assert.That(game.LastReport.Lines, Has.Some.Contains("역량 자동 배정"));
        }

        [Test]
        public void CompetencyAutoAssignmentBreaksEqualScoresByLowerFatigue()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            data.Crew[1].Fatigue = 30;
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.EqualTo(3));
        }

        [Test]
        public void CompetencyAutoAssignmentLeavesSuccessorsUnassignedUntilPredecessorCompletes()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "survey").AssignedCharacter, Is.GreaterThanOrEqualTo(0));
            Assert.That(game.Tasks.Find(task => task.Id == "power").AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == "habitat").AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == "safety").AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == "launch").AssignedCharacter, Is.EqualTo(-1));
        }

        [Test]
        public void CompetencyAutoAssignmentReconsidersSuccessorsOnCycleAfterCompletion()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            DisableAccidents(data);
            data.Balance.BaseSideMissionChance = 0;
            Array.Find(data.Tasks, task => task.Id == "survey").RequiredWork = .5f;
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(true);

            game.AdvanceDay();
            Assert.That(game.Tasks.Find(task => task.Id == "survey").State, Is.EqualTo(TaskState.Complete));
            Assert.That(game.Tasks.Find(task => task.Id == "power").AssignedCharacter, Is.EqualTo(-1));
            Assert.That(game.Tasks.Find(task => task.Id == "habitat").AssignedCharacter, Is.EqualTo(-1));

            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "power").AssignedCharacter, Is.GreaterThanOrEqualTo(0));
            Assert.That(game.Tasks.Find(task => task.Id == "habitat").AssignedCharacter, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void LearnedAutoAssignmentAlsoWaitsForTaskPredecessor()
        {
            var game = new MilestoneSimulation(1);
            WorkTask power = game.Tasks.Find(task => task.Id == "power");
            game.AssignmentRules.Add(new AssignmentRule
            {
                Kind = power.Kind,
                RequiredRole = power.RequiredRole,
                Difficulty = power.Difficulty,
                Risk = power.Risk,
                Importance = power.Importance,
                CrewName = game.Crew[0].Name
            });

            game.AdvanceDay();

            Assert.That(power.AssignedCharacter, Is.EqualTo(-1));
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
            UseShortPlanningFixture(data);
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
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");

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
        public void FullyFatiguedWorkerKeepsAssignmentAndContinuesWorking()
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
            Assert.That(survey.LastOutput, Is.EqualTo(.75f).Within(.001f));
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(100));
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
            Assert.That(offer.ArrivalDay, Is.EqualTo(game.Day));
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
        public void ProposalCanAskForARevisionOrBeDeclinedWithoutPenalty()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.RandomWorkLimit = 0;
            var game = new MilestoneSimulation(data, 4);
            int resources = game.Resources;
            string targetId = game.ProposalTargets[0].Id;
            string[] actionIds = { game.ProposalActions[0].Id, game.ProposalActions[1].Id };
            Assert.That(game.SubmitProposal(targetId, actionIds, ProposalPitch.Stability), Is.True);
            MailEvent offer = game.Mail.Find(mail => mail.IsProposal && !mail.Resolved &&
                !string.IsNullOrEmpty(mail.TargetWorkId));
            offer.ProposalStage = ProposalStage.Question;
            offer.Subject = "제안 보완 요청";
            game.AdvanceDay();
            WorkGroup work = game.Groups.Find(group => group.Id == offer.TargetWorkId);

            Assert.That(offer.ProposalStage, Is.EqualTo(ProposalStage.Question));
            Assert.That(work.AwaitingAcceptance, Is.True);
            Assert.That(game.RespondToProposal(offer.Id, ProposalPitch.Decline), Is.True);
            Assert.That(offer.ProposalStage, Is.EqualTo(ProposalStage.Declined));
            Assert.That(work.State, Is.EqualTo(WorkState.Failed));
            Assert.That(game.IsWorkVisible(work), Is.False);
            Assert.That(game.Resources, Is.EqualTo(resources));
        }

        [Test]
        public void ComposedProposalCalculatesBudgetAndActivatesFromNextDayMail()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.RandomWorkLimit = 0;
            var game = new MilestoneSimulation(data, 9);
            string targetId = game.ProposalTargets[0].Id;
            string[] actionIds =
            {
                game.ProposalActions[0].Id,
                game.ProposalActions[1].Id,
                game.ProposalActions[2].Id
            };
            ProposalEstimate estimate = game.EstimateProposal(targetId, actionIds);
            int resources = game.Resources;

            Assert.That(estimate.TaskCount, Is.EqualTo(3));
            Assert.That(estimate.TotalWork, Is.GreaterThan(0f));
            Assert.That(estimate.RewardCredits, Is.GreaterThan(estimate.CostCredits));
            Assert.That(game.SubmitProposal(targetId, actionIds, ProposalPitch.Growth), Is.True);
            MailEvent result = game.Mail.Find(mail => mail.IsProposal && !mail.Resolved &&
                !string.IsNullOrEmpty(mail.TargetWorkId));
            result.ProposalStage = ProposalStage.Accepted;
            WorkGroup work = game.Groups.Find(group => group.Id == result.TargetWorkId);
            Assert.That(game.IsWorkVisible(work), Is.False);
            Assert.That(work.SoftDeadline, Is.Zero);
            Assert.That(work.HardDeadline, Is.Zero);

            game.AdvanceDay();

            Assert.That(result.ArrivalDay, Is.EqualTo(game.Day));
            Assert.That(result.Resolved, Is.True);
            Assert.That(result.Read, Is.False);
            Assert.That(game.Resources, Is.EqualTo(resources - estimate.CostCredits));
            Assert.That(game.IsWorkVisible(work), Is.True);
            Assert.That(game.Tasks.FindAll(task => task.GroupId == work.Id), Has.Count.EqualTo(3));
            Assert.That(work.RewardCredits, Is.EqualTo(estimate.RewardCredits));
            Assert.That(work.SoftDeadline, Is.EqualTo(game.Day + estimate.SoftDurationDays));
            Assert.That(work.HardDeadline, Is.EqualTo(game.Day + estimate.HardDurationDays));
        }

        [Test]
        public void ReadyMadeProposalBatchHasExpiryAndTwoToThreeWeekCadence()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.RandomWorkLimit = 0;
            var game = new MilestoneSimulation(data, 23);
            int firstBatchDay = game.Day;
            int nextBatchDay = game.NextProposalBatchDay;
            int expiryDay = game.ReadyMadeProposals[0].ExpiresDay;

            Assert.That(game.ReadyMadeProposals, Has.Count.InRange(3, 4));
            Assert.That(nextBatchDay - firstBatchDay, Is.InRange(14, 21));
            Assert.That(game.ReadyMadeProposals.TrueForAll(item => item.ExpiresDay == expiryDay), Is.True);
            Assert.That(game.Mail.Exists(mail => mail.Subject.Contains("새 제안 후보")), Is.True);

            while (game.Day <= expiryDay) game.AdvanceDay();
            Assert.That(game.ReadyMadeProposals, Is.Empty);
            while (game.Day < nextBatchDay) game.AdvanceDay();
            Assert.That(game.ReadyMadeProposals, Has.Count.InRange(3, 4));
        }

        [Test]
        public void SelectingOneReadyMadeProposalClosesTheRestOfItsBatch()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.RandomWorkLimit = 0;
            var game = new MilestoneSimulation(data, 24);
            ReadyMadeProposal selected = game.ReadyMadeProposals[0];
            int batchId = selected.BatchId;

            Assert.That(game.SubmitReadyMadeProposal(selected.Id), Is.True);

            Assert.That(game.ReadyMadeProposals.Exists(item => item.BatchId == batchId), Is.False);
            Assert.That(game.Groups.Exists(group => group.Id.StartsWith("proposal-work-")), Is.True);
        }

        [Test]
        public void RandomWorkChanceScaleCanSuppressOtherwiseGuaranteedMission()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 100;
            data.Balance.RandomWorkChanceScalePercent = 0;
            var game = new MilestoneSimulation(data, 1);
            game.Tasks.Find(task => task.Id == "survey").Kind = TaskKind.SideMission;

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
            data.Balance.RandomWorkLimit = 0;
            DisableAccidents(data);
            var game = new MilestoneSimulation(data, 1);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            Assert.That(game.Assign(survey.Id, 1), Is.True);

            while (survey.State != TaskState.Complete) game.AdvanceDay();

            Assert.That(survey.State, Is.EqualTo(TaskState.Complete));
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(game.Assign("habitat", 1), Is.True);
            Assert.That(survey.AssignedCharacter, Is.EqualTo(1));
            Assert.That(game.Tasks.Find(task => task.Id == "habitat").AssignedCharacter,
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
            Assert.That(game.Groups.Find(group => group.Id == "incident").Required, Is.False);
            Assert.That(game.IsWorkVisible(game.Groups.Find(group => group.Id == "incident")), Is.False);
        }

        [Test]
        public void DecliningIncidentWorkConsumesResourcesAndNeverAdmitsItToGantt()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 100;
            data.Balance.RandomWorkChanceScalePercent = 100;
            data.Balance.RandomWorkLimit = 3;
            var game = new MilestoneSimulation(data, 1);

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
            UseShortPlanningFixture(data);
            var game = new MilestoneSimulation(data, 1);

            TaskScheduleEstimate estimate = game.EstimateSchedule("survey", 1);

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
            UseShortPlanningFixture(data);
            var game = new MilestoneSimulation(data, 1);
            Assert.That(game.Assign("survey", 1), Is.True);

            TaskScheduleEstimate estimate = game.EstimateSchedule("habitat", 1);

            Assert.That(estimate.StartDay, Is.EqualTo(3));
            Assert.That(estimate.CompletionDay, Is.EqualTo(7));
            Assert.That(estimate.RollingStart, Is.False);
        }

        [Test]
        public void UnassignedBlockerUsesBaselinePreviewAndMovesWithCurrentDay()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UseShortPlanningFixture(data);
            var game = new MilestoneSimulation(data, 1);
            game.SetCompetencyAutoAssignment(false);

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
            UseShortPlanningFixture(data);
            data.Crew[1].DailyOutput = 2f;
            var game = new MilestoneSimulation(data, 1);

            TaskScheduleEstimate estimate = game.EstimateSchedule("survey", 1);

            Assert.That(estimate.ExpectedDailyOutput, Is.EqualTo(3.45f).Within(.001f));
            Assert.That(estimate.DurationDays, Is.EqualTo(1));
            Assert.That(estimate.CompletionDay, Is.EqualTo(1));
        }

        [Test]
        public void UnassignedTasksPreviewAtOneWorkPerDayInDependencyOrder()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UseShortPlanningFixture(data);
            var game = new MilestoneSimulation(data, 1);

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
            TaskSystemData data = TaskSystemDataLoader.Load();
            UseShortPlanningFixture(data);
            var game = new MilestoneSimulation(data, 1);

            TaskScheduleEstimate launch = game.EstimatePreviewSchedule("launch");

            Assert.That(launch.StartDay, Is.EqualTo(8));
            Assert.That(launch.CompletionDay, Is.EqualTo(10));
        }

        [Test]
        public void AssignedPredecessorOutputMovesUnassignedSuccessorPreview()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UseShortPlanningFixture(data);
            data.Crew[1].DailyOutput = 2f;
            var game = new MilestoneSimulation(data, 1);
            game.Assign("survey", 1);

            TaskScheduleEstimate habitat = game.EstimatePreviewSchedule("habitat");

            Assert.That(habitat.StartDay, Is.EqualTo(2));
            Assert.That(habitat.CompletionDay, Is.EqualTo(5));
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
            Assert.That(game.TotalPayroll(), Is.EqualTo(5));
            Assert.That(game.Resources, Is.EqualTo(95));
            Assert.That(game.LastReport.Lines, Has.Some.Contains("기본급 합계 -5자원"));
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
        public void StatusTextOnlyOverridesEventLinesForResourceDepletion()
        {
            var report = new DayReport();
            report.Lines.Add("event");

            Assert.That(MilestonePrototypeController.FormatStatus(report, true, false), Is.EqualTo("event"));
            Assert.That(MilestonePrototypeController.FormatStatus(report, false, true), Is.EqualTo("자원 고갈 — 생존 기록 종료"));
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
            Assert.That(PatchBootstrapper.BaseVersion, Is.EqualTo(11));
        }

        [Test]
        public void TimedPopupAndToastExpireIndependently()
        {
            var presenter = new TimedNotificationPresenter();
            presenter.ShowPopup(new TimedPopupData { DurationSeconds = 5f }, 10f);
            presenter.ShowToast(new TimedToastData { DurationSeconds = 3f }, 10f);

            presenter.Update(12.99f);
            Assert.That(presenter.HasPopup, Is.True);
            Assert.That(presenter.HasToast, Is.True);
            presenter.Update(13f);
            Assert.That(presenter.HasPopup, Is.True);
            Assert.That(presenter.HasToast, Is.False);
            presenter.Update(15f);
            Assert.That(presenter.HasPopup, Is.False);
        }

        [Test]
        public void ZeroDurationPopupWaitsForAButton()
        {
            var presenter = new TimedNotificationPresenter();
            presenter.ShowPopup(new TimedPopupData { DurationSeconds = 0f }, 2f);

            presenter.Update(999f);

            Assert.That(presenter.HasPopup, Is.True);
        }

        [Test]
        public void ExpandingRingMaskOnlyShowsTheRingBand()
        {
            Assert.That(TimedNotificationPresenter.RingAlpha(0f), Is.EqualTo(0f));
            Assert.That(TimedNotificationPresenter.RingAlpha(.78f), Is.EqualTo(1f));
            Assert.That(TimedNotificationPresenter.RingAlpha(1.2f), Is.EqualTo(0f));
        }

        [Test]
        public void RadialRotationPivotMatchesTheScaledTextureCenter()
        {
            Rect effectRect = new Rect(100f, 50f, 112f, 112f);

            Vector2 pivot = TimedNotificationPresenter.ScaledRotationPivot(effectRect, 1.8f);

            Assert.That(pivot.x, Is.EqualTo(effectRect.center.x * 1.8f).Within(.001f));
            Assert.That(pivot.y, Is.EqualTo(effectRect.center.y * 1.8f).Within(.001f));
        }

        [Test]
        public void BurstLoopHasAVisibleWindowAndARealPause()
        {
            Assert.That(TimedNotificationPresenter.BurstPhase(0f, 1), Is.EqualTo(-1f));
            Assert.That(TimedNotificationPresenter.BurstPhase(.12f, 1), Is.EqualTo(0f));
            Assert.That(TimedNotificationPresenter.BurstPhase(.53f, 1), Is.EqualTo(.5f).Within(.001f));
            Assert.That(TimedNotificationPresenter.BurstPhase(1.2f, 1), Is.EqualTo(-1f));
            Assert.That(TimedNotificationPresenter.BurstPhase(1.77f, 1), Is.EqualTo(0f).Within(.001f));
        }

        [Test]
        public void GlowMaskHasATransparentEdgeAndBrightCore()
        {
            Assert.That(TimedNotificationPresenter.GlowAlpha(1.4f), Is.EqualTo(0f));
            Assert.That(TimedNotificationPresenter.GlowAlpha(0f), Is.EqualTo(1f));
            Assert.That(TimedNotificationPresenter.GlowAlpha(.48f), Is.InRange(.2f, .3f));
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
            var game = new MilestoneSimulation(1);
            WorkGroup foundation = game.Groups.Find(group => group.Id == "foundation");
            int softDeadline = foundation.SoftDeadline;
            int hardDeadline = foundation.HardDeadline;

            MailEvent weekly = game.Mail.Find(mail => mail.IsWeeklyFieldReport);
            Assert.That(weekly, Is.Not.Null);
            Assert.That(weekly.Subject, Is.EqualTo("주간현장 현황공유"));
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
            var game = new MilestoneSimulation(1);
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
            var game = new MilestoneSimulation(data, 1);

            while (game.Day < 8) game.AdvanceDay();

            MailEvent weekly = game.Mail.Find(mail => mail.Id == "weekly-field-8");
            Assert.That(weekly, Is.Not.Null);
            Assert.That(weekly.WeeklyFieldItems, Has.Length.EqualTo(1));
            Assert.That(weekly.WeeklyFieldItems[0].SourceMailId, Is.EqualTo("mail-4"));
            Assert.That(game.Mail.Exists(mail => mail.IsBossRequest && !mail.IsWeeklyFieldReport), Is.True);
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
            MailEvent weekly = original.Mail.Find(mail => mail.IsWeeklyFieldReport);
            original.DecideWeeklyFieldItem(weekly.Id, "mail-1", true);

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
        public void WorkPrerequisiteLocksLaunchUntilFoundationCompletes()
        {
            var game = new MilestoneSimulation(1);

            Assert.That(game.Groups.Find(group => group.Id == "launch").State, Is.EqualTo(WorkState.Locked));
            Assert.That(game.Tasks.Find(task => task.Id == "launch").State, Is.EqualTo(TaskState.Locked));
            Assert.That(game.Assign("launch", 0), Is.False);
        }

        [Test]
        public void FinalWorkBecomesVisibleOnDaySixty()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.Balance.BaseSideMissionChance = 0;
            var original = new MilestoneSimulation(data, 1);
            CampaignSnapshot snapshot = original.CreateSnapshot();
            snapshot.Day = 59;
            foreach (WorkTask task in snapshot.Tasks)
                if (task.GroupId == "foundation") task.State = TaskState.Complete;
            System.Array.Find(snapshot.Groups, group => group.Id == "foundation").State =
                WorkState.Complete;
            var game = new MilestoneSimulation(data, 2);
            Assert.That(game.Restore(snapshot), Is.True);
            WorkGroup finalWork = game.Groups.Find(group => group.Id == "launch");

            Assert.That(game.IsWorkVisible(finalWork), Is.False);
            Assert.That(game.Tasks.Find(task => task.Id == "launch").State, Is.EqualTo(TaskState.Locked));
            game.AdvanceDay();

            Assert.That(game.Day, Is.EqualTo(60));
            Assert.That(game.IsWorkVisible(finalWork), Is.True);
            Assert.That(game.Tasks.Find(task => task.Id == "launch").State,
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
            TaskSystemData data = TaskSystemDataLoader.Load();
            UseShortPlanningFixture(data);
            data.Balance.RandomWorkLimit = 0;
            foreach (CrewMember member in data.Crew) member.DailyOutput = 1f;
            ForceSuccessOutcome(data);
            var game = new MilestoneSimulation(data, 1);
            CompleteSurvey(game);
            Assert.That(game.Assign("power", 0), Is.True);
            WorkTask safety = game.Tasks.Find(task => task.Id == "safety");
            safety.PrerequisiteId = null;
            safety.State = TaskState.Available;

            Assert.That(game.AssignParallel("safety", 0), Is.True);
            game.AdvanceDay();

            Assert.That(game.Tasks.Find(task => task.Id == "power").Progress, Is.EqualTo(1.5f));
            Assert.That(safety.State, Is.EqualTo(TaskState.Complete));
            Assert.That(game.Crew[0].Fatigue, Is.EqualTo(16));
        }

        [Test]
        public void InterruptingTaskAddsOneDayContextCost()
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            UseShortPlanningFixture(data);
            data.Balance.RandomWorkLimit = 0;
            foreach (CrewMember member in data.Crew) member.DailyOutput = 1f;
            ForceSuccessOutcome(data);
            var game = new MilestoneSimulation(data, 1);
            CompleteSurvey(game);
            WorkTask habitat = game.Tasks.Find(task => task.Id == "habitat");
            Assert.That(game.Assign(habitat.Id, 0), Is.True);
            game.AdvanceDay();

            Assert.That(game.Assign(habitat.Id, -1), Is.True);
            Assert.That(habitat.Progress, Is.EqualTo(1.25f));
            Assert.That(habitat.ContextCostDays, Is.EqualTo(1f));
            Assert.That(habitat.EffectiveRequiredWork, Is.EqualTo(habitat.RequiredWork + 1f));
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
            Assert.That(preview.PrimaryFatigue, Is.EqualTo(4));
            Assert.That(preview.ParallelFatigue, Is.EqualTo(9));
            Assert.That(preview.CanRunInParallel, Is.False);
        }

        [Test]
        public void RestoredScrollbarIsAtLeastTwiceTheDefaultWidth()
        {
            Assert.That(MilestonePrototypeController.RestoredScrollbarWidth(0f),
                Is.EqualTo(MilestonePrototypeController.DefaultScrollbarWidth * 2f));
            Assert.That(MilestonePrototypeController.RestoredScrollbarWidth(20f), Is.EqualTo(40f));
        }

        [Test]
        public void GanttOwnsAnInsetScrollViewportSoItsHorizontalBarStaysFixed()
        {
            Assert.That(MilestonePrototypeController.UsesIndependentWindowScroll("gantt"), Is.True);
            Assert.That(MilestonePrototypeController.UsesIndependentWindowScroll("mail"), Is.False);
            Assert.That(MilestonePrototypeController.GanttViewportHeight(500f), Is.EqualTo(390f));
            Assert.That(MilestonePrototypeController.GanttViewportHeight(200f), Is.EqualTo(120f));
        }

        [Test]
        public void GanttTodayButtonLeavesThreePastDaysVisibleAndClampsAtTimelineEdges()
        {
            Assert.That(MilestonePrototypeController.GanttTodayScrollX(1, 28f, 280f, 1000f),
                Is.Zero);
            Assert.That(MilestonePrototypeController.GanttTodayScrollX(20, 28f, 280f, 1000f),
                Is.EqualTo(448f));
            Assert.That(MilestonePrototypeController.GanttTodayScrollX(40, 28f, 280f, 1000f),
                Is.EqualTo(720f));
        }

        [Test]
        public void PrioritySchedulePlacesDependentTasksAfterTheirForecastPredecessor()
        {
            var game = new MilestoneSimulation(41);
            WorkTask survey = game.Tasks.Find(task => task.Id == "survey");
            WorkTask habitat = game.Tasks.Find(task => task.Id == "habitat");

            TaskScheduleEstimate[] schedule = game.BuildPrioritySchedule();
            TaskScheduleEstimate surveyPlan = Array.Find(schedule, item => item.TaskId == survey.Id);
            TaskScheduleEstimate habitatPlan = Array.Find(schedule, item => item.TaskId == habitat.Id);

            Assert.That(surveyPlan, Is.Not.Null);
            Assert.That(habitatPlan, Is.Not.Null);
            Assert.That(habitatPlan.StartDay, Is.GreaterThan(surveyPlan.CompletionDay));
        }

        [Test]
        public void GanttDependencyAnchorsPreferTheDisplayedPrioritySchedule()
        {
            var displayed = new TaskScheduleEstimate { TaskId = "next", StartDay = 12 };
            var isolated = new TaskScheduleEstimate { TaskId = "next", StartDay = 4 };

            Assert.That(MilestonePrototypeController.GanttScheduleForTask(
                new[] { displayed }, "next", isolated), Is.SameAs(displayed));
            Assert.That(MilestonePrototypeController.GanttScheduleForTask(
                new TaskScheduleEstimate[0], "next", isolated), Is.SameAs(isolated));
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
        public void VisiblePanelBlocksPointerInsideItsEmptyArea()
        {
            Rect panel = new Rect(100, 80, 400, 300);

            Assert.That(MilestonePrototypeController.IsPointInsideVisiblePanel(
                panel, false, new Vector2(250, 200)), Is.True);
            Assert.That(MilestonePrototypeController.IsPointInsideVisiblePanel(
                panel, false, new Vector2(50, 50)), Is.False);
        }

        [Test]
        public void MinimizedPanelDoesNotBlockPointer()
        {
            Assert.That(MilestonePrototypeController.IsPointInsideVisiblePanel(
                new Rect(100, 80, 400, 300), true, new Vector2(250, 200)), Is.False);
        }

        [Test]
        public void PointerControlEventsAreBlockedWithoutChangingRepaintState()
        {
            Assert.That(MilestonePrototypeController.CanActivateControl(EventType.MouseDown), Is.True);
            Assert.That(MilestonePrototypeController.CanActivateControl(EventType.MouseDrag), Is.True);
            Assert.That(MilestonePrototypeController.CanActivateControl(EventType.MouseUp), Is.True);
            Assert.That(MilestonePrototypeController.CanActivateControl(EventType.Repaint), Is.False);
            Assert.That(MilestonePrototypeController.CanActivateControl(EventType.Layout), Is.False);
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
        public void WindowTitleBarAndControlsAreTwentyFivePercentThicker()
        {
            Rect result = MilestonePrototypeController.WindowDragHitRect(710);
            Rect minimize = MilestonePrototypeController.WindowMinimizeButtonRect(710);
            Rect close = MilestonePrototypeController.WindowCloseButtonRect(710);

            Assert.That(result, Is.EqualTo(new Rect(0, 0, 600, 65)));
            Assert.That(MilestonePrototypeController.WindowTitleBarHeight, Is.EqualTo(25f));
            Assert.That(MilestonePrototypeController.WindowContentTopSpacing, Is.EqualTo(6f));
            Assert.That(minimize.height, Is.EqualTo(25f));
            Assert.That(close, Is.EqualTo(new Rect(674f, 1f, 31f, 25f)));
        }

        [Test]
        public void ResizeHandleExtendsInsideAndOutsideBottomRightCorner()
        {
            Rect result = MilestonePrototypeController.ResizeHandleRect(
                new Rect(100, 80, 710, 500));

            Assert.That(result, Is.EqualTo(new Rect(770, 540, 80, 80)));
            Assert.That(result.Contains(new Vector2(825, 595)), Is.True);
        }

        [Test]
        public void WindowResizeTracksPointerAndStaysInsideDesktop()
        {
            Rect original = new Rect(100, 80, 500, 350);

            Rect enlarged = MilestonePrototypeController.CalculateResizedWindowRect(
                original, new Vector2(600, 430), new Vector2(750, 520), 1280, 720);
            Rect clamped = MilestonePrototypeController.CalculateResizedWindowRect(
                original, new Vector2(600, 430), new Vector2(2000, 2000), 1280, 720);

            Assert.That(enlarged, Is.EqualTo(new Rect(100, 80, 650, 440)));
            Assert.That(clamped.xMax, Is.EqualTo(1274));
            Assert.That(clamped.yMax, Is.EqualTo(670));
        }

        [Test]
        public void WindowGuiIdsRemainStableWhenFocusOrderChanges()
        {
            int nextGuiId = 100;

            int first = MilestonePrototypeController.AllocateStableWindowGuiId(ref nextGuiId);
            int second = MilestonePrototypeController.AllocateStableWindowGuiId(ref nextGuiId);

            Assert.That(first, Is.EqualTo(100));
            Assert.That(second, Is.EqualTo(101));
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void WindowResizeHonorsMinimumSize()
        {
            Rect result = MilestonePrototypeController.CalculateResizedWindowRect(
                new Rect(100, 80, 710, 500), new Vector2(810, 580), Vector2.zero,
                1280, 720);

            Assert.That(result.width, Is.EqualTo(420));
            Assert.That(result.height, Is.EqualTo(280));
        }

        [Test]
        public void PinchResizesAroundCenterAndDragMovesWindow()
        {
            Rect result = MilestonePrototypeController.CalculatePinchedWindowRect(
                new Rect(100, 80, 500, 350), new Vector2(350, 255),
                new Vector2(400, 285), 1.2f, 1280, 720);

            Assert.That(result.x, Is.EqualTo(100).Within(.001f));
            Assert.That(result.y, Is.EqualTo(75).Within(.001f));
            Assert.That(result.width, Is.EqualTo(600).Within(.001f));
            Assert.That(result.height, Is.EqualTo(420).Within(.001f));
        }

        [Test]
        public void PinchRespectsMinimumWindowSize()
        {
            Rect result = MilestonePrototypeController.CalculatePinchedWindowRect(
                new Rect(100, 80, 710, 500), new Vector2(455, 330),
                new Vector2(455, 330), .1f, 1280, 720);

            Assert.That(result.width, Is.EqualTo(420));
            Assert.That(result.height, Is.EqualTo(280));
        }

        [Test]
        public void TouchCoordinatesConvertFromScreenBottomLeftToLogicalTopLeft()
        {
            Vector2 result = MilestonePrototypeController.TouchToLogicalPosition(
                new Vector2(360, 200), 1280, 2f);

            Assert.That(result, Is.EqualTo(new Vector2(180, 540)));
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
                    MessengerSeenUpdateCount = 12,
                    Windows = new[]
                    {
                        new WindowSnapshot { Id = "mail", Width = 640f, Height = 420f, Open = true }
                    }
                };

                Assert.That(ProjectWSaveStore.SaveDesktop(key, snapshot), Is.True);
                Assert.That(ProjectWSaveStore.TryLoadDesktop(key, out DesktopSnapshot loaded), Is.True);
                Assert.That(loaded.UiMagnification, Is.EqualTo(1.4f));
                Assert.That(loaded.MessengerSeenUpdateCount, Is.EqualTo(12));
                Assert.That(loaded.Windows[0].Width, Is.EqualTo(640f));
                Assert.That(loaded.Windows[0].Height, Is.EqualTo(420f));
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
        public void OptionsIconRemainsInTwoRowSixColumnGridAtMaximumScale()
        {
            float width = 1280 / MilestonePrototypeController.CalculateUiScale(1280, 2.2f);
            Rect options = MilestonePrototypeController.DesktopIconRect(9, width);
            Rect optionsLabel = MilestonePrototypeController.DesktopIconLabelRect(9, width);
            Rect report = MilestonePrototypeController.DesktopReportRect(width, 720 / 2.2f);

            Assert.That(options.xMax, Is.LessThanOrEqualTo(width));
            Assert.That(optionsLabel.yMax, Is.LessThan(report.y));
        }

        [Test]
        public void DesktopIconsUseOneThirdSizeGapsAndSquareButtons()
        {
            Rect first = MilestonePrototypeController.DesktopIconRect(0, 1280);
            Rect second = MilestonePrototypeController.DesktopIconRect(1, 1280);
            Rect sixth = MilestonePrototypeController.DesktopIconRect(5, 1280);
            Rect nextRow = MilestonePrototypeController.DesktopIconRect(6, 1280);
            Rect firstLabel = MilestonePrototypeController.DesktopIconLabelRect(0, 1280);

            Assert.That(first.width, Is.EqualTo(first.height));
            Assert.That(second.x - first.xMax, Is.EqualTo(first.width / 3f).Within(.001f));
            Assert.That(sixth.y, Is.EqualTo(first.y));
            Assert.That(nextRow.x, Is.EqualTo(first.x));
            Assert.That(firstLabel.y, Is.EqualTo(first.yMax - 8f));
            Assert.That(firstLabel.height, Is.EqualTo(22f));
            Assert.That(nextRow.y - first.yMax - 18f, Is.EqualTo(first.height / 3f).Within(.001f));
        }

        [Test]
        public void DesktopBadgeOverlapsIconTopRightCorner()
        {
            Rect icon = MilestonePrototypeController.DesktopIconRect(0, 1280);
            Rect badge = MilestonePrototypeController.DesktopIconBadgeRect(0, 1280);

            Assert.That(badge.center.x, Is.EqualTo(icon.xMax));
            Assert.That(badge.y, Is.LessThan(icon.y));
            Assert.That(badge.Overlaps(icon), Is.True);
        }

        [Test]
        public void DesktopBadgeCountCanBeUsedByAnyDesktopApp()
        {
            var host = new GameObject("desktop-badge-test");
            try
            {
                var controller = host.AddComponent<MilestonePrototypeController>();
                controller.SetDesktopBadgeCount("report", 3);
                controller.SetDesktopBadgeCount("options", 7);

                Assert.That(controller.DesktopBadgeCount("report"), Is.EqualTo(3));
                Assert.That(controller.DesktopBadgeCount("options"), Is.EqualTo(7));
                Assert.That(controller.DesktopBadgeCount("mail"), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DesktopIconNamesStayOnOneLineAndWithinSevenCharacters()
        {
            string[] ids =
            {
                "mail", "gantt", "milestone", "workers", "report",
                "codex", "messenger", "profile", "options"
            };

            foreach (string id in ids)
            {
                string name = MilestonePrototypeController.DesktopIconName(id);
                Assert.That(name, Does.Not.Contain("\n"), id);
                Assert.That(name.Length, Is.LessThanOrEqualTo(7), id);
            }
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
                MailEvent weekly = game.Mail.Find(mail => mail.IsWeeklyFieldReport);
                game.DecideWeeklyFieldItem(weekly.Id, "mail-1", true);
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
            DisableAccidents(data);
            ForceSuccessOutcome(data);
            var game = new MilestoneSimulation(data, 17);
            game.SetCompetencyAutoAssignment(false);
            game.AdvanceDay();
            WorkTask urgentTask = game.Tasks.Find(task => task.Id == "water-intake-check");
            WorkTask lowerTask = game.Tasks.Find(task => task.Id == "survey");
            int bestWorker = 1;
            foreach (CrewMember member in game.Crew)
                foreach (int competency in urgentTask.RequiredCompetencies)
                    member.Competencies[competency] = 0;
            foreach (int competency in urgentTask.RequiredCompetencies)
                game.Crew[bestWorker].Competencies[competency] = 7;
            Assert.That(game.Assign(lowerTask.Id, bestWorker), Is.True);
            while (game.MoveWorkPriority("water-loop", -1)) { }
            Assert.That(game.ConfigureWorkFocus("water-loop", true, false), Is.True);
            Assert.That(game.Groups.Find(group => group.Id == "water-loop").Priority,
                Is.LessThan(game.Groups.Find(group => group.Id == "foundation").Priority));
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

        private static void CompleteSurvey(MilestoneSimulation game)
        {
            Assert.That(game.Assign("survey", 1), Is.True);
            for (int i = 0; i < 8 && game.Tasks.Find(task => task.Id == "survey").State != TaskState.Complete; i++)
                game.AdvanceDay();
            Assert.That(game.Tasks.Find(task => task.Id == "survey").State, Is.EqualTo(TaskState.Complete));
        }

        private static void UseShortPlanningFixture(TaskSystemData data)
        {
            Array.Find(data.Tasks, task => task.Id == "survey").RequiredWork = 3f;
            Array.Find(data.Tasks, task => task.Id == "power").RequiredWork = 4f;
            Array.Find(data.Tasks, task => task.Id == "habitat").RequiredWork = 4f;
            Array.Find(data.Tasks, task => task.Id == "safety").RequiredWork = 1f;
            Array.Find(data.Tasks, task => task.Id == "launch").RequiredWork = 3f;
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
