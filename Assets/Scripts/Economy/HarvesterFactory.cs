using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    public static class HarvesterFactory
    {
        private const float MinimumSpawnRadius = 7;
        private const float MaximumSpawnRadius = 15;
        private const float SpawnRingSpacing = 2;
        private const int CandidatesPerRing = 24;
        private static readonly Vector3 SpawnHalfExtents = new Vector3(1.2f, .8f, 1.2f);

        public static bool FindSpawn(BaseBuilding home, out Vector3 position) =>
            FindSpawn(home, null, out position);

        private static bool FindSpawn(BaseBuilding home, System.Func<Vector3, bool> acceptsPosition, out Vector3 position)
        {
            var units = home.transform.root.GetComponentsInChildren<RobotController>();
            var terrain = home.transform.parent.Find("Terrain").GetComponent<Collider>();
            Physics.SyncTransforms();
            for (float radius = MinimumSpawnRadius; radius <= MaximumSpawnRadius; radius += SpawnRingSpacing)
            {
                for (int i = 0; i < CandidatesPerRing; i++)
                {
                    float angle = (-90 + i * 360f / CandidatesPerRing) * Mathf.Deg2Rad;
                    var desired = home.transform.position +
                        new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    desired.y = ScenarioLandscape.HeightAt(desired.x, desired.z);
                    if (!NavMesh.SamplePosition(desired, out var hit, 2, NavMesh.AllAreas)) continue;
                    var offset = hit.position - home.transform.position;
                    if (offset.x * offset.x + offset.z * offset.z > MaximumSpawnRadius * MaximumSpawnRadius) continue;
                    bool free = true;
                    foreach (var unit in units)
                    {
                        if ((unit.transform.position - hit.position).sqrMagnitude >= 12) continue;
                        free = false;
                        break;
                    }
                    if (!free) continue;
                    foreach (var collider in Physics.OverlapBox(hit.position + Vector3.up * .9f,
                        SpawnHalfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (collider == terrain) continue;
                        free = false;
                        break;
                    }
                    if (!free || (acceptsPosition != null && !acceptsPosition(hit.position))) continue;
                    position = hit.position;
                    return true;
                }
            }
            position = default;
            return false;
        }
        public static RobotController Create(BaseBuilding home, Vector3 position, int number) => Create(home, position, number, home.Recipe(RobotKind.Harvester));

        public static bool TryCreate(BaseBuilding home, int number, UnitProductionRecipe recipe, out RobotController robot)
        {
            robot = null;
            Vector3 minePosition = default, basePosition = default;
            if (!FindSpawn(home, candidate => TryFindDestinations(home, candidate, number, recipe.Kind,
                out minePosition, out basePosition), out var position))
                return false;
            robot = CreateAt(home, position, number, recipe, minePosition, basePosition);
            return true;
        }

        public static RobotController Create(BaseBuilding home, Vector3 position, int number, UnitProductionRecipe recipe)
        {
            if (!TryFindDestinations(home, position, number, recipe.Kind, out var minePosition, out var basePosition))
                throw new System.InvalidOperationException("Brak dostępnego stanowiska przy bazie lub kopalni.");
            return CreateAt(home, position, number, recipe, minePosition, basePosition);
        }

        private static bool TryFindDestinations(BaseBuilding home, Vector3 from, int number, RobotKind kind,
            out Vector3 minePosition, out Vector3 basePosition)
        {
            minePosition = default;
            if (!TryFindDestination(home.transform, from, number, out basePosition)) return false;
            if (kind != RobotKind.Harvester) return true;
            var mine = home.transform.parent.Find("Mine").GetComponent<Mine>();
            return TryFindDestination(mine.transform, from, number, out minePosition);
        }

        internal static RobotController CreateAt(BaseBuilding home, Vector3 position, int number,
            UnitProductionRecipe recipe, Vector3 minePosition, Vector3 basePosition,
            Transform restoredMineTarget = null, Transform restoredBaseTarget = null)
        {
            var root = home.transform.root;
            var world = home.transform.parent;
            var parent = world.Find("Robots");
            var mine = world.Find("Mine").GetComponent<Mine>();
            var mineTarget = recipe.Kind == RobotKind.Harvester
                ? restoredMineTarget ?? Destination(mine.transform, number, "mine", minePosition) : null;
            var baseTarget = restoredBaseTarget ?? Destination(home.transform, number, "base", basePosition);
            string name = recipe.Kind + "-" + number.ToString("00");
            var body = RobotVisualFactory.Create(name, parent, position, recipe.Kind);

            var robot = body.gameObject.AddComponent<RobotController>();
            robot.InitializeType(recipe.Kind);
            var agent = body.gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 1;
            agent.height = 2;
            agent.speed = 4;
            agent.acceleration = 12;
            agent.stoppingDistance = .15f;
            agent.updateRotation = false;
            agent.avoidancePriority = 30 + number % 50;
            var movement = body.gameObject.AddComponent<RobotMovement>();
            movement.Initialize(agent);
            robot.InitializeMovement(movement, mineTarget);
            var inventory = body.gameObject.AddComponent<RobotInventory>();
            var cycle = recipe.Kind == RobotKind.Harvester ? body.gameObject.AddComponent<RobotHarvestCycle>() : null;
            robot.InitializeEconomy(inventory, cycle, baseTarget);
            if (cycle != null) cycle.Initialize(robot, mine, home);
            robot.Energy.ConfigurePower(recipe.BatteryKWh, recipe.MovementKW, recipe.ConstructionKW,
                world.GetComponent<AlienWorldLighting>(), recipe.MiningKW);
            body.gameObject.AddComponent<RobotCharging>();
            if (recipe.Kind == RobotKind.Hauler)
                body.gameObject.AddComponent<HaulerTowing>().Configure(recipe.TowingKW);
            if (recipe.Kind == RobotKind.Assembler)
            {
                body.gameObject.AddComponent<AssemblerConstruction>();
                body.gameObject.AddComponent<RobotToolEffects>();
            }
            var runtime = body.gameObject.AddComponent<RobotLuaRuntime>();
            runtime.LoadExample(0);
            if (recipe.Kind != RobotKind.Harvester) runtime.SourceCode = "print(unitName .. ': ready')";
            runtime.InitializeSavedProgram("stage01-" + name);
            robot.InitializeLua(runtime);
            if (!GameSaveService.IsRestoring)
            {
                root.GetComponent<FogOfWar>()?.RefreshUnits();
                root.GetComponent<MinimapPanel>()?.Initialize();
                root.GetComponent<SelectionPortraits>()?.Refresh();
            }
            return robot;
        }

        internal static bool TryFindDestination(Transform building, Vector3 from, int number, out Vector3 position, int areaMask = NavMesh.AllAreas)
        {
            var path = new NavMeshPath();
            for (int i = 0; i < 72; i++)
            {
                float angle = (number + i) * 137.5f * Mathf.Deg2Rad;
                float radius = 6 + (number / 12) * 2.8f + (i / 24) * 2;
                var desired = building.position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                desired.y = ScenarioLandscape.HeightAt(desired.x, desired.z);
                if (!NavMesh.SamplePosition(desired, out var hit, 3, areaMask) ||
                    !NavMesh.CalculatePath(from, hit.position, areaMask, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                    continue;
                position = hit.position;
                return true;
            }
            position = default;
            return false;
        }

        private static Transform Destination(Transform building, int number, string suffix, Vector3 position)
        {
            var target = new GameObject("Robot-" + number + " - " + suffix + " destination").transform;
            target.SetParent(building);
            target.position = position;
            return target;
        }
    }
}