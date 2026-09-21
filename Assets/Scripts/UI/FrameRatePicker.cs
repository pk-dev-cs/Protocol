using UnityEngine;

namespace Protocol
{
    public static class FrameRatePicker
    {
        private static readonly string[] Labels = { "30", "45", "60", "90", "120" };
        private static readonly string[] BackgroundLabels = { "15", "20", "30", "45", "60" };

        public static int Draw(Rect rect, int value, GUIStyle label)
        {
            GUI.Label(new Rect(rect.x, rect.y, 225, rect.height), "Limit FPS", label);
            int index = Mathf.Max(0, System.Array.IndexOf(FrameRatePolicy.Limits, value));
            index = GUI.SelectionGrid(new Rect(rect.x + 225, rect.y, rect.width - 225, rect.height),
                index, Labels, Labels.Length);
            return FrameRatePolicy.Limits[index];
        }

        public static int DrawBackground(Rect rect, int value, GUIStyle label)
        {
            GUI.Label(new Rect(rect.x, rect.y, 225, rect.height), "Background FPS", label);
            int index = Mathf.Max(0, System.Array.IndexOf(FrameRatePolicy.BackgroundLimits, value));
            index = GUI.SelectionGrid(new Rect(rect.x + 225, rect.y, rect.width - 225, rect.height),
                index, BackgroundLabels, BackgroundLabels.Length);
            return FrameRatePolicy.BackgroundLimits[index];
        }
    }
}
