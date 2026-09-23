#if UNITY_WEBGL || UNITY_EDITOR
using System;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    // Spec: MagicalGirlOfficePrototype — authored M1 only; not the campaign simulator.
    [Serializable]
    public sealed class OfficeSnapshot
    {
        public int SchemaVersion = 1;
        public string ScenarioId = "office-seven-days";
        public int Day = 1;
        public int[] Choices = { -1, -1, -1, -1, -1, -1, -1 };
    }

    public sealed class OfficeScenario
    {
        public const string SaveKey = "projectw.office.m1.v1";
        public static readonly string[] PersonIds = { "office-hana", "office-sori", "office-yuna" };
        public static readonly string[] Names = { "하나", "소리", "유나" };
        public static readonly string[] Roles = { "학교와 활동 사이의 균형", "회복하고, 다시 천천히", "졸업 너머의 일상을 준비" };
        public static readonly string[] Titles = {
            "발표가 있는 날, 조사는 누가 맡을까요?", "오늘은 쉬어도 괜찮을까요?",
            "하천 산책로 · 대응 전에 준비할 것", "현장에서 지킬 범위를 정해주세요",
            "유나의 다음 계절을 준비합니다", "다음 출동을 줄이는 하루", "우리가 지킨 일주일" };
        public static readonly string[] Senders = { "하나 · 본인 메시지", "소리 · 본인 메시지", "구청 협력 담당 · 확인된 보고", "현장 담당 · 확인된 보고", "유나 · 본인 메시지", "지역 협력 담당 · 확인된 보고", "사무소 · 주간 회고" };
        public static readonly string[] Bodies = {
            "오늘 오후에 학교 발표가 있어요. 제가 맡은 부분이라 빠지고 싶지 않아요. 하천 조사는 수업이 끝난 뒤에 가도 될까요?",
            "어제부터 좀 지쳐 있어요. 심해진 건 아닌데, 이번에는 미리 말하고 싶었어요. 오늘 현장 일정은 쉬고 싶어요.",
            "산책로의 이상 반응은 계속 관측되고 있습니다. 주민 접근은 제한했습니다. 대응 전에 관측 범위와 지원 경로를 정해주세요.",
            "관측 지점 밖에서 반응이 커졌습니다. 합의한 안전 범위를 넘는 접근은 하지 않습니다. 주민 안내를 계속할지 전문기관에 인계할지 결정해주세요.",
            "졸업한 뒤 어떤 생활을 할지 생각해보고 싶어요. 후배를 돕는 것도 좋지만, 바로 멘토가 되겠다는 약속은 아직 어렵고요.",
            "하천 사건은 안정됐습니다. 오늘 확보한 여유를 예방 점검이나 학교·지원센터 연락망 정리에 사용할 수 있습니다.",
            "많이 출동한 주보다, 필요한 도움을 제때 연결한 주를 기록합니다. 다음 주에도 이어갈 약속을 함께 정리해주세요." };
        public static readonly string[] Options = {
            "수업 후 조사 예약", "협력기관에 조사 요청", "하루 휴식 확보", "동의 후 지원센터 상담 예약",
            "유나와 관측 준비", "전문기관 지원 먼저 요청", "안전 범위에서 관측·주민 안내", "현장 대응을 전문기관에 인계",
            "진로 탐색 시간 확보", "희망하는 인계 자료 함께 정리", "지역 예방 점검", "학교·지원기관 연락망 정리",
            "다음 주 약속 정리", "지원 계획 함께 검토" };
        public static readonly string[] Reasons = {
            "발표 시간을 보호하고 조사 시작을 방과 후로 옮깁니다.", "하나는 발표에 집중하고 성인 협력기관이 조사합니다.",
            "오늘 출동을 제외하고 회복할 시간을 온전히 확보합니다.", "출동을 제외하고, 소리의 동의를 확인한 뒤 상담을 연결합니다.",
            "경험 있는 유나와 접근 한계·복귀 기준을 정리합니다.", "현장 접근보다 기관 지원과 정보 확보를 우선합니다.",
            "유나는 정해진 범위를 지키고 전문 대응팀을 지원합니다.", "더 깊이 접근하지 않고 자료와 주민 안내를 인계합니다.",
            "당사자가 원하는 다음 생활을 알아볼 시간을 지킵니다.", "유나가 남기고 싶은 경험만 후임용 자료로 정리합니다.",
            "반복 출동을 줄일 수 있도록 지역 상황을 미리 확인합니다.", "도움이 필요할 때 바로 연결할 담당과 연락 경로를 정리합니다.",
            "학교·회복·진로에 대한 각자의 약속을 기록합니다.", "본인과 함께 지원의 지속 여부와 방식을 돌아봅니다." };
        public static readonly string[] Results = {
            "하나: 발표를 마쳤어요. 시간을 바꿔줘서 고마워요. 방과 후 조사 기록을 남겼어요.",
            "하나: 발표에 집중할 수 있었어요. 협력기관이 보내온 하천 조사 보고도 확인했어요.",
            "소리: 오늘 푹 쉬었어요. 미리 말하길 잘한 것 같아요. 다음 일정은 함께 정하고 싶어요.",
            "소리: 제 동의를 먼저 물어봐줘서 좋았어요. 지원센터와 다음 상담 일정을 정했어요.",
            "유나: 관측 지점과 복귀 기준을 정리했어요. 범위를 넘으면 지원팀에 넘길게요.",
            "협력기관: 지원 요청을 접수했습니다. 현장 대응팀과 관측 자료를 공유했습니다.",
            "현장 보고: 안전 범위를 지키며 주민을 안내했습니다. 전문 대응팀이 이상 반응을 안정시켰습니다.",
            "기관 보고: 현장을 인계받아 안정화했습니다. 유나는 복귀했고 관측 기록은 보존했습니다.",
            "유나: 다음에 배우고 싶은 걸 찾아봤어요. 멘토 여부는 나중에 제 상황을 보고 이야기할게요.",
            "유나: 제가 어려웠던 순간과 도움받은 방법을 남겼어요. 졸업 후 계획은 계속 생각하고 있어요.",
            "지역 보고: 예방 점검을 마쳤습니다. 다음 주에는 반복 확인 대신 학교와 회복 일정을 확보했습니다.",
            "학교·지원센터: 담당 연락망이 정리됐습니다. 다음 요청은 혼자 해결하지 않고 함께 연결합니다.",
            "한 주의 약속: 하나의 학교 시간, 소리의 회복, 유나의 진로 탐색을 다음 주에도 이어갑니다.",
            "한 주의 검토: 각자의 동의와 지원 경로를 확인했습니다. 도움이 달라지면 계획도 다시 조정합니다." };

        private OfficeSnapshot state = new OfficeSnapshot();
        public int Day => state.Day;
        public bool Complete => Day == 8;
        public int CurrentIndex => Math.Min(Day - 1, 6);
        public bool CanAdvance => !Complete && Choice(CurrentIndex) >= 0;
        public int Choice(int index) => index >= 0 && index < 7 ? state.Choices[index] : -1;
        public bool Choose(int choice)
        {
            if (Complete || choice < 0 || choice > 1) return false;
            state.Choices[CurrentIndex] = choice;
            return true;
        }
        public bool Advance()
        {
            if (!CanAdvance) return false;
            state.Day++;
            return true;
        }
        public string Report(int index) => Choice(index) < 0 ? "아직 결정하지 않았습니다." : Results[index * 2 + Choice(index)];
        public string Export() => JsonUtility.ToJson(state);
        public bool Restore(string json)
        {
            // Require fields explicitly: JsonUtility otherwise accepts absent fields as defaults.
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"SchemaVersion\"") ||
                !json.Contains("\"ScenarioId\"") || !json.Contains("\"Day\"") || !json.Contains("\"Choices\"")) return false;
            OfficeSnapshot next;
            try { next = JsonUtility.FromJson<OfficeSnapshot>(json); }
            catch (ArgumentException) { return false; }
            if (next == null || next.SchemaVersion != 1 || next.ScenarioId != "office-seven-days" ||
                next.Day < 1 || next.Day > 8 || next.Choices == null || next.Choices.Length != 7) return false;
            for (int i = 0; i < 7; i++)
                if (next.Choices[i] < -1 || next.Choices[i] > 1 ||
                    (i < next.Day - 1 && next.Choices[i] < 0) || (i > next.Day - 1 && next.Choices[i] != -1)) return false;
            state = next;
            return true;
        }
    }
}
#endif
