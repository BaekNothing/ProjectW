using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    /// <summary>
    /// Captures runtime logs/exceptions and renders a lightweight in-game console overlay.
    /// Toggle with BackQuote(`) or F1.
    /// </summary>
    public sealed class RuntimeErrorConsole : MonoBehaviour
    {
        private const int MaxEntries = 300;

        private static RuntimeErrorConsole instance;
        private static readonly Queue<LogEntry> pendingThreadedEntries = new Queue<LogEntry>();
        private static readonly object queueLock = new object();

        private readonly List<LogEntry> entries = new List<LogEntry>(MaxEntries);

        private Vector2 scrollPosition;
        private bool visible;
        private string copyNotice;
        private float copyNoticeUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("[RuntimeErrorConsole]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RuntimeErrorConsole>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            Application.logMessageReceived += HandleLog;
            Application.logMessageReceivedThreaded += HandleLogThreaded;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            Application.logMessageReceivedThreaded -= HandleLogThreaded;
            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;

            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1))
            {
                visible = !visible;
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                entries.Clear();
                ShowCopyNotice("Logs cleared");
            }

            FlushThreadedEntries();
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            const int margin = 12;
            var width = Screen.width - (margin * 2);
            var height = Mathf.FloorToInt(Screen.height * 0.45f);
            var panelRect = new Rect(margin, margin, width, height);

            GUI.Box(panelRect, "Runtime Error Console");
            GUI.Label(
                new Rect(margin + 8, margin + 22, width - 16, 22),
                "` / F1: Toggle | C: Clear | Tap/Click panel: Copy latest critical | Logs: " + entries.Count);

            if (GUI.Button(new Rect(margin + 8, margin + 44, 180, 22), "Copy all critical logs"))
            {
                CopyAllCriticalLogs();
            }

            if (IsPanelClick(panelRect))
            {
                CopyLatestCriticalLog();
            }

            if (Time.unscaledTime < copyNoticeUntil)
            {
                GUI.Label(new Rect(margin + 200, margin + 44, width - 208, 22), copyNotice);
            }

            var viewRect = new Rect(0, 0, width - 36, Mathf.Max(height - 80, entries.Count * 48));
            var scrollRect = new Rect(margin + 8, margin + 68, width - 16, height - 76);

            scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);

            var y = 0f;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                GUI.contentColor = GetColor(entry.Type);
                if (GUI.Button(new Rect(0, y, viewRect.width, 44), entry.ToDisplayString()))
                {
                    GUI.contentColor = Color.white;
                    CopyEntry(entry);
                }

                y += 48f;
            }

            GUI.contentColor = Color.white;
            GUI.EndScrollView();
        }

        private static bool IsPanelClick(Rect panelRect)
        {
            if (Input.touchCount > 0)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    var touchPosition = new Vector2(touch.position.x, Screen.height - touch.position.y);
                    if (touch.phase == TouchPhase.Began && panelRect.Contains(touchPosition))
                    {
                        return true;
                    }
                }
            }

            if (Event.current.type == EventType.MouseDown && panelRect.Contains(Event.current.mousePosition))
            {
                return true;
            }

            return false;
        }

        private void FlushThreadedEntries()
        {
            lock (queueLock)
            {
                while (pendingThreadedEntries.Count > 0)
                {
                    AddEntry(pendingThreadedEntries.Dequeue());
                }
            }
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            AddEntry(new LogEntry(DateTime.Now, type, condition, stackTrace));
        }

        private void HandleLogThreaded(string condition, string stackTrace, LogType type)
        {
            lock (queueLock)
            {
                pendingThreadedEntries.Enqueue(new LogEntry(DateTime.Now, type, condition, stackTrace));
            }
        }

        private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var exceptionText = args.ExceptionObject != null
                ? args.ExceptionObject.ToString()
                : "Unhandled exception without details";

            lock (queueLock)
            {
                pendingThreadedEntries.Enqueue(new LogEntry(DateTime.Now, LogType.Exception, exceptionText, string.Empty));
            }
        }

        private void AddEntry(LogEntry entry)
        {
            entries.Add(entry);

            if (IsCritical(entry.Type))
            {
                visible = true;
            }

            if (entries.Count > MaxEntries)
            {
                entries.RemoveAt(0);
            }
        }

        private void CopyLatestCriticalLog()
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (!IsCritical(entries[i].Type))
                {
                    continue;
                }

                CopyEntry(entries[i]);
                return;
            }

            ShowCopyNotice("No critical log to copy");
        }

        private void CopyAllCriticalLogs()
        {
            var copied = 0;
            var builder = new StringBuilder();
            for (var i = 0; i < entries.Count; i++)
            {
                if (!IsCritical(entries[i].Type))
                {
                    continue;
                }

                if (copied > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("--------------------------------");
                }

                builder.Append(entries[i].ToCopyString());
                copied++;
            }

            if (copied == 0)
            {
                ShowCopyNotice("No critical logs to copy");
                return;
            }

            GUIUtility.systemCopyBuffer = builder.ToString();
            ShowCopyNotice("Copied " + copied + " critical logs");
        }

        private void CopyEntry(LogEntry entry)
        {
            GUIUtility.systemCopyBuffer = entry.ToCopyString();
            ShowCopyNotice("Copied latest critical log");
        }

        private void ShowCopyNotice(string message)
        {
            copyNotice = message;
            copyNoticeUntil = Time.unscaledTime + 1.8f;
        }

        private static bool IsCritical(LogType type)
        {
            return type == LogType.Error || type == LogType.Assert || type == LogType.Exception;
        }

        private static Color GetColor(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Assert:
                    return new Color(1f, 0.55f, 0.55f);
                case LogType.Warning:
                    return new Color(1f, 0.9f, 0.45f);
                case LogType.Exception:
                    return new Color(1f, 0.35f, 0.35f);
                default:
                    return new Color(0.85f, 0.95f, 1f);
            }
        }

        private readonly struct LogEntry
        {
            public LogEntry(DateTime timestamp, LogType type, string condition, string stackTrace)
            {
                Timestamp = timestamp;
                Type = type;
                Condition = condition ?? string.Empty;
                StackTrace = stackTrace ?? string.Empty;
            }

            public DateTime Timestamp { get; }
            public LogType Type { get; }
            public string Condition { get; }
            public string StackTrace { get; }

            public string ToDisplayString()
            {
                return "[" + Timestamp.ToString("HH:mm:ss") + "] [" + Type + "] " + Condition;
            }

            public string ToCopyString()
            {
                if (string.IsNullOrEmpty(StackTrace))
                {
                    return "[" + Timestamp.ToString("O") + "] [" + Type + "] " + Condition;
                }

                return "[" + Timestamp.ToString("O") + "] [" + Type + "] " + Condition + Environment.NewLine + StackTrace;
            }
        }
    }
}
