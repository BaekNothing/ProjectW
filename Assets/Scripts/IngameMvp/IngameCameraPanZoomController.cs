using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ProjectW.IngameMvp
{
    public sealed class IngameCameraPanZoomController : MonoBehaviour
    {
        private const string IngameSceneName = "MVP Scene";

        [SerializeField] private Camera targetCamera;
        [SerializeField] private float minOrthoSize = 2.5f;
        [SerializeField] private float maxOrthoSize = 16f;
        [SerializeField] private float wheelZoomSpeed = 5.00f;
        [SerializeField] private float pinchZoomSpeed = 0.02f;

        private bool _isMouseDragging;
        private Vector2 _lastMouseScreenPosition;

        private bool _isTouchDragging;
        private int _activeTouchId = -1;
        private Vector2 _lastTouchScreenPosition;

        private bool _isPinching;
        private float _pinchStartDistance;
        private float _pinchStartSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAttached()
        {
            var scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, IngameSceneName, StringComparison.Ordinal))
            {
                return;
            }

            var existing = FindFirstObjectByType<IngameCameraPanZoomController>();
            if (existing != null)
            {
                return;
            }

            var cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                return;
            }

            cam.gameObject.AddComponent<IngameCameraPanZoomController>();
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (targetCamera == null)
            {
                return;
            }

            HandleMouseControls();
            HandleTouchControls();
        }

        private void HandleMouseControls()
        {
            if (TryGetMouseDown(out var mouseDownPos))
            {
                if (!IsPointerOverUi())
                {
                    _isMouseDragging = true;
                    _lastMouseScreenPosition = mouseDownPos;
                }
            }

            if (_isMouseDragging && TryGetMouseHeld(out var mouseHeldPos))
            {
                PanByScreenDelta(_lastMouseScreenPosition, mouseHeldPos);
                _lastMouseScreenPosition = mouseHeldPos;
            }

            if (_isMouseDragging && TryGetMouseUp())
            {
                _isMouseDragging = false;
            }

            if (TryGetMouseScrollDelta(out var scrollDelta))
            {
                if (Mathf.Abs(scrollDelta) > 0.0001f)
                {
                    ApplyZoom(targetCamera.orthographicSize * (1f - (scrollDelta * wheelZoomSpeed)));
                }
            }
        }

        private void HandleTouchControls()
        {
            if (IsMouseInputActiveThisFrame())
            {
                _isTouchDragging = false;
                _activeTouchId = -1;
                _isPinching = false;
                return;
            }

            if (!TryGetTouchContacts(out var contacts))
            {
                _isTouchDragging = false;
                _activeTouchId = -1;
                _isPinching = false;
                return;
            }

            if (contacts.Count >= 2)
            {
                HandlePinch(contacts[0], contacts[1]);
                _isTouchDragging = false;
                _activeTouchId = -1;
                return;
            }

            _isPinching = false;
            var touch = contacts[0];
            if (touch.phase == TouchPhase.Began)
            {
                if (IsPointerOverUi())
                {
                    return;
                }

                _isTouchDragging = true;
                _activeTouchId = touch.fingerId;
                _lastTouchScreenPosition = touch.position;
                return;
            }

            if (!_isTouchDragging || touch.fingerId != _activeTouchId)
            {
                return;
            }

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                PanByScreenDelta(_lastTouchScreenPosition, touch.position);
                _lastTouchScreenPosition = touch.position;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                _isTouchDragging = false;
                _activeTouchId = -1;
            }
        }

        private bool IsMouseInputActiveThisFrame()
        {
            if (_isMouseDragging)
            {
                return true;
            }

            return TryGetMouseHeld(out _) || TryGetMouseDown(out _);
        }

        private void HandlePinch(TouchContact a, TouchContact b)
        {
            var distance = Vector2.Distance(a.position, b.position);
            if (!_isPinching || a.phase == TouchPhase.Began || b.phase == TouchPhase.Began)
            {
                _isPinching = true;
                _pinchStartDistance = Mathf.Max(1f, distance);
                _pinchStartSize = targetCamera.orthographicSize;
                return;
            }

            if (distance <= 0.001f)
            {
                return;
            }

            var ratio = _pinchStartDistance / distance;
            ApplyZoom(_pinchStartSize * Mathf.Lerp(1f, ratio, Mathf.Clamp01(pinchZoomSpeed * 50f)));
        }

        private void ApplyZoom(float nextSize)
        {
            if (!targetCamera.orthographic)
            {
                return;
            }

            targetCamera.orthographicSize = Mathf.Clamp(nextSize, minOrthoSize, maxOrthoSize);
        }

        private void PanByScreenDelta(Vector2 fromScreen, Vector2 toScreen)
        {
            var fromWorld = ScreenToWorld(fromScreen);
            var toWorld = ScreenToWorld(toScreen);
            var delta = fromWorld - toWorld;
            targetCamera.transform.position += new Vector3(delta.x, delta.y, 0f);
        }

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            var z = Mathf.Abs(targetCamera.transform.position.z);
            return targetCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z));
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static bool TryGetMouseDown(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryGetInputSystemMouseDown(out position))
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }
#endif
            position = default;
            return false;
        }

        private static bool TryGetMouseHeld(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryGetInputSystemMouseHeld(out position))
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0))
            {
                position = Input.mousePosition;
                return true;
            }
#endif
            position = default;
            return false;
        }

        private static bool TryGetMouseUp()
        {
#if ENABLE_INPUT_SYSTEM
            if (TryGetInputSystemMouseUp())
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonUp(0))
            {
                return true;
            }
#endif
            return false;
        }

        private static bool TryGetMouseScrollDelta(out float delta)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryGetInputSystemMouseScroll(out delta))
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            var wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                delta = wheel;
                return true;
            }
#endif
            delta = 0f;
            return false;
        }

        private static bool TryGetTouchContacts(out System.Collections.Generic.List<TouchContact> contacts)
        {
            contacts = new System.Collections.Generic.List<TouchContact>(2);
#if ENABLE_INPUT_SYSTEM
            if (TryGetInputSystemTouches(contacts))
            {
                return contacts.Count > 0;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            var count = Input.touchCount;
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var touch = Input.GetTouch(i);
                    contacts.Add(new TouchContact(touch.fingerId, touch.position, touch.phase));
                }

                return true;
            }
#endif
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryGetInputSystemMouseDown(out Vector2 position)
        {
            position = default;
            var mouse = GetCurrentInputSystemDevice("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            if (mouse == null)
            {
                return false;
            }

            if (!TryReadBoolProperty(TryReadProperty(mouse, "leftButton"), "wasPressedThisFrame", out var pressed) || !pressed)
            {
                return false;
            }

            return TryReadVector2FromControl(mouse, "position", out position);
        }

        private static bool TryGetInputSystemMouseHeld(out Vector2 position)
        {
            position = default;
            var mouse = GetCurrentInputSystemDevice("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            if (mouse == null)
            {
                return false;
            }

            if (!TryReadBoolProperty(TryReadProperty(mouse, "leftButton"), "isPressed", out var pressed) || !pressed)
            {
                return false;
            }

            return TryReadVector2FromControl(mouse, "position", out position);
        }

        private static bool TryGetInputSystemMouseUp()
        {
            var mouse = GetCurrentInputSystemDevice("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            if (mouse == null)
            {
                return false;
            }

            return TryReadBoolProperty(TryReadProperty(mouse, "leftButton"), "wasReleasedThisFrame", out var released) && released;
        }

        private static bool TryGetInputSystemMouseScroll(out float delta)
        {
            delta = 0f;
            var mouse = GetCurrentInputSystemDevice("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            if (mouse == null)
            {
                return false;
            }

            if (!TryReadVector2FromControl(mouse, "scroll", out var scroll))
            {
                return false;
            }

            if (Mathf.Abs(scroll.y) <= 0.0001f)
            {
                return false;
            }

            delta = Mathf.Sign(scroll.y) * Mathf.Clamp01(Mathf.Abs(scroll.y) / 120f);
            return true;
        }

        private static bool TryGetInputSystemTouches(System.Collections.Generic.List<TouchContact> output)
        {
            var touchscreen = GetCurrentInputSystemDevice("UnityEngine.InputSystem.Touchscreen, Unity.InputSystem");
            if (touchscreen == null)
            {
                return false;
            }

            var touches = TryReadProperty(touchscreen, "touches") as System.Collections.IEnumerable;
            if (touches == null)
            {
                return false;
            }

            foreach (var touch in touches)
            {
                if (touch == null)
                {
                    continue;
                }

                var press = TryReadProperty(touch, "press");
                if (!TryReadBoolProperty(press, "isPressed", out var isPressed) || !isPressed)
                {
                    continue;
                }

                var touchIdControl = TryReadProperty(touch, "touchId");
                var phaseControl = TryReadProperty(touch, "phase");
                if (!TryReadIntFromControl(touchIdControl, out var fingerId))
                {
                    continue;
                }

                if (!TryReadIntFromControl(phaseControl, out var phaseValue))
                {
                    phaseValue = (int)TouchPhase.Moved;
                }

                if (!TryReadVector2FromControl(touch, "position", out var pos))
                {
                    continue;
                }

                output.Add(new TouchContact(fingerId, pos, (TouchPhase)Mathf.Clamp(phaseValue, 0, 5)));
            }

            return true;
        }

        private static object GetCurrentInputSystemDevice(string typeName)
        {
            var type = Type.GetType(typeName);
            return type?.GetProperty("current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private static object TryReadProperty(object source, string propertyName)
        {
            return source?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(source);
        }

        private static bool TryReadBoolProperty(object source, string propertyName, out bool value)
        {
            value = false;
            if (source == null)
            {
                return false;
            }

            var prop = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || prop.PropertyType != typeof(bool))
            {
                return false;
            }

            value = (bool)prop.GetValue(source);
            return true;
        }

        private static bool TryReadVector2FromControl(object deviceOrTouch, string controlPropertyName, out Vector2 value)
        {
            value = default;
            var control = TryReadProperty(deviceOrTouch, controlPropertyName);
            if (control == null)
            {
                return false;
            }

            var method = control.GetType().GetMethod("ReadValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(Vector2))
            {
                return false;
            }

            value = (Vector2)method.Invoke(control, null);
            return true;
        }

        private static bool TryReadIntFromControl(object control, out int value)
        {
            value = 0;
            if (control == null)
            {
                return false;
            }

            var method = control.GetType().GetMethod("ReadValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null)
            {
                return false;
            }

            var raw = method.Invoke(control, null);
            if (raw is int asInt)
            {
                value = asInt;
                return true;
            }

            if (raw is byte asByte)
            {
                value = asByte;
                return true;
            }

            var rawType = raw?.GetType();
            if (rawType != null && rawType.IsEnum)
            {
                value = Convert.ToInt32(raw);
                return true;
            }

            return false;
        }
#endif

        private readonly struct TouchContact
        {
            public int fingerId { get; }
            public Vector2 position { get; }
            public TouchPhase phase { get; }

            public TouchContact(int fingerId, Vector2 position, TouchPhase phase)
            {
                this.fingerId = fingerId;
                this.position = position;
                this.phase = phase;
            }
        }
    }
}
