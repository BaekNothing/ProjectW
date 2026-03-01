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
        Move,
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
        public float stressDecayPerTick = 5f;
        public float stressRecoverPerMeal = 12f;
        public float stressRecoverPerSleep = 24f;
        public float stressThreshold = 35f;
        public string displayName;

        [NonSerialized] public Vector3 targetPosition;
        [NonSerialized] public int missionTicks;
        [NonSerialized] public float hunger = 100f;
        [NonSerialized] public float sleep = 100f;
        [NonSerialized] public float stress = 100f;
        [NonSerialized] public RoutineActionType currentAction;
        [NonSerialized] public RoutineActionType intendedAction;
        [NonSerialized] public bool runtimeInitialized;
        [NonSerialized] public Transform statusUiRoot;
        [NonSerialized] public Transform hungerFill;
        [NonSerialized] public Transform sleepFill;
        [NonSerialized] public Transform stressFill;
        [NonSerialized] public TextMesh nameLabel;
        [NonSerialized] public TextMesh intentLabel;
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
        private const float ZoneActionSpacing = 0.9f;
        private const string ZoneTagMission = "zone.mission";
        private const string ZoneTagNeedHunger = "need.hunger";
        private const string ZoneTagNeedSleep = "need.sleep";

        [Header("Tick")]
        [SerializeField] private bool autoRunOnStart = true;
        [SerializeField] private float tickIntervalSeconds = 0.6f;
        [SerializeField] private int ticksPerHalfDay = 30;

        [Header("Zone Anchors (Scene Fixed Objects)")]
        [SerializeField] private Transform zonesRoot;
        [SerializeField] private RoutineZoneAnchor missionZone;
        [SerializeField] private RoutineZoneAnchor cafeteriaZone;
        [SerializeField] private RoutineZoneAnchor sleepZone;
        [SerializeField] private Transform charactersRoot;

        [Header("UI")]
        [SerializeField] private Text currentTimeText;

        [Header("Characters")]
        [SerializeField] private List<RoutineCharacterBinding> characters = new List<RoutineCharacterBinding>();

        private int _absoluteTick;
        private Coroutine _loopCoroutine;
        private readonly List<RoutineZoneAnchor> _zoneAnchors = new List<RoutineZoneAnchor>();

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
            RoutineZoneAnchor defaultZone = ResolveZone(defaultAction, Vector3.zero);
            string zoneName = defaultZone != null ? defaultZone.ZoneId : "MissingZone";

            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null) continue;
                EnsureRuntimeBindingInitialized(binding, i);

                var desiredAction = ResolveCharacterAction(binding, tickInHalfDay);
                binding.intendedAction = desiredAction;
                RoutineZoneAnchor zone = ResolveZone(desiredAction, binding.actor.position);
                var actionTarget = zone != null ? zone.Position + GetZoneActionOffset(i) : binding.actor.position;
                binding.targetPosition = actionTarget;

                if (!HasArrived(binding.actor.position, actionTarget))
                {
                    binding.currentAction = RoutineActionType.Move;
                    ApplyNeedsAndProgress(binding, RoutineActionType.Move, false);
                    UpdateStatusVisual(binding);
                    UpdateIntentLabel(binding);
                    continue;
                }

                binding.currentAction = desiredAction;
                var canResolveNeed = CanResolveNeed(binding, desiredAction, zone);
                ApplyNeedsAndProgress(binding, desiredAction, canResolveNeed);
                UpdateStatusVisual(binding);
                UpdateIntentLabel(binding);
            }

            string timeText = BuildTimeText(dayIndex, halfDayIndex, tickInHalfDay);
            if (currentTimeText != null)
            {
                currentTimeText.text = timeText;
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

        private RoutineZoneAnchor ResolveZone(RoutineActionType action, Vector3 actorPosition)
        {
            switch (action)
            {
                case RoutineActionType.Eat:
                case RoutineActionType.Breakfast:
                case RoutineActionType.Lunch:
                case RoutineActionType.Dinner:
                    return FindZoneByTag(ZoneTagNeedHunger, actorPosition);
                case RoutineActionType.Sleep:
                    return FindZoneByTag(ZoneTagNeedSleep, actorPosition);
                default:
                    return FindZoneByTag(ZoneTagMission, actorPosition);
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
            bool isStressed = binding.stress <= binding.stressThreshold;

            if (isScheduledSleep && isHungry && isStressed)
            {
                return RoutineActionType.Sleep;
            }

            if (isScheduledMeal && isHungry && isStressed)
            {
                return isScheduledMeal ? scheduledAction : RoutineActionType.Eat;
            }

            return RoutineActionType.Mission;
        }

        private void ApplyNeedsAndProgress(RoutineCharacterBinding binding, RoutineActionType action, bool canResolveNeed)
        {
            switch (action)
            {
                case RoutineActionType.Move:
                    binding.hunger -= binding.hungerDecayPerTick * 0.5f;
                    binding.sleep -= binding.sleepDecayPerTick * 0.35f;
                    binding.stress -= binding.stressDecayPerTick * 0.3f;
                    break;
                case RoutineActionType.Mission:
                    binding.hunger -= binding.hungerDecayPerTick;
                    binding.sleep -= binding.sleepDecayPerTick;
                    binding.stress -= binding.stressDecayPerTick;
                    binding.missionTicks += 1;
                    break;
                case RoutineActionType.Sleep:
                    binding.hunger -= binding.hungerDecayPerTick * 0.35f;
                    if (canResolveNeed)
                    {
                        binding.sleep += binding.sleepRecoverPerSleep;
                        binding.stress += binding.stressRecoverPerSleep;
                    }
                    break;
                default:
                    if (canResolveNeed)
                    {
                        binding.hunger += binding.hungerRecoverPerMeal;
                        binding.stress += binding.stressRecoverPerMeal;
                    }
                    binding.sleep -= binding.sleepDecayPerTick * 0.5f;
                    break;
            }

            binding.hunger = Mathf.Clamp(binding.hunger, 0f, GaugeMax);
            binding.sleep = Mathf.Clamp(binding.sleep, 0f, GaugeMax);
            binding.stress = Mathf.Clamp(binding.stress, 0f, GaugeMax);
        }

        private static bool HasArrived(Vector3 currentPosition, Vector3 targetPosition)
        {
            return (targetPosition - currentPosition).sqrMagnitude <= 0.0001f;
        }

        private Vector3 GetZoneActionOffset(int index)
        {
            var row = index / 2;
            var col = index % 2;
            var x = (col == 0 ? -1f : 1f) * ZoneActionSpacing;
            var y = row * ZoneActionSpacing * 0.8f;
            return new Vector3(x, y, 0f);
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
            if (zonesRoot == null)
            {
                var zones = GameObject.Find("Zones");
                zonesRoot = zones != null ? zones.transform : null;
            }

            BindZonesFromAnchors();

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

        private void BindZonesFromAnchors()
        {
            _zoneAnchors.Clear();

            if (zonesRoot == null)
            {
                return;
            }

            var anchors = zonesRoot.GetComponentsInChildren<RoutineZoneAnchor>(true);
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                {
                    continue;
                }

                _zoneAnchors.Add(anchor);
            }

            missionZone = FindZoneByTag(ZoneTagMission, Vector3.zero);
            cafeteriaZone = FindZoneByTag(ZoneTagNeedHunger, Vector3.zero);
            sleepZone = FindZoneByTag(ZoneTagNeedSleep, Vector3.zero);
        }

        private RoutineZoneAnchor FindZoneByTag(string zoneTag, Vector3 referencePosition)
        {
            RoutineZoneAnchor selected = null;
            var bestDistance = float.MaxValue;

            for (int i = 0; i < _zoneAnchors.Count; i++)
            {
                var zone = _zoneAnchors[i];
                if (zone == null || !zone.HasTag(zoneTag))
                {
                    continue;
                }

                var distance = (zone.Position - referencePosition).sqrMagnitude;
                if (selected == null || distance < bestDistance)
                {
                    selected = zone;
                    bestDistance = distance;
                    continue;
                }

                if (Mathf.Approximately(distance, bestDistance) &&
                    string.CompareOrdinal(zone.ZoneId, selected.ZoneId) < 0)
                {
                    selected = zone;
                }
            }

            return selected;
        }

        private bool CanResolveNeed(RoutineCharacterBinding binding, RoutineActionType action, RoutineZoneAnchor zone)
        {
            if (binding.actor == null || zone == null)
            {
                return false;
            }

            if (action == RoutineActionType.Sleep)
            {
                return zone.HasTag(ZoneTagNeedSleep) && zone.Contains(binding.actor.position);
            }

            if (action == RoutineActionType.Eat ||
                action == RoutineActionType.Breakfast ||
                action == RoutineActionType.Lunch ||
                action == RoutineActionType.Dinner)
            {
                return zone.HasTag(ZoneTagNeedHunger) && zone.Contains(binding.actor.position);
            }

            return true;
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

            binding.displayName = string.IsNullOrWhiteSpace(binding.displayName) ? BuildDisplayName(binding.actor) : binding.displayName;
            binding.targetPosition = binding.actor != null ? binding.actor.position : Vector3.zero;
            binding.intendedAction = RoutineActionType.Mission;
            EnsureStatusVisual(binding);
            UpdateIntentLabel(binding);
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
                binding.hungerFill = CreateGauge(binding.statusUiRoot, "HungerGauge", new Vector3(0f, 0.26f, 0f), new Color(0.15f, 0.08f, 0.08f, 1f), new Color(1f, 0.42f, 0.2f, 1f));
                binding.sleepFill = CreateGauge(binding.statusUiRoot, "SleepGauge", new Vector3(0f, 0.02f, 0f), new Color(0.07f, 0.09f, 0.18f, 1f), new Color(0.25f, 0.7f, 1f, 1f));
                binding.stressFill = CreateGauge(binding.statusUiRoot, "StressGauge", new Vector3(0f, -0.22f, 0f), new Color(0.08f, 0.15f, 0.09f, 1f), new Color(0.35f, 1f, 0.45f, 1f));
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

            if (binding.intentLabel == null)
            {
                var intentObject = new GameObject("RoutineIntentLabel");
                intentObject.transform.SetParent(binding.actor, false);
                intentObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                var textMesh = intentObject.AddComponent<TextMesh>();
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.characterSize = 0.045f;
                textMesh.fontSize = 48;
                textMesh.color = new Color(1f, 0.95f, 0.65f, 1f);
                binding.intentLabel = textMesh;
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
            UpdateGaugeFill(binding.stressFill, binding.stress / GaugeMax);
        }

        private void UpdateIntentLabel(RoutineCharacterBinding binding)
        {
            if (binding.intentLabel == null)
            {
                return;
            }

            binding.intentLabel.text = "Intent: " + binding.intendedAction;
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
                props.SetColor("_BaseColor", color);
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
