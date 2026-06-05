using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProjectW.IngameCore.CaseReview
{
    public sealed class CaseReviewMvpSceneController : MonoBehaviour
    {
        private const int MaxLogLines = 28;

        private readonly List<string> visibleLogLines = new();
        private readonly List<Button> actionButtons = new();
        private readonly Dictionary<string, List<string>> plannedAssignments = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DebugDeckState> debugDecks = new(StringComparer.OrdinalIgnoreCase);

        private const string SampleScenarioResourcePath = "CaseReviewData/Scenarios/Events/Scenario_TeaAudit";
        private const float ScenarioTypewriterCharactersPerSecond = 42f;
        private const float WorkPerformanceAutoSeconds = 1.8f;

        private Text statusText;
        private Text boardTitleText;
        private Text actionTitleText;
        private Text actionHintText;
        private Text debugGaugeText;
        private Text staffTitleText;
        private Text cardHandTitleText;
        private Text logText;
        private Text milestoneText;
        private Text scenarioTitleText;
        private Text scenarioSpeakerText;
        private Text scenarioBodyText;
        private Text scenarioAutoButtonText;
        private Text workSceneTitleText;
        private Text workSceneWorkText;
        private Text workSceneActorText;
        private Text workSceneCardText;
        private Text workSceneImpactText;
        private Text workSceneProgressText;
        private Transform actionButtonRoot;
        private Transform boardCardRoot;
        private Transform rosterRoot;
        private Transform cardHandRoot;
        private Transform scenarioPortraitRoot;
        private Transform scenarioChoiceRoot;
        private Transform workSceneActorRoot;
        private Transform workSceneImpactRoot;
        private Transform dragLayer;
        private GameObject scenarioOverlay;
        private GameObject workSceneOverlay;
        private Font uiFont;
        private ScenarioEventDefinition sampleScenario;
        private ScenarioPlaybackSession scenarioSession;
        private float scenarioTypewriterAccumulator;
        private bool scenarioAutoPlay;
        private readonly List<WorkPerformanceEvent> workPerformanceEvents = new();
        private int workPerformanceIndex;
        private float workPerformanceTimer;
        private string selectedPersonnelId = "";
        private int cardStateDay = -1;

        public GameState CurrentState { get; private set; }

        public IReadOnlyList<string> VisibleLogLines => visibleLogLines;

        private void Awake()
        {
            EnsureEventSystem();
            BuildUi();
            InitializeForTests();
        }

        private void Update()
        {
            if (scenarioSession is null || scenarioSession.IsEventComplete)
            {
                UpdateWorkPerformanceOverlay();
            }
            else
            {
                UpdateScenarioOverlay();
                UpdateWorkPerformanceOverlay();
            }
        }

        private void UpdateScenarioOverlay()
        {
            if (scenarioSession is null || scenarioSession.IsEventComplete)
            {
                return;
            }

            scenarioTypewriterAccumulator += Time.unscaledDeltaTime * ScenarioTypewriterCharactersPerSecond;
            var characterCount = Mathf.FloorToInt(scenarioTypewriterAccumulator);
            if (characterCount > 0)
            {
                scenarioTypewriterAccumulator -= characterCount;
                scenarioSession.AdvanceTypewriter(characterCount);
                RenderScenarioOverlay();
            }

            if (scenarioAutoPlay && scenarioSession.IsLineComplete)
            {
                scenarioSession.TickAutoPlay();
                RenderScenarioOverlay();
            }
        }

        private void UpdateWorkPerformanceOverlay()
        {
            if (workSceneOverlay is null || !workSceneOverlay.activeSelf || workPerformanceEvents.Count == 0)
            {
                return;
            }

            workPerformanceTimer += Time.unscaledDeltaTime;
            RenderWorkPerformanceOverlay();
        }

        public void InitializeForTests(int seed = 1)
        {
            CurrentState = CaseReviewGame.Init(new GameConfig(), seed);
            visibleLogLines.Clear();
            selectedPersonnelId = CurrentState.Staff.FirstOrDefault(person => !person.HasLeft)?.Id ?? "";
            cardStateDay = -1;
            debugDecks.Clear();
            scenarioSession = null;
            scenarioAutoPlay = false;
            workPerformanceEvents.Clear();
            workPerformanceIndex = 0;
            workPerformanceTimer = 0f;
            SyncAssignmentsFromPlan();
            EnsureCardStateForToday();
            HideScenarioOverlay();
            HideWorkPerformanceOverlay();
            AddLog("MVP cycle started. Continue day by day until an ending condition appears.");
            Render();
        }

        public void ClickShowPlan()
        {
            Dispatch("plan");
        }

        public void ClickOpenPriorityWork()
        {
            var id = FirstActiveEventId();
            if (string.IsNullOrWhiteSpace(id))
            {
                AddLog("No active work is available.");
                Render();
                return;
            }

            Dispatch($"open {id}");
            Dispatch($"summary {id}");
        }

        public void ClickRecommendedAdjust()
        {
            if (CurrentState is null)
            {
                return;
            }

            var entry = CurrentState.MorningPlan?.Entries?.FirstOrDefault();
            if (entry is null)
            {
                AddLog("No morning plan entry is available.");
                Render();
                return;
            }

            var people = CurrentState.Staff
                .Where(person => !person.HasLeft)
                .OrderBy(person => person.LoadAssigned)
                .ThenByDescending(person => person.TrustToManager)
                .Take(2)
                .Select(person => person.Id)
                .ToList();

            if (people.Count == 0)
            {
                AddLog("No available personnel for adjustment.");
                Render();
                return;
            }

            plannedAssignments[entry.EventId] = people;
            SyncPlanAdjustment(entry.EventId);
            Render();
        }

        public void ClickConfirmPlan()
        {
            SyncAllPlanAdjustments();
            var workEvents = UseRandomCardsForAssignedWork();
            Dispatch("confirm plan");
            if (workEvents.Count > 0)
            {
                BeginWorkPerformanceOverlay(workEvents);
            }
        }

        public void ClickReportDay()
        {
            if (CurrentState?.Slot == Slot.Evening)
            {
                AddNightSummaryLog();
                Render();
                return;
            }

            Dispatch("report");
        }

        public void ClickReportNextEvent()
        {
            var target = CurrentState?.Queue
                .Where(item => item.AutoResolved)
                .OrderBy(item => item.ReportReviewed)
                .ThenByDescending(item => item.Severity + item.Urgency)
                .FirstOrDefault();

            if (target is null)
            {
                AddLog("No resolved event report is available.");
                Render();
                return;
            }

            Dispatch($"report {target.Id}");
        }

        public void ClickReviewAll()
        {
            Dispatch("review all");
        }

        public void ClickNextDay()
        {
            AutoReviewNightReports();
            Dispatch("next day");
            if (CurrentState is not null && CurrentState.Slot == Slot.Morning)
            {
                SyncAssignmentsFromPlan();
                EnsureCardStateForToday();
            }
        }

        public void ClickPlaySampleScenario()
        {
            sampleScenario ??= Resources.Load<ScenarioEventDefinition>(SampleScenarioResourcePath);
            if (sampleScenario is null)
            {
                AddLog($"Scenario sample not found: Resources/{SampleScenarioResourcePath}");
                Render();
                return;
            }

            scenarioSession = new ScenarioPlaybackSession(sampleScenario, "ko", "KR");
            scenarioTypewriterAccumulator = 0f;
            scenarioAutoPlay = false;
            AddLog($"Scenario sample opened: {sampleScenario.EventId}");
            RenderScenarioOverlay();
        }

        public void ClickScenarioNext()
        {
            if (scenarioSession is null)
            {
                return;
            }

            scenarioSession.Click();
            scenarioTypewriterAccumulator = 0f;
            if (scenarioSession.IsEventComplete)
            {
                AddLog("Scenario sample completed.");
                HideScenarioOverlay();
                Render();
                return;
            }

            RenderScenarioOverlay();
        }

        public void ClickScenarioSkip()
        {
            if (scenarioSession is null)
            {
                return;
            }

            scenarioSession.Skip();
            AddLog("Scenario sample skipped.");
            HideScenarioOverlay();
            Render();
        }

        public void ClickScenarioToggleAuto()
        {
            if (scenarioSession is null)
            {
                return;
            }

            scenarioAutoPlay = !scenarioAutoPlay;
            scenarioSession.SetAutoPlay(scenarioAutoPlay);
            RenderScenarioOverlay();
        }

        public void ClickWorkSceneNext()
        {
            if (workPerformanceEvents.Count == 0)
            {
                HideWorkPerformanceOverlay();
                return;
            }

            if (workPerformanceIndex >= workPerformanceEvents.Count - 1)
            {
                AddLog("Work performance scene completed.");
                HideWorkPerformanceOverlay();
                Render();
                return;
            }

            workPerformanceIndex++;
            workPerformanceTimer = 0f;
            RenderWorkPerformanceOverlay();
        }

        public void ClickWorkSceneSkip()
        {
            AddLog("Work performance scene skipped.");
            HideWorkPerformanceOverlay();
            Render();
        }

        public void SelectPersonnel(string personnelId)
        {
            if (CurrentState?.Staff.Any(person => person.Id.Equals(personnelId, StringComparison.OrdinalIgnoreCase)) != true)
            {
                return;
            }

            selectedPersonnelId = personnelId;
            Render();
        }

        public void DropPersonnelOnWork(string personnelId, string eventId, string sourceEventId)
        {
            if (CurrentState?.Slot != Slot.Morning)
            {
                AddLog("Drag assignment is only available in the morning.");
                Render();
                return;
            }

            var item = FindEvent(eventId);
            if (item is null)
            {
                return;
            }

            var assignment = AssignmentFor(eventId);
            var maxSlots = Math.Max(1, item.MaxPersonnelCount);
            if (assignment.Any(id => id.Equals(personnelId, StringComparison.OrdinalIgnoreCase)))
            {
                AddLog($"{personnelId} is already assigned to {eventId}.");
                Render();
                return;
            }

            if (assignment.Count >= maxSlots)
            {
                AddLog($"{eventId} slots are full ({assignment.Count}/{maxSlots}).");
                Render();
                return;
            }

            if (!string.IsNullOrWhiteSpace(sourceEventId) && !sourceEventId.Equals(eventId, StringComparison.OrdinalIgnoreCase))
            {
                RemovePersonnelFromWork(personnelId, sourceEventId, renderAfter: false);
            }

            assignment.Add(personnelId);
            SyncPlanAdjustment(eventId);
            AddLog($"{eventId} slot filled: {assignment.Count}/{maxSlots}");
            Render();
        }

        public void DropPersonnelOnRoster(string personnelId, string sourceEventId)
        {
            if (string.IsNullOrWhiteSpace(sourceEventId))
            {
                SelectPersonnel(personnelId);
                return;
            }

            RemovePersonnelFromWork(personnelId, sourceEventId, renderAfter: true);
        }

        public void RemovePersonnelFromWork(string personnelId, string eventId)
        {
            RemovePersonnelFromWork(personnelId, eventId, renderAfter: true);
        }

        private void Dispatch(string command)
        {
            if (CurrentState is null)
            {
                return;
            }

            AddLog($"> {command.ToUpperInvariant()}");
            var result = CaseReviewGame.Dispatch(CurrentState, command);
            if (result.Lines.Count == 0)
            {
                AddLog(result.Success ? "OK." : result.Code);
            }
            else
            {
                foreach (var line in result.Lines)
                {
                    AddLog(line);
                }
            }

            Render();
        }

        private void Render()
        {
            if (CurrentState is null || statusText is null)
            {
                return;
            }

            statusText.text = BuildStatusLine();
            milestoneText.text = CurrentState.Slot == Slot.Morning
                ? "Loop continues: assign work, confirm, read the night summary."
                : "Night summary only. Start the next morning when ready.";

            EnsureCardStateForToday();
            RenderBoard();
            RenderDebugGauges();
            RenderRoster();
            RenderCardHand();
            RenderActions();
            logText.text = string.Join("\n", visibleLogLines);
            if (scenarioSession is not null)
            {
                RenderScenarioOverlay();
            }

            if (workSceneOverlay is not null && workSceneOverlay.activeSelf)
            {
                RenderWorkPerformanceOverlay();
            }
        }

        private string BuildStatusLine()
        {
            var activeQueue = CurrentState.Queue.Count(item => item.Status != CaseStatus.Closed);
            return $"DAY {CurrentState.Day:00} | {CurrentState.Slot.ToString().ToUpperInvariant()} | Queue {activeQueue}/{CurrentState.Config.QueueSoftCap} | OVR {CurrentState.Overload} | AI Pressure {CurrentState.ReplacementPressure} | Redirect {CurrentState.RedirectBudget} | Audit {CurrentState.AuditBudget} | Interview {CurrentState.InterviewBudget}";
        }

        private void RenderBoard()
        {
            ClearDynamicRoot(boardCardRoot);
            if (CurrentState.Slot == Slot.Morning)
            {
                boardTitleText.text = CurrentState.MorningPlan.Confirmed ? "Work Slots - Confirmed" : "Work Slots";
                foreach (var entry in CurrentState.MorningPlan.Entries)
                {
                    CreateWorkCard(entry);
                }

                return;
            }

            boardTitleText.text = "Night Summary";
            CreateNightSummaryCard();
        }

        private static string FormatResolvedEvent(EventCase item)
        {
            var review = item.ReportReviewed ? "Reviewed" : "Needs review";
            var risk = item.LatentRisk >= 60 ? "High" : item.LatentRisk >= 30 ? "Medium" : "Low";
            return $"{item.Id}  {item.Title}\n{review} | Outcome {item.OutcomeScore} | Risk {risk}\n{item.ResultSummary}";
        }

        private void RenderDebugGauges()
        {
            var activeQueue = CurrentState.Queue.Count(item => item.Status != CaseStatus.Closed);
            var lines = new List<string>
            {
                "Gauge Debug",
                GaugeLine("Overload", CurrentState.Overload, 100),
                GaugeLine("Global Risk", CurrentState.GlobalLatentRisk, 200),
                GaugeLine("AI Pressure", CurrentState.ReplacementPressure, 100),
                GaugeLine("Talent Gap", CurrentState.TalentShortage, 10),
                GaugeLine("Queue", activeQueue, CurrentState.Config.QueueHardCap),
                GaugeLine("Redirect", CurrentState.RedirectBudget, CurrentState.Config.RedirectBudgetPerDay),
                GaugeLine("Audit", CurrentState.AuditBudget, CurrentState.Config.AuditBudgetPerDay),
                GaugeLine("Interview", CurrentState.InterviewBudget, CurrentState.Config.InterviewBudgetPerDay)
            };

            lines.Add("People");
            lines.AddRange(CurrentState.Staff
                .Where(person => !person.HasLeft)
                .Take(4)
                .Select(person =>
                    $"{person.Id} LOAD {Bar(person.LoadAssigned, Math.Max(1, person.MaxLoad))} {person.LoadAssigned}/{Math.Max(1, person.MaxLoad)}  " +
                    $"FAT {Bar(person.Fatigue, 100)} {person.Fatigue:000}  " +
                    $"TRUST {Bar(person.TrustToManager, 100)} {person.TrustToManager:000}  " +
                    $"RISK {Bar(person.RetentionRisk, 100)} {person.RetentionRisk:000}"));

            lines.Add("Work");
            lines.AddRange(CurrentState.Queue
                .Where(item => item.Status != CaseStatus.Closed || item.AutoResolved)
                .OrderByDescending(item => item.Urgency + item.Severity + item.LatentRisk)
                .Take(4)
                .Select(item =>
                    $"{item.Id} URG {Bar(item.Urgency, 100)} {item.Urgency:000}  " +
                    $"SEV {Bar(item.Severity, 100)} {item.Severity:000}  " +
                    $"RISK {Bar(item.LatentRisk, 100)} {item.LatentRisk:000}  " +
                    $"OUT {Bar(item.OutcomeScore, 100)} {item.OutcomeScore:000}"));

            debugGaugeText.text = string.Join("\n", lines);
        }

        private void RenderRoster()
        {
            ClearDynamicRoot(rosterRoot);
            staffTitleText.text = "Characters";
            foreach (var person in CurrentState.Staff.Where(person => !person.HasLeft))
            {
                CreateCharacterToken(person, rosterRoot, "", selectedPersonnelId.Equals(person.Id, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void RenderCardHand()
        {
            ClearDynamicRoot(cardHandRoot);
            var person = CurrentState.Staff.FirstOrDefault(item => item.Id.Equals(selectedPersonnelId, StringComparison.OrdinalIgnoreCase))
                ?? CurrentState.Staff.FirstOrDefault(item => !item.HasLeft);
            if (person is null)
            {
                cardHandTitleText.text = "Today Cards";
                return;
            }

            selectedPersonnelId = person.Id;
            var deck = DeckFor(person.Id);
            cardHandTitleText.text = $"{person.Name}'s Today Cards ({deck.TodayHand.Count}/5 from {deck.Pool.Count})";
            foreach (var card in deck.TodayHand)
            {
                CreateCardFace(card, cardHandRoot, deck.UsedToday.Contains(card.Id));
            }
        }

        private void RenderActions()
        {
            foreach (var button in actionButtons)
            {
                Destroy(button.gameObject);
            }

            actionButtons.Clear();

            if (CurrentState.Slot == Slot.Morning)
            {
                actionTitleText.text = "Morning: drag people into work slots";
                actionHintText.text = CurrentState.MorningPlan.Confirmed
                    ? "The plan is confirmed. Operations have moved to evening."
                    : "Drag a character from the roster into a work slot. Drag a slotted token back to Characters to remove it.";
                AddActionButton("Plan", ClickShowPlan, true);
                AddActionButton("Open Priority", ClickOpenPriorityWork, !CurrentState.MorningPlan.Confirmed);
                AddActionButton("Recommended Adjust", ClickRecommendedAdjust, !CurrentState.MorningPlan.Confirmed);
                AddActionButton("Confirm Plan", ClickConfirmPlan, !CurrentState.MorningPlan.Confirmed);
                AddActionButton("Scenario Sample", ClickPlaySampleScenario, true);
                return;
            }

            actionTitleText.text = "Night: summary only";
            actionHintText.text = "Skip detailed review. Read the summary, then continue the loop.";

            AddActionButton("Show Summary", ClickReportDay, true);
            AddActionButton("Next Morning", ClickNextDay, true);
            AddActionButton("Scenario Sample", ClickPlaySampleScenario, true);
        }

        private void AddActionButton(string label, UnityEngine.Events.UnityAction action, bool interactable)
        {
            var buttonObject = CreateUiObject(label + " Button", actionButtonRoot);
            var image = buttonObject.AddComponent<Image>();
            image.color = interactable ? new Color(0.25f, 0.36f, 0.44f, 1f) : new Color(0.16f, 0.18f, 0.2f, 1f);

            var button = buttonObject.AddComponent<Button>();
            button.interactable = interactable;
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.minHeight = 46;
            layout.flexibleWidth = 1;

            var labelText = CreateText(label, buttonObject.transform, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform);

            actionButtons.Add(button);
        }

        private void BuildUi()
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = CreateUiObject("MVP Cycle Canvas", transform);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var root = CreatePanel("Root", canvasObject.transform, new Color(0.06f, 0.07f, 0.08f, 1f));
            Stretch((RectTransform)root);
            var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(18, 18, 16, 16);
            rootLayout.spacing = 12;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            var statusPanel = CreatePanel("Status Bar", root, new Color(0.10f, 0.12f, 0.13f, 1f));
            statusPanel.gameObject.AddComponent<LayoutElement>().minHeight = 58;
            statusText = CreateText("", statusPanel, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            statusText.rectTransform.offsetMin = new Vector2(16, 0);
            statusText.rectTransform.offsetMax = new Vector2(-16, 0);

            var debugPanel = CreatePanel("Gauge Debug", root, new Color(0.07f, 0.08f, 0.09f, 1f));
            debugPanel.gameObject.AddComponent<LayoutElement>().minHeight = 164;
            debugGaugeText = CreateText("", debugPanel, 12, FontStyle.Normal, TextAnchor.UpperLeft);
            debugGaugeText.rectTransform.offsetMin = new Vector2(14, 10);
            debugGaugeText.rectTransform.offsetMax = new Vector2(-14, -10);

            var body = CreateUiObject("Body", root);
            var bodyLayoutElement = body.AddComponent<LayoutElement>();
            bodyLayoutElement.flexibleHeight = 1;
            bodyLayoutElement.minHeight = 360;
            var bodyLayout = body.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 12;
            bodyLayout.childForceExpandHeight = true;
            bodyLayout.childForceExpandWidth = false;

            var board = CreateColumn(body.transform, "Work Board", 0.48f, 620);
            boardTitleText = CreateHeader("Work Slots", board);
            boardCardRoot = CreateDynamicRoot("Work Cards", board);

            var roster = CreateColumn(body.transform, "Characters", 0.22f, 280);
            staffTitleText = CreateHeader("Characters", roster);
            rosterRoot = CreateDynamicRoot("Character Tokens", roster);
            roster.gameObject.AddComponent<RosterDropTarget>().Initialize(this);

            var hand = CreateColumn(body.transform, "Today Cards", 0.17f, 240);
            cardHandTitleText = CreateHeader("Today Cards", hand);
            cardHandRoot = CreateDynamicRoot("Card Faces", hand);

            var actions = CreateColumn(body.transform, "Action Guide", 0.20f, 260);
            actionTitleText = CreateHeader("Actions", actions);
            actionHintText = CreateText("", actions, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            actionHintText.gameObject.AddComponent<LayoutElement>().minHeight = 58;
            actionButtonRoot = CreateUiObject("Action Buttons", actions).transform;
            var actionButtonLayout = actionButtonRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            actionButtonLayout.spacing = 8;
            actionButtonLayout.childForceExpandWidth = true;
            actionButtonLayout.childForceExpandHeight = false;
            actionButtonRoot.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            milestoneText = CreateText("", actions, 14, FontStyle.Bold, TextAnchor.LowerLeft);
            milestoneText.gameObject.AddComponent<LayoutElement>().minHeight = 56;

            var logPanel = CreatePanel("Command Log", root, new Color(0.08f, 0.09f, 0.10f, 1f));
            logPanel.gameObject.AddComponent<LayoutElement>().minHeight = 170;
            logText = CreateText("", logPanel, 13, FontStyle.Normal, TextAnchor.UpperLeft);
            logText.rectTransform.offsetMin = new Vector2(14, 12);
            logText.rectTransform.offsetMax = new Vector2(-14, -12);

            dragLayer = CreateUiObject("Drag Layer", canvasObject.transform).transform;
            Stretch((RectTransform)dragLayer);
            dragLayer.SetAsLastSibling();

            BuildScenarioOverlay(canvasObject.transform);
            BuildWorkPerformanceOverlay(canvasObject.transform);
            dragLayer.SetAsLastSibling();
        }

        private void BuildScenarioOverlay(Transform parent)
        {
            scenarioOverlay = CreateUiObject("Scenario Sample Overlay", parent);
            Stretch((RectTransform)scenarioOverlay.transform);
            var blocker = scenarioOverlay.AddComponent<Image>();
            blocker.color = new Color(0.02f, 0.025f, 0.03f, 0.94f);

            var topBar = CreatePanel("Scenario Top Bar", scenarioOverlay.transform, new Color(0.07f, 0.08f, 0.09f, 0.92f));
            var topRect = (RectTransform)topBar;
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 66f);
            topRect.anchoredPosition = Vector2.zero;
            scenarioTitleText = CreateText("Scenario", topBar, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            scenarioTitleText.rectTransform.offsetMin = new Vector2(24f, 0f);
            scenarioTitleText.rectTransform.offsetMax = new Vector2(-360f, 0f);

            var skipButton = CreateOverlayButton("Skip", topBar, new Vector2(-210f, -33f), ClickScenarioSkip);
            ((RectTransform)skipButton.transform).sizeDelta = new Vector2(120f, 42f);
            var autoButton = CreateOverlayButton("Auto", topBar, new Vector2(-76f, -33f), ClickScenarioToggleAuto);
            ((RectTransform)autoButton.transform).sizeDelta = new Vector2(120f, 42f);
            scenarioAutoButtonText = autoButton.GetComponentInChildren<Text>();

            scenarioPortraitRoot = CreateUiObject("Portrait Stage", scenarioOverlay.transform).transform;
            var stageRect = (RectTransform)scenarioPortraitRoot;
            stageRect.anchorMin = new Vector2(0.03f, 0.28f);
            stageRect.anchorMax = new Vector2(0.97f, 0.86f);
            stageRect.offsetMin = Vector2.zero;
            stageRect.offsetMax = Vector2.zero;

            var textBox = CreatePanel("Scenario Text Box", scenarioOverlay.transform, new Color(0.08f, 0.09f, 0.10f, 0.98f));
            var textBoxRect = (RectTransform)textBox;
            textBoxRect.anchorMin = new Vector2(0.08f, 0.04f);
            textBoxRect.anchorMax = new Vector2(0.92f, 0.24f);
            textBoxRect.offsetMin = Vector2.zero;
            textBoxRect.offsetMax = Vector2.zero;

            scenarioSpeakerText = CreateText("", textBox, 18, FontStyle.Bold, TextAnchor.UpperLeft);
            scenarioSpeakerText.rectTransform.anchorMin = new Vector2(0f, 1f);
            scenarioSpeakerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            scenarioSpeakerText.rectTransform.pivot = new Vector2(0.5f, 1f);
            scenarioSpeakerText.rectTransform.offsetMin = new Vector2(22f, -48f);
            scenarioSpeakerText.rectTransform.offsetMax = new Vector2(-160f, -12f);

            scenarioBodyText = CreateText("", textBox, 24, FontStyle.Normal, TextAnchor.UpperLeft);
            scenarioBodyText.rectTransform.offsetMin = new Vector2(22f, 24f);
            scenarioBodyText.rectTransform.offsetMax = new Vector2(-160f, -58f);

            var nextButton = CreateOverlayButton("Next", textBox, new Vector2(-76f, 46f), ClickScenarioNext);
            ((RectTransform)nextButton.transform).sizeDelta = new Vector2(116f, 56f);

            scenarioChoiceRoot = CreateUiObject("Scenario Choices", textBox).transform;
            var choiceRect = (RectTransform)scenarioChoiceRoot;
            choiceRect.anchorMin = new Vector2(0f, 0f);
            choiceRect.anchorMax = new Vector2(1f, 0f);
            choiceRect.pivot = new Vector2(0.5f, 0f);
            choiceRect.offsetMin = new Vector2(22f, 10f);
            choiceRect.offsetMax = new Vector2(-160f, 52f);
            var choicesLayout = scenarioChoiceRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            choicesLayout.spacing = 8f;
            choicesLayout.childForceExpandWidth = false;
            choicesLayout.childForceExpandHeight = true;

            scenarioOverlay.SetActive(false);
        }

        private void BuildWorkPerformanceOverlay(Transform parent)
        {
            workSceneOverlay = CreateUiObject("Work Performance Overlay", parent);
            Stretch((RectTransform)workSceneOverlay.transform);
            var blocker = workSceneOverlay.AddComponent<Image>();
            blocker.color = new Color(0.025f, 0.030f, 0.034f, 0.95f);

            var topBar = CreatePanel("Work Scene Top Bar", workSceneOverlay.transform, new Color(0.07f, 0.08f, 0.09f, 0.94f));
            var topRect = (RectTransform)topBar;
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 70f);
            topRect.anchoredPosition = Vector2.zero;
            workSceneTitleText = CreateText("Work Performance", topBar, 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            workSceneTitleText.rectTransform.offsetMin = new Vector2(24f, 0f);
            workSceneTitleText.rectTransform.offsetMax = new Vector2(-360f, 0f);

            var skipButton = CreateOverlayButton("Skip", topBar, new Vector2(-210f, -35f), ClickWorkSceneSkip);
            ((RectTransform)skipButton.transform).sizeDelta = new Vector2(120f, 42f);
            var nextButton = CreateOverlayButton("Next", topBar, new Vector2(-76f, -35f), ClickWorkSceneNext);
            ((RectTransform)nextButton.transform).sizeDelta = new Vector2(120f, 42f);

            var stage = CreateUiObject("Work Scene Stage", workSceneOverlay.transform).transform;
            var stageRect = (RectTransform)stage;
            stageRect.anchorMin = new Vector2(0.04f, 0.14f);
            stageRect.anchorMax = new Vector2(0.96f, 0.88f);
            stageRect.offsetMin = Vector2.zero;
            stageRect.offsetMax = Vector2.zero;

            var actorPanel = CreatePanel("Worker Panel", stage, new Color(0.12f, 0.17f, 0.20f, 1f));
            var actorRect = (RectTransform)actorPanel;
            actorRect.anchorMin = new Vector2(0f, 0.10f);
            actorRect.anchorMax = new Vector2(0.27f, 0.90f);
            actorRect.offsetMin = Vector2.zero;
            actorRect.offsetMax = Vector2.zero;
            workSceneActorRoot = CreateUiObject("Worker Body", actorPanel).transform;
            Stretch((RectTransform)workSceneActorRoot);
            workSceneActorText = CreateText("", workSceneActorRoot, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            workSceneActorText.rectTransform.offsetMin = new Vector2(18f, 18f);
            workSceneActorText.rectTransform.offsetMax = new Vector2(-18f, -18f);

            var workPanel = CreatePanel("Work Panel", stage, new Color(0.10f, 0.11f, 0.13f, 1f));
            var workRect = (RectTransform)workPanel;
            workRect.anchorMin = new Vector2(0.31f, 0.20f);
            workRect.anchorMax = new Vector2(0.66f, 0.80f);
            workRect.offsetMin = Vector2.zero;
            workRect.offsetMax = Vector2.zero;
            workSceneWorkText = CreateText("", workPanel, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            workSceneWorkText.rectTransform.offsetMin = new Vector2(22f, 18f);
            workSceneWorkText.rectTransform.offsetMax = new Vector2(-22f, -18f);

            var impactPanel = CreatePanel("Impact Panel", stage, new Color(0.14f, 0.13f, 0.17f, 1f));
            var impactRect = (RectTransform)impactPanel;
            impactRect.anchorMin = new Vector2(0.70f, 0.10f);
            impactRect.anchorMax = new Vector2(1f, 0.90f);
            impactRect.offsetMin = Vector2.zero;
            impactRect.offsetMax = Vector2.zero;
            workSceneImpactRoot = CreateUiObject("Impact Body", impactPanel).transform;
            Stretch((RectTransform)workSceneImpactRoot);
            workSceneCardText = CreateText("", workSceneImpactRoot, 17, FontStyle.Bold, TextAnchor.UpperLeft);
            workSceneCardText.rectTransform.offsetMin = new Vector2(20f, 230f);
            workSceneCardText.rectTransform.offsetMax = new Vector2(-20f, -20f);
            workSceneImpactText = CreateText("", workSceneImpactRoot, 16, FontStyle.Normal, TextAnchor.UpperLeft);
            workSceneImpactText.rectTransform.offsetMin = new Vector2(20f, 80f);
            workSceneImpactText.rectTransform.offsetMax = new Vector2(-20f, -220f);
            workSceneProgressText = CreateText("", workSceneImpactRoot, 16, FontStyle.Bold, TextAnchor.LowerLeft);
            workSceneProgressText.rectTransform.offsetMin = new Vector2(20f, 20f);
            workSceneProgressText.rectTransform.offsetMax = new Vector2(-20f, -340f);

            workSceneOverlay.SetActive(false);
        }

        private Button CreateOverlayButton(string label, Transform parent, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = CreateUiObject(label + " Button", parent);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(110f, 42f);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.30f, 0.36f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = CreateText(label, buttonObject.transform, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            return button;
        }

        private void RenderScenarioOverlay()
        {
            if (scenarioOverlay is null || scenarioSession is null)
            {
                return;
            }

            scenarioOverlay.SetActive(true);
            scenarioTitleText.text = $"{scenarioSession.CurrentLine.Source?.LineId ?? "END"} | {sampleScenario?.EventId ?? "scenario"}";
            scenarioSpeakerText.text = string.IsNullOrWhiteSpace(scenarioSession.CurrentLine.Source?.SpeakerId)
                ? "Narration"
                : scenarioSession.CurrentLine.Source.SpeakerId;
            scenarioBodyText.text = scenarioSession.VisibleText;
            if (scenarioAutoButtonText is not null)
            {
                scenarioAutoButtonText.text = scenarioAutoPlay ? "Auto On" : "Auto";
            }

            RenderScenarioPortraits();
            RenderScenarioChoices();
        }

        private void HideScenarioOverlay()
        {
            scenarioSession = null;
            scenarioAutoPlay = false;
            scenarioTypewriterAccumulator = 0f;
            if (scenarioOverlay is not null)
            {
                scenarioOverlay.SetActive(false);
            }
        }

        private void RenderScenarioPortraits()
        {
            ClearDynamicRoot(scenarioPortraitRoot);
            if (scenarioSession?.StageState is null)
            {
                return;
            }

            foreach (var portrait in scenarioSession.StageState.Portraits)
            {
                var panel = CreatePanel("Portrait " + portrait.PortraitId, scenarioPortraitRoot, PortraitColor(portrait));
                var rect = (RectTransform)panel;
                rect.anchorMin = new Vector2(portrait.NormalizedX, 0.5f);
                rect.anchorMax = new Vector2(portrait.NormalizedX, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(portrait.IsFocused ? 300f : 260f, portrait.IsFocused ? 430f : 390f);

                var label = portrait.PortraitId;
                if (portrait.IsMoving)
                {
                    label += $"\nmove {portrait.PreviousNormalizedX:0.00}->{portrait.NormalizedX:0.00}";
                }

                if (portrait.IsFocused)
                {
                    label += "\nSPEAKER";
                }
                else if (portrait.IsDimmed)
                {
                    label += "\nDIM";
                }

                var text = CreateText(label, panel, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
                text.raycastTarget = false;
            }
        }

        private void RenderScenarioChoices()
        {
            ClearDynamicRoot(scenarioChoiceRoot);
            var choices = scenarioSession?.CurrentLine.Source?.Choices;
            if (choices is null || choices.Count == 0 || !scenarioSession.IsLineComplete)
            {
                return;
            }

            foreach (var choice in choices)
            {
                var label = ResolveScenarioChoiceLabel(choice);
                var buttonObject = CreateUiObject("Choice " + choice.ChoiceId, scenarioChoiceRoot);
                var image = buttonObject.AddComponent<Image>();
                image.color = new Color(0.20f, 0.22f, 0.25f, 1f);
                var button = buttonObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() =>
                {
                    AddLog($"Scenario choice selected: {choice.ChoiceId}");
                    ClickScenarioNext();
                });
                var layout = buttonObject.AddComponent<LayoutElement>();
                layout.minWidth = 220f;
                layout.minHeight = 42f;
                var text = CreateText(label, buttonObject.transform, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
                text.raycastTarget = false;
            }
        }

        private string ResolveScenarioChoiceLabel(ScenarioChoice choice)
        {
            if (choice is null || sampleScenario is null)
            {
                return "";
            }

            return sampleScenario.TextTable != null
                ? sampleScenario.TextTable.GetText(choice.LabelTextKey, "ko", "KR")
                : choice.LabelTextKey;
        }

        private static Color PortraitColor(ScenarioPortraitState portrait)
        {
            if (portrait.IsFocused)
            {
                return new Color(0.32f, 0.48f, 0.56f, 1f);
            }

            return portrait.IsDimmed
                ? new Color(0.10f, 0.11f, 0.12f, 0.78f)
                : new Color(0.18f, 0.24f, 0.28f, 1f);
        }

        private void BeginWorkPerformanceOverlay(List<WorkPerformanceEvent> events)
        {
            workPerformanceEvents.Clear();
            workPerformanceEvents.AddRange(events);
            HydrateWorkPerformanceResults();
            workPerformanceIndex = 0;
            workPerformanceTimer = 0f;
            RenderWorkPerformanceOverlay();
        }

        private void HydrateWorkPerformanceResults()
        {
            foreach (var performance in workPerformanceEvents)
            {
                var item = FindEvent(performance.EventId);
                if (item is null)
                {
                    continue;
                }

                performance.OutcomeAfter = item.OutcomeScore;
                performance.RiskAfter = item.LatentRisk;
                performance.ResultSummary = item.ResultSummary;
            }
        }

        private void RenderWorkPerformanceOverlay()
        {
            if (workSceneOverlay is null || workPerformanceEvents.Count == 0)
            {
                return;
            }

            var performance = workPerformanceEvents[Mathf.Clamp(workPerformanceIndex, 0, workPerformanceEvents.Count - 1)];
            var progress = Mathf.Clamp01(workPerformanceTimer / WorkPerformanceAutoSeconds);
            workSceneOverlay.SetActive(true);
            workSceneTitleText.text = $"Work Performance {workPerformanceIndex + 1}/{workPerformanceEvents.Count}";
            workSceneActorText.text = $"{performance.PersonnelId}\n{performance.PersonnelName}\n\nused";
            workSceneWorkText.text = $"{performance.EventId}\n{performance.WorkTitle}\n\n{performance.ResultSummary}";
            workSceneCardText.text = BuildHandRevealText(performance, progress);
            workSceneImpactText.text =
                $"Outcome {performance.OutcomeBefore} -> {LerpInt(performance.OutcomeBefore, performance.OutcomeAfter, progress)} ({Signed(performance.OutcomeModifier)})\n" +
                $"Risk {performance.RiskBefore} -> {LerpInt(performance.RiskBefore, performance.RiskAfter, progress)} ({Signed(performance.RiskModifier)})\n\n" +
                $"Selected: {performance.CardTitle}\n{performance.CardSummary}";
            workSceneProgressText.text =
                $"OUT {Bar(LerpInt(0, Mathf.Abs(performance.OutcomeModifier), progress), 12)} {Signed(performance.OutcomeModifier)}\n" +
                $"RISK {Bar(LerpInt(0, Mathf.Abs(performance.RiskModifier), progress), 12)} {Signed(performance.RiskModifier)}";
        }

        private void HideWorkPerformanceOverlay()
        {
            workPerformanceEvents.Clear();
            workPerformanceIndex = 0;
            workPerformanceTimer = 0f;
            if (workSceneOverlay is not null)
            {
                workSceneOverlay.SetActive(false);
            }
        }

        private static string BuildHandRevealText(WorkPerformanceEvent performance, float progress)
        {
            var revealSelection = progress >= 0.45f;
            var lines = new List<string> { "TODAY HAND" };
            foreach (var card in performance.HandCards)
            {
                var marker = card.IsUsed
                    ? revealSelection ? "> USED" : "> ???"
                    : "  ";
                lines.Add($"{marker} {card.Title} | OUT {Signed(card.OutcomeModifier)} RISK {Signed(card.RiskModifier)}");
            }

            if (!revealSelection)
            {
                lines.Add("");
                lines.Add("choosing...");
            }

            return string.Join("\n", lines);
        }

        private Transform CreateColumn(Transform parent, string name, float flexibleWidth, float minWidth)
        {
            var panel = CreatePanel(name, parent, new Color(0.11f, 0.13f, 0.15f, 1f));
            var layoutElement = panel.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = flexibleWidth;
            layoutElement.minWidth = minWidth;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        private Text CreateHeader(string value, Transform parent)
        {
            var text = CreateText(value, parent, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            text.gameObject.AddComponent<LayoutElement>().minHeight = 28;
            return text;
        }

        private Text CreateBodyText(Transform parent)
        {
            var text = CreateText("", parent, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            text.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            return text;
        }

        private Transform CreatePanel(string name, Transform parent, Color color)
        {
            var panel = CreateUiObject(name, parent).transform;
            var image = panel.gameObject.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        private Text CreateText(string value, Transform parent, int fontSize, FontStyle style, TextAnchor alignment)
        {
            var textObject = CreateUiObject("Text", parent);
            var text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = uiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.89f, 0.91f, 0.90f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(text.rectTransform);
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var instance = new GameObject(name, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private Transform CreateDynamicRoot(string name, Transform parent)
        {
            var root = CreateUiObject(name, parent).transform;
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            root.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            return root;
        }

        private void ClearDynamicRoot(Transform root)
        {
            if (root is null)
            {
                return;
            }

            for (var index = root.childCount - 1; index >= 0; index--)
            {
                Destroy(root.GetChild(index).gameObject);
            }
        }

        private void CreateWorkCard(WorkPlanEntry entry)
        {
            var item = FindEvent(entry.EventId);
            if (item is null)
            {
                return;
            }

            var assignment = AssignmentFor(entry.EventId);
            var maxSlots = Math.Max(1, item.MaxPersonnelCount);
            var panel = CreatePanel("Work " + entry.EventId, boardCardRoot, new Color(0.14f, 0.16f, 0.18f, 1f));
            panel.gameObject.AddComponent<LayoutElement>().minHeight = 134;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var remaining = item.Status == CaseStatus.Closed ? 0 : Math.Max(1, item.Volume);
            var tags = WorkTags(item);
            CreateText($"{item.Id}  {item.Title}", panel, 16, FontStyle.Bold, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().minHeight = 22;
            CreateText($"Remaining {remaining} | Slots {assignment.Count}/{maxSlots} | Tags {tags}", panel, 13, FontStyle.Normal, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().minHeight = 20;
            CreateText($"URG {item.Urgency}  SEV {item.Severity}  RISK {item.LatentRisk}  TTL {Math.Max(0, item.TtlSec)}s", panel, 12, FontStyle.Normal, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().minHeight = 18;

            var slots = CreateUiObject("Slots", panel).transform;
            var slotLayout = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
            slotLayout.spacing = 6;
            slotLayout.childForceExpandWidth = true;
            slotLayout.childForceExpandHeight = false;
            slots.gameObject.AddComponent<LayoutElement>().minHeight = 48;

            for (var slotIndex = 0; slotIndex < maxSlots; slotIndex++)
            {
                var slot = CreatePanel($"Slot {slotIndex + 1}", slots, new Color(0.09f, 0.10f, 0.11f, 1f));
                slot.gameObject.AddComponent<LayoutElement>().minHeight = 46;
                slot.gameObject.AddComponent<WorkSlotDropTarget>().Initialize(this, entry.EventId);

                if (slotIndex < assignment.Count)
                {
                    var person = CurrentState.Staff.FirstOrDefault(candidate => candidate.Id.Equals(assignment[slotIndex], StringComparison.OrdinalIgnoreCase));
                    if (person is not null)
                    {
                        CreateCharacterToken(person, slot, entry.EventId, false);
                    }
                }
                else
                {
                    var label = CreateText("Drop", slot, 13, FontStyle.Normal, TextAnchor.MiddleCenter);
                    label.color = new Color(0.45f, 0.50f, 0.52f, 1f);
                }
            }
        }

        private void CreateReportCard(EventCase item)
        {
            var panel = CreatePanel("Report " + item.Id, boardCardRoot, new Color(0.14f, 0.16f, 0.18f, 1f));
            panel.gameObject.AddComponent<LayoutElement>().minHeight = 104;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6;

            CreateText(FormatResolvedEvent(item), panel, 13, FontStyle.Normal, TextAnchor.UpperLeft)
                .gameObject.AddComponent<LayoutElement>().minHeight = 86;
        }

        private void CreateNightSummaryCard()
        {
            var resolved = CurrentState.Queue.Where(item => item.AutoResolved).ToList();
            var closed = CurrentState.Queue.Count(item => item.Status == CaseStatus.Closed);
            var open = CurrentState.Queue.Count(item => item.Status != CaseStatus.Closed);
            var averageOutcome = resolved.Count == 0 ? 0 : Mathf.RoundToInt((float)resolved.Average(item => item.OutcomeScore));
            var highestRisk = resolved.OrderByDescending(item => item.LatentRisk).FirstOrDefault();
            var pendingReviewCount = resolved.Count(item => !item.ReportReviewed);

            var panel = CreatePanel("Night Summary Card", boardCardRoot, new Color(0.14f, 0.16f, 0.18f, 1f));
            panel.gameObject.AddComponent<LayoutElement>().minHeight = 220;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateText($"DAY {CurrentState.Day:00} SUMMARY", panel, 20, FontStyle.Bold, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().minHeight = 28;
            CreateText($"Resolved {resolved.Count} | Closed {closed} | Open {open} | Avg Outcome {averageOutcome} | OVR {CurrentState.Overload} | Global Risk {CurrentState.GlobalLatentRisk}", panel, 14, FontStyle.Normal, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().minHeight = 24;
            CreateText($"Highest risk: {(highestRisk is null ? "none" : $"{highestRisk.Id} {highestRisk.Title} / risk {highestRisk.LatentRisk}")}", panel, 14, FontStyle.Normal, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().minHeight = 24;
            CreateText($"Detailed review is hidden for MVP flow. Pending reports will be auto-cleared on Next Morning: {pendingReviewCount}", panel, 13, FontStyle.Italic, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().minHeight = 24;

            foreach (var item in resolved.OrderByDescending(item => item.Severity + item.Urgency).Take(4))
            {
                CreateText($"{item.Id} | OUT {item.OutcomeScore} | RISK {item.LatentRisk} | {item.ResultSummary}", panel, 13, FontStyle.Normal, TextAnchor.UpperLeft)
                    .gameObject.AddComponent<LayoutElement>().minHeight = 34;
            }
        }

        private void CreateCharacterToken(Personnel person, Transform parent, string sourceEventId, bool selected)
        {
            var token = CreatePanel("Character " + person.Id, parent, selected ? new Color(0.23f, 0.30f, 0.36f, 1f) : new Color(0.17f, 0.20f, 0.22f, 1f));
            token.gameObject.AddComponent<LayoutElement>().minHeight = 42;
            if (parent.GetComponent<WorkSlotDropTarget>() is not null)
            {
                Stretch((RectTransform)token);
            }

            var drag = token.gameObject.AddComponent<DraggableCharacterToken>();
            drag.Initialize(this, person.Id, sourceEventId, dragLayer);

            var text = CreateText($"{person.Id} {person.Name}\nLoad {person.LoadAssigned}/{Math.Max(1, person.MaxLoad)} | Fat {person.Fatigue} | Trust {person.TrustToManager}", token, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
        }

        private void CreateCardFace(DebugCard card, Transform parent, bool used)
        {
            var panel = CreatePanel("Card " + card.Id, parent, used ? new Color(0.11f, 0.11f, 0.11f, 1f) : new Color(0.18f, 0.15f, 0.22f, 1f));
            panel.gameObject.AddComponent<LayoutElement>().minHeight = 66;
            var text = CreateText($"{card.Title}\n{string.Join(", ", card.Tags)} | OUT {Signed(card.OutcomeModifier)} RISK {Signed(card.RiskModifier)}\n{(used ? "USED" : card.Summary)}", panel, 12, used ? FontStyle.Italic : FontStyle.Normal, TextAnchor.MiddleLeft);
            text.rectTransform.offsetMin = new Vector2(8, 4);
            text.rectTransform.offsetMax = new Vector2(-8, -4);
            text.color = used ? new Color(0.52f, 0.52f, 0.52f, 1f) : new Color(0.91f, 0.89f, 0.94f, 1f);
        }

        private void SyncAssignmentsFromPlan()
        {
            plannedAssignments.Clear();
            if (CurrentState?.MorningPlan?.Entries is null)
            {
                return;
            }

            foreach (var entry in CurrentState.MorningPlan.Entries)
            {
                plannedAssignments[entry.EventId] = entry.PlannedPersonnel.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        private List<string> AssignmentFor(string eventId)
        {
            if (!plannedAssignments.TryGetValue(eventId, out var assignment))
            {
                var entry = CurrentState?.MorningPlan?.Entries.FirstOrDefault(item => item.EventId.Equals(eventId, StringComparison.OrdinalIgnoreCase));
                assignment = entry?.PlannedPersonnel.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
                plannedAssignments[eventId] = assignment;
            }

            return assignment;
        }

        private void SyncPlanAdjustment(string eventId)
        {
            var assignment = AssignmentFor(eventId);
            if (assignment.Count == 0)
            {
                DispatchWithoutRender($"adjust {eventId} none");
                return;
            }

            DispatchWithoutRender($"adjust {eventId} {string.Join(",", assignment)}");
        }

        private void SyncAllPlanAdjustments()
        {
            if (CurrentState?.Slot != Slot.Morning)
            {
                return;
            }

            foreach (var eventId in plannedAssignments.Keys.ToList())
            {
                SyncPlanAdjustment(eventId);
            }
        }

        private void DispatchWithoutRender(string command)
        {
            AddLog($"> {command.ToUpperInvariant()}");
            var result = CaseReviewGame.Dispatch(CurrentState, command);
            foreach (var line in result.Lines)
            {
                AddLog(line);
            }
        }

        private void RemovePersonnelFromWork(string personnelId, string eventId, bool renderAfter)
        {
            var assignment = AssignmentFor(eventId);
            var removed = assignment.RemoveAll(id => id.Equals(personnelId, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                SyncPlanAdjustment(eventId);
                AddLog($"{personnelId} removed from {eventId}.");
            }

            if (renderAfter)
            {
                Render();
            }
        }

        private List<WorkPerformanceEvent> UseRandomCardsForAssignedWork()
        {
            var performances = new List<WorkPerformanceEvent>();
            if (CurrentState?.Slot != Slot.Morning)
            {
                return performances;
            }

            var random = new System.Random(CurrentState.Seed + CurrentState.Day * 1009);
            var activeCards = new List<ActionCard>();
            foreach (var entry in CurrentState.MorningPlan.Entries)
            {
                var item = FindEvent(entry.EventId);
                foreach (var personId in AssignmentFor(entry.EventId))
                {
                    var deck = DeckFor(personId);
                    var available = deck.TodayHand.Where(card => !deck.UsedToday.Contains(card.Id)).ToList();
                    if (available.Count == 0)
                    {
                        AddLog($"{personId} has no unused cards for {entry.EventId}.");
                        continue;
                    }

                    var used = available[random.Next(available.Count)];
                    deck.UsedToday.Add(used.Id);
                    activeCards.Add(ToRuntimeCard(used, personId, entry.EventId));
                    var person = CurrentState.Staff.FirstOrDefault(candidate => candidate.Id.Equals(personId, StringComparison.OrdinalIgnoreCase));
                    performances.Add(new WorkPerformanceEvent
                    {
                        EventId = entry.EventId,
                        WorkTitle = item?.Title ?? entry.EventId,
                        PersonnelId = personId,
                        PersonnelName = person?.Name ?? personId,
                        CardTitle = used.Title,
                        CardSummary = used.Summary,
                        Tags = used.Tags.ToList(),
                        HandCards = deck.TodayHand.Select(card => new WorkPerformanceCardSnapshot
                        {
                            Title = card.Title,
                            OutcomeModifier = card.OutcomeModifier,
                            RiskModifier = card.RiskModifier,
                            IsUsed = card.Id.Equals(used.Id, StringComparison.OrdinalIgnoreCase)
                        }).ToList(),
                        OutcomeBefore = item?.OutcomeScore ?? 0,
                        RiskBefore = item?.LatentRisk ?? 0,
                        OutcomeModifier = used.OutcomeModifier,
                        RiskModifier = used.RiskModifier
                    });
                    AddLog($"{entry.EventId}: {personId} used card [{used.Title}]");
                }
            }

            CurrentState.MorningCards = activeCards;
            return performances;
        }

        private void AutoReviewNightReports()
        {
            if (CurrentState?.Slot != Slot.Evening)
            {
                return;
            }

            if (!CurrentState.Queue.Any(item => item.AutoResolved && !item.ReportReviewed))
            {
                return;
            }

            DispatchWithoutRender("review all");
            AddLog("Night review details skipped. Reports auto-cleared for MVP flow.");
        }

        private void AddNightSummaryLog()
        {
            if (CurrentState is null)
            {
                return;
            }

            var resolved = CurrentState.Queue.Where(item => item.AutoResolved).ToList();
            var averageOutcome = resolved.Count == 0 ? 0 : Mathf.RoundToInt((float)resolved.Average(item => item.OutcomeScore));
            var highestRisk = resolved.OrderByDescending(item => item.LatentRisk).FirstOrDefault();
            AddLog($"DAY {CurrentState.Day:00} SUMMARY | Resolved {resolved.Count} | Avg Outcome {averageOutcome} | OVR {CurrentState.Overload} | Risk {CurrentState.GlobalLatentRisk}");
            if (highestRisk is not null)
            {
                AddLog($"Focus: {highestRisk.Id} risk {highestRisk.LatentRisk} / {highestRisk.ResultSummary}");
            }
        }

        private void EnsureCardStateForToday()
        {
            if (CurrentState is null || cardStateDay == CurrentState.Day)
            {
                return;
            }

            cardStateDay = CurrentState.Day;
            foreach (var person in CurrentState.Staff.Where(person => !person.HasLeft))
            {
                debugDecks[person.Id] = GenerateDeck(person);
            }
        }

        private DebugDeckState DeckFor(string personnelId)
        {
            EnsureCardStateForToday();
            return debugDecks.TryGetValue(personnelId, out var deck) ? deck : new DebugDeckState();
        }

        private DebugDeckState GenerateDeck(Personnel person)
        {
            var templates = DebugCardTemplates();
            var random = new System.Random(StableHash($"{CurrentState.Seed}:{CurrentState.Day}:{person.Id}:deck"));
            var pool = new List<DebugCard>();
            for (var index = 0; index < 20; index++)
            {
                var template = templates[random.Next(templates.Count)];
                pool.Add(new DebugCard
                {
                    Id = $"{person.Id}-C{index + 1:00}",
                    Title = template.Title,
                    Summary = template.Summary,
                    Tags = template.Tags.ToList(),
                    OutcomeModifier = template.OutcomeModifier,
                    RiskModifier = template.RiskModifier
                });
            }

            return new DebugDeckState
            {
                Pool = pool,
                TodayHand = pool.OrderBy(_ => random.Next()).Take(5).ToList()
            };
        }

        private static List<DebugCard> DebugCardTemplates()
        {
            return new List<DebugCard>
            {
                new() { Title = "Fast Triage", Summary = "Cuts setup time.", Tags = new List<string> { "speed", "review" }, OutcomeModifier = 8, RiskModifier = 3 },
                new() { Title = "Second Pair", Summary = "Adds cross-check discipline.", Tags = new List<string> { "audit", "team" }, OutcomeModifier = 5, RiskModifier = -7 },
                new() { Title = "Shortcut Patch", Summary = "Skips a slow protocol.", Tags = new List<string> { "speed", "unsafe" }, OutcomeModifier = 12, RiskModifier = 10 },
                new() { Title = "Quiet Notes", Summary = "Finds hidden context.", Tags = new List<string> { "intel", "memory" }, OutcomeModifier = 4, RiskModifier = -4 },
                new() { Title = "Stress Buffer", Summary = "Protects morale under load.", Tags = new List<string> { "care", "fatigue" }, OutcomeModifier = 3, RiskModifier = -6 },
            };
        }

        private static ActionCard ToRuntimeCard(DebugCard card, string personnelId, string eventId)
        {
            return new ActionCard
            {
                Id = card.Id,
                OwnerPersonnelId = personnelId,
                TargetEventId = eventId,
                Title = card.Title,
                Summary = card.Summary,
                Tags = card.Tags.ToList(),
                OutcomeModifier = card.OutcomeModifier,
                RiskModifier = card.RiskModifier
            };
        }

        private EventCase FindEvent(string eventId)
        {
            return CurrentState?.Queue.FirstOrDefault(item => item.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase));
        }

        private static string WorkTags(EventCase item)
        {
            var tags = new List<string> { item.Kind, item.Subsystem };
            tags.AddRange(item.Tags);
            tags.AddRange(item.PerkTags);
            return string.Join(", ", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).Take(5));
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 23;
                foreach (var character in value)
                {
                    hash = hash * 31 + character;
                }

                return Math.Abs(hash);
            }
        }

        private void AddLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            visibleLogLines.Add(line);
            while (visibleLogLines.Count > MaxLogLines)
            {
                visibleLogLines.RemoveAt(0);
            }
        }

        private string FirstActiveEventId()
        {
            return CurrentState?.Queue
                .Where(item => item.Status != CaseStatus.Closed)
                .OrderByDescending(item => item.Urgency + item.Severity)
                .Select(item => item.Id)
                .FirstOrDefault() ?? "";
        }

        private static string Signed(int value)
        {
            return value >= 0 ? "+" + value : value.ToString();
        }

        private static string GaugeLine(string label, int value, int max)
        {
            return $"{label,-12} {Bar(value, max)} {value:000}/{Math.Max(1, max):000}";
        }

        private static string Bar(int value, int max)
        {
            const int width = 12;
            var safeMax = Math.Max(1, max);
            var filled = Mathf.Clamp(Mathf.RoundToInt(width * Mathf.Clamp01(value / (float)safeMax)), 0, width);
            return "[" + new string('#', filled) + new string('.', width - filled) + "]";
        }

        private static int LerpInt(int from, int to, float progress)
        {
            return Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.Clamp01(progress)));
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    internal sealed class WorkSlotDropTarget : MonoBehaviour
    {
        public CaseReviewMvpSceneController Controller { get; private set; }
        public string EventId { get; private set; } = "";

        public void Initialize(CaseReviewMvpSceneController controller, string eventId)
        {
            Controller = controller;
            EventId = eventId;
        }
    }

    internal sealed class RosterDropTarget : MonoBehaviour
    {
        public CaseReviewMvpSceneController Controller { get; private set; }

        public void Initialize(CaseReviewMvpSceneController controller)
        {
            Controller = controller;
        }
    }

    [RequireComponent(typeof(CanvasGroup))]
    internal sealed class DraggableCharacterToken : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private CaseReviewMvpSceneController controller;
        private string personnelId = "";
        private string sourceEventId = "";
        private Transform dragLayer;
        private RectTransform dragLayerRect;
        private RectTransform ghost;
        private CanvasGroup canvasGroup;

        public void Initialize(CaseReviewMvpSceneController owner, string id, string sourceWorkId, Transform dragRoot)
        {
            controller = owner;
            personnelId = id;
            sourceEventId = sourceWorkId ?? "";
            dragLayer = dragRoot;
            dragLayerRect = dragRoot as RectTransform;
            canvasGroup = EnsureCanvasGroup();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            controller.SelectPersonnel(personnelId);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            canvasGroup = EnsureCanvasGroup();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.45f;
            var ghostObject = new GameObject("Drag " + personnelId, typeof(RectTransform));
            ghost = ghostObject.GetComponent<RectTransform>();
            ghost.SetParent(dragLayer != null ? dragLayer : transform.parent, false);
            ghost.SetAsLastSibling();
            ghost.sizeDelta = new Vector2(120, 44);
            var image = ghostObject.AddComponent<Image>();
            image.color = new Color(0.28f, 0.38f, 0.46f, 0.86f);
            image.raycastTarget = false;
            MoveGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup = EnsureCanvasGroup();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            if (ghost is not null)
            {
                Destroy(ghost.gameObject);
            }

            var workSlot = RaycastFor<WorkSlotDropTarget>(eventData);
            if (workSlot is not null)
            {
                workSlot.Controller.DropPersonnelOnWork(personnelId, workSlot.EventId, sourceEventId);
                return;
            }

            var roster = RaycastFor<RosterDropTarget>(eventData);
            if (roster is not null)
            {
                roster.Controller.DropPersonnelOnRoster(personnelId, sourceEventId);
            }
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (ghost is null)
            {
                return;
            }

            if (dragLayerRect is not null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayerRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                ghost.anchoredPosition = localPoint;
                return;
            }

            ghost.position = eventData.position;
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            var group = gameObject.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private static T RaycastFor<T>(PointerEventData eventData) where T : Component
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            foreach (var hit in hits)
            {
                var component = hit.gameObject.GetComponentInParent<T>();
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }

    internal sealed class DebugDeckState
    {
        public List<DebugCard> Pool { get; set; } = new();
        public List<DebugCard> TodayHand { get; set; } = new();
        public HashSet<string> UsedToday { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class DebugCard
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public int OutcomeModifier { get; set; }
        public int RiskModifier { get; set; }
    }

    internal sealed class WorkPerformanceEvent
    {
        public string EventId { get; set; } = "";
        public string WorkTitle { get; set; } = "";
        public string PersonnelId { get; set; } = "";
        public string PersonnelName { get; set; } = "";
        public string CardTitle { get; set; } = "";
        public string CardSummary { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public List<WorkPerformanceCardSnapshot> HandCards { get; set; } = new();
        public int OutcomeBefore { get; set; }
        public int OutcomeAfter { get; set; }
        public int RiskBefore { get; set; }
        public int RiskAfter { get; set; }
        public int OutcomeModifier { get; set; }
        public int RiskModifier { get; set; }
        public string ResultSummary { get; set; } = "";
    }

    internal sealed class WorkPerformanceCardSnapshot
    {
        public string Title { get; set; } = "";
        public int OutcomeModifier { get; set; }
        public int RiskModifier { get; set; }
        public bool IsUsed { get; set; }
    }
}
