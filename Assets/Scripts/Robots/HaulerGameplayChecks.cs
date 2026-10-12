#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Protocol
{
    public sealed class HaulerGameplayChecks : MonoBehaviour
    {
        public static string Result { get; private set; } = "Not run";
        private readonly List<string> passed = new List<string>();
        private string savePath;

        [UnityEditor.MenuItem("Protocol/Validate Hauler Gameplay")]
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run Stage01 in Play Mode.");
            var owner = new GameObject("Hauler gameplay validation");
            DontDestroyOnLoad(owner);
            var checks = owner.AddComponent<HaulerGameplayChecks>();
            Result = "Running";
            checks.StartCoroutine(checks.Guard(checks.Run()));
        }

        private IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null;
                bool more;
                try
                {
                    more = stack.Peek().MoveNext();
                    if (more) current = stack.Peek().Current;
                }
                catch (Exception exception)
                {
                    Result = "FAILED after [" + string.Join(", ", passed) + "]: " + exception;
                    Debug.LogError(Result);
                    Finish();
                    yield break;
                }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator child) { stack.Push(child); continue; }
                yield return current;
            }
            Result = "PASS: " + string.Join(", ", passed);
            Debug.Log(Result);
            Finish();
        }

        private void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            passed.Add(message);
            Result = "Running: " + message;
        }

        private IEnumerator Until(Func<bool> ready, string message, float timeout = 45)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Check(ready(), message);
        }

        private RobotController Spawn(BaseBuilding home, RobotKind kind, int number)
        {
            Check(HarvesterFactory.TryCreate(home, number, home.Recipe(kind), out var robot), "Spawn " + kind);
            return robot;
        }

        private IEnumerator Run()
        {
            var root = FindAnyObjectByType<ScenarioBootstrap>();
            if (root == null)
            {
                yield return SceneManager.LoadSceneAsync("Stage01");
                yield return null;
                root = FindAnyObjectByType<ScenarioBootstrap>();
            }
            root.GetComponent<PauseMenu>().Resume();
            Time.timeScale = 4;
            var home = root.GetComponentInChildren<BaseBuilding>();
            var grid = home.PowerGrid;
            var resources = root.GetComponent<ResourceManager>();
            var selection = root.GetComponent<SelectionManager>();
            var panel = root.GetComponent<RobotProgrammingPanel>();
            var harvester = root.GetComponentsInChildren<RobotController>()[0];
            resources.Add(ResourceKind.Wood, 5000);
            grid.SetGeneration(this, 500);
            harvester.LuaRuntime.SourceCode = "robot.wait(1000)";
            Check(harvester.LuaRuntime.RunProgram(), "Run Harvester Lua");
            panel.Open(harvester.LuaRuntime);
            string code = harvester.LuaRuntime.SourceCode;
            panel.ApplyCode("replaced");
            Check(panel.IsReadOnly && harvester.LuaRuntime.SourceCode == code, "A: running editor cannot apply edits");
            panel.Close();
            Check(harvester.LuaRuntime.State == LuaProgramState.Running, "Closing panel preserves Lua");
            var assembler = Spawn(home, RobotKind.Assembler, 1);
            selection.Select(assembler);
            panel.Open(assembler.LuaRuntime);
            Check(!panel.IsReadOnly, "B: stopped Assembler remains editable");
            selection.Select(harvester);
            Check(panel.IsReadOnly, "Selection restores running lock");
            panel.StopTargets();
            Check(!panel.IsReadOnly, "Stop unlocks editor");
            harvester.LuaRuntime.SourceCode = "error('expected validation error')";
            harvester.LuaRuntime.RunProgram();
            yield return Until(() => harvester.LuaRuntime.State != LuaProgramState.Running, "Lua error executes");
            Check(harvester.LuaRuntime.State == LuaProgramState.Error && !panel.IsReadOnly, "Lua error unlocks editor");
            harvester.LuaRuntime.StopProgram();
            panel.Close();

            int beforeWood = resources.Wood;
            Check(home.TryBeginProduction(RobotKind.Hauler), "C: Hauler production begins");
            Check(resources.Wood == beforeWood - 200 && grid.TotalConsumptionKW >= 40, "Production cost and power");
            Check(!home.AdvanceProduction(15) && home.ProductionProgress < 1, "Timed production");
            Check(home.AdvanceProduction(1), "Hauler deployment");
            var hauler = Array.Find(root.GetComponentsInChildren<RobotController>(), r => r.UnitType == "Hauler");
            Check(hauler != null && hauler.Energy.CapacityKWh == 10 && hauler.Energy.Percentage == 100, "Hauler battery and type");
            var towing = hauler.GetComponent<HaulerTowing>();
            harvester.Energy.Consume(100);
            Check(!towing.TryBegin(harvester) && !harvester.Movement.IsBeingTowed, "I: no station fails cleanly");
            var world = root.transform.Find("World");
            var station = ChargingStationStructure.Create(world, new Vector3(25, ScenarioLandscape.HeightAt(25, -20), -20));
            yield return null;
            Check(towing.TryBegin(harvester), "D: Tow Harvester begins");
            var other = Spawn(home, RobotKind.Hauler, 2);
            Check(!other.GetComponent<HaulerTowing>().TryBegin(harvester), "Exclusive robot ownership");
            yield return Until(() => towing.IsTransporting || !towing.IsBusy, "Pickup finishes");
            Check(towing.IsTransporting, "Hauler attaches victim");
            double batteryBefore = hauler.Energy.StoredEnergyKWh;
            Vector3 targetBefore = harvester.transform.position;
            yield return Until(() => hauler.Energy.StoredEnergyKWh < batteryBefore &&
                Vector3.Distance(targetBefore, harvester.transform.position) > .01f, "Transport starts moving", 10);
            Check(hauler.Energy.StoredEnergyKWh < batteryBefore && harvester.Energy.IsDepleted &&
                Vector3.Distance(targetBefore, harvester.transform.position) > .01f, "Transport movement and independent energy");
            root.GetComponent<PauseMenu>().Open();
            yield return null;
            Vector3 paused = hauler.transform.position;
            double pausedBattery = hauler.Energy.StoredEnergyKWh;
            yield return new WaitForSecondsRealtime(.1f);
            Check(hauler.transform.position == paused && hauler.Energy.StoredEnergyKWh == pausedBattery, "Pause freezes towing and drain");
            root.GetComponent<PauseMenu>().Resume();
            towing.Cancel();
            Check(!towing.IsBusy && !harvester.Movement.IsBeingTowed && station.IsAvailable, "H: cancel releases robot and station");
            yield return null;
            Check(NavMesh.SamplePosition(harvester.transform.position, out _, 1.5f, NavMesh.AllAreas) ||
                harvester.GetComponent<NavMeshObstacle>().enabled, "Detached robot safely parked");

            Check(towing.TryBegin(harvester), "Restart recovery");
            yield return Until(() => !towing.IsBusy, "Recovery completes", 70);
            Check(towing.Succeeded && harvester.GetComponent<RobotCharging>().IsCharging &&
                !harvester.Movement.IsBeingTowed, "Harvester handed to charging");
            grid.AdvanceHours(.01);
            Check(harvester.Energy.Percentage > 0, "Harvester receives charging energy");
            grid.AdvanceHours(.5);
            Check(harvester.Energy.Percentage == 100 && station.IsAvailable, "Charging completes and releases dock");

            assembler.Energy.Consume(100);
            hauler.Energy.Recharge(100);
            Check(towing.TryBegin(assembler), "E: Tow Assembler begins");
            yield return Until(() => towing.IsTransporting || !towing.IsBusy, "Assembler pickup", 70);
            Check(towing.IsTransporting, "Assembler attached");
            hauler.LuaRuntime.SourceCode = "print('test')";
            Time.timeScale = 0;
            savePath = Path.Combine(Application.dataPath, "..", "Artifacts", "HaulerValidation-" + Guid.NewGuid().ToString("N"), "game.protocol");
            GameSaveService.TestSavePath = savePath;
            Check(GameSaveService.TrySave(out string error), "Save active tow: " + error);
            Check(GameSaveService.PrepareLoad(out error), "Read active tow: " + error);
            yield return SceneManager.LoadSceneAsync("Stage01");
            Time.timeScale = 0;
            yield return null;
            yield return GameSaveService.RestorePending();
            root = FindAnyObjectByType<ScenarioBootstrap>();
            home = root.GetComponentInChildren<BaseBuilding>();
            grid = home.PowerGrid;
            grid.SetGeneration(this, 500);
            hauler = Array.Find(root.GetComponentsInChildren<RobotController>(), r => r.name == "Hauler-01");
            assembler = Array.Find(root.GetComponentsInChildren<RobotController>(), r => r.name == "Assembler-01");
            towing = hauler.GetComponent<HaulerTowing>();
            station = assembler.GetComponent<RobotCharging>().Station;
            Check(hauler.UnitType == "Hauler" && towing.IsTransporting && assembler.Movement.IsBeingTowed &&
                station.AssignedRobot == assembler.GetComponent<RobotCharging>(), "Save/load restores Hauler, victim and reservation");
            Time.timeScale = 4;
            yield return Until(() => !towing.IsBusy, "Restored tow completes", 70);
            Check(towing.Succeeded && assembler.GetComponent<RobotCharging>().IsCharging, "Assembler recovery handoff");
            Check(grid.TotalConsumptionKW >= 31 && station.CurrentChargingKW == 20, "F: Assembler charges at 20 kW plus 1 kW idle");
            double startBattery = assembler.Energy.StoredEnergyKWh;
            grid.AdvanceHours(.1);
            Check(Math.Abs(assembler.Energy.StoredEnergyKWh - startBattery - 2) < .001, "Assembler receives 2 kWh in 0.1h");
            grid.AdvanceHours(.5);
            Check(assembler.Energy.Percentage == 100 && station.IsAvailable, "Assembler full and dock released");
            Check(assembler.GetComponent<AssemblerConstruction>().PerformWork(1) > 0, "Assembler can work after charging");

            assembler.Energy.Consume(20);
            Check(assembler.GetComponent<RobotCharging>().TryBegin(station), "Direct Assembler charging");
            yield return Until(() => assembler.GetComponent<RobotCharging>().IsCharging, "Assembler reaches dock");
            grid.AdvanceHours(.5);
            hauler.Energy.Consume(40);
            Check(hauler.GetComponent<RobotCharging>().TryBegin(station), "G: Hauler charges");
            yield return Until(() => hauler.GetComponent<RobotCharging>().IsCharging, "Hauler reaches dock");
            grid.AdvanceHours(.5);
            Check(hauler.Energy.Percentage == 100 && station.IsAvailable, "Hauler finishes charge");

            assembler.Energy.Consume(100);
            hauler.LuaRuntime.SourceCode = "local r = robot.findNearestDisabledRobot()\nrobot.tow(r)\nprint('TOW_DONE')";
            Check(hauler.LuaRuntime.RunProgram(), "Lua tow starts");
            yield return Until(() => hauler.LuaRuntime.State != LuaProgramState.Running, "Lua tow resumes", 90);
            Check(hauler.LuaRuntime.State == LuaProgramState.Completed && hauler.LuaRuntime.Output.Contains("TOW_DONE"), "Lua async tow completes");
            grid.AdvanceHours(.5);
            assembler.Energy.Consume(100);
            Check(towing.TryBegin(assembler), "Recovery before power loss");
            hauler.Energy.Consume(100);
            yield return null;
            Check(!towing.IsBusy && !assembler.Movement.IsBeingTowed && station.IsAvailable, "Hauler power loss releases ownership");
            hauler.Energy.Recharge(100);
            Check(towing.TryBegin(assembler), "Recovery before station loss");
            station.gameObject.SetActive(false);
            yield return null;
            Check(!towing.IsBusy && !assembler.Movement.IsBeingTowed && station.AssignedRobot == null, "Station loss releases ownership");
        }

        private void Finish()
        {
            GameSaveService.TestSavePath = null;
            GameSaveService.ClearPending();
            Time.timeScale = 1;
            string report = Path.Combine(Application.dataPath, "..", "Artifacts", "HaulerGameplayChecks.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, Result);
            Destroy(gameObject);
        }
    }
}
#endif
