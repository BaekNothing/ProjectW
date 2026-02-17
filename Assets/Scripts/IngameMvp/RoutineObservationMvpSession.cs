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
        Eat,
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
        public int routineOffsetTicks;
        public float hungerDecayPerTick = 5f;
        public float hungerRecoverPerMeal = 50f;
        public float hungerThreshold = 30f;
        public float sleepDecayPerTick = 4f;
        public float sleepRecoverPerSleep = 40f;
        public float sleepThreshold = 25f;
        public string displayName;

        [NonSerialized] public Vector3 targetPosition;
        [NonSerialized] public int missionTicks;
        [NonSerialized] public float hunger = 100f;
        [NonSerialized] public float sleep = 100f;
        [NonSerialized] public RoutineActionType currentAction;
        [NonSerialized] public bool runtimeInitialized;
        [NonSerialized] public Transform statusUiRoot;
        [NonSerialized] public Transform hungerFill;
        [NonSerialized] public Transform sleepFill;
        [NonSerialized] public TextMesh nameLabel;
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
        private const float GaugeMax = 100f;
        private const float GaugeWidth = 0.9f;
        private const float GaugeHeight = 0.08f;

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
            var defaultAction = RoutineSchedule.ResolveAction(tickInHalfDay);
            Transform defaultZone = ResolveZone(defaultAction);
            string zoneName = defaultZone != null ? defaultZone.name : "MissingZone";

            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null) continue;
                EnsureRuntimeBindingInitialized(binding, i);

                var action = ResolveCharacterAction(binding, tickInHalfDay);
                binding.currentAction = action;
                ApplyNeedsAndProgress(binding, action);

                Transform zone = ResolveZone(action);
                var offset = GetCharacterSlotOffset(i);
                binding.targetPosition = zone != null ? zone.position + offset : binding.actor.position;
                UpdateStatusVisual(binding);
            }

            string timeText = BuildTimeText(dayIndex, halfDayIndex, tickInHalfDay);
            if (currentTimeText != null)
            {
                currentTimeText.text = timeText;
            }

            Debug.Log($"[RoutineMVP] {timeText} schedule={defaultAction} zone={zoneName} mission=infinite");
            if (characters.Count > 0)
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    var binding = characters[i];
                    if (binding.actor == null) continue;
                    Debug.Log(
                        $"[RoutineMVP] {binding.actor.name} action={binding.currentAction} hunger={binding.hunger:0} sleep={binding.sleep:0} mission_ticks={binding.missionTicks}");
                }
            }

            return new RoutineTickSnapshot
            {
                dayIndex = dayIndex,
                halfDayIndex = halfDayIndex,
                tickInHalfDay = tickInHalfDay,
                timeText = timeText,
                action = defaultAction,
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
                case RoutineActionType.Eat:
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

        private RoutineActionType ResolveCharacterAction(RoutineCharacterBinding binding, int tickInHalfDay)
        {
            int adjustedTick = WrapTick(tickInHalfDay + binding.routineOffsetTicks);
            var scheduledAction = RoutineSchedule.ResolveAction(adjustedTick);
            bool isScheduledMeal = scheduledAction == RoutineActionType.Breakfast
                                   || scheduledAction == RoutineActionType.Lunch
                                   || scheduledAction == RoutineActionType.Dinner;
            bool isScheduledSleep = scheduledAction == RoutineActionType.Sleep;
            bool isHungry = binding.hunger <= binding.hungerThreshold;
            bool isSleepy = binding.sleep <= binding.sleepThreshold;

            if (isSleepy || isScheduledSleep)
            {
                return RoutineActionType.Sleep;
            }

            if (isHungry || isScheduledMeal)
            {
                return isScheduledMeal ? scheduledAction : RoutineActionType.Eat;
            }

            return RoutineActionType.Mission;
        }

        private void ApplyNeedsAndProgress(RoutineCharacterBinding binding, RoutineActionType action)
        {
            switch (action)
            {
                case RoutineActionType.Mission:
                    binding.hunger -= binding.hungerDecayPerTick;
                    binding.sleep -= binding.sleepDecayPerTick;
                    binding.missionTicks += 1;
                    break;
                case RoutineActionType.Sleep:
                    binding.hunger -= binding.hungerDecayPerTick * 0.35f;
                    binding.sleep += binding.sleepRecoverPerSleep;
                    break;
                default:
                    binding.hunger += binding.hungerRecoverPerMeal;
                    binding.sleep -= binding.sleepDecayPerTick * 0.5f;
                    break;
            }

            binding.hunger = Mathf.Clamp(binding.hunger, 0f, GaugeMax);
            binding.sleep = Mathf.Clamp(binding.sleep, 0f, GaugeMax);
        }

        private Vector3 GetCharacterSlotOffset(int index)
        {
            return new Vector3(index * 1.4f - 1.4f, 0f, 0f);
        }

        private int WrapTick(int tick)
        {
            int wrapped = tick % ticksPerHalfDay;
            if (wrapped <= 0)
            {
                wrapped += ticksPerHalfDay;
            }

            return wrapped;
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

            EnsureDefaultCharactersExist();
            EnsureCharacterBindingsFromRoot();
        }

        private void EnsureDefaultCharactersExist()
        {
            if (charactersRoot == null)
            {
                return;
            }

            EnsureCharacterExists("Character_A", new Vector3(-1.2f, 1f, 0f), Color.cyan);
            EnsureCharacterExists("Character_B", new Vector3(0f, 1f, 0f), new Color(0.5f, 1f, 0.5f, 1f));
            EnsureCharacterExists("Character_C", new Vector3(1.2f, 1f, 0f), new Color(1f, 0.7f, 0.4f, 1f));
        }

        private void EnsureCharacterExists(string characterName, Vector3 localPosition, Color tint)
        {
            if (charactersRoot == null || charactersRoot.Find(characterName) != null)
            {
                return;
            }

            var character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            character.name = characterName;
            character.transform.SetParent(charactersRoot, false);
            character.transform.localPosition = localPosition;
            character.transform.localRotation = Quaternion.identity;
            character.transform.localScale = Vector3.one;
            TryColorize(character, tint);
        }

        private void EnsureCharacterBindingsFromRoot()
        {
            if (charactersRoot == null)
            {
                return;
            }

            var existing = new HashSet<Transform>();
            for (int i = 0; i < characters.Count; i++)
            {
                var actor = characters[i].actor;
                if (actor != null)
                {
                    existing.Add(actor);
                }
            }

            for (int i = 0; i < charactersRoot.childCount; i++)
            {
                var child = charactersRoot.GetChild(i);
                if (existing.Contains(child))
                {
                    continue;
                }

                characters.Add(new RoutineCharacterBinding
                {
                    actor = child,
                    moveSpeed = 2.5f,
                    targetPosition = child.position
                });
            }
        }

        private void EnsureRuntimeBindingInitialized(RoutineCharacterBinding binding, int index)
        {
            if (binding.runtimeInitialized)
            {
                return;
            }

            binding.routineOffsetTicks = binding.routineOffsetTicks == 0 ? index * 2 : binding.routineOffsetTicks;
            binding.displayName = string.IsNullOrWhiteSpace(binding.displayName) ? BuildDisplayName(binding.actor) : binding.displayName;
            binding.targetPosition = binding.actor != null ? binding.actor.position : Vector3.zero;
            EnsureStatusVisual(binding);
            binding.runtimeInitialized = true;
        }

        private string BuildDisplayName(Transform actor)
        {
            if (actor == null)
            {
                return "Unknown";
            }

            string name = actor.name.Replace("Character_", string.Empty);
            return string.IsNullOrWhiteSpace(name) ? actor.name : name;
        }

        private void EnsureStatusVisual(RoutineCharacterBinding binding)
        {
            if (binding.actor == null)
            {
                return;
            }

            if (binding.statusUiRoot == null)
            {
                var uiRoot = new GameObject("RoutineStatusUI");
                uiRoot.transform.SetParent(binding.actor, false);
                uiRoot.transform.localPosition = new Vector3(0.95f, 0.9f, 0f);
                binding.statusUiRoot = uiRoot.transform;
                binding.hungerFill = CreateGauge(binding.statusUiRoot, "HungerGauge", new Vector3(0f, 0.22f, 0f), new Color(0.25f, 0.08f, 0.08f, 1f), new Color(1f, 0.35f, 0.2f, 1f));
                binding.sleepFill = CreateGauge(binding.statusUiRoot, "SleepGauge", new Vector3(0f, -0.02f, 0f), new Color(0.08f, 0.08f, 0.2f, 1f), new Color(0.3f, 0.55f, 1f, 1f));
            }

            if (binding.nameLabel == null)
            {
                var nameObject = new GameObject("RoutineNameLabel");
                nameObject.transform.SetParent(binding.actor, false);
                nameObject.transform.localPosition = new Vector3(0f, -1.15f, 0f);
                var textMesh = nameObject.AddComponent<TextMesh>();
                textMesh.text = binding.displayName;
                textMesh.anchor = TextAnchor.UpperCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.characterSize = 0.06f;
                textMesh.fontSize = 64;
                textMesh.color = Color.white;
                binding.nameLabel = textMesh;
            }
        }

        private Transform CreateGauge(Transform parent, string gaugeName, Vector3 localPosition, Color backgroundColor, Color fillColor)
        {
            var root = new GameObject(gaugeName).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;

            var background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Background";
            background.transform.SetParent(root, false);
            background.transform.localScale = new Vector3(GaugeWidth, GaugeHeight, GaugeHeight);
            background.transform.localPosition = Vector3.zero;
            TryColorize(background, backgroundColor);
            TryRemoveCollider(background);

            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Fill";
            fill.transform.SetParent(root, false);
            fill.transform.localScale = new Vector3(GaugeWidth, GaugeHeight * 0.8f, GaugeHeight * 0.8f);
            fill.transform.localPosition = Vector3.zero;
            TryColorize(fill, fillColor);
            TryRemoveCollider(fill);

            return fill.transform;
        }

        private void UpdateStatusVisual(RoutineCharacterBinding binding)
        {
            UpdateGaugeFill(binding.hungerFill, binding.hunger / GaugeMax);
            UpdateGaugeFill(binding.sleepFill, binding.sleep / GaugeMax);
        }

        private void UpdateGaugeFill(Transform fill, float normalizedValue)
        {
            if (fill == null)
            {
                return;
            }

            float clamped = Mathf.Clamp01(normalizedValue);
            float width = Mathf.Max(0.01f, GaugeWidth * clamped);
            fill.localScale = new Vector3(width, GaugeHeight * 0.8f, GaugeHeight * 0.8f);
            fill.localPosition = new Vector3((-GaugeWidth * 0.5f) + (width * 0.5f), 0f, 0f);
        }

        private void TryColorize(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var props = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(props);
                props.SetColor("_Color", color);
                renderer.SetPropertyBlock(props);
            }
        }

        private void TryRemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
        }
    }
}
