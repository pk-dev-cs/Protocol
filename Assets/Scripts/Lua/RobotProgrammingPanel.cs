using System.Collections.Generic;
using UnityEngine;

namespace Protocol
{
    public sealed class RobotProgrammingPanel : MonoBehaviour
    {
        private RobotLuaRuntime runtime;
        private readonly List<RobotLuaRuntime> targets = new List<RobotLuaRuntime>();
        private readonly List<RobotLuaRuntime> nextTargets = new List<RobotLuaRuntime>();
        private readonly LuaCodeEditor codeEditor = new LuaCodeEditor();
        private SelectionManager selection;
        private Vector2 codeScroll, logScroll;
        private int selectedExample;
        private bool examplesOpen, editorFocused;
        private GUIStyle textStyle, titleStyle, buttonStyle, editorStyle;
        private static RobotProgrammingPanel activePanel;

        public int TargetCount => targets.Count;
        public RobotLuaRuntime CurrentRuntime => runtime;
        public static bool IsOpen => activePanel != null && RightHudPanels.IsRequested(RightHudPanel.Software);
        public static bool HasEditorFocus => IsOpen && !PauseMenu.IsOpen && activePanel.editorFocused;
        public bool IsReadOnly => AnyRunning;
        private bool AnyRunning => targets.Exists(t => t != null && t.State == LuaProgramState.Running);
        private static bool Visible => !PauseMenu.IsOpen && !ScenarioLoadingScreen.IsVisible;
        public static Rect PanelRect
        {
            get
            {
                float scale = RightHudPanels.Scale;
                float width = Mathf.Min(Screen.width * .48f, Mathf.Clamp(Screen.width * .3f, 400 * scale, 680 * scale));
                float top = 78 * Mathf.Clamp(Screen.height / 900f, .7f, 1.5f);
                return RightHudPanels.SlideRect(RightHudPanel.Software, width, top, Screen.height - top);
            }
        }

        public static bool ContainsPointer(Vector2 position) => activePanel != null && Visible &&
            RightHudPanels.Progress(RightHudPanel.Software) > 0 && PanelRect.Contains(position);

        private void OnEnable()
        {
            activePanel = this;
            selection = GetComponent<SelectionManager>();
            if (selection != null) selection.SelectionChanged += FollowSelection;
        }

        private void OnDisable()
        {
            if (selection != null) selection.SelectionChanged -= FollowSelection;
            Close();
            if (activePanel == this) activePanel = null;
            runtime = null;
        }

        private void OnDestroy() => codeEditor.Dispose();

        private void Update()
        {
            if (!IsOpen) ReleaseEditorFocus();
            if (IsOpen && (runtime == null || !runtime.isActiveAndEnabled)) FollowSelection();
        }

        public void ToggleSelection()
        {
            if (IsOpen) Close();
            else if (BindSelection())
                RightHudPanels.Open(RightHudPanel.Software);
        }

        private void FollowSelection()
        {
            if (IsOpen && !BindSelection()) Close();
        }

        private bool BindSelection()
        {
            nextTargets.Clear();
            if (selection == null) return false;
            if (selection.SelectedStructure != null && !selection.SelectedStructure.IsMine)
            {
                var candidate = selection.SelectedStructure.GetComponent<RobotLuaRuntime>();
                if (candidate != null) nextTargets.Add(candidate);
            }
            else if (selection.SelectedRobots.Count > 0)
            {
                string kind = selection.SelectedRobots[0].UnitType;
                foreach (var robot in selection.SelectedRobots)
                {
                    if (robot == null || robot.UnitType != kind || robot.LuaRuntime == null) return false;
                    nextTargets.Add(robot.LuaRuntime);
                }
            }
            if (nextTargets.Count == 0) return false;
            bool same = targets.Count == nextTargets.Count;
            for (int i = 0; same && i < targets.Count; i++) same = targets[i] == nextTargets[i];
            if (!same) Bind(nextTargets);
            return true;
        }

        private void Bind(IReadOnlyList<RobotLuaRuntime> selected)
        {
            ReleaseEditorFocus();
            targets.Clear();
            for (int i = 0; i < selected.Count; i++) targets.Add(selected[i]);
            runtime = targets[0];
            codeEditor.Reset();
            codeEditor.IsBaseContext = runtime.IsBase;
            codeScroll = logScroll = Vector2.zero;
            selectedExample = runtime.ExampleIndex;
        }

        public void Open(RobotLuaRuntime selectedRuntime)
        {
            if (selectedRuntime == null) return;
            Bind(new[] { selectedRuntime });
            RightHudPanels.Open(RightHudPanel.Software);
        }

        public bool OpenGroup(IReadOnlyList<RobotController> robots)
        {
            if (robots == null || robots.Count == 0 || robots[0] == null) return false;
            nextTargets.Clear();
            string kind = robots[0].UnitType;
            foreach (var robot in robots)
            {
                if (robot == null || robot.UnitType != kind || robot.LuaRuntime == null) return false;
                nextTargets.Add(robot.LuaRuntime);
            }
            Bind(nextTargets);
            RightHudPanels.Open(RightHudPanel.Software);
            return true;
        }

        public void ApplyCode(string code)
        {
            if (IsReadOnly) return;
            foreach (var target in targets) if (target != null) target.SourceCode = code;
        }

        public void RunTargets()
        {
            if (runtime == null) return;
            ApplyCode(runtime.SourceCode);
            foreach (var target in targets) if (target != null) target.RunProgram();
        }

        public void StopTargets()
        {
            foreach (var target in targets) if (target != null) target.StopProgram();
        }

        public void SaveTargets()
        {
            if (runtime == null) return;
            ApplyCode(runtime.SourceCode);
            foreach (var target in targets) if (target != null) target.SaveProgram();
        }

        public void Close()
        {
            RightHudPanels.Close(RightHudPanel.Software);
            ReleaseEditorFocus();
        }

        private void ReleaseEditorFocus()
        {
            codeEditor.ReleaseFocus();
            editorFocused = false;
        }

        public static bool DismissCompletion()
        {
            if (!IsOpen || !activePanel.codeEditor.PopupOpen) return false;
            activePanel.codeEditor.Dismiss();
            return true;
        }

        private void OnGUI()
        {
            if (!Visible || runtime == null || RightHudPanels.Progress(RightHudPanel.Software) <= 0) return;
            Event e = Event.current;
            if (e.isKey && e.keyCode == KeyCode.Escape) return;
            Rect panel = PanelRect;
            if (e.rawType == EventType.MouseDown && !panel.Contains(e.mousePosition))
                ReleaseEditorFocus();
            string beforeEdit = runtime.SourceCode;
            if (IsReadOnly) codeEditor.Dismiss();
            if (IsOpen && !IsReadOnly) runtime.SourceCode = codeEditor.HandleInput(runtime.SourceCode ?? "", e);
            float scale = RightHudPanels.Scale;
            EnsureStyles(scale);
            int oldDepth = GUI.depth;
            GUI.depth = -20;
            RightHudPanels.DrawBackground(panel);
            Vector2 rootOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && IsOpen;
            float padding = 16 * scale;
            Rect content = new Rect(panel.x + padding, panel.y + 12 * scale, panel.width - padding * 2, panel.height - 24 * scale);
            GUILayout.BeginArea(content);
            GUILayout.BeginHorizontal();
            GUILayout.Label("SOFTWARE", titleStyle);
            bool close = GUILayout.Button("×", buttonStyle, GUILayout.Width(28 * scale), GUILayout.Height(26 * scale));
            GUILayout.EndHorizontal();
            string unitName = targets.Count > 1 ? targets.Count + " × " + runtime.GetComponent<RobotController>().UnitType : runtime.name;
            GUILayout.Label(unitName + (targets.Exists(t => t != null && t.HasUnsavedChanges) ? " *" : ""), textStyle);
            GUILayout.BeginHorizontal();
            int running = 0;
            foreach (var target in targets) if (target != null && target.State == LuaProgramState.Running) running++;
            GUILayout.Label(targets.Count > 1 ? "Running: " + running + "/" + targets.Count : runtime.State.ToString(), textStyle);
            if (GUILayout.Button(examplesOpen ? "Przykłady ▴" : "Przykłady ▾", buttonStyle, GUILayout.Width(112 * scale))) examplesOpen = !examplesOpen;
            GUILayout.EndHorizontal();
            if (examplesOpen) DrawExamples(scale);

            float editorHeight = Mathf.Max(70 * scale, content.height - (examplesOpen ? 260 : 204) * scale);
            float viewportWidth = content.width - 20;
            codeScroll = GUILayout.BeginScrollView(codeScroll, GUILayout.Height(editorHeight));
            string code = runtime.SourceCode ?? "";
            Vector2 codeSize = editorStyle.CalcSize(new GUIContent(code + "\n "));
            Rect codeRect = GUILayoutUtility.GetRect(Mathf.Max(viewportWidth, codeSize.x), Mathf.Max(editorHeight - 20, codeSize.y));
            runtime.SourceCode = codeEditor.Draw(code, codeRect, editorStyle, codeScroll, editorHeight, viewportWidth, rootOrigin, IsReadOnly);
            editorFocused = IsOpen && codeEditor.HasKeyboardFocus;
            GUILayout.EndScrollView();

            bool interactive = GUI.enabled;
            GUILayout.BeginHorizontal();
            GUI.enabled = interactive && !AnyRunning;
            if (GUILayout.Button("Run", buttonStyle, GUILayout.Height(30 * scale))) RunTargets();
            GUI.enabled = interactive && AnyRunning;
            if (GUILayout.Button("Stop", buttonStyle, GUILayout.Height(30 * scale))) StopTargets();
            GUI.enabled = interactive;
            if (GUILayout.Button("Save", buttonStyle, GUILayout.Height(30 * scale))) SaveTargets();
            GUILayout.EndHorizontal();
            if (IsReadOnly) GUILayout.Label("Stop script to edit", textStyle);
            GUILayout.Label("OUTPUT", titleStyle);
            logScroll = GUILayout.BeginScrollView(logScroll);
            if (runtime.StorageError) GUILayout.Label(runtime.StorageMessage, textStyle);
            if (!string.IsNullOrEmpty(runtime.Error)) GUILayout.Label(runtime.Error, textStyle);
            if (targets.Count > 1)
                foreach (var target in targets)
                    if (target != null) GUILayout.Label(target.name + ": " + target.State + (target.Error.Length > 0 ? " — " + target.Error : "") + (target.StorageError ? " — " + target.StorageMessage : ""), textStyle);
            if (runtime.Output.Length > 0) GUILayout.Label(runtime.Output, textStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (IsOpen && !IsReadOnly) codeEditor.DrawPopup(panel, scale, textStyle, buttonStyle);
            GUI.enabled = previousEnabled;
            if (runtime.SourceCode != beforeEdit) ApplyCode(runtime.SourceCode);
            if (panel.Contains(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();
            if (HasEditorFocus && e.isKey) e.Use();
            if (close) Close();
            GUI.depth = oldDepth;
        }

        private void DrawExamples(float scale)
        {
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !AnyRunning;
            GUILayout.BeginHorizontal();
            if (!runtime.IsBase && GUILayout.Button("‹", buttonStyle, GUILayout.Width(25 * scale))) selectedExample = (selectedExample + RobotLuaRuntime.ExampleCount - 1) % RobotLuaRuntime.ExampleCount;
            GUILayout.Label(runtime.IsBase ? "Produkcja" : RobotLuaRuntime.GetExampleName(selectedExample), textStyle);
            if (!runtime.IsBase && GUILayout.Button("›", buttonStyle, GUILayout.Width(25 * scale))) selectedExample = (selectedExample + 1) % RobotLuaRuntime.ExampleCount;
            if (GUILayout.Button("Wstaw", buttonStyle, GUILayout.Width(65 * scale)))
            {
                runtime.LoadExample(selectedExample);
                codeEditor.Reset();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = enabled;
        }

        private void EnsureStyles(float scale)
        {
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
                textStyle.normal.textColor = new Color(.83f, .88f, .92f);
                titleStyle = new GUIStyle(textStyle) { fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = RightHudPanels.Accent;
                buttonStyle = new GUIStyle(GUI.skin.button);
                editorStyle = new GUIStyle(GUI.skin.textArea)
                {
                    font = codeEditor.CodeFont, wordWrap = false, richText = false,
                    alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 8, 8)
                };
            }
            textStyle.fontSize = titleStyle.fontSize = buttonStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(16 * scale));
            editorStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(16 * scale));
        }
    }
}
