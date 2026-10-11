using System;
using UnityEngine;

namespace Protocol
{
    [DisallowMultipleComponent]
    public sealed class RobotEnergy : MonoBehaviour
    {
        [SerializeField, Min(1)] private float fullBatteryMovementDuration = 3600;
        [SerializeField, Range(0, 100)] private double percentage = 100;
        [SerializeField, Min(.01f)] private double capacityKWh = 10;
        [SerializeField, Min(0)] private double movementKW = 4;
        [SerializeField, Min(0)] private double constructionKW = 6;
        [SerializeField, Min(0)] private double miningKW = 6;
        [SerializeField] private AlienWorldLighting clock;
        [SerializeField] private bool usesPowerModel;
        private int consumptionFrame = -1;
        private double consumptionKW;

        public double CapacityKWh => capacityKWh;
        public double StoredEnergyKWh => capacityKWh * percentage / 100;
        public double MovementKW => movementKW;
        public double ConstructionKW => constructionKW;
        public double MiningKW => miningKW;
        public double CurrentConsumptionKW => !PauseMenu.IsOpen && Time.timeScale > 0 &&
            consumptionFrame == Time.frameCount && !IsDepleted ? consumptionKW : 0;
        public double RuntimeHours => CurrentConsumptionKW > 0
            ? StoredEnergyKWh / CurrentConsumptionKW : double.PositiveInfinity;
        public double GameHoursPerSimulationSecond => clock != null ? clock.GameHoursPerSimulationSecond : 0;

        public void ConfigurePower(double capacity, double movementPower, double constructionPower,
            AlienWorldLighting worldClock, double miningPower = 6)
        {
            ValidateAmount(capacity);
            ValidateAmount(movementPower);
            ValidateAmount(constructionPower);
            ValidateAmount(miningPower);
            if (capacity <= 0 || worldClock == null) throw new ArgumentException("Battery requires capacity and world clock.");
            capacityKWh = capacity;
            movementKW = movementPower;
            constructionKW = constructionPower;
            miningKW = miningPower;
            clock = worldClock;
            usesPowerModel = true;
        }

        public double ConsumePower(double powerKW, double simulationSeconds)
        {
            ValidateAmount(powerKW);
            ValidateAmount(simulationSeconds);
            if (clock == null) throw new InvalidOperationException("Battery has no world clock.");
            double requestedKWh = powerKW * simulationSeconds * clock.GameHoursPerSimulationSecond;
            double usedKWh = Consume(100 * requestedKWh / capacityKWh) * capacityKWh / 100;
            if (simulationSeconds > 0 && usedKWh > 0)
            {
                double actualKW = usedKWh / (simulationSeconds * clock.GameHoursPerSimulationSecond);
                if (consumptionFrame != Time.frameCount) consumptionKW = 0;
                consumptionFrame = Time.frameCount;
                consumptionKW += actualKW;
            }
            return usedKWh;
        }

        public double ConsumeConstruction(double simulationSeconds) => ConsumePower(constructionKW, simulationSeconds);
        public double ConsumeMining(double simulationSeconds) => ConsumePower(miningKW, simulationSeconds);

        public double ConsumeMiningTime(double simulationSeconds)
        {
            ValidateAmount(simulationSeconds);
            if (IsDepleted || simulationSeconds == 0) return 0;
            if (miningKW == 0) return simulationSeconds;
            double requested = miningKW * simulationSeconds * GameHoursPerSimulationSecond;
            double used = ConsumeMining(simulationSeconds);
            return requested <= 0 ? 0 : simulationSeconds * Math.Min(1, used / requested);
        }

        public double Percentage => percentage;
        public float Normalized => (float)(percentage / 100);
        public bool IsDepleted => percentage <= 0;
        public event Action Depleted;

        public float FullBatteryMovementDuration
        {
            get => Mathf.Max(1, fullBatteryMovementDuration);
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value));
                fullBatteryMovementDuration = value;
            }
        }

        public void ConsumeMovement(double simulationSeconds)
        {
            ValidateAmount(simulationSeconds);
            if (usesPowerModel) ConsumePower(movementKW, simulationSeconds);
            else Consume(100 * simulationSeconds / FullBatteryMovementDuration);
        }

        public double Consume(double percentagePoints)
        {
            ValidateAmount(percentagePoints);
            double before = percentage;
            percentage = Math.Max(0, percentage - percentagePoints);
            if (percentage < 1e-9) percentage = 0;
            if (before > 0 && IsDepleted) Depleted?.Invoke();
            return before - percentage;
        }

        public double Recharge(double percentagePoints)
        {
            ValidateAmount(percentagePoints);
            double before = percentage;
            percentage = Math.Min(100, percentage + percentagePoints);
            return percentage - before;
        }

        public double RechargeEnergy(double energyKWh)
        {
            ValidateAmount(energyKWh);
            return Recharge(100 * energyKWh / capacityKWh) * capacityKWh / 100;
        }

        private static void ValidateAmount(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
        }

        private void OnValidate()
        {
            capacityKWh = double.IsNaN(capacityKWh) || double.IsInfinity(capacityKWh) ? 10 : Math.Max(.01, capacityKWh);
            movementKW = double.IsNaN(movementKW) || double.IsInfinity(movementKW) ? 4 : Math.Max(0, movementKW);
            constructionKW = double.IsNaN(constructionKW) || double.IsInfinity(constructionKW) ? 6 : Math.Max(0, constructionKW);
            miningKW = double.IsNaN(miningKW) || double.IsInfinity(miningKW) ? 6 : Math.Max(0, miningKW);
            fullBatteryMovementDuration = float.IsNaN(fullBatteryMovementDuration) || float.IsInfinity(fullBatteryMovementDuration)
                ? 3600 : Mathf.Max(1, fullBatteryMovementDuration);
            percentage = double.IsNaN(percentage) || double.IsInfinity(percentage) ? 100 : Math.Max(0, Math.Min(100, percentage));
        }
    }
}
