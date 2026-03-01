using NUnit.Framework;
using ProjectW.IngameMvp;
using System.Reflection;
using UnityEngine;

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
        public void AdvanceTick_CreatesCharacterUiAndMovesByIndependentNeeds()
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
            Assert.IsNotNull(a.actor.Find("RoutineStatusUI"));
            Assert.IsNotNull(a.actor.Find("RoutineNameLabel"));
            Assert.AreEqual("A", a.nameLabel.text);

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
    }
}
