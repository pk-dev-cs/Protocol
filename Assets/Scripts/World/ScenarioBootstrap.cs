using UnityEngine;

namespace Protocol
{
    /// <summary>Builds the primitive scenario and attaches the current stage's components.</summary>
    public sealed class ScenarioBootstrap : MonoBehaviour
    {
        private void Awake()

        {
            // A scene baked with the editor command already contains the world.
            BuildWorld();
            PrepareInteraction();
            TreeResource.InitializeForest(transform.Find("World"));
            var navigation = GetComponent<ScenarioNavigation>();
            if (navigation == null)
                navigation = gameObject.AddComponent<ScenarioNavigation>();
            navigation.Initialize(transform.Find("World"));
            PrepareEconomy();
            int luaIndex = 0;
            foreach (Transform robot in transform.Find("World/Robots"))
            {
                var runtime = robot.gameObject.AddComponent<RobotLuaRuntime>();
                runtime.LoadExample(luaIndex++);
                runtime.InitializeSavedProgram("stage01-" + robot.name);
                robot.GetComponent<RobotController>().InitializeLua(runtime);
            }

            gameObject.AddComponent<RobotProgrammingPanel>();
            gameObject.AddComponent<PauseMenu>();
            gameObject.AddComponent<FogOfWar>().Initialize(transform.Find("World"));
            gameObject.AddComponent<SelectionPortraits>();
            gameObject.AddComponent<MinimapPanel>().Initialize();
        }

        private void PrepareEconomy()

        {
            var resources = gameObject.AddComponent<ResourceManager>();
            var mine = transform.Find("World/Mine").gameObject.AddComponent<Mine>();
            var baseBuilding = transform.Find("World/Base").gameObject.AddComponent<BaseBuilding>();
            baseBuilding.Initialize(resources);
            var baseRuntime = baseBuilding.gameObject.AddComponent<RobotLuaRuntime>();
            baseRuntime.LoadExample(0);
            baseRuntime.InitializeSavedProgram("stage01-Base");
            int index = 0;
            foreach (Transform robotTransform in transform.Find("World/Robots"))
            {
                var robot = robotTransform.GetComponent<RobotController>();
                var destination = new GameObject(robot.name + " - base destination").transform;
                destination.SetParent(baseBuilding.transform, false);
                destination.localPosition = new Vector3(index++ == 0 ? -1.3f : 1.3f, 0, -5);
                var inventory = robot.gameObject.AddComponent<RobotInventory>();
                var cycle = robot.gameObject.AddComponent<RobotHarvestCycle>();
                robot.InitializeEconomy(inventory, cycle, destination);
                cycle.Initialize(robot, mine, baseBuilding);
            }

            GetComponent<UnitPanel>().InitializeResources(resources);
        }

        public void PrepareInteraction()

        {
            foreach (string name in new[]
            {
                "Base",
                "Mine"
            }

            )
            {
                var structure = transform.Find("World/" + name);
                if (structure.GetComponent<SelectableStructure>() == null)
                    structure.gameObject.AddComponent<SelectableStructure>();
            }

            transform.Find("World/Main Camera").GetComponent<RTSCameraController>().ConfigureMap();
            // Also upgrades a world saved by the stage-one editor command.
            var robots = transform.Find("World/Robots");
            foreach (Transform robot in robots)
            {
                if (robot.name.StartsWith("Builder-"))
                    robot.name = robot.name.Replace("Builder-", "Harvester-");
                if (robot.GetComponent<RobotController>() == null)
                    robot.gameObject.AddComponent<RobotController>();
            }

            var selection = GetComponent<SelectionManager>();
            if (selection == null)
                selection = gameObject.AddComponent<SelectionManager>();
            selection.Initialize(transform.Find("World/Main Camera").GetComponent<Camera>());
            var panel = GetComponent<UnitPanel>();
            if (panel == null)
                panel = gameObject.AddComponent<UnitPanel>();
            panel.Initialize(selection);
        }

        public void BuildWorld()

        {
            if (transform.Find("World") != null)
            {
                ScenarioLandscape.Ensure(transform.Find("World"));
                return;
            }

            var world = new GameObject("World").transform;
            world.SetParent(transform, false);
            var dark = Material("Graphite", new Color(0.08f, 0.12f, 0.16f));
            var steel = Material("Steel", new Color(0.53f, 0.65f, 0.7f));
            var cyan = Material("Base cyan", new Color(0.08f, 0.72f, 0.83f));
            var gold = Material("Ore amber", new Color(0.95f, 0.52f, 0.12f));
            var white = Material("Robot shell", new Color(0.83f, 0.9f, 0.91f));
            ScenarioLandscape.Ensure(world);
            var baseRoot = Root("Base", world, new Vector3(-9, 0, 2));
            Part("Foundation", baseRoot, PrimitiveType.Cube, new Vector3(0, 0.3f, 0), new Vector3(7, 0.6f, 6), dark);
            Part("Command building", baseRoot, PrimitiveType.Cube, new Vector3(0, 1.6f, 0), new Vector3(5, 2.4f, 4), steel);
            Part("Roof", baseRoot, PrimitiveType.Cube, new Vector3(0, 2.9f, 0), new Vector3(5.5f, 0.3f, 4.5f), dark);
            Part("Energy core", baseRoot, PrimitiveType.Cylinder, new Vector3(0, 3.4f, 0), new Vector3(2, 0.45f, 2), cyan);
            Part("Front light", baseRoot, PrimitiveType.Cube, new Vector3(0, 1.7f, -2.03f), new Vector3(3.5f, 0.4f, 0.08f), cyan, false);
            Part("Delivery pad", baseRoot, PrimitiveType.Cube, new Vector3(0, 0.04f, -5), new Vector3(3, 0.08f, 3), cyan, false);
            var mine = Root("Mine", world, new Vector3(10, 0, 5));
            Part("Rock bed", mine, PrimitiveType.Cylinder, new Vector3(0, 0.3f, 0), new Vector3(7, 0.3f, 6), dark);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                var crystal = Part(
                    "Ore crystal",
                    mine,
                    PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(angle) * 1.6f, 1.1f + i * 0.12f, Mathf.Sin(angle) * 1.3f),
                    new Vector3(1.1f, 2 + i * 0.22f, 1.1f),
                    gold);
                crystal.transform.localRotation = Quaternion.Euler(12 + i * 5, i * 37, 18);
            }

            Part("Mine approach", mine, PrimitiveType.Cube, new Vector3(0, 0.04f, -4.5f), new Vector3(3, 0.08f, 2), gold, false);
            var robots = Root("Robots", world, Vector3.zero);
            BuildRobot("Harvester-01", robots, new Vector3(-5, 0, -5), white, dark, cyan);
            BuildRobot("Harvester-02", robots, new Vector3(-1, 0, -5), white, dark, gold);
            var sun = Root("Sun", world, Vector3.zero).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.68f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = false;
            var cameraObject = Root("Main Camera", world, Vector3.zero).gameObject;
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.08f, 0.11f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200;
            camera.fieldOfView = 50;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<RTSCameraController>().ResetView();
        }

        public static void BuildRobot(string name, Transform parent, Vector3 position, Material shell, Material dark, Material accent)

        {
            var robot = Root(name, parent, position);
            Part("Chassis", robot, PrimitiveType.Cube, new Vector3(0, 0.65f, 0), new Vector3(1.3f, 0.7f, 1.6f), shell);
            Part("Cargo tray", robot, PrimitiveType.Cube, new Vector3(0, 1.04f, 0.4f), new Vector3(1.05f, 0.15f, 0.6f), dark);
            Part("Sensor", robot, PrimitiveType.Cube, new Vector3(0, 1.28f, -0.4f), new Vector3(0.85f, 0.55f, 0.65f), shell);
            Part("Eye", robot, PrimitiveType.Cube, new Vector3(0, 1.3f, -0.74f), new Vector3(0.65f, 0.18f, 0.08f), accent, false);
            for (int side = -1; side <= 1; side += 2)
                Part("Track", robot, PrimitiveType.Cube, new Vector3(side * 0.8f, 0.35f, 0), new Vector3(0.38f, 0.6f, 1.9f), dark);
            Part("Identity stripe", robot, PrimitiveType.Cube, new Vector3(0, 1.015f, 0), new Vector3(1.31f, 0.04f, 0.2f), accent, false);
        }

        private static Transform Root(string name, Transform parent, Vector3 position)

        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            return root;
        }

        private static Material Material(string name, Color color)

        {
            return new Material(Shader.Find("Standard"))
            {
                name = name,
                color = color
            };
        }

        private static GameObject Part(
            string name,
            Transform parent,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool solid = true)

        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
                part.GetComponent<Collider>().enabled = false;
            return part;
        }
    }
}
