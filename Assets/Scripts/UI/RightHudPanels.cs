using UnityEngine;

namespace Protocol
{
    public enum RightHudPanel { None, Environment, Software }

    public sealed class RightHudPanels : MonoBehaviour
    {
        private static RightHudPanels active;
        private RightHudPanel requested;
        private RightHudPanel displayed;
        private float openness;

        public static float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 1600f, Screen.height / 900f), .65f, 1.6f);
        public static Color Background => new Color(.035f, .055f, .075f, .97f);
        public static Color Accent => new Color(.4f, 1, .8f);
        public static bool IsRequested(RightHudPanel panel) => active != null && active.requested == panel;
        public static float Progress(RightHudPanel panel) => active != null && active.displayed == panel ? active.openness : 0;

        public static void Open(RightHudPanel panel)
        {
            if (active != null) active.requested = panel;
        }

        public static void Close(RightHudPanel panel)
        {
            if (IsRequested(panel)) active.requested = RightHudPanel.None;
        }

        public static void Toggle(RightHudPanel panel)
        {
            if (IsRequested(panel)) Close(panel);
            else Open(panel);
        }

        private void OnEnable() => active = this;

        private void OnDisable()
        {
            if (active == this) active = null;
            requested = displayed = RightHudPanel.None;
            openness = 0;
        }

        private void Update()
        {
            float target = displayed == requested && displayed != RightHudPanel.None ? 1 : 0;
            openness = Mathf.MoveTowards(openness, target, Time.unscaledDeltaTime / .25f);
            if (openness == 0) displayed = requested;
        }

        public static Rect SlideRect(RightHudPanel panel, float width, float top, float height) =>
            new Rect(Screen.width - width * Mathf.SmoothStep(0, 1, Progress(panel)), top, width, height);

        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void DrawBackground(Rect rect)
        {
            Fill(rect, Background);
            Fill(new Rect(rect.x, rect.y, 2 * Scale, rect.height), new Color(.25f, .72f, .65f, .8f));
        }

        public static bool MonitorButton(Rect rect, bool selected)
        {
            int id = GUIUtility.GetControlID("SoftwareMonitor".GetHashCode(), FocusType.Passive, rect);
            Event e = Event.current;
            bool hover = rect.Contains(e.mousePosition);
            bool pressed = GUIUtility.hotControl == id;
            bool clicked = false;
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                GUIUtility.hotControl = id;
                e.Use();
                pressed = true;
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && pressed)
            {
                GUIUtility.hotControl = 0;
                clicked = hover && GUI.enabled;
                e.Use();
            }
            Fill(rect, pressed ? new Color(.16f, .38f, .37f) : selected || hover ? new Color(.11f, .26f, .28f) : new Color(.07f, .12f, .15f));
            Color ink = GUI.enabled ? selected || hover ? Accent : new Color(.72f, .85f, .9f) : Color.gray;
            Rect screen = new Rect(rect.x + rect.width * .2f, rect.y + rect.height * .2f, rect.width * .6f, rect.height * .43f);
            float line = Mathf.Max(1, rect.width / 24);
            Fill(new Rect(screen.x, screen.y, screen.width, line), ink);
            Fill(new Rect(screen.x, screen.yMax - line, screen.width, line), ink);
            Fill(new Rect(screen.x, screen.y, line, screen.height), ink);
            Fill(new Rect(screen.xMax - line, screen.y, line, screen.height), ink);
            Fill(new Rect(rect.center.x - line / 2, screen.yMax, line, rect.height * .12f), ink);
            Fill(new Rect(rect.x + rect.width * .35f, rect.y + rect.height * .75f, rect.width * .3f, line), ink);
            if (selected) Fill(new Rect(rect.x, rect.yMax - line, rect.width, line), Accent);
            return clicked;
        }
    }
}
