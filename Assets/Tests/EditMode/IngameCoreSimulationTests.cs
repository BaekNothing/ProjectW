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

            TaskAllocator.AssignWorkChunks(task, agents, new Random(42));

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
