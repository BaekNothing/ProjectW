using NUnit.Framework;
using ProjectW.IngameMvp;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectW.Tests.EditMode
{
    public class RoutineZoneAnchorBindingTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AdvanceOneTick_BindsZonesByTagWithoutNameDependency()
        {
            var zonesRoot = CreateObject("Zones_Test");
            var missionZone = CreateTaggedZone(zonesRoot.transform, "Alpha", "zone.alpha", new[] { "zone.mission" }, new Vector3(11f, 4f, 0f), new Vector3(2f, 2f, 2f));
            CreateTaggedZone(zonesRoot.transform, "Bravo", "zone.bravo", new[] { "need.hunger" }, new Vector3(-10f, 3f, 0f), new Vector3(2f, 2f, 2f));
            CreateTaggedZone(zonesRoot.transform, "Charlie", "zone.charlie", new[] { "need.sleep" }, new Vector3(9f, -5f, 0f), new Vector3(2f, 2f, 2f));

            var session = CreateConfiguredSession(zonesRoot.transform, out var actor, out var binding);
            binding.hunger = 80f;
            binding.sleep = 80f;
            actor.transform.position = Vector3.zero;

            session.AdvanceOneTick();

            Assert.AreEqual(missionZone.transform.position + new Vector3(-0.9f, 0f, 0f), session.Characters[0].targetPosition);
        }

        [Test]
        public void AdvanceOneTick_ResolvesHungerOnlyInsideTaggedZoneBoundary()
        {
            var zonesRoot = CreateObject("Zones_Test_Boundary");
            CreateTaggedZone(zonesRoot.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(2f, 2f, 2f));
            var mealZone = CreateTaggedZone(zonesRoot.transform, "Meal", "zone.meal.main", new[] { "need.hunger" }, new Vector3(8f, 0f, 0f), new Vector3(2f, 2f, 2f));
            CreateTaggedZone(zonesRoot.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-8f, 0f, 0f), new Vector3(2f, 2f, 2f));

            var session = CreateConfiguredSession(zonesRoot.transform, out var actor, out var binding);
            binding.hunger = 10f;
            binding.stress = 10f;
            binding.sleep = 80f;
            actor.transform.position = Vector3.zero;
            SetField(session, "_absoluteTick", 5);

            session.AdvanceOneTick();
            var hungerAfterMoveTick = binding.hunger;
            Assert.Less(hungerAfterMoveTick, 10f);
            Assert.AreEqual(RoutineActionType.Move, binding.currentAction);

            actor.transform.position = mealZone.transform.position + new Vector3(-0.9f, 0f, 0f);
            SetField(session, "_absoluteTick", 5);
            session.AdvanceOneTick();
            Assert.Greater(binding.hunger, hungerAfterMoveTick);
        }

        [Test]
        public void AdvanceOneTick_SetsMoveActionUntilArrival()
        {
            var zonesRoot = CreateObject("Zones_Test_MoveAction");
            CreateTaggedZone(zonesRoot.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(12f, 0f, 0f), new Vector3(2f, 2f, 2f));
            CreateTaggedZone(zonesRoot.transform, "Meal", "zone.meal.main", new[] { "need.hunger" }, new Vector3(-8f, 0f, 0f), new Vector3(2f, 2f, 2f));
            CreateTaggedZone(zonesRoot.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(0f, 8f, 0f), new Vector3(2f, 2f, 2f));

            var session = CreateConfiguredSession(zonesRoot.transform, out var actor, out var binding);
            binding.hunger = 100f;
            binding.stress = 100f;
            binding.sleep = 100f;
            actor.transform.position = Vector3.zero;

            session.AdvanceOneTick();

            Assert.AreEqual(RoutineActionType.Move, binding.currentAction);
            Assert.AreEqual(RoutineActionType.Mission, binding.intendedAction);
            Assert.NotNull(binding.intentLabel);
            Assert.AreEqual("Intent: Mission", binding.intentLabel.text);
        }

        [Test]
        public void AdvanceOneTick_DoesNotEatWhenScheduleOrConditionMissing()
        {
            var zonesRoot = CreateObject("Zones_Test_Conditions");
            CreateTaggedZone(zonesRoot.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(2f, 2f, 2f));
            CreateTaggedZone(zonesRoot.transform, "Meal", "zone.meal.main", new[] { "need.hunger" }, new Vector3(8f, 0f, 0f), new Vector3(2f, 2f, 2f));
            CreateTaggedZone(zonesRoot.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-8f, 0f, 0f), new Vector3(2f, 2f, 2f));

            var session = CreateConfiguredSession(zonesRoot.transform, out var actor, out var binding);
            binding.hunger = 10f;
            binding.stress = 90f;
            actor.transform.position = Vector3.zero;
            SetField(session, "_absoluteTick", 5); // next tick is breakfast timing

            session.AdvanceOneTick();
            Assert.AreEqual(RoutineActionType.Mission, binding.intendedAction);
        }

        [Test]
        public void AdvanceOneTick_AvoidsOverlapWhenActingInSameZone()
        {
            var zonesRoot = CreateObject("Zones_Test_NoOverlap");
            var missionZone = CreateTaggedZone(zonesRoot.transform, "Mission", "zone.mission.main", new[] { "zone.mission" }, new Vector3(0f, 0f, 0f), new Vector3(8f, 8f, 2f));
            CreateTaggedZone(zonesRoot.transform, "Meal", "zone.meal.main", new[] { "need.hunger" }, new Vector3(8f, 0f, 0f), new Vector3(2f, 2f, 2f));
            CreateTaggedZone(zonesRoot.transform, "Sleep", "zone.sleep.main", new[] { "need.sleep" }, new Vector3(-8f, 0f, 0f), new Vector3(2f, 2f, 2f));

            var session = CreateObject("RoutineMvpSession_Test_Overlap").AddComponent<RoutineObservationMvpSession>();
            var characters = CreateObject("Characters");
            var actorA = CreateObject("Character_A");
            var actorB = CreateObject("Character_B");
            actorA.transform.SetParent(characters.transform);
            actorB.transform.SetParent(characters.transform);
            actorA.transform.position = missionZone.transform.position;
            actorB.transform.position = missionZone.transform.position;
            var timeText = CreateObject("CurrentTimeText").AddComponent<Text>();

            var bindingA = new RoutineCharacterBinding { actor = actorA.transform, targetPosition = actorA.transform.position, hunger = 80f, stress = 80f, sleep = 80f };
            var bindingB = new RoutineCharacterBinding { actor = actorB.transform, targetPosition = actorB.transform.position, hunger = 80f, stress = 80f, sleep = 80f };

            SetField(session, "zonesRoot", zonesRoot.transform);
            SetField(session, "charactersRoot", characters.transform);
            SetField(session, "currentTimeText", timeText);
            SetField(session, "characters", new List<RoutineCharacterBinding> { bindingA, bindingB });

            session.AdvanceOneTick();

            Assert.AreNotEqual(bindingA.targetPosition, bindingB.targetPosition);
        }

        private RoutineObservationMvpSession CreateConfiguredSession(Transform zonesRoot, out GameObject actor, out RoutineCharacterBinding binding)
        {
            var sessionGo = CreateObject("RoutineMvpSession_Test");
            var session = sessionGo.AddComponent<RoutineObservationMvpSession>();

            var characters = CreateObject("Characters");
            actor = CreateObject("Character_A");
            actor.transform.SetParent(characters.transform);
            actor.transform.position = Vector3.zero;

            var timeTextGo = CreateObject("CurrentTimeText");
            var timeText = timeTextGo.AddComponent<Text>();

            binding = new RoutineCharacterBinding
            {
                actor = actor.transform,
                targetPosition = actor.transform.position
            };

            SetField(session, "zonesRoot", zonesRoot);
            SetField(session, "charactersRoot", characters.transform);
            SetField(session, "currentTimeText", timeText);
            SetField(session, "characters", new List<RoutineCharacterBinding> { binding });
            return session;
        }

        private GameObject CreateTaggedZone(Transform parent, string objectName, string zoneId, string[] tags, Vector3 position, Vector3 boundarySize)
        {
            var zone = CreateObject(objectName);
            zone.transform.SetParent(parent);
            zone.transform.position = position;

            var collider = zone.AddComponent<BoxCollider>();
            collider.size = boundarySize;
            collider.center = Vector3.zero;

            var anchor = zone.AddComponent<RoutineZoneAnchor>();
            anchor.SetZoneId(zoneId);
            anchor.SetTags(tags);

            return zone;
        }

        private GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }
    }
}
