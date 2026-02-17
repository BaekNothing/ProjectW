using NUnit.Framework;
using ProjectW.IngameMvp;
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
            var mission = new GameObject("MissionZone");
            mission.transform.position = new Vector3(-5f, 0f, 0f);
            var cafe = new GameObject("CafeteriaZone");
            cafe.transform.position = new Vector3(0f, 0f, 0f);
            var sleep = new GameObject("SleepZone");
            sleep.transform.position = new Vector3(5f, 0f, 0f);
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
            c.sleep = 0f;
            session.AdvanceOneTick();

            Assert.AreEqual(RoutineActionType.Mission, a.currentAction);
            Assert.AreEqual(RoutineActionType.Eat, b.currentAction);
            Assert.AreEqual(RoutineActionType.Sleep, c.currentAction);
            Assert.AreNotEqual(a.targetPosition, b.targetPosition);
            Assert.AreNotEqual(a.targetPosition, c.targetPosition);
            Assert.Greater(a.missionTicks, 0);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(mission);
            Object.DestroyImmediate(cafe);
            Object.DestroyImmediate(sleep);
            Object.DestroyImmediate(root);
        }
    }
}
