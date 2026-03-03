using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectW.IngameCore.Simulation;

namespace ProjectW.Tests.EditMode
{
    public class IngameCoreJobSystemTests
    {
        [Test]
        public void OfficeItemFactory_GeneratesTwelveItems_WithRequiredTags()
        {
            var items = OfficeItemFactory.GenerateOfficeItems(new Random(3), 12, new[] { "A", "B" });
            Assert.AreEqual(12, items.Count);
            AssertHasTag(items, "desk");
            AssertHasTag(items, "computer");
            AssertHasTag(items, "bed");
            AssertHasTag(items, "pillow");
            AssertHasTag(items, "blanket");
            AssertHasTag(items, "table");
            AssertHasTag(items, "tray");
            AssertHasTag(items, "cup");
        }

        [Test]
        public void JobSystem_BuildsUnifiedAtomicJobs_IncludingNeedAndWorkJobs()
        {
            var system = new JobSystem();
            var task = new TaskModel(50, 4f);
            var agents = new List<AgentRuntimeState>
            {
                new AgentRuntimeState("A", 4) { Satiety = 10f, Stress = 20f, SubtaskWork = 0 },
                new AgentRuntimeState("B", 4) { Satiety = 90f, Stress = 90f, SubtaskWork = 0 }
            };

            var jobs = system.BuildJobs(new DateTime(2026, 1, 1, 12, 0, 0), task, agents);
            Assert.IsTrue(jobs.Exists(x => x.JobType == AtomicJobType.Work));
            Assert.IsTrue(jobs.Exists(x => x.JobType == AtomicJobType.Eat));
            Assert.IsTrue(jobs.Exists(x => x.JobType == AtomicJobType.Sleep));
        }

        private static void AssertHasTag(List<WorldItem> items, string tag)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].HasTag(tag))
                {
                    return;
                }
            }

            Assert.Fail("Required tag missing: " + tag);
        }
    }
}
