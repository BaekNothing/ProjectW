#if UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectW.MilestonePrototype
{
    // Spec: MagicalGirlOfficePrototype / 화면과 조작. Shares only the existing UI font.
    public sealed class OfficeDesktop
    {
        public readonly OfficeScenario Scenario = new OfficeScenario();
        public readonly OfficeWindowLayout Layout = new OfficeWindowLayout();
        private readonly OfficeTouchGesture touchGesture = new OfficeTouchGesture();
        public bool SaveBlocked { get; private set; }
        private int page, person;
        private Vector2 scroll;
        private GUIStyle body, heading, caption, button, large;
        private static readonly Color Ink = new Color(.28f, .22f, .40f);
        private static readonly Color Muted = new Color(.49f, .43f, .59f);
        private static readonly Color Teal = new Color(.54f, .37f, .70f);
        private static readonly Color Paper = new Color(.98f, .96f, 1f);
        private static readonly Color Line = new Color(.73f, .64f, .83f);
        private static readonly Color Pink = new Color(.99f, .76f, .85f);
        private static readonly Color Mint = new Color(.74f, .91f, .86f);
        private static readonly string[] Pages = { "오늘의 업무", "일정표", "인물 기록", "지역 · 사건", "메신저", "활동 기록", "설정" };
        private static readonly string[] Executables = { "today.exe", "calendar.exe", "friends.exe", "town.exe", "letter.exe", "diary.exe", "settings.exe" };
        private enum Modal { None, ConfirmDay, DayReport, WeekReport, Reset, Preview }
        private Modal modal;
        private bool effects = true, started, leave;
        private float toastUntil;
        private string toastTitle, toastMessage, report;
        private int toastApp, unreadDay;
        private int pendingApp = -1, pendingPerson = -1, raise = -1, resizing = -1;
        private Rect resizeOrigin;
        private Vector2 resizePointer;
        private float width, height;
        private Texture2D radial, sparkle, ring, glow;
        private Rect ToastRect => new Rect(width - 378, height - 210, 354, 140);
        private bool HasToast => Time.unscaledTime < toastUntil;
        private bool CanInteract => modal == Modal.None;

        public OfficeDesktop()
        {
            if (PlayerPrefs.HasKey(OfficeScenario.SaveKey))
                SaveBlocked = !Scenario.Restore(PlayerPrefs.GetString(OfficeScenario.SaveKey));
        }
        public static float ScaleFor(int width, int height) => Mathf.Max(.1f, Mathf.Min(width / 1280f, height / 800f));
        public void UpdateTouchInput()
        {
            var touchscreen = Touchscreen.current;
            int count = 0, firstId = 0, secondId = 0;
            Vector2 first = Vector2.zero, second = Vector2.zero;
            float scale = ScaleFor(Screen.width, Screen.height);
            if (touchscreen != null)
                for (int i = 0; i < touchscreen.touches.Count; i++)
                {
                    var touch = touchscreen.touches[i];
                    if (!touch.press.isPressed) continue;
                    Vector2 point = MilestonePrototypeController.TouchToLogicalPosition(touch.position.ReadValue(), Screen.height, scale);
                    if (count == 0) { first = point; firstId = touch.touchId.ReadValue(); }
                    else if (count == 1) { second = point; secondId = touch.touchId.ReadValue(); }
                    count++;
                }
            width = Screen.width / scale; height = Screen.height / scale;
            int front = Layout.Front;
            bool blocked = !CanInteract || (HasToast && (ToastRect.Contains(first) || (count > 1 && ToastRect.Contains(second))));
            touchGesture.Process(Layout, count, firstId, secondId, first, second, scale, width, height, blocked);
            if (front != Layout.Front) raise = Layout.Front;
        }
        public bool Draw(Font font, Texture2D radialTexture = null, Texture2D sparkleTexture = null,
            Texture2D ringTexture = null, Texture2D glowTexture = null)
        {
            EnsureStyles(font);
            radial = radialTexture; sparkle = sparkleTexture; ring = ringTexture; glow = glowTexture;
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            bool previousEnabled = GUI.enabled;
            GUI.color = Color.white;
            GUI.enabled = true;
            float scale = ScaleFor(Screen.width, Screen.height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            width = Screen.width / scale; height = Screen.height / scale;
            leave = false;
            if (!started)
            {
                started = true;
                Notify(SaveBlocked ? "저장을 확인해주세요" : "새로운 하루가 도착했어요", SaveBlocked ? "지원하지 않는 저장입니다. 설정에서 확인하세요." : "오늘의 안건을 열고 이야기를 확인하세요.", SaveBlocked ? 6 : 0);
            }
            if (pendingApp >= 0)
            {
                Layout.Show(pendingApp);
                if (pendingPerson >= 0) Layout.Get(pendingApp).Person = pendingPerson;
                if (pendingApp == 4) unreadDay = Scenario.Day;
                raise = pendingApp;
                pendingApp = pendingPerson = -1;
            }
            Layout.ConstrainAll(width, height);
            if (touchGesture.SuppressPointer)
            {
                resizing = -1;
                GUIUtility.hotControl = 0;
                if (Event.current.isMouse || Event.current.type == EventType.ScrollWheel) Event.current.Use();
            }
            else HandleWindowInput();
            bool overToast = HasToast && ToastRect.Contains(Event.current.mousePosition);
            GUI.enabled = !OfficeWindowLayout.BlocksDesktop(!CanInteract, overToast, Layout.HitTest(Event.current.mousePosition));
            DrawWallpaper();
            DrawDesktopIcons();
            GUI.enabled = CanInteract;
            DrawTaskbar();
            for (int i = 0; i < OfficeWindowLayout.AppCount; i++)
            {
                var window = Layout.Get(i);
                if (!window.Open || window.Minimized) continue;
                GUI.enabled = CanInteract;
                window.Rect = OfficeWindowLayout.Constrain(GUI.Window(930000 + i, window.Rect, DrawAppWindow, string.Empty, GUIStyle.none), width, height);
            }
            if (raise >= 0) { GUI.BringWindowToFront(930000 + raise); raise = -1; }
            GUI.enabled = true;
            if (HasToast && CanInteract)
            {
                GUI.Window(930100, ToastRect, _ => DrawToast(), string.Empty, GUIStyle.none);
                GUI.BringWindowToFront(930100);
            }
            if (!CanInteract)
            {
                GUI.Window(930101, new Rect(0, 0, width, height), _ => DrawModal(), string.Empty, GUIStyle.none);
                GUI.BringWindowToFront(930101);
            }
            GUI.enabled = previousEnabled;
            GUI.color = previousColor;
            GUI.matrix = previous;
            return leave;
        }

        private void RequestApp(int app, int selectedPerson = -1) { pendingApp = app; pendingPerson = selectedPerson; }
        private void Notify(string title, string message, int app)
        {
            toastTitle = title; toastMessage = message; toastApp = app;
            toastUntil = Time.unscaledTime + 7;
        }
        private void HandleWindowInput()
        {
            Event e = Event.current;
            if (!CanInteract) { resizing = -1; return; }
            if (resizing >= 0)
            {
                if (e.type == EventType.MouseDrag)
                {
                    Vector2 delta = e.mousePosition - resizePointer;
                    Rect next = resizeOrigin;
                    next.width += delta.x; next.height += delta.y;
                    Layout.Get(resizing).Rect = OfficeWindowLayout.Constrain(next, width, height);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp) { resizing = -1; e.Use(); }
                return;
            }
            if (e.type != EventType.MouseDown || e.button != 0 || (HasToast && ToastRect.Contains(e.mousePosition))) return;
            int hit = Layout.HitTest(e.mousePosition);
            if (hit < 0) return;
            Layout.Focus(hit); raise = hit;
            Rect rect = Layout.Get(hit).Rect;
            if (!new Rect(rect.xMax - 25, rect.yMax - 25, 25, 25).Contains(e.mousePosition)) return;
            resizing = hit; resizeOrigin = rect; resizePointer = e.mousePosition; e.Use();
        }
        private void DrawWallpaper()
        {
            Fill(new Rect(0, 0, width, height), new Color(.88f, .84f, .96f));
            for (float x = 0; x < width; x += 48) Fill(new Rect(x, 32, 1, height - 86), new Color(.94f, .91f, .99f));
            for (float y = 32; y < height - 54; y += 48) Fill(new Rect(0, y, width, 1), new Color(.94f, .91f, .99f));
            Fill(new Rect(0, 0, width, 32), Paper);
            Text(new Rect(20, 5, 600, 23), "small office OS   /   마법소녀 사무소", caption, Ink);
            Text(new Rect(width - 270, 5, 250, 23), "WEEK 01     DAY " + Mathf.Min(Scenario.Day, 7).ToString("00") + "     ♡ ONLINE", caption, Teal);
            // Quiet wallpaper motif leaves room for overlapping applications.
            IconEffect(new Rect(width * .57f, height * .24f, 240, 240), Mint, false);
            Text(new Rect(width * .52f, height * .38f, 430, 63), "a little magic,", large, new Color(.71f, .61f, .83f));
            Text(new Rect(width * .52f + 50, height * .38f + 49, 420, 63), "a little everyday.", large, new Color(.71f, .61f, .83f));
            Text(new Rect(width - 456, height - 134, 410, 42), "오늘도, 각자의 내일을 함께.  ♡", heading, Teal);
        }
        private void DrawDesktopIcons()
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                Rect tile = new Rect(24 + (i / 4) * 126, 66 + (i % 4) * 143, 112, 124);
                bool hover = GUI.enabled && tile.Contains(Event.current.mousePosition);
                if (hover) Frame(tile, new Color(.97f, .91f, 1f), Line, false);
                Rect icon = new Rect(tile.x + 25, tile.y + 9, 62, 62);
                bool unread = (i == 0 && !Scenario.Complete && !Scenario.CanAdvance) || (i == 4 && unreadDay != Scenario.Day);
                if (unread || hover) IconEffect(new Rect(icon.x - 15, icon.y - 15, 92, 92), i == 4 ? Pink : Mint, hover);
                DrawIcon(icon, i);
                Text(new Rect(tile.x, tile.y + 82, tile.width, 30), Pages[i], button, Ink);
                if (unread)
                {
                    Frame(new Rect(tile.x + 76, tile.y + 4, 25, 25), Pink, Ink, false);
                    Text(new Rect(tile.x + 76, tile.y + 4, 25, 25), "!", button, Ink);
                }
                if (GUI.Button(tile, GUIContent.none, GUIStyle.none)) RequestApp(i);
            }
        }
        private void DrawTaskbar()
        {
            Frame(new Rect(0, height - 54, width, 54), new Color(.94f, .89f, .98f), Line, false);
            if (Action(new Rect(8, height - 46, 104, 38), "♡ 설정")) RequestApp(6);
            float x = 124;
            for (int i = 0; i < Pages.Length; i++)
            {
                var window = Layout.Get(i);
                if (!window.Open) continue;
                if (Action(new Rect(x, height - 46, 119, 38), Pages[i], Layout.Front == i && !window.Minimized))
                {
                    if (Layout.Front == i && !window.Minimized) Layout.Minimize(i);
                    else RequestApp(i);
                }
                x += 124;
            }
            Text(new Rect(width - 160, height - 40, 150, 30), "DAY " + Mathf.Min(Scenario.Day, 7).ToString("00") + "  /  09:00", caption, Ink);
        }
        private void DrawAppWindow(int id)
        {
            int app = id - 930000;
            var window = Layout.Get(app);
            float ww = window.Rect.width, wh = window.Rect.height;
            Frame(new Rect(0, 0, ww, wh), Paper, Ink, true);
            Fill(new Rect(3, 3, ww - 6, 35), Layout.Front == app ? Pink : new Color(.85f, .80f, .92f));
            DrawIcon(new Rect(10, 9, 22, 22), app);
            Text(new Rect(42, 8, ww - 165, 29), Pages[app] + "  /  " + Executables[app], caption, Ink);
            if (Action(new Rect(ww - 76, 7, 30, 27), "_")) Layout.Minimize(app);
            if (Action(new Rect(ww - 39, 7, 30, 27), "×")) Layout.Close(app);
            Text(new Rect(17, 47, ww - 34, 33), app == 6 ? "내 컴퓨터 · 알림과 화면" : PageHeading(app), caption, Muted);
            Fill(new Rect(12, 84, ww - 24, 1), Line);
            int previousPage = page, previousPerson = person;
            Vector2 previousScroll = scroll;
            page = app; person = window.Person; scroll = window.Scroll;
            float contentHeight = app == 1 ? 710 : app == 4 || app == 5 || (Scenario.Complete && app == 0) ? 1100 : 690;
            window.ContentHeight = contentHeight;
            OfficeTouchGesture.ClampScroll(window);
            scroll = window.Scroll;
            Rect view = new Rect(14, 97, ww - 28, wh - 165);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 22, contentHeight));
            float cw = view.width - 22;
            if (app == 6) DrawSettings(cw);
            else if (Scenario.Complete && app == 0) DrawEnding(cw);
            else if (app == 0) DrawToday(cw);
            else if (app == 1) DrawSchedule(cw);
            else if (app == 2) DrawPeople(cw);
            else if (app == 3) DrawDistrict(cw);
            else DrawJournal(cw, app == 4);
            GUI.EndScrollView();
            window.Scroll = scroll; window.Person = person;
            page = previousPage; person = previousPerson; scroll = previousScroll;
            Fill(new Rect(12, wh - 56, ww - 24, 1), Line);
            Text(new Rect(20, wh - 42, ww - 295, 33), SaveBlocked ? "저장 오류 · 설정에서 확인" : "자동 저장  ·  small office OS", caption, Muted);
            if (app == 0 && !Scenario.Complete)
            {
                GUI.enabled = CanInteract && Scenario.CanAdvance && !SaveBlocked;
                if (Action(new Rect(ww - 258, wh - 47, 223, 34), "계획 확정 · 하루 마무리", true)) modal = Modal.ConfirmDay;
                GUI.enabled = CanInteract;
            }
            for (int i = 0; i < 3; i++) Fill(new Rect(ww - 8 - i * 5, wh - 11 - i * 5, 2, 3 + i * 5), Ink);
            // Native IMGUI owns window dragging and pointer capture.
            if (CanInteract) GUI.DragWindow(new Rect(0, 0, ww - 85, 37));
        }
        private void DrawSettings(float w)
        {
            Text(new Rect(8, 5, w - 16, 46), "조금 더 나다운 바탕화면", heading, Ink);
            Text(new Rect(8, 64, w - 16, 104), "아이콘을 눌러 앱을 열고, 제목줄을 잡아 이동하세요.\n모바일: 창 안을 밀어 스크롤 / 두 손가락으로 창 확대·축소.\n창 모서리로도 크기 조절이 가능해요.\n최소화한 앱은 작업표시줄에서 다시 열 수 있어요.", body, Muted);
            if (Action(new Rect(8, 175, w - 16, 48), effects ? "아이콘 배경 이펙트  ON" : "아이콘 배경 이펙트  OFF", effects)) effects = !effects;
            Text(new Rect(8, 237, w - 16, 67), "빛 · 회전하는 후광 · 반짝임\n새 안건과 선택한 아이콘 뒤에서 표시돼요.", body, Muted);
            if (Action(new Rect(8, 325, (w - 28) / 2, 49), "전체화면 알림 보기")) modal = Modal.Preview;
            if (Action(new Rect(20 + (w - 28) / 2, 325, (w - 28) / 2, 49), "우하단 알림 보기")) Notify("새 메시지가 왔어요", "메신저를 열어 이야기를 확인해보세요.", 4);
            Text(new Rect(8, 400, w - 16, 80), SaveBlocked ? "현재 저장을 읽을 수 없어 원본을 보존했어요. 새로 시작하려면 아래에서 초기화를 확인해주세요." : "창 배치는 이번 접속 동안 유지됩니다.\n7일 시나리오의 결정은 따로 저장됩니다.", body, Muted);
            if (Action(new Rect(8, 503, w - 16, 48), "7일 시나리오 다시 시작")) modal = Modal.Reset;
            if (Action(new Rect(8, 568, w - 16, 48), "처음 화면으로 돌아가기")) leave = true;
        }
        private void DrawToast()
        {
            Frame(new Rect(0, 0, 354, 140), Paper, Ink, true);
            Fill(new Rect(3, 3, 348, 29), Mint);
            Text(new Rect(12, 5, 290, 27), "♡ " + toastTitle, caption, Ink);
            if (Action(new Rect(318, 5, 28, 24), "×")) toastUntil = 0;
            Text(new Rect(16, 43, 322, 52), toastMessage, body, Ink);
            if (Action(new Rect(16, 100, 322, 30), Pages[toastApp] + " 열기 →")) { RequestApp(toastApp); toastUntil = 0; }
        }
        private void DrawModal()
        {
            Fill(new Rect(0, 0, width, height), new Color(.20f, .13f, .32f, .82f));
            float mw = 704, mh = 514;
            Rect panel = new Rect((width - mw) / 2, (height - mh) / 2, mw, mh);
            GUI.BeginGroup(panel);
            Frame(new Rect(0, 0, mw, mh), Paper, Ink, true);
            Fill(new Rect(3, 3, mw - 6, 35), Pink);
            Text(new Rect(16, 8, mw - 32, 28), "small office OS  /  중요한 알림", caption, Ink);
            IconEffect(new Rect(mw / 2 - 80, 43, 160, 160), Pink, true);
            DrawIcon(new Rect(mw / 2 - 36, 91, 72, 72), modal == Modal.Reset ? 6 : modal == Modal.Preview ? 4 : 0);
            string title = modal == Modal.Reset ? "처음부터 다시 시작할까요?" : modal == Modal.ConfirmDay ? "오늘의 계획을 확정할까요?" : modal == Modal.WeekReport ? "우리의 작은 일주일, 저장 완료!" : modal == Modal.Preview ? "띵동! 중요한 이야기가 도착했어요" : "오늘의 기록이 도착했어요";
            Text(new Rect(36, 203, mw - 72, 46), title, heading, Ink);
            string message = modal == Modal.Reset ? "이 프로토타입의 7일 기록만 초기화됩니다.\n기존 캠페인과 인물 기록에는 영향을 주지 않아요." : modal == Modal.ConfirmDay ? OfficeScenario.Options[Scenario.CurrentIndex * 2 + Scenario.Choice(Scenario.CurrentIndex)] + "\n\n확정하면 오늘의 결정이 기록되고 하루가 진행됩니다." : modal == Modal.Preview ? "전체화면 알림은 중요한 결정을 잠시 기다려줘요.\n확인을 누르면 열어두었던 창으로 돌아갑니다." : report;
            Text(new Rect(36, 273, mw - 72, 126), message, body, Muted);
            bool confirm = modal == Modal.Reset || modal == Modal.ConfirmDay;
            if (confirm && Action(new Rect(36, 435, 300, 48), "잠깐, 돌아갈게요", false, false, true)) modal = Modal.None;
            if (Action(new Rect(confirm ? 354 : 202, 435, 314, 48), confirm ? "네, 확정할게요" : "확인 · 바탕화면으로", true, false, true))
            {
                if (modal == Modal.Reset)
                {
                    PlayerPrefs.DeleteKey(OfficeScenario.SaveKey); PlayerPrefs.Save();
                    Scenario.Restore(new OfficeScenario().Export()); SaveBlocked = false;
                    for (int i = 0; i < Pages.Length; i++) { Layout.Close(i); Layout.Get(i).Scroll = Vector2.zero; }
                    unreadDay = 0; modal = Modal.None;
                    Notify("새로운 일주일", "첫 안건이 도착했어요. 오늘의 업무를 열어주세요.", 0);
                }
                else if (modal == Modal.ConfirmDay)
                {
                    report = Scenario.Report(Scenario.CurrentIndex);
                    if (Scenario.Advance())
                    {
                        Save(); modal = Scenario.Complete ? Modal.WeekReport : Modal.DayReport;
                        for (int i = 0; i < Pages.Length; i++) Layout.Get(i).Scroll = Vector2.zero;
                    }
                    else modal = Modal.None;
                }
                else
                {
                    bool preview = modal == Modal.Preview;
                    modal = Modal.None;
                    if (!preview) Notify(Scenario.Complete ? "한 주를 마쳤어요" : "새로운 메시지가 왔어요", Scenario.Complete ? "오늘의 업무에서 한 주의 이야기를 읽어보세요." : "다음 안건과 후속 소식을 확인해주세요.", Scenario.Complete ? 0 : 4);
                }
            }
            GUI.EndGroup();
        }
        private void IconEffect(Rect r, Color color, bool highlighted)
        {
            if (!effects) return;
            float now = Time.unscaledTime;
            Color old = GUI.color;
            Matrix4x4 matrix = GUI.matrix;
            GUI.color = new Color(color.r, color.g, color.b, .32f + Mathf.Sin(now * 1.6f) * .09f);
            if (glow != null) GUI.DrawTexture(r, glow);
            if (radial != null)
            {
                GUIUtility.RotateAroundPivot(now * 12, r.center);
                GUI.DrawTexture(r, radial);
                GUI.matrix = matrix;
            }
            GUI.color = new Color(color.r, color.g, color.b, highlighted ? .7f : .45f);
            if (ring != null) GUI.DrawTexture(r, ring);
            if (sparkle != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    float phase = now * .7f + i * 1.57f;
                    float size = 10 + 6 * (Mathf.Sin(now * 2 + i) + 1);
                    GUI.DrawTexture(new Rect(r.center.x + Mathf.Cos(phase) * r.width * .4f - size / 2,
                        r.center.y + Mathf.Sin(phase) * r.height * .4f - size / 2, size, size), sparkle);
                }
            }
            GUI.color = old; GUI.matrix = matrix;
        }
        private static void Frame(Rect r, Color fill, Color border, bool shadow)
        {
            if (shadow) Fill(new Rect(r.x + 4, r.y + 4, r.width, r.height), new Color(.32f, .23f, .42f, .22f));
            Fill(r, border); Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), fill);
            Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, 2), Color.white);
            Fill(new Rect(r.x + 2, r.y + 2, 2, r.height - 4), Color.white);
        }
        private void DrawIcon(Rect r, int app)
        {
            // Code-native icons stay sharp and can be replaced independently of effect layers.
            GUI.BeginGroup(r);
            float s = r.width / 64;
            if (app == 4)
            {
                IconBlock(5, 15, 54, 37, s, Pink);
                IconBlock(9, 19, 46, 29, s, Paper);
                for (int i = 0; i < 6; i++) { IconBlock(10 + i * 4, 21 + i * 3, 5, 3, s, Teal); IconBlock(49 - i * 4, 21 + i * 3, 5, 3, s, Teal); }
            }
            else if (app == 3)
            {
                IconBlock(5, 8, 54, 49, s, Mint);
                IconBlock(9, 12, 46, 41, s, Paper);
                IconBlock(24, 12, 5, 41, s, Mint); IconBlock(9, 32, 46, 5, s, Mint);
                IconBlock(36, 19, 12, 12, s, Pink); IconBlock(39, 29, 6, 8, s, Teal);
            }
            else if (app == 2)
            {
                IconBlock(8, 6, 47, 52, s, Pink); IconBlock(12, 10, 39, 44, s, Paper);
                IconBlock(25, 17, 14, 14, s, Teal); IconBlock(19, 35, 26, 13, s, Mint);
                IconBlock(5, 16, 9, 4, s, Ink); IconBlock(5, 44, 9, 4, s, Ink);
            }
            else if (app == 6)
            {
                IconBlock(8, 7, 48, 50, s, Line);
                for (int i = 0; i < 3; i++) { IconBlock(16, 18 + i * 13, 32, 3, s, Paper); IconBlock(20 + (i % 2) * 16, 14 + i * 13, 7, 11, s, Mint); }
            }
            else
            {
                IconBlock(9, 5, 46, 54, s, app == 1 ? Mint : Pink);
                IconBlock(13, 14, 38, 41, s, Paper);
                if (app == 1)
                {
                    for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++) IconBlock(19 + col * 10, 23 + row * 9, 6, 5, s, row == 1 && col == 1 ? Pink : Teal);
                }
                else
                {
                    for (int i = 0; i < 3; i++) { IconBlock(18, 24 + i * 10, 5, 5, s, Teal); IconBlock(28, 25 + i * 10, 16, 3, s, Line); }
                }
                IconBlock(22, 3, 20, 9, s, Teal);
            }
            GUI.EndGroup();
        }
        private static void IconBlock(float x, float y, float w, float h, float scale, Color c)
            => Fill(new Rect(x * scale, y * scale, w * scale, h * scale), c);

        private string PageHeading(int app)
        {
            switch (app)
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
                Fill(new Rect(cx, 0, card, 136), Color.white);
                Text(new Rect(cx + 18, 15, card - 36, 31), OfficeScenario.Names[i], heading, Ink);
                Text(new Rect(cx + 18, 53, card - 36, 76), PersonStatus(i), body, Muted);
                if (GUI.Button(new Rect(cx, 0, card, 136), GUIContent.none, GUIStyle.none) && CanInteract) RequestApp(2, i);
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
                GUI.enabled = CanInteract && !SaveBlocked && !Scenario.Complete;
                if (Action(new Rect(cx, 428, bw, 55), (Scenario.Choice(day) == i ? "선택됨  ·  " : "") + OfficeScenario.Options[day * 2 + i], Scenario.Choice(day) == i))
                { Scenario.Choose(i); Save(); Notify("선택을 저장했어요", "계획 확정으로 오늘을 마무리할 수 있어요.", 0); }
                GUI.enabled = CanInteract;
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
                Text(new Rect(120, y + 10, w - 140, 30), OfficeScenario.Titles[d], caption, Ink);
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
            if (Action(new Rect(0, 607, 255, 46), "오늘의 대응 결정 보기 →", true)) RequestApp(0);
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
            if (Action(new Rect(0, 787, 280, 48), "한 주의 활동 기록 읽기 →", true)) RequestApp(5);
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
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true, richText = false };
            heading = new GUIStyle(body) { fontSize = 21, fontStyle = FontStyle.Bold };
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
        private bool Action(Rect r, string value, bool selected = false, bool dark = false, bool modalButton = false)
        {
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && (CanInteract || modalButton);
            Color fill = selected ? Pink : Paper;
            if (r.Contains(Event.current.mousePosition) && GUI.enabled) fill = selected ? new Color(1f, .84f, .90f) : Mint;
            Frame(r, fill, GUI.enabled ? Line : new Color(.84f, .80f, .88f), false);
            Text(r, value, button, GUI.enabled ? Ink : Muted);
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            GUI.enabled = enabled;
            return clicked;
        }
    }
}
#endif
