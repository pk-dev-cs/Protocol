using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Protocol.Editor
{
    public static class RobotEnergyChecks
    {
        [MenuItem("Protocol/Validate Robot Energy")]
        public static void Run()
        {
            foreach (int fps in new[] { 30, 60, 120, 144 })
            {
                var owner = new GameObject("Battery validation");
                try
                {
                    var energy = owner.AddComponent<RobotEnergy>();
                    Require(energy.Percentage == 100 && energy.FullBatteryMovementDuration == 3600, "Production defaults changed.");
                    for (int frame = 0; frame < fps * 1800; frame++) energy.ConsumeMovement(1d / fps);
                    Require(Math.Abs(energy.Percentage - 50) < .00001, "Half-hour drain depends on frame rate.");
                    for (int frame = 0; frame < fps * 1800; frame++) energy.ConsumeMovement(1d / fps);
                    Require(energy.IsDepleted, "One hour of movement must exhaust a full battery.");
                }
                finally { Object.DestroyImmediate(owner); }
            }

            var first = new GameObject("Battery A");
            var second = new GameObject("Battery B");
            try
            {
                var a = first.AddComponent<RobotEnergy>();
                var b = second.AddComponent<RobotEnergy>();
                a.Consume(13);
                Require(a.Percentage == 87 && b.Percentage == 100, "Batteries share state.");
                a.ConsumeMovement(0);
                Require(a.Percentage == 87, "Zero simulation time must not consume energy.");
                a.enabled = false;
                a.enabled = true;
                Require(a.Percentage == 87, "Enabling the component must not refill the battery.");
                int depletedEvents = 0;
                a.Depleted += () => depletedEvents++;
                a.Consume(200);
                a.Consume(1);
                Require(a.Percentage == 0 && depletedEvents == 1, "Depletion must clamp and emit once.");
                a.Recharge(150);
                Require(a.Percentage == 100, "Recharge API must clamp capacity.");
                a.FullBatteryMovementDuration = 60;
                a.ConsumeMovement(60);
                Require(a.IsDepleted, "Editor test duration is not respected.");
                a.FullBatteryMovementDuration = 3600;
                bool rejected = false;
                try { a.Consume(double.NaN); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "Invalid input corrupted battery state.");
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
            Debug.Log("PASS Robot energy: 30/60/120/144 FPS, half/full hour, independent state, zero delta, enable cycle, depletion, clamping and 60-second test duration.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
