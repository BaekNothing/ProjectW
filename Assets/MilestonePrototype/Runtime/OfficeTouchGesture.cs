#if UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public sealed class OfficeTouchGesture
    {
        private enum Mode { Idle, PendingScroll, Scroll, Pinch, WaitForRelease, IgnoreUntilRelease }
        private Mode mode;
        private int app = -1, firstId, secondId;
        private Vector2 origin, scrollOrigin, pinchCenter;
        private Rect pinchRect;
        private float pinchDistance;
        public bool SuppressPointer { get; private set; }
        public void Process(OfficeWindowLayout layout, int count, int id1, int id2,
            Vector2 first, Vector2 second, float scale, float width, float height, bool blocked)
        {
            bool claimed = mode == Mode.Scroll || mode == Mode.Pinch || mode == Mode.WaitForRelease;
            if (count == 0)
            {
                SuppressPointer = claimed; // Swallow the synthetic release in this frame.
                mode = Mode.Idle; app = -1;
                return;
            }
            SuppressPointer = claimed;
            if (blocked)
            {
                if (mode == Mode.Idle) mode = Mode.IgnoreUntilRelease;
                else if (mode != Mode.IgnoreUntilRelease) { mode = Mode.WaitForRelease; SuppressPointer = true; }
                return;
            }
            if (mode == Mode.IgnoreUntilRelease) return;
            if (mode == Mode.WaitForRelease) return;
            if (count > 2) { mode = Mode.WaitForRelease; SuppressPointer = true; return; }
            if (count == 2)
            {
                SuppressPointer = true;
                if (mode != Mode.Pinch)
                {
                    int target = layout.HitTest(first);
                    if (target < 0 || layout.HitTest(second) != target || (app >= 0 && app != target))
                    { mode = Mode.WaitForRelease; return; }
                    app = target; firstId = id1; secondId = id2;
                    pinchRect = layout.Get(app).Rect;
                    pinchCenter = (first + second) * .5f;
                    pinchDistance = Mathf.Max(1, Vector2.Distance(first, second));
                    mode = Mode.Pinch; layout.Focus(app);
                }
                else if (!((id1 == firstId && id2 == secondId) || (id1 == secondId && id2 == firstId)))
                { mode = Mode.WaitForRelease; return; }
                layout.Get(app).Rect = Pinch(pinchRect, pinchCenter, (first + second) * .5f,
                    Vector2.Distance(first, second) / pinchDistance, width, height);
                ClampScroll(layout.Get(app));
                return;
            }
            if (mode == Mode.Pinch) { mode = Mode.WaitForRelease; SuppressPointer = true; return; }
            if (mode == Mode.Idle)
            {
                app = layout.HitTest(first);
                firstId = id1; origin = first;
                if (app < 0 || !ContentRect(layout.Get(app).Rect).Contains(first))
                { app = -1; mode = Mode.PendingScroll; return; }
                scrollOrigin = layout.Get(app).Scroll;
                mode = Mode.PendingScroll;
            }
            if (id1 != firstId) { mode = Mode.WaitForRelease; SuppressPointer = true; return; }
            if (app < 0) return; // A titlebar gesture stays with native window dragging.
            if (mode == Mode.PendingScroll && Vector2.Distance(first, origin) * scale >= 8)
            { mode = Mode.Scroll; layout.Focus(app); }
            if (mode != Mode.Scroll) return;
            SuppressPointer = true;
            var window = layout.Get(app);
            window.Scroll = new Vector2(0, scrollOrigin.y + origin.y - first.y);
            ClampScroll(window);
        }
        public static Rect ContentRect(Rect window) => new Rect(window.x + 14, window.y + 97, window.width - 28, window.height - 165);
        public static void ClampScroll(OfficeWindowLayout.Window window)
            => window.Scroll = new Vector2(0, Mathf.Clamp(window.Scroll.y, 0, Mathf.Max(0, window.ContentHeight - ContentRect(window.Rect).height)));
        public static Rect Pinch(Rect initial, Vector2 anchor, Vector2 current, float ratio, float width, float height)
        {
            Rect sized = OfficeWindowLayout.Constrain(new Rect(initial.position, initial.size * Mathf.Max(.01f, ratio)), width, height);
            sized.position = current + new Vector2((initial.x - anchor.x) * sized.width / initial.width,
                (initial.y - anchor.y) * sized.height / initial.height);
            return OfficeWindowLayout.Constrain(sized, width, height);
        }
    }
}
#endif
