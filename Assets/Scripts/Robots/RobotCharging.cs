using UnityEngine;

namespace Protocol
{
    [RequireComponent(typeof(RobotController), typeof(RobotEnergy), typeof(RobotMovement))]
    public sealed class RobotCharging : MonoBehaviour
    {
        private ChargingStationStructure station;
        private Vector3 dock;
        private bool travelling;
        public RobotEnergy Energy { get; private set; }
        public bool IsCharging => station != null && station.IsCharging && station.AssignedRobot == this;
        public bool IsTravelling => station != null && travelling;
        public ChargingStationStructure Station => station;
        public double ChargingKW => IsCharging ? station.CurrentChargingKW : 0;
        public double FullInHours => ChargingKW > 0
            ? (Energy.CapacityKWh - Energy.StoredEnergyKWh) / ChargingKW : double.PositiveInfinity;
        public string Message { get; private set; } = "";

        private void Awake() => Energy = GetComponent<RobotEnergy>();

        public bool TryBegin(ChargingStationStructure destination, bool preserveProgram = false)
        {
            var robot = GetComponent<RobotController>();
            if (robot.Movement.IsBeingTowed || Energy.Percentage >= 100 || destination == null) return false;
            if (!preserveProgram) robot.LuaRuntime?.StopProgram();
            Cancel();
            if (!destination.TryReserve(this, out Vector3 dockPosition)) return false;
            station = destination;
            dock = dockPosition;
            robot.HarvestCycle?.CancelCycle();
            robot.GetComponent<AssemblerConstruction>()?.Cancel();
            robot.GetComponent<HaulerTowing>()?.Cancel();
            robot.Movement?.Cancel();
            if (Vector3.Distance(transform.position, dock) <= 1.5f)
            {
                if (!station.BeginCharging(this)) { Cancel(); return false; }
                travelling = false;
                Message = "Ładowanie";
                return true;
            }
            if (Energy.IsDepleted || !robot.Movement.MoveTo(dock)) { Cancel(); return false; }
            travelling = true;
            Message = "Jazda do stacji";
            return true;
        }

        internal bool ReserveForRecovery(ChargingStationStructure destination, Vector3 from, out Vector3 position)
        {
            position = default;
            if (station != null || !destination.TryReserve(this, from, out position)) return false;
            station = destination;
            dock = position;
            travelling = false;
            Message = "Oczekiwanie na holowanie";
            return true;
        }

        internal bool BeginAfterRecovery()
        {
            if (station == null || Vector3.Distance(transform.position, dock) > 4 ||
                !station.BeginCharging(this)) return false;
            Message = "Ładowanie";
            return true;
        }

        public bool TryBeginNearest()
        {
            ChargingStationStructure nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var candidate in transform.root.GetComponentsInChildren<ChargingStationStructure>())
            {
                if (!candidate.IsAvailable || !candidate.TryFindDock(transform.position, out _)) continue;
                float candidateDistance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (candidateDistance >= distance) continue;
                nearest = candidate;
                distance = candidateDistance;
            }
            bool started = nearest != null && TryBegin(nearest);
            if (!started) Message = "Brak dostępnej stacji ładowania";
            return started;
        }

        private void Update()
        {
            if (station == null || PauseMenu.IsOpen || GameSaveService.IsRestoring) return;
            var movement = GetComponent<RobotMovement>();
            if (travelling)
            {
                if (movement.IsMoving) return;
                if (movement.Result != MovementResult.Arrived || Vector3.Distance(transform.position, dock) > 2f ||
                    !station.BeginCharging(this))
                {
                    Cancel();
                    Message = "Nie udało się dotrzeć do stacji";
                    return;
                }
                travelling = false;
                Message = "Ładowanie";
            }
            else if (movement.IsMoving) Cancel();
        }

        public void ChargingCompleted()
        {
            station = null;
            travelling = false;
            Message = "Bateria pełna";
        }

        public void Cancel()
        {
            if (station == null) return;
            if (travelling) GetComponent<RobotMovement>().Cancel();
            var previous = station;
            station = null;
            travelling = false;
            previous.Release(this);
            Message = "Ładowanie anulowane";
        }

        private void OnDisable() => Cancel();
    }
}
