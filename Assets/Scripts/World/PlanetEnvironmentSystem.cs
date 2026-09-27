using UnityEngine;

namespace Protocol
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AlienWorldLighting))]
    public sealed class PlanetEnvironmentSystem : MonoBehaviour
    {
        [SerializeField] private PlanetEnvironmentProfile profile;
        private AlienWorldLighting clock;
        private bool ownsProfile;

        public PlanetEnvironmentState State { get; private set; }
        public PlanetEnvironmentProfile Profile => profile;

        private void Awake()
        {
            clock = GetComponent<AlienWorldLighting>();
            if (profile == null) profile = Resources.Load<PlanetEnvironmentProfile>("PlanetEnvironmentProfile");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PlanetEnvironmentProfile>();
                ownsProfile = true;
            }
            Refresh();
        }

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (clock == null || profile == null) return;
            State = PlanetEnvironmentModel.Evaluate(profile, clock.TimeOfDayHours, clock.ElapsedSimulationSeconds);
        }

        private void OnDestroy()
        {
            if (ownsProfile) Destroy(profile);
        }
    }
}
