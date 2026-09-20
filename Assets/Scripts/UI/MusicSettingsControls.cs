using UnityEngine;

namespace Protocol
{
    public static class MusicSettingsControls
    {
        public static void Draw(Rect rect, ref bool enabled, ref float volume, GUIStyle label)
        {
            enabled = GUI.Toggle(new Rect(rect.x, rect.y, 210, 32), enabled, " Muzyka",
                new GUIStyle(GUI.skin.toggle) { fontSize = 22 });
            GUI.Label(new Rect(rect.x + 225, rect.y, rect.width - 225, 30),
                "Głośność muzyki: " + Mathf.RoundToInt(volume * 100) + "%", label);
            volume = GUI.HorizontalSlider(new Rect(rect.x + 225, rect.y + 32, rect.width - 225, 20),
                volume, 0, 1);
        }
    }
}
