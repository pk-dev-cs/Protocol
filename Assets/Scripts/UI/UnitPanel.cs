using UnityEngine;

namespace Protocol
{
    public sealed class UnitPanel : MonoBehaviour
    {
        private SelectionManager selection;
        private ResourceManager resources;
        private GUIStyle titleStyle, textStyle, buttonStyle, panelStyle;
        private static UnitPanel activePanel;

        private static float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 640f), 0.25f, 1.5f);

        private static Rect PanelRect => new Rect(0, Screen.height - 200f * Scale, Screen.width, 200f * Scale);

        private static Rect ResourceRect => new Rect(12 * Scale, 12 * Scale, 400 * Scale, 44 * Scale);

        public static bool ContainsPointer(Vector2 position) => PauseMenu.IsOpen || RobotProgrammingPanel.IsOpen || (activePanel != null && activePanel.isActiveAndEnabled && (PanelRect.Contains(position) || (activePanel.resources != null && ResourceRect.Contains(position))));

        public void Initialize(SelectionManager manager) => selection = manager;

        public void InitializeResources(ResourceManager manager) => resources = manager;

        private void OnEnable() => activePanel = this;

        private void OnDisable()
        {
            if (activePanel == this)
                activePanel = null;
        }

        private void OnGUI()
        {
            GUI.depth = 0;
            if (PauseMenu.IsOpen)
                return;
            if (selection == null)
                return;
            EnsureStyles();
            Event e = Event.current;
            RobotController robot = selection.SelectedRobot;
            Rect panel = PanelRect;
            float scale = Scale;
            titleStyle.fontSize = Mathf.RoundToInt(22 * scale);
            textStyle.fontSize = buttonStyle.fontSize = Mathf.RoundToInt(16 * scale);
            Color previousColor = GUI.color;
            GUI.color = new Color(0.055f, 0.08f, 0.11f, 1f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            if (resources != null)
                GUI.DrawTexture(ResourceRect, Texture2D.whiteTexture);
            GUI.color = previousColor;
            if (resources != null)
                GUI.Label(
                    new Rect(ResourceRect.x + 10 * scale, ResourceRect.y + 6 * scale, ResourceRect.width - 20 * scale, 32 * scale),
                    $"Iron: {resources.Iron}    Wood: {resources.Wood}",
                    titleStyle);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GetComponent<MinimapPanel>()?.Draw(new Rect(12 * scale, panel.y + 12 * scale, 176 * scale, 176 * scale));
            float x = 212 * scale;
            float infoX = x + 112 * scale;
            float actionWidth = 240 * scale;
            float actionX = panel.width - actionWidth - 16 * scale;
            float infoWidth = Mathf.Max(40, actionX - infoX - 12 * scale);
            var portraits = GetComponent<SelectionPortraits>();
            float y = panel.y + 16 * scale;
            if (selection.SelectedStructure != null)
            {
                var structure = selection.SelectedStructure;
                DrawPortrait(new Rect(x, y + 40 * scale, 100 * scale, 100 * scale), portraits, structure.transform);
                GUI.Label(new Rect(x, y, panel.width - x * 2, 32 * scale), structure.DisplayName, titleStyle);
                GUI.Label(new Rect(infoX, y + 42 * scale, infoWidth, 76 * scale), structure.Description, textStyle);
                if (!structure.IsMine)
                {
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && !RobotProgrammingPanel.IsOpen;
                    if (GUI.Button(new Rect(actionX, y, actionWidth, 40 * scale), "Oprogramowanie", buttonStyle))
                        GetComponent<RobotProgrammingPanel>().Open(structure.GetComponent<RobotLuaRuntime>());
                    GUI.enabled = enabled;
                    GUI.Label(
                        new Rect(actionX, y + 48 * scale, actionWidth, 110 * scale),
                        "Harvester: 100 wood • 8 s\n" + structure.GetComponent<RobotLuaRuntime>()?.ActionStatus,
                        textStyle);
                }

                if (!structure.IsMine && resources != null)
                    GUI.Label(
                        new Rect(infoX, y + 122 * scale, infoWidth, 36 * scale),
                        "Iron: " + resources.Iron + " • Wood: " + resources.Wood,
                        textStyle);
            }
            else if (selection.SelectedRobots.Count > 1)
            {
                int portraitIndex = 0;
                int maxPortraits = Mathf.Max(1, Mathf.FloorToInt((actionX - x) / (102 * scale)));
                foreach (var unit in selection.SelectedRobots)
                {
                    if (unit == null)
                        continue;
                    if (portraitIndex >= maxPortraits)
                        break;
                    var rect = new Rect(x + portraitIndex++ * 102 * scale, y + 40 * scale, 92 * scale, 92 * scale);
                    DrawPortrait(rect, portraits, unit.transform);
                }

                int cargo = 0, capacity = 0;
                foreach (var unit in selection.SelectedRobots)
                    if (unit != null)
                    {
                        cargo += unit.CurrentCargo;
                        capacity += unit.CargoCapacity;
                    }

                GUI.Label(
                    new Rect(x, y, panel.width - x * 2, 32 * scale),
                    "Zaznaczone jednostki: " + selection.SelectedRobots.Count,
                    titleStyle);
                GUI.Label(new Rect(x, y + 140 * scale, actionX - x, 32 * scale), $"Łączne cargo: {cargo} / {capacity}", textStyle);
                bool sameType = true;
                string kind = selection.SelectedRobots[0].UnitType;
                foreach (var unit in selection.SelectedRobots)
                    if (unit == null || unit.UnitType != kind)
                        sameType = false;
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && sameType && !RobotProgrammingPanel.IsOpen;
                if (GUI.Button(new Rect(actionX, y, actionWidth, 40 * scale), "Oprogramowanie", buttonStyle))
                    GetComponent<RobotProgrammingPanel>().OpenGroup(selection.SelectedRobots);
                GUI.enabled = enabled;
                GUI.Label(
                    new Rect(actionX, y + 48 * scale, actionWidth, 120 * scale),
                    sameType ? "Wspólny kod dla zaznaczonych harvesterów.\n" + selection.CommandMessage : "Programowanie grupy wymaga jednostek tego samego typu.",
                    textStyle);
            }
            else if (robot == null)
            {
                GUI.Label(new Rect(x, y, panel.width - x * 2, 32 * scale), "Brak wybranej jednostki", titleStyle);
                GUI.Label(
                    new Rect(x, y + 44 * scale, panel.width - x * 2, 50 * scale),
                    "LPM — zaznacz robota, bazę lub kopalnię. Ramka — grupa robotów. PPM — ruch. Escape — pauza.",
                    textStyle);
            }
            else
            {
                DrawPortrait(new Rect(x, y + 40 * scale, 100 * scale, 100 * scale), portraits, robot.transform);
                GUI.Label(new Rect(x, y, actionX - x, 32 * scale), robot.DisplayName, titleStyle);
                GUI.Label(new Rect(infoX, y + 38 * scale, infoWidth, 27 * scale), "Status: " + robot.Status, textStyle);
                GUI.Label(
                    new Rect(infoX, y + 68 * scale, infoWidth, 27 * scale),
                    $"Cargo: {robot.CurrentCargo} / {robot.CargoCapacity} {robot.Inventory.Kind.ToString().ToLowerInvariant()}",
                    textStyle);
                bool panelEnabled = GUI.enabled;
                GUI.enabled = panelEnabled && !RobotProgrammingPanel.IsOpen;
                if (GUI.Button(new Rect(actionX, y, actionWidth, 40 * scale), "Oprogramowanie", buttonStyle))
                    GetComponent<RobotProgrammingPanel>().Open(robot.LuaRuntime);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && robot.Movement != null && !robot.Movement.IsMoving && (robot.LuaRuntime == null || robot.LuaRuntime.State != LuaProgramState.Running) && (robot.HarvestCycle == null || !robot.HarvestCycle.IsRunning);
                if (GUI.Button(new Rect(actionX, y + 48 * scale, actionWidth, 40 * scale), "Jedź do kopalni (test)", buttonStyle))
                {
                    if (robot.HarvestCycle != null)
                        robot.HarvestCycle.ClearFeedback();
                    robot.Movement.MoveTo(robot.MineDestination);
                }

                GUI.enabled = GUI.enabled && robot.HarvestCycle != null;
                if (GUI.Button(new Rect(actionX, y + 96 * scale, actionWidth, 40 * scale), "Zbierz i dostarcz (test)", buttonStyle))
                {
                    robot.HarvestCycle.StartCycle();
                }

                GUI.enabled = wasEnabled;
                if (robot.Movement != null)
                    GUI.Label(
                        new Rect(infoX, y + 103 * scale, infoWidth, 65 * scale),
                        robot.HarvestCycle != null && robot.HarvestCycle.Message.Length > 0 ? robot.HarvestCycle.Message : robot.Movement.Message,
                        textStyle);
                if (robot.LuaRuntime != null)
                    GUI.Label(new Rect(actionX, y + 140 * scale, actionWidth, 42 * scale), "Lua: " + robot.LuaRuntime.State, textStyle);
                GUI.enabled = panelEnabled;
            }

            // SelectionManager owns world mouse input; this panel consumes only UI input.
            if (RobotProgrammingPanel.IsOpen)
                return; // The modal owns its mouse events.
            if (ContainsPointer(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel))
                e.Use();
        }

        private static void DrawPortrait(Rect rect, SelectionPortraits portraits, Transform source)
        {
            GUI.Box(rect, GUIContent.none);
            var texture = portraits != null ? portraits.Get(source) : null;
            if (texture != null)
                GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), texture, ScaleMode.ScaleToFit);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0.4f, 1f, 0.75f);
            textStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true
            };
            textStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };
            panelStyle = new GUIStyle(GUI.skin.box);
        }
    }
}
