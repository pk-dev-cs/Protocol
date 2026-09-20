using UnityEngine;

namespace Protocol
{
    public static class FrameRatePicker
    {
        private static readonly string[] Labels = { "30", "45", "60", "90", "120" };

        public static int Draw(Rect rect, int value, GUIStyle label)
        {
            GUI.Label(new Rect(rect.x, rect.y, 145, rect.height), "Limit FPS", label);
            int index = Mathf.Max(0, System.Array.IndexOf(FrameRatePolicy.Limits, value));
            index = GUI.SelectionGrid(new Rect(rect.x + 145, rect.y, rect.width - 145, rect.height),
                index, Labels, Labels.Length);
            return FrameRatePolicy.Limits[index];
        }
    }
}
