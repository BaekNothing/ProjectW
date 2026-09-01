using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectW.Contracts;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ProjectW.MilestonePrototype
{
    public sealed class MilestonePrototypeController : MonoBehaviour
    {
        private sealed class DeskWindow
        {
            public int GuiId;
            public string Id;
            public string Title;
            public Rect Rect;
            public bool Minimized;
            public Vector2 Scroll;
            public Vector2 TimelineScroll;
            public int Selected;
            public int SelectedCrew;
            public int ScheduleDay;
            public List<string> CollapsedCodexCategories = new List<string>();
            public bool InheritRegenerationAbilities;
            public bool InheritRegenerationPerks;
            public string Notice;
            public bool Resizing;
            public Vector2 ResizePointerOrigin;
            public Rect ResizeRectOrigin;
        }

        private readonly List<DeskWindow> windows = new List<DeskWindow>();
        private readonly Dictionary<string, string> appTitles = new Dictionary<string, string>
        {
            { "mail", "MAIL / 통신" }, { "gantt", "GANTT / 작업" }, { "milestone", "MILESTONE" },
            { "workers", "CREW / 대원" }, { "report", "REPORT" }, { "codex", "CODEX / 도감" },
            { "profile", "MY INFO" }, { "log", "SYSTEM LOG" },
            { "messenger", "MESSENGER / 메신저" },
            { "worker-detail", "CREW PROFILE / 대원 상세" },
            { "task-detail", "TASK DETAIL / 작업 상세" },
            { "proposal", "PROPOSAL / 제안서" },
            { "priority", "PRIORITY / 우선순위" },
            { "options", "OPTIONS / 옵션" }
        };
        private readonly string[] desktopBadgeAppIds =
        {
            "mail", "gantt", "milestone", "workers", "report",
            "codex", "messenger", "profile", "options"
        };
        private readonly int[] desktopBadgeCounts = new int[10];
        private int proposalTargetIndex;
        private int proposalTaskCount = 2;
        private readonly int[] proposalActionIndices = new int[4];
        private ProposalPitch proposalPitch = ProposalPitch.Stability;
        private string proposalNotice;
        private bool proposalCustomMode;

        private MilestoneSimulation game;
        private GUIStyle title;
        private GUIStyle desktopIcon;
        private GUIStyle desktopIconLabel;
        private GUIStyle desktopBadge;
        private DeskWindow frontWindow;
        private int nextWindowGuiId = 100;
        private GUIStyle section;
        private GUIStyle small;
        private GUIStyle warning;
        private GUIStyle success;
        private string patchVersion = "embedded";
        private IPatchDiagnostics patchDiagnostics;
        private const string CampaignSaveKey = "projectw.campaign.v1";
        private const string DesktopSaveKey = "projectw.desktop.v1";
        private static readonly Color GrayColor = new Color(.6f, .6f, .6f, 1f);
        private static readonly Color DimmedGrayColor = new Color(.6f, .6f, .6f, .45f);
        private static readonly Color InkColor = new Color(.267f, .267f, .267f, 1f);
        private static readonly Color PaleColor = new Color(.88f, .88f, .88f, 1f);
        private static readonly Color WeekendColor = new Color(.94f, .92f, .88f, 1f);
        private static readonly string[] CompetencyNames =
        {
            "기지공학", "과학탐사", "자원운용", "환경적응", "생명유지", "지휘교섭"
        };
        public const float DefaultScrollbarWidth = 16f;
        private const float ResizeHandleReach = 40f;
        private const float MinimumWindowWidth = 420f;
        private const float MinimumWindowHeight = 280f;
        public const float DefaultUiMagnification = 1.8f;
        private float uiMagnification = DefaultUiMagnification;
        private bool inputLayerBlocked;
        private bool modalInputBlocked;
        private float logicalWidth;
        private float logicalHeight;
        private DeskWindow pinchWindow;
        private Rect pinchRectOrigin;
        private Vector2 pinchCenterOrigin;
        private float pinchDistanceOrigin;
        private int messengerSeenUpdateCount;
        private bool titleScreen = true;
        private bool titleOptions;
        private bool confirmCampaignReset;
        private bool confirmDesktopReset;
        private bool confirmDataReload;
        private bool confirmOverrideReset;
        private TaskSystemData editorData;
        private string editorNotice;
        private string categoryTransferText = string.Empty;
        private readonly TimedNotificationPresenter notifications = new TimedNotificationPresenter();
        public const float WindowTitleBarHeight = 25f;
        public const float WindowContentTopSpacing = 6f;
        public const float GanttWindowChromeReserve = 110f;

        private void Awake()
        {
            appTitles["data"] = "GAME DATA";
            game = new MilestoneSimulation();
            if (ProjectWSaveStore.TryLoadCampaign(CampaignSaveKey, out CampaignSnapshot snapshot)) game.Restore(snapshot);
            RestoreDesktop();
        }

        public void Initialize(string version, IPatchDiagnostics diagnostics, string dataPath)
        {
            patchVersion = string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim();
            patchDiagnostics = diagnostics;
            if (!string.IsNullOrWhiteSpace(dataPath)) StartCoroutine(LoadRemoteEffectAssets(dataPath));
        }

        private IEnumerator LoadRemoteEffectAssets(string dataPath)
        {
            string catalogPath = Directory.GetFiles(dataPath, "catalog*.bin").FirstOrDefault() ??
                                 Directory.GetFiles(dataPath, "catalog*.json").FirstOrDefault();
            if (string.IsNullOrEmpty(catalogPath)) yield break;

            Addressables.InternalIdTransformFunc = location =>
            {
                string internalId = location.InternalId;
                int query = internalId.IndexOf('?');
                if (query >= 0) internalId = internalId.Substring(0, query);
                int slash = Math.Max(internalId.LastIndexOf('/'), internalId.LastIndexOf('\\'));
                string localPath = Path.Combine(dataPath, slash >= 0 ? internalId.Substring(slash + 1) : internalId);
                return File.Exists(localPath) ? localPath : location.InternalId;
            };

            AsyncOperationHandle catalog = Addressables.LoadContentCatalogAsync(catalogPath, false);
            yield return catalog;
            if (catalog.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"Remote effect catalog failed: {catalog.OperationException?.Message}");
                yield break;
            }

            AsyncOperationHandle<Texture2D> radial = Addressables.LoadAssetAsync<Texture2D>("effects/radial-trapezoid");
            AsyncOperationHandle<Texture2D> sparkle = Addressables.LoadAssetAsync<Texture2D>("effects/sparkle");
            AsyncOperationHandle<Texture2D> ring = Addressables.LoadAssetAsync<Texture2D>("effects/ring");
            AsyncOperationHandle<Texture2D> glow = Addressables.LoadAssetAsync<Texture2D>("effects/glow");
            yield return radial;
            yield return sparkle;
            yield return ring;
            yield return glow;
            if (radial.Status == AsyncOperationStatus.Succeeded &&
                sparkle.Status == AsyncOperationStatus.Succeeded &&
                ring.Status == AsyncOperationStatus.Succeeded &&
                glow.Status == AsyncOperationStatus.Succeeded)
            {
                notifications.SetEffectTextures(radial.Result, sparkle.Result, ring.Result, glow.Result);
                Debug.Log("Remote effect assets loaded from the active patch slot.");
            }
            else Debug.LogWarning("One or more remote effect textures failed to load.");
        }

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
            if (titleScreen)
            {
                DrawTitleScreen();
                return;
            }
            float notificationTime = Time.unscaledTime;
            notifications.Update(notificationTime);
            modalInputBlocked = notifications.HasPopup;
            if (!modalInputBlocked) HandleWindowInput();
            inputLayerBlocked = IsPointerBlockedBelowWindow(-1);
            GUI.enabled = !inputLayerBlocked && !modalInputBlocked;
            DrawDesktop();
            DrawWindows();
            inputLayerBlocked = IsPointerBlockedBelowWindow(-1);
            GUI.enabled = !inputLayerBlocked && !modalInputBlocked;
            DrawTaskbar();
            inputLayerBlocked = false;
            GUI.enabled = true;
            notifications.Draw(logicalWidth, logicalHeight, notificationTime);
            modalInputBlocked = false;
            if (Event.current.type == EventType.MouseUp) SaveDesktop();
        }

        private void DrawTitleScreen()
        {
            GUI.DrawTexture(new Rect(0, 0, logicalWidth, logicalHeight), Texture2D.whiteTexture);
            float panelWidth = Mathf.Min(620f, logicalWidth - 40f);
            float x = (logicalWidth - panelWidth) * .5f;
            GUI.Label(new Rect(x, logicalHeight * .2f, panelWidth, 60f), "PROJECT W", title);
            GUI.Label(new Rect(x, logicalHeight * .2f + 55f, panelWidth, 32f),
                $"OPERATIONS CONTROL  ·  {patchVersion}", section);
            if (!titleOptions)
            {
                if (Button(new Rect(x, logicalHeight * .48f, panelWidth * .62f, 62f), "PRESS TO START"))
                    titleScreen = false;
                if (Button(new Rect(x + panelWidth * .65f, logicalHeight * .48f, panelWidth * .35f, 62f), "OPTIONS"))
                    titleOptions = true;
                return;
            }

            GUILayout.BeginArea(new Rect(x, logicalHeight * .36f, panelWidth, Mathf.Min(410f, logicalHeight * .58f)));
            GUILayout.Label("STARTUP OPTIONS", title);
            DrawUpdateControls();
            GUILayout.Space(12f);
            if (LayoutButton(confirmCampaignReset ? "CONFIRM: RESET CAMPAIGN" : "RESET CAMPAIGN", GUILayout.Height(40)))
            {
                if (confirmCampaignReset)
                {
                    ProjectWSaveStore.Delete(CampaignSaveKey);
                    game = new MilestoneSimulation();
                    confirmCampaignReset = false;
                }
                else confirmCampaignReset = true;
            }
            if (LayoutButton(confirmDesktopReset ? "CONFIRM: RESET DESKTOP" : "RESET DESKTOP", GUILayout.Height(40)))
            {
                if (confirmDesktopReset)
                {
                    ProjectWSaveStore.Delete(DesktopSaveKey);
                    windows.Clear();
                    confirmDesktopReset = false;
                }
                else confirmDesktopReset = true;
            }
            if (LayoutButton("BACK", GUILayout.Height(40))) titleOptions = false;
            GUILayout.EndArea();
        }

        private void DrawDesktop()
        {
            GUI.DrawTexture(new Rect(0, 0, logicalWidth, logicalHeight), Texture2D.whiteTexture);
            GUI.Label(new Rect(22, 15, Mathf.Max(220f, logicalWidth - 300f), 34),
                "PROJECT W  /  OPERATIONS DESK", title);
            GUI.Label(new Rect(logicalWidth - 250, 18, 225, 25),
                $"DAY {game.Day:00} ({MilestoneSimulation.WeekdayName(game.CurrentWeekday)})  ·  생존 기록", small);
            string[] ids = { "mail", "proposal", "priority", "gantt", "milestone", "workers", "report", "codex", "messenger", "profile", "options", "data" };
            RefreshDesktopBadges();
            for (int i = 0; i < ids.Length; i++)
            {
                Rect iconRect = DesktopIconRect(i, logicalWidth);
                if (Button(iconRect, DesktopIconGlyph(ids[i]), desktopIcon)) Open(ids[i]);
                GUI.Label(DesktopIconLabelRect(i, logicalWidth), DesktopIconName(ids[i]), desktopIconLabel);
                int badgeCount = DesktopBadgeCount(ids[i]);
                if (badgeCount > 0)
                    GUI.Label(DesktopIconBadgeRect(i, logicalWidth), $"({badgeCount})", desktopBadge);
            }
            OperationsReport report = game.BuildReport();
            GUI.Label(DesktopReportRect(logicalWidth, logicalHeight),
                $"운영 현황\n진행 {report.Active}  |  완료 {report.Complete}/{game.Tasks.Count}  |  지연 {report.Delayed}  |  고위험 {report.HighRisk}\n" +
                $"가용 대원 {game.Crew.Count(c => c.Available)}/{game.Crew.Count}  |  자원 {game.Resources}", section);
            if (logicalHeight >= 520f)
            {
                SetControlEnabled(!game.IsWon && !game.IsLost && !game.MustResolveCriticalChoice);
                if (Button(new Rect(25, 365, 210, 48), "하루 진행"))
                    AdvanceToNextDay();
                SetControlEnabled(true);
            }
            GUI.Label(DesktopStatusRect(logicalWidth, logicalHeight),
                game.MustResolveCriticalChoice
                    ? "[!중요!] 오늘 회신해야 다음 날로 진행할 수 있습니다."
                    : game.HasPendingCriticalChoice
                        ? $"[!중요!] 회신 기한 · DAY {game.CriticalResponseDeadlineDay}"
                    : game.HasActiveCriticalEvent
                        ? $"중요 이벤트 후속 보고 대기 · DAY {game.ActiveCriticalNodeArrivalDay}"
                    : FormatStatus(game.LastReport, game.IsWon, game.IsLost),
                game.IsLost || game.MustResolveCriticalChoice ? warning : success);
        }

        private void DrawWindows()
        {
            for (int i = 0; i < windows.Count; i++)
            {
                DeskWindow window = windows[i];
                if (window.Minimized) continue;
                window.Rect = ClampRect(window.Rect);
                inputLayerBlocked = IsPointerBlockedBelowWindow(i);
                GUI.enabled = !inputLayerBlocked && !modalInputBlocked;
                window.Rect = GUI.Window(window.GuiId, window.Rect, _ => DrawWindow(window), window.Title);
            }
            if (frontWindow != null && !frontWindow.Minimized)
                GUI.BringWindowToFront(frontWindow.GuiId);
            inputLayerBlocked = false;
            GUI.enabled = !modalInputBlocked;
        }

        private void DrawWindow(DeskWindow window)
        {
            DrawBorder(new Rect(0, 0, window.Rect.width, window.Rect.height), InkColor);
            DrawSolid(new Rect(1, WindowTitleBarHeight, window.Rect.width - 2, 1), InkColor);
            Rect minimizeRect = WindowMinimizeButtonRect(window.Rect.width);
            Rect closeRect = WindowCloseButtonRect(window.Rect.width);
            if (ExpandedHitButton(minimizeRect, "—"))
            {
                window.Minimized = true;
                ActivateFrontmostVisibleWindow();
                SaveDesktop();
            }
            if (ExpandedHitButton(closeRect, "X")) { Close(window.Id); return; }
            GUILayout.Space(WindowContentTopSpacing);
            bool usesIndependentScroll = UsesIndependentWindowScroll(window.Id);
            if (!usesIndependentScroll)
                window.Scroll = GUILayout.BeginScrollView(window.Scroll);
            switch (window.Id)
            {
                case "mail": DrawMail(window); break;
                case "proposal": DrawProposal(window); break;
                case "priority": DrawPriority(window); break;
                case "gantt": DrawGantt(window); break;
                case "milestone": DrawMilestones(window); break;
                case "workers": DrawWorkers(window); break;
                case "messenger": DrawMessenger(window); break;
                case "worker-detail": DrawWorkerDetail(window); break;
                case "task-detail": DrawTaskDetail(window); break;
                case "report": DrawReport(window); break;
                case "codex": DrawCodex(window); break;
                case "profile": DrawProfile(); break;
                case "options": DrawOptions(window); break;
                case "data": DrawDataEditor(window); break;
                case "log": DrawLog(window); break;
            }
            if (!usesIndependentScroll)
                GUILayout.EndScrollView();
            GUI.DragWindow(WindowDragHitRect(window.Rect.width));
        }

        public static bool UsesIndependentWindowScroll(string windowId) => windowId == "gantt";

        private void DrawMail(DeskWindow window)
        {
            List<MailEvent> arrived = game.Mail.Where(m => m.ArrivalDay <= game.Day &&
                    !game.IsWeeklyFieldIncidentMail(m))
                .OrderBy(m => m.Read ? 1 : 0)
                .ThenByDescending(m => m.ArrivalDay)
                .ToList();
            if (!string.IsNullOrEmpty(window.Notice))
            {
                int selectedMail = arrived.FindIndex(mail => mail.Id == window.Notice);
                if (selectedMail >= 0) window.Selected = selectedMail;
            }
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Min(230, window.Rect.width * .36f)));
            for (int i = 0; i < arrived.Count; i++)
            {
                MailEvent mail = arrived[i];
                string prefix = mail.Resolved ? "[완료] " : mail.Read ? "" : "[NEW] ";
                if (LayoutMailButton($"{prefix}{mail.Subject}\n{mail.From}", mail.Resolved,
                        GUILayout.Height(55)))
                {
                    window.Selected = i;
                    window.Notice = mail.Id;
                    game.MarkMailRead(mail.Id);
                    SaveCampaign();
                }
            }
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
                if (mail.IsCritical && !mail.Resolved)
                {
                    CriticalEventNode node = game.ActiveCriticalNode();
                    if (node != null && node.Id == mail.CriticalNodeId)
                    {
                        foreach (CriticalEventChoice choice in node.Choices)
                        {
                            GUILayout.Space(4);
                            GUILayout.Label(choice.Forecast, small);
                            if (LayoutButton(choice.Text, GUILayout.Height(38)))
                            {
                                game.ChooseCriticalEvent(choice.Id);
                                window.Notice = null;
                                SaveCampaign();
                                break;
                            }
                        }
                    }
                }
                else
                {
                    if (mail.IsWeeklyFieldReport)
                    {
                        DrawWeeklyFieldReport(mail);
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                        return;
                    }
                    if (mail.IsProposal && !mail.Resolved)
                    {
                        GUILayout.Label("이 메일은 검토 결과입니다. 답변이나 수정 제출은 제안서 앱에서 진행하세요.", small);
                        if (LayoutButton("제안서 앱 열기", GUILayout.Height(38))) Open("proposal");
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                        return;
                    }
                    SetControlEnabled(!mail.Resolved);
                    string actionLabel = mail.ActivatesWork
                        ? mail.Resolved ? "미션 수락 완료" : "미션 수락"
                        : mail.Resolved ? "처리 완료" : "지시 수락 및 반영";
                    if (mail.IsMedicalReport)
                        actionLabel = mail.Resolved ? "검진 파일 갱신 완료" : "검진 파일 다운로드";
                    if (LayoutButton(actionLabel, GUILayout.Height(38)))
                    {
                        game.ResolveMail(mail.Id);
                        SaveCampaign();
                    }
                    SetControlEnabled(true);
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawWeeklyFieldReport(MailEvent mail)
        {
            if (mail.WeeklyFieldItems == null || mail.WeeklyFieldItems.Length == 0)
            {
                GUILayout.Label("검토할 현장 안건이 없습니다.", small);
                return;
            }
            foreach (WeeklyFieldDecisionItem item in mail.WeeklyFieldItems)
            {
                GUILayout.Space(6);
                string state = item.Decided ? item.Approved ? "[승인]" : "[무시]" : "[대기]";
                GUILayout.Label($"{state} {item.Subject}", section);
                GUILayout.Label($"{item.From} · 일정 {(item.DeadlineDelta > 0 ? "+" : "")}{item.DeadlineDelta}일", small);
                GUILayout.Label(item.Body);
                GUILayout.Label(item.Instruction, warning);
                if (!item.Decided)
                {
                    GUILayout.BeginHorizontal();
                    if (LayoutButton("승인", GUILayout.Height(34)))
                    {
                        game.DecideWeeklyFieldItem(mail.Id, item.SourceMailId, true);
                        SaveCampaign();
                    }
                    if (LayoutButton("무시", GUILayout.Height(34)))
                    {
                        game.DecideWeeklyFieldItem(mail.Id, item.SourceMailId, false);
                        SaveCampaign();
                    }
                    GUILayout.EndHorizontal();
                }
                DrawSectionRule();
            }
        }

        private void DrawProposal(DeskWindow window)
        {
            RandomTaskTarget[] targets = game.ProposalTargets;
            RandomTaskAction[] actions = game.ProposalActions;
            if (targets == null || targets.Length == 0 || actions == null || actions.Length == 0)
            {
                GUILayout.Label("제안서 구성 데이터가 없습니다.", warning);
                return;
            }
            List<MailEvent> questions = game.Mail.Where(mail => mail.IsProposal &&
                mail.ProposalStage == ProposalStage.Question && !mail.Resolved && mail.ArrivalDay <= game.Day).ToList();
            foreach (MailEvent question in questions)
            {
                GUILayout.Label($"보완 요청 · {question.Subject}", warning);
                GUILayout.Label(question.Body);
                GUILayout.BeginHorizontal();
                if (LayoutButton("안정성 보완")) { game.RespondToProposal(question.Id, ProposalPitch.Stability); SaveCampaign(); }
                if (LayoutButton("성장성 보완")) { game.RespondToProposal(question.Id, ProposalPitch.Growth); SaveCampaign(); }
                if (LayoutButton("효율 보완")) { game.RespondToProposal(question.Id, ProposalPitch.Efficiency); SaveCampaign(); }
                if (LayoutButton("안 맡음 의견")) { game.RespondToProposal(question.Id, ProposalPitch.Decline); SaveCampaign(); }
                GUILayout.EndHorizontal();
                DrawSectionRule();
            }
            if (!proposalCustomMode)
            {
                GUILayout.Label("READY-MADE PROPOSALS", title);
                GUILayout.Label($"2~3주마다 새로운 후보가 도착합니다. 다음 묶음 예정: DAY {game.NextProposalBatchDay}", small);
                foreach (ReadyMadeProposal proposal in game.ReadyMadeProposals.OrderBy(item => item.ExpiresDay).ToList())
                {
                    RandomTaskTarget target = targets.FirstOrDefault(item => item.Id == proposal.TargetId);
                    string steps = string.Empty;
                    foreach (string actionId in proposal.ActionIds)
                    {
                        RandomTaskAction action = actions.FirstOrDefault(item => item.Id == actionId);
                        if (action == null) continue;
                        if (steps.Length > 0) steps += " → ";
                        steps += action.Text;
                    }
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label($"{target?.Text ?? "알 수 없는 목표"} 개선안", section);
                    GUILayout.Label(steps);
                    GUILayout.Label($"작업량 {proposal.TotalWork:0.0}일 · 투자비 {proposal.CostCredits} · 보상 {proposal.RewardCredits} · 위험 {RiskName(proposal.Risk)}", small);
                    GUILayout.Label($"DAY {proposal.ExpiresDay} 소멸 · 승인 후 소프트 +{proposal.SoftDurationDays}일 / 하드 +{proposal.HardDurationDays}일", warning);
                    SetControlEnabled(game.Day <= proposal.ExpiresDay && game.Resources >= proposal.CostCredits);
                    bool submitted = false;
                    if (LayoutButton("이 안을 사장에게 제안", GUILayout.Height(38)))
                    {
                        submitted = game.SubmitReadyMadeProposal(proposal.Id);
                        proposalNotice = submitted
                            ? "제출 완료. 같은 묶음의 다른 후보는 닫혔습니다. 결과는 다음 날 메일로 도착합니다."
                            : "제출할 수 없습니다. 소멸일·예산·대기 중 제안을 확인하세요.";
                        SaveCampaign();
                    }
                    SetControlEnabled(true);
                    GUILayout.EndVertical();
                    if (submitted) break;
                }
                if (game.ReadyMadeProposals.Count == 0)
                    GUILayout.Label("현재 사용 가능한 레디메이드 제안이 없습니다.", small);
                GUILayout.Space(10);
                if (LayoutButton("직접 제안서 작성 ›", GUILayout.Height(36))) proposalCustomMode = true;
                if (!string.IsNullOrEmpty(proposalNotice)) GUILayout.Label(proposalNotice, success);
                return;
            }
            if (LayoutButton("‹ 레디메이드 제안으로 돌아가기", GUILayout.Height(34)))
            {
                proposalCustomMode = false;
                return;
            }
            proposalTargetIndex = Mathf.Clamp(proposalTargetIndex, 0, targets.Length - 1);
            proposalTaskCount = Mathf.Clamp(proposalTaskCount, 2, 4);
            GUILayout.Label("PM PROJECT PROPOSAL", title);
            GUILayout.Label("목표와 실행 단계를 구성하면 투자비·보상·일정이 자동 산정됩니다.", small);
            DrawSectionRule();
            GUILayout.Label("1. 제안 목표", section);
            GUILayout.BeginHorizontal();
            if (LayoutButton("<", GUILayout.Width(42))) proposalTargetIndex = (proposalTargetIndex + targets.Length - 1) % targets.Length;
            GUILayout.Label(targets[proposalTargetIndex].Text, GUILayout.MinWidth(220));
            if (LayoutButton(">", GUILayout.Width(42))) proposalTargetIndex = (proposalTargetIndex + 1) % targets.Length;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"2. 실행 단계  {proposalTaskCount}개", section);
            if (LayoutButton("단계 -", GUILayout.Width(75))) proposalTaskCount = Mathf.Max(2, proposalTaskCount - 1);
            if (LayoutButton("단계 +", GUILayout.Width(75))) proposalTaskCount = Mathf.Min(4, proposalTaskCount + 1);
            GUILayout.EndHorizontal();
            var selectedActionIds = new string[proposalTaskCount];
            for (int i = 0; i < proposalTaskCount; i++)
            {
                proposalActionIndices[i] = Mathf.Clamp(proposalActionIndices[i], 0, actions.Length - 1);
                selectedActionIds[i] = actions[proposalActionIndices[i]].Id;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1}.", GUILayout.Width(28));
                if (LayoutButton("<", GUILayout.Width(42))) proposalActionIndices[i] = (proposalActionIndices[i] + actions.Length - 1) % actions.Length;
                GUILayout.Label($"{actions[proposalActionIndices[i]].Text}  ·  {RoleName(actions[proposalActionIndices[i]].Role)}", GUILayout.MinWidth(260));
                if (LayoutButton(">", GUILayout.Width(42))) proposalActionIndices[i] = (proposalActionIndices[i] + 1) % actions.Length;
                GUILayout.EndHorizontal();
                selectedActionIds[i] = actions[proposalActionIndices[i]].Id;
            }
            GUILayout.Space(8);
            GUILayout.Label("3. 제안 논리", section);
            GUILayout.BeginHorizontal();
            if (LayoutButton(proposalPitch == ProposalPitch.Stability ? "[안정성]" : "안정성")) proposalPitch = ProposalPitch.Stability;
            if (LayoutButton(proposalPitch == ProposalPitch.Growth ? "[장기 성장]" : "장기 성장")) proposalPitch = ProposalPitch.Growth;
            if (LayoutButton(proposalPitch == ProposalPitch.Efficiency ? "[비용 효율]" : "비용 효율")) proposalPitch = ProposalPitch.Efficiency;
            GUILayout.EndHorizontal();
            ProposalEstimate estimate = game.EstimateProposal(targets[proposalTargetIndex].Id, selectedActionIds);
            if (estimate != null)
            {
                GUILayout.Space(10);
                GUILayout.Label($"총 작업량 {estimate.TotalWork:0.0}일  ·  투자비 {estimate.CostCredits}자원  ·  완료 보상 {estimate.RewardCredits}자원", section);
                GUILayout.Label($"승인 후 소프트 +{estimate.SoftDurationDays}일  ·  하드 +{estimate.HardDurationDays}일  ·  위험 {RiskName(estimate.Risk)}", small);
                SetControlEnabled(game.Resources >= estimate.CostCredits);
                if (LayoutButton("사장에게 제안서 제출", GUILayout.Height(42)))
                {
                    proposalNotice = game.SubmitProposal(targets[proposalTargetIndex].Id, selectedActionIds, proposalPitch)
                        ? "제출 완료. 다음 날 사장실 메일로 결과가 도착합니다."
                        : "제출할 수 없습니다. 예산 또는 대기 중 제안 수를 확인하세요.";
                    SaveCampaign();
                }
                SetControlEnabled(true);
            }
            if (!string.IsNullOrEmpty(proposalNotice)) GUILayout.Label(proposalNotice, success);
        }

        private void DrawPriority(DeskWindow window)
        {
            GUILayout.Label("WORK PRIORITY", title);
            GUILayout.Label("자동배정은 위에서 아래 순서로 가용 작업을 검토합니다.", small);
            GUILayout.Label("긴급! = 최적 담당자를 낮은 우선순위 일에서 빼와 선점 · 총력! = 해당 일 담당자의 검진과 주말 휴식 생략", warning);
            DrawSectionRule();
            List<WorkGroup> works = game.Groups.Where(group => game.IsWorkVisible(group) &&
                    group.State != WorkState.Complete && group.State != WorkState.Failed)
                .OrderBy(group => group.Priority).ThenBy(group => group.Id).ToList();
            for (int i = 0; i < works.Count; i++)
            {
                WorkGroup work = works[i];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1}. {work.Name}", section, GUILayout.MinWidth(260));
                GUILayout.FlexibleSpace();
                SetControlEnabled(i > 0);
                if (LayoutButton("↑", GUILayout.Width(44))) { game.MoveWorkPriority(work.Id, -1); SaveCampaign(); }
                SetControlEnabled(i < works.Count - 1);
                if (LayoutButton("↓", GUILayout.Width(44))) { game.MoveWorkPriority(work.Id, 1); SaveCampaign(); }
                SetControlEnabled(true);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (LayoutButton(work.Urgent ? "[긴급!]" : "긴급!", GUILayout.Width(100)))
                {
                    game.ConfigureWorkFocus(work.Id, !work.Urgent, work.AllOut); SaveCampaign();
                }
                if (LayoutButton(work.AllOut ? "[총력!]" : "총력!", GUILayout.Width(100)))
                {
                    game.ConfigureWorkFocus(work.Id, work.Urgent, !work.AllOut); SaveCampaign();
                }
                List<WorkTask> workTasks = game.Tasks.Where(task => task.GroupId == work.Id &&
                    task.State != TaskState.Failed).ToList();
                float totalWork = workTasks.Sum(task => task.EffectiveRequiredWork);
                float remainingWork = workTasks.Sum(task => task.RemainingWork);
                int dDay = work.HardDeadline - game.Day;
                string dDayText = dDay >= 0 ? $"D-{dDay}" : $"D+{-dDay}";
                GUILayout.Label($"{dDayText}  ·  전체 {totalWork:0.#} 워크데이 (잔여 {remainingWork:0.#})  ·  " +
                                $"마감 {work.SoftDeadline}/{work.HardDeadline}  ·  상태 {WorkStateName(work.State)}", small);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            if (works.Count == 0) GUILayout.Label("우선순위를 조정할 진행 가능 일이 없습니다.");
        }

        private void DrawGantt(DeskWindow window)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("GANTT / 일감 계획", section);
            GUILayout.FlexibleSpace();
            if (LayoutButton(game.CompetencyAutoAssignment ? "[✓] 자동배정" : "[ ] 자동배정",
                    GUILayout.Width(125f), GUILayout.Height(30f)))
            {
                game.SetCompetencyAutoAssignment(!game.CompetencyAutoAssignment);
                SaveCampaign();
            }
            if (LayoutButton("검진 일괄", GUILayout.Width(95f), GUILayout.Height(30f)))
            {
                game.SendAllForMedicalCheckup();
                SaveCampaign();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"DAY {game.Day:00} ({MilestoneSimulation.WeekdayName(game.CurrentWeekday)})  " +
                            $"일정 외 검진 {game.UnscheduledCheckupResourceCost} 자원 · 금요일 정기검진 무료/반일", small);
            GUILayout.Label($"DAY {game.Day:00}  │  회색=완료  진회색=예상 잔여  ┆ SOFT  │ HARD", small);
            float availableHeight = GanttViewportHeight(window.Rect.height);
            Rect viewport = GUILayoutUtility.GetRect(100f, availableHeight,
                GUILayout.ExpandWidth(true));
            DrawGanttTimeline(window, viewport);
        }

        public static float GanttViewportHeight(float windowHeight) =>
            Mathf.Max(120f, windowHeight - GanttWindowChromeReserve);

        private void DrawGanttTimeline(DeskWindow window, Rect viewport)
        {
            const float labelWidth = 285f;
            const float dayWidth = 28f;
            const float rowHeight = 48f;
            List<WorkGroup> visibleGroups = game.Groups.Where(game.IsWorkVisible)
                .OrderBy(group => group.State == WorkState.Complete || group.State == WorkState.Failed ? 1 : 0)
                .ThenBy(group => group.Priority).ThenBy(group => group.Id).ToList();
            TaskScheduleEstimate[] prioritySchedule = game.BuildPrioritySchedule();
            int rowCount = visibleGroups.Sum(group =>
                1 + game.Tasks.Count(task => task.GroupId == group.Id));
            int planningHorizonDay = game.PlanningHorizonDay;
            float contentWidth = planningHorizonDay * dayWidth + 180f;
            float contentHeight = Math.Max(120f, rowCount * rowHeight + 28f);
            Rect content = new Rect(0, 0, contentWidth, contentHeight);
            Rect labelViewport = new Rect(viewport.x, viewport.y, labelWidth, viewport.height);
            Rect timelineViewport = new Rect(viewport.x + labelWidth, viewport.y,
                Mathf.Max(1f, viewport.width - labelWidth), viewport.height);

            window.TimelineScroll = GUI.BeginScrollView(timelineViewport, window.TimelineScroll, content);
            DrawSolid(new Rect(0, 0, contentWidth, contentHeight), Color.white);
            for (int day = 1; day <= planningHorizonDay; day++)
                if (MilestoneSimulation.IsWeekendDay(day))
                    DrawSolid(new Rect((day - 1) * dayWidth, 0, dayWidth, contentHeight), WeekendColor);
            for (int day = 1; day <= planningHorizonDay; day++)
            {
                float x = (day - 1) * dayWidth;
                DrawSolid(new Rect(x, 0, 1, contentHeight), day == game.Day ? InkColor : PaleColor);
                string weekday = MilestoneSimulation.WeekdayName((Weekday)((day - 1) % 7));
                GUI.Label(new Rect(x + 2, 1, dayWidth - 3, 27), $"{day}\n{weekday}", small);
            }

            float y = 28f;
            foreach (WorkGroup group in visibleGroups)
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
                    TaskScheduleEstimate preview = FindPrioritySchedule(prioritySchedule, task.Id) ??
                                                   game.EstimatePreviewSchedule(task.Id);
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
                    DrawCurrentWorkerSlot(task, y, rowHeight, dayWidth);
                    y += rowHeight;
                }
                DrawSolid(new Rect(0, y - 1, contentWidth, 1), GrayColor);
            }
            DrawDependencyArrows(dayWidth, rowHeight);
            GUI.EndScrollView();

            GUI.BeginGroup(labelViewport);
            DrawSolid(new Rect(0, 0, labelWidth, labelViewport.height), Color.white);
            y = 28f - window.TimelineScroll.y;
            foreach (WorkGroup group in visibleGroups)
            {
                List<WorkTask> tasks = game.Tasks.Where(task => task.GroupId == group.Id).ToList();
                DrawSolid(new Rect(0, y, labelWidth, rowHeight - 1), PaleColor);
                DrawSolid(new Rect(0, y, labelWidth, 1), GrayColor);
                GUI.Label(new Rect(6, y + 4, labelWidth - 10, 22),
                    $"{group.Name} · {WorkStateName(group.State)}", small);
                y += rowHeight;

                foreach (WorkTask task in tasks)
                {
                    string owner = GanttTaskOwner(task);
                    if (Button(new Rect(4, y + 2, labelWidth - 8, rowHeight - 3),
                            $"{task.Name} · {StateName(task.State)} · {GanttTaskCondition(task)}\n" +
                            $"담당 {owner}", small))
                        OpenTaskDetail(task.Id);
                    y += rowHeight;
                }
                DrawSolid(new Rect(0, y - 1, labelWidth, 1), GrayColor);
            }
            GUI.EndGroup();
        }

        private static TaskScheduleEstimate FindPrioritySchedule(
            TaskScheduleEstimate[] schedule, string taskId)
        {
            for (int index = 0; index < schedule.Length; index++)
                if (schedule[index]?.TaskId == taskId) return schedule[index];
            return null;
        }

        private void DrawCurrentWorkerSlot(WorkTask task, float y, float rowHeight, float dayWidth)
        {
            if (task.State == TaskState.Complete || task.State == TaskState.Failed) return;
            if (task.AssignedCharacter < 0 || task.AssignedCharacter >= game.Crew.Count) return;
            CrewMember member = game.Crew[task.AssignedCharacter];
            float currentX = (game.Day - 1) * dayWidth + 2f;
            var slot = new Rect(currentX, y + 3f, 172f, rowHeight - 6f);
            DrawSolid(slot, Color.white);
            DrawBorder(slot, InkColor);

            var portraitSlot = new Rect(slot.x + 3f, slot.y + 3f, 34f, slot.height - 6f);
            DrawSolid(portraitSlot, PaleColor);
            DrawBorder(portraitSlot, GrayColor);
            GUI.Label(portraitSlot, "초상", small);

            var statusIconSlot = new Rect(slot.x + 40f, slot.y + 4f, 13f, 13f);
            DrawSolid(statusIconSlot, member.Available ? PaleColor : GrayColor);
            DrawBorder(statusIconSlot, InkColor);
            GUI.Label(statusIconSlot, "I", small);

            GUI.Label(new Rect(slot.x + 57f, slot.y + 2f, slot.width - 60f, 19f),
                $"{member.Name} · {member.Condition}", small);
            GUI.Label(new Rect(slot.x + 42f, slot.y + 20f, slot.width - 45f, 19f),
                task.Name, small);
        }

        private string GanttTaskCondition(WorkTask task)
        {
            if (task.AssignedCharacter >= 0 && task.AssignedCharacter < game.Crew.Count)
                return game.Crew[task.AssignedCharacter].Condition;
            if (task.ScheduledWorker >= 0 && task.ScheduledWorker < game.Crew.Count)
                return game.Crew[task.ScheduledWorker].Condition;
            int previewWorker = game.PreviewAutomaticAssignee(task.Id, out _);
            if (previewWorker >= 0 && previewWorker < game.Crew.Count)
                return game.Crew[previewWorker].Condition;
            return "담당 없음";
        }

        private string GanttTaskOwner(WorkTask task)
        {
            int previewWorker = game.PreviewAutomaticAssignee(task.Id, out string source);
            if (previewWorker >= 0 && previewWorker < game.Crew.Count)
                return $"{game.Crew[previewWorker].Name} · {source} 예정";
            if (task.AssignedCharacter >= 0 && task.AssignedCharacter < game.Crew.Count)
                return $"{game.Crew[task.AssignedCharacter].Name}" +
                       (task.IsParallelAssignment ? " · 병행" : "");
            if (task.ScheduledDay > 0 && task.ScheduledWorker >= 0 &&
                task.ScheduledWorker < game.Crew.Count)
                return $"{game.Crew[task.ScheduledWorker].Name} · D{task.ScheduledDay:00} 예약";
            return "미배정";
        }

        private void DrawDependencyArrows(float dayWidth, float rowHeight)
        {
            foreach (WorkTask task in game.Tasks)
            {
                if (!game.IsWorkVisible(game.Groups.FirstOrDefault(group => group.Id == task.GroupId)))
                    continue;
                if (string.IsNullOrEmpty(task.PrerequisiteId)) continue;
                WorkTask predecessor = game.Tasks.FirstOrDefault(candidate =>
                    candidate.Id == task.PrerequisiteId);
                if (predecessor != null)
                    DrawDependencyArrow(predecessor, task, dayWidth, rowHeight);
            }

            foreach (WorkGroup group in game.Groups)
            {
                if (!game.IsWorkVisible(group)) continue;
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
            foreach (WorkGroup group in game.Groups.Where(game.IsWorkVisible))
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
            foreach (WorkGroup group in game.Groups.Where(game.IsWorkVisible))
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
            int matchingWorker = BestCompetencyWorker(task);
            TaskCostPreview cost = game.BuildCostPreview(task, matchingWorker);
            string assignee = task.AssignedCharacter < 0
                ? "미배정"
                : $"{game.Crew[task.AssignedCharacter].Name} / {(task.IsParallelAssignment ? "병행" : "주 작업")}";

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{task.Name}  ·  {(task.Required ? "필수" : "선택")}", section);
            GUILayout.Label(
                $"{work?.Name ?? "소속 없음"} / {WorkStateName(work?.State ?? WorkState.Locked)}   " +
                $"역할 {RoleName(task.RequiredRole)}   위험 {RiskName(game.EffectiveRisk(task))}", small);
            GUILayout.Label($"요구 역량  {RequiredCompetencySummary(task)}", section);
            GUILayout.HorizontalSlider(task.Completion, 0, 1);
            GUILayout.Label(
                $"진행 {task.Progress:0.#}일 / 유효 {task.EffectiveRequiredWork:0.#}일   " +
                $"잔여 {task.RemainingWork:0.#}일", small);
            GUILayout.Label(
                $"시작일 {(task.StartedDay > 0 ? $"DAY {task.StartedDay:00}" : "미시작")}  /  " +
                $"완료일 {(task.CompletedDay > 0 ? $"DAY {task.CompletedDay:00}" : "미완료")}", small);
            GUILayout.Label(
                $"최근 결과 {MilestoneSimulation.OutcomeName(task.LastOutcome)} · 산출 {task.LastOutput:0.##}  /  " +
                $"중요도 {ImportanceName(task.Importance)}", small);

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
            if (matchingWorker >= 0)
                GUILayout.Label(
                    $"역량 추천: {game.Crew[matchingWorker].Name} · {RequiredCompetencySummary(task, game.Crew[matchingWorker])} " +
                    $"· 산출 ×{cost.CompetencyMultiplier:0.##}", small);
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
                if (LayoutButton($"선행 상세 열기 · {predecessor.Name}"))
                    OpenTaskDetail(predecessor.Id);
            }
            if (successors.Count == 0)
                GUILayout.Label("이 작업에 막혀 있는 후행 작업: 없음", small);
            else
                foreach (WorkTask successor in successors)
                    if (LayoutButton($"후행 · {TaskRelationSummary(successor)}"))
                        OpenTaskDetail(successor.Id);
            GUILayout.EndVertical();

            GUILayout.Label($"현재 담당: {assignee}", section);
            GUILayout.BeginHorizontal();
            SetControlEnabled(task.State == TaskState.Available || task.State == TaskState.Active);
            if (LayoutButton("주 작업 담당 순환")) { AssignNext(task); SaveCampaign(); }
            SetControlEnabled((task.State == TaskState.Available || task.State == TaskState.Active) &&
                              cost.CanRunInParallel);
            if (LayoutButton("병행 담당 순환")) { AssignNextParallel(task); SaveCampaign(); }
            SetControlEnabled(task.AssignedCharacter >= 0);
            if (LayoutButton("배정 해제")) { game.Assign(task.Id, -1); SaveCampaign(); }
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
                game.Day, game.PlanningHorizonDay);
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
            if (LayoutButton("◀ DAY")) window.ScheduleDay = Mathf.Max(game.Day, window.ScheduleDay - 1);
            GUILayout.Label($"시작 DAY {window.ScheduleDay:00}", section);
            if (LayoutButton("DAY ▶")) window.ScheduleDay = Mathf.Min(game.PlanningHorizonDay, window.ScheduleDay + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (LayoutButton("◀ 작업자"))
            {
                window.SelectedCrew = (window.SelectedCrew - 1 + game.Crew.Count) % game.Crew.Count;
                SetEstimatedScheduleDay(window, task);
            }
            GUILayout.Label(game.Crew[window.SelectedCrew].Name, section);
            if (LayoutButton("작업자 ▶"))
            {
                window.SelectedCrew = (window.SelectedCrew + 1) % game.Crew.Count;
                SetEstimatedScheduleDay(window, task);
            }
            GUILayout.EndHorizontal();
            SetControlEnabled(task.State != TaskState.Complete && task.State != TaskState.Failed);
            if (LayoutButton("이 시작일로 예약"))
            {
                window.Notice = game.Schedule(task.Id, window.SelectedCrew, window.ScheduleDay)
                    ? "작업 시작일을 예약했습니다. 시작 후에는 완료하거나 다시 조정할 때까지 계속 작업합니다."
                    : "같은 작업자의 해당 날짜 예약과 충돌하거나 예약할 수 없는 작업입니다.";
                SaveCampaign();
            }
            SetControlEnabled(task.ScheduledDay > 0);
            if (LayoutButton("예약 취소"))
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
        }

        private void DrawMilestones(DeskWindow window)
        {
            GUILayout.Label("마일스톤", section);
            foreach (WorkGroup group in game.Groups.Where(g =>
                         g.Id != "incident" && game.IsWorkVisible(g)))
            {
                List<WorkTask> tasks = game.Tasks.Where(t => t.GroupId == group.Id).ToList();
                int progress = tasks.Count == 0 ? 0 : Mathf.RoundToInt(tasks.Average(t => t.Completion) * 100);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{group.Name}   {progress}%   HARD D{group.HardDeadline}", section);
                GUILayout.HorizontalSlider(progress, 0, 100);
                foreach (WorkTask task in tasks)
                    if (LayoutButton(
                            $"{(task.Required ? "[필수]" : "[선택]")} {task.Name} — {StateName(task.State)} {task.Progress}/{task.RequiredWork}"))
                        OpenTaskDetail(task.Id);
                GUILayout.EndVertical();
            }
        }

        private void DrawWorkers(DeskWindow window)
        {
            GUILayout.Label("대원 파일", section);
            for (int i = 0; i < game.Crew.Count; i++)
            {
                CrewMember member = game.Crew[i];
                GUILayout.BeginVertical(GUI.skin.box);
                if (LayoutButton(
                        $"{member.Name}   {RoleName(member.Specialty)} / SKILL {member.Skill} / EXP {member.Experience}",
                        section, GUILayout.Height(38)))
                    OpenWorkerDetail(i);
                GUILayout.Label($"상태 {member.Condition}   피로 {member.Fatigue}%   성격 {member.Personality}   기본급 {game.CurrentBaseSalary(member)}   담당 {AssignedTask(i)}");
                GUILayout.HorizontalSlider(member.Fatigue, 0, 100);
                GUILayout.Label($"담당자 신뢰도 {member.Trust}% · {MilestoneSimulation.TrustDescription(member.Trust)}", small);
                if (member.History.Count > 0) GUILayout.Label($"최근: {member.History[member.History.Count - 1]}", small);
                GUILayout.BeginHorizontal();
                SetControlEnabled(member.InjuryDays <= 0 && !member.RestScheduled);
                if (LayoutButton(member.RestScheduled ? "휴식 예약됨" : "휴식 예약")) { game.Rest(i); SaveCampaign(); }
                SetControlEnabled(true);
                if (LayoutButton("재생 계획 열기")) OpenWorkerDetail(i);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
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
            GUILayout.Label($"성격  {member.Personality}", section);
            GUILayout.Label($"경력 {member.Experience}  ·  월 기본급 {game.CurrentBaseSalary(member)}자원", section);
            GUILayout.Label($"현재 담당  {AssignedTask(window.Selected)}", small);
            GUILayout.Label($"담당자 신뢰도  {member.Trust}%", section);
            GUILayout.HorizontalSlider(member.Trust, 0, 100);
            GUILayout.Label(MilestoneSimulation.TrustDescription(member.Trust), small);
            DrawSectionRule();
            GUILayout.Label("재생 계획", section);
            GUILayout.Label(
                "경력과 급여는 항상 초기화됩니다. 추가 자원을 내면 현재 능력이나 퍽을 새 개체에 인계합니다. 성격은 가중 무작위로 다시 결정됩니다.",
                small);
            GUILayout.BeginHorizontal();
            if (LayoutButton(window.InheritRegenerationAbilities
                    ? $"능력 인계 +{game.RegenerationAbilityInheritanceCost}: 선택됨"
                    : $"능력 인계 +{game.RegenerationAbilityInheritanceCost}: 안 함"))
                window.InheritRegenerationAbilities = !window.InheritRegenerationAbilities;
            if (LayoutButton(window.InheritRegenerationPerks
                    ? $"퍽 인계 +{game.RegenerationPerkInheritanceCost}: 선택됨"
                    : $"퍽 인계 +{game.RegenerationPerkInheritanceCost}: 안 함"))
                window.InheritRegenerationPerks = !window.InheritRegenerationPerks;
            GUILayout.EndHorizontal();
            GUILayout.Label("검진 파일", section);
            if (member.MedicalFileUpdatedDay <= 0)
                GUILayout.Label("아직 다운로드된 검진 파일이 없습니다.", small);
            else
            {
                Weekday updatedWeekday = (Weekday)((member.MedicalFileUpdatedDay - 1) % 7);
                GUILayout.Label($"갱신일 DAY {member.MedicalFileUpdatedDay:00} ({MilestoneSimulation.WeekdayName(updatedWeekday)})", small);
                GUILayout.Label($"건강 {member.MedicalFileHealth} · {MilestoneSimulation.MedicalGrade(member.MedicalFileHealth)}");
                GUILayout.Label($"피로도 {member.MedicalFileFatigue} · {MilestoneSimulation.MedicalGrade(100 - member.MedicalFileFatigue)}");
                GUILayout.Label($"멘탈 {member.MedicalFileMental} · {MilestoneSimulation.MedicalGrade(member.MedicalFileMental)}");
                GUILayout.Label($"담당자 신뢰도 {member.MedicalFileTrust} · {MilestoneSimulation.MedicalGrade(member.MedicalFileTrust)}");
            }
            string checkupCostLabel = game.CurrentWeekday == Weekday.Friday
                ? "정기검진 무료 · 반일 근무"
                : $"일정 외 {game.UnscheduledCheckupResourceCost} 자원 · 전일 작업 중단";
            if (LayoutButton($"개별 검진 보내기 ({checkupCostLabel})"))
            {
                window.Notice = game.SendForMedicalCheckup(window.Selected)
                    ? "검진을 예약했습니다. 결과는 다음 월요일 메일로 도착합니다."
                    : "이미 오늘 검진했거나 자원이 부족합니다.";
                SaveCampaign();
            }
            DrawSectionRule();
            int regenerationCost = game.RegenerationCost(
                window.InheritRegenerationAbilities, window.InheritRegenerationPerks);
            GUILayout.Label(
                $"선불 비용 {regenerationCost}자원 · 기존 성격 유지 확률 {game.RegenerationPersonalityRetentionWeight}% · 이후 기본급 {game.InitialBaseSalary}자원부터 재시작",
                section);
            SetControlEnabled(game.Resources >= regenerationCost);
            if (LayoutButton($"이 계획으로 재생 ({member.RegenerationCount + 1}회차)"))
            {
                if (game.Regenerate(window.Selected,
                        window.InheritRegenerationAbilities, window.InheritRegenerationPerks))
                {
                    window.Notice = $"재생 완료 · 성격 {member.Personality} · 경력 0 · 기본급 {game.CurrentBaseSalary(member)}";
                    SaveCampaign();
                }
            }
            SetControlEnabled(true);
            if (!string.IsNullOrEmpty(window.Notice)) GUILayout.Label(window.Notice, success);
            DrawSectionRule();
            GUILayout.Label("메모", section);
            GUILayout.Label(string.IsNullOrEmpty(member.Memo) ? "메모 없음" : member.Memo);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("개척 역량 · 0 아주 못함 / 4 중간 / 7 탁월함", section);
            DrawCompetencyRadar(member);

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

        }

        private void DrawCompetencyRadar(CrewMember member)
        {
            int[] values = member.Competencies ?? new int[CrewMember.CompetencyCount];
            Rect chart = GUILayoutUtility.GetRect(320f, 270f, GUILayout.ExpandWidth(true));
            float radius = Math.Min(105f, chart.width * .27f);
            Vector2 center = new Vector2(chart.x + chart.width * .5f, chart.y + 130f);
            var directions = new Vector2[CrewMember.CompetencyCount];
            for (int i = 0; i < directions.Length; i++)
            {
                double angle = -Math.PI * .5 + Math.PI * 2.0 * i / directions.Length;
                directions[i] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            }

            for (int level = 1; level <= CrewMember.MaximumCompetency; level++)
            {
                float levelRadius = radius * level / CrewMember.MaximumCompetency;
                Color color = level == 4 ? GrayColor : PaleColor;
                for (int i = 0; i < directions.Length; i++)
                    DrawPixelLine(center + directions[i] * levelRadius,
                        center + directions[(i + 1) % directions.Length] * levelRadius, color);
            }

            for (int i = 0; i < directions.Length; i++)
                DrawPixelLine(center, center + directions[i] * radius, PaleColor);

            var points = new Vector2[directions.Length];
            for (int i = 0; i < directions.Length; i++)
            {
                int value = i < values.Length
                    ? Math.Max(0, Math.Min(CrewMember.MaximumCompetency, values[i]))
                    : 0;
                points[i] = center + directions[i] * (radius * value / CrewMember.MaximumCompetency);
            }
            for (int i = 0; i < points.Length; i++)
            {
                DrawPixelLine(points[i], points[(i + 1) % points.Length], InkColor, 3f);
                DrawSolid(new Rect(points[i].x - 3f, points[i].y - 3f, 6f, 6f), InkColor);
            }

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 labelPoint = center + directions[i] * (radius + 30f);
                int value = i < values.Length ? values[i] : 0;
                GUI.Label(new Rect(labelPoint.x - 48f, labelPoint.y - 12f, 96f, 34f),
                    $"{CompetencyNames[i]} {value}", small);
            }
            GUI.Label(new Rect(chart.x + 6f, chart.yMax - 22f, chart.width - 12f, 20f),
                "회색 강조선은 중간 역량(4) 기준입니다.", small);
        }

        private static void DrawPixelLine(Vector2 from, Vector2 to, Color color, float thickness = 2f)
        {
            float distance = Math.Max(Math.Abs(to.x - from.x), Math.Abs(to.y - from.y));
            int steps = Math.Max(1, (int)(distance / 3f));
            for (int step = 0; step <= steps; step++)
            {
                float amount = step / (float)steps;
                float x = from.x + (to.x - from.x) * amount;
                float y = from.y + (to.y - from.y) * amount;
                DrawSolid(new Rect(x - thickness * .5f, y - thickness * .5f, thickness, thickness), color);
            }
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
                if (LayoutButton($"{active}{member.Name}\n{MessengerPresence(i)}", GUILayout.Height(52)))
                {
                    window.Selected = i;
                    window.Scroll = Vector2.zero;
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(selected.Name, section);
            GUILayout.Label(
                $"{RoleName(selected.Specialty)} · 성격 {selected.Personality} · {MessengerPresence(window.Selected)} · 담당자 신뢰 {selected.Trust}%",
                small);
            GUILayout.Label(MilestoneSimulation.TrustDescription(selected.Trust), small);
            DrawSectionRule();

            bool hasMessages = false;
            for (int messageDay = 1; messageDay <= game.Day; messageDay++)
            {
                if (selected.History != null)
                {
                    foreach (string history in selected.History)
                    {
                        if (!HistoryOccursOnDay(history, messageDay)) continue;
                        DrawMessengerBubble(history);
                        hasMessages = true;
                    }
                }
                foreach (WorkTask task in game.Tasks)
                {
                    if (task.Records == null) continue;
                    foreach (TaskRecord record in task.Records)
                    {
                        if (record.Day != messageDay || record.Actor != selected.Name) continue;
                        DrawMessengerBubble(
                            $"DAY {record.Day:00} [{selected.Name}]\n{task.Name}: {record.Text}");
                        hasMessages = true;
                    }
                }
            }
            if (!hasMessages)
                GUILayout.Label("아직 대화나 작업 피드백이 없습니다.", small);
            GUILayout.Label("물어보기", small);
            GUILayout.BeginHorizontal();
            if (LayoutButton("안부 묻기", GUILayout.Height(36)))
            {
                game.AskWorker(window.Selected, "status");
                SaveCampaign();
            }
            if (LayoutButton("작업 현황 묻기", GUILayout.Height(36)))
            {
                game.AskWorker(window.Selected, "work");
                SaveCampaign();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            MarkMessengerSeen();
        }

        private string MessengerPresence(int crewIndex)
        {
            CrewMember member = game.Crew[crewIndex];
            if (member.InjuryDays > 0) return $"부상 · {member.InjuryDays}일";
            if (member.RestScheduled) return "휴식 예정";
            WorkTask task = game.Tasks.FirstOrDefault(candidate =>
                candidate.AssignedCharacter == crewIndex && !candidate.IsParallelAssignment &&
                candidate.State != TaskState.Complete && candidate.State != TaskState.Failed);
            return task == null ? "대기 중" : $"작업 중 · {task.Name}";
        }

        private void DrawMessengerBubble(string message)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(message, small);
            GUILayout.EndVertical();
        }

        private static bool HistoryOccursOnDay(string history, int day)
        {
            if (string.IsNullOrEmpty(history)) return false;
            return history.StartsWith($"DAY {day}:") || history.StartsWith($"DAY {day} [") ||
                   history.StartsWith($"DAY {day}\n");
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
            foreach (WorkTask task in game.Tasks.Where(t => game.EffectiveRisk(t) == RiskLevel.High && t.State != TaskState.Complete))
                GUILayout.Label($"[고위험] {task.Name} / D{task.Deadline} / {StateName(task.State)}", warning);
            GUILayout.Space(8);
            GUILayout.Label("최근 결과", section);
            foreach (string line in game.LastReport.Lines) GUILayout.Label(line);
        }

        private void DrawCodex(DeskWindow window)
        {
            if (game.Codex.Count == 0)
            {
                GUILayout.Label("아직 해금된 도감 항목이 없습니다.", small);
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(185));
            var renderedCategories = new List<string>();
            foreach (CodexEntry codexEntry in game.Codex)
            {
                string category = codexEntry.Category;
                if (ListContains(renderedCategories, category)) continue;
                renderedCategories.Add(category);
                bool collapsed = ListContains(window.CollapsedCodexCategories, category);
                if (LayoutButton($"{(collapsed ? "▶" : "▼")} {category}", section, GUILayout.Height(36)))
                {
                    if (collapsed) RemoveFromList(window.CollapsedCodexCategories, category);
                    else window.CollapsedCodexCategories.Add(category);
                }
                if (collapsed) continue;
                for (int i = 0; i < game.Codex.Count; i++)
                {
                    if (game.Codex[i].Category != category) continue;
                    if (LayoutButton($"  {game.Codex[i].Name}", GUILayout.Height(40))) window.Selected = i;
                }
            }
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

        private static bool ListContains(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (values[i] == value) return true;
            return false;
        }

        private static void RemoveFromList(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] != value) continue;
                values.RemoveAt(i);
                return;
            }
        }

        private void DrawProfile()
        {
            GUILayout.Label("내정보 / 캠페인 관리", section);
            GUILayout.Label(
                $"PROJECT W 운영 담당자\nDAY {game.Day} · 생존 기록\n" +
                $"중간평가 DAY {game.MidpointReviewDay} ({(game.MidpointReviewIssued ? "완료" : "예정")})\n" +
                $"자원 {game.Resources}\n작업 성공 보정 {game.TaskSuccessChanceModifier:+#;-#;0}%p\n" +
                $"월 기본급 합계 {game.TotalPayroll()} · 다음 지급 DAY {game.NextPayrollDay}\n패치 {patchVersion}");
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
            DrawUpdateControls();
            GUILayout.Space(16f);
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
            GUILayout.Label("버전 및 진단", section);
            GUILayout.Label($"실행 버전: {patchVersion}", small);
            if (patchDiagnostics != null)
            {
                GUILayout.Label(
                    $"설치 버전: {patchDiagnostics.InstalledVersion}\n" +
                    $"패치 상태: {patchDiagnostics.Status}\n" +
                    $"최근 다운로드 결과: {patchDiagnostics.LastPatchResult}",
                    small);
            }
            if (LayoutButton("로그 열기", GUILayout.Height(38)))
                Open("log");
            GUILayout.Space(16);
            GUILayout.Label("디버그", section);
            GUILayout.Label("팝업 / 알림 이펙트", small);
            if (LayoutButton("[데모] 버튼으로 닫는 팝업", GUILayout.Height(38))) ShowPopupDemo();
            if (LayoutButton("[데모] 우하단 3초 알림", GUILayout.Height(38)))
            {
                notifications.ShowToast(new TimedToastData
                {
                    Title = "새 알림",
                    Message = "시간이 지나면 자동으로 사라지는 우하단 알림입니다.",
                    DurationSeconds = 3f
                }, Time.unscaledTime);
            }
            GUILayout.Space(10);
            GUILayout.Label("가장 이른 중요 이벤트를 현재 날짜에 즉시 시작합니다. 완료한 이벤트도 다시 시험할 수 있습니다.", small);
            SetControlEnabled(game.CanForceCriticalEvent);
            if (LayoutButton("[디버그] 중요 이벤트 강제 발생", GUILayout.Height(38)))
            {
                if (game.ForceCriticalEvent())
                {
                    SaveCampaign();
                    Open("mail");
                }
            }
            SetControlEnabled(true);
            GUILayout.Space(16);
            GUILayout.Label("초기화", section);
            if (LayoutButton("창 위치 및 열린 상태 초기화", GUILayout.Height(38)))
            {
                ProjectWSaveStore.Delete(DesktopSaveKey);
                windows.Clear();
                SaveDesktop();
            }
            if (LayoutButton("새 캠페인 시작", GUILayout.Height(38)))
            {
                ProjectWSaveStore.Delete(CampaignSaveKey);
                game = new MilestoneSimulation();
                SaveCampaign();
            }
        }

        private void ShowPopupDemo()
        {
            notifications.ShowPopup(new TimedPopupData
            {
                Title = "보급품 획득",
                Message = "아이콘 앞 반짝이, 팡 터지는 버스트, 확장 링을 한 화면에서 시험하는 이펙트 샘플입니다.",
                DurationSeconds = 0f,
                RotationSeconds = 2.5f,
                Icons = new[]
                {
                    new PopupIconData
                    {
                        Glyph = "R", Label = "앞쪽 반짝이", EffectColor = new Color(.96f, .72f, .18f, .9f),
                        ShowRadial = false, ShowSparkles = true, SparkleCount = 12
                    },
                    new PopupIconData
                    {
                        Glyph = "C", Label = "팡 버스트", EffectColor = new Color(.28f, .72f, .9f, .9f),
                        ShowRadial = false, ShowSparkles = false, ShowBurst = true
                    },
                    new PopupIconData
                    {
                        Glyph = "!", Label = "확장 링", EffectColor = new Color(.86f, .38f, .32f, .9f),
                        ShowRadial = true, ShowSparkles = false, ShowRing = true
                    }
                },
                Buttons = new[] { "1", "2", "3" }
            }, Time.unscaledTime);
        }

        private void DrawUpdateControls()
        {
            GUILayout.Label("UPDATE", section);
            if (patchDiagnostics == null)
            {
                GUILayout.Label("Update service is unavailable.", warning);
                return;
            }
            GUILayout.Label(
                $"Running: {patchDiagnostics.ActiveVersion}  Installed: {patchDiagnostics.InstalledVersion}\n" +
                $"State: {patchDiagnostics.Status}\nResult: {patchDiagnostics.LastPatchResult}", small);
            SetControlEnabled(!patchDiagnostics.UpdateInProgress);
            if (LayoutButton(patchDiagnostics.UpdateInProgress ? "CHECKING..." : "CHECK FOR UPDATE", GUILayout.Height(38)))
                patchDiagnostics.RequestUpdate();
            SetControlEnabled(true);
            GUILayout.Label("An installed code update is activated after restarting the application.", small);
        }

        private void DrawScaleOption(float value, string label)
        {
            bool selected = Mathf.Abs(uiMagnification - value) < .01f;
            if (LayoutButton($"{(selected ? "●" : "○")}  {label}", GUILayout.Height(48f)))
            {
                uiMagnification = value;
                SaveDesktop();
            }
        }

        private void DrawLog(DeskWindow window)
        {
            GUILayout.BeginHorizontal();
            if (LayoutButton(window.Selected == 0 ? "● 다운로드 / 패치" : "다운로드 / 패치",
                    GUILayout.Height(36)))
                window.Selected = 0;
            if (LayoutButton(window.Selected == 1 ? "● 게임 로그" : "게임 로그",
                    GUILayout.Height(36)))
                window.Selected = 1;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            if (window.Selected == 0)
            {
                DrawPatchLog();
                return;
            }

            GUILayout.Label("게임 로그", section);
            foreach (string line in game.SystemLog.AsEnumerable().Reverse()) GUILayout.Label(line, small);
        }

        private void DrawDataEditor(DeskWindow window)
        {
            if (editorData == null) editorData = TaskSystemDataLoader.Parse(TaskSystemDataLoader.Serialize(TaskSystemDataLoader.Load()));
            GUILayout.Label("GAME DATA SPREADSHEET", title);
            GUILayout.Label("Edit a validated local working copy. Reload starts a new campaign.", small);
            GUILayout.BeginHorizontal();
            string[] tabs = { "CHARACTERS", "BALANCE / PROBABILITY", "CRITICAL EVENTS", "MAIL" };
            for (int i = 0; i < tabs.Length; i++)
                if (LayoutButton(window.Selected == i ? $"● {tabs[i]}" : tabs[i], GUILayout.Height(34)))
                {
                    window.Selected = i;
                    categoryTransferText = string.Empty;
                    editorNotice = string.Empty;
                }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            if (window.Selected == 0) DrawCharacterTable();
            else if (window.Selected == 1) DrawBalanceTable();
            else if (window.Selected == 2) DrawCriticalEventTable(window);
            else DrawMailTable();
            DrawCategoryTransfer(window.Selected);
            GUILayout.Space(12f);
            if (!string.IsNullOrWhiteSpace(editorNotice))
                GUILayout.Label(editorNotice, editorNotice.StartsWith("ERROR", StringComparison.Ordinal) ? warning : success);
            GUILayout.BeginHorizontal();
            if (LayoutButton("SAVE DRAFT", GUILayout.Height(38))) SaveDataDraft(false);
            if (LayoutButton(confirmDataReload ? "CONFIRM RELOAD + NEW CAMPAIGN" : "RELOAD GAME DATA", GUILayout.Height(38)))
            {
                if (confirmDataReload) SaveDataDraft(true);
                else confirmDataReload = true;
            }
            if (LayoutButton(confirmOverrideReset ? "CONFIRM RESET OVERRIDE" : "RESET OVERRIDE", GUILayout.Height(38)))
            {
                if (confirmOverrideReset)
                {
                    TaskSystemDataLoader.DeleteOverride();
                    editorData = TaskSystemDataLoader.Load();
                    ProjectWSaveStore.Delete(CampaignSaveKey);
                    game = new MilestoneSimulation(editorData);
                    confirmOverrideReset = false;
                    editorNotice = "Override removed; patch or embedded data reloaded.";
                }
                else confirmOverrideReset = true;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCategoryTransfer(int category)
        {
            GUILayout.Space(12f);
            GUILayout.Label("CATEGORY JSON TRANSFER", section);
            GUILayout.Label("Copy sends this category to the field and clipboard. Paste replacement JSON, then Apply.", small);
            categoryTransferText = GUILayout.TextField(categoryTransferText ?? string.Empty,
                GUILayout.Height(72f), GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            if (LayoutButton("COPY CATEGORY", GUILayout.Height(36)))
            {
                categoryTransferText = TaskSystemDataCategoryTransfer.Copy(editorData, category);
                GUIUtility.systemCopyBuffer = categoryTransferText;
                editorNotice = "Category JSON was copied to the field and system clipboard.";
            }
            if (LayoutButton("APPLY CATEGORY JSON", GUILayout.Height(36)))
            {
                if (TaskSystemDataCategoryTransfer.TryApply(editorData, category, categoryTransferText,
                        out TaskSystemData updated, out string error))
                {
                    editorData = updated;
                    editorNotice = "Category JSON passed validation and was applied to the working copy.";
                }
                else editorNotice = $"ERROR: {error}";
            }
            GUILayout.EndHorizontal();
        }

        private void SaveDataDraft(bool reload)
        {
            try
            {
                TaskSystemDataLoader.SaveOverride(editorData);
                editorNotice = "Draft saved and validated.";
                if (!reload) return;
                ProjectWSaveStore.Delete(CampaignSaveKey);
                game = new MilestoneSimulation(TaskSystemDataLoader.Load());
                SaveCampaign();
                confirmDataReload = false;
                editorNotice = "Game data reloaded; a new campaign was created.";
            }
            catch (Exception exception)
            {
                confirmDataReload = false;
                editorNotice = $"ERROR: {exception.Message}";
            }
        }

        private void DrawCharacterTable()
        {
            DrawTableHeader("NAME", "PERSONALITY", "SKILL", "TRUST", "FATIGUE", "OUTPUT");
            foreach (CrewMember member in editorData.Crew)
            {
                GUILayout.BeginHorizontal();
                member.Name = GUILayout.TextField(member.Name ?? string.Empty, GUILayout.Width(130));
                member.Personality = GUILayout.TextField(member.Personality ?? string.Empty, GUILayout.Width(130));
                EditInt(ref member.Skill, 60); EditInt(ref member.Trust, 60); EditInt(ref member.Fatigue, 60);
                EditFloat(ref member.DailyOutput, 70);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("COMPETENCIES (0..7)", section);
            foreach (CrewMember member in editorData.Crew)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(member.Name, GUILayout.Width(130));
                for (int i = 0; i < member.Competencies.Length; i++) EditInt(ref member.Competencies[i], 55);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawBalanceTable()
        {
            TaskSystemBalance balance = editorData.Balance;
            DrawTableHeader("FIELD", "VALUE", "FIELD", "VALUE", "FIELD", "VALUE");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Start resources", GUILayout.Width(130)); EditInt(ref editorData.StartingResources, 70);
            GUILayout.Label("Side mission %", GUILayout.Width(130)); EditInt(ref balance.BaseSideMissionChance, 70);
            GUILayout.Label("Accident high %", GUILayout.Width(130)); EditInt(ref balance.HighFatigueAccidentChance, 70);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Fresh failure %", GUILayout.Width(130)); EditInt(ref balance.FreshLowOutputChance, 70);
            GUILayout.Label("Fresh great %", GUILayout.Width(130)); EditInt(ref balance.FreshHighOutputChance, 70);
            GUILayout.Label("Random work scale %", GUILayout.Width(130)); EditInt(ref balance.RandomWorkChanceScalePercent, 70);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Failure output", GUILayout.Width(130)); EditFloat(ref balance.LowOutputMultiplier, 70);
            GUILayout.Label("Great output", GUILayout.Width(130)); EditFloat(ref balance.HighOutputMultiplier, 70);
            GUILayout.Label("Weekend recovery", GUILayout.Width(130)); EditInt(ref balance.WeekendFatigueRecovery, 70);
            GUILayout.EndHorizontal();
        }

        private void DrawCriticalEventTable(DeskWindow window)
        {
            CriticalEventDefinition[] events = editorData.CriticalEvents ?? new CriticalEventDefinition[0];
            DrawTableHeader("ID", "START DAY", "FIRST NODE", "NODES", "", "");
            for (int i = 0; i < events.Length; i++)
            {
                CriticalEventDefinition definition = events[i];
                GUILayout.BeginHorizontal();
                definition.Id = GUILayout.TextField(definition.Id ?? string.Empty, GUILayout.Width(150));
                EditInt(ref definition.StartDay, 80);
                definition.FirstNodeId = GUILayout.TextField(definition.FirstNodeId ?? string.Empty, GUILayout.Width(150));
                GUILayout.Label((definition.Nodes?.Length ?? 0).ToString(), GUILayout.Width(70));
                if (LayoutButton("DETAIL", GUILayout.Width(90))) window.SelectedCrew = i;
                GUILayout.EndHorizontal();
            }
            if (events.Length == 0) return;
            CriticalEventDefinition selected = events[Mathf.Clamp(window.SelectedCrew, 0, events.Length - 1)];
            GUILayout.Label($"NODES / {selected.Id}", section);
            foreach (CriticalEventNode node in selected.Nodes ?? new CriticalEventNode[0])
            {
                GUILayout.BeginHorizontal();
                node.Id = GUILayout.TextField(node.Id ?? string.Empty, GUILayout.Width(120));
                node.Subject = GUILayout.TextField(node.Subject ?? string.Empty, GUILayout.Width(220));
                GUILayout.Label($"Choices {node.Choices?.Length ?? 0}", GUILayout.Width(100));
                GUILayout.EndHorizontal();
                foreach (CriticalEventChoice choice in node.Choices ?? new CriticalEventChoice[0])
                    foreach (CriticalEventOutcome outcome in choice.Outcomes ?? new CriticalEventOutcome[0])
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"↳ {choice.Id}", GUILayout.Width(120));
                        GUILayout.Label("Weight", GUILayout.Width(60)); EditInt(ref outcome.Weight, 60);
                        GUILayout.Label("Resource", GUILayout.Width(70)); EditInt(ref outcome.ResourceDelta, 60);
                        GUILayout.Label("Success %p", GUILayout.Width(80)); EditInt(ref outcome.SuccessChanceDelta, 60);
                        GUILayout.EndHorizontal();
                    }
            }
        }

        private void DrawMailTable()
        {
            DrawTableHeader("ID", "DAY", "FROM", "SUBJECT", "RESOURCE", "DEADLINE");
            foreach (MailEvent mail in editorData.Mail ?? new MailEvent[0])
            {
                GUILayout.BeginHorizontal();
                mail.Id = GUILayout.TextField(mail.Id ?? string.Empty, GUILayout.Width(120));
                EditInt(ref mail.ArrivalDay, 55);
                mail.From = GUILayout.TextField(mail.From ?? string.Empty, GUILayout.Width(110));
                mail.Subject = GUILayout.TextField(mail.Subject ?? string.Empty, GUILayout.Width(190));
                EditInt(ref mail.ResourceDelta, 65); EditInt(ref mail.DeadlineDelta, 65);
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawTableHeader(params string[] columns)
        {
            GUILayout.BeginHorizontal();
            foreach (string column in columns) GUILayout.Label(column, GUILayout.Width(120));
            GUILayout.EndHorizontal();
        }

        private static void EditInt(ref int value, float width)
        {
            string text = GUILayout.TextField(value.ToString(), GUILayout.Width(width));
            if (int.TryParse(text, out int parsed)) value = parsed;
        }

        private static void EditFloat(ref float value, float width)
        {
            string text = GUILayout.TextField(value.ToString("0.###"), GUILayout.Width(width));
            if (float.TryParse(text, out float parsed)) value = parsed;
        }

        private void DrawPatchLog()
        {
            GUILayout.Label("다운로드 / 패치 로그", section);
            if (patchDiagnostics == null)
            {
                GUILayout.Label("패치 진단 정보를 사용할 수 없습니다.", warning);
                return;
            }

            GUILayout.Label(
                $"실행: {patchDiagnostics.ActiveVersion}  설치: {patchDiagnostics.InstalledVersion}\n" +
                $"상태: {patchDiagnostics.Status}\n최근 결과: {patchDiagnostics.LastPatchResult}",
                small);
            GUILayout.Space(8);
            PatchDiagnosticEntry[] logs = patchDiagnostics.GetLogs();
            for (int i = logs.Length - 1; i >= 0; i--)
            {
                PatchDiagnosticEntry log = logs[i];
                GUIStyle style = log.Type == "Error" || log.Type == "Exception" || log.Type == "Assert"
                    ? warning : small;
                GUILayout.Label($"[{log.Type}] {log.Message}\n{log.StackTrace}", style);
            }
            if (logs.Length == 0) GUILayout.Label("기록된 다운로드 로그가 없습니다.", small);
            if (LayoutButton("다운로드 로그 지우기", GUILayout.Height(34)))
                patchDiagnostics.ClearLogs();
        }

        private void DrawTaskbar()
        {
            Rect bar = new Rect(0, logicalHeight - 44, logicalWidth, 44);
            DrawSolid(bar, GrayColor);
            SetControlEnabled(!game.IsWon && !game.IsLost && !game.MustResolveCriticalChoice);
            if (Button(NextDayButtonRect(logicalWidth, logicalHeight), "다음날로 →"))
                AdvanceToNextDay();
            SetControlEnabled(true);
            float x = 8;
            foreach (DeskWindow window in windows.ToArray())
            {
                if (Button(new Rect(x, logicalHeight - 38, 118, 31), window.Title))
                {
                    window.Minimized = !window.Minimized;
                    if (window.Minimized) ActivateFrontmostVisibleWindow();
                    else Focus(window);
                    SaveDesktop();
                }
                x += 122;
                if (x > logicalWidth - 330) break;
            }
            int unread = game.Mail.Count(m => m.ArrivalDay <= game.Day && !m.Read);
            if (Button(new Rect(logicalWidth - 310, logicalHeight - 38, 95, 31), unread > 0 ? $"MAIL ({unread})" : "MAIL")) Open("mail");
            GUI.Label(new Rect(logicalWidth - 108, logicalHeight - 34, 100, 25),
                $"D{game.Day:00} {MilestoneSimulation.WeekdayName(game.CurrentWeekday)}", small);
        }

        private void AdvanceToNextDay()
        {
            game.AdvanceDay();
            if (game.MustResolveCriticalChoice) Open("mail");
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
            const int columns = 6;
            const float maximumSize = 58f;
            const float labelHeight = 18f;
            float availableWidth = Mathf.Max(0f, width - 40f);
            float iconSize = Mathf.Min(maximumSize, availableWidth * 3f / 23f);
            float gap = iconSize / 3f;
            int column = index % columns;
            int row = index / columns;
            return new Rect(
                20f + column * (iconSize + gap),
                70f + row * (iconSize + labelHeight + gap),
                iconSize,
                iconSize);
        }

        public static Rect DesktopIconLabelRect(int index, float width)
        {
            Rect icon = DesktopIconRect(index, width);
            return new Rect(icon.x, icon.yMax - 8f, icon.width, 22f);
        }

        public static Rect DesktopIconBadgeRect(int index, float width)
        {
            Rect icon = DesktopIconRect(index, width);
            return new Rect(icon.xMax - 15f, icon.y - 7f, 30f, 21f);
        }

        public static Rect WindowMinimizeButtonRect(float windowWidth) =>
            new Rect(windowWidth - 75f, 1f, 31f, WindowTitleBarHeight);

        public static Rect WindowCloseButtonRect(float windowWidth) =>
            new Rect(windowWidth - 36f, 1f, 31f, WindowTitleBarHeight);

        private void Open(string id)
        {
            if (id == "messenger") MarkMessengerSeen();
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
                GuiId = AllocateStableWindowGuiId(ref nextWindowGuiId),
                Id = id, Title = appTitles[id],
                Rect = new Rect(490 + offset, 55 + offset, 710, 500)
            };
            windows.Add(window);
            window.Rect = ClampRect(window.Rect);
            Focus(window);
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
            window.ScheduleDay = Mathf.Clamp(estimate?.StartDay ?? game.Day, game.Day, game.PlanningHorizonDay);
        }

        private void Close(string id)
        {
            bool closedFrontWindow = frontWindow != null && frontWindow.Id == id;
            windows.RemoveAll(w => w.Id == id);
            if (closedFrontWindow) ActivateFrontmostVisibleWindow();
            SaveDesktop();
        }

        private void Focus(DeskWindow window)
        {
            windows.Remove(window);
            windows.Add(window);
            frontWindow = window;
        }

        private void ActivateFrontmostVisibleWindow()
        {
            frontWindow = null;
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (windows[i].Minimized) continue;
                frontWindow = windows[i];
                return;
            }
        }

        public static int AllocateStableWindowGuiId(ref int nextGuiId)
        {
            int allocated = nextGuiId;
            nextGuiId++;
            return allocated;
        }

        private void HandleWindowInput()
        {
            if (HandlePinchWindowInput()) return;

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

        private bool HandlePinchWindowInput()
        {
            if (!TryGetTwoTouchPositions(out Vector2 firstScreenPosition,
                    out Vector2 secondScreenPosition))
            {
                if (pinchWindow != null)
                {
                    pinchWindow = null;
                    SaveDesktop();
                }
                return false;
            }

            Vector2 firstPosition = TouchToLogicalPosition(firstScreenPosition, Screen.height,
                CalculateUiScale(Screen.width, uiMagnification));
            Vector2 secondPosition = TouchToLogicalPosition(secondScreenPosition, Screen.height,
                CalculateUiScale(Screen.width, uiMagnification));
            Vector2 center = (firstPosition + secondPosition) * .5f;
            float distance = Vector2.Distance(firstPosition, secondPosition);

            if (pinchWindow == null)
            {
                for (int i = windows.Count - 1; i >= 0; i--)
                {
                    DeskWindow candidate = windows[i];
                    if (candidate.Minimized || !candidate.Rect.Contains(center)) continue;
                    pinchWindow = candidate;
                    pinchRectOrigin = candidate.Rect;
                    pinchCenterOrigin = center;
                    pinchDistanceOrigin = Mathf.Max(1f, distance);
                    candidate.Resizing = false;
                    Focus(candidate);
                    break;
                }
            }

            if (pinchWindow == null) return true;
            pinchWindow.Rect = CalculatePinchedWindowRect(pinchRectOrigin, pinchCenterOrigin,
                center, distance / pinchDistanceOrigin, logicalWidth, logicalHeight);
            return true;
        }

        private static bool TryGetTwoTouchPositions(out Vector2 first, out Vector2 second)
        {
            first = Vector2.zero;
            second = Vector2.zero;
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;

            int found = 0;
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                TouchControl touch = touchscreen.touches[i];
                if (!touch.press.isPressed) continue;
                if (found == 0) first = touch.position.ReadValue();
                else
                {
                    second = touch.position.ReadValue();
                    return true;
                }
                found++;
            }
            return false;
        }

        public static Vector2 TouchToLogicalPosition(Vector2 screenPosition, float screenHeight,
            float uiScale)
        {
            float scale = Mathf.Max(.01f, uiScale);
            return new Vector2(screenPosition.x / scale, (screenHeight - screenPosition.y) / scale);
        }

        public static Rect CalculatePinchedWindowRect(Rect original, Vector2 centerOrigin,
            Vector2 centerCurrent, float pinchScale, float desktopWidth, float desktopHeight)
        {
            float scale = Mathf.Max(.01f, pinchScale);
            Vector2 topLeft = centerCurrent + (original.position - centerOrigin) * scale;
            float width = Mathf.Clamp(original.width * scale, MinimumWindowWidth,
                Mathf.Max(MinimumWindowWidth, desktopWidth - 12f));
            float height = Mathf.Clamp(original.height * scale, MinimumWindowHeight,
                Mathf.Max(MinimumWindowHeight, desktopHeight - 56f));
            return ClampWindowRect(new Rect(topLeft.x, topLeft.y, width, height),
                desktopWidth, desktopHeight);
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
            GUI.enabled = !inputLayerBlocked && !modalInputBlocked && enabled;

        private static bool ExpandedHitButton(Rect visualRect, string label)
        {
            if (Button(visualRect, label)) return true;

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
            return new Rect(0, 0, Mathf.Max(0, windowWidth - 110f), 65f);
        }

        private void RefreshDesktopBadges()
        {
            SetDesktopBadgeCount("mail", game.Mail.Count(mail => mail.ArrivalDay <= game.Day && !mail.Read));
            SetDesktopBadgeCount("messenger", Mathf.Max(0, MessengerUpdateCount() - messengerSeenUpdateCount));
        }

        public void SetDesktopBadgeCount(string appId, int count)
        {
            if (string.IsNullOrEmpty(appId) || !appTitles.ContainsKey(appId)) return;
            for (int i = 0; i < desktopBadgeAppIds.Length; i++)
            {
                if (desktopBadgeAppIds[i] != appId) continue;
                desktopBadgeCounts[i] = Mathf.Max(0, count);
                return;
            }
        }

        public int DesktopBadgeCount(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return 0;
            for (int i = 0; i < desktopBadgeAppIds.Length; i++)
                if (desktopBadgeAppIds[i] == appId) return desktopBadgeCounts[i];
            return 0;
        }

        private int MessengerUpdateCount()
        {
            int count = 0;
            foreach (CrewMember member in game.Crew)
                if (member.History != null) count += member.History.Count;
            foreach (WorkTask task in game.Tasks)
                if (task.Records != null) count += task.Records.Count;
            return count;
        }

        private void MarkMessengerSeen()
        {
            int current = MessengerUpdateCount();
            if (current == messengerSeenUpdateCount) return;
            messengerSeenUpdateCount = current;
            SaveDesktop();
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
            string[] tasks = game.Tasks.Where(t => t.AssignedCharacter == crewIndex &&
                    t.State != TaskState.Complete && t.State != TaskState.Failed)
                .Select(t => t.IsParallelAssignment ? $"{t.Name}(병행)" : t.Name).ToArray();
            return tasks.Length == 0 ? "없음" : string.Join(", ", tasks);
        }

        private int BestCompetencyWorker(WorkTask task)
        {
            int bestIndex = -1;
            float bestMultiplier = -1f;
            for (int i = 0; i < game.Crew.Count; i++)
            {
                float multiplier = MilestoneSimulation.CompetencyOutputMultiplier(game.Crew[i], task);
                if (multiplier <= bestMultiplier) continue;
                bestMultiplier = multiplier;
                bestIndex = i;
            }
            return bestIndex;
        }

        private static string RequiredCompetencySummary(WorkTask task, CrewMember member = null)
        {
            if (task?.RequiredCompetencies == null || task.RequiredCompetencies.Length == 0)
                return "없음";
            var labels = new string[task.RequiredCompetencies.Length];
            for (int i = 0; i < task.RequiredCompetencies.Length; i++)
            {
                int competency = task.RequiredCompetencies[i];
                string name = competency >= 0 && competency < CompetencyNames.Length
                    ? CompetencyNames[competency]
                    : $"역량 {competency}";
                labels[i] = member?.Competencies != null && competency < member.Competencies.Length
                    ? $"{name} {member.Competencies[competency]}"
                    : name;
            }
            return string.Join(" · ", labels);
        }

        public static float RestoredScrollbarWidth(float currentWidth) =>
            Mathf.Max(DefaultScrollbarWidth, currentWidth) * 2f;

        private static bool Button(Rect rect, string label)
        {
            Color previous = GUI.color;
            GUI.color = GrayColor;
            bool clicked = GUI.Button(rect, label);
            GUI.color = previous;
            return clicked;
        }

        private static bool Button(Rect rect, string label, GUIStyle style)
        {
            Color previous = GUI.color;
            GUI.color = GrayColor;
            bool clicked = GUI.Button(rect, label, style);
            GUI.color = previous;
            return clicked;
        }

        private static bool LayoutButton(string label, params GUILayoutOption[] options)
        {
            Color previous = GUI.color;
            GUI.color = GrayColor;
            bool clicked = GUILayout.Button(label, options);
            GUI.color = previous;
            return clicked;
        }

        private static bool LayoutButton(string label, GUIStyle style, params GUILayoutOption[] options)
        {
            Color previous = GUI.color;
            GUI.color = GrayColor;
            bool clicked = GUILayout.Button(label, style, options);
            GUI.color = previous;
            return clicked;
        }

        private static bool LayoutMailButton(string label, bool dimmed, params GUILayoutOption[] options)
        {
            Color previous = GUI.color;
            GUI.color = dimmed ? DimmedGrayColor : GrayColor;
            bool clicked = GUILayout.Button(label, options);
            GUI.color = previous;
            return clicked;
        }

        private void SaveCampaign() => ProjectWSaveStore.SaveCampaign(CampaignSaveKey, game.CreateSnapshot());

        private void SaveDesktop()
        {
            var snapshot = new DesktopSnapshot
            {
                SchemaVersion = ProjectWSaveStore.DesktopSchema,
                UiMagnification = uiMagnification,
                MessengerSeenUpdateCount = messengerSeenUpdateCount,
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
            messengerSeenUpdateCount = Mathf.Max(0, snapshot.MessengerSeenUpdateCount);
            foreach (WindowSnapshot saved in snapshot.Windows.Where(w => w.Open).OrderBy(w => w.Order))
            {
                if (!appTitles.ContainsKey(saved.Id)) continue;
                windows.Add(new DeskWindow
                {
                    GuiId = AllocateStableWindowGuiId(ref nextWindowGuiId),
                    Id = saved.Id, Title = appTitles[saved.Id],
                    Rect = new Rect(saved.X, saved.Y,
                        saved.Width > 0f ? saved.Width : 710f,
                        saved.Height > 0f ? saved.Height : 500f),
                    Minimized = saved.Minimized
                });
            }
            ActivateFrontmostVisibleWindow();
        }

        private void SaveAll() { SaveCampaign(); SaveDesktop(); }

        public static string FormatStatus(DayReport report, bool isWon, bool isLost)
        {
            if (isLost) return "자원 고갈 — 생존 기록 종료";
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
            Color ink = InkColor;
            Texture2D whiteFill = Texture2D.whiteTexture;

            GUI.skin.label.normal.textColor = ink;
            SetAllTextColors(GUI.skin.label, ink);
            GUI.skin.button.normal.background = whiteFill;
            GUI.skin.button.hover.background = whiteFill;
            GUI.skin.button.active.background = whiteFill;
            GUI.skin.button.focused.background = whiteFill;
            GUI.skin.button.normal.textColor = ink;
            GUI.skin.button.hover.textColor = ink;
            GUI.skin.button.active.textColor = ink;
            GUI.skin.button.focused.textColor = ink;
            SetAllTextColors(GUI.skin.button, ink);
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
            SetAllTextColors(GUI.skin.window, ink);
            GUI.skin.window.border = new RectOffset();
            GUI.skin.window.padding = new RectOffset(8, 8, 24, 8);

            GUI.skin.box.normal.background = whiteFill;
            GUI.skin.box.normal.textColor = ink;
            SetAllTextColors(GUI.skin.box, ink);
            GUI.skin.box.border = new RectOffset();
            GUI.skin.scrollView.normal.background = whiteFill;
            SetAllTextColors(GUI.skin.textField, ink);
            float scrollbarWidth = RestoredScrollbarWidth(GUI.skin.verticalScrollbar.fixedWidth);
            GUI.skin.verticalScrollbar.fixedWidth = scrollbarWidth;
            GUI.skin.verticalScrollbarThumb.fixedWidth = scrollbarWidth;
            GUI.skin.verticalScrollbarUpButton.fixedWidth = scrollbarWidth;
            GUI.skin.verticalScrollbarDownButton.fixedWidth = scrollbarWidth;

            title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            section = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(6, 6, 3, 3)
            };
            section.normal.background = whiteFill;
            section.normal.textColor = ink;
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            desktopIcon = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            desktopIconLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                wordWrap = false
            };
            desktopBadge = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            warning = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
            warning.normal.textColor = ink;
            success = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
            success.normal.textColor = ink;
        }

        private static void SetAllTextColors(GUIStyle style, Color color)
        {
            if (style == null) return;
            style.normal.textColor = color;
            style.onNormal.textColor = color;
            style.hover.textColor = color;
            style.onHover.textColor = color;
            style.active.textColor = color;
            style.onActive.textColor = color;
            style.focused.textColor = color;
            style.onFocused.textColor = color;
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

        public static string DesktopIconName(string id)
        {
            switch (id)
            {
                case "mail": return "통신";
                case "proposal": return "제안서";
                case "priority": return "우선순위";
                case "gantt": return "작업";
                case "milestone": return "마일스톤";
                case "workers": return "대원";
                case "report": return "보고서";
                case "codex": return "도감";
                case "messenger": return "메신저";
                case "options": return "옵션";
                case "data": return "게임데이터";
                default: return "내정보";
            }
        }

        private static string DesktopIconGlyph(string id)
        {
            switch (id)
            {
                case "mail": return "MAIL";
                case "proposal": return "PROP";
                case "priority": return "PRIO";
                case "gantt": return "TASK";
                case "milestone": return "MILE";
                case "workers": return "CREW";
                case "report": return "RPT";
                case "codex": return "CODEX";
                case "messenger": return "CHAT";
                case "options": return "OPT";
                case "data": return "DATA";
                default: return "INFO";
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
