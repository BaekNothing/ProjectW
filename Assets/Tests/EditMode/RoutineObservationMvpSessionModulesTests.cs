using NUnit.Framework;
using ProjectW.IngameMvp;
using System.Collections.Generic;
using UnityEngine;
using ProjectW.IngameCore.StateMachine;

namespace ProjectW.Tests.EditMode
{
    public class RoutineObservationMvpSessionModulesTests
    {
        [Test]
        public void CoreLoopOrchestrator_ExecutesHandlersInExpectedOrder()
        {
            var orchestrator = new CoreLoopOrchestrator();
            var state = CoreLoopState.Plan;
            var sequence = new List<string>();
            var defaultAction = RoutineActionType.Mission;
            var zoneName = "zone.mission.main";

            orchestrator.Execute(
                10,
                0,
                ref defaultAction,
                ref zoneName,
                () => state,
                (expected, requested, handler) =>
                {
                    if (state != expected) return;
                    sequence.Add(expected.ToString());
                    handler();
                    state = requested;
                },
                () => true,
                () => true,
                () => true,
                () => true,
                () => true,
                (_, __, context) =>
                {
                    context.RequestedNextState = CoreLoopState.NextCycle;
                    return true;
                },
                () => true,
                () => { });

            CollectionAssert.AreEqual(
                new[] { "Plan", "Drop", "AutoNarrative", "CaptainIntervention", "NightDream", "Resolve", "NextCycle" },
                sequence);
            Assert.AreEqual(CoreLoopState.Plan, state);
        }

        [Test]
        public void CharacterActionResolver_MissionAction_AccumulatesProgressAndNeeds()
        {
            var resolver = new CharacterActionResolver(100f);
            var actor = new GameObject("Character_A");
            var binding = new RoutineCharacterBinding
            {
                actor = actor.transform,
                hunger = 100f,
                sleep = 100f,
                stress = 100f,
                hungerDecayPerTick = 2f,
                sleepDecayPerTick = 3f,
                stressDecayPerTick = 4f
            };

            var dynamicProgressCalled = 0;
            resolver.ApplyNeedsAndProgress(binding, RoutineActionType.Mission, true, true, (_, __) => dynamicProgressCalled++);

            Assert.AreEqual(1, binding.missionTicks);
            Assert.AreEqual(98f, binding.hunger);
            Assert.AreEqual(97f, binding.sleep);
            Assert.AreEqual(96f, binding.stress);
            Assert.AreEqual(1, dynamicProgressCalled);

            Object.DestroyImmediate(actor);
        }

        [Test]
        public void IngameDashboardPresenter_BuildSituationSummary_OrdersContextAndSkipsChronicle()
        {
            var presenter = new IngameDashboardPresenter();
            var context = new Dictionary<string, string>
            {
                ["Chronicle"] = "ignored",
                ["LevelGen"] = "Seed=42",
                ["FactionEvents"] = "Alliance+"
            };

            var summary = presenter.BuildSituationSummary(2, 7, "priority_conflict", context);

            StringAssert.Contains("Interventions pending:2, lastApplied:7, latestReject:priority_conflict", summary);
            StringAssert.Contains("Faction: Alliance+", summary);
            StringAssert.Contains("LevelGen: Seed=42", summary);
            Assert.IsFalse(summary.Contains("Chronicle: ignored"));
        }
    }
}
