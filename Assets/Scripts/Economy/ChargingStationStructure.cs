using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    [RequireComponent(typeof(SelectableStructure))]
    public sealed class ChargingStationStructure : MonoBehaviour
    {
        [SerializeField, Min(0)] private double idleConsumptionKW = 1;
        [SerializeField, Min(0)] private double chargingPowerKW = 20;
        [SerializeField] private ColonyPowerGrid grid;
        private Material shell, accent;
        private RobotCharging assignedRobot;
        private bool charging;
        private double allocatedKW;

        public double IdleConsumptionKW => idleConsumptionKW;
        public double MaximumChargingKW => chargingPowerKW;
        public double CurrentChargingKW => charging ? allocatedKW : 0;
        public double TotalConsumptionKW => idleConsumptionKW + CurrentChargingKW;
        public bool IsAvailable => isActiveAndEnabled && assignedRobot == null;
        public bool IsCharging => charging && assignedRobot != null;
        public RobotCharging AssignedRobot => assignedRobot;

        public void Initialize(ColonyPowerGrid powerGrid)
        {
            if (grid != null) grid.Unregister(this);
            grid = powerGrid;
            Register();
        }

        private void Start()
        {
            if (grid == null)
            {
                var scenario = GetComponentInParent<ScenarioBootstrap>();
                if (scenario != null) Initialize(scenario.GetComponentInChildren<ColonyPowerGrid>());
            }
        }

        private void OnEnable() => Register();

        private void Register()
        {
            if (isActiveAndEnabled && grid != null)
            {
                grid.SetConsumption(this, idleConsumptionKW);
                grid.RegisterChargingStation(this);
            }
        }

        public bool TryReserve(RobotCharging robot, out Vector3 dock)
        {
            dock = default;
            if (!IsAvailable || robot == null || grid == null || robot.Energy == null || robot.Energy.Percentage >= 100)
                return false;
            if (!TryFindDock(robot.transform.position, out dock)) return false;
            assignedRobot = robot;
            return true;
        }

        public bool TryFindDock(Vector3 from, out Vector3 dock)
        {
            dock = default;
            Vector2 separation = new Vector2(from.x - transform.position.x, from.z - transform.position.z);
            if (separation.sqrMagnitude <= 36)
            {
                dock = from;
                return true;
            }
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 3, NavMesh.AllAreas)) return false;
            var offsets = new[] { new Vector3(0, 0, -5), new Vector3(5, 0, 0),
                new Vector3(0, 0, 5), new Vector3(-5, 0, 0) };
            float best = float.PositiveInfinity;
            var path = new NavMeshPath();
            foreach (var offset in offsets)
            {
                Vector3 candidate = transform.position + offset;
                candidate.y = ScenarioLandscape.HeightAt(candidate.x, candidate.z);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit end, 1.5f, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete) continue;
                float distance = Vector3.Distance(from, end.position);
                if (distance >= best) continue;
                best = distance;
                dock = end.position;
            }
            return best < float.PositiveInfinity;
        }

        public bool BeginCharging(RobotCharging robot)
        {
            if (assignedRobot != robot || robot == null || robot.Energy == null || robot.Energy.Percentage >= 100)
                return false;
            charging = true;
            grid.RefreshChargingAllocation();
            return true;
        }

        public double RequestedChargingKW(double gameHours)
        {
            if (!IsCharging || assignedRobot.Energy.Percentage >= 100) return 0;
            double remainingKWh = assignedRobot.Energy.CapacityKWh - assignedRobot.Energy.StoredEnergyKWh;
            return gameHours > 0 ? System.Math.Min(chargingPowerKW, remainingKWh / gameHours) : chargingPowerKW;
        }

        public void SetAllocatedPower(double powerKW) => allocatedKW = powerKW;

        public double ReceiveEnergy(double energyKWh)
        {
            if (!IsCharging || energyKWh <= 0) return 0;
            double delivered = assignedRobot.Energy.RechargeEnergy(energyKWh);
            if (assignedRobot.Energy.Percentage >= 100 - 1e-8)
            {
                var robot = assignedRobot;
                Release(robot);
                robot.ChargingCompleted();
            }
            return delivered;
        }

        public void Release(RobotCharging robot)
        {
            if (assignedRobot != robot) return;
            assignedRobot = null;
            charging = false;
            allocatedKW = 0;
            if (grid != null) grid.RefreshChargingAllocation();
        }

        private void OnDisable()
        {
            if (assignedRobot != null) assignedRobot.Cancel();
            if (grid != null) grid.Unregister(this);
        }

        private void OnValidate()
        {
            if (double.IsNaN(idleConsumptionKW) || double.IsInfinity(idleConsumptionKW)) idleConsumptionKW = 1;
            if (double.IsNaN(chargingPowerKW) || double.IsInfinity(chargingPowerKW)) chargingPowerKW = 20;
            idleConsumptionKW = System.Math.Max(0, idleConsumptionKW);
            chargingPowerKW = System.Math.Max(0, chargingPowerKW);
        }

        public static ChargingStationStructure Create(Transform parent, Vector3 position)
        {
            var root = new GameObject("Charging Station");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var station = root.AddComponent<ChargingStationStructure>();
            station.Initialize(parent.GetComponentInChildren<ColonyPowerGrid>());
            var shader = Resources.Load<Shader>("FogSurface");
            station.shell = new Material(shader) { color = new Color(.24f, .3f, .34f) };
            station.accent = new Material(shader) { color = new Color(.1f, .8f, .7f) };
            ConstructionSite.Part(root.transform, "Dock", new Vector3(0, .15f, 0), new Vector3(5, .3f, 4), station.shell);
            ConstructionSite.Part(root.transform, "Power cabinet", new Vector3(0, 1.4f, 1.3f), new Vector3(3, 2.5f, 1), station.shell);
            ConstructionSite.Part(root.transform, "Connector", new Vector3(0, 1.4f, .74f), new Vector3(2, .6f, .12f), station.accent);
            ConstructionSite.AddObstacle(root, new Vector2(6, 5));
            return station;
        }

        private void OnDestroy()
        {
            if (shell != null) Destroy(shell);
            if (accent != null) Destroy(accent);
        }
    }
}
