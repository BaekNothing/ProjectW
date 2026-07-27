using System;
using NUnit.Framework;
using ProjectW.Bootstrap;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class MilestoneSimulationTests
    {
        [Test]
        public void MatchingSpecialtyAdvancesWorkAndAddsFatigue()
        {
            var game = new MilestoneSimulation(1);
            Assert.That(game.Assign("survey", 1), Is.True);
            game.AdvanceDay();
            Assert.That(game.Tasks[0].Progress, Is.EqualTo(6));
            Assert.That(game.Crew[1].Fatigue, Is.EqualTo(9));
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
        public void ResolvingMailAppliesItsRuleOnlyOnce()
        {
            var game = new MilestoneSimulation(1);
            WorkTask survey = game.Tasks.Find(t => t.Id == "survey");
            int deadline = survey.Deadline;

            Assert.That(game.ResolveMail("mail-1"), Is.True);
            Assert.That(survey.Deadline, Is.EqualTo(deadline - 1));
            Assert.That(survey.Importance, Is.EqualTo(ImportanceLevel.High));
            Assert.That(survey.Records, Has.Count.EqualTo(1));
            Assert.That(game.ResolveMail("mail-1"), Is.False);
            Assert.That(survey.Deadline, Is.EqualTo(deadline - 1));
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
            Assert.That(restored.Mail.Find(m => m.Id == "mail-1").Resolved, Is.True);
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
        public void CampaignPlayerPrefsRoundTripsAndRejectsCorruptJson()
        {
            string key = $"projectw.test.{Guid.NewGuid():N}";
            try
            {
                var game = new MilestoneSimulation(1);
                game.ResolveMail("mail-1");
                Assert.That(ProjectWSaveStore.SaveCampaign(key, game.CreateSnapshot()), Is.True);
                Assert.That(ProjectWSaveStore.TryLoadCampaign(key, out CampaignSnapshot loaded), Is.True);
                Assert.That(loaded.Mail[0].Resolved, Is.True);

                PlayerPrefs.SetString(key, "{not-json");
                Assert.That(ProjectWSaveStore.TryLoadCampaign(key, out _), Is.False);
            }
            finally
            {
                ProjectWSaveStore.Delete(key);
            }
        }
    }
}
