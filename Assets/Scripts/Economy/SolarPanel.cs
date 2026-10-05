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
        private Material frameMaterial, cellMaterial;

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

        private void OnDestroy()
        {
            if (frameMaterial != null) Destroy(frameMaterial);
            if (cellMaterial != null) Destroy(cellMaterial);
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

        private void BuildVisuals()
        {
            if (transform.Find("Solar array") != null) return;
            var shader = Resources.Load<Shader>("FogSurface");
            frameMaterial = new Material(shader) { name = "Solar frame", color = new Color(.2f, .25f, .29f) };
            cellMaterial = new Material(shader) { name = "Solar cells", color = new Color(.035f, .12f, .24f) };
            if (cellMaterial.HasProperty("_Smoothness")) cellMaterial.SetFloat("_Smoothness", .75f);
            var visual = new GameObject("Solar array").transform;
            visual.SetParent(transform, false);
            Part(visual, "Foot", new Vector3(0, .15f, 0), new Vector3(3, .3f, 2), frameMaterial);
            Part(visual, "Support", new Vector3(0, 1, 0), new Vector3(.45f, 1.5f, .45f), frameMaterial);
            var rack = new GameObject("Tilted rack").transform;
            rack.SetParent(visual, false);
            rack.localPosition = new Vector3(0, 1.9f, 0);
            rack.localRotation = Quaternion.Euler(24, 0, 0);
            Part(rack, "Frame", Vector3.zero, new Vector3(5, .16f, 3.2f), frameMaterial);
            for (int x = 0; x < 6; x++)
                for (int z = 0; z < 4; z++)
                    Part(rack, "Cell", new Vector3(-2.04f + x * .816f, .095f, -1.17f + z * .78f),
                        new Vector3(.77f, .035f, .735f), cellMaterial);
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 1.3f, 0);
            collider.size = new Vector3(5, 2.6f, 3.8f);
            var obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = collider.size;
            obstacle.carving = true;
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
        }
    }
}
