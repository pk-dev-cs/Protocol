using UnityEngine;

namespace Protocol
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SelectableStructure))]
    public sealed class SolarPanel : MonoBehaviour
    {
        [SerializeField, Min(0)] private float maximumGenerationKW = 10;
        [SerializeField] private ColonyPowerGrid grid;
        [SerializeField] private PlanetEnvironmentSystem environment;


        public float MaximumGenerationKW => maximumGenerationKW;
        public double CurrentGenerationKW => isActiveAndEnabled && environment != null
            ? CalculateGeneration(maximumGenerationKW, environment.State.Solar) : 0;
        public ColonyPowerGrid Grid => grid;
        public static double CalculateGeneration(double maximumKW, float solar)
        {
            if (double.IsNaN(maximumKW) || double.IsInfinity(maximumKW) || maximumKW < 0)
                throw new System.ArgumentOutOfRangeException(nameof(maximumKW));
            if (float.IsNaN(solar) || float.IsInfinity(solar))
                throw new System.ArgumentOutOfRangeException(nameof(solar));
            return maximumKW * Mathf.Clamp01(solar);
        }

        public void Initialize(ColonyPowerGrid powerGrid, PlanetEnvironmentSystem planet)
        {
            if (grid != null) grid.Unregister(this);
            grid = powerGrid;
            environment = planet;
            Register();
        }

        private void OnEnable() => Register();

        private void Start()
        {
            if (grid == null || environment == null)
            {
                var scenario = GetComponentInParent<ScenarioBootstrap>();
                if (scenario != null)
                    Initialize(scenario.GetComponentInChildren<ColonyPowerGrid>(),
                        scenario.GetComponentInChildren<PlanetEnvironmentSystem>());
            }
            BuildVisuals();
        }

        private void Register()
        {
            if (isActiveAndEnabled && grid != null && environment != null)
                grid.RegisterGeneration(this, () => CurrentGenerationKW);
        }

        private void OnDisable()
        {
            if (grid != null) grid.Unregister(this);
        }



        private void OnValidate()
        {
            if (float.IsNaN(maximumGenerationKW) || float.IsInfinity(maximumGenerationKW)) maximumGenerationKW = 10;
            maximumGenerationKW = Mathf.Max(0, maximumGenerationKW);
        }

        public static SolarPanel Create(Transform parent, Vector3 position, ColonyPowerGrid powerGrid, PlanetEnvironmentSystem planet)
        {
            var root = new GameObject("Solar Panel");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var panel = root.AddComponent<SolarPanel>();
            panel.Initialize(powerGrid, planet);
            panel.BuildVisuals();
            return panel;
        }

        public void BuildVisuals()
        {
            if (transform.Find("Visuals") == null)
            {
                var prefab = Resources.Load<GameObject>("Buildings/SolarPanel");
                if (prefab == null) throw new System.InvalidOperationException("Missing solar panel model");
                var old = transform.Find("Solar array");
                if (old != null)
                {
                    old.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(old.gameObject);
                    else DestroyImmediate(old.gameObject);
                }
                GameObject model;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    model = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
                else
#endif
                    model = Instantiate(prefab, transform, false);
                model.name = "Visuals";
                model.transform.localRotation = Quaternion.identity;
            }
            var collider = GetComponent<BoxCollider>();
            if (collider == null) collider = gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 1.7f, 0);
            collider.size = new Vector3(5, 3.4f, 3.8f);
            var obstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle == null) obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = collider.size;
            obstacle.carving = true;
        }
    }
}