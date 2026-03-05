using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectW.IngameCore.Simulation;
using UnityEngine;
using Random = System.Random;

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

        [Test]
        public void OfficeItemFactory_WithZoneRules_IsDeterministicForSameSeed()
        {
            var rules = ScriptableObject.CreateInstance<ZoneGenerationRuleSet>();
            SetField(rules, "zoneRules", new[]
            {
                new ZoneTypeRule
                {
                    ZoneKey = "workzone",
                    MinItemCount = 4,
                    MaxItemCount = 4,
                    PersonalRatioMin = 0.25f,
                    PersonalRatioMax = 0.25f,
                    RequiredTags = new[] { "desk", "computer" },
                    ItemTemplates = new[]
                    {
                        new ItemTemplateRule { DisplayName = "Desk", Weight = 1f, Tags = new[] { "desk" } },
                        new ItemTemplateRule { DisplayName = "Computer", Weight = 1f, Tags = new[] { "computer" } }
                    }
                }
            });

            var itemsA = OfficeItemFactory.GenerateOfficeItems(new Random(42), 8, new[] { "A", "B" }, rules);
            var itemsB = OfficeItemFactory.GenerateOfficeItems(new Random(42), 8, new[] { "A", "B" }, rules);
            Assert.AreEqual(itemsA.Count, itemsB.Count);
            for (var i = 0; i < itemsA.Count; i++)
            {
                Assert.AreEqual(itemsA[i].Id, itemsB[i].Id);
                Assert.AreEqual(itemsA[i].UsagePolicy, itemsB[i].UsagePolicy);
                Assert.AreEqual(itemsA[i].ZoneKey, itemsB[i].ZoneKey);
            }
        }

        [Test]
        public void OfficeItemFactory_WithZoneRules_MeetsRequiredTagsAndPersonalBounds()
        {
            var rules = ScriptableObject.CreateInstance<ZoneGenerationRuleSet>();
            SetField(rules, "zoneRules", new[]
            {
                new ZoneTypeRule
                {
                    ZoneKey = "eatzone",
                    MinItemCount = 6,
                    MaxItemCount = 6,
                    PersonalRatioMin = 0.5f,
                    PersonalRatioMax = 0.5f,
                    RequiredTags = new[] { "table", "tray", "cup" },
                    ItemTemplates = new[]
                    {
                        new ItemTemplateRule { DisplayName = "Table", Weight = 1f, Tags = new[] { "table" } },
                        new ItemTemplateRule { DisplayName = "Tray", Weight = 1f, Tags = new[] { "tray" } },
                        new ItemTemplateRule { DisplayName = "Cup", Weight = 1f, Tags = new[] { "cup" } }
                    }
                }
            });

            var items = OfficeItemFactory.GenerateOfficeItems(new Random(7), 6, new[] { "A", "B", "C" }, rules);
            AssertHasTag(items, "table");
            AssertHasTag(items, "tray");
            AssertHasTag(items, "cup");

            var personalCount = 0;
            for (var i = 0; i < items.Count; i++)
            {
                Assert.AreEqual("eatzone", items[i].ZoneKey);
                if (items[i].UsagePolicy == ItemUsagePolicy.Personal)
                {
                    personalCount += 1;
                }
            }

            Assert.AreEqual(3, personalCount);
        }

        [Test]
        public void JobSystem_WithTaskRules_PreservesRemainingWorkTotal()
        {
            var taskRules = ScriptableObject.CreateInstance<TaskGenerationRuleSet>();
            SetField(taskRules, "workUnitJitterRatio", 0.2f);
            SetField(taskRules, "templates", new[]
            {
                new TaskTemplateRule
                {
                    TemplateId = "work-a",
                    NeedKey = "work",
                    ZoneKey = "labzone",
                    Weight = 1f,
                    MaxWorkUnits = 50,
                    RequiredTags = new[] { "desk", "computer" }
                }
            });

            var system = new JobSystem();
            var task = new TaskModel(11, 4f);
            var agents = new List<AgentRuntimeState>
            {
                new AgentRuntimeState("A", 4) { SubtaskWork = 0 },
                new AgentRuntimeState("B", 4) { SubtaskWork = 0 },
                new AgentRuntimeState("C", 4) { SubtaskWork = 0 }
            };

            var jobs = system.BuildJobs(new DateTime(2026, 1, 1, 10, 0, 0), task, agents, taskRules, new Random(9));
            var workSum = 0;
            for (var i = 0; i < jobs.Count; i++)
            {
                if (jobs[i].JobType != AtomicJobType.Work)
                {
                    continue;
                }

                Assert.AreEqual("labzone", jobs[i].ZoneKey);
                workSum += jobs[i].WorkUnits;
            }

            Assert.AreEqual(11, workSum);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field, $"Field not found: {fieldName}");
            field.SetValue(target, value);
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
