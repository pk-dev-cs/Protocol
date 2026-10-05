using System;
using System.Collections.Generic;
using UnityEngine;

namespace Protocol
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class ColonyPowerGrid : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private double batteryCapacityKWh = 100;
        [SerializeField, Min(0)] private double storedEnergyKWh = 100;
        [SerializeField, Min(0)] private double baseConsumptionKW = 10;
        private readonly Dictionary<UnityEngine.Object, Func<double>> producers = new Dictionary<UnityEngine.Object, Func<double>>();
        private readonly Dictionary<UnityEngine.Object, double> consumers = new Dictionary<UnityEngine.Object, double>();
        private readonly List<ChargingStationStructure> chargingStations = new List<ChargingStationStructure>();
        private bool allocatingCharging;
        private AlienWorldLighting clock;

        public double BatteryCapacityKWh => batteryCapacityKWh;
        public double StoredEnergyKWh => storedEnergyKWh;
        public double BatteryPercent => 100 * storedEnergyKWh / batteryCapacityKWh;
        public double TotalGenerationKW => Sum(producers);
        public double TotalConsumptionKW => FixedConsumptionKW + ChargingConsumptionKW;
        private double FixedConsumptionKW => baseConsumptionKW + Sum(consumers);
        private double ChargingConsumptionKW
        {
            get
            {
                double total = 0;
                foreach (var station in chargingStations)
                    if (station != null && station.isActiveAndEnabled) total += station.CurrentChargingKW;
                return total;
            }
        }
        public double BalanceKW => TotalGenerationKW - TotalConsumptionKW;
        public double RuntimeHours => BalanceKW < 0 ? storedEnergyKWh / -BalanceKW : double.PositiveInfinity;
        public bool HasPower => isActiveAndEnabled && (storedEnergyKWh > 0 || BalanceKW >= 0);
        public string Status => HasPower ? "POWER OK" : "NO POWER";

        public void Initialize(AlienWorldLighting worldClock) => clock = worldClock;

        public void SetGeneration(UnityEngine.Object owner, double powerKW)
        {
            Validate(powerKW);
            RegisterGeneration(owner, () => powerKW);
        }

        public void RegisterGeneration(UnityEngine.Object owner, Func<double> generationKW)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (generationKW == null) throw new ArgumentNullException(nameof(generationKW));
            producers[owner] = generationKW;
        }

        private static double Sum(Dictionary<UnityEngine.Object, Func<double>> entries)
        {
            double total = 0;
            foreach (var entry in entries)
            {
                if (entry.Key == null || (entry.Key is Behaviour behaviour && !behaviour.isActiveAndEnabled)) continue;
                double power = entry.Value();
                Validate(power);
                total += power;
            }
            return total;
        }
        public void SetConsumption(UnityEngine.Object owner, double powerKW) => Set(consumers, owner, powerKW);
        public void RegisterChargingStation(ChargingStationStructure station)
        {
            if (station == null) throw new ArgumentNullException(nameof(station));
            if (!chargingStations.Contains(station)) chargingStations.Add(station);
            RefreshChargingAllocation();
        }

        public void RefreshChargingAllocation()
        {
            if (!allocatingCharging) AllocateCharging(0);
        }

        public void Unregister(UnityEngine.Object owner)
        {
            producers.Remove(owner);
            consumers.Remove(owner);
            if (owner is ChargingStationStructure station) chargingStations.Remove(station);
        }

        private static void Set(Dictionary<UnityEngine.Object, double> entries, UnityEngine.Object owner, double powerKW)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Validate(powerKW);
            entries[owner] = powerKW;
        }

        private static double Sum(Dictionary<UnityEngine.Object, double> entries)
        {
            double total = 0;
            foreach (var entry in entries)
                if (entry.Key != null && (!(entry.Key is Behaviour behaviour) || behaviour.isActiveAndEnabled))
                    total += entry.Value;
            return total;
        }

        private void Update()
        {
            if (clock != null)
                AdvanceHours(Time.deltaTime * clock.GameHoursPerSimulationSecond);
        }

        public void AdvanceHours(double gameHours)
        {
            Validate(gameHours);
            double deliveredKWh = AllocateCharging(gameHours);
            storedEnergyKWh = Math.Max(0, Math.Min(batteryCapacityKWh,
                storedEnergyKWh + (TotalGenerationKW - FixedConsumptionKW) * gameHours - deliveredKWh));
            if (storedEnergyKWh < 1e-9) storedEnergyKWh = 0;
            RefreshChargingAllocation();
        }

        private double AllocateCharging(double gameHours)
        {
            allocatingCharging = true;
            double availableKW = TotalGenerationKW - FixedConsumptionKW;
            if (gameHours > 0) availableKW = Math.Max(0, availableKW + storedEnergyKWh / gameHours);
            else if (storedEnergyKWh > 0) availableKW = double.PositiveInfinity;
            else availableKW = Math.Max(0, availableKW);
            double deliveredKWh = 0;
            try
            {
                foreach (var station in chargingStations)
                {
                    if (station == null || !station.isActiveAndEnabled) continue;
                    double grantedKW = Math.Min(station.RequestedChargingKW(gameHours), availableKW);
                    station.SetAllocatedPower(grantedKW);
                    availableKW -= grantedKW;
                    if (gameHours > 0) deliveredKWh += station.ReceiveEnergy(grantedKW * gameHours);
                }
            }
            finally { allocatingCharging = false; }
            return deliveredKWh;
        }

        public static string FormatDuration(double hours)
        {
            if (double.IsInfinity(hours) || hours > 9999) return "∞";
            int minutes = (int)Math.Ceiling(Math.Max(0, hours * 60 - 1e-8));
            return minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}m" : $"{minutes}m";
        }

        private static void Validate(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
        }

        private void OnValidate()
        {
            if (double.IsNaN(batteryCapacityKWh) || double.IsInfinity(batteryCapacityKWh)) batteryCapacityKWh = 100;
            batteryCapacityKWh = Math.Max(.01, batteryCapacityKWh);
            if (double.IsNaN(storedEnergyKWh) || double.IsInfinity(storedEnergyKWh)) storedEnergyKWh = batteryCapacityKWh;
            storedEnergyKWh = Math.Max(0, Math.Min(batteryCapacityKWh, storedEnergyKWh));
            if (double.IsNaN(baseConsumptionKW) || double.IsInfinity(baseConsumptionKW)) baseConsumptionKW = 10;
            baseConsumptionKW = Math.Max(0, baseConsumptionKW);
        }
    }
}
