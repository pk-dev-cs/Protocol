using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    public sealed class SelectionManager : MonoBehaviour
    {
        private Camera worldCamera;
        private readonly List<RobotController> selected = new List<RobotController>();

        public IReadOnlyList<RobotController> SelectedRobots => selected;

        public RobotController SelectedRobot => selected.Count == 1 ? selected[0] : null;

        public SelectableStructure SelectedStructure { get; private set; }

        private bool dragging;
        private Vector2 dragStart, dragEnd;

        public bool IsDragging => dragging;

        public string CommandMessage { get; private set; } = "";

        public void Initialize(Camera camera) => worldCamera = camera;

        private bool Blocked => RobotProgrammingPanel.IsOpen || PauseMenu.IsOpen;

        public void SelectAtPointer(Vector2 guiPosition)
        {
            if (Blocked || UnitPanel.ContainsPointer(guiPosition) || worldCamera == null)
                return;
            var ray = worldCamera.ScreenPointToRay(new Vector3(guiPosition.x, Screen.height - guiPosition.y, 0));
            RobotController target = null;
            if (Physics.Raycast(ray, out RaycastHit hit, worldCamera.farClipPlane, ~0, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<RobotController>();
                if (target == null)
                {
                    var structure = hit.collider.GetComponentInParent<SelectableStructure>();
                    if (structure != null)
                    {
                        SelectStructure(structure);
                        return;
                    }
                }
            }

            Select(target);
        }

        public void SelectStructure(SelectableStructure structure)
        {
            if (Blocked)
                return;
            ClearSelection();
            if (structure == null || !structure.isActiveAndEnabled)
                return;
            var fog = GetComponent<FogOfWar>();
            if (fog != null && !fog.IsExplored(structure.transform.position))
                return;
            SelectedStructure = structure;
            structure.SetSelected(true);
        }

        public void Select(RobotController robot)
        {
            ClearSelection();
            Add(robot);
        }

        private void Add(RobotController robot)
        {
            if (robot == null || !robot.isActiveAndEnabled || selected.Contains(robot))
                return;
            selected.Add(robot);
            robot.SetSelected(true);
        }

        private void ClearSelection()
        {
            if (SelectedStructure != null)
                SelectedStructure.SetSelected(false);
            SelectedStructure = null;
            foreach (var robot in selected)
                if (robot != null)
                    robot.SetSelected(false);
            selected.Clear();
            CommandMessage = "";
        }

        public void SelectInRectangle(Rect rectangle)
        {
            if (Blocked || worldCamera == null)
                return;
            ClearSelection();
            foreach (var robot in transform.root.GetComponentsInChildren<RobotController>())
            {
                var p = worldCamera.WorldToScreenPoint(robot.transform.position + Vector3.up * 0.65f);
                var screen = new Vector2(p.x, Screen.height - p.y);
                if (p.z > 0 && !UnitPanel.ContainsPointer(screen) && rectangle.Contains(screen))
                    Add(robot);
            }
        }

        public void MoveSelectedTo(Vector3 point)
        {
            if (Blocked || selected.Count == 0)
                return;
            int accepted = 0;
            for (int i = 0; i < selected.Count; i++)
            {
                var robot = selected[i];
                if (robot == null || !robot.isActiveAndEnabled || robot.Movement == null || !robot.Movement.isActiveAndEnabled)
                    continue;
                var destination = point + Vector3.right * ((i - (selected.Count - 1) / 2f) * 2.8f);
                if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 2, NavMesh.AllAreas))
                    continue;
                robot.LuaRuntime?.StopProgram();
                robot.HarvestCycle?.CancelCycle();
                robot.HarvestCycle?.ClearFeedback();
                if (robot.Movement.MoveTo(hit.position))
                    accepted++;
            }

            CommandMessage = accepted > 0 ? "Rozkaz ruchu: " + accepted + ". Programy tych robotów zatrzymano." : "Brak dostępnego celu ruchu.";
        }

        public void CancelDrag() => dragging = false;

        private void OnGUI()
        {
            GUI.depth = -5;
            if (Blocked)
            {
                CancelDrag();
                return;
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && !UnitPanel.ContainsPointer(e.mousePosition))
            {
                dragging = true;
                dragStart = dragEnd = e.mousePosition;
                e.Use();
            }

            if (dragging && e.type == EventType.MouseDrag && e.button == 0)
            {
                dragEnd = e.mousePosition;
                e.Use();
            }

            if (dragging && e.rawType == EventType.MouseUp && e.button == 0)
            {
                dragEnd = e.mousePosition;
                dragging = false;
                if ((dragEnd - dragStart).sqrMagnitude < 36)
                    SelectAtPointer(dragEnd);
                else
                    SelectInRectangle(DragRect());
                if (e.type != EventType.Used)
                    e.Use();
            }

            if (e.type == EventType.MouseDown && e.button == 1 && !UnitPanel.ContainsPointer(e.mousePosition) && worldCamera != null)
            {
                var ray = worldCamera.ScreenPointToRay(new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y, 0));
                if (Physics.Raycast(ray, out RaycastHit hit, worldCamera.farClipPlane, ~0, QueryTriggerInteraction.Ignore))
                    MoveSelectedTo(hit.point);
                e.Use();
            }

            if (dragging && (dragEnd - dragStart).sqrMagnitude >= 36)
            {
                Rect r = DragRect();
                Color old = GUI.color;
                GUI.color = new Color(0.2f, 1, 0.65f, 0.16f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = new Color(0.2f, 1, 0.65f, 1);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x, r.y, 2, r.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.xMax - 2, r.y, 2, r.height), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }

        private Rect DragRect() => Rect.MinMaxRect(
            Mathf.Min(dragStart.x, dragEnd.x),
            Mathf.Min(dragStart.y, dragEnd.y),
            Mathf.Max(dragStart.x, dragEnd.x),
            Mathf.Max(dragStart.y, dragEnd.y));

        private void Update()
        {
            if (SelectedStructure != null && !SelectedStructure.isActiveAndEnabled)
            {
                SelectedStructure.SetSelected(false);
                SelectedStructure = null;
            }

            if (Blocked)
                CancelDrag();
            for (int i = selected.Count - 1; i >= 0; i--)
                if (selected[i] == null || !selected[i].isActiveAndEnabled)
                {
                    if (selected[i] != null)
                        selected[i].SetSelected(false);
                    selected.RemoveAt(i);
                }
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
                CancelDrag();
        }

        private void OnDisable()
        {
            CancelDrag();
            ClearSelection();
        }
    }
}
