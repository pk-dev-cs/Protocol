using UnityEngine;

namespace Protocol
{
    public enum HarvestState
    {
        Idle,
        Moving,
        Mining,
        Returning,
        Depositing,
        Error
    }

    /// <summary>One manually started harvesting trip. Lua is not involved.</summary>
    public sealed class RobotHarvestCycle : MonoBehaviour
    {
        private RobotController robot;
        private Mine mine;
        private BaseBuilding baseBuilding;
        private float elapsed;
        public HarvestState State { get; private set; }

        public bool IsRunning => State != HarvestState.Idle && State != HarvestState.Error;

        public string Message { get; private set; } = "";


        public void Initialize(RobotController controller, Mine source, BaseBuilding destination)

        {
            robot = controller;
            mine = source;
            baseBuilding = destination;
        }

        public bool StartCycle()

        {
            if (robot != null && robot.LuaRuntime != null && robot.LuaRuntime.State == LuaProgramState.Running)
                return false;
            if (!isActiveAndEnabled || IsRunning || robot == null || robot.Movement == null || robot.Movement.IsMoving)
                return false;
            if (!robot.isActiveAndEnabled || !robot.Movement.isActiveAndEnabled || robot.Inventory == null || !robot.Inventory.isActiveAndEnabled)
                return Fail("Robot lub jego komponenty są niedostępne.");
            if (mine == null || !mine.isActiveAndEnabled || baseBuilding == null || !baseBuilding.isActiveAndEnabled)
                return Fail("Brak kopalni lub bazy.");
            elapsed = 0;
            if (robot.Inventory.IsFull)
                return ReturnToBase();
            if (!robot.Movement.MoveTo(robot.MineDestination))
                return Fail(robot.Movement.Message);
            State = HarvestState.Moving;
            Message = "Jazda do kopalni...";
            return true;
        }

        public void ClearFeedback()

        {
            if (IsRunning)
                return;
            State = HarvestState.Idle;
            Message = "";
        }

        private void Update()

        {
            if (PauseMenu.IsOpen)
                return;
            if (!IsRunning)
                return;
            if (robot == null || !robot.isActiveAndEnabled || robot.Movement == null || !robot.Movement.isActiveAndEnabled || robot.Inventory == null || !robot.Inventory.isActiveAndEnabled || mine == null || !mine.isActiveAndEnabled || baseBuilding == null || !baseBuilding.isActiveAndEnabled)
            {
                Fail("Przerwano cykl: robot, kopalnia lub baza niedostępna.");
                return;
            }

            if (State == HarvestState.Moving || State == HarvestState.Returning)
            {
                if (robot.Movement.IsMoving)
                    return;
                if (robot.Movement.Result != MovementResult.Arrived)
                {
                    Fail(robot.Movement.Message);
                    return;
                }

                elapsed = 0;
                State = State == HarvestState.Moving ? HarvestState.Mining : HarvestState.Depositing;
                Message = State == HarvestState.Mining ? "Wydobywanie iron..." : "Rozładunek w bazie...";
                return;
            }

            if (State == HarvestState.Mining)
            {
                if (!mine.CanMine(robot))
                {
                    Fail("Robot jest poza zasięgiem kopalni.");
                    return;
                }

                elapsed += Time.deltaTime;
                if (elapsed < mine.SecondsPerUnit)
                    return;
                elapsed -= mine.SecondsPerUnit;
                if (!mine.TryExtractUnit(robot))
                {
                    Fail("Nie można wydobyć surowca.");
                    return;
                }

                if (robot.Inventory.IsFull)
                    ReturnToBase();
            }
            else if (State == HarvestState.Depositing)
            {
                if (!baseBuilding.CanDeposit(robot))
                {
                    Fail("Robot jest poza zasięgiem bazy.");
                    return;
                }

                elapsed += Time.deltaTime;
                if (elapsed < baseBuilding.DepositSeconds)
                    return;
                if (!baseBuilding.TryDeposit(robot, out int deposited))
                {
                    Fail("Nie udało się oddać surowca.");
                    return;
                }

                State = HarvestState.Idle;
                Message = $"Cykl zakończony. Oddano {deposited} iron.";
            }
        }

        private bool ReturnToBase()

        {
            if (!robot.Movement.MoveTo(robot.BaseDestination))
                return Fail(robot.Movement.Message);
            State = HarvestState.Returning;
            Message = "Pełny magazyn. Powrót do bazy...";
            return true;
        }

        private bool Fail(string message)

        {
            State = HarvestState.Error;
            Message = message;
            if (robot != null && robot.Movement != null)
                robot.Movement.Cancel();
            return false;
        }

        public void CancelCycle()

        {
            if (!IsRunning)
                return;
            if (robot != null && robot.Movement != null)
                robot.Movement.Cancel();
            State = HarvestState.Idle;
            Message = "Cykl przerwany. Cargo zachowane.";
        }

        private void OnDisable() => CancelCycle();

    }
}
