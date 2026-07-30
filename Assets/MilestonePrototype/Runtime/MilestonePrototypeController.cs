using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public sealed class MilestonePrototypeController : MonoBehaviour
    {
        private sealed class DeskWindow
        {
            public string Id;
            public string Title;
            public Rect Rect;
            public bool Minimized;
            public Vector2 Scroll;
            public Vector2 TimelineScroll;
            public int Selected;
            public int SelectedCrew;
            public int ScheduleDay;
            public string Notice;
            public string DragRegion;
            public Vector2 DragPointerOrigin;
            public Vector2 DragScrollOrigin;
            public bool DraggingContent;
            public bool Resizing;
            public Vector2 ResizePointerOrigin;
            public Rect ResizeRectOrigin;
        }

        private readonly List<DeskWindow> windows = new List<DeskWindow>();
        private readonly Dictionary<string, string> appTitles = new Dictionary<string, string>
        {
            { "mail", "MAIL / 통신" }, { "gantt", "GANTT / 작업" }, { "milestone", "MILESTONE" },
            { "workers", "CREW / 대원" }, { "report", "REPORT" }, { "codex", "CODEX / 도감" },
            { "help", "HELP" }, { "profile", "MY INFO" }, { "log", "SYSTEM LOG" },
            { "messenger", "MESSENGER / 메신저" },
            { "worker-detail", "CREW PROFILE / 대원 상세" },
            { "task-detail", "TASK DETAIL / 작업 상세" },
            { "options", "OPTIONS / 옵션" }
        };

        private MilestoneSimulation game;
        private GUIStyle title;
        private GUIStyle desktopIcon;
        private GUIStyle section;
        private GUIStyle small;
        private GUIStyle warning;
        private GUIStyle success;
        private string patchVersion = "embedded";
        private const string CampaignSaveKey = "projectw.campaign.v1";
        private const string DesktopSaveKey = "projectw.desktop.v1";
        private static readonly Color GrayColor = new Color(.6f, .6f, .6f, 1f);
        private static readonly Color InkColor = new Color(.267f, .267f, .267f, 1f);
        private static readonly Color PaleColor = new Color(.88f, .88f, .88f, 1f);
        private const float TouchDragThreshold = 8f;
        private const float ResizeHandleReach = 40f;
        private const float MinimumWindowWidth = 420f;
        private const float MinimumWindowHeight = 280f;
        public const float DefaultUiMagnification = 1.8f;
        private float uiMagnification = DefaultUiMagnification;
        private bool inputLayerBlocked;
        private float logicalWidth;
        private float logicalHeight;

        private void Awake()
        {
            game = new MilestoneSimulation();
            if (ProjectWSaveStore.TryLoadCampaign(CampaignSaveKey, out CampaignSnapshot snapshot)) game.Restore(snapshot);
            RestoreDesktop();
        }

        public void Initialize(string version) => patchVersion = string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim();

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveAll();
        }

        private void OnApplicationQuit() => SaveAll();

        private void OnGUI()
        {
            EnsureStyles();
            float scale = CalculateUiScale(Screen.width, uiMagnification);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            logicalWidth = Screen.width / scale;
            logicalHeight = Screen.height / scale;
            HandleWindowInput();
            inputLayerBlocked = IsPointerBlockedBelowWindow(-1);
            GUI.enabled = !inputLayerBlocked;
            DrawDesktop();
            DrawWindows();
            inputLayerBlocked = IsPointerBlockedBelowWindow(-1);
            GUI.enabled = !inputLayerBlocked;
            DrawTaskbar();
            inputLayerBlocked = false;
            GUI.enabled = true;
            if (Event.current.type == EventType.MouseUp) SaveDesktop();
        }

        private void DrawDesktop()
        {
            GUI.DrawTexture(new Rect(0, 0, logicalWidth, logicalHeight), Texture2D.whiteTexture);
            GUI.Label(new Rect(22, 15, Mathf.Max(220f, logicalWidth - 300f), 34),
                "PROJECT W  /  OPERATIONS DESK", title);
            GUI.Label(new Rect(logicalWidth - 250, 18, 225, 25), $"DAY {game.Day:00}/{game.CampaignEndDay}   PATCH {patchVersion}", small);
            string[] ids = { "mail", "gantt", "milestone", "workers", "report", "codex", "messenger", "help", "profile", "options" };
            for (int i = 0; i < ids.Length; i++)
            {
                Rect rect = DesktopIconRect(i, logicalWidth);
                if (GUI.Button(rect, IconLabel(ids[i]), desktopIcon)) Open(ids[i]);
            }
            OperationsReport report = game.BuildReport();
            GUI.Label(DesktopReportRect(logicalWidth, logicalHeight),
                $"운영 현황\n진행 {report.Active}  |  완료 {report.Complete}/{game.Tasks.Count}  |  지연 {report.Delayed}  |  고위험 {report.HighRisk}\n" +
                $"가용 대원 {game.Crew.Count(c => c.Available)}/{game.Crew.Count}  |  자원 {game.Resources}", section);
            if (logicalHeight >= 520f)
            {
                SetControlEnabled(!game.IsWon && !game.IsLost);
                if (GUI.Button(new Rect(25, 365, 210, 48), "하루 진행"))
                    AdvanceToNextDay();
                SetControlEnabled(true);
            }
            GUI.Label(DesktopStatusRect(logicalWidth, logicalHeight),
                FormatStatus(game.LastReport, game.IsWon, game.IsLost),
                game.IsLost ? warning : success);
        }

        private void DrawWindows()
        {
            bool compact = logicalWidth < 900;
            for (int i = 0; i < windows.Count; i++)
            {
                DeskWindow window = windows[i];
                if (window.Minimized) continue;
                if (compact) window.Rect = new Rect(6, 6, logicalWidth - 12, logicalHeight - 56);
                window.Rect = ClampRect(window.Rect);
                inputLayerBlocked = IsPointerBlockedBelowWindow(i);
                GUI.enabled = !inputLayerBlocked;
                window.Rect = GUI.Window(100 + i, window.Rect, _ => DrawWindow(window), window.Title);
            }
            inputLayerBlocked = false;
            GUI.enabled = true;
        }

        private void DrawWindow(DeskWindow window)
        {
            DrawBorder(new Rect(0, 0, window.Rect.width, window.Rect.height), InkColor);
            Rect minimizeRect = new Rect(window.Rect.width - 83, 2, 25, 20);
            Rect closeRect = new Rect(window.Rect.width - 30, 2, 25, 20);
            if (ExpandedHitButton(minimizeRect, "—"))
            {
                window.Minimized = true;
                SaveDesktop();
            }
            if (ExpandedHitButton(closeRect, "X")) { Close(window.Id); return; }
            GUILayout.Space(6);
            switch (window.Id)
            {
                case "mail": DrawMail(window); break;
                case "gantt": DrawGantt(window); break;
                case "milestone": DrawMilestones(window); break;
                case "workers": DrawWorkers(window); break;
                case "messenger": DrawMessenger(window); break;
                case "worker-detail": DrawWorkerDetail(window); break;
                case "task-detail": DrawTaskDetail(window); break;
                case "report": DrawReport(window); break;
                case "codex": DrawCodex(window); break;
                case "help": DrawHelp(); break;
                case "profile": DrawProfile(); break;
                case "options": DrawOptions(window); break;
                case "log": DrawLog(window); break;
            }
            GUI.DragWindow(WindowDragHitRect(window.Rect.width));
        }

        private void DrawMail(DeskWindow window)
        {
            List<MailEvent> arrived = game.Mail.Where(m => m.ArrivalDay <= game.Day).ToList();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Min(230, window.Rect.width * .36f)));
            window.Scroll = GUILayout.BeginScrollView(window.Scroll);
            for (int i = 0; i < arrived.Count; i++)
            {
                MailEvent mail = arrived[i];
                string prefix = mail.Resolved ? "[완료] " : mail.Read ? "" : "[NEW] ";
                if (GUILayout.Button($"{prefix}{mail.Subject}\n{mail.From}", GUILayout.Height(55)))
                {
                    window.Selected = i;
                    game.MarkMailRead(mail.Id);
                    SaveCampaign();
                }
            }
            EndTouchScroll(window, "mail-list", ref window.Scroll);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);
            if (arrived.Count == 0) GUILayout.Label("도착한 통신이 없습니다.");
            else
            {
                window.Selected = Mathf.Clamp(window.Selected, 0, arrived.Count - 1);
                MailEvent mail = arrived[window.Selected];
                GUILayout.Label(mail.Subject, section);
                GUILayout.Label($"FROM  {mail.From}    RISK  {RiskName(mail.Risk)}", small);
                GUILayout.Space(8);
                GUILayout.Label(mail.Body);
                GUILayout.Space(8);
                GUILayout.Label($"지시: {mail.Instruction}", warning);
                SetControlEnabled(!mail.Resolved);
                if (GUILayout.Button(mail.Resolved ? "처리 완료" : "지시 수락 및 반영", GUILayout.Height(38)))
                {
                    game.ResolveMail(mail.Id);
                    SaveCampaign();
                }
                SetControlEnabled(true);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawGantt(DeskWindow window)
        {
            GUILayout.Label("GANTT / 일감 계획", section);
            GUILayout.Label($"DAY {game.Day:00}  │  회색=완료  진회색=예상 잔여  ┆ SOFT  │ HARD", small);
            float availableHeight = Mathf.Max(120f, window.Rect.height - 82f);
            Rect viewport = GUILayoutUtility.GetRect(100f, availableHeight,
                GUILayout.ExpandWidth(true));
            DrawGanttTimeline(window, viewport);
        }

        private void DrawGanttTimeline(DeskWindow window, Rect viewport)
        {
            const float labelWidth = 190f;
            const float dayWidth = 28f;
            const float rowHeight = 28f;
            int rowCount = game.Groups.Sum(group =>
                1 + game.Tasks.Count(task => task.GroupId == group.Id));
            float contentWidth = game.CampaignEndDay * dayWidth + 16f;
            float contentHeight = Math.Max(120f, rowCount * rowHeight + 28f);
            Rect content = new Rect(0, 0, contentWidth, contentHeight);
            Rect labelViewport = new Rect(viewport.x, viewport.y, labelWidth, viewport.height);
            Rect timelineViewport = new Rect(viewport.x + labelWidth, viewport.y,
                Mathf.Max(1f, viewport.width - labelWidth), viewport.height);

            window.TimelineScroll = GUI.BeginScrollView(timelineViewport, window.TimelineScroll, content);
            DrawSolid(new Rect(0, 0, contentWidth, contentHeight), Color.white);
            for (int day = 1; day <= game.CampaignEndDay; day++)
            {
                float x = (day - 1) * dayWidth;
                DrawSolid(new Rect(x, 0, 1, contentHeight), day == game.Day ? InkColor : PaleColor);
                GUI.Label(new Rect(x + 2, 1, dayWidth - 3, 24), day.ToString(), small);
            }

            float y = 28f;
            foreach (WorkGroup group in game.Groups)
            {
                List<WorkTask> tasks = game.Tasks.Where(task => task.GroupId == group.Id).ToList();
                DrawSolid(new Rect(0, y, contentWidth, rowHeight - 1), PaleColor);
                DrawSolid(new Rect(0, y, contentWidth, 1), GrayColor);
                DrawDeadlineLine(group.SoftDeadline, y, rowHeight * (tasks.Count + 1),
                    dayWidth, false);
                DrawDeadlineLine(group.HardDeadline, y, rowHeight * (tasks.Count + 1),
                    dayWidth, true);
                y += rowHeight;

                foreach (WorkTask task in tasks)
                {
                    TaskScheduleEstimate preview = game.EstimatePreviewSchedule(task.Id);
                    int actualDays = TaskActualDurationDays(task, game.Day);
                    int startDay = task.StartedDay > 0
                        ? task.StartedDay
                        : preview?.StartDay ?? game.Day;
                    float completedWidth = actualDays * dayWidth;
                    float remainingWidth = (preview?.DurationDays ?? 0) * dayWidth;
                    float barX = (startDay - 1) * dayWidth + 3;
                    if (completedWidth > 0)
                        DrawSolid(new Rect(barX, y + 7, completedWidth, 14), GrayColor);
                    int projectedStartDay = task.StartedDay > 0
                        ? preview?.StartDay ?? Mathf.Max(game.Day, task.StartedDay + actualDays)
                        : preview?.StartDay ?? game.Day;
                    float remainingX = (projectedStartDay - 1) * dayWidth + 3;
                    if (remainingWidth > 0)
                        DrawSolid(new Rect(remainingX, y + 7, remainingWidth, 14),
                            task.State == TaskState.Locked ? PaleColor : InkColor);
                    GUI.Label(new Rect(Math.Max(barX, remainingX) + 3, y + 4,
                            Math.Max(54, remainingWidth - 4), 21),
                        $"{task.RemainingWork:0.#}d", small);
                    y += rowHeight;
                }
                DrawSolid(new Rect(0, y - 1, contentWidth, 1), GrayColor);
            }
            DrawDependencyArrows(dayWidth, rowHeight);
            GUI.EndScrollView();
            HandleTouchScroll(window, "gantt-timeline", timelineViewport, ref window.TimelineScroll);

            GUI.BeginGroup(labelViewport);
            DrawSolid(new Rect(0, 0, labelWidth, labelViewport.height), Color.white);
            y = 28f - window.TimelineScroll.y;
            foreach (WorkGroup group in game.Groups)
            {
                List<WorkTask> tasks = game.Tasks.Where(task => task.GroupId == group.Id).ToList();
                DrawSolid(new Rect(0, y, labelWidth, rowHeight - 1), PaleColor);
                DrawSolid(new Rect(0, y, labelWidth, 1), GrayColor);
                GUI.Label(new Rect(6, y + 4, labelWidth - 10, 22),
                    $"{group.Name} · {WorkStateName(group.State)}", small);
                y += rowHeight;

                foreach (WorkTask task in tasks)
                {
                    if (GUI.Button(new Rect(4, y + 2, labelWidth - 8, rowHeight - 3),
                            $"{StateName(task.State)}  {task.Name}" +
                            (task.ScheduledDay > 0 ? $"  [D{task.ScheduledDay:00}]" : ""), small))
                        OpenTaskDetail(task.Id);
                    y += rowHeight;
                }
                DrawSolid(new Rect(0, y - 1, labelWidth, 1), GrayColor);
            }
            GUI.EndGroup();
        }

        private void DrawDependencyArrows(float dayWidth, float rowHeight)
        {
            foreach (WorkTask task in game.Tasks)
            {
                if (string.IsNullOrEmpty(task.PrerequisiteId)) continue;
                WorkTask predecessor = game.Tasks.FirstOrDefault(candidate =>
                    candidate.Id == task.PrerequisiteId);
                if (predecessor != null)
                    DrawDependencyArrow(predecessor, task, dayWidth, rowHeight);
            }

            foreach (WorkGroup group in game.Groups)
            {
                if (group.PredecessorIds == null) continue;
                foreach (string predecessorId in group.PredecessorIds)
                {
                    DrawWorkDependencyArrow(predecessorId, group.Id, dayWidth, rowHeight);
                }
            }
        }

        private void DrawWorkDependencyArrow(string predecessorId, string successorId,
            float dayWidth, float rowHeight)
        {
            float fromY = WorkRowCenterY(predecessorId, rowHeight);
            float toY = WorkRowCenterY(successorId, rowHeight);
            if (fromY < 0 || toY < 0) return;

            float x = Mathf.Max(8f, (Mathf.Max(1, game.Day) - 1) * dayWidth - 8f);
            DrawSolid(new Rect(x, Mathf.Min(fromY, toY), 2f,
                Mathf.Max(2f, Mathf.Abs(toY - fromY))), GrayColor);
            float direction = toY >= fromY ? 1f : -1f;
            DrawSolid(new Rect(x - 4f, toY - direction * 6f, 10f, 2f), GrayColor);
            DrawSolid(new Rect(x - 3f, toY - direction * 4f, 8f, 2f), GrayColor);
            DrawSolid(new Rect(x - 2f, toY - direction * 2f, 6f, 2f), GrayColor);
        }

        private void DrawDependencyArrow(WorkTask predecessor, WorkTask successor,
            float dayWidth, float rowHeight)
        {
            float fromX = TaskBarEndX(predecessor, dayWidth);
            float toX = TaskBarStartX(successor, dayWidth);
            float fromY = TaskRowCenterY(predecessor.Id, rowHeight);
            float toY = TaskRowCenterY(successor.Id, rowHeight);
            float bendX = Mathf.Max(2f, toX - 10f);

            DrawHorizontalLine(fromX, bendX, fromY, GrayColor);
            DrawSolid(new Rect(bendX, Mathf.Min(fromY, toY), 2f,
                Mathf.Max(2f, Mathf.Abs(toY - fromY))), GrayColor);
            DrawHorizontalLine(bendX, toX, toY, GrayColor);
            DrawSolid(new Rect(toX - 6f, toY - 4f, 2f, 8f), GrayColor);
            DrawSolid(new Rect(toX - 4f, toY - 3f, 2f, 6f), GrayColor);
            DrawSolid(new Rect(toX - 2f, toY - 2f, 2f, 4f), GrayColor);
        }

        private float TaskRowCenterY(string taskId, float rowHeight)
        {
            float y = 28f;
            foreach (WorkGroup group in game.Groups)
            {
                y += rowHeight;
                foreach (WorkTask task in game.Tasks.Where(candidate =>
                             candidate.GroupId == group.Id))
                {
                    if (task.Id == taskId) return y + rowHeight * .5f;
                    y += rowHeight;
                }
            }
            return 28f;
        }

        private float WorkRowCenterY(string groupId, float rowHeight)
        {
            float y = 28f;
            foreach (WorkGroup group in game.Groups)
            {
                if (group.Id == groupId) return y + rowHeight * .5f;
                y += rowHeight;
                y += game.Tasks.Count(task => task.GroupId == group.Id) * rowHeight;
            }
            return -1f;
        }

        private float TaskBarStartX(WorkTask task, float dayWidth)
        {
            TaskScheduleEstimate preview = game.EstimatePreviewSchedule(task.Id);
            int startDay = task.StartedDay > 0
                ? task.StartedDay
                : preview?.StartDay ?? game.Day;
            return (startDay - 1) * dayWidth + 3f;
        }

        private float TaskBarEndX(WorkTask task, float dayWidth)
        {
            int actualDays = TaskActualDurationDays(task, game.Day);
            TaskScheduleEstimate preview = game.EstimatePreviewSchedule(task.Id);
            int startDay = task.StartedDay > 0
                ? task.StartedDay
                : preview?.StartDay ?? game.Day;
            float completedEnd = (startDay - 1) * dayWidth + 3f + actualDays * dayWidth;
            int projectedStartDay = task.StartedDay > 0
                ? preview?.StartDay ?? Mathf.Max(game.Day, task.StartedDay + actualDays)
                : preview?.StartDay ?? game.Day;
            float remainingEnd = (projectedStartDay - 1) * dayWidth + 3f +
                                 (preview?.DurationDays ?? 0) * dayWidth;
            return Mathf.Max(completedEnd, remainingEnd);
        }

        public static int TaskActualDurationDays(WorkTask task, int currentDay)
        {
            if (task == null || task.StartedDay <= 0) return 0;
            int endDay = task.CompletedDay > 0
                ? task.CompletedDay
                : Mathf.Max(task.StartedDay, currentDay - 1);
            return Mathf.Max(1, endDay - task.StartedDay + 1);
        }

        private static void DrawDeadlineLine(int day, float y, float height, float dayWidth, bool hard)
        {
            if (day <= 0) return;
            float x = (day - 1) * dayWidth + (hard ? dayWidth - 2 : dayWidth * .5f);
            Color color = hard ? InkColor : GrayColor;
            float segment = hard ? height : 4f;
            if (hard) DrawSolid(new Rect(x, y, 2, height), color);
            else
                for (float offset = 0; offset < height; offset += 8f)
                    DrawSolid(new Rect(x, y + offset, 1, Math.Min(segment, height - offset)), color);
        }

        private void DrawTaskDetail(DeskWindow window)
        {
            if (game.Tasks.Count == 0) return;
            WorkTask task = game.Tasks[Mathf.Clamp(window.Selected, 0, game.Tasks.Count - 1)];
            WorkGroup work = game.Groups.FirstOrDefault(group => group.Id == task.GroupId);
            WorkTask predecessor = string.IsNullOrEmpty(task.PrerequisiteId)
                ? null
                : game.Tasks.FirstOrDefault(candidate => candidate.Id == task.PrerequisiteId);
            List<WorkTask> successors = game.Tasks.Where(candidate =>
                candidate.PrerequisiteId == task.Id).ToList();
            int matchingWorker = game.Crew.FindIndex(member => member.Specialty == task.RequiredRole);
            TaskCostPreview cost = game.BuildCostPreview(task, matchingWorker);
            string assignee = task.AssignedCharacter < 0
                ? "미배정"
                : $"{game.Crew[task.AssignedCharacter].Name} / {(task.IsParallelAssignment ? "병행" : "주 작업")}";

            window.Scroll = GUILayout.BeginScrollView(window.Scroll);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{task.Name}  ·  {(task.Required ? "필수" : "선택")}", section);
            GUILayout.Label(
                $"{work?.Name ?? "소속 없음"} / {WorkStateName(work?.State ?? WorkState.Locked)}   " +
                $"역할 {RoleName(task.RequiredRole)}   위험 {RiskName(game.EffectiveRisk(task))}", small);
            GUILayout.HorizontalSlider(task.Completion, 0, 1);
            GUILayout.Label(
                $"진행 {task.Progress:0.#}일 / 유효 {task.EffectiveRequiredWork:0.#}일   " +
                $"잔여 {task.RemainingWork:0.#}일", small);
            GUILayout.Label(
                $"시작일 {(task.StartedDay > 0 ? $"DAY {task.StartedDay:00}" : "미시작")}  /  " +
                $"완료일 {(task.CompletedDay > 0 ? $"DAY {task.CompletedDay:00}" : "미완료")}", small);
            GUILayout.Label($"최근 하루 산출 {task.LastOutput:0.#}  /  중요도 {ImportanceName(task.Importance)}", small);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("일정", section);
            GUILayout.Label($"SOFT D{work?.SoftDeadline ?? 0}  /  HARD D{work?.HardDeadline ?? 0}");
            GUILayout.Label($"상태: {(work?.SoftDeadlineMissed == true ? "소프트 마감 초과" : "정상 일정")}");
            GUILayout.Label($"잠금: {TaskLockReason(task, work)}", small);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("비용", section);
            GUILayout.Label($"기본 {task.RequiredWork:0.#}일 + 문맥 {task.ContextCostDays:0.#}일");
            GUILayout.Label($"중단/인수인계 {task.SplitCount}회");
            GUILayout.Label(task.AssignedCharacter >= 0 && task.Progress > 0 &&
                            task.State != TaskState.Complete
                ? $"지금 담당 변경 시 +{game.InterruptionAndResumptionCostDays:0.#}일"
                : "지금 담당 변경 비용 없음", warning);
            GUILayout.Label($"적합 인력 피로: 주 {cost.PrimaryFatigue} / 병행 {cost.ParallelFatigue}", small);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Label("연결 관계", section);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"상위 일감: {work?.Name ?? "없음"}  ·  {WorkStateName(work?.State ?? WorkState.Locked)}");
            if (predecessor == null)
                GUILayout.Label("막고 있는 선행 작업: 없음", small);
            else
            {
                GUILayout.Label($"막고 있는 선행 작업: {TaskRelationSummary(predecessor)}", small);
                if (GUILayout.Button($"선행 상세 열기 · {predecessor.Name}"))
                    OpenTaskDetail(predecessor.Id);
            }
            if (successors.Count == 0)
                GUILayout.Label("이 작업에 막혀 있는 후행 작업: 없음", small);
            else
                foreach (WorkTask successor in successors)
                    if (GUILayout.Button($"후행 · {TaskRelationSummary(successor)}"))
                        OpenTaskDetail(successor.Id);
            GUILayout.EndVertical();

            GUILayout.Label($"현재 담당: {assignee}", section);
            GUILayout.BeginHorizontal();
            SetControlEnabled(task.State == TaskState.Available || task.State == TaskState.Active);
            if (GUILayout.Button("주 작업 담당 순환")) { AssignNext(task); SaveCampaign(); }
            SetControlEnabled((task.State == TaskState.Available || task.State == TaskState.Active) &&
                              cost.CanRunInParallel);
            if (GUILayout.Button("병행 담당 순환")) { AssignNextParallel(task); SaveCampaign(); }
            SetControlEnabled(task.AssignedCharacter >= 0);
            if (GUILayout.Button("배정 해제")) { game.Assign(task.Id, -1); SaveCampaign(); }
            SetControlEnabled(true);
            GUILayout.EndHorizontal();

            GUILayout.Label("작업 시작 예약", section);
            GUILayout.BeginVertical(GUI.skin.box);
            if (task.ScheduledDay > 0 && task.ScheduledWorker >= 0 &&
                task.ScheduledWorker < game.Crew.Count)
                GUILayout.Label($"예약 시작일: DAY {task.ScheduledDay:00} · {game.Crew[task.ScheduledWorker].Name}",
                    success);
            else
                GUILayout.Label("예약된 시작일 없음", small);
            window.ScheduleDay = Mathf.Clamp(window.ScheduleDay <= 0 ? game.Day : window.ScheduleDay,
                game.Day, game.CampaignEndDay);
            window.SelectedCrew = Mathf.Clamp(window.SelectedCrew, 0, Mathf.Max(0, game.Crew.Count - 1));
            TaskScheduleEstimate estimate = game.EstimateSchedule(task.Id, window.SelectedCrew);
            if (estimate != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"자동 일정 산정 · {game.Crew[window.SelectedCrew].Name}", section);
                GUILayout.Label(
                    $"잔여 산정 작업량 {estimate.EstimatedWork:0.##} ÷ 기대 산출 {estimate.ExpectedDailyOutput:0.##}/일 " +
                    $"= {estimate.DurationDays}일", small);
                GUILayout.Label(
                    $"예상 시작 DAY {estimate.StartDay:00}  →  예상 완료 DAY {estimate.CompletionDay:00}",
                    estimate.RollingStart ? warning : success);
                GUILayout.Label(estimate.StartReason, small);
                GUILayout.EndVertical();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ DAY")) window.ScheduleDay = Mathf.Max(game.Day, window.ScheduleDay - 1);
            GUILayout.Label($"시작 DAY {window.ScheduleDay:00}", section);
            if (GUILayout.Button("DAY ▶")) window.ScheduleDay = Mathf.Min(game.CampaignEndDay, window.ScheduleDay + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ 작업자"))
            {
                window.SelectedCrew = (window.SelectedCrew - 1 + game.Crew.Count) % game.Crew.Count;
                SetEstimatedScheduleDay(window, task);
            }
            GUILayout.Label(game.Crew[window.SelectedCrew].Name, section);
            if (GUILayout.Button("작업자 ▶"))
            {
                window.SelectedCrew = (window.SelectedCrew + 1) % game.Crew.Count;
                SetEstimatedScheduleDay(window, task);
            }
            GUILayout.EndHorizontal();
            SetControlEnabled(task.State != TaskState.Complete && task.State != TaskState.Failed);
            if (GUILayout.Button("이 시작일로 예약"))
            {
                window.Notice = game.Schedule(task.Id, window.SelectedCrew, window.ScheduleDay)
                    ? "작업 시작일을 예약했습니다. 시작 후에는 완료하거나 다시 조정할 때까지 계속 작업합니다."
                    : "같은 작업자의 해당 날짜 예약과 충돌하거나 예약할 수 없는 작업입니다.";
                SaveCampaign();
            }
            SetControlEnabled(task.ScheduledDay > 0);
            if (GUILayout.Button("예약 취소"))
            {
                game.CancelSchedule(task.Id);
                window.Notice = "예약을 취소했습니다.";
                SaveCampaign();
            }
            SetControlEnabled(true);
            if (!string.IsNullOrEmpty(window.Notice)) GUILayout.Label(window.Notice, small);
            GUILayout.EndVertical();

            GUILayout.Label("최근 기록", section);
            if (task.Records == null || task.Records.Count == 0) GUILayout.Label("기록 없음", small);
            else
                foreach (TaskRecord record in task.Records.Skip(Math.Max(0, task.Records.Count - 4)))
                    GUILayout.Label($"D{record.Day:00}  {record.Actor}  {record.Text}", small);
            GUILayout.EndVertical();
            EndTouchScroll(window, "task-detail", ref window.Scroll);
        }

        private void DrawMilestones(DeskWindow window)
        {
            GUILayout.Label("마일스톤", section);
            window.Scroll = GUILayout.BeginScrollView(window.Scroll);
            foreach (WorkGroup group in game.Groups.Where(g => g.Id != "incident"))
            {
                List<WorkTask> tasks = game.Tasks.Where(t => t.GroupId == group.Id).ToList();
                int progress = tasks.Count == 0 ? 0 : Mathf.RoundToInt(tasks.Average(t => t.Completion) * 100);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{group.Name}   {progress}%   HARD D{group.HardDeadline}", section);
                GUILayout.HorizontalSlider(progress, 0, 100);
                foreach (WorkTask task in tasks)
                    if (GUILayout.Button(
                            $"{(task.Required ? "[필수]" : "[선택]")} {task.Name} — {StateName(task.State)} {task.Progress}/{task.RequiredWork}"))
                        OpenTaskDetail(task.Id);
                GUILayout.EndVertical();
            }
            EndTouchScroll(window, "milestones", ref window.Scroll);
        }

        private void DrawWorkers(DeskWindow window)
        {
            GUILayout.Label("대원 파일", section);
            window.Scroll = GUILayout.BeginScrollView(window.Scroll);
            for (int i = 0; i < game.Crew.Count; i++)
            {
                CrewMember member = game.Crew[i];
                GUILayout.BeginVertical(GUI.skin.box);
                if (GUILayout.Button(
                        $"{member.Name}   {RoleName(member.Specialty)} / SKILL {member.Skill} / EXP {member.Experience}",
                        section, GUILayout.Height(38)))
                    OpenWorkerDetail(i);
                GUILayout.Label($"상태 {member.Condition}   피로 {member.Fatigue}%   담당 {AssignedTask(i)}");
                GUILayout.HorizontalSlider(member.Fatigue, 0, 100);
                GUILayout.Label($"담당자 신뢰도 {member.Trust}% · {MilestoneSimulation.TrustDescription(member.Trust)}", small);
                if (member.History.Count > 0) GUILayout.Label($"최근: {member.History[member.History.Count - 1]}", small);
                GUILayout.BeginHorizontal();
                SetControlEnabled(member.InjuryDays <= 0 && !member.RestScheduled);
                if (GUILayout.Button(member.RestScheduled ? "휴식 예약됨" : "휴식 예약")) { game.Rest(i); SaveCampaign(); }
                SetControlEnabled(game.Resources >= 3);
                if (GUILayout.Button($"재생 시술 {game.RegenerationResourceCost}자원 ({member.RegenerationCount})")) { game.Regenerate(i); SaveCampaign(); }
                SetControlEnabled(true);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            EndTouchScroll(window, "workers", ref window.Scroll);
        }

        private void DrawWorkerDetail(DeskWindow window)
        {
            GUILayout.Label("대원 세부 파일", section);
            if (game.Crew.Count == 0)
            {
                GUILayout.Label("등록된 대원이 없습니다.");
                return;
            }

            window.Selected = Mathf.Clamp(window.Selected, 0, game.Crew.Count - 1);
            CrewMember member = game.Crew[window.Selected];
            window.Scroll = GUILayout.BeginScrollView(window.Scroll);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(155), GUILayout.Height(185));
            GUILayout.Label("사진", small);
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.IsNullOrEmpty(member.PortraitLabel) ? "NO PHOTO" : member.PortraitLabel,
                title, GUILayout.Height(105));
            GUILayout.FlexibleSpace();
            GUILayout.Label(RoleName(member.Specialty), small);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(member.Name, title);
            GUILayout.Label($"{RoleName(member.Specialty)}  /  SKILL {member.Skill}  /  EXP {member.Experience}",
                section);
            GUILayout.Label($"상태 {member.Condition}   피로 {member.Fatigue}%");
            GUILayout.Label($"현재 담당  {AssignedTask(window.Selected)}", small);
            GUILayout.Label($"담당자 신뢰도  {member.Trust}%", section);
            GUILayout.HorizontalSlider(member.Trust, 0, 100);
            GUILayout.Label(MilestoneSimulation.TrustDescription(member.Trust), small);
            DrawSectionRule();
            GUILayout.Label("메모", section);
            GUILayout.Label(string.IsNullOrEmpty(member.Memo) ? "메모 없음" : member.Memo);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            DrawSectionRule();
            GUILayout.Label("퍽", section);
            if (member.Perks == null || member.Perks.Length == 0)
                GUILayout.Label("보유 퍽 없음", small);
            else
                foreach (string perk in member.Perks)
                    GUILayout.Label($"• {perk}");

            DrawSectionRule();
            GUILayout.Label("작업 히스토리", section);
            bool hasHistory = false;
            if (member.History != null)
            {
                foreach (string history in member.History.AsEnumerable().Reverse())
                {
                    GUILayout.Label(history, small);
                    hasHistory = true;
                }
            }
            foreach (WorkTask task in game.Tasks)
            {
                if (task.Records == null) continue;
                foreach (TaskRecord record in task.Records.AsEnumerable().Reverse())
                {
                    if (record.Actor != member.Name ||
                        !string.IsNullOrEmpty(record.Text) && record.Text.Contains("배정")) continue;
                    GUILayout.Label($"DAY {record.Day}: {task.Name} — {record.Text}", small);
                    hasHistory = true;
                }
            }
            if (!hasHistory) GUILayout.Label("아직 작업 기록이 없습니다.", small);

            EndTouchScroll(window, "worker-detail", ref window.Scroll);
        }

        private void DrawMessenger(DeskWindow window)
        {
            if (game.Crew.Count == 0)
            {
                GUILayout.Label("등록된 작업자가 없습니다.");
                return;
            }

            window.Selected = Mathf.Clamp(window.Selected, 0, game.Crew.Count - 1);
            CrewMember selected = game.Crew[window.Selected];
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(Mathf.Min(190f, window.Rect.width * .32f)));
            GUILayout.Label("인원", section);
            for (int i = 0; i < game.Crew.Count; i++)
            {
                CrewMember member = game.Crew[i];
                string active = i == window.Selected ? "● " : "";
                if (GUILayout.Button($"{active}{member.Name}\n{MessengerPresence(i)}", GUILayout.Height(52)))
                {
                    window.Selected = i;
                    window.Scroll = Vector2.zero;
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(selected.Name, section);
            GUILayout.Label(
                $"{RoleName(selected.Specialty)} · {MessengerPresence(window.Selected)} · 담당자 신뢰 {selected.Trust}%",
                small);
            GUILayout.Label(MilestoneSimulation.TrustDescription(selected.Trust), small);
            DrawSectionRule();

            window.Scroll = GUILayout.BeginScrollView(window.Scroll);
            bool hasMessages = false;
            if (selected.History != null)
            {
                foreach (string history in selected.History)
                {
                    DrawMessengerBubble(history);
                    hasMessages = true;
                }
            }
            foreach (WorkTask task in game.Tasks)
            {
                if (task.Records == null) continue;
                foreach (TaskRecord record in task.Records)
                {
                    if (record.Actor != selected.Name) continue;
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label($"DAY {record.Day:00}  {selected.Name}", small);
                    GUILayout.Label($"{task.Name}: {record.Text}");
                    GUILayout.EndVertical();
                    hasMessages = true;
                }
            }
            if (!hasMessages)
                GUILayout.Label("아직 대화나 작업 피드백이 없습니다.", small);
            EndTouchScroll(window, "messenger-chat", ref window.Scroll);

            GUILayout.Label("물어보기", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("안부 묻기", GUILayout.Height(36)))
            {
                game.AskWorker(window.Selected, "status");
                SaveCampaign();
            }
            if (GUILayout.Button("작업 현황 묻기", GUILayout.Height(36)))
            {
                game.AskWorker(window.Selected, "work");
                SaveCampaign();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private string MessengerPresence(int crewIndex)
        {
            CrewMember member = game.Crew[crewIndex];
            if (member.InjuryDays > 0) return $"부상 · {member.InjuryDays}일";
            if (member.RestScheduled) return "휴식 예정";
            WorkTask task = game.Tasks.FirstOrDefault(candidate =>
                candidate.AssignedCharacter == crewIndex && !candidate.IsParallelAssignment);
            return task == null ? "대기 중" : $"작업 중 · {task.Name}";
        }

        private void DrawMessengerBubble(string message)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(message, small);
            GUILayout.EndVertical();
        }

        private void DrawReport(DeskWindow window)
        {
            OperationsReport report = game.BuildReport();
            GUILayout.Label("운영 보고서", section);
            GUILayout.Label($"DAY {game.Day:00} 상태 요약", section);
            GUILayout.Label($"완료 {report.Complete}  진행 {report.Active}  대기 {report.Available}  잠김 {report.Locked}");
            GUILayout.Label($"지연 {report.Delayed}  고위험 {report.HighRisk}  과로/부상 대원 {report.OverloadedCrew}",
                report.Delayed + report.HighRisk > 0 ? warning : success);
            GUILayout.Space(8);
            GUILayout.Label("주의 작업", section);
            window.Scroll = GUILayout.BeginScrollView(window.Scroll);
            foreach (WorkTask task in game.Tasks.Where(t => game.EffectiveRisk(t) == RiskLevel.High && t.State != TaskState.Complete))
                GUILayout.Label($"[고위험] {task.Name} / D{task.Deadline} / {StateName(task.State)}", warning);
            GUILayout.Space(8);
            GUILayout.Label("최근 결과", section);
            foreach (string line in game.LastReport.Lines) GUILayout.Label(line);
            EndTouchScroll(window, "report", ref window.Scroll);
        }

        private void DrawCodex(DeskWindow window)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(185));
            for (int i = 0; i < game.Codex.Count; i++)
                if (GUILayout.Button($"{game.Codex[i].Category}\n{game.Codex[i].Name}", GUILayout.Height(48))) window.Selected = i;
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);
            CodexEntry entry = game.Codex[Mathf.Clamp(window.Selected, 0, game.Codex.Count - 1)];
            GUILayout.Label(entry.Name, section);
            GUILayout.Label(entry.Category, small);
            GUILayout.Space(10);
            GUILayout.Label(entry.Description);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawHelp()
        {
            GUILayout.Label("OPS DESK 사용법", section);
            GUILayout.Label("• 바탕화면 아이콘을 한 번 탭해 앱을 엽니다.\n• 창 제목을 드래그해 이동합니다.\n• 창 내용은 터치 드래그 또는 마우스 휠로 이동합니다.\n• Gantt에서는 왼쪽 일감 열이 고정되고 날짜 영역만 움직입니다.\n• 주 작업은 하루 하나, 잔여 1일 이하 작업은 피로를 더 써서 병행할 수 있습니다.\n• 통신 지시를 수락하면 마감·중요도·자원이 실제 게임에 반영됩니다.\n• 옵션에서 화면 배율과 초기화 기능을 설정할 수 있습니다.");
        }

        private void DrawProfile()
        {
            GUILayout.Label("내정보 / 캠페인 관리", section);
            GUILayout.Label(
                $"PROJECT W 운영 담당자\nDAY {game.Day}/{game.CampaignEndDay}\n" +
                $"중간평가 DAY {game.MidpointReviewDay} ({(game.MidpointReviewIssued ? "완료" : "예정")})\n" +
                $"자원 {game.Resources}\n패치 {patchVersion}");
            GUILayout.Space(12);
            GUILayout.Label($"자동지정 규칙 {game.AssignmentRules.Count}개", section);
            if (game.AssignmentRules.Count == 0)
                GUILayout.Label("아직 학습된 규칙이 없습니다. 작업자를 손으로 지정하면 기록됩니다.", small);
            foreach (AssignmentRule rule in game.AssignmentRules)
                GUILayout.Label(
                    $"{RoleName(rule.RequiredRole)} / {rule.Kind} / 난이도 {rule.Difficulty} / " +
                    $"{rule.Risk} 위험 / {rule.Importance} 중요 → {rule.CrewName} " +
                    $"(갱신 {rule.UpdateCount}회)", small);
        }

        private void DrawOptions(DeskWindow window)
        {
            window.Scroll = GUILayout.BeginScrollView(window.Scroll);
            GUILayout.Label("화면 설정", title);
            GUILayout.Space(8);
            GUILayout.Label("화면 배율", section);
            GUILayout.Label(
                "글자, 버튼, 여백, 패널과 터치 영역이 함께 조정됩니다. 선택 즉시 적용되며 재실행 후에도 유지됩니다.",
                small);
            GUILayout.Space(8);
            DrawScaleOption(1f, "1.0×  기본");
            DrawScaleOption(1.4f, "1.4×  크게");
            DrawScaleOption(1.8f, "1.8×  매우 크게");
            DrawScaleOption(2.2f, "2.2×  최대");
            GUILayout.Space(8);
            GUILayout.Label($"현재 화면 배율: {uiMagnification:0.0}×", success);
            GUILayout.Space(16);
            GUILayout.Label("초기화", section);
            if (GUILayout.Button("창 위치 및 열린 상태 초기화", GUILayout.Height(38)))
            {
                ProjectWSaveStore.Delete(DesktopSaveKey);
                windows.Clear();
                SaveDesktop();
            }
            if (GUILayout.Button("새 캠페인 시작", GUILayout.Height(38)))
            {
                ProjectWSaveStore.Delete(CampaignSaveKey);
                game = new MilestoneSimulation();
                SaveCampaign();
            }
            EndTouchScroll(window, "options", ref window.Scroll);
        }

        private void DrawScaleOption(float value, string label)
        {
            bool selected = Mathf.Abs(uiMagnification - value) < .01f;
            if (GUILayout.Button($"{(selected ? "●" : "○")}  {label}", GUILayout.Height(48f)))
            {
                uiMagnification = value;
                SaveDesktop();
            }
        }

        private void DrawLog(DeskWindow window)
        {
            window.Scroll = GUILayout.BeginScrollView(window.Scroll);
            foreach (string line in game.SystemLog.AsEnumerable().Reverse()) GUILayout.Label(line, small);
            EndTouchScroll(window, "system-log", ref window.Scroll);
        }

        private void DrawTaskbar()
        {
            Rect bar = new Rect(0, logicalHeight - 44, logicalWidth, 44);
            DrawSolid(bar, GrayColor);
            SetControlEnabled(!game.IsWon && !game.IsLost);
            if (GUI.Button(NextDayButtonRect(logicalWidth, logicalHeight), "다음날로 →"))
                AdvanceToNextDay();
            SetControlEnabled(true);
            float x = 8;
            foreach (DeskWindow window in windows.ToArray())
            {
                if (GUI.Button(new Rect(x, logicalHeight - 38, 118, 31), window.Title))
                {
                    window.Minimized = !window.Minimized;
                    Focus(window);
                    SaveDesktop();
                }
                x += 122;
                if (x > logicalWidth - 330) break;
            }
            int unread = game.Mail.Count(m => m.ArrivalDay <= game.Day && !m.Read);
            if (GUI.Button(new Rect(logicalWidth - 310, logicalHeight - 38, 95, 31), unread > 0 ? $"MAIL ({unread})" : "MAIL")) Open("mail");
            if (GUI.Button(new Rect(logicalWidth - 210, logicalHeight - 38, 95, 31), "LOG")) Open("log");
            GUI.Label(new Rect(logicalWidth - 108, logicalHeight - 34, 100, 25), $"DAY {game.Day:00}", small);
        }

        private void AdvanceToNextDay()
        {
            game.AdvanceDay();
            SaveCampaign();
        }

        public static Rect NextDayButtonRect(float width, float height) =>
            new Rect(Mathf.Max(10f, width - 180f), Mathf.Max(10f, height - 98f), 170f, 48f);

        public static float CalculateUiScale(float screenWidth) =>
            CalculateUiScale(screenWidth, DefaultUiMagnification);

        public static float CalculateUiScale(float screenWidth, float magnification) =>
            Mathf.Clamp(screenWidth / 1280f, 0.72f, 1.5f) *
            NormalizeUiMagnification(magnification);

        public static float NormalizeUiMagnification(float magnification) =>
            magnification <= 0f
                ? DefaultUiMagnification
                : Mathf.Clamp(magnification, 1f, 2.2f);

        public static Rect DesktopReportRect(float width, float height)
        {
            bool compactHeight = height < 520f;
            return new Rect(25f, compactHeight ? 250f : 275f,
                Mathf.Max(220f, Mathf.Min(455f, width - (compactHeight ? 220f : 50f))),
                compactHeight ? 68f : 80f);
        }

        public static Rect DesktopStatusRect(float width, float height)
        {
            bool compactHeight = height < 520f;
            return new Rect(25f, compactHeight ? 324f : height - 105f,
                Mathf.Max(180f, width - (compactHeight ? 220f : 60f)), 40f);
        }

        public static Rect DesktopIconRect(int index, float width)
        {
            const int columns = 5;
            float cellWidth = Mathf.Max(72f, (width - 40f) / columns);
            float iconWidth = Mathf.Min(104f, cellWidth - 8f);
            int column = index % columns;
            int row = index / columns;
            return new Rect(20f + column * cellWidth, 70f + row * 94f, iconWidth, 78f);
        }

        private void Open(string id)
        {
            DeskWindow existing = windows.FirstOrDefault(w => w.Id == id);
            if (existing != null)
            {
                existing.Minimized = false;
                Focus(existing);
                return;
            }
            int offset = windows.Count * 22;
            var window = new DeskWindow
            {
                Id = id, Title = appTitles[id], Rect = new Rect(490 + offset, 55 + offset, 710, 500)
            };
            windows.Add(window);
            window.Rect = ClampRect(window.Rect);
            SaveDesktop();
        }

        private void OpenWorkerDetail(int crewIndex)
        {
            Open("worker-detail");
            DeskWindow detail = windows.FirstOrDefault(window => window.Id == "worker-detail");
            if (detail == null) return;
            detail.Selected = Mathf.Clamp(crewIndex, 0, Mathf.Max(0, game.Crew.Count - 1));
            detail.Scroll = Vector2.zero;
            Focus(detail);
        }

        private void OpenTaskDetail(string taskId)
        {
            int taskIndex = game.Tasks.FindIndex(task => task.Id == taskId);
            if (taskIndex < 0) return;
            Open("task-detail");
            DeskWindow detail = windows.FirstOrDefault(window => window.Id == "task-detail");
            if (detail == null) return;
            WorkTask task = game.Tasks[taskIndex];
            detail.Selected = taskIndex;
            detail.SelectedCrew = task.AssignedCharacter >= 0 ? task.AssignedCharacter : 0;
            TaskScheduleEstimate estimate = game.EstimateSchedule(task.Id, detail.SelectedCrew);
            detail.ScheduleDay = task.ScheduledDay > 0
                ? task.ScheduledDay
                : estimate?.StartDay ?? game.Day;
            detail.Scroll = Vector2.zero;
            detail.Notice = null;
            Focus(detail);
        }

        private void SetEstimatedScheduleDay(DeskWindow window, WorkTask task)
        {
            TaskScheduleEstimate estimate = game.EstimateSchedule(task.Id, window.SelectedCrew);
            window.ScheduleDay = Mathf.Clamp(estimate?.StartDay ?? game.Day, game.Day, game.CampaignEndDay);
        }

        private void Close(string id)
        {
            windows.RemoveAll(w => w.Id == id);
            SaveDesktop();
        }

        private void Focus(DeskWindow window)
        {
            windows.Remove(window);
            windows.Add(window);
        }

        private void HandleWindowInput()
        {
            Event current = Event.current;
            if (current == null) return;

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                for (int i = windows.Count - 1; i >= 0; i--)
                {
                    if (windows[i].Minimized) continue;
                    Close(windows[i].Id);
                    current.Use();
                    return;
                }
            }

            DeskWindow resizing = windows.FirstOrDefault(window => window.Resizing);
            if (resizing != null && current.button == 0)
            {
                if (current.type == EventType.MouseDrag)
                {
                    resizing.Rect = CalculateResizedWindowRect(resizing.ResizeRectOrigin,
                        resizing.ResizePointerOrigin, current.mousePosition,
                        logicalWidth, logicalHeight);
                    current.Use();
                    return;
                }
                if (current.type == EventType.MouseUp)
                {
                    resizing.Resizing = false;
                    SaveDesktop();
                    current.Use();
                    return;
                }
            }

            if (current.type != EventType.MouseDown || current.button != 0) return;
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                DeskWindow window = windows[i];
                if (window.Minimized) continue;
                if (logicalWidth >= 900f && ResizeHandleRect(window.Rect).Contains(current.mousePosition))
                {
                    Focus(window);
                    window.Resizing = true;
                    window.ResizePointerOrigin = current.mousePosition;
                    window.ResizeRectOrigin = window.Rect;
                    current.Use();
                    return;
                }
                if (!window.Rect.Contains(current.mousePosition)) continue;
                Focus(window);
                return;
            }
        }

        public static Rect ResizeHandleRect(Rect windowRect) =>
            new Rect(windowRect.xMax - ResizeHandleReach,
                windowRect.yMax - ResizeHandleReach,
                ResizeHandleReach * 2f, ResizeHandleReach * 2f);

        public static Rect CalculateResizedWindowRect(Rect original, Vector2 pointerOrigin,
            Vector2 pointerCurrent, float desktopWidth, float desktopHeight)
        {
            Vector2 delta = pointerCurrent - pointerOrigin;
            float maxWidth = Mathf.Max(MinimumWindowWidth, desktopWidth - original.x - 6f);
            float maxHeight = Mathf.Max(MinimumWindowHeight, desktopHeight - original.y - 50f);
            original.width = Mathf.Clamp(original.width + delta.x, MinimumWindowWidth, maxWidth);
            original.height = Mathf.Clamp(original.height + delta.y, MinimumWindowHeight, maxHeight);
            return original;
        }

        private bool IsPointerBlockedBelowWindow(int windowIndex)
        {
            Event current = Event.current;
            if (current == null || !CanActivateControl(current.type)) return false;
            for (int i = windows.Count - 1; i > windowIndex; i--)
            {
                DeskWindow window = windows[i];
                if (IsPointInsideVisiblePanel(window.Rect, window.Minimized, current.mousePosition))
                    return true;
            }
            return false;
        }

        public static bool CanActivateControl(EventType eventType) =>
            eventType == EventType.MouseDown ||
            eventType == EventType.MouseDrag ||
            eventType == EventType.MouseUp;

        public static bool IsPointInsideVisiblePanel(Rect panel, bool minimized, Vector2 point) =>
            !minimized && panel.Contains(point);

        private void SetControlEnabled(bool enabled) =>
            GUI.enabled = !inputLayerBlocked && enabled;

        private static bool ExpandedHitButton(Rect visualRect, string label)
        {
            if (GUI.Button(visualRect, label)) return true;

            Event current = Event.current;
            if (current == null || current.type != EventType.MouseUp || current.button != 0)
                return false;

            Rect hitRect = ExpandHitRect(visualRect);
            if (!hitRect.Contains(current.mousePosition)) return false;
            current.Use();
            return true;
        }

        public static Rect ExpandHitRect(Rect visualRect)
        {
            return new Rect(visualRect.center.x - visualRect.width,
                visualRect.center.y - visualRect.height,
                visualRect.width * 2f, visualRect.height * 2f);
        }

        public static Rect WindowDragHitRect(float windowWidth)
        {
            return new Rect(0, 0, Mathf.Max(0, windowWidth - 95f), 52f);
        }

        private Rect ClampRect(Rect rect)
        {
            rect.width = Mathf.Clamp(rect.width, 420, Mathf.Max(420, logicalWidth - 12));
            rect.height = Mathf.Clamp(rect.height, 280, Mathf.Max(280, logicalHeight - 56));
            rect.x = Mathf.Clamp(rect.x, 6, Mathf.Max(6, logicalWidth - rect.width - 6));
            rect.y = Mathf.Clamp(rect.y, 6, Mathf.Max(6, logicalHeight - rect.height - 50));
            return rect;
        }

        private void AssignNext(WorkTask task)
        {
            int start = task.AssignedCharacter;
            for (int offset = 1; offset <= game.Crew.Count + 1; offset++)
            {
                int candidate = start + offset;
                if (candidate >= game.Crew.Count) candidate = -1;
                if (candidate < 0 || game.Crew[candidate].Available) { game.Assign(task.Id, candidate); return; }
            }
        }

        private void AssignNextParallel(WorkTask task)
        {
            int start = task.AssignedCharacter;
            for (int offset = 1; offset <= game.Crew.Count; offset++)
            {
                int candidate = (start + offset) % game.Crew.Count;
                if (game.AssignParallel(task.Id, candidate)) return;
            }
        }

        private string AssignedTask(int crewIndex)
        {
            string[] tasks = game.Tasks.Where(t => t.AssignedCharacter == crewIndex)
                .Select(t => t.IsParallelAssignment ? $"{t.Name}(병행)" : t.Name).ToArray();
            return tasks.Length == 0 ? "없음" : string.Join(", ", tasks);
        }

        private void EndTouchScroll(DeskWindow window, string region, ref Vector2 scroll)
        {
            GUILayout.EndScrollView();
            Rect viewport = GUILayoutUtility.GetLastRect();
            HandleTouchScroll(window, region, viewport, ref scroll);
        }

        private static void HandleTouchScroll(DeskWindow window, string region, Rect viewport,
            ref Vector2 scroll)
        {
            Event current = Event.current;
            if (current == null) return;

            if (current.type == EventType.MouseDown && current.button == 0 &&
                viewport.Contains(current.mousePosition) && string.IsNullOrEmpty(window.DragRegion))
            {
                window.DragRegion = region;
                window.DragPointerOrigin = current.mousePosition;
                window.DragScrollOrigin = scroll;
                window.DraggingContent = false;
                return;
            }

            if (window.DragRegion != region) return;
            if (current.type == EventType.MouseDrag && current.button == 0)
            {
                if (!window.DraggingContent &&
                    Vector2.Distance(window.DragPointerOrigin, current.mousePosition) >= TouchDragThreshold)
                {
                    window.DraggingContent = true;
                    GUIUtility.hotControl = 0;
                    GUIUtility.keyboardControl = 0;
                }
                if (!window.DraggingContent) return;
                scroll = CalculateDragScroll(window.DragScrollOrigin, window.DragPointerOrigin,
                    current.mousePosition);
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 0)
            {
                bool consumed = window.DraggingContent;
                window.DragRegion = null;
                window.DraggingContent = false;
                if (consumed) current.Use();
            }
        }

        public static Vector2 CalculateDragScroll(Vector2 originalScroll, Vector2 pointerOrigin,
            Vector2 pointerCurrent)
        {
            Vector2 delta = pointerCurrent - pointerOrigin;
            return new Vector2(
                Mathf.Max(0f, originalScroll.x - delta.x),
                Mathf.Max(0f, originalScroll.y - delta.y));
        }

        private void SaveCampaign() => ProjectWSaveStore.SaveCampaign(CampaignSaveKey, game.CreateSnapshot());

        private void SaveDesktop()
        {
            var snapshot = new DesktopSnapshot
            {
                SchemaVersion = ProjectWSaveStore.DesktopSchema,
                UiMagnification = uiMagnification,
                Windows = windows.Select((w, i) => new WindowSnapshot
                {
                    Id = w.Id, X = w.Rect.x, Y = w.Rect.y,
                    Width = w.Rect.width, Height = w.Rect.height,
                    Open = true, Minimized = w.Minimized, Order = i
                }).ToArray()
            };
            ProjectWSaveStore.SaveDesktop(DesktopSaveKey, snapshot);
        }

        private void RestoreDesktop()
        {
            if (!ProjectWSaveStore.TryLoadDesktop(DesktopSaveKey, out DesktopSnapshot snapshot)) return;
            uiMagnification = NormalizeUiMagnification(snapshot.UiMagnification);
            foreach (WindowSnapshot saved in snapshot.Windows.Where(w => w.Open).OrderBy(w => w.Order))
            {
                if (!appTitles.ContainsKey(saved.Id)) continue;
                windows.Add(new DeskWindow
                {
                    Id = saved.Id, Title = appTitles[saved.Id],
                    Rect = new Rect(saved.X, saved.Y,
                        saved.Width > 0f ? saved.Width : 710f,
                        saved.Height > 0f ? saved.Height : 500f),
                    Minimized = saved.Minimized
                });
            }
        }

        private void SaveAll() { SaveCampaign(); SaveDesktop(); }

        public static string FormatStatus(DayReport report, bool isWon, bool isLost)
        {
            if (isWon) return "마일스톤 완료 — 캠페인 승리";
            if (isLost) return "운영 붕괴 — 캠페인 실패";
            if (report == null || report.Lines.Count == 0) return string.Empty;
            return string.Join("\n", report.Lines.Skip(Math.Max(0, report.Lines.Count - 2)));
        }

        public static Rect ClampWindowRect(Rect rect, float width, float height)
        {
            rect.width = Mathf.Clamp(rect.width, 100, width);
            rect.height = Mathf.Clamp(rect.height, 100, height);
            rect.x = Mathf.Clamp(rect.x, 0, Mathf.Max(0, width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0, Mathf.Max(0, height - rect.height));
            return rect;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            Color white = Color.white;
            Color black = Color.black;
            Color gray = GrayColor;
            Color ink = InkColor;
            Texture2D whiteFill = Texture2D.whiteTexture;

            GUI.skin.label.normal.textColor = ink;
            GUI.skin.button.normal.background = whiteFill;
            GUI.skin.button.hover.background = whiteFill;
            GUI.skin.button.active.background = whiteFill;
            GUI.skin.button.focused.background = whiteFill;
            GUI.skin.button.normal.textColor = ink;
            GUI.skin.button.hover.textColor = ink;
            GUI.skin.button.active.textColor = ink;
            GUI.skin.button.focused.textColor = ink;
            GUI.skin.button.border = new RectOffset();
            GUI.skin.button.margin = new RectOffset(2, 2, 2, 2);
            GUI.skin.button.padding = new RectOffset(7, 7, 5, 5);

            GUI.skin.window.normal.background = whiteFill;
            GUI.skin.window.onNormal.background = whiteFill;
            GUI.skin.window.normal.textColor = black;
            GUI.skin.window.onNormal.textColor = black;
            GUI.skin.window.hover.textColor = black;
            GUI.skin.window.onHover.textColor = black;
            GUI.skin.window.active.textColor = black;
            GUI.skin.window.onActive.textColor = black;
            GUI.skin.window.focused.textColor = black;
            GUI.skin.window.onFocused.textColor = black;
            GUI.skin.window.border = new RectOffset();
            GUI.skin.window.padding = new RectOffset(8, 8, 24, 8);

            GUI.skin.box.normal.background = whiteFill;
            GUI.skin.box.normal.textColor = ink;
            GUI.skin.box.border = new RectOffset();
            GUI.skin.scrollView.normal.background = whiteFill;
            GUI.skin.verticalScrollbar = GUIStyle.none;

            title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            section = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(6, 6, 3, 3)
            };
            section.normal.background = whiteFill;
            section.normal.textColor = gray;
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            desktopIcon = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            warning = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
            warning.normal.textColor = ink;
            success = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
            success.normal.textColor = ink;
        }

        private static void DrawSolid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawHorizontalLine(float x1, float x2, float y, Color color)
        {
            float start = Mathf.Min(x1, x2);
            DrawSolid(new Rect(start, y, Mathf.Max(2f, Mathf.Abs(x2 - x1)), 2f), color);
        }

        private static void DrawSectionRule()
        {
            Rect rule = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            DrawSolid(rule, GrayColor);
            GUILayout.Space(5);
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, 1), color);
            DrawSolid(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            DrawSolid(new Rect(rect.x, rect.y, 1, rect.height), color);
            DrawSolid(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        private static string IconLabel(string id)
        {
            switch (id)
            {
                case "mail": return "MAIL\n통신";
                case "gantt": return "TASK\n작업";
                case "milestone": return "MILE\n마일스톤";
                case "workers": return "CREW\n대원";
                case "report": return "REPORT\n보고서";
                case "codex": return "CODEX\n도감";
                case "messenger": return "CHAT\n메신저";
                case "help": return "HELP\n도움말";
                case "options": return "OPTIONS\n옵션";
                default: return "INFO\n내정보";
            }
        }

        private static string RoleName(WorkRole role) => role == WorkRole.Tech ? "기술" : role == WorkRole.Analysis ? "분석" : role == WorkRole.Management ? "관리" : "적응";
        private static string StateName(TaskState state) => state == TaskState.Locked ? "잠김" : state == TaskState.Available ? "대기" : state == TaskState.Active ? "진행" : state == TaskState.Complete ? "완료" : "실패";
        private static string WorkStateName(WorkState state) => state == WorkState.Locked ? "잠김" :
            state == WorkState.Available ? "대기" : state == WorkState.InProgress ? "진행" :
            state == WorkState.Complete ? "완료" : "실패";
        private static string TaskLockReason(WorkTask task, WorkGroup work)
        {
            if (task.State != TaskState.Locked) return "없음";
            if (work == null) return "상위 일 없음";
            if (work.State == WorkState.Locked) return "선행 일 미완료";
            if (!string.IsNullOrEmpty(task.PrerequisiteId)) return $"선행 Task {task.PrerequisiteId} 미완료";
            return "진입 조건 미충족";
        }
        private string TaskRelationSummary(WorkTask task)
        {
            string owner = task.AssignedCharacter >= 0 && task.AssignedCharacter < game.Crew.Count
                ? game.Crew[task.AssignedCharacter].Name
                : "미배정";
            return $"{task.Name} · {task.Completion * 100:0}% · {owner} · HARD D{task.Deadline}";
        }
        private static string RiskName(RiskLevel risk) => risk == RiskLevel.High ? "높음" : risk == RiskLevel.Medium ? "보통" : "낮음";
        private static string ImportanceName(ImportanceLevel value) => value == ImportanceLevel.High ? "높음" : value == ImportanceLevel.Medium ? "보통" : "낮음";
    }
}
