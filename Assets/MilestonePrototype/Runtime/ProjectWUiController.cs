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
        private const int TimelineDays = 21;
        private const float DayWidth = 64f;
        private const int NotificationHistoryLimit = 50;
        private const int VisibleBannerLimit = 3;
        private const float NotificationBannerSeconds = 6f;
        private const float WorkerPreviewHoldSeconds = .45f;

        private UIDocument document;
        private PanelSettings panelSettings;
        private VisualElement root;
        private VisualElement titleScreen;
        private VisualElement desktopScreen;
        private VisualElement pageContent;
        private VisualElement modalHost;
        private Label pageEyebrow;
        private Label pageTitle;
        private Label dayLabel;
        private Label resourceLabel;
        private Button nextDayButton;
        private Button skipEventButton;
        private Button notificationButton;
        private VisualElement notificationPanel;
        private VisualElement notificationToastHost;
        private MilestoneSimulation game;
        private NotificationCenter notifications;
        private readonly List<string> knownMailIds = new List<string>();
        private string lastKnownSystemLog;
        private float nextNotificationExpiry = float.MaxValue;
        private IPatchDiagnostics diagnostics;
        private string patchVersion;
        private string activePage = "schedule";
        private string activeStakeholderRequestId;
        private bool darkTheme;
        private bool skipToNextEvent;
        private string heldWorkerTaskId;
        private int heldWorkerCrewIndex = -1;
        private float heldWorkerStartedAt;
        private bool heldWorkerPreviewVisible;
        private string suppressedWorkerTaskId;
        private int suppressedWorkerCrewIndex = -1;

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
            VisualElement appRoot = root.Q<VisualElement>("app-root");
            if (appRoot == null) throw new InvalidOperationException("UI element 'app-root' is required.");
            appRoot.style.flexGrow = 1f;
            if (appRoot.parent != null) appRoot.parent.style.flexGrow = 1f;
            ApplyTheme();
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
            nextDayButton = Require<Button>("next-day-button");
            skipEventButton = ActionButton("□ 다음 이벤트까지", "secondary");
            nextDayButton.parent.Add(skipEventButton);

            Button startButton = Require<Button>("start-button");
            startButton.clicked += EnterDesktop;
            Require<Button>("title-options-button").clicked += ShowTitleOptions;
            Require<Button>("theme-button").clicked += ToggleTheme;
            nextDayButton.clicked += AdvanceDay;
            root.RegisterCallback<PointerDownEvent>(DismissWorkerPreviewOnPointerDown);
            root.RegisterCallback<PointerUpEvent>(DismissWorkerPreviewOnPointerUp);
            skipEventButton.clicked += () =>
            {
                skipToNextEvent = !skipToNextEvent;
                RefreshChrome();
            };

            foreach (Button button in root.Query<Button>(className: "nav-button").ToList())
            {
                string destination = button.name.Replace("nav-", string.Empty);
                button.clicked += () => Navigate(destination);
            }
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
            bool show = notificationPanel.style.display == DisplayStyle.None;
            notificationPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) RebuildNotificationHistory();
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
                notifications.Clear();
                RefreshNotificationUi(Time.unscaledTime);
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
            activePage = page;
            foreach (Button button in root.Query<Button>(className: "nav-button").ToList())
                button.EnableInClassList("is-selected", button.name == "nav-" + page);

            pageContent.Clear();
            switch (page)
            {
                case "overview": BuildOverview(); break;
                case "crew": BuildCrew(); break;
                case "mail": BuildMail(); break;
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
            SetPageHeader("REQUEST DESK", "스테이크홀더 요청과 메일");
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
                    BuildStakeholderRequestBoard(active);
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

        private void BuildStakeholderRequestBoard(StakeholderRequestState request)
        {
            StakeholderRequestDefinition definition = game.StakeholderDefinition(request.Id);
            WorkGroup work = game.Groups.FirstOrDefault(group => group.Id == request.WorkId);
            if (definition == null || work == null) return;

            string status = request.FitScore >= 0
                ? $"납품 완료 · {request.FitScore}점"
                : request.PlanConfirmed ? "실행 중"
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

            if (!request.PlanConfirmed && request.FitScore < 0)
            {
                var clarification = Box("button-row");
                if (!string.IsNullOrEmpty(request.PendingClarificationId))
                    clarification.Add(Badge($"D{request.PendingClarificationDay:00} 교감 예정", "accent"));
                foreach (StakeholderClarificationDefinition option in definition.Clarifications)
                {
                    StakeholderClarificationDefinition selected = option;
                    Button action = ActionButton(
                        $"{option.Name} 요청 · {option.LeadDays}일 / 명확도 +{option.ClarityGain}",
                        "secondary");
                    action.SetEnabled(string.IsNullOrEmpty(request.PendingClarificationId));
                    action.clicked += () =>
                    {
                        game.RequestStakeholderClarification(request.Id, selected.Id);
                        SaveAndRefresh("mail");
                    };
                    clarification.Add(action);
                }
                board.Add(clarification);
            }

            if (!request.PlanningStarted && request.FitScore < 0)
            {
                Button plan = ActionButton("계획하기", "primary");
                plan.clicked += () =>
                {
                    game.StartStakeholderPlanning(request.Id);
                    SaveAndRefresh("mail");
                };
                board.Add(plan);
                board.Add(Text("계획하기를 시작하면 산출물 해석, 담당자, 실행 일정을 별도로 정합니다.", "caption"));
                pageContent.Add(board);
                return;
            }

            foreach (WorkTask task in game.Tasks.Where(item => item.StakeholderRequestId == request.Id)
                         .OrderByDescending(item => item.ContributionWeight))
                board.Add(BuildStakeholderTaskCard(request, task));

            if (!request.PlanConfirmed && request.FitScore < 0)
            {
                Button confirm = ActionButton("작업 계획 확정", "primary");
                confirm.SetEnabled(game.CanConfirmStakeholderPlan(request.Id));
                confirm.clicked += () =>
                {
                    game.ConfirmStakeholderPlan(request.Id);
                    SaveAndRefresh("mail");
                };
                board.Add(confirm);
                board.Add(Text("모든 Task의 해석과 적격 담당자를 선택해야 확정할 수 있습니다.", "caption"));
            }
            pageContent.Add(board);
        }

        private VisualElement BuildStakeholderTaskCard(StakeholderRequestState request, WorkTask task)
        {
            StakeholderTaskDefinition definition = game.StakeholderTaskDefinition(task);
            var card = Box("summary-row");
            card.Add(Badge($"{task.ContributionWeight}%", task.ContributionWeight >= 60 ? "accent" : "neutral"));
            card.Add(Text(task.Name, "summary-title"));
            card.Add(Text($"{task.RequiredRole} · {task.RequiredWork:0.#}일 · 진행 {task.Completion * 100f:0}%", "muted"));

            if (definition != null)
            {
                card.Add(Text("산출물 해석", "caption"));
                var interpretations = Box("button-row");
                foreach (StakeholderInterpretationDefinition interpretation in definition.Interpretations)
                {
                    StakeholderInterpretationDefinition selected = interpretation;
                    Button choice = ActionButton(interpretation.Name,
                        task.SelectedInterpretationId == interpretation.Id ? "primary" : "secondary");
                    choice.SetEnabled(!request.PlanConfirmed);
                    choice.clicked += () =>
                    {
                        game.ChooseStakeholderInterpretation(task.Id, selected.Id);
                        SaveAndRefresh("mail");
                    };
                    interpretations.Add(choice);
                }
                card.Add(interpretations);
            }

            card.Add(Text("담당자 슬롯", "caption"));
            var workers = Box("button-row");
            for (int crewIndex = 0; crewIndex < game.Crew.Count; crewIndex++)
            {
                int selectedCrew = crewIndex;
                CrewMember crew = game.Crew[crewIndex];
                string blocked = game.StakeholderAssignmentBlockReason(task.Id, crewIndex);
                bool assigned = task.AssignedCharacter == crewIndex;
                Button worker = ActionButton(assigned ? crew.Name + " · 배정됨" : crew.Name,
                    assigned ? "primary" : "secondary");
                worker.SetEnabled(request.FitScore < 0 && (assigned || string.IsNullOrEmpty(blocked)));
                worker.RegisterCallback<PointerDownEvent>(_ =>
                    BeginWorkerPreview(task.Id, selectedCrew));
                worker.RegisterCallback<PointerUpEvent>(_ => EndWorkerPreview());
                worker.clicked += () =>
                {
                    if (ConsumeSuppressedWorkerClick(task.Id, selectedCrew)) return;
                    game.AssignStakeholderTask(task.Id, selectedCrew);
                    SaveAndRefresh("mail");
                };
                workers.Add(worker);
                if (!assigned && !string.IsNullOrEmpty(blocked))
                    workers.Add(Text(crew.Name + ": " + blocked, "caption"));
            }
            card.Add(workers);
            if (request.FitScore >= 0)
                card.Add(Text($"부합도 {game.StakeholderTaskFit(task.Id)}점 × {task.ContributionWeight}%", "body"));
            return card;
        }

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
            Button reset = ActionButton("캠페인 초기화", "danger");
            reset.clicked += ShowResetConfirmation;
            Button close = ActionButton("닫기", "primary");
            close.clicked += () => modalHost.Clear();
            dialog.Add(theme);
            dialog.Add(reset);
            dialog.Add(close);
            overlay.Add(dialog);
            modalHost.Add(overlay);
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
            overlay.Add(dialog);
            modalHost.Add(overlay);
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
            overlay.Add(dialog);
            modalHost.Add(overlay);
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
            overlay.Add(dialog);
            modalHost.Add(overlay);
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
            if (root == null) return;
            root.EnableInClassList("theme-dark", darkTheme);
            root.EnableInClassList("theme-light", !darkTheme);
        }

        private void SaveAndRefresh(string page)
        {
            CaptureNotifications();
            ProjectWSaveStore.SaveCampaign(CampaignSaveKey, game.CreateSnapshot());
            Navigate(page);
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(heldWorkerTaskId) && !heldWorkerPreviewVisible &&
                Time.unscaledTime - heldWorkerStartedAt >= WorkerPreviewHoldSeconds)
                ShowWorkerPreview();
            if (heldWorkerPreviewVisible && IsWorkerPreviewDismissInputPressed())
                EndWorkerPreview();
            if (notifications != null && Time.unscaledTime >= nextNotificationExpiry)
                RefreshNotificationUi(Time.unscaledTime);
        }

        private void DismissWorkerPreviewOnPointerDown(PointerDownEvent pointerEvent)
        {
            if (heldWorkerPreviewVisible)
                EndWorkerPreview();
        }

        private void DismissWorkerPreviewOnPointerUp(PointerUpEvent pointerEvent)
        {
            if (!string.IsNullOrEmpty(heldWorkerTaskId))
                EndWorkerPreview();
        }

        private static bool IsWorkerPreviewDismissInputPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.escapeKey.isPressed || keyboard.anyKey.isPressed);
        }

        private void BeginWorkerPreview(string taskId, int crewIndex)
        {
            heldWorkerTaskId = taskId;
            heldWorkerCrewIndex = crewIndex;
            heldWorkerStartedAt = Time.unscaledTime;
            heldWorkerPreviewVisible = false;
            suppressedWorkerTaskId = null;
            suppressedWorkerCrewIndex = -1;
        }

        private void EndWorkerPreview()
        {
            if (heldWorkerPreviewVisible)
            {
                suppressedWorkerTaskId = heldWorkerTaskId;
                suppressedWorkerCrewIndex = heldWorkerCrewIndex;
                modalHost.Clear();
            }
            heldWorkerTaskId = null;
            heldWorkerCrewIndex = -1;
            heldWorkerPreviewVisible = false;
        }

        private bool ConsumeSuppressedWorkerClick(string taskId, int crewIndex)
        {
            bool suppressed = suppressedWorkerTaskId == taskId &&
                              suppressedWorkerCrewIndex == crewIndex;
            suppressedWorkerTaskId = null;
            suppressedWorkerCrewIndex = -1;
            return suppressed;
        }

        private void ShowWorkerPreview()
        {
            WorkTask task = game.Tasks.FirstOrDefault(item => item.Id == heldWorkerTaskId);
            if (task == null || heldWorkerCrewIndex < 0 || heldWorkerCrewIndex >= game.Crew.Count)
                return;
            heldWorkerPreviewVisible = true;
            CrewMember crew = game.Crew[heldWorkerCrewIndex];
            bool parallel = game.Tasks.Any(item => item != task &&
                item.AssignedCharacter == heldWorkerCrewIndex && !item.IsParallelAssignment &&
                item.State != TaskState.Complete && item.State != TaskState.Failed);
            float efficiency = game.AssignmentEfficiencyMultiplier(task.Id, heldWorkerCrewIndex);
            int dissatisfaction = game.AssignmentDissatisfactionGain(
                task.Id, heldWorkerCrewIndex, parallel);
            int activeTasks = game.Tasks.Count(item => item.AssignedCharacter == heldWorkerCrewIndex &&
                item.State != TaskState.Complete && item.State != TaskState.Failed);

            modalHost.Clear();
            var overlay = Box("modal-overlay");
            var dialog = Section(crew.Name + " · 배정 미리보기",
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
            dialog.Add(Text($"현재 작업 {activeTasks}개 · {(parallel ? "강제 병행 배정" : "주 작업 배정")}", "caption"));
            overlay.Add(dialog);
            modalHost.Add(overlay);
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
