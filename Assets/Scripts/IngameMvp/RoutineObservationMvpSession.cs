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
        [NonSerialized] public bool hasLatchedNeedAction;
        [NonSerialized] public RoutineActionType latchedNeedAction;
        [NonSerialized] public LineRenderer targetLineRenderer;
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
        private const float HumanHungerDaysToZero = 1f;
        private const float HumanFatigueDaysToZero = 3f;
        private const float DefaultMoveSpeed = 5f;
        private const float CharacterSpawnHeight = 1.6f;
        private const float GaugeBarMaxWidth = 1.2f;
        private const float GaugeBarMinWidth = 0.06f;
        private const float TargetLineY = 0.35f;
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

        [Header("Generation")]
        [SerializeField] private bool persistGeneratedObjectsInScene = true;
        [SerializeField] private bool useCubeDummyVisual = true;

        [Header("Debug View")]
        [SerializeField] private bool showDebugOnGui = true;
        [SerializeField] private Vector2 debugPanelPosition = new Vector2(16f, 16f);
        [SerializeField] private Vector2 debugPanelSize = new Vector2(430f, 240f);
        [SerializeField] private bool enableDecisionLog = true;

        private int _absoluteTick;
        private Coroutine _loopCoroutine;
        private readonly List<RoutineZoneAnchor> _zoneAnchors = new List<RoutineZoneAnchor>();
        private GUIStyle _debugLabelStyle;

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
                UpdateTargetLineVisual(binding, i);
            }
        }

        private void OnGUI()
        {
            if (!showDebugOnGui)
            {
                return;
            }

            EnsureDebugGuiStyle();

            GUILayout.BeginArea(new Rect(debugPanelPosition.x, debugPanelPosition.y, debugPanelSize.x, debugPanelSize.y), GUI.skin.box);
            GUILayout.Label($"Routine Debug | Tick {_absoluteTick}", _debugLabelStyle);
            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null)
                {
                    continue;
                }

                var label = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} | Cur:{1} Int:{2} | H:{3:0} S:{4:0} T:{5:0}",
                    string.IsNullOrWhiteSpace(binding.displayName) ? binding.actor.name : binding.displayName,
                    binding.currentAction,
                    binding.intendedAction,
                    binding.hunger,
                    binding.sleep,
                    binding.stress);
                GUILayout.Label(label, _debugLabelStyle);
            }

            GUILayout.EndArea();
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

        [ContextMenu("ProjectW/Bake Generated Objects To Scene")]
        public void BakeGeneratedObjectsToScene()
        {
            AutoBindSceneReferences();
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].actor == null)
                {
                    continue;
                }

                EnsureRuntimeBindingInitialized(characters[i], i);
                UpdateRuntimeStateTexts(characters[i]);
            }
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

                var desiredAction = ResolveCharacterAction(
                    binding,
                    tickInHalfDay,
                    out var decisionReason,
                    out var scheduledAction,
                    out var isScheduledMeal,
                    out var isScheduledSleep,
                    out var isHungry,
                    out var isStressed);

                var preLatchAction = desiredAction;
                desiredAction = ResolveLatchedOrNewNeedAction(binding, desiredAction);
                binding.intendedAction = desiredAction;
                RoutineZoneAnchor zone = ResolveZone(desiredAction, binding.actor.position);
                var actionTarget = ResolveActionTargetPosition(zone, i, desiredAction, binding.actor.position);
                binding.targetPosition = actionTarget;
                UpdateTargetLineVisual(binding, i);

                if (!HasArrived(binding.actor.position, actionTarget))
                {
                    binding.currentAction = RoutineActionType.Move;
                    ApplyNeedsAndProgress(binding, RoutineActionType.Move, false);
                    UpdateRuntimeStateTexts(binding);
                    LogDecision(
                        binding,
                        tickInHalfDay,
                        scheduledAction,
                        preLatchAction,
                        desiredAction,
                        decisionReason,
                        isScheduledMeal,
                        isScheduledSleep,
                        isHungry,
                        isStressed,
                        zone,
                        actionTarget,
                        true,
                        false,
                        false);
                    continue;
                }

                binding.currentAction = desiredAction;
                var canResolveNeed = CanResolveNeed(binding, desiredAction, zone);
                ApplyNeedsAndProgress(binding, desiredAction, canResolveNeed);
                var hadLatchBeforeResolve = binding.hasLatchedNeedAction;
                ResolveNeedLatchAfterAction(binding, desiredAction, canResolveNeed);
                var latchReleased = hadLatchBeforeResolve && !binding.hasLatchedNeedAction;
                UpdateRuntimeStateTexts(binding);
                LogDecision(
                    binding,
                    tickInHalfDay,
                    scheduledAction,
                    preLatchAction,
                    desiredAction,
                    decisionReason,
                    isScheduledMeal,
                    isScheduledSleep,
                    isHungry,
                    isStressed,
                    zone,
                    actionTarget,
                    false,
                    canResolveNeed,
                    latchReleased);
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

        private RoutineActionType ResolveCharacterAction(
            RoutineCharacterBinding binding,
            int tickInHalfDay,
            out string decisionReason,
            out RoutineActionType scheduledAction,
            out bool isScheduledMeal,
            out bool isScheduledSleep,
            out bool isHungry,
            out bool isStressed)
        {
            int adjustedTick = WrapTick(tickInHalfDay + binding.routineOffsetTicks);
            scheduledAction = RoutineSchedule.ResolveAction(adjustedTick);
            isScheduledMeal = scheduledAction == RoutineActionType.Breakfast
                              || scheduledAction == RoutineActionType.Lunch
                              || scheduledAction == RoutineActionType.Dinner;
            isScheduledSleep = scheduledAction == RoutineActionType.Sleep;
            isHungry = binding.hunger <= binding.hungerThreshold;
            isStressed = binding.stress <= binding.stressThreshold;

            if (isScheduledSleep && isHungry && isStressed)
            {
                decisionReason = "scheduled_sleep && hungry && stressed";
                return RoutineActionType.Sleep;
            }

            if (isScheduledMeal && isHungry && isStressed)
            {
                decisionReason = "scheduled_meal && hungry && stressed";
                return isScheduledMeal ? scheduledAction : RoutineActionType.Eat;
            }

            decisionReason = "fallback_mission (time/need condition not satisfied)";
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

        private Vector3 ResolveActionTargetPosition(RoutineZoneAnchor zone, int actorIndex, RoutineActionType action, Vector3 fallback)
        {
            if (zone == null)
            {
                return fallback;
            }

            var slot = zone.Position + GetZoneActionOffset(actorIndex);
            if (IsNeedAction(action) && !zone.Contains(slot))
            {
                // Need action must complete at least once; if slot is outside boundary, fallback to zone center.
                return zone.Position;
            }

            return slot;
        }

        private RoutineActionType ResolveLatchedOrNewNeedAction(RoutineCharacterBinding binding, RoutineActionType desiredAction)
        {
            if (desiredAction == RoutineActionType.Sleep)
            {
                // Sleep always has priority over meal-type need actions.
                binding.hasLatchedNeedAction = true;
                binding.latchedNeedAction = RoutineActionType.Sleep;
                return RoutineActionType.Sleep;
            }

            if (binding.hasLatchedNeedAction && IsNeedAction(binding.latchedNeedAction))
            {
                return binding.latchedNeedAction;
            }

            if (IsNeedAction(desiredAction))
            {
                binding.hasLatchedNeedAction = true;
                binding.latchedNeedAction = desiredAction;
                return desiredAction;
            }

            return desiredAction;
        }

        private void ResolveNeedLatchAfterAction(RoutineCharacterBinding binding, RoutineActionType appliedAction, bool canResolveNeed)
        {
            if (!binding.hasLatchedNeedAction)
            {
                return;
            }

            if (!IsNeedAction(binding.latchedNeedAction))
            {
                binding.hasLatchedNeedAction = false;
                return;
            }

            if (binding.latchedNeedAction == appliedAction && canResolveNeed)
            {
                if (IsMealAction(binding.latchedNeedAction))
                {
                    // Keep eating until hunger is resolved.
                    if (binding.hunger > binding.hungerThreshold)
                    {
                        binding.hasLatchedNeedAction = false;
                    }
                }
                else
                {
                    // Sleep and other need actions: execute at least once after arrival.
                    binding.hasLatchedNeedAction = false;
                }
            }
        }

        private static bool IsNeedAction(RoutineActionType action)
        {
            return action == RoutineActionType.Eat
                   || action == RoutineActionType.Breakfast
                   || action == RoutineActionType.Lunch
                   || action == RoutineActionType.Dinner
                   || action == RoutineActionType.Sleep;
        }

        private static bool IsMealAction(RoutineActionType action)
        {
            return action == RoutineActionType.Eat
                   || action == RoutineActionType.Breakfast
                   || action == RoutineActionType.Lunch
                   || action == RoutineActionType.Dinner;
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

            EnsureCharacterExists("Character_A", new Vector3(-1.2f, CharacterSpawnHeight, 0f), new Color(0.2f, 0.85f, 1f, 1f));
            EnsureCharacterExists("Character_B", new Vector3(0f, CharacterSpawnHeight, 0f), new Color(0.4f, 1f, 0.4f, 1f));
            EnsureCharacterExists("Character_C", new Vector3(1.2f, CharacterSpawnHeight, 0f), new Color(1f, 0.7f, 0.25f, 1f));
        }

        private void EnsureCharacterExists(string characterName, Vector3 localPosition, Color tint)
        {
            if (charactersRoot == null || charactersRoot.Find(characterName) != null)
            {
                return;
            }

            var character = CreateDummyCharacterObject("Character");
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
                    moveSpeed = DefaultMoveSpeed,
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
            ApplyHumanPhysiologyPreset(binding);
            binding.targetPosition = binding.actor != null ? binding.actor.position : Vector3.zero;
            binding.intendedAction = RoutineActionType.Mission;
            UpdateRuntimeStateTexts(binding);
            EnsureTargetLineRenderer(binding, index);
            binding.runtimeInitialized = true;
        }

        private void ApplyHumanPhysiologyPreset(RoutineCharacterBinding binding)
        {
            var ticksPerDay = Mathf.Max(1f, ticksPerHalfDay * 2f);
            binding.hungerDecayPerTick = GaugeMax / (ticksPerDay * HumanHungerDaysToZero);
            binding.sleepDecayPerTick = GaugeMax / (ticksPerDay * HumanFatigueDaysToZero);
            binding.stressDecayPerTick = GaugeMax / (ticksPerDay * HumanFatigueDaysToZero);
        }

        private void EnsureTargetLineRenderer(RoutineCharacterBinding binding, int index)
        {
            if (binding.actor == null || binding.targetLineRenderer != null)
            {
                return;
            }

            var lineTransform = binding.actor.Find("TargetPathLine");
            if (lineTransform == null)
            {
                var go = new GameObject("TargetPathLine");
                go.transform.SetParent(binding.actor, false);
                lineTransform = go.transform;
            }

            var line = lineTransform.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = lineTransform.gameObject.AddComponent<LineRenderer>();
            }

            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 2;
            line.startWidth = 0.06f;
            line.endWidth = 0.03f;
            line.numCapVertices = 2;
            line.material = new Material(Shader.Find("Sprites/Default"));
            var lineColor = GetLineColor(index);
            line.startColor = lineColor;
            line.endColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.35f);
            binding.targetLineRenderer = line;
        }

        private static Color GetLineColor(int index)
        {
            switch (index % 3)
            {
                case 0: return new Color(0.98f, 0.85f, 0.2f, 0.95f);
                case 1: return new Color(1f, 0.55f, 0.12f, 0.95f);
                default: return new Color(0.62f, 0.36f, 0.95f, 0.95f);
            }
        }

        private void UpdateTargetLineVisual(RoutineCharacterBinding binding, int index)
        {
            if (binding.actor == null)
            {
                return;
            }

            EnsureTargetLineRenderer(binding, index);
            var line = binding.targetLineRenderer;
            if (line == null)
            {
                return;
            }

            var start = binding.actor.position + new Vector3(0f, TargetLineY, 0f);
            var end = binding.targetPosition + new Vector3(0f, TargetLineY, 0f);
            line.enabled = !HasArrived(binding.actor.position, binding.targetPosition);
            if (!line.enabled)
            {
                return;
            }

            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private static string BuildDisplayName(Transform actor)
        {
            if (actor == null)
            {
                return "Unknown";
            }

            string name = actor.name.Replace("Character_", string.Empty);
            return string.IsNullOrWhiteSpace(name) ? actor.name : name;
        }

        private void UpdateRuntimeStateTexts(RoutineCharacterBinding binding)
        {
            binding.hunger = Mathf.Clamp(binding.hunger, 0f, GaugeMax);
            binding.sleep = Mathf.Clamp(binding.sleep, 0f, GaugeMax);
            binding.stress = Mathf.Clamp(binding.stress, 0f, GaugeMax);
            UpdateSceneGaugeForBinding(binding);
        }

        private void UpdateSceneGaugeForBinding(RoutineCharacterBinding binding)
        {
            if (binding.actor == null)
            {
                return;
            }

            var gaugeRoot = binding.actor.Find("GaugeRoot");
            if (gaugeRoot == null)
            {
                return;
            }

            UpdateGaugeBar(gaugeRoot, "HungerBar", binding.hunger / GaugeMax, new Color(0.94f, 0.35f, 0.35f, 1f));
            UpdateGaugeBar(gaugeRoot, "SleepBar", binding.sleep / GaugeMax, new Color(0.30f, 0.55f, 0.95f, 1f));
            UpdateGaugeBar(gaugeRoot, "StressBar", binding.stress / GaugeMax, new Color(0.30f, 0.80f, 0.40f, 1f));
        }

        private static void UpdateGaugeBar(Transform gaugeRoot, string barName, float normalized, Color color)
        {
            var bar = gaugeRoot.Find(barName);
            if (bar == null)
            {
                return;
            }

            var clamped = Mathf.Clamp01(normalized);
            var width = Mathf.Lerp(GaugeBarMinWidth, GaugeBarMaxWidth, clamped);
            var scale = bar.localScale;
            scale.x = width;
            bar.localScale = scale;

            var localPosition = bar.localPosition;
            localPosition.x = -0.5f * (GaugeBarMaxWidth - width);
            bar.localPosition = localPosition;

            var renderer = bar.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        private void TryColorize(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var current = renderer.sharedMaterial;
            if (current != null)
            {
                var coloredInstance = new Material(current);
                if (coloredInstance.HasProperty("_Color"))
                {
                    coloredInstance.SetColor("_Color", color);
                }

                if (coloredInstance.HasProperty("_BaseColor"))
                {
                    coloredInstance.SetColor("_BaseColor", color);
                }

                renderer.sharedMaterial = coloredInstance;
            }

            var props = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", color);
            props.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(props);
        }

        private void TryRemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (persistGeneratedObjectsInScene)
            {
                collider.enabled = false;
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        private GameObject CreateDummyCharacterObject(string name)
        {
            // Extension point: replace primitive path with Spine/sprite prefab injection later.
            var primitiveType = useCubeDummyVisual ? PrimitiveType.Cube : PrimitiveType.Capsule;
            var go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            TryRemoveCollider(go);
            return go;
        }

        private void EnsureDebugGuiStyle()
        {
            if (_debugLabelStyle != null)
            {
                return;
            }

            _debugLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.95f, 0.98f, 1f, 1f) }
            };
        }

        private void LogDecision(
            RoutineCharacterBinding binding,
            int tickInHalfDay,
            RoutineActionType scheduledAction,
            RoutineActionType preLatchAction,
            RoutineActionType postLatchAction,
            string decisionReason,
            bool isScheduledMeal,
            bool isScheduledSleep,
            bool isHungry,
            bool isStressed,
            RoutineZoneAnchor zone,
            Vector3 target,
            bool movedThisTick,
            bool canResolveNeed,
            bool latchReleased)
        {
            if (!enableDecisionLog)
            {
                return;
            }

            var actorName = binding.actor != null ? binding.actor.name : "Unknown";
            var zoneId = zone != null ? zone.ZoneId : "null";
            var msg = string.Format(
                CultureInfo.InvariantCulture,
                "[RoutineDecision] tick={0} actor={1} scheduled={2} cond(meal={3},sleep={4},hungry={5},stressed={6}) reason={7} action(preLatch={8},postLatch={9},current={10}) latch(active={11},released={12}) zone={13} target=({14:0.##},{15:0.##},{16:0.##}) moved={17} canResolveNeed={18}",
                tickInHalfDay,
                actorName,
                scheduledAction,
                isScheduledMeal,
                isScheduledSleep,
                isHungry,
                isStressed,
                decisionReason,
                preLatchAction,
                postLatchAction,
                binding.currentAction,
                binding.hasLatchedNeedAction,
                latchReleased,
                zoneId,
                target.x,
                target.y,
                target.z,
                movedThisTick,
                canResolveNeed);

            Debug.Log(msg);
        }
    }
}
