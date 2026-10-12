using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;

namespace Protocol
{
    internal sealed class LuaSaveReplay
    {
        private static readonly ConditionalWeakTable<Script, LuaSaveReplay> Owners = new ConditionalWeakTable<Script, LuaSaveReplay>();
        private readonly Dictionary<Table, int> tableIds = new Dictionary<Table, int>();
        private readonly Dictionary<int, Table> tables = new Dictionary<int, Table>();
        private readonly Script script;
        private int cursor, repeated;
        public bool Replaying { get; private set; }
        public long ResumeCount { get; set; }
        public string RunningSource { get; }
        public JArray Calls { get; private set; } = new JArray();

        public LuaSaveReplay(Script owner, string source)
        {
            script = owner;
            RunningSource = source;
            Owners.Add(owner, this);
            var math = script.Globals.Get("math").Table;
            foreach (string name in new[] { "random", "randomseed" })
            {
                string key = "math." + name;
                DynValue original = math.Get(name);
                math.Set(name, DynValue.NewCallback((context, args) => Invoke(owner, key, () =>
                {
                    var values = new DynValue[args.Count];
                    for (int i = 0; i < values.Length; i++) values[i] = args[i];
                    return owner.Call(original, values);
                })));
            }
        }

        // Replaying reconstructs the VM stack and locals, never repeats gameplay side effects.
        public static DynValue Invoke(Script owner, string name, Func<DynValue> action)
        {
            if (!Owners.TryGetValue(owner, out var replay)) return action();
            if (replay.Replaying)
            {
                if (replay.cursor >= replay.Calls.Count || (string)replay.Calls[replay.cursor]["name"] != name)
                    throw new InvalidOperationException("Lua save replay diverged at " + name);
                var call = replay.Calls[replay.cursor];
                DynValue recorded = replay.Decode(call["value"]);
                if (++replay.repeated >= ((int?)call["repeat"] ?? 1))
                {
                    replay.cursor++;
                    replay.repeated = 0;
                }
                return recorded;
            }
            DynValue value = action();
            JObject encoded = replay.Encode(value);
            var last = replay.Calls.Count > 0 ? (JObject)replay.Calls[replay.Calls.Count - 1] : null;
            if (last != null && (string)last["name"] == name && JToken.DeepEquals(last["value"], encoded))
                last["repeat"] = (int?)last["repeat"] + 1 ?? 2;
            else replay.Calls.Add(new JObject { ["name"] = name, ["value"] = encoded, ["repeat"] = 1 });
            return value;
        }

        public int TableId(Table table)
        {
            if (!tableIds.TryGetValue(table, out int id))
            {
                id = tableIds.Count + 1;
                tableIds.Add(table, id);
                tables.Add(id, table);
            }
            return id;
        }

        public Table ResolveTable(int id) => tables.TryGetValue(id, out var table) ? table
            : throw new InvalidOperationException("Missing Lua table in save.");

        private JObject Encode(DynValue value)
        {
            var node = new JObject { ["type"] = value.Type.ToString() };
            switch (value.Type)
            {
                case DataType.Nil:
                case DataType.Void:
                case DataType.YieldRequest: break;
                case DataType.Boolean: node["value"] = value.Boolean; break;
                case DataType.Number: node["value"] = value.Number; break;
                case DataType.String: node["value"] = value.String; break;
                case DataType.Table:
                    bool known = tableIds.ContainsKey(value.Table);
                    node["id"] = TableId(value.Table);
                    if (!known)
                    {
                        var pairs = new JArray();
                        foreach (var pair in value.Table.Pairs) pairs.Add(new JArray(Encode(pair.Key), Encode(pair.Value)));
                        node["pairs"] = pairs;
                    }
                    break;
                case DataType.Tuple:
                    var tuple = new JArray();
                    foreach (var item in value.Tuple) tuple.Add(Encode(item));
                    node["items"] = tuple;
                    break;
                default: throw new InvalidOperationException("Unsupported Lua save value: " + value.Type);
            }
            return node;
        }

        private DynValue Decode(JToken node)
        {
            switch ((string)node["type"])
            {
                case "Nil": return DynValue.Nil;
                case "Void": return DynValue.Void;
                case "YieldRequest": return DynValue.NewYieldReq(new DynValue[0]);
                case "Boolean": return DynValue.NewBoolean((bool)node["value"]);
                case "Number": return DynValue.NewNumber((double)node["value"]);
                case "String": return DynValue.NewString((string)node["value"]);
                case "Table":
                    int id = (int)node["id"];
                    if (!tables.TryGetValue(id, out var table))
                    {
                        table = new Table(script);
                        tables.Add(id, table);
                        tableIds.Add(table, id);
                        foreach (var pair in (JArray)node["pairs"]) table.Set(Decode(pair[0]), Decode(pair[1]));
                    }
                    return DynValue.NewTable(table);
                case "Tuple":
                    var items = (JArray)node["items"];
                    var values = new DynValue[items.Count];
                    for (int i = 0; i < values.Length; i++) values[i] = Decode(items[i]);
                    return DynValue.NewTuple(values);
                default: throw new InvalidOperationException("Invalid Lua save value.");
            }
        }

        public JObject Capture() => new JObject
        {
            ["source"] = RunningSource, ["resumes"] = ResumeCount, ["calls"] = Calls.DeepClone()
        };

        public void BeginRestore(JObject data)
        {
            Calls = (JArray)data["calls"].DeepClone();
            cursor = repeated = 0;
            ResumeCount = 0;
            Replaying = true;
        }

        public void FinishRestore()
        {
            if (cursor != Calls.Count) throw new InvalidOperationException("Incomplete Lua replay.");
            Replaying = false;
        }
    }
}
