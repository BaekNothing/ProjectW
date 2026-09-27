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
        private static string[] Pages => new[] { SheetContent.T("office.ui.001"), SheetContent.T("office.ui.002"), SheetContent.T("office.ui.003"), SheetContent.T("office.ui.004"), SheetContent.T("office.ui.005"), SheetContent.T("office.ui.006"), SheetContent.T("office.ui.007") };
        private static string[] Executables => new[] { SheetContent.T("office.executable.today"), SheetContent.T("office.executable.calendar"), SheetContent.T("office.executable.friends"), SheetContent.T("office.executable.town"), SheetContent.T("office.executable.letter"), SheetContent.T("office.executable.diary"), SheetContent.T("office.executable.settings") };
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
                Notify(SaveBlocked ? SheetContent.T("office.ui.008") : SheetContent.T("office.ui.009"), SaveBlocked ? SheetContent.T("office.ui.010") : SheetContent.T("office.ui.011"), SaveBlocked ? 6 : 0);
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
            Text(new Rect(20, 5, 600, 23), SheetContent.T("office.ui.012"), caption, Ink);
            Text(new Rect(width - 270, 5, 250, 23), SheetContent.Format("office.clock.header", "day", Mathf.Min(Scenario.Day, 7).ToString("00")), caption, Teal);
            // Quiet wallpaper motif leaves room for overlapping applications.
            IconEffect(new Rect(width * .57f, height * .24f, 240, 240), Mint, false);
            Text(new Rect(width * .52f, height * .38f, 430, 63), SheetContent.T("office.wallpaper.magic"), large, new Color(.71f, .61f, .83f));
            Text(new Rect(width * .52f + 50, height * .38f + 49, 420, 63), SheetContent.T("office.wallpaper.everyday"), large, new Color(.71f, .61f, .83f));
            Text(new Rect(width - 456, height - 134, 410, 42), SheetContent.T("office.ui.013"), heading, Teal);
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
            if (Action(new Rect(8, height - 46, 104, 38), SheetContent.T("office.ui.014"))) RequestApp(6);
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
            Text(new Rect(width - 160, height - 40, 150, 30), SheetContent.Format("office.clock.taskbar", "day", Mathf.Min(Scenario.Day, 7).ToString("00")), caption, Ink);
        }
        private void DrawAppWindow(int id)
        {
            int app = id - 930000;
            var window = Layout.Get(app);
            float ww = window.Rect.width, wh = window.Rect.height;
            Frame(new Rect(0, 0, ww, wh), Paper, Ink, true);
            Fill(new Rect(3, 3, ww - 6, 35), Layout.Front == app ? Pink : new Color(.85f, .80f, .92f));
            DrawIcon(new Rect(10, 9, 22, 22), app);
            Text(new Rect(42, 8, ww - 165, 29), SheetContent.Format("office.window.title", "page", Pages[app], "executable", Executables[app]), caption, Ink);
            if (Action(new Rect(ww - 76, 7, 30, 27), "_")) Layout.Minimize(app);
            if (Action(new Rect(ww - 39, 7, 30, 27), "×")) Layout.Close(app);
            Text(new Rect(17, 47, ww - 34, 33), app == 6 ? SheetContent.T("office.ui.015") : PageHeading(app), caption, Muted);
            Fill(new Rect(12, 84, ww - 24, 1), Line);
            int previousPage = page, previousPerson = person;
            Vector2 previousScroll = scroll;
            page = app; person = window.Person; scroll = window.Scroll;
            float contentHeight = app == 6 ? 770 : app == 1 ? 710 : app == 4 || app == 5 || (Scenario.Complete && app == 0) ? 1100 : 690;
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
            Text(new Rect(20, wh - 42, ww - 295, 33), SaveBlocked ? SheetContent.T("office.ui.016") : SheetContent.T("office.ui.017"), caption, Muted);
            if (app == 0 && !Scenario.Complete)
            {
                GUI.enabled = CanInteract && Scenario.CanAdvance && !SaveBlocked;
                if (Action(new Rect(ww - 258, wh - 47, 223, 34), SheetContent.T("office.ui.018"), true)) modal = Modal.ConfirmDay;
                GUI.enabled = CanInteract;
            }
            for (int i = 0; i < 3; i++) Fill(new Rect(ww - 8 - i * 5, wh - 11 - i * 5, 2, 3 + i * 5), Ink);
            // Native IMGUI owns window dragging and pointer capture.
            if (CanInteract) GUI.DragWindow(new Rect(0, 0, ww - 85, 37));
        }
        private void DrawSettings(float w)
        {
            Text(new Rect(8, 632, w - 16, 30), SheetContent.T("settings.language.label"), caption, Ink);
            string[] modes = { "ko", "en", "tid" };
            for (int i = 0; i < modes.Length; i++)
                if (Action(new Rect(8 + i * ((w - 28) / 3 + 6), 670, (w - 28) / 3, 48), SheetContent.T("settings.language." + modes[i]), SheetContent.Locale == modes[i]))
                    SheetContent.SetLocale(modes[i]);

            Text(new Rect(8, 5, w - 16, 46), SheetContent.T("office.ui.019"), heading, Ink);
            Text(new Rect(8, 64, w - 16, 104), SheetContent.T("office.ui.020"), body, Muted);
            if (Action(new Rect(8, 175, w - 16, 48), effects ? SheetContent.T("office.ui.021") : SheetContent.T("office.ui.022"), effects)) effects = !effects;
            Text(new Rect(8, 237, w - 16, 67), SheetContent.T("office.ui.023"), body, Muted);
            if (Action(new Rect(8, 325, (w - 28) / 2, 49), SheetContent.T("office.ui.024"))) modal = Modal.Preview;
            if (Action(new Rect(20 + (w - 28) / 2, 325, (w - 28) / 2, 49), SheetContent.T("office.ui.025"))) Notify(SheetContent.T("office.ui.026"), SheetContent.T("office.ui.027"), 4);
            Text(new Rect(8, 400, w - 16, 80), SaveBlocked ? SheetContent.T("office.ui.028") : SheetContent.T("office.ui.029"), body, Muted);
            if (Action(new Rect(8, 503, w - 16, 48), SheetContent.T("office.ui.030"))) modal = Modal.Reset;
            if (Action(new Rect(8, 568, w - 16, 48), SheetContent.T("office.ui.031"))) leave = true;
        }
        private void DrawToast()
        {
            Frame(new Rect(0, 0, 354, 140), Paper, Ink, true);
            Fill(new Rect(3, 3, 348, 29), Mint);
            Text(new Rect(12, 5, 290, 27), SheetContent.Format("office.toast.title", "title", toastTitle), caption, Ink);
            if (Action(new Rect(318, 5, 28, 24), "×")) toastUntil = 0;
            Text(new Rect(16, 43, 322, 52), toastMessage, body, Ink);
            if (Action(new Rect(16, 100, 322, 30), SheetContent.Format("office.toast.open", "page", Pages[toastApp]))) { RequestApp(toastApp); toastUntil = 0; }
        }
        private void DrawModal()
        {
            Fill(new Rect(0, 0, width, height), new Color(.20f, .13f, .32f, .82f));
            float mw = 704, mh = 514;
            Rect panel = new Rect((width - mw) / 2, (height - mh) / 2, mw, mh);
            GUI.BeginGroup(panel);
            Frame(new Rect(0, 0, mw, mh), Paper, Ink, true);
            Fill(new Rect(3, 3, mw - 6, 35), Pink);
            Text(new Rect(16, 8, mw - 32, 28), SheetContent.T("office.ui.033"), caption, Ink);
            IconEffect(new Rect(mw / 2 - 80, 43, 160, 160), Pink, true);
            DrawIcon(new Rect(mw / 2 - 36, 91, 72, 72), modal == Modal.Reset ? 6 : modal == Modal.Preview ? 4 : 0);
            string title = modal == Modal.Reset ? SheetContent.T("office.ui.034") : modal == Modal.ConfirmDay ? SheetContent.T("office.ui.035") : modal == Modal.WeekReport ? SheetContent.T("office.ui.036") : modal == Modal.Preview ? SheetContent.T("office.ui.037") : SheetContent.T("office.ui.038");
            Text(new Rect(36, 203, mw - 72, 46), title, heading, Ink);
            string message = modal == Modal.Reset ? SheetContent.T("office.ui.039") : modal == Modal.ConfirmDay ? SheetContent.Format("office.confirm_day", "choice", OfficeScenario.Options[Scenario.CurrentIndex * 2 + Scenario.Choice(Scenario.CurrentIndex)]) : modal == Modal.Preview ? SheetContent.T("office.ui.041") : report;
            Text(new Rect(36, 273, mw - 72, 126), message, body, Muted);
            bool confirm = modal == Modal.Reset || modal == Modal.ConfirmDay;
            if (confirm && Action(new Rect(36, 435, 300, 48), SheetContent.T("office.ui.042"), false, false, true)) modal = Modal.None;
            if (Action(new Rect(confirm ? 354 : 202, 435, 314, 48), confirm ? SheetContent.T("office.ui.043") : SheetContent.T("office.ui.044"), true, false, true))
            {
                if (modal == Modal.Reset)
                {
                    PlayerPrefs.DeleteKey(OfficeScenario.SaveKey); PlayerPrefs.Save();
                    Scenario.Restore(new OfficeScenario().Export()); SaveBlocked = false;
                    for (int i = 0; i < Pages.Length; i++) { Layout.Close(i); Layout.Get(i).Scroll = Vector2.zero; }
                    unreadDay = 0; modal = Modal.None;
                    Notify(SheetContent.T("office.ui.045"), SheetContent.T("office.ui.046"), 0);
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
                    if (!preview) Notify(Scenario.Complete ? SheetContent.T("office.ui.047") : SheetContent.T("office.ui.048"), Scenario.Complete ? SheetContent.T("office.ui.049") : SheetContent.T("office.ui.050"), Scenario.Complete ? 0 : 4);
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
                case 1: return SheetContent.T("office.ui.051");
                case 2: return SheetContent.T("office.ui.052");
                case 3: return SheetContent.T("office.ui.053");
                case 4: return SheetContent.T("office.ui.054");
                case 5: return SheetContent.T("office.ui.055");
                default: return SheetContent.T("office.ui.056");
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
            Text(new Rect(0, 151, w, 26), SheetContent.Format("office.agenda.deadline", "day", Scenario.Day.ToString("00")), caption, Teal);
            Fill(new Rect(0, 187, w, 222), Color.white);
            Fill(new Rect(0, 187, 5, 222), Teal);
            Text(new Rect(25, 205, w - 50, 50), OfficeScenario.Titles[day], heading, Ink);
            Text(new Rect(25, 262, w - 50, 27), SheetContent.Format("office.agenda.sender", "sender", OfficeScenario.Senders[day]), caption, Muted);
            Text(new Rect(25, 304, w - 50, 83), OfficeScenario.Bodies[day], body, Ink);
            for (int i = 0; i < 2; i++)
            {
                float cx = i * ((w - 14) / 2 + 14), bw = (w - 14) / 2;
                GUI.enabled = CanInteract && !SaveBlocked && !Scenario.Complete;
                if (Action(new Rect(cx, 428, bw, 55), (Scenario.Choice(day) == i ? SheetContent.Format("office.choice.selected", "choice", OfficeScenario.Options[day * 2 + i]) : OfficeScenario.Options[day * 2 + i]), Scenario.Choice(day) == i))
                { Scenario.Choose(i); Save(); Notify(SheetContent.T("office.ui.061"), SheetContent.T("office.ui.062"), 0); }
                GUI.enabled = CanInteract;
                Text(new Rect(cx + 8, 497, bw - 16, 68), OfficeScenario.Reasons[day * 2 + i], body, Muted);
            }
            if (Scenario.CanAdvance)
            {
                Fill(new Rect(0, 571, w, 89), new Color(.86f, .92f, .86f));
                Text(new Rect(18, 582, w - 36, 71), SheetContent.Format("office.report.preview", "report", Scenario.Report(day)), body, Teal);
            }
        }
        private string PersonStatus(int i)
        {
            if (i == 0) return Scenario.Day > 1 ? SheetContent.T("office.ui.064") : SheetContent.T("office.ui.065");
            if (i == 1) return Scenario.Day > 2 ? (Scenario.Choice(1) == 0 ? SheetContent.T("office.ui.066") : SheetContent.T("office.ui.067")) : SheetContent.T("office.ui.068");
            return Scenario.Day > 5 ? (Scenario.Choice(4) == 0 ? SheetContent.T("office.ui.069") : SheetContent.T("office.ui.070")) : SheetContent.T("office.ui.071");
        }
        private void DrawPeople(float w)
        {
            for (int i = 0; i < 3; i++)
                if (Action(new Rect(i * 170, 0, 156, 45), OfficeScenario.Names[i], person == i)) person = i;
            Fill(new Rect(0, 70, w, 470), Color.white);
            Text(new Rect(26, 94, w - 52, 55), SheetContent.Format("office.person.heading", "name", OfficeScenario.Names[person], "role", OfficeScenario.Roles[person]), heading, Ink);
            Text(new Rect(26, 160, w - 52, 40), SheetContent.Format("office.person.status_day", "day", Mathf.Min(Scenario.Day, 7).ToString("00")), caption, Teal);
            Text(new Rect(26, 205, w - 52, 50), PersonStatus(person), body, Ink);
            int source = person == 0 ? 0 : person == 1 ? 1 : 4;
            Text(new Rect(26, 276, w - 52, 35), SheetContent.Format("office.person.message_day", "day", (source + 1).ToString("00")), caption, Teal);
            Text(new Rect(26, 321, w - 52, 115), Scenario.Day >= source + 1 ? OfficeScenario.Bodies[source] : SheetContent.T("office.ui.074"), body, Ink);
            Text(new Rect(26, 451, w - 52, 61), SheetContent.Format("office.person.plan", "role", OfficeScenario.Roles[person]), body, Muted);
        }
        private void DrawSchedule(float w)
        {
            Text(new Rect(0, 0, w, 45), SheetContent.T("office.ui.077"), heading, Ink);
            for (int d = 0; d < 7; d++)
            {
                float y = 62 + d * 88;
                Fill(new Rect(0, y, w, 77), d == Scenario.CurrentIndex ? new Color(.85f, .91f, .86f) : Color.white);
                Text(new Rect(18, y + 16, 90, 40), SheetContent.Format("office.day", "day", (d + 1).ToString("00")), heading, Teal);
                Text(new Rect(120, y + 10, w - 140, 30), OfficeScenario.Titles[d], caption, Ink);
                string plan = d > Scenario.CurrentIndex ? SheetContent.T("office.ui.078") : Scenario.Choice(d) < 0 ? SheetContent.T("office.ui.079") : OfficeScenario.Options[d * 2 + Scenario.Choice(d)];
                Text(new Rect(120, y + 43, w - 140, 27), plan, caption, Muted);
            }
        }
        private void DrawDistrict(float w)
        {
            string[] sites = { SheetContent.T("office.ui.080"), SheetContent.T("office.ui.081"), SheetContent.T("office.ui.082") };
            string[] notes = { SheetContent.T("office.ui.083"), Scenario.Day > 4 ? SheetContent.T("office.ui.084") : SheetContent.T("office.ui.085"), SheetContent.T("office.ui.086") };
            for (int i = 0; i < 3; i++)
            {
                Fill(new Rect(0, i * 113, w, 98), Color.white);
                Text(new Rect(22, i * 113 + 14, 250, 36), sites[i], heading, Teal);
                Text(new Rect(285, i * 113 + 20, w - 310, 65), notes[i], body, Ink);
            }
            Text(new Rect(0, 360, w, 42), SheetContent.T("office.ui.087"), heading, Ink);
            Text(new Rect(0, 421, w, 180), SheetContent.Format("office.district.report", "day", Mathf.Min(Scenario.Day, 7).ToString("00"), "report",
                Scenario.Day > 4 ? Scenario.Report(3) : SheetContent.T("office.ui.089")), body, Muted);
            if (Action(new Rect(0, 607, 255, 46), SheetContent.T("office.ui.091"), true)) RequestApp(0);
        }
        private void DrawJournal(float w, bool messages)
        {
            Text(new Rect(0, 0, w, 40), messages ? SheetContent.T("office.ui.092") : SheetContent.T("office.ui.093"), heading, Ink);
            float y = 60;
            for (int d = Mathf.Min(Scenario.Day - 1, 6); d >= 0; d--)
            {
                bool committed = d < Scenario.Day - 1;
                if (!messages && !committed) continue;
                Fill(new Rect(0, y, w, 130), Color.white);
                Text(new Rect(18, y + 13, w - 36, 26), SheetContent.Format("office.journal.heading", "day", (d + 1).ToString("00"), "title", messages ? OfficeScenario.Senders[d] : OfficeScenario.Titles[d]), caption, Teal);
                Text(new Rect(18, y + 51, w - 36, 70), committed ? Scenario.Report(d) : OfficeScenario.Bodies[d], body, Ink);
                y += 142;
            }
            if (y == 60) Text(new Rect(0, y, w, 60), SheetContent.T("office.ui.094"), body, Muted);
        }
        private void DrawEnding(float w)
        {
            Fill(new Rect(0, 0, w, 132), Teal);
            Text(new Rect(24, 20, w - 48, 40), SheetContent.T("office.ui.095"), heading, Color.white);
            Text(new Rect(24, 74, w - 48, 42), SheetContent.T("office.ui.096"), body, Color.white);
            for (int i = 0; i < 3; i++)
            {
                float y = 154 + i * 160;
                Fill(new Rect(0, y, w, 140), Color.white);
                Text(new Rect(20, y + 13, w - 40, 39), SheetContent.Format("office.ending.person", "name", OfficeScenario.Names[i], "status", PersonStatus(i)), heading, Ink);
                Text(new Rect(20, y + 62, w - 40, 68), Scenario.Report(i == 0 ? 0 : i == 1 ? 1 : 4), body, Muted);
            }
            Text(new Rect(0, 657, w, 100), SheetContent.Format("office.ending.report", "report", Scenario.Report(6)), body, Teal);
            if (Action(new Rect(0, 787, 280, 48), SheetContent.T("office.ui.098"), true)) RequestApp(5);
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
