using System;
using UnityEngine;

namespace Protocol
{
    [Serializable]
    public sealed class EnvironmentVariation
    {
        [Range(0, 1)] public float Baseline;
        [Range(0, 1)] public float Minimum;
        [Range(0, 1)] public float Maximum = 1;
        [Range(0, 1)] public float Amplitude;
        [Min(1)] public float PeriodSeconds;
        public float NoiseOffset;

        public EnvironmentVariation(float baseline, float minimum, float maximum, float amplitude, float period, float offset)
        {
            Baseline = baseline;
            Minimum = minimum;
            Maximum = maximum;
            Amplitude = amplitude;
            PeriodSeconds = period;
            NoiseOffset = offset;
        }

        public float Evaluate(double simulationSeconds)
        {
            float noise = Mathf.PerlinNoise((float)(simulationSeconds / Mathf.Max(1, PeriodSeconds)), NoiseOffset) * 2 - 1;
            float lower = Mathf.Clamp01(Minimum);
            float upper = Mathf.Max(lower, Mathf.Clamp01(Maximum));
            return Mathf.Clamp(Baseline + noise * Mathf.Clamp01(Amplitude), lower, upper);
        }
    }

    [CreateAssetMenu(menuName = "Protocol/Planet Environment Profile")]
    public sealed class PlanetEnvironmentProfile : ScriptableObject
    {
        [Header("Temperature cycle")]
        [Range(0, 1)] public float TemperatureBaseline = .42f;
        [Range(0, 1)] public float TemperatureAmplitude = .24f;
        [Range(0, 1)] public float TemperatureMinimum = .1f;
        [Range(0, 1)] public float TemperatureMaximum = .85f;
        [Range(0, 6)] public float ThermalLagHours = 2;

        [Header("Solar availability")]
        [Range(0, 1)] public float PeakSolar = 1;
        [Range(0, 1)] public float DustSolarAttenuation;

        [Header("Slow variation (simulation seconds)")]
        public EnvironmentVariation Wind = new EnvironmentVariation(.55f, .15f, .9f, .25f, 300, 17.3f);
        public EnvironmentVariation Dust = new EnvironmentVariation(.35f, .15f, .6f, .08f, 1200, 83.7f);
        public EnvironmentVariation EMActivity = new EnvironmentVariation(.18f, .1f, .26f, .025f, 1800, 41.2f);
        public EnvironmentVariation Radiation = new EnvironmentVariation(.05f, .03f, .07f, .005f, 7200, 112.8f);
    }
}
