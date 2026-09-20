using UnityEngine;

namespace Protocol
{
    /// <summary>Edits the selected robot's draft; running code is a separate compiled snapshot.</summary>
    public sealed class RobotProgrammingPanel : MonoBehaviour
    {
        private RobotLuaRuntime runtime;
        private readonly System.Collections.Generic.List<RobotLuaRuntime> targets = new System.Collections.Generic.List<RobotLuaRuntime>();
        public int TargetCount => targets.Count;

        private bool AnyRunning => targets.Exists(t => t != null && t.State == LuaProgramState.Running);


        public bool OpenGroup(System.Collections.Generic.IReadOnlyList<RobotController> robots)

        {
            if (robots == null || robots.Count == 0)
                return false;
            string kind = robots[0] != null ? robots[0].UnitType : null;
            foreach (var unit in robots)
                if (unit == null || unit.UnitType != kind || unit.LuaRuntime == null)
                    return false;
            Open(robots[0].LuaRuntime);
            targets.Clear();
            foreach (var unit in robots)
                if (!targets.Contains(unit.LuaRuntime))
                    targets.Add(unit.LuaRuntime);
            return true;
        }

        public void ApplyCode(string code)

        {
            foreach (var target in targets)
                if (target != null)
                    target.SourceCode = code;
        }

        public void RunTargets()

        {
            ApplyCode(runtime.SourceCode);
            foreach (var target in targets)
                if (target != null)
                    target.RunProgram();
        }

        public void StopTargets()

        {
            foreach (var target in targets)
                if (target != null)
                    target.StopProgram();
        }

        public void SaveTargets()

        {
            ApplyCode(runtime.SourceCode);
            foreach (var target in targets)
                if (target != null)
                    target.SaveProgram();
        }

        private Vector2 codeScroll, logScroll;
        private int selectedExample;
        private bool focusEditor;
        private readonly LuaCodeEditor codeEditor = new LuaCodeEditor();
        private static RobotProgrammingPanel activePanel;
        public static bool IsOpen => activePanel != null && activePanel.runtime != null;


        public static bool DismissCompletion()

        {
            if (!IsOpen || !activePanel.codeEditor.PopupOpen)
                return false;
            activePanel.codeEditor.Dismiss();
            return true;
        }

        private void OnDestroy() => codeEditor.Dispose();

        private void OnEnable() => activePanel = this;

        private void OnDisable()

        {
            if (activePanel == this)
                activePanel = null;
            runtime = null;
        }

        public void Open(RobotLuaRuntime selectedRuntime)

        {
            runtime = selectedRuntime;
            targets.Clear();
            if (runtime != null)
                targets.Add(runtime);
            codeEditor.IsBaseContext = runtime != null && runtime.IsBase;
            codeEditor.Reset();
            codeScroll = logScroll = Vector2.zero;
            selectedExample = runtime != null ? runtime.ExampleIndex : 0;
            focusEditor = true;
        }

        public void Close()

        {
            targets.Clear();
            runtime = null;
            codeEditor.Reset();
        }

        private void OnGUI()

        {
            if (PauseMenu.IsOpen || (Event.current.isKey && Event.current.keyCode == KeyCode.Escape))
                return;
            if (runtime == null)
                return;
            string beforeEdit = runtime.SourceCode;
            runtime.SourceCode = codeEditor.HandleInput(runtime.SourceCode ?? "", Event.current);
            Vector2 rootOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
            // Draw after the unit panel and consume mouse events over the whole modal.
            GUI.depth = -10;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 640f), 0.5f, 1.5f);
            Rect panel = new Rect((Screen.width - 900 * scale) / 2, (Screen.height - 600 * scale) / 2, 900 * scale, 600 * scale);
            Color previousColor = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.09f, 1);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 16 * scale, panel.y + 12 * scale, panel.width - 32 * scale, panel.height - 24 * scale));
            var text = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16 * scale),
                wordWrap = true,
                richText = false
            };
            var title = new GUIStyle(text)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(20 * scale)
            };
            var button = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(15 * scale)
            };
            GUILayout.Label(
                "Oprogramowanie — " + (targets.Count > 1 ? targets.Count + " × " + runtime.GetComponent<RobotController>().UnitType : runtime.name) + " — " + runtime.State,
                title);
            GUILayout.Label(
                runtime.HasUnsavedChanges ? "Niezapisane zmiany • Uruchom wykonuje kod z edytora." : "Kod zapisany • Uruchom wykonuje kod z edytora.",
                text);
            if (targets.Count > 1)
                GUILayout.Label("Wspólny kod z pierwszej jednostki. Edycja, Uruchom i Zapisz dotyczą całej grupy.", text);
            GUILayout.BeginHorizontal();
            bool wasEnabled = GUI.enabled;
            GUI.enabled = !AnyRunning;
            if (!runtime.IsBase && GUILayout.Button("◀", button, GUILayout.Width(36 * scale)))
                selectedExample = (selectedExample + RobotLuaRuntime.ExampleCount - 1) % RobotLuaRuntime.ExampleCount;
            GUILayout.Label(runtime.IsBase ? "Automatyczna produkcja harvesterów" : RobotLuaRuntime.GetExampleName(selectedExample), text);
            if (!runtime.IsBase && GUILayout.Button("▶", button, GUILayout.Width(36 * scale)))
                selectedExample = (selectedExample + 1) % RobotLuaRuntime.ExampleCount;
            if (GUILayout.Button("Zastąp kod przykładem", button))
            {
                runtime.LoadExample(selectedExample);
                codeEditor.Reset();
                focusEditor = true;
            }

            GUI.enabled = wasEnabled;
            GUILayout.EndHorizontal();
            var editor = new GUIStyle(GUI.skin.textArea)
            {
                font = codeEditor.CodeFont,
                fontSize = Mathf.RoundToInt(16 * scale),
                wordWrap = false,
                richText = false,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(8, 8, 8, 8)
            };
            codeScroll = GUILayout.BeginScrollView(codeScroll, GUILayout.Height(230 * scale));
            string code = runtime.SourceCode ?? "";
            Vector2 codeSize = editor.CalcSize(new GUIContent(code + "\n "));
            Rect codeRect = GUILayoutUtility.GetRect(Mathf.Max(panel.width - 70 * scale, codeSize.x), Mathf.Max(210 * scale, codeSize.y));
            runtime.SourceCode = codeEditor.Draw(code, codeRect, editor, codeScroll, 230 * scale, panel.width - 70 * scale, rootOrigin);
            if (focusEditor && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("RobotLuaCode");
                focusEditor = false;
            }

            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUI.enabled = !AnyRunning;
            if (GUILayout.Button("Uruchom", button, GUILayout.Height(34 * scale)))
                RunTargets();
            GUI.enabled = AnyRunning;
            if (GUILayout.Button("Zatrzymaj", button, GUILayout.Height(34 * scale)))
                StopTargets();
            GUI.enabled = wasEnabled;
            if (GUILayout.Button("Zapisz", button, GUILayout.Height(34 * scale)))
                SaveTargets();
            bool close = GUILayout.Button("Zamknij", button, GUILayout.Height(34 * scale));
            GUILayout.EndHorizontal();
            GUILayout.Label((runtime.StorageError ? "BŁĄD ZAPISU/ODCZYTU: " : "") + runtime.StorageMessage, text);
            if (AnyRunning)
                GUILayout.Label("Program działa. Edycja i zapis nie zmieniają go do następnego uruchomienia.", text);
            GUILayout.Label("Wynik / błędy", title);
            logScroll = GUILayout.BeginScrollView(logScroll);
            if (!string.IsNullOrEmpty(runtime.Error))
                GUILayout.Label("ERROR: " + runtime.Error, text);
            if (targets.Count > 1)
                foreach (var target in targets)
                    if (target != null)
                        GUILayout.Label(
                            target.name + ": " + target.State + (target.Error.Length > 0 ? " — " + target.Error : "") + (target.StorageError ? " — " + target.StorageMessage : ""),
                            text);
            GUILayout.Label(runtime.Output.Length == 0 ? "Brak komunikatów." : runtime.Output, text);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            codeEditor.DrawPopup(panel, scale, text, button);
            Event e = Event.current;
            if (e.isMouse || e.isKey || e.type == EventType.ScrollWheel)
                e.Use();
            if (runtime.SourceCode != beforeEdit)
                ApplyCode(runtime.SourceCode);
            if (close)
                Close(); // Draft and running program belong to the robot, not the panel.
        }
    }
}
