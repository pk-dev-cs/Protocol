using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    public sealed class BaseBuilding : MonoBehaviour
    {
        private ResourceManager resources;
        public const int HarvesterWoodCost = 100;
        public const float ProductionSeconds = 8;
        [SerializeField] private UnitProductionRecipe harvester = new UnitProductionRecipe();
        [SerializeField] private UnitProductionRecipe assembler = new UnitProductionRecipe
        {
            Kind = RobotKind.Assembler, WoodCost = 150, DurationSeconds = 12
        };
        [SerializeField] private UnitProductionRecipe hauler = new UnitProductionRecipe
        {
            Kind = RobotKind.Hauler, WoodCost = 200, DurationSeconds = 16
        };
        private int nextHauler = 1;
        private UnitProductionRecipe activeRecipe;
        private float productionElapsed;
        private float deploymentRetrySeconds;
        private int paidWood;
        private int nextAssembler = 1;
        public float ProductionProgress => !IsProducing ? 0 : Mathf.Clamp01(productionElapsed / activeRecipe.DurationSeconds);
        public string ProductionName => IsProducing ? activeRecipe.Kind.ToString() : "";
        public UnitProductionRecipe Recipe(RobotKind kind) => kind == RobotKind.Hauler ? hauler : kind == RobotKind.Assembler ? assembler : harvester;

        public bool IsProducing { get; private set; }
        public bool IsWaitingForDeployment { get; private set; }
        public string ProductionStatus => !IsProducing ? "" : !HasPower ? "NO POWER — produkcja wstrzymana"
            : IsWaitingForDeployment ? ProductionName + " 100% — oczekiwanie na wolne miejsce"
            : ProductionName + " " + (ProductionProgress * 100).ToString("0") + "%";
        public ColonyPowerGrid PowerGrid => GetComponent<ColonyPowerGrid>();
        public bool HasPower => PowerGrid != null && PowerGrid.HasPower;

        private int nextHarvester = 3;

        public bool TryBeginProduction() => TryBeginProduction(RobotKind.Harvester);

        public bool TryBeginProduction(RobotKind kind)
        {
            var recipe = Recipe(kind);
            if (!isActiveAndEnabled || !HasPower || IsProducing || resources == null || recipe == null ||
                !recipe.IsValid || (PowerGrid.StoredEnergyKWh <= 0 && PowerGrid.BalanceKW < recipe.ProductionKW) ||
                !resources.TrySpendWood(recipe.WoodCost))
                return false;
            activeRecipe = JsonUtility.FromJson<UnitProductionRecipe>(JsonUtility.ToJson(recipe));
            activeRecipe.Kind = kind;
            paidWood = recipe.WoodCost;
            productionElapsed = 0;
            deploymentRetrySeconds = 0;
            IsProducing = true;
            IsWaitingForDeployment = false;
            PowerGrid.SetConsumption(this, activeRecipe.ProductionKW);
            return true;
        }

        public bool AdvanceProduction(float simulationSeconds)
        {
            if (float.IsNaN(simulationSeconds) || float.IsInfinity(simulationSeconds) || simulationSeconds < 0)
                throw new System.ArgumentOutOfRangeException(nameof(simulationSeconds));
            if (!IsProducing) return true;
            if (!HasPower || simulationSeconds == 0) return false;
            if (IsWaitingForDeployment)
            {
                deploymentRetrySeconds -= simulationSeconds;
                if (deploymentRetrySeconds > 0) return false;
            }
            productionElapsed = Mathf.Min(activeRecipe.DurationSeconds, productionElapsed + simulationSeconds);
            if (productionElapsed < activeRecipe.DurationSeconds) return false;
            return CompleteProduction();
        }

        public bool CompleteProduction()
        {
            if (!IsProducing)
                throw new System.InvalidOperationException("Brak produkcji.");
            if (!HasPower) throw new System.InvalidOperationException("NO POWER");
            if (productionElapsed < activeRecipe.DurationSeconds)
                throw new System.InvalidOperationException("Produkcja nie została ukończona.");
            PowerGrid.Unregister(this);
            int number = activeRecipe.Kind == RobotKind.Hauler ? nextHauler : activeRecipe.Kind == RobotKind.Assembler ? nextAssembler : nextHarvester;
            if (!HarvesterFactory.TryCreate(this, number, activeRecipe, out _))
            {
                IsWaitingForDeployment = true;
                deploymentRetrySeconds = .25f;
                return false;
            }
            if (activeRecipe.Kind == RobotKind.Hauler) nextHauler++;
            else if (activeRecipe.Kind == RobotKind.Assembler) nextAssembler++;
            else nextHarvester++;
            IsProducing = false;
            IsWaitingForDeployment = false;
            paidWood = 0;
            return true;
        }

        public void CancelProduction()
        {
            if (!IsProducing)
                return;
            if (resources != null)
                resources.Add(ResourceKind.Wood, paidWood);
            IsProducing = false;
            IsWaitingForDeployment = false;
            paidWood = 0;
            if (PowerGrid != null) PowerGrid.Unregister(this);
        }

        private void OnDisable() => CancelProduction();

        public float DepositSeconds => 1f;

        public void Initialize(ResourceManager manager) => resources = manager;

        internal bool TryResolveReturnTarget(RobotController robot, NavMeshAgent agent, out Vector3 position)
        {
            position = default;
            if (!isActiveAndEnabled || robot == null || robot.BaseDestination == null ||
                !robot.BaseDestination.IsChildOf(transform) || agent == null || !agent.isOnNavMesh)
                return false;

            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(robot.BaseDestination.position, out var hit, .75f, agent.areaMask) &&
                agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                position = hit.position;
            }
            else if (!HarvesterFactory.TryFindDestination(transform, robot.transform.position, 0, out position, agent.areaMask))
            {
                return false;
            }

            robot.BaseDestination.position = position;
            return true;
        }
        public bool CanDeposit(RobotController robot)
        {
            return isActiveAndEnabled && resources != null && resources.isActiveAndEnabled && robot != null && robot.Inventory != null && robot.BaseDestination != null && robot.BaseDestination.IsChildOf(transform) && Vector3.Distance(
                robot.transform.position,
                robot.BaseDestination.position) <= 0.65f;
        }

        public bool TryDeposit(RobotController robot, out int deposited)
        {
            deposited = 0;
            if (!CanDeposit(robot))
                return false;
            deposited = robot.Inventory.DepositInto(resources);
            return true;
        }
    }
}
