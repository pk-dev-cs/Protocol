using System;
using UnityEngine;

namespace Protocol
{
    [DisallowMultipleComponent]
    public sealed class RobotEnergy : MonoBehaviour
    {
        [SerializeField, Min(1)] private float fullBatteryMovementDuration = 3600;
        [SerializeField, Range(0, 100)] private double percentage = 100;

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
            Consume(100 * simulationSeconds / FullBatteryMovementDuration);
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

        private static void ValidateAmount(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
        }

        private void OnValidate()
        {
            fullBatteryMovementDuration = float.IsNaN(fullBatteryMovementDuration) || float.IsInfinity(fullBatteryMovementDuration)
                ? 3600 : Mathf.Max(1, fullBatteryMovementDuration);
            percentage = double.IsNaN(percentage) || double.IsInfinity(percentage) ? 100 : Math.Max(0, Math.Min(100, percentage));
        }
    }
}
