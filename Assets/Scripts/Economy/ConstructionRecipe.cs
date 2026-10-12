using System;
using UnityEngine;

namespace Protocol
{
    public enum ConstructionKind { SolarPanel, ChargingStation, BatteryStorage }

    [Serializable]
    public sealed class ConstructionRecipe
    {
        public ConstructionKind Kind;
        [Min(0)] public int WoodCost = 50;
        [Min(.1f)] public float DurationSeconds = 12;
        public Vector2 Footprint = new Vector2(6, 5);
        [Min(0)] public float MaximumHeightDifference = .6f;
        public string DisplayName => Kind == ConstructionKind.SolarPanel ? "Solar Panel" : Kind == ConstructionKind.ChargingStation ? "Charging Station" : "Battery Storage";
        public bool IsValid => Enum.IsDefined(typeof(ConstructionKind), Kind) && WoodCost >= 0 &&
            float.IsFinite(DurationSeconds) && DurationSeconds > 0 &&
            float.IsFinite(Footprint.x) && float.IsFinite(Footprint.y) && Footprint.x >= 6 && Footprint.y >= 5 &&
            float.IsFinite(MaximumHeightDifference) && MaximumHeightDifference >= 0;
    }
}
