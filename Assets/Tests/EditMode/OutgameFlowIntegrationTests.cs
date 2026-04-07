using NUnit.Framework;
using ProjectW.IngameMvp;
using ProjectW.IngameCore.Simulation;
using ProjectW.Outgame;
using System.Collections.Generic;
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
                SelectedCharacterCount = 2,
                PresetId = "preset.alpha"
            };

            var cloned = setup.Clone();
            Assert.AreEqual(SessionDifficulty.Risky, cloned.SelectedDifficulty);
            Assert.AreEqual(WorkType.Reflex, cloned.PriorityPair.PrimaryWorkType);
            Assert.AreEqual(WorkType.Observe, cloned.PriorityPair.SecondaryWorkType);
            Assert.AreEqual(2, cloned.SelectedCharacterCount);
            Assert.AreEqual("preset.alpha", cloned.PresetId);
        }

        [Test]
        public void ApplyOutgameSetup_AppliesPlacementPresetInNormalMode()
        {
            var session = new GameObject("RoutineSession_PresetApply_Normal").AddComponent<RoutineObservationMvpSession>();
            var preset = BuildPlacementPreset(
                "preset.normal.apply",
                new[]
                {
                    BuildZonePlacement("PresetMission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(5f, 3f, 1f)),
                    BuildZonePlacement("PresetCafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(-4f, -1f, 0f), new Vector3(4f, 3f, 1f)),
                    BuildZonePlacement("PresetSleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(4f, 1f, 0f), new Vector3(4f, 3f, 1f)),
                    BuildCharacterPlacement("PresetCharacter_A", new Vector3(-1f, -2f, 0f)),
                    BuildCharacterPlacement("PresetCharacter_B", new Vector3(1f, -2f, 0f))
                });

            InjectPlacementPresetCatalog(session, preset);
            session.ApplyOutgameSetup(new OutgameSessionSetup
            {
                SessionMode = SessionModePreset.Normal,
                SelectedCharacterCount = 2,
                PresetId = "preset.normal.apply"
            });

            Assert.AreEqual(3, CountChildren("Zones_Dynamic"));
            Assert.AreEqual(2, CountChildren("Characters_Dynamic"));
            Assert.AreEqual("preset.normal.apply", GetDashboardContext(session, "PlacementPresetId"));
            Assert.AreEqual("Resolved", GetDashboardContext(session, "PlacementPresetStatus"));
            Assert.AreEqual("No", GetDashboardContext(session, "PlacementPresetFallback"));
            Assert.AreEqual("Applied", GetDashboardContext(session, "PlacementPresetBuild"));

            CleanupSessionWorld(session, preset);
        }

        [Test]
        public void ApplyOutgameSetup_MissingPresetFallsBackInExhibitionMode()
        {
            var session = new GameObject("RoutineSession_PresetMissing_Exhibition").AddComponent<RoutineObservationMvpSession>();
            var preset = BuildPlacementPreset(
                "preset.exhibition.available",
                new[]
                {
                    BuildZonePlacement("AvailableZone", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(5f, 3f, 1f)),
                    BuildCharacterPlacement("AvailableCharacter", new Vector3(0f, -2f, 0f))
                });

            InjectPlacementPresetCatalog(session, preset);
            session.ApplyOutgameSetup(new OutgameSessionSetup
            {
                SessionMode = SessionModePreset.Exhibition,
                SelectedCharacterCount = 2,
                PresetId = "preset.not.found"
            });

            Assert.AreEqual(3, CountChildren("Zones_Dynamic"));
            Assert.AreEqual(2, CountChildren("Characters_Dynamic"));
            Assert.AreEqual("preset.not.found", GetDashboardContext(session, "PlacementPresetId"));
            Assert.AreEqual("Missing", GetDashboardContext(session, "PlacementPresetStatus"));
            Assert.AreEqual("Yes", GetDashboardContext(session, "PlacementPresetFallback"));
            Assert.AreEqual("NoPresetOrPlacements", GetDashboardContext(session, "PlacementPresetBuild"));

            CleanupSessionWorld(session, preset);
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

        private static ResourcePlacement BuildZonePlacement(string objectName, string zoneId, string[] tags, Vector3 position, Vector3 scale)
        {
            return new ResourcePlacement
            {
                Type = ResourcePlacementType.Zone,
                ObjectName = objectName,
                ZoneId = zoneId,
                ZoneTags = tags,
                LocalPosition = position,
                LocalScale = scale,
                Active = true
            };
        }

        private static ResourcePlacement BuildCharacterPlacement(string objectName, Vector3 position)
        {
            return new ResourcePlacement
            {
                Type = ResourcePlacementType.Character,
                ObjectName = objectName,
                LocalPosition = position,
                Active = true
            };
        }

        private static ResourcePlacementPreset BuildPlacementPreset(string presetId, ResourcePlacement[] placements)
        {
            var preset = ScriptableObject.CreateInstance<ResourcePlacementPreset>();
            preset.SetData(
                new List<ResourcePlacement>(placements),
                new ResourcePlacementMeta
                {
                    PresetId = presetId,
                    DisplayName = presetId
                });
            return preset;
        }

        private static void InjectPlacementPresetCatalog(RoutineObservationMvpSession session, ResourcePlacementPreset preset)
        {
            var field = typeof(RoutineObservationMvpSession).GetField("placementPresetCatalog", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(session, new List<ResourcePlacementPreset> { preset });
        }

        private static string GetDashboardContext(RoutineObservationMvpSession session, string key)
        {
            var method = typeof(RoutineObservationMvpSession).GetMethod("TryGetDashboardValue", BindingFlags.NonPublic | BindingFlags.Instance);
            return method.Invoke(session, new object[] { key }) as string;
        }

        private static int CountChildren(string rootName)
        {
            var root = GameObject.Find(rootName);
            return root == null ? 0 : root.transform.childCount;
        }

        private static void CleanupSessionWorld(RoutineObservationMvpSession session, ResourcePlacementPreset preset)
        {
            Object.DestroyImmediate(session.gameObject);
            var zonesDynamic = GameObject.Find("Zones_Dynamic");
            if (zonesDynamic != null)
            {
                Object.DestroyImmediate(zonesDynamic);
            }

            var charactersDynamic = GameObject.Find("Characters_Dynamic");
            if (charactersDynamic != null)
            {
                Object.DestroyImmediate(charactersDynamic);
            }

            if (preset != null)
            {
                Object.DestroyImmediate(preset);
            }
        }
    }
}
