using System;
using System.Collections.Generic;
using System.IO;
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
        private string logFilePath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
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

            var fileName = "runtime-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            logFilePath = Path.Combine(Application.persistentDataPath, fileName);

            AppendLineToFile("==== RuntimeErrorConsole started at " + DateTime.Now.ToString("O") + " ====");
            AppendLineToFile("Application.persistentDataPath: " + Application.persistentDataPath);

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

            GUI.Box(new Rect(margin, margin, width, height), "Runtime Error Console");

            GUI.Label(
                new Rect(margin + 8, margin + 22, width - 16, 22),
                "` / F1: Toggle | C: Clear | Logs: " + entries.Count + " | File: " + logFilePath);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.C)
            {
                entries.Clear();
            }

            var viewRect = new Rect(0, 0, width - 36, Mathf.Max(height - 60, entries.Count * 48));
            var scrollRect = new Rect(margin + 8, margin + 44, width - 16, height - 52);

            scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);

            var y = 0f;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                GUI.contentColor = GetColor(entry.Type);
                GUI.Label(new Rect(0, y, viewRect.width, 44), entry.ToDisplayString());
                y += 48f;
            }

            GUI.contentColor = Color.white;
            GUI.EndScrollView();
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
            if (entries.Count > MaxEntries)
            {
                entries.RemoveAt(0);
            }

            AppendLineToFile(entry.ToFileString());
        }

        private void AppendLineToFile(string line)
        {
            File.AppendAllText(logFilePath, line + Environment.NewLine, Encoding.UTF8);
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

            public string ToFileString()
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
