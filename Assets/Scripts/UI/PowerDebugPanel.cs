#if UNITY_EDITOR
using UnityEngine;

namespace Protocol
{
    public sealed class PowerDebugPanel : MonoBehaviour
    {
        private ScenarioBootstrap scenario;
        private ColonyPowerGrid grid;
        private PlanetEnvironmentSystem environment;

        private static float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 640f), .5f, 1.5f);
        private static Rect PanelRect => new Rect(12 * Scale, 64 * Scale, 210 * Scale, 82 * Scale);

        public static bool ContainsPointer(Vector2 position) => PanelRect.Contains(position);

        public void Initialize(ScenarioBootstrap owner, ColonyPowerGrid powerGrid, PlanetEnvironmentSystem planet)
        {
            scenario = owner;
            grid = powerGrid;
            environment = planet;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || PauseMenu.IsOpen || ScenarioLoadingScreen.IsVisible)
                return;

            float scale = Scale;
            GUI.depth = -30;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("DEBUG — POWER");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ SOLAR PANEL", GUILayout.Height(28 * scale)))
                AddSolarPanel();
            if (GUILayout.Button(Time.timeScale > 1 ? "1×" : "20×", GUILayout.Width(48 * scale), GUILayout.Height(28 * scale)))
                Time.timeScale = Time.timeScale > 1 ? 1 : 20;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Event current = Event.current;
            if (PanelRect.Contains(current.mousePosition) && (current.isMouse || current.type == EventType.ScrollWheel))
                current.Use();
        }

        private void AddSolarPanel()
        {
            if (scenario == null || grid == null || environment == null)
                return;
            int count = scenario.GetComponentsInChildren<SolarPanel>().Length;
            Vector3 position = grid.transform.position + new Vector3(-12 - count % 4 * 7, 0, count / 4 * 6);
            position.y = ScenarioLandscape.HeightAt(position.x, position.z);
            SolarPanel.Create(scenario.transform.Find("World"), position, grid, environment);
            GetComponent<MinimapPanel>()?.Initialize();
        }
    }
}
#endif
