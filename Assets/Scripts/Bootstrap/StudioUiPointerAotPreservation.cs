using UnityEngine.Scripting;
using UnityEngine.UIElements;

namespace ProjectW.Bootstrap
{
    [Preserve]
    public static class StudioUiPointerAotPreservation
    {
        [Preserve]
        public static void Preserve(VisualElement element)
        {
            if (element == null) return;
            element.RegisterCallback<PointerDownEvent>(OnPointerDown);
            element.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private static void OnPointerDown(PointerDownEvent pointerEvent)
        {
        }

        private static void OnPointerUp(PointerUpEvent pointerEvent)
        {
        }
    }
}
