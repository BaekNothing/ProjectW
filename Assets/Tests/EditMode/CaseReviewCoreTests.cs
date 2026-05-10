using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectW.IngameCore.CaseReview;
using ProjectW.IngameMvp;
using UnityEngine;

namespace ProjectW.Tests.EditMode
{
    public sealed class CaseReviewCoreTests
    {
        [Test]
        public void Replay_WithSameSeedAndTape_ProducesSameSnapshotHash()
        {
            string[] tape = { "plan", "adjust E-108 B-04,C-22", "confirm plan", "report day" };

            var first = CaseReviewGame.Replay(77, tape);
            var second = CaseReviewGame.Replay(77, tape);

            Assert.AreEqual(first.SnapshotHash, second.SnapshotHash);
            Assert.AreEqual(tape.Length, first.CommandCount);
        }

        [Test]
        public void SummaryOnlyApprove_RaisesLatentRisk()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            ForceNoon(state);
            var item = state.Queue.First(e => e.Id == "E-108");
            var before = item.LatentRisk;

            CaseReviewGame.Dispatch(state, "summary E-108");
            var approve = CaseReviewGame.Dispatch(state, "approve E-108");

            Assert.IsTrue(approve.Success);
            Assert.Greater(item.LatentRisk, before);
            Assert.IsTrue(item.ApprovedFromSummaryOnly);
        }

        [Test]
        public void EquipLog_IsUnavailableBeforeArrivalAndVisibleAfterAdvance()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            ForceNoon(state);

            var early = CaseReviewGame.Dispatch(state, "log E-108 equip");
            CaseReviewGame.Dispatch(state, "advance 100");
            var late = CaseReviewGame.Dispatch(state, "log E-108 equip");

            Assert.IsFalse(early.Success);
            Assert.AreEqual("ERR031", early.Code);
            Assert.IsTrue(late.Success);
            Assert.IsTrue(late.Lines.Any(line => line.Contains("SIGNAL_LOSS")));
        }

        [Test]
        public void RedirectBudget_IsConsumedAndThenBlocks()
        {
            var state = CaseReviewGame.Init(new GameConfig { RedirectBudgetPerDay = 1 }, 1);
            ForceNoon(state);

            var ok = CaseReviewGame.Dispatch(state, "redirect E-108 B-04,C-22");
            var fail = CaseReviewGame.Dispatch(state, "redirect R-211 D-11");

            Assert.IsTrue(ok.Success);
            Assert.IsFalse(fail.Success);
            Assert.AreEqual("ERR051", fail.Code);
        }

        [Test]
        public void ConfirmPlan_SimulatesOperationsAndMovesToEvening()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);

            var result = CaseReviewGame.Dispatch(state, "confirm plan");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(Slot.Evening, state.Slot);
            Assert.AreEqual(0, state.TimeRemainingSec);
            Assert.IsTrue(state.Queue.All(e => e.AutoResolved));
            Assert.AreEqual(1, state.Reports.Count);
        }

        [Test]
        public void Report_AfterConfirmPlan_ReturnsGeneratedDailyReport()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            CaseReviewGame.Dispatch(state, "adjust E-108 B-04,C-22");
            CaseReviewGame.Dispatch(state, "confirm plan");

            var report = CaseReviewGame.Dispatch(state, "report");

            Assert.IsTrue(report.Success);
            Assert.AreEqual(1, state.Reports.Count);
            Assert.IsTrue(report.Lines.Count > 10);
            Assert.IsFalse(string.IsNullOrWhiteSpace(state.Reports[0].Body));
        }

        [Test]
        public void EventReports_MustBeReviewedBeforeNextDay()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            CaseReviewGame.Dispatch(state, "adjust E-108 B-04,C-22");
            CaseReviewGame.Dispatch(state, "confirm plan");

            var eventReport = CaseReviewGame.Dispatch(state, "report E-108");
            var blocked = CaseReviewGame.Dispatch(state, "next day");
            CaseReviewGame.Dispatch(state, "review all");
            var next = CaseReviewGame.Dispatch(state, "next day");

            Assert.IsTrue(eventReport.Success);
            Assert.IsTrue(eventReport.Lines.Count > 3);
            Assert.IsFalse(blocked.Success);
            Assert.AreEqual("ERR091", blocked.Code);
            Assert.IsTrue(next.Success);
            Assert.AreEqual(2, state.Day);
            Assert.AreEqual(Slot.Morning, state.Slot);
            Assert.IsTrue(state.MorningPlan.Entries.Count > 0);
        }

        [Test]
        public void HighRetentionRisk_CreatesAttritionAndHiringWork()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            CaseReviewGame.Dispatch(state, "adjust E-108 B-04,C-22");
            CaseReviewGame.Dispatch(state, "confirm plan");
            var target = state.Staff.First(s => s.Id == "B-04");
            target.RetentionRisk = 90;

            CaseReviewGame.Dispatch(state, "review all");
            var next = CaseReviewGame.Dispatch(state, "next day");

            Assert.IsTrue(next.Success);
            Assert.IsTrue(target.HasLeft);
            Assert.Greater(state.TalentShortage, 0);
            Assert.IsTrue(state.Queue.Any(e => e.Kind == "hiring" && e.Status != CaseStatus.Closed));
        }

        [Test]
        public void MorningPlan_CanBeAdjustedAndConfirmed()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);

            var blocked = CaseReviewGame.Dispatch(state, "advance NOON");
            var adjust = CaseReviewGame.Dispatch(state, "adjust E-108 B-04,C-22");
            var confirm = CaseReviewGame.Dispatch(state, "confirm plan");
            var eventCase = state.Queue.First(e => e.Id == "E-108");

            Assert.IsFalse(blocked.Success);
            Assert.AreEqual("ERR071", blocked.Code);
            Assert.IsTrue(adjust.Success);
            Assert.IsTrue(confirm.Success);
            Assert.IsTrue(state.MorningPlan.Confirmed);
            Assert.AreEqual("B-04,C-22", string.Join(",", eventCase.AssignedPersonnel));
            Assert.IsFalse(string.IsNullOrWhiteSpace(eventCase.ResultSummary));
        }

        [Test]
        public void SessionController_DispatchesCommandsAndRestoresSnapshots()
        {
            var go = new GameObject("CaseReviewSessionControllerTest");
            try
            {
                var controller = go.AddComponent<CaseReviewSessionController>();
                controller.Initialize(1042);

                var plan = controller.DispatchCommand("plan");
                var snapshot = controller.Snapshot();
                controller.DispatchCommand("adjust E-108 B-04,C-22");
                controller.RestoreSnapshot(snapshot);

                Assert.IsTrue(plan.Success);
                Assert.IsFalse(string.IsNullOrWhiteSpace(controller.LastOutput));
                Assert.AreEqual(Slot.Morning, controller.State.Slot);
                Assert.IsFalse(controller.State.MorningPlan.Entries.First(e => e.EventId == "E-108").Adjusted);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void ForceNoon(GameState state)
        {
            state.MorningPlan.Confirmed = true;
            state.Slot = Slot.Noon;
            state.TimeRemainingSec = state.Config.NoonSeconds;
        }
    }
}
