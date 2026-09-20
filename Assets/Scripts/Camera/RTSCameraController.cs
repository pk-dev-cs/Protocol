using UnityEngine;

namespace Protocol
{
    [RequireComponent(typeof(Camera))]
    public sealed class RTSCameraController : MonoBehaviour
    {
        [SerializeField]
        private float movementSpeed = 16f;
        [SerializeField]
        private float zoomSpeed = 3f;
        [SerializeField]
        private float minDistance = 12f;
        [SerializeField]
        private float maxDistance = 48f;
        private Vector3 focus = Vector3.zero;
        private float distance = 50f;
        private Vector2 lastMouse;
        private bool dragging;
        private readonly Quaternion viewRotation = Quaternion.Euler(58, 0, 0);

        public void ResetView()
        {
            focus = Vector3.zero;
            distance = 50f;
            ApplyView();
        }

        public void ConfigureMap()
        {
            maxDistance = 180;
            movementSpeed = 24;
            GetComponent<Camera>().farClipPlane = 600;
            ApplyView();
        }

        public void FocusOn(Vector3 position)
        {
            if (PauseMenu.IsOpen || RobotProgrammingPanel.IsOpen)
                return;
            focus = position;
            ClearInput();
            ApplyView();
        }

        // IMGUI keyboard events keep the prototype independent of Input System packages
        // and of the project's Active Input Handling setting.
        private bool up, down, left, right, fast;

        private void OnGUI()
        {
            Event e = Event.current;
            if (RobotProgrammingPanel.IsOpen || PauseMenu.IsOpen)
            {
                ClearInput();
                return;
            }

            if (e.rawType == EventType.MouseUp && e.button == 2)
                dragging = false;
            if (UnitPanel.ContainsPointer(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel))
            {
                dragging = false;
                return;
            }

            if (e.type == EventType.KeyDown || e.type == EventType.KeyUp)
            {
                bool pressed = e.type == EventType.KeyDown;
                switch (e.keyCode)
                {
                    case KeyCode.W:
                    case KeyCode.UpArrow:
                        up = pressed;
                        break;
                    case KeyCode.S:
                    case KeyCode.DownArrow:
                        down = pressed;
                        break;
                    case KeyCode.A:
                    case KeyCode.LeftArrow:
                        left = pressed;
                        break;
                    case KeyCode.D:
                    case KeyCode.RightArrow:
                        right = pressed;
                        break;
                    case KeyCode.LeftShift:
                    case KeyCode.RightShift:
                        fast = pressed;
                        break;
                    case KeyCode.Home:
                        if (pressed)
                            ResetView();
                        break;
                }
            }

            if (e.type == EventType.ScrollWheel)
            {
                distance = Mathf.Clamp(distance + e.delta.y * zoomSpeed * GameSettings.CameraSensitivity, minDistance, maxDistance);
                ApplyView();
                e.Use();
            }

            if (e.type == EventType.MouseDown && e.button == 2)
            {
                dragging = true;
                lastMouse = e.mousePosition;
            }

            if (e.type == EventType.MouseUp && e.button == 2)
                dragging = false;
            if (e.type == EventType.MouseDrag && e.button == 2 && dragging)
            {
                Vector2 delta = e.mousePosition - lastMouse;
                lastMouse = e.mousePosition;
                float unitsPerPixel = 2f * distance * Mathf.Tan(GetComponent<Camera>().fieldOfView * Mathf.Deg2Rad / 2) / Mathf.Max(
                    1,
                    Screen.height);
                focus += new Vector3(-delta.x, 0, delta.y / Mathf.Sin(58 * Mathf.Deg2Rad)) * unitsPerPixel * GameSettings.CameraSensitivity;
                ApplyView();
                e.Use();
            }
        }

        private void Update()
        {
            if (RobotProgrammingPanel.IsOpen || PauseMenu.IsOpen)
            {
                ClearInput();
                return;
            }

            var movement = new Vector3((right ? 1 : 0) - (left ? 1 : 0), 0, (up ? 1 : 0) - (down ? 1 : 0));
            focus += Vector3.ClampMagnitude(
                movement,
                1) * movementSpeed * GameSettings.CameraSensitivity * (fast ? 2 : 1) * Time.unscaledDeltaTime;
            ApplyView();
        }

        private void ApplyView()
        {
            focus.x = Mathf.Clamp(focus.x, -ScenarioLandscape.HalfSize + 8, ScenarioLandscape.HalfSize - 8);
            focus.z = Mathf.Clamp(focus.z, -ScenarioLandscape.HalfSize + 8, ScenarioLandscape.HalfSize - 8);
            focus.y = ScenarioLandscape.HeightAt(focus.x, focus.z);
            Vector3 position = focus - viewRotation * Vector3.forward * distance;
            position.y = Mathf.Max(position.y, ScenarioLandscape.HeightAt(position.x, position.z) + 5);
            transform.SetPositionAndRotation(position, viewRotation);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                ClearInput();
        }

        private void OnDisable() => ClearInput();

        private void ClearInput()
        {
            up = down = left = right = fast = dragging = false;
        }
    }
}
