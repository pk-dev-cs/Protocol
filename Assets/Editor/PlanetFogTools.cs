using BKPureNature;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Protocol.Editor
{
    public static class PlanetFogTools
    {
        [MenuItem("Protocol/Configure Planet Volumetric Fog")]
        public static void Configure()
        {
            var world = GameObject.Find("Scenario/World");
            if (EditorApplication.isPlaying || world == null)
                throw new System.InvalidOperationException("Open Stage01 outside Play mode first.");

            const string path = "Assets/Art/PlanetStudy/FogHeightSource.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                data = new TerrainData { heightmapResolution = 257, size = new Vector3(256, 64, 256) };
                AssetDatabase.CreateAsset(data, path);
            }
            var heights = new float[257, 257];
            for (int z = 0; z <= 256; z++)
                for (int x = 0; x <= 256; x++)
                    heights[z, x] = ScenarioLandscape.HeightAt(x - 128, z - 128) / 64;
            data.SetHeights(0, 0, heights);

            var source = world.transform.Find("Fog height source");
            if (source == null)
            {
                source = new GameObject("Fog height source").transform;
                source.SetParent(world.transform);
            }
            source.position = new Vector3(-128, 0, -128);
            var terrain = source.GetComponent<Terrain>();
            if (terrain == null) terrain = source.gameObject.AddComponent<Terrain>();
            terrain.terrainData = data;
            terrain.drawHeightmap = false;
            terrain.drawTreesAndFoliage = false;

            var fogTransform = world.transform.Find("Planet volumetric fog");
            if (fogTransform == null)
            {
                fogTransform = new GameObject("Planet volumetric fog").transform;
                fogTransform.SetParent(world.transform);
            }
            var fog = fogTransform.GetComponent<PureVolumetricFog>();
            if (fog == null) fog = fogTransform.gameObject.AddComponent<PureVolumetricFog>();
            fog.SetTerrain(terrain);
            var settings = new SerializedObject(fog);
            Set(settings, "horizontalPadding", 0);
            Set(settings, "bottomPadding", 4);
            Set(settings, "topPadding", 32);
            Set(settings, "terrainEdgeFade", 6);
            Set(settings, "terrainFollow", 1);
            Set(settings, "density", .04f);
            Set(settings, "groundFogHeight", 14);
            Set(settings, "topSoftness", 8);
            Set(settings, "largeNoiseScale", 24);
            Set(settings, "detailNoiseScale", 8);
            Set(settings, "noiseAmount", .9f);
            Set(settings, "noiseFloor", .2f);
            Set(settings, "noiseStrength", 1.5f);
            Set(settings, "windSpeed", 1.2f);
            Set(settings, "ambientStrength", 1.6f);
            Set(settings, "maximumFogDistance", 400);
            Set(settings, "nearFadeDistance", 1);
            Set(settings, "targetStepLength", 2);
            settings.FindProperty("maximumRaySteps").intValue = 32;
            settings.FindProperty("minimumRaySteps").intValue = 8;
            settings.FindProperty("followSceneFogColor").boolValue = false;
            settings.FindProperty("fogColor").colorValue = new Color(.5f, .42f, .62f);
            settings.FindProperty("directionalLight").objectReferenceValue = world.transform.Find("Sun").GetComponent<Light>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            fog.Refresh();

            AlienWorldLighting.Ensure(world.transform);
            world.GetComponentInChildren<Camera>().GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(world.scene);
            EditorSceneManager.SaveScene(world.scene);
            AssetDatabase.SaveAssets();
        }

        private static void Set(SerializedObject settings, string property, float value)
        {
            settings.FindProperty(property).floatValue = value;
        }
    }
}
