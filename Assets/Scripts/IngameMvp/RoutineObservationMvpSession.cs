using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using ProjectW.IngameCore;
using ProjectW.IngameCore.Config;
using ProjectW.IngameCore.Meta;
using ProjectW.IngameCore.Simulation;
using ProjectW.IngameCore.StateMachine;
using ProjectW.Outgame;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
        [NonSerialized] public float mood = 55f;
        [NonSerialized] public Dictionary<string, float> knowledgeMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        [NonSerialized] public string selfTalkText;
        [NonSerialized] public TextMesh selfTalkTextMesh;
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
        private const float TargetLineY = 0.35f;
        private const float ZoneActionSpacing = 0.9f;
        private const float DefaultCharacterSpriteSize = 1f;
        private const float DefaultZoneAlpha = 0.4f;
        private const float TargetZoneSize = 4f;
        private const float MinZoneSizeNearTarget = 3.2f;
        private const float MaxZoneSizeNearTarget = 4.8f;
        private const float DeskActionArrivalThresholdSqr = 0.01f;
        private const float SelfTalkHeight = 1.35f;
        private const float SelfTalkTextSize = 0.14f;
        private const string ZoneObjectRootName = "ObjectSlots";
        private const string ZoneTagMission = "zone.mission";
        private const string ZoneTagNeedHunger = "need.hunger";
        private const string ZoneTagNeedSleep = "need.sleep";
        private const string JobZoneWork = "workzone";
        private const string JobZoneEat = "eatzone";
        private const string JobZoneSleep = "sleepzone";
        private const float QualityCompletionThreshold = 70f;
        private const float QualityReworkThreshold = 45f;
        private const string DefaultCharacterAnimatorControllerPath = "AnimatorControllers/routine_character_default";
        private const int DefaultWorldSeed = 17;
        private static readonly Vector2 UiReferenceResolution = new Vector2(1280f, 720f);

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
        [SerializeField] private Text goalText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text situationText;
        [SerializeField] private Text chronicleText;
        [SerializeField] private Text currentTimeText;
        [SerializeField] private RoutineNeuronPanelView neuronPanelView;
        [SerializeField] private RoutineObjectInfoPanelView objectInfoPanelView;
        [SerializeField] private Camera interactionCamera;

        [Header("Dashboard")]
        [SerializeField] private bool autoCreateDashboardUi = false;
        [SerializeField] private string dashboardGoalTitle = "Routine Stability";
        [SerializeField] private int dashboardMissionGoalTicks = 100;

        [Header("Characters")]
        [SerializeField] private List<RoutineCharacterBinding> characters = new List<RoutineCharacterBinding>();

        [Header("Visual Resources")]
        [SerializeField] private RoutineVisualResourceSet visualResources = new RoutineVisualResourceSet();
        [SerializeField] private RuntimeAnimatorController characterAnimatorController;
        [SerializeField, Min(0.5f)] private float zoneSpriteAnimationFps = 4f;

        [Header("Procedural Rules")]
        [SerializeField] private ZoneGenerationRuleSet zoneGenerationRuleSet;
        [SerializeField] private TaskGenerationRuleSet taskGenerationRuleSet;
        [SerializeField] private ScriptableObject dynamicGenerationRuleSet;
        [SerializeField] private ScriptableObject difficultyProfileSet;
        [SerializeField] private ScriptableObject spawnLayoutRuleSet;
        [SerializeField] private Text taskUiBinding;
        [SerializeField] private int fallbackWorldSeed = DefaultWorldSeed;
        [SerializeField] private bool dynamicWorldEnabled = true;

        [Header("Generation")]
        [SerializeField] private bool persistGeneratedObjectsInScene = true;
        [SerializeField] private bool useCubeDummyVisual = true;

        [Header("2D World")]
        [SerializeField] private bool use2DWorld = true;
        [SerializeField] private bool disableLegacy3DRenderers = true;
        [SerializeField] private float zoneGap = 2f;
        [SerializeField, Min(1f)] private float zoneSizeMultiplier = 4f;
        [SerializeField, Range(0f, 0.8f)] private float zoneSizeRandomJitter = 0.15f;
        [SerializeField] private Vector2 minimumZoneSize = new Vector2(1f, 1f);
        [SerializeField] private Vector2 deskVisualScale = new Vector2(0.95f, 0.95f);
        [SerializeField] private Vector2 objectVisualScale = new Vector2(0.55f, 0.55f);
        [SerializeField, Min(0.5f)] private float minimumCharacterSeparation = 0.5f;
        [SerializeField] private bool autoExpandZoneWidthForSeparation = true;

        [Header("Depth Layout")]
        [SerializeField] private bool enforceDepthLayout = true;
        [SerializeField] private float zoneDepthZ = 0f;
        [SerializeField] private float objectDepthZ = -1f;
        [SerializeField] private float characterDepthZ = -2f;

        [Header("Debug View")]
        [SerializeField] private bool showDebugOnGui = false;
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
        private readonly Dictionary<string, List<WorldItem>> _officeItemsByZoneKey = new Dictionary<string, List<WorldItem>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _boundCloseButtons = new HashSet<int>();
        private readonly Dictionary<string, CharacterAptitudeProfile> _characterAptitudeByActorName = new Dictionary<string, CharacterAptitudeProfile>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _affinityScores = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private RoutineCharacterBinding _selectedCharacter;
        private RoutineCharacterBinding _pinnedNeuronCharacter;
        private RoutineInspectableWorldObject _selectedWorldObject;
        private bool _hasDeskTemplateScale;
        private Vector2 _templateDeskScale;
        private bool _hasComputerTemplateScale;
        private Vector2 _templateComputerScale;
        private bool _hasCupTemplateScale;
        private Vector2 _templateCupScale;
        private PanelKind _lastOpenedPanel = PanelKind.None;
        private bool _didWarnMissingCharacterAnimatorController;
        private CoreLoopState _coreLoopCurrentState = CoreLoopState.Plan;
        private int _coreLoopStateStartedTick;
        private readonly Dictionary<CoreLoopState, TransitionDecision> _coreLoopStateResults = new Dictionary<CoreLoopState, TransitionDecision>();
        private readonly List<string> _coreLoopEventLog = new List<string>();
        private IngameCsvConfigSet _runtimeConfigSet;
        private bool _isReadOnlyRecoveryMode;
        private readonly SessionMetaState _sessionMetaState = new SessionMetaState();
        private readonly List<ChronicleEvent> _cycleChronicleEvents = new List<ChronicleEvent>();
        private ChronicleSummary _latestChronicleSummary;
        private CycleKpiSnapshot _latestCycleKpi = new CycleKpiSnapshot();
        private readonly PlaytestSurveyForm _playtestSurveyForm = new PlaytestSurveyForm();
        private int _cycleInterventionOfferCount;
        private int _cycleInterventionUseCount;
        private int _cycleInterventionOutcomeChangedCount;
        private int _lastDayIndex = -1;
        private int _rememberedEventsSelfScore = 3;
        private readonly InterventionQueueService _interventionQueueService = new InterventionQueueService();
        private bool _sessionEndRequested;
        private SessionEndResult _latestSessionEndResult;
        private SnapshotPersistenceResult _latestPersistenceResult;
        private bool _sessionEndEventRaised;
        private bool _outgameSetupApplied;
        private int _resolvedWorldSeed = DefaultWorldSeed;
        private SessionDifficulty _selectedDifficulty = SessionDifficulty.Normal;
        private PriorityPair _selectedPriorityPair = new PriorityPair(WorkType.Routine, WorkType.Labor);
        private int _selectedCharacterCount = 3;
        private DynamicTaskModel _currentDynamicTask;
        private int _lastDynamicTaskSeedTick = -1;

        private enum PanelKind
        {
            None,
            Neuron,
            Object
        }

        public IReadOnlyList<RoutineCharacterBinding> Characters => characters;
        public int AbsoluteTick => _absoluteTick;
        public CoreLoopState CoreLoopCurrentState => _coreLoopCurrentState;
        public int CoreLoopStateStartedTick => _coreLoopStateStartedTick;
        public IReadOnlyDictionary<CoreLoopState, TransitionDecision> CoreLoopStateResults => _coreLoopStateResults;
        public IReadOnlyList<string> CoreLoopEventLog => _coreLoopEventLog;
        public ChronicleSummary LatestChronicleSummary => _latestChronicleSummary;
        public CycleKpiSnapshot LatestCycleKpi => _latestCycleKpi;
        public IReadOnlyList<MetaChoice> AvailableMetaChoices => MetaChoiceCatalog.BuildDefaultChoices();
        public event Action<SessionResultSummary> SessionEnded;

        public void SetInterventionVisibility(int pendingCount, int lastAppliedTick, string recentRejectedReason)
        {
            _cycleInterventionOfferCount += Mathf.Max(0, pendingCount);
            if (lastAppliedTick > _lastAppliedInterventionTick)
            {
                _cycleInterventionUseCount += 1;
            }

            _pendingInterventionCount = Mathf.Max(0, pendingCount);
            _lastAppliedInterventionTick = Mathf.Max(-1, lastAppliedTick);
            _recentRejectedInterventionReason = string.IsNullOrWhiteSpace(recentRejectedReason)
                ? "None"
                : recentRejectedReason.Trim();
        }

        public void SelectMetaChoice(string choiceId)
        {
            var choice = MetaChoiceCatalog.ResolveOrDefault(choiceId);
            _sessionMetaState.ApplyChoice(choice);
            if (_runtimeConfigSet != null)
            {
                _sessionMetaState.ApplyToSessionConfig(_runtimeConfigSet.SessionConfig);
                tickIntervalSeconds = Mathf.Max(0.01f, _runtimeConfigSet.SessionConfig.TickSeconds);
            }

            SetDashboardContext("MetaChoice", choice.Label);
        }

        public void SetRememberedEventsSelfScore(int score1To5)
        {
            _rememberedEventsSelfScore = Mathf.Clamp(score1To5, 1, 5);
        }

        public void SetPlaytestSurveyResponse(int questionIndex, int score1To5)
        {
            _playtestSurveyForm.SetResponse(questionIndex, score1To5);
            SetDashboardContext("PlaytestSurvey", _playtestSurveyForm.ToCompactText());
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

        public void ApplyOutgameSetup(OutgameSessionSetup setup)
        {
            if (_outgameSetupApplied)
            {
                return;
            }

            _outgameSetupApplied = true;
            var resolvedSetup = setup ?? OutgameSessionSetup.CreateDefault();
            _resolvedWorldSeed = resolvedSetup.ResolveWorldSeed(fallbackWorldSeed);
            _selectedDifficulty = resolvedSetup.SelectedDifficulty;
            _selectedPriorityPair = resolvedSetup.PriorityPair;
            var selectedIds = new HashSet<string>(
                (resolvedSetup.SelectedCharacterIds != null && resolvedSetup.SelectedCharacterIds.Count > 0)
                    ? resolvedSetup.SelectedCharacterIds
                    : OutgameSessionSetup.CreateDefault().SelectedCharacterIds,
                StringComparer.Ordinal);
            _selectedCharacterCount = Mathf.Max(1, resolvedSetup.SelectedCharacterCount > 0 ? resolvedSetup.SelectedCharacterCount : selectedIds.Count);

            var hasMatchingSelection = false;
            for (int i = 0; i < characters.Count; i++)
            {
                var actor = characters[i]?.actor;
                if (actor != null && selectedIds.Contains(actor.name))
                {
                    hasMatchingSelection = true;
                    break;
                }
            }

            if (!hasMatchingSelection)
            {
                selectedIds.Clear();
                for (int i = 0; i < characters.Count; i++)
                {
                    var actor = characters[i]?.actor;
                    if (actor != null)
                    {
                        selectedIds.Add(actor.name);
                    }
                }
            }

            for (int i = characters.Count - 1; i >= 0; i--)
            {
                var binding = characters[i];
                if (binding?.actor == null)
                {
                    continue;
                }

                if (selectedIds.Contains(binding.actor.name))
                {
                    binding.actor.gameObject.SetActive(true);
                    continue;
                }

                binding.actor.gameObject.SetActive(false);
                characters.RemoveAt(i);
            }

            dashboardMissionGoalTicks = ResolveMissionGoalTicks(resolvedSetup.InitialMissionType);
            var safety = Mathf.Clamp(resolvedSetup.SafetyPriority, 0, 100);
            var resource = Mathf.Clamp(resolvedSetup.ResourcePriority, 0, 100);
            var bias = Mathf.Clamp((safety - resource) / 100f, -1f, 1f);
            ApplyPriorityBiasToCharacters(bias);

            SetDashboardContext("InitialMission", resolvedSetup.InitialMissionType.ToString());
            SetDashboardContext("Priority", $"R:{resource}, S:{safety}");
            SetDashboardContext("SelectedCharacters", string.Join(",", selectedIds));
            SetDashboardContext("WorldSeed", _resolvedWorldSeed.ToString(CultureInfo.InvariantCulture));
            SetDashboardContext("Difficulty", _selectedDifficulty.ToString());
            SetDashboardContext("PriorityPair", $"{_selectedPriorityPair.PrimaryWorkType}>{_selectedPriorityPair.SecondaryWorkType}");
            RebuildDynamicWorldIfEnabled();
            ApplyPriorityBiasToCharacters(bias);
            ResetProceduralGenerationState();
            EnsureOfficeItemsAndJobBindings();
        }

        private void ApplyPriorityBiasToCharacters(float bias)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                binding.hungerThreshold = Mathf.Clamp(binding.hungerThreshold + (bias * 10f), 0f, GaugeMax);
                binding.sleepThreshold = Mathf.Clamp(binding.sleepThreshold + (bias * 10f), 0f, GaugeMax);
                binding.stressThreshold = Mathf.Clamp(binding.stressThreshold + (bias * 10f), 0f, GaugeMax);
            }
        }

        private void Awake()
        {
            Application.runInBackground = true;
            showDebugOnGui = false;
            _resolvedWorldSeed = fallbackWorldSeed == 0 ? DefaultWorldSeed : fallbackWorldSeed;
            AutoBindSceneReferences();
            var cameraForInteraction = interactionCamera != null ? interactionCamera : (Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>());
            if (cameraForInteraction == null)
            {
                Debug.LogWarning("[RoutineMVP] No camera found in Awake. Camera pan/zoom controller fallback attach skipped.");
            }
            else
            {
                IngameCameraPanZoomController.EnsureAttachedTo(cameraForInteraction);
                if (interactionCamera == null)
                {
                    interactionCamera = cameraForInteraction;
                }
            }

            visualResources?.EnsureLoadedFromResources();
            EnsureCharacterAnimatorControllerLoaded();
            EnsureOfficeItemsAndJobBindings();
        }


        private void EnsureCharacterAnimatorControllerLoaded()
        {
            if (characterAnimatorController != null)
            {
                return;
            }

            characterAnimatorController = Resources.Load<RuntimeAnimatorController>(DefaultCharacterAnimatorControllerPath);
            if (characterAnimatorController == null)
            {
                if (!_didWarnMissingCharacterAnimatorController)
                {
                    Debug.LogWarning("[RoutineMVP] Character animator controller missing. Generate one via: ProjectW/Animation/Create Default Character Animator Controller");
                    _didWarnMissingCharacterAnimatorController = true;
                }
            }
            else
            {
                _didWarnMissingCharacterAnimatorController = false;
            }
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

            visualResources?.EnsureLoadedFromResources();
            AutoBindSceneReferencesEditorSafe();
            ApplyDepthLayout();
        }

        private void AutoBindSceneReferencesEditorSafe()
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

            EnsureDashboardUiReferences();
            EnsureInteractionReferences();
            EnsureCharacterBindingsFromRoot();
            EnsureZoneWidthsForCharacterSeparation();
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
            EnsureCharactersRootDepth();
            HandleCharacterSelectionInput();
            HandleObjectInfoCloseInput();
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
                UpdateCharacterAnimator(binding);
                UpdateTargetLineVisual(binding, i);
                UpdateSelfTalkTransform(binding);
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

            _sessionEndEventRaised = false;
            _sessionEndRequested = false;

            if (!TryBootstrapRuntimeConfig())
            {
                return;
            }

            if (_isReadOnlyRecoveryMode)
            {
                return;
            }

            EnsureOfficeItemsAndJobBindings();

            _loopCoroutine = StartCoroutine(RunLoop());
        }

        private bool TryBootstrapRuntimeConfig()
        {
            if (_runtimeConfigSet != null || _isReadOnlyRecoveryMode)
            {
                return true;
            }

            var bootstrap = new IngameCsvBootstrapService(new StreamingAssetsCsvConfigProvider(), new PersistentSnapshotRecoveryProbe());
            var result = bootstrap.Load();
            if (result.Success)
            {
                _runtimeConfigSet = result.ConfigSet;
                _sessionMetaState.ApplyToSessionConfig(_runtimeConfigSet.SessionConfig);
                tickIntervalSeconds = Mathf.Max(0.01f, result.ConfigSet.SessionConfig.TickSeconds);
                return true;
            }

            if (result.IsReadOnlyRecoveryMode)
            {
                _isReadOnlyRecoveryMode = true;
                SetDashboardContext("RecoveryMode", "ReadOnly");
                SetDashboardContext("StartupError", string.IsNullOrWhiteSpace(result.ErrorCode) ? "CSVError" : result.ErrorCode);
                Debug.LogWarning($"[RoutineMVP] CSV validation failed ({result.ErrorCode}). Session start blocked and switched to read-only recovery mode.");
                return false;
            }

            Debug.LogError($"[RoutineMVP] CSV validation failed ({result.ErrorCode}). Session start blocked.");
            return false;
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

        [ContextMenu("ProjectW/Recompose Scene From Mission Zone")]
        public void ComposeCafeteriaAndSleepFromMission()
        {
            AutoBindSceneReferences();
            if (missionZone == null)
            {
                Debug.LogWarning("[RoutineMVP] MissionZone is required to compose secondary zones.");
                return;
            }

            if (zonesRoot == null)
            {
                var zonesGo = GameObject.Find("Zones");
                if (zonesGo == null)
                {
                    zonesGo = new GameObject("Zones");
                }

                zonesRoot = zonesGo.transform;
            }

            RemoveSecondaryZones();
            _zoneLockedPositions.Clear();
            _zoneDeskPositions.Clear();
            _zonePreferredDeskPositions.Clear();

            var missionPos = missionZone.transform.position;
            var missionBaseSize = ResolveZoneBaseSize(missionZone);
            var missionSize = ResolveRandomizedLargeZoneSize(missionBaseSize, 101);
            ApplyZoneAreaSize(missionZone.gameObject, missionSize);
            var desiredOffset = Mathf.Max(Mathf.Abs(missionSize.x) + zoneGap, Mathf.Abs(missionSize.x) * 1.75f);
            var offsetX = ResolveZoneSpacingForViewport(missionSize, missionSize, missionSize, desiredOffset);
            var cafeteriaPos = new Vector3(missionPos.x - offsetX, missionPos.y - 0.08f, zoneDepthZ);
            var sleepPos = new Vector3(missionPos.x + offsetX, missionPos.y + 0.08f, zoneDepthZ);

            cafeteriaZone = EnsureOrUpdateSecondaryZone(
                "CafeteriaZone",
                "need.hunger",
                new[] { ZoneTagNeedHunger },
                cafeteriaPos,
                missionSize,
                new Color(0.12f, 0.53f, 0.9f, DefaultZoneAlpha),
                false);

            sleepZone = EnsureOrUpdateSecondaryZone(
                "SleepZone",
                "need.sleep",
                new[] { ZoneTagNeedSleep },
                sleepPos,
                missionSize,
                new Color(0.26f, 0.66f, 0.29f, DefaultZoneAlpha),
                false);

            BindZonesFromAnchors();
            EnsureZoneWidthsForCharacterSeparation();
            Ensure2DZones();
            ApplyDepthLayout();
            Debug.Log("[RoutineMVP] Composed Cafeteria/Sleep zones and required objects from Mission layout.");
        }

        [ContextMenu("ProjectW/Setup Scene From Current Layout")]
        public void SetupSceneFromCurrentLayout()
        {
            AutoBindSceneReferences();
            if (missionZone == null)
            {
                Debug.LogWarning("[RoutineMVP] MissionZone is required to setup scene from current layout.");
                return;
            }

            if (zonesRoot == null)
            {
                var zonesGo = GameObject.Find("Zones");
                if (zonesGo == null)
                {
                    zonesGo = new GameObject("Zones");
                }

                zonesRoot = zonesGo.transform;
            }

            var missionPos = missionZone.transform.position;
            var missionBaseSize = ResolveZoneBaseSize(missionZone);
            var missionSize = ResolveRandomizedLargeZoneSize(missionBaseSize, 101);
            ApplyZoneAreaSize(missionZone.gameObject, missionSize);
            var desiredOffset = Mathf.Max(Mathf.Abs(missionSize.x) + zoneGap, Mathf.Abs(missionSize.x) * 1.75f);
            var offsetX = ResolveZoneSpacingForViewport(missionSize, missionSize, missionSize, desiredOffset);
            var cafeteriaPos = new Vector3(missionPos.x - offsetX, missionPos.y - 0.08f, zoneDepthZ);
            var sleepPos = new Vector3(missionPos.x + offsetX, missionPos.y + 0.08f, zoneDepthZ);

            cafeteriaZone = EnsureOrUpdateSecondaryZone(
                "CafeteriaZone",
                "need.hunger",
                new[] { ZoneTagNeedHunger },
                cafeteriaPos,
                missionSize,
                new Color(0.12f, 0.53f, 0.9f, DefaultZoneAlpha),
                true);

            sleepZone = EnsureOrUpdateSecondaryZone(
                "SleepZone",
                "need.sleep",
                new[] { ZoneTagNeedSleep },
                sleepPos,
                missionSize,
                new Color(0.26f, 0.66f, 0.29f, DefaultZoneAlpha),
                true);

            BindZonesFromAnchors();
            EnsureDefaultCharactersExist();
            EnsureCharacterBindingsFromRoot();
            EnsureZoneWidthsForCharacterSeparation();
            Ensure2DZones();
            Ensure2DCharacters();
            ApplyDepthLayout();
            BakeGeneratedObjectsToScene();
            Debug.Log("[RoutineMVP] Setup scene from current layout completed.");
        }

        private void RemoveSecondaryZones()
        {
            if (zonesRoot == null)
            {
                return;
            }

            var toRemove = new List<GameObject>();
            for (int i = 0; i < zonesRoot.childCount; i++)
            {
                var child = zonesRoot.GetChild(i);
                if (child == null || child == missionZone?.transform)
                {
                    continue;
                }

                toRemove.Add(child.gameObject);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                SafeDestroyGameObject(toRemove[i]);
            }

            cafeteriaZone = null;
            sleepZone = null;
        }

        private RoutineZoneAnchor EnsureOrUpdateSecondaryZone(
            string objectName,
            string zoneId,
            string[] tags,
            Vector3 worldPosition,
            Vector2 areaSize,
            Color color,
            bool keepExistingTransform)
        {
            var existing = zonesRoot != null ? zonesRoot.Find(objectName) : null;
            GameObject zoneGo;
            if (existing == null)
            {
                zoneGo = new GameObject(objectName);
                zoneGo.transform.SetParent(zonesRoot, false);
            }
            else
            {
                zoneGo = existing.gameObject;
            }

            if (existing == null || !keepExistingTransform)
            {
                zoneGo.transform.position = new Vector3(worldPosition.x, worldPosition.y, zoneDepthZ);
                var scaled = ResolveRandomizedLargeZoneSize(areaSize, zoneId != null ? zoneId.GetHashCode() : 0);
                ApplyZoneAreaSize(zoneGo, scaled);
            }
            else
            {
                var p = zoneGo.transform.position;
                p.z = zoneDepthZ;
                zoneGo.transform.position = p;
            }

            EnsureSpriteRenderer(zoneGo, color, Vector2.one, false, visualResources != null ? visualResources.ResolveZoneSprite(zoneId) : null);
            EnsureZoneBoundary2D(zoneGo.GetComponent<RoutineZoneAnchor>() ?? zoneGo.AddComponent<RoutineZoneAnchor>());

            var anchor = zoneGo.GetComponent<RoutineZoneAnchor>();
            if (anchor == null)
            {
                anchor = zoneGo.AddComponent<RoutineZoneAnchor>();
            }

            anchor.SetZoneId(zoneId);
            anchor.SetTags(tags);
            anchor.RebindBoundaries();
            return anchor;
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
            EnsureDynamicTaskForTick(dayIndex, tickInHalfDay);

            var defaultAction = RoutineActionType.Mission;
            string zoneName = "MissingZone";
            ExecuteCoreLoopHandlers(hour, minute, ref defaultAction, ref zoneName);
            TrackCycleTelemetry(dayIndex, defaultAction);
            HandleCycleBoundary(dayIndex);

            string timeText = BuildTimeText(dayIndex, halfDayIndex, tickInHalfDay, totalMinutes);
            UpdatePerformanceScoresForTick();
            UpdateDashboardUi(dayIndex, halfDayIndex, tickInHalfDay, timeText);
            UpdateNeuronPanel();
            UpdateObjectInfoPanel();

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

        private void ExecuteCoreLoopHandlers(int hour, int minute, ref RoutineActionType defaultAction, ref string zoneName)
        {
            var resolveResult = new CoreLoopResolveResult
            {
                Action = defaultAction,
                ZoneName = zoneName
            };

            ExecuteState(CoreLoopState.Plan, CoreLoopState.Drop, HandlePlan);
            ExecuteState(CoreLoopState.Drop, CoreLoopState.AutoNarrative, HandleDrop);
            ExecuteState(CoreLoopState.AutoNarrative, CoreLoopState.CaptainIntervention, HandleAutoNarrative);
            ExecuteState(CoreLoopState.CaptainIntervention, CoreLoopState.NightDream, HandleCaptainIntervention);
            ExecuteState(CoreLoopState.NightDream, CoreLoopState.Resolve, HandleNightDream);
            if (_coreLoopCurrentState == CoreLoopState.Resolve)
            {
                var resolveGuard = HandleResolve(hour, minute, resolveResult);
                ExecuteState(CoreLoopState.Resolve, resolveResult.RequestedNextState, () => resolveGuard);
            }
            if (resolveResult.RequestedNextState == CoreLoopState.NextCycle)
            {
                ExecuteState(CoreLoopState.NextCycle, CoreLoopState.Plan, HandleNextCycle);
            }
            else if (_coreLoopCurrentState == CoreLoopState.SessionEnd)
            {
                StopSession();
            }

            defaultAction = resolveResult.Action;
            zoneName = resolveResult.ZoneName;
        }

        private bool ExecuteState(CoreLoopState expectedState, CoreLoopState requestedState, Func<bool> stateHandler)
        {
            if (_coreLoopCurrentState != expectedState)
            {
                return false;
            }

            bool guardResult = stateHandler();
            var executionContext = new CoreLoopExecutionContext(
                entryHook: nextState => { _coreLoopStateStartedTick = _absoluteTick; },
                updateHook: _ => { },
                exitHook: _ => { },
                guardHook: (_, __) => guardResult,
                onFailHook: (from, to, errorCode) =>
                {
                    if (!string.IsNullOrWhiteSpace(errorCode))
                    {
                        var logMessage = string.Format(CultureInfo.InvariantCulture,
                            "[CoreLoop][Tick:{0}] {1} -> {2} blocked ({3})",
                            _absoluteTick,
                            from,
                            to,
                            errorCode);
                        _coreLoopEventLog.Add(logMessage);
                        Debug.LogWarning(logMessage);
                    }
                });

            var decision = CoreLoopStateMachine.EvaluateTransition(_coreLoopCurrentState, requestedState, executionContext);
            _coreLoopStateResults[expectedState] = decision;
            _coreLoopCurrentState = decision.NextState;
            if (!decision.AppliedGuard && string.IsNullOrWhiteSpace(decision.ErrorCode))
            {
                _coreLoopEventLog.Add(string.Format(CultureInfo.InvariantCulture,
                    "[CoreLoop][Tick:{0}] {1} -> {2} blocked (E-STATE-101)",
                    _absoluteTick,
                    expectedState,
                    requestedState));
            }

            return decision.NextState != expectedState;
        }

        private bool HandlePlan()
        {
            return true;
        }

        private bool HandleDrop()
        {
            return true;
        }

        private bool HandleAutoNarrative()
        {
            return true;
        }

        private bool HandleCaptainIntervention()
        {
            var queue = _runtimeConfigSet?.InterventionCommands;
            var result = _interventionQueueService.ApplyInterventions(_absoluteTick, queue);
            SetInterventionVisibility(result.PendingCount, result.LastAppliedTick, result.RecentRejectedReason);

            for (int i = 0; i < result.AppliedCommandIds.Count; i++)
            {
                _coreLoopEventLog.Add($"[Intervention][Tick:{_absoluteTick}] applied:{result.AppliedCommandIds[i]}");
            }

            for (int i = 0; i < result.RejectedCommandIds.Count; i++)
            {
                _coreLoopEventLog.Add($"[Intervention][Tick:{_absoluteTick}] rejected:{result.RejectedCommandIds[i]} ({result.RecentRejectedReason})");
            }

            return true;
        }

        private bool HandleNightDream()
        {
            return true;
        }

        private bool HandleResolve(int hour, int minute, CoreLoopResolveResult resolveResult)
        {
            resolveResult.Action = ToRoutineAction(_neuronSystem.EvaluateRoutine(new RoutineNeuronContext("default", _absoluteTick, hour, minute, 0, 100f, 100f, 100f, 0f, 0f, 0f, false, CharacterNeuronIntent.Work)).ScheduledIntent);
            RoutineZoneAnchor defaultZone = ResolveZone(resolveResult.Action, Vector3.zero);
            resolveResult.ZoneName = defaultZone != null ? defaultZone.ZoneId : "MissingZone";
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

            ProcessRelationshipAndMood();

            if (!EvaluateSessionEndAndPersist())
            {
                resolveResult.RequestedNextState = CoreLoopState.Resolve;
                return false;
            }

            resolveResult.RequestedNextState = _sessionEndRequested
                ? CoreLoopState.SessionEnd
                : CoreLoopState.NextCycle;
            return true;
        }

        private bool EvaluateSessionEndAndPersist()
        {
            bool objectiveComplete = GetMissionProgressRatio() >= 1f;
            bool totalWipe = characters.Count > 0;
            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                var active = binding.hunger > 0.01f || binding.sleep > 0.01f || binding.stress > 0.01f;
                if (active)
                {
                    totalWipe = false;
                    break;
                }
            }

            bool emergencyExtract = _recentRejectedInterventionReason == "emergency_extract";
            _latestSessionEndResult = SessionEndResolver.ResolveSessionEnd(totalWipe, emergencyExtract, objectiveComplete);
            if (!_latestSessionEndResult.IsEnd)
            {
                _sessionEndRequested = false;
                return true;
            }

            var runtimeConfig = _runtimeConfigSet?.SessionConfig;
            var persistenceConfig = new ProjectW.IngameCore.SessionConfig(
                runtimeConfig != null ? runtimeConfig.MaxPersistRetry : 0,
                runtimeConfig != null ? runtimeConfig.PersistRetryBackoffMs : 0);

            var snapshot = BuildSessionSnapshot(_latestSessionEndResult);
            var writer = new JsonSnapshotWriter(maxSnapshotsPerSession: 3);
            var service = new SnapshotPersistenceService(writer);
            _latestPersistenceResult = service.PersistWithRetry(snapshot, persistenceConfig);

            if (!_latestPersistenceResult.Success)
            {
                SetDashboardContext("Persistence", $"{_latestPersistenceResult.State}:{_latestPersistenceResult.ErrorCode}");
                EmitSessionEndedOnce("PERSISTENCE_ERROR");
                StopSession();
                return false;
            }

            SetDashboardContext("Termination", _latestSessionEndResult.EndReasonCode);
            SetDashboardContext("Persistence", _latestPersistenceResult.State.ToString());
            EmitSessionEndedOnce();
            _sessionEndRequested = true;
            return true;
        }

        private void EmitSessionEndedOnce(string overrideTerminationReasonCode = null)
        {
            if (_sessionEndEventRaised)
            {
                return;
            }

            _sessionEndEventRaised = true;
            SessionEnded?.Invoke(BuildSessionResultSummary(overrideTerminationReasonCode));
        }

        private SessionResultSummary BuildSessionResultSummary(string overrideTerminationReasonCode = null)
        {
            var survivingCount = 0;
            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding == null || binding.actor == null || !binding.actor.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var active = binding.hunger > 0.01f || binding.sleep > 0.01f || binding.stress > 0.01f;
                if (active)
                {
                    survivingCount += 1;
                }
            }

            return new SessionResultSummary
            {
                TerminationReasonCode = string.IsNullOrWhiteSpace(overrideTerminationReasonCode)
                    ? _latestSessionEndResult.EndReasonCode
                    : overrideTerminationReasonCode,
                MissionProgressRatio = GetMissionProgressRatio(),
                SurvivingCharacterCount = survivingCount,
                TickIndex = _absoluteTick,
                SessionId = _runtimeConfigSet?.SessionConfig?.SessionId ?? "default"
            };
        }

        private SessionSnapshotDto BuildSessionSnapshot(SessionEndResult sessionEndResult)
        {
            var snapshot = new SessionSnapshotDto
            {
                SessionId = _runtimeConfigSet?.SessionConfig?.SessionId ?? "default",
                TickIndex = _absoluteTick,
                LoopState = _coreLoopCurrentState.ToString(),
                TerminationResultCode = sessionEndResult.EndReasonCode,
                LastAppliedTick = _lastAppliedInterventionTick
            };

            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null)
                {
                    continue;
                }

                snapshot.CharactersSnapshot.Add(new CharacterSnapshotDto
                {
                    CharacterId = binding.actor.name,
                    SerializedState = string.Format(CultureInfo.InvariantCulture, "H:{0:0.##}|S:{1:0.##}|T:{2:0.##}|M:{3}", binding.hunger, binding.sleep, binding.stress, binding.missionTicks)
                });
            }

            for (int i = 0; i < _coreLoopEventLog.Count; i++)
            {
                snapshot.EventLog.Add(new EventLogEntryDto
                {
                    EventCode = "CORE",
                    Message = _coreLoopEventLog[i],
                    TimestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                });
            }

            return snapshot;
        }

        private sealed class CoreLoopResolveResult
        {
            public RoutineActionType Action;
            public string ZoneName;
            public CoreLoopState RequestedNextState = CoreLoopState.NextCycle;
        }

        private bool HandleNextCycle()
        {
            return true;
        }

        private void HandleCycleBoundary(int dayIndex)
        {
            if (_lastDayIndex < 0)
            {
                _lastDayIndex = dayIndex;
                return;
            }

            if (dayIndex == _lastDayIndex)
            {
                return;
            }

            FinalizeCycleAndPrepareNext();
            _lastDayIndex = dayIndex;
        }

        private void FinalizeCycleAndPrepareNext()
        {
            _cycleChronicleEvents.Add(new ChronicleEvent
            {
                Category = ChronicleEventCategory.Termination,
                Description = "사이클 종료: Day 전환으로 자동 종료 처리되었습니다.",
                Severity = 5,
                TerminationReason = "DAY_ROLLOVER"
            });

            _latestChronicleSummary = ChronicleSummarizer.Summarize(_cycleChronicleEvents, 3, 5);
            _latestCycleKpi = new CycleKpiSnapshot
            {
                InterventionUsageRate = _cycleInterventionOfferCount <= 0
                    ? 0f
                    : Mathf.Clamp01(_cycleInterventionUseCount / (float)_cycleInterventionOfferCount),
                InterventionOutcomeChangeRate = _cycleInterventionUseCount <= 0
                    ? 0f
                    : Mathf.Clamp01(_cycleInterventionOutcomeChangedCount / (float)_cycleInterventionUseCount),
                RememberedEventSelfScore = Mathf.Clamp(_rememberedEventsSelfScore, 1, 5)
            };

            SetDashboardContext("Chronicle", _latestChronicleSummary.RawSummary);
            SetDashboardContext("KPI", string.Format(CultureInfo.InvariantCulture,
                "UseRate:{0:0.00}, ChangeRate:{1:0.00}, Recall:{2}",
                _latestCycleKpi.InterventionUsageRate,
                _latestCycleKpi.InterventionOutcomeChangeRate,
                _latestCycleKpi.RememberedEventSelfScore));
            SetDashboardContext("ReplayIntent", _playtestSurveyForm.WantsReplay() ? "YES" : "NO");

            var defaultMeta = MetaChoiceCatalog.BuildDefaultChoices()[0];
            SelectMetaChoice(defaultMeta.ChoiceId);

            for (int i = 0; i < characters.Count; i++)
            {
                characters[i].stress = Mathf.Clamp(characters[i].stress + _sessionMetaState.StressBiasDelta, 0f, GaugeMax);
            }

            _cycleChronicleEvents.Clear();
            _cycleInterventionOfferCount = 0;
            _cycleInterventionUseCount = 0;
            _cycleInterventionOutcomeChangedCount = 0;
        }

        private void TrackCycleTelemetry(int dayIndex, RoutineActionType defaultAction)
        {
            if (characters.Count >= 2)
            {
                var tension = Mathf.Abs(characters[0].stress - characters[1].stress);
                if (tension >= 20f)
                {
                    _cycleChronicleEvents.Add(new ChronicleEvent
                    {
                        Category = ChronicleEventCategory.RelationshipChange,
                        Severity = 3,
                        Description = string.Format(CultureInfo.InvariantCulture,
                            "관계 변화 감지: {0}↔{1} 긴장도 격차 {2:0}",
                            ResolveDisplayName(characters[0]),
                            ResolveDisplayName(characters[1]),
                            tension)
                    });
                }
            }

            if (_lastAppliedInterventionTick == _absoluteTick)
            {
                var changed = defaultAction != RoutineActionType.Mission;
                if (changed)
                {
                    _cycleInterventionOutcomeChangedCount += 1;
                }

                _cycleChronicleEvents.Add(new ChronicleEvent
                {
                    Category = ChronicleEventCategory.Intervention,
                    Severity = changed ? 4 : 2,
                    InterventionSucceeded = changed,
                    Description = changed
                        ? "개입 성공: 기본 미션 흐름에서 다른 행동으로 분기되었습니다."
                        : "개입 실패: 행동 흐름이 유지되었습니다."
                });
            }

            if (_absoluteTick % ticksPerHalfDay == 0)
            {
                _cycleChronicleEvents.Add(new ChronicleEvent
                {
                    Category = ChronicleEventCategory.Progress,
                    Severity = 2,
                    Description = string.Format(CultureInfo.InvariantCulture,
                        "Day {0} 중간 점검: 주요 루틴이 HalfDay 경계에 도달했습니다.", dayIndex)
                });
            }
        }

        private static string ResolveDisplayName(RoutineCharacterBinding binding)
        {
            if (!string.IsNullOrWhiteSpace(binding.displayName))
            {
                return binding.displayName;
            }

            return binding.actor != null ? binding.actor.name : "Unknown";
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

            var dx = actionTarget.x - binding.actor.position.x;
            var dy = actionTarget.y - binding.actor.position.y;
            var remainingDistance = Mathf.Sqrt((dx * dx) + (dy * dy));
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
                        ApplyDynamicSubtaskProgress(binding, 1);
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

        private void ApplyDynamicSubtaskProgress(RoutineCharacterBinding binding, int baseProgress)
        {
            if (_currentDynamicTask == null || binding == null || baseProgress <= 0)
            {
                return;
            }

            var subtask = _currentDynamicTask.FirstIncomplete();
            if (subtask == null)
            {
                return;
            }

            var actorName = binding.actor != null ? binding.actor.name : string.Empty;
            var aptitudeMultiplier = 1f;
            if (!string.IsNullOrWhiteSpace(actorName) && _characterAptitudeByActorName.TryGetValue(actorName, out var profile) && profile != null)
            {
                aptitudeMultiplier = Mathf.Max(0.5f, profile.GetMultiplier(subtask.WorkType));
            }

            var speedMultiplier = aptitudeMultiplier;
            speedMultiplier *= ResolveWorkEfficiencyFactor(binding);

            var knowledgeSatisfaction = CalculateKnowledgeSatisfaction(binding, subtask);
            var knowledgeSpeedMultiplier = Mathf.Lerp(0.7f, 1.25f, knowledgeSatisfaction);
            var knowledgeQualityMultiplier = Mathf.Lerp(0.75f, 1.3f, knowledgeSatisfaction);

            var delta = Mathf.Max(1, Mathf.RoundToInt(baseProgress * speedMultiplier * knowledgeSpeedMultiplier));
            subtask.Progress = Mathf.Min(subtask.RequiredWork, subtask.Progress + delta);
            var qualityAptitudeMultiplier = aptitudeMultiplier * knowledgeQualityMultiplier;
            subtask.QualityScore = Mathf.Clamp(CalculateQualityScore(binding, subtask, qualityAptitudeMultiplier), 0f, 100f);
            subtask.PerformanceScore = Mathf.Clamp((subtask.QualityScore * 0.6f) + (Mathf.Clamp01(subtask.Progress / (float)Mathf.Max(1, subtask.RequiredWork)) * 40f), 0f, 100f);

            if (subtask.Progress >= subtask.RequiredWork)
            {
                if (subtask.QualityScore < QualityReworkThreshold)
                {
                    var reworkUnits = Mathf.Max(1, Mathf.RoundToInt((QualityReworkThreshold - subtask.QualityScore) / 8f));
                    subtask.ReworkUnits += reworkUnits;
                    subtask.RequiredWork += reworkUnits;
                    _cycleChronicleEvents.Add(new ChronicleEvent
                    {
                        Category = ChronicleEventCategory.Progress,
                        Severity = 3,
                        Description = string.Format(
                            CultureInfo.InvariantCulture,
                            "재작업 발생: {0} 품질 {1:0.0}로 추가 작업 {2} 유닛이 배정되었습니다.",
                            ResolveDisplayName(binding),
                            subtask.QualityScore,
                            reworkUnits)
                    });
                }
                else
                {
                    subtask.CompletedTick = _absoluteTick;
                    if (TryGetSubtaskDeadline(subtask.SubtaskId, out var deadlineTick) && subtask.CompletedTick <= deadlineTick)
                    {
                        _cycleChronicleEvents.Add(new ChronicleEvent
                        {
                            Category = ChronicleEventCategory.Progress,
                            Severity = 4,
                            Description = string.Format(
                                CultureInfo.InvariantCulture,
                                "빠른 완료: {0}가 마감 {1} tick 이전({2})에 {3}를 완료했습니다.",
                                ResolveDisplayName(binding),
                                deadlineTick,
                                subtask.CompletedTick,
                                subtask.SubtaskId)
                        });
                    }

                    if (subtask.QualityScore >= QualityCompletionThreshold)
                    {
                        _cycleChronicleEvents.Add(new ChronicleEvent
                        {
                            Category = ChronicleEventCategory.Progress,
                            Severity = 5,
                            Description = string.Format(
                                CultureInfo.InvariantCulture,
                                "고품질 완료: {0}가 {1}를 품질 {2:0.0}으로 완료했습니다.",
                                ResolveDisplayName(binding),
                                subtask.SubtaskId,
                                subtask.QualityScore)
                        });
                    }
                }
            }

            ApplyDynamicTaskRequirementBinding();
            UpdateTaskDashboardContext();
        }

        private static float CalculateKnowledgeSatisfaction(RoutineCharacterBinding binding, DynamicSubtask subtask)
        {
            if (binding == null)
            {
                return 0f;
            }

            if (subtask?.RequiredKnowledgeKeys == null || subtask.RequiredKnowledgeKeys.Count == 0)
            {
                return 1f;
            }

            if (binding.knowledgeMap == null || binding.knowledgeMap.Count == 0)
            {
                return 0f;
            }

            var confidenceSum = 0f;
            var counted = 0;
            for (var i = 0; i < subtask.RequiredKnowledgeKeys.Count; i++)
            {
                var key = subtask.RequiredKnowledgeKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                counted += 1;
                if (binding.knowledgeMap.TryGetValue(key.Trim(), out var confidence))
                {
                    confidenceSum += Mathf.Clamp01(confidence);
                }
            }

            if (counted <= 0)
            {
                return 1f;
            }

            return Mathf.Clamp01(confidenceSum / counted);
        }

        private float CalculateQualityScore(RoutineCharacterBinding binding, DynamicSubtask subtask, float aptitudeMultiplier)
        {
            var hungerScore = Mathf.Clamp01(binding.hunger / GaugeMax);
            var sleepScore = Mathf.Clamp01(binding.sleep / GaugeMax);
            var stressScore = Mathf.Clamp01(binding.stress / GaugeMax);
            var moodScore = Mathf.Clamp01(((binding.hunger + binding.sleep + binding.stress) / 3f) / GaugeMax);
            var knowledgeScore = Mathf.Clamp01((aptitudeMultiplier - 0.5f) / 1.0f);
            var progressRatio = Mathf.Clamp01(subtask.RequiredWork <= 0 ? 0f : subtask.Progress / (float)subtask.RequiredWork);

            var weighted = (moodScore * 0.25f) + (stressScore * 0.2f) + (sleepScore * 0.2f) + (hungerScore * 0.15f) + (knowledgeScore * 0.2f);
            return Mathf.Clamp((weighted * 100f) + (progressRatio * 4f), 0f, 100f);
        }

        private void UpdatePerformanceScoresForTick()
        {
            _characterPerformanceScores.Clear();
            if (characters == null || characters.Count == 0)
            {
                SetDashboardContext("Performance", "-");
                return;
            }

            for (var i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                var actorName = binding.actor != null ? binding.actor.name : ResolveDisplayName(binding);
                var score = CalculateCharacterPerformanceScore(binding);
                _characterPerformanceScores[actorName] = score;
            }

            SetDashboardContext("Performance", BuildPerformanceDashboardText());
        }

        private float CalculateCharacterPerformanceScore(RoutineCharacterBinding binding)
        {
            var actorName = binding.actor != null ? binding.actor.name : string.Empty;
            var aptitudeScore = 0.6f;
            if (!string.IsNullOrWhiteSpace(actorName) && _characterAptitudeByActorName.TryGetValue(actorName, out var profile) && profile != null)
            {
                aptitudeScore = Mathf.Clamp01((profile.GetMultiplier(WorkType.Routine) - 0.5f) / 1.0f);
            }

            var missionScore = Mathf.Clamp01(binding.missionTicks / 100f);
            var conditionScore = Mathf.Clamp01((binding.hunger + binding.sleep + binding.stress) / (GaugeMax * 3f));
            return Mathf.Clamp(((missionScore * 0.45f) + (conditionScore * 0.35f) + (aptitudeScore * 0.2f)) * 100f, 0f, 100f);
        }

        private string BuildPerformanceDashboardText()
        {
            if (_characterPerformanceScores.Count == 0)
            {
                return "-";
            }

            var parts = new List<string>(_characterPerformanceScores.Count);
            foreach (var binding in characters)
            {
                var actorName = binding.actor != null ? binding.actor.name : ResolveDisplayName(binding);
                if (_characterPerformanceScores.TryGetValue(actorName, out var score))
                {
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}:{1:0.0}", ResolveDisplayName(binding), score));
                }
            }

            return parts.Count == 0 ? "-" : string.Join(" | ", parts);
        }

        private static bool HasArrived(Vector3 currentPosition, Vector3 targetPosition)
        {
            var dx = targetPosition.x - currentPosition.x;
            var dy = targetPosition.y - currentPosition.y;
            return (dx * dx) + (dy * dy) <= 0.0001f;
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
                    return WithObjectDepth(lockedPosition);
                }

                ReleaseZoneLock(binding);
            }

            var currentPosition = WithObjectDepth(binding.actor != null ? binding.actor.position : fallback);
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

            var normalized = WithObjectDepth(position);
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

            var fallback = WithObjectDepth(deskPositions[0]);
            var best = fallback;
            var bestTravel = float.PositiveInfinity;
            var found = false;
            for (int i = 0; i < deskPositions.Count; i++)
            {
                var candidate = WithObjectDepth(deskPositions[i]);
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

            preferredDesk = WithObjectDepth(preferredDesk);
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

            preferredMap[binding] = WithObjectDepth(deskPosition);
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

            var deskPosition = WithObjectDepth(binding.lockedZonePosition);
            if (!ContainsDeskPosition(desks, deskPosition) || !zone.Contains(deskPosition))
            {
                return false;
            }

            var actorPosition = WithObjectDepth(binding.actor.position);
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
            if (goalText == null && progressText == null && situationText == null && currentTimeText == null)
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
            var avgPerformance = ComputeAveragePerformanceScore();

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
                    "Progress: {0:0}% | Move:{1}/{2} | Avg H/S/T: {3:0}/{4:0}/{5:0} | Perf:{6:0.0}",
                    percentage,
                    movingCount,
                    characters.Count,
                    avgHunger,
                    avgSleep,
                    avgStress,
                    avgPerformance);
            }

            if (situationText != null)
            {
                situationText.text = BuildSituationSummary();
            }

            if (currentTimeText != null)
            {
                currentTimeText.text = timeText;
            }

            UpdateChronicleUi();
        }

        private float ComputeAveragePerformanceScore()
        {
            if (_characterPerformanceScores.Count <= 0)
            {
                return 0f;
            }

            var sum = 0f;
            var count = 0;
            foreach (var pair in _characterPerformanceScores)
            {
                sum += pair.Value;
                count += 1;
            }

            return count <= 0 ? 0f : sum / count;
        }

        private string BuildSituationSummary()
        {
            var summary = new StringBuilder(256);
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
                    if (string.Equals(key, "Chronicle", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    summary.AppendLine();
                    summary.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1}", key, _dashboardContext[key]);
                }
            }

            return summary.ToString();
        }

        private void UpdateChronicleUi()
        {
            var chronicleValue = TryGetDashboardValue("Chronicle");
            if (string.IsNullOrWhiteSpace(chronicleValue))
            {
                if (chronicleText != null)
                {
                    chronicleText.text = string.Empty;
                }

                return;
            }

            EnsureChronicleTextReference();
            if (chronicleText == null)
            {
                return;
            }

            chronicleText.text = "Chronicle:\n" + chronicleValue;
        }

        private string TryGetDashboardValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return _dashboardContext.TryGetValue(key, out var value) ? value : null;
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

        private static int ResolveMissionGoalTicks(MissionType missionType)
        {
            switch (missionType)
            {
                case MissionType.ResourceSweep:
                    return 240;
                case MissionType.SafetyPatrol:
                    return 140;
                case MissionType.Recon:
                default:
                    return 180;
            }
        }

        private float GetMissionProgressRatio()
        {
            var target = Mathf.Max(1, dashboardMissionGoalTicks);
            var missionProgress = Mathf.Clamp01(GetTotalMissionTicks() / (float)target);
            if (!IsDynamicWorldRuntimeEnabled())
            {
                return missionProgress;
            }

            var taskProgress = GetDynamicTaskCompletionRatio();
            return Mathf.Clamp01((missionProgress * 0.6f) + (taskProgress * 0.4f));
        }

        private float GetDynamicTaskCompletionRatio()
        {
            if (_currentDynamicTask == null)
            {
                return 0f;
            }

            var total = Mathf.Max(1, _currentDynamicTask.TotalRequiredWork);
            return Mathf.Clamp01(_currentDynamicTask.TotalProgress / (float)total);
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

        private RoutineCharacterBinding FindCharacterBindingByActorName(string actorName)
        {
            if (string.IsNullOrWhiteSpace(actorName))
            {
                return null;
            }

            for (var i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding == null || binding.actor == null)
                {
                    continue;
                }

                if (string.Equals(binding.actor.name, actorName, StringComparison.OrdinalIgnoreCase))
                {
                    return binding;
                }
            }

            return null;
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
            RebuildDynamicWorldIfEnabled();

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

            EnsureDashboardUiReferences();
            EnsureInteractionReferences();

            EnsureDefaultCharactersExist();
            EnsureCharacterBindingsFromRoot();
            EnsureZoneWidthsForCharacterSeparation();
            Ensure2DWorldSetup();
            EnsureZoneDesks();
            ApplyDepthLayout();
        }

        private bool IsDynamicWorldRuntimeEnabled()
        {
            return dynamicWorldEnabled && _outgameSetupApplied;
        }

        private void RebuildDynamicWorldIfEnabled()
        {
            if (!IsDynamicWorldRuntimeEnabled())
            {
                return;
            }

            CleanupLegacySceneWorldForDynamicMode();
            var dynamicZones = EnsureDynamicRoot("Zones_Dynamic");
            var dynamicCharacters = EnsureDynamicRoot("Characters_Dynamic");
            ClearChildrenImmediate(dynamicZones);
            ClearChildrenImmediate(dynamicCharacters);

            zonesRoot = dynamicZones;
            charactersRoot = dynamicCharacters;

            EnsureMainCamera2D();
            var layout = ResolveDynamicLayoutBounds();
            var missionSize = ResolveRandomizedLargeZoneSize(layout.zoneSize, 211);
            var cafeteriaSize = ResolveRandomizedLargeZoneSize(layout.zoneSize, 223);
            var sleepSize = ResolveRandomizedLargeZoneSize(layout.zoneSize, 227);
            var spacing = ResolveZoneSpacingForViewport(missionSize, cafeteriaSize, sleepSize, layout.zoneSpacing);
            missionZone = CreateZone2D(dynamicZones, "WorkZone", JobZoneWork, new[] { ZoneTagMission }, new Vector2(0f, 0f), missionSize, new Color(0.88f, 0.34f, 0.34f, DefaultZoneAlpha));
            cafeteriaZone = CreateZone2D(dynamicZones, "EatZone", JobZoneEat, new[] { ZoneTagNeedHunger }, new Vector2(-spacing, -0.4f), cafeteriaSize, new Color(0.12f, 0.53f, 0.9f, DefaultZoneAlpha));
            sleepZone = CreateZone2D(dynamicZones, "SleepZone", JobZoneSleep, new[] { ZoneTagNeedSleep }, new Vector2(spacing, 0.4f), sleepSize, new Color(0.26f, 0.66f, 0.29f, DefaultZoneAlpha));
            BindZonesFromAnchors();

            var spawnCount = Mathf.Clamp(_selectedCharacterCount, 1, 6);
            var rng = new System.Random(_resolvedWorldSeed ^ 0x5A91);
            for (var i = 0; i < spawnCount; i++)
            {
                var x = Mathf.Lerp(-layout.characterXSpan, layout.characterXSpan, (i + 0.5f) / Mathf.Max(1f, spawnCount));
                var y = layout.characterYMin + (float)(rng.NextDouble() * (layout.characterYMax - layout.characterYMin));
                CreateCharacter2D(dynamicCharacters, $"DynamicCharacter_{i + 1:D2}", new Vector2(x, y), GetLineColor(i));
            }

            characters.Clear();
            _characterAptitudeByActorName.Clear();
            _characterPerformanceScores.Clear();
            _subtaskDeadlinesById.Clear();
            EnsureCharacterBindingsFromRoot();
            for (var i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding?.actor == null)
                {
                    continue;
                }

                _characterAptitudeByActorName[binding.actor.name] = BuildCharacterAptitude(binding.actor.name, _resolvedWorldSeed + (i * 37));
            }

            SetDashboardContext("DynamicWorld", $"enabled/{spawnCount}chars");
            Ensure2DWorldSetup();
            EnsureZoneWidthsForCharacterSeparation();
            EnsureZoneDesks();
            ApplyDepthLayout();
        }

        private (float zoneSpacing, Vector2 zoneSize, float characterXSpan, float characterYMin, float characterYMax) ResolveDynamicLayoutBounds()
        {
            var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam == null || !cam.orthographic)
            {
                return (Mathf.Max(TargetZoneSize + 0.5f, zoneGap * 2.4f), new Vector2(TargetZoneSize, TargetZoneSize), 1f, 0.4f, 1.0f);
            }

            var halfHeight = Mathf.Max(2f, cam.orthographicSize * 0.82f);
            var halfWidth = Mathf.Max(3f, halfHeight * Mathf.Max(1f, cam.aspect) * 0.86f);
            var zoneWidth = Mathf.Clamp(halfWidth * 0.32f, MinZoneSizeNearTarget, MaxZoneSizeNearTarget);
            var zoneHeight = Mathf.Clamp(halfHeight * 0.58f, MinZoneSizeNearTarget, MaxZoneSizeNearTarget);
            // Keep zones well inside the viewport and avoid excessive left/right spread.
            var minSpacing = (zoneWidth * 0.75f) + 0.45f;
            var maxSpacing = halfWidth - (zoneWidth * 0.42f) - 0.15f;
            var preferredSpacing = Mathf.Min(halfWidth * 0.82f, ((zoneWidth * 1.05f) + 0.70f) * 2f);
            var zoneSpacing = Mathf.Clamp(preferredSpacing, minSpacing, Mathf.Max(minSpacing, maxSpacing));
            var characterXSpan = Mathf.Clamp(zoneWidth * 0.60f, 0.9f, 2.2f);
            var characterYMin = Mathf.Clamp(zoneHeight * 0.12f, 0.2f, 0.7f);
            var characterYMax = Mathf.Clamp(zoneHeight * 0.72f, 0.9f, 1.9f);
            if (characterYMax < characterYMin + 0.1f)
            {
                characterYMax = characterYMin + 0.1f;
            }

            return (zoneSpacing, new Vector2(zoneWidth, zoneHeight), characterXSpan, characterYMin, characterYMax);
        }

        private Vector2 ResolveRandomizedLargeZoneSize(Vector2 baseSize, int salt)
        {
            var baseX = Mathf.Max(0.1f, Mathf.Abs(baseSize.x));
            var baseY = Mathf.Max(0.1f, Mathf.Abs(baseSize.y));
            var targetX = Mathf.Clamp(baseX * Mathf.Max(1f, zoneSizeMultiplier), MinZoneSizeNearTarget, MaxZoneSizeNearTarget);
            var targetY = Mathf.Clamp(baseY * Mathf.Max(1f, zoneSizeMultiplier), MinZoneSizeNearTarget, MaxZoneSizeNearTarget);

            var jitter = Mathf.Clamp(zoneSizeRandomJitter, 0f, 0.25f);
            var minFactor = 1f - jitter;
            var maxFactor = 1f + jitter;
            var seed = _resolvedWorldSeed != 0 ? _resolvedWorldSeed : DefaultWorldSeed;
            var rng = new System.Random(unchecked((seed * 397) ^ salt));
            var randomX = Mathf.Lerp(minFactor, maxFactor, (float)rng.NextDouble());
            var randomY = Mathf.Lerp(minFactor, maxFactor, (float)rng.NextDouble());

            var finalX = Mathf.Clamp(Mathf.Max(minimumZoneSize.x, targetX * randomX), MinZoneSizeNearTarget, MaxZoneSizeNearTarget);
            var finalY = Mathf.Clamp(Mathf.Max(minimumZoneSize.y, targetY * randomY), MinZoneSizeNearTarget, MaxZoneSizeNearTarget);
            return new Vector2(finalX, finalY);
        }

        private float ResolveZoneSpacingForViewport(Vector2 missionSize, Vector2 cafeteriaSize, Vector2 sleepSize, float baseSpacing)
        {
            var desired = Mathf.Max(baseSpacing, Mathf.Max((missionSize.x + cafeteriaSize.x) * 0.55f, (missionSize.x + sleepSize.x) * 0.55f));
            var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam == null || !cam.orthographic)
            {
                return desired;
            }

            var halfHeight = Mathf.Max(2f, cam.orthographicSize * 0.9f);
            var halfWidth = Mathf.Max(3f, halfHeight * Mathf.Max(1f, cam.aspect) * 0.9f);
            var sideHalfWidth = Mathf.Max(cafeteriaSize.x, sleepSize.x) * 0.5f;
            var maxVisibleSpacing = Mathf.Max(0.5f, halfWidth - sideHalfWidth - 0.15f);
            return Mathf.Min(desired, maxVisibleSpacing);
        }

        private static Vector2 ResolveZoneBaseSize(RoutineZoneAnchor zone)
        {
            if (zone == null)
            {
                return new Vector2(TargetZoneSize, TargetZoneSize);
            }

            var box2D = zone.GetComponent<BoxCollider2D>();
            if (box2D != null && box2D.size.x > 0.0001f && box2D.size.y > 0.0001f)
            {
                return new Vector2(Mathf.Abs(box2D.size.x), Mathf.Abs(box2D.size.y));
            }

            var spriteRenderer = zone.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                var drawSize = spriteRenderer.drawMode == SpriteDrawMode.Simple
                    ? (spriteRenderer.sprite != null ? (Vector2)spriteRenderer.sprite.bounds.size : Vector2.zero)
                    : spriteRenderer.size;
                if (drawSize.x > 0.0001f && drawSize.y > 0.0001f)
                {
                    return new Vector2(Mathf.Abs(drawSize.x), Mathf.Abs(drawSize.y));
                }
            }

            return new Vector2(TargetZoneSize, TargetZoneSize);
        }

        private void ApplyZoneAreaSize(GameObject zoneGo, Vector2 size)
        {
            if (zoneGo == null)
            {
                return;
            }

            var width = Mathf.Max(minimumZoneSize.x, Mathf.Abs(size.x));
            var height = Mathf.Max(minimumZoneSize.y, Mathf.Abs(size.y));
            zoneGo.transform.localScale = Vector3.one;

            var box2D = zoneGo.GetComponent<BoxCollider2D>();
            if (box2D == null)
            {
                box2D = zoneGo.AddComponent<BoxCollider2D>();
            }

            box2D.size = new Vector2(width, height);
            box2D.offset = Vector2.zero;

            var spriteRenderer = zoneGo.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
                spriteRenderer.size = new Vector2(width, height);
            }
        }

        private void NormalizeZoneAreaFromLegacyScale(RoutineZoneAnchor zone)
        {
            if (zone == null)
            {
                return;
            }

            var scale = zone.transform.localScale;
            if (Mathf.Abs(scale.x - 1f) < 0.001f && Mathf.Abs(scale.y - 1f) < 0.001f)
            {
                return;
            }

            var box2D = zone.GetComponent<BoxCollider2D>();
            var size = box2D != null && box2D.size.x > 0.0001f && box2D.size.y > 0.0001f
                ? new Vector2(Mathf.Abs(box2D.size.x * scale.x), Mathf.Abs(box2D.size.y * scale.y))
                : new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            ApplyZoneAreaSize(zone.gameObject, size);
        }

        private void CleanupLegacySceneWorldForDynamicMode()
        {
            DestroyRootIfExists("Zones");
            DestroyRootIfExists("Characters");

            var allZones = FindObjectsByType<RoutineZoneAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < allZones.Length; i++)
            {
                var zone = allZones[i];
                if (zone == null || zone.transform == null)
                {
                    continue;
                }

                var parent = zone.transform.parent;
                if (parent != null && string.Equals(parent.name, "Zones_Dynamic", StringComparison.Ordinal))
                {
                    continue;
                }

                SafeDestroyGameObject(zone.gameObject);
            }

            var roots = new[] { "Zones_Dynamic", "Characters_Dynamic" };
            for (var r = 0; r < roots.Length; r++)
            {
                var rootGo = GameObject.Find(roots[r]);
                if (rootGo == null)
                {
                    continue;
                }

                var objectSlots = rootGo.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < objectSlots.Length; i++)
                {
                    var t = objectSlots[i];
                    if (t != null && string.Equals(t.name, ZoneObjectRootName, StringComparison.Ordinal))
                    {
                        SafeDestroyGameObject(t.gameObject);
                    }
                }
            }
        }

        private static void DestroyRootIfExists(string rootName)
        {
            if (string.IsNullOrWhiteSpace(rootName))
            {
                return;
            }

            var root = GameObject.Find(rootName);
            if (root != null)
            {
                SafeDestroyGameObject(root);
            }
        }

        private static Transform EnsureDynamicRoot(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                return existing.transform;
            }

            return new GameObject(objectName).transform;
        }

        private static void ClearChildrenImmediate(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var toDelete = new List<GameObject>();
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null)
                {
                    toDelete.Add(child.gameObject);
                }
            }

            for (var i = 0; i < toDelete.Count; i++)
            {
                if (Application.isPlaying)
                {
                    DestroyImmediate(toDelete[i]);
                }
                else
                {
                    DestroyImmediate(toDelete[i]);
                }
            }
        }

        private CharacterAptitudeProfile BuildCharacterAptitude(string actorName, int seed)
        {
            var rng = new System.Random(seed ^ (actorName?.GetHashCode() ?? 0));
            var all = new[] { WorkType.Observe, WorkType.Labor, WorkType.Routine, WorkType.Reflex };
            var primary = all[rng.Next(0, all.Length)];
            var secondary = all[rng.Next(0, all.Length)];
            if (secondary == primary)
            {
                secondary = all[(Array.IndexOf(all, primary) + 1) % all.Length];
            }

            var profile = new CharacterAptitudeProfile
            {
                PrimaryType = primary,
                SecondaryType = secondary,
                ObserveMultiplier = 1f,
                LaborMultiplier = 1f,
                RoutineMultiplier = 1f,
                ReflexMultiplier = 1f
            };
            ApplyAptitudeBonus(profile, primary, 1.2f);
            ApplyAptitudeBonus(profile, secondary, 1.1f);
            return profile;
        }

        private static void ApplyAptitudeBonus(CharacterAptitudeProfile profile, WorkType type, float value)
        {
            switch (type)
            {
                case WorkType.Observe:
                    profile.ObserveMultiplier = value;
                    break;
                case WorkType.Labor:
                    profile.LaborMultiplier = value;
                    break;
                case WorkType.Routine:
                    profile.RoutineMultiplier = value;
                    break;
                case WorkType.Reflex:
                    profile.ReflexMultiplier = value;
                    break;
            }
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
                neuronPanelView = TryBindExistingNeuronPanelView();
                if (neuronPanelView == null)
                {
                    neuronPanelView = FindFirstObjectByType<RoutineNeuronPanelView>();
                }

                if (neuronPanelView == null && Application.isPlaying)
                {
                    neuronPanelView = CreateRuntimeNeuronPanelView();
                }
            }

            if (neuronPanelView != null)
            {
                neuronPanelView.Hide();
            }

            if (objectInfoPanelView == null)
            {
                objectInfoPanelView = TryBindExistingObjectInfoPanelView();
                if (objectInfoPanelView == null)
                {
                    objectInfoPanelView = FindFirstObjectByType<RoutineObjectInfoPanelView>();
                }

                if (objectInfoPanelView == null && Application.isPlaying)
                {
                    objectInfoPanelView = CreateRuntimeObjectInfoPanelView();
                }
            }

            if (objectInfoPanelView != null)
            {
                objectInfoPanelView.Hide();
            }
        }

        private RoutineNeuronPanelView TryBindExistingNeuronPanelView()
        {
            var panelGo = GameObject.Find("RoutineNeuronPanel");
            if (panelGo == null)
            {
                return null;
            }

            var view = panelGo.GetComponent<RoutineNeuronPanelView>();
            if (view == null)
            {
                view = panelGo.AddComponent<RoutineNeuronPanelView>();
            }

            var title = FindTextByName(panelGo.transform, "TitleText");
            var intent = FindTextByName(panelGo.transform, "IntentText");
            var reason = FindTextByName(panelGo.transform, "ReasonText");
            var condition = FindTextByName(panelGo.transform, "ConditionText");
            var gauge = FindTextByName(panelGo.transform, "GaugeText");
            view.Configure(panelGo, title, intent, reason, condition, gauge);
            EnsureCloseButtonBinding(panelGo.transform, CloseNeuronPanel);
            return view;
        }

        private RoutineObjectInfoPanelView TryBindExistingObjectInfoPanelView()
        {
            var panelGo = GameObject.Find("RoutineObjectInfoPanel");
            if (panelGo == null)
            {
                return null;
            }

            var view = panelGo.GetComponent<RoutineObjectInfoPanelView>();
            if (view == null)
            {
                view = panelGo.AddComponent<RoutineObjectInfoPanelView>();
            }

            var title = FindTextByName(panelGo.transform, "TitleText");
            var body = FindTextByName(panelGo.transform, "BodyText");
            view.Configure(panelGo, title, body);
            EnsureCloseButtonBinding(panelGo.transform, CloseObjectInfoPanel);
            return view;
        }

        private static Text FindTextByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            var candidates = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void EnsureCloseButtonBinding(Transform panelRoot, Action onClick)
        {
            if (panelRoot == null || onClick == null)
            {
                return;
            }

            var candidates = panelRoot.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                var button = candidates[i];
                if (button == null || !string.Equals(button.name, "CloseButton", StringComparison.Ordinal))
                {
                    continue;
                }

                var key = button.GetInstanceID();
                if (_boundCloseButtons.Contains(key))
                {
                    return;
                }

                button.onClick.AddListener(() => onClick());
                _boundCloseButtons.Add(key);
                return;
            }
        }

        private void EnsureCharactersRootDepth()
        {
            if (!enforceDepthLayout || charactersRoot == null)
            {
                return;
            }

            var rootPos = charactersRoot.position;
            if (!Mathf.Approximately(rootPos.z, characterDepthZ))
            {
                rootPos.z = characterDepthZ;
                charactersRoot.position = rootPos;
            }
        }

        private RoutineNeuronPanelView CreateRuntimeNeuronPanelView()
        {
            var canvas = EnsureSharedMvpCanvas();

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

            var closeButtonGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeButtonGo.transform.SetParent(panelGo.transform, false);
            var closeRect = (RectTransform)closeButtonGo.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-8f, -8f);
            closeRect.sizeDelta = new Vector2(28f, 28f);
            var closeImage = closeButtonGo.GetComponent<Image>();
            closeImage.color = new Color(0.72f, 0.20f, 0.20f, 0.94f);
            var closeLabel = CreateNeuronPanelText(closeButtonGo.transform, "Label", new Vector2(0f, -2f), 18);
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.text = "X";
            closeLabel.color = Color.white;
            closeLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            closeLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var closeLabelRect = closeLabel.rectTransform;
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.pivot = new Vector2(0.5f, 0.5f);
            closeLabelRect.anchoredPosition = Vector2.zero;
            closeLabelRect.sizeDelta = Vector2.zero;
            closeButtonGo.GetComponent<Button>().onClick.AddListener(CloseNeuronPanel);

            var view = panelGo.GetComponent<RoutineNeuronPanelView>();
            view.Configure(panelGo, titleText, intentText, reasonText, conditionText, gaugeText);
            view.Hide();
            return view;
        }

        private RoutineObjectInfoPanelView CreateRuntimeObjectInfoPanelView()
        {
            var canvas = EnsureSharedMvpCanvas();

            var panelGo = new GameObject("RoutineObjectInfoPanel", typeof(RectTransform), typeof(Image), typeof(RoutineObjectInfoPanelView));
            panelGo.transform.SetParent(canvas.transform, false);

            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(20f, -20f);
            panelRect.sizeDelta = new Vector2(440f, 140f);

            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.12f, 0.10f, 0.06f, 0.86f);

            var titleText = CreateNeuronPanelText(panelGo.transform, "TitleText", new Vector2(14f, -12f), 19);
            var bodyText = CreateNeuronPanelText(panelGo.transform, "BodyText", new Vector2(14f, -48f), 15);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;

            var closeButtonGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeButtonGo.transform.SetParent(panelGo.transform, false);
            var closeRect = (RectTransform)closeButtonGo.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-8f, -8f);
            closeRect.sizeDelta = new Vector2(28f, 28f);
            var closeImage = closeButtonGo.GetComponent<Image>();
            closeImage.color = new Color(0.72f, 0.20f, 0.20f, 0.94f);
            var closeLabel = CreateNeuronPanelText(closeButtonGo.transform, "Label", new Vector2(0f, -2f), 18);
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.text = "X";
            closeLabel.color = Color.white;
            closeLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            closeLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var closeLabelRect = closeLabel.rectTransform;
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.pivot = new Vector2(0.5f, 0.5f);
            closeLabelRect.anchoredPosition = Vector2.zero;
            closeLabelRect.sizeDelta = Vector2.zero;
            closeButtonGo.GetComponent<Button>().onClick.AddListener(CloseObjectInfoPanel);

            var view = panelGo.GetComponent<RoutineObjectInfoPanelView>();
            view.Configure(panelGo, titleText, bodyText);
            view.Hide();
            return view;
        }

        private Canvas EnsureSharedMvpCanvas()
        {
            var existing = GameObject.Find("MvpDashboardCanvas");
            if (existing == null)
            {
                existing = GameObject.Find("MvpCanvas");
            }

            if (existing == null)
            {
                var anyCanvas = FindFirstObjectByType<Canvas>();
                if (anyCanvas != null)
                {
                    existing = anyCanvas.gameObject;
                }
            }

            if (existing != null)
            {
                var existingCanvas = existing.GetComponent<Canvas>();
                if (existingCanvas != null)
                {
                    var existingCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                    existingCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    existingCanvas.worldCamera = existingCamera;
                    existingCanvas.planeDistance = 5f;
                    if (existingCanvas.GetComponent<GraphicRaycaster>() == null)
                    {
                        existingCanvas.gameObject.AddComponent<GraphicRaycaster>();
                    }

                    if (existingCanvas.GetComponent<CanvasScaler>() == null)
                    {
                        existingCanvas.gameObject.AddComponent<CanvasScaler>();
                    }

                    ConfigureCanvasScaler(existingCanvas.gameObject.GetComponent<CanvasScaler>());

                    EnsureEventSystemExists();
                    return existingCanvas;
                }
            }

            var canvasGo = new GameObject("MvpDashboardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            var uiCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 5f;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            ConfigureCanvasScaler(scaler);
            EnsureEventSystemExists();
            return canvas;
        }

        private static void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void EnsureEventSystemExists()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = go.GetComponent<EventSystem>();
            }

            if (eventSystem == null)
            {
                return;
            }

            var eventGo = eventSystem.gameObject;
#if ENABLE_INPUT_SYSTEM
            var inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType != null)
            {
                var standalone = eventGo.GetComponent<StandaloneInputModule>();
                if (standalone != null)
                {
                    if (Application.isPlaying) Destroy(standalone);
                    else DestroyImmediate(standalone);
                }

                if (eventGo.GetComponent(inputSystemModuleType) == null)
                {
                    eventGo.AddComponent(inputSystemModuleType);
                }

                return;
            }
#endif
            if (eventGo.GetComponent<StandaloneInputModule>() == null)
            {
                eventGo.AddComponent<StandaloneInputModule>();
            }
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
            var currentSize = ResolveZoneBaseSize(zone);
            var currentWidth = Mathf.Abs(currentSize.x);
            if (currentWidth >= requiredWidth)
            {
                return;
            }

            ApplyZoneAreaSize(zone.gameObject, new Vector2(requiredWidth, currentSize.y));
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

            if (currentTimeText == null)
            {
                var timeGo = GameObject.Find("CurrentTimeText");
                currentTimeText = timeGo != null ? timeGo.GetComponent<Text>() : null;
            }

            EnsureChronicleTextReference();

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

            var createdSituationText = false;
            var createdCurrentTimeText = false;

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
                createdSituationText = true;
            }

            if (chronicleText == null)
            {
                chronicleText = CreateChronicleText(canvas.transform);
            }

            if (currentTimeText == null)
            {
                currentTimeText = CreateDashboardLine(panelTransform, "CurrentTimeText", -132f);
                createdCurrentTimeText = true;
            }

            if (createdCurrentTimeText)
            {
                ConfigureCurrentTimeTextLayout(currentTimeText);
            }

            if (createdSituationText)
            {
                ConfigureSituationTextLayout(situationText);
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

        private void EnsureChronicleTextReference()
        {
            if (chronicleText != null)
            {
                return;
            }

            var chronicleGo = GameObject.Find("ChronicleText");
            chronicleText = chronicleGo != null ? chronicleGo.GetComponent<Text>() : null;
            if (chronicleText != null)
            {
                ConfigureChronicleTextLayout(chronicleText);
                return;
            }

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            chronicleText = CreateChronicleText(canvas.transform);
        }

        private static Text CreateChronicleText(Transform canvasTransform)
        {
            var go = new GameObject("ChronicleText", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(canvasTransform, false);
            var text = go.GetComponent<Text>();
            try
            {
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (ArgumentException)
            {
                text.font = null;
            }

            text.fontSize = 18;
            text.alignment = TextAnchor.LowerRight;
            text.color = new Color(0.92f, 0.96f, 1f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = string.Empty;
            ConfigureChronicleTextLayout(text);
            return text;
        }

        private static void ConfigureChronicleTextLayout(Text text)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-16f, 16f);
            rect.sizeDelta = new Vector2(520f, 220f);
        }

        private static void ConfigureCurrentTimeTextLayout(Text text)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(560f, 36f);

            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void ConfigureSituationTextLayout(Text text)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(16f, 16f);
            rect.sizeDelta = new Vector2(620f, 240f);

            text.alignment = TextAnchor.LowerLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
                var workRequirement = ResolveTaskRequirement("work", JobZoneWork, new[] { "desk", "computer" });
                var eatRequirement = ResolveTaskRequirement("eat", JobZoneEat, new[] { "table", "tray", "cup" });
                var sleepRequirement = ResolveTaskRequirement("sleep", JobZoneSleep, new[] { "bed", "pillow", "blanket" });
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

            var desiredCount = ResolveDesiredOfficeItemCount();
            _officeItems.AddRange(OfficeItemFactory.GenerateOfficeItems(new System.Random(_resolvedWorldSeed), desiredCount, owners, zoneGenerationRuleSet));
            RebuildOfficeItemsByZoneCache();
            EnsureRequiredTagCoverageForNeedRequirements();
            var itemDump = new StringBuilder();
            for (int i = 0; i < _officeItems.Count; i++)
            {
                itemDump.AppendLine(_officeItems[i].BuildInspectorSummary());
            }

            var zoneRulesId = zoneGenerationRuleSet != null ? zoneGenerationRuleSet.RuleSetId : "fallback";
            var taskRulesId = taskGenerationRuleSet != null ? taskGenerationRuleSet.RuleSetId : "fallback";
            SetDashboardContext("RuleSet", $"zone:{zoneRulesId}, task:{taskRulesId}");
            SetDashboardContext("WorldSeed", _resolvedWorldSeed.ToString(CultureInfo.InvariantCulture));
            SetDashboardContext("ItemCount", _officeItems.Count.ToString(CultureInfo.InvariantCulture));
            Debug.Log("[RoutineMVP] Generated office items for job system:\n" + itemDump);
        }

        private int ResolveDesiredOfficeItemCount()
        {
            var baseCount = 12;
            if (taskGenerationRuleSet != null)
            {
                var scale = taskGenerationRuleSet.GetDifficultySupplyScale(_selectedDifficulty);
                baseCount = Mathf.RoundToInt(baseCount * scale);
            }
            else
            {
                if (_selectedDifficulty == SessionDifficulty.Easy) baseCount = 15;
                if (_selectedDifficulty == SessionDifficulty.Risky) baseCount = 8;
            }

            return Mathf.Clamp(baseCount, 6, 24);
        }

        private void EnsureRequiredTagCoverageForNeedRequirements()
        {
            var requiredTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _actionJobRequirements)
            {
                var requirement = pair.Value;
                if (requirement?.RequiredTags == null)
                {
                    continue;
                }

                for (var i = 0; i < requirement.RequiredTags.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(requirement.RequiredTags[i]))
                    {
                        requiredTags.Add(requirement.RequiredTags[i].Trim());
                    }
                }
            }

            foreach (var tag in requiredTags)
            {
                var found = false;
                for (var i = 0; i < _officeItems.Count; i++)
                {
                    if (_officeItems[i] != null && _officeItems[i].HasTag(tag))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    continue;
                }

                var fallbackZone = ResolveDefaultZoneByTag(tag);
                _officeItems.Add(new WorldItem($"guarantee-{tag}", $"Guaranteed {tag}", ItemUsagePolicy.Public, string.Empty, new[] { tag }, fallbackZone));
            }

            RebuildOfficeItemsByZoneCache();
        }

        private static string ResolveDefaultZoneByTag(string tag)
        {
            if (string.Equals(tag, "desk", StringComparison.OrdinalIgnoreCase) || string.Equals(tag, "computer", StringComparison.OrdinalIgnoreCase))
            {
                return JobZoneWork;
            }

            if (string.Equals(tag, "table", StringComparison.OrdinalIgnoreCase) || string.Equals(tag, "tray", StringComparison.OrdinalIgnoreCase) || string.Equals(tag, "cup", StringComparison.OrdinalIgnoreCase))
            {
                return JobZoneEat;
            }

            if (string.Equals(tag, "bed", StringComparison.OrdinalIgnoreCase) || string.Equals(tag, "pillow", StringComparison.OrdinalIgnoreCase) || string.Equals(tag, "blanket", StringComparison.OrdinalIgnoreCase))
            {
                return JobZoneSleep;
            }

            return JobZoneWork;
        }

        private void EnsureDynamicTaskForTick(int dayIndex, int tickInHalfDay)
        {
            if (!IsDynamicWorldRuntimeEnabled())
            {
                return;
            }

            var shouldRegenerate = _currentDynamicTask == null || _currentDynamicTask.FirstIncomplete() == null;
            if (!shouldRegenerate && (_absoluteTick - _lastDynamicTaskSeedTick) < 12)
            {
                return;
            }

            var seed = _resolvedWorldSeed ^ (dayIndex * 739) ^ (tickInHalfDay * 131);
            _subtaskDeadlinesById.Clear();
            _currentDynamicTask = BuildDynamicTask(seed);
            _lastDynamicTaskSeedTick = _absoluteTick;
            ApplyDynamicTaskRequirementBinding();
            UpdateTaskDashboardContext();
        }

        private DynamicTaskModel BuildDynamicTask(int seed)
        {
            var rng = new System.Random(seed);
            var task = new DynamicTaskModel
            {
                TaskId = $"task-{seed & 0x7FFF:X4}",
                Difficulty = _selectedDifficulty
            };

            var subtaskCount = _selectedDifficulty == SessionDifficulty.Risky ? 4 : _selectedDifficulty == SessionDifficulty.Easy ? 2 : 3;
            for (var i = 0; i < subtaskCount; i++)
            {
                var template = PickTaskTemplate(rng);
                var type = template != null ? template.WorkType : (WorkType)rng.Next(0, 4);
                var tags = ResolveSubtaskTags(template, type, rng);
                var requiredKnowledgeKeys = ResolveRequiredKnowledgeKeys(template, type);
                var workMin = template != null ? Math.Max(1, template.BaseWorkUnitsRange.x) : 4;
                var workMax = template != null ? Math.Max(workMin, template.BaseWorkUnitsRange.y) : 12;
                var requiredWork = rng.Next(workMin, workMax + 1);
                if (_selectedDifficulty == SessionDifficulty.Risky)
                {
                    requiredWork = Mathf.RoundToInt(requiredWork * 1.2f);
                }
                else if (_selectedDifficulty == SessionDifficulty.Easy)
                {
                    requiredWork = Mathf.Max(1, Mathf.RoundToInt(requiredWork * 0.85f));
                }

                var subtask = new DynamicSubtask
                {
                    SubtaskId = $"{task.TaskId}-s{i + 1}",
                    RequiredWork = Math.Max(1, requiredWork),
                    RequiredTags = tags,
                    RequiredKnowledgeKeys = requiredKnowledgeKeys,
                    WorkType = type,
                    AssignedZoneKey = ResolveZoneAffinity(template, tags),
                    Progress = 0,
                    QualityScore = 50f,
                    ReworkUnits = 0,
                    PerformanceScore = 0f,
                    CompletedTick = -1
                };
                task.Subtasks.Add(subtask);
                _subtaskDeadlinesById[subtask.SubtaskId] = _absoluteTick + Math.Max(3, Mathf.RoundToInt(requiredWork * 1.35f));
            }

            return task;
        }

        private TaskTemplateRule PickTaskTemplate(System.Random rng)
        {
            if (taskGenerationRuleSet == null || !taskGenerationRuleSet.HasTemplates())
            {
                return null;
            }

            var templates = taskGenerationRuleSet.Templates;
            var total = 0f;
            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i];
                if (template == null)
                {
                    continue;
                }

                total += Mathf.Max(0f, template.Weight) * taskGenerationRuleSet.GetPriorityWeight(template.WorkType, _selectedPriorityPair);
            }

            if (total <= 0.0001f)
            {
                return templates[rng.Next(0, templates.Count)];
            }

            var roll = (float)rng.NextDouble() * total;
            var cumulative = 0f;
            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i];
                if (template == null)
                {
                    continue;
                }

                cumulative += Mathf.Max(0f, template.Weight) * taskGenerationRuleSet.GetPriorityWeight(template.WorkType, _selectedPriorityPair);
                if (roll <= cumulative)
                {
                    return template;
                }
            }

            return templates[templates.Count - 1];
        }

        private static List<string> ResolveSubtaskTags(TaskTemplateRule template, WorkType type, System.Random rng)
        {
            var tags = new List<string>();
            if (template != null)
            {
                var pool = (template.RequiredTagPool != null && template.RequiredTagPool.Length > 0) ? template.RequiredTagPool : template.RequiredTags;
                if (pool != null && pool.Length > 0)
                {
                    var count = Math.Min(2, pool.Length);
                    for (var i = 0; i < count; i++)
                    {
                        var candidate = pool[rng.Next(0, pool.Length)];
                        if (!string.IsNullOrWhiteSpace(candidate) && !tags.Contains(candidate))
                        {
                            tags.Add(candidate.Trim());
                        }
                    }
                }
            }

            if (tags.Count == 0)
            {
                switch (type)
                {
                    case WorkType.Observe:
                        tags.Add("computer");
                        break;
                    case WorkType.Labor:
                        tags.Add("desk");
                        break;
                    case WorkType.Reflex:
                        tags.Add("tray");
                        break;
                    default:
                        tags.Add("table");
                        break;
                }
            }

            return tags;
        }

        private static List<string> ResolveRequiredKnowledgeKeys(TaskTemplateRule template, WorkType type)
        {
            var keys = new List<string>();
            if (template?.RequiredKnowledgeKeys != null)
            {
                for (var i = 0; i < template.RequiredKnowledgeKeys.Length; i++)
                {
                    var key = template.RequiredKnowledgeKeys[i];
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        var normalized = key.Trim();
                        if (!keys.Contains(normalized))
                        {
                            keys.Add(normalized);
                        }
                    }
                }
            }

            if (keys.Count > 0)
            {
                return keys;
            }

            switch (type)
            {
                case WorkType.Observe:
                    keys.Add("knowledge.observe.basics");
                    break;
                case WorkType.Labor:
                    keys.Add("knowledge.labor.basics");
                    break;
                case WorkType.Reflex:
                    keys.Add("knowledge.reflex.basics");
                    break;
                default:
                    keys.Add("knowledge.routine.basics");
                    break;
            }

            return keys;
        }

        private static string ResolveZoneAffinity(TaskTemplateRule template, IReadOnlyList<string> tags)
        {
            if (!string.IsNullOrWhiteSpace(template?.ZoneAffinity))
            {
                return template.ZoneAffinity.Trim();
            }

            if (!string.IsNullOrWhiteSpace(template?.ZoneKey))
            {
                return template.ZoneKey.Trim();
            }

            if (tags != null)
            {
                for (var i = 0; i < tags.Count; i++)
                {
                    var zone = ResolveDefaultZoneByTag(tags[i]);
                    if (!string.IsNullOrWhiteSpace(zone))
                    {
                        return zone;
                    }
                }
            }

            return JobZoneWork;
        }

        private bool TryGetSubtaskDeadline(string subtaskId, out int deadlineTick)
        {
            if (string.IsNullOrWhiteSpace(subtaskId))
            {
                deadlineTick = -1;
                return false;
            }

            return _subtaskDeadlinesById.TryGetValue(subtaskId, out deadlineTick);
        }

        private void ApplyDynamicTaskRequirementBinding()
        {
            if (_currentDynamicTask == null)
            {
                return;
            }

            var subtask = _currentDynamicTask.FirstIncomplete();
            if (subtask == null)
            {
                return;
            }

            _actionJobRequirements[RoutineActionType.Mission] = new NeedRequirement("work", subtask.AssignedZoneKey, subtask.RequiredTags);
        }

        private void UpdateTaskDashboardContext()
        {
            if (_currentDynamicTask == null)
            {
                SetDashboardContext("CurrentTask", "-");
                SetDashboardContext("Subtasks", "-");
                if (taskUiBinding != null)
                {
                    taskUiBinding.text = "Current Task: -\nSubtasks: -";
                }
                return;
            }

            var header = $"{_currentDynamicTask.TaskId} ({_currentDynamicTask.TotalProgress}/{_currentDynamicTask.TotalRequiredWork})";
            SetDashboardContext("CurrentTask", header);
            var top = _currentDynamicTask.FirstIncomplete();
            if (top == null)
            {
                SetDashboardContext("Subtasks", "completed");
                if (taskUiBinding != null)
                {
                    taskUiBinding.text = $"Current Task: {header}\nSubtasks: completed";
                }
                return;
            }

            var tags = top.RequiredTags != null && top.RequiredTags.Count > 0
                ? string.Join(",", top.RequiredTags)
                : "-";
            var deadlineLabel = TryGetSubtaskDeadline(top.SubtaskId, out var topDeadline) ? topDeadline.ToString(CultureInfo.InvariantCulture) : "-";
            var subtaskText = $"{top.WorkType} {top.Progress}/{top.RequiredWork} q[{top.QualityScore:0.0}] rw[{top.ReworkUnits}] dl[{deadlineLabel}] tags[{tags}] zone[{top.AssignedZoneKey}]";
            SetDashboardContext("Subtasks", subtaskText);
            if (taskUiBinding != null)
            {
                taskUiBinding.text = $"Current Task: {header}\nSubtasks: {subtaskText}";
            }
        }

        private NeedRequirement ResolveTaskRequirement(string needKey, string fallbackZoneKey, IReadOnlyList<string> fallbackTags)
        {
            if (taskGenerationRuleSet == null || !taskGenerationRuleSet.HasTemplates())
            {
                return new NeedRequirement(needKey, fallbackZoneKey, fallbackTags);
            }

            var templates = taskGenerationRuleSet.Templates;
            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i];
                if (template == null || !string.Equals(template.NeedKey, needKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var zoneKey = string.IsNullOrWhiteSpace(template.ZoneKey) ? fallbackZoneKey : template.ZoneKey.Trim();
                var tags = (template.RequiredTags == null || template.RequiredTags.Length == 0) ? fallbackTags : template.RequiredTags;
                return new NeedRequirement(needKey, zoneKey, tags);
            }

            return new NeedRequirement(needKey, fallbackZoneKey, fallbackTags);
        }

        private void RebuildOfficeItemsByZoneCache()
        {
            _officeItemsByZoneKey.Clear();
            for (var i = 0; i < _officeItems.Count; i++)
            {
                var item = _officeItems[i];
                if (item == null)
                {
                    continue;
                }

                var zoneKey = string.IsNullOrWhiteSpace(item.ZoneKey) ? "unknown" : item.ZoneKey.Trim();
                if (!_officeItemsByZoneKey.TryGetValue(zoneKey, out var bucket))
                {
                    bucket = new List<WorldItem>();
                    _officeItemsByZoneKey[zoneKey] = bucket;
                }

                bucket.Add(item);
            }
        }

        private void ResetProceduralGenerationState()
        {
            _actionJobRequirements.Clear();
            _officeItems.Clear();
            _officeItemsByZoneKey.Clear();
            _currentDynamicTask = null;
            _lastDynamicTaskSeedTick = -1;
            _characterAptitudeByActorName.Clear();
            _characterPerformanceScores.Clear();
            _subtaskDeadlinesById.Clear();
        }

        private void EnsureDefaultCharactersExist()
        {
            if (IsDynamicWorldRuntimeEnabled())
            {
                return;
            }

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
                localPosition.z = 0f;
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
            if (binding.knowledgeMap == null)
            {
                binding.knowledgeMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            }

            EnsureCharacterKnowledgeMap(binding, index);
            UpdateRuntimeStateTexts(binding);
            EnsureTargetLineRenderer(binding, index);
            EnsureSelfTalkText(binding);
            binding.runtimeInitialized = true;
        }

        private static void EnsureCharacterKnowledgeMap(RoutineCharacterBinding binding, int index)
        {
            if (binding == null)
            {
                return;
            }

            if (binding.knowledgeMap == null)
            {
                binding.knowledgeMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            }

            if (binding.knowledgeMap.Count > 0)
            {
                return;
            }

            var baseConfidence = Mathf.Clamp01(0.55f + (index * 0.08f));
            binding.knowledgeMap["knowledge.observe.basics"] = Mathf.Clamp01(baseConfidence - 0.05f);
            binding.knowledgeMap["knowledge.labor.basics"] = Mathf.Clamp01(baseConfidence + 0.05f);
            binding.knowledgeMap["knowledge.routine.basics"] = baseConfidence;
            binding.knowledgeMap["knowledge.reflex.basics"] = Mathf.Clamp01(baseConfidence - 0.1f);
        }

        private void ApplyDepthLayout()
        {
            if (!enforceDepthLayout)
            {
                return;
            }

            if (charactersRoot != null)
            {
                var rootPos = charactersRoot.position;
                rootPos.z = characterDepthZ;
                charactersRoot.position = rootPos;
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
            zone.transform.localScale = Vector3.one;
            EnsureSpriteRenderer(zone, color, Vector2.one, false);
            ApplyZoneAreaSize(zone, size);

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
            actor.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            actor.transform.localScale = new Vector3(DefaultCharacterSpriteSize, DefaultCharacterSpriteSize, 1f);
            EnsureSpriteRenderer(actor, color, new Vector2(DefaultCharacterSpriteSize, DefaultCharacterSpriteSize), true);
            actor.AddComponent<CapsuleCollider2D>();
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
                var zoneSprite = visualResources != null ? visualResources.ResolveZoneSprite(anchor.ZoneId) : null;
                EnsureSpriteRenderer(anchor.gameObject, color, Vector2.one, false, zoneSprite);
                NormalizeZoneAreaFromLegacyScale(anchor);
                var zoneFrames = visualResources != null ? visualResources.ResolveZoneAnimationFrames(anchor.ZoneId) : null;
                EnsureObjectSpriteAnimation(anchor.gameObject, zoneFrames, zoneSpriteAnimationFps);
                EnsureZoneBoundary2D(anchor);
            }

            EnsureZoneDesks();
            EnsureZoneObjects();
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
                var actorSprite = visualResources != null ? visualResources.ResolveCharacterSprite(binding.actor.name, i) : null;
                EnsureSpriteRenderer(actorGo, color, new Vector2(DefaultCharacterSpriteSize, DefaultCharacterSpriteSize), true, actorSprite);
                EnsureCharacterAnimator(binding.actor.gameObject);
            }
        }


        private void EnsureCharacterAnimator(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            EnsureCharacterAnimatorControllerLoaded();

            var animator = go.GetComponent<Animator>();
            if (animator == null)
            {
                animator = go.AddComponent<Animator>();
            }

            if (characterAnimatorController != null && animator.runtimeAnimatorController != characterAnimatorController)
            {
                animator.runtimeAnimatorController = characterAnimatorController;
            }

            if (go.GetComponent<RoutineCharacterAnimatorDriver>() == null)
            {
                go.AddComponent<RoutineCharacterAnimatorDriver>();
            }
        }

        [ContextMenu("Rebind Character Animators")]
        public void RebindCharacterAnimators()
        {
            EnsureCharacterAnimatorControllerLoaded();
            EnsureCharacterBindingsFromRoot();
            for (int i = 0; i < characters.Count; i++)
            {
                var actor = characters[i].actor;
                if (actor == null)
                {
                    continue;
                }

                EnsureCharacterAnimator(actor.gameObject);
            }
        }

        private void EnsureObjectSpriteAnimation(GameObject go, Sprite[] frames, float fps)
        {
            if (go == null)
            {
                return;
            }

            if (frames == null || frames.Length <= 1)
            {
                return;
            }

            var player = go.GetComponent<RoutineObjectSpriteAnimationPlayer>();
            if (player == null)
            {
                player = go.AddComponent<RoutineObjectSpriteAnimationPlayer>();
            }

            player.Configure(frames, fps);
        }

        private static void UpdateCharacterAnimator(RoutineCharacterBinding binding)
        {
            if (binding.actor == null)
            {
                return;
            }

            var driver = binding.actor.GetComponent<RoutineCharacterAnimatorDriver>();
            if (driver == null)
            {
                return;
            }

            var position = binding.actor.position;
            var delta = binding.targetPosition - position;
            var isMoving = delta.sqrMagnitude > 0.0001f;
            var speed = isMoving ? binding.moveSpeed : 0f;
            driver.ApplyState(binding.currentAction, binding.intendedAction, isMoving, speed, delta.x);

            if (isMoving && Mathf.Abs(delta.x) > 0.0001f)
            {
                var scale = binding.actor.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(delta.x);
                binding.actor.localScale = scale;
            }
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
                box2D.size = ResolveZoneBaseSize(anchor);
            }

            anchor.RebindBoundaries();
        }

        private void EnsureZoneObjects()
        {
            var zones = GetManagedZones();
            for (int i = 0; i < zones.Count; i++)
            {
                EnsureZoneObjects(zones[i]);
            }
        }

        private void EnsureZoneObjects(RoutineZoneAnchor zone)
        {
            if (zone == null)
            {
                return;
            }

            var objectRoot = zone.transform.Find(ZoneObjectRootName);
            if (objectRoot == null)
            {
                var rootGo = new GameObject(ZoneObjectRootName);
                rootGo.transform.SetParent(zone.transform, false);
                objectRoot = rootGo.transform;
            }

            var activeNames = new HashSet<string>(StringComparer.Ordinal);

            if (TryEnsureProceduralZoneFurniture(zone, objectRoot, activeNames))
            {
            }
            else if (zone.HasTag(ZoneTagMission))
            {
                EnsureMissionZoneFurniture(zone, objectRoot, activeNames);
            }
            else if (zone.HasTag(ZoneTagNeedHunger))
            {
                EnsureCafeteriaZoneFurniture(zone, objectRoot, activeNames);
            }
            else if (zone.HasTag(ZoneTagNeedSleep))
            {
                EnsureSleepZoneFurniture(zone, objectRoot, activeNames);
            }

            for (int i = 0; i < objectRoot.childCount; i++)
            {
                var child = objectRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (!activeNames.Contains(child.name))
                {
                    SafeDestroyGameObject(child.gameObject);
                }
            }
        }

        private bool TryEnsureProceduralZoneFurniture(RoutineZoneAnchor zone, Transform objectRoot, HashSet<string> activeNames)
        {
            if (zone == null)
            {
                return false;
            }

            var zoneKey = GetZoneReservationKey(zone);
            if (string.IsNullOrWhiteSpace(zoneKey)
                || !_officeItemsByZoneKey.TryGetValue(zoneKey.Trim(), out var items)
                || items == null
                || items.Count == 0)
            {
                return false;
            }

            var positions = GenerateScatteredPositions(zone, items.Count, 0.20f);
            var anchorPositions = new List<Vector3>();
            var anchorTag = ResolvePrimaryAnchorTag(zone);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var pos = positions[Mathf.Min(i, positions.Count - 1)];
                var tag = ResolvePrimaryItemTag(item);
                var objectName = string.Format(CultureInfo.InvariantCulture, "{0}_{1:D2}_{2}", zoneKey, i + 1, tag);
                var go = CreateFurnitureObject(
                    objectRoot,
                    objectName,
                    pos,
                    tag,
                    ResolveObjectScaleForTag(tag),
                    zoneKey,
                    item != null ? item.BuildInspectorSummary() : "generated",
                    activeNames);
                if (go != null && string.Equals(tag, anchorTag, StringComparison.OrdinalIgnoreCase))
                {
                    anchorPositions.Add(go.transform.position);
                }
            }

            if (anchorPositions.Count == 0)
            {
                anchorPositions.AddRange(positions);
            }

            _zoneDeskPositions[zoneKey] = anchorPositions;
            return true;
        }

        private static string ResolvePrimaryItemTag(WorldItem item)
        {
            if (item?.Tags == null)
            {
                return "desk";
            }

            foreach (var tag in item.Tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    return tag.Trim();
                }
            }

            return "desk";
        }

        private static string ResolvePrimaryAnchorTag(RoutineZoneAnchor zone)
        {
            if (zone != null)
            {
                if (zone.HasTag(ZoneTagNeedHunger))
                {
                    return "table";
                }

                if (zone.HasTag(ZoneTagNeedSleep))
                {
                    return "bed";
                }
            }

            return "desk";
        }

        private void EnsureMissionZoneFurniture(RoutineZoneAnchor zone, Transform objectRoot, HashSet<string> activeNames)
        {
            RefreshMissionFurnitureTemplate(objectRoot);

            var zoneKey = GetZoneReservationKey(zone);
            if (!_zoneDeskPositions.TryGetValue(zoneKey, out var desks) || desks == null || desks.Count == 0)
            {
                EnsureZoneDesks(zone, zoneKey);
                _zoneDeskPositions.TryGetValue(zoneKey, out desks);
            }

            if (desks == null || desks.Count == 0)
            {
                return;
            }

            for (int i = 0; i < desks.Count; i++)
            {
                var baseYJitter = (i % 3 == 0 ? -0.04f : i % 3 == 1 ? 0.02f : 0.05f);
                var deskPos = WithObjectDepth(desks[i] + new Vector3(0f, baseYJitter, 0f));
                var deskName = string.Format(CultureInfo.InvariantCulture, "DeskSet_{0:D2}", i + 1);
                CreateFurnitureObject(objectRoot, deskName, deskPos, "desk", ResolveObjectScaleForTag("desk"), zoneKey, "mission-desk", activeNames);

                var jitter = (i % 2 == 0 ? -0.02f : 0.02f);
                var computerPos = deskPos + new Vector3(-0.11f + jitter, 0.24f, 0f);
                var cupPos = deskPos + new Vector3(0.12f + jitter, 0.28f, 0f);
                CreateFurnitureObject(objectRoot, deskName + "_Computer", computerPos, "computer", ResolveObjectScaleForTag("computer"), zoneKey, "desk-top", activeNames);
                CreateFurnitureObject(objectRoot, deskName + "_Cup", cupPos, "cup", ResolveObjectScaleForTag("cup"), zoneKey, "desk-top", activeNames);
            }

            _zoneDeskPositions[zoneKey] = new List<Vector3>(desks);
        }

        private void EnsureCafeteriaZoneFurniture(RoutineZoneAnchor zone, Transform objectRoot, HashSet<string> activeNames)
        {
            var zoneKey = GetZoneReservationKey(zone);
            var tableCount = Mathf.Clamp(Mathf.Max(2, characters.Count), 2, 5);
            var tablePositions = GenerateScatteredPositions(zone, tableCount, 0.20f);
            for (int i = 0; i < tablePositions.Count; i++)
            {
                var tableName = string.Format(CultureInfo.InvariantCulture, "TableSet_{0:D2}", i + 1);
                var tablePos = tablePositions[i] + new Vector3(0f, (i % 2 == 0 ? 0.04f : -0.03f), 0f);
                CreateFurnitureObject(objectRoot, tableName, tablePos, "table", ResolveObjectScaleForTag("table"), zoneKey, "cafeteria-table", activeNames);

                CreateFurnitureObject(objectRoot, tableName + "_Tray", tablePos + new Vector3(-0.09f, 0.22f, 0f), "tray", ResolveObjectScaleForTag("tray"), zoneKey, "table-top", activeNames);
                CreateFurnitureObject(objectRoot, tableName + "_Cup", tablePos + new Vector3(0.10f, 0.26f, 0f), "cup", ResolveObjectScaleForTag("cup"), zoneKey, "table-top", activeNames);
            }

            _zoneDeskPositions[zoneKey] = tablePositions;
        }

        private void EnsureSleepZoneFurniture(RoutineZoneAnchor zone, Transform objectRoot, HashSet<string> activeNames)
        {
            var zoneKey = GetZoneReservationKey(zone);
            var bedCount = Mathf.Clamp(Mathf.Max(3, (characters.Count + 1) / 2), 3, 5);
            var bedPositions = GenerateScatteredPositions(zone, bedCount, 0.24f);
            var bedScale = ResolveObjectScaleForTag("bed");

            for (int i = 0; i < bedPositions.Count; i++)
            {
                var bedName = string.Format(CultureInfo.InvariantCulture, "BedSet_{0:D2}", i + 1);
                var bedPos = bedPositions[i] + new Vector3(0f, (i % 2 == 0 ? -0.02f : 0.04f), 0f);
                CreateFurnitureObject(objectRoot, bedName, bedPos, "bed", bedScale, zoneKey, "sleep-set", activeNames);
                CreateFurnitureObject(objectRoot, bedName + "_Pillow", bedPos + new Vector3(-0.12f, 0.20f, 0f), "pillow", ResolveObjectScaleForTag("pillow"), zoneKey, "bed-top", activeNames);
                CreateFurnitureObject(objectRoot, bedName + "_Blanket", bedPos + new Vector3(0.08f, 0.14f, 0f), "blanket", ResolveObjectScaleForTag("blanket"), zoneKey, "bed-top", activeNames);
            }

            _zoneDeskPositions[zoneKey] = bedPositions;
        }

        private static void SafeDestroyGameObject(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(go);
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (go != null)
                {
                    DestroyImmediate(go);
                }
            };
#else
            Destroy(go);
#endif
        }

        private Vector2 ResolveObjectScaleForTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return objectVisualScale;
            }

            if (string.Equals(tag, "bed", StringComparison.OrdinalIgnoreCase))
            {
                var baseDesk = ResolveObjectScaleForTag("desk");
                return new Vector2(baseDesk.x * 1.35f, baseDesk.y * 0.95f);
            }

            if (string.Equals(tag, "desk", StringComparison.OrdinalIgnoreCase))
            {
                if (_hasDeskTemplateScale)
                {
                    return _templateDeskScale;
                }

                return new Vector2(deskVisualScale.x, deskVisualScale.y * 0.9f);
            }

            if (string.Equals(tag, "table", StringComparison.OrdinalIgnoreCase))
            {
                var baseDesk = ResolveObjectScaleForTag("desk");
                return new Vector2(baseDesk.x * 1.15f, baseDesk.y * 0.85f);
            }

            if (string.Equals(tag, "blanket", StringComparison.OrdinalIgnoreCase))
            {
                var baseDesk = ResolveObjectScaleForTag("desk");
                return new Vector2(baseDesk.x * 0.95f, baseDesk.y * 0.7f);
            }

            if (string.Equals(tag, "computer", StringComparison.OrdinalIgnoreCase))
            {
                if (_hasComputerTemplateScale)
                {
                    return _templateComputerScale;
                }

                return objectVisualScale * 0.95f;
            }

            if (string.Equals(tag, "tray", StringComparison.OrdinalIgnoreCase))
            {
                return objectVisualScale * 0.52f;
            }

            if (string.Equals(tag, "pillow", StringComparison.OrdinalIgnoreCase))
            {
                return objectVisualScale * 0.72f;
            }

            if (string.Equals(tag, "cup", StringComparison.OrdinalIgnoreCase))
            {
                if (_hasCupTemplateScale)
                {
                    return _templateCupScale;
                }

                return objectVisualScale * 0.46f;
            }

            return objectVisualScale;
        }

        private void RefreshMissionFurnitureTemplate(Transform objectRoot)
        {
            _hasDeskTemplateScale = TryReadTemplateScale(objectRoot, "DeskSet_01", out _templateDeskScale);
            _hasComputerTemplateScale = TryReadTemplateScale(objectRoot, "DeskSet_01_Computer", out _templateComputerScale);
            _hasCupTemplateScale = TryReadTemplateScale(objectRoot, "DeskSet_01_Cup", out _templateCupScale);
        }

        private static bool TryReadTemplateScale(Transform root, string name, out Vector2 scale)
        {
            scale = default;
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var target = root.Find(name);
            if (target == null)
            {
                return false;
            }

            var local = target.localScale;
            scale = new Vector2(Mathf.Abs(local.x), Mathf.Abs(local.y));
            return scale.x > 0.0001f && scale.y > 0.0001f;
        }

        private List<Vector3> GenerateScatteredPositions(RoutineZoneAnchor zone, int count, float insetRatio)
        {
            var result = new List<Vector3>(Mathf.Max(1, count));
            count = Mathf.Max(1, count);
            var bounds = zone.GetComponent<BoxCollider2D>()?.bounds;
            if (bounds == null)
            {
                result.Add(WithObjectDepth(zone.Position));
                return result;
            }

            var b = bounds.Value;
            var inset = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.y) * insetRatio, 0.06f, 0.45f);
            var minX = b.min.x + inset;
            var maxX = b.max.x - inset;
            var minY = b.min.y + inset;
            var maxY = b.max.y - inset;

            var minSpacing = Mathf.Max(0.12f, Mathf.Min(maxX - minX, maxY - minY) * 0.25f);
            var minSpacingSqr = minSpacing * minSpacing;
            var rng = new System.Random(zone.GetInstanceID() ^ (count * 397));
            var attempts = count * 30;

            for (int i = 0; i < attempts && result.Count < count; i++)
            {
                var x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
                var y = Mathf.Lerp(minY, maxY, (float)rng.NextDouble());
                var candidate = WithObjectDepth(new Vector3(x, y, objectDepthZ));
                if (!zone.Contains(candidate))
                {
                    continue;
                }

                var overlaps = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if ((result[j] - candidate).sqrMagnitude < minSpacingSqr)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    result.Add(candidate);
                }
            }

            while (result.Count < count)
            {
                var t = (result.Count + 0.5f) / count;
                var x = Mathf.Lerp(minX, maxX, t);
                var y = Mathf.Lerp(minY, maxY, 0.5f);
                result.Add(WithObjectDepth(new Vector3(x, y, objectDepthZ)));
            }

            return result;
        }

        private GameObject CreateFurnitureObject(
            Transform parent,
            string objectName,
            Vector3 worldPosition,
            string tag,
            Vector2 scale,
            string zoneKey,
            string detail,
            HashSet<string> activeNames)
        {
            var existing = parent.Find(objectName);
            var go = existing != null ? existing.gameObject : new GameObject(objectName);
            if (existing == null)
            {
                go.transform.SetParent(parent, false);
            }

            go.transform.position = WithObjectDepth(worldPosition);
            go.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f);
            go.SetActive(true);
            activeNames?.Add(objectName);
            var sprite = visualResources != null ? visualResources.ResolveItemSprite(tag) : null;
            if (sprite == null && zoneGenerationRuleSet != null)
            {
                var resourcePath = zoneGenerationRuleSet.ResolveSpriteResourcePath(tag);
                if (!string.IsNullOrWhiteSpace(resourcePath))
                {
                    sprite = Resources.Load<Sprite>(resourcePath);
                }
            }

            EnsureSpriteRenderer(go, ResolveObjectColor(tag), scale, true, sprite);
            EnsureClickableObjectCollider(go, scale);
            EnsureInspectableWorldObject(go, objectName, zoneKey, tag, detail);
            return go;
        }

        private static Color ResolveObjectColor(string tag)
        {
            if (string.Equals(tag, "bed", StringComparison.OrdinalIgnoreCase)) return new Color(0.73f, 0.88f, 1f, 1f);
            if (string.Equals(tag, "pillow", StringComparison.OrdinalIgnoreCase)) return new Color(0.93f, 0.93f, 0.98f, 1f);
            if (string.Equals(tag, "blanket", StringComparison.OrdinalIgnoreCase)) return new Color(0.54f, 0.76f, 0.94f, 1f);
            if (string.Equals(tag, "table", StringComparison.OrdinalIgnoreCase)) return new Color(0.86f, 0.69f, 0.46f, 1f);
            if (string.Equals(tag, "tray", StringComparison.OrdinalIgnoreCase)) return new Color(0.98f, 0.82f, 0.38f, 1f);
            if (string.Equals(tag, "cup", StringComparison.OrdinalIgnoreCase)) return new Color(0.95f, 0.94f, 0.9f, 1f);
            if (string.Equals(tag, "computer", StringComparison.OrdinalIgnoreCase)) return new Color(0.66f, 0.74f, 0.82f, 1f);
            return new Color(0.86f, 0.86f, 0.86f, 1f);
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
            var anchors = CollectActionAnchorPositions(zone);
            if (anchors.Count == 0)
            {
                anchors.Add(WithObjectDepth(zone.Position));
            }

            _zoneDeskPositions[zoneKey] = anchors;
        }

        private List<Vector3> CollectActionAnchorPositions(RoutineZoneAnchor zone)
        {
            var results = new List<Vector3>();
            var objectRoot = zone != null ? zone.transform.Find(ZoneObjectRootName) : null;
            if (objectRoot == null)
            {
                return results;
            }

            var primaryTag = zone.HasTag(ZoneTagMission)
                ? "desk"
                : zone.HasTag(ZoneTagNeedHunger)
                    ? "table"
                    : zone.HasTag(ZoneTagNeedSleep)
                        ? "bed"
                        : string.Empty;

            for (int i = 0; i < objectRoot.childCount; i++)
            {
                var child = objectRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var inspectable = child.GetComponent<RoutineInspectableWorldObject>();
                if (inspectable == null)
                {
                    continue;
                }

                if (!string.Equals(inspectable.PrimaryTag, primaryTag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(WithObjectDepth(child.position));
            }

            return results;
        }

        private void EnsureClickableObjectCollider(GameObject go, Vector2 targetScale)
        {
            if (go == null)
            {
                return;
            }

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }

            var spriteRenderer = go.GetComponent<SpriteRenderer>();
            var width = Mathf.Max(0.05f, Mathf.Abs(targetScale.x));
            var height = Mathf.Max(0.05f, Mathf.Abs(targetScale.y));
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                var drawSize = spriteRenderer.drawMode == SpriteDrawMode.Simple
                    ? (Vector2)spriteRenderer.sprite.bounds.size
                    : spriteRenderer.size;
                width = Mathf.Max(0.05f, drawSize.x);
                height = Mathf.Max(0.05f, drawSize.y);
            }

            collider.size = new Vector2(width, height);
            collider.offset = Vector2.zero;
            collider.isTrigger = true;
        }

        private static void EnsureInspectableWorldObject(
            GameObject go,
            string displayName,
            string zoneId,
            string primaryTag,
            string detail)
        {
            if (go == null)
            {
                return;
            }

            var inspectable = go.GetComponent<RoutineInspectableWorldObject>();
            if (inspectable == null)
            {
                inspectable = go.AddComponent<RoutineInspectableWorldObject>();
            }

            inspectable.Configure(displayName, zoneId, primaryTag, detail);
        }

        private void EnsureSpriteRenderer(GameObject go, Color color, Vector2 targetScale, bool opaque, Sprite spriteOverride = null)
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

            spriteRenderer.sprite = spriteOverride != null ? spriteOverride : GetRuntimeSquareSprite();
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
            if (actor == null)
            {
                return;
            }

            var position = actor.position;
            position.y = 0f;
            actor.position = position;

            var local = actor.localPosition;
            local.z = 0f;
            actor.localPosition = local;
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

        private Vector3 WithObjectDepth(Vector3 position)
        {
            if (!enforceDepthLayout)
            {
                return position;
            }

            position.y = 0f;
            position.z = objectDepthZ;
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
            binding.mood = Mathf.Clamp(binding.mood, -100f, 100f);

            EnsureSelfTalkText(binding);
            UpdateSelfTalkVisual(binding);
        }

        private void ProcessRelationshipAndMood()
        {
            for (var i = 0; i < characters.Count; i++)
            {
                var source = characters[i];
                if (source.actor == null)
                {
                    continue;
                }

                var socialPenalty = 0f;
                for (var j = 0; j < characters.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    var target = characters[j];
                    if (target.actor == null)
                    {
                        continue;
                    }

                    var distance = Vector3.Distance(source.actor.position, target.actor.position);
                    if (distance > 1.25f)
                    {
                        continue;
                    }

                    if (source.currentAction == RoutineActionType.Mission && target.currentAction == RoutineActionType.Move)
                    {
                        ChangeAffinity(source, target, -0.8f);
                        socialPenalty -= 2.5f;
                    }

                    if (source.currentAction == RoutineActionType.Mission && target.currentAction == RoutineActionType.Mission)
                    {
                        ChangeAffinity(source, target, 0.2f);
                        socialPenalty += 0.4f;
                    }
                }

                var hungerPenalty = source.hunger < source.hungerThreshold ? (source.hungerThreshold - source.hunger) * 0.09f : 0f;
                var sleepPenalty = source.sleep < source.sleepThreshold ? (source.sleepThreshold - source.sleep) * 0.08f : 0f;
                var stressPenalty = source.stress < source.stressThreshold ? (source.stressThreshold - source.stress) * 0.06f : 0f;
                var affinityBias = ComputeAverageAffinity(source) * 0.015f;
                var moodDelta = affinityBias + socialPenalty - hungerPenalty - sleepPenalty - stressPenalty;
                source.mood = Mathf.Clamp(source.mood + moodDelta, -100f, 100f);
                UpdateRuntimeStateTexts(source);
            }
        }

        private float ResolveWorkEfficiencyFactor(RoutineCharacterBinding binding)
        {
            var moodFactor = Mathf.Lerp(0.65f, 1.2f, Mathf.InverseLerp(-100f, 100f, binding.mood));
            var affinityFactor = Mathf.Lerp(0.8f, 1.15f, Mathf.InverseLerp(-60f, 60f, ComputeAverageAffinity(binding)));
            return Mathf.Clamp(moodFactor * affinityFactor, 0.5f, 1.3f);
        }

        private float ComputeAverageAffinity(RoutineCharacterBinding source)
        {
            var sum = 0f;
            var count = 0;
            for (var i = 0; i < characters.Count; i++)
            {
                var target = characters[i];
                if (target == source || target.actor == null)
                {
                    continue;
                }

                sum += GetAffinity(source, target);
                count += 1;
            }

            return count == 0 ? 0f : (sum / count);
        }

        private float GetAffinity(RoutineCharacterBinding source, RoutineCharacterBinding target)
        {
            if (!TryBuildAffinityKey(source, target, out var key))
            {
                return 0f;
            }

            return _affinityScores.TryGetValue(key, out var score) ? score : 0f;
        }

        private void ChangeAffinity(RoutineCharacterBinding source, RoutineCharacterBinding target, float delta)
        {
            if (!TryBuildAffinityKey(source, target, out var key))
            {
                return;
            }

            _affinityScores.TryGetValue(key, out var current);
            _affinityScores[key] = Mathf.Clamp(current + delta, -100f, 100f);
        }

        private static bool TryBuildAffinityKey(RoutineCharacterBinding source, RoutineCharacterBinding target, out string key)
        {
            key = null;
            if (source == null || target == null || source.actor == null || target.actor == null)
            {
                return false;
            }

            key = source.actor.name + "=>" + target.actor.name;
            return true;
        }

        private static string ResolveMoodLabel(float mood)
        {
            if (mood <= -55f)
            {
                return "최악";
            }

            if (mood <= -20f)
            {
                return "짜증";
            }

            if (mood < 20f)
            {
                return "보통";
            }

            if (mood < 55f)
            {
                return "좋음";
            }

            return "최상";
        }

        private static string BuildSelfTalk(RoutineCharacterBinding binding)
        {
            if (binding.hunger < 20f)
            {
                return "배고파... 집중이 안 돼";
            }

            if (binding.sleep < 20f)
            {
                return "너무 피곤해...";
            }

            if (binding.stress < 15f)
            {
                return "오늘은 버겁다...";
            }

            var knowledgeAverage = CalculateAverageKnowledgeConfidence(binding);
            if (binding.currentAction == RoutineActionType.Mission && knowledgeAverage >= 0.78f)
            {
                return "방금 배운 게 연결된다... 이건 된다";
            }

            if (binding.currentAction == RoutineActionType.Mission && HasKnowledgeConfusion(binding))
            {
                return "정보가 엉켜서 헷갈려... 다시 맞춰보자";
            }

            if (binding.currentAction == RoutineActionType.Mission && binding.mood >= 30f)
            {
                return "좋아, 이 흐름대로 가자";
            }

            if (binding.currentAction == RoutineActionType.Move)
            {
                return "잠깐, 자리 좀 옮기자";
            }

            return "일단 해보자";
        }


        private static float CalculateAverageKnowledgeConfidence(RoutineCharacterBinding binding)
        {
            if (binding?.knowledgeMap == null || binding.knowledgeMap.Count == 0)
            {
                return 0f;
            }

            var sum = 0f;
            var count = 0;
            foreach (var pair in binding.knowledgeMap)
            {
                sum += Mathf.Clamp01(pair.Value);
                count += 1;
            }

            return count <= 0 ? 0f : (sum / count);
        }

        private static bool HasKnowledgeConfusion(RoutineCharacterBinding binding)
        {
            if (binding?.knowledgeMap == null || binding.knowledgeMap.Count < 2)
            {
                return false;
            }

            var min = 1f;
            var max = 0f;
            foreach (var pair in binding.knowledgeMap)
            {
                var confidence = Mathf.Clamp01(pair.Value);
                min = Mathf.Min(min, confidence);
                max = Mathf.Max(max, confidence);
            }

            return (max - min) >= 0.35f || CalculateAverageKnowledgeConfidence(binding) <= 0.4f;
        }
        private void EnsureSelfTalkText(RoutineCharacterBinding binding)
        {
            if (binding == null || binding.actor == null || binding.selfTalkTextMesh != null)
            {
                return;
            }

            var textObject = new GameObject("SelfTalkText");
            textObject.transform.SetParent(binding.actor, false);
            textObject.transform.localPosition = new Vector3(0f, SelfTalkHeight, 0f);
            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = SelfTalkTextSize;
            textMesh.fontSize = 36;
            textMesh.color = Color.white;
            textMesh.text = string.Empty;
            binding.selfTalkTextMesh = textMesh;
        }

        private void UpdateSelfTalkVisual(RoutineCharacterBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            EnsureSelfTalkText(binding);
            if (binding.selfTalkTextMesh == null)
            {
                return;
            }

            var line = BuildSelfTalk(binding);
            binding.selfTalkText = string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1})",
                line,
                ResolveMoodLabel(binding.mood));
            binding.selfTalkTextMesh.text = binding.selfTalkText;
            binding.selfTalkTextMesh.color = binding.mood < -20f ? new Color(1f, 0.72f, 0.72f, 1f) : Color.white;
            UpdateSelfTalkTransform(binding);
        }

        private void UpdateSelfTalkTransform(RoutineCharacterBinding binding)
        {
            if (binding == null || binding.selfTalkTextMesh == null)
            {
                return;
            }

            var textTransform = binding.selfTalkTextMesh.transform;
            textTransform.localPosition = new Vector3(0f, SelfTalkHeight, 0f);

            var cameraToUse = interactionCamera != null ? interactionCamera : Camera.main;
            if (cameraToUse == null)
            {
                return;
            }

            var cameraForward = cameraToUse.transform.forward;
            textTransform.rotation = Quaternion.LookRotation(cameraForward, Vector3.up);
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

            if (IsPointerOverUi())
            {
                return;
            }

            var cameraToUse = interactionCamera != null ? interactionCamera : Camera.main;
            if (cameraToUse == null)
            {
                Debug.LogWarning("[RoutineMVP][Click] pointer-down detected but no camera is available.");
                return;
            }

            var worldPoint = cameraToUse.ScreenToWorldPoint(new Vector3(pointerScreenPosition.x, pointerScreenPosition.y, Mathf.Abs(cameraToUse.transform.position.z)));
            var hits = Physics2D.OverlapPointAll(new Vector2(worldPoint.x, worldPoint.y));
            var hitNames = new StringBuilder();
            if (hits != null)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i] == null)
                    {
                        continue;
                    }

                    if (hitNames.Length > 0)
                    {
                        hitNames.Append(", ");
                    }

                    hitNames.Append(hits[i].name);
                }
            }

            if (hits == null || hits.Length == 0)
            {
                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[RoutineMVP][Click] screen=({0:0.0},{1:0.0}) world=({2:0.00},{3:0.00}) hits=0",
                    pointerScreenPosition.x,
                    pointerScreenPosition.y,
                    worldPoint.x,
                    worldPoint.y));
                return;
            }

            for (int i = 0; i < characters.Count; i++)
            {
                var binding = characters[i];
                if (binding.actor == null)
                {
                    continue;
                }

                for (int j = 0; j < hits.Length; j++)
                {
                    var hit = hits[j];
                    if (hit == null)
                    {
                        continue;
                    }

                    if (hit.transform == binding.actor || hit.transform.IsChildOf(binding.actor))
                    {
                        Debug.Log(string.Format(
                            CultureInfo.InvariantCulture,
                            "[RoutineMVP][Click] character selected name={0} hits={1} [{2}]",
                            binding.actor.name,
                            hits.Length,
                            hitNames.ToString()));
                        _selectedCharacter = binding;
                        _pinnedNeuronCharacter = binding;
                        UpdateNeuronPanel();
                        return;
                    }
                }
            }

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                var selectedObject = hit.GetComponentInParent<RoutineInspectableWorldObject>();
                if (selectedObject != null)
                {
                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "[RoutineMVP][Click] object selected name={0} tag={1} zone={2} hits={3} [{4}]",
                        selectedObject.DisplayName,
                        selectedObject.PrimaryTag,
                        selectedObject.ZoneId,
                        hits.Length,
                        hitNames.ToString()));
                    _selectedWorldObject = selectedObject;
                    UpdateObjectInfoPanel();
                    return;
                }
            }

            if (TrySelectWorldObjectByProximity(worldPoint, out var nearbyObject))
            {
                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[RoutineMVP][Click] object selected (fallback) name={0} tag={1} zone={2}",
                    nearbyObject.DisplayName,
                    nearbyObject.PrimaryTag,
                    nearbyObject.ZoneId));
                _selectedWorldObject = nearbyObject;
                UpdateObjectInfoPanel();
                return;
            }

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                var zone = hit.GetComponentInParent<RoutineZoneAnchor>();
                if (zone != null)
                {
                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "[RoutineMVP][Click] zone selected zoneId={0} hits={1} [{2}]",
                        zone.ZoneId,
                        hits.Length,
                        hitNames.ToString()));
                    return;
                }
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[RoutineMVP][Click] no selectable target hits={0} [{1}]",
                hits.Length,
                hitNames.ToString()));
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void HandleObjectInfoCloseInput()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseLastOpenedPanel();
                return;
            }
#endif
#if ENABLE_INPUT_SYSTEM
            var keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType == null)
            {
                return;
            }

            var keyboard = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var escapeKey = TryReadProperty(keyboard, "escapeKey");
            if (TryReadBoolProperty(escapeKey, "wasPressedThisFrame", out var pressed) && pressed)
            {
                CloseLastOpenedPanel();
            }
#endif
        }

        private void CloseLastOpenedPanel()
        {
            if (_lastOpenedPanel == PanelKind.Object)
            {
                CloseObjectInfoPanel();
                return;
            }

            if (_lastOpenedPanel == PanelKind.Neuron)
            {
                CloseNeuronPanel();
            }
        }

        private void CloseObjectInfoPanel()
        {
            _selectedWorldObject = null;
            UpdateObjectInfoPanel();
            if (_lastOpenedPanel == PanelKind.Object)
            {
                _lastOpenedPanel = PanelKind.None;
            }
        }

        private void CloseNeuronPanel()
        {
            _pinnedNeuronCharacter = null;
            if (neuronPanelView != null)
            {
                neuronPanelView.Hide();
            }

            if (_lastOpenedPanel == PanelKind.Neuron)
            {
                _lastOpenedPanel = PanelKind.None;
            }
        }

        private static bool TrySelectWorldObjectByProximity(Vector3 worldPoint, out RoutineInspectableWorldObject selected)
        {
            selected = null;
            var candidates = FindObjectsByType<RoutineInspectableWorldObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (candidates == null || candidates.Length == 0)
            {
                return false;
            }

            var click2D = new Vector2(worldPoint.x, worldPoint.y);
            var bestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var sr = candidate.GetComponent<SpriteRenderer>();
                if (sr != null && sr.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, sr.bounds.center.z)))
                {
                    selected = candidate;
                    return true;
                }

                var p = candidate.transform.position;
                var distanceSqr = (new Vector2(p.x, p.y) - click2D).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    selected = candidate;
                }
            }

            if (selected == null)
            {
                return false;
            }

            // Keep fallback conservative to avoid selecting far-away objects on empty clicks.
            return bestDistanceSqr <= 0.20f * 0.20f;
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

            if (_pinnedNeuronCharacter == null || !_latestNeuronSnapshots.TryGetValue(_pinnedNeuronCharacter, out var snapshot))
            {
                return;
            }

            var actorName = _pinnedNeuronCharacter.actor != null ? _pinnedNeuronCharacter.actor.name : string.Empty;
            var aptitudeLine = BuildAptitudeLine(actorName);
            var subtaskLine = BuildCurrentSubtaskLine();
            var viewModel = RoutineNeuronPanelViewModel.FromSnapshot(
                snapshot,
                _pinnedNeuronCharacter.currentAction.ToString(),
                _pinnedNeuronCharacter.intendedAction.ToString(),
                aptitudeLine,
                subtaskLine);
            neuronPanelView.Render(viewModel);
            _lastOpenedPanel = PanelKind.Neuron;
        }

        private string BuildAptitudeLine(string actorName)
        {
            if (string.IsNullOrWhiteSpace(actorName) || !_characterAptitudeByActorName.TryGetValue(actorName, out var profile) || profile == null)
            {
                return "apt:-";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "apt:{0}/{1} mult(O:{2:0.0},L:{3:0.0},R:{4:0.0},X:{5:0.0})",
                profile.PrimaryType,
                profile.SecondaryType,
                profile.ObserveMultiplier,
                profile.LaborMultiplier,
                profile.RoutineMultiplier,
                profile.ReflexMultiplier);
        }

        private string BuildCurrentSubtaskLine()
        {
            var subtask = _currentDynamicTask != null ? _currentDynamicTask.FirstIncomplete() : null;
            if (subtask == null)
            {
                return "task:-";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "task:{0} {1}/{2}",
                subtask.WorkType,
                subtask.Progress,
                subtask.RequiredWork);
        }

        private void UpdateObjectInfoPanel()
        {
            if (objectInfoPanelView == null)
            {
                return;
            }

            if (_selectedWorldObject == null)
            {
                objectInfoPanelView.Hide();
                return;
            }

            var title = string.Format(CultureInfo.InvariantCulture, "Object | {0}", _selectedWorldObject.DisplayName);
            var body = string.Format(
                CultureInfo.InvariantCulture,
                "tag: {0}\nzone: {1}\n{2}",
                _selectedWorldObject.PrimaryTag,
                _selectedWorldObject.ZoneId,
                string.IsNullOrWhiteSpace(_selectedWorldObject.Detail) ? "detail: -" : "detail: " + _selectedWorldObject.Detail);
            objectInfoPanelView.Render(title, body);
            _lastOpenedPanel = PanelKind.Object;
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
