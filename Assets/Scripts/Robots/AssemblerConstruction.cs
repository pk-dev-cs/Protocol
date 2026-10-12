using System;
using UnityEngine;

namespace Protocol
{
    [RequireComponent(typeof(RobotController), typeof(RobotEnergy))]
    public sealed class AssemblerConstruction : MonoBehaviour
    {
        private ConstructionSite site;
        private ResourceManager resources;
        private double elapsed;
        private int paidWood;
        private bool travelling;
        private Vector3 approach;
        public bool IsBuilding => site != null;
        public Vector3? WorkPosition => site != null && !travelling ? site.transform.position : (Vector3?)null;
        public string Status { get; private set; } = "";

        public bool TryBegin(BuildingPlacement placement, ConstructionRecipe recipe, Vector3 position)
        {
            if (IsBuilding || PauseMenu.IsOpen || Time.timeScale <= 0 || GetComponent<RobotController>().UnitType != "Assembler") return false;
            if (!placement.Validate(this, recipe, position, out approach, out string reason))
            {
                Status = reason;
                return false;
            }
            resources = placement.GetComponent<ResourceManager>();
            if (!resources.TrySpendWood(recipe.WoodCost)) return false;
            paidWood = recipe.WoodCost;
            var robot = GetComponent<RobotController>();
            robot.LuaRuntime?.StopProgram();
            robot.GetComponent<RobotCharging>()?.Cancel();
            site = ConstructionSite.Create(placement.transform.Find("World"), position,
                JsonUtility.FromJson<ConstructionRecipe>(JsonUtility.ToJson(recipe)));
            elapsed = 0;
            travelling = true;
            Status = "Jazda do budowy";
            if (!robot.Movement.MoveTo(approach)) { Cancel(); return false; }
            placement.GetComponent<MinimapPanel>()?.Initialize();
            return true;
        }

        private void Update()
        {
            if (!IsBuilding)
            {
                if (paidWood > 0) Cancel();
                return;
            }
            if (PauseMenu.IsOpen || Time.deltaTime <= 0) return;
            var robot = GetComponent<RobotController>();
            if (travelling)
            {
                if (robot.Movement.IsMoving) return;
                if (robot.Movement.Result != MovementResult.Arrived) { Cancel(); Status = "Budowa anulowana: brak dojazdu"; return; }
                travelling = false;
            }
            if (robot.Movement.IsMoving || Vector3.Distance(transform.position, approach) > 2)
            { Cancel(); return; }
            if (robot.Energy.IsDepleted) { Status = "Budowa wstrzymana: brak energii"; return; }
            elapsed += PerformWork(Math.Min(Time.deltaTime, site.Recipe.DurationSeconds - elapsed));
            site.SetProgress((float)(elapsed / site.Recipe.DurationSeconds));
            Status = site.Recipe.DisplayName + " " + Mathf.FloorToInt(site.Progress * 100) + "%";
            if (elapsed + .000001 < site.Recipe.DurationSeconds) return;
            var scenario = GetComponentInParent<ScenarioBootstrap>();
            var parent = site.transform.parent;
            Vector3 position = site.transform.position;
            var kind = site.Recipe.Kind;
            site.gameObject.SetActive(false);
            Destroy(site.gameObject);
            site = null;
            paidWood = 0;
            if (kind == ConstructionKind.SolarPanel)
                SolarPanel.Create(parent, position, scenario.GetComponentInChildren<ColonyPowerGrid>(),
                    scenario.GetComponentInChildren<PlanetEnvironmentSystem>());
            else if (kind == ConstructionKind.ChargingStation) ChargingStationStructure.Create(parent, position);
            else BatteryStorage.Create(parent, position, scenario.GetComponentInChildren<ColonyPowerGrid>());
            scenario.GetComponent<MinimapPanel>()?.Initialize();
            scenario.GetComponent<SelectionPortraits>()?.Refresh();
            Status = "Budowa ukończona";
        }

        public void Cancel()
        {
            if (IsBuilding && travelling) GetComponent<RobotController>().Movement?.Cancel();
            if (site != null)
            {
                site.gameObject.SetActive(false);
                Destroy(site.gameObject);
            }
            site = null;
            if (resources != null && paidWood > 0) resources.Add(ResourceKind.Wood, paidWood);
            paidWood = 0;
            travelling = false;
            Status = "";
        }

        private void OnDisable() => Cancel();

        public double PerformWork(double simulationSeconds)
        {
            if (double.IsNaN(simulationSeconds) || double.IsInfinity(simulationSeconds) || simulationSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds));
            var robot = GetComponent<RobotController>();
            var energy = GetComponent<RobotEnergy>();
            if (!isActiveAndEnabled || PauseMenu.IsOpen || Time.timeScale <= 0 || energy.IsDepleted ||
                (robot.Movement != null && robot.Movement.IsMoving)) return 0;
            double requested = energy.ConstructionKW * simulationSeconds * energy.GameHoursPerSimulationSecond;
            double used = energy.ConsumeConstruction(simulationSeconds);
            if (energy.ConstructionKW == 0) return simulationSeconds;
            return requested <= 0 ? 0 : simulationSeconds * Math.Min(1, used / requested);
        }
    }
}
