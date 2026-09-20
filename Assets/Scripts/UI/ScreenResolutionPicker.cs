using System.Collections.Generic;
using UnityEngine;

namespace Protocol
{
    /// <summary>Shared settings control; selecting a resolution only edits the draft.</summary>
    public sealed class ScreenResolutionPicker
    {
        private readonly List<Vector2Int> options = new List<Vector2Int>();
        private int index;
        public Vector2Int Selected => options[index];


        public ScreenResolutionPicker()

        {
            foreach (var resolution in Screen.resolutions)
                Add(resolution.width, resolution.height);
            // Window sizes can differ from the monitor's fullscreen modes.
            Add(Screen.width, Screen.height);
            if (options.Count == 0)
                Add(960, 640);
            options.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            int width = PlayerPrefs.GetInt("Settings.ScreenWidth", Screen.width);
            int height = PlayerPrefs.GetInt("Settings.ScreenHeight", Screen.height);
            index = options.FindIndex(size => size.x == width && size.y == height);
            if (index < 0)
                index = Mathf.Max(0, options.FindIndex(size => size.x == Screen.width && size.y == Screen.height));
        }

        private void Add(int width, int height)

        {
            var size = new Vector2Int(width, height);
            if (width > 0 && height > 0 && !options.Contains(size))
                options.Add(size);
        }

        public void Draw(Rect rect, GUIStyle label, GUIStyle button)

        {
            float arrowWidth = 44;
            if (GUI.Button(new Rect(rect.x, rect.y, arrowWidth, rect.height), "<", button))
                index = (index + options.Count - 1) % options.Count;
            var centered = new GUIStyle(label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            GUI.Label(
                new Rect(rect.x + arrowWidth, rect.y, rect.width - 2 * arrowWidth, rect.height),
                Selected.x + " × " + Selected.y,
                centered);
            if (GUI.Button(new Rect(rect.xMax - arrowWidth, rect.y, arrowWidth, rect.height), ">", button))
                index = (index + 1) % options.Count;
        }
    }
}
