using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Protocol.Editor
{
    public static class ModelSceneTools
    {
        [MenuItem("Protocol/Art/Apply Models To Saved Scenes")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play Mode before updating saved scenes.");
            var activeScene = SceneManager.GetActiveScene();
            foreach (string path in new[] { "Assets/Scenes/Stage01.unity", "Assets/Scenes/MainMenu.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var scenario in root.GetComponentsInChildren<ScenarioBootstrap>(true))
                        {
                            foreach (var solar in scenario.GetComponentsInChildren<SolarPanel>(true))
                                solar.BuildVisuals();
                            foreach (var storage in scenario.GetComponentsInChildren<BatteryStorage>(true))
                                storage.BuildVisuals();
                            BaseVisualFactory.EnsureModel(scenario.transform.Find("World/Base"));
                            IronVisualFactory.EnsureModel(scenario.transform.Find("World/Mine"));
                            foreach (Transform robot in scenario.transform.Find("World/Robots"))
                            {
                                var controller = robot.GetComponent<RobotController>();
                                var kind = controller != null && controller.UnitType == "Assembler"
                                    ? RobotKind.Assembler : RobotKind.Harvester;
                                RobotVisualFactory.EnsureModel(robot, kind);
                            }
                        }
                        foreach (var menu in root.GetComponentsInChildren<MainMenuPresentation>(true))
                        {
                            if (menu.Hero == null) continue;
                            RobotVisualFactory.EnsureModel(menu.Hero, RobotKind.Harvester);
                            foreach (var collider in menu.Hero.GetComponentsInChildren<Collider>(true))
                                Object.DestroyImmediate(collider);
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new System.IO.IOException("Could not save " + path);
                    Debug.Log("Concept models saved in " + path);
                }
                finally
                {
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
            if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
        }
    }
}