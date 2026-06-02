using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectW.IngameCore.CaseReview;
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
        public void Init_DrawsOneMorningCardForEachAvailablePersonnel()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            var activeStaff = state.Staff.Where(s => !s.HasLeft).Select(s => s.Id).ToList();

            Assert.AreEqual(activeStaff.Count, state.MorningCards.Count);
            CollectionAssert.AreEquivalent(activeStaff, state.MorningCards.Select(c => c.OwnerPersonnelId).ToList());
        }

        [Test]
        public void ConfigRules_CanPlugCustomCardDrawService()
        {
            var state = CaseReviewGame.Init(new GameConfig
            {
                Rules = new CaseReviewRules
                {
                    CardDrawService = new FixedCardDrawService()
                }
            }, 1);

            Assert.AreEqual(1, state.MorningCards.Count);
            Assert.AreEqual("test-card", state.MorningCards[0].Id);
            Assert.AreEqual(25, state.MorningCards[0].OutcomeModifier);
        }

        [Test]
        public void CharacterBaseDefinition_CreatesRuntimeModelWithCardsAndPerks()
        {
            var card = ScriptableObject.CreateInstance<ActionCardDefinition>();
            var perk = ScriptableObject.CreateInstance<PerkDefinition>();
            var character = ScriptableObject.CreateInstance<CharacterBaseDefinition>();
            try
            {
                SetPrivateField(card, "cardId", "card.work");
                SetPrivateField(card, "title", "Work Card");
                SetPrivateField(perk, "perkId", "perk.logic");
                SetPrivateField(perk, "title", "Logic Perk");
                SetPrivateField(character, "personnelId", "P-01");
                SetPrivateField(character, "displayName", "Planner");
                SetPrivateField(character, "cloneLineageId", "CL-01");
                SetPrivateField(character, "startingDeck", new List<ActionCardDefinition> { card });
                SetPrivateField(character, "startingPerks", new List<PerkDefinition> { perk });

                var model = character.CreateRuntimeModel();

                Assert.AreEqual("P-01", model.Id);
                Assert.AreEqual("CL-01", model.CloneLineageId);
                Assert.AreEqual(1, model.Deck.Count);
                Assert.AreEqual("card.work", model.Deck[0].Id);
                Assert.AreEqual(1, model.Perks.Count);
                Assert.AreEqual("perk.logic", model.Perks[0].Id);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(card);
                UnityEngine.Object.DestroyImmediate(perk);
                UnityEngine.Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void DataDefinitions_CanReferenceRenderResources()
        {
            var resources = ScriptableObject.CreateInstance<RenderResourceDefinition>();
            var card = ScriptableObject.CreateInstance<ActionCardDefinition>();
            var perk = ScriptableObject.CreateInstance<PerkDefinition>();
            var characterBase = ScriptableObject.CreateInstance<CharacterBaseDefinition>();
            var runtime = ScriptableObject.CreateInstance<CharacterRuntimeData>();
            try
            {
                SetPrivateField(resources, "resourceId", "render.clone.alpha");
                SetPrivateField(resources, "displayLabel", "Clone Alpha Render Kit");
                SetPrivateField(card, "renderResources", resources);
                SetPrivateField(perk, "renderResources", resources);
                SetPrivateField(characterBase, "renderResources", resources);
                SetPrivateField(runtime, "baseDefinition", characterBase);

                Assert.AreEqual("render.clone.alpha", resources.ResourceId);
                Assert.AreSame(resources, card.RenderResources);
                Assert.AreSame(resources, perk.RenderResources);
                Assert.AreSame(resources, characterBase.RenderResources);
                Assert.AreSame(resources, runtime.RenderResources);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(resources);
                UnityEngine.Object.DestroyImmediate(card);
                UnityEngine.Object.DestroyImmediate(perk);
                UnityEngine.Object.DestroyImmediate(characterBase);
                UnityEngine.Object.DestroyImmediate(runtime);
            }
        }

        [Test]
        public void CharacterRuntimeData_StoresRelationshipsPerCharacter()
        {
            var first = ScriptableObject.CreateInstance<CharacterRuntimeData>();
            var second = ScriptableObject.CreateInstance<CharacterRuntimeData>();
            try
            {
                SetPrivateField(first, "personnelIdOverride", "P-01");
                SetPrivateField(second, "personnelIdOverride", "P-02");

                first.GetOrCreateRelationship("P-02").Trust = 42;
                second.GetOrCreateRelationship("P-01").Trust = -17;

                Assert.AreEqual(42, first.Relationships.Single().Trust);
                Assert.AreEqual("P-02", first.Relationships.Single().TargetPersonnelId);
                Assert.AreEqual(-17, second.Relationships.Single().Trust);
                Assert.AreEqual("P-01", second.Relationships.Single().TargetPersonnelId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void InitialData_CanSeedStaffFromCharacterRuntimeData()
        {
            var card = ScriptableObject.CreateInstance<ActionCardDefinition>();
            var character = ScriptableObject.CreateInstance<CharacterRuntimeData>();
            try
            {
                SetPrivateField(card, "cardId", "card.injected");
                SetPrivateField(card, "title", "Injected");
                SetPrivateField(character, "personnelIdOverride", "P-77");
                SetPrivateField(character, "deck", new List<ActionCardDefinition> { card });

                var state = CaseReviewGame.Init(new GameConfig
                {
                    InitialData = new CaseReviewSeedData
                    {
                        CharacterData = new List<CharacterRuntimeData> { character }
                    }
                }, 4);

                var person = state.Staff.Single(s => s.Id == "P-77");
                Assert.AreEqual(1, person.Deck.Count);
                Assert.AreEqual("card.injected", person.Deck[0].Id);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(card);
                UnityEngine.Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void WorkDefinition_CreatesRuntimeEventCase()
        {
            var work = ScriptableObject.CreateInstance<WorkDefinition>();
            try
            {
                SetPrivateField(work, "workId", "work.o2-bypass");
                SetPrivateField(work, "title", "O2 Bypass");
                SetPrivateField(work, "kind", "incident");
                SetPrivateField(work, "subsystem", "O2");
                SetPrivateField(work, "importance", 70);
                SetPrivateField(work, "volume", 18);
                SetPrivateField(work, "risk", 30);
                SetPrivateField(work, "latentRisk", 25);
                SetPrivateField(work, "urgency", 65);
                SetPrivateField(work, "tags", new List<string> { "repair", "procedure" });
                SetPrivateField(work, "requiredAptitudes", new List<WorkAptitudeRequirement>
                {
                    new WorkAptitudeRequirement { Key = "dexterity", Value = 7 }
                });

                var item = work.CreateInstance(new WorkGenerationContext { Seed = 1, Day = 2, Difficulty = 2 }, 1);

                Assert.AreEqual("work.o2-bypass", item.DefinitionId);
                Assert.AreEqual("E-0201", item.Id);
                Assert.AreEqual("incident", item.Kind);
                Assert.AreEqual(72, item.Importance);
                Assert.AreEqual(20, item.Volume);
                Assert.AreEqual(1, item.ConcurrentLimit);
                Assert.AreEqual(1, item.ConcurrentSlotCost);
                Assert.IsTrue(item.Tags.Contains("repair"));
                Assert.AreEqual(7, item.RequiredAptitudes["dexterity"]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(work);
            }
        }

        [Test]
        public void WorkGeneration_UsesDifficultyAndConditionWeights()
        {
            var easy = ScriptableObject.CreateInstance<WorkDefinition>();
            var hard = ScriptableObject.CreateInstance<WorkDefinition>();
            try
            {
                SetPrivateField(easy, "workId", "work.easy");
                SetPrivateField(easy, "title", "Easy Work");
                SetPrivateField(easy, "spawnProfile", SpawnProfile(10));
                SetPrivateField(hard, "workId", "work.hard");
                SetPrivateField(hard, "title", "Hard Work");
                SetPrivateField(hard, "spawnProfile", SpawnProfile(
                    0,
                    difficulty: new List<WorkDifficultyWeight>
                    {
                        new WorkDifficultyWeight { MinDifficulty = 3, WeightDelta = 20 }
                    },
                    conditions: new List<WorkConditionWeight>
                    {
                        new WorkConditionWeight { Key = WorkConditionKey.GlobalLatentRisk, Threshold = 40, WeightDelta = 30 }
                    }));

                var low = new WorkGenerationContext { Seed = 7, Day = 1, Difficulty = 1, GlobalLatentRisk = 10 };
                var high = new WorkGenerationContext { Seed = 7, Day = 1, Difficulty = 4, GlobalLatentRisk = 50 };

                Assert.AreEqual(0, hard.EvaluateSpawnWeight(low));
                Assert.AreEqual(50, hard.EvaluateSpawnWeight(high));

                var generated = WorkGenerationSystem.Generate(new List<WorkDefinition> { easy, hard }, high, 2);

                Assert.AreEqual(2, generated.Count);
                Assert.IsTrue(generated.Any(item => item.DefinitionId == "work.hard"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(easy);
                UnityEngine.Object.DestroyImmediate(hard);
            }
        }

        [Test]
        public void InitialData_CanGenerateQueueFromWorkDefinitions()
        {
            var work = ScriptableObject.CreateInstance<WorkDefinition>();
            try
            {
                SetPrivateField(work, "workId", "work.generated");
                SetPrivateField(work, "title", "Generated Work");
                SetPrivateField(work, "kind", "audit");
                SetPrivateField(work, "spawnProfile", SpawnProfile(100));

                var state = CaseReviewGame.Init(new GameConfig
                {
                    InitialData = new CaseReviewSeedData
                    {
                        WorkDefinitions = new List<WorkDefinition> { work }
                    }
                }, 11);

                Assert.AreEqual(1, state.Queue.Count);
                Assert.AreEqual("work.generated", state.Queue[0].DefinitionId);
                Assert.AreEqual("audit", state.Queue[0].Kind);
                Assert.IsTrue(state.MorningPlan.Entries.Any(e => e.EventId == state.Queue[0].Id));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(work);
            }
        }

        [Test]
        public void ReviewActions_RecordReviewCostEntries()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            ForceNoon(state);

            CaseReviewGame.Dispatch(state, "summary E-108");
            CaseReviewGame.Dispatch(state, "log E-108 equip");

            Assert.IsTrue(state.ReviewCosts.Any(c => c.ActionType == ReviewActionType.Summary && c.SubjectId == "E-108"));
            Assert.IsTrue(state.ReviewCosts.Any(c => c.ActionType == ReviewActionType.Log && c.SourceType == "equip"));
        }

        [Test]
        public void ConfirmingUnadjustedAiPlan_IncreasesReplacementPressure()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            var before = state.ReplacementPressure;

            var result = CaseReviewGame.Dispatch(state, "confirm plan");

            Assert.IsTrue(result.Success);
            Assert.Greater(state.ReplacementPressure, before);
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

        private static void ForceNoon(GameState state)
        {
            state.MorningPlan.Confirmed = true;
            state.Slot = Slot.Noon;
            state.TimeRemainingSec = state.Config.NoonSeconds;
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static WorkSpawnProfile SpawnProfile(
            int baseWeight,
            List<WorkDifficultyWeight> difficulty = null,
            List<WorkConditionWeight> conditions = null)
        {
            var profile = new WorkSpawnProfile();
            SetPrivateField(profile, "baseSpawnWeight", baseWeight);
            SetPrivateField(profile, "difficultyWeights", difficulty ?? new List<WorkDifficultyWeight>());
            SetPrivateField(profile, "conditionWeights", conditions ?? new List<WorkConditionWeight>());
            return profile;
        }

        private sealed class FixedCardDrawService : ICardDrawService
        {
            public List<ActionCard> DrawMorningCards(GameState state)
            {
                return new List<ActionCard>
                {
                    new ActionCard
                    {
                        Id = "test-card",
                        OwnerPersonnelId = "A-17",
                        Title = "Injected Test Card",
                        OutcomeModifier = 25,
                        Tags = new List<string> { "test" }
                    }
                };
            }
        }
    }
}
