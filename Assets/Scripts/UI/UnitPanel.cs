using UnityEngine;

namespace Protocol
{
    public sealed class UnitPanel : MonoBehaviour
    {
        private SelectionManager selection;
        private ResourceManager resources;
        private RobotEnergy displayedEnergy;
        private int displayedEnergyPercent = -1;
        private string energyPercentLabel;
        private GUIStyle titleStyle, textStyle, buttonStyle, panelStyle, energyStyle;
        private static UnitPanel activePanel;

        private static float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 640f), 0.25f, 1.5f);

        private static Rect PanelRect => new Rect(0, Screen.height - 200f * Scale, Screen.width, 200f * Scale);

        private static Rect ResourceRect => new Rect(12 * Scale, 12 * Scale, 400 * Scale, 44 * Scale);

        public static bool ContainsPointer(Vector2 position) => PlanetEnvironmentPanel.ContainsPointer(position) || RobotProgrammingPanel.ContainsPointer(position)
#if UNITY_EDITOR
            || PowerDebugPanel.ContainsPointer(position)
#endif
            || PauseMenu.IsOpen || (activePanel != null && activePanel.isActiveAndEnabled && (PanelRect.Contains(position) || (activePanel.resources != null && ResourceRect.Contains(position))));

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
            if (ScenarioLoadingScreen.IsVisible) return;

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
            float hudWidth = RightHudPanels.Progress(RightHudPanel.Software) > 0
                ? Mathf.Clamp(RobotProgrammingPanel.PanelRect.x, 0, Screen.width)
                : Screen.width;
            GUI.BeginGroup(new Rect(0, 0, hudWidth, Screen.height));
            titleStyle.fontSize = Mathf.RoundToInt(22 * scale);
            textStyle.fontSize = buttonStyle.fontSize = Mathf.RoundToInt(16 * scale);
            energyStyle.fontSize = Mathf.RoundToInt(14 * scale);
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
            float y = panel.y + 8 * scale;
            if (selection.SelectedStructure != null)
            {
                var structure = selection.SelectedStructure;
                DrawPortrait(new Rect(x, y + 40 * scale, 100 * scale, 100 * scale), portraits, structure.transform);
                GUI.Label(new Rect(x, y, panel.width - x * 2, 32 * scale), structure.DisplayName, titleStyle);
                var grid = structure.GetComponent<ColonyPowerGrid>();
                var solar = structure.GetComponent<SolarPanel>();
                var site = structure.GetComponent<ConstructionSite>();
                var station = structure.GetComponent<ChargingStationStructure>();
                if (grid != null) DrawBasePower(new Rect(infoX, y + 38 * scale, infoWidth, 140 * scale), grid, scale);
                else if (site != null)
                    GUI.Label(new Rect(infoX, y + 42 * scale, infoWidth, 100 * scale), $"Budowa    {site.Progress:P0}", textStyle);
                else if (station != null)
                    GUI.Label(new Rect(infoX, y + 42 * scale, infoWidth, 100 * scale),
                        $"Idle            {station.IdleConsumptionKW:0.##} kW\nCharging    {station.CurrentChargingKW:0.##} kW\nConsumption   {station.TotalConsumptionKW:0.##} kW", textStyle);
                else if (solar != null)
                    GUI.Label(new Rect(infoX, y + 42 * scale, infoWidth, 100 * scale),
                        $"Generation    {solar.CurrentGenerationKW:0.##} kW\nMaximum       {solar.MaximumGenerationKW:0.##} kW", textStyle);
                else GUI.Label(new Rect(infoX, y + 42 * scale, infoWidth, 76 * scale), structure.Description, textStyle);
                if (structure.GetComponent<BaseBuilding>() != null)
                {
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && structure.GetComponent<RobotLuaRuntime>() != null;
                    DrawSoftwareButton(actionX, y, scale);
                    GUI.enabled = enabled;
                    GUI.Label(
                        new Rect(actionX, y + 48 * scale, actionWidth, 110 * scale),
                        "Harvester / Assembler\n" + structure.GetComponent<RobotLuaRuntime>()?.ActionStatus,
                        textStyle);
                }

                if (structure.GetComponent<BaseBuilding>() != null && resources != null && grid == null)
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
                    var rect = new Rect(x + portraitIndex++ * 102 * scale, y + 10 * scale, 92 * scale, 92 * scale);
                    DrawPortrait(rect, portraits, unit.transform);
                }

                int cargo = 0, capacity = 0;
                foreach (var unit in selection.SelectedRobots)
                    if (unit != null)
                    {
                        cargo += unit.CurrentCargo;
                        capacity += unit.CargoCapacity;
                    }

                GUI.Label(new Rect(x, y + 140 * scale, actionX - x, 32 * scale), $"Łączne cargo: {cargo} / {capacity}", textStyle);
                bool sameType = true;
                string kind = selection.SelectedRobots[0].UnitType;
                foreach (var unit in selection.SelectedRobots)
                    if (unit == null || unit.UnitType != kind)
                        sameType = false;
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && sameType;
                DrawSoftwareButton(actionX, y, scale);
                GUI.enabled = enabled;
                GUI.Label(
                    new Rect(actionX, y + 48 * scale, actionWidth, 120 * scale),
                    sameType ? "Wspólny kod: " + kind + "\n" + selection.CommandMessage : "Programowanie grupy wymaga jednostek tego samego typu.",
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
                DrawEnergy(new Rect(infoX, y + 101 * scale, 100 * scale, 20 * scale), robot.Energy, scale);
                if (robot.Energy != null)
                    GUI.Label(new Rect(infoX + 112 * scale, y + 100 * scale, infoWidth - 112 * scale, 24 * scale),
                        $"Stored  {robot.Energy.StoredEnergyKWh:0.##} / {robot.Energy.CapacityKWh:0.##} kWh", textStyle);
                bool panelEnabled = GUI.enabled;
                GUI.enabled = panelEnabled && robot.LuaRuntime != null;
                DrawSoftwareButton(actionX, y, scale);
                bool wasEnabled = GUI.enabled;
                var construction = robot.GetComponent<AssemblerConstruction>();
                if (construction != null)
                {
                    GUI.enabled = panelEnabled;
                    DrawConstruction(construction, actionX, y, actionWidth, scale);
                }
                else
                {
                    var charging = robot.GetComponent<RobotCharging>();
                    bool chargeEnabled = wasEnabled && charging != null &&
                        (charging.Station != null || robot.Energy.Percentage < 100);
                    GUI.enabled = chargeEnabled;
                    if (GUI.Button(new Rect(actionX, y + 48 * scale, actionWidth * .49f, 40 * scale),
                        charging != null && charging.Station != null ? "Odłącz" : "Ładuj", buttonStyle))
                    {
                        if (charging.Station != null) charging.Cancel();
                        else charging.TryBeginNearest();
                    }
                    GUI.enabled = wasEnabled && robot.UnitType == "Harvester" && robot.Energy != null && !robot.Energy.IsDepleted && robot.Movement != null && !robot.Movement.IsMoving && (robot.LuaRuntime == null || robot.LuaRuntime.State != LuaProgramState.Running) && (robot.HarvestCycle == null || !robot.HarvestCycle.IsRunning);
                    if (GUI.Button(new Rect(actionX + actionWidth * .51f, y + 48 * scale, actionWidth * .49f, 40 * scale), "Kopalnia", buttonStyle))
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
                }

                GUI.enabled = wasEnabled;
                var activeCharging = robot.GetComponent<RobotCharging>();
                if (activeCharging != null && activeCharging.IsCharging)
                {
                    GUI.Label(new Rect(infoX, y + 126 * scale, infoWidth, 24 * scale),
                        $"Consumption  0 kW    Charging  {activeCharging.ChargingKW:0.##} kW", textStyle);
                    GUI.Label(new Rect(infoX, y + 150 * scale, infoWidth, 24 * scale),
                        $"Runtime  ∞    Full In  {ColonyPowerGrid.FormatDuration(activeCharging.FullInHours)}", textStyle);
                }
                else if (robot.Energy != null)
                {
                    GUI.Label(new Rect(infoX, y + 126 * scale, infoWidth, 24 * scale),
                        $"Consumption  {robot.Energy.CurrentConsumptionKW:0.##} kW", textStyle);
                    GUI.Label(new Rect(infoX, y + 150 * scale, infoWidth, 24 * scale),
                        $"Runtime  {ColonyPowerGrid.FormatDuration(robot.Energy.RuntimeHours)}", textStyle);
                }
                string feedback = construction != null && construction.Status.Length > 0
                    ? construction.Status
                    : activeCharging != null && activeCharging.Message.Length > 0
                        ? activeCharging.Message
                        : robot.HarvestCycle != null && robot.HarvestCycle.Message.Length > 0
                            ? robot.HarvestCycle.Message
                            : robot.Movement.Message;
                string actionStatus = robot.LuaRuntime != null ? "Lua: " + robot.LuaRuntime.State : string.Empty;
                if (!string.IsNullOrEmpty(feedback))
                    actionStatus = string.IsNullOrEmpty(actionStatus) ? feedback : actionStatus + "\n" + feedback;
                GUI.Label(new Rect(actionX, y + 138 * scale, actionWidth, 54 * scale), actionStatus, textStyle);
                GUI.enabled = panelEnabled;
            }

            GUI.EndGroup();

            // SelectionManager owns world mouse input; this panel consumes only UI input.
            if (!PlanetEnvironmentPanel.ContainsPointer(e.mousePosition) && !RobotProgrammingPanel.ContainsPointer(e.mousePosition) && ContainsPointer(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel))
                e.Use();
        }

        private void DrawBasePower(Rect rect, ColonyPowerGrid grid, float scale)
        {
            string[] rows =
            {
                $"Battery       {grid.BatteryPercent:0}%",
                $"Stored        {grid.StoredEnergyKWh:0.##} / {grid.BatteryCapacityKWh:0.##} kWh",
                $"Generation    {grid.TotalGenerationKW:0.##} kW",
                $"Consumption   {grid.TotalConsumptionKW:0.##} kW",
                $"Balance       {grid.BalanceKW:+0.##;-0.##;0} kW",
                $"Runtime       {ColonyPowerGrid.FormatDuration(grid.RuntimeHours)}"
            };
            for (int i = 0; i < rows.Length; i++)
                GUI.Label(new Rect(rect.x, rect.y + i * 22 * scale, rect.width, 24 * scale), rows[i], textStyle);
            if (!grid.HasPower)
                GUI.Label(new Rect(rect.x, rect.y - 24 * scale, rect.width, 24 * scale), grid.Status, titleStyle);
        }
        private void DrawConstruction(AssemblerConstruction builder, float x, float y, float width, float scale)
        {
            var placement = GetComponent<BuildingPlacement>();
            if (placement == null) return;
            bool previous = GUI.enabled;
            if (builder.IsBuilding)
            {
                if (GUI.Button(new Rect(x, y + 48 * scale, width, 40 * scale), "Anuluj budowę", buttonStyle)) builder.Cancel();
                return;
            }
            var kinds = new[] { ConstructionKind.SolarPanel, ConstructionKind.ChargingStation };
            for (int i = 0; i < kinds.Length; i++)
            {
                var recipe = placement.Recipe(kinds[i]);
                GUI.enabled = previous && resources != null && resources.Wood >= recipe.WoodCost &&
                    !builder.GetComponent<RobotEnergy>().IsDepleted;
                if (GUI.Button(new Rect(x, y + (48 + i * 48) * scale, width, 40 * scale),
                    recipe.DisplayName + " · " + recipe.WoodCost + " wood", buttonStyle)) placement.Begin(builder, kinds[i]);
            }
            GUI.enabled = previous;
        }
        private void DrawSoftwareButton(float x, float y, float scale)
        {
            if (RightHudPanels.MonitorButton(new Rect(x, y, 40 * scale, 40 * scale), RobotProgrammingPanel.IsOpen))
                GetComponent<RobotProgrammingPanel>().ToggleSelection();
        }

        private void DrawEnergy(Rect rect, RobotEnergy energy, float scale)
        {
            if (energy == null) return;
            int percent = Mathf.Clamp(Mathf.CeilToInt((float)energy.Percentage), 0, 100);
            if (displayedEnergy != energy || displayedEnergyPercent != percent)
            {
                displayedEnergy = energy;
                displayedEnergyPercent = percent;
                energyPercentLabel = percent + "%";
            }
            Color previous = GUI.color;
            var bar = rect;
            GUI.color = new Color(.15f, .22f, .25f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            bar.width *= energy.Normalized;
            GUI.color = energy.Percentage <= 20 ? new Color(1, .6f, .24f) : new Color(.3f, .9f, .72f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = previous;
            DrawEnergyBolt(rect, scale);
            GUI.Label(rect, energyPercentLabel, energyStyle);
        }

        private static void DrawEnergyBolt(Rect rect, float scale)
        {
            Vector2 first = new Vector2(rect.x + 14 * scale, rect.y + 3 * scale);
            Vector2 second = new Vector2(rect.x + 8 * scale, rect.y + 10 * scale);
            Vector2 third = new Vector2(rect.x + 14 * scale, rect.y + 10 * scale);
            Vector2 fourth = new Vector2(rect.x + 8 * scale, rect.y + 17 * scale);
            DrawLine(first, second, 2 * scale);
            DrawLine(second, third, 2 * scale);
            DrawLine(third, fourth, 2 * scale);
        }

        private static void DrawLine(Vector2 from, Vector2 to, float thickness)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - thickness * .5f, Vector2.Distance(from, to), thickness), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
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
            energyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 6, 0, 0)
            };
            energyStyle.normal.textColor = Color.white;
        }
    }
}
