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
            public string DragRegion;
            public Vector2 DragPointerOrigin;
            public Vector2 DragScrollOrigin;
            public bool DraggingContent;
        }

        private readonly List<DeskWindow> windows = new List<DeskWindow>();
        private readonly Dictionary<string, string> appTitles = new Dictionary<string, string>
        {
            { "mail", "MAIL / 통신" }, { "gantt", "GANTT / 작업" }, { "milestone", "MILESTONE" },
            { "workers", "CREW / 대원" }, { "report", "REPORT" }, { "codex", "CODEX / 도감" },
            { "help", "HELP" }, { "profile", "MY INFO" }, { "log", "SYSTEM LOG" }
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
            float scale = Mathf.Clamp(Screen.width / 1280f, 0.72f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            logicalWidth = Screen.width / scale;
            logicalHeight = Screen.height / scale;
            DrawDesktop();
            DrawWindows();
            DrawTaskbar();
            if (Event.current.type == EventType.MouseUp) SaveDesktop();
        }

        private void DrawDesktop()
        {
            GUI.DrawTexture(new Rect(0, 0, logicalWidth, logicalHeight), Texture2D.whiteTexture);
            GUI.Label(new Rect(22, 15, 480, 34), "PROJECT W  /  OPERATIONS DESK", title);
            GUI.Label(new Rect(logicalWidth - 250, 18, 225, 25), $"DAY {game.Day:00}/{game.CampaignEndDay}   PATCH {patchVersion}", small);
            string[] ids = { "mail", "gantt", "milestone", "workers", "report", "codex", "help", "profile" };
            for (int i = 0; i < ids.Length; i++)
            {
                int column = i % 4;
                int row = i / 4;
                var rect = new Rect(25 + column * 116, 70 + row * 94, 104, 78);
                if (GUI.Button(rect, IconLabel(ids[i]), desktopIcon)) Open(ids[i]);
            }
            OperationsReport report = game.BuildReport();
            GUI.Label(new Rect(25, 275, 455, 80),
                $"운영 현황\n진행 {report.Active}  |  완료 {report.Complete}/{game.Tasks.Count}  |  지연 {report.Delayed}  |  고위험 {report.HighRisk}\n" +
                $"가용 대원 {game.Crew.Count(c => c.Available)}/{game.Crew.Count}  |  자원 {game.Resources}", section);
            GUI.enabled = !game.IsWon && !game.IsLost;
            if (GUI.Button(new Rect(25, 365, 210, 48), "하루 진행"))
            {
                game.AdvanceDay();
                SaveCampaign();
            }
            GUI.enabled = true;
            GUI.Label(new Rect(25, logicalHeight - 105, 650, 40), FormatStatus(game.LastReport, game.IsWon, game.IsLost),
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
                window.Rect = GUI.Window(100 + i, window.Rect, _ => DrawWindow(window), window.Title);
            }
        }

        private void DrawWindow(DeskWindow window)
        {
            DrawBorder(new Rect(0, 0, window.Rect.width, window.Rect.height), InkColor);
            if (GUI.Button(new Rect(window.Rect.width - 58, 2, 25, 20), "—")) { window.Minimized = true; SaveDesktop(); }
            if (GUI.Button(new Rect(window.Rect.width - 30, 2, 25, 20), "X")) { Close(window.Id); return; }
            GUILayout.Space(6);
            switch (window.Id)
            {
                case "mail": DrawMail(window); break;
                case "gantt": DrawGantt(window); break;
                case "milestone": DrawMilestones(window); break;
                case "workers": DrawWorkers(window); break;
                case "report": DrawReport(window); break;
                case "codex": DrawCodex(window); break;
                case "help": DrawHelp(); break;
                case "profile": DrawProfile(); break;
                case "log": DrawLog(window); break;
            }
            GUI.DragWindow(new Rect(0, 0, Mathf.Max(0, window.Rect.width - 65), 26));
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
                GUI.enabled = !mail.Resolved;
                if (GUILayout.Button(mail.Resolved ? "처리 완료" : "지시 수락 및 반영", GUILayout.Height(38)))
                {
                    game.ResolveMail(mail.Id);
                    SaveCampaign();
                }
                GUI.enabled = true;
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
                DrawDeadlineLine(group.SoftDeadline, y, rowHeight * (tasks.Count + 1),
                    dayWidth, false);
                DrawDeadlineLine(group.HardDeadline, y, rowHeight * (tasks.Count + 1),
                    dayWidth, true);
                y += rowHeight;

                foreach (WorkTask task in tasks)
                {
                    int completedDays = Mathf.CeilToInt(task.Progress);
                    int startDay = Mathf.Max(1, game.Day - completedDays);
                    float completedWidth = completedDays * dayWidth;
                    float remainingWidth = Mathf.Ceil(task.RemainingWork) * dayWidth;
                    float barX = (startDay - 1) * dayWidth + 3;
                    if (completedWidth > 0)
                        DrawSolid(new Rect(barX, y + 7, completedWidth, 14), GrayColor);
                    float remainingX = (Mathf.Max(1, game.Day) - 1) * dayWidth + 3;
                    if (remainingWidth > 0)
                        DrawSolid(new Rect(remainingX, y + 7, remainingWidth, 14),
                            task.State == TaskState.Locked ? PaleColor : InkColor);
                    GUI.Label(new Rect(Math.Max(barX, remainingX) + 3, y + 4,
                            Math.Max(54, remainingWidth - 4), 21),
                        $"{task.RemainingWork:0.#}d", small);
                    y += rowHeight;
                }
            }
            GUI.EndScrollView();
            HandleTouchScroll(window, "gantt-timeline", timelineViewport, ref window.TimelineScroll);

            GUI.BeginGroup(labelViewport);
            DrawSolid(new Rect(0, 0, labelWidth, labelViewport.height), Color.white);
            y = 28f - window.TimelineScroll.y;
            foreach (WorkGroup group in game.Groups)
            {
                List<WorkTask> tasks = game.Tasks.Where(task => task.GroupId == group.Id).ToList();
                DrawSolid(new Rect(0, y, labelWidth, rowHeight - 1), PaleColor);
                GUI.Label(new Rect(6, y + 4, labelWidth - 10, 22),
                    $"{group.Name} · {WorkStateName(group.State)}", small);
                y += rowHeight;

                foreach (WorkTask task in tasks)
                {
                    GUI.Label(new Rect(6, y + 4, labelWidth - 10, rowHeight - 4),
                        $"{StateName(task.State)}  {task.Name}", small);
                    y += rowHeight;
                }
            }
            GUI.EndGroup();
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
            int matchingWorker = game.Crew.FindIndex(member => member.Specialty == task.RequiredRole);
            TaskCostPreview cost = game.BuildCostPreview(task, matchingWorker);
            string assignee = task.AssignedCharacter < 0
                ? "미배정"
                : $"{game.Crew[task.AssignedCharacter].Name} / {(task.IsParallelAssignment ? "병행" : "주 작업")}";

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{task.Name}  ·  {(task.Required ? "필수" : "선택")}", section);
            GUILayout.Label(
                $"{work?.Name ?? "소속 없음"} / {WorkStateName(work?.State ?? WorkState.Locked)}   " +
                $"역할 {RoleName(task.RequiredRole)}   위험 {RiskName(game.EffectiveRisk(task))}", small);
            GUILayout.HorizontalSlider(task.Completion, 0, 1);
            GUILayout.Label(
                $"진행 {task.Progress:0.#}일 / 유효 {task.EffectiveRequiredWork:0.#}일   " +
                $"잔여 {task.RemainingWork:0.#}일", small);

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

            GUILayout.Label($"현재 담당: {assignee}", section);
            GUILayout.BeginHorizontal();
            GUI.enabled = task.State == TaskState.Available || task.State == TaskState.Active;
            if (GUILayout.Button("주 작업 담당 순환")) { AssignNext(task); SaveCampaign(); }
            GUI.enabled = (task.State == TaskState.Available || task.State == TaskState.Active) &&
                          cost.CanRunInParallel;
            if (GUILayout.Button("병행 담당 순환")) { AssignNextParallel(task); SaveCampaign(); }
            GUI.enabled = task.AssignedCharacter >= 0;
            if (GUILayout.Button("배정 해제")) { game.Assign(task.Id, -1); SaveCampaign(); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label("최근 기록", section);
            if (task.Records == null || task.Records.Count == 0) GUILayout.Label("기록 없음", small);
            else
                foreach (TaskRecord record in task.Records.Skip(Math.Max(0, task.Records.Count - 4)))
                    GUILayout.Label($"D{record.Day:00}  {record.Actor}  {record.Text}", small);
            GUILayout.EndVertical();
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
                    GUILayout.Label($"{(task.Required ? "[필수]" : "[선택]")} {task.Name} — {StateName(task.State)} {task.Progress}/{task.RequiredWork}");
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
                GUILayout.Label($"{member.Name}   {RoleName(member.Specialty)} / SKILL {member.Skill} / EXP {member.Experience}", section);
                GUILayout.Label($"상태 {member.Condition}   피로 {member.Fatigue}%   담당 {AssignedTask(i)}");
                GUILayout.HorizontalSlider(member.Fatigue, 0, 100);
                if (member.History.Count > 0) GUILayout.Label($"최근: {member.History[member.History.Count - 1]}", small);
                GUILayout.BeginHorizontal();
                GUI.enabled = member.InjuryDays <= 0 && !member.RestScheduled;
                if (GUILayout.Button(member.RestScheduled ? "휴식 예약됨" : "휴식 예약")) { game.Rest(i); SaveCampaign(); }
                GUI.enabled = game.Resources >= 3;
                if (GUILayout.Button($"재생 시술 {game.RegenerationResourceCost}자원 ({member.RegenerationCount})")) { game.Regenerate(i); SaveCampaign(); }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            EndTouchScroll(window, "workers", ref window.Scroll);
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
            GUILayout.Label("• 바탕화면 아이콘을 한 번 탭해 앱을 엽니다.\n• 창 제목을 드래그해 이동합니다.\n• 창 내용은 스크롤바 또는 빈 곳을 밀어서 이동합니다.\n• Gantt에서는 왼쪽 일감 열이 고정되고 날짜 영역만 움직입니다.\n• 주 작업은 하루 하나, 잔여 1일 이하 작업은 피로를 더 써서 병행할 수 있습니다.\n• 통신 지시를 수락하면 마감·중요도·자원이 실제 게임에 반영됩니다.\n• 내정보에서 캠페인 또는 창 배치를 각각 초기화할 수 있습니다.");
        }

        private void DrawProfile()
        {
            GUILayout.Label("내 정보 / 캠페인 관리", section);
            GUILayout.Label($"PROJECT W 운영 담당자\nDAY {game.Day}/{game.CampaignEndDay}\n자원 {game.Resources}\n패치 {patchVersion}");
            GUILayout.Space(12);
            if (GUILayout.Button("창 위치 및 열린 상태 초기화", GUILayout.Height(38)))
            {
                ProjectWSaveStore.Delete(DesktopSaveKey);
                windows.Clear();
            }
            if (GUILayout.Button("새 캠페인 시작", GUILayout.Height(38)))
            {
                ProjectWSaveStore.Delete(CampaignSaveKey);
                game = new MilestoneSimulation();
                SaveCampaign();
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
            HandleTouchScroll(window, region, GUILayoutUtility.GetLastRect(), ref scroll);
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
                Windows = windows.Select((w, i) => new WindowSnapshot
                {
                    Id = w.Id, X = w.Rect.x, Y = w.Rect.y, Open = true, Minimized = w.Minimized, Order = i
                }).ToArray()
            };
            ProjectWSaveStore.SaveDesktop(DesktopSaveKey, snapshot);
        }

        private void RestoreDesktop()
        {
            if (!ProjectWSaveStore.TryLoadDesktop(DesktopSaveKey, out DesktopSnapshot snapshot)) return;
            foreach (WindowSnapshot saved in snapshot.Windows.Where(w => w.Open).OrderBy(w => w.Order))
            {
                if (!appTitles.ContainsKey(saved.Id)) continue;
                windows.Add(new DeskWindow
                {
                    Id = saved.Id, Title = appTitles[saved.Id],
                    Rect = new Rect(saved.X, saved.Y, 710, 500), Minimized = saved.Minimized
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
                case "help": return "HELP\n도움말";
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
        private static string RiskName(RiskLevel risk) => risk == RiskLevel.High ? "높음" : risk == RiskLevel.Medium ? "보통" : "낮음";
        private static string ImportanceName(ImportanceLevel value) => value == ImportanceLevel.High ? "높음" : value == ImportanceLevel.Medium ? "보통" : "낮음";
    }
}
