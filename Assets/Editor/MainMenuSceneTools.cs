using System;
using System.Collections.Generic;
using BKPureNature;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Protocol.Editor
{
    public static class MainMenuSceneTools
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string Materials = "Assets/Materials/MainMenu";
        private const string Planet = "Assets/Art/PlanetStudy/";
        private static readonly Color Ink = new Color(.035f, .055f, .07f, .68f);
        private static readonly Color Accent = new Color(.36f, .87f, .83f);
        private static Font font;

        [MenuItem("Protocol/Rebuild Main Menu Presentation")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/Materials", "MainMenu");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildWorld();
            BuildUI();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Main menu presentation saved.");
        }

        private static T Asset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing menu source: " + path);
            return asset;
        }

        private static Material Variant(string source, string name)
        {
            var path = Materials + "/" + name + ".mat";
            var original = Asset<Material>(source);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(original);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.CopyPropertiesFromMaterial(original);
            material.name = name;
            material.enableInstancing = true;
            if (material.HasProperty("_IgnoreWarFog")) material.SetFloat("_IgnoreWarFog", 1);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildWorld()
        {
            var root = new GameObject("Menu environment").transform;
            var ground = Variant(Planet + "Mineral regolith.mat", "Menu regolith");
            var basalt = Variant(Planet + "Weathered basalt.mat", "Menu basalt");
            var leaves = Variant(Planet + "Blue silver leaves.mat", "Menu leaves");
            var rose = Variant(Planet + "Rose succulent.mat", "Menu succulent");
            var shell = Variant("Assets/Materials/Environment/Robot shell.mat", "Menu robot shell");
            var dark = Variant("Assets/Materials/Environment/Graphite.mat", "Menu graphite");
            var cyan = Variant("Assets/Materials/Environment/Base cyan.mat", "Menu sensor");
            cyan.SetColor("_EmissionColor", new Color(.035f, .3f, .25f));
            shell.color = new Color(.52f, .59f, .61f);
            shell.SetFloat("_SurfaceDetail", 0);
            shell.SetFloat("_Glossiness", .23f);
            shell.SetFloat("_Metallic", .16f);
            var terrainMesh = Asset<Mesh>("Assets/Meshes/Environment/Scenario Landscape Surface.asset");
            var terrain = MeshObject("Planet surface", root, terrainMesh, ground, Vector3.zero, Vector3.one);
            var groundMaterials = new Material[terrainMesh.subMeshCount];
            for (int i = 0; i < groundMaterials.Length; i++) groundMaterials[i] = ground;
            terrain.GetComponent<MeshRenderer>().sharedMaterials = groundMaterials;

            ScenarioBootstrap.BuildRobot("Harvester presentation", root, new Vector3(1.4f, 0, 0), shell, dark, cyan);
            var hero = root.Find("Harvester presentation");
            hero.localRotation = Quaternion.Euler(0, -22, 0);
            foreach (var collider in hero.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);

            var random = new System.Random(91026);
            var rockMeshes = new Mesh[5];
            for (int i = 0; i < 5; i++) rockMeshes[i] = Asset<Mesh>(Planet + "Basalt " + i + ".asset");
            var rosette = Asset<Mesh>(Planet + "Curved leaf rosette.asset");
            var tuft = Asset<Mesh>(Planet + "Fine leaf tuft.asset");
            var details = new GameObject("Rocks and xenoflora").transform;
            details.SetParent(root);
            for (int i = 0; i < 280; i++)
            {
                float x = Range(random, -19, 25);
                float z = Range(random, -3, 49);
                if (z < 4 && x > -1.5f && x < 3.7f) continue;
                bool rock = i % 5 == 0;
                float scale = rock ? Range(random, .22f, 1.6f) : Range(random, .16f, .6f);
                var item = MeshObject(rock ? "Basalt" : "Xenoflora", details,
                    rock ? rockMeshes[i % 5] : (i % 3 == 0 ? rosette : tuft),
                    rock ? basalt : (i % 7 == 0 ? rose : leaves),
                    new Vector3(x, ScenarioLandscape.HeightAt(x, z) - .03f, z), Vector3.one * scale);
                item.transform.localRotation = Quaternion.Euler(0, Range(random, 0, 360), 0);
                if (!rock) item.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = 0; i < 9; i++)
            {
                float x = -32 + i * 9;
                float z = 64 + i % 3 * 11;
                var item = MeshObject("Distant basalt ridge", details, rockMeshes[i % 5], basalt,
                    new Vector3(x, ScenarioLandscape.HeightAt(x, z), z), new Vector3(5, 2.2f + i % 3 * 1.5f, 6));
                item.transform.localRotation = Quaternion.Euler(0, i * 73, 0);
            }

            var station = new GameObject("Remote service platform").transform;
            station.SetParent(root);
            station.position = new Vector3(-1.5f, 0, 14);
            Box("Platform", station, new Vector3(0, .12f, 0), new Vector3(5, .24f, 4), dark);
            Box("Power cabinet", station, new Vector3(1.1f, .9f, .4f), new Vector3(1.2f, 1.5f, 1), shell);
            Box("Cabinet indicator", station, new Vector3(1.1f, 1.2f, -.11f), new Vector3(.7f, .08f, .04f), cyan);
            Box("Antenna", station, new Vector3(-1.2f, 2, .7f), new Vector3(.1f, 4, .1f), dark);
            Box("Antenna crossbar", station, new Vector3(-1.2f, 3.8f, .7f), new Vector3(1.2f, .07f, .07f), dark);
            ScenarioBootstrap.BuildRobot("Parked service harvester", station, new Vector3(-.5f, .24f, -.4f), shell, dark, cyan);
            foreach (var collider in station.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);

            var sun = NewLight("Violet dawn", root, LightType.Directional, new Color(1, .77f, .69f), 1.1f);
            sun.transform.rotation = Quaternion.Euler(24, -48, 0);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .8f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.34f, .25f, .49f);
            RenderSettings.ambientEquatorColor = new Color(.24f, .27f, .34f);
            RenderSettings.ambientGroundColor = new Color(.10f, .09f, .14f);
            RenderSettings.fog = false;
            var sky = Variant("Assets/Resources/Materials/AlienNightSky.mat", "Menu violet sky");
            sky.SetFloat("_Daylight", .35f);
            sky.SetFloat("_Twilight", .8f);
            sky.SetFloat("_StarIntensity", .75f);
            sky.SetFloat("_Rotation", 15);
            RenderSettings.skybox = sky;

            var fill = NewLight("Cool machine fill", root, LightType.Point, new Color(.47f, .78f, 1), 1.1f);
            fill.transform.position = new Vector3(-1, 2.5f, -2);
            fill.range = 7;
            var sensor = NewLight("Sensor glow", hero, LightType.Point, Accent, .035f);
            sensor.transform.localPosition = new Vector3(0, 1.3f, -.9f);
            sensor.range = 1.7f;
            var cameraObject = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(root);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(-.5f, 1.05f, -4.4f);
            camera.fieldOfView = 40;
            camera.nearClipPlane = .08f;
            camera.farClipPlane = 230;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var presentation = root.gameObject.AddComponent<MainMenuPresentation>();
            presentation.MenuCamera = camera;
            presentation.Hero = hero;
            presentation.SensorLight = sensor;
            var direction = hero.position + Vector3.up * .83f - camera.transform.position;
            camera.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, -15.5f, 0);
            BuildFog(root, sun);
            var profilePath = Materials + "/Menu grading.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            if (!profile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>();
                AssetDatabase.AddObjectToAsset(tonemapping, profile);
            }
            tonemapping.mode.Override(TonemappingMode.Neutral);
            EditorUtility.SetDirty(tonemapping);
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("Menu color grading").AddComponent<Volume>();
            volume.transform.SetParent(root);
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static void BuildFog(Transform parent, Light sun)
        {
            var source = new GameObject("Fog height source", typeof(Terrain));
            source.transform.SetParent(parent);
            source.transform.position = new Vector3(-128, 0, -128);
            var terrain = source.GetComponent<Terrain>();
            terrain.terrainData = Asset<TerrainData>(Planet + "FogHeightSource.asset");
            terrain.drawHeightmap = false;
            terrain.drawTreesAndFoliage = false;
            var fog = new GameObject("Menu atmosphere").AddComponent<PureVolumetricFog>();
            fog.transform.SetParent(parent);
            fog.SetTerrain(terrain);
            var settings = new SerializedObject(fog);
            SetFloat(settings, "density", .012f);
            SetFloat(settings, "groundFogHeight", 4);
            SetFloat(settings, "topSoftness", 5);
            SetFloat(settings, "topPadding", 18);
            SetFloat(settings, "bottomPadding", 3);
            SetFloat(settings, "terrainFollow", 1);
            SetFloat(settings, "maximumFogDistance", 140);
            SetFloat(settings, "nearFadeDistance", 6);
            SetFloat(settings, "targetStepLength", 3);
            SetFloat(settings, "largeNoiseScale", 24);
            SetFloat(settings, "detailNoiseScale", 8);
            SetFloat(settings, "windSpeed", .4f);
            SetFloat(settings, "ambientStrength", 1.2f);
            settings.FindProperty("maximumRaySteps").intValue = 24;
            settings.FindProperty("minimumRaySteps").intValue = 6;
            settings.FindProperty("followSceneFogColor").boolValue = false;
            settings.FindProperty("fogColor").colorValue = new Color(.43f, .33f, .55f);
            settings.FindProperty("directionalLight").objectReferenceValue = sun;
            settings.ApplyModifiedPropertiesWithoutUndo();
            fog.Refresh();
        }

        private static void SetFloat(SerializedObject target, string property, float value) => target.FindProperty(property).floatValue = value;
        private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            return item;
        }

        private static void Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(item.GetComponent<Collider>());
        }

        private static Light NewLight(string name, Transform parent, LightType type, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private static void BuildUI()
        {
            var controller = new GameObject("Main Menu");
            var view = controller.AddComponent<MainMenuView>();
            controller.AddComponent<MainMenu>();
            var canvas = new GameObject("Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            var shade = Rect("Readability gradient", canvas.transform, Vector2.zero, new Vector2(.76f, 1));
            var texture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            for (int x = 0; x < 256; x++)
                texture.SetPixel(x, 0, new Color(.016f, .027f, .041f, .93f * Mathf.Pow(1 - x / 255f, 1.4f)));
            texture.Apply();
            const string gradientPath = Materials + "/Menu gradient.png";
            System.IO.File.WriteAllBytes(gradientPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(gradientPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(gradientPath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            var image = shade.gameObject.AddComponent<RawImage>();
            image.texture = Asset<Texture2D>(gradientPath);
            image.raycastTarget = false;

            var interaction = Rect("Navigation", canvas.transform, Vector2.zero, Vector2.one);
            view.Interaction = interaction.gameObject.AddComponent<CanvasGroup>();
            var title = Label("PROTOCOL", interaction, 64, TextAnchor.MiddleLeft);
            Anchor(title.rectTransform, new Vector2(.065f, .81f), new Vector2(.5f, .94f));
            title.fontStyle = FontStyle.Bold;
            var line = Rect("Title accent", interaction, new Vector2(.065f, .815f), new Vector2(.106f, .818f));
            Graphic(line, Accent);
            view.Home = Page("Home", interaction);
            view.Scenarios = Page("Scenarios", interaction);
            view.Settings = Page("Settings", interaction);
            view.ScenariosButton = Button("Scenariusze", Row(view.Home.transform, 0, 72));
            view.SettingsButton = Button("Ustawienia", Row(view.Home.transform, 88, 72));
            view.QuitButton = Button("Wyjdź z gry", Row(view.Home.transform, 176, 72));
            Heading(view.Scenarios.transform, "SCENARIUSZE");
            Label("Scenariusz 1", Row(view.Scenarios.transform, 76, 60), 30, TextAnchor.MiddleLeft);
            view.StartButton = Button("Rozpocznij", Row(view.Scenarios.transform, 156, 66));
            view.ScenariosBack = Button("Wróć", Row(view.Scenarios.transform, 246, 54), false);
            Heading(view.Settings.transform, "USTAWIENIA");
            view.Volume = SliderRow(view.Settings.transform, 65, "Głośność", 0, 1, out view.VolumeValue);
            view.MusicButton = ValueButton(view.Settings.transform, 122, "Muzyka", out view.MusicValue);
            view.MusicVolume = SliderRow(view.Settings.transform, 179, "Głośność muzyki", 0, 1, out view.MusicVolumeValue);
            view.Sensitivity = SliderRow(view.Settings.transform, 236, "Czułość kamery", .25f, 2.5f, out view.SensitivityValue);
            view.FullscreenButton = ValueButton(view.Settings.transform, 293, "Pełny ekran", out view.FullscreenValue);
            view.Resolution = ChoiceRow(view.Settings.transform, 350, "Rozdzielczość");
            view.Fps = ChoiceRow(view.Settings.transform, 407, "Limit FPS");
            view.BackgroundFps = ChoiceRow(view.Settings.transform, 464, "FPS w tle");
            var footer = Row(view.Settings.transform, 540, 56);
            view.SettingsBack = Button("Wróć", Rect("Back", footer, Vector2.zero, new Vector2(.44f, 1)), false);
            view.ApplyButton = Button("Zastosuj", Rect("Apply", footer, new Vector2(.5f, 0), Vector2.one));
            view.Message = Label("", interaction, 20, TextAnchor.MiddleLeft);
            Anchor(view.Message.rectTransform, new Vector2(.065f, .06f), new Vector2(.41f, .11f));
            view.Message.color = Accent;
            var curtain = Rect("Transition", canvas.transform, Vector2.zero, Vector2.one);
            Graphic(curtain, Color.black);
            view.Curtain = curtain.gameObject.AddComponent<CanvasGroup>();
            view.Curtain.alpha = 0;
            view.Curtain.blocksRaycasts = false;
            view.Scenarios.SetActive(false);
            view.Settings.SetActive(false);
            new GameObject("Menu EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorUtility.SetDirty(view);
        }

        private static GameObject Page(string name, Transform parent) => Rect(name, parent, new Vector2(.065f, .14f), new Vector2(.40f, .73f)).gameObject;

        private static void Heading(Transform parent, string text)
        {
            var label = Label(text, Row(parent, 0, 48), 24, TextAnchor.MiddleLeft);
            label.color = Accent;
        }

        private static RectTransform Row(Transform parent, float top, float height)
        {
            var row = Rect("Row", parent, new Vector2(0, 1), Vector2.one);
            row.pivot = new Vector2(.5f, 1);
            row.anchoredPosition = new Vector2(0, -top);
            row.sizeDelta = new Vector2(0, height);
            return row;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Anchor(rect, min, max);
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Graphic(RectTransform parent, Color color)
        {
            var image = parent.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string text, Transform parent, int size, TextAnchor alignment)
        {
            var label = Rect(text.Length > 0 ? text : "Value", parent, Vector2.zero, Vector2.one).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.text = text;
            label.color = new Color(.9f, .94f, .95f);
            label.alignment = alignment;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static Button Button(string text, RectTransform parent, bool filled = true)
        {
            parent.name = text;
            var image = Graphic(parent, filled ? Ink : new Color(.03f, .04f, .06f, .25f));
            image.raycastTarget = true;
            var button = parent.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.62f, 1, .93f) * 1.5f;
            colors.selectedColor = new Color(.6f, 1, .93f) * 1.4f;
            colors.pressedColor = new Color(.32f, .73f, .69f);
            colors.disabledColor = new Color(.4f, .4f, .4f, .5f);
            colors.fadeDuration = .1f;
            button.colors = colors;
            var label = Label(text, parent, 26, TextAnchor.MiddleLeft);
            label.rectTransform.offsetMin = new Vector2(22, 0);
            label.rectTransform.offsetMax = new Vector2(-16, 0);
            var marker = Rect("Accent", parent, Vector2.zero, new Vector2(0, 1));
            marker.sizeDelta = new Vector2(2, 0);
            Graphic(marker, new Color(Accent.r, Accent.g, Accent.b, .55f));
            return button;
        }

        private static RectTransform Setting(Transform parent, float top, string name)
        {
            var row = Row(parent, top, 45);
            var label = Label(name, row, 21, TextAnchor.MiddleLeft);
            Anchor(label.rectTransform, Vector2.zero, new Vector2(.47f, 1));
            return Rect("Control", row, new Vector2(.49f, 0), Vector2.one);
        }

        private static Button ValueButton(Transform parent, float top, string name, out Text value)
        {
            var control = Setting(parent, top, name);
            var button = Button("", control);
            value = button.GetComponentInChildren<Text>();
            value.fontSize = 21;
            value.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static MainMenuChoice ChoiceRow(Transform parent, float top, string name)
        {
            var control = Setting(parent, top, name);
            var choice = new MainMenuChoice();
            choice.Previous = Button("‹", Rect("Previous", control, Vector2.zero, new Vector2(.16f, 1)));
            choice.Next = Button("›", Rect("Next", control, new Vector2(.84f, 0), Vector2.one));
            foreach (var button in new[] { choice.Previous, choice.Next })
            {
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.alignment = TextAnchor.MiddleCenter;
            }
            choice.Value = Label("", Rect("Value", control, new Vector2(.17f, 0), new Vector2(.83f, 1)), 20, TextAnchor.MiddleCenter);
            return choice;
        }

        private static Slider SliderRow(Transform parent, float top, string name, float min, float max, out Text value)
        {
            var control = Setting(parent, top, name);
            value = Label("", Rect("Value", control, new Vector2(.75f, 0), Vector2.one), 20, TextAnchor.MiddleRight);
            var area = Rect("Slider", control, Vector2.zero, new Vector2(.70f, 1));
            var hit = Graphic(area, new Color(0, 0, 0, 0));
            hit.raycastTarget = true;
            Graphic(Rect("Track", area, new Vector2(0, .46f), new Vector2(1, .54f)), new Color(.3f, .4f, .44f));
            var handleArea = Rect("Handle area", area, new Vector2(0, .2f), new Vector2(1, .8f));
            var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(0, 1));
            handle.sizeDelta = new Vector2(10, 0);
            var handleImage = Graphic(handle, Accent);
            var slider = area.gameObject.AddComponent<Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = min;
            slider.maxValue = max;
            var navigation = slider.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            slider.navigation = navigation;
            return slider;
        }
    }
}
