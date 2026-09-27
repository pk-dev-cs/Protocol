using UnityEngine;

namespace Protocol
{
    public readonly struct PlanetEnvironmentState
    {
        public float Temperature { get; }
        public float Wind { get; }
        public float Dust { get; }
        public float Solar { get; }
        public float EMActivity { get; }
        public float Radiation { get; }

        public PlanetEnvironmentState(float temperature, float wind, float dust, float solar, float emActivity, float radiation)
        {
            Temperature = Mathf.Clamp01(temperature);
            Wind = Mathf.Clamp01(wind);
            Dust = Mathf.Clamp01(dust);
            Solar = Mathf.Clamp01(solar);
            EMActivity = Mathf.Clamp01(emActivity);
            Radiation = Mathf.Clamp01(radiation);
        }
    }

    public static class PlanetEnvironmentModel
    {
        public static PlanetEnvironmentState Evaluate(PlanetEnvironmentProfile profile, float timeOfDayHours, double simulationSeconds)
        {
            float lower = Mathf.Clamp01(profile.TemperatureMinimum);
            float upper = Mathf.Max(lower, Mathf.Clamp01(profile.TemperatureMaximum));
            float thermalCycle = AlienWorldLighting.GetSolarElevation(timeOfDayHours - profile.ThermalLagHours);
            float temperature = Mathf.Clamp(profile.TemperatureBaseline + profile.TemperatureAmplitude * thermalCycle, lower, upper);
            float dust = profile.Dust.Evaluate(simulationSeconds);
            float solar = Mathf.Max(0, AlienWorldLighting.GetSolarElevation(timeOfDayHours)) * profile.PeakSolar;
            solar *= 1 - dust * Mathf.Clamp01(profile.DustSolarAttenuation);
            return new PlanetEnvironmentState(temperature, profile.Wind.Evaluate(simulationSeconds), dust,
                solar, profile.EMActivity.Evaluate(simulationSeconds), profile.Radiation.Evaluate(simulationSeconds));
        }
    }
}
