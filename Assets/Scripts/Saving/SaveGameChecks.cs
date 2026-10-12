#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Protocol
{
    public sealed class SaveGameChecks : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        public static string Result { get; private set; } = "Not run";
        private string testPath;
        private readonly System.Collections.Generic.List<string> namedTestPaths = new System.Collections.Generic.List<string>();

        [UnityEditor.MenuItem("Protocol/Validate Save Game")]
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var owner = new GameObject("Save game validation");
            DontDestroyOnLoad(owner);
            var checks = owner.AddComponent<SaveGameChecks>();
            Result = "Running";
            checks.StartCoroutine(checks.Guard(checks.Run()));
        }

        private IEnumerator Guard(IEnumerator test)
        {
            while (true)
            {
                bool more = false;
                object current = null;
                try { more = test.MoveNext(); if (more) current = test.Current; }
                catch (Exception exception)
                {
                    Result = "FAILED: " + exception;
                    Debug.LogError(Result);
                    Finish();
                    yield break;
                }
                if (!more) break;
                yield return current;
            }
            Result = "PASS: full scene roundtrip, Lua continuation, independent batteries, resources, production, construction, charging, forest, fog, camera, selection, atomic save and backup, named slots, duplicate names, selected load and safe paths, main menu list, row selection and loading screen.";
            Debug.Log(Result);
            Finish();
        }

        private IEnumerator Run()
        {
            var root = FindAnyObjectByType<ScenarioBootstrap>();
            root.GetComponent<PauseMenu>().Resume();
            var home = root.GetComponentInChildren<BaseBuilding>();
            var resources = root.GetComponent<ResourceManager>();
            resources.Add(ResourceKind.Wood, 2000);
            resources.AddIron(37);
            var robots = root.GetComponentsInChildren<RobotController>();
            robots[0].LuaRuntime.SourceCode = "local mine = robot.findNearestMine()\nlocal n = 21\nrobot.moveTo(mine)\nrobot.mine(mine)\nprint('RESTORED_MINING:' .. (n * 2))\nrobot.wait(100)";
            Require(robots[0].LuaRuntime.RunProgram(), "Mining program start");
            robots[1].Energy.Consume(37);
            robots[1].Inventory.TryAddUnit(ResourceKind.Wood);
            robots[1].LuaRuntime.SourceCode = "local n = math.random(100, 999)\nlocal wood = economy.wood + economy.wood + economy.wood\nprint('BEFORE:' .. n .. ':' .. wood)\nrobot.wait(100)\nprint('AFTER:' .. n .. ':' .. wood)\nrobot.wait(100)";
            Require(robots[1].LuaRuntime.RunProgram(), "Waiting program start");
            float deadline = Time.realtimeSinceStartup + 30;
            while (robots[0].LuaRuntime.ActionStatus != "Mining" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Require(robots[0].LuaRuntime.ActionStatus == "Mining", "Arrive for mining: " + robots[0].LuaRuntime.Error);

            // Manufacture the infrastructure test fixture without spending saved resources.
            var world = root.transform.Find("World");
            var solar = SolarPanel.Create(world, new Vector3(30, ScenarioLandscape.HeightAt(30, 0), 0), home.PowerGrid,
                world.GetComponent<PlanetEnvironmentSystem>());
            BatteryStorage.Create(world, new Vector3(40, ScenarioLandscape.HeightAt(40, 0), 0), home.PowerGrid);
            var station = ChargingStationStructure.Create(world, new Vector3(25, ScenarioLandscape.HeightAt(25, -20), -20));
            var chargingRobot = HarvesterFactory.CreateAt(home,
                new Vector3(25, ScenarioLandscape.HeightAt(25, -25), -25), 3, home.Recipe(RobotKind.Harvester),
                robots[0].MineDestination.position, robots[0].BaseDestination.position);
            chargingRobot.Energy.Consume(70);
            Require(chargingRobot.GetComponent<RobotCharging>().TryBegin(station), "Charging start");

            var assembler = HarvesterFactory.CreateAt(home,
                new Vector3(50, ScenarioLandscape.HeightAt(50, -15), -15), 1, home.Recipe(RobotKind.Assembler),
                Vector3.zero, robots[0].BaseDestination.position);
            var construction = assembler.GetComponent<AssemblerConstruction>();
            var recipe = new ConstructionRecipe { Kind = ConstructionKind.BatteryStorage, DurationSeconds = 60 };
            var site = ConstructionSite.Create(world, new Vector3(50, ScenarioLandscape.HeightAt(50, -10), -10), recipe);
            site.SetProgress(.25f);
            Set(construction, "site", site);
            Set(construction, "resources", resources);
            Set(construction, "elapsed", 15d);
            Set(construction, "paidWood", 50);
            Set(construction, "travelling", false);
            Set(construction, "approach", assembler.transform.position);
            assembler.Energy.Consume(11);

            Require(HarvesterFactory.TryCreate(home, 4, home.Recipe(RobotKind.Harvester), out var manualRobot), "Manual robot spawn");
            Require(manualRobot.HarvestCycle.StartCycle(), "Manual harvest cycle");

            var tree = world.GetComponentsInChildren<TreeResource>()[5];
            Set(tree, "<WoodRemaining>k__BackingField", 0);
            root.GetComponent<FogOfWar>().TreeRemoved(tree.transform.position);
            tree.gameObject.SetActive(false);
            root.GetComponent<SelectionManager>().Select(robots[1]);
            root.GetComponent<RobotProgrammingPanel>().Open(robots[1].LuaRuntime);
            root.GetComponentInChildren<RTSCameraController>().FocusOn(new Vector3(15, 0, 10));
            var baseRuntime = home.GetComponent<RobotLuaRuntime>();
            baseRuntime.SourceCode = "local n = 42\nbuildAssembler()\nprint('PRODUCTION:' .. n)\ncoroutine.yield()";
            Require(baseRuntime.RunProgram(), "Base program start");
            yield return null;
            Require(home.IsProducing, "Production began");
            home.AdvanceProduction(2);
            root.GetComponent<FogOfWar>().RefreshUnits();
            root.GetComponent<FogOfWar>().Refresh();
            root.GetComponent<PauseMenu>().Open();

            testPath = Path.Combine(Application.temporaryCachePath, "Protocol-SaveGame-Validation-" + Guid.NewGuid().ToString("N"), "game.protocol");
            GameSaveService.TestSavePath = testPath;
            Require(GameSaveService.TrySave(out string error), error);
            JObject expected = GameSaveService.Capture();
            int savedIron = resources.Iron;
            Require(!GameSaveService.TrySave("   ", out _), "Empty save name rejected");
            Require(GameSaveService.TrySave("Kolonia / północ", out error), error);
            var firstNamed = GameSaveService.ListSaves().Find(entry => entry.Name == "Kolonia / północ");
            namedTestPaths.Add(Path.Combine(Path.GetDirectoryName(testPath), firstNamed.Id + ".protocol"));
            resources.AddIron(1);
            Require(GameSaveService.TrySave("Kolonia / północ", out error), error);
            var entries = GameSaveService.ListSaves();
            var secondNamed = entries.Find(entry => entry.Name == "Kolonia / północ" && entry.Id != firstNamed.Id);
            namedTestPaths.Add(Path.Combine(Path.GetDirectoryName(testPath), secondNamed.Id + ".protocol"));
            Require(entries.Count == 3 && firstNamed.Id != secondNamed.Id, "Duplicate names have independent files");
            Require(secondNamed.Name == "Kolonia / północ", "Unicode name preserved");
            Require(secondNamed.SavedUtc > firstNamed.SavedUtc && entries[0].Id == secondNamed.Id, "Newest save first");
            Require(GameSaveService.PrepareLoad(secondNamed.Id, out error), error);
            var pendingField = typeof(GameSaveService).GetField("pending", BindingFlags.NonPublic | BindingFlags.Static);
            Compare(GameSaveService.Capture(), (JObject)pendingField.GetValue(null));
            Require(GameSaveService.PrepareLoad(firstNamed.Id, out error), error);
            Compare(expected, (JObject)pendingField.GetValue(null));
            Require(!GameSaveService.PrepareLoad("../escape", out _), "Path traversal rejected");
            Require(GameSaveService.TrySave(out error), error);
            Require(File.Exists(testPath + ".backup"), "Backup created");
            byte[] validFile = File.ReadAllBytes(testPath);
            File.WriteAllText(testPath, "corrupt");
            Require(GameSaveService.PrepareLoad(out error), "Backup recovery: " + error);
            File.WriteAllBytes(testPath, validFile);
            Require(GameSaveService.PrepareLoad(firstNamed.Id, out error), error);

            root.GetComponent<PauseMenu>().Resume();
            Time.timeScale = 0;
            yield return SceneManager.LoadSceneAsync("Stage01");
            Time.timeScale = 0;
            yield return null;
            var restore = GameSaveService.RestorePending();
            while (restore.MoveNext()) yield return restore.Current;
            Time.timeScale = 0;
            JObject actual = GameSaveService.Capture();
            Compare(expected, actual);
            root = FindAnyObjectByType<ScenarioBootstrap>();
            home = root.GetComponentInChildren<BaseBuilding>();
            Require(root.GetComponentInChildren<ColonyPowerGrid>().BatteryCapacityKWh == 300, "Storage capacity");
            Require(root.GetComponentInChildren<ChargingStationStructure>().AssignedRobot != null, "Dock ownership");
            Require(root.GetComponentInChildren<ChargingStationStructure>().CurrentChargingKW == 20, "Charging power");
            Require(home.IsProducing && home.ProductionProgress > .15f, "Production progress retained");
            Require(root.GetComponent<RobotProgrammingPanel>().CurrentRuntime.name == "Harvester-02", "Software context");
            var restoredRobots = root.GetComponentsInChildren<RobotController>();
            RobotLuaRuntime waiter = null;
            foreach (var robot in restoredRobots)
                if (robot.name == "Harvester-02") waiter = robot.LuaRuntime;
            Require(waiter != null && waiter.State == LuaProgramState.Running, "Lua still running");
            var savedOutput = waiter.Output.Trim();
            var pendingApi = typeof(RobotLuaRuntime).GetField("api", Private).GetValue(waiter);
            Set(pendingApi, "elapsed", 100f);
            Time.timeScale = 1;
            yield return null;
            Time.timeScale = 0;
            Require(waiter.Output.Contains(savedOutput.Replace("BEFORE:", "AFTER:")), "Lua locals/random/economy retained");

            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return new WaitForSecondsRealtime(.5f);
            var menu = FindAnyObjectByType<MainMenu>();
            menu.LoadGame();
            Require(menu.Page == MenuPage.Saves, "Load Game opens save list");
            var view = menu.GetComponent<MainMenuView>();
            var rows = (System.Collections.Generic.List<UnityEngine.UI.Button>)
                typeof(MainMenuView).GetField("saveRows", Private).GetValue(view);
            var listed = GameSaveService.ListSaves();
            Require(rows.Count == listed.Count && rows.Count == 3, "Menu lists all saves");
            int selectedIndex = listed.FindIndex(entry => entry.Id == firstNamed.Id);
            Require(selectedIndex >= 0 && rows[selectedIndex].GetComponentInChildren<UnityEngine.UI.Text>().text.Contains(firstNamed.Name),
                "Save name displayed");
            var loadButton = (UnityEngine.UI.Button)typeof(MainMenuView).GetField("loadSelected", Private).GetValue(view);
            Require(!loadButton.interactable, "Load disabled before selecting");
            rows[selectedIndex].onClick.Invoke();
            Require(loadButton.interactable, "Selection enables Load");
            yield return null;
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", "Artifacts"));
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath, "..", "Artifacts", "NamedSavesList.png"));
            yield return null;
            loadButton.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 45;
            while ((SceneManager.GetActiveScene().name != "Stage01" || ScenarioLoadingScreen.IsVisible) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Require(SceneManager.GetActiveScene().name == "Stage01" && !ScenarioLoadingScreen.IsVisible, "Selected menu save loaded");
            Time.timeScale = 0;
            root = FindAnyObjectByType<ScenarioBootstrap>();
            Require(root.GetComponent<ResourceManager>().Iron == savedIron, "Menu loads selected state, not most recent");
        }

        private static void Compare(JObject expected, JObject actual)
        {
            foreach (JObject before in (JArray)expected["objects"])
            {
                JObject after = null;
                foreach (JObject candidate in (JArray)actual["objects"])
                    if ((string)candidate["id"] == (string)before["id"]) after = candidate;
                Require(after != null, "Missing object: " + before["name"]);
                foreach (string key in new[] { "name", "active", "position", "rotation", "states", "recipe", "progress", "lua" })
                    if (before[key] != null)
                        Require(Equal(before[key], after[key]), "Changed state: " + before["name"] + "." + key);
            }
            Require(((JArray)actual["objects"]).Count == ((JArray)expected["objects"]).Count, "No duplicated objects");
        }

        private static bool Equal(JToken a, JToken b)
        {
            if (b == null) return false;
            if ((a.Type == JTokenType.Float || a.Type == JTokenType.Integer) &&
                (b.Type == JTokenType.Float || b.Type == JTokenType.Integer))
                return Math.Abs((double)a - (double)b) <= 0.000001 * Math.Max(1, Math.Abs((double)a));
            if (a is JObject ao && b is JObject bo)
            {
                if (ao.Count != bo.Count) return false;
                foreach (var property in ao.Properties()) if (!Equal(property.Value, bo[property.Name])) return false;
                return true;
            }
            if (a is JArray aa && b is JArray ba)
            {
                if (aa.Count != ba.Count) return false;
                for (int i = 0; i < aa.Count; i++) if (!Equal(aa[i], ba[i])) return false;
                return true;
            }
            return JToken.DeepEquals(a, b);
        }

        private void Finish()
        {
            GameSaveService.TestSavePath = null;
            GameSaveService.ClearPending();
            Time.timeScale = 1;
            if (testPath != null)
                foreach (string suffix in new[] { "", ".backup", ".tmp", ".info" })
                    if (File.Exists(testPath + suffix)) File.Delete(testPath + suffix);
            foreach (string path in namedTestPaths)
                foreach (string suffix in new[] { "", ".info", ".tmp" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            Destroy(gameObject);
        }

        private static void Set(object value, string name, object data) => value.GetType().GetField(name, Private).SetValue(value, data);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
