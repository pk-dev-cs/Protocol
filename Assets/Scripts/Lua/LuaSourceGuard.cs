using MoonSharp.Interpreter;

namespace Protocol
{
    /// <summary>Bounds parser recursion before compiling player input. Not a replacement Lua parser.</summary>
    public static class LuaSourceGuard
    {
        public static void Validate(string code)

        {
            int delimiters = 0, blocks = 0, operators = 0, tokens = 0;
            for (int i = 0; i < code.Length;)
            {
                char c = code[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '-' && i + 1 < code.Length && code[i + 1] == '-')
                {
                    i += 2;
                    if (!SkipLongString(code, ref i))
                        while (i < code.Length && code[i] != '\n')
                            i++;
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    char quote = c;
                    i++;
                    while (i < code.Length)
                    {
                        if (code[i] == '\\')
                        {
                            i = System.Math.Min(code.Length, i + 2);
                            continue;
                        }

                        if (code[i++] == quote)
                            break;
                    }

                    continue;
                }

                if (c == '[' && SkipLongString(code, ref i))
                    continue;
                if (++tokens > 2048)
                    Reject();
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i++;
                    while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                        i++;
                    string token = code.Substring(start, i - start);
                    if (token == "function" || token == "if" || token == "do" || token == "repeat")
                        blocks++;
                    if (token == "end" || token == "until")
                        blocks = System.Math.Max(0, blocks - 1);
                    if (token == "not" || token == "and" || token == "or")
                        operators++;
                }
                else
                {
                    if (c == '(' || c == '[' || c == '{')
                        delimiters++;
                    if (c == ')' || c == ']' || c == '}')
                        delimiters = System.Math.Max(0, delimiters - 1);
                    if ("+-*/%^#=<>~.:".IndexOf(c) >= 0)
                        operators++;
                    i++;
                }

                if (delimiters + blocks > 48 || operators > 128)
                    Reject();
            }
        }

        private static bool SkipLongString(string code, ref int position)

        {
            if (position >= code.Length || code[position] != '[')
                return false;
            int cursor = position + 1;
            while (cursor < code.Length && code[cursor] == '=')
                cursor++;
            if (cursor >= code.Length || code[cursor] != '[')
                return false;
            string end = "]" + new string ('=', cursor - position - 1) + "]";
            int closing = code.IndexOf(end, cursor + 1, System.StringComparison.Ordinal);
            position = closing < 0 ? code.Length : closing + end.Length;
            return true;
        }

        private static void Reject() => throw new ScriptRuntimeException("Przekroczono limit złożoności kodu (48 poziomów, 128 operatorów lub 2048 tokenów).");

    }
}
