using MoonSharp.Interpreter;
using UnityEngine;

namespace Protocol
{
    public sealed class BaseLuaAPI
    {
        readonly BaseBuilding home;
        bool building;

        public string Status => building && home != null ? home.ProductionStatus : "";

        public BaseLuaAPI(BaseBuilding home, Script script)
        {
            this.home = home;
            RegisterProduction(script, "buildHarvester", RobotKind.Harvester);
            RegisterProduction(script, "buildAssembler", RobotKind.Assembler);
            RegisterProduction(script, "buildHauler", RobotKind.Hauler);
        }

        private void RegisterProduction(Script script, string name, RobotKind kind)
        {
            script.Globals.Set(name, DynValue.NewCallback((context, args) => LuaSaveReplay.Invoke(script, name, () =>
            {
                if (args.Count != 0)
                    throw new ScriptRuntimeException(name + "() nie przyjmuje argumentów.");
                if (!home.TryBeginProduction(kind))
                    throw new ScriptRuntimeException("Produkcja wymaga zasilania, " + home.Recipe(kind).WoodCost + " wood i wolnej bazy.");
                building = true;
                return DynValue.NewYieldReq(new DynValue[0]);
            })));
        }

        public bool Tick(float delta)
        {
            if (!building)
                return true;
            if (home == null || !home.isActiveAndEnabled)
                throw new ScriptRuntimeException("Baza jest niedostępna.");
            if (!home.AdvanceProduction(delta)) return false;
            building = false;
            return true;
        }

        public void Cancel()
        {
            if (building && home != null)
                home.CancelProduction();
            building = false;
        }

        public static void RegisterEconomy(Script script, ResourceManager resources)
        {
            var economy = new Table(script);
            var meta = new Table(script);
            meta.Set("__index", DynValue.NewCallback((context, args) => LuaSaveReplay.Invoke(script, "economy." + args[1].String, () =>
            {
                if (resources == null || !resources.isActiveAndEnabled)
                    throw new ScriptRuntimeException("Ekonomia jest niedostępna.");
                string key = args[1].String;
                return key == "iron" ? DynValue.NewNumber(resources.Iron) : key == "wood" ? DynValue.NewNumber(resources.Wood) : DynValue.Nil;
            })));
            meta.Set("__newindex", DynValue.NewCallback((context, args) =>
            {
                throw new ScriptRuntimeException("economy jest tylko do odczytu.");
            }));
            economy.MetaTable = meta;
            script.Globals.Set("economy", DynValue.NewTable(economy));
        }
    }
}
