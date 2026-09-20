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
            if (IsMoving)
                Cancel();
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
            if (PauseMenu.IsOpen)
                return;
            if (!IsMoving)
                return;
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
            agent.enabled = true;
            if (!agent.isOnNavMesh)
                return Fail("Robot nie może rozpocząć ruchu na NavMesh.");
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
                parkedObstacle.enabled = true;
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

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            if (manualTarget != null)
                Destroy(manualTarget.gameObject);
        }
    }
}
