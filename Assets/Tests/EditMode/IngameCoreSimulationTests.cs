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
