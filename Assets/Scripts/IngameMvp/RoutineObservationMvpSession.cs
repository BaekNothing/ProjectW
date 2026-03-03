using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using ProjectW.IngameCore.Simulation;
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
        [NonSerialized] public bool hasLockedZonePosition;
        [NonSerialized] public string lockedZoneKey;
        [NonSerialized] public Vector3 lockedZonePosition;
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
        private const float DeskActionArrivalThresholdSqr = 0.01f;
        private const string DeskRootName = "DeskSlots";
        private const string ZoneTagMission = "zone.mission";
        private const string ZoneTagNeedHunger = "need.hunger";
        private const string ZoneTagNeedSleep = "need.sleep";
        private const string JobZoneWork = "workzone";
        private const string JobZoneEat = "eatzone";
        private const string JobZoneSleep = "sleepzone";

        [Header("Tick")]
        [SerializeField] private bool autoRunOnStart = true;
        [SerializeField] private float tickIntervalSeconds = 1f;
        [SerializeField] private int ticksPerHalfDay = 48;
        [SerializeField, Range(0, 23)] private int simulationStartHour = 10;
        [SerializeField, Range(0, 45)] private int simulationStartMinute = 0;

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
        [SerializeField] private RoutineNeuronPanelView neuronPanelView;
        [SerializeField] private Camera interactionCamera;

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
        [SerializeField, Min(1)] private int minDeskCountPerZone = 2;
        [SerializeField, Min(1)] private int maxDeskCountPerZone = 5;
        [SerializeField, Range(0.02f, 0.45f)] private float deskInsetRatio = 0.14f;
        [SerializeField, Range(0.08f, 0.9f)] private float deskMinSpacingRatio = 0.7f;
        [SerializeField] private Vector2 deskVisualScale = new Vector2(0.16f, 0.09f);
        [SerializeField, Min(0.5f)] private float minimumCharacterSeparation = 0.5f;
        [SerializeField] private bool autoExpandZoneWidthForSeparation = true;

        [Header("Depth Layout")]
        [SerializeField] private bool enforceDepthLayout = true;
        [SerializeField] private float zoneDepthZ = 0f;
        [SerializeField] private float characterDepthZ = -1f;

        [Header("Debug View")]
        [SerializeField] private bool showDebugOnGui = true;
        [SerializeField] private Vector2 debugPanelPosition = new Vector2(16f, 16f);
        [SerializeField] private Vector2 debugPanelSize = new Vector2(430f, 240f);

        private int _absoluteTick;
        private Coroutine _loopCoroutine;
        private readonly List<RoutineZoneAnchor> _zoneAnchors = new List<RoutineZoneAnchor>();
        private readonly Dictionary<string, string> _dashboardContext = new Dictionary<string, string>();
        private readonly Dictionary<string, Dictionary<RoutineCharacterBinding, Vector3>> _zoneLockedPositions = new Dictionary<string, Dictionary<RoutineCharacterBinding, Vector3>>();
        private readonly Dictionary<string, List<Vector3>> _zoneDeskPositions = new Dictionary<string, List<Vector3>>();
        private readonly Dictionary<string, Dictionary<RoutineCharacterBinding, Vector3>> _zonePreferredDeskPositions = new Dictionary<string, Dictionary<RoutineCharacterBinding, Vector3>>();
        private GUIStyle _debugLabelStyle;
        private int _lastAppliedInterventionTick = -1;
        private int _pendingInterventionCount;
        private string _recentRejectedInterventionReason = "None";
        private static Sprite _runtimeSquareSprite;
        private readonly CharacterNeuronSystem _neuronSystem = new CharacterNeuronSystem();
        private readonly Dictionary<RoutineCharacterBinding, CharacterNeuronSnapshot> _latestNeuronSnapshots = new Dictionary<RoutineCharacterBinding, CharacterNeuronSnapshot>();
        private readonly Dictionary<RoutineActionType, NeedRequirement> _actionJobRequirements = new Dictionary<RoutineActionType, NeedRequirement>();
        private readonly List<WorldItem> _officeItems = new List<WorldItem>();
        private RoutineCharacterBinding _selectedCharacter;

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
            Application.runInBackground = true;
            AutoBindSceneReferences();
            EnsureOfficeItemsAndJobBindings();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                Application.runInBackground = true;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Application.runInBackground = true;
            }
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
            HandleCharacterSelectionInput();
            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null) continue;
                if (enforceDepthLayout)
                {
                    ForceActorDepth(binding.actor);
                    var target = binding.targetPosition;
                    target.y = 0f;
                    target.z = characterDepthZ;
                    binding.targetPosition = target;
                }

                binding.actor.position = Vector3.MoveTowards(
                    binding.actor.position,
                    binding.targetPosition,
                    binding.moveSpeed * Time.deltaTime);
                binding.actor.position = WithCharacterDepth(binding.actor.position);
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

            EnsureOfficeItemsAndJobBindings();

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
            int totalMinutes = (GetSimulationStartMinutes() + (tickInDay - 1) * 15) % 1440;
            int hour = (totalMinutes / 60) % 24;
            int minute = totalMinutes % 60;
            var defaultAction = ToRoutineAction(_neuronSystem.EvaluateRoutine(new RoutineNeuronContext("default", _absoluteTick, hour, minute, 0, 100f, 100f, 100f, 0f, 0f, 0f, false, CharacterNeuronIntent.Work)).ScheduledIntent);
            RoutineZoneAnchor defaultZone = ResolveZone(defaultAction, Vector3.zero);
            string zoneName = defaultZone != null ? defaultZone.ZoneId : "MissingZone";
            CleanupZoneLocks();

            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null) continue;
                EnsureRuntimeBindingInitialized(binding, i);
                if (enforceDepthLayout)
                {
                    ForceActorDepth(binding.actor);
                }

                var neuronSnapshot = _neuronSystem.EvaluateRoutine(new RoutineNeuronContext(
                    binding.actor.name,
                    _absoluteTick,
                    hour,
                    minute,
                    binding.routineOffsetTicks,
                    binding.hunger,
                    binding.sleep,
                    binding.stress,
                    binding.hungerThreshold,
                    binding.sleepThreshold,
                    binding.stressThreshold,
                    binding.hasLatchedNeedAction,
                    ToNeuronIntent(binding.latchedNeedAction)));

                _latestNeuronSnapshots[binding] = neuronSnapshot;
                var desiredAction = ToRoutineAction(neuronSnapshot.IntendedIntent);

                if (binding.remainingActionTicks > 0 && IsNeedAction(binding.currentAction))
                {
                    desiredAction = binding.currentAction;
                }

                if (binding.hasLatchedNeedAction && IsNeedAction(binding.latchedNeedAction))
                {
                    desiredAction = binding.latchedNeedAction;
                }
                else if (IsNeedAction(desiredAction))
                {
                    binding.hasLatchedNeedAction = true;
                    binding.latchedNeedAction = desiredAction;
                }

                binding.intendedAction = desiredAction;
                RoutineZoneAnchor zone = ResolveZone(desiredAction, binding.actor.position);
                var actionTarget = ResolveActionTargetPosition(zone, binding, desiredAction, binding.actor.position);
                binding.targetPosition = actionTarget;
                UpdateTargetLineVisual(binding, i);
                var isMicroAdjust = zone != null
                                    && zone.Contains(binding.actor.position)
                                    && zone.Contains(actionTarget);

                var movedThisTick = ResolveMovementTick(binding, desiredAction, actionTarget, isMicroAdjust);
                if (movedThisTick)
                {
                    ApplyNeedsAndProgress(binding, RoutineActionType.Move, false, false);
                    UpdateRuntimeStateTexts(binding);
                    continue;
                }

                if (binding.remainingActionTicks <= 0)
                {
                    binding.remainingActionTicks = ResolveActionDurationTicks(desiredAction);
                }

                binding.currentAction = desiredAction;
                var canPerformAction = CanPerformActionAtDesk(binding, zone);
                var canResolveNeed = canPerformAction && CanResolveNeed(binding, desiredAction, zone);
                ApplyNeedsAndProgress(binding, desiredAction, canResolveNeed, canPerformAction);
                ApplyNeedLatchAfterAction(binding, desiredAction, canResolveNeed);
                if (binding.remainingActionTicks > 0)
                {
                    binding.remainingActionTicks -= 1;
                }
                UpdateRuntimeStateTexts(binding);
            }

            string timeText = BuildTimeText(dayIndex, halfDayIndex, tickInHalfDay, totalMinutes);
            if (currentTimeText != null)
            {
                currentTimeText.text = timeText;
            }

            UpdateDashboardUi(dayIndex, halfDayIndex, tickInHalfDay, timeText);
            UpdateNeuronPanel();
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

        private bool ResolveMovementTick(
            RoutineCharacterBinding binding,
            RoutineActionType desiredAction,
            Vector3 actionTarget,
            bool isMicroAdjust)
        {
            bool needsMove = !HasArrived(binding.actor.position, actionTarget);
            if (needsMove && binding.remainingMoveTicks <= 0)
            {
                binding.remainingMoveTicks = isMicroAdjust ? 1 : UnityEngine.Random.Range(2, 5);
            }

            if (needsMove && isMicroAdjust)
            {
                binding.remainingMoveTicks = Mathf.Clamp(binding.remainingMoveTicks, 1, 1);
            }

            if (!needsMove)
            {
                binding.remainingMoveTicks = 0;
                return false;
            }

            if (binding.remainingMoveTicks <= 0)
            {
                binding.remainingMoveTicks = 1;
            }

            var remainingDistance = Vector3.Distance(binding.actor.position, actionTarget);
            var secondsLeft = Mathf.Max(0.05f, binding.remainingMoveTicks * tickIntervalSeconds);
            binding.moveSpeed = Mathf.Max(0.01f, remainingDistance / secondsLeft);
            binding.currentAction = RoutineActionType.Move;
            binding.remainingMoveTicks -= 1;
            binding.targetPosition = actionTarget;
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

        private void ApplyNeedsAndProgress(RoutineCharacterBinding binding, RoutineActionType action, bool canResolveNeed, bool canPerformAction)
        {
            if (action != RoutineActionType.Move && !canPerformAction)
            {
                action = RoutineActionType.Move;
            }

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
                    if (canResolveNeed)
                    {
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

        private Vector3 ResolveActionTargetPosition(
            RoutineZoneAnchor zone,
            RoutineCharacterBinding binding,
            RoutineActionType desiredAction,
            Vector3 fallback)
        {
            if (zone == null)
            {
                ReleaseZoneLock(binding);
                return WithCharacterDepth(fallback);
            }

            var zoneKey = GetZoneReservationKey(zone);
            if (binding.hasLockedZonePosition && !string.Equals(binding.lockedZoneKey, zoneKey, StringComparison.Ordinal))
            {
                ReleaseZoneLock(binding);
            }

            if (!_zoneDeskPositions.TryGetValue(zoneKey, out var deskPositions) || deskPositions == null || deskPositions.Count == 0)
            {
                EnsureZoneDesks(zone, zoneKey);
                _zoneDeskPositions.TryGetValue(zoneKey, out deskPositions);
            }

            if (TryGetZoneLock(zoneKey, binding, out var lockedPosition))
            {
                if (IsDeskPositionAvailableInZone(zone, zoneKey, lockedPosition))
                {
                    return WithCharacterDepth(lockedPosition);
                }

                ReleaseZoneLock(binding);
            }

            var currentPosition = WithCharacterDepth(binding.actor != null ? binding.actor.position : fallback);
            if (TryGetPreferredDeskPosition(zone, zoneKey, binding, out var preferredDesk)
                && !IsDeskReservedByOther(zoneKey, binding, preferredDesk))
            {
                AssignZoneLock(zoneKey, binding, preferredDesk);
                RememberPreferredDesk(zoneKey, binding, preferredDesk);
                return preferredDesk;
            }

            if (!TryFindAvailableDeskPosition(zone, zoneKey, binding, currentPosition, deskPositions, out var deskTarget))
            {
                ReleaseZoneLock(binding);
                return currentPosition;
            }

            AssignZoneLock(zoneKey, binding, deskTarget);
            RememberPreferredDesk(zoneKey, binding, deskTarget);
            return deskTarget;
        }

        private void CleanupZoneLocks()
        {
            if (_zoneLockedPositions.Count == 0)
            {
                return;
            }

            var zoneKeys = new List<string>(_zoneLockedPositions.Keys);
            for (int i = 0; i < zoneKeys.Count; i++)
            {
                var zoneKey = zoneKeys[i];
                if (!_zoneLockedPositions.TryGetValue(zoneKey, out var occupants))
                {
                    continue;
                }

                var bindings = new List<RoutineCharacterBinding>(occupants.Keys);
                for (int j = 0; j < bindings.Count; j++)
                {
                    var binding = bindings[j];
                    if (binding == null || binding.actor == null)
                    {
                        occupants.Remove(binding);
                        continue;
                    }

                    if (!binding.hasLockedZonePosition || !string.Equals(binding.lockedZoneKey, zoneKey, StringComparison.Ordinal))
                    {
                        occupants.Remove(binding);
                        continue;
                    }

                    if (!_zoneDeskPositions.TryGetValue(zoneKey, out var desks) || desks == null || desks.Count == 0)
                    {
                        occupants.Remove(binding);
                        binding.hasLockedZonePosition = false;
                        binding.lockedZoneKey = null;
                        binding.lockedZonePosition = Vector3.zero;
                        continue;
                    }

                    if (!ContainsDeskPosition(desks, binding.lockedZonePosition))
                    {
                        occupants.Remove(binding);
                        binding.hasLockedZonePosition = false;
                        binding.lockedZoneKey = null;
                        binding.lockedZonePosition = Vector3.zero;
                    }
                }

                if (occupants.Count == 0)
                {
                    _zoneLockedPositions.Remove(zoneKey);
                }
            }
        }

        private bool TryGetZoneLock(string zoneKey, RoutineCharacterBinding binding, out Vector3 position)
        {
            position = default;
            if (binding == null || !binding.hasLockedZonePosition || !string.Equals(binding.lockedZoneKey, zoneKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (!_zoneLockedPositions.TryGetValue(zoneKey, out var occupants))
            {
                return false;
            }

            if (!occupants.TryGetValue(binding, out position))
            {
                return false;
            }

            return true;
        }

        private void AssignZoneLock(string zoneKey, RoutineCharacterBinding binding, Vector3 position)
        {
            if (binding == null)
            {
                return;
            }

            if (!_zoneLockedPositions.TryGetValue(zoneKey, out var occupants))
            {
                occupants = new Dictionary<RoutineCharacterBinding, Vector3>();
                _zoneLockedPositions[zoneKey] = occupants;
            }

            var normalized = WithCharacterDepth(position);
            occupants[binding] = normalized;
            binding.hasLockedZonePosition = true;
            binding.lockedZoneKey = zoneKey;
            binding.lockedZonePosition = normalized;
        }

        private void ReleaseZoneLock(RoutineCharacterBinding binding)
        {
            if (binding == null || !binding.hasLockedZonePosition)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(binding.lockedZoneKey)
                && _zoneLockedPositions.TryGetValue(binding.lockedZoneKey, out var occupants))
            {
                occupants.Remove(binding);
                if (occupants.Count == 0)
                {
                    _zoneLockedPositions.Remove(binding.lockedZoneKey);
                }
            }

            binding.hasLockedZonePosition = false;
            binding.lockedZoneKey = null;
            binding.lockedZonePosition = Vector3.zero;
        }

        private static bool ContainsDeskPosition(List<Vector3> deskPositions, Vector3 candidate)
        {
            if (deskPositions == null)
            {
                return false;
            }

            for (int i = 0; i < deskPositions.Count; i++)
            {
                if ((deskPositions[i] - candidate).sqrMagnitude <= 0.0001f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsDeskPositionAvailableInZone(RoutineZoneAnchor zone, string zoneKey, Vector3 position)
        {
            if (zone == null || !_zoneDeskPositions.TryGetValue(zoneKey, out var desks) || desks == null)
            {
                return false;
            }

            if (!ContainsDeskPosition(desks, position))
            {
                return false;
            }

            return zone.Contains(position);
        }

        private bool TryFindAvailableDeskPosition(
            RoutineZoneAnchor zone,
            string zoneKey,
            RoutineCharacterBinding binding,
            Vector3 referencePosition,
            List<Vector3> deskPositions,
            out Vector3 bestPosition)
        {
            bestPosition = referencePosition;
            if (deskPositions == null || deskPositions.Count == 0)
            {
                return false;
            }

            var fallback = WithCharacterDepth(deskPositions[0]);
            var best = fallback;
            var bestTravel = float.PositiveInfinity;
            var found = false;
            for (int i = 0; i < deskPositions.Count; i++)
            {
                var candidate = WithCharacterDepth(deskPositions[i]);
                if (zone != null && !zone.Contains(candidate))
                {
                    continue;
                }

                if (IsDeskReservedByOther(zoneKey, binding, candidate))
                {
                    continue;
                }

                var travel = (referencePosition - candidate).sqrMagnitude;
                if (travel < bestTravel)
                {
                    bestTravel = travel;
                    best = candidate;
                    found = true;
                }
            }

            bestPosition = best;
            return found;
        }

        private bool TryGetPreferredDeskPosition(RoutineZoneAnchor zone, string zoneKey, RoutineCharacterBinding binding, out Vector3 preferredDesk)
        {
            preferredDesk = default;
            if (zone == null || binding == null)
            {
                return false;
            }

            if (!_zonePreferredDeskPositions.TryGetValue(zoneKey, out var preferredMap) || preferredMap == null)
            {
                return false;
            }

            if (!preferredMap.TryGetValue(binding, out preferredDesk))
            {
                return false;
            }

            preferredDesk = WithCharacterDepth(preferredDesk);
            if (!_zoneDeskPositions.TryGetValue(zoneKey, out var desks) || desks == null || !ContainsDeskPosition(desks, preferredDesk))
            {
                preferredMap.Remove(binding);
                return false;
            }

            if (!zone.Contains(preferredDesk))
            {
                preferredMap.Remove(binding);
                return false;
            }

            return true;
        }

        private void RememberPreferredDesk(string zoneKey, RoutineCharacterBinding binding, Vector3 deskPosition)
        {
            if (binding == null || string.IsNullOrWhiteSpace(zoneKey))
            {
                return;
            }

            if (!_zonePreferredDeskPositions.TryGetValue(zoneKey, out var preferredMap))
            {
                preferredMap = new Dictionary<RoutineCharacterBinding, Vector3>();
                _zonePreferredDeskPositions[zoneKey] = preferredMap;
            }

            preferredMap[binding] = WithCharacterDepth(deskPosition);
        }

        private bool IsDeskReservedByOther(string zoneKey, RoutineCharacterBinding binding, Vector3 deskPosition)
        {
            if (!_zoneLockedPositions.TryGetValue(zoneKey, out var occupants))
            {
                return false;
            }

            foreach (var kvp in occupants)
            {
                if (ReferenceEquals(kvp.Key, binding))
                {
                    continue;
                }

                if ((kvp.Value - deskPosition).sqrMagnitude <= 0.0001f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanPerformActionAtDesk(RoutineCharacterBinding binding, RoutineZoneAnchor zone)
        {
            if (binding == null || binding.actor == null || zone == null || !binding.hasLockedZonePosition)
            {
                return false;
            }

            var zoneKey = GetZoneReservationKey(zone);
            if (string.IsNullOrWhiteSpace(zoneKey) || !string.Equals(binding.lockedZoneKey, zoneKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (!_zoneDeskPositions.TryGetValue(zoneKey, out var desks) || desks == null || desks.Count == 0)
            {
                return false;
            }

            var deskPosition = WithCharacterDepth(binding.lockedZonePosition);
            if (!ContainsDeskPosition(desks, deskPosition) || !zone.Contains(deskPosition))
            {
                return false;
            }

            var actorPosition = WithCharacterDepth(binding.actor.position);
            return (actorPosition - deskPosition).sqrMagnitude <= DeskActionArrivalThresholdSqr;
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

        private string GetZoneReservationKey(RoutineZoneAnchor zone)
        {
            var key = zone.ZoneId;
            if (string.IsNullOrWhiteSpace(key))
            {
                key = zone.name;
            }

            return key;
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

        private string BuildTimeText(int dayIndex, int halfDayIndex, int tickInHalfDay, int totalMinutes)
        {
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

        private int GetSimulationStartMinutes()
        {
            int alignedMinute = Mathf.Clamp((simulationStartMinute / 15) * 15, 0, 45);
            return (simulationStartHour * 60) + alignedMinute;
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
            EnsureInteractionReferences();

            EnsureDefaultCharactersExist();
            EnsureCharacterBindingsFromRoot();
            EnsureZoneWidthsForCharacterSeparation();
            Ensure2DWorldSetup();
            EnsureZoneDesks();
            ApplyDepthLayout();
        }

        private void EnsureInteractionReferences()
        {
            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
                if (interactionCamera == null)
                {
                    interactionCamera = FindFirstObjectByType<Camera>();
                }
            }

            if (neuronPanelView == null)
            {
                neuronPanelView = FindFirstObjectByType<RoutineNeuronPanelView>();
                if (neuronPanelView == null && Application.isPlaying)
                {
                    neuronPanelView = CreateRuntimeNeuronPanelView();
                }
            }

            if (neuronPanelView != null)
            {
                neuronPanelView.Hide();
            }
        }

        private RoutineNeuronPanelView CreateRuntimeNeuronPanelView()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            var panelGo = new GameObject("RoutineNeuronPanel", typeof(RectTransform), typeof(Image), typeof(RoutineNeuronPanelView));
            panelGo.transform.SetParent(canvas.transform, false);

            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-20f, -20f);
            panelRect.sizeDelta = new Vector2(620f, 210f);

            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.04f, 0.08f, 0.14f, 0.86f);

            var titleText = CreateNeuronPanelText(panelGo.transform, "TitleText", new Vector2(16f, -12f), 20);
            var intentText = CreateNeuronPanelText(panelGo.transform, "IntentText", new Vector2(16f, -50f), 16);
            var reasonText = CreateNeuronPanelText(panelGo.transform, "ReasonText", new Vector2(16f, -82f), 16);
            var conditionText = CreateNeuronPanelText(panelGo.transform, "ConditionText", new Vector2(16f, -114f), 16);
            var gaugeText = CreateNeuronPanelText(panelGo.transform, "GaugeText", new Vector2(16f, -146f), 16);
            gaugeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            gaugeText.verticalOverflow = VerticalWrapMode.Overflow;

            var view = panelGo.GetComponent<RoutineNeuronPanelView>();
            view.Configure(panelGo, titleText, intentText, reasonText, conditionText, gaugeText);
            view.Hide();
            return view;
        }

        private static Text CreateNeuronPanelText(Transform parent, string name, Vector2 anchoredPosition, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-24f, 28f);

            var text = go.GetComponent<Text>();
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
            text.color = new Color(0.92f, 0.96f, 1f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = string.Empty;
            return text;
        }

        private void EnsureZoneWidthsForCharacterSeparation()
        {
            if (!autoExpandZoneWidthForSeparation)
            {
                return;
            }

            var requiredOccupants = Mathf.Max(1, characters.Count);
            EnsureZoneWidthForCharacterSeparation(missionZone, requiredOccupants);
            EnsureZoneWidthForCharacterSeparation(cafeteriaZone, requiredOccupants);
            EnsureZoneWidthForCharacterSeparation(sleepZone, requiredOccupants);
        }

        private void EnsureZoneWidthForCharacterSeparation(RoutineZoneAnchor zone, int occupants)
        {
            if (zone == null || occupants <= 1)
            {
                return;
            }

            var minGap = Mathf.Max(0.5f, minimumCharacterSeparation);
            var margin = 0.08f;
            var requiredWidth = ((occupants - 1) * minGap) + (margin * 2f);
            var scale = zone.transform.localScale;
            var currentWidth = Mathf.Abs(scale.x);
            if (currentWidth >= requiredWidth)
            {
                return;
            }

            scale.x = Mathf.Sign(scale.x == 0f ? 1f : scale.x) * requiredWidth;
            zone.transform.localScale = scale;
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

            if (!_actionJobRequirements.TryGetValue(action, out var requirement))
            {
                requirement = null;
            }

            var hasRequiredItems = requirement == null || requirement.IsSatisfied(_officeItems, binding.actor.name);

            if (action == RoutineActionType.Sleep)
            {
                return zone.HasTag(ZoneTagNeedSleep) && zone.Contains(binding.actor.position) && hasRequiredItems;
            }

            if (action == RoutineActionType.Eat ||
                action == RoutineActionType.Breakfast ||
                action == RoutineActionType.Lunch ||
                action == RoutineActionType.Dinner)
            {
                return zone.HasTag(ZoneTagNeedHunger) && zone.Contains(binding.actor.position) && hasRequiredItems;
            }

            return zone.HasTag(ZoneTagMission) && zone.Contains(binding.actor.position) && hasRequiredItems;
        }

        private void EnsureOfficeItemsAndJobBindings()
        {
            if (_actionJobRequirements.Count == 0)
            {
                var workRequirement = new NeedRequirement("work", JobZoneWork, new[] { "desk", "computer" });
                var eatRequirement = new NeedRequirement("eat", JobZoneEat, new[] { "table", "tray", "cup" });
                var sleepRequirement = new NeedRequirement("sleep", JobZoneSleep, new[] { "bed", "pillow", "blanket" });
                _actionJobRequirements[RoutineActionType.Mission] = workRequirement;
                _actionJobRequirements[RoutineActionType.Eat] = eatRequirement;
                _actionJobRequirements[RoutineActionType.Breakfast] = eatRequirement;
                _actionJobRequirements[RoutineActionType.Lunch] = eatRequirement;
                _actionJobRequirements[RoutineActionType.Dinner] = eatRequirement;
                _actionJobRequirements[RoutineActionType.Sleep] = sleepRequirement;
            }

            if (_officeItems.Count > 0)
            {
                return;
            }

            var owners = new List<string>();
            for (int i = 0; i < characters.Count; i++)
            {
                var actor = characters[i].actor;
                if (actor != null)
                {
                    owners.Add(actor.name);
                }
            }

            _officeItems.AddRange(OfficeItemFactory.GenerateOfficeItems(new System.Random(17), 12, owners));
            var itemDump = new StringBuilder();
            for (int i = 0; i < _officeItems.Count; i++)
            {
                itemDump.AppendLine(_officeItems[i].BuildInspectorSummary());
            }

            Debug.Log("[RoutineMVP] Generated office items for job system:\n" + itemDump);
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
            binding.hasLockedZonePosition = false;
            binding.lockedZoneKey = null;
            binding.lockedZonePosition = Vector3.zero;
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
            CreateCharacter2D(charactersRoot, "Character_A", new Vector2(-1.2f, 0f), new Color(0.2f, 0.85f, 1f, 1f));
            CreateCharacter2D(charactersRoot, "Character_B", new Vector2(0f, 0f), new Color(0.4f, 1f, 0.4f, 1f));
            CreateCharacter2D(charactersRoot, "Character_C", new Vector2(1.2f, 0f), new Color(1f, 0.7f, 0.25f, 1f));

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

            EnsureZoneDesks();
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

        private void EnsureZoneDesks()
        {
            var zones = GetManagedZones();
            var validKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null)
                {
                    continue;
                }

                var zoneKey = GetZoneReservationKey(zone);
                if (string.IsNullOrWhiteSpace(zoneKey))
                {
                    continue;
                }

                validKeys.Add(zoneKey);
                EnsureZoneDesks(zone, zoneKey);
            }

            var keys = new List<string>(_zoneDeskPositions.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                if (!validKeys.Contains(keys[i]))
                {
                    _zoneDeskPositions.Remove(keys[i]);
                }
            }

            var preferredKeys = new List<string>(_zonePreferredDeskPositions.Keys);
            for (int i = 0; i < preferredKeys.Count; i++)
            {
                var zoneKey = preferredKeys[i];
                if (!validKeys.Contains(zoneKey))
                {
                    _zonePreferredDeskPositions.Remove(zoneKey);
                    continue;
                }

                if (!_zonePreferredDeskPositions.TryGetValue(zoneKey, out var preferredMap)
                    || !_zoneDeskPositions.TryGetValue(zoneKey, out var desks)
                    || preferredMap == null
                    || desks == null)
                {
                    continue;
                }

                var bindings = new List<RoutineCharacterBinding>(preferredMap.Keys);
                for (int j = 0; j < bindings.Count; j++)
                {
                    var binding = bindings[j];
                    if (binding == null || binding.actor == null || !ContainsDeskPosition(desks, preferredMap[binding]))
                    {
                        preferredMap.Remove(binding);
                    }
                }

                if (preferredMap.Count == 0)
                {
                    _zonePreferredDeskPositions.Remove(zoneKey);
                }
            }
        }

        private List<RoutineZoneAnchor> GetManagedZones()
        {
            var zones = new List<RoutineZoneAnchor>(3);
            AddZoneIfMissing(zones, missionZone);
            AddZoneIfMissing(zones, cafeteriaZone);
            AddZoneIfMissing(zones, sleepZone);

            for (int i = 0; i < _zoneAnchors.Count; i++)
            {
                AddZoneIfMissing(zones, _zoneAnchors[i]);
            }

            return zones;
        }

        private static void AddZoneIfMissing(List<RoutineZoneAnchor> zones, RoutineZoneAnchor zone)
        {
            if (zone == null || zones.Contains(zone))
            {
                return;
            }

            zones.Add(zone);
        }

        private void EnsureZoneDesks(RoutineZoneAnchor zone, string zoneKey)
        {
            var deskRoot = zone.transform.Find(DeskRootName);
            if (deskRoot == null)
            {
                var rootGo = new GameObject(DeskRootName);
                rootGo.transform.SetParent(zone.transform, false);
                deskRoot = rootGo.transform;
            }

            var deskPositions = CollectDeskPositionsFromChildren(deskRoot);
            if (deskPositions.Count == 0)
            {
                var deskCount = UnityEngine.Random.Range(Mathf.Max(1, minDeskCountPerZone), Mathf.Max(Mathf.Max(1, minDeskCountPerZone), maxDeskCountPerZone) + 1);
                deskPositions = GenerateDeskPositions(zone, deskCount);
                for (int i = 0; i < deskPositions.Count; i++)
                {
                    CreateDeskObject(deskRoot, i, deskPositions[i]);
                }
            }

            for (int i = 0; i < deskPositions.Count; i++)
            {
                deskPositions[i] = WithCharacterDepth(deskPositions[i]);
            }

            _zoneDeskPositions[zoneKey] = deskPositions;
        }

        private List<Vector3> CollectDeskPositionsFromChildren(Transform deskRoot)
        {
            var deskPositions = new List<Vector3>();
            for (int i = 0; i < deskRoot.childCount; i++)
            {
                var child = deskRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                deskPositions.Add(WithCharacterDepth(child.position));
                EnsureSpriteRenderer(child.gameObject, new Color(0.95f, 0.95f, 0.95f, 0.95f), deskVisualScale, true);
            }

            return deskPositions;
        }

        private List<Vector3> GenerateDeskPositions(RoutineZoneAnchor zone, int deskCount)
        {
            var desks = new List<Vector3>(Mathf.Max(1, deskCount));
            var bounds = zone.GetComponent<BoxCollider2D>()?.bounds;
            if (bounds == null)
            {
                desks.Add(WithCharacterDepth(zone.Position));
                return desks;
            }

            var b = bounds.Value;
            var inset = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.y) * deskInsetRatio, 0.02f, 0.4f);
            var minX = b.min.x + inset;
            var maxX = b.max.x - inset;
            var minY = b.min.y + inset;
            var maxY = b.max.y - inset;
            if (minX > maxX)
            {
                minX = b.center.x;
                maxX = b.center.x;
            }

            if (minY > maxY)
            {
                minY = b.center.y;
                maxY = b.center.y;
            }

            var zoneWidth = Mathf.Max(0.001f, maxX - minX);
            var zoneHeight = Mathf.Max(0.001f, maxY - minY);
            var minDeskSpacing = Mathf.Max(0.06f, Mathf.Min(zoneWidth, zoneHeight) * deskMinSpacingRatio);
            var minDeskSpacingSqr = minDeskSpacing * minDeskSpacing;
            var attempts = Mathf.Max(24, deskCount * 24);

            for (int i = 0; i < attempts && desks.Count < deskCount; i++)
            {
                var x = UnityEngine.Random.Range(minX, maxX);
                var y = UnityEngine.Random.Range(minY, maxY);

                var pos = WithCharacterDepth(new Vector3(x, y, characterDepthZ));
                if (!zone.Contains(pos))
                {
                    continue;
                }

                var overlaps = false;
                for (int j = 0; j < desks.Count; j++)
                {
                    if ((desks[j] - pos).sqrMagnitude < minDeskSpacingSqr)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    desks.Add(pos);
                }
            }

            // Fallback: if random sampling couldn't place enough desks, fill remaining with center-biased samples.
            for (int i = desks.Count; i < deskCount; i++)
            {
                var t = (i + 0.5f) / deskCount;
                var x = Mathf.Lerp(minX, maxX, t);
                var y = (i % 2 == 0) ? Mathf.Lerp(minY, maxY, 0.3f) : Mathf.Lerp(minY, maxY, 0.7f);
                var pos = WithCharacterDepth(new Vector3(x, y, characterDepthZ));
                if (!zone.Contains(pos))
                {
                    pos = WithCharacterDepth(zone.Position);
                }

                desks.Add(pos);
            }

            return desks;
        }

        private void CreateDeskObject(Transform deskRoot, int index, Vector3 worldPosition)
        {
            var deskGo = new GameObject(string.Format(CultureInfo.InvariantCulture, "Desk_{0:D2}", index + 1));
            deskGo.transform.SetParent(deskRoot, false);
            deskGo.transform.position = WithCharacterDepth(worldPosition);
            deskGo.transform.localScale = new Vector3(Mathf.Abs(deskVisualScale.x), Mathf.Abs(deskVisualScale.y), 1f);
            EnsureSpriteRenderer(deskGo, new Color(0.95f, 0.95f, 0.95f, 0.95f), deskVisualScale, true);
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
            position.y = 0f;
            position.z = characterDepthZ;
            actor.position = position;
        }

        private Vector3 WithCharacterDepth(Vector3 position)
        {
            position.y = 0f;
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
        private void HandleCharacterSelectionInput()
        {
            if (!TryGetPointerDownScreenPosition(out var pointerScreenPosition))
            {
                return;
            }

            var cameraToUse = interactionCamera != null ? interactionCamera : Camera.main;
            if (cameraToUse == null)
            {
                return;
            }

            var worldPoint = cameraToUse.ScreenToWorldPoint(new Vector3(pointerScreenPosition.x, pointerScreenPosition.y, Mathf.Abs(cameraToUse.transform.position.z)));
            var hit = Physics2D.OverlapPoint(new Vector2(worldPoint.x, worldPoint.y));
            if (hit == null)
            {
                _selectedCharacter = null;
                UpdateNeuronPanel();
                return;
            }

            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null)
                {
                    continue;
                }

                if (hit.transform == binding.actor || hit.transform.IsChildOf(binding.actor))
                {
                    _selectedCharacter = binding;
                    UpdateNeuronPanel();
                    return;
                }
            }
        }

        private static bool TryGetPointerDownScreenPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryGetInputSystemPointerDownScreenPosition(out screenPosition))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif

            screenPosition = default;
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryGetInputSystemPointerDownScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = default;
            var mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            if (mouseType != null)
            {
                var mouse = mouseType.GetProperty("current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (mouse != null &&
                    TryReadBoolProperty(TryReadProperty(mouse, "leftButton"), "wasPressedThisFrame", out var mousePressed) &&
                    mousePressed &&
                    TryReadVector2FromControl(mouse, "position", out screenPosition))
                {
                    return true;
                }
            }

            var touchType = Type.GetType("UnityEngine.InputSystem.Touchscreen, Unity.InputSystem");
            if (touchType == null)
            {
                return false;
            }

            var touchscreen = touchType.GetProperty("current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (touchscreen == null)
            {
                return false;
            }

            var primaryTouch = TryReadProperty(touchscreen, "primaryTouch");
            if (primaryTouch == null)
            {
                return false;
            }

            var pressControl = TryReadProperty(primaryTouch, "press");
            if (!TryReadBoolProperty(pressControl, "wasPressedThisFrame", out var touchPressed) || !touchPressed)
            {
                return false;
            }

            return TryReadVector2FromControl(primaryTouch, "position", out screenPosition);
        }

        private static object TryReadProperty(object source, string propertyName)
        {
            return source?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(source);
        }

        private static bool TryReadBoolProperty(object source, string propertyName, out bool value)
        {
            value = false;
            if (source == null)
            {
                return false;
            }

            var prop = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || prop.PropertyType != typeof(bool))
            {
                return false;
            }

            value = (bool)prop.GetValue(source);
            return true;
        }

        private static bool TryReadVector2FromControl(object deviceOrTouch, string controlPropertyName, out Vector2 value)
        {
            value = default;
            var control = TryReadProperty(deviceOrTouch, controlPropertyName);
            if (control == null)
            {
                return false;
            }

            var readValueMethod = control.GetType().GetMethod("ReadValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (readValueMethod == null || readValueMethod.ReturnType != typeof(Vector2))
            {
                return false;
            }

            value = (Vector2)readValueMethod.Invoke(control, null);
            return true;
        }
#endif

        private void UpdateNeuronPanel()
        {
            if (neuronPanelView == null)
            {
                return;
            }

            if (_selectedCharacter == null || !_latestNeuronSnapshots.TryGetValue(_selectedCharacter, out var snapshot))
            {
                neuronPanelView.Hide();
                return;
            }

            var viewModel = RoutineNeuronPanelViewModel.FromSnapshot(snapshot, _selectedCharacter.currentAction.ToString(), _selectedCharacter.intendedAction.ToString());
            neuronPanelView.Render(viewModel);
        }

        private static RoutineActionType ToRoutineAction(CharacterNeuronIntent intent)
        {
            return intent == CharacterNeuronIntent.Eat
                ? RoutineActionType.Eat
                : intent == CharacterNeuronIntent.Sleep
                    ? RoutineActionType.Sleep
                    : RoutineActionType.Mission;
        }

        private static CharacterNeuronIntent ToNeuronIntent(RoutineActionType action)
        {
            return action == RoutineActionType.Sleep
                ? CharacterNeuronIntent.Sleep
                : IsMealAction(action)
                    ? CharacterNeuronIntent.Eat
                    : CharacterNeuronIntent.Work;
        }

        private static void ApplyNeedLatchAfterAction(RoutineCharacterBinding binding, RoutineActionType appliedAction, bool canResolveNeed)
        {
            if (binding == null)
            {
                return;
            }

            if (!binding.hasLatchedNeedAction)
            {
                return;
            }

            if (!IsNeedAction(binding.latchedNeedAction))
            {
                binding.hasLatchedNeedAction = false;
                return;
            }

            if (binding.latchedNeedAction != appliedAction || !canResolveNeed)
            {
                return;
            }

            if (IsMealAction(binding.latchedNeedAction))
            {
                if (binding.hunger > binding.hungerThreshold)
                {
                    binding.hasLatchedNeedAction = false;
                }
                return;
            }

            if (binding.latchedNeedAction == RoutineActionType.Sleep && binding.sleep > binding.sleepThreshold && binding.stress > binding.stressThreshold)
            {
                binding.hasLatchedNeedAction = false;
            }
        }

    }
}
