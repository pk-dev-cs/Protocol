using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    public static class HarvesterFactory
    {
        public static bool FindSpawn(BaseBuilding home, out Vector3 position)
        {
            var units = home.transform.root.GetComponentsInChildren<RobotController>();
            for (int i = 0; i < 120; i++)
            {
                float angle = i * 137.5f * Mathf.Deg2Rad, radius = 9 + i / 24 * 3;
                var p = home.transform.position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                if (!NavMesh.SamplePosition(p, out var hit, 2, NavMesh.AllAreas))
                    continue;
                bool free = true;
                foreach (var unit in units)
                    if ((unit.transform.position - hit.position).sqrMagnitude < 12)
                    {
                        free = false;
                        break;
                    }

                if (free)
                {
                    position = hit.position;
                    return true;
                }
            }

            position = default;
            return false;
        }

        public static RobotController Create(BaseBuilding home, Vector3 position, int number) => Create(home, position, number, home.Recipe(RobotKind.Harvester));

        public static RobotController Create(BaseBuilding home, Vector3 position, int number, UnitProductionRecipe recipe)
        {
            var root = home.transform.root;
            var world = home.transform.parent;
            var parent = world.Find("Robots");
            var model = parent.GetChild(0);
            var mine = world.Find("Mine").GetComponent<Mine>();
            // Validate both destinations before creating a unit or taking its production payment.
            Transform mineTarget = Destination(mine.transform, number, "mine"), baseTarget = null;
            try
            {
                baseTarget = Destination(home.transform, number, "base");
            }
            catch
            {
                Object.Destroy(mineTarget.gameObject);
                throw;
            }

            string name = recipe.Kind + "-" + number.ToString("00");
            ScenarioBootstrap.BuildRobot(
                name,
                parent,
                position,
                model.Find("Chassis").GetComponent<Renderer>().sharedMaterial,
                model.Find("Cargo tray").GetComponent<Renderer>().sharedMaterial,
                model.Find("Eye").GetComponent<Renderer>().sharedMaterial);
            var body = parent.GetChild(parent.childCount - 1);
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
            if (recipe.Kind == RobotKind.Harvester)
            {
                robot.Energy.ConfigurePower(recipe.BatteryKWh, recipe.MovementKW, recipe.ConstructionKW,
                    world.GetComponent<AlienWorldLighting>(), recipe.MiningKW);
                body.gameObject.AddComponent<RobotCharging>();
            }
            if (recipe.Kind == RobotKind.Assembler)
            {
                robot.Energy.ConfigurePower(recipe.BatteryKWh, recipe.MovementKW, recipe.ConstructionKW, world.GetComponent<AlienWorldLighting>());
                body.gameObject.AddComponent<AssemblerConstruction>();
                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.name = "Construction arm";
                arm.transform.SetParent(body, false);
                arm.transform.localPosition = new Vector3(0, 1.65f, .1f);
                arm.transform.localScale = new Vector3(.3f, 1.2f, .35f);
                arm.transform.localRotation = Quaternion.Euler(0, 0, 35);
                arm.GetComponent<Renderer>().sharedMaterial = model.Find("Eye").GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(arm.GetComponent<Collider>());
            }
            var runtime = body.gameObject.AddComponent<RobotLuaRuntime>();
            runtime.LoadExample(0);
            if (recipe.Kind == RobotKind.Assembler) runtime.SourceCode = "print(unitName .. ': ready')";
            runtime.InitializeSavedProgram("stage01-" + name);
            robot.InitializeLua(runtime);
            root.GetComponent<FogOfWar>()?.RefreshUnits();
            root.GetComponent<MinimapPanel>()?.Initialize();
            root.GetComponent<SelectionPortraits>()?.Refresh();
            return robot;
        }

        static Transform Destination(Transform building, int number, string suffix)
        {
            float angle = (number * 137.5f) * Mathf.Deg2Rad;
            float radius = 6 + (number / 12) * 2.8f;
            Vector3 desired = building.position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            if (!NavMesh.SamplePosition(desired, out var hit, 3, NavMesh.AllAreas))
                throw new System.InvalidOperationException("Brak stanowiska przy " + building.name);
            var target = new GameObject("Harvester-" + number + " - " + suffix + " destination").transform;
            target.SetParent(building);
            target.position = hit.position;
            return target;
        }
    }
}
