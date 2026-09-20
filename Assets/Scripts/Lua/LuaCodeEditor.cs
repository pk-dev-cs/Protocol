using System.Collections.Generic;
using UnityEngine;

namespace Protocol
{
    public sealed class LuaCodeEditor
    {
        private List<LuaToken> tokens = new List<LuaToken>();
        private List<LuaApiSuggestion> matches = new List<LuaApiSuggestion>();
        private string highlighted;
        private int caret, replacementStart, replacementEnd, selected, firstRow, pendingCaret = -1;
        private bool dismissed;
        private TextEditor textEditor;
        private int inputControl;
        private bool suppressReturnCharacter, suppressTabCharacter;

        public bool IsBaseContext { get; set; }

        private Rect popup;
        private Vector2 anchor;
        private float uiScale = 1;
        private Font font;
        private const int VisibleRows = 4;

        public bool PopupOpen => matches.Count > 0 && !dismissed;

        public Font CodeFont => font ?? (font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "monospace" }, 16));

        public void Reset()
        {
            matches.Clear();
            dismissed = false;
            highlighted = null;
            caret = 0;
            pendingCaret = -1;
            textEditor = null;
            inputControl = 0;
            suppressReturnCharacter = suppressTabCharacter = false;
        }

        public void Dismiss()
        {
            dismissed = true;
        }

        public void Dispose()
        {
            if (font != null)
                Object.Destroy(font);
        }

        public string HandleInput(string code, Event e)
        {
            // Run before BeginScrollView: scroll views consume arrows and Tab themselves.
            // The previous draw owns this ID; control names are not yet registered here.
            if (e.isKey && inputControl != 0 && GUIUtility.keyboardControl == inputControl && textEditor != null)
                return HandleKeyboard(code, e, textEditor.cursorIndex);
            if (e.isKey || !PopupOpen)
                return code;
            if (popup.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    int row = Mathf.FloorToInt((e.mousePosition.y - popup.y - 6 * uiScale) / (25 * uiScale));
                    if (row >= 0 && row < Mathf.Min(VisibleRows, matches.Count))
                    {
                        selected = firstRow + row;
                        code = Accept(code);
                    }

                    e.Use();
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    selected = Mathf.Clamp(selected + (e.delta.y > 0 ? 1 : -1), 0, matches.Count - 1);
                    KeepVisible();
                    e.Use();
                }
                else if (e.isMouse)
                    e.Use();
            }
            else if (e.type == EventType.MouseDown)
                Dismiss();
            return code;
        }

        public string HandleKeyboard(string code, Event e, int cursor)
        {
            caret = Mathf.Clamp(cursor, 0, code.Length);
            bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n' || e.character == '\r';
            bool tab = e.keyCode == KeyCode.Tab || e.character == '\t';
            if (e.type == EventType.KeyUp)
            {
                if (enter)
                    suppressReturnCharacter = false;
                if (tab)
                    suppressTabCharacter = false;
                return code;
            }

            if (e.type != EventType.KeyDown)
                return code;
            // Some platforms send a separate character event after the physical Enter.
            if (enter && suppressReturnCharacter)
            {
                e.Use();
                return code;
            }

            if (!enter)
                suppressReturnCharacter = false;
            if (e.control && (e.keyCode == KeyCode.Space || e.character == ' '))
            {
                OpenCompletion(code, caret);
                e.Use();
                return code;
            }

            if (tab && suppressTabCharacter)
            {
                e.Use();
                return code;
            }

            if (!tab)
                suppressTabCharacter = false;
            if (!PopupOpen)
            {
                if (tab)
                {
                    code = Indent(code, e.shift);
                    suppressTabCharacter = true;
                    e.Use();
                }

                return code;
            }

            if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.UpArrow)
            {
                selected = (selected + matches.Count + (e.keyCode == KeyCode.DownArrow ? 1 : -1)) % matches.Count;
                KeepVisible();
                e.Use();
            }
            else if (enter || tab)
            {
                code = Accept(code);
                suppressReturnCharacter = enter;
                suppressTabCharacter = tab;
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                Dismiss();
                e.Use();
            }
            else if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.Home || e.keyCode == KeyCode.End || e.keyCode == KeyCode.PageUp || e.keyCode == KeyCode.PageDown)
                Dismiss();
            return code;
        }

        public void OpenCompletion(string code, int cursor)
        {
            caret = Mathf.Clamp(cursor, 0, code.Length);
            matches = LuaEditorLanguage.Complete(code, caret, out replacementStart, out replacementEnd, IsBaseContext);
            selected = firstRow = 0;
            dismissed = false;
        }

        public void AfterEdit(string before, string after, int cursor, bool typedDot, int previousCursor)
        {
            if (typedDot && after != before && cursor > 0 && after[cursor - 1] == '.')
                OpenCompletion(after, cursor);
            else if (PopupOpen && after != before)
                OpenCompletion(after, cursor);
            else if (after != before && cursor > 0 && cursor <= after.Length && LuaEditorLanguage.Identifier(after[cursor - 1]))
            {
                // Typing names opens variable suggestions; moving the caret never does.
                var variables = LuaEditorLanguage.Complete(after, cursor, out int start, out int end, IsBaseContext);
                variables.RemoveAll(item => item.IsMethod);
                if (variables.Count > 0)
                {
                    matches = variables;
                    replacementStart = start;
                    replacementEnd = end;
                    selected = firstRow = 0;
                    dismissed = false;
                }
            }
            else if (PopupOpen && cursor != previousCursor)
                Dismiss();
        }

        private string Indent(string code, bool remove)
        {
            int selection = textEditor == null ? caret : Mathf.Clamp(textEditor.selectIndex, 0, code.Length);
            int start = Mathf.Min(caret, selection), end = Mathf.Max(caret, selection);
            if (!remove && start == end)
            {
                if (code.Length + 4 > RobotLuaRuntime.MaxCodeLength)
                    return code;
                code = code.Insert(caret, "    ");
                caret += 4;
            }
            else
            {
                int lineStart = start == 0 ? 0 : code.LastIndexOf('\n', start - 1) + 1;
                int last = end > start && code[end - 1] == '\n' ? end - 1 : end;
                int delta = 0;
                for (int pos = lineStart; pos <= last;)
                {
                    int count = 0;
                    if (remove)
                    {
                        while (count < 4 && pos + count < code.Length && code[pos + count] == ' ')
                            count++;
                        if (count == 0 && pos < code.Length && code[pos] == '\t')
                            count = 1;
                        code = code.Remove(pos, count);
                        count = -count;
                    }
                    else
                    {
                        if (code.Length + 4 > RobotLuaRuntime.MaxCodeLength)
                            break;
                        code = code.Insert(pos, "    ");
                        count = 4;
                    }

                    last += count;
                    delta += count;
                    int next = code.IndexOf('\n', pos);
                    if (next < 0)
                        break;
                    pos = next + 1;
                }

                caret = Mathf.Clamp(end + delta, 0, code.Length);
            }

            pendingCaret = caret;
            if (textEditor != null)
            {
                textEditor.text = code;
                textEditor.cursorIndex = textEditor.selectIndex = caret;
            }

            return code;
        }

        private void KeepVisible()
        {
            firstRow = Mathf.Clamp(firstRow, Mathf.Max(0, selected - VisibleRows + 1), selected);
        }

        private string Accept(string code)
        {
            string inserted = LuaEditorLanguage.Insert(code, replacementStart, replacementEnd, matches[selected], out int insertionCaret);
            if (inserted.Length > RobotLuaRuntime.MaxCodeLength)
            {
                Dismiss();
                return code;
            }

            code = inserted;
            pendingCaret = insertionCaret;
            caret = pendingCaret;
            if (textEditor != null)
            {
                textEditor.text = code;
                textEditor.cursorIndex = textEditor.selectIndex = caret;
            }

            matches.Clear();
            dismissed = true;
            return code;
        }

        public string Draw(
            string code,
            Rect rect,
            GUIStyle style,
            Vector2 scroll,
            float viewportHeight,
            float viewportWidth,
            Vector2 rootOrigin)
        {
            style.font = CodeFont;
            if (code != highlighted)
            {
                tokens = LuaEditorLanguage.Tokenize(code);
                highlighted = code;
            }

            GUI.Box(rect, GUIContent.none);

            var input = new GUIStyle(style)
            {
                richText = false
            };
            foreach (var state in new[]
            {
                input.normal,
                input.hover,
                input.active,
                input.focused,
                input.onNormal,
                input.onHover,
                input.onActive,
                input.onFocused
            }

            )
            {
                state.textColor = Color.clear;
                state.background = null;
            }

            bool typedDot = Event.current.type == EventType.KeyDown && Event.current.character == '.';
            int previousCursor = caret;
            GUI.SetNextControlName("RobotLuaCode");
            string edited = GUI.TextArea(rect, code, RobotLuaRuntime.MaxCodeLength, input);
            if (Event.current.type == EventType.Repaint)
            {
                // Reserve glyphs before token draws so atlas growth cannot invalidate earlier tokens.
                CodeFont.RequestCharactersInTexture(code, style.fontSize, style.fontStyle);
                var label = new GUIStyle(GUIStyle.none)
                {
                    font = style.font,
                    fontSize = style.fontSize,
                    fontStyle = style.fontStyle,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = false,
                    richText = false,
                    padding = new RectOffset(),
                    margin = new RectOffset()
                };
                label.normal.background = null;
                label.hover.background = null;
                label.active.background = null;
                label.focused.background = null;
                Vector2 p = new Vector2(rect.x + style.padding.left, rect.y + style.padding.top);
                foreach (var token in tokens)
                {
                    int end = token.Start + token.Length;
                    for (int start = token.Start; start < end;)
                    {
                        int newline = code.IndexOf('\n', start, end - start);
                        int stop = newline < 0 ? end : newline;
                        if (stop > start)
                        {
                            string segment = code.Substring(start, stop - start);
                            float width = label.CalcSize(new GUIContent(segment)).x;
                            if (p.y >= rect.y + scroll.y - style.lineHeight && p.y <= rect.y + scroll.y + viewportHeight && p.x + width >= rect.x + scroll.x && p.x <= rect.x + scroll.x + viewportWidth)
                            {
                                label.normal.textColor = ColorFor(token.Kind);
                                label.Draw(
                                    new Rect(p.x, p.y, width + 2, style.lineHeight + 2),
                                    new GUIContent(segment),
                                    false,
                                    false,
                                    false,
                                    false);
                            }

                            p.x += width;
                        }

                        if (newline >= 0)
                        {
                            p.x = rect.x + style.padding.left;
                            p.y += style.lineHeight;
                        }

                        start = stop + 1;
                    }
                }
            }
            // A consumed key does not register the TextArea's name again. Its stable
            // keyboard ID still owns focus; do not dismiss completion on EventType.Used.
            if (GUI.GetNameOfFocusedControl() == "RobotLuaCode" || (inputControl != 0 && GUIUtility.keyboardControl == inputControl))
            {
                inputControl = GUIUtility.keyboardControl;
                textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                if (pendingCaret >= 0)
                {
                    textEditor.cursorIndex = textEditor.selectIndex = Mathf.Min(pendingCaret, edited.Length);
                    pendingCaret = -1;
                }

                caret = Mathf.Clamp(textEditor.cursorIndex, 0, edited.Length);
                AfterEdit(code, edited, caret, typedDot, previousCursor);
                if (textEditor.cursorIndex != textEditor.selectIndex)
                    matches.Clear();
                anchor = GUIUtility.GUIToScreenPoint(style.GetCursorPixelPosition(
                    rect,
                    new GUIContent(edited),
                    caret) + Vector2.up * style.lineHeight) - rootOrigin;
            }
            else
                matches.Clear();
            return edited;
        }

        public void DrawPopup(Rect panel, float scale, GUIStyle text, GUIStyle button)
        {
            if (!PopupOpen)
                return;
            uiScale = scale;
            int rows = Mathf.Min(VisibleRows, matches.Count);
            float height = (rows * 25 + 94) * scale, width = Mathf.Min(460 * scale, panel.width - 24 * scale);
            float x = Mathf.Clamp(anchor.x, panel.x + 8 * scale, panel.xMax - width - 8 * scale);
            float y = anchor.y + 3 * scale;
            if (y + height > panel.yMax - 8 * scale)
                y = anchor.y - height - 22 * scale;
            popup = new Rect(x, Mathf.Clamp(y, panel.y + 8 * scale, panel.yMax - height - 8 * scale), width, height);
            Color old = GUI.color;
            GUI.color = new Color(.08f, .11f, .16f);
            GUI.DrawTexture(popup, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Box(popup, GUIContent.none);
            var label = new GUIStyle(text)
            {
                font = CodeFont,
                fontSize = Mathf.RoundToInt(14 * scale),
                wordWrap = false
            };
            for (int row = 0; row < rows; row++)
            {
                int index = firstRow + row;
                var r = new Rect(x + 6 * scale, popup.y + (6 + row * 25) * scale, width - 12 * scale, 25 * scale);
                if (index == selected)
                {
                    GUI.color = new Color(.15f, .35f, .48f);
                    GUI.DrawTexture(r, Texture2D.whiteTexture);
                    GUI.color = old;
                }

                GUI.Label(r, matches[index].Signature, label);
            }

            var description = new GUIStyle(text)
            {
                fontSize = Mathf.RoundToInt(13 * scale),
                wordWrap = true
            };
            GUI.Label(
                new Rect(x + 8 * scale, popup.y + (rows * 25 + 12) * scale, width - 16 * scale, 48 * scale),
                matches[selected].Description,
                description);
            GUI.Label(
                new Rect(x + 8 * scale, popup.yMax - 24 * scale, width - 16 * scale, 20 * scale),
                "↑ ↓ wybór • Enter / Tab wstaw • Esc zamknij   " + (selected + 1) + "/" + matches.Count,
                description);
        }

        private static Color ColorFor(LuaTokenKind kind)
        {
            switch (kind)
            {
                case LuaTokenKind.Keyword:
                    return new Color(.77f, .55f, .95f);
                case LuaTokenKind.String:
                    return new Color(.91f, .72f, .47f);
                case LuaTokenKind.Comment:
                    return new Color(.47f, .66f, .46f);
                case LuaTokenKind.Number:
                    return new Color(.62f, .82f, .67f);
                case LuaTokenKind.Api:
                    return new Color(.37f, .8f, .95f);
                default:
                    return new Color(.86f, .9f, .94f);
            }
        }
    }
}
