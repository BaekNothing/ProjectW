using System;
using System.Collections.Generic;
using System.Linq;
using ProjectW.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ProjectW.MilestonePrototype
{
    public sealed class ProjectWUiController : MonoBehaviour
    {
        private const string CampaignSaveKey = "projectw.campaign.v1";
        private const string UiThemeKey = "projectw.ui.theme.v1";
        private const string ReduceMotionKey = "projectw.ui.reduce-motion.v1";
        private const int TimelineDays = 21;
        private const float DayWidth = 64f;
        private const int NotificationHistoryLimit = 50;
        private const int VisibleBannerLimit = 3;
        private const float NotificationBannerSeconds = 6f;

        private UIDocument document;
        private PanelSettings panelSettings;
        private VisualElement root;
        private VisualElement appRoot;
        private VisualElement titleScreen;
        private VisualElement desktopScreen;
        private VisualElement pageContent;
        private VisualElement modalHost;
        private Label pageEyebrow;
        private Label pageTitle;
        private Label dayLabel;
        private Label resourceLabel;
        private Label commandHint;
        private Button nextDayButton;
        private Button skipEventButton;
        private Button planNavigationButton;
        private Button notificationButton;
        private VisualElement notificationPanel;
        private VisualElement notificationToastHost;
        private MilestoneSimulation game;
        private NotificationCenter notifications;
        private readonly List<string> knownMailIds = new List<string>();
        private string lastKnownSystemLog;
        private float nextNotificationExpiry = float.MaxValue;
        private float motionReleaseAt = float.MaxValue;
        private float notificationPanelHideAt = float.MaxValue;
        private IPatchDiagnostics diagnostics;
        private string patchVersion;
        private string activePage = "schedule";
        private string activeStakeholderRequestId;
        private bool darkTheme;
        private bool reduceMotion;
        private bool notificationPanelClosing;
        private bool skipToNextEvent;
        private bool workerDetailPopupVisible;

        public void Initialize(string version, IPatchDiagnostics patchDiagnostics)
        {
            patchVersion = version;
            diagnostics = patchDiagnostics;
            game = new MilestoneSimulation();
            if (ProjectWSaveStore.TryLoadCampaign(CampaignSaveKey, out CampaignSnapshot snapshot))
                game.Restore(snapshot);
            notifications = new NotificationCenter(NotificationHistoryLimit, NotificationBannerSeconds);
            ResetNotificationTracking();

            darkTheme = PlayerPrefs.GetInt(UiThemeKey, 0) == 1;
            reduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
            BuildDocument();
            BindShell();
            BuildNotificationUi();
            ShowTitle();
        }

        private void BuildDocument()
        {
            VisualTreeAsset template = Resources.Load<VisualTreeAsset>("StudioUI/ProjectWRoot");
            if (template == null)
                throw new InvalidOperationException("StudioUI/ProjectWRoot.uxml is required.");

            panelSettings = Resources.Load<PanelSettings>("StudioUI/ProjectWPanelSettings");
            if (panelSettings == null || panelSettings.themeStyleSheet == null)
                throw new InvalidOperationException(
                    "StudioUI/ProjectWPanelSettings.asset with a runtime theme is required.");

            var documentHost = new GameObject("StudioUI Document");
            documentHost.transform.SetParent(transform, false);
            documentHost.SetActive(false);
            document = documentHost.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = 100;
            document.visualTreeAsset = template;
            documentHost.SetActive(true);
            root = document.rootVisualElement;
            root.style.flexGrow = 1f;
            appRoot = root.Q<VisualElement>("app-root");
            if (appRoot == null) throw new InvalidOperationException("UI element 'app-root' is required.");
            appRoot.style.flexGrow = 1f;
            if (appRoot.parent != null) appRoot.parent.style.flexGrow = 1f;
            ApplyTheme();
            ApplyMotionPreference();
        }

        private void BindShell()
        {
            titleScreen = Require<VisualElement>("title-screen");
            desktopScreen = Require<VisualElement>("desktop-screen");
            pageContent = Require<VisualElement>("page-content");
            modalHost = Require<VisualElement>("modal-host");
            pageEyebrow = Require<Label>("page-eyebrow");
            pageTitle = Require<Label>("page-title");
            dayLabel = Require<Label>("day-label");
            resourceLabel = Require<Label>("resource-label");
            commandHint = Require<Label>("command-hint");
            nextDayButton = Require<Button>("next-day-button");
            skipEventButton = ActionButton("□ 다음 이벤트까지", "secondary");
            nextDayButton.parent.Add(skipEventButton);

            Button startButton = Require<Button>("start-button");
            startButton.clicked += EnterDesktop;
            Require<Button>("title-options-button").clicked += ShowTitleOptions;
            Require<Button>("theme-button").clicked += ToggleTheme;
            nextDayButton.clicked += AdvanceDay;
            root.RegisterCallback<PointerDownEvent>(DismissWorkerDetailOnPointerDown);
            AddPlanNavigation();
            skipEventButton.clicked += () =>
            {
                skipToNextEvent = !skipToNextEvent;
                RefreshChrome();
            };

            foreach (Button button in root.Query<Button>(className: "nav-button").ToList())
            {
                if (button == planNavigationButton) continue;
                string destination = button.name.Replace("nav-", string.Empty);
                button.clicked += () => Navigate(destination);
            }
            planNavigationButton.clicked += () => Navigate("plan");
        }

        private void AddPlanNavigation()
        {
            Button overview = Require<Button>("nav-overview");
            Button schedule = Require<Button>("nav-schedule");
            Button crew = Require<Button>("nav-crew");
            Button mail = Require<Button>("nav-mail");
            Button codex = Require<Button>("nav-codex");
            Button settings = Require<Button>("nav-settings");
            VisualElement sidebar = mail.parent;
            Label label = sidebar.Q<Label>(className: "sidebar-label");
            VisualElement spacer = sidebar.Q<VisualElement>(className: "sidebar-spacer");
            VisualElement note = sidebar.Q<VisualElement>(className: "sidebar-note");
            planNavigationButton = new Button { text = "계획" };
            planNavigationButton.AddToClassList("nav-button");

            sidebar.Clear();
            if (label != null) sidebar.Add(label);
            sidebar.Add(overview);
            sidebar.Add(schedule);
            sidebar.Add(crew);
            sidebar.Add(mail);
            sidebar.Add(planNavigationButton);
            sidebar.Add(codex);
            if (spacer != null) sidebar.Add(spacer);
            sidebar.Add(settings);
            if (note != null) sidebar.Add(note);
        }

        private void BuildNotificationUi()
        {
            notificationButton = ActionButton("알림 0", "secondary");
            notificationButton.clicked += ToggleNotificationHistory;
            Require<Button>("theme-button").parent.Add(notificationButton);

            notificationToastHost = Box("notification-toast-host");
            notificationToastHost.style.position = Position.Absolute;
            notificationToastHost.style.right = 20f;
            notificationToastHost.style.bottom = 88f;
            notificationToastHost.style.width = 360f;
            desktopScreen.Add(notificationToastHost);

            notificationPanel = Box("section-card");
            notificationPanel.AddToClassList("notification-panel");
            notificationPanel.style.position = Position.Absolute;
            notificationPanel.style.right = 18f;
            notificationPanel.style.top = 64f;
            notificationPanel.style.width = 420f;
            notificationPanel.style.maxHeight = 520f;
            notificationPanel.style.display = DisplayStyle.None;
            desktopScreen.Add(notificationPanel);
            RefreshNotificationUi(Time.unscaledTime);
        }

        private void ToggleNotificationHistory()
        {
            bool show = notificationPanel.style.display == DisplayStyle.None || notificationPanelClosing;
            if (show)
            {
                notificationPanelClosing = false;
                notificationPanelHideAt = float.MaxValue;
                notificationPanel.RemoveFromClassList("motion-panel-exit");
                notificationPanel.style.display = DisplayStyle.Flex;
                RebuildNotificationHistory();
                QueueEntrance(notificationPanel, "motion-panel-enter");
                return;
            }

            if (reduceMotion)
            {
                notificationPanel.style.display = DisplayStyle.None;
                return;
            }

            notificationPanelClosing = true;
            notificationPanel.AddToClassList("motion-panel-exit");
            notificationPanelHideAt = Time.unscaledTime + 0.11f;
        }

        private void RebuildNotificationHistory()
        {
            notificationPanel.Clear();
            var header = Box("page-toolbar");
            header.Add(Text($"알림 이력 · {notifications.Count}", "section-title"));
            Button clear = ActionButton("모두 지우기", "secondary");
            clear.SetEnabled(notifications.Count > 0);
            clear.clicked += () =>
            {
                ShowNotificationClearConfirmation();
            };
            header.Add(clear);
            notificationPanel.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            if (notifications.Count == 0) scroll.Add(Empty("아직 도착한 알림이 없습니다."));
            for (int index = 0; index < notifications.Count; index++)
            {
                NotificationEntry entry = notifications.Get(index);
                var row = Box("codex-row");
                row.Add(Badge(entry.Category, entry.Category == "메일" ? "accent" : "neutral"));
                row.Add(Text(entry.Title, "summary-title"));
                row.Add(Text($"D{entry.Day:00} · {entry.Message}", "muted"));
                scroll.Add(row);
            }
            notificationPanel.Add(scroll);
        }

        private void ResetNotificationTracking()
        {
            knownMailIds.Clear();
            foreach (MailEvent mail in game.Mail)
                if (mail.ArrivalDay <= game.Day) knownMailIds.Add(mail.Id);
            lastKnownSystemLog = game.SystemLog.Count == 0
                ? null
                : game.SystemLog[game.SystemLog.Count - 1];
        }

        private void CaptureNotifications()
        {
            float now = Time.unscaledTime;
            foreach (MailEvent mail in game.Mail)
            {
                if (mail.ArrivalDay > game.Day || knownMailIds.Contains(mail.Id)) continue;
                knownMailIds.Add(mail.Id);
                notifications.Add("메일", mail.Subject, mail.Body, game.Day, now);
            }

            int firstNewLog = 0;
            if (!string.IsNullOrEmpty(lastKnownSystemLog))
            {
                int lastKnownIndex = game.SystemLog.LastIndexOf(lastKnownSystemLog);
                firstNewLog = lastKnownIndex >= 0 ? lastKnownIndex + 1 : Math.Max(0, game.SystemLog.Count - 1);
            }
            for (int index = firstNewLog; index < game.SystemLog.Count; index++)
                notifications.Add("활동", "운영 업데이트", game.SystemLog[index], game.Day, now);
            lastKnownSystemLog = game.SystemLog.Count == 0
                ? null
                : game.SystemLog[game.SystemLog.Count - 1];

            RefreshNotificationUi(now);
            QueueEntrance(notificationToastHost, "motion-toast-enter");
        }

        private void RefreshNotificationUi(float now)
        {
            notificationButton.text = $"알림 {notifications.Count}";
            notificationToastHost.Clear();
            int count = notifications.ActiveBannerCount(now, VisibleBannerLimit);
            for (int index = 0; index < count; index++)
            {
                NotificationEntry entry = notifications.ActiveBannerAt(now, index);
                var toast = Box("section-card");
                toast.Add(Badge(entry.Category, entry.Category == "메일" ? "accent" : "neutral"));
                toast.Add(Text(entry.Title, "summary-title"));
                toast.Add(Text(entry.Message, "muted"));
                notificationToastHost.Add(toast);
            }
            nextNotificationExpiry = notifications.NextExpiry(now);
            if (notificationPanel.style.display == DisplayStyle.Flex) RebuildNotificationHistory();
        }

        private T Require<T>(string elementName) where T : VisualElement
        {
            T element = root.Q<T>(elementName);
            if (element == null) throw new InvalidOperationException($"UI element '{elementName}' is required.");
            return element;
        }

        private void ShowTitle()
        {
            titleScreen.style.display = DisplayStyle.Flex;
            desktopScreen.style.display = DisplayStyle.None;
            modalHost.Clear();
        }

        private void EnterDesktop()
        {
            titleScreen.style.display = DisplayStyle.None;
            desktopScreen.style.display = DisplayStyle.Flex;
            Navigate(activePage);
        }

        private void Navigate(string page)
        {
            workerDetailPopupVisible = false;
            modalHost.Clear();
            activePage = page;
            foreach (Button button in root.Query<Button>(className: "nav-button").ToList())
                button.EnableInClassList("is-selected",
                    button == planNavigationButton ? page == "plan" : button.name == "nav-" + page);

            pageContent.Clear();
            switch (page)
            {
                case "overview": BuildOverview(); break;
                case "crew": BuildCrew(); break;
                case "mail": BuildMail(); break;
                case "plan": BuildPlan(); break;
                case "codex": BuildCodex(); break;
                case "settings": BuildSettings(); break;
                default: BuildSchedule(); break;
            }
            RefreshChrome();
            ShowIncidentModalIfNeeded();
        }

        private void RefreshChrome()
        {
            dayLabel.text = $"DAY {game.Day:00} · {MilestoneSimulation.WeekdayName(game.CurrentWeekday)}요일";
            resourceLabel.text = $"RESOURCES  {game.Resources:N0}";
            nextDayButton.SetEnabled(game.CanAdvanceDay);
            nextDayButton.text = !game.CanAdvanceDay
                ? "결정 필요"
                : skipToNextEvent
                    ? $"D{game.NextEventDay:00}까지  →"
                    : "다음날로  →";
            commandHint.text = !game.CanAdvanceDay
                ? "미결정 돌발 임무를 처리하면 하루를 진행할 수 있습니다."
                : skipToNextEvent
                    ? $"{game.NextEventSummary}이 있는 D{game.NextEventDay:00}까지 진행합니다."
                    : "오늘의 결정을 저장하고 다음 날로 진행합니다.";
            skipEventButton.text = skipToNextEvent
                ? $"☑ 다음 이벤트까지 · {game.NextEventSummary} D{game.NextEventDay:00}"
                : "□ 다음 이벤트까지";
            skipEventButton.EnableInClassList("is-selected", skipToNextEvent);
        }

        private void BuildOverview()
        {
            SetPageHeader("OPERATIONS", "운영 개요");
            var grid = Box("metric-grid");
            grid.Add(Metric("진행 중", game.Groups.Count(group => group.State == WorkState.InProgress).ToString(), "WORK"));
            grid.Add(Metric("인력 공백", game.StaffingGapCount().ToString(), "STAFFING"));
            grid.Add(Metric("다음 급여", $"D{game.NextPayrollDay:00}", "PAYROLL"));
            grid.Add(Metric("읽지 않은 메일", game.Mail.Count(mail => !mail.Read && mail.ArrivalDay <= game.Day).ToString(), "INBOX"));
            grid.Add(Metric("요청 부합도", game.CampaignFitScore.ToString(), "FIT SCORE"));
            pageContent.Add(grid);

            var section = Section("오늘의 운영 상태", "조직 계획에 따라 빈 작업이 자동 배정됩니다.");
            foreach (CrewMember crew in game.Crew)
            {
                var row = Box("summary-row");
                row.Add(Text(crew.Name, "summary-title"));
                row.Add(Badge(crew.Condition, ConditionIntent(crew)));
                row.Add(Text($"피로 {crew.Fatigue} · 신뢰 {crew.Trust} · {crew.Specialty}", "muted"));
                section.Add(row);
            }
            pageContent.Add(section);
        }

        private void BuildSchedule()
        {
            SetPageHeader("TASK SYSTEM", "일정과 우선순위");
            var toolbar = Box("page-toolbar");
            toolbar.Add(Text("21일 계획 · 오늘 기준 과거 3일 표시", "muted"));
            Button today = ActionButton("오늘로", "secondary");
            today.clicked += () => Navigate("schedule");
            toolbar.Add(today);
            pageContent.Add(toolbar);

            var gantt = Box("gantt-frame");
            List<WorkGroup> works = game.Groups.Where(game.IsWorkVisible)
                .Where(group => group.State != WorkState.Complete || RecentlyCompleted(group))
                .OrderBy(group => group.Priority).ThenBy(group => group.Id).ToList();
            TaskScheduleEstimate[] estimates = game.BuildPrioritySchedule();
            int startDay = ProjectWUiProjection.TimelineStartDay(game.Day, game.PlanningHorizonDay);

            var header = Box("gantt-header-row");
            header.Add(Text("WORK / TASK", "gantt-fixed-header"));
            var headerScroll = new ScrollView(ScrollViewMode.Horizontal);
            headerScroll.AddToClassList("gantt-horizontal");
            var dates = Box("gantt-date-row");
            dates.style.width = TimelineDays * DayWidth;
            for (int offset = 0; offset < TimelineDays; offset++)
            {
                int day = startDay + offset;
                Label cell = Text($"D{day:00}\n{MilestoneSimulation.WeekdayName((Weekday)((day - 1) % 7))}", "gantt-date-cell");
                cell.style.width = DayWidth;
                cell.EnableInClassList("is-today", day == game.Day);
                cell.EnableInClassList("is-weekend", MilestoneSimulation.IsWeekendDay(day));
                dates.Add(cell);
            }
            headerScroll.Add(dates);
            header.Add(headerScroll);
            gantt.Add(header);

            var body = Box("gantt-body");
            var labels = Box("gantt-labels");
            var timelineScroll = new ScrollView(ScrollViewMode.Horizontal);
            timelineScroll.AddToClassList("gantt-horizontal");
            var timeline = Box("gantt-timeline");
            timeline.style.width = TimelineDays * DayWidth;

            foreach (WorkGroup work in works)
            {
                AddWorkRow(labels, timeline, work, startDay);
                foreach (WorkTask task in game.Tasks.Where(task => task.GroupId == work.Id))
                    AddTaskRow(labels, timeline, task, estimates.FirstOrDefault(item => item.TaskId == task.Id), startDay);
            }
            timelineScroll.Add(timeline);
            body.Add(labels);
            body.Add(timelineScroll);
            gantt.Add(body);
            pageContent.Add(gantt);
        }

        private bool RecentlyCompleted(WorkGroup work)
        {
            int latest = game.Tasks.Where(task => task.GroupId == work.Id && task.Required)
                .Select(task => task.CompletedDay).DefaultIfEmpty(0).Max();
            return latest == 0 || game.Day - latest < 14;
        }

        private void AddWorkRow(VisualElement labels, VisualElement timeline, WorkGroup work, int startDay)
        {
            var label = Box("gantt-work-label");
            var title = Box("gantt-label-title-row");
            title.Add(Text(work.Name, "gantt-work-title"));
            title.Add(Badge(ProjectWUiProjection.WorkDeadlineLabel(work, game.Day), DeadlineIntent(work)));
            label.Add(title);
            label.Add(Text($"{work.State} · 우선순위 {work.Priority + 1}", "caption"));
            labels.Add(label);

            var row = TimelineRow(startDay, true);
            AddDeadlineMarker(row, work.SoftDeadline, startDay, "soft");
            AddDeadlineMarker(row, work.HardDeadline, startDay, "hard");
            timeline.Add(row);
        }

        private void AddTaskRow(VisualElement labels, VisualElement timeline, WorkTask task,
            TaskScheduleEstimate estimate, int startDay)
        {
            var label = Box("gantt-task-label");
            label.Add(Text(task.Name, "gantt-task-title"));
            string owner = task.AssignedCharacter >= 0 && task.AssignedCharacter < game.Crew.Count
                ? game.Crew[task.AssignedCharacter].Name
                : task.ScheduledWorker >= 0 && task.ScheduledWorker < game.Crew.Count
                    ? $"{game.Crew[task.ScheduledWorker].Name} · 예약"
                    : AutomaticOwner(task);
            label.Add(Text($"{StateLabel(task.State)} · {owner}", "caption"));
            label.EnableInClassList("is-complete", task.State == TaskState.Complete);
            labels.Add(label);

            var row = TimelineRow(startDay, false);
            GanttBarProjection projection = ProjectWUiProjection.Project(task, estimate, game.Day);
            float left = (projection.StartDay - startDay) * DayWidth + 5f;
            float width = (projection.EndDayExclusive - projection.StartDay) * DayWidth - 10f;
            var bar = Box("gantt-bar");
            bar.style.left = left;
            bar.style.width = Math.Max(20f, width);
            bar.EnableInClassList("is-complete", task.State == TaskState.Complete);
            bar.EnableInClassList("is-active", task.State == TaskState.Active);
            var progress = Box("gantt-bar-progress");
            progress.style.width = Length.Percent(projection.Progress01 * 100f);
            bar.Add(progress);
            bar.Add(Text($"{projection.Progress01 * 100f:0}%", "gantt-bar-text"));
            row.Add(bar);
            timeline.Add(row);
        }

        private VisualElement TimelineRow(int startDay, bool workRow)
        {
            var row = Box(workRow ? "timeline-work-row" : "timeline-task-row");
            row.style.width = TimelineDays * DayWidth;
            for (int offset = 0; offset < TimelineDays; offset++)
            {
                int day = startDay + offset;
                var cell = Box("timeline-cell");
                cell.style.left = offset * DayWidth;
                cell.style.width = DayWidth;
                cell.EnableInClassList("is-today", day == game.Day);
                cell.EnableInClassList("is-weekend", MilestoneSimulation.IsWeekendDay(day));
                row.Add(cell);
            }
            return row;
        }

        private void AddDeadlineMarker(VisualElement row, int day, int startDay, string kind)
        {
            int offset = day - startDay;
            if (offset < 0 || offset >= TimelineDays) return;
            var marker = Box("deadline-marker");
            marker.AddToClassList("is-" + kind);
            marker.style.left = offset * DayWidth + DayWidth * .5f;
            row.Add(marker);
        }

        private string AutomaticOwner(WorkTask task)
        {
            int worker = game.PreviewAutomaticAssignee(task.Id, out string source);
            return worker >= 0 && worker < game.Crew.Count
                ? $"{game.Crew[worker].Name} · {source}"
                : "미배정";
        }

        private void BuildCrew()
        {
            SetPageHeader("STAFFING", "조직과 승무원");
            var grid = Box("card-grid");
            for (int index = 0; index < game.Crew.Count; index++)
            {
                int crewIndex = index;
                CrewMember crew = game.Crew[index];
                CrewOperatingPolicy policy = game.CrewPolicy(index);
                var card = Section(crew.Name, $"{crew.Personality} · {crew.Specialty}");
                card.Add(Badge(crew.Condition, ConditionIntent(crew)));
                card.Add(Progress("피로", crew.Fatigue));
                card.Add(Progress("불만", crew.Dissatisfaction));
                card.Add(Progress("신뢰", crew.Trust));
                card.Add(Text($"전문 영역  {crew.Specialty}  ·  운영 모드 {policy.Mode}", "body"));
                card.Add(Text(CrewStatsText(crew), "caption"));
                var actions = Box("button-row");
                Button mode = ActionButton("모드 변경", "secondary");
                mode.clicked += () => { CycleMode(crewIndex); SaveAndRefresh("crew"); };
                Button rest = ActionButton(crew.RestScheduled ? "휴식 예약됨" : "휴식 예약", "secondary");
                rest.clicked += () => { game.Rest(crewIndex); SaveAndRefresh("crew"); };
                actions.Add(mode);
                actions.Add(rest);
                card.Add(actions);
                grid.Add(card);
            }
            pageContent.Add(grid);
        }

        private void CycleMode(int crewIndex)
        {
            CrewOperatingMode current = game.CrewPolicy(crewIndex).Mode;
            game.SetCrewOperatingMode(crewIndex,
                (CrewOperatingMode)(((int)current + 1) % Enum.GetValues(typeof(CrewOperatingMode)).Length));
        }

        private void BuildMail()
        {
            SetPageHeader("REQUEST DESK", "메일");
            if (!string.IsNullOrEmpty(activeStakeholderRequestId))
            {
                StakeholderRequestState active = game.StakeholderState(activeStakeholderRequestId);
                if (active != null)
                {
                    Button back = ActionButton("← 수신함으로", "secondary");
                    back.clicked += () =>
                    {
                        activeStakeholderRequestId = null;
                        Navigate("mail");
                    };
                    pageContent.Add(back);
                    BuildStakeholderMailView(active);
                    return;
                }
                activeStakeholderRequestId = null;
            }

            var list = Section("수신함", "메일에서는 요청·제약사항·기한을 확인합니다. 실행 설계는 계획하기에서 시작합니다.");
            foreach (MailEvent mail in game.Mail.Where(item => item.ArrivalDay <= game.Day)
                         .OrderBy(item => item.Read).ThenByDescending(item => item.ArrivalDay))
            {
                var row = Box("mail-row");
                row.Add(Badge(mail.Resolved ? "완료" : mail.IsCritical ? "중요" : mail.Read ? "읽음" : "NEW",
                    mail.IsCritical && !mail.Resolved ? "error" : mail.Read ? "neutral" : "accent"));
                var copy = Box("mail-copy");
                copy.Add(Text(mail.Subject, "summary-title"));
                copy.Add(Text($"D{mail.ArrivalDay:00} · {mail.Body}", "muted"));
                if (!string.IsNullOrEmpty(mail.StakeholderRequestId) &&
                    !mail.Id.StartsWith("stakeholder-result-"))
                {
                    Button open = ActionButton(
                        game.StakeholderState(mail.StakeholderRequestId) == null ? "요청 확인" : "요청 계속 보기",
                        "primary");
                    string mailId = mail.Id;
                    string requestId = mail.StakeholderRequestId;
                    open.clicked += () =>
                    {
                        game.OpenStakeholderRequest(mailId);
                        activeStakeholderRequestId = requestId;
                        SaveAndRefresh("mail");
                    };
                    copy.Add(open);
                }
                row.Add(copy);
                list.Add(row);
            }
            if (game.Mail.Count == 0) list.Add(Empty("도착한 메일이 없습니다."));
            pageContent.Add(list);
        }

        private void BuildStakeholderMailView(StakeholderRequestState request)
        {
            StakeholderRequestDefinition definition = game.StakeholderDefinition(request.Id);
            WorkGroup work = game.Groups.FirstOrDefault(group => group.Id == request.WorkId);
            if (definition == null || work == null) return;

            string status = request.FitScore >= 0
                ? $"납품 완료 · {request.FitScore}점"
                : request.PlanFailed ? "요청 실패"
                : request.PlanConfirmed ? "실행 중"
                : request.PendingPlanReviewDay > 0 ? $"계획 검토 중 · D{request.PendingPlanReviewDay:00} 답변"
                : request.PlanningStarted ? "계획 중" : "요청 검토";
            var board = Section(definition.Stakeholder, $"{definition.PlannedWeeks}주 요청 · {status}");
            board.Add(Text("요청", "caption"));
            board.Add(Text(definition.Brief, "body"));
            var metrics = Box("metric-grid");
            metrics.Add(Metric("명확도", $"{request.Clarity}%", "CLARITY"));
            metrics.Add(Metric("남은 일정", $"{Math.Max(0, work.HardDeadline - game.Day + 1)}일", "DEADLINE"));
            metrics.Add(Metric("소프트 마감", $"D{work.SoftDeadline:00}", "TARGET"));
            metrics.Add(Metric("누적 점수", game.CampaignFitScore.ToString(), "SCORE"));
            board.Add(metrics);

            var guidance = Box("summary-row");
            guidance.Add(Text("현재 확인된 요구", "summary-title"));
            int revealed = 0;
            foreach (StakeholderGuidanceDefinition item in definition.Guidance)
            {
                if (item.MinimumClarity > request.Clarity) continue;
                guidance.Add(Text("• " + item.Text, "muted"));
                revealed++;
            }
            if (revealed < definition.Guidance.Length)
                guidance.Add(Text($"추가 정보 {definition.Guidance.Length - revealed}건 미확인", "caption"));
            board.Add(guidance);

            if (!request.PlanConfirmed && !request.PlanFailed && request.FitScore < 0)
            {
                var clarification = Box("button-row");
                if (!string.IsNullOrEmpty(request.PendingClarificationId))
                    clarification.Add(Badge($"D{request.PendingClarificationDay:00} 교감 예정", "accent"));
                if (request.PendingPlanReviewDay > 0)
                    clarification.Add(Badge($"D{request.PendingPlanReviewDay:00} 계획 검토 중", "accent"));
                foreach (StakeholderClarificationDefinition option in definition.Clarifications)
                {
                    StakeholderClarificationDefinition selected = option;
                    Button action = ActionButton(
                        $"{option.Name} 요청 · {option.LeadDays}일 / 명확도 +{option.ClarityGain}",
                        "secondary");
                    action.SetEnabled(string.IsNullOrEmpty(request.PendingClarificationId) &&
                                      request.PendingPlanReviewDay <= 0);
                    action.clicked += () =>
                    {
                        game.RequestStakeholderClarification(request.Id, selected.Id);
                        SaveAndRefresh("mail");
                    };
                    clarification.Add(action);
                }
                board.Add(clarification);
            }

            Button plan = ActionButton(request.PlanningStarted || request.FitScore >= 0
                ? "계획으로  →"
                : "계획하기  →", "primary");
            plan.clicked += () =>
            {
                if (!request.PlanningStarted && request.FitScore < 0)
                    game.StartStakeholderPlanning(request.Id);
                activeStakeholderRequestId = request.Id;
                SaveAndRefresh("plan");
            };
            board.Add(plan);
            board.Add(Text("작업 블록과 담당자는 별도의 계획 탭에서 정합니다.", "caption"));
            pageContent.Add(board);
        }

        private void BuildPlan()
        {
            SetPageHeader("DELIVERY PLAN", "작업 계획");
            if (!string.IsNullOrEmpty(activeStakeholderRequestId))
            {
                StakeholderRequestState active = game.StakeholderState(activeStakeholderRequestId);
                if (ProjectWUiProjection.IsStakeholderPlanVisible(active))
                {
                    var toolbar = Box("page-toolbar");
                    Button back = ActionButton("← 계획 목록", "secondary");
                    back.clicked += () =>
                    {
                        activeStakeholderRequestId = null;
                        Navigate("plan");
                    };
                    toolbar.Add(back);
                    Button mail = ActionButton("메일로  →", "secondary");
                    mail.clicked += () => Navigate("mail");
                    toolbar.Add(mail);
                    pageContent.Add(toolbar);
                    BuildStakeholderPlanBoard(active);
                    return;
                }
                activeStakeholderRequestId = null;
            }

            var list = Section("요청별 계획", "메일에서 계획하기를 시작한 요청을 관리합니다.");
            int count = 0;
            foreach (StakeholderRequestState request in game.StakeholderRequests
                         .Where(ProjectWUiProjection.IsStakeholderPlanVisible)
                         .OrderByDescending(item => item.FitScore < 0).ThenBy(item => item.Id))
            {
                StakeholderRequestDefinition definition = game.StakeholderDefinition(request.Id);
                WorkGroup work = game.Groups.FirstOrDefault(group => group.Id == request.WorkId);
                if (definition == null || work == null) continue;
                count++;
                var row = Box("mail-row");
                row.Add(Badge(request.FitScore >= 0 ? "완료" : request.PlanFailed ? "실패" :
                        request.PlanConfirmed ? "승인·실행" : request.PendingPlanReviewDay > 0 ? "검토 대기" :
                        request.PlanRejectionCount > 0 ? "수정 요청" : "계획 중",
                    request.FitScore >= 0 ? "success" : request.PlanFailed ? "error" :
                    request.PlanConfirmed ? "accent" : "neutral"));
                var copy = Box("mail-copy");
                copy.Add(Text(definition.Stakeholder, "summary-title"));
                copy.Add(Text($"명확도 {request.Clarity}% · 마감 D{work.HardDeadline:00}", "muted"));
                Button open = ActionButton("계획 열기", "primary");
                string requestId = request.Id;
                open.clicked += () =>
                {
                    activeStakeholderRequestId = requestId;
                    Navigate("plan");
                };
                copy.Add(open);
                row.Add(copy);
                list.Add(row);
            }
            if (count == 0)
                list.Add(Empty("진행 중인 요청 계획이 없습니다. 메일에서 요청을 열고 계획하기를 선택하세요."));
            pageContent.Add(list);
        }

        private void BuildStakeholderPlanBoard(StakeholderRequestState request)
        {
            StakeholderRequestDefinition definition = game.StakeholderDefinition(request.Id);
            WorkGroup work = game.Groups.FirstOrDefault(group => group.Id == request.WorkId);
            if (definition == null || work == null) return;
            if (game.UsesCatalogPlanning(request.Id))
            {
                BuildCatalogPlanBoard(request, definition, work);
                return;
            }

            string status = request.FitScore >= 0
                ? $"납품 완료 · {request.FitScore}점"
                : request.PlanFailed ? "요청 실패"
                : request.PlanConfirmed ? "승인·실행 중"
                : request.PendingPlanReviewDay > 0 ? $"검토 대기 · D{request.PendingPlanReviewDay:00} 답변"
                : request.PlanRejectionCount > 0 ? "반려 후 수정 중" : "작성 중";
            var board = Section(definition.Stakeholder + " 계획", status);
            var metrics = Box("metric-grid");
            metrics.Add(Metric("명확도", $"{request.Clarity}%", "CLARITY"));
            metrics.Add(Metric("남은 일정", $"{Math.Max(0, work.HardDeadline - game.Day + 1)}일", "DEADLINE"));
            metrics.Add(Metric("소프트 마감", $"D{work.SoftDeadline:00}", "TARGET"));
            int projectedCompletion = game.StakeholderPlanProjectedCompletionDay(request.Id);
            metrics.Add(Metric("예상 완료", projectedCompletion > 0 ? $"D{projectedCompletion:00}" : "미정", "FORECAST"));
            board.Add(metrics);

            var guidance = Box("summary-row");
            guidance.Add(Text("계획에 반영할 확인 정보", "summary-title"));
            int revealed = 0;
            foreach (StakeholderGuidanceDefinition item in definition.Guidance)
            {
                if (item.MinimumClarity > request.Clarity) continue;
                guidance.Add(Text("• " + item.Text, "muted"));
                revealed++;
            }
            if (revealed < definition.Guidance.Length)
                guidance.Add(Text($"추가 정보 {definition.Guidance.Length - revealed}건 미확인", "caption"));
            board.Add(guidance);

            if (request.PlanSubmissionCount > 0)
            {
                var review = Box("summary-row");
                bool pendingReview = request.PendingPlanReviewDay > 0;
                review.Add(Badge(request.PlanConfirmed ? "승인" : request.PlanFailed ? "실패" :
                        pendingReview ? "검토 중" : "반려",
                    request.PlanConfirmed ? "success" : request.PlanFailed ? "error" :
                    pendingReview ? "accent" : "warning"));
                review.Add(Text($"검토 {request.PlanSubmissionCount}회 · 반려 " +
                                $"{request.PlanRejectionCount}/{definition.MaxPlanRejections}", "summary-title"));
                review.Add(Text(request.LastPlanReviewMessage, "muted"));
                board.Add(review);
            }

            if (!request.PlanConfirmed && !request.PlanFailed && request.FitScore < 0)
                board.Add(BuildAvailableTaskBlocks(request, definition));

            foreach (WorkTask task in game.Tasks.Where(item => item.StakeholderRequestId == request.Id)
                         .OrderByDescending(item => item.ContributionWeight))
                board.Add(BuildStakeholderTaskCard(request, task));

            if (!request.PlanConfirmed && !request.PlanFailed && request.FitScore < 0)
            {
                Button submit = ActionButton(request.PendingPlanReviewDay > 0
                    ? $"검토 대기 · D{request.PendingPlanReviewDay:00}"
                    : "계획서 보내기", "primary");
                submit.SetEnabled(game.CanSubmitStakeholderPlan(request.Id));
                submit.clicked += () =>
                {
                    game.SubmitStakeholderPlan(request.Id);
                    SaveAndRefresh("plan");
                };
                board.Add(submit);
                board.Add(Text(request.PendingPlanReviewDay > 0
                        ? "검토 결과가 도착할 때까지 계획과 담당자를 변경하거나 다시 제출할 수 없습니다."
                        : $"모든 슬롯과 담당자를 채우고 추가 정보 대기가 없어야 보낼 수 있습니다. " +
                          $"답변에는 1일 이상이 걸리며, 예상 완료가 소프트 마감 D{work.SoftDeadline:00} 이내이고 내용 이견이 없어야 승인됩니다.",
                    "caption"));
            }
            pageContent.Add(board);
        }

        private void BuildCatalogPlanBoard(
            StakeholderRequestState request, StakeholderRequestDefinition definition, WorkGroup work)
        {
            string status = request.FitScore >= 0 ? $"납품 완료 · {request.FitScore}점" :
                request.PlanFailed ? "요청 실패" : request.PlanConfirmed ? "승인·실행 중" :
                request.PendingPlanReviewDay > 0 ? $"검토 대기 · D{request.PendingPlanReviewDay:00}" :
                request.PlanRejectionCount > 0 ? "반려 후 수정 중" : "범위 설계 중";
            var board = Section(string.IsNullOrEmpty(definition.ProjectName)
                    ? definition.Stakeholder + " 프로젝트" : definition.ProjectName, status);
            List<WorkTask> tasks = game.Tasks.Where(item =>
                    item.StakeholderRequestId == request.Id).ToList();
            int selectedTasks = tasks.Count(item => item.ScopeIncluded);
            int selectedOptions = tasks.Sum(item => item.SelectedOptionIds?.Length ?? 0);
            int totalOptions = definition.Tasks.Sum(item => item.Options?.Length ?? 0);
            int projectedCompletion = game.StakeholderPlanProjectedCompletionDay(request.Id);

            var summary = Box("metric-grid");
            summary.Add(Metric("포함 작업", $"{selectedTasks}/{tasks.Count}", "SCOPE"));
            summary.Add(Metric("확장 사양", $"{selectedOptions}/{totalOptions}", "OPTIONS"));
            summary.Add(Metric("총 공수", $"{game.StakeholderPlanTotalWork(request.Id):0.#}인일", "EFFORT"));
            summary.Add(Metric("예상 완료", projectedCompletion > 0
                ? $"D{projectedCompletion:00}" : "미정", $"마감 D{work.SoftDeadline:00}"));
            board.Add(summary);

            var schedule = Box("summary-row");
            schedule.Add(Badge(status, request.PlanConfirmed ? "success" :
                request.PendingPlanReviewDay > 0 ? "accent" : "neutral"));
            if (projectedCompletion > work.SoftDeadline)
                schedule.Add(Badge($"{projectedCompletion - work.SoftDeadline}일 초과", "error"));
            else if (projectedCompletion > 0)
                schedule.Add(Badge($"{work.SoftDeadline - projectedCompletion}일 여유", "success"));
            schedule.Add(Text($"명확도 {request.Clarity}% · 미확인 정보는 구현 위험과 검수 반려로 이어질 수 있습니다.",
                "caption"));
            board.Add(schedule);

            foreach (WorkTask task in tasks)
                board.Add(BuildCatalogTaskCard(request, task));

            var risks = Box("catalog-risk-panel");
            risks.Add(Text("현재 계획 위험", "summary-title"));
            bool hasVisibleRisk = false;
            if (selectedTasks < definition.MinimumSelectedTasks)
            {
                risks.Add(Text($"• 최소 {definition.MinimumSelectedTasks}개 작업을 범위에 포함해야 합니다.", "error-text"));
                hasVisibleRisk = true;
            }
            foreach (WorkTask task in tasks.Where(item => item.ScopeIncluded))
            {
                if (task.PlannedCharacter < 0 && !request.PlanConfirmed)
                {
                    risks.Add(Text("• " + task.Name + ": 담당자 미배정", "warning-text"));
                    hasVisibleRisk = true;
                }
                StakeholderTaskDefinition taskDefinition = game.StakeholderTargetTaskDefinition(task);
                if (taskDefinition?.Options == null) continue;
                foreach (StakeholderOptionDefinition option in taskDefinition.Options)
                {
                    if (!game.IsStakeholderOptionSelected(task, option.Id) || option.ConflictingOptionIds == null) continue;
                    foreach (string conflictId in option.ConflictingOptionIds)
                        if (game.IsStakeholderOptionSelected(task, conflictId))
                        {
                            risks.Add(Text($"• {task.Name}: {option.Name} 확장 사양 충돌", "error-text"));
                            hasVisibleRisk = true;
                        }
                }
            }
            if (!hasVisibleRisk)
                risks.Add(Text("현재 드러난 치명적 위험이 없습니다. 미확인 가정은 별도로 확인하세요.", "muted"));
            board.Add(risks);

            if (!request.PlanConfirmed && !request.PlanFailed && request.FitScore < 0)
            {
                Button submit = ActionButton(request.PendingPlanReviewDay > 0
                    ? $"검토 대기 · D{request.PendingPlanReviewDay:00}" : "고객에게 계획서 보내기", "primary");
                submit.SetEnabled(game.CanSubmitStakeholderPlan(request.Id));
                submit.clicked += () =>
                {
                    game.SubmitStakeholderPlan(request.Id);
                    SaveAndRefresh("plan");
                };
                board.Add(submit);
                board.Add(Text(request.PendingPlanReviewDay > 0
                    ? "검토 중에는 범위, 확장 사양, 담당자를 변경할 수 없습니다."
                    : "포함한 모든 작업에 담당자를 지정하고 진행 중인 확인 요청이 없어야 제출할 수 있습니다.", "caption"));
            }
            pageContent.Add(board);
        }

        private VisualElement BuildCatalogTaskCard(
            StakeholderRequestState request, WorkTask task)
        {
            StakeholderTaskDefinition definition = game.StakeholderTargetTaskDefinition(task);
            bool editable = !request.PlanConfirmed && !request.PlanFailed &&
                            request.PendingPlanReviewDay <= 0 && request.FitScore < 0;
            var card = Box("task-slot-card");
            int worker = StakeholderWorkerIndex(request, task);
            bool conflict = CatalogTaskHasConflict(task, definition);

            var heading = Box("summary-row");
            Button toggle = ActionButton(task.ScopeIncluded ? "ON" : "OFF",
                task.ScopeIncluded ? "primary" : "secondary");
            toggle.SetEnabled(editable);
            toggle.clicked += () =>
            {
                game.SetStakeholderTaskIncluded(task.Id, !task.ScopeIncluded);
                SaveAndRefresh("plan");
            };
            heading.Add(toggle);
            heading.Add(Text(definition?.Name ?? task.Name, "summary-title"));
            heading.Add(Badge(task.ScopeIncluded ? "범위 포함" : "범위 제외",
                task.ScopeIncluded ? "success" : "neutral"));
            Button detail = ActionButton("상세", "secondary");
            detail.clicked += () => ShowCatalogTaskDetail(task, null);
            heading.Add(detail);
            card.Add(heading);
            card.Add(Text(task.ScopeIncluded
                ? $"기본 {definition?.RequiredWork ?? 0f:0.#}인일 · 확장 +{Math.Max(0f, task.RequiredWork - (definition?.RequiredWork ?? 0f)):0.#}인일"
                : "공수·담당자·일정에서 제외", task.ScopeIncluded ? "body" : "muted"));
            if (definition?.RequiredInformation != null)
                card.Add(Text($"필요 정보 {definition.RequiredInformation.Length}건", "caption"));

            if (definition?.Options != null)
                foreach (StakeholderOptionDefinition option in definition.Options)
                {
                    bool selected = game.IsStakeholderOptionSelected(task, option.Id);
                    var optionRow = Box("button-row");
                    Button optionToggle = ActionButton((selected ? "☑ " : "☐ ") + option.Name,
                        selected ? "primary" : "secondary");
                    optionToggle.SetEnabled(editable && task.ScopeIncluded);
                    optionToggle.clicked += () =>
                    {
                        game.SetStakeholderOption(task.Id, option.Id, !selected);
                        SaveAndRefresh("plan");
                    };
                    optionRow.Add(optionToggle);
                    Button optionDetail = ActionButton($"+{option.AdditionalWork:0.#}일 · 상세", "secondary");
                    optionDetail.clicked += () => ShowCatalogTaskDetail(task, option);
                    optionRow.Add(optionDetail);
                    card.Add(optionRow);
                }

            var assignment = Box("assignment-row");
            Button picker = ActionButton(worker >= 0 && worker < game.Crew.Count
                ? "담당 · " + game.Crew[worker].Name : "담당자 선택", worker >= 0 ? "primary" : "secondary");
            picker.AddToClassList("assignment-selector");
            picker.SetEnabled(editable && task.ScopeIncluded);
            picker.clicked += () => ShowWorkerPicker(task);
            assignment.Add(picker);
            if (worker >= 0 && worker < game.Crew.Count)
            {
                int crewIndex = worker;
                Button workerDetail = ActionButton(WorkerAssignmentSummary(task, crewIndex), "secondary");
                workerDetail.AddToClassList("assignment-summary");
                workerDetail.clicked += () => ShowWorkerDetailPopup(task.Id, crewIndex);
                assignment.Add(workerDetail);
            }
            else if (task.ScopeIncluded)
            {
                Label missing = Text("미배정 · 제출 전 담당자가 필요합니다.", "warning-text");
                missing.AddToClassList("assignment-summary");
                assignment.Add(missing);
            }
            card.Add(assignment);
            if (conflict)
                card.Add(Text("선택한 확장 사양이 서로 충돌합니다.", "error-text"));
            return card;
        }

        private bool CatalogTaskHasConflict(
            WorkTask task, StakeholderTaskDefinition definition)
        {
            if (task == null || definition?.Options == null) return false;
            foreach (StakeholderOptionDefinition option in definition.Options)
            {
                if (!game.IsStakeholderOptionSelected(task, option.Id) ||
                    option.ConflictingOptionIds == null) continue;
                foreach (string conflictId in option.ConflictingOptionIds)
                    if (game.IsStakeholderOptionSelected(task, conflictId)) return true;
            }
            return false;
        }

        private void ShowCatalogTaskDetail(WorkTask task, StakeholderOptionDefinition option)
        {
            StakeholderTaskDefinition definition = game.StakeholderTargetTaskDefinition(task);
            if (definition == null) return;
            modalHost.Clear();
            var overlay = Box("modal-overlay");
            var dialog = Section(option == null ? definition.Name : option.Name,
                option == null ? definition.Description : option.Description);
            dialog.Add(Text(option == null
                ? $"기본 공수 {definition.RequiredWork:0.#}인일 · 주요 능력 {RoleLabel(definition.RequiredRole)}"
                : $"추가 공수 {option.AdditionalWork:0.#}인일", "body"));
            string[] information = option == null ? definition.RequiredInformation : option.RequiredInformation;
            if (information != null && information.Length > 0)
            {
                dialog.Add(Text("필요 정보", "summary-title"));
                foreach (string item in information) dialog.Add(Text("• " + item, "muted"));
            }
            if (option?.RiskNotes != null && option.RiskNotes.Length > 0)
            {
                dialog.Add(Text("새로운 위험", "summary-title"));
                foreach (string risk in option.RiskNotes) dialog.Add(Text("• " + risk, "warning-text"));
            }
            Button close = ActionButton("닫기", "secondary");
            close.clicked += () => modalHost.Clear();
            dialog.Add(close);
            ShowModal(overlay, dialog);
        }

        private VisualElement BuildStakeholderTaskCard(StakeholderRequestState request, WorkTask task)
        {
            StakeholderTaskDefinition selectedDefinition = game.StakeholderTaskDefinition(task);
            StakeholderInterpretationDefinition selectedBlock = game.StakeholderTaskBlock(task);
            StakeholderTaskDefinition target = game.StakeholderTargetTaskDefinition(task);
            bool insightRevealed = request.Clarity >= (task.ContributionWeight >= 60 ? 40 : 70);
            var card = Box("task-slot-card");
            card.Add(Badge(insightRevealed ? $"중요도 {task.ContributionWeight}%" : "중요도 미확인",
                insightRevealed && task.ContributionWeight >= 60 ? "accent" : "neutral"));
            card.Add(Text(StakeholderSlotLabel(task), "summary-title"));
            card.Add(Text(insightRevealed && target != null
                    ? $"요청 힌트 · {target.Name}"
                    : "필요한 작업과 우선순위가 아직 명확하지 않습니다.",
                insightRevealed ? "body" : "muted"));

            Button blockPicker = ActionButton(selectedBlock == null
                ? "작업 블록 선택  ▼"
                : selectedBlock.Name + "  ▼", selectedBlock == null ? "secondary" : "primary");
            blockPicker.SetEnabled(!request.PlanConfirmed && !request.PlanFailed &&
                                   request.PendingPlanReviewDay <= 0 && request.FitScore < 0);
            blockPicker.clicked += () => ShowTaskBlockPicker(request, task);
            card.Add(blockPicker);

            if (selectedDefinition != null && selectedBlock != null)
                card.Add(Text($"{selectedBlock.Description} · {RoleLabel(selectedDefinition.RequiredRole)} · " +
                              $"{selectedDefinition.RequiredWork:0.#}일",
                    "muted"));
            else
                card.Add(Text("이 슬롯은 아직 비어 있습니다.", "caption"));

            card.Add(Text("담당자", "caption"));
            var assignment = Box("assignment-row");
            int assignedWorker = StakeholderWorkerIndex(request, task);
            string crewName = assignedWorker >= 0 && assignedWorker < game.Crew.Count
                ? game.Crew[assignedWorker].Name : "담당자 선택";
            Button workerPicker = ActionButton(crewName + "  ▼",
                assignedWorker >= 0 ? "primary" : "secondary");
            workerPicker.AddToClassList("assignment-selector");
            workerPicker.SetEnabled(request.FitScore < 0 && !request.PlanFailed &&
                                    request.PendingPlanReviewDay <= 0 && selectedBlock != null);
            workerPicker.clicked += () => ShowWorkerPicker(task);
            assignment.Add(workerPicker);
            if (assignedWorker >= 0 && assignedWorker < game.Crew.Count)
            {
                int crewIndex = assignedWorker;
                Button info = ActionButton(WorkerAssignmentSummary(task, crewIndex), "secondary");
                info.AddToClassList("assignment-summary");
                info.clicked += () => ShowWorkerDetailPopup(task.Id, crewIndex);
                assignment.Add(info);
            }
            else
            {
                Label emptyInfo = Text(selectedBlock == null
                    ? "작업 블록을 먼저 선택하세요."
                    : "담당자를 고르면 예상 효율이 표시됩니다.", "muted");
                emptyInfo.AddToClassList("assignment-summary");
                assignment.Add(emptyInfo);
            }
            card.Add(assignment);
            if (request.FitScore >= 0)
                card.Add(Text($"부합도 {game.StakeholderTaskFit(task.Id)}점 × {task.ContributionWeight}%", "body"));
            return card;
        }

        private VisualElement BuildAvailableTaskBlocks(
            StakeholderRequestState request, StakeholderRequestDefinition definition)
        {
            var pool = Section("사용 가능한 작업 블록",
                "아래 블록 중 원하는 것을 각 빈 슬롯에 배치합니다. 한 블록은 한 번만 사용할 수 있습니다.");
            foreach (StakeholderTaskDefinition taskBlock in definition.Tasks)
                foreach (StakeholderInterpretationDefinition interpretation in taskBlock.Interpretations)
                {
                    bool used = game.IsStakeholderTaskBlockInUse(
                        request.Id, taskBlock.Id, interpretation.Id, null);
                    var row = Box("task-block-row");
                    row.Add(Badge(used ? "배치됨" : "사용 가능", used ? "success" : "neutral"));
                    row.Add(Text(interpretation.Name, "summary-title"));
                    row.Add(Text(interpretation.Description, "muted"));
                    row.Add(Text($"{RoleLabel(taskBlock.RequiredRole)} · {taskBlock.RequiredWork:0.#}일", "caption"));
                    pool.Add(row);
                }
            return pool;
        }

        private void ShowTaskBlockPicker(StakeholderRequestState request, WorkTask task)
        {
            StakeholderRequestDefinition definition = game.StakeholderDefinition(request.Id);
            if (definition == null || request.PlanConfirmed || request.PlanFailed ||
                request.PendingPlanReviewDay > 0) return;
            modalHost.Clear();
            var overlay = Box("modal-overlay");
            var dialog = Section(StakeholderSlotLabel(task) + " · 작업 블록 선택",
                "선택한 블록의 작업량과 요구 역량이 이 슬롯에 적용됩니다.");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 520f;
            if (!string.IsNullOrEmpty(task.SelectedTaskBlockId))
            {
                Button clear = ActionButton("슬롯 비우기", "danger");
                clear.clicked += () =>
                {
                    game.SelectStakeholderTaskBlock(task.Id, string.Empty, string.Empty);
                    SaveAndRefresh("plan");
                };
                scroll.Add(clear);
            }
            foreach (StakeholderTaskDefinition taskBlock in definition.Tasks)
                foreach (StakeholderInterpretationDefinition interpretation in taskBlock.Interpretations)
                {
                    StakeholderTaskDefinition selectedDefinition = taskBlock;
                    StakeholderInterpretationDefinition selectedInterpretation = interpretation;
                    bool current = task.SelectedTaskBlockId == taskBlock.Id &&
                                   task.SelectedInterpretationId == interpretation.Id;
                    bool used = game.IsStakeholderTaskBlockInUse(
                        request.Id, taskBlock.Id, interpretation.Id, task.Id);
                    Button choice = ActionButton(
                        $"{interpretation.Name} · {RoleLabel(taskBlock.RequiredRole)} · {taskBlock.RequiredWork:0.#}일",
                        current ? "primary" : "secondary");
                    choice.SetEnabled(!used);
                    choice.clicked += () =>
                    {
                        game.SelectStakeholderTaskBlock(task.Id,
                            selectedDefinition.Id, selectedInterpretation.Id);
                        SaveAndRefresh("plan");
                    };
                    scroll.Add(choice);
                    scroll.Add(Text(interpretation.Description, "caption"));
                }
            dialog.Add(scroll);
            Button close = ActionButton("닫기", "secondary");
            close.clicked += () => modalHost.Clear();
            dialog.Add(close);
            ShowModal(overlay, dialog);
        }

        private void ShowWorkerPicker(WorkTask task)
        {
            if (task == null || string.IsNullOrEmpty(task.SelectedTaskBlockId)) return;
            StakeholderRequestState request = game.StakeholderState(task.StakeholderRequestId);
            if (request == null || request.PlanFailed || request.PendingPlanReviewDay > 0) return;
            int assignedWorker = StakeholderWorkerIndex(request, task);
            modalHost.Clear();
            var overlay = Box("modal-overlay");
            var dialog = Section(task.Name + " · 담당자 선택",
                "스탯이나 전문 영역이 맞지 않아도 배정할 수 있지만 효율과 불만에 영향을 줍니다.");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 520f;
            if (assignedWorker >= 0)
            {
                Button clear = ActionButton("미배정", "danger");
                clear.clicked += () =>
                {
                    game.AssignStakeholderTask(task.Id, -1);
                    SaveAndRefresh("plan");
                };
                scroll.Add(clear);
            }
            for (int crewIndex = 0; crewIndex < game.Crew.Count; crewIndex++)
            {
                int selectedCrew = crewIndex;
                CrewMember crew = game.Crew[crewIndex];
                string blocked = game.StakeholderAssignmentBlockReason(task.Id, crewIndex);
                Button choice = ActionButton(
                    $"{crew.Name} · {RoleLabel(crew.Specialty)} · {WorkerAssignmentSummary(task, crewIndex)}",
                    assignedWorker == crewIndex ? "primary" : "secondary");
                choice.SetEnabled(string.IsNullOrEmpty(blocked));
                choice.clicked += () =>
                {
                    game.AssignStakeholderTask(task.Id, selectedCrew);
                    SaveAndRefresh("plan");
                };
                scroll.Add(choice);
                if (!string.IsNullOrEmpty(blocked)) scroll.Add(Text(blocked, "caption"));
            }
            dialog.Add(scroll);
            Button close = ActionButton("닫기", "secondary");
            close.clicked += () => modalHost.Clear();
            dialog.Add(close);
            ShowModal(overlay, dialog);
        }

        private string WorkerAssignmentSummary(WorkTask task, int crewIndex)
        {
            CrewMember crew = game.Crew[crewIndex];
            string grade = "요구 스탯 없음";
            if (task.RequiredCompetencies != null && task.RequiredCompetencies.Length > 0)
            {
                int competency = task.RequiredCompetencies[0];
                int actual = competency >= 0 && competency < crew.Competencies.Length
                    ? crew.Competencies[competency] : CrewMember.MinimumCompetency;
                grade = CompetencyName(competency) + " " + CrewMember.CompetencyGrade(actual) + "급";
            }
            bool parallel = game.Tasks.Any(item => item != task &&
                ((item.AssignedCharacter == crewIndex && !item.IsParallelAssignment) ||
                 item.PlannedCharacter == crewIndex) &&
                item.State != TaskState.Complete && item.State != TaskState.Failed);
            float efficiency = game.AssignmentEfficiencyMultiplier(task.Id, crewIndex);
            int dissatisfaction = game.AssignmentDissatisfactionGain(task.Id, crewIndex, parallel);
            return $"{grade} · {(crew.Specialty == task.RequiredRole ? "전문 일치" : "전문 불일치")} · " +
                   $"효율 {efficiency * 100f:0}% · 불만 +{dissatisfaction}";
        }

        private static string StakeholderSlotLabel(WorkTask task) => task.ContributionWeight >= 60
            ? "작업 슬롯 1"
            : task.ContributionWeight >= 30 ? "작업 슬롯 2" : "작업 슬롯 3";

        private static int StakeholderWorkerIndex(StakeholderRequestState request, WorkTask task) =>
            request != null && request.PlanConfirmed ? task.AssignedCharacter : task.PlannedCharacter;

        private static string RoleLabel(WorkRole role) => role == WorkRole.Tech
            ? "기술"
            : role == WorkRole.Analysis ? "분석" : role == WorkRole.Management ? "관리" : "적응";

        private void BuildCodex()
        {
            SetPageHeader("KNOWLEDGE", "코덱스");
            var list = Section("운영 지식", "현재 구현된 규칙과 발견한 정보를 확인합니다.");
            foreach (CodexEntry entry in game.Codex.Take(40))
            {
                var row = Box("codex-row");
                row.Add(Badge(entry.Category, "neutral"));
                row.Add(Text(entry.Name, "summary-title"));
                row.Add(Text(entry.Description, "muted"));
                list.Add(row);
            }
            pageContent.Add(list);
        }

        private void BuildSettings()
        {
            SetPageHeader("SYSTEM", "설정");
            var appearance = Section("화면", "명확한 계층과 접근 가능한 대비를 유지합니다.");
            Button theme = ActionButton(darkTheme ? "라이트 테마 사용" : "다크 테마 사용", "secondary");
            theme.clicked += ToggleTheme;
            appearance.Add(theme);
            Button motion = ActionButton(reduceMotion ? "기본 모션 사용" : "모션 줄이기", "secondary");
            motion.clicked += ToggleReducedMotion;
            appearance.Add(motion);
            appearance.Add(Text(reduceMotion
                ? "현재 상태 변화는 이동·확대 없이 즉시 표시됩니다."
                : "입력 반응은 70ms, 화면 전환은 최대 240ms의 짧은 모션을 사용합니다.", "caption"));
            pageContent.Add(appearance);

            var progress = Section("진행 상황", "화면과 패치 설정은 유지하고 현재 캠페인만 처음부터 다시 시작합니다.");
            Button resetProgress = ActionButton("진행상황 리셋", "danger");
            resetProgress.clicked += ShowProgressResetConfirmation;
            progress.Add(resetProgress);
            pageContent.Add(progress);

            var update = Section("업데이트", "설치된 패치는 다음 재시작부터 적용됩니다.");
            update.Add(Text($"실행 버전  {patchVersion}", "body"));
            update.Add(Text($"설치 버전  {diagnostics?.InstalledVersion ?? "none"}", "body"));
            update.Add(Text(diagnostics?.Status ?? "진단 정보 없음", "muted"));
            Button request = ActionButton(diagnostics != null && diagnostics.UpdateInProgress ? "업데이트 확인 중" : "업데이트 확인", "primary");
            request.SetEnabled(diagnostics != null && !diagnostics.UpdateInProgress);
            request.clicked += () => { diagnostics?.RequestUpdate(); Navigate("settings"); };
            update.Add(request);
            pageContent.Add(update);
        }

        private void ShowTitleOptions()
        {
            modalHost.Clear();
            var overlay = Box("modal-overlay");
            var dialog = Section("시작 옵션", "캠페인 데이터와 화면 환경을 관리합니다.");
            Button theme = ActionButton(darkTheme ? "라이트 테마" : "다크 테마", "secondary");
            theme.clicked += ToggleTheme;
            Button motion = ActionButton(reduceMotion ? "기본 모션" : "모션 줄이기", "secondary");
            motion.clicked += ToggleReducedMotion;
            Button reset = ActionButton("캠페인 초기화", "danger");
            reset.clicked += ShowResetConfirmation;
            Button close = ActionButton("닫기", "primary");
            close.clicked += () => modalHost.Clear();
            dialog.Add(theme);
            dialog.Add(motion);
            dialog.Add(reset);
            dialog.Add(close);
            ShowModal(overlay, dialog);
        }

        private void ShowNotificationClearConfirmation()
        {
            var overlay = Box("modal-overlay");
            var dialog = Section("알림 이력을 지울까요?", "현재 표시된 알림 이력이 모두 삭제됩니다.");
            Button confirm = ActionButton("알림 이력 삭제", "danger");
            confirm.clicked += () =>
            {
                notifications.Clear();
                modalHost.Clear();
                RefreshNotificationUi(Time.unscaledTime);
            };
            Button cancel = ActionButton("취소", "secondary");
            cancel.clicked += () => modalHost.Clear();
            dialog.Add(confirm);
            dialog.Add(cancel);
            ShowModal(overlay, dialog);
        }

        private void ShowResetConfirmation()
        {
            modalHost.Clear();
            var overlay = Box("modal-overlay");
            var dialog = Section("캠페인을 초기화할까요?", "진행 상황은 되돌릴 수 없습니다. 화면 설정은 유지됩니다.");
            Button confirm = ActionButton("초기화 확인", "danger");
            confirm.clicked += () =>
            {
                ProjectWSaveStore.Delete(CampaignSaveKey);
                game = new MilestoneSimulation();
                notifications.Clear();
                ResetNotificationTracking();
                RefreshNotificationUi(Time.unscaledTime);
                modalHost.Clear();
                ShowTitle();
            };
            Button cancel = ActionButton("취소", "secondary");
            cancel.clicked += () => modalHost.Clear();
            dialog.Add(confirm);
            dialog.Add(cancel);
            ShowModal(overlay, dialog);
        }

        private void ShowProgressResetConfirmation()
        {
            modalHost.Clear();
            var overlay = Box("modal-overlay");
            var dialog = Section("진행 상황을 리셋할까요?", "현재 캠페인의 날짜, 작업, 인원 상태와 메일 기록이 초기화됩니다.");
            Button confirm = ActionButton("진행상황 리셋 확인", "danger");
            confirm.clicked += () =>
            {
                ProjectWSaveStore.Delete(CampaignSaveKey);
                game = new MilestoneSimulation();
                activeStakeholderRequestId = null;
                notifications.Clear();
                ResetNotificationTracking();
                RefreshNotificationUi(Time.unscaledTime);
                modalHost.Clear();
                Navigate("overview");
            };
            Button cancel = ActionButton("취소", "secondary");
            cancel.clicked += () => modalHost.Clear();
            dialog.Add(confirm);
            dialog.Add(cancel);
            ShowModal(overlay, dialog);
        }

        private void AdvanceDay()
        {
            if (!game.CanAdvanceDay) { ShowIncidentModalIfNeeded(); return; }
            if (skipToNextEvent) game.AdvanceToNextEvent();
            else game.AdvanceDay();
            SaveAndRefresh(activePage);
        }

        private void ShowIncidentModalIfNeeded()
        {
            MailEvent offer = game.PendingIncidentOffer;
            if (offer == null) return;
            WorkGroup work = game.Groups.FirstOrDefault(group => group.Id == offer.TargetWorkId);
            if (work == null) return;

            modalHost.Clear();
            var overlay = Box("modal-overlay");
            var dialog = Section("돌발 임무 결정", work.Name);
            dialog.Add(Badge("즉시 결정", "error"));
            dialog.Add(Text($"현재 자원 {game.Resources} · 거절 비용 {work.HardPenaltyCredits}", "body"));
            dialog.Add(Text($"마감 D{work.SoftDeadline:00} / D{work.HardDeadline:00}", "muted"));
            Button accept = ActionButton("일함 / 승인", "primary");
            accept.clicked += () => { game.AcceptIncidentWork(offer.Id); modalHost.Clear(); SaveAndRefresh(activePage); };
            Button decline = ActionButton("거절", "danger");
            decline.clicked += () => { game.DeclineIncidentWork(offer.Id); modalHost.Clear(); SaveAndRefresh(activePage); };
            dialog.Add(accept);
            dialog.Add(decline);
            ShowModal(overlay, dialog);
        }

        private void ToggleTheme()
        {
            darkTheme = !darkTheme;
            PlayerPrefs.SetInt(UiThemeKey, darkTheme ? 1 : 0);
            PlayerPrefs.Save();
            ApplyTheme();
            if (desktopScreen != null && desktopScreen.style.display == DisplayStyle.Flex)
                Navigate(activePage);
        }

        private void ApplyTheme()
        {
            if (appRoot == null) return;
            appRoot.EnableInClassList("theme-dark", darkTheme);
            appRoot.EnableInClassList("theme-light", !darkTheme);
        }

        private void ToggleReducedMotion()
        {
            reduceMotion = !reduceMotion;
            PlayerPrefs.SetInt(ReduceMotionKey, reduceMotion ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMotionPreference();
            if (titleScreen.style.display == DisplayStyle.Flex) ShowTitleOptions();
            else Navigate(activePage);
        }

        private void ApplyMotionPreference()
        {
            if (appRoot == null) return;
            appRoot.EnableInClassList("reduce-motion", reduceMotion);
            if (!reduceMotion) return;
            ReleaseEntranceClasses();
            if (notificationPanelClosing)
            {
                notificationPanelClosing = false;
                notificationPanelHideAt = float.MaxValue;
                notificationPanel.style.display = DisplayStyle.None;
                notificationPanel.RemoveFromClassList("motion-panel-exit");
            }
        }

        private void SaveAndRefresh(string page)
        {
            CaptureNotifications();
            ProjectWSaveStore.SaveCampaign(CampaignSaveKey, game.CreateSnapshot());
            Navigate(page);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (workerDetailPopupVisible && IsWorkerDetailDismissInputPressed())
                DismissWorkerDetailPopup();
            if (motionReleaseAt != float.MaxValue && now >= motionReleaseAt)
                ReleaseEntranceClasses();
            if (notificationPanelClosing && now >= notificationPanelHideAt)
            {
                notificationPanelClosing = false;
                notificationPanelHideAt = float.MaxValue;
                notificationPanel.RemoveFromClassList("motion-panel-exit");
                notificationPanel.style.display = DisplayStyle.None;
            }
            if (notifications != null && now >= nextNotificationExpiry)
                RefreshNotificationUi(now);
        }

        private void DismissWorkerDetailOnPointerDown(PointerDownEvent pointerEvent)
        {
            if (workerDetailPopupVisible)
                DismissWorkerDetailPopup();
        }

        private static bool IsWorkerDetailDismissInputPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.escapeKey.isPressed || keyboard.anyKey.isPressed);
        }

        private void DismissWorkerDetailPopup()
        {
            workerDetailPopupVisible = false;
            modalHost.Clear();
        }

        private void ShowWorkerDetailPopup(string taskId, int crewIndex)
        {
            WorkTask task = game.Tasks.FirstOrDefault(item => item.Id == taskId);
            if (task == null || crewIndex < 0 || crewIndex >= game.Crew.Count)
                return;
            workerDetailPopupVisible = true;
            CrewMember crew = game.Crew[crewIndex];
            bool parallel = game.Tasks.Any(item => item != task &&
                ((item.AssignedCharacter == crewIndex && !item.IsParallelAssignment) ||
                 item.PlannedCharacter == crewIndex) &&
                item.State != TaskState.Complete && item.State != TaskState.Failed);
            float efficiency = game.AssignmentEfficiencyMultiplier(task.Id, crewIndex);
            int dissatisfaction = game.AssignmentDissatisfactionGain(
                task.Id, crewIndex, parallel);
            int activeTasks = game.Tasks.Count(item =>
                (item.AssignedCharacter == crewIndex || item.PlannedCharacter == crewIndex) &&
                item.State != TaskState.Complete && item.State != TaskState.Failed);

            modalHost.Clear();
            var overlay = Box("modal-overlay");
            var dialog = Section(crew.Name + " · 담당자 상세",
                $"{task.Name} · 전문 영역 {(crew.Specialty == task.RequiredRole ? "일치" : "불일치")}");
            if (task.RequiredCompetencies != null)
                for (int index = 0; index < task.RequiredCompetencies.Length; index++)
                {
                    int competency = task.RequiredCompetencies[index];
                    int actual = competency >= 0 && competency < crew.Competencies.Length
                        ? crew.Competencies[competency] : CrewMember.MinimumCompetency;
                    int required = task.MinimumCompetencies != null &&
                                   index < task.MinimumCompetencies.Length
                        ? task.MinimumCompetencies[index] : 4;
                    dialog.Add(Text($"{CompetencyName(competency)}  {CrewMember.CompetencyGrade(actual)}급 " +
                                    $"/ 요구 {CrewMember.CompetencyGrade(required)}급",
                        actual >= required ? "body" : "muted"));
                }
            dialog.Add(Text($"예상 효율  {efficiency * 100f:0}%", "summary-title"));
            dialog.Add(Text($"예상 불만  +{dissatisfaction}/일 · 현재 {crew.Dissatisfaction}",
                dissatisfaction > 0 ? "muted" : "body"));
            dialog.Add(Text($"현재·계획 작업 {activeTasks}개 · {(parallel ? "강제 병행 예상" : "주 작업 배정")}", "caption"));
            Button close = ActionButton("닫기", "secondary");
            close.clicked += DismissWorkerDetailPopup;
            dialog.Add(close);
            ShowModal(overlay, dialog);
        }

        private static string CrewStatsText(CrewMember crew)
        {
            return $"기지 {CrewMember.CompetencyGrade(crew.Competencies[0])} · " +
                   $"과학 {CrewMember.CompetencyGrade(crew.Competencies[1])} · " +
                   $"자원 {CrewMember.CompetencyGrade(crew.Competencies[2])} · " +
                   $"환경 {CrewMember.CompetencyGrade(crew.Competencies[3])} · " +
                   $"생명 {CrewMember.CompetencyGrade(crew.Competencies[4])} · " +
                   $"지휘 {CrewMember.CompetencyGrade(crew.Competencies[5])}";
        }

        private static string CompetencyName(int competency)
        {
            switch (competency)
            {
                case 0: return "기지 공학";
                case 1: return "과학 탐사";
                case 2: return "자원 운영";
                case 3: return "환경 적응";
                case 4: return "생명 유지";
                default: return "지휘·외교";
            }
        }

        private void SetPageHeader(string eyebrow, string title)
        {
            pageEyebrow.text = eyebrow;
            pageTitle.text = title;
        }

        private void ShowModal(VisualElement overlay, VisualElement dialog)
        {
            modalHost.Clear();
            overlay.Add(dialog);
            modalHost.Add(overlay);
            QueueEntrance(modalHost, "motion-modal-enter");
        }

        private void QueueEntrance(VisualElement element, string className)
        {
            if (reduceMotion || element == null) return;
            element.RemoveFromClassList(className);
            element.AddToClassList(className);
            motionReleaseAt = Time.unscaledTime + 0.01f;
        }

        private void ReleaseEntranceClasses()
        {
            motionReleaseAt = float.MaxValue;
            modalHost?.RemoveFromClassList("motion-modal-enter");
            notificationToastHost?.RemoveFromClassList("motion-toast-enter");
            notificationPanel?.RemoveFromClassList("motion-panel-enter");
        }

        private static VisualElement Box(string className)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            return element;
        }

        private static Label Text(string value, string className)
        {
            var label = new Label(value);
            label.AddToClassList(className);
            return label;
        }

        private static VisualElement Section(string title, string subtitle)
        {
            var section = Box("section-card");
            section.Add(Text(title, "section-title"));
            if (!string.IsNullOrEmpty(subtitle)) section.Add(Text(subtitle, "muted"));
            return section;
        }

        private static VisualElement Metric(string title, string value, string caption)
        {
            var card = Box("metric-card");
            card.Add(Text(caption, "eyebrow"));
            card.Add(Text(value, "metric-value"));
            card.Add(Text(title, "muted"));
            return card;
        }

        private static Label Badge(string value, string intent)
        {
            Label badge = Text(value, "badge");
            badge.AddToClassList("badge-" + intent);
            return badge;
        }

        private static VisualElement Progress(string label, int value)
        {
            var group = Box("progress-group");
            var header = Box("progress-header");
            header.Add(Text(label, "muted"));
            header.Add(Text(value.ToString(), "caption"));
            group.Add(header);
            var track = Box("progress-track");
            var fill = Box("progress-fill");
            fill.style.width = Length.Percent(Math.Max(0, Math.Min(100, value)));
            track.Add(fill);
            group.Add(track);
            return group;
        }

        private static Label Empty(string message) => Text(message, "empty-state");

        private static Button ActionButton(string label, string variant)
        {
            var button = new Button { text = label };
            button.AddToClassList("ds-button");
            button.AddToClassList("button-" + variant);
            return button;
        }

        private static string ConditionIntent(CrewMember crew) =>
            crew.InjuryDays > 0 || crew.Fatigue >= 80 ? "error" : crew.Fatigue >= 55 ? "warning" : "success";

        private static string DeadlineIntent(WorkGroup work) =>
            work.State == WorkState.Failed ? "error" : work.SoftDeadlineMissed ? "warning" : "neutral";

        private static string StateLabel(TaskState state)
        {
            switch (state)
            {
                case TaskState.Active: return "진행 중";
                case TaskState.Complete: return "완료";
                case TaskState.Locked: return "잠김";
                case TaskState.Failed: return "실패";
                default: return "대기";
            }
        }

        private void OnDestroy()
        {
        }
    }
}
