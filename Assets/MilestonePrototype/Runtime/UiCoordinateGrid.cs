using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public static class UiCoordinateGrid
    {
        public const int ColumnCount = 12;

        private static readonly Color LineColor = new Color(0.38f, 0.72f, 0.88f, 0.18f);
        private static readonly Color MajorLineColor = new Color(0.38f, 0.72f, 0.88f, 0.32f);
        private static readonly Color LabelColor = new Color(0.68f, 0.86f, 0.94f, 0.55f);
        private static GUIStyle labelStyle;

        public static void Draw(float width, float height)
        {
            if (Event.current.type != EventType.Repaint || width <= 0f || height <= 0f)
                return;

            EnsureStyle();

            float cellSize = width / ColumnCount;
            int rowCount = Mathf.CeilToInt(height / cellSize);

            for (int column = 0; column <= ColumnCount; column++)
            {
                float x = Mathf.Min(width - 1f, column * cellSize);
                DrawLine(new Rect(x, 0f, column % 4 == 0 ? 2f : 1f, height), column % 4 == 0);
            }

            for (int row = 0; row <= rowCount; row++)
            {
                float y = Mathf.Min(height - 1f, row * cellSize);
                DrawLine(new Rect(0f, y, width, row % 4 == 0 ? 2f : 1f), row % 4 == 0);
            }

            for (int column = 0; column < ColumnCount; column++)
            {
                var rect = new Rect(column * cellSize, 4f, cellSize, 22f);
                GUI.Label(rect, (column + 1).ToString(), labelStyle);
            }

            for (int row = 0; row < rowCount; row++)
            {
                var rect = new Rect(5f, row * cellSize, 28f, cellSize);
                GUI.Label(rect, RowLabel(row), labelStyle);
            }
        }

        public static string RowLabel(int zeroBasedRow)
        {
            if (zeroBasedRow < 0)
                return string.Empty;

            string label = string.Empty;
            int value = zeroBasedRow + 1;
            while (value > 0)
            {
                value--;
                label = (char)('A' + value % 26) + label;
                value /= 26;
            }

            return label;
        }

        private static void DrawLine(Rect rect, bool major)
        {
            Color previousColor = GUI.color;
            GUI.color = major ? MajorLineColor : LineColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static void EnsureStyle()
        {
            if (labelStyle != null)
                return;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = LabelColor;
        }
    }
}
