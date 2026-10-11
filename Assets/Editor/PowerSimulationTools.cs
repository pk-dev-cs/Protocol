using UnityEditor;
using UnityEngine;

namespace Protocol.Editor
{
    public static class PowerSimulationTools
    {
        [MenuItem("Protocol/Debug/Simulation speed/20x")]
        private static void Fast() => Time.timeScale = 20;

        [MenuItem("Protocol/Debug/Simulation speed/1x")]
        private static void Normal() => Time.timeScale = 1;

        [MenuItem("Protocol/Debug/Simulation speed/20x", true)]
        [MenuItem("Protocol/Debug/Simulation speed/1x", true)]
        private static bool CanChangeSpeed() => EditorApplication.isPlaying && !PauseMenu.IsOpen;

        [MenuItem("Protocol/Debug/Power/Add Solar Panel")]
        private static void AddSolarPanel()
        {
            var scenario = Object.FindAnyObjectByType<ScenarioBootstrap>();
            if (scenario == null || !scenario.IsInitialized) return;
            var world = scenario.transform.Find("World");
            var grid = scenario.GetComponentInChildren<ColonyPowerGrid>();
            var environment = scenario.GetComponentInChildren<PlanetEnvironmentSystem>();
            if (grid == null || environment == null) return;
            int count = scenario.GetComponentsInChildren<SolarPanel>().Length;
            Vector3 position = grid.transform.position + new Vector3(-12 - (count % 4) * 7, 0, (count / 4) * 6);
            position.y = ScenarioLandscape.HeightAt(position.x, position.z);
            var panel = SolarPanel.Create(world, position, grid, environment);
            Selection.activeGameObject = panel.gameObject;
            scenario.GetComponent<MinimapPanel>()?.Initialize();
        }

        [MenuItem("Protocol/Debug/Power/Add Solar Panel", true)]
        private static bool CanAddSolarPanel() => EditorApplication.isPlaying && !PauseMenu.IsOpen;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Time.timeScale = 1;
        }
    }
}
