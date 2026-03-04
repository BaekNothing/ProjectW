using System;
using ProjectW.Outgame;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectW.IngameMvp
{
    public sealed class IngameResultPopupController : MonoBehaviour
    {
        private const string LegacyRuntimeFontName = "LegacyRuntime.ttf";
        private Canvas _canvas;
        private GameObject _panel;
        private Text _bodyText;
        private Button _confirmButton;

        public void Show(SessionResultSummary summary, Action onConfirm)
        {
            EnsureUi();
            if (_panel == null)
            {
                return;
            }

            var code = string.IsNullOrWhiteSpace(summary?.TerminationReasonCode) ? "UNKNOWN" : summary.TerminationReasonCode;
            _bodyText.text = string.Format(
                "Reason: {0}\nMission: {1:P0}\nSurvivors: {2}\nTick: {3}\nSession: {4}",
                code,
                Mathf.Clamp01(summary?.MissionProgressRatio ?? 0f),
                Mathf.Max(0, summary?.SurvivingCharacterCount ?? 0),
                Mathf.Max(0, summary?.TickIndex ?? 0),
                string.IsNullOrWhiteSpace(summary?.SessionId) ? "default" : summary.SessionId);

            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(() => onConfirm?.Invoke());
            _panel.SetActive(true);
        }

        private void EnsureUi()
        {
            if (_panel != null)
            {
                return;
            }

            EnsureEventSystem();

            var canvasGo = GameObject.Find("IngameResultCanvas");
            if (canvasGo != null)
            {
                _canvas = canvasGo.GetComponent<Canvas>();
            }

            if (_canvas == null)
            {
                canvasGo = new GameObject("IngameResultCanvas");
                _canvas = canvasGo.AddComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = ResolveMainCamera();
            _canvas.planeDistance = 100f;
            if (canvasGo.GetComponent<CanvasScaler>() == null)
            {
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            if (TryBindExistingUi(_canvas.transform))
            {
                _panel.SetActive(false);
                return;
            }

            _panel = new GameObject("ResultPanel", typeof(RectTransform));
            _panel.transform.SetParent(canvasGo.transform, false);
            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.78f);
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.3f, 0.25f);
            panelRect.anchorMax = new Vector2(0.7f, 0.75f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var title = CreateText(_panel.transform, "Session Result", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(360f, 40f));
            title.color = Color.white;

            _bodyText = CreateText(_panel.transform, string.Empty, 18, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -90f), new Vector2(-20f, 0f));
            var bodyRect = _bodyText.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0.25f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(20f, 0f);
            bodyRect.offsetMax = new Vector2(-20f, -90f);

            _confirmButton = CreateButton(_panel.transform, "아웃게임으로", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(180f, 42f));
            _panel.SetActive(false);
        }

        private bool TryBindExistingUi(Transform canvasRoot)
        {
            if (canvasRoot == null)
            {
                return false;
            }

            var panelTransform = canvasRoot.Find("ResultPanel");
            if (panelTransform == null)
            {
                return false;
            }

            _panel = panelTransform.gameObject;
            var buttons = panelTransform.GetComponentsInChildren<Button>(true);
            _confirmButton = buttons.Length > 0 ? buttons[0] : null;
            var texts = panelTransform.GetComponentsInChildren<Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null)
                {
                    continue;
                }

                var value = text.text ?? string.Empty;
                if (value.Contains("Reason:") || text.alignment == TextAnchor.UpperLeft)
                {
                    _bodyText = text;
                    break;
                }
            }

            if (_bodyText == null)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null && texts[i].alignment != TextAnchor.UpperCenter)
                    {
                        _bodyText = texts[i];
                        break;
                    }
                }
            }

            return _panel != null && _confirmButton != null && _bodyText != null;
        }

        private static Camera ResolveMainCamera()
        {
            var main = Camera.main;
            if (main != null)
            {
                return main;
            }

            return FindFirstObjectByType<Camera>();
        }

        private static void EnsureEventSystem()
        {
            var existing = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (existing == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                ConfigureEventSystemInputModule(eventSystemGo);
                return;
            }

            ConfigureEventSystemInputModule(existing.gameObject);
        }

        private static void ConfigureEventSystemInputModule(GameObject eventSystemGo)
        {
            if (eventSystemGo == null)
            {
                return;
            }

            var standalone = eventSystemGo.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            var inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType != null)
            {
                var inputSystemModule = eventSystemGo.GetComponent(inputSystemModuleType) ?? eventSystemGo.AddComponent(inputSystemModuleType);
                if (inputSystemModule is Behaviour behaviour)
                {
                    behaviour.enabled = true;
                }

                if (standalone != null)
                {
                    standalone.enabled = false;
                }

                return;
            }

            if (standalone == null)
            {
                standalone = eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            standalone.enabled = true;
        }

        private static Text CreateText(Transform parent, string text, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = ResolveRuntimeFont();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return label;
        }

        private static Font ResolveRuntimeFont()
        {
            return Resources.GetBuiltinResource<Font>(LegacyRuntimeFontName);
        }

        private static Button CreateButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.55f, 0.32f, 1f);
            var button = go.AddComponent<Button>();

            var label = CreateText(go.transform, text, 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            label.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            return button;
        }
    }
}
