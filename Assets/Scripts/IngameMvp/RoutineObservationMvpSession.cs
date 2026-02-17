using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectW.IngameMvp
{
    public enum RoutineActionType
    {
        Mission,
        Breakfast,
        Lunch,
        Dinner,
        Sleep
    }

    [Serializable]
    public class RoutineCharacterBinding
    {
        public Transform actor;
        public float moveSpeed = 2.5f;
        [NonSerialized] public Vector3 targetPosition;
        [NonSerialized] public int missionTicks;
    }

    public struct RoutineTickSnapshot
    {
        public int dayIndex;
        public int halfDayIndex;
        public int tickInHalfDay;
        public string timeText;
        public RoutineActionType action;
        public string zoneName;
    }

    public static class RoutineSchedule
    {
        public static RoutineActionType ResolveAction(int tickInHalfDay)
        {
            if (tickInHalfDay == 6) return RoutineActionType.Breakfast;
            if (tickInHalfDay == 14) return RoutineActionType.Lunch;
            if (tickInHalfDay == 22) return RoutineActionType.Dinner;
            if (tickInHalfDay >= 27 && tickInHalfDay <= 30) return RoutineActionType.Sleep;
            return RoutineActionType.Mission;
        }
    }

    public class RoutineObservationMvpSession : MonoBehaviour
    {
        [Header("Tick")]
        [SerializeField] private bool autoRunOnStart = true;
        [SerializeField] private float tickIntervalSeconds = 0.6f;
        [SerializeField] private int ticksPerHalfDay = 30;

        [Header("Zone Anchors (Scene Fixed Objects)")]
        [SerializeField] private Transform missionZone;
        [SerializeField] private Transform cafeteriaZone;
        [SerializeField] private Transform sleepZone;
        [SerializeField] private Transform charactersRoot;

        [Header("UI")]
        [SerializeField] private Text currentTimeText;

        [Header("Characters")]
        [SerializeField] private List<RoutineCharacterBinding> characters = new List<RoutineCharacterBinding>();

        private int _absoluteTick;
        private Coroutine _loopCoroutine;

        public IReadOnlyList<RoutineCharacterBinding> Characters => characters;
        public int AbsoluteTick => _absoluteTick;

        private void Awake()
        {
            AutoBindSceneReferences();
        }

        private void Start()
        {
            if (autoRunOnStart)
            {
                StartSession();
            }
        }

        private void Update()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null) continue;
                binding.actor.position = Vector3.MoveTowards(
                    binding.actor.position,
                    binding.targetPosition,
                    binding.moveSpeed * Time.deltaTime);
            }
        }

        public void StartSession()
        {
            if (_loopCoroutine != null)
            {
                return;
            }

            Debug.Log("[RoutineMVP] Session started: infinite mission routine with meal/sleep zones.");
            _loopCoroutine = StartCoroutine(RunLoop());
        }

        public void StopSession()
        {
            if (_loopCoroutine == null) return;
            StopCoroutine(_loopCoroutine);
            _loopCoroutine = null;
        }

        public RoutineTickSnapshot AdvanceOneTick()
        {
            if (missionZone == null || cafeteriaZone == null || sleepZone == null || characters.Count == 0 || currentTimeText == null)
            {
                AutoBindSceneReferences();
            }

            _absoluteTick += 1;

            int ticksPerDay = ticksPerHalfDay * 2;
            int dayIndex = ((_absoluteTick - 1) / ticksPerDay) + 1;
            int halfDayIndex = (((_absoluteTick - 1) / ticksPerHalfDay) % 2) + 1;
            int tickInHalfDay = ((_absoluteTick - 1) % ticksPerHalfDay) + 1;
            var action = RoutineSchedule.ResolveAction(tickInHalfDay);

            Transform zone = ResolveZone(action);
            string zoneName = zone != null ? zone.name : "MissingZone";

            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null) continue;

                var offset = new Vector3(i * 1.4f - 1.4f, 0f, 0f);
                binding.targetPosition = zone != null ? zone.position + offset : binding.actor.position;
                if (action == RoutineActionType.Mission)
                {
                    binding.missionTicks += 1;
                }
            }

            string timeText = BuildTimeText(dayIndex, halfDayIndex, tickInHalfDay);
            if (currentTimeText != null)
            {
                currentTimeText.text = timeText;
            }

            Debug.Log($"[RoutineMVP] {timeText} action={action} zone={zoneName} mission=infinite");
            if (characters.Count > 0 && characters[0] != null)
            {
                Debug.Log($"[RoutineMVP] {characters[0].actor?.name ?? "Character"} mission_ticks={characters[0].missionTicks}");
            }

            return new RoutineTickSnapshot
            {
                dayIndex = dayIndex,
                halfDayIndex = halfDayIndex,
                tickInHalfDay = tickInHalfDay,
                timeText = timeText,
                action = action,
                zoneName = zoneName
            };
        }

        private IEnumerator RunLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(tickIntervalSeconds);
                AdvanceOneTick();
            }
        }

        private Transform ResolveZone(RoutineActionType action)
        {
            switch (action)
            {
                case RoutineActionType.Breakfast:
                case RoutineActionType.Lunch:
                case RoutineActionType.Dinner:
                    return cafeteriaZone;
                case RoutineActionType.Sleep:
                    return sleepZone;
                default:
                    return missionZone;
            }
        }

        private string BuildTimeText(int dayIndex, int halfDayIndex, int tickInHalfDay)
        {
            int ticksFromDayStart = (halfDayIndex - 1) * ticksPerHalfDay + (tickInHalfDay - 1);
            int minutesFromDayStart = ticksFromDayStart * 24;
            int totalMinutes = (6 * 60) + minutesFromDayStart;
            int hour = (totalMinutes / 60) % 24;
            int minute = totalMinutes % 60;
            return string.Format(
                CultureInfo.InvariantCulture,
                "Day {0} | {1:D2}:{2:D2} | Half {3} Tick {4}/{5}",
                dayIndex,
                hour,
                minute,
                halfDayIndex,
                tickInHalfDay,
                ticksPerHalfDay);
        }

        private void AutoBindSceneReferences()
        {
            if (missionZone == null)
            {
                var go = GameObject.Find("MissionZone");
                missionZone = go != null ? go.transform : null;
            }

            if (cafeteriaZone == null)
            {
                var go = GameObject.Find("CafeteriaZone");
                cafeteriaZone = go != null ? go.transform : null;
            }

            if (sleepZone == null)
            {
                var go = GameObject.Find("SleepZone");
                sleepZone = go != null ? go.transform : null;
            }

            if (charactersRoot == null)
            {
                var go = GameObject.Find("Characters");
                charactersRoot = go != null ? go.transform : null;
            }

            if (currentTimeText == null)
            {
                var go = GameObject.Find("CurrentTimeText");
                currentTimeText = go != null ? go.GetComponent<Text>() : null;
            }

            if (characters.Count == 0 && charactersRoot != null)
            {
                for (int i = 0; i < charactersRoot.childCount; i++)
                {
                    var child = charactersRoot.GetChild(i);
                    characters.Add(new RoutineCharacterBinding
                    {
                        actor = child,
                        moveSpeed = 2.5f,
                        targetPosition = child.position
                    });
                }
            }
        }
    }
}
