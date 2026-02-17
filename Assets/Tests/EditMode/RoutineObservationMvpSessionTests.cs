using NUnit.Framework;
using ProjectW.IngameMvp;

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
        public void AdvanceTick_UpdatesSnapshotAndMissionTicks()
        {
            var mission = new UnityEngine.GameObject("MissionZone");
            var cafe = new UnityEngine.GameObject("CafeteriaZone");
            var sleep = new UnityEngine.GameObject("SleepZone");
            var root = new UnityEngine.GameObject("Characters");
            var c1 = new UnityEngine.GameObject("Character_A");
            c1.transform.SetParent(root.transform);
            var go = new UnityEngine.GameObject("RoutineSession_EditMode");
            var session = go.AddComponent<RoutineObservationMvpSession>();

            var first = session.AdvanceOneTick();
            Assert.AreEqual(1, first.dayIndex);
            Assert.AreEqual(1, first.halfDayIndex);
            Assert.AreEqual(1, first.tickInHalfDay);
            Assert.AreEqual(RoutineActionType.Mission, first.action);

            for (int i = 0; i < 5; i++)
            {
                session.AdvanceOneTick();
            }

            var mealTick = session.AdvanceOneTick();
            Assert.AreEqual(7, mealTick.tickInHalfDay);
            Assert.AreEqual(RoutineActionType.Breakfast, RoutineSchedule.ResolveAction(6));

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(mission);
            UnityEngine.Object.DestroyImmediate(cafe);
            UnityEngine.Object.DestroyImmediate(sleep);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
