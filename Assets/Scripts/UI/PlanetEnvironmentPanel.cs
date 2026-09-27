using System.Globalization;
using UnityEngine;

namespace Protocol
{
    public sealed class PlanetEnvironmentPanel : MonoBehaviour
    {
        private static PlanetEnvironmentPanel active;
        private static readonly string[] Names = { "Temperature", "Wind", "Dust", "Solar", "EM Activity", "Radiation" };
        private readonly string[] values = new string[6];
        private readonly float[] levels = new float[6];
        private PlanetEnvironmentSystem environment;
        private bool open;
        private float openness;
        private float nextRefresh;
        private Texture2D planetIcon;
        private GUIStyle headingStyle;
        private GUIStyle labelStyle;
        private GUIStyle numberStyle;
        private GUIStyle iconStyle;

        public bool IsOpen => open;
        public Rect VisiblePanelRect => PanelRect;
        private static float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 1600f, Screen.height / 900f), .45f, 1.6f);
        private static float ClockScale => Mathf.Clamp(Screen.height / 900f, .7f, 1.5f);
        public static Rect IconRect => new Rect(Screen.width - 222 * ClockScale, 16 * ClockScale, 46 * ClockScale, 46 * ClockScale);
        private bool Visible => !PauseMenu.IsOpen && !RobotProgrammingPanel.IsOpen && !ScenarioLoadingScreen.IsVisible;
        private Rect PanelRect
        {
            get
            {
                float width = Screen.width * .2f;
                float eased = Mathf.SmoothStep(0, 1, openness);
                return new Rect(Screen.width - width * eased, 78 * ClockScale, width, 360 * Scale);
            }
        }

        public void Initialize(PlanetEnvironmentSystem source)
        {
            environment = source;
            CacheValues();
        }

        private void OnEnable() => active = this;

        private void OnDisable()
        {
            if (active == this) active = null;
        }

        public static bool ContainsPointer(Vector2 guiPosition) => active != null && active.isActiveAndEnabled && active.Visible &&
            (IconRect.Contains(guiPosition) || (active.openness > 0 && active.PanelRect.Contains(guiPosition)));

        public void Toggle()
        {
            open = !open;
            if (open) CacheValues();
        }

        private void Update()
        {
            openness = Mathf.MoveTowards(openness, open ? 1 : 0, Time.unscaledDeltaTime / .25f);
            if (openness > 0 && Visible && Time.unscaledTime >= nextRefresh)
            {
                CacheValues();
                nextRefresh = Time.unscaledTime + .25f;
            }
        }

        private void CacheValues()
        {
            if (environment == null) return;
            var state = environment.State;
            levels[0] = state.Temperature;
            levels[1] = state.Wind;
            levels[2] = state.Dust;
            levels[3] = state.Solar;
            levels[4] = state.EMActivity;
            levels[5] = state.Radiation;
            for (int i = 0; i < levels.Length; i++)
            {
                string value = levels[i].ToString("0.00", CultureInfo.InvariantCulture);
                if (values[i] != value) values[i] = value;
            }
        }

        private void OnGUI()
        {
            if (!Visible || environment == null) return;
            EnsureStyles();
            int oldDepth = GUI.depth;
            GUI.depth = -20;
            var pointer = Event.current.mousePosition;
            var icon = IconRect;
            bool hover = icon.Contains(pointer);
            Fill(icon, open || hover ? new Color(.11f, .26f, .28f, .98f) : new Color(.055f, .08f, .11f, .97f));
            if (GUI.Button(icon, GUIContent.none, iconStyle)) Toggle();
            var tint = GUI.color;
            GUI.color = hover || open ? new Color(.4f, 1, .8f) : new Color(.72f, .85f, .9f);
            GUI.DrawTexture(new Rect(icon.x + icon.width * .19f, icon.y + icon.height * .19f, icon.width * .62f, icon.height * .62f), planetIcon);
            GUI.color = tint;
            if (openness > 0)
            {
                var panel = PanelRect;
                float padding = 20 * Scale;
                Fill(panel, new Color(.035f, .055f, .075f, .97f));
                Fill(new Rect(panel.x, panel.y, 2 * Scale, panel.height), new Color(.25f, .72f, .65f, .8f));
                headingStyle.fontSize = Mathf.RoundToInt(16 * Scale);
                labelStyle.fontSize = numberStyle.fontSize = Mathf.RoundToInt(16 * Scale);
                GUI.Label(new Rect(panel.x + padding, panel.y + 16 * Scale, panel.width - padding * 2, 32 * Scale), "PLANET ENVIRONMENT", headingStyle);
                for (int i = 0; i < Names.Length; i++)
                {
                    float y = panel.y + (66 + i * 46) * Scale;
                    GUI.Label(new Rect(panel.x + padding, y, panel.width - padding * 2 - 52 * Scale, 24 * Scale), Names[i], labelStyle);
                    GUI.Label(new Rect(panel.xMax - padding - 52 * Scale, y, 52 * Scale, 24 * Scale), values[i], numberStyle);
                    var bar = new Rect(panel.x + padding, y + 29 * Scale, panel.width - padding * 2, 2 * Scale);
                    Fill(bar, new Color(.13f, .21f, .25f));
                    bar.width *= levels[i];
                    Fill(bar, new Color(.3f, .73f, .68f));
                }
            }
            if (ContainsPointer(pointer) && (Event.current.isMouse || Event.current.type == EventType.ScrollWheel)) Event.current.Use();
            GUI.depth = oldDepth;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (headingStyle != null) return;
            headingStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            headingStyle.normal.textColor = new Color(.4f, 1, .8f);
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = new Color(.83f, .88f, .92f);
            numberStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleRight };
            iconStyle = new GUIStyle(GUIStyle.none);
            planetIcon = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Environment icon", filterMode = FilterMode.Bilinear };
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float px = (x - 31.5f) / 27, py = (y - 31.5f) / 27;
                    float radius = Mathf.Sqrt(px * px + py * py);
                    float globe = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.035f, .075f, Mathf.Abs(radius - 1)));
                    float meridian = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.035f, .075f, Mathf.Abs(Mathf.Sqrt(px * px * 4 + py * py) - 1)));
                    float equator = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.025f, .065f, Mathf.Abs(py)))) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.97f, 1.04f, Mathf.Abs(px))));
                    pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Max(globe, Mathf.Max(meridian, equator)));
                }
            planetIcon.SetPixels(pixels);
            planetIcon.Apply(false, true);
        }

        private void OnDestroy()
        {
            if (planetIcon != null) Destroy(planetIcon);
        }
    }
}
