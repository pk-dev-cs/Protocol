using System;
using UnityEngine;

namespace Protocol
{
    public enum RobotKind { Harvester, Assembler, Hauler }

    [Serializable]
    public sealed class UnitProductionRecipe
    {
        public RobotKind Kind;
        [Min(0)] public int WoodCost = 100;
        [Min(.1f)] public float DurationSeconds = 8;
        [Min(0)] public float ProductionKW = 30;
        [Min(.01f)] public float BatteryKWh = 10;
        [Min(0)] public float MovementKW = 4;
        [Min(0)] public float ConstructionKW = 6;
        [Min(0)] public float MiningKW = 6;
        [Min(0)] public float TowingKW = 6;
        public bool IsValid => WoodCost >= 0 && Valid(DurationSeconds, true) && Valid(ProductionKW, false) &&
            Valid(BatteryKWh, true) && Valid(MovementKW, false) && Valid(ConstructionKW, false) &&
            Valid(MiningKW, false) && Valid(TowingKW, false);

        private static bool Valid(float value, bool positive) => !float.IsNaN(value) && !float.IsInfinity(value) &&
            (positive ? value > 0 : value >= 0);
    }
}
