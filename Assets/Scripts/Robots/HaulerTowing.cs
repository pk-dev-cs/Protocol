using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    [RequireComponent(typeof(RobotController), typeof(RobotMovement), typeof(RobotEnergy))]
    public sealed class HaulerTowing : MonoBehaviour
    {
        private enum Stage { Idle, Pickup, Transport }
        [SerializeField, Min(0)] private float towingKW = 6;
        private RobotController target;
        private ChargingStationStructure station;
        private Vector3 dock;
        private Stage stage;
        public bool IsBusy => stage != Stage.Idle;
        public bool IsTransporting => stage == Stage.Transport;
        public float TowingKW => towingKW;
        public RobotController Target => target;
        public string Message { get; private set; } = "";
        public bool Succeeded { get; private set; }

        public void Configure(float powerKW)
        {
            if (float.IsNaN(powerKW) || float.IsInfinity(powerKW) || powerKW < 0)
                throw new System.ArgumentOutOfRangeException(nameof(powerKW));
            towingKW = powerKW;
        }

        public bool CanRecover(RobotController candidate) => candidate != null && candidate.isActiveAndEnabled &&
            candidate != GetComponent<RobotController>() &&
            (candidate.UnitType == "Harvester" || candidate.UnitType == "Assembler") &&
            candidate.Energy != null && candidate.Energy.IsDepleted &&
            candidate.Movement != null && !candidate.Movement.IsBeingTowed &&
            candidate.GetComponent<RobotCharging>() is RobotCharging charging && charging.Station == null;

        public bool TryBegin(RobotController candidate, bool preserveProgram = false)
        {
            if (IsBusy || !isActiveAndEnabled || GetComponent<RobotController>().UnitType != "Hauler" ||
                GetComponent<RobotEnergy>().IsDepleted || !CanRecover(candidate))
                return Reject("Tow wymaga rozładowanego Harvestera lub Assemblera i sprawnego Haulera.");

            var charging = candidate.GetComponent<RobotCharging>();
            ChargingStationStructure nearest = null;
            Vector3 bestDock = default;
            float best = float.PositiveInfinity;
            foreach (var available in transform.root.GetComponentsInChildren<ChargingStationStructure>())
            {
                if (!available.IsAvailable || !available.TryFindDock(transform.position, out var point)) continue;
                float distance = (point - transform.position).sqrMagnitude;
                if (distance >= best) continue;
                nearest = available;
                bestDock = point;
                best = distance;
            }
            if (nearest == null || !charging.ReserveForRecovery(nearest, transform.position, out bestDock))
                return Reject("Brak dostępnej stacji ładowania.");

            var robot = GetComponent<RobotController>();
            if (!preserveProgram) robot.LuaRuntime?.StopProgram();
            robot.GetComponent<RobotCharging>()?.Cancel();
            candidate.LuaRuntime?.StopProgram();
            candidate.HarvestCycle?.CancelCycle();
            candidate.GetComponent<AssemblerConstruction>()?.Cancel();
            target = candidate;
            station = nearest;
            dock = bestDock;
            Succeeded = false;
            stage = Stage.Pickup;
            target.Movement.SetTransportOwner(this);
            Vector3 direction = transform.position - candidate.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .01f) direction = Vector3.back;
            Vector3 pickup = candidate.transform.position + direction.normalized * 2.8f;
            if (!NavMesh.SamplePosition(pickup, out var hit, 2, NavMesh.AllAreas) ||
                !robot.Movement.MoveTo(hit.position))
            {
                Cancel();
                return Reject("Brak dostępnej trasy do holowanego robota.");
            }
            Message = "Jazda po " + candidate.DisplayName;
            return true;
        }

        private bool Reject(string message)
        {
            Message = message;
            return false;
        }

        private void Update()
        {
            if (!IsBusy || PauseMenu.IsOpen || Time.deltaTime <= 0 || GameSaveService.IsRestoring) return;
            var movement = GetComponent<RobotMovement>();
            if (target == null || !target.isActiveAndEnabled || station == null || !station.isActiveAndEnabled ||
                station.AssignedRobot != target.GetComponent<RobotCharging>() || GetComponent<RobotEnergy>().IsDepleted)
            {
                Cancel();
                Message = "Holowanie przerwane — robot lub stacja niedostępne.";
                return;
            }
            if (movement.IsMoving) return;
            if (movement.Result != MovementResult.Arrived)
            {
                Cancel();
                Message = "Holowanie przerwane — trasa niedostępna.";
                return;
            }
            if (stage == Stage.Pickup)
            {
                if (Vector3.Distance(transform.position, target.transform.position) > 4)
                {
                    Cancel();
                    Message = "Nie udało się dotrzeć do robota.";
                    return;
                }
                stage = Stage.Transport;
                Message = "Holowanie " + target.DisplayName;
                if (!movement.MoveTo(dock))
                {
                    Cancel();
                    Message = "Brak trasy do stacji.";
                }
                return;
            }
            FollowTarget();
            var charging = target.GetComponent<RobotCharging>();
            target.Movement.SetTransportOwner(null);
            if (!charging.BeginAfterRecovery())
            {
                Cancel();
                Message = "Nie udało się przekazać robota do stacji.";
                return;
            }
            target = null;
            station = null;
            stage = Stage.Idle;
            Succeeded = true;
            Message = "Robot przekazany do ładowania";
        }

        private void LateUpdate()
        {
            if (IsTransporting && Time.timeScale > 0 && !PauseMenu.IsOpen && !GameSaveService.IsRestoring) FollowTarget();
        }

        private void FollowTarget()
        {
            if (target == null) return;
            Vector3 position = transform.position + transform.forward * 3;
            if (!NavMesh.SamplePosition(position, out var hit, 2, NavMesh.AllAreas) ||
                Vector3.Distance(hit.position, transform.position) < 2.2f) return;
            target.transform.SetPositionAndRotation(hit.position, transform.rotation);
        }

        public void Cancel()
        {
            if (!IsBusy) return;
            stage = Stage.Idle;
            GetComponent<RobotMovement>()?.Cancel();
            if (target != null)
            {
                target.Movement?.SetTransportOwner(null);
                target.GetComponent<RobotCharging>()?.Cancel();
            }
            target = null;
            station = null;
            Succeeded = false;
            Message = "Holowanie anulowane";
        }

        private void OnDisable() => Cancel();
    }
}
