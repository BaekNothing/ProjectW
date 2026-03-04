using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectW.Outgame
{
    public sealed class OutgameFlowController : MonoBehaviour
    {
        private const string OutgameSceneName = "Outgame Scene";
        private const string IngameSceneName = "MVP Scene";
        private const string LegacyRuntimeFontName = "LegacyRuntime.ttf";

        private Canvas _canvas;
        private Toggle _toggleA;
        private Toggle _toggleB;
        private Toggle _toggleC;
        private Dropdown _missionDropdown;
        private Slider _resourceSlider;
        private Slider _safetySlider;
        private Text _resourceValueText;
        private Text _safetyValueText;
        private Text _resultText;
        private Button _startButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureController()
        {
            var scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, OutgameSceneName, StringComparison.Ordinal))
            {
                return;
            }

            var existing = FindFirstObjectByType<OutgameFlowController>();
            if (existing != null)
            {
                return;
            }

            var root = new GameObject("OutgameFlowRoot");
            root.AddComponent<OutgameFlowController>();
        }

        private void Awake()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, OutgameSceneName, StringComparison.Ordinal))
            {
                enabled = false;
                return;
            }

            var mainCamera = ResolveMainCamera();
            _canvas = GetOrCreateCanvas(mainCamera);
            EnsureEventSystem();

            if (!TryBindExistingUi(_canvas.transform))
            {
                BuildUi(_canvas.transform);
            }

            WireUiEvents();
        }

        private void Start()
        {
            RefreshResultPanel();
            RefreshPriorityLabels();
            ValidateStartButton();
        }

        private Camera ResolveMainCamera()
        {
            var main = Camera.main;
            if (main != null)
            {
                return main;
            }

            return FindFirstObjectByType<Camera>();
        }

        private Canvas GetOrCreateCanvas(Camera mainCamera)
        {
            var canvasGo = GameObject.Find("OutgameCanvas");
            Canvas canvas = null;
            if (canvasGo != null)
            {
                canvas = canvasGo.GetComponent<Canvas>();
            }

            if (canvas == null)
            {
                canvasGo = new GameObject("OutgameCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
            }

            if (canvasGo.GetComponent<CanvasScaler>() == null)
            {
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCamera;
            canvas.planeDistance = 100f;
            return canvas;
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

        private bool TryBindExistingUi(Transform canvasRoot)
        {
            if (canvasRoot == null)
            {
                return false;
            }

            var panel = canvasRoot.Find("Panel");
            if (panel == null)
            {
                return false;
            }

            _toggleA = FindToggleByLabel(panel, "Character_A");
            _toggleB = FindToggleByLabel(panel, "Character_B");
            _toggleC = FindToggleByLabel(panel, "Character_C");

            var dropdowns = panel.GetComponentsInChildren<Dropdown>(true);
            _missionDropdown = dropdowns.Length > 0 ? dropdowns[0] : null;

            var sliders = panel.GetComponentsInChildren<Slider>(true);
            if (sliders.Length >= 2)
            {
                Array.Sort(sliders, (a, b) =>
                {
                    var ay = (a.transform as RectTransform)?.anchoredPosition.y ?? 0f;
                    var by = (b.transform as RectTransform)?.anchoredPosition.y ?? 0f;
                    return by.CompareTo(ay);
                });

                _resourceSlider = sliders[0];
                _safetySlider = sliders[1];
            }

            _startButton = FindButtonByText(panel, "Start Ingame Loop");
            _resultText = FindTextByPrefix(panel, "Last Result", "No result yet");

            if (_resourceSlider != null)
            {
                _resourceValueText = FindNearestNumericText(panel, _resourceSlider.transform as RectTransform);
            }

            if (_safetySlider != null)
            {
                _safetyValueText = FindNearestNumericText(panel, _safetySlider.transform as RectTransform, _resourceValueText);
            }

            return _toggleA != null
                   && _toggleB != null
                   && _toggleC != null
                   && _missionDropdown != null
                   && _resourceSlider != null
                   && _safetySlider != null
                   && _startButton != null;
        }

        private void WireUiEvents()
        {
            _toggleA.onValueChanged.RemoveAllListeners();
            _toggleB.onValueChanged.RemoveAllListeners();
            _toggleC.onValueChanged.RemoveAllListeners();
            _resourceSlider.onValueChanged.RemoveAllListeners();
            _safetySlider.onValueChanged.RemoveAllListeners();
            _startButton.onClick.RemoveAllListeners();

            _toggleA.onValueChanged.AddListener(_ => ValidateStartButton());
            _toggleB.onValueChanged.AddListener(_ => ValidateStartButton());
            _toggleC.onValueChanged.AddListener(_ => ValidateStartButton());
            _resourceSlider.onValueChanged.AddListener(_ => RefreshPriorityLabels());
            _safetySlider.onValueChanged.AddListener(_ => RefreshPriorityLabels());
            _startButton.onClick.AddListener(OnClickStartSession);
        }

        private void BuildUi(Transform canvasRoot)
        {
            var panel = CreateUiObject("Panel", canvasRoot);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.09f, 0.12f, 0.95f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0.1f);
            panelRect.anchorMax = new Vector2(0.9f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var y = -30f;
            CreateLabel(panel.transform, "TitleText", "Outgame Setup", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), 26, TextAnchor.MiddleCenter);

            y -= 55f;
            CreateLabel(panel.transform, "CharactersLabel", "Characters", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), 18, TextAnchor.MiddleLeft);
            y -= 40f;
            _toggleA = CreateToggle(panel.transform, "CharacterToggle_A", "A (Character_A)", new Vector2(30f, y));
            y -= 35f;
            _toggleB = CreateToggle(panel.transform, "CharacterToggle_B", "B (Character_B)", new Vector2(30f, y));
            y -= 35f;
            _toggleC = CreateToggle(panel.transform, "CharacterToggle_C", "C (Character_C)", new Vector2(30f, y));

            _toggleA.isOn = true;
            _toggleB.isOn = true;
            _toggleC.isOn = true;

            y -= 55f;
            CreateLabel(panel.transform, "MissionLabel", "Initial Mission", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), 18, TextAnchor.MiddleLeft);
            y -= 40f;
            _missionDropdown = CreateDropdown(panel.transform, new Vector2(30f, y), new List<string> { "ResourceSweep", "Recon", "SafetyPatrol" });
            _missionDropdown.value = 1;

            y -= 55f;
            CreateLabel(panel.transform, "ResourcePriorityLabel", "Priority - Resource", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), 18, TextAnchor.MiddleLeft);
            y -= 35f;
            _resourceSlider = CreateSlider(panel.transform, "ResourceSlider", new Vector2(30f, y), 50);
            _resourceValueText = CreateLabel(panel.transform, "ResourceValueText", "50", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(410f, y), 16, TextAnchor.MiddleLeft);

            y -= 50f;
            CreateLabel(panel.transform, "SafetyPriorityLabel", "Priority - Safety", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), 18, TextAnchor.MiddleLeft);
            y -= 35f;
            _safetySlider = CreateSlider(panel.transform, "SafetySlider", new Vector2(30f, y), 50);
            _safetyValueText = CreateLabel(panel.transform, "SafetyValueText", "50", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(410f, y), 16, TextAnchor.MiddleLeft);

            y -= 70f;
            _startButton = CreateButton(panel.transform, "StartButton", "Start Ingame Loop", new Vector2(30f, y));
            _startButton.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);

            _resultText = CreateLabel(panel.transform, "ResultText", "No result yet.", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 20f), 15, TextAnchor.LowerRight);
            _resultText.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 180f);
        }

        private void OnClickStartSession()
        {
            if (!ValidateStartButton())
            {
                return;
            }

            var selected = new List<string>();
            if (_toggleA.isOn) selected.Add("Character_A");
            if (_toggleB.isOn) selected.Add("Character_B");
            if (_toggleC.isOn) selected.Add("Character_C");

            var setup = new OutgameSessionSetup
            {
                SelectedCharacterIds = selected,
                InitialMissionType = (MissionType)Mathf.Clamp(_missionDropdown.value, 0, 2),
                ResourcePriority = Mathf.RoundToInt(_resourceSlider.value),
                SafetyPriority = Mathf.RoundToInt(_safetySlider.value)
            };

            SessionFlowRuntimeContext.SetPendingSetup(setup);

            if (!Application.CanStreamedLevelBeLoaded(IngameSceneName))
            {
                Debug.LogError("[Outgame] Cannot load scene: " + IngameSceneName);
                return;
            }

            SceneManager.LoadScene(IngameSceneName);
        }

        private bool ValidateStartButton()
        {
            var valid = (_toggleA != null && _toggleA.isOn)
                        || (_toggleB != null && _toggleB.isOn)
                        || (_toggleC != null && _toggleC.isOn);
            if (_startButton != null)
            {
                _startButton.interactable = valid;
            }

            return valid;
        }

        private void RefreshPriorityLabels()
        {
            if (_resourceValueText != null)
            {
                _resourceValueText.text = Mathf.RoundToInt(_resourceSlider.value).ToString();
            }

            if (_safetyValueText != null)
            {
                _safetyValueText.text = Mathf.RoundToInt(_safetySlider.value).ToString();
            }
        }

        private void RefreshResultPanel()
        {
            if (_resultText == null)
            {
                return;
            }

            var result = SessionFlowRuntimeContext.LastResult;
            if (result == null)
            {
                _resultText.text = "No result yet.";
                return;
            }

            _resultText.text = string.Format(
                "Last Result\nReason: {0}\nMission: {1:P0}\nSurvivors: {2}\nTick: {3}\nSession: {4}",
                string.IsNullOrWhiteSpace(result.TerminationReasonCode) ? "UNKNOWN" : result.TerminationReasonCode,
                Mathf.Clamp01(result.MissionProgressRatio),
                Mathf.Max(0, result.SurvivingCharacterCount),
                Mathf.Max(0, result.TickIndex),
                string.IsNullOrWhiteSpace(result.SessionId) ? "default" : result.SessionId);
        }

        private static Toggle FindToggleByLabel(Transform root, string keyword)
        {
            var toggles = root.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                var text = toggles[i].GetComponentInChildren<Text>(true);
                if (text != null && !string.IsNullOrWhiteSpace(text.text) && text.text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return toggles[i];
                }
            }

            return null;
        }

        private static Button FindButtonByText(Transform root, string textKeyword)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var text = buttons[i].GetComponentInChildren<Text>(true);
                if (text != null && !string.IsNullOrWhiteSpace(text.text) && text.text.IndexOf(textKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return buttons[i];
                }
            }

            return buttons.Length > 0 ? buttons[0] : null;
        }

        private static Text FindTextByPrefix(Transform root, params string[] prefixes)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var value = texts[i].text ?? string.Empty;
                for (int p = 0; p < prefixes.Length; p++)
                {
                    if (value.StartsWith(prefixes[p], StringComparison.OrdinalIgnoreCase))
                    {
                        return texts[i];
                    }
                }
            }

            return null;
        }

        private static Text FindNearestNumericText(Transform root, RectTransform target, Text except = null)
        {
            if (target == null)
            {
                return null;
            }

            var texts = root.GetComponentsInChildren<Text>(true);
            var bestDistance = float.MaxValue;
            Text best = null;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == except)
                {
                    continue;
                }

                var value = texts[i].text;
                if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value.Trim(), out _))
                {
                    continue;
                }

                var rect = texts[i].transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                var dx = rect.anchoredPosition.x - target.anchoredPosition.x;
                var dy = rect.anchoredPosition.y - target.anchoredPosition.y;
                var distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = texts[i];
                }
            }

            return best;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, int fontSize, TextAnchor align)
        {
            var go = CreateUiObject(name, parent);
            var label = go.AddComponent<Text>();
            label.font = ResolveRuntimeFont();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = align;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(420f, 30f);
            return label;
        }

        private static Font ResolveRuntimeFont()
        {
            return Resources.GetBuiltinResource<Font>(LegacyRuntimeFontName);
        }

        private static Toggle CreateToggle(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var root = CreateUiObject(name, parent);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(320f, 24f);

            var bg = CreateUiObject("Background", root.transform);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.18f, 0.22f, 1f);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = new Vector2(10f, 0f);
            bgRect.sizeDelta = new Vector2(18f, 18f);

            var checkmark = CreateUiObject("Checkmark", bg.transform);
            var checkImage = checkmark.AddComponent<Image>();
            checkImage.color = new Color(0.4f, 0.9f, 0.4f, 1f);
            var checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(2f, 2f);
            checkRect.offsetMax = new Vector2(-2f, -2f);

            var text = CreateLabel(root.transform, "Label", label, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 0f), 15, TextAnchor.MiddleLeft);
            text.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 24f);

            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            return toggle;
        }

        private static Dropdown CreateDropdown(Transform parent, Vector2 anchoredPos, List<string> options)
        {
            var go = CreateUiObject("MissionDropdown", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(260f, 32f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.24f, 0.3f, 1f);
            var dropdown = go.AddComponent<Dropdown>();

            var label = CreateLabel(go.transform, "Caption", options.Count > 0 ? options[0] : string.Empty, Vector2.zero, Vector2.one, new Vector2(8f, 0f), 14, TextAnchor.MiddleLeft);
            label.color = Color.white;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-25f, 0f);
            dropdown.captionText = label;

            var template = CreateUiObject("Template", go.transform);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 120f);
            var templateImage = template.AddComponent<Image>();
            templateImage.color = new Color(0.16f, 0.2f, 0.25f, 1f);
            var scrollRect = template.AddComponent<ScrollRect>();

            var viewport = CreateUiObject("Viewport", template.transform);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.05f);
            var viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            var content = CreateUiObject("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.spacing = 2f;
            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var item = CreateUiObject("Item", content.transform);
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0f, 28f);
            var itemImage = item.AddComponent<Image>();
            itemImage.color = new Color(0.22f, 0.27f, 0.34f, 1f);
            var itemToggle = item.AddComponent<Toggle>();
            itemToggle.targetGraphic = itemImage;

            var itemLabel = CreateLabel(item.transform, "ItemLabel", "Option", Vector2.zero, Vector2.one, Vector2.zero, 14, TextAnchor.MiddleLeft);
            var itemLabelRect = itemLabel.GetComponent<RectTransform>();
            itemLabelRect.offsetMin = new Vector2(10f, 0f);
            itemLabelRect.offsetMax = new Vector2(-10f, 0f);
            itemLabel.color = Color.white;
            itemToggle.graphic = itemImage;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            template.SetActive(false);

            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            dropdown.options = options.ConvertAll(option => new Dropdown.OptionData(option));
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPos, int value)
        {
            var go = CreateUiObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(360f, 20f);

            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = Mathf.Clamp(value, 0, 100);

            var background = CreateUiObject("Background", go.transform);
            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.18f, 0.22f, 1f);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(1f, 1f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var fillArea = CreateUiObject("Fill Area", go.transform);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(10f, 5f);
            fillAreaRect.offsetMax = new Vector2(-10f, -5f);

            var fill = CreateUiObject("Fill", fillArea.transform);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.32f, 0.72f, 0.96f, 1f);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var handle = CreateUiObject("Handle", go.transform);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(16f, 24f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Button CreateButton(Transform parent, string name, string text, Vector2 anchoredPos)
        {
            var go = CreateUiObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(200f, 40f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.25f, 0.58f, 0.3f, 1f);
            var button = go.AddComponent<Button>();

            var label = CreateLabel(go.transform, "ButtonLabel", text, Vector2.zero, Vector2.one, Vector2.zero, 15, TextAnchor.MiddleCenter);
            label.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            return button;
        }
    }
}
