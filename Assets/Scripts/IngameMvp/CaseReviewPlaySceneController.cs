using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProjectW.IngameCore.CaseReview;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectW.IngameMvp
{
    public sealed class CaseReviewPlaySceneController : MonoBehaviour
    {
        private const string IngameSceneName = "MVP Scene";
        private const string LegacyRuntimeFontName = "LegacyRuntime.ttf";
        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

        [SerializeField] private int seed = 1042;

        private CaseReviewSessionController _session;
        private Text _publicText;
        private Text _hiddenText;
        private Text _historyText;
        private ScrollRect _historyScroll;
        private InputField _input;
        private readonly List<string> _history = new List<string>();

        public CaseReviewSessionController Session => _session;
        public string ConsoleHistory => string.Join(Environment.NewLine, _history);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneController()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, IngameSceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (FindFirstObjectByType<CaseReviewPlaySceneController>() != null)
            {
                return;
            }

            var root = new GameObject(nameof(CaseReviewPlaySceneController));
            root.AddComponent<CaseReviewPlaySceneController>();
        }

        private void Awake()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, IngameSceneName, StringComparison.Ordinal))
            {
                enabled = false;
                return;
            }

            EnsureCamera();
            EnsureEventSystem();
            EnsureSession();
            BuildUi();
            SubmitCommand("status");
        }

        public void SubmitInput()
        {
            if (_input == null)
            {
                return;
            }

            var command = _input.text;
            _input.text = string.Empty;
            SubmitCommand(command);
            _input.ActivateInputField();
        }

        public void SubmitCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                RefreshPanels();
                return;
            }

            EnsureSession();
            var trimmed = command.Trim();
            var result = _session.DispatchCommand(trimmed);
            _history.Add("> " + trimmed);
            if (!string.IsNullOrWhiteSpace(result.Code))
            {
                _history.Add((result.Success ? "OK " : "FAIL ") + result.Code);
            }

            if (result.Lines.Count == 0)
            {
                _history.Add(result.Success ? "OK" : "FAIL");
            }
            else
            {
                _history.AddRange(result.Lines);
            }

            TrimHistory();
            RefreshPanels();
        }

        public void SubmitQuickCommand(string commandTemplate)
        {
            var command = ResolveCommandTemplate(commandTemplate);
            SubmitCommand(command);
        }

        private void EnsureSession()
        {
            _session = FindFirstObjectByType<CaseReviewSessionController>();
            if (_session == null)
            {
                _session = gameObject.AddComponent<CaseReviewSessionController>();
            }

            if (!_session.IsInitialized)
            {
                _session.Initialize(seed);
            }
        }

        private void BuildUi()
        {
            var oldCanvas = GameObject.Find("CaseReviewCanvas");
            if (oldCanvas != null)
            {
                Destroy(oldCanvas);
            }

            var canvasGo = new GameObject("CaseReviewCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            var root = CreateRect(canvasGo.transform, "CaseReviewRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.offsetMin = new Vector2(18f, 18f);
            root.offsetMax = new Vector2(-18f, -18f);

            var publicPanel = CreatePanel(root, "PublicPanel", new Vector2(0f, 0.38f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(-8f, -8f), "Public");
            var hiddenPanel = CreatePanel(root, "HiddenPanel", new Vector2(0.5f, 0.38f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(0f, -8f), "Hidden");
            var historyPanel = CreatePanel(root, "ConsoleHistoryPanel", new Vector2(0f, 0f), new Vector2(0.68f, 0.38f), new Vector2(0f, 0f), new Vector2(-8f, -8f), "Console History");
            var consolePanel = CreatePanel(root, "ConsolePanel", new Vector2(0.68f, 0f), new Vector2(1f, 0.38f), new Vector2(8f, 0f), Vector2.zero, "Console");

            _publicText = CreateScrollText(publicPanel, "PublicText", out _);
            _hiddenText = CreateScrollText(hiddenPanel, "HiddenText", out _);
            _historyText = CreateScrollText(historyPanel, "ConsoleHistoryText", out _historyScroll);
            BuildConsole(consolePanel);
        }

        private void BuildConsole(RectTransform parent)
        {
            _input = CreateInput(parent, "ConsoleInput", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -88f), new Vector2(-104f, -48f));
            SetTopStretch(_input.GetComponent<RectTransform>(), left: 12f, right: 104f, top: 48f, height: 40f);
            _input.onSubmit.AddListener(_ => SubmitInput());

            var submit = CreateButton(parent, "SubmitButton", "Submit", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-96f, -88f), new Vector2(-12f, -48f));
            SetTopRightFixed(submit.GetComponent<RectTransform>(), right: 12f, top: 48f, width: 84f, height: 40f);
            submit.onClick.AddListener(SubmitInput);

            var commands = new[]
            {
                "HELP", "STATUS", "PLAN", "QUEUE", "CONFIRM PLAN", "REPORT", "REVIEW ALL", "NEXT DAY",
                "OPEN {id}", "SUMMARY {id}", "LOG {id} summary", "LOG {id} equip", "CHECK {id}", "APPROVE {id}", "HOLD {id}"
            };

            const int columns = 3;
            const float left = 12f;
            const float top = -104f;
            const float width = 116f;
            const float height = 20f;
            const float gap = 6f;

            for (var i = 0; i < commands.Length; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var x = left + col * (width + gap);
                var y = top - row * (height + gap);
                var command = commands[i];
                var button = CreateButton(parent, "Quick_" + SanitizeName(command), command.Replace(" {id}", ""), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y - height), new Vector2(x + width, y));
                SetTopLeftFixed(button.GetComponent<RectTransform>(), x, -y, width, height);
                button.onClick.AddListener(() => SubmitQuickCommand(command));
            }
        }

        private void RefreshPanels()
        {
            var state = _session.State;
            _publicText.text = BuildPublicText(state);
            _hiddenText.text = BuildHiddenText(state);
            _historyText.text = ConsoleHistory;
            Canvas.ForceUpdateCanvases();
            if (_historyScroll != null)
            {
                _historyScroll.verticalNormalizedPosition = 0f;
            }
        }

        private static string BuildPublicText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Day {state.Day} | Slot {state.Slot} | Time {FormatClock(state.TimeRemainingSec)}");
            builder.AppendLine($"Queue {state.Queue.Count(e => e.Status != CaseStatus.Closed)}/{state.Queue.Count} | Overload {state.Overload} | Risk {state.GlobalLatentRisk}");
            builder.AppendLine($"Budgets Redirect {state.RedirectBudget}/{state.Config.RedirectBudgetPerDay} | Audit {state.AuditBudget}/{state.Config.AuditBudgetPerDay} | Interview {state.InterviewBudget}/{state.Config.InterviewBudgetPerDay}");
            builder.AppendLine();
            builder.AppendLine("Morning Plan");
            builder.AppendLine(state.MorningPlan.Confirmed ? "CONFIRMED" : "DRAFT");
            foreach (var entry in state.MorningPlan.Entries)
            {
                var item = state.Queue.FirstOrDefault(e => e.Id.Equals(entry.EventId, StringComparison.OrdinalIgnoreCase));
                builder.AppendLine($"{entry.EventId} | {item?.Title ?? "unknown"} | {string.Join(",", entry.PlannedPersonnel)} | {entry.Reason}");
            }

            builder.AppendLine();
            builder.AppendLine("Public Queue");
            foreach (var item in state.Queue.Where(e => e.Status != CaseStatus.Closed).OrderByDescending(e => e.Urgency + e.Severity))
            {
                builder.AppendLine($"{item.Id} | {item.Kind} | {item.Title}");
                builder.AppendLine($"  status {item.Status} | urgency {item.Urgency} | severity {item.Severity} | ttl {FormatClock(item.TtlSec)}");
                if (!string.IsNullOrWhiteSpace(item.ResultSummary))
                {
                    builder.AppendLine("  " + item.ResultSummary);
                }
            }

            return builder.ToString();
        }

        private static string BuildHiddenText(GameState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Debug Truth");
            builder.AppendLine($"Seed {state.Seed} | Rng {state.RngState} | Elapsed {state.TotalElapsedSec}s | Talent shortage {state.TalentShortage}");
            builder.AppendLine();
            builder.AppendLine("Cases");
            foreach (var item in state.Queue.OrderBy(e => e.Id))
            {
                builder.AppendLine($"{item.Id} | latent {item.LatentRisk} | mismatch {item.MismatchScore} | reviewed {item.ReportReviewed} | auto {item.AutoResolved}");
                builder.AppendLine($"  assigned [{string.Join(",", item.AssignedPersonnel)}] | outcome {item.OutcomeScore} | physical {item.PhysicalCost} | mental {item.MentalCost}");
            }

            builder.AppendLine();
            builder.AppendLine("TruthFrames");
            foreach (var frame in state.TruthFrames.OrderBy(f => f.Tick).ThenBy(f => f.Id))
            {
                builder.AppendLine($"{frame.Id} | {frame.EventId} | tick {frame.Tick} | {frame.ActorId} | {frame.ActionCode}");
                builder.AppendLine("  " + frame.FactBlob);
            }

            builder.AppendLine();
            builder.AppendLine("Staff");
            foreach (var staff in state.Staff.OrderBy(s => s.Id))
            {
                builder.AppendLine($"{staff.Id} {staff.Name} | load {staff.LoadAssigned}/{staff.MaxLoad} | energy {staff.PhysicalEnergy} | stress {staff.MentalStress} | fatigue {staff.Fatigue} | retention {staff.RetentionRisk} | left {staff.HasLeft}");
            }

            return builder.ToString();
        }

        private string ResolveCommandTemplate(string command)
        {
            if (command.IndexOf("{id}", StringComparison.Ordinal) < 0)
            {
                return command;
            }

            var selected = _session.State.OpenEventId;
            if (string.IsNullOrWhiteSpace(selected))
            {
                selected = _session.State.Queue.FirstOrDefault(e => e.Status != CaseStatus.Closed)?.Id ?? "E-108";
            }

            return command.Replace("{id}", selected);
        }

        private static RectTransform CreatePanel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string title)
        {
            var panel = CreateRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(0.09f, 0.1f, 0.11f, 0.96f);

            var titleText = CreateText(panel, name + "Title", title, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(12f, -34f);
            titleText.rectTransform.offsetMax = new Vector2(-12f, -6f);
            titleText.color = new Color(0.93f, 0.9f, 0.82f, 1f);
            return panel;
        }

        private static Text CreateScrollText(RectTransform parent, string name, out ScrollRect scroll)
        {
            var scrollGo = new GameObject(name + "Scroll");
            scrollGo.transform.SetParent(parent, false);
            var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(10f, 10f);
            scrollRectTransform.offsetMax = new Vector2(-10f, -42f);
            var image = scrollGo.AddComponent<Image>();
            image.color = new Color(0.04f, 0.045f, 0.05f, 0.7f);
            scroll = scrollGo.AddComponent<ScrollRect>();

            var viewport = CreateRect(scrollRectTransform, name + "Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect(viewport, name + "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(content.sizeDelta.x, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 8, 8);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = CreateText(content, name, string.Empty, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            return text;
        }

        private static InputField CreateInput(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.02f, 0.025f, 0.03f, 1f);
            var input = rect.gameObject.AddComponent<InputField>();

            var text = CreateText(rect, name + "Text", string.Empty, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8f, 2f);
            text.rectTransform.offsetMax = new Vector2(-8f, -2f);

            var placeholder = CreateText(rect, name + "Placeholder", "type command", 15, FontStyle.Italic, TextAnchor.MiddleLeft);
            placeholder.color = new Color(0.55f, 0.58f, 0.6f, 1f);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(8f, 2f);
            placeholder.rectTransform.offsetMax = new Vector2(-8f, -2f);

            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.25f, 0.28f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(rect, name + "Label", label, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(4f, 2f);
            text.rectTransform.offsetMax = new Vector2(-4f, -2f);
            return button;
        }

        private static Text CreateText(RectTransform parent, string name, string value, int fontSize, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = new Color(0.88f, 0.9f, 0.9f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static void SetTopLeftFixed(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        }

        private static void SetTopRightFixed(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        }

        private static void SetTopStretch(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - Mathf.Abs(height));
            rect.offsetMax = new Vector2(-right, -top);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Abs(height));
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.06f, 0.065f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
        }

        private static Font ResolveFont()
        {
            return Resources.GetBuiltinResource<Font>(LegacyRuntimeFontName);
        }

        private static string FormatClock(int seconds)
        {
            var clamped = Math.Max(0, seconds);
            return $"{clamped / 60:00}:{clamped % 60:00}";
        }

        private static string SanitizeName(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return builder.ToString().Trim('_');
        }

        private void TrimHistory()
        {
            const int maxLines = 220;
            if (_history.Count <= maxLines)
            {
                return;
            }

            _history.RemoveRange(0, _history.Count - maxLines);
        }
    }
}
