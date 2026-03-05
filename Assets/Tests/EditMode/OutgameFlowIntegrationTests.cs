using NUnit.Framework;
using ProjectW.IngameMvp;
using ProjectW.IngameCore.Simulation;
using ProjectW.Outgame;
using System.Reflection;
using UnityEngine;

namespace ProjectW.Tests.EditMode
{
    public class OutgameFlowIntegrationTests
    {
        [Test]
        public void ApplyOutgameSetup_SafetyPriorityRaisesThresholds()
        {
            var zones = new GameObject("Zones");
            CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-6f, 0f, 0f), new Vector3(4f, 4f, 2f));

            var root = new GameObject("Characters");
            var actor = new GameObject("Character_A");
            actor.transform.SetParent(root.transform, false);

            var go = new GameObject("RoutineSession_OutgameSetup");
            var session = go.AddComponent<RoutineObservationMvpSession>();
            session.AdvanceOneTick();

            var before = session.Characters[0].hungerThreshold;
            session.ApplyOutgameSetup(new OutgameSessionSetup
            {
                SelectedCharacterIds = new System.Collections.Generic.List<string> { "Character_A" },
                InitialMissionType = MissionType.Recon,
                ResourcePriority = 20,
                SafetyPriority = 80
            });

            Assert.Greater(session.Characters[0].hungerThreshold, before);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ApplyOutgameSetup_MapsMissionTypeToGoalTicks()
        {
            var session = new GameObject("RoutineSession_MissionMap").AddComponent<RoutineObservationMvpSession>();
            session.ApplyOutgameSetup(new OutgameSessionSetup
            {
                SelectedCharacterIds = new System.Collections.Generic.List<string> { "Character_A" },
                InitialMissionType = MissionType.ResourceSweep,
                ResourcePriority = 50,
                SafetyPriority = 50
            });

            var field = typeof(RoutineObservationMvpSession).GetField("dashboardMissionGoalTicks", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(240, (int)field.GetValue(session));
            Object.DestroyImmediate(session.gameObject);
        }

        [Test]
        public void SessionFlowRuntimeContext_ConsumePendingSetupClearsAndPreservesLastResult()
        {
            SessionFlowRuntimeContext.ClearPendingSetup();
            SessionFlowRuntimeContext.ClearLastResult();

            var setup = new OutgameSessionSetup
            {
                SelectedCharacterIds = new System.Collections.Generic.List<string> { "Character_A", "Character_B" },
                InitialMissionType = MissionType.SafetyPatrol,
                ResourcePriority = 30,
                SafetyPriority = 70
            };

            SessionFlowRuntimeContext.SetPendingSetup(setup);
            var consumed = SessionFlowRuntimeContext.ConsumePendingSetupOrDefault();

            Assert.AreEqual(2, consumed.SelectedCharacterIds.Count);
            Assert.IsNull(SessionFlowRuntimeContext.PendingSetup);

            SessionFlowRuntimeContext.SetLastResult(new SessionResultSummary
            {
                TerminationReasonCode = "ObjectiveComplete",
                MissionProgressRatio = 1f,
                SurvivingCharacterCount = 2,
                TickIndex = 123,
                SessionId = "default"
            });

            Assert.IsNotNull(SessionFlowRuntimeContext.LastResult);
            Assert.AreEqual("ObjectiveComplete", SessionFlowRuntimeContext.LastResult.TerminationReasonCode);

            SessionFlowRuntimeContext.ClearLastResult();
        }

        [Test]
        public void OutgameSessionSetup_ClonesDifficultyAndPriorityFields()
        {
            var setup = new OutgameSessionSetup
            {
                SelectedDifficulty = SessionDifficulty.Risky,
                PriorityPair = new PriorityPair(WorkType.Reflex, WorkType.Observe),
                SelectedCharacterCount = 2
            };

            var cloned = setup.Clone();
            Assert.AreEqual(SessionDifficulty.Risky, cloned.SelectedDifficulty);
            Assert.AreEqual(WorkType.Reflex, cloned.PriorityPair.PrimaryWorkType);
            Assert.AreEqual(WorkType.Observe, cloned.PriorityPair.SecondaryWorkType);
            Assert.AreEqual(2, cloned.SelectedCharacterCount);
        }

        private static GameObject CreateZone(Transform parent, string objectName, string zoneId, string[] tags, Vector3 position, Vector3 boundarySize)
        {
            var zone = new GameObject(objectName);
            zone.transform.SetParent(parent);
            zone.transform.position = position;
            var collider = zone.AddComponent<BoxCollider>();
            collider.size = boundarySize;
            var anchor = zone.AddComponent<RoutineZoneAnchor>();
            anchor.SetZoneId(zoneId);
            anchor.SetTags(tags);
            return zone;
        }
    }
}
