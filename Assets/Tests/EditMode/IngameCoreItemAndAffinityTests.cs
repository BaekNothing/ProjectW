using System.Collections.Generic;
using NUnit.Framework;
using ProjectW.IngameCore.Simulation;

namespace ProjectW.Tests.EditMode
{
    public class IngameCoreItemAndAffinityTests
    {
        [Test]
        public void NeedRequirement_UsesPreferredPersonalItemsFirst()
        {
            var requirement = new NeedRequirement("sleep", "sleepzone", new List<string> { "bed", "pillow", "blanket" });
            var items = new List<WorldItem>
            {
                new WorldItem("bed-public", "Shared Bed", ItemUsagePolicy.Public, string.Empty, new [] { "bed" }),
                new WorldItem("pillow-alice", "Alice Pillow", ItemUsagePolicy.Personal, "alice", new [] { "pillow" }),
                new WorldItem("blanket-public", "Shared Blanket", ItemUsagePolicy.Public, string.Empty, new [] { "blanket" })
            };

            Assert.IsTrue(requirement.IsSatisfied(items, "alice"));
            Assert.IsTrue(requirement.IsSatisfied(items, "bob"));
        }

        [Test]
        public void AgentRuntimeState_RespectsCarryCapacityDefaultTwo()
        {
            var agent = new AgentRuntimeState("A", inspirationCooldown: 3);
            Assert.IsTrue(agent.TryCarryItem("item-1"));
            Assert.IsTrue(agent.TryCarryItem("item-2"));
            Assert.IsFalse(agent.TryCarryItem("item-3"));
            Assert.AreEqual(2, agent.CarriedItemIds.Count);

            Assert.IsTrue(agent.DropItem("item-2"));
            Assert.IsTrue(agent.TryCarryItem("item-3"));
            Assert.AreEqual(2, agent.CarriedItemIds.Count);
        }

        [Test]
        public void AffinitySystem_AppliesPersonalItemViolationAndConflict()
        {
            var item = new WorldItem("pillow-1", "Pillow", ItemUsagePolicy.Personal, "alice", new[] { "pillow" });
            var affinitySystem = new AffinitySystem();
            var events = affinitySystem.BuildItemUsageEvents("bob", item, "alice", escalatedConflict: true);

            Assert.AreEqual(3, events.Count);
            for (var i = 0; i < events.Count; i++)
            {
                affinitySystem.ApplyEvent(events[i]);
            }

            Assert.Less(affinitySystem.GetAffinity("alice", "bob"), 0f);
            Assert.Less(affinitySystem.GetAffinity("bob", "alice"), 0f);
        }

        [Test]
        public void AffinitySystem_WorkOutcome_AdjustsAffinity()
        {
            var affinitySystem = new AffinitySystem();
            var events = affinitySystem.BuildWorkOutcomeEvents("alice", completedQuickly: true, overloaded: true, helperId: "bob");

            foreach (var affinityEvent in events)
            {
                affinitySystem.ApplyEvent(affinityEvent);
            }

            Assert.Greater(affinitySystem.GetAffinity("alice", "bob"), 0f);
            Assert.Greater(affinitySystem.GetAffinity("bob", "alice"), 0f);
            Assert.Less(affinitySystem.GetAffinity("alice", "alice"), 0f);
        }
    }
}
