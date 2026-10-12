using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Protocol
{
    public static class GameSaveService
    {
        private const int Version = 1;
        private const long MaximumSaveBytes = 256 * 1024 * 1024;
        private static ScenarioBootstrap scenario;
        private static SaveObjectIndex index;
        private static JObject pending;
        public static bool IsRestoring { get; private set; }
        public static bool HasPendingLoad => pending != null;
        #if UNITY_EDITOR
        internal static string TestSavePath;
#else
        private const string TestSavePath = null;
#endif
        public static string SavePath => TestSavePath ?? Path.Combine(Application.persistentDataPath, "Saves", "game.protocol");
        public static bool HasSave => ListSaves().Count > 0;

        internal static void BindWorld(ScenarioBootstrap owner)
        {
            scenario = owner;
            index = new SaveObjectIndex(owner.transform);
        }

        public static void ClearPending() => pending = null;

        public sealed class SaveEntry
        {
            public string Id;
            public string Name;
            public DateTime SavedUtc;
            public string DisplayDate => SavedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        }

        public static List<SaveEntry> ListSaves()
        {
            var entries = new List<SaveEntry>();
            string directory = Path.GetDirectoryName(SavePath);
            if (!Directory.Exists(directory)) return entries;
            try
            {
                var paths = new HashSet<string>(Directory.GetFiles(directory, "*.protocol"));
                foreach (string backup in Directory.GetFiles(directory, "*.protocol.backup"))
                    paths.Add(backup.Substring(0, backup.Length - ".backup".Length));
                foreach (string path in paths)
                {
                    string id = Path.GetFileNameWithoutExtension(path);
                    if (id != Path.GetFileNameWithoutExtension(SavePath) && !Guid.TryParseExact(id, "N", out _)) continue;
                    var entry = new SaveEntry
                    {
                        Id = id, Name = "Poprzedni zapis",
                        SavedUtc = File.GetLastWriteTimeUtc(File.Exists(path) ? path : path + ".backup")
                    };
                    try
                    {
                        string metadata = path + ".info";
                        if (File.Exists(metadata) && new FileInfo(metadata).Length < 16384)
                        {
                            var info = JObject.Parse(File.ReadAllText(metadata));
                            entry.Name = (string)info["name"] ?? entry.Name;
                            if (info["savedUtc"]?.Type == JTokenType.Date)
                                entry.SavedUtc = info["savedUtc"].Value<DateTime>().ToUniversalTime();
                            else if (DateTime.TryParse((string)info["savedUtc"], null,
                                System.Globalization.DateTimeStyles.RoundtripKind, out var date))
                                entry.SavedUtc = date.ToUniversalTime();
                        }
                        else if (Guid.TryParseExact(id, "N", out _)) entry.Name = "Zapis " + entry.DisplayDate;
                    }
                    catch (Exception) { entry.Name = "Zapis " + entry.DisplayDate; }
                    entries.Add(entry);
                }
                entries.Sort((a, b) => b.SavedUtc.CompareTo(a.SavedUtc));
            }
            catch (Exception exception) { Debug.LogWarning("Save list: " + exception.Message); }
            return entries;
        }

        private static string ResolveSavePath(string id)
        {
            if (id == Path.GetFileNameWithoutExtension(SavePath)) return SavePath;
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Niepoprawny zapis.");
            return Path.Combine(Path.GetDirectoryName(SavePath), id + ".protocol");
        }

        public static bool TrySave(string name, out string error)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 80 || Array.Exists(name.ToCharArray(), char.IsControl))
            {
                error = "Podaj nazwę zapisu (1–80 znaków).";
                return false;
            }
            string path = ResolveSavePath(Guid.NewGuid().ToString("N"));
            return WriteSave(path, name, out error);
        }

        public static bool TrySave(out string error) => WriteSave(SavePath, null, out error);

        private static bool WriteSave(string path, string name, out string error)
        {
            try
            {
                if (scenario == null || !scenario.IsInitialized || IsRestoring ||
                    SceneManager.GetActiveScene().name != "Stage01")
                    throw new InvalidOperationException("Nie można teraz zapisać rozgrywki.");
                var data = Capture();
                if (name != null) data["name"] = name;
                byte[] bytes = Encoding.UTF8.GetBytes(data.ToString(Formatting.None));
                var envelope = new JObject
                {
                    ["format"] = Version,
                    ["checksum"] = Checksum(bytes),
                    ["data"] = Convert.ToBase64String(bytes)
                };
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var gzip = new GZipStream(file, CompressionMode.Compress, true))
                    {
                        byte[] contents = Encoding.UTF8.GetBytes(envelope.ToString(Formatting.None));
                        gzip.Write(contents, 0, contents.Length);
                    }
                    file.Flush(true);
                }
                ReadFile(temporary);
                if (bytes.LongLength > MaximumSaveBytes / 2) throw new InvalidDataException("Zapis przekracza limit rozmiaru.");
                var info = new JObject { ["name"] = name ?? "Poprzedni zapis", ["savedUtc"] = data["savedUtc"] };
                File.WriteAllText(path + ".info", info.ToString(Formatting.None), Encoding.UTF8);
                if (File.Exists(path)) File.Replace(temporary, path, path + ".backup");
                else File.Move(temporary, path);
                error = "";
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Save Game: " + exception.Message);
                error = "Nie udało się zapisać gry: " + exception.Message;
                return false;
            }
        }

        public static bool PrepareLoad(out string error) => ReadSave(SavePath, out error);

        public static bool PrepareLoad(string id, out string error)
        {
            try { return ReadSave(ResolveSavePath(id), out error); }
            catch (Exception exception) { pending = null; error = exception.Message; return false; }
        }

        private static bool ReadSave(string path, out string error)
        {
            pending = null;
            if (!File.Exists(path) && !File.Exists(path + ".backup")) { error = "Brak zapisanej rozgrywki."; return false; }
            try
            {
                try { pending = ReadFile(path); }
                catch
                {
                    if (!File.Exists(path + ".backup")) throw;
                    pending = ReadFile(path + ".backup");
                }
                if (!Application.CanStreamedLevelBeLoaded("Stage01"))
                    throw new InvalidOperationException("Scenariusz nie jest dostępny w tym buildzie.");
                error = "";
                return true;
            }
            catch (Exception exception)
            {
                pending = null;
                error = "Nie udało się wczytać gry: " + exception.Message;
                return false;
            }
        }

        private static string Checksum(byte[] data)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(data));
        }

        private static JObject ReadFile(string path)
        {
            using (var file = File.OpenRead(path))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            using (var stream = new MemoryStream())
            {
                var buffer = new byte[8192];
                int count;
                while ((count = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (stream.Length + count > MaximumSaveBytes) throw new InvalidDataException("Zapis jest zbyt duży.");
                    stream.Write(buffer, 0, count);
                }
                var envelope = JObject.Parse(Encoding.UTF8.GetString(stream.ToArray()));
                if ((int?)envelope["format"] != Version) throw new InvalidDataException("Nieobsługiwana wersja zapisu.");
                byte[] data = Convert.FromBase64String((string)envelope["data"]);
                if (Checksum(data) != (string)envelope["checksum"]) throw new InvalidDataException("Uszkodzony zapis gry.");
                var result = JObject.Parse(Encoding.UTF8.GetString(data));
                Validate(result);
                return result;
            }
        }

        private static void Validate(JObject data)
        {
            if ((int?)data["version"] != Version || (string)data["scene"] != "Stage01" ||
                !(data["objects"] is JArray objects) || objects.Count == 0 ||
                !(data["targets"] is JArray) || !(data["selection"] is JArray) ||
                (float?)data["timeScale"] == null || (float)data["timeScale"] <= 0 ||
                string.IsNullOrEmpty((string)data["environment"]))
                throw new InvalidDataException("Niekompletny zapis gry.");
            var ids = new HashSet<string>();
            foreach (JObject item in objects)
            {
                if (string.IsNullOrEmpty((string)item["id"]) || !ids.Add((string)item["id"]) ||
                    !(item["states"] is JObject states) || states.Count == 0)
                    throw new InvalidDataException("Niepoprawne obiekty zapisu.");
                foreach (var property in states.Properties())
                    if (property.Name != "site") SaveStateCodec.ComponentType(property.Name);
                if (item["lua"] is JObject lua && lua["execution"] is JObject execution &&
                    ((long?)execution["resumes"] == null || (long)execution["resumes"] < 0 ||
                    !(execution["calls"] is JArray) || string.IsNullOrWhiteSpace((string)execution["source"])))
                    throw new InvalidDataException("Niepoprawny stan programu Lua.");
            }
        }

        internal static JObject Capture()
        {
            Transform root = scenario.transform;
            var objects = new Dictionary<Transform, JObject>();
            foreach (var type in SaveStateCodec.ComponentTypes)
            {
                if (type == typeof(RobotLuaRuntime)) continue;
                foreach (Component component in root.GetComponentsInChildren(type, true))
                {
                    Transform transform = component.transform;
                    if (!objects.TryGetValue(transform, out var item))
                    {
                        string kind = transform.GetComponent<RobotController>() != null ? "robot"
                            : transform.GetComponent<SolarPanel>() != null ? "solar"
                            : transform.GetComponent<ChargingStationStructure>() != null ? "charger"
                            : transform.GetComponent<BatteryStorage>() != null ? "battery" : "static";
                        item = new JObject
                        {
                            ["id"] = index.Id(transform), ["kind"] = kind, ["name"] = transform.name,
                            ["position"] = Vector(transform.position), ["rotation"] = Rotation(transform.rotation),
                            ["active"] = transform.gameObject.activeSelf, ["states"] = new JObject()
                        };
                        objects.Add(transform, item);
                    }
                    ((JObject)item["states"])[type.Name] = new JObject
                    {
                        ["enabled"] = !(component is Behaviour behaviour) || behaviour.enabled,
                        ["fields"] = SaveStateCodec.Capture(component, index)
                    };
                }
            }
            foreach (var site in root.GetComponentsInChildren<ConstructionSite>(true))
                objects[site.transform] = new JObject
                {
                    ["id"] = index.Id(site.transform), ["kind"] = "site", ["name"] = site.name,
                    ["position"] = Vector(site.transform.position), ["rotation"] = Rotation(site.transform.rotation),
                    ["active"] = site.gameObject.activeSelf, ["recipe"] = JsonUtility.ToJson(site.Recipe),
                    ["progress"] = site.Progress,
                    ["states"] = new JObject { ["site"] = new JObject() }
                };

            var targets = new JArray();
            var targetIds = new HashSet<string>();
            foreach (var robot in root.GetComponentsInChildren<RobotController>(true))
            {
                JObject item = objects[robot.transform];
                item["unitType"] = robot.UnitType;
                item["mineTargetId"] = robot.MineDestination != null ? index.Id(robot.MineDestination) : null;
                item["baseTargetId"] = index.Id(robot.BaseDestination);
                item["mineTarget"] = robot.MineDestination != null ? Vector(robot.MineDestination.position) : Vector(Vector3.zero);
                item["baseTarget"] = Vector(robot.BaseDestination.position);
                item["movement"] = new JObject
                {
                    ["result"] = (int)robot.Movement.Result, ["message"] = robot.Movement.Message,
                    ["target"] = Vector(robot.Movement.SavedTarget)
                };
                foreach (Transform destination in new[] { robot.MineDestination, robot.BaseDestination })
                {
                    if (destination == null || !targetIds.Add(index.Id(destination))) continue;
                    targets.Add(new JObject
                    {
                        ["id"] = index.Id(destination), ["parent"] = index.Id(destination.parent),
                        ["name"] = destination.name, ["position"] = Vector(destination.position)
                    });
                }
            }
            foreach (var runtime in root.GetComponentsInChildren<RobotLuaRuntime>(true))
                if (objects.TryGetValue(runtime.transform, out var item)) item["lua"] = runtime.CaptureGameState(index);

            var selection = root.GetComponent<SelectionManager>();
            var selected = new JArray();
            foreach (var robot in selection.SelectedRobots) selected.Add(index.Id(robot.transform));
            var records = new JArray();
            foreach (var record in objects.Values) records.Add(record);
            var data = new JObject
            {
                ["version"] = Version, ["scene"] = "Stage01", ["savedUtc"] = DateTime.UtcNow.ToString("O"),
                ["timeScale"] = Mathf.Max(.01f, root.GetComponent<PauseMenu>().SimulationTimeScale),
                ["environment"] = JsonUtility.ToJson(root.GetComponentInChildren<PlanetEnvironmentSystem>().Profile),
                ["objects"] = records, ["targets"] = targets, ["selection"] = selected,
                ["structure"] = selection.SelectedStructure != null ? index.Id(selection.SelectedStructure.transform) : null,
                ["panel"] = RightHudPanels.IsRequested(RightHudPanel.Software) ? "Software"
                    : RightHudPanels.IsRequested(RightHudPanel.Environment) ? "Environment" : "None"
            };
            // Sites are reconstructed from recipes rather than ordinary components.
            return data;
        }

        public static IEnumerator RestorePending()
        {
            JObject data = pending ?? throw new InvalidOperationException("Brak zapisu do odtworzenia.");
            pending = null;
            return Restore(data);
        }

        internal static IEnumerator Restore(JObject data)
        {
            if (scenario == null || !scenario.IsInitialized) throw new InvalidOperationException("Scenario not initialized.");
            IsRestoring = true;
            float previousScale = Time.timeScale;
            Time.timeScale = 0;
            bool restored = false;
            try
            {
                Transform root = scenario.transform, world = root.Find("World");
                var home = world.GetComponentInChildren<BaseBuilding>();
                var grid = home.PowerGrid;
                var environment = world.GetComponent<PlanetEnvironmentSystem>();
                foreach (var runtime in root.GetComponentsInChildren<RobotLuaRuntime>(true)) runtime.StopProgram();
                var removals = new HashSet<GameObject>();
                foreach (var robot in root.GetComponentsInChildren<RobotController>(true)) removals.Add(robot.gameObject);
                foreach (var solar in root.GetComponentsInChildren<SolarPanel>(true)) removals.Add(solar.gameObject);
                foreach (var charger in root.GetComponentsInChildren<ChargingStationStructure>(true)) removals.Add(charger.gameObject);
                foreach (var battery in root.GetComponentsInChildren<BatteryStorage>(true)) removals.Add(battery.gameObject);
                foreach (var site in root.GetComponentsInChildren<ConstructionSite>(true)) removals.Add(site.gameObject);
                foreach (var owner in removals) { owner.SetActive(false); UnityEngine.Object.Destroy(owner); }
                yield return null;

                foreach (JObject target in (JArray)data["targets"])
                {
                    if (!index.TryResolve((string)target["id"], out var destination))
                        destination = new GameObject((string)target["name"]).transform;
                    destination.SetParent(index.Resolve((string)target["parent"]), false);
                    destination.position = ReadVector(target["position"]);
                    index.Bind((string)target["id"], destination);
                }

                foreach (JObject item in (JArray)data["objects"])
                {
                    Transform transform;
                    Vector3 position = ReadVector(item["position"]);
                    switch ((string)item["kind"])
                    {
                        case "robot":
                            if (!Enum.TryParse((string)item["unitType"], out RobotKind kind))
                                throw new InvalidOperationException("Unknown robot type in save.");
                            string name = (string)item["name"];
                            int number = int.Parse(name.Substring(name.LastIndexOf('-') + 1));
                            transform = HarvesterFactory.CreateAt(home, position, number, home.Recipe(kind),
                                ReadVector(item["mineTarget"]), ReadVector(item["baseTarget"]),
                                item["mineTargetId"].Type != JTokenType.Null ? index.Resolve((string)item["mineTargetId"]) : null,
                                index.Resolve((string)item["baseTargetId"])).transform;
                            break;
                        case "solar": transform = SolarPanel.Create(world, position, grid, environment).transform; break;
                        case "charger": transform = ChargingStationStructure.Create(world, position).transform; break;
                        case "battery": transform = BatteryStorage.Create(world, position, grid).transform; break;
                        case "site":
                            transform = ConstructionSite.Create(world, position, JsonUtility.FromJson<ConstructionRecipe>((string)item["recipe"])).transform;
                            transform.GetComponent<ConstructionSite>().SetProgress((float)item["progress"]);
                            break;
                        default: transform = index.Resolve((string)item["id"]); break;
                    }
                    index.Bind((string)item["id"], transform);
                    transform.name = (string)item["name"];
                    transform.SetPositionAndRotation(position, ReadRotation(item["rotation"]));
                }
                // Let Start registrations settle before restoring saved ownership and battery levels.
                yield return null;
                foreach (JObject item in (JArray)data["objects"])
                {
                    if (!(item["lua"] is JObject lua)) continue;
                    var runtime = index.Resolve((string)item["id"]).GetComponent<RobotLuaRuntime>();
                    var replay = runtime.RestoreGameState(lua, index);
                    while (replay.MoveNext()) yield return replay.Current;
                }
                foreach (JObject item in (JArray)data["objects"])
                {
                    var transform = index.Resolve((string)item["id"]);
                    if ((string)item["kind"] != "site")
                    {
                        foreach (var property in ((JObject)item["states"]).Properties())
                        {
                            Component component = transform.GetComponent(SaveStateCodec.ComponentType(property.Name));
                            if (component == null) throw new InvalidOperationException("Missing save component: " + property.Name);
                            SaveStateCodec.Restore(component, (JObject)property.Value["fields"], index);
                            if (component is Behaviour behaviour) behaviour.enabled = (bool)property.Value["enabled"];
                        }
                    }
                    if ((bool)item["active"] != transform.gameObject.activeSelf)
                    {
                        if (transform.TryGetComponent<TreeResource>(out _) && !(bool)item["active"])
                            root.GetComponent<FogOfWar>().TreeRemoved(transform.position);
                        transform.gameObject.SetActive((bool)item["active"]);
                    }
                }
                environment.RestoreProfile((string)data["environment"]);
                environment.Refresh();
                world.GetComponent<AlienWorldLighting>().RefreshLighting();
                Physics.SyncTransforms();
                yield return null;
                yield return null;
                foreach (JObject item in (JArray)data["objects"])
                {
                    if (!(item["movement"] is JObject movement)) continue;
                    index.Resolve((string)item["id"]).GetComponent<RobotMovement>().RestoreGameState(
                        (MovementResult)(int)movement["result"], (string)movement["message"], ReadVector(movement["target"]));
                }
                var fog = root.GetComponent<FogOfWar>();
                fog.RefreshUnits();
                fog.Refresh();
                root.GetComponent<MinimapPanel>().Initialize();
                root.GetComponent<SelectionPortraits>().Refresh();
                var robots = new List<RobotController>();
                foreach (var id in (JArray)data["selection"]) robots.Add(index.Resolve((string)id).GetComponent<RobotController>());
                var structure = data["structure"].Type != JTokenType.Null
                    ? index.Resolve((string)data["structure"]).GetComponent<SelectableStructure>() : null;
                root.GetComponent<SelectionManager>().RestoreSelection(robots, structure);
                if ((string)data["panel"] == "Software") root.GetComponent<RobotProgrammingPanel>().ToggleSelection();
                else if ((string)data["panel"] == "Environment") RightHudPanels.Open(RightHudPanel.Environment);
                grid.RefreshChargingAllocation();
                Time.timeScale = (float)data["timeScale"];
                restored = true;
            }
            finally
            {
                IsRestoring = false;
                if (!restored) Time.timeScale = previousScale;
            }
        }

        private static JArray Vector(Vector3 value) => new JArray(value.x, value.y, value.z);
        private static Vector3 ReadVector(JToken value) => new Vector3((float)value[0], (float)value[1], (float)value[2]);
        private static JArray Rotation(Quaternion value) => new JArray(value.x, value.y, value.z, value.w);
        private static Quaternion ReadRotation(JToken value) => new Quaternion((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
    }
}
