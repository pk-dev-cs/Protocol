using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Protocol.Editor
{
    public static class StageOneSceneTools
    {
        [MenuItem("Protocol/Prepare Stage 1 Scene")]
        public static void PrepareScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage01.unity");
            var bootstrap = Object.FindAnyObjectByType<ScenarioBootstrap>();
            bootstrap.BuildWorld();
            bootstrap.PrepareInteraction();
            // Persist generated materials so the saved scene works in builds as well.
            Directory.CreateDirectory("Assets/Materials");
            Directory.CreateDirectory("Assets/Meshes");
            foreach (var filter in bootstrap.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh))
                    continue;
                string meshPath = "Assets/Meshes/" + mesh.name + ".asset";
                var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existingMesh != null)
                {
                    EditorUtility.CopySerialized(mesh, existingMesh);
                    filter.sharedMesh = existingMesh;
                    var collider = filter.GetComponent<MeshCollider>();
                    if (collider != null)
                        collider.sharedMesh = existingMesh;
                }
                else
                    AssetDatabase.CreateAsset(mesh, meshPath);
            }

            foreach (var renderer in bootstrap.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null || AssetDatabase.Contains(material))
                        continue;
                    string path = "Assets/Materials/" + material.name + ".mat";
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (existing != null)
                        materials[i] = existing;
                    else
                        AssetDatabase.CreateAsset(material, path);
                }

                renderer.sharedMaterials = materials;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene(scene.path, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Stage 1 prepared: Base, Mine, Builder-01, Builder-02 and RTS camera.");
        }
    }
}
