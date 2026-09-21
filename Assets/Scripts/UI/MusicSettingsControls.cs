using UnityEngine;

namespace Protocol
{
    public static class MusicSettingsControls
    {
        public static void Draw(Rect rect, ref bool enabled, ref float volume, GUIStyle label)
        {
            enabled = GUI.Toggle(new Rect(rect.x, rect.y, 210, 32), enabled, " Muzyka",
                new GUIStyle(GUI.skin.toggle) { fontSize = 22 });
            volume = GUI.HorizontalSlider(new Rect(rect.x + 225, rect.y + 12, rect.width - 305, 20),
                volume, 0, 1);
            GUI.Label(new Rect(rect.xMax - 70, rect.y, 70, 30),
                Mathf.RoundToInt(volume * 100) + "%", label);
        }
    }
}
