#if UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    // Spec: MagicalGirlOfficePrototype / 화면과 조작. Shares only the existing UI font.
    public sealed class OfficeDesktop
    {
        public readonly OfficeScenario Scenario = new OfficeScenario();
        public bool SaveBlocked { get; private set; }
        private int page;
        private int person;
        private bool resetConfirmation;
        private Vector2 scroll;
        private GUIStyle body, heading, caption, button, large;
        private static readonly Color Ink = new Color(.13f, .23f, .25f);
        private static readonly Color Muted = new Color(.40f, .49f, .49f);
        private static readonly Color Teal = new Color(.10f, .38f, .37f);
        private static readonly Color Paper = new Color(.95f, .95f, .91f);
        private static readonly Color Line = new Color(.83f, .87f, .84f);
        private static readonly string[] Pages = { "오늘의 업무", "일정표", "인물 기록", "지역 · 사건", "메신저", "활동 기록" };

        public OfficeDesktop()
        {
            if (PlayerPrefs.HasKey(OfficeScenario.SaveKey))
                SaveBlocked = !Scenario.Restore(PlayerPrefs.GetString(OfficeScenario.SaveKey));
        }
        public static float ScaleFor(int width, int height) => Mathf.Max(.1f, Mathf.Min(width / 1280f, height / 800f));
        public bool Draw(Font font)
        {
            EnsureStyles(font);
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            bool previousEnabled = GUI.enabled;
            GUI.color = Color.white;
            GUI.enabled = true;
            float scale = ScaleFor(Screen.width, Screen.height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            bool leave = false;
            Fill(new Rect(0, 0, width, height), Paper);
            Fill(new Rect(0, 0, 220, height), Ink);
            Text(new Rect(26, 30, 172, 22), "SMALL OFFICE / 01", caption, new Color(.66f, .79f, .73f));
            Text(new Rect(26, 61, 180, 75), "마법소녀\n사무소", large, Color.white);
            Text(new Rect(26, 156, 170, 50), "동네를 돌보고,\n각자의 내일을 함께.", body, new Color(.71f, .79f, .75f));
            for (int i = 0; i < Pages.Length; i++)
            {
                Rect item = new Rect(16, 249 + i * 55, 188, 46);
                if (Action(item, (i + 1).ToString("00") + "   " + Pages[i], page == i, true)) { page = i; scroll = Vector2.zero; }
            }
            Text(new Rect(26, height - 143, 170, 42), "7 DAYS / 새로운 시작\n결정할 때마다 자동 저장", caption, new Color(.67f, .78f, .73f));
            if (Action(new Rect(20, height - 88, 180, 44), "처음 화면으로", false, true)) leave = true;

            float x = 252, w = width - 284;
            Text(new Rect(x, 28, 550, 24), "사무소 데스크   /   " + Pages[page], caption, Muted);
            Text(new Rect(width - 285, 28, 255, 24), Scenario.Complete ? "WEEK 01  /  한 주 마무리" : "WEEK 01  /  DAY " + Scenario.Day.ToString("00") + "  ·  업무 시작", caption, Teal);
            Text(new Rect(x, 69, w, 52), Scenario.Complete ? "잘 돌본 시간은, 다음으로 이어집니다." : PageHeading(), large, Ink);
            Text(new Rect(x, 126, w, 34), SaveBlocked ? "저장 형식을 읽을 수 없습니다. 원본을 보존했습니다. 아래 초기화로 새로 시작할 수 있습니다." : "오늘의 선택을 확인하고 하루를 마무리하세요. 현장의 이야기는 보고와 메시지로 도착합니다.", body, SaveBlocked ? new Color(.7f, .25f, .18f) : Muted);
            Fill(new Rect(x, 182, w, 1), Line);

            Rect viewport = new Rect(x, 200, w, height - 300);
            float contentHeight = page == 1 ? 710 : page == 4 || page == 5 || Scenario.Complete ? 1100 : 670;
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, w - 22, contentHeight));
            float cw = w - 22;
            if (Scenario.Complete && page == 0) DrawEnding(cw);
            else if (page == 0) DrawToday(cw);
            else if (page == 1) DrawSchedule(cw);
            else if (page == 2) DrawPeople(cw);
            else if (page == 3) DrawDistrict(cw);
            else DrawJournal(cw, page == 4);
            GUI.EndScrollView();

            Fill(new Rect(220, height - 84, width - 220, 84), Color.white);
            if (Action(new Rect(x, height - 63, 215, 42), resetConfirmation ? "확인: 7일 기록 초기화" : "처음부터 다시 시작"))
            {
                if (resetConfirmation)
                {
                    PlayerPrefs.DeleteKey(OfficeScenario.SaveKey);
                    PlayerPrefs.Save();
                    Scenario.Restore(new OfficeScenario().Export());
                    SaveBlocked = false;
                    resetConfirmation = false;
                    page = 0;
                    scroll = Vector2.zero;
                }
                else resetConfirmation = true;
            }
            if (resetConfirmation && Action(new Rect(x + 226, height - 63, 70, 42), "취소")) resetConfirmation = false;
            if (!resetConfirmation)
                Text(new Rect(x + 234, height - 54, w - 540, 36), Scenario.Complete ? "7일 기록이 저장되었습니다" : Scenario.CanAdvance ? "선택 완료 · 아래 기록에 남습니다" : "오늘의 안건에 응답해주세요", caption, Muted);
            GUI.enabled = Scenario.CanAdvance && !SaveBlocked;
            if (Action(new Rect(width - 300, height - 65, 268, 46), Scenario.Day == 7 ? "계획 확정 · 한 주 마무리 →" : "계획 확정 · 다음 날 →", true))
            {
                Scenario.Advance();
                Save();
                page = 0;
                scroll = Vector2.zero;
            }
            GUI.enabled = previousEnabled;
            GUI.color = previousColor;
            GUI.matrix = previous;
            return leave;
        }

        private string PageHeading()
        {
            switch (page)
            {
                case 1: return "시간표에, 각자의 삶을 먼저.";
                case 2: return "상태 너머에 있는 사람들";
                case 3: return "우리 동네의 작은 변화";
                case 4: return "먼저 말해줘서 고마워요.";
                case 5: return "선택과 그 이후의 기록";
                default: return "오늘, 우리가 챙겨야 할 것들";
            }
        }
        private void DrawToday(float w)
        {
            float card = (w - 24) / 3;
            for (int i = 0; i < 3; i++)
            {
                float cx = i * (card + 12);
                Fill(new Rect(cx, 0, card, 126), Color.white);
                Text(new Rect(cx + 18, 15, card - 36, 31), OfficeScenario.Names[i], heading, Ink);
                Text(new Rect(cx + 18, 53, card - 36, 50), PersonStatus(i), body, Muted);
                if (GUI.Button(new Rect(cx, 0, card, 126), GUIContent.none, GUIStyle.none)) { person = i; page = 2; scroll = Vector2.zero; }
            }
            int day = Scenario.CurrentIndex;
            Text(new Rect(0, 151, w, 26), "응답할 안건  01     /     DAY " + Scenario.Day.ToString("00") + " 마감", caption, Teal);
            Fill(new Rect(0, 187, w, 222), Color.white);
            Fill(new Rect(0, 187, 5, 222), Teal);
            Text(new Rect(25, 205, w - 50, 50), OfficeScenario.Titles[day], heading, Ink);
            Text(new Rect(25, 262, w - 50, 27), OfficeScenario.Senders[day] + "  ·  오늘 09:00", caption, Muted);
            Text(new Rect(25, 304, w - 50, 83), OfficeScenario.Bodies[day], body, Ink);
            for (int i = 0; i < 2; i++)
            {
                float cx = i * ((w - 14) / 2 + 14), bw = (w - 14) / 2;
                GUI.enabled = !SaveBlocked && !Scenario.Complete;
                if (Action(new Rect(cx, 428, bw, 55), (Scenario.Choice(day) == i ? "선택됨  ·  " : "") + OfficeScenario.Options[day * 2 + i], Scenario.Choice(day) == i))
                { Scenario.Choose(i); Save(); }
                GUI.enabled = true;
                Text(new Rect(cx + 8, 497, bw - 16, 68), OfficeScenario.Reasons[day * 2 + i], body, Muted);
            }
            if (Scenario.CanAdvance)
            {
                Fill(new Rect(0, 571, w, 89), new Color(.86f, .92f, .86f));
                Text(new Rect(18, 582, w - 36, 71), "확정 후 도착할 보고\n" + Scenario.Report(day), body, Teal);
            }
        }
        private string PersonStatus(int i)
        {
            if (i == 0) return Scenario.Day > 1 ? "발표 완료 · 학교 일정 유지" : "오늘 학교 발표 · 일정 조율 필요";
            if (i == 1) return Scenario.Day > 2 ? (Scenario.Choice(1) == 0 ? "휴식 확보 · 다음 일정 협의" : "상담 연결 · 본인 동의 확인") : "회복이 필요한 때 · 출동 보류";
            return Scenario.Day > 5 ? (Scenario.Choice(4) == 0 ? "진로 탐색 중 · 다음 생활 준비" : "인계 자료 작성 · 경험을 남기는 중") : "활동 지원 · 졸업 준비 예정";
        }
        private void DrawPeople(float w)
        {
            for (int i = 0; i < 3; i++)
                if (Action(new Rect(i * 170, 0, 156, 45), OfficeScenario.Names[i], person == i)) person = i;
            Fill(new Rect(0, 70, w, 470), Color.white);
            Text(new Rect(26, 94, w - 52, 55), OfficeScenario.Names[person] + "  /  " + OfficeScenario.Roles[person], heading, Ink);
            Text(new Rect(26, 160, w - 52, 40), "확인된 상태  ·  DAY " + Mathf.Min(Scenario.Day, 7).ToString("00"), caption, Teal);
            Text(new Rect(26, 205, w - 52, 50), PersonStatus(person), body, Ink);
            int source = person == 0 ? 0 : person == 1 ? 1 : 4;
            Text(new Rect(26, 276, w - 52, 35), "본인이 전한 이야기  ·  DAY " + (source + 1).ToString("00"), caption, Teal);
            Text(new Rect(26, 321, w - 52, 115), Scenario.Day >= source + 1 ? OfficeScenario.Bodies[source] : "아직 도착한 메시지가 없습니다. 당사자가 전하는 시점에 기록을 갱신합니다.", body, Ink);
            Text(new Rect(26, 451, w - 52, 61), "지원 계획  /  " + OfficeScenario.Roles[person] + "\n개인 기록은 졸업 이후에도 같은 사람에게 남습니다.", body, Muted);
        }
        private void DrawSchedule(float w)
        {
            Text(new Rect(0, 0, w, 45), "WEEK 01     학교 · 휴식 · 지역 대응 · 다음 삶", heading, Ink);
            for (int d = 0; d < 7; d++)
            {
                float y = 62 + d * 88;
                Fill(new Rect(0, y, w, 77), d == Scenario.CurrentIndex ? new Color(.85f, .91f, .86f) : Color.white);
                Text(new Rect(18, y + 16, 90, 40), "DAY " + (d + 1).ToString("00"), heading, Teal);
                Text(new Rect(120, y + 10, w - 140, 30), OfficeScenario.Titles[d], body, Ink);
                string plan = d > Scenario.CurrentIndex ? "계획 · 당일 요청에 맞춰 조정" : Scenario.Choice(d) < 0 ? "계획 미확정 · 오늘의 업무에서 응답" : OfficeScenario.Options[d * 2 + Scenario.Choice(d)];
                Text(new Rect(120, y + 43, w - 140, 27), plan, caption, Muted);
            }
        }
        private void DrawDistrict(float w)
        {
            string[] sites = { "01  학교", "02  하천 산책로", "03  지원센터" };
            string[] notes = { "하나의 발표와 수업 시간을 보호합니다.", Scenario.Day > 4 ? "사건 안정 · 후속 예방과 지역 연결" : "이상 반응 관측 · 주민 접근 제한", "회복·상담과 전문기관 지원을 연결합니다." };
            for (int i = 0; i < 3; i++)
            {
                Fill(new Rect(0, i * 113, w, 98), Color.white);
                Text(new Rect(22, i * 113 + 14, 250, 36), sites[i], heading, Teal);
                Text(new Rect(285, i * 113 + 20, w - 310, 65), notes[i], body, Ink);
            }
            Text(new Rect(0, 360, w, 42), "하천 사건   /   조사 → 준비 → 대응 → 후속 점검", heading, Ink);
            Text(new Rect(0, 421, w, 180), "확인된 보고 · DAY " + Mathf.Min(Scenario.Day, 7).ToString("00") + "\n\n" +
                (Scenario.Day > 4 ? Scenario.Report(3) : "주민 접근을 제한하고 관측 중입니다. 사건의 원인과 범위는 추가 확인이 필요합니다.") +
                "\n\n대응 방침 / 안전 범위 초과 시 접근 중단. 전문기관과 연결하고, 주민 보호를 우선합니다.", body, Muted);
            if (Action(new Rect(0, 607, 255, 46), "오늘의 대응 결정 보기 →", true)) { page = 0; scroll = Vector2.zero; }
        }
        private void DrawJournal(float w, bool messages)
        {
            Text(new Rect(0, 0, w, 40), messages ? "받은 이야기와 후속 소식" : "확정한 결정은 이곳에 남습니다", heading, Ink);
            float y = 60;
            for (int d = Mathf.Min(Scenario.Day - 1, 6); d >= 0; d--)
            {
                bool committed = d < Scenario.Day - 1;
                if (!messages && !committed) continue;
                Fill(new Rect(0, y, w, 130), Color.white);
                Text(new Rect(18, y + 13, w - 36, 26), "DAY " + (d + 1).ToString("00") + "  ·  " + (messages ? OfficeScenario.Senders[d] : OfficeScenario.Titles[d]), caption, Teal);
                Text(new Rect(18, y + 51, w - 36, 70), committed ? Scenario.Report(d) : OfficeScenario.Bodies[d], body, Ink);
                y += 142;
            }
            if (y == 60) Text(new Rect(0, y, w, 60), "오늘의 계획을 확정하면 첫 기록이 남습니다.", body, Muted);
        }
        private void DrawEnding(float w)
        {
            Fill(new Rect(0, 0, w, 132), Teal);
            Text(new Rect(24, 20, w - 48, 40), "7일 동안 남긴 것은, 각자의 다음입니다.", heading, Color.white);
            Text(new Rect(24, 74, w - 48, 42), "학교를 지킨 하나 · 도움을 요청한 소리 · 다음 생활을 준비하는 유나", body, Color.white);
            for (int i = 0; i < 3; i++)
            {
                float y = 154 + i * 160;
                Fill(new Rect(0, y, w, 140), Color.white);
                Text(new Rect(20, y + 13, w - 40, 39), OfficeScenario.Names[i] + "  /  " + PersonStatus(i), heading, Ink);
                Text(new Rect(20, y + 62, w - 40, 68), Scenario.Report(i == 0 ? 0 : i == 1 ? 1 : 4), body, Muted);
            }
            Text(new Rect(0, 657, w, 100), Scenario.Report(6) + "\n\n졸업과 멘토 참여는 당사자의 다음 선택으로 남겨둡니다.", body, Teal);
            if (Action(new Rect(0, 787, 280, 48), "한 주의 활동 기록 읽기 →", true)) { page = 5; scroll = Vector2.zero; }
        }
        private void Save()
        {
            if (SaveBlocked) return;
            PlayerPrefs.SetString(OfficeScenario.SaveKey, Scenario.Export());
            PlayerPrefs.Save();
        }
        private void EnsureStyles(Font font)
        {
            if (body != null) return;
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true, richText = false };
            heading = new GUIStyle(body) { fontSize = 23, fontStyle = FontStyle.Bold };
            large = new GUIStyle(heading) { fontSize = 31 };
            caption = new GUIStyle(body) { fontSize = 15 };
            button = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 17, fontStyle = FontStyle.Bold };
        }
        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
        private static void Text(Rect r, string value, GUIStyle style, Color c)
        {
            style.normal.textColor = c;
            GUI.Label(r, value, style);
        }
        private bool Action(Rect r, string value, bool selected = false, bool dark = false)
        {
            Color fill = selected ? Teal : dark ? Ink : new Color(.88f, .91f, .87f);
            if (r.Contains(Event.current.mousePosition) && GUI.enabled) fill = selected ? new Color(.15f, .46f, .43f) : new Color(.71f, .82f, .75f);
            Fill(r, fill);
            Text(r, value, button, selected || dark ? Color.white : Ink);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
    }
}
#endif
