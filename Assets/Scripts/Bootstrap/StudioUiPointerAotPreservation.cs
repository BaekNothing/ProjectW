using UnityEngine.Scripting;
using UnityEngine.InputSystem;
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
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            _ = keyboard.escapeKey.isPressed;
            _ = keyboard.anyKey.isPressed;
        }

        private static void OnPointerDown(PointerDownEvent pointerEvent)
        {
        }

        private static void OnPointerUp(PointerUpEvent pointerEvent)
        {
        }
    }
}
