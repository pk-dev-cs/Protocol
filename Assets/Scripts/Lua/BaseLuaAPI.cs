using MoonSharp.Interpreter;
using UnityEngine;

namespace Protocol
{
    public sealed class BaseLuaAPI
    {
        readonly BaseBuilding home;
        bool building;
        float elapsed;

        public string Status => building ? "Produkcja harvestera" : "";

        public BaseLuaAPI(BaseBuilding home, Script script)
        {
            this.home = home;
            script.Globals.Set("buildHarvester", DynValue.NewCallback((context, args) =>
            {
                if (args.Count != 0)
                    throw new ScriptRuntimeException("buildHarvester() nie przyjmuje argumentów.");
                if (!home.TryBeginProduction())
                    throw new ScriptRuntimeException("Produkcja wymaga 100 wood i wolnej bazy.");
                building = true;
                elapsed = 0;
                return DynValue.NewYieldReq(new DynValue[0]);
            }));
        }

        public bool Tick(float delta)
        {
            if (!building)
                return true;
            if (home == null || !home.isActiveAndEnabled)
                throw new ScriptRuntimeException("Baza jest niedostępna.");
            elapsed += delta;
            if (elapsed < BaseBuilding.ProductionSeconds)
                return false;
            home.CompleteProduction();
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
            meta.Set("__index", DynValue.NewCallback((context, args) =>
            {
                if (resources == null || !resources.isActiveAndEnabled)
                    throw new ScriptRuntimeException("Ekonomia jest niedostępna.");
                string key = args[1].String;
                return key == "iron" ? DynValue.NewNumber(resources.Iron) : key == "wood" ? DynValue.NewNumber(resources.Wood) : DynValue.Nil;
            }));
            meta.Set("__newindex", DynValue.NewCallback((context, args) =>
            {
                throw new ScriptRuntimeException("economy jest tylko do odczytu.");
            }));
            economy.MetaTable = meta;
            script.Globals.Set("economy", DynValue.NewTable(economy));
        }
    }
}
