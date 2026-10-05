using UnityEngine;

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
        private UnitProductionRecipe activeRecipe;
        private float productionElapsed;
        private int paidWood;
        private int nextAssembler = 1;
        public float ProductionProgress => !IsProducing ? 0 : Mathf.Clamp01(productionElapsed / activeRecipe.DurationSeconds);
        public string ProductionName => IsProducing ? activeRecipe.Kind.ToString() : "";
        public UnitProductionRecipe Recipe(RobotKind kind) => kind == RobotKind.Assembler ? assembler : harvester;

        public bool IsProducing { get; private set; }
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
            IsProducing = true;
            PowerGrid.SetConsumption(this, activeRecipe.ProductionKW);
            return true;
        }

        public bool AdvanceProduction(float simulationSeconds)
        {
            if (float.IsNaN(simulationSeconds) || float.IsInfinity(simulationSeconds) || simulationSeconds < 0)
                throw new System.ArgumentOutOfRangeException(nameof(simulationSeconds));
            if (!IsProducing) return true;
            if (!HasPower || simulationSeconds == 0) return false;
            productionElapsed += simulationSeconds;
            if (productionElapsed < activeRecipe.DurationSeconds) return false;
            CompleteProduction();
            return true;
        }

        public void CompleteProduction()
        {
            if (!IsProducing)
                throw new System.InvalidOperationException("Brak produkcji.");
            if (!HasPower) throw new System.InvalidOperationException("NO POWER");
            if (productionElapsed < activeRecipe.DurationSeconds)
                throw new System.InvalidOperationException("Produkcja nie została ukończona.");
            if (!HarvesterFactory.FindSpawn(this, out var position))
                throw new System.InvalidOperationException("Brak miejsca na harvestera przy bazie.");
            int number = activeRecipe.Kind == RobotKind.Assembler ? nextAssembler : nextHarvester;
            HarvesterFactory.Create(this, position, number, activeRecipe);
            if (activeRecipe.Kind == RobotKind.Assembler) nextAssembler++; else nextHarvester++;
            IsProducing = false;
            PowerGrid.Unregister(this);
        }

        public void CancelProduction()
        {
            if (!IsProducing)
                return;
            if (resources != null)
                resources.Add(ResourceKind.Wood, paidWood);
            IsProducing = false;
            if (PowerGrid != null) PowerGrid.Unregister(this);
        }

        private void OnDisable() => CancelProduction();

        public float DepositSeconds => 1f;

        public void Initialize(ResourceManager manager) => resources = manager;

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
