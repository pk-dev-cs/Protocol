using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Protocol.Editor
{
    public static class BatteryStorageChecks
    {
        [MenuItem("Protocol/Validate Battery Storage")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run validation in Play Mode.");
            var root = new GameObject("Battery storage validation");
            var source = new GameObject("Validation generation");
            try
            {
                var grid = root.AddComponent<ColonyPowerGrid>();
                Set(grid, "baseConsumptionKW", 50d);
                Set(grid, "storedEnergyKWh", 80d);
                var first = AddStorage(root, grid);
                Near(grid.BatteryCapacityKWh, 300, "A capacity");
                Near(grid.StoredEnergyKWh, 80, "A no free energy");
                Near(grid.BatteryPercent, 100d * 80 / 300, "A percentage");
                first.Initialize(grid);
                Near(grid.BatteryCapacityKWh, 300, "Duplicate initialization");
                Near(grid.TotalConsumptionKW, 50.2, "Single idle draw");
                var second = AddStorage(root, grid);
                Near(grid.TotalConsumptionKW, 50.4, "B idle draw");
                var third = AddStorage(root, grid);
                Near(grid.BatteryCapacityKWh, 700, "F capacity");
                Near(grid.TotalConsumptionKW, 50.6, "F idle draw");
                Object.DestroyImmediate(second.gameObject);
                Object.DestroyImmediate(third.gameObject);
                Near(grid.BatteryCapacityKWh, 300, "Removal capacity");
                grid.SetGeneration(source, 80);
                Near(grid.BalanceKW, 29.8, "C balance");
                grid.AdvanceHours(1);
                Near(grid.StoredEnergyKWh, 109.8, "C charging");
                Set(grid, "storedEnergyKWh", 200d);
                grid.SetGeneration(source, 0);
                Near(grid.RuntimeHours, 200d / 50.2, "D runtime");
                grid.AdvanceHours(1);
                Near(grid.StoredEnergyKWh, 149.8, "D night discharge");
                Set(grid, "storedEnergyKWh", 300d);
                grid.SetGeneration(source, 70.2);
                grid.AdvanceHours(1);
                Near(grid.StoredEnergyKWh, 300, "E capacity clamp");
                grid.AdvanceHours(0);
                Near(grid.StoredEnergyKWh, 300, "Zero elapsed time");
                Set(grid, "storedEnergyKWh", 250d);
                first.gameObject.SetActive(false);
                Near(grid.BatteryCapacityKWh, 100, "Disable capacity");
                Near(grid.StoredEnergyKWh, 100, "Disable energy clamp");
                Near(grid.TotalConsumptionKW, 50, "Disable draw");
                first.gameObject.SetActive(true);
                first.Initialize(grid);
                Near(grid.BatteryCapacityKWh, 300, "Enable once");
                Near(grid.StoredEnergyKWh, 100, "Enable no free energy");
                Near(grid.TotalConsumptionKW, 50.2, "Enable draw once");
                Set(grid, "storedEnergyKWh", 250d);
                Object.DestroyImmediate(first.gameObject);
                Near(grid.StoredEnergyKWh, 100, "Destroy clamp");
                grid.SetGeneration(source, 0);
                grid.AdvanceHours(2);
                Near(grid.StoredEnergyKWh, 0, "Depletion");
                if (grid.HasPower) throw new InvalidOperationException("Empty grid should report no power.");
                Near(SolarPanel.CalculateGeneration(10, .8f) * 5, 40, "Solar regression", .00001);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(source);
            }
            Debug.Log("PASS Battery Storage: tests A-F, no free energy, idle draw, enable/disable/destroy, duplicate registration, pause delta, capacity clamp, power failure and solar generation.");
        }

        [MenuItem("Protocol/Validate Battery Storage", true)]
        private static bool CanRun() => EditorApplication.isPlaying;

        private static BatteryStorage AddStorage(GameObject root, ColonyPowerGrid grid)
        {
            var owner = new GameObject("Validation storage");
            owner.transform.SetParent(root.transform, false);
            var storage = owner.AddComponent<BatteryStorage>();
            storage.Initialize(grid);
            return storage;
        }

        private static void Set(ColonyPowerGrid grid, string field, double value)
        {
            typeof(ColonyPowerGrid).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(grid, value);
        }

        private static void Near(double actual, double expected, string name, double tolerance = .0000001)
        {
            if (Math.Abs(actual - expected) > tolerance)
                throw new InvalidOperationException(name + ": expected " + expected + ", got " + actual);
        }
    }
}