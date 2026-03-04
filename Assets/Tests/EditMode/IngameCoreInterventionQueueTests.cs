using System.Collections.Generic;
using NUnit.Framework;
using ProjectW.IngameCore;
using ProjectW.IngameCore.Config;

namespace ProjectW.Tests.EditMode
{
    public class IngameCoreInterventionQueueTests
    {
        [Test]
        public void ApplyInterventions_UsesNextTickBoundaryAndTieBreakOrder()
        {
            var service = new InterventionQueueService();
            var queue = new List<InterventionCommandRow>
            {
                new InterventionCommandRow
                {
                    CommandId = "cmd-2",
                    IssuedTick = 1,
                    ApplyTick = 1,
                    CommandType = "policy",
                    TargetScope = "global",
                    Priority = 2
                },
                new InterventionCommandRow
                {
                    CommandId = "cmd-1",
                    IssuedTick = 1,
                    ApplyTick = 1,
                    CommandType = "policy",
                    TargetScope = "global",
                    Priority = 2
                }
            };

            var tick1 = service.ApplyInterventions(1, queue);
            Assert.AreEqual(0, tick1.AppliedCount);
            Assert.AreEqual(2, tick1.PendingCount);

            var tick2 = service.ApplyInterventions(2, queue);
            Assert.AreEqual(1, tick2.AppliedCount);
            CollectionAssert.Contains(tick2.AppliedCommandIds, "cmd-1");
            CollectionAssert.Contains(tick2.RejectedCommandIds, "cmd-2");
            CollectionAssert.Contains(tick2.RejectionReasons, InterventionRejectionReasons.ConflictOverridden);
        }

        [Test]
        public void ApplyInterventions_RejectsSelfSupersede()
        {
            var service = new InterventionQueueService();
            var queue = new List<InterventionCommandRow>
            {
                new InterventionCommandRow
                {
                    CommandId = "cmd-1",
                    IssuedTick = 1,
                    ApplyTick = 2,
                    CommandType = "policy",
                    TargetScope = "global",
                    Priority = 1,
                    SupersedesCommandId = "cmd-1"
                }
            };

            var result = service.ApplyInterventions(2, queue);

            Assert.AreEqual(0, result.AppliedCount);
            Assert.AreEqual(1, result.RejectedCount);
            Assert.AreEqual(InterventionRejectionReasons.SelfSupersede, result.RecentRejectedReason);
        }
    }
}
