using System;
using UnityEditor;
using UnityEngine;

namespace Protocol.Editor
{
    public static class PlanetEnvironmentChecks
    {
        [MenuItem("Protocol/Validate Planet Environment")]
        public static void Run()
        {
            var profile = AssetDatabase.LoadAssetAtPath<PlanetEnvironmentProfile>("Assets/Resources/PlanetEnvironmentProfile.asset");
            Require(profile != null, "Environment profile is missing.");
            var midnight = PlanetEnvironmentModel.Evaluate(profile, 0, 0);
            var morning = PlanetEnvironmentModel.Evaluate(profile, 6, 720);
            var noon = PlanetEnvironmentModel.Evaluate(profile, 12, 1440);
            var afternoon = PlanetEnvironmentModel.Evaluate(profile, 14, 1680);
            Require(midnight.Solar < .001f && noon.Solar > .99f, "Solar must follow the existing solar orbit.");
            Require(morning.Temperature < noon.Temperature && noon.Temperature < afternoon.Temperature,
                "Default thermal response must peak after noon.");
            Require(midnight.Temperature < afternoon.Temperature, "Night must be cooler than the afternoon.");
            Require(Mathf.Abs(PlanetEnvironmentModel.Evaluate(profile, 0, 0).Temperature -
                PlanetEnvironmentModel.Evaluate(profile, 24, 0).Temperature) < .00001f, "Thermal cycle must wrap continuously.");

            var previous = midnight;
            for (int second = 1; second <= 86400; second++)
            {
                float hour = second % 2880 / 120f;
                var state = PlanetEnvironmentModel.Evaluate(profile, hour, second);
                foreach (float value in Values(state)) Require(value >= 0 && value <= 1 && !float.IsNaN(value), "Value outside normalized range.");
                Require(Mathf.Abs(state.Wind - previous.Wind) < .005f, "Wind changed too quickly.");
                Require(Mathf.Abs(state.Dust - previous.Dust) < .001f, "Dust changed too quickly.");
                Require(Mathf.Abs(state.EMActivity - previous.EMActivity) < .0002f, "EM activity changed too quickly.");
                Require(Mathf.Abs(state.Radiation - previous.Radiation) < .00002f, "Radiation changed too quickly.");
                Require(Mathf.Abs(state.Radiation - profile.Radiation.Baseline) <= profile.Radiation.Amplitude + .00001f,
                    "Radiation drift exceeded its baseline envelope.");
                if (hour < 6 || hour > 18) Require(state.Solar < .0001f, "Solar must vanish at night.");
                previous = state;
            }
            var first = Values(PlanetEnvironmentModel.Evaluate(profile, 11.5f, 1500));
            var repeated = Values(PlanetEnvironmentModel.Evaluate(profile, 11.5f, 1500));
            for (int i = 0; i < first.Length; i++) Require(first[i] == repeated[i], "Evaluation must not advance its own clock.");
            Debug.Log("PASS Planet environment: 86,400 samples, normalized ranges, smooth variation, thermal lag, solar nights, radiation stability and repeatable evaluation.");
        }

        private static float[] Values(PlanetEnvironmentState state) => new[]
        {
            state.Temperature, state.Wind, state.Dust, state.Solar, state.EMActivity, state.Radiation
        };

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
