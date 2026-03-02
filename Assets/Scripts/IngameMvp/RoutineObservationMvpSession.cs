using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
        [NonSerialized] public int remainingMoveTicks;
        [NonSerialized] public int remainingActionTicks;
        [NonSerialized] public int completedWorkCount;
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
        private const float DefaultCharacterSpriteSize = 0.1f;
        private const float DefaultZoneAlpha = 0.4f;
        private const string ZoneTagMission = "zone.mission";
        private const string ZoneTagNeedHunger = "need.hunger";
        private const string ZoneTagNeedSleep = "need.sleep";

        [Header("Tick")]
        [SerializeField] private bool autoRunOnStart = true;
        [SerializeField] private float tickIntervalSeconds = 1f;
        [SerializeField] private int ticksPerHalfDay = 48;

        [Header("Zone Anchors (Scene Fixed Objects)")]
        [SerializeField] private Transform zonesRoot;
        [SerializeField] private RoutineZoneAnchor missionZone;
        [SerializeField] private RoutineZoneAnchor cafeteriaZone;
        [SerializeField] private RoutineZoneAnchor sleepZone;
        [SerializeField] private Transform charactersRoot;

        [Header("UI")]
        [SerializeField] private Text currentTimeText;
        [SerializeField] private Text goalText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text situationText;

        [Header("Dashboard")]
        [SerializeField] private bool autoCreateDashboardUi = false;
        [SerializeField] private string dashboardGoalTitle = "Routine Stability";
        [SerializeField] private int dashboardMissionGoalTicks = 100;

        [Header("Characters")]
        [SerializeField] private List<RoutineCharacterBinding> characters = new List<RoutineCharacterBinding>();

        [Header("Generation")]
        [SerializeField] private bool persistGeneratedObjectsInScene = true;
        [SerializeField] private bool useCubeDummyVisual = true;

        [Header("2D World")]
        [SerializeField] private bool use2DWorld = true;
        [SerializeField] private bool disableLegacy3DRenderers = true;
        [SerializeField] private float zoneGap = 2f;
        [SerializeField] private Vector2 zoneScale = new Vector2(0.5f, 0.4f);

        [Header("Depth Layout")]
        [SerializeField] private bool enforceDepthLayout = true;
        [SerializeField] private float zoneDepthZ = 0f;
        [SerializeField] private float characterDepthZ = -1f;

        [Header("Debug View")]
        [SerializeField] private bool showDebugOnGui = true;
        [SerializeField] private Vector2 debugPanelPosition = new Vector2(16f, 16f);
        [SerializeField] private Vector2 debugPanelSize = new Vector2(430f, 240f);
        [SerializeField] private bool enableDecisionLog = true;

        private int _absoluteTick;
        private Coroutine _loopCoroutine;
        private readonly List<RoutineZoneAnchor> _zoneAnchors = new List<RoutineZoneAnchor>();
        private readonly Dictionary<string, string> _dashboardContext = new Dictionary<string, string>();
        private GUIStyle _debugLabelStyle;
        private int _lastAppliedInterventionTick = -1;
        private int _pendingInterventionCount;
        private string _recentRejectedInterventionReason = "None";
        private static Sprite _runtimeSquareSprite;

        public IReadOnlyList<RoutineCharacterBinding> Characters => characters;
        public int AbsoluteTick => _absoluteTick;

        public void SetInterventionVisibility(int pendingCount, int lastAppliedTick, string recentRejectedReason)
        {
            _pendingInterventionCount = Mathf.Max(0, pendingCount);
            _lastAppliedInterventionTick = Mathf.Max(-1, lastAppliedTick);
            _recentRejectedInterventionReason = string.IsNullOrWhiteSpace(recentRejectedReason)
                ? "None"
                : recentRejectedReason.Trim();
        }

        public void SetDashboardContext(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _dashboardContext[key.Trim()] = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        public void ClearDashboardContext()
        {
            _dashboardContext.Clear();
        }

        private void Awake()
        {
            AutoBindSceneReferences();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            AutoBindSceneReferences();
            SyncLayoutConfigFromSceneObjects();
            ApplyDepthLayout();
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
                if (enforceDepthLayout)
                {
                    ForceActorDepth(binding.actor);
                    var target = binding.targetPosition;
                    target.z = characterDepthZ;
                    binding.targetPosition = target;
                }
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

        [ContextMenu("ProjectW/Apply Depth Layout")]
        public void ApplyDepthLayoutInEditor()
        {
            AutoBindSceneReferences();
            ApplyDepthLayout();
        }

        [ContextMenu("ProjectW/Rebuild MVP Scene (2D Only)")]
        public void RebuildMvpScene2D()
        {
            SyncLayoutConfigFromSceneObjects();
            RebuildMvpScene2DInternal();
            AutoBindSceneReferences();
            ApplyDepthLayout();
        }

        [ContextMenu("ProjectW/Sync 2D Layout Config From Scene")]
        public void SyncLayoutConfigFromScene()
        {
            AutoBindSceneReferences();
            SyncLayoutConfigFromSceneObjects();
        }

        public RoutineTickSnapshot AdvanceOneTick()
        {
            if (missionZone == null || cafeteriaZone == null || sleepZone == null || characters.Count == 0)
            {
                AutoBindSceneReferences();
            }

            _absoluteTick += 1;

            int ticksPerDay = ticksPerHalfDay * 2;
            int dayIndex = ((_absoluteTick - 1) / ticksPerDay) + 1;
            int halfDayIndex = (((_absoluteTick - 1) / ticksPerHalfDay) % 2) + 1;
            int tickInHalfDay = ((_absoluteTick - 1) % ticksPerHalfDay) + 1;
            int tickInDay = ((_absoluteTick - 1) % ticksPerDay) + 1;
            int totalMinutes = (tickInDay - 1) * 15;
            int hour = (totalMinutes / 60) % 24;
            int minute = totalMinutes % 60;
            var defaultAction = ResolveScheduleAction(hour, minute);
            RoutineZoneAnchor defaultZone = ResolveZone(defaultAction, Vector3.zero);
            string zoneName = defaultZone != null ? defaultZone.ZoneId : "MissingZone";

            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null) continue;
                EnsureRuntimeBindingInitialized(binding, i);
                if (enforceDepthLayout)
                {
                    ForceActorDepth(binding.actor);
                }

                var desiredAction = ResolveCharacterAction(
                    binding,
                    tickInDay,
                    hour,
                    minute,
                    out var decisionReason,
                    out var scheduledAction,
                    out var isScheduledMeal,
                    out var isScheduledSleep,
                    out var isHungry,
                    out var isStressed);

                if (binding.remainingActionTicks > 0 && IsNeedAction(binding.currentAction))
                {
                    desiredAction = binding.currentAction;
                }

                var preLatchAction = desiredAction;
                desiredAction = ResolveLatchedOrNewNeedAction(binding, desiredAction);
                binding.intendedAction = desiredAction;
                RoutineZoneAnchor zone = ResolveZone(desiredAction, binding.actor.position);
                var actionTarget = ResolveActionTargetPosition(zone, i, desiredAction, binding.actor.position);
                binding.targetPosition = actionTarget;
                UpdateTargetLineVisual(binding, i);

                var movedThisTick = ResolveMovementTick(binding, desiredAction, actionTarget);
                if (movedThisTick)
                {
                    ApplyNeedsAndProgress(binding, RoutineActionType.Move, false);
                    UpdateRuntimeStateTexts(binding);
                    LogDecision(
                        binding,
                        tickInDay,
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

                if (binding.remainingActionTicks <= 0)
                {
                    binding.remainingActionTicks = ResolveActionDurationTicks(desiredAction);
                }

                binding.currentAction = desiredAction;
                var canResolveNeed = CanResolveNeed(binding, desiredAction, zone);
                ApplyNeedsAndProgress(binding, desiredAction, canResolveNeed);
                var hadLatchBeforeResolve = binding.hasLatchedNeedAction;
                ResolveNeedLatchAfterAction(binding, desiredAction, canResolveNeed);
                if (binding.remainingActionTicks > 0)
                {
                    binding.remainingActionTicks -= 1;
                }
                var latchReleased = hadLatchBeforeResolve && !binding.hasLatchedNeedAction;
                UpdateRuntimeStateTexts(binding);
                LogDecision(
                    binding,
                    tickInDay,
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

            UpdateDashboardUi(dayIndex, halfDayIndex, tickInHalfDay, timeText);
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[RoutineMVP] {0} | Goal:{1:0}% ({2}/{3})",
                timeText,
                GetMissionProgressRatio() * 100f,
                GetTotalMissionTicks(),
                Mathf.Max(1, dashboardMissionGoalTicks)));

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
            int tickInDay,
            int hour,
            int minute,
            out string decisionReason,
            out RoutineActionType scheduledAction,
            out bool isScheduledMeal,
            out bool isScheduledSleep,
            out bool isHungry,
            out bool isStressed)
        {
            int ticksPerDay = ticksPerHalfDay * 2;
            int adjustedTickInDay = ((tickInDay + binding.routineOffsetTicks - 1 + ticksPerDay) % ticksPerDay) + 1;
            int adjustedMinutes = (adjustedTickInDay - 1) * 15;
            int adjustedHour = (adjustedMinutes / 60) % 24;
            int adjustedMinute = adjustedMinutes % 60;
            scheduledAction = ResolveScheduleAction(adjustedHour, adjustedMinute);
            isScheduledMeal = scheduledAction == RoutineActionType.Breakfast
                              || scheduledAction == RoutineActionType.Lunch
                              || scheduledAction == RoutineActionType.Dinner;
            isScheduledSleep = scheduledAction == RoutineActionType.Sleep;
            isHungry = binding.hunger <= binding.hungerThreshold;
            isStressed = binding.stress <= binding.stressThreshold;

            if (isScheduledSleep)
            {
                decisionReason = "scheduled_sleep_window";
                return RoutineActionType.Sleep;
            }

            if (isScheduledMeal)
            {
                decisionReason = "scheduled_meal_window";
                return isScheduledMeal ? scheduledAction : RoutineActionType.Eat;
            }

            if (hour >= 20)
            {
                decisionReason = "after_work_sleep";
                return RoutineActionType.Sleep;
            }

            decisionReason = "work_time_mission";
            return RoutineActionType.Mission;
        }

        private static RoutineActionType ResolveScheduleAction(int hour, int minute)
        {
            if (hour < 8 || hour >= 22)
            {
                return RoutineActionType.Sleep;
            }

            if (minute == 0)
            {
                if (hour == 8) return RoutineActionType.Breakfast;
                if (hour == 12) return RoutineActionType.Lunch;
                if (hour == 18) return RoutineActionType.Dinner;
            }

            return RoutineActionType.Mission;
        }

        private bool ResolveMovementTick(RoutineCharacterBinding binding, RoutineActionType desiredAction, Vector3 actionTarget)
        {
            bool needsMove = !HasArrived(binding.actor.position, actionTarget);
            if (needsMove && binding.remainingMoveTicks <= 0)
            {
                binding.remainingMoveTicks = UnityEngine.Random.Range(2, 5);
            }

            if (binding.remainingMoveTicks <= 0)
            {
                return false;
            }

            binding.currentAction = RoutineActionType.Move;
            binding.remainingMoveTicks -= 1;
            binding.targetPosition = actionTarget;
            if (binding.remainingMoveTicks <= 0)
            {
                binding.actor.position = actionTarget;
                return true;
            }

            return true;
        }

        private static int ResolveActionDurationTicks(RoutineActionType action)
        {
            if (IsMealAction(action))
            {
                return UnityEngine.Random.Range(4, 7);
            }

            if (action == RoutineActionType.Sleep)
            {
                return 1;
            }

            return 1;
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
                    if (binding.missionTicks >= 100)
                    {
                        binding.completedWorkCount += 1;
                        binding.missionTicks = 0;
                        Debug.Log(string.Format(
                            CultureInfo.InvariantCulture,
                            "[RoutineWork] actor={0} completed={1} -> assigned_new_work",
                            binding.actor != null ? binding.actor.name : "Unknown",
                            binding.completedWorkCount));
                    }
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
                return WithCharacterDepth(fallback);
            }

            var slot = zone.Position + GetZoneActionOffset(actorIndex);
            if (IsNeedAction(action) && !zone.Contains(slot))
            {
                // Need action must complete at least once; if slot is outside boundary, fallback to zone center.
                return WithCharacterDepth(zone.Position);
            }

            return WithCharacterDepth(slot);
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
            int minutesFromDayStart = ticksFromDayStart * 15;
            int totalMinutes = minutesFromDayStart;
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

        private void UpdateDashboardUi(int dayIndex, int halfDayIndex, int tickInHalfDay, string timeText)
        {
            EnsureDashboardUiReferences();
            if (goalText == null && progressText == null && situationText == null)
            {
                return;
            }

            var totalMissionTicks = GetTotalMissionTicks();
            var ratio = GetMissionProgressRatio();
            var percentage = ratio * 100f;
            var movingCount = CountCurrentAction(RoutineActionType.Move);
            var avgHunger = ComputeAverageNeedValue(binding => binding.hunger);
            var avgSleep = ComputeAverageNeedValue(binding => binding.sleep);
            var avgStress = ComputeAverageNeedValue(binding => binding.stress);

            if (goalText != null)
            {
                goalText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Goal: {0} ({1}/{2})",
                    dashboardGoalTitle,
                    totalMissionTicks,
                    Mathf.Max(1, dashboardMissionGoalTicks));
            }

            if (progressText != null)
            {
                progressText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Progress: {0:0}% | Move:{1}/{2} | Avg H/S/T: {3:0}/{4:0}/{5:0}",
                    percentage,
                    movingCount,
                    characters.Count,
                    avgHunger,
                    avgSleep,
                    avgStress);
            }

            if (situationText != null)
            {
                situationText.text = BuildSituationSummary(dayIndex, halfDayIndex, tickInHalfDay, timeText);
            }
        }

        private string BuildSituationSummary(int dayIndex, int halfDayIndex, int tickInHalfDay, string timeText)
        {
            var summary = new StringBuilder(256);
            summary.AppendFormat(
                CultureInfo.InvariantCulture,
                "Situation: Day {0} Half {1} Tick {2} ({3})",
                dayIndex,
                halfDayIndex,
                tickInHalfDay,
                timeText);

            summary.AppendLine();
            summary.AppendFormat(
                CultureInfo.InvariantCulture,
                "Interventions pending:{0}, lastApplied:{1}, latestReject:{2}",
                _pendingInterventionCount,
                _lastAppliedInterventionTick < 0 ? "N/A" : _lastAppliedInterventionTick.ToString(CultureInfo.InvariantCulture),
                _recentRejectedInterventionReason);

            if (_dashboardContext.Count > 0)
            {
                var orderedKeys = new List<string>(_dashboardContext.Keys);
                orderedKeys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < orderedKeys.Count; i++)
                {
                    var key = orderedKeys[i];
                    summary.AppendLine();
                    summary.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1}", key, _dashboardContext[key]);
                }
            }

            return summary.ToString();
        }

        private int GetTotalMissionTicks()
        {
            var total = 0;
            for (int i = 0; i < characters.Count; i++)
            {
                total += Mathf.Max(0, characters[i].missionTicks);
            }

            return total;
        }

        private float GetMissionProgressRatio()
        {
            var target = Mathf.Max(1, dashboardMissionGoalTicks);
            return Mathf.Clamp01(GetTotalMissionTicks() / (float)target);
        }

        private int CountCurrentAction(RoutineActionType action)
        {
            var count = 0;
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].actor != null && characters[i].currentAction == action)
                {
                    count += 1;
                }
            }

            return count;
        }

        private float ComputeAverageNeedValue(Func<RoutineCharacterBinding, float> selector)
        {
            if (characters.Count == 0)
            {
                return 0f;
            }

            var sum = 0f;
            var count = 0;
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].actor == null)
                {
                    continue;
                }

                sum += selector(characters[i]);
                count += 1;
            }

            return count == 0 ? 0f : sum / count;
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

            EnsureDashboardUiReferences();

            EnsureDefaultCharactersExist();
            EnsureCharacterBindingsFromRoot();
            Ensure2DWorldSetup();
            ApplyDepthLayout();
        }

        private void EnsureDashboardUiReferences()
        {
            if (goalText == null)
            {
                var goalGo = GameObject.Find("GoalText");
                goalText = goalGo != null ? goalGo.GetComponent<Text>() : null;
            }

            if (progressText == null)
            {
                var progressGo = GameObject.Find("ProgressText");
                progressText = progressGo != null ? progressGo.GetComponent<Text>() : null;
            }

            if (situationText == null)
            {
                var situationGo = GameObject.Find("SituationText");
                situationText = situationGo != null ? situationGo.GetComponent<Text>() : null;
            }

            if (!autoCreateDashboardUi || (goalText != null && progressText != null && situationText != null))
            {
                return;
            }

            CreateDashboardUiIfMissing();
        }

        private void CreateDashboardUiIfMissing()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var panelTransform = canvas.transform.Find("RoutineDashboardPanel");
            if (panelTransform == null)
            {
                var panel = new GameObject("RoutineDashboardPanel", typeof(RectTransform));
                panelTransform = panel.transform;
                panelTransform.SetParent(canvas.transform, false);
                var panelRect = panelTransform as RectTransform;
                if (panelRect != null)
                {
                    panelRect.anchorMin = new Vector2(1f, 1f);
                    panelRect.anchorMax = new Vector2(1f, 1f);
                    panelRect.pivot = new Vector2(1f, 1f);
                    panelRect.anchoredPosition = new Vector2(-16f, -16f);
                    panelRect.sizeDelta = new Vector2(560f, 180f);
                }
            }

            if (goalText == null)
            {
                goalText = CreateDashboardLine(panelTransform, "GoalText", 0f);
            }

            if (progressText == null)
            {
                progressText = CreateDashboardLine(panelTransform, "ProgressText", -44f);
            }

            if (situationText == null)
            {
                situationText = CreateDashboardLine(panelTransform, "SituationText", -88f);
                situationText.horizontalOverflow = HorizontalWrapMode.Wrap;
                situationText.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        private static Text CreateDashboardLine(Transform parent, string objectName, float yOffset)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var lineTransform = go.transform;
            lineTransform.SetParent(parent, false);
            var rect = lineTransform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(0f, yOffset);
                rect.sizeDelta = new Vector2(0f, 40f);
            }

            var text = go.GetComponent<Text>();
            try
            {
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (ArgumentException)
            {
                text.font = null;
            }
            text.fontSize = 20;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(0.92f, 0.96f, 1f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = string.Empty;
            return text;
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
            if (enforceDepthLayout)
            {
                localPosition.z = characterDepthZ;
            }

            character.transform.localPosition = localPosition;
            character.transform.localRotation = Quaternion.identity;
            character.transform.localScale = new Vector3(DefaultCharacterSpriteSize, DefaultCharacterSpriteSize, 1f);
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

        private void ApplyDepthLayout()
        {
            if (!enforceDepthLayout)
            {
                return;
            }

            for (int i = 0; i < _zoneAnchors.Count; i++)
            {
                var zone = _zoneAnchors[i];
                if (zone == null)
                {
                    continue;
                }

                var zonePosition = zone.transform.position;
                zonePosition.z = zoneDepthZ;
                zone.transform.position = zonePosition;
            }

            for (int i = 0; i < characters.Count; i++)
            {
                var actor = characters[i].actor;
                if (actor == null)
                {
                    continue;
                }

                ForceActorDepth(actor);
                var target = characters[i].targetPosition;
                target.z = characterDepthZ;
                characters[i].targetPosition = target;
            }
        }

        private void RebuildMvpScene2DInternal()
        {
            use2DWorld = true;
            disableLegacy3DRenderers = true;
            enforceDepthLayout = true;
            zoneDepthZ = 0f;
            characterDepthZ = -1f;
            autoCreateDashboardUi = false;

            zoneGap = Mathf.Max(1.5f, zoneGap);
            if (zoneScale.x <= 0f)
            {
                zoneScale.x = 0.5f;
            }
            if (zoneScale.y <= 0f)
            {
                zoneScale.y = 0.5f;
            }

            EnsureMainCamera2D();

            DestroyIfExists("Zones");
            DestroyIfExists("Characters");

            var zonesGo = new GameObject("Zones");
            zonesRoot = zonesGo.transform;
            var halfWidth = zoneScale.x * 0.5f;
            var centerDistance = (halfWidth + halfWidth) + zoneGap;
            missionZone = CreateZone2D(
                zonesRoot,
                "MissionZone",
                "zone.mission",
                new[] { ZoneTagMission },
                new Vector2(0f, 0f),
                zoneScale,
                new Color(0.9f, 0.2f, 0.2f, DefaultZoneAlpha));
            cafeteriaZone = CreateZone2D(
                zonesRoot,
                "CafeteriaZone",
                "need.hunger",
                new[] { ZoneTagNeedHunger },
                new Vector2(-centerDistance, 0f),
                zoneScale,
                new Color(0.12f, 0.53f, 0.9f, DefaultZoneAlpha));
            sleepZone = CreateZone2D(
                zonesRoot,
                "SleepZone",
                "need.sleep",
                new[] { ZoneTagNeedSleep },
                new Vector2(centerDistance, 0f),
                zoneScale,
                new Color(0.26f, 0.66f, 0.29f, DefaultZoneAlpha));

            var charactersGo = new GameObject("Characters");
            charactersRoot = charactersGo.transform;
            CreateCharacter2D(charactersRoot, "Character_A", new Vector2(-1.2f, CharacterSpawnHeight), new Color(0.2f, 0.85f, 1f, 1f));
            CreateCharacter2D(charactersRoot, "Character_B", new Vector2(0f, CharacterSpawnHeight), new Color(0.4f, 1f, 0.4f, 1f));
            CreateCharacter2D(charactersRoot, "Character_C", new Vector2(1.2f, CharacterSpawnHeight), new Color(1f, 0.7f, 0.25f, 1f));

            characters.Clear();
            currentTimeText = null;
            goalText = null;
            progressText = null;
            situationText = null;
        }

        private void SyncLayoutConfigFromSceneObjects()
        {
            if (missionZone == null || cafeteriaZone == null || sleepZone == null)
            {
                return;
            }

            // Use current scene-authored zone size as canonical rebuild baseline.
            var missionScale = missionZone.transform.localScale;
            var x = Mathf.Abs(missionScale.x);
            var y = Mathf.Abs(missionScale.y);
            if (x > 0.0001f && y > 0.0001f)
            {
                zoneScale = new Vector2(x, y);
            }

            // Estimate gap from current left-center-right layout if possible.
            var zones = new[] { cafeteriaZone.transform, missionZone.transform, sleepZone.transform };
            System.Array.Sort(zones, (a, b) => a.position.x.CompareTo(b.position.x));
            var left = zones[0];
            var center = zones[1];
            var right = zones[2];
            var gapLeft = ComputeEdgeGap(left, center);
            var gapRight = ComputeEdgeGap(center, right);
            var observedGap = Mathf.Min(gapLeft, gapRight);
            if (observedGap > 0.01f)
            {
                zoneGap = Mathf.Max(1.5f, observedGap);
            }
        }

        private static float ComputeEdgeGap(Transform left, Transform right)
        {
            var leftHalf = Mathf.Abs(left.localScale.x) * 0.5f;
            var rightHalf = Mathf.Abs(right.localScale.x) * 0.5f;
            var leftEdge = left.position.x + leftHalf;
            var rightEdge = right.position.x - rightHalf;
            return rightEdge - leftEdge;
        }

        private void EnsureMainCamera2D()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                camera = cameraGo.AddComponent<Camera>();
            }

            camera.orthographic = true;
            var cameraTransform = camera.transform;
            cameraTransform.position = new Vector3(0f, 0f, -10f);
            cameraTransform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.18f, 0.26f, 1f);
        }

        private RoutineZoneAnchor CreateZone2D(
            Transform parent,
            string objectName,
            string zoneId,
            string[] tags,
            Vector2 center,
            Vector2 size,
            Color color)
        {
            var zone = new GameObject(objectName);
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = new Vector3(center.x, center.y, zoneDepthZ);
            zone.transform.localScale = new Vector3(size.x, size.y, 1f);
            EnsureSpriteRenderer(zone, color, Vector2.one, false);

            var collider2D = zone.AddComponent<BoxCollider2D>();
            collider2D.size = Vector2.one;
            collider2D.offset = Vector2.zero;

            var anchor = zone.AddComponent<RoutineZoneAnchor>();
            anchor.SetZoneId(zoneId);
            anchor.SetTags(tags);
            anchor.RebindBoundaries();
            return anchor;
        }

        private void CreateCharacter2D(Transform parent, string name, Vector2 localPosition, Color color)
        {
            var actor = new GameObject(name);
            actor.transform.SetParent(parent, false);
            actor.transform.localPosition = new Vector3(localPosition.x, localPosition.y, characterDepthZ);
            actor.transform.localScale = new Vector3(DefaultCharacterSpriteSize, DefaultCharacterSpriteSize, 1f);
            EnsureSpriteRenderer(actor, color, new Vector2(DefaultCharacterSpriteSize, DefaultCharacterSpriteSize), true);
            actor.AddComponent<CapsuleCollider2D>();

            var gaugeRoot = new GameObject("GaugeRoot");
            gaugeRoot.transform.SetParent(actor.transform, false);
            gaugeRoot.transform.localPosition = new Vector3(0f, 1f, 0f);

            CreateGaugeBar2D(gaugeRoot.transform, "HungerBar", 0.24f, new Color(0.94f, 0.35f, 0.35f, 1f));
            CreateGaugeBar2D(gaugeRoot.transform, "SleepBar", 0f, new Color(0.30f, 0.55f, 0.95f, 1f));
            CreateGaugeBar2D(gaugeRoot.transform, "StressBar", -0.24f, new Color(0.30f, 0.80f, 0.40f, 1f));
        }

        private void CreateGaugeBar2D(Transform parent, string name, float y, Color color)
        {
            var bar = new GameObject(name);
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0f, y, 0f);
            bar.transform.localScale = new Vector3(GaugeBarMaxWidth, 0.12f, 1f);
            EnsureSpriteRenderer(bar, color, Vector2.one, true);
        }

        private void BuildDashboardCanvas2D()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            GameObject canvasGo;
            if (canvas == null)
            {
                canvasGo = new GameObject("MvpDashboardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
            }
            else
            {
                canvasGo = canvas.gameObject;
                if (canvasGo.GetComponent<CanvasScaler>() == null)
                {
                    canvasGo.AddComponent<CanvasScaler>();
                }

                if (canvasGo.GetComponent<GraphicRaycaster>() == null)
                {
                    canvasGo.AddComponent<GraphicRaycaster>();
                }
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 5f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            currentTimeText = EnsureDashboardText(canvasGo.transform, "CurrentTimeText", new Vector2(24f, -24f), 40, "Day 1 | 06:00");
            goalText = EnsureDashboardText(canvasGo.transform, "GoalText", new Vector2(24f, -82f), 30, "Goal: Routine Stability");
            progressText = EnsureDashboardText(canvasGo.transform, "ProgressText", new Vector2(24f, -126f), 28, "Progress: 0%");
            situationText = EnsureDashboardText(canvasGo.transform, "SituationText", new Vector2(24f, -170f), 24, "Situation: Initialized");
            situationText.horizontalOverflow = HorizontalWrapMode.Wrap;
            situationText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static Text EnsureDashboardText(Transform parent, string name, Vector2 anchorPos, int fontSize, string initialText)
        {
            var existing = parent.Find(name);
            GameObject textGo;
            if (existing == null)
            {
                textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(parent, false);
            }
            else
            {
                textGo = existing.gameObject;
                if (textGo.GetComponent<Text>() == null)
                {
                    textGo.AddComponent<Text>();
                }
            }

            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchorPos;
            rect.sizeDelta = new Vector2(1200f, 44f);

            var text = textGo.GetComponent<Text>();
            try
            {
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (ArgumentException)
            {
                text.font = null;
            }
            text.fontSize = fontSize;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(0.94f, 0.97f, 1f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = initialText;
            return text;
        }

        private static void DestroyIfExists(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        private void Ensure2DWorldSetup()
        {
            if (!use2DWorld)
            {
                return;
            }

            Ensure2DZones();
            Ensure2DCharacters();
        }

        private void Ensure2DZones()
        {
            for (int i = 0; i < _zoneAnchors.Count; i++)
            {
                var anchor = _zoneAnchors[i];
                if (anchor == null)
                {
                    continue;
                }

                var color = ReadRendererColor(anchor.GetComponent<Renderer>(), new Color(0.9f, 0.2f, 0.2f, DefaultZoneAlpha));
                color.a = DefaultZoneAlpha;
                EnsureSpriteRenderer(anchor.gameObject, color, Vector2.one, false);
                EnsureZoneBoundary2D(anchor);
            }
        }

        private void Ensure2DCharacters()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null)
                {
                    continue;
                }

                var actorGo = binding.actor.gameObject;
                actorGo.transform.localScale = new Vector3(DefaultCharacterSpriteSize, DefaultCharacterSpriteSize, 1f);
                var color = ReadRendererColor(actorGo.GetComponent<Renderer>(), GetLineColor(i));
                EnsureSpriteRenderer(actorGo, color, new Vector2(DefaultCharacterSpriteSize, DefaultCharacterSpriteSize), true);
                EnsureGaugeBarsAre2D(binding.actor);
            }
        }

        private void EnsureGaugeBarsAre2D(Transform actor)
        {
            var gaugeRoot = actor.Find("GaugeRoot");
            if (gaugeRoot == null)
            {
                return;
            }

            EnsureSpriteRenderer(gaugeRoot.Find("HungerBar")?.gameObject, new Color(0.94f, 0.35f, 0.35f, 1f), Vector2.one, true);
            EnsureSpriteRenderer(gaugeRoot.Find("SleepBar")?.gameObject, new Color(0.30f, 0.55f, 0.95f, 1f), Vector2.one, true);
            EnsureSpriteRenderer(gaugeRoot.Find("StressBar")?.gameObject, new Color(0.30f, 0.80f, 0.40f, 1f), Vector2.one, true);
        }

        private void EnsureZoneBoundary2D(RoutineZoneAnchor anchor)
        {
            var go = anchor.gameObject;
            var box2D = go.GetComponent<BoxCollider2D>();
            if (box2D == null)
            {
                box2D = go.AddComponent<BoxCollider2D>();
            }

            var box3D = go.GetComponent<BoxCollider>();
            if (box3D != null)
            {
                var size = box3D.size;
                var scale = go.transform.lossyScale;
                box2D.size = new Vector2(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y));
                var center = box3D.center;
                box2D.offset = new Vector2(center.x * scale.x, center.y * scale.y);
                if (disableLegacy3DRenderers)
                {
                    box3D.enabled = false;
                }
            }
            else if (box2D.size == Vector2.zero)
            {
                box2D.size = new Vector2(Mathf.Max(1f, go.transform.localScale.x), Mathf.Max(1f, go.transform.localScale.y));
            }

            anchor.RebindBoundaries();
        }

        private void EnsureSpriteRenderer(GameObject go, Color color, Vector2 targetScale, bool opaque)
        {
            if (go == null)
            {
                return;
            }

            var spriteRenderer = go.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = go.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = GetRuntimeSquareSprite();
            spriteRenderer.color = opaque ? new Color(color.r, color.g, color.b, 1f) : new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a));
            var scale = go.transform.localScale;
            scale.x = Mathf.Abs(scale.x) < 0.0001f ? targetScale.x : scale.x;
            scale.y = Mathf.Abs(scale.y) < 0.0001f ? targetScale.y : scale.y;
            go.transform.localScale = scale;

            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null && disableLegacy3DRenderers)
            {
                meshRenderer.enabled = false;
            }
        }

        private static Color ReadRendererColor(Renderer renderer, Color fallback)
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return fallback;
            }

            var material = renderer.sharedMaterial;
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return fallback;
        }

        private static Sprite GetRuntimeSquareSprite()
        {
            if (_runtimeSquareSprite != null)
            {
                return _runtimeSquareSprite;
            }

            var texture = Texture2D.whiteTexture;
            _runtimeSquareSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                1f);
            _runtimeSquareSprite.name = "RuntimeSquare";
            return _runtimeSquareSprite;
        }

        private void ForceActorDepth(Transform actor)
        {
            var position = actor.position;
            position.z = characterDepthZ;
            actor.position = position;
        }

        private Vector3 WithCharacterDepth(Vector3 position)
        {
            if (!enforceDepthLayout)
            {
                return position;
            }

            position.z = characterDepthZ;
            return position;
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
            var spriteRenderer = go.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }

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
            if (use2DWorld)
            {
                var go2D = new GameObject(name);
                EnsureSpriteRenderer(go2D, Color.white, new Vector2(DefaultCharacterSpriteSize, DefaultCharacterSpriteSize), true);
                return go2D;
            }

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
