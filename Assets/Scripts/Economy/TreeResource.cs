using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    public sealed class TreeResource : MonoBehaviour
    {
        public int WoodRemaining { get; private set; } = 30;

        public RobotController ReservedBy { get; private set; }

        public bool Available => isActiveAndEnabled && WoodRemaining > 0;

        public bool Reserve(RobotController robot)
        {
            if (!Available || (ReservedBy != null && ReservedBy != robot))
                return false;
            ReservedBy = robot;
            return true;
        }

        public void Release(RobotController robot)
        {
            if (ReservedBy == robot)
                ReservedBy = null;
        }

        public bool CanChop(RobotController robot) => Available && ReservedBy == robot && Vector3.Distance(
            robot.transform.position,
            transform.position) < 4.5f;

        public bool Extract(RobotController robot)
        {
            if (!CanChop(robot) || !robot.Inventory.TryAddUnit(ResourceKind.Wood))
                return false;
            if (--WoodRemaining == 0)
            {
                transform.root.GetComponent<FogOfWar>()?.TreeRemoved(transform.position);
                gameObject.SetActive(false); // Also removes the carving obstacle; no invisible collision remains.
            }

            return true;
        }

        public bool Approach(Vector3 from, out Vector3 point)
        {
            point = default;
            var path = new NavMeshPath();
            // Sample a walkable ring outside the trunk, preferring the near side.
            var direction = (from - transform.position).normalized;
            for (int i = 0; i < 16; i++)
            {
                var offset = Quaternion.Euler(0, i * 22.5f, 0) * direction * 2.8f;
                if (!NavMesh.SamplePosition(transform.position + offset, out var hit, 1.5f, NavMesh.AllAreas))
                    continue;
                // A parked robot carves its own position; sample beyond that small hole.
                if (!NavMesh.SamplePosition(from, out var origin, 2.5f, NavMesh.AllAreas))
                    continue;
                if (NavMesh.CalculatePath(
                    origin.position,
                    hit.position,
                    NavMesh.AllAreas,
                    path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    point = hit.position;
                    return true;
                }
            }

            return false;
        }

        public static void InitializeForest(Transform world)
        {
            foreach (Transform tree in world.Find("Trees"))
            {
                if (tree.GetComponent<TreeResource>() == null)
                    tree.gameObject.AddComponent<TreeResource>();
                if (tree.GetComponent<NavMeshObstacle>() != null)
                    continue;
                var obstacle = tree.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.radius = .45f;
                obstacle.height = 4;
                obstacle.center = Vector3.up * 2;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
            }
        }
    }
}
