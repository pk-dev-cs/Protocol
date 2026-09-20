using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;

namespace Protocol
{
    /// <summary>Per-program API. Lua receives values and opaque table handles, never Unity objects.</summary>
    public sealed class RobotLuaAPI
    {
        private enum Action

        {
            None,
            Moving,
            Mining,
            Returning,
            Depositing,
            Waiting,
            Chopping
        }

        private readonly RobotController robot;
        private readonly Script script;
        private readonly Dictionary<Table, Mine> mines = new Dictionary<Table, Mine>();
        private readonly Dictionary<Table, TreeResource> trees = new Dictionary<Table, TreeResource>();
        private TreeResource reservedTree, activeTree;
        private Action pending;
        private Mine activeMine;
        private BaseBuilding activeBase;
        private float elapsed, waitSeconds;
        private string callingLocation = "";
        public string PendingLocation { get; private set; } = "";

        public bool IsPending => pending != Action.None;

        public string Status => pending == Action.Waiting ? "Idle" : IsPending ? pending.ToString() : "";


        public RobotLuaAPI(RobotController controller, Script owner)

        {
            robot = controller;
            script = owner;
        }

        public void Register()

        {
            var table = new Table(script);
            table.Set("findNearestMine", Callback(args =>
            {
                Count(args, 0, "findNearestMine");
                Mine nearest = null;
                float distance = float.PositiveInfinity;
                foreach (var mine in robot.transform.root.GetComponentsInChildren<Mine>())
                {
                    if (!mine.isActiveAndEnabled)
                        continue;
                    var fog = robot.transform.root.GetComponent<FogOfWar>();
                    if (fog != null && !fog.IsExplored(mine.transform.position))
                        continue;
                    float candidate = (mine.transform.position - robot.transform.position).sqrMagnitude;
                    if (candidate < distance)
                    {
                        nearest = mine;
                        distance = candidate;
                    }
                }

                if (nearest == null)
                    return DynValue.Nil;
                foreach (var entry in mines)
                    if (entry.Value == nearest)
                        return DynValue.NewTable(entry.Key);
                var handle = new Table(script);
                handle.Set("name", DynValue.NewString(nearest.name));
                mines.Add(handle, nearest);
                return DynValue.NewTable(handle);
            }));
            table.Set("findNearestTree", Callback(args =>
            {
                Count(args, 0, "findNearestTree");
                TreeResource nearest = null;
                float distance = float.PositiveInfinity;
                var fog = robot.transform.root.GetComponent<FogOfWar>();
                foreach (var tree in robot.transform.root.GetComponentsInChildren<TreeResource>())
                {
                    if (!tree.Available || (tree.ReservedBy != null && tree.ReservedBy != robot) || (fog != null && !fog.IsExplored(tree.transform.position)))
                        continue;
                    float candidate = (tree.transform.position - robot.transform.position).sqrMagnitude;
                    if (candidate >= distance || !tree.Approach(robot.transform.position, out _))
                        continue;
                    nearest = tree;
                    distance = candidate;
                }

                if (reservedTree != null && reservedTree != nearest)
                    reservedTree.Release(robot);
                reservedTree = nearest;
                if (nearest == null || !nearest.Reserve(robot))
                    return DynValue.Nil;
                foreach (var item in trees)
                    if (item.Value == nearest)
                        return DynValue.NewTable(item.Key);
                var handle = new Table(script);
                handle.Set("name", DynValue.NewString(nearest.name));
                trees.Add(handle, nearest);
                return DynValue.NewTable(handle);
            }));
            table.Set("chop", Callback(args =>
            {
                Count(args, 1, "chop");
                activeTree = ResolveTree(args[0]);
                if (!activeTree.CanChop(robot))
                    throw Error("chop", "robot musi dotrzeć do drzewa.");
                if (robot.Inventory.Cargo > 0 && robot.Inventory.Kind != ResourceKind.Wood)
                    throw Error("chop", "najpierw rozładuj iron w bazie.");
                Begin(Action.Chopping);
                return Yield();
            }));
            table.Set("moveTo", Callback(args =>
            {
                Count(args, 1, "moveTo");
                activeTree = null;
                if (args[0].Type == DataType.Table && trees.ContainsKey(args[0].Table))
                {
                    activeTree = ResolveTree(args[0]);
                    activeMine = null;
                    if (!activeTree.Approach(robot.transform.position, out var point) || !robot.Movement.MoveTo(point))
                        throw Error("moveTo", "brak dostępnej drogi do drzewa.");
                    Begin(Action.Moving);
                    return Yield();
                }

                Mine mine = ResolveMine(args[0], "moveTo");
                activeMine = mine;
                BeginMove(MinePoint(mine), Action.Moving);
                return Yield();
            }));
            table.Set("mine", Callback(args =>
            {
                Count(args, 1, "mine");
                activeMine = ResolveMine(args[0], "mine");
                if (!activeMine.CanMine(robot))
                    throw Error("mine", "robot musi najpierw dotrzeć do kopalni.");
                if (robot.Inventory.Cargo > 0 && robot.Inventory.Kind != ResourceKind.Iron)
                    throw Error("mine", "najpierw rozładuj wood w bazie.");
                Begin(Action.Mining);
                return Yield();
            }));
            table.Set("returnToBase", Callback(args =>
            {
                Count(args, 0, "returnToBase");
                activeBase = Base();
                BeginMove(robot.BaseDestination, Action.Returning);
                return Yield();
            }));
            table.Set("depositResources", Callback(args =>
            {
                Count(args, 0, "depositResources");
                activeBase = Base();
                if (!activeBase.CanDeposit(robot))
                    throw Error("depositResources", "robot musi najpierw dotrzeć do bazy.");
                Begin(Action.Depositing);
                return Yield();
            }));
            table.Set("getCargo", Callback(args =>
            {
                Count(args, 0, "getCargo");
                return DynValue.NewNumber(robot.CurrentCargo);
            }));
            table.Set("getCargoCapacity", Callback(args =>
            {
                Count(args, 0, "getCargoCapacity");
                return DynValue.NewNumber(robot.CargoCapacity);
            }));
            table.Set("getPosition", Callback(args =>
            {
                Count(args, 0, "getPosition");
                Vector3 p = robot.transform.position;
                var result = new Table(script);
                result.Set("x", DynValue.NewNumber(p.x));
                result.Set("y", DynValue.NewNumber(p.y));
                result.Set("z", DynValue.NewNumber(p.z));
                return DynValue.NewTable(result);
            }));
            table.Set("wait", Callback(args =>
            {
                Count(args, 1, "wait");
                if (args[0].Type != DataType.Number || double.IsNaN(args[0].Number) || double.IsInfinity(args[0].Number) || args[0].Number < 0 || args[0].Number > 3600)
                    throw Error("wait", "oczekiwano liczby sekund od 0 do 3600.");
                waitSeconds = (float)args[0].Number;
                Begin(Action.Waiting);
                return Yield();
            }));
            script.Globals.Set("robot", DynValue.NewTable(table));
        }

        // Called by the host before resuming Lua; false keeps the coroutine suspended.
        public bool Tick(float deltaTime)

        {
            if (!IsPending)
                return true;
            EnsureRobotAvailable();
            elapsed += deltaTime;
            switch (pending)
            {
                case Action.Moving:
                case Action.Returning:
                    if (pending == Action.Moving && (activeMine == null || !activeMine.isActiveAndEnabled) && (activeTree == null || !activeTree.Available))
                        throw Error("moveTo", "kopalnia przestała istnieć lub została wyłączona.");
                    if (pending == Action.Returning && (activeBase == null || !activeBase.isActiveAndEnabled))
                        throw Error("returnToBase", "baza przestała istnieć lub została wyłączona.");
                    if (robot.Movement.IsMoving)
                        return false;
                    if (robot.Movement.Result != MovementResult.Arrived)
                        throw Error(pending == Action.Moving ? "moveTo" : "returnToBase", robot.Movement.Message);
                    break;
                case Action.Mining:
                    if (activeMine == null || !activeMine.CanMine(robot))
                        throw Error("mine", "kopalnia jest niedostępna lub poza zasięgiem.");
                    if (robot.Inventory.IsFull)
                        break;
                    if (elapsed < activeMine.SecondsPerUnit)
                        return false;
                    elapsed -= activeMine.SecondsPerUnit;
                    if (!activeMine.TryExtractUnit(robot))
                        throw Error("mine", "wydobycie nie powiodło się.");
                    if (!robot.Inventory.IsFull)
                        return false;
                    break;
                case Action.Chopping:
                    if (robot.Inventory.IsFull || (activeTree != null && activeTree.WoodRemaining == 0))
                        break;
                    if (activeTree == null || !activeTree.CanChop(robot))
                        throw Error("chop", "drzewo jest niedostępne lub poza zasięgiem.");
                    if (elapsed < .75f)
                        return false;
                    elapsed -= .75f;
                    if (!activeTree.Extract(robot))
                        throw Error("chop", "nie udało się zebrać wood.");
                    if (!robot.Inventory.IsFull && activeTree.WoodRemaining > 0)
                        return false;
                    break;
                case Action.Depositing:
                    if (activeBase == null || !activeBase.CanDeposit(robot))
                        throw Error("depositResources", "baza jest niedostępna lub poza zasięgiem.");
                    if (elapsed < activeBase.DepositSeconds)
                        return false;
                    if (!activeBase.TryDeposit(robot, out _))
                        throw Error("depositResources", "rozładunek nie powiódł się.");
                    break;
                case Action.Waiting:
                    if (elapsed < waitSeconds)
                        return false;
                    break;
            }

            pending = Action.None;
            PendingLocation = "";
            activeMine = null;
            activeBase = null;
            return true;
        }

        private TreeResource ResolveTree(DynValue handle)

        {
            if (handle.Type != DataType.Table || !trees.TryGetValue(
                handle.Table,
                out var tree) || tree == null || !tree.Available || !tree.Reserve(robot))
                throw Error("tree", "oczekiwano dostępnego drzewa zwróconego przez findNearestTree().");
            return tree;
        }

        public void Cancel()

        {
            foreach (var tree in trees.Values)
                if (tree != null)
                    tree.Release(robot);
            reservedTree = activeTree = null;
            if ((pending == Action.Moving || pending == Action.Returning) && robot != null && robot.Movement != null)
                robot.Movement.Cancel();
            pending = Action.None;
            activeMine = null;
            activeBase = null;
        }

        private void Begin(Action action)

        {
            pending = action;
            elapsed = 0;
            PendingLocation = callingLocation;
        }

        private void BeginMove(Transform target, Action action)

        {
            if (!robot.Movement.MoveTo(target))
                throw Error(action == Action.Moving ? "moveTo" : "returnToBase", robot.Movement.Message);
            Begin(action);
        }

        private Mine ResolveMine(DynValue handle, string function)

        {
            if (handle.Type != DataType.Table || !mines.TryGetValue(handle.Table, out var mine) || mine == null || !mine.isActiveAndEnabled)
                throw Error(function, "oczekiwano istniejącej kopalni zwróconej przez findNearestMine().");
            return mine;
        }

        private Transform MinePoint(Mine mine)

        {
            if (robot.MineDestination == null || !robot.MineDestination.IsChildOf(mine.transform))
                throw Error("moveTo", "brak punktu dostępu do tej kopalni.");
            return robot.MineDestination;
        }

        private BaseBuilding Base()

        {
            var result = robot.BaseDestination != null ? robot.BaseDestination.GetComponentInParent<BaseBuilding>() : null;
            if (result == null || !result.isActiveAndEnabled)
                throw Error("returnToBase/depositResources", "baza jest niedostępna.");
            return result;
        }

        private static void Count(CallbackArguments args, int count, string function)

        {
            if (args.Count != count)
                throw Error(function, "nieprawidłowa liczba argumentów; użyj składni robot." + function + "(...).");
        }

        private DynValue Callback(System.Func<CallbackArguments, DynValue> callback) => DynValue.NewCallback((context, args) =>

        {
            EnsureRobotAvailable();
            callingLocation = context.CallingLocation != null ? context.CallingLocation.FormatLocation(script, false) : "";
            if (callingLocation.Length == 0)
            {
                foreach (var frame in context.GetCallingCoroutine().GetStackTrace(0, null))
                    if (frame.Location != null)
                    {
                        callingLocation = frame.Location.FormatLocation(script, false);
                        if (callingLocation.Length > 0)
                            break;
                    }
            }

            if (callingLocation.Length == 0)
                callingLocation = robot.name + ".lua";
            return callback(args);
        });
        private void EnsureRobotAvailable()
        {
            if (robot == null || !robot.isActiveAndEnabled || robot.Movement == null || !robot.Movement.isActiveAndEnabled || robot.Inventory == null || !robot.Inventory.isActiveAndEnabled)
                throw Error("action", "robot lub jego komponenty są niedostępne.");
        }

        private static DynValue Yield() => DynValue.NewYieldReq(new DynValue[0]);

        private static ScriptRuntimeException Error(
            string function,
            string message) => new ScriptRuntimeException("robot." + function + ": " + message);

    }
}
