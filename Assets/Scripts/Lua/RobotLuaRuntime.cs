using System;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using UnityEngine;

namespace Protocol
{
    public enum LuaProgramState
    {
        Stopped,
        Running,
        Completed,
        Error
    }

    public sealed class RobotLuaRuntime : MonoBehaviour
    {
        public const int MaxCodeLength = 16000;
        [SerializeField, TextArea(6, 16)]
        private string sourceCode;
        private RobotProgramStorage storage;
        private string savedSourceCode;

        public bool HasUnsavedChanges => sourceCode != savedSourceCode;

        public string StorageMessage { get; private set; } = "";

        public bool StorageError { get; private set; }

        public string SavedFilePath => storage != null ? storage.FilePath : "";

        private Script script;
        private DynValue routine;
        private int forcedYields;
        private readonly System.Diagnostics.Stopwatch executionClock = new System.Diagnostics.Stopwatch();
        public const int MaxInstructionsPerFrame = 512;
        public const double MillisecondsPerFrame = 2;

        public double LastExecutionMilliseconds { get; private set; }

        public int LastInstructionSteps { get; private set; }

        private RobotLuaAPI api;
        private BaseLuaAPI baseApi;

        public bool IsBase => GetComponent<BaseBuilding>() != null;

        public string ActionStatus => api != null ? api.Status : baseApi != null ? baseApi.Status : "";

        public string SourceCode { get => sourceCode; set => sourceCode = value; }

        public LuaProgramState State { get; private set; }

        public string Output { get; private set; } = "";

        public string Error { get; private set; } = "";

        public int ExampleIndex { get; private set; }

        public string ExampleName => ExampleNames[ExampleIndex];

        public static int ExampleCount => Examples.Length;

        public static string GetExampleName(int index) => ExampleNames[index];
        private static readonly string[] ExampleNames =
        {
            "Powitanie i obliczenie",
            "Licznik do zatrzymania",
            "Błąd składni",
            "Błąd wykonania",
            "API 1: znajdź kopalnię",
            "API 2: ruch do kopalni",
            "API 3: wydobycie",
            "API 4: powrót do bazy",
            "API 5: rozładunek",
            "API 6: cargo i wait",
            "Automatyczne wydobycie (pętla)",
            "Błąd: pętla bez yield",
            "Błąd: fałszywa kopalnia",
            "Błąd: argument wait",
            "Błąd: nieistniejący cel",
            "Błąd: spam logu",
            "Błąd: dostęp do pliku",
            "Automatyczna wycinka (wood)"
        };
        private static readonly string[] Examples =
        {
            "print(unitName .. ': wynik = ' .. (6 * 7))\nreturn 42",
            "counter = 0\nwhile true do\n    counter = counter + 1\n    if counter % 60 == 0 then\n        print(unitName .. ': licznik = ' .. counter)\n    end\n    coroutine.yield()\nend",
            "local value = 42\nif value > 0 then\n    print(value)\n-- Brakuje end: celowy błąd składni.",
            "print(unitName .. ': start')\nmissingFunction()",
            "local mine = robot.findNearestMine()\nif mine ~= nil then\n    print('Kopalnia: ' .. mine.name)\nelse\n    print('Brak kopalni')\nend",
            "local mine = robot.findNearestMine()\nassert(mine ~= nil, 'Brak kopalni')\nrobot.moveTo(mine)\nprint('Ruch zakończony: robot przy kopalni')",
            "-- Najpierw uruchom API 2: ruch do kopalni.\nlocal mine = robot.findNearestMine()\nrobot.mine(mine)\nprint('Wydobycie zakończone. Cargo: ' .. robot.getCargo())",
            "robot.returnToBase()\nprint('Powrót zakończony: robot przy bazie')",
            "-- Najpierw uruchom API 4: powrót do bazy.\nrobot.depositResources()\nprint('Rozładunek zakończony. Cargo: ' .. robot.getCargo())",
            "print('Cargo: ' .. robot.getCargo() .. '/' .. robot.getCargoCapacity())\nlocal p = robot.getPosition()\nprint('Pozycja: ' .. p.x .. ', ' .. p.z)\nrobot.wait(2)\nprint('Minęły 2 sekundy czasu gry')",
            "while true do\n    local mine = robot.findNearestMine()\n    if mine ~= nil then\n        robot.moveTo(mine)\n        robot.mine(mine)\n        robot.returnToBase()\n        robot.depositResources()\n        print(unitName .. ': dostarczono iron')\n    else\n        robot.wait(1)\n    end\nend",
            "while true do\nend",
            "robot.moveTo({name = 'Mine'})",
            "robot.wait('dwie sekundy')",
            "robot.moveTo(nil)",
            "while true do\n    print('spam')\nend",
            "io.open('test.txt', 'w')",
            "while true do\n    local tree = robot.findNearestTree()\n    if tree ~= nil then\n        robot.moveTo(tree)\n        robot.chop(tree)\n        robot.returnToBase()\n        robot.depositResources()\n    else\n        robot.wait(1)\n    end\nend"
        };

        public void LoadExample(int index)
        {
            StopProgram();
            if (IsBase)
            {
                sourceCode = "while true do\n    if economy.wood >= 100 then\n        buildHarvester()\n    end\n    coroutine.yield()\nend";
                Output = Error = "";
                return;
            }

            ExampleIndex = (index % Examples.Length + Examples.Length) % Examples.Length;
            sourceCode = Examples[ExampleIndex];
            Output = Error = "";
            StorageMessage = "Wstawiono przykład. Zapisz, aby zachować go na później.";
            StorageError = false;
        }

        public void InitializeSavedProgram(string programId, string directory = null)
        {
            storage = new RobotProgramStorage(programId, directory);
            savedSourceCode = null;
            if (!storage.TryLoad(out string code, out string error))
            {
                StorageMessage = error;
                StorageError = true;
                return;
            }

            if (code == null && programId.Contains("Harvester-"))
                new RobotProgramStorage(programId.Replace("Harvester-", "Builder-"), directory).TryLoad(out code, out _);
            StorageError = false;
            if (code != null)
            {
                sourceCode = savedSourceCode = code;
                StorageMessage = "Wczytano zapisany program.";
            }
            else
                StorageMessage = "Kod przykładowy. Zapisz, aby zachować własny program.";
        }

        public bool SaveProgram()
        {
            if (storage == null)
            {
                StorageError = true;
                StorageMessage = "Magazyn programów nie został zainicjalizowany.";
                return false;
            }

            if (!storage.TrySave(sourceCode ?? "", out string error))
            {
                StorageError = true;
                StorageMessage = error;
                return false;
            }

            savedSourceCode = sourceCode ?? "";
            StorageError = false;
            StorageMessage = "Zapisano program tego robota.";
            return true;
        }

        public bool RunProgram()
        {
            StopProgram();
            Output = Error = "";
            forcedYields = 0;
            LastExecutionMilliseconds = 0;
            LastInstructionSteps = 0;
            if (!isActiveAndEnabled)
                return false;
            var robot = GetComponent<RobotController>();
            if (!IsBase && (robot == null || robot.Movement == null || robot.Inventory == null))
            {
                SetError("Robot nie został zainicjalizowany.");
                return false;
            }

            if (robot != null && (robot.Movement.IsMoving || (robot.HarvestCycle != null && robot.HarvestCycle.IsRunning)))
            {
                SetError("Poczekaj na zakończenie ręcznie uruchomionej akcji robota.");
                return false;
            }

            if (robot != null && robot.HarvestCycle != null)
                robot.HarvestCycle.ClearFeedback();
            if (string.IsNullOrWhiteSpace(sourceCode) || sourceCode.Length > MaxCodeLength)
            {
                SetError("Kod musi mieć od 1 do 16000 znaków.");
                return false;
            }

            try
            {
                LuaSourceGuard.Validate(sourceCode);
                // Explicit modules: no IO, OS, loaders, dynamic code, metatables or CLR objects.
                script = new Script(CoreModules.Basic | CoreModules.GlobalConsts | CoreModules.Math);
                script.Options.ScriptLoader = new NoFileLoader();
                script.Options.DebugPrint = AppendOutput;
                script.Globals.Set("collectgarbage", DynValue.Nil);
                script.Globals.Set("unitName", DynValue.NewString(gameObject.name));
                // Expose only yield, not create/resume/wrap that could bypass the host budget.
                var coroutine = new Table(script);
                coroutine.Set("yield", DynValue.NewCallback((context, args) => DynValue.NewYieldReq(new DynValue[0])));
                script.Globals.Set("coroutine", DynValue.NewTable(coroutine));
                BaseLuaAPI.RegisterEconomy(script, transform.root.GetComponent<ResourceManager>());
                if (IsBase)
                    baseApi = new BaseLuaAPI(GetComponent<BaseBuilding>(), script);
                else
                {
                    api = new RobotLuaAPI(robot, script);
                    api.Register();
                }

                var function = script.LoadString(sourceCode, null, gameObject.name + ".lua");
                routine = script.CreateCoroutine(function);
                routine.Coroutine.AutoYieldCounter = 1;
                State = LuaProgramState.Running;
                return true;
            }
            catch (InterpreterException exception)
            {
                SetError(exception.DecoratedMessage ?? exception.Message);
            }
            catch (Exception exception)
            {
                SetError("Błąd runtime Lua: " + exception.Message);
            }

            return false;
        }

        private void Update()
        {
            if (PauseMenu.IsOpen)
                return;
            if (State != LuaProgramState.Running)
                return;
            executionClock.Restart();
            LastInstructionSteps = 0;
            try
            {
                if (api != null && !api.Tick(Time.deltaTime))
                    return;
                if (baseApi != null && !baseApi.Tick(Time.deltaTime))
                    return;
                // Tiny VM slices let the host check time between instructions. Explicit yields still end this frame.
                while (LastInstructionSteps < MaxInstructionsPerFrame && executionClock.Elapsed.TotalMilliseconds < MillisecondsPerFrame)
                {
                    routine.Coroutine.Resume();
                    LastInstructionSteps++;
                    if (routine.Coroutine.State == CoroutineState.Dead)
                    {
                        State = LuaProgramState.Completed;
                        ReleaseRuntime();
                        return;
                    }

                    if (routine.Coroutine.State != CoroutineState.ForceSuspended)
                    {
                        forcedYields = 0;
                        return;
                    }
                }

                if (++forcedYields >= 120)
                    SetError("Przekroczono limit czasu/instrukcji: 120 klatek obliczeń bez yield lub akcji oczekującej.");
            }
            catch (InterpreterException exception)
            {
                SetError(exception.DecoratedMessage ?? exception.Message);
            }
            catch (Exception exception)
            {
                SetError("Błąd runtime Lua: " + exception.Message);
            }
            finally
            {
                executionClock.Stop();
                LastExecutionMilliseconds = executionClock.Elapsed.TotalMilliseconds;
            }
        }

        public void StopProgram()
        {
            State = LuaProgramState.Stopped;
            ReleaseRuntime();
        }

        private void AppendOutput(string line)
        {
            if (line.Length > 512)
                line = line.Substring(0, 512);
            Output += line + "\n";
            if (Output.Length > 4096)
                Output = Output.Substring(Output.Length - 4096);
        }

        private void SetError(string message)
        {
            if (api != null && api.PendingLocation.Length > 0 && !message.Contains(".lua"))
                message = api.PendingLocation + ": " + message;
            Error = message.Length > 2048 ? message.Substring(0, 2048) : message;
            State = LuaProgramState.Error;
            ReleaseRuntime();
        }

        private void ReleaseRuntime()
        {
            if (api != null)
                api.Cancel();
            baseApi?.Cancel();
            baseApi = null;
            api = null;
            routine = null;
            script = null;
        }

        private void OnDisable() => StopProgram();

        private sealed class NoFileLoader : ScriptLoaderBase
        {
            public override bool ScriptFileExists(string name) => false;

            public override object LoadFile(
                string file,
                Table globalContext) => throw new ScriptRuntimeException("Dostęp do plików jest wyłączony.");
        }
    }
}
