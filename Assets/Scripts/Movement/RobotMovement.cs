using System;
using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    public enum MovementResult
    {
        None,
        Moving,
        Arrived,
        Failed,
        Cancelled
    }

    public sealed class RobotMovement : MonoBehaviour
    {
        private NavMeshAgent agent;
        private HaulerTowing transportOwner;
        public bool IsBeingTowed => transportOwner != null;
        internal HaulerTowing TransportOwner => transportOwner;

        internal void SetTransportOwner(HaulerTowing owner)
        {
            Cancel();
            transportOwner = owner;
            Park();
            if (owner == null && NavMesh.SamplePosition(transform.position, out var hit, 3, NavMesh.AllAreas))
                transform.position = hit.position;
            lastEnergyPosition = transform.position;
        }
        private RobotEnergy energy;
        private Vector3 lastEnergyPosition;
        private NavMeshObstacle parkedObstacle;
        private bool preparingPath;
        private int activateAfterFrame;
        private Transform target;
        private Transform manualTarget;
        private Vector3 destination;
        private Vector3 progressPosition;
        private float startedAt, lastProgressAt;
        private float allowedTravelSeconds = 30;

        public MovementResult Result { get; private set; }

        public bool IsMoving => Result == MovementResult.Moving;

        public string Message { get; private set; } = "Gotowy do jazdy.";

        public event Action<MovementResult> Completed;

        public void Initialize(NavMeshAgent navAgent)
        {
            agent = navAgent;
            energy = GetComponent<RobotEnergy>();
            if (energy == null) energy = gameObject.AddComponent<RobotEnergy>();
            energy.Depleted -= OnEnergyDepleted;
            energy.Depleted += OnEnergyDepleted;
            lastEnergyPosition = transform.position;
            agent.enabled = false;
            parkedObstacle = gameObject.AddComponent<NavMeshObstacle>();
            parkedObstacle.enabled = false;
            parkedObstacle.shape = NavMeshObstacleShape.Capsule;
            parkedObstacle.radius = agent.radius;
            parkedObstacle.height = agent.height;
            parkedObstacle.center = new Vector3(0, agent.height / 2, 0);
            parkedObstacle.carving = true;
            parkedObstacle.carveOnlyStationary = false;
            Park();
        }

        public bool MoveTo(Transform requestedTarget)
        {
            if (IsBeingTowed) return false;
            if (IsMoving)
                Cancel();
            if (energy != null && energy.IsDepleted)
                return Fail("Brak energii. Robot nie może jechać.");
            if (requestedTarget == null)
                return Fail("Cel nie istnieje.");
            if (!isActiveAndEnabled || agent == null || parkedObstacle == null)
                return Fail("Ruch robota jest niedostępny.");
            // Carving changes become queryable on a later frame. Never enable agent and obstacle together.
            parkedObstacle.enabled = false;
            preparingPath = true;
            activateAfterFrame = Time.frameCount + 2;
            target = requestedTarget;
            progressPosition = transform.position;
            startedAt = lastProgressAt = Time.time;
            allowedTravelSeconds = 30;
            Result = MovementResult.Moving;
            Message = "Jazda do celu...";
            return true;
        }

        public bool MoveTo(Vector3 point)
        {
            if (manualTarget == null)
                manualTarget = new GameObject(name + " - movement target").transform;
            manualTarget.position = point;
            return MoveTo(manualTarget);
        }

        private void Update()
        {
            if (PauseMenu.IsOpen || GameSaveService.IsRestoring)
                return;
            if (!IsMoving)
                return;
            if (energy != null && energy.IsDepleted)
            {
                OnEnergyDepleted();
                return;
            }
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                Fail("Cel został usunięty lub wyłączony.");
                return;
            }

            if (preparingPath)
            {
                if (Time.frameCount < activateAfterFrame)
                    return;
                preparingPath = false;
                if (!StartPath())
                    return;
            }

            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                Fail("Robot utracił NavMesh.");
                return;
            }

            if (Time.time - startedAt > allowedTravelSeconds)
            {
                Fail("Przekroczono czas przejazdu.");
                return;
            }

            if (agent.pathPending)
                return;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Fail("Trasa jest niedostępna.");
                return;
            }

            Vector3 offset = destination - transform.position;
            offset.y = 0;
            if (offset.magnitude <= agent.stoppingDistance + 0.12f && agent.remainingDistance <= agent.stoppingDistance + 0.12f)
            {
                Finish(MovementResult.Arrived, "Dotarto do celu.");
                return;
            }

            if ((transform.position - progressPosition).sqrMagnitude > 0.04f)
            {
                progressPosition = transform.position;
                lastProgressAt = Time.time;
            }

            if (Time.time - lastProgressAt > 6)
            {
                Fail("Robot utknął. Spróbuj ponownie.");
                return;
            }

            Vector3 velocity = agent.velocity;
            velocity.y = 0;
            if (velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-velocity), 250 * Time.deltaTime);
        }

        private void LateUpdate()
        {
            Vector3 displacement = transform.position - lastEnergyPosition;
            lastEnergyPosition = transform.position;
            float elapsed = Time.deltaTime;
            if (elapsed <= 0 || Time.timeScale <= 0 || PauseMenu.IsOpen || energy == null || energy.IsDepleted || !IsMoving ||
                preparingPath || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh || agent.pathPending)
                return;
            displacement.y = 0;
            Vector3 velocity = agent.velocity;
            velocity.y = 0;
            const float minimumSpeed = .01f;
            if (displacement.sqrMagnitude > minimumSpeed * minimumSpeed * elapsed * elapsed &&
                velocity.sqrMagnitude > minimumSpeed * minimumSpeed)
                {
                energy.ConsumeMovement(elapsed);
                var towing = GetComponent<HaulerTowing>();
                if (towing != null && towing.IsTransporting) energy.ConsumePower(towing.TowingKW, elapsed);
            }
        }

        private void OnEnergyDepleted()
        {
            if (IsMoving) Fail("Brak energii. Robot nie może jechać.");
        }

        public void Cancel()
        {
            if (IsMoving)
                Finish(MovementResult.Cancelled, "Ruch zatrzymany.");
        }

        private bool StartPath()
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, 0.75f, agent.areaMask))
                return Fail("Robot jest poza NavMesh.");
            transform.position = start.position;
            lastEnergyPosition = transform.position;
            agent.enabled = true;
            if (!agent.isOnNavMesh)
                return Fail("Robot nie może rozpocząć ruchu na NavMesh.");
            var robot = GetComponent<RobotController>();
            if (robot != null && target == robot.BaseDestination)
            {
                var home = target.GetComponentInParent<BaseBuilding>();
                if (home == null || !home.TryResolveReturnTarget(robot, agent, out _))
                    return Fail("Brak dostępnego miejsca rozładunku przy bazie.");
            }
            if (!NavMesh.SamplePosition(target.position, out NavMeshHit hit, 0.75f, agent.areaMask))
                return Fail("Cel jest poza NavMesh.");
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return Fail("Brak pełnej trasy do celu.");
            float pathLength = 0;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
                pathLength += Vector3.Distance(corners[i - 1], corners[i]);
            allowedTravelSeconds = Mathf.Max(30, pathLength / Mathf.Max(.1f, agent.speed) * 2 + 10);
            agent.isStopped = false;
            if (!agent.SetPath(path))
                return Fail("Nie udało się rozpocząć ruchu.");
            destination = hit.position;
            progressPosition = transform.position;
            lastProgressAt = Time.time;
            return true;
        }

        private void Park()
        {
            if (agent == null)
                return;
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            agent.enabled = false;
            if (parkedObstacle != null)
                parkedObstacle.enabled = !IsBeingTowed;
        }

        private bool Fail(string message)
        {
            Finish(MovementResult.Failed, message);
            return false;
        }

        private void Finish(MovementResult result, string message)
        {
            preparingPath = false;
            Park();
            target = null;
            Result = result;
            Message = message;
            Completed?.Invoke(result);
        }

        internal Vector3 SavedTarget => target != null ? target.position : destination;

        internal void RestoreGameState(MovementResult result, string message, Vector3 point)
        {
            Park();
            lastEnergyPosition = transform.position;
            if (result == MovementResult.Moving)
            {
                if (!MoveTo(point)) throw new InvalidOperationException("Cannot restore movement: " + Message);
            }
            else
            {
                Result = result;
                Message = message;
            }
        }

        private void OnDisable()
        {
            if (transportOwner != null) transportOwner.Cancel();
            Cancel();
        }

        private void OnDestroy()
        {
            if (energy != null) energy.Depleted -= OnEnergyDepleted;
            if (manualTarget != null)
                Destroy(manualTarget.gameObject);
        }
    }
}
