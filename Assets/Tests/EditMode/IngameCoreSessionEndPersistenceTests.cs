using System.Collections.Generic;
using NUnit.Framework;
using ProjectW.IngameCore;

namespace ProjectW.Tests.EditMode
{
    public class IngameCoreSessionEndPersistenceTests
    {
        [Test]
        public void CoreLoopT03_ObjectiveComplete_WhenNoHigherPriorityReason()
        {
            var result = SessionEndResolver.ResolveSessionEnd(totalWipe: false, emergencyExtract: false, objectiveComplete: true);

            Assert.IsTrue(result.IsEnd);
            Assert.AreEqual(SessionTerminationReason.ObjectiveComplete, result.EndReason);
            CollectionAssert.IsEmpty(result.SuppressedReasons);
        }

        [Test]
        public void CoreLoop_TerminationPriority_RecordsSuppressedReasons()
        {
            var result = SessionEndResolver.ResolveSessionEnd(totalWipe: true, emergencyExtract: true, objectiveComplete: true);

            Assert.IsTrue(result.IsEnd);
            Assert.AreEqual(SessionTerminationReason.TotalWipe, result.EndReason);
            CollectionAssert.AreEqual(
                new[] { SessionTerminationReason.EmergencyExtract, SessionTerminationReason.ObjectiveComplete },
                result.SuppressedReasons);
        }

        [Test]
        public void CoreLoopT21_PersistRetry_TransitionsToRetryThenSuccess()
        {
            var writer = new SequenceSnapshotWriter(
                new SnapshotWriteResult(false, "E-PST-301"),
                new SnapshotWriteResult(true));
            var delayedBackoff = new List<int>();
            var service = new SnapshotPersistenceService(writer, delayedBackoff.Add);
            var snapshot = new SessionSnapshotDto
            {
                SessionId = "S-1",
                TickIndex = 17,
                LoopState = "Resolve",
                TerminationResultCode = "ObjectiveComplete",
                LastAppliedTick = 17
            };
            var config = new SessionConfig(maxRetry: 2, backoffMs: 150);

            var result = service.PersistWithRetry(snapshot, config);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.AttemptCount);
            Assert.AreEqual(PersistenceResolutionState.PersistenceRetry, result.State);
            CollectionAssert.AreEqual(new[] { "E-PST-301" }, result.LoggedErrorCodes);
            CollectionAssert.AreEqual(new[] { 150 }, delayedBackoff);
        }

        [Test]
        public void PersistRetry_Exhausted_EntersSafeHaltAndLogsEPst399()
        {
            var writer = new SequenceSnapshotWriter(
                new SnapshotWriteResult(false, "E-PST-301"),
                new SnapshotWriteResult(false, "E-PST-301"));
            var service = new SnapshotPersistenceService(writer);
            var config = new SessionConfig(maxRetry: 1, backoffMs: 0);

            var result = service.PersistWithRetry(new SessionSnapshotDto { SessionId = "S-2" }, config);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PersistenceResolutionState.SafeHalt, result.State);
            Assert.AreEqual("E-PST-399", result.ErrorCode);
            CollectionAssert.AreEqual(new[] { "E-PST-301", "E-PST-301", "E-PST-399" }, result.LoggedErrorCodes);
        }

        private sealed class SequenceSnapshotWriter : ISnapshotWriter
        {
            private readonly Queue<SnapshotWriteResult> results;

            public SequenceSnapshotWriter(params SnapshotWriteResult[] results)
            {
                this.results = new Queue<SnapshotWriteResult>(results);
            }

            public SnapshotWriteResult PersistSnapshot(SessionSnapshotDto snapshot)
            {
                if (results.Count == 0)
                {
                    return new SnapshotWriteResult(true);
                }

                return results.Dequeue();
            }
        }
    }
}
