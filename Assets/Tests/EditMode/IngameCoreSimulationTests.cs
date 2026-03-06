using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectW.IngameCore.Simulation;
using ProjectW.IngameCore.Spatial;
using ProjectW.IngameCore.StateMachine;
using UnityEngine;

namespace ProjectW.Tests.EditMode
{
    public class IngameCoreSimulationTests
    {
        [Test]
        public void StateMachine_RejectsForbiddenTransition()
        {
            var decision = CoreLoopStateMachine.EvaluateTransition(CoreLoopState.Plan, CoreLoopState.AutoNarrative, true);
            Assert.AreEqual(CoreLoopState.Plan, decision.NextState);
            Assert.IsFalse(decision.AppliedGuard);
            Assert.AreEqual("E-STATE-199", decision.ErrorCode);
        }

        [Test]
        public void TaskAllocator_AssignsToFreeAgentsOnly()
        {
            var task = new TaskModel(120, 10f);
            var agents = new List<AgentRuntimeState>
            {
                new AgentRuntimeState("A", 6) { SubtaskWork = 20 },
                new AgentRuntimeState("B", 6) { SubtaskWork = 0 },
                new AgentRuntimeState("C", 6) { SubtaskWork = 0 }
            };

            TaskAllocator.AssignWorkChunks(task, agents, new System.Random(42));

            Assert.AreEqual(20, agents[0].SubtaskWork);
            Assert.Greater(agents[1].SubtaskWork, 0);
            Assert.Greater(agents[2].SubtaskWork, 0);
        }

        [Test]
        public void TickEngine_UsesCorridorMoveOnlyRule()
        {
            var task = new TaskModel(100, 10f);
            var agent = new AgentRuntimeState("A", 6)
            {
                Position = (int)RoutineZone.Sleep,
                TargetZone = (int)RoutineZone.Sleep,
                SubtaskWork = 10
            };

            var engine = new SimulationTickEngine(seed: 1);
            var progress = engine.AdvanceTick(new DateTime(2026, 1, 1, 9, 0, 0), 1, task, new List<AgentRuntimeState> { agent });

            Assert.AreEqual((int)RoutineZone.PathSleepWork, agent.Position);
            Assert.AreEqual(0, progress);
        }


        [Test]
        public void T23_DeterministicReplay_LogCoreFieldsMatchForSameSeedAndInput()
        {
            var firstCollector = new EventLogCollector();
            var secondCollector = new EventLogCollector();
            var firstEngine = new SimulationTickEngine(seed: 2026, firstCollector);
            var secondEngine = new SimulationTickEngine(seed: 2026, secondCollector);

            var firstTask = new TaskModel(100, 10f);
            var secondTask = new TaskModel(100, 10f);
            var firstAgents = new List<AgentRuntimeState>
            {
                new AgentRuntimeState("A", 6) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work },
                new AgentRuntimeState("B", 6) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work }
            };
            var secondAgents = new List<AgentRuntimeState>
            {
                new AgentRuntimeState("A", 6) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work },
                new AgentRuntimeState("B", 6) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work }
            };

            for (var tick = 1; tick <= 4; tick++)
            {
                var simTime = new DateTime(2026, 1, 1, 9, 0, 0).AddMinutes((tick - 1) * SimulationConstants.TickMinutes);
                firstEngine.AdvanceTick(simTime, tick, firstTask, firstAgents);
                secondEngine.AdvanceTick(simTime, tick, secondTask, secondAgents);
            }

            var verifier = new ReplayVerifier();
            var result = verifier.Verify(firstCollector.TickLogs, secondCollector.TickLogs);

            Assert.IsTrue(result.IsMatch);
            Assert.IsEmpty(result.ErrorCodes);
            Assert.IsEmpty(result.Differences);
        }

        [Test]
        public void ReplayVerifier_RecordsERPL001OnCoreFieldMismatch()
        {
            var verifier = new ReplayVerifier();
            var expected = new List<TickLogRecord>
            {
                new TickLogRecord { TickIndex = 1, LoopState = "SimulationTick", StateTransition = "Resolve->Resolve", SelectedActionId = "work-0", Seed = 10 }
            };
            var actual = new List<TickLogRecord>
            {
                new TickLogRecord { TickIndex = 1, LoopState = "SimulationTick", StateTransition = "Resolve->Resolve", SelectedActionId = "work-0", Seed = 11 }
            };

            var result = verifier.Verify(expected, actual);

            Assert.IsFalse(result.IsMatch);
            CollectionAssert.Contains(result.ErrorCodes, "E-RPL-001");
        }


        [Test]
        public void ReplayVerifier_FailsWhenAppliedInterventionSetsDiffer()
        {
            var verifier = new ReplayVerifier();
            var expected = new List<TickLogRecord>
            {
                new TickLogRecord
                {
                    TickIndex = 1,
                    LoopState = "SimulationTick",
                    StateTransition = "Resolve->Resolve",
                    SelectedActionId = "work-0",
                    Seed = 42,
                    AppliedInterventionIds = new List<string> { "cmd-a", "cmd-b" }
                }
            };

            var actual = new List<TickLogRecord>
            {
                new TickLogRecord
                {
                    TickIndex = 1,
                    LoopState = "SimulationTick",
                    StateTransition = "Resolve->Resolve",
                    SelectedActionId = "work-0",
                    Seed = 42,
                    AppliedInterventionIds = new List<string> { "cmd-a" }
                }
            };

            var result = verifier.Verify(expected, actual);

            Assert.IsFalse(result.IsMatch);
            CollectionAssert.Contains(result.ErrorCodes, "E-RPL-001");
            Assert.That(result.Differences[0], Does.Contain("applied_interventions_mismatch"));
        }


        [Test]
        public void TickEngine_RecordsKnowledgePropagationEvents()
        {
            var collector = new EventLogCollector();
            var engine = new SimulationTickEngine(seed: 77, eventLogCollector: collector);
            var task = new TaskModel(80, 12f);
            var agents = new List<AgentRuntimeState>
            {
                new AgentRuntimeState("Teacher", 6) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work, SubtaskWork = 5 },
                new AgentRuntimeState("Learner", 6) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work, SubtaskWork = 5 }
            };

            for (var i = 0; i < 4; i++)
            {
                var time = new DateTime(2026, 1, 1, 9, 0, 0).AddMinutes(i * SimulationConstants.TickMinutes);
                engine.AdvanceTick(time, i + 1, task, agents);
            }

            var hasKnowledgeEvent = false;
            for (var i = 0; i < collector.TickLogs.Count; i++)
            {
                if (collector.TickLogs[i].KnowledgeEvents != null && collector.TickLogs[i].KnowledgeEvents.Count > 0)
                {
                    hasKnowledgeEvent = true;
                    break;
                }
            }

            Assert.IsTrue(hasKnowledgeEvent);
        }


        [Test]
        public void TickEngine_AssignsFactionAndIsolationScores()
        {
            var collector = new EventLogCollector();
            var engine = new SimulationTickEngine(seed: 81, eventLogCollector: collector);
            var task = new TaskModel(60, 6f);
            var agents = new List<AgentRuntimeState>
            {
                new AgentRuntimeState("A", 3) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work },
                new AgentRuntimeState("B", 3) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work },
                new AgentRuntimeState("C", 3) { Position = (int)RoutineZone.Sleep, TargetZone = (int)RoutineZone.Sleep }
            };

            // Drive several ticks so faction clustering and knowledge logs stabilize.
            for (var tick = 1; tick <= 3; tick++)
            {
                var time = new DateTime(2026, 1, 1, 9, 0, 0).AddMinutes((tick - 1) * SimulationConstants.TickMinutes);
                engine.AdvanceTick(time, tick, task, agents);
            }

            Assert.IsFalse(string.IsNullOrWhiteSpace(agents[0].FactionId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(agents[1].FactionId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(agents[2].FactionId));
            Assert.That(agents[0].IsolationScore, Is.InRange(0f, 1f));
            Assert.That(agents[1].IsolationScore, Is.InRange(0f, 1f));
            Assert.That(agents[2].IsolationScore, Is.InRange(0f, 1f));
        }

        [Test]
        public void TickEngine_RecordsFactionEventsInKnowledgeLogs()
        {
            var collector = new EventLogCollector();
            var engine = new SimulationTickEngine(seed: 2027, eventLogCollector: collector);
            var task = new TaskModel(70, 8f);
            var agents = new List<AgentRuntimeState>
            {
                new AgentRuntimeState("Alpha", 3) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work, SubtaskWork = 4 },
                new AgentRuntimeState("Beta", 3) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work, SubtaskWork = 4 },
                new AgentRuntimeState("Gamma", 3) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work, SubtaskWork = 4 }
            };

            for (var tick = 1; tick <= 5; tick++)
            {
                var time = new DateTime(2026, 1, 1, 9, 0, 0).AddMinutes((tick - 1) * SimulationConstants.TickMinutes);
                engine.AdvanceTick(time, tick, task, agents);
            }

            var hasFactionEvent = false;
            for (var i = 0; i < collector.TickLogs.Count; i++)
            {
                var events = collector.TickLogs[i].KnowledgeEvents;
                if (events == null)
                {
                    continue;
                }

                for (var j = 0; j < events.Count; j++)
                {
                    if (events[j].IndexOf("faction_event", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasFactionEvent = true;
                        break;
                    }
                }

                if (hasFactionEvent)
                {
                    break;
                }
            }

            Assert.IsTrue(hasFactionEvent || !string.IsNullOrWhiteSpace(engine.LastFactionSummary));
        }


        [Test]
        public void IsolatedAgent_HasLongerAverageCompletionTime()
        {
            var baselineTicks = SimulateCompletionTicksForIsolation(0.1f, 0f, seed: 1234);
            var isolatedTicks = SimulateCompletionTicksForIsolation(0.95f, 0f, seed: 1234);

            Assert.Greater(isolatedTicks, baselineTicks);
        }

        [Test]
        public void KnowledgePropagation_InFactionSpreadsFasterThanCrossFaction()
        {
            var system = new KnowledgePropagationSystem();
            var affinity = new AffinitySystem();
            var random = new System.Random(7);

            var inA = new AgentRuntimeState("InA", 3) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work, FactionId = "faction:alpha" };
            var inB = new AgentRuntimeState("InB", 3) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work, FactionId = "faction:alpha" };
            var outC = new AgentRuntimeState("OutC", 3) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work, FactionId = "faction:beta" };

            inA.SetKnowledgeConfidence("knowledge.observe.basics", 1f);
            inB.SetKnowledgeConfidence("knowledge.observe.basics", 0f);
            outC.SetKnowledgeConfidence("knowledge.observe.basics", 0f);

            var snapshot = new FactionTickSnapshot(
                new Dictionary<string, string>
                {
                    ["InA"] = "faction:alpha",
                    ["InB"] = "faction:alpha",
                    ["OutC"] = "faction:beta"
                },
                null,
                null,
                null);

            for (var i = 0; i < 24; i++)
            {
                system.ProcessTick(new List<AgentRuntimeState> { inA, inB, outC }, affinity, null, random, snapshot);
            }

            Assert.Greater(inB.GetKnowledgeConfidence("knowledge.observe.basics"), outC.GetKnowledgeConfidence("knowledge.observe.basics"));
        }

        [Test]
        public void HighFactionCohesion_ReducesExternalCollaborationOutcome()
        {
            var system = new KnowledgePropagationSystem();
            var affinity = new AffinitySystem();

            var source = new AgentRuntimeState("Source", 3) { Position = (int)RoutineZone.Work, TargetZone = (int)RoutineZone.Work, FactionId = "faction:alpha" };
            var highCohesionTarget = new AgentRuntimeState("TargetHigh", 3)
            {
                Position = (int)RoutineZone.Work,
                TargetZone = (int)RoutineZone.Work,
                FactionId = "faction:beta",
                GroupthinkPenalty = 0.95f
            };
            var lowCohesionTarget = new AgentRuntimeState("TargetLow", 3)
            {
                Position = (int)RoutineZone.Work,
                TargetZone = (int)RoutineZone.Work,
                FactionId = "faction:beta",
                GroupthinkPenalty = 0.05f
            };

            source.SetKnowledgeConfidence("knowledge.observe.basics", 1f);
            highCohesionTarget.SetKnowledgeConfidence("knowledge.observe.basics", 0f);
            lowCohesionTarget.SetKnowledgeConfidence("knowledge.observe.basics", 0f);

            var snapshot = new FactionTickSnapshot(
                new Dictionary<string, string>
                {
                    ["Source"] = "faction:alpha",
                    ["TargetHigh"] = "faction:beta",
                    ["TargetLow"] = "faction:beta"
                },
                null,
                null,
                null);

            for (var i = 0; i < 28; i++)
            {
                system.ProcessTick(new List<AgentRuntimeState> { source, highCohesionTarget }, affinity, null, new System.Random(77 + i), snapshot);
                system.ProcessTick(new List<AgentRuntimeState> { source, lowCohesionTarget }, affinity, null, new System.Random(77 + i), snapshot);
            }

            Assert.Less(highCohesionTarget.GetKnowledgeConfidence("knowledge.observe.basics"), lowCohesionTarget.GetKnowledgeConfidence("knowledge.observe.basics"));
        }

        private static float SimulateCompletionTicksForIsolation(float isolationScore, float groupthinkPenalty, int seed)
        {
            var random = new System.Random(seed);
            var samples = 30;
            var totalTicks = 0f;

            for (var i = 0; i < samples; i++)
            {
                var agent = new AgentRuntimeState("Worker_" + i, 8)
                {
                    SubtaskWork = 110,
                    IsolationScore = isolationScore,
                    GroupthinkPenalty = groupthinkPenalty
                };

                var ticks = 0;
                while (agent.SubtaskWork > 0 && ticks < 500)
                {
                    ticks += 1;
                    agent.Work(ticks, false, random);
                }

                totalTicks += ticks;
            }

            return totalTicks / samples;
        }

        [Test]
        public void SpatialQuery_SortsByDistanceThenPriorityThenId()
        {
            var entities = new List<InteractableEntity>
            {
                new InteractableEntity("B", new Vector2(1, 0), 0.1f, 1, InteractableCategory.Prop),
                new InteractableEntity("A", new Vector2(1, 0), 0.1f, 1, InteractableCategory.Prop),
                new InteractableEntity("C", new Vector2(1, 0), 0.1f, 2, InteractableCategory.Prop)
            };

            var candidates = SpatialQueryService.FindCandidates(Vector2.zero, 2f, InteractableCategory.Prop, entities);
            Assert.AreEqual("C", candidates[0].EntityId);
            Assert.AreEqual("A", candidates[1].EntityId);
            Assert.AreEqual("B", candidates[2].EntityId);
        }
    }
}
