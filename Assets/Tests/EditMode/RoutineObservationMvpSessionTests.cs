using NUnit.Framework;
using ProjectW.IngameMvp;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectW.Tests.EditMode
{
    public class RoutineObservationMvpSessionTests
    {
        [Test]
        public void ResolveAction_UsesMealSleepMissionSchedule()
        {
            Assert.AreEqual(RoutineActionType.Breakfast, RoutineSchedule.ResolveAction(6));
            Assert.AreEqual(RoutineActionType.Lunch, RoutineSchedule.ResolveAction(14));
            Assert.AreEqual(RoutineActionType.Dinner, RoutineSchedule.ResolveAction(22));
            Assert.AreEqual(RoutineActionType.Sleep, RoutineSchedule.ResolveAction(27));
            Assert.AreEqual(RoutineActionType.Sleep, RoutineSchedule.ResolveAction(30));
            Assert.AreEqual(RoutineActionType.Mission, RoutineSchedule.ResolveAction(1));
            Assert.AreEqual(RoutineActionType.Mission, RoutineSchedule.ResolveAction(18));
        }

        [Test]
        public void AdvanceTick_UpdatesActionsByIndependentNeeds()
        {
            var zones = new GameObject("Zones");
            CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(-5f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(5f, 0f, 0f), new Vector3(4f, 4f, 2f));
            var root = new GameObject("Characters");

            var c1 = new GameObject("Character_A");
            c1.transform.SetParent(root.transform);
            var c2 = new GameObject("Character_B");
            c2.transform.SetParent(root.transform);
            var c3 = new GameObject("Character_C");
            c3.transform.SetParent(root.transform);

            var go = new GameObject("RoutineSession_EditMode");
            var session = go.AddComponent<RoutineObservationMvpSession>();

            var first = session.AdvanceOneTick();
            Assert.AreEqual(1, first.dayIndex);
            Assert.AreEqual(1, first.halfDayIndex);
            Assert.AreEqual(1, first.tickInHalfDay);
            Assert.AreEqual(RoutineActionType.Mission, first.action);

            Assert.AreEqual(3, session.Characters.Count);
            var a = session.Characters[0];
            var b = session.Characters[1];
            var c = session.Characters[2];
            Assert.IsNull(a.actor.Find("RoutineStatusUI"));
            Assert.IsNull(a.actor.Find("RoutineNameLabel"));

            b.hunger = 0f;
            b.stress = 0f;
            c.hunger = 0f;
            c.stress = 90f;
            SetPrivateField(session, "_absoluteTick", 5); // next tick => breakfast
            session.AdvanceOneTick();

            Assert.AreEqual(RoutineActionType.Mission, a.intendedAction);
            Assert.AreEqual(RoutineActionType.Breakfast, b.intendedAction);
            Assert.AreEqual(RoutineActionType.Mission, c.intendedAction);
            Assert.AreEqual(RoutineActionType.Move, a.currentAction);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void AutoCreateDefaultCharacters_Uses2DSpriteVisual()
        {
            var zones = new GameObject("Zones");
            CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            var root = new GameObject("Characters");
            var go = new GameObject("RoutineSession_DefaultDummy");
            var session = go.AddComponent<RoutineObservationMvpSession>();

            session.BakeGeneratedObjectsToScene();
            session.AdvanceOneTick();

            var actor = root.transform.Find("Character_A");
            Assert.IsNotNull(actor);
            Assert.IsNotNull(actor.GetComponent<SpriteRenderer>());
            Assert.IsNull(actor.GetComponent<CapsuleCollider>());
            Assert.AreEqual(5f, session.Characters[0].moveSpeed, 0.001f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ExplicitGaugeObjects_AreUpdatedWithoutDynamicCreation()
        {
            var zones = new GameObject("Zones");
            CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            var root = new GameObject("Characters");
            var actorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actorGo.name = "Character_A";
            actorGo.transform.SetParent(root.transform, false);
            actorGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var gaugeRoot = new GameObject("GaugeRoot").transform;
            gaugeRoot.SetParent(actorGo.transform, false);
            CreateGaugeBar(gaugeRoot, "HungerBar", 0.4f);
            CreateGaugeBar(gaugeRoot, "SleepBar", 0f);
            CreateGaugeBar(gaugeRoot, "StressBar", -0.4f);

            var go = new GameObject("RoutineSession_Gauge");
            var session = go.AddComponent<RoutineObservationMvpSession>();
            session.AdvanceOneTick();
            var binding = session.Characters[0];
            binding.hunger = 10f;
            binding.sleep = 60f;
            binding.stress = 90f;

            InvokePrivateMethod(session, "UpdateRuntimeStateTexts", binding);

            Assert.Less(actorGo.transform.Find("GaugeRoot/HungerBar").localScale.x, actorGo.transform.Find("GaugeRoot/SleepBar").localScale.x);
            Assert.Less(actorGo.transform.Find("GaugeRoot/SleepBar").localScale.x, actorGo.transform.Find("GaugeRoot/StressBar").localScale.x);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void AdvanceTick_CreatesAndEnablesTargetPathLine()
        {
            var zones = new GameObject("Zones");
            CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            var root = new GameObject("Characters");
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name = "Character_A";
            actor.transform.SetParent(root.transform, false);
            actor.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var go = new GameObject("RoutineSession_TargetLine");
            var session = go.AddComponent<RoutineObservationMvpSession>();
            session.AdvanceOneTick();

            var line = actor.transform.Find("TargetPathLine");
            Assert.IsNotNull(line);
            var renderer = line.GetComponent<LineRenderer>();
            Assert.IsNotNull(renderer);
            Assert.IsTrue(renderer.enabled);
            Assert.AreEqual(2, renderer.positionCount);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void HumanPhysiologyPreset_UsesOneDayHungerAndThreeDayFatigueDecay()
        {
            var zones = new GameObject("Zones");
            CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(3f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-3f, 0f, 0f), new Vector3(4f, 4f, 2f));
            var root = new GameObject("Characters");
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name = "Character_A";
            actor.transform.SetParent(root.transform, false);
            actor.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var go = new GameObject("RoutineSession_Physiology");
            var session = go.AddComponent<RoutineObservationMvpSession>();
            session.AdvanceOneTick();

            var binding = session.Characters[0];
            Assert.AreEqual(100f / 60f, binding.hungerDecayPerTick, 0.0001f);
            Assert.AreEqual(100f / 180f, binding.sleepDecayPerTick, 0.0001f);
            Assert.AreEqual(100f / 180f, binding.stressDecayPerTick, 0.0001f);

            binding.hunger = 100f;
            binding.sleep = 100f;
            binding.stress = 100f;

            for (int i = 0; i < 60; i++)
            {
                InvokePrivateMethod(session, "ApplyNeedsAndProgress", binding, RoutineActionType.Mission, false);
            }

            Assert.LessOrEqual(binding.hunger, 0.01f);
            Assert.Greater(binding.sleep, 60f);
            Assert.Greater(binding.stress, 60f);

            binding.sleep = 100f;
            binding.stress = 100f;
            for (int i = 0; i < 180; i++)
            {
                InvokePrivateMethod(session, "ApplyNeedsAndProgress", binding, RoutineActionType.Mission, false);
            }

            Assert.LessOrEqual(binding.sleep, 0.01f);
            Assert.LessOrEqual(binding.stress, 0.01f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void AdvanceTick_UpdatesGoalProgressAndSituationDashboardTexts()
        {
            var zones = new GameObject("Zones");
            CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            var root = new GameObject("Characters");
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name = "Character_A";
            actor.transform.SetParent(root.transform, false);
            actor.transform.localPosition = Vector3.zero;

            var timeText = new GameObject("CurrentTimeText").AddComponent<Text>();
            var goalText = new GameObject("GoalText").AddComponent<Text>();
            var progressText = new GameObject("ProgressText").AddComponent<Text>();
            var situationText = new GameObject("SituationText").AddComponent<Text>();

            var go = new GameObject("RoutineSession_Dashboard");
            var session = go.AddComponent<RoutineObservationMvpSession>();
            SetPrivateField(session, "autoCreateDashboardUi", false);
            SetPrivateField(session, "currentTimeText", timeText);
            SetPrivateField(session, "goalText", goalText);
            SetPrivateField(session, "progressText", progressText);
            SetPrivateField(session, "situationText", situationText);
            session.SetInterventionVisibility(2, 7, "priority_conflict");
            session.AdvanceOneTick();

            StringAssert.Contains("Goal:", goalText.text);
            StringAssert.Contains("Progress:", progressText.text);
            StringAssert.Contains("Interventions pending:2", situationText.text);
            StringAssert.Contains("latestReject:priority_conflict", situationText.text);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(timeText.gameObject);
            Object.DestroyImmediate(goalText.gameObject);
            Object.DestroyImmediate(progressText.gameObject);
            Object.DestroyImmediate(situationText.gameObject);
        }

        [Test]
        public void DashboardContext_ExposesCustomRuntimeStatusForDevelopers()
        {
            var zones = new GameObject("Zones");
            CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            var root = new GameObject("Characters");
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name = "Character_A";
            actor.transform.SetParent(root.transform, false);
            actor.transform.localPosition = Vector3.zero;

            var timeText = new GameObject("CurrentTimeText").AddComponent<Text>();
            var situationText = new GameObject("SituationText").AddComponent<Text>();

            var go = new GameObject("RoutineSession_DashboardContext");
            var session = go.AddComponent<RoutineObservationMvpSession>();
            SetPrivateField(session, "autoCreateDashboardUi", false);
            SetPrivateField(session, "currentTimeText", timeText);
            SetPrivateField(session, "situationText", situationText);
            session.SetDashboardContext("LevelGen", "Seed=42, Stage=Layout");
            session.AdvanceOneTick();

            StringAssert.Contains("LevelGen: Seed=42, Stage=Layout", situationText.text);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(timeText.gameObject);
            Object.DestroyImmediate(situationText.gameObject);
        }

        [Test]
        public void AdvanceTick_AppliesDepthLayoutSoCharactersStayCloserToCameraThanZones()
        {
            var zones = new GameObject("Zones");
            var mission = CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            var root = new GameObject("Characters");
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name = "Character_A";
            actor.transform.SetParent(root.transform, false);
            actor.transform.localPosition = Vector3.zero;
            var timeText = new GameObject("CurrentTimeText").AddComponent<Text>();

            var go = new GameObject("RoutineSession_RenderOrder");
            var session = go.AddComponent<RoutineObservationMvpSession>();
            session.AdvanceOneTick();

            Assert.AreEqual(0f, mission.transform.position.z, 0.0001f);
            Assert.AreEqual(-1f, actor.transform.position.z, 0.0001f);
            Assert.Less(actor.transform.position.z, mission.transform.position.z);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(timeText.gameObject);
        }

        [Test]
        public void AdvanceTick_ConvertsLegacy3DObjectsTo2DRuntimeRepresentation()
        {
            var zones = new GameObject("Zones");
            var mission = CreateZone(zones.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Cafeteria", "zone.meal.main", new[] { "need.hunger" }, new Vector3(6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            CreateZone(zones.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-6f, 0f, 0f), new Vector3(4f, 4f, 2f));
            var root = new GameObject("Characters");
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name = "Character_A";
            actor.transform.SetParent(root.transform, false);
            actor.transform.localPosition = Vector3.zero;
            var timeText = new GameObject("CurrentTimeText").AddComponent<Text>();

            var go = new GameObject("RoutineSession_2DConversion");
            var session = go.AddComponent<RoutineObservationMvpSession>();
            session.AdvanceOneTick();

            Assert.IsNotNull(mission.GetComponent<SpriteRenderer>());
            Assert.IsNotNull(mission.GetComponent<BoxCollider2D>());
            Assert.IsNotNull(actor.GetComponent<SpriteRenderer>());
            Assert.IsFalse(actor.GetComponent<MeshRenderer>().enabled);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zones);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(timeText.gameObject);
        }

        [Test]
        public void ComposeFromMissionZone_CreatesSecondaryZonesAndKeepsCharacters()
        {
            DestroyIfExists("Zones");

            var zones = new GameObject("Zones");
            var mission = new GameObject("MissionZone");
            mission.transform.SetParent(zones.transform, false);
            mission.transform.position = Vector3.zero;
            mission.transform.localScale = new Vector3(6f, 6f, 1f);
            mission.AddComponent<SpriteRenderer>();
            var missionCollider = mission.AddComponent<BoxCollider2D>();
            missionCollider.size = Vector2.one;
            var missionAnchor = mission.AddComponent<RoutineZoneAnchor>();
            missionAnchor.SetZoneId("zone.mission");
            missionAnchor.SetTags("zone.mission");

            var characters = new GameObject("Characters");
            new GameObject("Character_A").transform.SetParent(characters.transform, false);
            new GameObject("Character_B").transform.SetParent(characters.transform, false);
            new GameObject("Character_C").transform.SetParent(characters.transform, false);

            var go = new GameObject("RoutineSession_ComposeMission");
            var session = go.AddComponent<RoutineObservationMvpSession>();
            session.ComposeCafeteriaAndSleepFromMission();

            var cafeteria = GameObject.Find("CafeteriaZone");
            var sleep = GameObject.Find("SleepZone");

            Assert.IsNotNull(zones);
            Assert.IsNotNull(characters);
            Assert.IsNotNull(mission);
            Assert.IsNotNull(cafeteria);
            Assert.IsNotNull(sleep);
            Assert.IsNotNull(GameObject.Find("Character_A"));
            Assert.IsNotNull(GameObject.Find("Character_B"));
            Assert.IsNotNull(GameObject.Find("Character_C"));

            var missionScale = mission.transform.localScale;
            var missionRight = mission.transform.position.x + (missionScale.x * 0.5f);
            var sleepLeft = sleep.transform.position.x - (sleep.transform.localScale.x * 0.5f);
            var zoneGap = sleepLeft - missionRight;
            Assert.GreaterOrEqual(zoneGap, 1.5f);
            Assert.IsNotNull(cafeteria.transform.Find("ObjectSlots"));
            Assert.IsNotNull(sleep.transform.Find("ObjectSlots"));

            Object.DestroyImmediate(go);
            DestroyIfExists("Zones");
            DestroyIfExists("Characters");
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName, object argument)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(target, new[] { argument });
        }

        private static void InvokePrivateMethod(object target, string methodName, object arg0, object arg1, object arg2)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(target, new[] { arg0, arg1, arg2 });
        }

        private static void CreateGaugeBar(Transform parent, string name, float y)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0f, y, 0f);
            bar.transform.localScale = new Vector3(1f, 0.15f, 0.1f);
        }

        private static void DestroyIfExists(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
