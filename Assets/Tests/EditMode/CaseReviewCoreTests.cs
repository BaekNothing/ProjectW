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
                SetPrivateField(card, "criticalChancePercent", 35);
                SetPrivateField(card, "criticalMultiplier", 2f);
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
                Assert.AreEqual(35, model.Deck[0].CriticalChancePercent);
                Assert.AreEqual(2f, model.Deck[0].CriticalMultiplier);
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
        public void CharacterRuntimeData_CanReceiveInjectedCardAndPerkMutations()
        {
            var card = ScriptableObject.CreateInstance<ActionCardDefinition>();
            var perk = ScriptableObject.CreateInstance<PerkDefinition>();
            var character = ScriptableObject.CreateInstance<CharacterRuntimeData>();
            try
            {
                SetPrivateField(card, "cardId", "card.growth");
                SetPrivateField(card, "title", "Growth Card");
                SetPrivateField(perk, "perkId", "perk.focus");
                SetPrivateField(perk, "title", "Focus Perk");
                SetPrivateField(character, "personnelIdOverride", "P-42");

                ICharacterMutationTarget mutations = character;

                Assert.IsTrue(mutations.AddCard(card).Changed);
                Assert.IsFalse(mutations.AddCard(card).Changed);
                Assert.IsTrue(mutations.AddPerk(perk).Changed);
                Assert.AreEqual(1, character.Deck.Count);
                Assert.AreEqual(1, character.Perks.Count);

                var model = character.CreateRuntimeModel();
                Assert.AreEqual("card.growth", model.Deck.Single().Id);
                Assert.AreEqual("perk.focus", model.Perks.Single().Id);

                Assert.IsTrue(mutations.RemoveCard("card.growth").Changed);
                Assert.IsFalse(mutations.RemoveCard("card.growth").Changed);
                Assert.IsTrue(mutations.RemovePerk("perk.focus").Changed);
                Assert.AreEqual(0, character.Deck.Count);
                Assert.AreEqual(0, character.Perks.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(card);
                UnityEngine.Object.DestroyImmediate(perk);
                UnityEngine.Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void CharacterRuntimeData_CanReceiveInjectedStatAndTraitMutations()
        {
            var character = ScriptableObject.CreateInstance<CharacterRuntimeData>();
            try
            {
                ICharacterMutationTarget mutations = character;
                var trait = new TraitSampleRecord
                {
                    TraitSampleId = "trait.overconfident",
                    Strength = 75
                };

                Assert.IsTrue(mutations.SetStat(CharacterStatKey.Fatigue, 90).Changed);
                Assert.IsTrue(mutations.AdjustStat(CharacterStatKey.Fatigue, 20).Changed);
                Assert.AreEqual(100, mutations.GetStat(CharacterStatKey.Fatigue));

                Assert.IsTrue(mutations.AdjustStat(CharacterStatKey.PhysicalEnergy, -200).Changed);
                Assert.AreEqual(0, mutations.GetStat(CharacterStatKey.PhysicalEnergy));

                Assert.IsTrue(mutations.SetStat(CharacterStatKey.TrustToManager, -150).Changed);
                Assert.AreEqual(-100, mutations.GetStat(CharacterStatKey.TrustToManager));

                Assert.IsTrue(mutations.AddTraitSample(trait).Changed);
                Assert.IsFalse(mutations.AddTraitSample(trait).Changed);
                Assert.IsTrue(mutations.AdjustTraitSampleStrength("trait.overconfident", 50).Changed);
                Assert.AreEqual(100, character.TraitSamples.Single().Strength);
                Assert.IsTrue(mutations.RemoveTraitSample("trait.overconfident").Changed);
                Assert.AreEqual(0, character.TraitSamples.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void CharacterRuntimeData_CanReceiveInjectedMemoryAndRelationshipMutations()
        {
            var character = ScriptableObject.CreateInstance<CharacterRuntimeData>();
            try
            {
                ICharacterMutationTarget mutations = character;
                var memory = new CharacterMemoryRecord
                {
                    MemoryId = "mem.bad-briefing",
                    TargetId = "P-77",
                    Intensity = 40,
                    Decay = 5
                };

                Assert.IsTrue(mutations.AddMemoryRecord(memory).Changed);
                Assert.IsFalse(mutations.AddMemoryRecord(memory).Changed);
                Assert.IsTrue(mutations.AdjustMemoryStat("mem.bad-briefing", CharacterMemoryStatKey.Intensity, 90).Changed);
                Assert.AreEqual(100, character.Memories.Single().Intensity);
                Assert.IsTrue(mutations.SetMemoryStat("mem.bad-briefing", CharacterMemoryStatKey.Decay, -10).Changed);
                Assert.AreEqual(0, character.Memories.Single().Decay);

                Assert.IsTrue(mutations.AdjustRelationshipStat("P-77", CharacterRelationshipStatKey.Trust, 35).Changed);
                Assert.IsTrue(mutations.AdjustRelationshipStat("P-77", CharacterRelationshipStatKey.Trust, 90).Changed);
                Assert.AreEqual(100, character.Relationships.Single().Trust);
                Assert.IsTrue(mutations.SetRelationshipStat("P-77", CharacterRelationshipStatKey.Resentment, -150).Changed);
                Assert.AreEqual(-100, character.Relationships.Single().Resentment);

                Assert.IsTrue(mutations.RemoveMemory("mem.bad-briefing").Changed);
                Assert.IsTrue(mutations.RemoveRelationship("P-77").Changed);
                Assert.AreEqual(0, character.Memories.Count);
                Assert.AreEqual(0, character.Relationships.Count);
            }
            finally
            {
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
        public void LocalizedTextTable_ResolvesByLanguageAndCountryWithFallback()
        {
            var table = ScriptableObject.CreateInstance<LocalizedTextTable>();
            try
            {
                SetPrivateField(table, "defaultLanguageKey", "ko");
                SetPrivateField(table, "defaultCountryCode", "KR");
                SetPrivateField(table, "entries", new List<LocalizedTextEntry>
                {
                    new LocalizedTextEntry
                    {
                        Key = "scenario.audit.opening",
                        Values = new List<LocalizedTextValue>
                        {
                            new LocalizedTextValue { LanguageKey = "ko", CountryCode = "KR", Text = "감사팀이 도착했다." },
                            new LocalizedTextValue { LanguageKey = "en", CountryCode = "US", Text = "The audit team arrived." }
                        }
                    }
                });

                Assert.AreEqual("감사팀이 도착했다.", table.GetText("scenario.audit.opening", "ko", "KR"));
                Assert.AreEqual("The audit team arrived.", table.GetText("scenario.audit.opening", "en", "US"));
                Assert.AreEqual("The audit team arrived.", table.GetText("scenario.audit.opening", "en", "GB"));
                Assert.AreEqual("감사팀이 도착했다.", table.GetText("scenario.audit.opening", "ja", "JP"));
                Assert.AreEqual("missing.key", table.GetText("missing.key", "ko", "KR"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void LocalizedTextCsv_RoundTripsSpreadsheetColumns()
        {
            var entries = new List<LocalizedTextEntry>
            {
                new LocalizedTextEntry
                {
                    Key = "scenario.csv.line.001",
                    Values = new List<LocalizedTextValue>
                    {
                        new LocalizedTextValue { LanguageKey = "ko", CountryCode = "KR", Text = "\uc27c\ud45c, \ub530\uc634\ud45c \" \uc904\ubc14\uafc8\n\ud14c\uc2a4\ud2b8" },
                        new LocalizedTextValue { LanguageKey = "en", CountryCode = "US", Text = "Comma, quote \" and newline\ntext" }
                    }
                }
            };

            var csv = LocalizedTextCsv.ToCsv(entries);
            var imported = LocalizedTextCsv.FromCsv(csv);

            Assert.AreEqual(1, imported.Count);
            Assert.AreEqual("scenario.csv.line.001", imported[0].Key);
            Assert.AreEqual("\uc27c\ud45c, \ub530\uc634\ud45c \" \uc904\ubc14\uafc8\n\ud14c\uc2a4\ud2b8", imported[0].Values.Single(v => v.LanguageKey == "ko").Text);
            Assert.AreEqual("Comma, quote \" and newline\ntext", imported[0].Values.Single(v => v.LanguageKey == "en").Text);
        }

        [Test]
        public void RemoteSpreadsheetManifest_ParsesEnabledDatasets()
        {
            var csv =
                "datasetId,sheetName,enabled,schemaVersion,required,notes\n" +
                "localized_text,localized_text,TRUE,1,TRUE,text\n" +
                "cards,cards,FALSE,2,FALSE,cards\n";

            var manifest = RemoteSpreadsheetData.ParseManifest(csv);

            Assert.AreEqual(2, manifest.Count);
            Assert.AreEqual("localized_text", manifest[0].DatasetId);
            Assert.IsTrue(manifest[0].Enabled);
            Assert.IsTrue(manifest[0].Required);
            Assert.AreEqual(2, manifest[1].SchemaVersion);
            Assert.IsFalse(manifest[1].Enabled);
        }

        [Test]
        public void LocalizedTextRuntimeOverrides_TakePriorityOverAssetText()
        {
            var table = ScriptableObject.CreateInstance<LocalizedTextTable>();
            try
            {
                SetPrivateField(table, "entries", new List<LocalizedTextEntry>
                {
                    new LocalizedTextEntry
                    {
                        Key = "remote.test",
                        Values = new List<LocalizedTextValue>
                        {
                            new LocalizedTextValue { LanguageKey = "ko", CountryCode = "KR", Text = "asset" }
                        }
                    }
                });
                LocalizedTextRuntimeOverrides.Replace(new[]
                {
                    new LocalizedTextEntry
                    {
                        Key = "remote.test",
                        Values = new List<LocalizedTextValue>
                        {
                            new LocalizedTextValue { LanguageKey = "ko", CountryCode = "KR", Text = "remote" }
                        }
                    }
                });

                Assert.AreEqual("remote", table.GetText("remote.test", "ko", "KR"));
            }
            finally
            {
                LocalizedTextRuntimeOverrides.Clear();
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void SpreadsheetCsv_RoundTripsJsonCells()
        {
            var headers = new[] { "id", "json" };
            var json = "{\"tags\":[\"audit\",\"records\"],\"note\":\"quoted \\\"text\\\"\"}";
            var csv = SpreadsheetCsv.ToCsv(headers, new[] { new[] { "row.1", json } });

            var table = CsvTable.Read(csv);

            Assert.AreEqual(1, table.Rows.Count);
            Assert.AreEqual("row.1", table.Rows[0].Value("id"));
            Assert.AreEqual(json, table.Rows[0].Value("json"));
        }

        [Test]
        public void RemoteSpreadsheetSnapshot_ReplacesInitialStaffAndWork()
        {
            var datasets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["localized_text"] = "Key,ko-KR\nremote.line,원격 대사\n",
                ["cards"] =
                    "cardId,title,visibleSummary,tags,outcomeModifier,riskModifier,reviewCostModifier,criticalChancePercent,criticalMultiplier\n" +
                    "card.remote,Remote Card,Remote summary,audit,3,-2,1,20,2\n",
                ["characters"] =
                    "personnelId,displayName,cloneLineageId,background,interests,personality,workStyle,initialInformationScope,aptitudesJson,physicalEnergy,mentalStress,loadAssigned,fatigue,stagnation,trustToManager,retentionRisk,hasLeft,daysSinceJoined,optLow,optHigh,maxLoad,connectionLimit,cloneVersion,regenerationCount,regeneratedFromId,startingDeckIds,perksJson,relationshipsJson,memoriesJson,traitSamplesJson\n" +
                    "P-REMOTE,Remote Person,LINE-R,background,audit,calm,careful,Surface,\"{\"\"logic\"\":8}\",90,5,0,3,2,60,4,FALSE,0,2,5,7,3,1,0,,card.remote,[],[],[],[]\n",
                ["work_definitions"] =
                    "eventId,workId,title,kind,subsystem,importance,volume,urgency,severity,ttlSec,status,latentRisk,mismatchScore,assignedPersonnel,physicalCost,mentalCost,baseSuccessChance,requiredAptitudes,recommendedPersonnelCount,minPersonnelCount,maxPersonnelCount,concurrentLimit,concurrentSlotCost,splitPenalty,soloPenalty,tags,perkTags,cardHooks,bossReactionTags,memoryHooks,visibleSummary,hiddenFacts,perkInteractionInfo,truthFramesJson,logsJson\n" +
                    "E-REMOTE,work.remote,Remote Work,incident,O2,50,10,70,65,120,Open,20,2,P-REMOTE,5,6,60,\"{\"\"logic\"\":5}\",1,1,2,1,1,0,0,audit,audit,,,,summary,,,[],[]\n",
                ["scenarios"] =
                    "eventId,timing,priority,playbackStateKey,triggerMode,allowedExplicitLocationsJson,triggerConditionsJson,textTableId,linesJson,oneShot,cooldownDays,allowReplayInDebug\n" +
                    "scenario.remote,Morning,1,scenario.remote,LoopBoundary,[],\"[{\"\"Key\"\":0,\"\"SubjectId\"\":\"\"audit\"\",\"\"Value\"\":\"\"audit\"\",\"\"Threshold\"\":0,\"\"Comparison\"\":0}]\",localized,\"[{\"\"LineId\"\":\"\"L1\"\",\"\"Kind\"\":0,\"\"SpeakerId\"\":\"\"P-REMOTE\"\",\"\"PortraitIds\"\":[],\"\"TextKey\"\":\"\"remote.line\"\",\"\"StageCommands\"\":[],\"\"Choices\"\":[],\"\"Effects\"\":[]}]\",TRUE,0,FALSE\n"
            };

            var snapshot = RemoteSpreadsheetSnapshotParser.Parse(datasets);
            var state = CaseReviewGame.Init(snapshot.CreateGameConfig(), 1);

            Assert.AreEqual(1, state.Staff.Count);
            Assert.AreEqual("P-REMOTE", state.Staff[0].Id);
            Assert.AreEqual(1, state.Queue.Count);
            Assert.AreEqual("E-REMOTE", state.Queue[0].Id);
            Assert.AreEqual("card.remote", state.Staff[0].Deck[0].Id);
            Assert.AreEqual(1, snapshot.Scenarios.Count);
        }

        [Test]
        public void ScenarioEventDefinition_StoresRowsAndResolvesLocalizedText()
        {
            var table = ScriptableObject.CreateInstance<LocalizedTextTable>();
            var scenario = ScriptableObject.CreateInstance<ScenarioEventDefinition>();
            try
            {
                SetPrivateField(table, "entries", new List<LocalizedTextEntry>
                {
                    new LocalizedTextEntry
                    {
                        Key = "scenario.tea.line.001",
                        Values = new List<LocalizedTextValue>
                        {
                            new LocalizedTextValue { LanguageKey = "ko", CountryCode = "KR", Text = "차라도 한 잔 하죠." }
                        }
                    },
                    new LocalizedTextEntry
                    {
                        Key = "scenario.tea.choice.listen",
                        Values = new List<LocalizedTextValue>
                        {
                            new LocalizedTextValue { LanguageKey = "ko", CountryCode = "KR", Text = "끝까지 듣는다" }
                        }
                    }
                });

                SetPrivateField(scenario, "eventId", "scenario.tea.audit");
                SetPrivateField(scenario, "timing", ScenarioTiming.Night);
                SetPrivateField(scenario, "textTable", table);
                SetPrivateField(scenario, "lines", new List<ScenarioScriptLine>
                {
                    new ScenarioScriptLine
                    {
                        LineId = "L001",
                        Kind = ScenarioLineKind.Dialogue,
                        SpeakerId = "P-quiet-auditor",
                        TextKey = "scenario.tea.line.001",
                        ExpressionKey = "tired",
                        StageCommands = new List<ScenarioStageCommand>
                        {
                            new ScenarioStageCommand
                            {
                                CommandType = ScenarioStageCommandType.FocusSpeaker,
                                TargetId = "P-quiet-auditor",
                                Intensity = 1f
                            },
                            new ScenarioStageCommand
                            {
                                CommandType = ScenarioStageCommandType.DimOthers,
                                TargetId = "others",
                                Intensity = 0.6f
                            }
                        },
                        Choices = new List<ScenarioChoice>
                        {
                            new ScenarioChoice
                            {
                                ChoiceId = "listen",
                                LabelTextKey = "scenario.tea.choice.listen",
                                Costs = new List<ScenarioStateEffect>
                                {
                                    new ScenarioStateEffect { Key = ScenarioEffectKey.FocusCost, Delta = -2 }
                                },
                                Effects = new List<ScenarioStateEffect>
                                {
                                    new ScenarioStateEffect { Key = ScenarioEffectKey.RelationshipDelta, SubjectId = "P-quiet-auditor", Delta = 5 }
                                }
                            }
                        }
                    }
                });

                var resolved = scenario.ResolveLine(0, "ko", "KR");

                Assert.AreEqual("scenario.tea.audit", scenario.EventId);
                Assert.AreEqual(ScenarioTiming.Night, scenario.Timing);
                Assert.AreEqual("차라도 한 잔 하죠.", resolved.Text);
                Assert.AreEqual("P-quiet-auditor", resolved.Source.SpeakerId);
                Assert.AreEqual(2, resolved.Source.StageCommands.Count);
                Assert.AreEqual(ScenarioStageCommandType.FocusSpeaker, resolved.Source.StageCommands[0].CommandType);
                Assert.AreEqual(ScenarioEffectKey.FocusCost, resolved.Source.Choices.Single().Costs.Single().Key);
                Assert.AreEqual(ScenarioEffectKey.RelationshipDelta, resolved.Source.Choices.Single().Effects.Single().Key);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
                UnityEngine.Object.DestroyImmediate(scenario);
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
        public void ConfirmPlan_AppliesTargetedCardsOnlyToMatchingWork()
        {
            var baseline = CaseReviewGame.Init(new GameConfig(), 1);
            CaseReviewGame.Dispatch(baseline, "adjust E-108 B-04");
            CaseReviewGame.Dispatch(baseline, "adjust R-211 B-04");
            baseline.MorningCards = new List<ActionCard>();
            CaseReviewGame.Dispatch(baseline, "confirm plan");
            var baselineTarget = baseline.Queue.Single(item => item.Id == "E-108");
            var baselineOther = baseline.Queue.Single(item => item.Id == "R-211");

            var targeted = CaseReviewGame.Init(new GameConfig(), 1);
            CaseReviewGame.Dispatch(targeted, "adjust E-108 B-04");
            CaseReviewGame.Dispatch(targeted, "adjust R-211 B-04");
            targeted.MorningCards = new List<ActionCard>
            {
                new()
                {
                    Id = "card.targeted",
                    OwnerPersonnelId = "B-04",
                    TargetEventId = "E-108",
                    OutcomeModifier = 25,
                    RiskModifier = -10
                }
            };

            CaseReviewGame.Dispatch(targeted, "confirm plan");
            var targetedEvent = targeted.Queue.Single(item => item.Id == "E-108");
            var otherEvent = targeted.Queue.Single(item => item.Id == "R-211");

            Assert.Greater(targetedEvent.OutcomeScore, baselineTarget.OutcomeScore);
            Assert.AreEqual(baselineOther.OutcomeScore, otherEvent.OutcomeScore);
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
        public void ConfirmPlan_RecordsRelationshipMemoryForSharedWork()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            CaseReviewGame.Dispatch(state, "adjust E-108 B-04,C-22");
            var before = state.Staff.Single(person => person.Id == "B-04")
                .Relationships.Single(relation => relation.TargetId == "C-22")
                .Trust;

            var confirm = CaseReviewGame.Dispatch(state, "confirm plan");

            var source = state.Staff.Single(person => person.Id == "B-04");
            var relation = source.Relationships.Single(item => item.TargetId == "C-22");
            Assert.IsTrue(confirm.Success);
            Assert.AreNotEqual(before, relation.Trust);
            Assert.IsTrue(source.Memories.Any(memory => memory.TargetId == "C-22" && memory.SourceEventId == "E-108"));
        }

        [Test]
        public void Regenerate_ReplacesPersonnelAndArchivesActiveRelationships()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            state.MeritTokens = 3;
            var source = state.Staff.Single(person => person.Id == "A-17");
            source.Memories.Add(new PersonnelMemory { Id = "mem.manual", TargetId = "B-04", Intensity = 70, Note = "old memory" });
            source.Relationships.Add(new PersonnelRelationship { TargetId = "D-11", Trust = 40, Affinity = 12 });
            var observer = state.Staff.Single(person => person.Id == "B-04");
            Assert.IsTrue(observer.Relationships.Any(relation => relation.TargetId == "A-17"));

            var request = CaseReviewGame.Dispatch(state, "regenerate A-17");
            var approval = state.ApprovalRequests.Single(item => item.Kind == ApprovalRequestKind.Regeneration && item.TargetId == "A-17");
            var result = CaseReviewGame.Dispatch(state, $"submit approval {approval.Id} 3");

            var regenerated = state.Staff.Single(person => person.RegeneratedFromId == "A-17");
            Assert.IsTrue(request.Success);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(ApprovalStatus.Executed, approval.Status);
            Assert.AreEqual(0, state.MeritTokens);
            Assert.AreNotEqual("A-17", regenerated.Id);
            Assert.AreEqual(1, regenerated.RegenerationCount);
            Assert.AreEqual(0, regenerated.Relationships.Count);
            Assert.IsFalse(regenerated.Memories.Any(memory => memory.Id == "mem.manual"));
            Assert.IsTrue(regenerated.Memories.All(memory => memory.Tags.Contains("lineage-residue")));
            Assert.IsFalse(observer.Relationships.Any(relation => relation.TargetId == "A-17"));
            Assert.IsTrue(observer.Relationships.Any(relation => relation.TargetId == regenerated.Id));
            Assert.IsTrue(observer.Memories.Any(memory => memory.TargetId == regenerated.Id && memory.Type == "Clone"));
            Assert.IsTrue(state.MorningPlan.Entries.All(entry => !entry.PlannedPersonnel.Contains("A-17")));
        }

        [Test]
        public void Regenerate_RequestCreatesApprovalWithoutImmediateExecution()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);

            var result = CaseReviewGame.Dispatch(state, "regenerate A-17");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, state.ApprovalRequests.Count);
            Assert.AreEqual(ApprovalRequestKind.Regeneration, state.ApprovalRequests[0].Kind);
            Assert.AreEqual(3, state.ApprovalRequests[0].RequiredTokens);
            Assert.IsTrue(state.Staff.Any(person => person.Id == "A-17" && person.RegeneratedFromId == ""));
        }

        [Test]
        public void ConfirmPlan_AwardsMeritTokensForSuccessAndRisk()
        {
            var state = CaseReviewGame.Init(new GameConfig
            {
                InitialData = new CaseReviewSeedData
                {
                    Staff = new List<Personnel>
                    {
                        new Personnel
                        {
                            Id = "P-01",
                            Name = "Planner",
                            MaxLoad = 3,
                            OptHigh = 3,
                            Aptitudes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["repair"] = 10
                            }
                        }
                    },
                    Queue = new List<EventCase>
                    {
                        new EventCase
                        {
                            Id = "E-TOKEN",
                            Kind = "incident",
                            Title = "Token Work",
                            Urgency = 80,
                            Severity = 70,
                            BaseSuccessChance = 85,
                            RequiredAptitudes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["repair"] = 5
                            },
                            MaxPersonnelCount = 1
                        }
                    }
                }
            }, 12);

            CaseReviewGame.Dispatch(state, "adjust E-TOKEN P-01");
            var result = CaseReviewGame.Dispatch(state, "confirm plan");

            Assert.IsTrue(result.Success);
            Assert.GreaterOrEqual(state.MeritTokens, 1);
        }

        [Test]
        public void Approval_RejectionHintsAtHiddenCompanyState()
        {
            var state = CaseReviewGame.Init(new GameConfig(), 1);
            state.MeritTokens = 3;
            state.ReplacementPressure = 80;
            state.GlobalLatentRisk = 130;

            CaseReviewGame.Dispatch(state, "regenerate A-17");
            var approval = state.ApprovalRequests.Single();
            var result = CaseReviewGame.Dispatch(state, $"submit approval {approval.Id} 3");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ApprovalStatus.Rejected, approval.Status);
            Assert.AreEqual(3, state.MeritTokens);
            Assert.IsFalse(string.IsNullOrWhiteSpace(approval.Hint));
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
