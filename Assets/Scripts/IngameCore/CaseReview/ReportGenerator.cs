using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectW.IngameCore.CaseReview
{
public interface IReportGenerator
{
    DailyReportDocument Generate(GameState state);
    string GenerateEventReport(GameState state, EventCase item);
}

public sealed class TemplateReportGenerator : IReportGenerator
{
    public string GenerateEventReport(GameState state, EventCase item)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {item.Id} 개별 작업 검토 보고서");
        builder.AppendLine();
        builder.AppendLine($"## 대상");
        builder.AppendLine($"- 제목: {item.Title}");
        builder.AppendLine($"- 하위계통: {item.Subsystem}");
        builder.AppendLine($"- 배정: {FormatTeam(item.AssignedPersonnel)}");
        builder.AppendLine($"- 결과: {OutcomeBand(item.OutcomeScore)} / {item.OutcomeScore}");
        builder.AppendLine($"- 잠복 리스크: {item.LatentRisk}");
        builder.AppendLine();
        builder.AppendLine("## 검토 본문");
        builder.AppendLine(ManagementSentence(item));
        builder.AppendLine(ReviewMemo(item));
        builder.AppendLine();
        builder.AppendLine("## 문장상 의심 지점");
        builder.AppendLine($"- 누락 가능성: {OmissionHint(item)}");
        builder.AppendLine($"- 감사 사유: {AuditReason(item)}");
        builder.AppendLine();
        builder.AppendLine("## 관리자 확인");
        builder.AppendLine("검토를 마쳤다면 `REVIEW " + item.Id + "`를 입력하십시오.");
        return builder.ToString();
    }

    public DailyReportDocument Generate(GameState state)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# DAY {state.Day:D2} 운영 결과 종합 보고서");
        builder.AppendLine();
        builder.AppendLine("## 1. 총괄 의견");
        builder.AppendLine("금일 계획된 작업은 모두 종결 처리되었으나, 일부 항목은 종결 기준과 실제 잔여 리스크 사이에 간극이 있습니다.");
        builder.AppendLine("본 보고서는 관리 요약, 작업 로그, 설비 흔적, 관계 관찰 기록을 취합한 검토본이며, 원시 truth frame 전체를 직접 노출하지 않습니다.");
        builder.AppendLine();
        builder.AppendLine("## 2. 사건별 처리 내역");
        foreach (var item in state.Queue.OrderByDescending(e => e.Severity))
        {
            builder.AppendLine();
            builder.AppendLine($"### {item.Id} / {item.Title}");
            builder.AppendLine($"- 배정: {FormatTeam(item.AssignedPersonnel)}");
            builder.AppendLine($"- 판정: {OutcomeBand(item.OutcomeScore)} / 결과 점수 {item.OutcomeScore}");
            builder.AppendLine($"- 잠복 리스크: {item.LatentRisk}");
            builder.AppendLine($"- 관리 요약: {ManagementSentence(item)}");
            builder.AppendLine($"- 검토 메모: {ReviewMemo(item)}");
            builder.AppendLine($"- 누락 가능성: {OmissionHint(item)}");
        }

        builder.AppendLine();
        builder.AppendLine("## 3. 인력 상태");
        foreach (var staff in state.Staff)
        {
            var employment = staff.HasLeft ? "이탈" : "재직";
            builder.AppendLine($"- {staff.Id} {staff.Name}: {employment}, 부하 {staff.LoadAssigned}, 피로 {staff.Fatigue}, 무료함 {staff.Stagnation}, 신뢰 {staff.TrustToManager}, 유지위험 {staff.RetentionRisk}. {StaffMemo(staff)}");
        }

        builder.AppendLine();
        builder.AppendLine("## 3-1. 인재 유지/채용난");
        builder.AppendLine($"현재 채용난 지수는 {state.TalentShortage}입니다.");
        builder.AppendLine("인재이탈은 단순 결원이 아니라 이슈 증가, 부진 누적, 추가 이탈 위험으로 이어지는 핵심 위험입니다.");

        builder.AppendLine();
        builder.AppendLine("## 4. 감사 후보");
        var auditCandidates = state.Queue
            .Where(e => e.LatentRisk >= 30 || e.OutcomeScore < 60 || e.MismatchScore >= 2)
            .OrderByDescending(e => e.LatentRisk)
            .ToList();
        if (auditCandidates.Count == 0)
        {
            builder.AppendLine("감사 후보 없음. 단, summary-only 종결 여부는 익일 재검토 권장.");
        }
        else
        {
            foreach (var item in auditCandidates)
            {
                builder.AppendLine($"- {item.Id}: {AuditReason(item)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 5. 관리자 확인 요청");
        builder.AppendLine("다음 아침 계획서 반영 전, 위 감사 후보와 인력 부하 편중을 확인하십시오.");
        builder.AppendLine("특히 '종결'로 표기된 항목 중 잠복 리스크가 상승한 건은 실제 해결이 아니라 책임 이월일 수 있습니다.");

        return new DailyReportDocument
        {
            Day = state.Day,
            Title = $"DAY {state.Day:D2} 운영 결과 종합 보고서",
            Body = builder.ToString()
        };
    }

    private static string FormatTeam(List<string> team) => team.Count == 0 ? "미배정" : string.Join(", ", team);

    private static string OutcomeBand(int score)
    {
        if (score >= 75) return "양호";
        if (score >= 55) return "불완전";
        return "위험";
    }

    private static string ManagementSentence(EventCase item)
    {
        if (item.OutcomeScore >= 75) return "절차상 종결 가능하나, 일부 로그의 공백은 후속 표본 확인 대상으로 남습니다.";
        if (item.OutcomeScore >= 55) return "표면상 복구되었으나 확인 근거가 분산되어 있으며, 다음 계획서에 예방 작업 반영이 필요합니다.";
        return "종결 처리는 되었지만 재발 가능성을 낮췄다고 보기 어렵습니다.";
    }

    private static string ReviewMemo(EventCase item)
    {
        if (item.Id == "E-108" && item.AssignedPersonnel.Count >= 2)
        {
            return "단독 우회 처리 대신 절차/관찰 축을 보강한 점은 긍정적입니다. 다만 최초 요약의 '특이사항 없음' 표현은 설비 흔적과 완전히 일치하지 않습니다.";
        }

        if (item.Id == "E-108")
        {
            return "처리 속도는 빨랐으나 승인 메모 공백이 남아, 내일 감사에서 책임 소재가 다시 열릴 수 있습니다.";
        }

        if (item.Kind == "routine")
        {
            return "루틴 backlog는 숫자상 줄었지만, 같은 인력에게 기록 업무가 몰리는 경향이 있습니다.";
        }

        return "민원 분류는 정리되었으나 묶음 종결된 항목의 개별 차이는 충분히 보존되지 않았습니다.";
    }

    private static string OmissionHint(EventCase item)
    {
        if (item.MismatchScore >= 3) return "높음. 요약과 설비/관계 로그 사이의 문장 온도가 다릅니다.";
        if (item.OutcomeScore < 60) return "중간. 종결 문구가 결과 품질보다 단정적입니다.";
        return "낮음. 단, 보고서 문장은 원시 기록보다 매끄럽게 정리되어 있습니다.";
    }

    private static string StaffMemo(Personnel staff)
    {
        if ((staff.Injuries?.Count ?? 0) > 0)
        {
            var latest = staff.Injuries.OrderByDescending(injury => injury.DayAcquired).First();
            return $"INJURY {latest.Kind} severity {latest.Severity}. Work assignment risk must be reviewed.";
        }
        if (staff.HasLeft) return "이미 이탈하여 다음 계획서에 배정할 수 없습니다.";
        if (staff.RetentionRisk >= 70) return "이탈 위험이 높습니다. 부하 경감 또는 신뢰 회복 조치가 필요합니다.";
        if (staff.LoadAssigned > staff.OptHigh) return "부하가 적정 상한을 넘었습니다.";
        if (staff.LoadAssigned < staff.OptLow) return "저부하로 무료함 누적 가능성이 있습니다.";
        return "적정 범위입니다.";
    }

    private static string AuditReason(EventCase item)
    {
        if (item.LatentRisk >= 50) return $"잠복 리스크 {item.LatentRisk}. 종결 문장보다 후폭풍 가능성이 큽니다.";
        if (item.OutcomeScore < 60) return $"결과 점수 {item.OutcomeScore}. 후속 확인 없이는 재발 방지가 불확실합니다.";
        return $"불일치 점수 {item.MismatchScore}. 보고 층위 간 비교 필요.";
    }
}

}
