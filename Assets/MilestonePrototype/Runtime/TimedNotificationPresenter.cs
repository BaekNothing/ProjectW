using System;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public sealed class PopupIconData
    {
        public string Glyph;
        public string Label;
        public Color EffectColor = new Color(.95f, .72f, .2f, .78f);
        public bool ShowRadial = true;
        public int SparkleCount = 8;
        public float SparkleSeconds = 1.15f;
        public float PulseSeconds = 1.8f;
    }

    public sealed class TimedPopupData
    {
        public string Title;
        public string Message;
        public PopupIconData[] Icons = Array.Empty<PopupIconData>();
        public string[] Buttons = Array.Empty<string>();
        public float DurationSeconds;
        public float RotationSeconds = 2.5f;
    }

    public sealed class TimedToastData
    {
        public string Title;
        public string Message;
        public float DurationSeconds = 3f;
    }

    public sealed class TimedNotificationPresenter
    {
        private const int ToastWindowId = 910010;
        private const int PopupWindowId = 910011;
        private const int RadialFrameCount = 48;
        private TimedPopupData popup;
        private TimedToastData toast;
        private float popupStartedAt;
        private float toastStartedAt;
        private Texture2D[] radialFrames;
        private Texture2D sparkleTexture;
        private GUIStyle overlayWindow;
        private GUIStyle popupTitle;
        private GUIStyle popupBody;
        private GUIStyle iconGlyph;
        private GUIStyle iconLabel;
        private GUIStyle toastTitle;
        private GUIStyle toastBody;

        public bool HasPopup => popup != null;
        public bool HasToast => toast != null;
        public string LastAction { get; private set; }

        public void ShowPopup(TimedPopupData data, float now)
        {
            popup = data ?? throw new ArgumentNullException(nameof(data));
            popupStartedAt = now;
            LastAction = null;
        }

        public void ShowToast(TimedToastData data, float now)
        {
            toast = data ?? throw new ArgumentNullException(nameof(data));
            toastStartedAt = now;
        }

        public void Update(float now)
        {
            if (popup != null && popup.DurationSeconds > 0f && now - popupStartedAt >= popup.DurationSeconds)
                popup = null;
            if (toast != null && toast.DurationSeconds > 0f && now - toastStartedAt >= toast.DurationSeconds)
                toast = null;
        }

        public void Draw(float width, float height, float now)
        {
            EnsureResources();
            if (toast != null)
            {
                Rect toastRect = ToastRect(width, height);
                GUI.Window(ToastWindowId, toastRect, _ => DrawToast(toastRect.width, toastRect.height),
                    string.Empty, overlayWindow);
                GUI.BringWindowToFront(ToastWindowId);
            }
            if (popup != null)
            {
                GUI.Window(PopupWindowId, new Rect(0f, 0f, width, height),
                    _ => DrawPopup(width, height, now), string.Empty, overlayWindow);
                GUI.BringWindowToFront(PopupWindowId);
            }
        }

        private void DrawPopup(float width, float height, float now)
        {
            DrawSolid(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, .62f));

            float panelWidth = Mathf.Min(720f, width - 36f);
            float panelHeight = Mathf.Min(480f, height - 36f);
            Rect panel = new Rect((width - panelWidth) * .5f, (height - panelHeight) * .5f,
                panelWidth, panelHeight);
            DrawSolid(panel, new Color(.97f, .97f, .95f, 1f));
            DrawBorder(panel, new Color(.18f, .18f, .18f, 1f));

            GUI.Label(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, 38f),
                popup.Title ?? string.Empty, popupTitle);

            PopupIconData[] icons = popup.Icons ?? Array.Empty<PopupIconData>();
            if (icons.Length > 0)
            {
                float slotWidth = Mathf.Min(150f, (panel.width - 40f) / icons.Length);
                float rowWidth = slotWidth * icons.Length;
                float startX = panel.center.x - rowWidth * .5f;
                for (int i = 0; i < icons.Length; i++)
                {
                    PopupIconData icon = icons[i] ?? new PopupIconData();
                    Rect effectRect = new Rect(startX + i * slotWidth + (slotWidth - 112f) * .5f,
                        panel.y + 65f, 112f, 112f);
                    DrawIconEffect(effectRect, icon, now, popup.RotationSeconds, i);
                    GUI.Label(effectRect, icon.Glyph ?? "?", iconGlyph);
                    GUI.Label(new Rect(startX + i * slotWidth + 4f, panel.y + 174f, slotWidth - 8f, 34f),
                        icon.Label ?? string.Empty, iconLabel);
                }
            }

            GUI.Label(new Rect(panel.x + 34f, panel.y + 220f, panel.width - 68f, 110f),
                popup.Message ?? string.Empty, popupBody);

            string[] buttons = popup.Buttons ?? Array.Empty<string>();
            if (buttons.Length == 0) buttons = new[] { "확인" };
            float gap = 10f;
            float buttonWidth = Mathf.Min(180f, (panel.width - 48f - gap * (buttons.Length - 1)) / buttons.Length);
            float buttonsWidth = buttonWidth * buttons.Length + gap * (buttons.Length - 1);
            float buttonX = panel.center.x - buttonsWidth * .5f;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (!GUI.Button(new Rect(buttonX + i * (buttonWidth + gap), panel.yMax - 66f, buttonWidth, 42f),
                    buttons[i])) continue;
                LastAction = buttons[i];
                popup = null;
                break;
            }
        }

        private static Rect ToastRect(float width, float height)
        {
            const float toastWidth = 360f;
            const float toastHeight = 92f;
            return new Rect(Mathf.Max(16f, width - toastWidth - 20f),
                Mathf.Max(16f, height - toastHeight - 22f), toastWidth, toastHeight);
        }

        private void DrawToast(float width, float height)
        {
            Rect rect = new Rect(0f, 0f, width, height);
            DrawSolid(rect, new Color(.12f, .12f, .12f, .94f));
            DrawBorder(rect, new Color(.85f, .68f, .24f, 1f));
            GUI.Label(new Rect(rect.x + 16f, rect.y + 10f, rect.width - 32f, 26f),
                toast.Title ?? string.Empty, toastTitle);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 38f, rect.width - 32f, 44f),
                toast.Message ?? string.Empty, toastBody);
        }

        private void DrawIconEffect(Rect rect, PopupIconData icon, float now, float rotationSeconds, int iconIndex)
        {
            float pulseSeconds = Mathf.Max(.1f, icon.PulseSeconds);
            float pulse = .82f + Mathf.Sin(now / pulseSeconds * Mathf.PI * 2f) * .18f;
            if (icon.ShowRadial)
            {
                float seconds = Mathf.Max(.1f, rotationSeconds);
                int frame = (int)(Mathf.Repeat(now / seconds, 1f) * radialFrames.Length);
                frame = Mathf.Clamp(frame, 0, radialFrames.Length - 1);
                Color previousColor = GUI.color;
                GUI.color = new Color(icon.EffectColor.r, icon.EffectColor.g, icon.EffectColor.b,
                    icon.EffectColor.a * pulse);
                GUI.DrawTexture(rect, radialFrames[frame]);
                GUI.color = previousColor;
            }

            DrawSparkles(rect, icon, now, iconIndex);
        }

        private void DrawSparkles(Rect rect, PopupIconData icon, float now, int iconIndex)
        {
            int count = Mathf.Clamp(icon.SparkleCount, 0, 20);
            float seconds = Mathf.Max(.2f, icon.SparkleSeconds);
            Color previousColor = GUI.color;
            for (int i = 0; i < count; i++)
            {
                float seed = iconIndex * 17.17f + i * 5.31f;
                float phase = Mathf.Repeat(now / seconds + seed * .137f, 1f);
                float blink = Mathf.Sin(phase * Mathf.PI);
                blink *= blink;
                float orbit = seed * 2.17f;
                float radius = rect.width * (.25f + Mathf.Repeat(seed * .173f, 1f) * .27f);
                float x = rect.center.x + Mathf.Cos(orbit) * radius;
                float y = rect.center.y + Mathf.Sin(orbit) * radius;
                float size = 5f + Mathf.Repeat(seed * .319f, 1f) * 9f * blink;
                GUI.color = new Color(icon.EffectColor.r, icon.EffectColor.g, icon.EffectColor.b,
                    icon.EffectColor.a * blink);
                GUI.DrawTexture(new Rect(x - size * .5f, y - size * .5f, size, size), sparkleTexture);
            }
            GUI.color = previousColor;
        }

        private void EnsureResources()
        {
            if (radialFrames == null)
            {
                radialFrames = new Texture2D[RadialFrameCount];
                for (int i = 0; i < radialFrames.Length; i++)
                    radialFrames[i] = CreateRadialTexture(128, 16, i * Mathf.PI * 2f / radialFrames.Length);
            }
            if (sparkleTexture == null) sparkleTexture = CreateSparkleTexture(24);
            if (popupTitle != null) return;
            Color ink = new Color(.16f, .16f, .16f, 1f);
            overlayWindow = new GUIStyle(GUIStyle.none);
            popupTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            popupTitle.normal.textColor = ink;
            popupBody = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true
            };
            popupBody.normal.textColor = ink;
            iconGlyph = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            iconGlyph.normal.textColor = ink;
            iconLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            iconLabel.normal.textColor = ink;
            toastTitle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            toastTitle.normal.textColor = Color.white;
            toastBody = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            toastBody.normal.textColor = new Color(.92f, .92f, .92f, 1f);
        }

        public static Texture2D CreateRadialTexture(int size, int rayCount, float phase = 0f)
        {
            int safeSize = Mathf.Max(8, size);
            int safeRays = Mathf.Max(2, rayCount);
            var texture = new Texture2D(safeSize, safeSize, TextureFormat.RGBA32, false);
            var pixels = new Color[safeSize * safeSize];
            float center = (safeSize - 1) * .5f;
            for (int y = 0; y < safeSize; y++)
            {
                for (int x = 0; x < safeSize; x++)
                {
                    float nx = (x - center) / center;
                    float ny = (y - center) / center;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float angle = Mathf.Repeat(Mathf.Atan2(ny, nx) - phase + Mathf.PI * 2f,
                        Mathf.PI * 2f);
                    float rayPosition = angle * safeRays / (Mathf.PI * 2f);
                    int rayIndex = (int)rayPosition;
                    float rayCenterDistance = Mathf.Abs(Mathf.Repeat(rayPosition + .5f, 1f) - .5f) * 2f;
                    float rayShape = Mathf.Pow(Mathf.Clamp01(1f - rayCenterDistance), 5f);
                    float rayVariation = .55f + Mathf.Repeat((rayIndex + 1) * .6180339f, 1f) * .45f;
                    float ray = rayShape * rayVariation;
                    float inner = Mathf.Clamp01(radius * 5f);
                    float outer = Mathf.Clamp01((1f - radius) * 1.35f);
                    pixels[y * safeSize + x] = new Color(1f, 1f, 1f, ray * inner * outer * .8f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public static Texture2D CreateSparkleTexture(int size)
        {
            int safeSize = Mathf.Max(8, size);
            var texture = new Texture2D(safeSize, safeSize, TextureFormat.RGBA32, false);
            var pixels = new Color[safeSize * safeSize];
            float center = (safeSize - 1) * .5f;
            for (int y = 0; y < safeSize; y++)
            {
                for (int x = 0; x < safeSize; x++)
                {
                    float nx = Mathf.Abs((x - center) / center);
                    float ny = Mathf.Abs((y - center) / center);
                    float cross = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Min(nx * 4.5f + ny, ny * 4.5f + nx)), 2f);
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny) * 3.2f);
                    float alpha = Mathf.Max(cross, core);
                    pixels[y * safeSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawSolid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, 1f), color);
            DrawSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            DrawSolid(new Rect(rect.x, rect.y, 1f, rect.height), color);
            DrawSolid(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }
    }
}
