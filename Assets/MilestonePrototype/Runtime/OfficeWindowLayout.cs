#if UNITY_WEBGL || UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    // Session-owned window layout. Campaign saves do not contain desktop presentation state.
    public sealed class OfficeWindowLayout
    {
        public sealed class Window
        {
            public bool Open, Minimized;
            public Rect Rect;
            public Vector2 Scroll;
            public int Person;
        }
        public const int AppCount = 7;
        public const float MinimumWidth = 640, MinimumHeight = 420;
        private readonly Window[] windows = new Window[AppCount];
        private readonly List<int> order = new List<int>();
        public OfficeWindowLayout()
        {
            for (int i = 0; i < AppCount; i++)
                windows[i] = new Window { Rect = new Rect(230 + i * 28, 76 + i * 25, 800, 600) };
        }
        public Window Get(int app) => app >= 0 && app < AppCount ? windows[app] : null;
        public int Front => order.Count == 0 ? -1 : order[order.Count - 1];
        public bool Show(int app)
        {
            Window window = Get(app);
            if (window == null) return false;
            window.Open = true;
            window.Minimized = false;
            Focus(app);
            return true;
        }
        public void Focus(int app)
        {
            Window window = Get(app);
            if (window == null || !window.Open || window.Minimized) return;
            order.Remove(app);
            order.Add(app);
        }
        public void Minimize(int app)
        {
            Window window = Get(app);
            if (window == null || !window.Open) return;
            window.Minimized = true;
            order.Remove(app);
        }
        public void Close(int app)
        {
            Window window = Get(app);
            if (window == null) return;
            window.Open = window.Minimized = false;
            order.Remove(app);
        }
        public int HitTest(Vector2 point)
        {
            for (int i = order.Count - 1; i >= 0; i--)
                if (windows[order[i]].Rect.Contains(point)) return order[i];
            return -1;
        }
        public static Rect Constrain(Rect rect, float width, float height)
        {
            float availableWidth = Mathf.Max(1, width - 16);
            float availableHeight = Mathf.Max(1, height - 100);
            rect.width = Mathf.Clamp(rect.width, Mathf.Min(MinimumWidth, availableWidth), availableWidth);
            rect.height = Mathf.Clamp(rect.height, Mathf.Min(MinimumHeight, availableHeight), availableHeight);
            rect.x = Mathf.Clamp(rect.x, 8, Mathf.Max(8, width - rect.width - 8));
            rect.y = Mathf.Clamp(rect.y, 38, Mathf.Max(38, height - 62 - rect.height));
            return rect;
        }
        public void ConstrainAll(float width, float height)
        {
            foreach (Window window in windows) window.Rect = Constrain(window.Rect, width, height);
        }
        public static bool BlocksDesktop(bool modal, bool overToast, int hitWindow)
            => modal || overToast || hitWindow >= 0;
    }
}
#endif
