using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace ProjectW.IngameCore.CaseReview
{
public static class CaseReviewGame
{
    private static readonly IReportGenerator ReportGenerator = new TemplateReportGenerator();

    public static GameState Init(GameConfig config, int seed)
    {
        var state = new GameState
        {
            Config = config,
            Seed = seed,
            RngState = seed,
            TimeRemainingSec = config.MorningSeconds,
            RedirectBudget = config.RedirectBudgetPerDay,
            AuditBudget = config.AuditBudgetPerDay,
            InterviewBudget = config.InterviewBudgetPerDay
        };

        if (config.InitialData != null)
        {
            ApplyInitialData(state, config.InitialData);
        }
        else
        {
            SeedStaff(state);
            SeedDayOneCases(state);
        }

        BuildMorningPlan(state);
        DrawMorningCards(state);
        return state;
    }

    public static DispatchResult Dispatch(GameState state, string command, int wallclockDeltaSec = 0)
    {
        var lines = new List<string>();
        if (wallclockDeltaSec > 0)
        {
            lines.AddRange(Advance(state, wallclockDeltaSec).Lines);
        }

        var trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            return Result(true, lines);
        }

        state.CommandTape.Add(trimmed);
        var tokens = Tokenize(trimmed);
        var verb = tokens.Count == 0 ? "" : tokens[0].ToUpperInvariant();

        if (verb is "Q" or "QUIT" or "EXIT")
        {
            lines.Add("QUIT");
            return Result(true, lines);
        }

        return verb switch
        {
            "HELP" => Help(lines),
            "STATUS" => Status(state, lines),
            "TIME" => WithCost(state, lines, 0, () => lines.Add(StatusLine(state))),
            "PLAN" => WithCost(state, lines, 1, () => ShowPlan(state, lines)),
            "CONFIRM" => ConfirmPlan(state, tokens, lines),
            "ADJUST" => WithCost(state, lines, 6, () => AdjustPlan(state, tokens, lines)),
            "QUEUE" => WithCost(state, lines, 1, () => Queue(state, tokens, lines)),
            "OPEN" => WithCost(state, lines, 1, () => Open(state, tokens, lines)),
            "SUMMARY" or "SUM" => WithCost(state, lines, 1, () => Summary(state, tokens, lines)),
            "LOG" => WithCost(state, lines, 4, () => Log(state, tokens, lines)),
            "CHECK" => WithCost(state, lines, 3, () => Check(state, tokens, lines)),
            "ASSIGN" => WithCost(state, lines, 5, () => Assign(state, tokens, lines, redirect: false)),
            "APPROVE" => WithCost(state, lines, 1, () => Approve(state, tokens, lines)),
            "HOLD" => WithCost(state, lines, 2, () => Hold(state, tokens, lines)),
            "REDIRECT" => WithCost(state, lines, 6, () => Assign(state, tokens, lines, redirect: true)),
            "ADVANCE" => AdvanceCommand(state, tokens, lines),
            "REPORT" => ReportCommand(state, tokens, lines),
            "REVIEW" => ReviewCommand(state, tokens, lines),
            "REQUEST" => RequestApprovalCommand(state, tokens, lines),
            "SUBMIT" => SubmitApprovalCommand(state, tokens, lines),
            "APPROVALS" => ApprovalListCommand(state, lines),
            "REGENERATE" or "REGEN" => RegenerateCommand(state, tokens, lines),
            "NEXT" when tokens.Count > 1 && tokens[1].Equals("DAY", StringComparison.OrdinalIgnoreCase)
                => NextDay(state, lines),
            _ => Error("ERR001", "알 수 없는 명령입니다. HELP를 입력하십시오.", lines)
        };
    }

    public static TickResult Advance(GameState state, int deltaSec)
    {
        var lines = new List<string>();
        var remainingDelta = Math.Max(0, deltaSec);
        var slotChanged = false;

        while (remainingDelta > 0)
        {
            var step = Math.Min(remainingDelta, state.TimeRemainingSec);
            state.TotalElapsedSec += step;
            state.TimeRemainingSec -= step;
            remainingDelta -= step;

            TickOpenCases(state, step);
            lines.AddRange(AnnounceNewLogs(state));

            if (state.TimeRemainingSec == 0)
            {
                slotChanged = true;
                MoveNextSlot(state, lines);
                if (remainingDelta == 0)
                {
                    break;
                }
            }
        }

        RecalculateOverload(state);
        return new TickResult { Lines = lines, SlotChanged = slotChanged };
    }

    public static void ApplyScenarioEffects(
        GameState state,
        IReadOnlyList<ScenarioStateEffect> effects,
        string sourceEventId = "")
    {
        ScenarioEffectApplier.Apply(state, effects, sourceEventId);
    }

    public static string Snapshot(GameState state)
    {
        var serializer = new DataContractSerializer(typeof(GameState));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, state);
        return Convert.ToBase64String(stream.ToArray());
    }

    public static GameState Restore(string json)
    {
        var bytes = Convert.FromBase64String(json);
        var serializer = new DataContractSerializer(typeof(GameState));
        using var stream = new MemoryStream(bytes);
        return (GameState)(serializer.ReadObject(stream)
            ?? throw new InvalidOperationException("Snapshot restore failed."));
    }

    public static ReplayReport Replay(int seed, IReadOnlyList<string> commands)
    {
        var state = Init(new GameConfig(), seed);
        var transcript = new List<string>();
        foreach (var command in commands)
        {
            transcript.Add($"> {command}");
            transcript.AddRange(Dispatch(state, command).Lines);
        }

        var snapshot = Snapshot(state);
        return new ReplayReport
        {
            Seed = seed,
            CommandCount = commands.Count,
            Snapshot = snapshot,
            SnapshotHash = Sha256(snapshot),
            Transcript = transcript
        };
    }

    private static DispatchResult Help(List<string> lines)
    {
        lines.Add("명령: HELP, STATUS, TIME, PLAN, CONFIRM PLAN, ADJUST <id> <p1[,p2]>, QUEUE, OPEN <id>, SUMMARY <id>, LOG <id> <work|equip|rel|summary>, CHECK <id>");
        lines.Add("보고: REPORT, REPORT <id>, REPORT DAY, REVIEW <id>, REVIEW ALL, NEXT DAY");
        lines.Add("후속 실험: APPROVE <id>, HOLD <id>, REDIRECT <id> <p1[,p2]>, ASSIGN <id> <p1[,p2]>, ADVANCE <MORNING|EVENING>, QUIT");
        return Result(true, lines);
    }

    private static DispatchResult Status(GameState state, List<string> lines)
    {
        lines.Add(StatusLine(state));
        return Result(true, lines);
    }

    private static DispatchResult AdvanceCommand(GameState state, List<string> tokens, List<string> lines)
    {
        if (tokens.Count < 2)
        {
            return Error("ERR001", "ADVANCE에는 초 또는 슬롯 이름이 필요합니다.", lines);
        }

        var delta = ParseAdvanceDelta(state, tokens[1]);
        if (!state.Config.UseTimePressure && TryParseSlot(tokens[1], out var targetSlot))
        {
            return AdvanceToSlotWithoutTimePressure(state, targetSlot, lines);
        }

        if (state.Slot == Slot.Morning && !state.MorningPlan.Confirmed && delta > 0)
        {
            return Error("ERR071", "작업계획서가 아직 확정되지 않았습니다. PLAN 확인 후 CONFIRM PLAN 또는 ADJUST를 사용하십시오.", lines);
        }

        lines.AddRange(Advance(state, delta).Lines);
        lines.Add($"OK. {delta}초 경과.");
        return Result(true, lines, timeCost: delta);
    }

    private static DispatchResult WithCost(GameState state, List<string> lines, int costSec, Action action)
    {
        if (!state.Config.UseTimePressure)
        {
            action();
            var codeNow = FirstErrorCode(lines);
            return Result(string.IsNullOrEmpty(codeNow), lines, codeNow, timeCost: 0);
        }

        if (costSec > 0 && costSec >= state.TimeRemainingSec)
        {
            lines.Add("WARN101 이 명령을 실행하면 현재 슬롯이 종료됩니다.");
        }

        var before = lines.Count;
        action();
        var code = FirstErrorCode(lines.Skip(before));
        if (string.IsNullOrEmpty(code) && costSec > 0)
        {
            lines.AddRange(Advance(state, costSec).Lines);
            lines.Add($"OK. {costSec}초 경과.");
        }

        return Result(string.IsNullOrEmpty(code), lines, code, string.IsNullOrEmpty(code) ? costSec : 0);
    }

    private static void Queue(GameState state, List<string> tokens, List<string> lines)
    {
        var cases = state.Queue.Where(e => e.Status != CaseStatus.Closed);
        cases = tokens.Count > 1 && tokens[1].Equals("late", StringComparison.OrdinalIgnoreCase)
            ? cases.OrderBy(e => e.TtlSec)
            : cases.OrderByDescending(e => e.Urgency + e.Severity);

        foreach (var item in cases)
        {
            lines.Add($"{item.Id} | {item.Title} | 긴급 {item.Urgency} | 심각 {item.Severity} | TTL {FormatClock(item.TtlSec)} | {item.Status}");
        }
    }

    private static void ShowPlan(GameState state, List<string> lines)
    {
        RecordReviewCost(state, ReviewActionType.Plan, $"DAY-{state.Day:D2}", "plan");
        lines.Add($"DAY {state.MorningPlan.Day:D2} 작업계획서 {(state.MorningPlan.Confirmed ? "(확정됨)" : "(미확정)")}");
        foreach (var entry in state.MorningPlan.Entries)
        {
            var item = FindCaseById(state, entry.EventId);
            var title = item?.Title ?? "unknown";
            var adjusted = entry.Adjusted ? " / 조정됨" : "";
            lines.Add($"{entry.EventId} | {title} | 배정 {string.Join(",", entry.PlannedPersonnel)} | 근거 {entry.Reason}{adjusted}");
        }
    }

    private static DispatchResult ConfirmPlan(GameState state, List<string> tokens, List<string> lines)
    {
        if (state.Slot != Slot.Morning)
        {
            return Error("ERR012", "현재 슬롯에서는 사용할 수 없는 명령입니다.", lines);
        }

        if (tokens.Count > 1 && !tokens[1].Equals("PLAN", StringComparison.OrdinalIgnoreCase))
        {
            return Error("ERR001", "CONFIRM PLAN 형식입니다.", lines);
        }

        if (state.MorningPlan.Confirmed)
        {
            lines.Add("작업계획서는 이미 확정되었습니다.");
            return Result(true, lines);
        }

        if (state.Config.UseTimePressure && 8 >= state.TimeRemainingSec)
        {
            lines.Add("WARN101 이 명령을 실행하면 현재 슬롯이 종료됩니다.");
        }

        foreach (var entry in state.MorningPlan.Entries)
        {
            var item = FindCaseById(state, entry.EventId);
            if (item is null || item.Status == CaseStatus.Closed) continue;
            ApplyAssignment(state, item, entry.PlannedPersonnel);
            AddTruth(state, item.Id, item.AssignedPersonnel.FirstOrDefault() ?? "SYS", "PLAN_CONFIRMED", $"작업계획서 확정: {string.Join(",", item.AssignedPersonnel)}");
        }

        state.MorningPlan.Confirmed = true;
        state.ReplacementPressure = Rules(state).ReplacementPressurePolicy.AfterPlanConfirmed(state, state.ReplacementPressure);
        lines.Add("OK. 작업계획서를 확정했습니다.");
        lines.Add("운영 시뮬레이션을 진행합니다...");
        state.TotalElapsedSec += state.Config.NoonSeconds;
        SimulateConfirmedPlan(state, lines);
        state.Reports.Add(ReportGenerator.Generate(state));
        state.Slot = Slot.Evening;
        state.TimeRemainingSec = state.Config.UseTimePressure ? state.Config.EveningSeconds : 0;
        RecalculateOverload(state);
        lines.Add("== EVENING 평가 슬롯 시작 ==");
        lines.Add("보고서가 생성되었습니다. REPORT를 입력해 검토하십시오.");
        return Result(true, lines, timeCost: 0);
    }

    private static void AdjustPlan(GameState state, List<string> tokens, List<string> lines)
    {
        if (state.Slot != Slot.Morning)
        {
            ErrorLine("ERR012", "현재 슬롯에서는 사용할 수 없는 명령입니다.", lines);
            return;
        }

        if (state.MorningPlan.Confirmed)
        {
            ErrorLine("ERR072", "이미 확정된 작업계획서는 조정할 수 없습니다.", lines);
            return;
        }

        if (tokens.Count < 3)
        {
            ErrorLine("ERR001", "ADJUST <eventId> <p1[,p2...]> 형식입니다.", lines);
            return;
        }

        var entry = state.MorningPlan.Entries.FirstOrDefault(e => e.EventId.Equals(tokens[1], StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            ErrorLine("ERR021", "대상 이벤트를 찾을 수 없습니다.", lines);
            return;
        }

        var clearAssignment = tokens.Count == 3
            && (tokens[2].Equals("none", StringComparison.OrdinalIgnoreCase)
                || tokens[2].Equals("clear", StringComparison.OrdinalIgnoreCase));
        var people = clearAssignment ? new List<string>() : ParsePeople(tokens.Skip(2));
        if (!clearAssignment && !ValidPeople(state, people))
        {
            ErrorLine("ERR041", "인력 지정이 잘못되었습니다.", lines);
            return;
        }

        entry.PlannedPersonnel = people;
        entry.Adjusted = true;
        entry.Reason = "관리자 조정";
        var assigned = people.Count == 0 ? "none" : string.Join(",", people);
        lines.Add($"OK. {entry.EventId} 계획 배정을 {assigned}로 조정했습니다.");
    }

    private static void Open(GameState state, List<string> tokens, List<string> lines)
    {
        var item = FindCase(state, tokens, lines);
        if (item is null) return;

        state.OpenEventId = item.Id;
        lines.Add($"[{item.Id}] {item.Title}");
        lines.Add($"종류 {item.Kind} / 하위계통 {item.Subsystem} / 긴급도 {item.Urgency} / 심각도 {item.Severity} / 상태 {item.Status}");
        if (!string.IsNullOrWhiteSpace(item.ResultSummary))
        {
            lines.Add($"결과: {item.ResultSummary}");
        }

        lines.Add($"마지막 표시 source: {LastVisibleSource(state, item.Id)}");
    }

    private static void Summary(GameState state, List<string> tokens, List<string> lines)
    {
        var item = FindCase(state, tokens, lines);
        if (item is null) return;

        RecordReviewCost(state, ReviewActionType.Summary, item.Id, "summary");
        item.SummaryRead = true;
        var log = state.Logs.FirstOrDefault(l => l.EventId == item.Id && l.SourceType == "summary" && l.VisibleAtSec <= state.TotalElapsedSec);
        lines.Add(log?.Text ?? $"[SUMMARY][{item.Id}] 관리 요약이 아직 정리되지 않았습니다.");
        if (log is not null) log.Read = true;
    }

    private static void Log(GameState state, List<string> tokens, List<string> lines)
    {
        if (tokens.Count < 3)
        {
            ErrorLine("ERR001", "LOG <eventId> <work|equip|rel|summary> 형식입니다.", lines);
            return;
        }

        var item = FindCaseById(state, tokens[1]);
        if (item is null)
        {
            ErrorLine("ERR021", "대상 이벤트를 찾을 수 없습니다.", lines);
            return;
        }

        var source = tokens[2].ToLowerInvariant();
        RecordReviewCost(state, ReviewActionType.Log, item.Id, source);
        var logs = state.Logs
            .Where(l => l.EventId == item.Id && l.SourceType.Equals(source, StringComparison.OrdinalIgnoreCase) && l.VisibleAtSec <= state.TotalElapsedSec)
            .OrderBy(l => l.VisibleAtSec)
            .ToList();

        if (logs.Count == 0)
        {
            ErrorLine("ERR031", "요청한 로그 소스가 아직 도착하지 않았습니다.", lines);
            return;
        }

        foreach (var log in logs)
        {
            log.Read = true;
            lines.Add(log.Text);
        }
    }

    private static void Check(GameState state, List<string> tokens, List<string> lines)
    {
        var item = FindCase(state, tokens, lines);
        if (item is null) return;

        RecordReviewCost(state, ReviewActionType.Check, item.Id, "check");
        var visible = state.Logs.Where(l => l.EventId == item.Id && l.VisibleAtSec <= state.TotalElapsedSec).ToList();
        var warnings = new List<string>();
        if (!visible.Any(l => l.SourceType == "work")) warnings.Add("work 로그 공백 존재");
        if (visible.Any(l => l.SourceType == "equip" && l.Text.IndexOf("SIGNAL_LOSS", StringComparison.OrdinalIgnoreCase) >= 0)) warnings.Add("sensorDropSec >= 3");
        if (visible.Any(l => l.Distorted)) warnings.Add("왜곡 가능성이 있는 관계/요약 로그");
        if (item.AssignedPersonnel.Count == 1 && item.Severity >= 70) warnings.Add("고심각도 사건 단독 배정");

        if (warnings.Count == 0)
        {
            lines.Add("CHECK OK: 현재 서류층에서 경고를 찾지 못했습니다.");
        }
        else
        {
            lines.Add($"경고 {warnings.Count}건:");
            foreach (var warning in warnings) lines.Add($"- {warning}");
        }
    }

    private static void Assign(GameState state, List<string> tokens, List<string> lines, bool redirect)
    {
        if (redirect && state.Slot != Slot.Noon)
        {
            ErrorLine("ERR012", "현재 슬롯에서는 사용할 수 없는 명령입니다.", lines);
            return;
        }

        if (!redirect && state.Slot == Slot.Evening)
        {
            ErrorLine("ERR012", "현재 슬롯에서는 사용할 수 없는 명령입니다.", lines);
            return;
        }

        if (tokens.Count < 3)
        {
            ErrorLine("ERR001", $"{(redirect ? "REDIRECT" : "ASSIGN")} <eventId> <p1[,p2...]> 형식입니다.", lines);
            return;
        }

        var item = FindCaseById(state, tokens[1]);
        if (item is null)
        {
            ErrorLine("ERR021", "대상 이벤트를 찾을 수 없습니다.", lines);
            return;
        }

        if (item.Status == CaseStatus.Closed)
        {
            ErrorLine("ERR022", "대상 이벤트는 이미 종결되었습니다.", lines);
            return;
        }

        if (redirect && state.RedirectBudget <= 0)
        {
            ErrorLine("ERR051", "리다이렉트 예산이 없습니다.", lines);
            return;
        }

        var people = ParsePeople(tokens.Skip(2));
        if (!ValidPeople(state, people))
        {
            ErrorLine("ERR041", "대상 인력이 현재 다른 작업에 묶여 있습니다.", lines);
            return;
        }

        ApplyAssignment(state, item, people);

        if (redirect)
        {
            state.RedirectBudget--;
            item.Redirected = true;
            item.TtlSec = Math.Max(item.TtlSec, 30);
            item.LatentRisk = Math.Max(0, item.LatentRisk - 18);
            AddTruth(state, item.Id, item.AssignedPersonnel[0], "REDIRECT", "재배정 후 원인 계통부터 재확인");
            AddLogFromTruth(state, item, "work", state.TotalElapsedSec + 10);
            AddLogFromTruth(state, item, "rel", state.TotalElapsedSec + 16);
            lines.Add($"OK. {item.Id} 재지시: {string.Join(",", item.AssignedPersonnel)}");
            lines.Add("예상 결과 +18 / 예상 지연 +14초");
        }
        else
        {
            AddTruth(state, item.Id, item.AssignedPersonnel[0], "ASSIGN", "초기 배정 접수");
            lines.Add($"OK. {item.Id} 배정: {string.Join(",", item.AssignedPersonnel)}");
        }
    }

    private static void Approve(GameState state, List<string> tokens, List<string> lines)
    {
        if (state.Slot == Slot.Morning)
        {
            ErrorLine("ERR012", "현재 슬롯에서는 사용할 수 없는 명령입니다.", lines);
            return;
        }

        var item = FindCase(state, tokens, lines);
        if (item is null) return;
        if (item.Status == CaseStatus.Closed)
        {
            ErrorLine("ERR022", "대상 이벤트는 이미 종결되었습니다.", lines);
            return;
        }

        var readSources = state.Logs.Where(l => l.EventId == item.Id && l.Read).Select(l => l.SourceType).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        item.ApprovedFromSummaryOnly = item.SummaryRead && readSources.All(s => s == "summary");
        var unresolved = Math.Max(0, item.MismatchScore - (item.Redirected ? 5 : 0));
        if (item.ApprovedFromSummaryOnly) unresolved += 4;
        item.LatentRisk = Clamp(item.LatentRisk + unresolved * 8 - (item.Redirected ? 15 : 0), 0, 100);
        item.Status = CaseStatus.Closed;
        state.ReplacementPressure = Rules(state).ReplacementPressurePolicy.AfterApproval(state, item, state.ReplacementPressure);
        state.GlobalLatentRisk = Clamp(state.Queue.Where(e => e.Status != CaseStatus.Closed).Sum(e => e.LatentRisk) + item.LatentRisk, 0, 200);
        lines.Add($"OK. {item.Id} 사건 종결.");
        lines.Add($"잠복 리스크: {RiskBand(item.LatentRisk)}");
    }

    private static void Hold(GameState state, List<string> tokens, List<string> lines)
    {
        if (state.Slot == Slot.Morning)
        {
            ErrorLine("ERR012", "현재 슬롯에서는 사용할 수 없는 명령입니다.", lines);
            return;
        }

        var item = FindCase(state, tokens, lines);
        if (item is null) return;
        item.Status = CaseStatus.Held;
        item.HoldCount++;
        item.TtlSec = Math.Max(35, item.TtlSec - 6);
        AddTruth(state, item.Id, item.AssignedPersonnel.FirstOrDefault() ?? "SYS", "HOLD", "추가 source 요청");
        AddLogFromTruth(state, item, "work", state.TotalElapsedSec + 8);
        lines.Add($"OK. {item.Id} 보류. 추가 work 로그를 요청했습니다.");
    }

    private static void ReportDay(GameState state, List<string> lines)
    {
        var closed = state.Queue.Count(e => e.Status == CaseStatus.Closed);
        var open = state.Queue.Count(e => e.Status != CaseStatus.Closed);
        lines.Add($"DAY {state.Day:D2} REPORT");
        lines.Add($"종결 {closed} / 미결 {open} / OVR {state.Overload} / 잠복 리스크 {state.GlobalLatentRisk}");
        foreach (var item in state.Queue.Where(e => e.AutoResolved).OrderByDescending(e => e.Severity))
        {
            lines.Add($"{item.Id}: {item.ResultSummary}");
        }

        foreach (var staff in state.Staff)
        {
            lines.Add($"{staff.Id} {staff.Name}: LOAD {LoadBand(staff)}, 기류 {TrustBand(staff)}");
        }
    }

    private static DispatchResult ReportCommand(GameState state, List<string> tokens, List<string> lines)
    {
        if (tokens.Count > 1 && tokens[1].Equals("DAY", StringComparison.OrdinalIgnoreCase))
        {
            ReportDay(state, lines);
            return Result(true, lines);
        }

        if (tokens.Count > 1)
        {
            var item = FindCaseById(state, tokens[1]);
            if (item is null)
            {
                return Error("ERR021", "대상 이벤트를 찾을 수 없습니다.", lines);
            }

            if (!item.AutoResolved)
            {
                return Error("ERR082", "아직 결과가 생성되지 않은 작업입니다.", lines);
            }

            RecordReviewCost(state, ReviewActionType.Report, item.Id, "event-report");
            AddBodyLines(lines, ReportGenerator.GenerateEventReport(state, item));
            return Result(true, lines);
        }

        var report = state.Reports.LastOrDefault(r => r.Day == state.Day) ?? state.Reports.LastOrDefault();
        if (report is null)
        {
            return Error("ERR081", "아직 생성된 보고서가 없습니다. CONFIRM PLAN 이후 다시 시도하십시오.", lines);
        }

        RecordReviewCost(state, ReviewActionType.Report, $"DAY-{state.Day:D2}", "daily-report");
        AddBodyLines(lines, report.Body);
        return Result(true, lines);
    }

    private static DispatchResult ReviewCommand(GameState state, List<string> tokens, List<string> lines)
    {
        if (tokens.Count < 2)
        {
            return Error("ERR001", "REVIEW <eventId|ALL> 형식입니다.", lines);
        }

        if (tokens[1].Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            RecordReviewCost(state, ReviewActionType.Review, $"DAY-{state.Day:D2}", "all");
            var awarded = 0;
            foreach (var item in state.Queue.Where(e => e.AutoResolved))
            {
                item.ReportReviewed = true;
                awarded += GrantMeritTokens(state, Rules(state).MeritTokenPolicy.AwardForReportReview(state, item));
            }

            if (awarded > 0)
            {
                lines.Add($"MERIT +{awarded}. Report review produced usable filing credit.");
            }

            lines.Add("OK. 모든 개별 작업 리포트를 검토 완료로 표시했습니다.");
            return Result(true, lines);
        }

        var target = FindCaseById(state, tokens[1]);
        if (target is null)
        {
            return Error("ERR021", "대상 이벤트를 찾을 수 없습니다.", lines);
        }

        if (!target.AutoResolved)
        {
            return Error("ERR082", "아직 결과가 생성되지 않은 작업입니다.", lines);
        }

        RecordReviewCost(state, ReviewActionType.Review, target.Id, "event-review");
        target.ReportReviewed = true;
        var reviewAward = GrantMeritTokens(state, Rules(state).MeritTokenPolicy.AwardForReportReview(state, target));
        if (reviewAward > 0)
        {
            lines.Add($"MERIT +{reviewAward}. Report review produced usable filing credit.");
        }

        lines.Add($"OK. {target.Id} 리포트 검토 완료.");
        return Result(true, lines);
    }

    private static DispatchResult NextDay(GameState state, List<string> lines)
    {
        if (state.Slot != Slot.Evening)
        {
            return Error("ERR012", "다음날 이동은 저녁 평가 슬롯에서만 가능합니다.", lines);
        }

        var pending = state.Queue.Where(e => e.AutoResolved && !e.ReportReviewed).Select(e => e.Id).ToList();
        if (pending.Count > 0)
        {
            return Error("ERR091", $"검토하지 않은 개별 리포트가 있습니다: {string.Join(", ", pending)}. REVIEW <id> 또는 REVIEW ALL을 사용하십시오.", lines);
        }

        MoveNextSlot(state, lines);
        return Result(true, lines);
    }

    private static DispatchResult RequestApprovalCommand(GameState state, List<string> tokens, List<string> lines)
    {
        if (tokens.Count < 3)
        {
            return Error("ERR001", "REQUEST <REGENERATION|REPORT|AUDIT|EXPENSE> <targetId> format required.", lines);
        }

        if (!TryParseApprovalKind(tokens[1], out var kind))
        {
            return Error("ERR001", "Unknown approval request kind.", lines);
        }

        if (kind == ApprovalRequestKind.Regeneration
            && (state.Slot != Slot.Morning || state.MorningPlan?.Confirmed == true))
        {
            return Error("ERR112", "Regeneration approval is available only before morning plan approval.", lines);
        }

        if (kind == ApprovalRequestKind.Regeneration
            && !state.Staff.Any(person => person.Id.Equals(tokens[2], StringComparison.OrdinalIgnoreCase) && !person.HasLeft))
        {
            return Error("ERR041", "Regeneration target personnel not found.", lines);
        }

        var existing = state.ApprovalRequests.FirstOrDefault(request =>
            request.Kind == kind
            && request.TargetId.Equals(tokens[2], StringComparison.OrdinalIgnoreCase)
            && request.Status == ApprovalStatus.Draft);
        if (existing is not null)
        {
            lines.Add($"APPROVAL {existing.Id} already pending. Required tokens {existing.RequiredTokens}. MERIT {state.MeritTokens}.");
            return Result(true, lines);
        }

        var request = new ApprovalRequest
        {
            Id = NextApprovalId(state),
            Day = state.Day,
            Kind = kind,
            TargetId = tokens[2],
            RequiredTokens = Rules(state).ApprovalPolicy.RequiredTokens(kind),
            Hint = "draft"
        };
        state.ApprovalRequests.Add(request);
        lines.Add($"APPROVAL {request.Id} opened: {request.Kind} / {request.TargetId} / requires {request.RequiredTokens} merit tokens. MERIT {state.MeritTokens}.");
        return Result(true, lines);
    }

    private static DispatchResult SubmitApprovalCommand(GameState state, List<string> tokens, List<string> lines)
    {
        if (tokens.Count < 4 || !tokens[1].Equals("APPROVAL", StringComparison.OrdinalIgnoreCase))
        {
            return Error("ERR001", "SUBMIT APPROVAL <requestId> <tokens> format required.", lines);
        }

        var request = state.ApprovalRequests.FirstOrDefault(item => item.Id.Equals(tokens[2], StringComparison.OrdinalIgnoreCase));
        if (request is null)
        {
            return Error("ERR121", "Approval request not found.", lines);
        }

        if (request.Status is ApprovalStatus.Executed or ApprovalStatus.Approved or ApprovalStatus.ConditionalApproved)
        {
            return Error("ERR122", "Approval request is already resolved.", lines);
        }

        if (!int.TryParse(tokens[3], out var submittedTokens) || submittedTokens < 0)
        {
            return Error("ERR001", "Submitted token count must be a non-negative integer.", lines);
        }

        if (submittedTokens > state.MeritTokens)
        {
            return Error("ERR123", $"Not enough merit tokens. MERIT {state.MeritTokens}.", lines);
        }

        var decision = Rules(state).ApprovalPolicy.Evaluate(state, request, submittedTokens);
        request.SubmittedTokens = submittedTokens;
        request.Status = decision.Status;
        request.Hint = decision.Hint;

        if (decision.Status == ApprovalStatus.Rejected)
        {
            lines.Add($"REJECTED {request.Id}. Hint: {request.Hint}. MERIT {state.MeritTokens}.");
            return Result(false, lines);
        }

        state.MeritTokens -= submittedTokens;
        lines.Add($"{decision.Status.ToString().ToUpperInvariant()} {request.Id}. Spent {submittedTokens} merit tokens. Hint: {request.Hint}. MERIT {state.MeritTokens}.");

        if (request.Kind == ApprovalRequestKind.Regeneration)
        {
            var result = ExecuteRegeneration(state, request.TargetId, lines);
            if (result.Success)
            {
                request.Status = ApprovalStatus.Executed;
                request.Hint = decision.Status == ApprovalStatus.ConditionalApproved
                    ? "conditional regeneration executed; audit trail remains warm"
                    : "regeneration executed";
            }

            return result;
        }

        request.Status = ApprovalStatus.Executed;
        return Result(true, lines);
    }

    private static DispatchResult ApprovalListCommand(GameState state, List<string> lines)
    {
        lines.Add($"MERIT {state.MeritTokens}");
        if (state.ApprovalRequests.Count == 0)
        {
            lines.Add("No approval requests.");
            return Result(true, lines);
        }

        foreach (var request in state.ApprovalRequests.OrderByDescending(item => item.Day).ThenBy(item => item.Id))
        {
            lines.Add($"{request.Id} | {request.Status} | {request.Kind} {request.TargetId} | {request.SubmittedTokens}/{request.RequiredTokens} | {request.Hint}");
        }

        return Result(true, lines);
    }

    private static DispatchResult RegenerateCommand(GameState state, List<string> tokens, List<string> lines)
    {
        if (tokens.Count < 2)
        {
            return Error("ERR001", "REGENERATE <personnelId> format required.", lines);
        }

        return RequestApprovalCommand(state, new List<string> { "REQUEST", "REGENERATION", tokens[1] }, lines);
    }

    private static DispatchResult ExecuteRegeneration(GameState state, string personnelId, List<string> lines)
    {
        if (state.Slot != Slot.Morning || state.MorningPlan?.Confirmed == true)
        {
            return Error("ERR112", "재생성은 아침 작업계획 확정 전 캐릭터 패널에서만 처리할 수 있습니다.", lines);
        }

        var source = state.Staff.FirstOrDefault(person => person.Id.Equals(personnelId, StringComparison.OrdinalIgnoreCase));
        if (source is null || source.HasLeft)
        {
            return Error("ERR041", "재생성 대상 인력을 찾을 수 없습니다.", lines);
        }

        var sourceId = source.Id;
        var lineageId = string.IsNullOrWhiteSpace(source.CloneLineageId) ? $"LINE-{sourceId}" : source.CloneLineageId;
        var nextVersion = Math.Max(1, source.CloneVersion) + 1;
        var regeneratedId = NextRegeneratedPersonnelId(state, sourceId, lineageId, nextVersion);
        var archivedMemoryCount = source.Memories.Count;
        var archivedRelationshipCount = source.Relationships.Count;
        var persistentPerks = source.Perks.Where(perk => perk.ClonePersistent).Select(ClonePerk).ToList();
        var persistentTraits = source.TraitSamples
            .Where(trait => trait.ClonePersistent)
            .Select(CloneTraitSample)
            .ToList();

        foreach (var observer in state.Staff.Where(person => !person.HasLeft && !person.Id.Equals(sourceId, StringComparison.OrdinalIgnoreCase)))
        {
            var previous = observer.Relationships.FirstOrDefault(rel => rel.TargetId.Equals(sourceId, StringComparison.OrdinalIgnoreCase));
            observer.Relationships.RemoveAll(rel => rel.TargetId.Equals(sourceId, StringComparison.OrdinalIgnoreCase));
            var inheritedTrust = previous is null ? -4 : Clamp((previous.Trust - 50) / 4, -18, 12);
            var inheritedAffinity = previous is null ? -6 : Clamp((previous.Affinity - 50) / 5, -16, 10);
            observer.Relationships.Add(new PersonnelRelationship
            {
                TargetId = regeneratedId,
                Trust = inheritedTrust,
                Affinity = inheritedAffinity,
                Resentment = previous is null ? 6 : Clamp(previous.Resentment + Math.Max(0, 45 - previous.Trust) / 4, -100, 100),
                Reliability = previous is null ? 0 : Clamp(previous.Reliability / 3, -100, 100),
                Note = $"{sourceId} 재생성 이후 같은 계보에 남은 경계"
            });
            observer.Memories.Add(new PersonnelMemory
            {
                Id = $"mem.regen.{state.Day:00}.{sourceId}.{observer.Id}",
                TargetId = regeneratedId,
                Type = "Clone",
                Valence = "Mixed",
                Intensity = Clamp(28 + Math.Max(0, previous?.Affinity ?? 0) / 4, 0, 100),
                Decay = 18,
                Tags = new List<string> { "clone", "regeneration", lineageId },
                SourceEventId = $"regen.{sourceId}",
                DayCreated = state.Day,
                Note = $"{source.Name} 계보가 재생성되었다는 운영 기억"
            });
        }

        source.Id = regeneratedId;
        source.CloneLineageId = lineageId;
        source.CloneVersion = nextVersion;
        source.RegenerationCount++;
        source.RegeneratedFromId = sourceId;
        source.Name = $"{source.Name} R{source.CloneVersion}";
        source.PhysicalEnergy = 100;
        source.MentalStress = Clamp(source.MentalStress / 3, 0, 100);
        source.LoadAssigned = 0;
        source.Fatigue = 0;
        source.Stagnation = Clamp(source.Stagnation / 2, 0, 100);
        source.TrustToManager = Clamp(source.TrustToManager - 8, -100, 100);
        source.RetentionRisk = Clamp(source.RetentionRisk / 2, 0, 100);
        source.DaysSinceJoined = 0;
        source.Relationships.Clear();
        source.Memories.Clear();
        source.Perks = persistentPerks;
        source.TraitSamples = persistentTraits;
        if (persistentTraits.Count > 0)
        {
            source.Memories.Add(new PersonnelMemory
            {
                Id = $"mem.residue.{state.Day:00}.{regeneratedId}",
                TargetId = regeneratedId,
                Type = "Clone",
                Valence = "Mixed",
                Intensity = Clamp(persistentTraits.Max(trait => trait.Strength) / 2, 8, 45),
                Decay = 30,
                Tags = new List<string> { "lineage-residue", lineageId },
                SourceEventId = $"regen.{sourceId}",
                DayCreated = state.Day,
                Note = "이전 개체의 전문 기억이 아니라 낮은 강도의 계보 잔향"
            });
        }

        ReplacePersonnelIdInPlans(state, sourceId, regeneratedId);
        foreach (var card in state.MorningCards.Where(card => card.OwnerPersonnelId.Equals(sourceId, StringComparison.OrdinalIgnoreCase)))
        {
            card.OwnerPersonnelId = regeneratedId;
        }

        state.ReplacementPressure = Clamp(state.ReplacementPressure + 6, 0, 100);
        state.GlobalLatentRisk = Clamp(state.GlobalLatentRisk + 4, 0, 200);
        AddTruth(state, $"regen.{sourceId}", "CLONE_BAY", "REGENERATE", $"{sourceId} -> {regeneratedId}. memories {archivedMemoryCount}, relationships {archivedRelationshipCount} archived.");
        lines.Add($"OK. {sourceId} 계보를 {regeneratedId}로 재생성했습니다.");
        lines.Add($"APPROVAL EXECUTED. 기억 {archivedMemoryCount}건, 관계 {archivedRelationshipCount}건을 활성 상태에서 제거/보관 처리했습니다.");
        lines.Add($"조직 반응 기록 생성. AI 대체 압력 +6, 글로벌 리스크 +4.");
        return Result(true, lines);
    }

    private static string NextRegeneratedPersonnelId(GameState state, string sourceId, string lineageId, int nextVersion)
    {
        var compactLineage = lineageId
            .Replace("LINE-", "")
            .Replace("line-", "")
            .Replace("_", "")
            .Replace("-", "");
        if (string.IsNullOrWhiteSpace(compactLineage))
        {
            compactLineage = sourceId.Replace("-", "");
        }

        compactLineage = compactLineage.Length > 5 ? compactLineage[..5] : compactLineage;
        var candidate = $"{compactLineage}-R{nextVersion:D2}".ToUpperInvariant();
        var suffix = 2;
        while (state.Staff.Any(person => person.Id.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{compactLineage}-R{nextVersion:D2}-{suffix}".ToUpperInvariant();
            suffix++;
        }

        return candidate;
    }

    private static void ReplacePersonnelIdInPlans(GameState state, string sourceId, string regeneratedId)
    {
        foreach (var entry in state.MorningPlan?.Entries ?? Enumerable.Empty<WorkPlanEntry>())
        {
            for (var index = 0; index < entry.PlannedPersonnel.Count; index++)
            {
                if (entry.PlannedPersonnel[index].Equals(sourceId, StringComparison.OrdinalIgnoreCase))
                {
                    entry.PlannedPersonnel[index] = regeneratedId;
                    entry.Adjusted = true;
                    entry.Reason = "재생성으로 인한 계보 id 치환";
                }
            }
        }

        foreach (var item in state.Queue)
        {
            for (var index = 0; index < item.AssignedPersonnel.Count; index++)
            {
                if (item.AssignedPersonnel[index].Equals(sourceId, StringComparison.OrdinalIgnoreCase))
                {
                    item.AssignedPersonnel[index] = regeneratedId;
                }
            }
        }
    }

    private static EventCase FindCase(GameState state, List<string> tokens, List<string> lines)
    {
        var id = tokens.Count > 1 ? tokens[1] : state.OpenEventId;
        if (string.IsNullOrWhiteSpace(id))
        {
            ErrorLine("ERR021", "대상 이벤트를 찾을 수 없습니다.", lines);
            return null;
        }

        var item = FindCaseById(state, id);
        if (item is null)
        {
            ErrorLine("ERR021", "대상 이벤트를 찾을 수 없습니다.", lines);
        }

        return item;
    }

    private static EventCase FindCaseById(GameState state, string id)
    {
        return state.Queue.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private static DispatchResult Error(string code, string message, List<string> lines)
    {
        ErrorLine(code, message, lines);
        return Result(false, lines, code);
    }

    private static void ErrorLine(string code, string message, List<string> lines) => lines.Add($"{code} {message}");

    private static string FirstErrorCode(IEnumerable<string> lines)
    {
        var error = lines.FirstOrDefault(l => l.StartsWith("ERR", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(error)) return "";
        var space = error.IndexOf(' ');
        return space > 0 ? error[..space] : error;
    }

    private static void AddBodyLines(List<string> lines, string body)
    {
        lines.AddRange(body.TrimEnd()
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line => line.Length > 0));
    }

    private static DispatchResult Result(bool success, List<string> lines, string code = "", int timeCost = 0)
    {
        return new DispatchResult { Success = success, Code = code, Lines = lines, TimeCostSec = timeCost };
    }

    private static List<string> Tokenize(string command)
    {
        return command
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .ToList();
    }

    private static int ParseAdvanceDelta(GameState state, string value)
    {
        if (int.TryParse(value, out var seconds)) return Math.Max(0, seconds);
        return value.ToUpperInvariant() switch
        {
            "MORNING" => SecondsUntilSlot(state, Slot.Morning),
            "NOON" => SecondsUntilSlot(state, Slot.Noon),
            "EVENING" => SecondsUntilSlot(state, Slot.Evening),
            _ => 0
        };
    }

    private static bool TryParseSlot(string value, out Slot slot)
    {
        switch (value.ToUpperInvariant())
        {
            case "MORNING":
                slot = Slot.Morning;
                return true;
            case "NOON":
                slot = Slot.Noon;
                return true;
            case "EVENING":
                slot = Slot.Evening;
                return true;
            default:
                slot = Slot.Morning;
                return false;
        }
    }

    private static DispatchResult AdvanceToSlotWithoutTimePressure(GameState state, Slot targetSlot, List<string> lines)
    {
        if (state.Slot == targetSlot)
        {
            lines.Add($"이미 {targetSlot.ToString().ToUpperInvariant()} 슬롯입니다.");
            return Result(true, lines);
        }

        if (state.Slot == Slot.Morning && !state.MorningPlan.Confirmed)
        {
            return Error("ERR071", "작업계획서가 아직 확정되지 않았습니다. PLAN 확인 후 CONFIRM PLAN 또는 ADJUST를 사용하십시오.", lines);
        }

        if (state.Slot == Slot.Evening && targetSlot == Slot.Morning)
        {
            MoveNextSlot(state, lines);
            return Result(true, lines);
        }

        if (state.Slot == Slot.Noon && targetSlot == Slot.Evening)
        {
            state.Slot = Slot.Evening;
            state.TimeRemainingSec = 0;
            lines.Add("== EVENING 평가 슬롯 시작 ==");
            return Result(true, lines);
        }

        return Error("ERR012", "현재 구조에서는 CONFIRM PLAN으로 운영을 실행하거나, EVENING에서 ADVANCE MORNING으로 하루를 마감하십시오.", lines);
    }

    private static int SecondsUntilSlot(GameState state, Slot target)
    {
        if (state.Slot == target) return 0;
        return state.Slot switch
        {
            Slot.Morning when target == Slot.Noon => state.TimeRemainingSec,
            Slot.Morning when target == Slot.Evening => state.TimeRemainingSec + state.Config.NoonSeconds,
            Slot.Noon when target == Slot.Evening => state.TimeRemainingSec,
            Slot.Noon when target == Slot.Morning => state.TimeRemainingSec + state.Config.EveningSeconds,
            Slot.Evening when target == Slot.Morning => state.TimeRemainingSec,
            Slot.Evening when target == Slot.Noon => state.TimeRemainingSec + state.Config.MorningSeconds,
            _ => 0
        };
    }

    private static void MoveNextSlot(GameState state, List<string> lines)
    {
        switch (state.Slot)
        {
            case Slot.Morning:
                state.Slot = Slot.Noon;
                state.TimeRemainingSec = state.Config.UseTimePressure ? state.Config.NoonSeconds : 0;
                lines.Add("== NOON 운영 슬롯 시작 ==");
                break;
            case Slot.Noon:
                state.Slot = Slot.Evening;
                state.TimeRemainingSec = state.Config.UseTimePressure ? state.Config.EveningSeconds : 0;
                lines.Add("== EVENING 평가 슬롯 시작 ==");
                break;
            case Slot.Evening:
                state.Day++;
                state.Slot = Slot.Morning;
                state.TimeRemainingSec = state.Config.UseTimePressure ? state.Config.MorningSeconds : 0;
                state.RedirectBudget = state.Config.RedirectBudgetPerDay;
                state.AuditBudget = state.Config.AuditBudgetPerDay;
                state.InterviewBudget = state.Config.InterviewBudgetPerDay;
                foreach (var person in state.Staff)
                {
                    if (person.HasLeft) continue;
                    person.Fatigue = Clamp(person.Fatigue + Math.Max(0, person.LoadAssigned - person.OptHigh) * 3 - 2, 0, 100);
                    person.Stagnation = Clamp(person.Stagnation + Math.Max(0, person.OptLow - person.LoadAssigned) * 2, 0, 100);
                    person.RetentionRisk = Clamp(
                        person.RetentionRisk
                        + Math.Max(0, person.LoadAssigned - person.OptHigh) * 8
                        + Math.Max(0, person.Fatigue - 55) / 2
                        + Math.Max(0, 45 - person.TrustToManager) / 2
                        + Math.Max(0, person.Stagnation - 60) / 3
                        - 6,
                        0,
                        100);
                    person.DaysSinceJoined++;
                    person.LoadAssigned = 0;
                }

                ResolveAttrition(state, lines);
                SeedNextDayCases(state, lines);
                ScenarioEffectApplier.AdvanceDay(state);
                BuildMorningPlan(state);
                DrawMorningCards(state);
                lines.Add($"== DAY {state.Day:D2} MORNING 지시 슬롯 시작 ==");
                break;
        }
    }

    private static void TickOpenCases(GameState state, int step)
    {
        if (state.Slot != Slot.Noon) return;
        foreach (var item in state.Queue.Where(e => e.Status is CaseStatus.Open or CaseStatus.Held))
        {
            item.TtlSec = Math.Max(0, item.TtlSec - step);
            if (item.TtlSec == 0 && item.Status != CaseStatus.Escalated)
            {
                item.Status = CaseStatus.Escalated;
                item.Urgency = Clamp(item.Urgency + 10, 0, 100);
                item.Severity = Clamp(item.Severity + 5, 0, 100);
                item.LatentRisk = Clamp(item.LatentRisk + 12, 0, 100);
            }
        }
    }

    private static List<string> AnnounceNewLogs(GameState state)
    {
        var arrived = state.Logs
            .Where(l => !l.Announced && l.VisibleAtSec <= state.TotalElapsedSec)
            .OrderBy(l => l.VisibleAtSec)
            .ToList();
        foreach (var log in arrived) log.Announced = true;
        if (arrived.Count == 0) return new List<string>();

        var lines = new List<string> { "수신:" };
        lines.AddRange(arrived.Select(l => l.Text));
        return lines;
    }

    private static void RecalculateOverload(GameState state)
    {
        var q = state.Queue.Count(e => e.Status != CaseStatus.Closed);
        var delta = 2 * Math.Max(0, q - state.Config.QueueSoftCap) + 4 * Math.Max(0, q - state.Config.QueueHardCap);
        if (q <= state.Config.QueueSoftCap) delta -= 3;
        state.Overload = Clamp(state.Overload + delta, 0, 100);
    }

    private static CaseReviewRules Rules(GameState state) => state.Config.Rules ?? CaseReviewRules.Default;

    private static void DrawMorningCards(GameState state)
    {
        state.MorningCards = Rules(state).CardDrawService.DrawMorningCards(state);
    }

    private static void RecordReviewCost(GameState state, ReviewActionType actionType, string subjectId, string sourceType)
    {
        var cost = Rules(state).ReviewCostPolicy.Assess(state, actionType, subjectId, sourceType);
        state.ReviewCosts.Add(cost);
        state.ReplacementPressure = Rules(state).ReplacementPressurePolicy.AfterManualReview(state, cost, state.ReplacementPressure);
    }

    private static int GrantMeritTokens(GameState state, int amount)
    {
        var granted = Math.Max(0, amount);
        if (granted == 0)
        {
            return 0;
        }

        state.MeritTokens = Math.Max(0, state.MeritTokens + granted);
        return granted;
    }

    private static bool TryParseApprovalKind(string value, out ApprovalRequestKind kind)
    {
        if (value.Equals("REGENERATION", StringComparison.OrdinalIgnoreCase)
            || value.Equals("REGEN", StringComparison.OrdinalIgnoreCase))
        {
            kind = ApprovalRequestKind.Regeneration;
            return true;
        }

        if (value.Equals("REPORT", StringComparison.OrdinalIgnoreCase)
            || value.Equals("REPORTCORRECTION", StringComparison.OrdinalIgnoreCase))
        {
            kind = ApprovalRequestKind.ReportCorrection;
            return true;
        }

        if (value.Equals("AUDIT", StringComparison.OrdinalIgnoreCase)
            || value.Equals("AUDITDEFENSE", StringComparison.OrdinalIgnoreCase))
        {
            kind = ApprovalRequestKind.AuditDefense;
            return true;
        }

        if (value.Equals("EXPENSE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("SPECIALEXPENSE", StringComparison.OrdinalIgnoreCase))
        {
            kind = ApprovalRequestKind.SpecialExpense;
            return true;
        }

        kind = ApprovalRequestKind.ReportCorrection;
        return false;
    }

    private static string NextApprovalId(GameState state)
    {
        var candidate = $"AR-{state.Day:00}-{state.ApprovalRequests.Count + 1:D2}";
        var suffix = 2;
        while (state.ApprovalRequests.Any(request => request.Id.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"AR-{state.Day:00}-{state.ApprovalRequests.Count + 1:D2}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static void ApplyInitialData(GameState state, CaseReviewSeedData data)
    {
        state.Staff = BuildInitialStaff(data);
        state.WorkDefinitions = (data.WorkDefinitions ?? new List<WorkDefinition>())
            .Where(definition => definition != null)
            .ToList();
        state.Queue = BuildInitialQueue(state, data);
        state.TruthFrames = (data.TruthFrames ?? new List<TruthFrame>()).Select(CloneTruthFrame).ToList();
        state.Logs = (data.Logs ?? new List<VisibleLog>()).Select(CloneVisibleLog).ToList();
    }

    private static List<EventCase> BuildInitialQueue(GameState state, CaseReviewSeedData data)
    {
        var queue = (data.Queue ?? new List<EventCase>()).Select(CloneEventCase).ToList();
        if (queue.Count > 0 || data.WorkDefinitions == null || data.WorkDefinitions.Count == 0)
        {
            return queue;
        }

        var request = new WorkGenerationRequest
        {
            Definitions = data.WorkDefinitions,
            Count = 3,
            Difficulty = 0
        };

        return Rules(state).WorkGenerationService.Generate(state, request);
    }

    private static List<Personnel> BuildInitialStaff(CaseReviewSeedData data)
    {
        var staff = (data.Staff ?? new List<Personnel>()).Select(ClonePersonnel).ToList();
        if (data.CharacterData == null || data.CharacterData.Count == 0)
        {
            return staff;
        }

        staff.AddRange(data.CharacterData.Where(character => character != null).Select(character => ClonePersonnel(character.CreateRuntimeModel())));
        return staff
            .GroupBy(person => person.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
    }

    private static Personnel ClonePersonnel(Personnel source)
    {
        return new Personnel
        {
            Id = source.Id,
            Name = source.Name,
            Background = source.Background,
            Interests = new List<string>(source.Interests ?? new List<string>()),
            Personality = source.Personality,
            WorkStyle = source.WorkStyle,
            PhysicalEnergy = source.PhysicalEnergy,
            MentalStress = source.MentalStress,
            LoadAssigned = source.LoadAssigned,
            Fatigue = source.Fatigue,
            Stagnation = source.Stagnation,
            TrustToManager = source.TrustToManager,
            RetentionRisk = source.RetentionRisk,
            HasLeft = source.HasLeft,
            DaysSinceJoined = source.DaysSinceJoined,
            OptLow = source.OptLow,
            OptHigh = source.OptHigh,
            MaxLoad = source.MaxLoad,
            ConnectionLimit = source.ConnectionLimit,
            CloneLineageId = source.CloneLineageId,
            CloneVersion = source.CloneVersion,
            RegenerationCount = source.RegenerationCount,
            RegeneratedFromId = source.RegeneratedFromId,
            InformationScope = source.InformationScope,
            Aptitudes = new Dictionary<string, int>(source.Aptitudes ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase),
            Deck = (source.Deck ?? new List<ActionCard>()).Select(card => CloneActionCard(card, source.Id)).ToList(),
            Perks = (source.Perks ?? new List<PersonnelPerk>()).Select(ClonePerk).ToList(),
            Relationships = (source.Relationships ?? new List<PersonnelRelationship>()).Select(CloneRelationship).ToList(),
            Memories = (source.Memories ?? new List<PersonnelMemory>()).Select(CloneMemory).ToList(),
            TraitSamples = (source.TraitSamples ?? new List<PersonnelTraitSample>()).Select(CloneTraitSample).ToList()
        };
    }

    private static ActionCard CloneActionCard(ActionCard source, string ownerId = "")
    {
        return new ActionCard
        {
            Id = source.Id,
            OwnerPersonnelId = string.IsNullOrWhiteSpace(source.OwnerPersonnelId) ? ownerId : source.OwnerPersonnelId,
            TargetEventId = source.TargetEventId,
            Title = source.Title,
            Summary = source.Summary,
            Tags = new List<string>(source.Tags ?? new List<string>()),
            OutcomeModifier = source.OutcomeModifier,
            RiskModifier = source.RiskModifier,
            ReviewCostModifier = source.ReviewCostModifier,
            CriticalChancePercent = source.CriticalChancePercent,
            CriticalMultiplier = source.CriticalMultiplier,
            CriticalTriggered = source.CriticalTriggered,
            CriticalRoll = source.CriticalRoll
        };
    }

    private static PersonnelPerk ClonePerk(PersonnelPerk source)
    {
        return new PersonnelPerk
        {
            Id = source.Id,
            Name = source.Name,
            TriggerTags = new List<string>(source.TriggerTags ?? new List<string>()),
            AptitudeModifiers = new Dictionary<string, int>(source.AptitudeModifiers ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase),
            OutcomeModifier = source.OutcomeModifier,
            PhysicalCostModifier = source.PhysicalCostModifier,
            MentalCostModifier = source.MentalCostModifier,
            ClonePersistent = source.ClonePersistent,
            Note = source.Note
        };
    }

    private static PersonnelRelationship CloneRelationship(PersonnelRelationship source)
    {
        return new PersonnelRelationship
        {
            TargetId = source.TargetId,
            Trust = source.Trust,
            Affinity = source.Affinity,
            Debt = source.Debt,
            Resentment = source.Resentment,
            Reliability = source.Reliability,
            Note = source.Note
        };
    }

    private static PersonnelMemory CloneMemory(PersonnelMemory source)
    {
        return new PersonnelMemory
        {
            Id = source.Id,
            TargetId = source.TargetId,
            Type = source.Type,
            Valence = source.Valence,
            Intensity = source.Intensity,
            Decay = source.Decay,
            Tags = new List<string>(source.Tags ?? new List<string>()),
            SourceEventId = source.SourceEventId,
            DayCreated = source.DayCreated,
            Note = source.Note
        };
    }

    private static PersonnelTraitSample CloneTraitSample(PersonnelTraitSample source)
    {
        return new PersonnelTraitSample
        {
            Id = source.Id,
            SourceEventId = source.SourceEventId,
            Tags = new List<string>(source.Tags ?? new List<string>()),
            Strength = source.Strength,
            ClonePersistent = source.ClonePersistent,
            Note = source.Note
        };
    }

    private static EventCase CloneEventCase(EventCase source)
    {
        return new EventCase
        {
            Id = source.Id,
            DefinitionId = source.DefinitionId,
            ProjectId = source.ProjectId,
            Tier = source.Tier,
            ParentEventId = source.ParentEventId,
            RootEventId = source.RootEventId,
            TriggerReason = source.TriggerReason,
            OutcomeEventsProcessed = source.OutcomeEventsProcessed,
            Kind = source.Kind,
            Title = source.Title,
            Subsystem = source.Subsystem,
            Importance = source.Importance,
            Volume = source.Volume,
            Urgency = source.Urgency,
            Severity = source.Severity,
            TtlSec = source.TtlSec,
            Status = source.Status,
            LatentRisk = source.LatentRisk,
            MismatchScore = source.MismatchScore,
            SummaryRead = source.SummaryRead,
            ApprovedFromSummaryOnly = source.ApprovedFromSummaryOnly,
            AssignedPersonnel = new List<string>(source.AssignedPersonnel ?? new List<string>()),
            HoldCount = source.HoldCount,
            Redirected = source.Redirected,
            OutcomeScore = source.OutcomeScore,
            ResultSummary = source.ResultSummary,
            AutoResolved = source.AutoResolved,
            ReportReviewed = source.ReportReviewed,
            PhysicalCost = source.PhysicalCost,
            MentalCost = source.MentalCost,
            BaseSuccessChance = source.BaseSuccessChance,
            RequiredAptitudes = new Dictionary<string, int>(source.RequiredAptitudes ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase),
            RecommendedPersonnelCount = source.RecommendedPersonnelCount,
            MinPersonnelCount = source.MinPersonnelCount,
            MaxPersonnelCount = source.MaxPersonnelCount,
            ConcurrentLimit = source.ConcurrentLimit,
            ConcurrentSlotCost = source.ConcurrentSlotCost,
            SplitPenalty = source.SplitPenalty,
            SoloPenalty = source.SoloPenalty,
            Tags = new List<string>(source.Tags ?? new List<string>()),
            PerkTags = new List<string>(source.PerkTags ?? new List<string>()),
            CardHooks = new List<string>(source.CardHooks ?? new List<string>()),
            BossReactionTags = new List<string>(source.BossReactionTags ?? new List<string>()),
            MemoryHooks = new List<string>(source.MemoryHooks ?? new List<string>()),
            PerkInteractionInfo = source.PerkInteractionInfo,
            VisibleSummary = source.VisibleSummary,
            HiddenFacts = new List<string>(source.HiddenFacts ?? new List<string>())
        };
    }

    private static TruthFrame CloneTruthFrame(TruthFrame source)
    {
        return new TruthFrame
        {
            Id = source.Id,
            EventId = source.EventId,
            Tick = source.Tick,
            ActorId = source.ActorId,
            ActionCode = source.ActionCode,
            FactBlob = source.FactBlob
        };
    }

    private static VisibleLog CloneVisibleLog(VisibleLog source)
    {
        return new VisibleLog
        {
            Id = source.Id,
            EventId = source.EventId,
            SourceType = source.SourceType,
            VisibleAtSec = source.VisibleAtSec,
            Text = source.Text,
            Omitted = source.Omitted,
            Distorted = source.Distorted,
            Delayed = source.Delayed,
            Announced = source.Announced,
            Read = source.Read
        };
    }

    private static void SeedStaff(GameState state)
    {
        state.Staff.Add(new Personnel
        {
            Id = "A-17",
            Name = "미카",
            Background = "현장 정비 출신",
            Interests = new List<string> { "기계", "응급복구" },
            Personality = "침착",
            WorkStyle = "현장우선",
            PhysicalEnergy = 82,
            MentalStress = 18,
            Fatigue = 18,
            Stagnation = 12,
            TrustToManager = 58,
            OptLow = 4,
            OptHigh = 6,
            MaxLoad = 8,
            ConnectionLimit = 2,
            CloneLineageId = "LINE-FIELD",
            Aptitudes = Aptitudes(observation: 5, dexterity: 8, boldness: 7, intuition: 4, logic: 5),
            Perks = new List<PersonnelPerk>
            {
                Perk("field_bypass", "현장 우회 경험", new[] { "o2", "emergency", "repair" }, outcome: 8, physicalCost: -2, note: "긴급 설비 복구에서 빠르지만 절차 공백을 남기기 쉽다.")
            },
            TraitSamples = new List<PersonnelTraitSample>
            {
                Trait("trait.field.shortcut", new[] { "repair", "shortcut", "lineage-residue" }, 54, true, "현장 복구 때 절차보다 우회로를 먼저 본다.")
            }
        });
        state.Staff.Add(new Personnel
        {
            Id = "B-04",
            Name = "라울",
            Background = "감사/절차 담당",
            Interests = new List<string> { "규정", "안전" },
            Personality = "의심많음",
            WorkStyle = "절차우선",
            PhysicalEnergy = 74,
            MentalStress = 22,
            Fatigue = 22,
            Stagnation = 10,
            TrustToManager = 63,
            OptLow = 3,
            OptHigh = 5,
            MaxLoad = 7,
            ConnectionLimit = 3,
            CloneLineageId = "LINE-PROCEDURE",
            Aptitudes = Aptitudes(observation: 6, dexterity: 4, boldness: 3, intuition: 5, logic: 8),
            Perks = new List<PersonnelPerk>
            {
                Perk("procedure_anchor", "절차 앵커", new[] { "audit", "procedure", "o2" }, outcome: 10, mentalCost: 2, note: "불일치와 승인 공백을 줄인다.")
            },
            TraitSamples = new List<PersonnelTraitSample>
            {
                Trait("trait.procedure.watch", new[] { "audit", "procedure", "skeptic" }, 48, false, "타인의 현장 판단을 문서 공백으로 먼저 해석한다.")
            }
        });
        state.Staff.Add(new Personnel
        {
            Id = "C-22",
            Name = "니아",
            Background = "환경 관찰 연구원",
            Interests = new List<string> { "센서", "패턴" },
            Personality = "호기심강함",
            WorkStyle = "관찰우선",
            PhysicalEnergy = 86,
            MentalStress = 14,
            Fatigue = 14,
            Stagnation = 18,
            TrustToManager = 56,
            OptLow = 4,
            OptHigh = 6,
            MaxLoad = 7,
            ConnectionLimit = 4,
            CloneLineageId = "LINE-SIGNAL",
            Aptitudes = Aptitudes(observation: 9, dexterity: 4, boldness: 4, intuition: 7, logic: 6),
            Perks = new List<PersonnelPerk>
            {
                Perk("signal_reader", "신호 독해", new[] { "sensor", "o2", "mismatch" }, outcome: 9, mentalCost: -1, note: "센서 흔적과 요약 문장의 온도차를 빨리 잡는다.")
            },
            TraitSamples = new List<PersonnelTraitSample>
            {
                Trait("trait.signal.fixation", new[] { "sensor", "pattern", "lineage-residue" }, 62, true, "작은 센서 흔적에 오래 머무는 계보 습관.")
            }
        });
        state.Staff.Add(new Personnel
        {
            Id = "D-11",
            Name = "세린",
            Background = "거주구역 조정 담당",
            Interests = new List<string> { "사람", "기록" },
            Personality = "사교적",
            WorkStyle = "중재우선",
            PhysicalEnergy = 78,
            MentalStress = 16,
            Fatigue = 16,
            Stagnation = 14,
            TrustToManager = 60,
            OptLow = 3,
            OptHigh = 5,
            MaxLoad = 6,
            ConnectionLimit = 5,
            CloneLineageId = "LINE-MEDIATION",
            Aptitudes = Aptitudes(observation: 6, dexterity: 3, boldness: 5, intuition: 7, logic: 6),
            Perks = new List<PersonnelPerk>
            {
                Perk("complaint_weaver", "민원 엮기", new[] { "complaint", "hab", "relation" }, outcome: 8, mentalCost: -2, note: "여러 사람의 말을 하나의 처리 가능한 묶음으로 만든다.")
            },
            TraitSamples = new List<PersonnelTraitSample>
            {
                Trait("trait.mediation.echo", new[] { "relation", "complaint", "social" }, 45, false, "충돌을 줄이려다 책임 소재까지 흐리게 만든다.")
            }
        });

        Link(state, "A-17", "B-04", trust: 38, affinity: 32, "절차 지적을 잔소리로 받아들임");
        Link(state, "B-04", "A-17", trust: 54, affinity: 36, "현장 판단은 인정하지만 승인 공백을 경계");
        Link(state, "B-04", "C-22", trust: 66, affinity: 58, "센서 해석을 신뢰");
        Link(state, "C-22", "B-04", trust: 61, affinity: 55, "논리적 검증을 편하게 느낌");
        Link(state, "C-22", "D-11", trust: 47, affinity: 68, "관찰 메모를 잘 들어줌");
        Link(state, "D-11", "C-22", trust: 52, affinity: 72, "조용한 관찰력을 좋아함");
    }

    private static Dictionary<string, int> Aptitudes(int observation, int dexterity, int boldness, int intuition, int logic)
    {
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["observation"] = observation,
            ["dexterity"] = dexterity,
            ["boldness"] = boldness,
            ["intuition"] = intuition,
            ["logic"] = logic
        };
    }

    private static PersonnelPerk Perk(string id, string name, string[] tags, int outcome = 0, int physicalCost = 0, int mentalCost = 0, string note = "")
    {
        return new PersonnelPerk
        {
            Id = id,
            Name = name,
            TriggerTags = tags.ToList(),
            OutcomeModifier = outcome,
            PhysicalCostModifier = physicalCost,
            MentalCostModifier = mentalCost,
            Note = note
        };
    }

    private static PersonnelTraitSample Trait(string id, string[] tags, int strength, bool clonePersistent, string note)
    {
        return new PersonnelTraitSample
        {
            Id = id,
            Tags = tags.ToList(),
            Strength = Clamp(strength, 0, 100),
            ClonePersistent = clonePersistent,
            Note = note
        };
    }

    private static void Link(GameState state, string sourceId, string targetId, int trust, int affinity, string note)
    {
        var source = state.Staff.First(s => s.Id.Equals(sourceId, StringComparison.OrdinalIgnoreCase));
        if (source.Relationships.Count >= source.ConnectionLimit)
        {
            return;
        }

        source.Relationships.Add(new PersonnelRelationship
        {
            TargetId = targetId,
            Trust = trust,
            Affinity = affinity,
            Reliability = Clamp((trust + affinity) / 2, -100, 100),
            Note = note
        });
    }

    private static void SeedDayOneCases(GameState state)
    {
        var e108 = new EventCase
        {
            Id = "E-108",
            Kind = "incident",
            Title = "예비 라인 센서 불일치",
            Subsystem = "O2",
            Urgency = 72,
            Severity = 72,
            TtlSec = 120,
            LatentRisk = 28,
            MismatchScore = 3,
            AssignedPersonnel = new List<string> { "A-17" },
            PhysicalCost = 18,
            MentalCost = 14,
            BaseSuccessChance = 42,
            RequiredAptitudes = Aptitudes(observation: 7, dexterity: 7, boldness: 6, intuition: 5, logic: 6),
            PerkTags = new List<string> { "o2", "sensor", "mismatch", "emergency", "repair", "procedure" },
            PerkInteractionInfo = "현장 우회 perk는 복구 속도를 올리지만, 절차/관찰 보강이 없으면 불일치가 남는다."
        };
        var r211 = new EventCase
        {
            Id = "R-211",
            Kind = "complaint",
            Title = "하층 거주구역 민원",
            Subsystem = "HAB",
            Urgency = 38,
            Severity = 34,
            TtlSec = 180,
            LatentRisk = 12,
            MismatchScore = 1,
            PhysicalCost = 6,
            MentalCost = 12,
            BaseSuccessChance = 58,
            RequiredAptitudes = Aptitudes(observation: 5, dexterity: 2, boldness: 3, intuition: 7, logic: 5),
            PerkTags = new List<string> { "complaint", "hab", "relation", "records" },
            PerkInteractionInfo = "중재/기록 계열 perk가 민원 묶음의 누락을 줄인다."
        };
        var r311 = new EventCase
        {
            Id = "R-311",
            Kind = "routine",
            Title = "식량 합성기 보고 backlog",
            Subsystem = "FOOD",
            Urgency = 24,
            Severity = 26,
            TtlSec = 240,
            LatentRisk = 10,
            MismatchScore = 0,
            PhysicalCost = 4,
            MentalCost = 9,
            BaseSuccessChance = 68,
            RequiredAptitudes = Aptitudes(observation: 4, dexterity: 3, boldness: 2, intuition: 3, logic: 7),
            PerkTags = new List<string> { "routine", "records", "procedure" },
            PerkInteractionInfo = "논리/절차 성향이 높으면 낮은 비용으로 처리된다."
        };

        state.Queue.AddRange(new[] { e108, r211, r311 });
        AddTruth(state, "E-108", "A-17", "BYPASS_APPLIED", "수동 우회 적용. 승인 메모 없음. 센서 신호 손실 3.2초");
        AddTruth(state, "E-108", "B-04", "ORDER_OBJECTION", "순서 확인 없이 우회 적용했다고 관찰");
        AddTruth(state, "R-211", "D-11", "GROUPED_COMPLAINT", "민원 4건이 중복으로 묶임");
        AddLog(state, e108, "summary", 0, "[SUMMARY][E-108] 산소 라인 정상. 지연 없음. 후속 확인 필요 없음.", omitted: true, distorted: true);
        AddLog(state, e108, "equip", 100, "[EQUIP][E-108][12:14:07] SIGNAL_LOSS=3.2s | O2_PRESS=? | FALLBACK=CACHE");
        AddLog(state, e108, "rel", 118, "[REL][E-108][B-04->A-17] 순서 확인 없이 우회 적용. 중간 보고 없음.", distorted: true);
        AddLog(state, r211, "summary", 0, "[SUMMARY][R-211] 하층 거주구역 민원 4건 묶음 종결 후보. 중복 내용으로 분류.", omitted: true);
        AddLog(state, r311, "summary", 0, "[SUMMARY][R-311] 식량 합성기 보고서 3건 대기. 서류 정리 권장.");
    }

    private static void BuildMorningPlan(GameState state)
    {
        var entries = new List<WorkPlanEntry>();
        foreach (var item in state.Queue.Where(e => e.Status != CaseStatus.Closed).OrderByDescending(e => e.Urgency + e.Severity))
        {
            var people = item.Id switch
            {
                "E-108" => new List<string> { "A-17" },
                "R-211" => new List<string> { "D-11" },
                "R-311" => new List<string> { "B-04" },
                _ => AutoPlanPeople(state, item)
            };
            people = people.Where(p => state.Staff.Any(s => !s.HasLeft && s.Id.Equals(p, StringComparison.OrdinalIgnoreCase))).ToList();
            entries.Add(new WorkPlanEntry
            {
                EventId = item.Id,
                PlannedPersonnel = people,
                Reason = PlanReason(item, people)
            });
        }

        state.MorningPlan = new WorkPlan { Day = state.Day, Confirmed = false, Entries = entries };
    }

    private static void SeedNextDayCases(GameState state, List<string> lines)
    {
        var added = new List<EventCase>();
        var previous = state.Queue
            .Where(e => e.AutoResolved && !e.OutcomeEventsProcessed)
            .OrderByDescending(e => e.LatentRisk + Math.Max(0, 60 - e.OutcomeScore))
            .Take(2)
            .ToList();

        foreach (var item in previous)
        {
            var linked = WorkOutcomeEventSystem.Generate(
                item,
                state.WorkDefinitions,
                WorkGenerationContext.FromState(state, difficulty: 0, seedOffset: StableSeedOffset(item.Id)));
            item.OutcomeEventsProcessed = true;

            if (linked.Count > 0)
            {
                foreach (var linkedItem in linked)
                {
                    linkedItem.Id = UniqueEventId(state, added, linkedItem.Id);
                    added.Add(linkedItem);
                }
            }
            else if (item.LatentRisk >= 30 || item.OutcomeScore < 60 || item.MismatchScore >= 2)
            {
                added.Add(CreateFollowUpCase(state, item));
            }
        }

        var overloaded = state.Staff
            .Where(s => !s.HasLeft)
            .OrderByDescending(s => Math.Max(0, s.LoadAssigned - s.OptHigh) + s.Fatigue)
            .FirstOrDefault();
        if (overloaded is not null)
        {
            added.Add(new EventCase
            {
                Id = $"R-{state.Day:D2}01",
                Kind = "routine",
                Title = $"{overloaded.Name} 작업기록 재정리 및 부하 재배분",
                Subsystem = "STAFF",
                Urgency = 34,
                Severity = 28,
                TtlSec = 0,
                LatentRisk = 12,
                MismatchScore = 1
            });
        }

        if (added.Count == 0)
        {
            added.Add(new EventCase
            {
                Id = $"R-{state.Day:D2}00",
                Kind = "routine",
                Title = "전일 종결건 표본 감사 준비",
                Subsystem = "RECORDS",
                Urgency = 30,
                Severity = 24,
                TtlSec = 0,
                LatentRisk = 10,
                MismatchScore = 1
            });
        }

        state.Queue.AddRange(added);
        foreach (var item in added)
        {
            ScenarioEffectApplier.ApplyActiveModifiersToWork(state, item);
            AddTruth(state, item.Id, "SYS", "NEXT_DAY_SEED", $"전일 결과 기반 후속 작업 생성: {item.Title}");
            AddLog(state, item, "summary", state.TotalElapsedSec, $"[SUMMARY][{item.Id}] {item.Title}. 전일 보고서 후속 조치 후보.", omitted: item.MismatchScore >= 2);
        }

        lines.Add($"익일 작업 후보 {added.Count}건이 생성되었습니다: {string.Join(", ", added.Select(e => e.Id))}");
    }

    private static int StableSeedOffset(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value ?? "")
            {
                hash = hash * 31 + char.ToUpperInvariant(character);
            }

            return hash;
        }
    }

    private static string UniqueEventId(GameState state, IReadOnlyCollection<EventCase> pending, string requestedId)
    {
        var baseId = string.IsNullOrWhiteSpace(requestedId) ? $"W-{state.Day:D2}01" : requestedId;
        var candidate = baseId;
        var suffix = 2;
        while (state.Queue.Any(item => item.Id.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            || pending.Any(item => item.Id.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}-{suffix++}";
        }

        return candidate;
    }

    private static void ResolveAttrition(GameState state, List<string> lines)
    {
        var leavers = state.Staff
            .Where(s => !s.HasLeft && s.RetentionRisk >= 78)
            .OrderByDescending(s => s.RetentionRisk)
            .Take(1)
            .ToList();

        foreach (var staff in leavers)
        {
            staff.HasLeft = true;
            state.TalentShortage = Clamp(state.TalentShortage + 25, 0, 100);
            state.GlobalLatentRisk = Clamp(state.GlobalLatentRisk + 20, 0, 200);
            lines.Add($"인재이탈: {staff.Id} {staff.Name} 퇴사. 채용난 +25, 잠복 리스크 +20.");
            state.Queue.Add(new EventCase
            {
                Id = $"H-{state.Day:D2}{Suffix(staff.Id, 2)}",
                Kind = "hiring",
                Title = $"{staff.Name} 이탈 후 대체 인력 확보",
                Subsystem = "HR",
                Urgency = Clamp(50 + state.TalentShortage / 2, 35, 95),
                Severity = Clamp(45 + state.TalentShortage / 3, 35, 90),
                TtlSec = 0,
                LatentRisk = Clamp(20 + state.TalentShortage / 2, 20, 85),
                MismatchScore = 2
            });
        }
    }

    private static EventCase CreateFollowUpCase(GameState state, EventCase source)
    {
        var isAudit = source.MismatchScore >= 2 || source.LatentRisk >= 35;
        return new EventCase
        {
            Id = $"{(isAudit ? "A" : "F")}-{state.Day:D2}{Suffix(source.Id, 3)}",
            Kind = isAudit ? "audit" : "followup",
            Title = $"{source.Title} 후속 {(isAudit ? "감사" : "재점검")}",
            Subsystem = source.Subsystem,
            Urgency = Clamp(35 + source.LatentRisk / 2, 20, 85),
            Severity = Clamp(source.Severity - 10 + Math.Max(0, 60 - source.OutcomeScore) / 2, 20, 90),
            TtlSec = 0,
            LatentRisk = Clamp(source.LatentRisk / 2 + 10, 5, 70),
            MismatchScore = Math.Max(1, source.MismatchScore - 1)
        };
    }

    private static void SimulateConfirmedPlan(GameState state, List<string> lines)
    {
        foreach (var item in state.Queue.Where(e => e.Status != CaseStatus.Closed).OrderByDescending(e => e.Urgency + e.Severity))
        {
            var team = BuildTeam(state, item);
            var score = CalculateOutcomeScore(state, item, team);
            item.OutcomeScore = score;
            item.AutoResolved = true;
            item.Status = CaseStatus.Closed;

            var previousRisk = item.LatentRisk;
            item.LatentRisk = Clamp(item.LatentRisk + Math.Max(0, 65 - score) + (item.MismatchScore * 5), 0, 100);
            item.LatentRisk = Clamp(item.LatentRisk + ActiveCardsFor(state, item).Sum(card => card.RiskModifier), 0, 100);
            if (score >= 75)
            {
                item.LatentRisk = Math.Max(0, item.LatentRisk - 18);
            }

            item.ResultSummary = BuildResultSummary(item, score, previousRisk);
            AddTruth(state, item.Id, item.AssignedPersonnel.FirstOrDefault() ?? "SYS", "AUTO_RESULT", item.ResultSummary);
            AddLogFromTruth(state, item, "work", state.TotalElapsedSec);
            ApplyTaskCost(state, item, team, score);
            ApplyRelationshipMemoryAfterWork(state, item, team, score);
            var awarded = GrantMeritTokens(state, Rules(state).MeritTokenPolicy.AwardForResolvedWork(state, item));
            if (awarded > 0)
            {
                lines.Add($"MERIT +{awarded}. {item.Id} produced approval tokens.");
            }

            lines.Add($"{item.Id}: {item.ResultSummary}");
        }

        UpdateRetentionRiskAfterOperations(state);
        state.GlobalLatentRisk = Clamp(state.Queue.Sum(e => e.LatentRisk), 0, 200);
    }

    private static void UpdateRetentionRiskAfterOperations(GameState state)
    {
        foreach (var staff in state.Staff.Where(s => !s.HasLeft))
        {
            staff.RetentionRisk = Clamp(
                staff.RetentionRisk
                + Math.Max(0, staff.LoadAssigned - staff.OptHigh) * 10
                + Math.Max(0, staff.Fatigue - 50) / 3
                + Math.Max(0, 50 - staff.TrustToManager) / 3
                + Math.Max(0, staff.Stagnation - 55) / 4,
                0,
                100);
        }
    }

    private static List<Personnel> BuildTeam(GameState state, EventCase item)
    {
        return item.AssignedPersonnel
            .Select(id => state.Staff.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .OfType<Personnel>()
            .Where(p => !p.HasLeft)
            .ToList();
    }

    private static int CalculateOutcomeScore(GameState state, EventCase item, List<Personnel> team)
    {
        if (item.AssignedPersonnel.Count == 0) return 30;

        var skillFit = RequiredAptitudeScore(item, team);
        var coverageBonus = team.Count >= 2 ? 10 : 0;
        var singleHighSeverityPenalty = item.Severity >= 70 && team.Count == 1 ? 18 : 0;
        var loadPenalty = team.Sum(p => Math.Max(0, p.LoadAssigned - p.OptHigh) * 3);
        var fatiguePenalty = team.Sum(p => p.Fatigue + Math.Max(0, 100 - p.PhysicalEnergy) + p.MentalStress) / Math.Max(1, team.Count * 10);
        var perkBonus = team.Sum(p => MatchingPerks(p, item).Sum(perk => perk.OutcomeModifier));
        var cardBonus = ActiveCardsFor(state, item).Sum(card => card.OutcomeModifier);
        var relationBonus = TeamRelationBonus(team);
        var baseScore = item.BaseSuccessChance;
        return Clamp(baseScore + skillFit + coverageBonus + perkBonus + cardBonus + relationBonus - singleHighSeverityPenalty - loadPenalty - fatiguePenalty, 0, 100);
    }

    private static List<ActionCard> ActiveCardsFor(GameState state, EventCase item)
    {
        return state.MorningCards
            .Where(card => item.AssignedPersonnel.Contains(card.OwnerPersonnelId, StringComparer.OrdinalIgnoreCase))
            .Where(card => string.IsNullOrWhiteSpace(card.TargetEventId)
                || card.TargetEventId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static int Best(List<Personnel> team, string skill)
    {
        return team.Count == 0 ? 0 : team.Max(p => p.Aptitudes.TryGetValue(skill, out var value) ? value : 0);
    }

    private static int RequiredAptitudeScore(EventCase item, List<Personnel> team)
    {
        if (item.RequiredAptitudes.Count == 0)
        {
            return item.Subsystem switch
            {
                "O2" => Best(team, "dexterity") * 5 + Best(team, "logic") * 3 + Best(team, "observation") * 4,
                "HAB" => Best(team, "intuition") * 5 + Best(team, "logic") * 4 + Best(team, "observation") * 2,
                "FOOD" => Best(team, "logic") * 5 + Best(team, "observation") * 3 + Best(team, "dexterity") * 2,
                _ => Best(team, "logic") * 4 + Best(team, "observation") * 3
            };
        }

        var total = 0;
        foreach (var requirement in item.RequiredAptitudes)
        {
            var best = Best(team, requirement.Key);
            total += Math.Min(best, requirement.Value) * 5;
            total -= Math.Max(0, requirement.Value - best) * 4;
        }

        return total / Math.Max(1, item.RequiredAptitudes.Count);
    }

    private static int TeamRelationBonus(List<Personnel> team)
    {
        if (team.Count < 2)
        {
            return 0;
        }

        var total = 0;
        var count = 0;
        foreach (var source in team)
        {
            foreach (var target in team.Where(t => t.Id != source.Id))
            {
                var relationship = source.Relationships.FirstOrDefault(r => r.TargetId.Equals(target.Id, StringComparison.OrdinalIgnoreCase));
                if (relationship is null)
                {
                    total -= 2;
                }
                else
                {
                    total += (relationship.Trust - 50) / 10 + (relationship.Affinity - 50) / 15;
                }

                count++;
            }
        }

        return count == 0 ? 0 : Clamp(total / count, -8, 8);
    }

    private static List<PersonnelPerk> MatchingPerks(Personnel person, EventCase item)
    {
        return person.Perks
            .Where(perk => perk.TriggerTags.Any(tag => item.PerkTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    private static void ApplyTaskCost(GameState state, EventCase item, List<Personnel> team, int score)
    {
        if (team.Count == 0)
        {
            return;
        }

        foreach (var person in team)
        {
            var matchingPerks = MatchingPerks(person, item);
            var physicalCost = Math.Max(0, item.PhysicalCost + matchingPerks.Sum(p => p.PhysicalCostModifier));
            var mentalCost = Math.Max(0, item.MentalCost + matchingPerks.Sum(p => p.MentalCostModifier));
            if (score < 55)
            {
                mentalCost += 4;
            }

            person.PhysicalEnergy = Clamp(person.PhysicalEnergy - physicalCost, 0, 100);
            person.MentalStress = Clamp(person.MentalStress + mentalCost, 0, 100);
            person.Fatigue = Clamp(person.Fatigue + physicalCost / 2 + mentalCost / 4, 0, 100);
            if (score >= 75 && matchingPerks.Count > 0)
            {
                person.Stagnation = Clamp(person.Stagnation - 3, 0, 100);
            }
        }
    }

    private static void ApplyRelationshipMemoryAfterWork(GameState state, EventCase item, List<Personnel> team, int score)
    {
        if (team.Count < 2)
        {
            return;
        }

        var success = score >= 70;
        var failure = score < 55;
        var highRisk = item.Severity >= 60 || item.LatentRisk >= 45 || item.Urgency >= 70;
        foreach (var source in team)
        {
            foreach (var target in team.Where(person => !person.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase)))
            {
                var relation = GetOrCreateRuntimeRelationship(source, target.Id);
                if (success)
                {
                    relation.Trust = Clamp(relation.Trust + (highRisk ? 6 : 3), -100, 100);
                    relation.Affinity = Clamp(relation.Affinity + 2, -100, 100);
                    relation.Reliability = Clamp(relation.Reliability + (highRisk ? 7 : 4), -100, 100);
                    relation.Note = $"{item.Id} 공동 처리 성공으로 협업 안정감 증가";
                }
                else if (failure)
                {
                    relation.Trust = Clamp(relation.Trust - (highRisk ? 8 : 5), -100, 100);
                    relation.Affinity = Clamp(relation.Affinity - 3, -100, 100);
                    relation.Resentment = Clamp(relation.Resentment + (highRisk ? 8 : 5), -100, 100);
                    relation.Note = $"{item.Id} 공동 처리 실패 원인을 서로 다르게 기억";
                }
                else
                {
                    relation.Trust = Clamp(relation.Trust + 1, -100, 100);
                    relation.Reliability = Clamp(relation.Reliability + 1, -100, 100);
                    relation.Debt = Clamp(relation.Debt + (highRisk ? 2 : 0), -100, 100);
                    relation.Note = $"{item.Id} 불완전 처리 후 협업 흔적 누적";
                }

                var shouldRecordMemory = highRisk || success || failure || source.Memories.Count(memory => memory.TargetId.Equals(target.Id, StringComparison.OrdinalIgnoreCase)) < 2;
                if (!shouldRecordMemory)
                {
                    continue;
                }

                source.Memories.Add(new PersonnelMemory
                {
                    Id = $"mem.work.{state.Day:00}.{item.Id}.{source.Id}.{target.Id}",
                    TargetId = target.Id,
                    Type = "Work",
                    Valence = success ? "Positive" : failure ? "Negative" : "Mixed",
                    Intensity = Clamp((highRisk ? 34 : 18) + Math.Abs(score - 60) / 2, 0, 100),
                    Decay = success ? 12 : 8,
                    Tags = item.MemoryHooks.Concat(item.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList(),
                    SourceEventId = item.Id,
                    DayCreated = state.Day,
                    Note = $"{target.Name}와 함께 처리한 {item.Title}: {item.ResultSummary}"
                });
            }
        }
    }

    private static PersonnelRelationship GetOrCreateRuntimeRelationship(Personnel source, string targetId)
    {
        source.Relationships ??= new List<PersonnelRelationship>();
        var relationship = source.Relationships.FirstOrDefault(item => item.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase));
        if (relationship is not null)
        {
            return relationship;
        }

        relationship = new PersonnelRelationship { TargetId = targetId };
        source.Relationships.Add(relationship);
        return relationship;
    }

    private static string BuildResultSummary(EventCase item, int score, int previousRisk)
    {
        var band = score >= 75 ? "양호" : score >= 55 ? "불완전" : "위험";
        var riskMove = item.LatentRisk > previousRisk ? "잠복 리스크 상승" : item.LatentRisk < previousRisk ? "잠복 리스크 감소" : "잠복 리스크 유지";
        var team = item.AssignedPersonnel.Count == 0 ? "미배정" : string.Join(",", item.AssignedPersonnel);
        return $"{band}. 배정 {team}. 결과 {score}. {riskMove}. {ResultDetail(item, score)}";
    }

    private static string ResultDetail(EventCase item, int score)
    {
        if (item.Id == "E-108" && item.AssignedPersonnel.Count == 1)
        {
            return "단독 처리로 복구는 빨랐지만 우회 승인 공백이 남았습니다.";
        }

        if (item.Id == "E-108")
        {
            return "센서 교정과 절차 확인을 병행해 우회 공백을 줄였습니다.";
        }

        if (score < 55)
        {
            return "후속 확인이 필요합니다.";
        }

        return "저녁 보고 대상으로 정리되었습니다.";
    }

    private static List<string> AutoPlanPeople(GameState state, EventCase item)
    {
        var staff = state.Staff.Where(s => !s.HasLeft).OrderBy(s => s.LoadAssigned).ThenBy(s => s.Fatigue).FirstOrDefault();
        return staff is null ? new List<string>() : new List<string> { staff.Id };
    }

    private static string PlanReason(EventCase item, List<string> people)
    {
        if (item.Severity >= 70 && people.Count == 1) return "고심각도지만 최고 적합자 단독 배정";
        if (item.Kind == "routine") return "기록/절차 backlog 처리";
        if (item.Kind == "complaint") return "문서 정리와 민원 분류 우선";
        return "긴급도 우선 기본 배정";
    }

    private static List<string> ParsePeople(IEnumerable<string> tokens)
    {
        return tokens
            .SelectMany(t => t.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => p.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ValidPeople(GameState state, List<string> people)
    {
        return people.Count is > 0 and <= 3
            && people.All(p => state.Staff.Any(s => !s.HasLeft && s.Id.Equals(p, StringComparison.OrdinalIgnoreCase)));
    }

    private static void ApplyAssignment(GameState state, EventCase item, List<string> people)
    {
        foreach (var oldPerson in item.AssignedPersonnel.Select(id => state.Staff.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).OfType<Personnel>())
        {
            oldPerson.LoadAssigned = Math.Max(0, oldPerson.LoadAssigned - (item.Severity >= 70 ? 5 : 3));
        }

        item.AssignedPersonnel = people.Select(p => p.ToUpperInvariant()).ToList();
        foreach (var person in item.AssignedPersonnel.Select(id => state.Staff.First(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))))
        {
            person.LoadAssigned += item.Severity >= 70 ? 5 : 3;
        }
    }

    private static void AddTruth(GameState state, string eventId, string actorId, string actionCode, string factBlob)
    {
        state.TruthFrames.Add(new TruthFrame
        {
            Id = $"T-{state.TruthFrames.Count + 1:D3}",
            EventId = eventId,
            Tick = state.TotalElapsedSec,
            ActorId = actorId,
            ActionCode = actionCode,
            FactBlob = factBlob
        });
    }

    private static void AddLogFromTruth(GameState state, EventCase item, string sourceType, int visibleAtSec)
    {
        var truth = state.TruthFrames.LastOrDefault(t => t.EventId == item.Id);
        var actor = truth?.ActorId ?? "SYS";
        var text = sourceType switch
        {
            "work" => $"[WORK][{item.Id}][{actor}] {WorkText(item, truth)}",
            "rel" => $"[REL][{item.Id}][{actor}->관리] 재배정 후 근거 확인 빈도가 늘었습니다.",
            _ => $"[{sourceType.ToUpperInvariant()}][{item.Id}] {truth?.FactBlob ?? "추가 기록 없음"}"
        };
        AddLog(state, item, sourceType, visibleAtSec, text, delayed: visibleAtSec > state.TotalElapsedSec + 12);
    }

    private static string WorkText(EventCase item, TruthFrame truth)
    {
        if (item.Redirected) return "우회 대신 센서 교정부터 수행. 압력 안정화 확인.";
        if (truth?.ActionCode == "HOLD") return "추가 확인 요청 수신. 이전 요약에 빠진 작업 공백 재검토 중.";
        return truth?.FactBlob ?? "작업 기록 확인.";
    }

    private static void AddLog(GameState state, EventCase item, string sourceType, int visibleAtSec, string text, bool omitted = false, bool distorted = false, bool delayed = false)
    {
        state.Logs.Add(new VisibleLog
        {
            Id = $"L-{state.Logs.Count + 1:D3}",
            EventId = item.Id,
            SourceType = sourceType,
            VisibleAtSec = visibleAtSec,
            Text = text,
            Omitted = omitted,
            Distorted = distorted,
            Delayed = delayed,
            Announced = visibleAtSec <= state.TotalElapsedSec
        });
    }

    private static string StatusLine(GameState state)
    {
        var q = state.Queue.Count(e => e.Status != CaseStatus.Closed);
        var time = state.Config.UseTimePressure ? $" | {FormatClock(state.TimeRemainingSec)} LEFT" : "";
        return $"DAY {state.Day:D2} | {state.Slot.ToString().ToUpperInvariant()}{time} | Q {q}/{state.Config.QueueSoftCap} | OVR {state.Overload} | TOKENS {state.MeritTokens} | REDIR {state.RedirectBudget} | AUDIT {state.AuditBudget} | KPI {state.KpiMode}";
    }

    private static string LastVisibleSource(GameState state, string eventId)
    {
        return state.Logs
            .Where(l => l.EventId == eventId && l.VisibleAtSec <= state.TotalElapsedSec)
            .OrderByDescending(l => l.VisibleAtSec)
            .Select(l => l.SourceType.ToUpperInvariant())
            .FirstOrDefault() ?? "NONE";
    }

    private static string LoadBand(Personnel staff)
    {
        if (staff.LoadAssigned > staff.OptHigh) return "HIGH";
        if (staff.LoadAssigned < staff.OptLow) return "LOW";
        return "OK";
    }

    private static string TrustBand(Personnel staff)
    {
        if (staff.TrustToManager < 40) return "대답 짧음";
        if (staff.Fatigue > 65) return "피로 누적";
        if (staff.Stagnation > 65) return "무료함";
        return "안정";
    }

    private static string RiskBand(int risk)
    {
        if (risk >= 70) return "높음";
        if (risk >= 35) return "중간";
        return "낮음";
    }

    private static string FormatClock(int seconds)
    {
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

    private static string Trim(string value, int max)
    {
        return value.Length <= max ? value : value.Substring(0, Math.Max(0, max - 1)) + ".";
    }

    private static string Suffix(string value, int length)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= length ? value : value.Substring(value.Length - length);
    }

    private static string Sha256(string text)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}

}
