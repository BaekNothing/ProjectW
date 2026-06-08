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
        private readonly Dictionary<string, List<string>> plannedAssignments = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DebugDeckState> debugDecks = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<MvpDesktopWindow> openWindows = new();
        private readonly Dictionary<MvpDesktopWindow, WindowLayoutState> windowLayouts = new();

        private const string SampleScenarioResourcePath = "CaseReviewData/Scenarios/Events/Scenario_TeaAudit";
        private const float ScenarioTypewriterCharactersPerSecond = 42f;
        private const float WorkPerformanceAutoSeconds = 1.8f;
        private const int MinUiFontSize = 30;
        private const float WindowMinWidth = 520f;
        private const float WindowMinHeight = 360f;
        private static readonly Color DeskColor = new(0.18f, 0.14f, 0.10f, 1f);
        private static readonly Color CrtShellColor = new(0.075f, 0.095f, 0.085f, 1f);
        private static readonly Color CrtPanelColor = new(0.045f, 0.075f, 0.065f, 1f);
        private static readonly Color CrtTextColor = new(0.68f, 0.95f, 0.73f, 1f);
        private static readonly Color FolderColor = new(0.72f, 0.58f, 0.34f, 1f);
        private static readonly Color FolderDarkColor = new(0.50f, 0.38f, 0.22f, 1f);
        private static readonly Color PaperColor = new(0.82f, 0.78f, 0.66f, 1f);
        private static readonly Color PaperTextColor = new(0.17f, 0.15f, 0.12f, 1f);
        private static readonly Color IdCardColor = new(0.76f, 0.82f, 0.80f, 1f);
        private static readonly Color TerminalButtonColor = new(0.13f, 0.20f, 0.17f, 1f);
        private static readonly Color WarningStampColor = new(0.58f, 0.12f, 0.10f, 1f);

        private Text statusText;
        private Text boardTitleText;
        private Text debugGaugeText;
        private Text staffTitleText;
        private Text cardHandTitleText;
        private Text logText;
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
        private Transform desktopObjectRoot;
        private Transform windowLayer;
        private RectTransform assignmentPickerPanelRect;
        private Transform boardCardRoot;
        private Transform rosterRoot;
        private Transform cardHandRoot;
        private Transform scenarioPortraitRoot;
        private Transform scenarioMeetingContentRoot;
        private Transform scenarioChoiceRoot;
        private Transform workSceneActorRoot;
        private Transform workSceneImpactRoot;
        private Transform dragLayer;
        private GameObject scenarioOverlay;
        private GameObject workSceneOverlay;
        private Text scenarioMeetingEffectText;
        private Font uiFont;
        private ScenarioEventDefinition sampleScenario;
        private ScenarioPlaybackSession scenarioSession;
        private string scenarioMeetingLayoutSignature = "";
        private float scenarioTypewriterAccumulator;
        private bool scenarioAutoPlay;
        private readonly List<WorkPerformanceEvent> workPerformanceEvents = new();
        private int workPerformanceIndex;
        private float workPerformanceTimer;
        private string selectedPersonnelId = "";
        private string selectedWorkId = "";
        private string assignmentPickerEventId = "";
        private int assignmentPickerSlotIndex = -1;
        private bool showPlanApprovalModal;
        private bool pendingDailyReportAfterWork;
        private MvpDesktopWindow focusedWindow = MvpDesktopWindow.None;
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

        private void RenderDesktopActionButtons()
        {
            if (windowLayer is null)
            {
                return;
            }

            var actions = CreateUiObject("Desktop Action Buttons", windowLayer).transform;
            var rect = (RectTransform)actions;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(330f, 184f);
            rect.anchoredPosition = new Vector2(-28f, 28f);
            var layout = actions.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateDesktopActionButton("STAMP APPROVED\nStart Work", actions, ClickConfirmPlan, CurrentState?.Slot == Slot.Morning && CurrentState.MorningPlan?.Confirmed == false);
            CreateDesktopActionButton("NEXT MORNING\nAdvance Day", actions, ClickNextDay, CurrentState?.Slot == Slot.Evening);
        }

        private void CreateDesktopActionButton(string label, Transform parent, UnityEngine.Events.UnityAction action, bool interactable)
        {
            var buttonObject = CreatePanel("Desktop Action " + label, parent, interactable ? TerminalButtonColor : new Color(0.10f, 0.11f, 0.10f, 0.88f));
            var button = buttonObject.gameObject.AddComponent<Button>();
            button.interactable = interactable;
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(action);
            buttonObject.gameObject.AddComponent<LayoutElement>().minHeight = 86;
            var text = CreateText(label, buttonObject, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.color = interactable ? CrtTextColor : new Color(CrtTextColor.r, CrtTextColor.g, CrtTextColor.b, 0.42f);
            text.raycastTarget = false;
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
            selectedWorkId = CurrentState.MorningPlan?.Entries?.FirstOrDefault()?.EventId ?? FirstActiveEventId();
            assignmentPickerEventId = "";
            assignmentPickerSlotIndex = -1;
            showPlanApprovalModal = false;
            pendingDailyReportAfterWork = false;
            openWindows.Clear();
            windowLayouts.Clear();
            focusedWindow = MvpDesktopWindow.None;
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
            OpenDesktopWindow(MvpDesktopWindow.TodayWorkPlan);
            Render();
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
            selectedWorkId = id;
            OpenDesktopWindow(MvpDesktopWindow.CurrentWorkDashboard);
            Render();
        }

        public void ClickConfirmPlan()
        {
            if (CurrentState?.Slot != Slot.Morning || CurrentState.MorningPlan?.Confirmed != false)
            {
                AddLog("Plan approval is only available before morning work starts.");
                OpenDesktopWindow(MvpDesktopWindow.DailyReport);
                Render();
                return;
            }

            SyncAllPlanAdjustments();
            showPlanApprovalModal = true;
            Render();
        }

        public void ClickCancelPlanApproval()
        {
            showPlanApprovalModal = false;
            AddLog("Plan approval cancelled.");
            Render();
        }

        public void ClickApprovePlanAndStartWork()
        {
            SyncAllPlanAdjustments();
            var workEvents = UseRandomCardsForAssignedWork();
            showPlanApprovalModal = false;
            Dispatch("confirm plan");
            pendingDailyReportAfterWork = true;
            if (CurrentState?.Slot == Slot.Evening)
            {
                openWindows.Clear();
                focusedWindow = MvpDesktopWindow.None;
            }

            if (workEvents.Count > 0)
            {
                BeginWorkPerformanceOverlay(workEvents);
            }
            else
            {
                ShowDailyReportAfterWork();
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
                selectedWorkId = CurrentState.MorningPlan?.Entries?.FirstOrDefault()?.EventId ?? FirstActiveEventId();
                showPlanApprovalModal = false;
                pendingDailyReportAfterWork = false;
                openWindows.Clear();
                focusedWindow = MvpDesktopWindow.None;
                Render();
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
                if (pendingDailyReportAfterWork)
                {
                    ShowDailyReportAfterWork();
                }
                else
                {
                    Render();
                }
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
            OpenDesktopWindow(MvpDesktopWindow.CharacterProfiling);
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

            var existingEventId = FindAssignedEventId(personnelId, eventId);
            if (!string.IsNullOrWhiteSpace(sourceEventId) && !sourceEventId.Equals(eventId, StringComparison.OrdinalIgnoreCase))
            {
                RemovePersonnelFromWork(personnelId, sourceEventId, renderAfter: false);
            }
            else if (!string.IsNullOrWhiteSpace(existingEventId))
            {
                RemovePersonnelFromWork(personnelId, existingEventId, renderAfter: false);
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

            EnsureCardStateForToday();
            RenderDesktopObjects();
            RenderDesktopWindows();
            logText.text = BuildPrinterLogText();
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
            return $"[PROJECT_W INTERNAL TERMINAL]  DAY {CurrentState.Day:00}  |  {CurrentState.Slot.ToString().ToUpperInvariant()}  |  QUEUE {activeQueue}/{CurrentState.Config.QueueSoftCap}  |  OVR {CurrentState.Overload}  |  AI PRESSURE {CurrentState.ReplacementPressure}  |  REDIRECT {CurrentState.RedirectBudget}  |  AUDIT {CurrentState.AuditBudget}  |  INTERVIEW {CurrentState.InterviewBudget}";
        }

        private void RenderDesktopObjects()
        {
            ClearDynamicRoot(desktopObjectRoot);
            if (desktopObjectRoot is null)
            {
                return;
            }

            var shortcuts = DesktopShortcuts()
                .Where(shortcut => shortcut.IsEnabled?.Invoke() != false)
                .ToList();

            const float iconSize = 112f;
            const float labelHeight = 58f;
            const float gap = 22f;
            const float startX = 28f;
            const float startY = -28f;
            const int columns = 4;

            for (var index = 0; index < shortcuts.Count; index++)
            {
                var shortcut = shortcuts[index];
                var column = index % columns;
                var row = index / columns;
                var cell = CreateUiObject("Shortcut " + shortcut.Id, desktopObjectRoot).transform;
                var cellRect = (RectTransform)cell;
                cellRect.anchorMin = new Vector2(0f, 1f);
                cellRect.anchorMax = new Vector2(0f, 1f);
                cellRect.pivot = new Vector2(0f, 1f);
                cellRect.sizeDelta = new Vector2(iconSize, iconSize + labelHeight);
                cellRect.anchoredPosition = new Vector2(startX + column * (iconSize + gap), startY - row * (iconSize + labelHeight + gap));

                var icon = CreatePanel("Icon", cell, shortcut.Color);
                var iconRect = (RectTransform)icon;
                iconRect.anchorMin = new Vector2(0f, 1f);
                iconRect.anchorMax = new Vector2(0f, 1f);
                iconRect.pivot = new Vector2(0f, 1f);
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);
                iconRect.anchoredPosition = Vector2.zero;
                var button = icon.gameObject.AddComponent<Button>();
                button.targetGraphic = icon.GetComponent<Image>();
                button.onClick.AddListener(() =>
                {
                    OpenDesktopWindow(shortcut.TargetWindow);
                    Render();
                });

                var glyph = CreateText(shortcut.IconText, icon, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
                glyph.color = shortcut.TextColor;
                glyph.raycastTarget = false;

                var label = CreateText(shortcut.Label, cell, 13, FontStyle.Bold, TextAnchor.UpperCenter);
                label.color = CrtTextColor;
                label.raycastTarget = false;
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(1f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                label.rectTransform.sizeDelta = new Vector2(0f, labelHeight);
                label.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        private void RenderDesktopWindows()
        {
            ClearDynamicRoot(windowLayer);
            if (windowLayer is null)
            {
                return;
            }

            assignmentPickerPanelRect = null;

            foreach (var window in openWindows.Where(window => window != focusedWindow).ToList())
            {
                RenderDesktopWindow(window);
            }

            if (focusedWindow != MvpDesktopWindow.None && openWindows.Contains(focusedWindow))
            {
                RenderDesktopWindow(focusedWindow);
            }

            RenderDesktopActionButtons();
            RenderPlanApprovalModal();
        }

        private void RenderDesktopWindow(MvpDesktopWindow window)
        {
            switch (window)
            {
                case MvpDesktopWindow.CurrentWorkDashboard:
                    CreateCurrentWorkDashboardWindow();
                    break;
                case MvpDesktopWindow.TodayWorkPlan:
                    CreateTodayWorkPlanWindow();
                    CreateFloatingAssignmentPicker();
                    break;
                case MvpDesktopWindow.DailyReport:
                    CreateDailyReportWindow();
                    break;
                case MvpDesktopWindow.CharacterProfiling:
                    CreateCharacterProfilingWindow();
                    break;
                case MvpDesktopWindow.DevTools:
                    CreateDevToolsWindow();
                    break;
            }
        }

        private void OpenDesktopWindow(MvpDesktopWindow window)
        {
            if (window == MvpDesktopWindow.None)
            {
                return;
            }

            openWindows.Add(window);
            focusedWindow = window;
        }

        private void CloseDesktopWindow(MvpDesktopWindow window)
        {
            openWindows.Remove(window);
            focusedWindow = openWindows.LastOrDefault();
            Render();
        }

        private void TryOpenCardsWindow()
        {
            if (string.IsNullOrWhiteSpace(selectedPersonnelId))
            {
                AddLog("No personnel selected.");
                Render();
                return;
            }

            OpenDesktopWindow(MvpDesktopWindow.CharacterProfiling);
            Render();
        }

        private void SelectWorkFile(string eventId)
        {
            selectedWorkId = eventId;
            OpenDesktopWindow(MvpDesktopWindow.CurrentWorkDashboard);
            Render();
        }

        private Transform CreateDockWindow(MvpDesktopWindow window, string title, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var panel = CreatePanel(title + " Window", windowLayer, color);
            var rect = (RectTransform)panel;
            var layoutState = WindowLayoutFor(window, anchorMin, anchorMax);
            rect.anchorMin = layoutState.AnchorMin;
            rect.anchorMax = layoutState.AnchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = panel.GetComponent<Image>();
            if (image is not null)
            {
                image.raycastTarget = true;
            }

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 12);
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var titleBar = CreateUiObject("Title Bar", panel).transform;
            titleBar.gameObject.AddComponent<LayoutElement>().minHeight = 58;
            var titleBarImage = titleBar.gameObject.AddComponent<Image>();
            titleBarImage.color = new Color(0f, 0f, 0f, 0.01f);
            titleBar.gameObject.AddComponent<DesktopWindowDragHandle>().Initialize(this, window, rect, windowLayer as RectTransform);
            var titleText = CreateText(title, titleBar, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.color = color == PaperColor || color == FolderColor || color == IdCardColor ? PaperTextColor : CrtTextColor;
            titleText.raycastTarget = false;
            titleText.rectTransform.offsetMax = new Vector2(-120f, 0f);
            var close = CreateSmallWindowButton("CLOSE", titleBar, () => CloseDesktopWindow(window));
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(1f, 0f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.sizeDelta = new Vector2(110f, 0f);
            closeRect.anchoredPosition = Vector2.zero;
            var resizeHandle = CreatePanel("Resize Handle", panel, new Color(0.04f, 0.05f, 0.04f, 0.55f));
            var resizeRect = (RectTransform)resizeHandle;
            resizeRect.anchorMin = new Vector2(1f, 0f);
            resizeRect.anchorMax = new Vector2(1f, 0f);
            resizeRect.pivot = new Vector2(1f, 0f);
            resizeRect.sizeDelta = new Vector2(36f, 36f);
            resizeRect.anchoredPosition = Vector2.zero;
            resizeHandle.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            resizeHandle.gameObject.AddComponent<DesktopWindowResizeHandle>().Initialize(this, window, rect, windowLayer as RectTransform);
            var scrollContent = CreateWindowScrollContent(panel);
            resizeHandle.SetAsLastSibling();
            return scrollContent;
        }

        private Transform CreateWindowScrollContent(Transform parent)
        {
            var scrollRoot = CreateUiObject("Window Scroll", parent).transform;
            var rootElement = scrollRoot.gameObject.AddComponent<LayoutElement>();
            rootElement.flexibleHeight = 1;
            rootElement.minHeight = 120;
            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 36f;

            var viewport = CreateUiObject("Viewport", scrollRoot).transform;
            Stretch((RectTransform)viewport);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = true;
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = CreateUiObject("Content", viewport).transform;
            var contentRect = (RectTransform)content;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = (RectTransform)viewport;
            scrollRect.content = contentRect;
            return content;
        }

        internal void FocusDesktopWindow(MvpDesktopWindow window)
        {
            if (!openWindows.Contains(window))
            {
                return;
            }

            focusedWindow = window;
        }

        internal WindowLayoutState RememberWindowLayout(MvpDesktopWindow window, Vector2 anchorMin, Vector2 anchorMax)
        {
            var parentRect = windowLayer as RectTransform;
            var state = ClampWindowLayout(anchorMin, anchorMax, parentRect);
            windowLayouts[window] = state;
            return state;
        }

        private WindowLayoutState WindowLayoutFor(MvpDesktopWindow window, Vector2 defaultMin, Vector2 defaultMax)
        {
            if (windowLayouts.TryGetValue(window, out var state))
            {
                return state;
            }

            state = ClampWindowLayout(defaultMin, defaultMax, windowLayer as RectTransform);
            windowLayouts[window] = state;
            return state;
        }

        private static WindowLayoutState ClampWindowLayout(Vector2 anchorMin, Vector2 anchorMax, RectTransform parent)
        {
            var parentSize = parent is null ? new Vector2(1920f, 1080f) : parent.rect.size;
            var minAnchorSize = new Vector2(
                Mathf.Clamp01(WindowMinWidth / Mathf.Max(1f, parentSize.x)),
                Mathf.Clamp01(WindowMinHeight / Mathf.Max(1f, parentSize.y)));

            var size = anchorMax - anchorMin;
            size.x = Mathf.Max(size.x, minAnchorSize.x);
            size.y = Mathf.Max(size.y, minAnchorSize.y);

            anchorMin.x = Mathf.Clamp(anchorMin.x, 0f, 1f - size.x);
            anchorMin.y = Mathf.Clamp(anchorMin.y, 0f, 1f - size.y);
            anchorMax = anchorMin + size;
            anchorMax.x = Mathf.Clamp01(anchorMax.x);
            anchorMax.y = Mathf.Clamp01(anchorMax.y);
            return new WindowLayoutState(anchorMin, anchorMax);
        }

        private WindowLayoutState FloatingWingLayoutFor(MvpDesktopWindow ownerWindow, Vector2 defaultMin, Vector2 defaultMax, float minWidth, float preferredWidth)
        {
            var owner = WindowLayoutFor(ownerWindow, defaultMin, defaultMax);
            var availableRight = 0.98f - owner.AnchorMax.x - 0.01f;
            var width = Mathf.Clamp(availableRight, minWidth, preferredWidth);
            var minX = owner.AnchorMax.x + 0.01f;
            if (availableRight < 0.08f)
            {
                width = preferredWidth;
                minX = Mathf.Max(0.02f, owner.AnchorMin.x - width - 0.01f);
            }

            return new WindowLayoutState(
                new Vector2(minX, owner.AnchorMin.y),
                new Vector2(Mathf.Min(0.98f, minX + width), owner.AnchorMax.y));
        }

        private float CharacterTabGridHeight(MvpDesktopWindow window, int itemCount)
        {
            const float cellWidth = 190f;
            const float cellHeight = 66f;
            const float spacing = 8f;
            const float padding = 16f;
            var layout = WindowLayoutFor(window, new Vector2(0.48f, 0.16f), new Vector2(0.96f, 0.92f));
            var parentRect = windowLayer as RectTransform;
            var parentWidth = parentRect is null || parentRect.rect.width <= 0f ? 1920f : parentRect.rect.width;
            var width = Mathf.Max(cellWidth, (layout.AnchorMax.x - layout.AnchorMin.x) * parentWidth - 48f);
            var columns = Mathf.Max(1, Mathf.FloorToInt((width - padding + spacing) / (cellWidth + spacing)));
            var rows = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)columns));
            return padding + rows * cellHeight + Mathf.Max(0, rows - 1) * spacing;
        }

        private void CreateCurrentWorkDashboardWindow()
        {
            var panel = CreateDockWindow(MvpDesktopWindow.CurrentWorkDashboard, "CURRENT WORK DASHBOARD", new Vector2(0.04f, 0.20f), new Vector2(0.70f, 0.92f), CrtPanelColor);
            CreateDashboardStatusSection(panel);
            if (CurrentState.Slot == Slot.Evening)
            {
                CreateCurrentDiagnosticsSection(panel);
                return;
            }

            var body = CreateUiObject("Dashboard Body", panel).transform;
            body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 10;
            bodyLayout.childForceExpandHeight = true;

            var list = CreatePanel("Work List", body, FolderDarkColor);
            list.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0.36f;
            var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(8, 8, 8, 8);
            listLayout.spacing = 6;
            listLayout.childForceExpandWidth = true;
            foreach (var entry in CurrentState.MorningPlan?.Entries ?? Enumerable.Empty<WorkPlanEntry>())
            {
                var item = FindEvent(entry.EventId);
                if (item is null)
                {
                    continue;
                }

                var selected = item.Id.Equals(selectedWorkId, StringComparison.OrdinalIgnoreCase);
                CreateWindowButton($"{item.Id}\n{item.Title}", list, selected ? FolderColor : PaperColor, () => SelectWorkFile(item.Id), 104, selected ? WarningStampColor : PaperTextColor);
            }

            var detailPanel = CreatePanel("Work Detail", body, FolderColor);
            detailPanel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0.64f;
            var detailLayout = detailPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            detailLayout.padding = new RectOffset(10, 10, 10, 10);
            detailLayout.spacing = 8;
            detailLayout.childForceExpandWidth = true;
            var selectedWork = SelectedWorkOrFallback();
            if (selectedWork is not null)
            {
                var detail = CreateText($"FILE {selectedWork.Id}  {selectedWork.Title}\nLabels: {WorkTags(selectedWork)}\nRemaining {Math.Max(1, selectedWork.Volume)} | URG {selectedWork.Urgency} | SEV {selectedWork.Severity} | RISK {selectedWork.LatentRisk} | TTL {Math.Max(0, selectedWork.TtlSec)}s", detailPanel, 14, FontStyle.Bold, TextAnchor.UpperLeft);
                detail.color = PaperTextColor;
                detail.gameObject.AddComponent<LayoutElement>().minHeight = 150;
                CreateProgressRow("Outcome", selectedWork.OutcomeScore, 100, detailPanel, PaperTextColor);
                CreateProgressRow("Risk", selectedWork.LatentRisk, 100, detailPanel, WarningStampColor);
                CreateAssignmentTimeline(selectedWork, detailPanel);
            }

            CreateCurrentDiagnosticsSection(panel);
        }

        private void CreateTodayWorkPlanWindow()
        {
            var panel = CreateDockWindow(MvpDesktopWindow.TodayWorkPlan, "TODAY WORK PLAN", new Vector2(0.12f, 0.12f), new Vector2(0.78f, 0.86f), FolderColor);
            if (CurrentState.Slot == Slot.Morning && CurrentState.MorningPlan?.Confirmed == false)
            {
                CreatePlanAssignmentSection(panel);
            }
            else
            {
                CreateReadOnlyPlanSection(panel);
            }

            var note = CreateText("Plan decides assignment only. Use the desktop action buttons to start work or advance the day.", panel, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            note.color = PaperTextColor;
            note.gameObject.AddComponent<LayoutElement>().minHeight = 72;
        }

        private void CreateDailyReportWindow()
        {
            var panel = CreateDockWindow(MvpDesktopWindow.DailyReport, "DAILY REPORT", new Vector2(0.18f, 0.16f), new Vector2(0.82f, 0.86f), PaperColor);
            var resolved = CurrentState.Queue.Any(item => item.AutoResolved);
            if (!resolved)
            {
                var text = CreateText("No daily report has been generated yet.", panel, 16, FontStyle.Bold, TextAnchor.MiddleLeft);
                text.color = PaperTextColor;
                text.gameObject.AddComponent<LayoutElement>().minHeight = 120;
                return;
            }

            if (CurrentState.Slot != Slot.Evening)
            {
                var note = CreateText("READ ONLY - latest available daily report.", panel, 16, FontStyle.Bold, TextAnchor.MiddleLeft);
                note.color = WarningStampColor;
                note.gameObject.AddComponent<LayoutElement>().minHeight = 58;
            }

            CreateNightSummarySection(panel);
        }

        private void RenderPlanApprovalModal()
        {
            if (!showPlanApprovalModal || windowLayer is null)
            {
                return;
            }

            var blocker = CreatePanel("Plan Approval Modal Blocker", windowLayer, new Color(0.02f, 0.025f, 0.02f, 0.72f));
            Stretch((RectTransform)blocker);
            blocker.SetAsLastSibling();

            var modal = CreatePanel("Plan Approval Modal", blocker, PaperColor);
            var modalRect = (RectTransform)modal;
            modalRect.anchorMin = new Vector2(0.25f, 0.18f);
            modalRect.anchorMax = new Vector2(0.75f, 0.86f);
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;

            var layout = modal.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateText("STAMP APPROVED - FINAL CHECK", modal, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.color = WarningStampColor;
            title.gameObject.AddComponent<LayoutElement>().minHeight = 62;

            var body = CreateEmbeddedScrollContent("Plan Approval Scroll", modal, 460);
            var summary = CreateText(BuildPlanApprovalSummary(), body, 14, FontStyle.Bold, TextAnchor.UpperLeft);
            summary.color = PaperTextColor;
            summary.gameObject.AddComponent<LayoutElement>().minHeight = 760;

            var actions = CreateUiObject("Plan Approval Actions", modal).transform;
            actions.gameObject.AddComponent<LayoutElement>().minHeight = 96;
            var actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 12;
            actionLayout.childForceExpandWidth = true;
            actionLayout.childForceExpandHeight = true;

            CreateWindowButton("CANCEL", actions, new Color(0.40f, 0.35f, 0.28f, 1f), ClickCancelPlanApproval, 86, PaperTextColor);
            CreateWindowButton("CONFIRM\nSTART WORK", actions, TerminalButtonColor, ClickApprovePlanAndStartWork, 86, CrtTextColor);
        }

        private void CreateCharacterProfilingWindow()
        {
            var panel = CreateDockWindow(MvpDesktopWindow.CharacterProfiling, "CHARACTER PROFILING", new Vector2(0.48f, 0.16f), new Vector2(0.96f, 0.92f), IdCardColor);
            rosterRoot = CreatePanel("Character Tabs", panel, new Color(0.64f, 0.70f, 0.68f, 1f));
            var staffCount = CurrentState.Staff.Count(person => !person.HasLeft);
            rosterRoot.gameObject.AddComponent<LayoutElement>().minHeight = CharacterTabGridHeight(MvpDesktopWindow.CharacterProfiling, staffCount);
            var rosterLayout = rosterRoot.gameObject.AddComponent<GridLayoutGroup>();
            rosterLayout.padding = new RectOffset(8, 8, 8, 8);
            rosterLayout.spacing = new Vector2(8, 8);
            rosterLayout.cellSize = new Vector2(190, 66);
            rosterLayout.constraint = GridLayoutGroup.Constraint.Flexible;
            rosterLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            rosterLayout.childAlignment = TextAnchor.UpperLeft;
            foreach (var person in CurrentState.Staff.Where(person => !person.HasLeft))
            {
                CreateCharacterTab(person, rosterRoot, selectedPersonnelId.Equals(person.Id, StringComparison.OrdinalIgnoreCase));
            }

            var profilePanel = CreatePanel("Profile Status", panel, PaperColor);
            profilePanel.gameObject.AddComponent<LayoutElement>().minHeight = 180;
            var profileLayout = profilePanel.gameObject.AddComponent<VerticalLayoutGroup>();
            profileLayout.padding = new RectOffset(10, 10, 10, 10);
            profileLayout.spacing = 8;
            profileLayout.childForceExpandWidth = true;
            var selected = CurrentState.Staff.FirstOrDefault(person => person.Id.Equals(selectedPersonnelId, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                var statusRow = CreateUiObject("Profile Status Row", profilePanel).transform;
                statusRow.gameObject.AddComponent<LayoutElement>().minHeight = 144;
                var statusLayout = statusRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                statusLayout.spacing = 10;
                statusLayout.childForceExpandHeight = true;
                statusLayout.childForceExpandWidth = false;
                CreateCharacterFaceBlock(selected, statusRow);
                var detail = CreateText($"ID {selected.Id}  {selected.Name}\nLOAD {selected.LoadAssigned}/{Math.Max(1, selected.MaxLoad)} | FATIGUE {selected.Fatigue} | TRUST {selected.TrustToManager} | RETENTION {selected.RetentionRisk}", statusRow, 16, FontStyle.Bold, TextAnchor.MiddleLeft);
                detail.color = PaperTextColor;
                var detailLayout = detail.gameObject.AddComponent<LayoutElement>();
                detailLayout.flexibleWidth = 1;
                detailLayout.minHeight = 124;
                cardHandRoot = CreatePanel("Today Cards", panel, new Color(0.30f, 0.25f, 0.18f, 1f));
                cardHandRoot.gameObject.AddComponent<LayoutElement>().minHeight = 360;
                var cardLayout = cardHandRoot.gameObject.AddComponent<VerticalLayoutGroup>();
                cardLayout.padding = new RectOffset(10, 10, 10, 10);
                cardLayout.spacing = 8;
                cardLayout.childForceExpandWidth = true;
                var deck = DeckFor(selected.Id);
                foreach (var card in deck.TodayHand)
                {
                    CreateCardFace(card, cardHandRoot, deck.UsedToday.Contains(card.Id));
                }
            }
        }

        private void CreateDevToolsWindow()
        {
            var panel = CreateDockWindow(MvpDesktopWindow.DevTools, "DEV TOOLS", new Vector2(0.04f, 0.08f), new Vector2(0.44f, 0.62f), CrtPanelColor);
            CreateHeader("SCENARIO LAB", panel);
            var note = CreateText("Sample scenario playback and future development-only tools live here.", panel, 13, FontStyle.Bold, TextAnchor.UpperLeft);
            note.gameObject.AddComponent<LayoutElement>().minHeight = 86;
            CreateWindowButton("> PLAY SCENARIO SAMPLE", panel, TerminalButtonColor, ClickPlaySampleScenario, 82, CrtTextColor);
        }

        private IReadOnlyList<DesktopShortcutDefinition> DesktopShortcuts()
        {
            return new List<DesktopShortcutDefinition>
            {
                new("current-work", "Current Work", "Status, risk, and progress", "WORK", MvpDesktopWindow.CurrentWorkDashboard, FolderColor, PaperTextColor),
                new("today-plan", "Today Plan", "Plan, approval, and commands", "PLAN", MvpDesktopWindow.TodayWorkPlan, TerminalButtonColor, CrtTextColor),
                new("daily-report", "Daily Report", "Resolved work and night summary", "RPT", MvpDesktopWindow.DailyReport, PaperColor, PaperTextColor),
                new("characters", "Characters", "Profiles and daily cards", "ID", MvpDesktopWindow.CharacterProfiling, IdCardColor, PaperTextColor),
                new("dev-tools", "Dev Tools", "Scenario lab and debug tools", "DEV", MvpDesktopWindow.DevTools, CrtPanelColor, CrtTextColor),
            };
        }

        private void CreateDashboardStatusSection(Transform parent)
        {
            var activeQueue = CurrentState.Queue.Count(item => item.Status != CaseStatus.Closed);
            var text = CreateText($"DAY {CurrentState.Day:00} | {CurrentState.Slot.ToString().ToUpperInvariant()} | QUEUE {activeQueue}/{CurrentState.Config.QueueSoftCap} | OVR {CurrentState.Overload} | AI {CurrentState.ReplacementPressure} | GLOBAL RISK {CurrentState.GlobalLatentRisk}", parent, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            text.gameObject.AddComponent<LayoutElement>().minHeight = 58;
        }

        private void CreateNightSummarySection(Transform parent)
        {
            boardCardRoot = CreateDynamicRoot("Night Report Body", parent);
            CreateNightSummaryCard();
        }

        private void CreateCurrentDiagnosticsSection(Transform parent)
        {
            CreateHeader("SYS DIAG", parent);
            debugGaugeText = CreateText("", parent, 11, FontStyle.Normal, TextAnchor.UpperLeft);
            debugGaugeText.gameObject.AddComponent<LayoutElement>().minHeight = 320;
            RenderDebugGauges();
            var recentLogs = visibleLogLines.Skip(Math.Max(0, visibleLogLines.Count - 12));
            var log = CreateText("RECENT LOG\n" + string.Join("\n", recentLogs), parent, 11, FontStyle.Normal, TextAnchor.UpperLeft);
            log.gameObject.AddComponent<LayoutElement>().minHeight = 220;
        }

        private void CreatePlanAssignmentSection(Transform parent)
        {
            var item = SelectedWorkOrFallback();
            if (item is null)
            {
                return;
            }

            var entries = CreateUiObject("Plan Entries", parent).transform;
            var entryLayout = entries.gameObject.AddComponent<HorizontalLayoutGroup>();
            entryLayout.spacing = 8;
            entryLayout.childForceExpandWidth = true;
            entryLayout.childForceExpandHeight = true;
            entries.gameObject.AddComponent<LayoutElement>().minHeight = 112;
            foreach (var entry in CurrentState.MorningPlan?.Entries ?? Enumerable.Empty<WorkPlanEntry>())
            {
                var work = FindEvent(entry.EventId);
                if (work is null)
                {
                    continue;
                }

                var selected = work.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase);
                CreateWindowButton($"{work.Id}\n{work.Title}", entries, selected ? FolderDarkColor : PaperColor, () =>
                {
                    selectedWorkId = work.Id;
                    assignmentPickerEventId = "";
                    assignmentPickerSlotIndex = -1;
                    OpenDesktopWindow(MvpDesktopWindow.TodayWorkPlan);
                    Render();
                }, 58, selected ? CrtTextColor : PaperTextColor);
            }

            var plan = CreateText($"SELECTED FILE {item.Id}  {item.Title}\nSelect personnel from each slot list, then approve the plan.", parent, 13, FontStyle.Bold, TextAnchor.UpperLeft);
            plan.color = PaperTextColor;
            plan.gameObject.AddComponent<LayoutElement>().minHeight = 94;
            var assignment = AssignmentFor(item.Id);
            var assignmentBody = CreateUiObject("Assignment Picker Body", parent).transform;
            assignmentBody.gameObject.AddComponent<LayoutElement>().minHeight = 390;
            var slots = CreateUiObject("Assignment Slots", assignmentBody).transform;
            Stretch((RectTransform)slots);
            var slotLayout = slots.gameObject.AddComponent<VerticalLayoutGroup>();
            slotLayout.spacing = 8;
            slotLayout.childForceExpandWidth = true;
            slotLayout.childForceExpandHeight = false;
            var maxSlots = Math.Max(1, item.MaxPersonnelCount);
            for (var slotIndex = 0; slotIndex < maxSlots; slotIndex++)
            {
                CreateAssignmentSlotPicker(item, assignment, slotIndex, slots);
            }
        }

        private void CreateReadOnlyPlanSection(Transform parent)
        {
            var entries = CurrentState.MorningPlan?.Entries;
            if (entries is null || entries.Count == 0)
            {
                var empty = CreateText("No plan information is available yet.", parent, 16, FontStyle.Bold, TextAnchor.MiddleLeft);
                empty.color = PaperTextColor;
                empty.gameObject.AddComponent<LayoutElement>().minHeight = 120;
                return;
            }

            var note = CreateText("READ ONLY - latest available work plan.", parent, 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            note.color = WarningStampColor;
            note.gameObject.AddComponent<LayoutElement>().minHeight = 58;

            var list = CreatePanel("Read Only Plan", parent, PaperColor);
            list.gameObject.AddComponent<LayoutElement>().minHeight = 320;
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            foreach (var entry in entries)
            {
                var item = FindEvent(entry.EventId);
                var assignment = AssignmentFor(entry.EventId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
                var line = item is null
                    ? $"{entry.EventId}\nAssigned: {ReadableAssignmentLine(assignment)}"
                    : $"{item.Id}  {item.Title}\nAssigned: {ReadableAssignmentLine(assignment)}\nURG {item.Urgency} | SEV {item.Severity} | RISK {item.LatentRisk} | STATUS {item.Status}";
                var text = CreateText(line, list, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
                text.color = PaperTextColor;
                text.gameObject.AddComponent<LayoutElement>().minHeight = 108;
            }
        }

        private void CreateAssignmentSlotPicker(EventCase item, List<string> assignment, int slotIndex, Transform parent)
        {
            var assignedId = slotIndex < assignment.Count ? assignment[slotIndex] : "";
            var assigned = string.IsNullOrWhiteSpace(assignedId)
                ? null
                : CurrentState.Staff.FirstOrDefault(person => person.Id.Equals(assignedId, StringComparison.OrdinalIgnoreCase));
            var pickerOpen = assignmentPickerSlotIndex == slotIndex
                && assignmentPickerEventId.Equals(item.Id, StringComparison.OrdinalIgnoreCase);
            var slot = CreatePanel($"Assignment Slot {slotIndex + 1}", parent, pickerOpen ? FolderDarkColor : new Color(0.24f, 0.19f, 0.13f, 1f));
            var slotElement = slot.gameObject.AddComponent<LayoutElement>();
            slotElement.minHeight = 112;
            var slotLayout = slot.gameObject.AddComponent<VerticalLayoutGroup>();
            slotLayout.padding = new RectOffset(8, 8, 8, 8);
            slotLayout.spacing = 8;
            slotLayout.childForceExpandWidth = true;
            slotLayout.childForceExpandHeight = false;

            var label = assigned is null
                ? $"SLOT {slotIndex + 1}\nEMPTY - SELECT CHARACTER"
                : $"SLOT {slotIndex + 1}\n{BuildPersonnelPickerLabel(assigned)}";
            CreateWindowButton(label, slot, assigned is null ? TerminalButtonColor : IdCardColor, () =>
            {
                ToggleAssignmentPicker(item.Id, slotIndex);
            }, 96, assigned is null ? CrtTextColor : PaperTextColor);

            if (!pickerOpen)
            {
                return;
            }

            var hint = CreateText("Picker opened on the right.", slot, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            hint.color = CrtTextColor;
            hint.gameObject.AddComponent<LayoutElement>().minHeight = 48;
        }

        private void CreateAssignmentPickerWing(EventCase item, List<string> assignment, Transform parent)
        {
            var wing = CreatePanel("Character Picker Wing", parent, FolderDarkColor);
            var wingElement = wing.gameObject.AddComponent<LayoutElement>();
            wingElement.flexibleWidth = 0.55f;
            wingElement.flexibleHeight = 1;
            wingElement.minWidth = 430;
            var wingLayout = wing.gameObject.AddComponent<VerticalLayoutGroup>();
            wingLayout.padding = new RectOffset(10, 10, 10, 10);
            wingLayout.spacing = 8;
            wingLayout.childForceExpandWidth = true;
            wingLayout.childForceExpandHeight = true;

            var slotOpen = assignmentPickerSlotIndex >= 0 && assignmentPickerEventId.Equals(item.Id, StringComparison.OrdinalIgnoreCase);
            var headerText = slotOpen
                ? $"SLOT {assignmentPickerSlotIndex + 1} CHARACTER SELECT"
                : "CHARACTER SELECT";
            var header = CreateText(headerText, wing, 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            header.color = CrtTextColor;
            header.gameObject.AddComponent<LayoutElement>().minHeight = 54;

            if (!slotOpen)
            {
                var empty = CreateText("Select a slot on the left.", wing, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
                empty.color = CrtTextColor;
                empty.gameObject.AddComponent<LayoutElement>().minHeight = 90;
                return;
            }

            var assignedId = assignmentPickerSlotIndex < assignment.Count ? assignment[assignmentPickerSlotIndex] : "";
            var listContent = CreateEmbeddedScrollContent("Character Wing Dropdown", wing, 300);
            CreateWindowButton("NONE\nClear this slot", listContent, PaperColor, () =>
            {
                SelectPersonnelForAssignmentSlot(item.Id, assignmentPickerSlotIndex, "");
            }, 86, PaperTextColor);

            foreach (var person in CurrentState.Staff.Where(person => !person.HasLeft))
            {
                var selectable = CanSelectPersonnelForSlot(person, item.Id, assignmentPickerSlotIndex);
                var currentSlot = assignedId.Equals(person.Id, StringComparison.OrdinalIgnoreCase);
                var assignedElsewhere = FindAssignedEventId(person.Id, item.Id);
                var status = string.IsNullOrWhiteSpace(assignedElsewhere) || currentSlot
                    ? BuildPersonnelPickerStatus(person)
                    : $"{BuildPersonnelPickerStatus(person)} | MOVE FROM {assignedElsewhere}";
                var rowLabel = $"FACE {person.Id}\n{person.Name} ({person.Id})\n{status}";
                var rowColor = currentSlot ? new Color(0.90f, 0.88f, 0.72f, 1f) : selectable ? IdCardColor : new Color(0.34f, 0.36f, 0.34f, 0.72f);
                var button = CreateWindowButton(rowLabel, listContent, rowColor, () =>
                {
                    SelectPersonnelForAssignmentSlot(item.Id, assignmentPickerSlotIndex, person.Id);
                }, 124, selectable ? PaperTextColor : new Color(PaperTextColor.r, PaperTextColor.g, PaperTextColor.b, 0.42f));
                button.interactable = selectable;
            }
        }

        private void CreateFloatingAssignmentPicker()
        {
            if (CurrentState?.Slot != Slot.Morning || CurrentState.MorningPlan?.Confirmed != false)
            {
                return;
            }

            if (assignmentPickerSlotIndex < 0 || string.IsNullOrWhiteSpace(assignmentPickerEventId))
            {
                return;
            }

            var item = FindEvent(assignmentPickerEventId);
            if (item is null)
            {
                return;
            }

            var assignment = AssignmentFor(item.Id);
            var panel = CreatePanel("Floating Character Picker", windowLayer, FolderDarkColor);
            assignmentPickerPanelRect = (RectTransform)panel;
            ApplyFloatingAssignmentPickerLayout();
            panel.SetAsLastSibling();

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            CreateAssignmentPickerWing(item, assignment, panel);
        }

        internal void RefreshFloatingPanels()
        {
            ApplyFloatingAssignmentPickerLayout();
        }

        private void ApplyFloatingAssignmentPickerLayout()
        {
            if (assignmentPickerPanelRect == null)
            {
                return;
            }

            var wingLayout = FloatingWingLayoutFor(MvpDesktopWindow.TodayWorkPlan, new Vector2(0.12f, 0.12f), new Vector2(0.78f, 0.86f), 0.12f, 0.24f);
            assignmentPickerPanelRect.anchorMin = wingLayout.AnchorMin;
            assignmentPickerPanelRect.anchorMax = wingLayout.AnchorMax;
            assignmentPickerPanelRect.offsetMin = Vector2.zero;
            assignmentPickerPanelRect.offsetMax = Vector2.zero;
        }

        private Transform CreateEmbeddedScrollContent(string name, Transform parent, float minHeight)
        {
            var scrollRoot = CreatePanel(name, parent, new Color(0.08f, 0.09f, 0.08f, 0.72f));
            var scrollElement = scrollRoot.gameObject.AddComponent<LayoutElement>();
            scrollElement.minHeight = minHeight;
            scrollElement.flexibleHeight = 1;
            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 36f;

            var viewport = CreateUiObject("Viewport", scrollRoot).transform;
            Stretch((RectTransform)viewport);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = true;
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = CreateUiObject("Content", viewport).transform;
            var contentRect = (RectTransform)content;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = (RectTransform)viewport;
            scrollRect.content = contentRect;
            return content;
        }

        private void ToggleAssignmentPicker(string eventId, int slotIndex)
        {
            if (assignmentPickerSlotIndex == slotIndex && assignmentPickerEventId.Equals(eventId, StringComparison.OrdinalIgnoreCase))
            {
                assignmentPickerEventId = "";
                assignmentPickerSlotIndex = -1;
            }
            else
            {
                assignmentPickerEventId = eventId;
                assignmentPickerSlotIndex = slotIndex;
            }

            Render();
        }

        private void SelectPersonnelForAssignmentSlot(string eventId, int slotIndex, string personnelId)
        {
            var assignment = AssignmentFor(eventId);
            while (assignment.Count <= slotIndex)
            {
                assignment.Add("");
            }

            var current = assignment[slotIndex];
            if (!string.IsNullOrWhiteSpace(current))
            {
                assignment[slotIndex] = "";
            }

            if (!string.IsNullOrWhiteSpace(personnelId))
            {
                var existingEventId = FindAssignedEventId(personnelId, eventId);
                if (!string.IsNullOrWhiteSpace(existingEventId))
                {
                    RemovePersonnelFromWork(personnelId, existingEventId, renderAfter: false);
                }

                for (var index = 0; index < assignment.Count; index++)
                {
                    if (index != slotIndex && assignment[index].Equals(personnelId, StringComparison.OrdinalIgnoreCase))
                    {
                        assignment[index] = "";
                    }
                }

                assignment[slotIndex] = personnelId;
            }

            SyncPlanAdjustment(eventId);
            assignmentPickerEventId = "";
            assignmentPickerSlotIndex = -1;
            AddLog(string.IsNullOrWhiteSpace(personnelId) ? $"{eventId} slot cleared." : $"{eventId} slot selected: {personnelId}.");
            Render();
        }

        private bool CanSelectPersonnelForSlot(Personnel person, string eventId, int slotIndex)
        {
            if (person is null || person.HasLeft || CurrentState?.Slot != Slot.Morning)
            {
                return false;
            }

            var assignment = AssignmentFor(eventId);
            if (slotIndex < assignment.Count && assignment[slotIndex].Equals(person.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !assignment.Any(id => id.Equals(person.Id, StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildPersonnelPickerLabel(Personnel person)
        {
            return $"FACE {person.Id} | {person.Name} ({person.Id})\n{BuildPersonnelPickerStatus(person)}";
        }

        private static string BuildPersonnelPickerStatus(Personnel person)
        {
            return $"LOAD {person.LoadAssigned}/{Math.Max(1, person.MaxLoad)} | FAT {person.Fatigue} | TRUST {person.TrustToManager}";
        }

        private static string ReadableAssignmentLine(IReadOnlyList<string> assignment)
        {
            return assignment.Count == 0 ? "none" : string.Join(", ", assignment);
        }

        private void CreateProgressRow(string label, int value, int max, Transform parent, Color color)
        {
            var text = CreateText($"{label,-10} {Bar(value, max)} {value:000}/{Math.Max(1, max):000}", parent, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            text.color = color;
            text.gameObject.AddComponent<LayoutElement>().minHeight = 52;
        }

        private void CreateAssignmentTimeline(EventCase item, Transform parent)
        {
            var assignment = AssignmentFor(item.Id);
            var lines = new List<string> { "WORKER TIMELINE" };
            if (assignment.Count == 0)
            {
                lines.Add("No personnel assigned.");
            }
            else
            {
                var maxSlots = Math.Max(1, item.MaxPersonnelCount);
                for (var index = 0; index < assignment.Count; index++)
                {
                    var person = CurrentState.Staff.FirstOrDefault(candidate => candidate.Id.Equals(assignment[index], StringComparison.OrdinalIgnoreCase));
                    var progress = Mathf.RoundToInt(100f * (index + 1) / Math.Max(1, maxSlots));
                    lines.Add($"{assignment[index],-6} {person?.Name ?? "Unknown",-16} {Bar(progress, 100)}");
                }
            }

            var text = CreateText(string.Join("\n", lines), parent, 12, FontStyle.Bold, TextAnchor.UpperLeft);
            text.color = PaperTextColor;
            text.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        }

        private EventCase SelectedWorkOrFallback()
        {
            var item = string.IsNullOrWhiteSpace(selectedWorkId) ? null : FindEvent(selectedWorkId);
            item ??= CurrentState.MorningPlan?.Entries?.Select(entry => FindEvent(entry.EventId)).FirstOrDefault(candidate => candidate is not null);
            item ??= CurrentState.Queue.FirstOrDefault(candidate => candidate.Status != CaseStatus.Closed);
            if (item is not null)
            {
                selectedWorkId = item.Id;
            }

            return item;
        }

        private Button CreateWindowButton(string label, Transform parent, Color color, UnityEngine.Events.UnityAction onClick, float minHeight, Color textColor)
        {
            var buttonObject = CreatePanel("Button " + label, parent, color);
            buttonObject.gameObject.AddComponent<LayoutElement>().minHeight = Mathf.Max(minHeight, 86f);
            var button = buttonObject.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            var text = CreateText(label, buttonObject, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            text.color = textColor;
            text.rectTransform.offsetMin = new Vector2(10f, 4f);
            text.rectTransform.offsetMax = new Vector2(-10f, -4f);
            text.raycastTarget = false;
            return button;
        }

        private Button CreateSmallWindowButton(string label, Transform parent, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = CreatePanel(label + " Button", parent, TerminalButtonColor);
            var button = buttonObject.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            var text = CreateText(label, buttonObject, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            return button;
        }

        private string BuildCompactDiag()
        {
            return $"OVR {CurrentState.Overload} | AI {CurrentState.ReplacementPressure}\nRISK {CurrentState.GlobalLatentRisk}";
        }

        private string BuildPlanApprovalSummary()
        {
            if (CurrentState?.MorningPlan?.Entries is null || CurrentState.MorningPlan.Entries.Count == 0)
            {
                return "No morning plan is available.";
            }

            var lines = new List<string>
            {
                $"DAY {CurrentState.Day:00} MORNING PLAN",
                $"QUEUE {CurrentState.Queue.Count(item => item.Status != CaseStatus.Closed)}/{CurrentState.Config.QueueSoftCap} | OVR {CurrentState.Overload} | AI PRESSURE {CurrentState.ReplacementPressure} | GLOBAL RISK {CurrentState.GlobalLatentRisk}",
                "",
                "Confirming will stamp the current assignments, start work, and move the day to Evening.",
                ""
            };

            foreach (var entry in CurrentState.MorningPlan.Entries)
            {
                var item = FindEvent(entry.EventId);
                var assignment = AssignmentFor(entry.EventId);
                var maxSlots = item is null ? Math.Max(1, assignment.Count) : Math.Max(1, item.MaxPersonnelCount);
                var expected = EstimateExpectedAssignmentDelta(assignment);
                lines.Add($"[{entry.EventId}] {(item is null ? entry.EventId : item.Title)}");
                if (item is not null)
                {
                    lines.Add($"  Work: OUT {item.OutcomeScore:000} | RISK {item.LatentRisk:000} | URG {item.Urgency} | SEV {item.Severity} | VOL {Math.Max(1, item.Volume)}");
                    lines.Add($"  Tags: {WorkTags(item)}");
                }

                lines.Add($"  Assignment: {assignment.Count}/{maxSlots}");
                if (assignment.Count == 0)
                {
                    lines.Add("  - EMPTY: no personnel assigned.");
                }
                else
                {
                    foreach (var personnelId in assignment)
                    {
                        var person = CurrentState.Staff.FirstOrDefault(candidate => candidate.Id.Equals(personnelId, StringComparison.OrdinalIgnoreCase));
                        var label = person is null
                            ? personnelId
                            : $"{person.Name} ({person.Id}) | LOAD {person.LoadAssigned}/{Math.Max(1, person.MaxLoad)} | FAT {person.Fatigue} | TRUST {person.TrustToManager}";
                        lines.Add("  - " + label);
                    }
                }

                lines.Add($"  Low card delta: OUT {Signed(expected.Outcome)} | RISK {Signed(expected.Risk)}");
                lines.Add($"  Basis: {entry.Reason}{(entry.Adjusted ? " | adjusted" : "")}");
                lines.Add("");
            }

            return string.Join("\n", lines);
        }

        private (int Outcome, int Risk) EstimateExpectedAssignmentDelta(IReadOnlyCollection<string> personnelIds)
        {
            if (personnelIds is null || personnelIds.Count == 0)
            {
                return (0, 0);
            }

            var outcome = 0d;
            var risk = 0d;
            var contributors = 0;
            foreach (var personnelId in personnelIds)
            {
                var deck = DeckFor(personnelId);
                var available = deck.TodayHand.Where(card => !deck.UsedToday.Contains(card.Id)).ToList();
                if (available.Count == 0)
                {
                    continue;
                }

                outcome += available.Average(card => card.OutcomeModifier);
                risk += available.Average(card => card.RiskModifier);
                contributors++;
            }

            if (contributors == 0)
            {
                return (0, 0);
            }

            return ((int)Math.Round(outcome), (int)Math.Round(risk));
        }

        private string BuildPrinterLogText()
        {
            var count = Math.Min(3, visibleLogLines.Count);
            var recent = visibleLogLines.Skip(Math.Max(0, visibleLogLines.Count - count));
            return "PRINT LOG:\n" + string.Join("\n", recent);
        }

        private void RenderBoard()
        {
            ClearDynamicRoot(boardCardRoot);
            if (CurrentState.Slot == Slot.Morning)
            {
                boardTitleText.text = CurrentState.MorningPlan.Confirmed ? "INBOX FILE TRAY - STAMPED" : "INBOX FILE TRAY";
                foreach (var entry in CurrentState.MorningPlan.Entries)
                {
                    CreateWorkCard(entry);
                }

                return;
            }

            boardTitleText.text = "END-OF-DAY REPORT FILE";
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
                "SYS DIAG",
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
            staffTitleText.text = "PERSONNEL CARD RACK";
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
                cardHandTitleText.text = "TODAY HAND";
                return;
            }

            selectedPersonnelId = person.Id;
            var deck = DeckFor(person.Id);
            cardHandTitleText.text = $"DRAWER: {person.Name} ({deck.TodayHand.Count}/5 from {deck.Pool.Count})";
            foreach (var card in deck.TodayHand)
            {
                CreateCardFace(card, cardHandRoot, deck.UsedToday.Contains(card.Id));
            }
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

            var root = CreatePanel("Root Desk Surface", canvasObject.transform, DeskColor);
            Stretch((RectTransform)root);
            var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(18, 18, 16, 16);
            rootLayout.spacing = 12;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            var statusPanel = CreatePanel("CRT System Bar", root, CrtShellColor);
            statusPanel.gameObject.AddComponent<LayoutElement>().minHeight = 86;
            statusText = CreateText("", statusPanel, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            statusText.rectTransform.offsetMin = new Vector2(16, 0);
            statusText.rectTransform.offsetMax = new Vector2(-16, 0);

            var desktop = CreatePanel("Desktop Workspace", root, new Color(0.16f, 0.12f, 0.08f, 1f));
            var desktopLayout = desktop.gameObject.AddComponent<LayoutElement>();
            desktopLayout.flexibleHeight = 1;
            desktopLayout.minHeight = 600;

            desktopObjectRoot = CreateUiObject("Desktop Object Layer", desktop).transform;
            Stretch((RectTransform)desktopObjectRoot);

            var logPanel = CreatePanel("Dot Matrix Command Log", root, PaperColor);
            logPanel.gameObject.AddComponent<LayoutElement>().minHeight = 160;
            logText = CreateText("", logPanel, 13, FontStyle.Normal, TextAnchor.UpperLeft);
            logText.color = PaperTextColor;
            logText.rectTransform.offsetMin = new Vector2(14, 12);
            logText.rectTransform.offsetMax = new Vector2(-14, -12);

            windowLayer = CreateUiObject("Window Layer", canvasObject.transform).transform;
            Stretch((RectTransform)windowLayer);

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
            blocker.color = new Color(0.025f, 0.035f, 0.030f, 0.96f);

            var topBar = CreatePanel("Internal Message Top Bar", scenarioOverlay.transform, CrtShellColor);
            var topRect = (RectTransform)topBar;
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 86f);
            topRect.anchoredPosition = Vector2.zero;
            scenarioTitleText = CreateText("INTERNAL MESSAGE VIEWER", topBar, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            scenarioTitleText.rectTransform.offsetMin = new Vector2(24f, 0f);
            scenarioTitleText.rectTransform.offsetMax = new Vector2(-360f, 0f);

            var skipButton = CreateOverlayButton("Skip", topBar, new Vector2(-210f, -33f), ClickScenarioSkip);
            ((RectTransform)skipButton.transform).sizeDelta = new Vector2(150f, 58f);
            var autoButton = CreateOverlayButton("Auto", topBar, new Vector2(-76f, -33f), ClickScenarioToggleAuto);
            ((RectTransform)autoButton.transform).sizeDelta = new Vector2(150f, 58f);
            scenarioAutoButtonText = autoButton.GetComponentInChildren<Text>();

            var meetingMask = CreatePanel("Zoom Meeting Mask", scenarioOverlay.transform, new Color(0.018f, 0.025f, 0.023f, 1f));
            var mask = meetingMask.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            scenarioPortraitRoot = meetingMask;
            var stageRect = (RectTransform)meetingMask;
            stageRect.anchorMin = new Vector2(0.03f, 0.28f);
            stageRect.anchorMax = new Vector2(0.97f, 0.86f);
            stageRect.offsetMin = Vector2.zero;
            stageRect.offsetMax = Vector2.zero;

            scenarioMeetingContentRoot = CreateUiObject("Zoom Meeting Tiles", scenarioPortraitRoot).transform;
            Stretch((RectTransform)scenarioMeetingContentRoot);

            var meetingChrome = CreatePanel("Zoom Meeting Chrome", scenarioPortraitRoot, new Color(0f, 0f, 0f, 0.28f));
            var chromeRect = (RectTransform)meetingChrome;
            chromeRect.anchorMin = new Vector2(0f, 1f);
            chromeRect.anchorMax = new Vector2(1f, 1f);
            chromeRect.pivot = new Vector2(0.5f, 1f);
            chromeRect.sizeDelta = new Vector2(0f, 46f);
            chromeRect.anchoredPosition = Vector2.zero;
            var chromeText = CreateText("SECURE MEETING ROOM", meetingChrome, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            chromeText.rectTransform.offsetMin = new Vector2(16f, 0f);
            chromeText.rectTransform.offsetMax = new Vector2(-260f, 0f);
            scenarioMeetingEffectText = CreateText("", meetingChrome, 14, FontStyle.Bold, TextAnchor.MiddleRight);
            scenarioMeetingEffectText.rectTransform.offsetMin = new Vector2(260f, 0f);
            scenarioMeetingEffectText.rectTransform.offsetMax = new Vector2(-16f, 0f);

            var textBox = CreatePanel("Internal Message Output", scenarioOverlay.transform, CrtPanelColor);
            var textBoxRect = (RectTransform)textBox;
            textBoxRect.anchorMin = new Vector2(0.08f, 0.04f);
            textBoxRect.anchorMax = new Vector2(0.92f, 0.24f);
            textBoxRect.offsetMin = Vector2.zero;
            textBoxRect.offsetMax = Vector2.zero;

            scenarioSpeakerText = CreateText("", textBox, 18, FontStyle.Bold, TextAnchor.UpperLeft);
            scenarioSpeakerText.color = CrtTextColor;
            scenarioSpeakerText.rectTransform.anchorMin = new Vector2(0f, 1f);
            scenarioSpeakerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            scenarioSpeakerText.rectTransform.pivot = new Vector2(0.5f, 1f);
            scenarioSpeakerText.rectTransform.offsetMin = new Vector2(22f, -48f);
            scenarioSpeakerText.rectTransform.offsetMax = new Vector2(-160f, -12f);

            scenarioBodyText = CreateText("", textBox, 24, FontStyle.Normal, TextAnchor.UpperLeft);
            scenarioBodyText.color = CrtTextColor;
            scenarioBodyText.rectTransform.offsetMin = new Vector2(22f, 24f);
            scenarioBodyText.rectTransform.offsetMax = new Vector2(-160f, -58f);

            var nextButton = CreateOverlayButton("Next", textBox, new Vector2(-76f, 46f), ClickScenarioNext);
            ((RectTransform)nextButton.transform).sizeDelta = new Vector2(150f, 66f);

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
            blocker.color = new Color(0.18f, 0.14f, 0.10f, 0.96f);

            var topBar = CreatePanel("Processing Report Top Bar", workSceneOverlay.transform, CrtShellColor);
            var topRect = (RectTransform)topBar;
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 90f);
            topRect.anchoredPosition = Vector2.zero;
            workSceneTitleText = CreateText("PROCESSING REPORT", topBar, 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            workSceneTitleText.rectTransform.offsetMin = new Vector2(24f, 0f);
            workSceneTitleText.rectTransform.offsetMax = new Vector2(-360f, 0f);

            var skipButton = CreateOverlayButton("Skip", topBar, new Vector2(-210f, -35f), ClickWorkSceneSkip);
            ((RectTransform)skipButton.transform).sizeDelta = new Vector2(150f, 58f);
            var nextButton = CreateOverlayButton("Next", topBar, new Vector2(-76f, -35f), ClickWorkSceneNext);
            ((RectTransform)nextButton.transform).sizeDelta = new Vector2(150f, 58f);

            var stage = CreateUiObject("Work Scene Stage", workSceneOverlay.transform).transform;
            var stageRect = (RectTransform)stage;
            stageRect.anchorMin = new Vector2(0.04f, 0.14f);
            stageRect.anchorMax = new Vector2(0.96f, 0.88f);
            stageRect.offsetMin = Vector2.zero;
            stageRect.offsetMax = Vector2.zero;

            var actorPanel = CreatePanel("Worker ID Card", stage, IdCardColor);
            var actorRect = (RectTransform)actorPanel;
            actorRect.anchorMin = new Vector2(0f, 0.10f);
            actorRect.anchorMax = new Vector2(0.27f, 0.90f);
            actorRect.offsetMin = Vector2.zero;
            actorRect.offsetMax = Vector2.zero;
            workSceneActorRoot = CreateUiObject("Worker Body", actorPanel).transform;
            Stretch((RectTransform)workSceneActorRoot);
            workSceneActorText = CreateText("", workSceneActorRoot, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            workSceneActorText.color = PaperTextColor;
            workSceneActorText.rectTransform.offsetMin = new Vector2(18f, 18f);
            workSceneActorText.rectTransform.offsetMax = new Vector2(-18f, -18f);

            var workPanel = CreatePanel("Target Work File", stage, FolderColor);
            var workRect = (RectTransform)workPanel;
            workRect.anchorMin = new Vector2(0.31f, 0.20f);
            workRect.anchorMax = new Vector2(0.66f, 0.80f);
            workRect.offsetMin = Vector2.zero;
            workRect.offsetMax = Vector2.zero;
            workSceneWorkText = CreateText("", workPanel, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            workSceneWorkText.color = PaperTextColor;
            workSceneWorkText.rectTransform.offsetMin = new Vector2(22f, 18f);
            workSceneWorkText.rectTransform.offsetMax = new Vector2(-22f, -18f);

            var impactPanel = CreatePanel("Result Report", stage, PaperColor);
            var impactRect = (RectTransform)impactPanel;
            impactRect.anchorMin = new Vector2(0.70f, 0.10f);
            impactRect.anchorMax = new Vector2(1f, 0.90f);
            impactRect.offsetMin = Vector2.zero;
            impactRect.offsetMax = Vector2.zero;
            workSceneImpactRoot = CreateUiObject("Impact Body", impactPanel).transform;
            Stretch((RectTransform)workSceneImpactRoot);
            workSceneCardText = CreateText("", workSceneImpactRoot, 17, FontStyle.Bold, TextAnchor.UpperLeft);
            workSceneCardText.color = PaperTextColor;
            workSceneCardText.rectTransform.offsetMin = new Vector2(20f, 230f);
            workSceneCardText.rectTransform.offsetMax = new Vector2(-20f, -20f);
            workSceneImpactText = CreateText("", workSceneImpactRoot, 16, FontStyle.Normal, TextAnchor.UpperLeft);
            workSceneImpactText.color = PaperTextColor;
            workSceneImpactText.rectTransform.offsetMin = new Vector2(20f, 80f);
            workSceneImpactText.rectTransform.offsetMax = new Vector2(-20f, -220f);
            workSceneProgressText = CreateText("", workSceneImpactRoot, 16, FontStyle.Bold, TextAnchor.LowerLeft);
            workSceneProgressText.color = WarningStampColor;
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
            rect.sizeDelta = new Vector2(150f, 58f);
            var image = buttonObject.AddComponent<Image>();
            image.color = TerminalButtonColor;
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
            scenarioTitleText.text = $"INTERNAL MESSAGE VIEWER | EVENT: {sampleScenario?.EventId ?? "scenario"} | LINE: {scenarioSession.CurrentLine.Source?.LineId ?? "END"}";
            scenarioSpeakerText.text = string.IsNullOrWhiteSpace(scenarioSession.CurrentLine.Source?.SpeakerId)
                ? "SPEAKER: NARRATION"
                : $"SPEAKER: {scenarioSession.CurrentLine.Source.SpeakerId}";
            scenarioBodyText.text = scenarioSession.VisibleText;
            if (scenarioAutoButtonText is not null)
            {
                scenarioAutoButtonText.text = scenarioAutoPlay ? "AUTO ON" : "AUTO";
            }

            RenderScenarioPortraits();
            UpdateScenarioMeetingMotion();
            RenderScenarioChoices();
        }

        private void HideScenarioOverlay()
        {
            scenarioSession = null;
            scenarioAutoPlay = false;
            scenarioTypewriterAccumulator = 0f;
            scenarioMeetingLayoutSignature = "";
            if (scenarioOverlay is not null)
            {
                scenarioOverlay.SetActive(false);
            }
        }

        private void RenderScenarioPortraits()
        {
            if (scenarioSession?.StageState is null)
            {
                ClearDynamicRoot(scenarioMeetingContentRoot);
                scenarioMeetingLayoutSignature = "";
                if (scenarioMeetingEffectText is not null)
                {
                    scenarioMeetingEffectText.text = "";
                }

                return;
            }

            var portraits = OrderMeetingParticipants(scenarioSession.StageState.Portraits);
            var stageCommands = scenarioSession.CurrentLine.Source?.StageCommands;
            if (scenarioMeetingEffectText is not null)
            {
                scenarioMeetingEffectText.text = BuildScenarioEffectText(stageCommands);
            }

            var layoutSignature = BuildMeetingLayoutSignature(scenarioSession.CurrentLine.Source?.LineId, portraits);
            if (layoutSignature.Equals(scenarioMeetingLayoutSignature, StringComparison.Ordinal))
            {
                return;
            }

            scenarioMeetingLayoutSignature = layoutSignature;
            ClearDynamicRoot(scenarioMeetingContentRoot);

            var slotCount = portraits.Count <= 1 ? portraits.Count : 4;
            var visibleCount = portraits.Count <= 4 ? portraits.Count : 3;
            var stageAspect = MeetingStageAspect();
            for (var index = 0; index < visibleCount; index++)
            {
                var portrait = portraits[index];
                var panel = CreatePanel("Meeting Tile " + portrait.PortraitId, scenarioMeetingContentRoot, MeetingTileColor(portrait));
                var rect = (RectTransform)panel;
                var tile = CalculateMeetingTileRect(index, portraits.Count, stageAspect);
                rect.anchorMin = tile.AnchorMin;
                rect.anchorMax = tile.AnchorMax;
                rect.offsetMin = tile.OffsetMin;
                rect.offsetMax = tile.OffsetMax;

                RenderMeetingParticipantTile(panel, portrait);
            }

            if (portraits.Count > 1 && portraits.Count <= 4)
            {
                for (var index = visibleCount; index < slotCount; index++)
                {
                    var emptyPanel = CreatePanel("Meeting Tile Empty " + index, scenarioMeetingContentRoot, new Color(0.040f, 0.048f, 0.044f, 1f));
                    var rect = (RectTransform)emptyPanel;
                    var tile = CalculateMeetingTileRect(index, portraits.Count, stageAspect);
                    rect.anchorMin = tile.AnchorMin;
                    rect.anchorMax = tile.AnchorMax;
                    rect.offsetMin = tile.OffsetMin;
                    rect.offsetMax = tile.OffsetMax;
                    RenderMeetingEmptyTile(emptyPanel);
                }
            }

            if (portraits.Count > 4)
            {
                var overflowPanel = CreatePanel("Meeting Tile Overflow", scenarioMeetingContentRoot, new Color(0.055f, 0.065f, 0.060f, 1f));
                var rect = (RectTransform)overflowPanel;
                var tile = CalculateMeetingTileRect(3, portraits.Count, stageAspect);
                rect.anchorMin = tile.AnchorMin;
                rect.anchorMax = tile.AnchorMax;
                rect.offsetMin = tile.OffsetMin;
                rect.offsetMax = tile.OffsetMax;
                RenderMeetingOverflowTile(overflowPanel, portraits.Count - 4);
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
                image.color = TerminalButtonColor;
                var button = buttonObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() =>
                {
                    AddLog($"Scenario choice selected: {choice.ChoiceId}");
                    ClickScenarioNext();
                });
                var layout = buttonObject.AddComponent<LayoutElement>();
                layout.minWidth = 220f;
                layout.minHeight = 76f;
                var text = CreateText("> " + label, buttonObject.transform, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
                text.color = CrtTextColor;
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

        private static Color MeetingTileColor(ScenarioPortraitState portrait)
        {
            if (portrait.IsFocused)
            {
                return new Color(0.30f, 0.48f, 0.38f, 1f);
            }

            return portrait.IsDimmed
                ? new Color(0.06f, 0.07f, 0.065f, 1f)
                : new Color(0.10f, 0.13f, 0.12f, 1f);
        }

        private static Color MeetingFeedColor(ScenarioPortraitState portrait)
        {
            if (portrait.IsFocused)
            {
                return new Color(0.075f, 0.12f, 0.095f, 1f);
            }

            return portrait.IsDimmed
                ? new Color(0.025f, 0.030f, 0.028f, 1f)
                : new Color(0.045f, 0.060f, 0.055f, 1f);
        }

        private void RenderMeetingParticipantTile(Transform panel, ScenarioPortraitState portrait)
        {
            var border = CreatePanel("Camera Feed Border", panel, portrait.IsFocused ? CrtTextColor : new Color(0.16f, 0.20f, 0.18f, 1f));
            Stretch((RectTransform)border);
            var inset = CreatePanel("Camera Feed", border, MeetingFeedColor(portrait));
            var insetRect = (RectTransform)inset;
            insetRect.offsetMin = new Vector2(4f, 4f);
            insetRect.offsetMax = new Vector2(-4f, -4f);

            var label = $"{portrait.PortraitId}\n";
            if (portrait.IsNewlyJoined)
            {
                label += "JOINED\n";
            }
            else if (portrait.IsMoving)
            {
                label += $"REORDER {portrait.PreviousNormalizedX:0.00}->{portrait.NormalizedX:0.00}\n";
            }

            if (portrait.IsFocused)
            {
                label += "SPEAKING";
            }
            else if (portrait.IsDimmed)
            {
                label += "LISTENING";
            }
            else
            {
                label += "CONNECTED";
            }

            var text = CreateText(label, inset, portrait.IsFocused ? 28 : 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.color = portrait.IsDimmed ? new Color(0.54f, 0.60f, 0.56f, 1f) : CrtTextColor;
            text.raycastTarget = false;

            var namePlate = CreatePanel("Participant Name Plate", inset, new Color(0f, 0f, 0f, portrait.IsFocused ? 0.72f : 0.56f));
            var nameRect = (RectTransform)namePlate;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.sizeDelta = new Vector2(0f, 48f);
            nameRect.anchoredPosition = Vector2.zero;
            var nameText = CreateText(portrait.IsFocused ? $"LIVE  {portrait.PortraitId}" : portrait.PortraitId, namePlate, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            nameText.rectTransform.offsetMin = new Vector2(14f, 0f);
            nameText.rectTransform.offsetMax = new Vector2(-14f, 0f);
            nameText.raycastTarget = false;
        }

        private void RenderMeetingOverflowTile(Transform panel, int overflowCount)
        {
            var border = CreatePanel("Overflow Feed Border", panel, new Color(0.16f, 0.20f, 0.18f, 1f));
            Stretch((RectTransform)border);
            var inset = CreatePanel("Overflow Feed", border, new Color(0.030f, 0.038f, 0.034f, 1f));
            var insetRect = (RectTransform)inset;
            insetRect.offsetMin = new Vector2(4f, 4f);
            insetRect.offsetMax = new Vector2(-4f, -4f);

            var text = CreateText($"+{Math.Max(1, overflowCount)}", inset, 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.color = CrtTextColor;
            text.raycastTarget = false;

            var footer = CreatePanel("Overflow Name Plate", inset, new Color(0f, 0f, 0f, 0.58f));
            var footerRect = (RectTransform)footer;
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.sizeDelta = new Vector2(0f, 48f);
            footerRect.anchoredPosition = Vector2.zero;
            var footerText = CreateText("WAITING ROOM", footer, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            footerText.rectTransform.offsetMin = new Vector2(14f, 0f);
            footerText.rectTransform.offsetMax = new Vector2(-14f, 0f);
            footerText.raycastTarget = false;
        }

        private void RenderMeetingEmptyTile(Transform panel)
        {
            var border = CreatePanel("Empty Feed Border", panel, new Color(0.12f, 0.15f, 0.14f, 1f));
            Stretch((RectTransform)border);
            var inset = CreatePanel("Empty Feed", border, new Color(0.024f, 0.029f, 0.027f, 1f));
            var insetRect = (RectTransform)inset;
            insetRect.offsetMin = new Vector2(4f, 4f);
            insetRect.offsetMax = new Vector2(-4f, -4f);

            var avatar = CreatePanel("Default Avatar", inset, new Color(0.10f, 0.13f, 0.12f, 1f));
            var avatarRect = (RectTransform)avatar;
            avatarRect.anchorMin = new Vector2(0.5f, 0.5f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.5f);
            avatarRect.pivot = new Vector2(0.5f, 0.5f);
            avatarRect.sizeDelta = new Vector2(120f, 120f);
            avatarRect.anchoredPosition = new Vector2(0f, 28f);
            var avatarText = CreateText("?", avatar, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            avatarText.color = new Color(0.42f, 0.55f, 0.49f, 1f);
            avatarText.raycastTarget = false;

            var statusText = CreateText("DEFAULT AVATAR\nNO SIGNAL", inset, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            statusText.color = new Color(0.44f, 0.58f, 0.50f, 1f);
            statusText.rectTransform.offsetMin = new Vector2(18f, 18f);
            statusText.rectTransform.offsetMax = new Vector2(-18f, -130f);
            statusText.raycastTarget = false;

            var footer = CreatePanel("Empty Name Plate", inset, new Color(0f, 0f, 0f, 0.52f));
            var footerRect = (RectTransform)footer;
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.sizeDelta = new Vector2(0f, 48f);
            footerRect.anchoredPosition = Vector2.zero;
            var footerText = CreateText("EMPTY SLOT", footer, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            footerText.rectTransform.offsetMin = new Vector2(14f, 0f);
            footerText.rectTransform.offsetMax = new Vector2(-14f, 0f);
            footerText.raycastTarget = false;
        }

        private void UpdateScenarioMeetingMotion()
        {
            if (scenarioMeetingContentRoot is null)
            {
                return;
            }

            var rect = (RectTransform)scenarioMeetingContentRoot;
            rect.anchoredPosition = CalculateScenarioShakeOffset(scenarioSession?.CurrentLine.Source?.StageCommands);
        }

        private static List<ScenarioPortraitState> OrderMeetingParticipants(IReadOnlyList<ScenarioPortraitState> portraits)
        {
            var ordered = portraits?.ToList() ?? new List<ScenarioPortraitState>();
            var focused = ordered.FirstOrDefault(portrait => portrait.IsFocused);
            if (focused is not null)
            {
                ordered.Remove(focused);
                ordered.Insert(0, focused);
                return ordered;
            }

            var newlyJoined = ordered.Where(portrait => portrait.IsNewlyJoined).ToList();
            if (newlyJoined.Count == 0)
            {
                return ordered;
            }

            ordered.RemoveAll(portrait => portrait.IsNewlyJoined);
            newlyJoined.AddRange(ordered);
            return newlyJoined;
        }

        private string BuildMeetingLayoutSignature(string lineId, IReadOnlyList<ScenarioPortraitState> portraits)
        {
            var ids = portraits is null
                ? ""
                : string.Join(",", portraits.Select(portrait => $"{portrait.PortraitId}:{portrait.IsFocused}:{portrait.IsDimmed}:{portrait.IsNewlyJoined}"));
            var overflow = portraits is null || portraits.Count <= 4 ? 0 : portraits.Count - 4;
            return $"{lineId ?? ""}|{ids}|overflow:{overflow}|aspect:{MeetingStageAspect():0.000}";
        }

        private float MeetingStageAspect()
        {
            if (scenarioPortraitRoot is RectTransform rect && rect.rect.height > 0.01f)
            {
                return rect.rect.width / rect.rect.height;
            }

            return 16f / 9f;
        }

        private static MeetingTileRect CalculateMeetingTileRect(int index, int participantCount, float stageAspect)
        {
            const float gutter = 0.012f;
            const float targetAspect = 4f / 3f;
            if (participantCount <= 1)
            {
                return ApplyTileAspect(gutter, gutter, 1f - gutter, 1f - gutter, stageAspect, targetAspect);
            }

            var safeIndex = Mathf.Clamp(index, 0, 3);
            var row = safeIndex / 2;
            var column = safeIndex % 2;
            var minX = column * 0.5f + gutter;
            var maxX = (column + 1) * 0.5f - gutter;
            var minY = 1f - (row + 1) * 0.5f + gutter;
            var maxY = 1f - row * 0.5f - gutter;
            return ApplyTileAspect(minX, minY, maxX, maxY, stageAspect, targetAspect);
        }

        private static MeetingTileRect ApplyTileAspect(float minX, float minY, float maxX, float maxY, float stageAspect, float targetAspect)
        {
            var width = maxX - minX;
            var height = maxY - minY;
            var safeStageAspect = Mathf.Max(0.01f, stageAspect);
            var targetHeight = width * safeStageAspect / targetAspect;
            if (targetHeight < height)
            {
                var centerY = (minY + maxY) * 0.5f;
                height = targetHeight;
                minY = centerY - height * 0.5f;
                maxY = centerY + height * 0.5f;
            }
            else
            {
                var targetWidth = height * targetAspect / safeStageAspect;
                var centerX = (minX + maxX) * 0.5f;
                width = targetWidth;
                minX = centerX - width * 0.5f;
                maxX = centerX + width * 0.5f;
            }

            return new MeetingTileRect(
                new Vector2(minX, minY),
                new Vector2(maxX, maxY),
                Vector2.zero,
                Vector2.zero);
        }

        private static Vector2 CalculateScenarioShakeOffset(IReadOnlyList<ScenarioStageCommand> stageCommands)
        {
            if (stageCommands is null)
            {
                return Vector2.zero;
            }

            var shake = stageCommands
                .Where(command => command.CommandType == ScenarioStageCommandType.Shake)
                .Select(command => Mathf.Clamp(command.Intensity, 0f, 1f))
                .DefaultIfEmpty(0f)
                .Max();
            if (shake <= 0f)
            {
                return Vector2.zero;
            }

            var phase = Time.unscaledTime * 34f;
            return new Vector2(Mathf.Sin(phase) * 34f * shake, Mathf.Cos(phase * 0.7f) * 22f * shake);
        }

        private static string BuildScenarioEffectText(IReadOnlyList<ScenarioStageCommand> stageCommands)
        {
            if (stageCommands is null || stageCommands.Count == 0)
            {
                return "";
            }

            var labels = stageCommands
                .Where(command => command.CommandType is ScenarioStageCommandType.Shake or ScenarioStageCommandType.ShowEffect or ScenarioStageCommandType.ShowSpeedLines or ScenarioStageCommandType.Collapse)
                .Select(command => command.CommandType.ToString().ToUpperInvariant())
                .Distinct()
                .ToList();
            return labels.Count == 0 ? "" : string.Join(" / ", labels);
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
            workSceneTitleText.text = $"PROCESSING REPORT  {workPerformanceIndex + 1}/{workPerformanceEvents.Count}";
            workSceneActorText.text = $"[WORKER ID CARD]\n{performance.PersonnelId}\n{performance.PersonnelName}\n\nSTAMP: USED";
            workSceneWorkText.text = $"[TARGET WORK FILE]\n{performance.EventId}\n{performance.WorkTitle}\n\n{performance.ResultSummary}";
            workSceneCardText.text = BuildHandRevealText(performance, progress);
            var criticalLine = performance.CriticalTriggered
                ? $"CRITICAL: DAESEONGGONG x{FormatMultiplier(performance.CriticalMultiplier)}  ROLL {performance.CriticalRoll:00}/{performance.CriticalChancePercent:00}\n"
                : $"CRITICAL: no hit  ROLL {performance.CriticalRoll:00}/{performance.CriticalChancePercent:00}\n";
            workSceneImpactText.text =
                $"RESULT REPORT\n" +
                $"OUTCOME {performance.OutcomeBefore:000} -> {LerpInt(performance.OutcomeBefore, performance.OutcomeAfter, progress):000}  {Signed(performance.OutcomeModifier)}\n" +
                $"RISK    {performance.RiskBefore:000} -> {LerpInt(performance.RiskBefore, performance.RiskAfter, progress):000}  {Signed(performance.RiskModifier)}\n\n" +
                $"SELECTED CARD: {performance.CardTitle}\n" +
                $"LOW OUT {Signed(performance.BaseOutcomeModifier)} RISK {Signed(performance.BaseRiskModifier)} | CRIT {performance.CriticalChancePercent}% x{FormatMultiplier(performance.CriticalMultiplier)}\n" +
                criticalLine +
                $"{performance.CardSummary}\n\nREPORT STATUS: STAMPED / RECORDED";
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

        private void ShowDailyReportAfterWork()
        {
            pendingDailyReportAfterWork = false;
            AddNightSummaryLog();
            openWindows.Clear();
            OpenDesktopWindow(MvpDesktopWindow.DailyReport);
            Render();
        }

        private static string BuildHandRevealText(WorkPerformanceEvent performance, float progress)
        {
            var revealSelection = progress >= 0.45f;
            var lines = new List<string> { "[CARD HAND]" };
            foreach (var card in performance.HandCards)
            {
                var marker = card.IsUsed
                    ? revealSelection ? "USED" : "????"
                    : "    ";
                lines.Add($"{marker} {card.Title} | OUT {Signed(card.OutcomeModifier)} RISK {Signed(card.RiskModifier)} | CRIT {card.CriticalChancePercent}% x{FormatMultiplier(card.CriticalMultiplier)}");
            }

            if (!revealSelection)
            {
                lines.Add("");
                lines.Add("CHOOSING...");
            }
            else if (performance.CriticalTriggered)
            {
                lines.Add("");
                lines.Add($"DAESEONGGONG x{FormatMultiplier(performance.CriticalMultiplier)}  ROLL {performance.CriticalRoll:00}/{performance.CriticalChancePercent:00}");
            }

            return string.Join("\n", lines);
        }

        private Transform CreateColumn(Transform parent, string name, float flexibleWidth, float minWidth, Color color)
        {
            var panel = CreatePanel(name, parent, color);
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
            text.gameObject.AddComponent<LayoutElement>().minHeight = 52;
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
            text.fontSize = Mathf.Max(MinUiFontSize, fontSize);
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = CrtTextColor;
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
            var panel = CreatePanel("Work File " + entry.EventId, boardCardRoot, FolderColor);
            panel.gameObject.AddComponent<LayoutElement>().minHeight = 196;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var remaining = item.Status == CaseStatus.Closed ? 0 : Math.Max(1, item.Volume);
            var tags = WorkTags(item);
            var title = CreateText($"FILE {item.Id}  {item.Title}", panel, 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.color = PaperTextColor;
            title.gameObject.AddComponent<LayoutElement>().minHeight = 46;
            var detail = CreateText($"Remaining sheets {remaining} | ID slots {assignment.Count}/{maxSlots} | Labels {tags}", panel, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
            detail.color = PaperTextColor;
            detail.gameObject.AddComponent<LayoutElement>().minHeight = 44;
            var riskLine = CreateText($"STAMP URG {item.Urgency}  SEV {item.Severity}  RISK {item.LatentRisk}  DEADLINE {Math.Max(0, item.TtlSec)}s", panel, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            riskLine.color = item.LatentRisk >= 60 || item.Urgency >= 70 ? WarningStampColor : PaperTextColor;
            riskLine.gameObject.AddComponent<LayoutElement>().minHeight = 44;

            var slots = CreateUiObject("Slots", panel).transform;
            var slotLayout = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
            slotLayout.spacing = 6;
            slotLayout.childForceExpandWidth = true;
            slotLayout.childForceExpandHeight = false;
            slots.gameObject.AddComponent<LayoutElement>().minHeight = 86;

            for (var slotIndex = 0; slotIndex < maxSlots; slotIndex++)
            {
                var slot = CreatePanel($"ID Slot {slotIndex + 1}", slots, new Color(0.24f, 0.19f, 0.13f, 1f));
                slot.gameObject.AddComponent<LayoutElement>().minHeight = 82;
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
                    var label = CreateText("EMPTY ID SLOT", slot, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
                    label.color = new Color(0.62f, 0.52f, 0.36f, 1f);
                }
            }
        }

        private void CreateReportCard(EventCase item)
        {
            var panel = CreatePanel("Report " + item.Id, boardCardRoot, PaperColor);
            panel.gameObject.AddComponent<LayoutElement>().minHeight = 150;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6;

            var reportText = CreateText(FormatResolvedEvent(item), panel, 13, FontStyle.Normal, TextAnchor.UpperLeft);
            reportText.color = PaperTextColor;
            reportText.gameObject.AddComponent<LayoutElement>().minHeight = 128;
        }

        private void CreateNightSummaryCard()
        {
            var resolved = CurrentState.Queue.Where(item => item.AutoResolved).ToList();
            var closed = CurrentState.Queue.Count(item => item.Status == CaseStatus.Closed);
            var open = CurrentState.Queue.Count(item => item.Status != CaseStatus.Closed);
            var averageOutcome = resolved.Count == 0 ? 0 : Mathf.RoundToInt((float)resolved.Average(item => item.OutcomeScore));
            var highestRisk = resolved.OrderByDescending(item => item.LatentRisk).FirstOrDefault();
            var pendingReviewCount = resolved.Count(item => !item.ReportReviewed);

            var panel = CreatePanel("Night Summary Card", boardCardRoot, PaperColor);
            panel.gameObject.AddComponent<LayoutElement>().minHeight = 320;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var summaryTitle = CreateText($"DAY {CurrentState.Day:00} REPORT - STAMPED", panel, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            summaryTitle.color = PaperTextColor;
            summaryTitle.gameObject.AddComponent<LayoutElement>().minHeight = 52;
            var summaryLine = CreateText($"Resolved {resolved.Count} | Closed {closed} | Open {open} | Avg Outcome {averageOutcome} | OVR {CurrentState.Overload} | Global Risk {CurrentState.GlobalLatentRisk}", panel, 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            summaryLine.color = PaperTextColor;
            summaryLine.gameObject.AddComponent<LayoutElement>().minHeight = 48;
            var highRiskLine = CreateText($"Highest risk: {(highestRisk is null ? "none" : $"{highestRisk.Id} {highestRisk.Title} / risk {highestRisk.LatentRisk}")}", panel, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            highRiskLine.color = highestRisk is not null && highestRisk.LatentRisk >= 60 ? WarningStampColor : PaperTextColor;
            highRiskLine.gameObject.AddComponent<LayoutElement>().minHeight = 48;
            var pendingLine = CreateText($"MVP filing note: pending reports auto-clear on Next Morning: {pendingReviewCount}", panel, 13, FontStyle.Italic, TextAnchor.MiddleLeft);
            pendingLine.color = PaperTextColor;
            pendingLine.gameObject.AddComponent<LayoutElement>().minHeight = 48;

            foreach (var item in resolved.OrderByDescending(item => item.Severity + item.Urgency).Take(4))
            {
                var itemText = CreateText($"{item.Id} | OUT {item.OutcomeScore} | RISK {item.LatentRisk} | {item.ResultSummary}", panel, 13, FontStyle.Normal, TextAnchor.UpperLeft);
                itemText.color = PaperTextColor;
                itemText.gameObject.AddComponent<LayoutElement>().minHeight = 64;
            }
        }

        private void CreateCharacterToken(Personnel person, Transform parent, string sourceEventId, bool selected, bool dimmed = false, string statusSuffix = "")
        {
            var baseColor = selected ? new Color(0.90f, 0.88f, 0.72f, 1f) : IdCardColor;
            var token = CreatePanel("Personnel ID " + person.Id, parent, dimmed ? new Color(baseColor.r * 0.55f, baseColor.g * 0.55f, baseColor.b * 0.55f, 0.72f) : baseColor);
            token.gameObject.AddComponent<LayoutElement>().minHeight = 110;
            if (parent.GetComponent<WorkSlotDropTarget>() is not null)
            {
                Stretch((RectTransform)token);
            }

            var drag = token.gameObject.AddComponent<DraggableCharacterToken>();
            drag.Initialize(this, person.Id, sourceEventId, dragLayer);

            var suffix = string.IsNullOrWhiteSpace(statusSuffix) ? "" : $"\n{statusSuffix}";
            var text = CreateText($"ID {person.Id}  {person.Name}\nLOAD {person.LoadAssigned}/{Math.Max(1, person.MaxLoad)} | FAT {person.Fatigue} | TRUST {person.TrustToManager}{suffix}", token, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.color = dimmed ? new Color(PaperTextColor.r, PaperTextColor.g, PaperTextColor.b, 0.45f) : PaperTextColor;
            text.raycastTarget = false;
        }

        private void CreateCharacterTab(Personnel person, Transform parent, bool selected)
        {
            var tab = CreatePanel("Character Tab " + person.Id, parent, selected ? new Color(0.90f, 0.88f, 0.72f, 1f) : IdCardColor);
            var layout = tab.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 190;
            layout.minHeight = 66;
            var button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = tab.GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                selectedPersonnelId = person.Id;
                Render();
            });

            var text = CreateText($"{person.Id}\n{person.Name}", tab, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.offsetMin = new Vector2(6, 4);
            text.rectTransform.offsetMax = new Vector2(-6, -4);
            text.color = PaperTextColor;
            text.raycastTarget = false;
        }

        private void CreateCharacterFaceBlock(Personnel person, Transform parent)
        {
            var face = CreatePanel("Character Face " + person.Id, parent, IdCardColor);
            var layout = face.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 132;
            layout.preferredWidth = 132;
            layout.minHeight = 132;
            var text = CreateText($"FACE\n{person.Id}", face, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.color = PaperTextColor;
            text.raycastTarget = false;
        }

        private void CreateCardFace(DebugCard card, Transform parent, bool used)
        {
            var panel = CreatePanel("Desk Card " + card.Id, parent, used ? new Color(0.46f, 0.43f, 0.36f, 1f) : PaperColor);
            panel.gameObject.AddComponent<LayoutElement>().minHeight = 140;
            var text = CreateText($"{card.Title}\n{string.Join(", ", card.Tags)} | LOW OUT {Signed(card.OutcomeModifier)} RISK {Signed(card.RiskModifier)} | CRIT {card.CriticalChancePercent}% x{FormatMultiplier(card.CriticalMultiplier)}\n{(used ? "STAMP: USED" : card.Summary)}", panel, 16, used ? FontStyle.Italic : FontStyle.Normal, TextAnchor.MiddleLeft);
            text.rectTransform.offsetMin = new Vector2(8, 4);
            text.rectTransform.offsetMax = new Vector2(-8, -4);
            text.color = used ? WarningStampColor : PaperTextColor;
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
            var compactAssignment = assignment.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (compactAssignment.Count == 0)
            {
                DispatchWithoutRender($"adjust {eventId} none");
                return;
            }

            DispatchWithoutRender($"adjust {eventId} {string.Join(",", compactAssignment)}");
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

        private string FindAssignedEventId(string personnelId, string exceptEventId)
        {
            foreach (var pair in plannedAssignments)
            {
                if (pair.Key.Equals(exceptEventId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (pair.Value.Any(id => id.Equals(personnelId, StringComparison.OrdinalIgnoreCase)))
                {
                    return pair.Key;
                }
            }

            return "";
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
                    var resolved = ResolveCardUse(used, personId, entry.EventId);
                    deck.UsedToday.Add(used.Id);
                    activeCards.Add(ToRuntimeCard(used, personId, entry.EventId, resolved));
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
                            CriticalChancePercent = card.CriticalChancePercent,
                            CriticalMultiplier = card.CriticalMultiplier,
                            IsUsed = card.Id.Equals(used.Id, StringComparison.OrdinalIgnoreCase)
                        }).ToList(),
                        OutcomeBefore = item?.OutcomeScore ?? 0,
                        RiskBefore = item?.LatentRisk ?? 0,
                        OutcomeModifier = resolved.OutcomeModifier,
                        RiskModifier = resolved.RiskModifier,
                        BaseOutcomeModifier = used.OutcomeModifier,
                        BaseRiskModifier = used.RiskModifier,
                        CriticalChancePercent = used.CriticalChancePercent,
                        CriticalMultiplier = used.CriticalMultiplier,
                        CriticalTriggered = resolved.CriticalTriggered,
                        CriticalRoll = resolved.CriticalRoll
                    });
                    AddLog($"{entry.EventId}: {personId} used card [{used.Title}] {(resolved.CriticalTriggered ? "DAESEONGGONG" : "normal")} OUT {Signed(resolved.OutcomeModifier)} RISK {Signed(resolved.RiskModifier)}");
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
                    RiskModifier = template.RiskModifier,
                    CriticalChancePercent = template.CriticalChancePercent,
                    CriticalMultiplier = template.CriticalMultiplier
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
                new() { Title = "Fast Triage", Summary = "Cuts setup time.", Tags = new List<string> { "speed", "review" }, OutcomeModifier = 8, RiskModifier = 3, CriticalChancePercent = 15, CriticalMultiplier = 1.5f },
                new() { Title = "Second Pair", Summary = "Adds cross-check discipline.", Tags = new List<string> { "audit", "team" }, OutcomeModifier = 5, RiskModifier = -7, CriticalChancePercent = 18, CriticalMultiplier = 2f },
                new() { Title = "Shortcut Patch", Summary = "Skips a slow protocol.", Tags = new List<string> { "speed", "unsafe" }, OutcomeModifier = 12, RiskModifier = 10, CriticalChancePercent = 12, CriticalMultiplier = 1.5f },
                new() { Title = "Quiet Notes", Summary = "Finds hidden context.", Tags = new List<string> { "intel", "memory" }, OutcomeModifier = 4, RiskModifier = -4, CriticalChancePercent = 24, CriticalMultiplier = 2.25f },
                new() { Title = "Stress Buffer", Summary = "Protects morale under load.", Tags = new List<string> { "care", "fatigue" }, OutcomeModifier = 3, RiskModifier = -6, CriticalChancePercent = 20, CriticalMultiplier = 2f },
            };
        }

        private CardUseResult ResolveCardUse(DebugCard card, string personnelId, string eventId)
        {
            var chance = Mathf.Clamp(card.CriticalChancePercent, 0, 100);
            var multiplier = Mathf.Max(1f, card.CriticalMultiplier);
            var roll = StablePositiveHash($"{CurrentState.Seed}:{CurrentState.Day}:{eventId}:{personnelId}:{card.Id}:crit") % 100 + 1;
            var critical = chance > 0 && multiplier > 1f && roll <= chance;
            return new CardUseResult
            {
                OutcomeModifier = critical ? ApplyCriticalOutcomeBonus(card.OutcomeModifier, multiplier) : card.OutcomeModifier,
                RiskModifier = critical ? ApplyCriticalRiskBonus(card.RiskModifier, multiplier) : card.RiskModifier,
                CriticalTriggered = critical,
                CriticalRoll = roll
            };
        }

        private static int ApplyCriticalOutcomeBonus(int modifier, float multiplier)
        {
            return modifier > 0 ? Mathf.RoundToInt(modifier * multiplier) : modifier;
        }

        private static int ApplyCriticalRiskBonus(int modifier, float multiplier)
        {
            return modifier < 0 ? Mathf.RoundToInt(modifier * multiplier) : modifier;
        }

        private static ActionCard ToRuntimeCard(DebugCard card, string personnelId, string eventId, CardUseResult result)
        {
            return new ActionCard
            {
                Id = card.Id,
                OwnerPersonnelId = personnelId,
                TargetEventId = eventId,
                Title = card.Title,
                Summary = card.Summary,
                Tags = card.Tags.ToList(),
                OutcomeModifier = result.OutcomeModifier,
                RiskModifier = result.RiskModifier,
                CriticalChancePercent = card.CriticalChancePercent,
                CriticalMultiplier = card.CriticalMultiplier,
                CriticalTriggered = result.CriticalTriggered,
                CriticalRoll = result.CriticalRoll
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

        private static int StablePositiveHash(string value)
        {
            unchecked
            {
                var hash = 23;
                foreach (var character in value)
                {
                    hash = hash * 31 + character;
                }

                return hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
            }
        }

        private static string FormatMultiplier(float multiplier)
        {
            return Mathf.Max(1f, multiplier).ToString("0.##");
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

        private readonly struct MeetingTileRect
        {
            public MeetingTileRect(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
            {
                AnchorMin = anchorMin;
                AnchorMax = anchorMax;
                OffsetMin = offsetMin;
                OffsetMax = offsetMax;
            }

            public Vector2 AnchorMin { get; }
            public Vector2 AnchorMax { get; }
            public Vector2 OffsetMin { get; }
            public Vector2 OffsetMax { get; }
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

    internal enum MvpDesktopWindow
    {
        None,
        CurrentWorkDashboard,
        TodayWorkPlan,
        DailyReport,
        CharacterProfiling,
        DevTools
    }

    internal sealed class DesktopShortcutDefinition
    {
        public DesktopShortcutDefinition(string id, string label, string summary, string iconText, MvpDesktopWindow targetWindow, Color color, Color textColor, Func<bool> isEnabled = null)
        {
            Id = id;
            Label = label;
            Summary = summary;
            IconText = iconText;
            TargetWindow = targetWindow;
            Color = color;
            TextColor = textColor;
            IsEnabled = isEnabled;
        }

        public string Id { get; }
        public string Label { get; }
        public string Summary { get; }
        public string IconText { get; }
        public MvpDesktopWindow TargetWindow { get; }
        public Color Color { get; }
        public Color TextColor { get; }
        public Func<bool> IsEnabled { get; }
    }

    internal readonly struct WindowLayoutState
    {
        public WindowLayoutState(Vector2 anchorMin, Vector2 anchorMax)
        {
            AnchorMin = anchorMin;
            AnchorMax = anchorMax;
        }

        public Vector2 AnchorMin { get; }
        public Vector2 AnchorMax { get; }
    }

    internal sealed class DesktopWindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
    {
        private CaseReviewMvpSceneController controller;
        private MvpDesktopWindow window;
        private RectTransform windowRect;
        private RectTransform parentRect;
        private Vector2 startAnchorMin;
        private Vector2 startAnchorMax;
        private Vector2 startScreenPosition;

        public void Initialize(CaseReviewMvpSceneController owner, MvpDesktopWindow targetWindow, RectTransform targetRect, RectTransform targetParent)
        {
            controller = owner;
            window = targetWindow;
            windowRect = targetRect;
            parentRect = targetParent;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            controller?.FocusDesktopWindow(window);
            windowRect?.SetAsLastSibling();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (windowRect is null)
            {
                return;
            }

            controller?.FocusDesktopWindow(window);
            windowRect.SetAsLastSibling();
            startAnchorMin = windowRect.anchorMin;
            startAnchorMax = windowRect.anchorMax;
            startScreenPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (controller is null || windowRect is null)
            {
                return;
            }

            var parentSize = ParentSize();
            var delta = eventData.position - startScreenPosition;
            var anchorDelta = new Vector2(delta.x / parentSize.x, delta.y / parentSize.y);
            var state = controller.RememberWindowLayout(window, startAnchorMin + anchorDelta, startAnchorMax + anchorDelta);
            Apply(state);
        }

        private Vector2 ParentSize()
        {
            var size = parentRect is null ? new Vector2(1920f, 1080f) : parentRect.rect.size;
            return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        }

        private void Apply(WindowLayoutState state)
        {
            windowRect.anchorMin = state.AnchorMin;
            windowRect.anchorMax = state.AnchorMax;
            windowRect.offsetMin = Vector2.zero;
            windowRect.offsetMax = Vector2.zero;
            controller?.RefreshFloatingPanels();
        }
    }

    internal sealed class DesktopWindowResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
    {
        private CaseReviewMvpSceneController controller;
        private MvpDesktopWindow window;
        private RectTransform windowRect;
        private RectTransform parentRect;
        private Vector2 startAnchorMin;
        private Vector2 startAnchorMax;
        private Vector2 startScreenPosition;

        public void Initialize(CaseReviewMvpSceneController owner, MvpDesktopWindow targetWindow, RectTransform targetRect, RectTransform targetParent)
        {
            controller = owner;
            window = targetWindow;
            windowRect = targetRect;
            parentRect = targetParent;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            controller?.FocusDesktopWindow(window);
            windowRect?.SetAsLastSibling();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (windowRect is null)
            {
                return;
            }

            controller?.FocusDesktopWindow(window);
            windowRect.SetAsLastSibling();
            startAnchorMin = windowRect.anchorMin;
            startAnchorMax = windowRect.anchorMax;
            startScreenPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (controller is null || windowRect is null)
            {
                return;
            }

            var parentSize = ParentSize();
            var delta = eventData.position - startScreenPosition;
            var anchorDelta = new Vector2(delta.x / parentSize.x, delta.y / parentSize.y);
            var state = controller.RememberWindowLayout(
                window,
                new Vector2(startAnchorMin.x, startAnchorMin.y + anchorDelta.y),
                new Vector2(startAnchorMax.x + anchorDelta.x, startAnchorMax.y));
            Apply(state);
        }

        private Vector2 ParentSize()
        {
            var size = parentRect is null ? new Vector2(1920f, 1080f) : parentRect.rect.size;
            return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        }

        private void Apply(WindowLayoutState state)
        {
            windowRect.anchorMin = state.AnchorMin;
            windowRect.anchorMax = state.AnchorMax;
            windowRect.offsetMin = Vector2.zero;
            windowRect.offsetMax = Vector2.zero;
            controller?.RefreshFloatingPanels();
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
            ghost.sizeDelta = new Vector2(180, 64);
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
        public int CriticalChancePercent { get; set; }
        public float CriticalMultiplier { get; set; } = 1f;
    }

    internal struct CardUseResult
    {
        public int OutcomeModifier { get; set; }
        public int RiskModifier { get; set; }
        public bool CriticalTriggered { get; set; }
        public int CriticalRoll { get; set; }
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
        public int BaseOutcomeModifier { get; set; }
        public int BaseRiskModifier { get; set; }
        public int CriticalChancePercent { get; set; }
        public float CriticalMultiplier { get; set; } = 1f;
        public bool CriticalTriggered { get; set; }
        public int CriticalRoll { get; set; }
        public string ResultSummary { get; set; } = "";
    }

    internal sealed class WorkPerformanceCardSnapshot
    {
        public string Title { get; set; } = "";
        public int OutcomeModifier { get; set; }
        public int RiskModifier { get; set; }
        public int CriticalChancePercent { get; set; }
        public float CriticalMultiplier { get; set; } = 1f;
        public bool IsUsed { get; set; }
    }
}
