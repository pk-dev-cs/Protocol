using UnityEngine;

namespace Protocol
{
    public sealed class BaseBuilding : MonoBehaviour
    {
        private ResourceManager resources;
        public const int HarvesterWoodCost = 100;
        public const float ProductionSeconds = 8;

        public bool IsProducing { get; private set; }

        private int nextHarvester = 3;

        public bool TryBeginProduction()
        {
            if (!isActiveAndEnabled || IsProducing || resources == null || !resources.TrySpendWood(HarvesterWoodCost))
                return false;
            IsProducing = true;
            return true;
        }

        public void CompleteProduction()
        {
            if (!IsProducing)
                throw new System.InvalidOperationException("Brak produkcji.");
            if (!HarvesterFactory.FindSpawn(this, out var position))
                throw new System.InvalidOperationException("Brak miejsca na harvestera przy bazie.");
            HarvesterFactory.Create(this, position, nextHarvester++);
            IsProducing = false;
        }

        public void CancelProduction()
        {
            if (!IsProducing)
                return;
            if (resources != null)
                resources.Add(ResourceKind.Wood, HarvesterWoodCost);
            IsProducing = false;
        }

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
