using UnityEngine;

namespace Protocol
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SelectableStructure))]
    public sealed class BatteryStorage : MonoBehaviour
    {
        [SerializeField, Min(0)] private double capacityKWh = 200;
        [SerializeField, Min(0)] private double idleConsumptionKW = .2;
        [SerializeField] private ColonyPowerGrid grid;



        public double CapacityKWh => capacityKWh;
        public double IdleConsumptionKW => idleConsumptionKW;
        public string Status => !isActiveAndEnabled || grid == null || !grid.isActiveAndEnabled
            ? "Offline" : grid.HasPower ? "Online" : "No Power";

        public void Initialize(ColonyPowerGrid powerGrid)
        {
            if (grid != powerGrid && grid != null) grid.Unregister(this);
            grid = powerGrid;
            Register();
        }

        private void OnEnable() => Register();

        private void Start()
        {
            if (grid == null)
            {
                var scenario = GetComponentInParent<ScenarioBootstrap>();
                if (scenario != null) Initialize(scenario.GetComponentInChildren<ColonyPowerGrid>());
            }
            BuildVisuals();
        }

        private void Register()
        {
            if (!isActiveAndEnabled || grid == null) return;
            grid.RegisterStorage(this, capacityKWh);
            grid.SetConsumption(this, idleConsumptionKW);
            grid.RefreshChargingAllocation();
        }

        private void OnDisable()
        {
            if (grid == null) return;
            grid.Unregister(this);
            grid.RefreshChargingAllocation();
        }

        private void OnValidate()
        {
            if (double.IsNaN(capacityKWh) || double.IsInfinity(capacityKWh)) capacityKWh = 200;
            if (double.IsNaN(idleConsumptionKW) || double.IsInfinity(idleConsumptionKW)) idleConsumptionKW = .2;
            capacityKWh = System.Math.Max(0, capacityKWh);
            idleConsumptionKW = System.Math.Max(0, idleConsumptionKW);
        }

        public static BatteryStorage Create(Transform parent, Vector3 position, ColonyPowerGrid powerGrid)
        {
            var root = new GameObject("Battery Storage");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var storage = root.AddComponent<BatteryStorage>();
            storage.BuildVisuals();
            storage.Initialize(powerGrid);
            return storage;
        }

        public void BuildVisuals()
        {
            if (transform.Find("Visuals") == null)
            {
                var prefab = Resources.Load<GameObject>("Buildings/BatteryStorage");
                if (prefab == null) throw new System.InvalidOperationException("Missing Battery Storage model");
                var old = transform.Find("Battery cabinets");
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
            }
            var collider = GetComponent<BoxCollider>();
            if (collider == null) collider = gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.up * 1.5f;
            collider.size = new Vector3(6, 3, 5);
            var obstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle == null) obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = collider.size;
            obstacle.carving = true;
        }
    }
}