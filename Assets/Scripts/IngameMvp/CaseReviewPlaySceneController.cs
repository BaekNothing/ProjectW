using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProjectW.IngameCore.CaseReview;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectW.IngameMvp
{
    public sealed class CaseReviewPlaySceneController : MonoBehaviour
    {
        private const string IngameSceneName = "MVP Scene";
        private const string LegacyRuntimeFontName = "LegacyRuntime.ttf";
        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

        [SerializeField] private int seed = 1042;

        private CaseReviewSessionController _session;
        private OfficePanel _activePanel = OfficePanel.Dashboard;
        private string _selectedEventId = string.Empty;
        private string _selectedPersonId = string.Empty;

        private Text _topBarText;
        private Text _centerText;
        private Text _rightText;
        private Text _phaseText;
        private Text _historyText;
        private GameObject _cycleGuideOverlay;
        private ScrollRect _historyScroll;
        private InputField _input;
        private readonly List<string> _history = new List<string>();

        public CaseReviewSessionController Session => _session;
        public string ConsoleHistory => string.Join(Environment.NewLine, _history);
        public bool IsCycleGuideVisible => _cycleGuideOverlay != null && _cycleGuideOverlay.activeSelf;

        private enum OfficePanel
        {
            Dashboard,
            People,
            TaskBoard,
            AssignmentPlan,
            Reports,
            Finance,
            Events,
            Lab,
            Records
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneController()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, IngameSceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (FindFirstObjectByType<CaseReviewPlaySceneController>() != null)
            {
                return;
            }

            var root = new GameObject(nameof(CaseReviewPlaySceneController));
            root.AddComponent<CaseReviewPlaySceneController>();
        }

        private void Awake()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, IngameSceneName, StringComparison.Ordinal))
            {
                enabled = false;
                return;
            }

            InitializeOffice();
        }

        public void InitializeForTests()
        {
            InitializeOffice();
        }

        private void InitializeOffice()
        {
            EnsureCamera();
            EnsureEventSystem();
            EnsureSession();
            EnsureDefaultSelection();
            BuildUi();
            SubmitCommand("status");
        }

        private void Update()
        {
            if (Keyboard.current?.backquoteKey.wasPressedThisFrame == true)
            {
                ToggleCycleGuide();
            }
        }

        public void ToggleCycleGuide()
        {
            if (_cycleGuideOverlay == null)
            {
                return;
            }

            _cycleGuideOverlay.SetActive(!_cycleGuideOverlay.activeSelf);
        }

        public void SubmitInput()
        {
            if (_input == null)
            {
                return;
            }

            var command = _input.text;
            _input.text = string.Empty;
            SubmitCommand(command);
            _input.ActivateInputField();
        }

        public void SubmitCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                RefreshPanels();
                return;
            }

            EnsureSession();
            var trimmed = command.Trim();
            var result = _session.DispatchCommand(trimmed);
            _history.Add("> " + trimmed);
            if (!string.IsNullOrWhiteSpace(result.Code))
            {
                _history.Add((result.Success ? "OK " : "FAIL ") + result.Code);
            }

            if (result.Lines.Count == 0)
            {
                _history.Add(result.Success ? "OK" : "FAIL");
            }
            else
            {
                _history.AddRange(result.Lines);
            }

            TrimHistory();
            EnsureDefaultSelection();
            RefreshPanels();
        }

        public void SubmitQuickCommand(string commandTemplate)
        {
            var command = ResolveCommandTemplate(commandTemplate);
            SubmitCommand(command);
        }

        private void EnsureSession()
        {
            _session = FindFirstObjectByType<CaseReviewSessionController>();
            if (_session == null)
            {
                _session = gameObject.AddComponent<CaseReviewSessionController>();
            }

            if (!_session.IsInitialized)
            {
                _session.Initialize(seed);
            }
        }

        private void EnsureDefaultSelection()
        {
            if (_session?.State == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedEventId)
                || _session.State.Queue.All(e => !e.Id.Equals(_selectedEventId, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedEventId = _session.State.Queue.FirstOrDefault(e => e.Status != CaseStatus.Closed)?.Id
                    ?? _session.State.Queue.FirstOrDefault()?.Id
                    ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(_selectedPersonId)
                || _session.State.Staff.All(s => !s.Id.Equals(_selectedPersonId, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedPersonId = _session.State.Staff.FirstOrDefault(s => !s.HasLeft)?.Id
                    ?? _session.State.Staff.FirstOrDefault()?.Id
                    ?? string.Empty;
            }
        }

        private void BuildUi()
        {
            var oldCanvas = GameObject.Find("CaseReviewCanvas");
            if (oldCanvas != null)
            {
                Destroy(oldCanvas);
            }

            var canvasGo = new GameObject("CaseReviewCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            var root = CreateRect(canvasGo.transform, "ManagementOfficeRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.offsetMin = new Vector2(18f, 18f);
            root.offsetMax = new Vector2(-18f, -18f);

            var topBar = CreatePanel(root, "GlobalStatusBar", new Vector2(0f, 0.9f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, "Regeneration Management Console");
            _topBarText = CreateText(topBar, "GlobalStatusText", string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetStretch(_topBarText.rectTransform, 18f, 16f, 42f, 8f);

            var navPanel = CreatePanel(root, "OfficeNavigationPanel", new Vector2(0f, 0.25f), new Vector2(0.16f, 0.89f), Vector2.zero, new Vector2(-8f, 0f), "업무 메뉴");
            BuildNavigation(navPanel);

            var centerPanel = CreatePanel(root, "OfficeWorkPanel", new Vector2(0.16f, 0.25f), new Vector2(0.74f, 0.89f), new Vector2(8f, 0f), new Vector2(-8f, 0f), "작업 패널");
            _centerText = CreateScrollText(centerPanel, "OfficeWorkText", out _);

            var rightPanel = CreatePanel(root, "OfficeSignalPanel", new Vector2(0.74f, 0.25f), new Vector2(1f, 0.89f), new Vector2(8f, 0f), Vector2.zero, "상태 / 알림");
            _rightText = CreateScrollText(rightPanel, "OfficeSignalText", out _);

            var phasePanel = CreatePanel(root, "TurnProgressPanel", new Vector2(0f, 0.12f), new Vector2(0.58f, 0.24f), Vector2.zero, new Vector2(-8f, 0f), "턴 진행");
            _phaseText = CreateText(phasePanel, "TurnProgressText", string.Empty, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetStretch(_phaseText.rectTransform, 14f, 12f, 40f, 8f);

            var actionsPanel = CreatePanel(root, "OfficeActionPanel", new Vector2(0.58f, 0.12f), new Vector2(1f, 0.24f), new Vector2(8f, 0f), Vector2.zero, "주요 처리");
            BuildActionButtons(actionsPanel);

            var historyPanel = CreatePanel(root, "CommandLogPanel", new Vector2(0f, 0f), new Vector2(0.68f, 0.11f), Vector2.zero, new Vector2(-8f, 0f), "Command Log");
            _historyText = CreateScrollText(historyPanel, "ConsoleHistoryText", out _historyScroll);

            var commandPanel = CreatePanel(root, "DebugCommandPanel", new Vector2(0.68f, 0f), new Vector2(1f, 0.11f), new Vector2(8f, 0f), Vector2.zero, "Debug Command");
            BuildDebugCommand(commandPanel);

            BuildCycleGuideOverlay(root);
        }

        private void BuildNavigation(RectTransform parent)
        {
            var items = new[]
            {
                (OfficePanel.Dashboard, "대시보드"),
                (OfficePanel.People, "재생 인력"),
                (OfficePanel.TaskBoard, "업무 보드"),
                (OfficePanel.AssignmentPlan, "배정 계획"),
                (OfficePanel.Reports, "보고서함"),
                (OfficePanel.Finance, "재무"),
                (OfficePanel.Events, "이벤트"),
                (OfficePanel.Lab, "실험실"),
                (OfficePanel.Records, "기록")
            };

            for (var i = 0; i < items.Length; i++)
            {
                var y = 48f + i * 34f;
                var item = items[i];
                var button = CreateButton(parent, "Nav_" + item.Item1, item.Item2, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                SetTopStretch(button.GetComponent<RectTransform>(), 12f, 12f, y, 28f);
                button.onClick.AddListener(() =>
                {
                    _activePanel = item.Item1;
                    RefreshPanels();
                });
            }
        }

        private void BuildActionButtons(RectTransform parent)
        {
            var actions = new[]
            {
                ("요약", "SUMMARY {id}"),
                ("검토", "CHECK {id}"),
                ("보류", "HOLD {id}"),
                ("승인", "APPROVE {id}"),
                ("보고서", "REPORT"),
                ("전체 검토", "REVIEW ALL"),
                ("계획 확정", "CONFIRM PLAN"),
                ("다음 날", "NEXT DAY"),
                ("다음 업무", "SELECT NEXT TASK"),
                ("다음 인력", "SELECT NEXT PERSON"),
                ("업무 주입", "LAB ADD TASK"),
                ("재무 압박", "LAB FINANCE PRESSURE"),
                ("인력 과부하", "LAB STAFF OVERLOAD"),
                ("보고 생성", "LAB GENERATE REPORT")
            };

            const int columns = 7;
            const float left = 12f;
            const float top = 38f;
            const float width = 66f;
            const float height = 22f;
            const float gap = 6f;

            for (var i = 0; i < actions.Length; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var x = left + col * (width + gap);
                var y = top + row * (height + gap);
                var action = actions[i];
                var button = CreateButton(parent, "Action_" + SanitizeName(action.Item2), action.Item1, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                SetTopLeftFixed(button.GetComponent<RectTransform>(), x, y, width, height);
                button.onClick.AddListener(() => SubmitOfficeAction(action.Item2));
            }
        }

        private void SubmitOfficeAction(string action)
        {
            if (action.Equals("SELECT NEXT TASK", StringComparison.OrdinalIgnoreCase))
            {
                SelectNextTask();
                RefreshPanels();
                return;
            }

            if (action.Equals("SELECT NEXT PERSON", StringComparison.OrdinalIgnoreCase))
            {
                SelectNextPerson();
                RefreshPanels();
                return;
            }

            if (action.StartsWith("LAB ", StringComparison.OrdinalIgnoreCase))
            {
                ApplyLabAction(action);
                RefreshPanels();
                return;
            }

            SubmitQuickCommand(action);
        }

        private void ApplyLabAction(string action)
        {
            var state = _session.State;
            _activePanel = OfficePanel.Lab;

            if (action.Equals("LAB ADD TASK", StringComparison.OrdinalIgnoreCase))
            {
                var id = $"X-{state.Day:D2}{state.Queue.Count + 1:D2}";
                state.Queue.Add(new EventCase
                {
                    Id = id,
                    Kind = "experiment",
                    Title = "실험 주입 업무 - 보고와 현장 상태 불일치 검증",
                    Subsystem = "LAB",
                    Urgency = 72,
                    Severity = 68,
                    TtlSec = 0,
                    LatentRisk = 45,
                    MismatchScore = 3,
                    BaseSuccessChance = 52,
                    PhysicalCost = 6,
                    MentalCost = 12
                });
                state.Logs.Add(new VisibleLog
                {
                    Id = $"L-{state.Logs.Count + 1:D3}",
                    EventId = id,
                    SourceType = "summary",
                    VisibleAtSec = state.TotalElapsedSec,
                    Text = $"[SUMMARY][{id}] 신규 실험 업무가 접수되었습니다. 요약만으로는 실제 영향 범위를 확인할 수 없습니다.",
                    Omitted = true
                });
                _selectedEventId = id;
                _history.Add("[LAB] 업무 주입: " + id);
                return;
            }

            if (action.Equals("LAB FINANCE PRESSURE", StringComparison.OrdinalIgnoreCase))
            {
                state.AuditBudget = Math.Max(0, state.AuditBudget - 1);
                state.RedirectBudget = Math.Max(0, state.RedirectBudget - 1);
                state.GlobalLatentRisk = Math.Min(200, state.GlobalLatentRisk + 28);
                _history.Add("[LAB] 재무 압박 증가: 검토/재배정 여유 감소");
                return;
            }

            if (action.Equals("LAB STAFF OVERLOAD", StringComparison.OrdinalIgnoreCase))
            {
                var person = state.Staff.FirstOrDefault(s => s.Id.Equals(_selectedPersonId, StringComparison.OrdinalIgnoreCase))
                    ?? state.Staff.FirstOrDefault(s => !s.HasLeft);
                if (person == null)
                {
                    _history.Add("[LAB] 조작 실패: 가용 인력 없음");
                    return;
                }

                person.LoadAssigned += 6;
                person.Fatigue = Math.Min(100, person.Fatigue + 24);
                person.MentalStress = Math.Min(100, person.MentalStress + 18);
                person.RetentionRisk = Math.Min(100, person.RetentionRisk + 20);
                _selectedPersonId = person.Id;
                _history.Add("[LAB] 인력 과부하: " + person.Id);
                return;
            }

            if (action.Equals("LAB GENERATE REPORT", StringComparison.OrdinalIgnoreCase))
            {
                var active = state.Queue.Where(e => e.Status != CaseStatus.Closed).ToList();
                state.Reports.Add(new DailyReportDocument
                {
                    Day = state.Day,
                    Title = $"Day {state.Day:D2} 실험 보고서",
                    Generator = "ux-lab",
                    Body = $"실험 상태: 진행 업무 {active.Count}건. 재무 반응 {FinanceResponseBand(state)}. 선택 업무 {_selectedEventId}. 보고서는 현재 상태를 완전히 증명하지 않습니다."
                });
                _history.Add("[LAB] 실험 보고서 생성");
            }
        }

        private void SelectNextTask()
        {
            var tasks = _session.State.Queue
                .Where(e => e.Status != CaseStatus.Closed)
                .OrderByDescending(e => e.Urgency + e.Severity)
                .ToList();
            if (tasks.Count == 0)
            {
                return;
            }

            var index = tasks.FindIndex(e => e.Id.Equals(_selectedEventId, StringComparison.OrdinalIgnoreCase));
            _selectedEventId = tasks[(index + 1 + tasks.Count) % tasks.Count].Id;
            _activePanel = OfficePanel.TaskBoard;
        }

        private void SelectNextPerson()
        {
            var people = _session.State.Staff
                .Where(s => !s.HasLeft)
                .OrderBy(s => s.Id)
                .ToList();
            if (people.Count == 0)
            {
                return;
            }

            var index = people.FindIndex(s => s.Id.Equals(_selectedPersonId, StringComparison.OrdinalIgnoreCase));
            _selectedPersonId = people[(index + 1 + people.Count) % people.Count].Id;
            _activePanel = OfficePanel.People;
        }

        private void BuildDebugCommand(RectTransform parent)
        {
            _input = CreateInput(parent, "ConsoleInput", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTopStretch(_input.GetComponent<RectTransform>(), 12f, 88f, 38f, 26f);
            _input.onSubmit.AddListener(_ => SubmitInput());

            var submit = CreateButton(parent, "SubmitButton", "Run", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTopRightFixed(submit.GetComponent<RectTransform>(), 12f, 38f, 68f, 26f);
            submit.onClick.AddListener(SubmitInput);
        }

        private void BuildCycleGuideOverlay(RectTransform parent)
        {
            var shade = CreateRect(parent, "CycleGuideOverlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var shadeImage = shade.gameObject.AddComponent<Image>();
            shadeImage.color = new Color(0.015f, 0.018f, 0.02f, 0.78f);
            _cycleGuideOverlay = shade.gameObject;

            var panel = CreatePanel(shade, "CycleGuidePanel", new Vector2(0.14f, 0.12f), new Vector2(0.86f, 0.88f), Vector2.zero, Vector2.zero, "한 사이클 운영 설명서   (` 로 닫기)");
            var guideText = CreateScrollText(panel, "CycleGuideText", out _);
            guideText.fontSize = 15;
            guideText.text = BuildCycleGuideText();

            var close = CreateButton(panel, "CycleGuideCloseButton", "닫기", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTopRightFixed(close.GetComponent<RectTransform>(), 12f, 8f, 64f, 26f);
            close.onClick.AddListener(ToggleCycleGuide);

            _cycleGuideOverlay.SetActive(false);
        }

        private void RefreshPanels()
        {
            if (_session?.State == null)
            {
                return;
            }

            EnsureDefaultSelection();
            var state = _session.State;
            _topBarText.text = BuildTopBarText(state);
            _centerText.text = BuildCenterText(state);
            _rightText.text = BuildRightText(state);
            _phaseText.text = BuildPhaseText(state);
            _historyText.text = BuildCompactHistory();

            Canvas.ForceUpdateCanvases();
            if (_historyScroll != null)
            {
                _historyScroll.verticalNormalizedPosition = 0f;
            }
        }

        private string BuildCenterText(GameState state)
        {
            return _activePanel switch
            {
                OfficePanel.People => BuildPeopleText(state),
                OfficePanel.TaskBoard => BuildTaskBoardText(state),
                OfficePanel.AssignmentPlan => BuildAssignmentText(state),
                OfficePanel.Reports => BuildReportsText(state),
                OfficePanel.Finance => BuildFinanceText(state),
                OfficePanel.Events => BuildEventsText(state),
                OfficePanel.Lab => BuildLabText(state),
                OfficePanel.Records => BuildRecordsText(state),
                _ => BuildDashboardText(state)
            };
        }

        private static string BuildTopBarText(GameState state)
        {
            var activeQueue = state.Queue.Count(e => e.Status != CaseStatus.Closed);
            return $"Day {state.Day:D2}   {state.Slot.ToString().ToUpperInvariant()}   업무 {activeQueue}/{state.Config.QueueSoftCap}   과부하 {Band(state.Overload)}   재검토 {state.AuditBudget}   재배정 {state.RedirectBudget}   면담 {state.InterviewBudget}";
        }

        private string BuildDashboardText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("오늘의 관리 요약");
            builder.AppendLine();
            builder.AppendLine($"- 진행 업무: {state.Queue.Count(e => e.Status != CaseStatus.Closed)}건");
            builder.AppendLine($"- 승인/검토 자원: 재검토 {state.AuditBudget}, 재배정 {state.RedirectBudget}, 면담 {state.InterviewBudget}");
            builder.AppendLine($"- 조직 위험 신호: {Band(state.GlobalLatentRisk)}");
            builder.AppendLine($"- 재무 응답 상태: {FinanceResponseBand(state)}");
            builder.AppendLine();
            builder.AppendLine("우선 확인 대상");
            foreach (var item in state.Queue.Where(e => e.Status != CaseStatus.Closed).OrderByDescending(e => e.Urgency + e.Severity).Take(5))
            {
                builder.AppendLine($"[{item.Id}] {item.Title}");
                builder.AppendLine($"  {item.Kind} / 긴급 {Band(item.Urgency)} / 영향 {Band(item.Severity)} / 상태 {item.Status}");
            }

            builder.AppendLine();
            builder.AppendLine("진행 힌트");
            builder.AppendLine("- 업무 카드를 열고 요약, 로그, 검토 순서로 근거를 확인하십시오.");
            builder.AppendLine("- 계획 확정 전에는 배정 계획에서 담당자를 조정할 수 있습니다.");
            builder.AppendLine("- 보고서상 완료와 현장 상태는 일치하지 않을 수 있습니다.");
            return builder.ToString();
        }

        private string BuildPeopleText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("재생 인력 관리");
            builder.AppendLine("능력 수치 대신 배정 상태, 반복 징후, 업무 적합 신호를 표시합니다.");
            builder.AppendLine();

            foreach (var person in state.Staff.OrderBy(s => s.Id))
            {
                var selected = person.Id.Equals(_selectedPersonId, StringComparison.OrdinalIgnoreCase) ? ">" : " ";
                builder.AppendLine($"{selected} {person.Id}  {person.Name}  {person.Background}");
                builder.AppendLine($"   상태 {PersonStateBand(person)} / 부하 {LoadBand(person)} / 신뢰 {TrustBand(person)} / 사용 {(person.HasLeft ? "불가" : "가능")}");
                builder.AppendLine($"   성향: {Trim(person.WorkStyle, 72)}");
            }

            var selectedPerson = state.Staff.FirstOrDefault(s => s.Id.Equals(_selectedPersonId, StringComparison.OrdinalIgnoreCase));
            if (selectedPerson != null)
            {
                builder.AppendLine();
                builder.AppendLine("선택 인력 상세");
                builder.AppendLine($"{selectedPerson.Id} {selectedPerson.Name}");
                builder.AppendLine($"관심: {string.Join(", ", selectedPerson.Interests)}");
                builder.AppendLine($"특성: {string.Join(", ", selectedPerson.Perks.Select(p => p.Name).DefaultIfEmpty("기록 없음"))}");
                builder.AppendLine($"관계 기록: {selectedPerson.Relationships.Count}건");
            }

            return builder.ToString();
        }

        private string BuildTaskBoardText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("업무 보드");
            builder.AppendLine("선택 업무는 주요 처리 버튼의 대상이 됩니다.");
            builder.AppendLine();

            foreach (var item in state.Queue.Where(e => e.Status != CaseStatus.Closed).OrderByDescending(e => e.Urgency + e.Severity))
            {
                var selected = item.Id.Equals(_selectedEventId, StringComparison.OrdinalIgnoreCase) ? ">" : " ";
                builder.AppendLine($"{selected} [{item.Id}] {item.Title}");
                builder.AppendLine($"   분류 {item.Kind} / 하위계통 {item.Subsystem} / 긴급 {Band(item.Urgency)} / 영향 {Band(item.Severity)} / 상태 {item.Status}");
                builder.AppendLine($"   배정: {(item.AssignedPersonnel.Count == 0 ? "미배정" : string.Join(", ", item.AssignedPersonnel))}");
                builder.AppendLine($"   검토 신호: {ReviewSignal(item)}");
            }

            var selectedItem = FindSelectedCase(state);
            if (selectedItem != null)
            {
                builder.AppendLine();
                builder.AppendLine("선택 업무 상세");
                builder.AppendLine($"마지막 공개 출처: {LastVisibleSource(state, selectedItem.Id)}");
                builder.AppendLine($"결과 요약: {(string.IsNullOrWhiteSpace(selectedItem.ResultSummary) ? "아직 보고되지 않음" : selectedItem.ResultSummary)}");
                builder.AppendLine($"처리 제안: {ActionAdvice(selectedItem)}");
            }

            return builder.ToString();
        }

        private static string BuildAssignmentText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("배정 계획");
            builder.AppendLine(state.MorningPlan.Confirmed ? "현재 계획은 확정되었습니다." : "확정 전 계획입니다. 필요한 경우 디버그 명령 ADJUST <id> <person>으로 조정할 수 있습니다.");
            builder.AppendLine();

            foreach (var entry in state.MorningPlan.Entries)
            {
                var item = state.Queue.FirstOrDefault(e => e.Id.Equals(entry.EventId, StringComparison.OrdinalIgnoreCase));
                builder.AppendLine($"[{entry.EventId}] {item?.Title ?? "unknown"}");
                builder.AppendLine($"   담당: {(entry.PlannedPersonnel.Count == 0 ? "미정" : string.Join(", ", entry.PlannedPersonnel))}");
                builder.AppendLine($"   근거: {entry.Reason}");
                builder.AppendLine($"   경고: {AssignmentWarning(state, item, entry)}");
            }

            return builder.ToString();
        }

        private static string BuildReportsText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("보고서함");
            builder.AppendLine();

            if (state.Reports.Count == 0)
            {
                builder.AppendLine("미검토 보고서가 없습니다. 보고가 없다는 사실도 상태 신호로 취급하십시오.");
                return builder.ToString();
            }

            foreach (var report in state.Reports.OrderByDescending(r => r.Day))
            {
                builder.AppendLine($"Day {report.Day:D2} / {report.Title}");
                builder.AppendLine($"   생성: {report.Generator}");
                builder.AppendLine($"   본문: {Trim(report.Body.Replace(Environment.NewLine, " "), 180)}");
            }

            return builder.ToString();
        }

        private static string BuildFinanceText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("재무 / 승인 반응");
            builder.AppendLine("정확한 금액 대신 승인 지연, 증빙 요구, 반려 패턴만 표시합니다.");
            builder.AppendLine();
            builder.AppendLine($"현재 응답: {FinanceResponseBand(state)}");
            builder.AppendLine($"재검토 가능: {state.AuditBudget}건");
            builder.AppendLine($"재배정 가능: {state.RedirectBudget}건");
            builder.AppendLine();
            builder.AppendLine("최근 신호");
            builder.AppendLine($"- 업무 과부하: {Band(state.Overload)}");
            builder.AppendLine($"- 조직 위험: {Band(state.GlobalLatentRisk)}");
            builder.AppendLine($"- 인력 공백: {Band(state.TalentShortage)}");
            return builder.ToString();
        }

        private static string BuildEventsText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("조직 이벤트");
            builder.AppendLine();

            var events = state.Queue
                .Where(e => e.Status != CaseStatus.Closed && IsOrganizationEvent(e))
                .OrderByDescending(e => e.Urgency + e.Severity)
                .ToList();

            if (events.Count == 0)
            {
                builder.AppendLine("현재 전면 처리 중인 조직 이벤트가 없습니다. 공지가 늦게 도착할 수 있습니다.");
                return builder.ToString();
            }

            foreach (var item in events)
            {
                builder.AppendLine($"[{item.Id}] {item.Title}");
                builder.AppendLine($"   발신 분류 {item.Subsystem} / 대응 위험 {Band(item.Severity)} / 무시 가능성 낮음");
            }

            return builder.ToString();
        }

        private string BuildLabText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("재미검증 실험실");
            builder.AppendLine("목표는 기존 데이터를 예쁘게 열람하는 것이 아니라, 상태를 조작해 판단 루프가 재미있는지 빠르게 검증하는 것입니다.");
            builder.AppendLine();
            builder.AppendLine("조작 버튼");
            builder.AppendLine("- 업무 주입: 불일치가 큰 실험 업무를 큐에 추가합니다.");
            builder.AppendLine("- 재무 압박: 검토/재배정 여유를 줄이고 조직 위험 신호를 올립니다.");
            builder.AppendLine("- 인력 과부하: 선택 인력의 피로, 스트레스, 이탈 위험을 올립니다.");
            builder.AppendLine("- 보고 생성: 현재 상태를 불완전한 보고서로 생성합니다.");
            builder.AppendLine();
            builder.AppendLine("현재 실험 상태");
            builder.AppendLine($"- 선택 업무: {(string.IsNullOrWhiteSpace(_selectedEventId) ? "없음" : _selectedEventId)}");
            builder.AppendLine($"- 선택 인력: {(string.IsNullOrWhiteSpace(_selectedPersonId) ? "없음" : _selectedPersonId)}");
            builder.AppendLine($"- 진행 업무: {state.Queue.Count(e => e.Status != CaseStatus.Closed)}건");
            builder.AppendLine($"- 재무 반응: {FinanceResponseBand(state)}");
            builder.AppendLine($"- 조직 위험 신호: {Band(state.GlobalLatentRisk)}");
            builder.AppendLine();
            builder.AppendLine("검증 질문");
            builder.AppendLine("- 업무를 갑자기 늘렸을 때, 플레이어가 먼저 봐야 할 신호가 명확한가?");
            builder.AppendLine("- 재무 압박을 올렸을 때, 정답 숫자 없이도 위험을 읽을 수 있는가?");
            builder.AppendLine("- 인력 과부하가 업무 배정 판단을 바꾸게 만드는가?");
            builder.AppendLine("- 보고서가 생겼을 때, 승인/보류/추가 검토의 선택이 갈리는가?");
            return builder.ToString();
        }

        private static string BuildRecordsText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("기록 보관소");
            builder.AppendLine("공개 로그만 표시합니다. TruthFrame 원문은 일반 UI에 노출하지 않습니다.");
            builder.AppendLine();

            foreach (var log in state.Logs.Where(l => l.VisibleAtSec <= state.TotalElapsedSec).OrderByDescending(l => l.VisibleAtSec).Take(18))
            {
                var flags = new List<string>();
                if (log.Omitted) flags.Add("누락 가능");
                if (log.Distorted) flags.Add("왜곡 가능");
                if (log.Delayed) flags.Add("지연");
                builder.AppendLine($"[{log.EventId}] {log.SourceType.ToUpperInvariant()} {(flags.Count == 0 ? "" : "(" + string.Join(", ", flags) + ")")}");
                builder.AppendLine($"   {Trim(log.Text, 150)}");
            }

            return builder.ToString();
        }

        private string BuildRightText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("오늘의 신호");
            builder.AppendLine($"재무: {FinanceResponseBand(state)}");
            builder.AppendLine($"조직: {Band(state.GlobalLatentRisk)}");
            builder.AppendLine($"인력 공백: {Band(state.TalentShortage)}");
            builder.AppendLine();

            builder.AppendLine("중요 알림");
            foreach (var item in state.Queue.Where(e => e.Status != CaseStatus.Closed).OrderByDescending(NotificationScore).Take(6))
            {
                builder.AppendLine($"{Priority(item)} [{item.Id}] {Trim(item.Title, 42)}");
                builder.AppendLine($"   {ReviewSignal(item)}");
            }

            builder.AppendLine();
            builder.AppendLine("선택 대상");
            builder.AppendLine($"업무: {(string.IsNullOrWhiteSpace(_selectedEventId) ? "없음" : _selectedEventId)}");
            builder.AppendLine($"인력: {(string.IsNullOrWhiteSpace(_selectedPersonId) ? "없음" : _selectedPersonId)}");

            return builder.ToString();
        }

        private static string BuildPhaseText(GameState state)
        {
            var phase = state.Slot switch
            {
                Slot.Morning => state.MorningPlan.Confirmed ? "검토 > 배정 > 승인 > 실행 중 > 보고 > 정산" : "검토 > 배정 대기 > 승인 > 실행 > 보고 > 정산",
                Slot.Noon => "검토 > 배정 > 승인 > 실행 중 > 보고 > 정산",
                Slot.Evening => "검토 > 배정 > 승인 > 실행 > 보고 대기 > 정산",
                _ => "검토 > 배정 > 승인 > 실행 > 보고 > 정산"
            };
            return phase;
        }

        private static string BuildCycleGuideText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("목표");
            builder.AppendLine("이미 있는 데이터를 감상하는 것이 아니라, 데이터를 일부러 조작하면서 판단 루프가 재미있는지 확인합니다.");
            builder.AppendLine();
            builder.AppendLine("한 사이클 권장 진행");
            builder.AppendLine("1. 실험실에서 상태를 흔듭니다: 업무 주입, 재무 압박, 인력 과부하, 보고 생성 중 1~2개를 누릅니다.");
            builder.AppendLine("2. 대시보드로 돌아와 오늘의 신호를 읽습니다. 무엇이 급한지, 무엇이 불명확한지 먼저 판단합니다.");
            builder.AppendLine("3. 업무 보드에서 선택 업무를 바꿔가며 요약과 검토를 누릅니다. 요약만 믿어도 되는지 확인합니다.");
            builder.AppendLine("4. 재생 인력과 배정 계획을 봅니다. 과부하 인력에게 계속 맡길지, 다른 사람에게 넘길지 결정합니다.");
            builder.AppendLine("5. 계획 확정 또는 보류/승인을 선택합니다. 정답을 맞히는 느낌보다 선택 근거가 생기는지가 중요합니다.");
            builder.AppendLine("6. 보고서함에서 결과 문서를 봅니다. 보고서상 완료와 실제 위험 신호가 어긋나는지 확인합니다.");
            builder.AppendLine("7. 다음 날을 눌러 상태 변화를 보고, 같은 조작을 다르게 반복합니다.");
            builder.AppendLine();
            builder.AppendLine("재미검증 체크");
            builder.AppendLine("- 언제 개입할지 고민이 생겼는가?");
            builder.AppendLine("- 무엇에 개입해야 할지 화면 신호만으로 후보가 좁혀졌는가?");
            builder.AppendLine("- 왜 그 선택을 했는지 말로 설명할 수 있었는가?");
            builder.AppendLine("- 조작 후 결과가 너무 뻔하지 않고, 다음 실험을 해보고 싶어졌는가?");
            builder.AppendLine();
            builder.AppendLine("키");
            builder.AppendLine("- ` : 이 설명서 열기/닫기");
            builder.AppendLine("- 다음 업무 / 다음 인력: 실험 대상 전환");
            builder.AppendLine("- 업무 주입 / 재무 압박 / 인력 과부하 / 보고 생성: 테스트 데이터 조작");
            return builder.ToString();
        }

        private string BuildCompactHistory()
        {
            return string.Join("  |  ", _history.Skip(Math.Max(0, _history.Count - 6)));
        }

        private EventCase FindSelectedCase(GameState state)
        {
            return state.Queue.FirstOrDefault(e => e.Id.Equals(_selectedEventId, StringComparison.OrdinalIgnoreCase));
        }

        private string ResolveCommandTemplate(string command)
        {
            if (command.IndexOf("{id}", StringComparison.Ordinal) < 0)
            {
                return command;
            }

            var selected = !string.IsNullOrWhiteSpace(_selectedEventId)
                ? _selectedEventId
                : _session.State.OpenEventId;
            if (string.IsNullOrWhiteSpace(selected))
            {
                selected = _session.State.Queue.FirstOrDefault(e => e.Status != CaseStatus.Closed)?.Id ?? "E-108";
            }

            return command.Replace("{id}", selected);
        }

        private static RectTransform CreatePanel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string title)
        {
            var panel = CreateRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(0.09f, 0.1f, 0.11f, 0.96f);

            var titleText = CreateText(panel, name + "Title", title, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(12f, -34f);
            titleText.rectTransform.offsetMax = new Vector2(-12f, -6f);
            titleText.color = new Color(0.93f, 0.9f, 0.82f, 1f);
            return panel;
        }

        private static Text CreateScrollText(RectTransform parent, string name, out ScrollRect scroll)
        {
            var scrollGo = new GameObject(name + "Scroll");
            scrollGo.transform.SetParent(parent, false);
            var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(10f, 10f);
            scrollRectTransform.offsetMax = new Vector2(-10f, -42f);
            var image = scrollGo.AddComponent<Image>();
            image.color = new Color(0.04f, 0.045f, 0.05f, 0.7f);
            scroll = scrollGo.AddComponent<ScrollRect>();

            var viewport = CreateRect(scrollRectTransform, name + "Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect(viewport, name + "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(content.sizeDelta.x, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 8, 8);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = CreateText(content, name, string.Empty, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            return text;
        }

        private static InputField CreateInput(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.02f, 0.025f, 0.03f, 1f);
            var input = rect.gameObject.AddComponent<InputField>();

            var text = CreateText(rect, name + "Text", string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8f, 2f);
            text.rectTransform.offsetMax = new Vector2(-8f, -2f);

            var placeholder = CreateText(rect, name + "Placeholder", "debug command", 13, FontStyle.Italic, TextAnchor.MiddleLeft);
            placeholder.color = new Color(0.55f, 0.58f, 0.6f, 1f);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(8f, 2f);
            placeholder.rectTransform.offsetMax = new Vector2(-8f, -2f);

            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.25f, 0.28f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(rect, name + "Label", label, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(4f, 2f);
            text.rectTransform.offsetMax = new Vector2(-4f, -2f);
            return button;
        }

        private static Text CreateText(RectTransform parent, string name, string value, int fontSize, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = new Color(0.88f, 0.9f, 0.9f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopLeftFixed(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        }

        private static void SetTopRightFixed(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        }

        private static void SetTopStretch(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - Mathf.Abs(height));
            rect.offsetMax = new Vector2(-right, -top);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Abs(height));
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.06f, 0.065f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
        }

        private static Font ResolveFont()
        {
            return Resources.GetBuiltinResource<Font>(LegacyRuntimeFontName);
        }

        private static bool IsOrganizationEvent(EventCase item)
        {
            return item.Kind.Equals("complaint", StringComparison.OrdinalIgnoreCase)
                || item.Kind.Equals("audit", StringComparison.OrdinalIgnoreCase)
                || item.Subsystem.Equals("HR", StringComparison.OrdinalIgnoreCase)
                || item.Subsystem.Equals("RECORDS", StringComparison.OrdinalIgnoreCase);
        }

        private static int NotificationScore(EventCase item)
        {
            return item.Urgency + item.Severity + (item.MismatchScore * 12) + (item.AssignedPersonnel.Count == 0 ? 10 : 0);
        }

        private static string Priority(EventCase item)
        {
            var score = NotificationScore(item);
            if (score >= 150) return "P0";
            if (score >= 110) return "P1";
            if (score >= 75) return "P2";
            return "P3";
        }

        private static string Band(int value)
        {
            if (value >= 85) return "치명";
            if (value >= 65) return "높음";
            if (value >= 35) return "검토 필요";
            if (value >= 15) return "낮음";
            return "정상";
        }

        private static string FinanceResponseBand(GameState state)
        {
            if (state.AuditBudget <= 0 && state.GlobalLatentRisk >= 80) return "승인 재검토";
            if (state.RedirectBudget <= 0) return "설명 필요";
            if (state.Overload >= 70) return "확인 필요";
            return "정상 응답";
        }

        private static string PersonStateBand(Personnel person)
        {
            if (person.HasLeft) return "이탈";
            if (person.RetentionRisk >= 75) return "면담 요청";
            if (person.Fatigue >= 70) return "회복 필요";
            if (person.Stagnation >= 70) return "정체";
            return "가용";
        }

        private static string LoadBand(Personnel staff)
        {
            if (staff.LoadAssigned > staff.OptHigh) return "과부하";
            if (staff.LoadAssigned < staff.OptLow) return "여유";
            return "적정";
        }

        private static string TrustBand(Personnel staff)
        {
            if (staff.TrustToManager < 40) return "응답 둔화";
            if (staff.Fatigue > 65) return "피로 누적";
            if (staff.Stagnation > 65) return "무력화";
            return "안정";
        }

        private static string ReviewSignal(EventCase item)
        {
            if (item.AssignedPersonnel.Count == 0) return "미배정";
            if (item.MismatchScore >= 3) return "문서 불일치";
            if (!item.ReportReviewed && item.Status == CaseStatus.Closed) return "보고서 검토 필요";
            if (item.SummaryRead && item.ApprovedFromSummaryOnly) return "요약만 확인됨";
            if (item.Severity >= 75 && item.AssignedPersonnel.Count == 1) return "단독 처리 위험";
            return "정상 범위";
        }

        private static string ActionAdvice(EventCase item)
        {
            if (item.Status == CaseStatus.Closed) return "보고서를 검토하고 다음 업무로 이동하십시오.";
            if (item.AssignedPersonnel.Count == 0) return "담당 인력을 먼저 지정하십시오.";
            if (item.MismatchScore >= 2) return "요약 외 로그를 확인한 뒤 승인하십시오.";
            if (item.Severity >= 70) return "단독 배정 여부를 재검토하십시오.";
            return "요약 확인 후 승인 또는 보류를 선택하십시오.";
        }

        private static string AssignmentWarning(GameState state, EventCase item, WorkPlanEntry entry)
        {
            if (item == null) return "업무 원본 없음";
            if (entry.PlannedPersonnel.Count == 0) return "미배정";
            if (item.Severity >= 70 && entry.PlannedPersonnel.Count == 1) return "고영향 업무 단독 배정";
            var overload = entry.PlannedPersonnel
                .Select(id => state.Staff.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                .Where(p => p != null)
                .Any(p => p.LoadAssigned > p.OptHigh);
            return overload ? "담당자 과부하 가능" : "경고 없음";
        }

        private static string LastVisibleSource(GameState state, string eventId)
        {
            return state.Logs
                .Where(l => l.EventId == eventId && l.VisibleAtSec <= state.TotalElapsedSec)
                .OrderByDescending(l => l.VisibleAtSec)
                .Select(l => l.SourceType.ToUpperInvariant())
                .FirstOrDefault() ?? "NONE";
        }

        private static string SanitizeName(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return builder.ToString().Trim('_');
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= max ? value : value.Substring(0, Math.Max(0, max - 1)) + ".";
        }

        private void TrimHistory()
        {
            const int maxLines = 220;
            if (_history.Count <= maxLines)
            {
                return;
            }

            _history.RemoveRange(0, _history.Count - maxLines);
        }
    }
}
