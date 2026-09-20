using System;
using System.Collections.Generic;

namespace Protocol
{
    public enum LuaTokenKind
    {
        Plain,
        Keyword,
        String,
        Comment,
        Number,
        Api
    }

    public struct LuaToken
    {
        public int Start, Length;
        public LuaTokenKind Kind;

        public LuaToken(int start, int length, LuaTokenKind kind)
        {
            Start = start;
            Length = length;
            Kind = kind;
        }
    }

    public sealed class LuaApiSuggestion
    {
        public readonly string Name, Argument, Description;
        public readonly bool IsMethod;
        public readonly string Receiver;

        public string Signature => IsMethod ? (Receiver.Length > 0 ? Receiver + "." : "") + Name + "(" + Argument + ")" : Name;

        public LuaApiSuggestion(string name, string argument, string description, bool isMethod = true, string receiver = "robot")
        {
            Name = name;
            Argument = argument;
            Description = description;
            IsMethod = isMethod;
            Receiver = receiver;
        }
    }

    public static class LuaEditorLanguage
    {
        public static readonly LuaApiSuggestion[] Methods =
        {
            new LuaApiSuggestion("findNearestMine", "", "Zwraca najbliższą odkrytą kopalnię lub nil."),
            new LuaApiSuggestion("moveTo", "mine", "Jedzie do kopalni lub drzewa i czeka na dotarcie."),
            new LuaApiSuggestion("mine", "mine", "Wydobywa iron do pełnego cargo. Wymaga obecności przy kopalni."),
            new LuaApiSuggestion("returnToBase", "", "Wraca do bazy i czeka na dotarcie."),
            new LuaApiSuggestion("depositResources", "", "Oddaje cargo. Wymaga obecności przy bazie."),
            new LuaApiSuggestion("getCargo", "", "Zwraca bieżącą ilość surowca w cargo."),
            new LuaApiSuggestion("getCargoCapacity", "", "Zwraca maksymalną pojemność cargo."),
            new LuaApiSuggestion("getPosition", "", "Zwraca tabelę pozycji: x, y, z."),
            new LuaApiSuggestion("findNearestTree", "", "Zwraca i rezerwuje najbliższe odkryte, dostępne drzewo lub nil."),
            new LuaApiSuggestion("chop", "tree", "Zbiera wood do pełnego cargo lub wyczerpania drzewa. Najpierw moveTo(tree)."),
            new LuaApiSuggestion("wait", "seconds", "Czeka 0–3600 sekund czasu gry. Oddaje sterowanie."),
        };
        private static readonly HashSet<string> Keywords = new HashSet<string>("and break do else elseif end false for function goto if in local nil not or repeat return then true until while".Split(' '));

        public static bool Identifier(char c) => char.IsLetterOrDigit(c) || c == '_';

        public static List<LuaToken> Tokenize(string code)
        {
            var result = new List<LuaToken>();
            for (int i = 0; i < code.Length;)
            {
                int start = i;
                var kind = LuaTokenKind.Plain;
                char c = code[i];
                if (c == '-' && i + 1 < code.Length && code[i + 1] == '-')
                {
                    kind = LuaTokenKind.Comment;
                    i += 2;
                    if (!LongString(code, ref i))
                        while (i < code.Length && code[i] != '\n')
                            i++;
                }
                else if (c == '\'' || c == '"')
                {
                    kind = LuaTokenKind.String;
                    i++;
                    while (i < code.Length)
                    {
                        if (code[i] == '\\')
                        {
                            i = Math.Min(code.Length, i + 2);
                            continue;
                        }

                        if (code[i++] == c)
                            break;
                    }
                }
                else if (c == '[' && LongString(code, ref i))
                    kind = LuaTokenKind.String;
                else if (char.IsDigit(c) || (c == '.' && i + 1 < code.Length && char.IsDigit(code[i + 1])))
                {
                    kind = LuaTokenKind.Number;
                    i++;
                    while (i < code.Length)
                    {
                        char n = code[i];
                        if (char.IsLetterOrDigit(n) || (n == '.' && (i + 1 == code.Length || code[i + 1] != '.') && code[i - 1] != '.'))
                        {
                            i++;
                            continue;
                        }

                        if ((n == '+' || n == '-') && "eEpP".IndexOf(code[i - 1]) >= 0)
                        {
                            i++;
                            continue;
                        }

                        break;
                    }
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    i++;
                    while (i < code.Length && Identifier(code[i]))
                        i++;
                    string word = code.Substring(start, i - start);
                    if (Keywords.Contains(word))
                        kind = LuaTokenKind.Keyword;
                    else if (word == "robot" || word == "economy" || word == "buildHarvester" || word == "print" || word == "coroutine" || word == "math" || Array.Exists(
                        Methods,
                        m => m.Name == word))
                        kind = LuaTokenKind.Api;
                }
                else
                    i++;
                result.Add(new LuaToken(start, i - start, kind));
            }

            return result;
        }

        private static bool LongString(string code, ref int i)
        {
            if (i >= code.Length || code[i] != '[')
                return false;
            int next = i + 1;
            while (next < code.Length && code[next] == '=')
                next++;
            if (next >= code.Length || code[next] != '[')
                return false;
            string close = "]" + new string ('=', next - i - 1) + "]";
            int end = code.IndexOf(close, next + 1, StringComparison.Ordinal);
            i = end < 0 ? code.Length : end + close.Length;
            return true;
        }

        public static List<LuaApiSuggestion> Complete(string code, int caret, out int start, out int end, bool baseContext = false)
        {
            caret = Math.Max(0, Math.Min(caret, code.Length));
            start = caret;
            end = caret;
            while (start > 0 && Identifier(code[start - 1]))
                start--;
            while (end < code.Length && Identifier(code[end]))
                end++;
            var result = new List<LuaApiSuggestion>();
            int receiver = start - 6;
            bool member = receiver >= 0 && code.Substring(
                receiver,
                6) == "robot." && (receiver == 0 || (!Identifier(code[receiver - 1]) && code[receiver - 1] != '.' && code[receiver - 1] != ':'));
            var tokens = Tokenize(code);
            int contextPosition = member ? receiver : Math.Max(0, caret - 1);
            foreach (var token in tokens)
                if (contextPosition >= token.Start && contextPosition < token.Start + token.Length && (token.Kind == LuaTokenKind.Comment || token.Kind == LuaTokenKind.String))
                    return result;
            string prefix = code.Substring(start, caret - start);
            int economyStart = start - 8;
            if (economyStart >= 0 && code.Substring(
                economyStart,
                8) == "economy." && (economyStart == 0 || (!Identifier(code[economyStart - 1]) && code[economyStart - 1] != '.')))
            {
                foreach (var name in new[]
                {
                    "iron",
                    "wood"
                }

                )
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        result.Add(new LuaApiSuggestion(name, "", "Aktualna ilość zasobu w bazie; tylko odczyt.", false, "economy"));
                return result;
            }

            if (member)
            {
                if (!baseContext)
                    foreach (var method in Methods)
                        if (method.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            result.Add(method);
                return result;
            }

            if (start > 0 && (code[start - 1] == '.' || code[start - 1] == ':'))
                return result;
            var names = new HashSet<string>();
            if (baseContext && "buildHarvester".StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                result.Add(new LuaApiSuggestion("buildHarvester", "", "Produkuje harvestera za 100 wood w 8 s.", true, ""));
            foreach (var name in LocalNames(code, tokens, caret))
                if (names.Add(name) && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(new LuaApiSuggestion(name, "", "Zmienna lokalna zadeklarowana przed kursorem.", false));
            foreach (var name in baseContext ? new[]
            {
                "economy",
                "unitName"
            }

            : new[]
            {
                "robot",
                "economy",
                "unitName"
            }

            )
                if (names.Add(name) && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(new LuaApiSuggestion(
                        name,
                        "",
                        name == "robot" ? "API sterowania robotem. Wpisz kropkę, aby zobaczyć metody." : name == "economy" ? "Stan zasobów: iron i wood. Wpisz kropkę." : "Nazwa obiektu uruchamiającego program.",
                        false));
            return result;
        }

        private static IEnumerable<string> LocalNames(string code, List<LuaToken> tokens, int caret)
        {
            var scopes = new List<List<string>>
            {
                new List<string>()
            };
            bool declaration = false, expectName = false;
            foreach (var token in tokens)
            {
                if (token.Start >= caret)
                    break;
                string value = code.Substring(token.Start, token.Length);
                if (token.Kind == LuaTokenKind.Comment || token.Kind == LuaTokenKind.String || string.IsNullOrWhiteSpace(value))
                    continue;
                if (value == "end" || value == "until")
                {
                    if (scopes.Count > 1)
                        scopes.RemoveAt(scopes.Count - 1);
                    declaration = false;
                }

                if (value == "else")
                {
                    if (scopes.Count > 1)
                        scopes[scopes.Count - 1].Clear();
                }

                if (value == "local")
                {
                    declaration = expectName = true;
                    continue;
                }

                if (declaration)
                {
                    if (value == "function")
                        continue;
                    if (expectName && (char.IsLetter(value[0]) || value[0] == '_') && !Keywords.Contains(value))
                    {
                        scopes[scopes.Count - 1].Add(value);
                        expectName = false;
                        continue;
                    }

                    if (!expectName && value == ",")
                    {
                        expectName = true;
                        continue;
                    }

                    declaration = false;
                }

                if (value == "then" || value == "do" || value == "function" || value == "repeat")
                    scopes.Add(new List<string>());
            }

            for (int i = scopes.Count - 1; i >= 0; i--)
                foreach (var name in scopes[i])
                    yield return name;
        }

        public static string Insert(string code, int start, int end, LuaApiSuggestion method, out int caret)
        {
            if (!method.IsMethod)
            {
                caret = start + method.Name.Length;
                return code.Substring(0, start) + method.Name + code.Substring(end);
            }

            int next = end;
            while (next < code.Length && (code[next] == ' ' || code[next] == '\t'))
                next++;
            bool hasCall = next < code.Length && code[next] == '(';
            string inserted = method.Name + (hasCall ? "" : "()");
            string result = code.Substring(0, start) + inserted + code.Substring(end);
            caret = start + method.Name.Length + (hasCall ? next - end + 1 : method.Argument.Length > 0 ? 1 : 2);
            return result;
        }
    }
}
