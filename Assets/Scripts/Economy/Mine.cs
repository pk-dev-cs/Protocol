using UnityEngine;

namespace Protocol
{
    public sealed class Mine : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float secondsPerUnit = 0.75f;

        public float SecondsPerUnit => Mathf.Max(0.1f, secondsPerUnit);

        public bool CanMine(RobotController robot)
        {
            return isActiveAndEnabled && robot != null && robot.Inventory != null && robot.MineDestination != null && robot.MineDestination.IsChildOf(transform) && Vector3.Distance(
                robot.transform.position,
                robot.MineDestination.position) <= 0.65f;
        }

        public bool TryExtractUnit(RobotController robot) => CanMine(robot) && robot.Inventory.TryAddUnit();
    }
}
