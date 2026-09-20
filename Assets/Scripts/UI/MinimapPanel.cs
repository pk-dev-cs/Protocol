using UnityEngine;

namespace Protocol
{
    public sealed class MinimapPanel : MonoBehaviour
    {
        private FogOfWar fog;
        private SelectionManager selection;
        private Camera worldCamera;
        private RTSCameraController cameraControl;
        private RobotController[] robots;
        private SelectableStructure[] structures;

        public void Initialize()
        {
            fog = GetComponent<FogOfWar>();
            selection = GetComponent<SelectionManager>();
            worldCamera = transform.Find("World/Main Camera").GetComponent<Camera>();
            cameraControl = worldCamera.GetComponent<RTSCameraController>();
            robots = GetComponentsInChildren<RobotController>();
            structures = GetComponentsInChildren<SelectableStructure>();
        }

        public static Vector2 ToMap(
            Rect rect,
            Vector3 point) => new Vector2(
            rect.x + (point.x / ScenarioLandscape.Size + .5f) * rect.width,
            rect.y + (.5f - point.z / ScenarioLandscape.Size) * rect.height);

        public static Vector3 ToWorld(Rect rect, Vector2 point)
        {
            float x = (Mathf.Clamp01((point.x - rect.x) / rect.width) - .5f) * ScenarioLandscape.Size;
            float z = (.5f - Mathf.Clamp01((point.y - rect.y) / rect.height)) * ScenarioLandscape.Size;
            return new Vector3(x, ScenarioLandscape.HeightAt(x, z), z);
        }

        public void Draw(Rect rect)
        {
            if (fog == null || fog.MinimapTexture == null)
                return;
            GUI.DrawTexture(rect, fog.MinimapTexture);
            foreach (var building in structures)
                if (building != null && building.isActiveAndEnabled && fog.IsExplored(building.transform.position))
                    Mark(rect, building.transform.position, building.IsMine ? new Color(1, .7f, .2f) : Color.cyan, 6);
            foreach (var robot in robots)
                if (robot != null && robot.isActiveAndEnabled)
                    Mark(rect, robot.transform.position, robot.IsSelected ? Color.white : new Color(.3f, 1, .5f), 4);
            // Approximate ground footprint of the camera; clipped to minimap bounds.
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var uv in new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            }

            )
            {
                var ray = worldCamera.ViewportPointToRay(uv);
                var plane = new Plane(Vector3.up, Vector3.zero);
                if (!plane.Raycast(ray, out float distance))
                    continue;
                var p = ToMap(rect, ray.GetPoint(distance));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Rect view = Rect.MinMaxRect(
                Mathf.Clamp(min.x, rect.x, rect.xMax),
                Mathf.Clamp(min.y, rect.y, rect.yMax),
                Mathf.Clamp(max.x, rect.x, rect.xMax),
                Mathf.Clamp(max.y, rect.y, rect.yMax));
            Color old = GUI.color;
            GUI.color = new Color(1, 1, 1, .65f);
            GUI.DrawTexture(new Rect(view.x, view.y, view.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(view.x, view.yMax - 1, view.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(view.x, view.y, 1, view.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(view.xMax - 1, view.y, 1, view.height), Texture2D.whiteTexture);
            GUI.color = old;
            var e = Event.current;
            if (!PauseMenu.IsOpen && !RobotProgrammingPanel.IsOpen && rect.Contains(e.mousePosition))
            {
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    cameraControl.FocusOn(ToWorld(rect, e.mousePosition));
                    e.Use();
                }

                if (e.type == EventType.MouseDown && e.button == 1)
                {
                    selection.MoveSelectedTo(ToWorld(rect, e.mousePosition));
                    e.Use();
                }
            }
        }

        private static void Mark(Rect rect, Vector3 point, Color color, float size)
        {
            var p = ToMap(rect, point);
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(
                    Mathf.Clamp(p.x - size / 2, rect.x, rect.xMax - size),
                    Mathf.Clamp(p.y - size / 2, rect.y, rect.yMax - size),
                    size,
                    size),
                Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
