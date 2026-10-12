using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MoonSharp.Interpreter;

namespace Protocol
{
    // Only these explicitly versioned gameplay fields belong to the save contract.
    internal static class SaveStateCodec
    {
        private static readonly Dictionary<Type, string[]> Fields = new Dictionary<Type, string[]>
        {
            { typeof(ResourceManager), new[] { "@Iron", "@Wood" } },
            { typeof(BaseBuilding), new[] { "harvester", "assembler", "hauler", "nextHauler", "activeRecipe", "productionElapsed", "deploymentRetrySeconds", "paidWood", "nextAssembler", "nextHarvester", "@IsProducing", "@IsWaitingForDeployment" } },
            { typeof(ColonyPowerGrid), new[] { "batteryCapacityKWh", "storedEnergyKWh", "baseConsumptionKW", "consumers" } },
            { typeof(AlienWorldLighting), new[] { "cycleDurationSeconds", "initialPhase", "phase", "@ElapsedSimulationSeconds" } },
            { typeof(RTSCameraController), new[] { "focus", "distance" } },
            { typeof(FogOfWar), new[] { "explored" } },
            { typeof(RobotController), new[] { "cargoCapacity", "unitType", "@MineDestination", "@BaseDestination" } },
            { typeof(RobotInventory), new[] { "@Capacity", "@Cargo", "@Kind" } },
            { typeof(RobotEnergy), new[] { "fullBatteryMovementDuration", "percentage", "capacityKWh", "movementKW", "constructionKW", "miningKW", "usesPowerModel" } },
            { typeof(RobotHarvestCycle), new[] { "elapsed", "@State", "@Message" } },
            { typeof(HaulerTowing), new[] { "target", "station", "dock", "stage", "towingKW", "@Message", "@Succeeded" } },
            { typeof(RobotMovement), new[] { "transportOwner" } },
            { typeof(RobotCharging), new[] { "station", "dock", "travelling", "@Message" } },
            { typeof(AssemblerConstruction), new[] { "site", "resources", "elapsed", "paidWood", "travelling", "approach", "@Status" } },
            { typeof(TreeResource), new[] { "@WoodRemaining", "@ReservedBy" } },
            { typeof(Mine), new[] { "secondsPerUnit" } },
            { typeof(SolarPanel), new[] { "maximumGenerationKW" } },
            { typeof(BatteryStorage), new[] { "capacityKWh", "idleConsumptionKW" } },
            { typeof(ChargingStationStructure), new[] { "idleConsumptionKW", "chargingPowerKW", "assignedRobot", "charging", "allocatedKW" } },
            { typeof(RobotLuaAPI), new[] { "mines", "trees", "stations", "robots", "reservedTree", "activeTree", "pending", "activeMine", "activeBase", "activeStation", "elapsed", "waitSeconds", "callingLocation", "@PendingLocation" } },
            { typeof(BaseLuaAPI), new[] { "building" } },
            { typeof(RobotLuaRuntime), new[] { "sourceCode", "savedSourceCode", "forcedYields", "@State", "@Output", "@Error", "@ExampleIndex", "@StorageMessage", "@StorageError" } }
        };

        public static Type ComponentType(string name)
        {
            foreach (var type in Fields.Keys)
                if (typeof(Component).IsAssignableFrom(type) && type.Name == name) return type;
            throw new InvalidOperationException("Unsupported saved component: " + name);
        }

        public static JObject Capture(object instance, SaveObjectIndex index, LuaSaveReplay replay = null)
        {
            var result = new JObject();
            foreach (string name in Fields[instance.GetType()])
            {
                var field = Field(instance.GetType(), name);
                result[name] = Write(field.GetValue(instance), index, replay);
            }
            return result;
        }

        public static void Restore(object instance, JObject data, SaveObjectIndex index, LuaSaveReplay replay = null)
        {
            foreach (string name in Fields[instance.GetType()])
            {
                if (!data.TryGetValue(name, out var token))
                    {
                    if ((instance is BaseBuilding && (name == "hauler" || name == "nextHauler")) ||
                        (instance is RobotLuaAPI && name == "robots")) continue;
                    throw new InvalidOperationException("Incomplete save field: " + name);
                }
                var field = Field(instance.GetType(), name);
                object value = Read(token, field.FieldType, index, replay);
                if (field.IsInitOnly && field.GetValue(instance) is IDictionary dictionary)
                {
                    dictionary.Clear();
                    foreach (DictionaryEntry entry in (IDictionary)value) dictionary.Add(entry.Key, entry.Value);
                }
                else if (field.IsInitOnly && field.GetValue(instance) is Array array)
                    Array.Copy((Array)value, array, array.Length);
                else field.SetValue(instance, value);
            }
        }

        private static FieldInfo Field(Type type, string name)
        {
            string actual = name.StartsWith("@") ? "<" + name.Substring(1) + ">k__BackingField" : name;
            return type.GetField(actual, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?? throw new InvalidOperationException("Save contract changed: " + type.Name + "." + name);
        }

        private static JToken Write(object value, SaveObjectIndex index, LuaSaveReplay replay)
        {
            if (value == null) return JValue.CreateNull();
            if (value is UnityEngine.Object unity)
            {
                if (unity == null) return JValue.CreateNull();
                var transform = unity is GameObject go ? go.transform : ((Component)unity).transform;
                return new JObject { ["ref"] = index.Id(transform), ["component"] = unity.GetType().Name };
            }
            if (value is Table table) return new JObject { ["table"] = replay.TableId(table) };
            if (value is IDictionary dictionary)
            {
                var pairs = new JArray();
                foreach (DictionaryEntry item in dictionary)
                    pairs.Add(new JArray(Write(item.Key, index, replay), Write(item.Value, index, replay)));
                return pairs;
            }
            if (value is Array array)
            {
                var items = new JArray();
                foreach (var item in array) items.Add(Write(item, index, replay));
                return items;
            }
            Type type = value.GetType();
            if (type.IsEnum) return new JValue(Convert.ToInt32(value));
            if (type.IsPrimitive || value is string) return new JValue(value);
            return new JObject { ["json"] = JsonUtility.ToJson(value) };
        }

        private static object Read(JToken token, Type type, SaveObjectIndex index, LuaSaveReplay replay)
        {
            if (token.Type == JTokenType.Null) return null;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var transform = index.Resolve((string)token["ref"]);
                if (type == typeof(Transform)) return transform;
                if (type == typeof(GameObject)) return transform.gameObject;
                Type componentType = type == typeof(UnityEngine.Object) ? ComponentType((string)token["component"]) : type;
                return transform.GetComponent(componentType) ?? throw new InvalidOperationException("Missing saved component: " + type.Name);
            }
            if (type == typeof(Table)) return replay.ResolveTable((int)token["table"]);
            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                var result = (IDictionary)Activator.CreateInstance(type);
                var arguments = type.GetGenericArguments();
                foreach (var pair in (JArray)token)
                    result.Add(Read(pair[0], arguments[0], index, replay), Read(pair[1], arguments[1], index, replay));
                return result;
            }
            if (type.IsArray)
            {
                var items = (JArray)token;
                var result = Array.CreateInstance(type.GetElementType(), items.Count);
                for (int i = 0; i < items.Count; i++) result.SetValue(Read(items[i], type.GetElementType(), index, replay), i);
                return result;
            }
            if (type.IsEnum) return Enum.ToObject(type, (int)token);
            if (type.IsPrimitive || type == typeof(string)) return token.ToObject(type);
            return JsonUtility.FromJson((string)token["json"], type);
        }

        public static IEnumerable<Type> ComponentTypes
        {
            get
            {
                foreach (var type in Fields.Keys)
                    if (typeof(Component).IsAssignableFrom(type)) yield return type;
            }
        }
    }

    internal sealed class SaveObjectIndex
    {
        private readonly Dictionary<Transform, string> ids = new Dictionary<Transform, string>();
        private readonly Dictionary<string, Transform> objects = new Dictionary<string, Transform>();

        public SaveObjectIndex(Transform root)
        {
            RegisterTree(root, "scene");
        }

        private void RegisterTree(Transform item, string id)
        {
            Bind(id, item);
            for (int i = 0; i < item.childCount; i++) RegisterTree(item.GetChild(i), id + "/" + i);
        }

        public string Id(Transform item)
        {
            if (item == null) return null;
            if (!ids.TryGetValue(item, out var id))
            {
                id = Guid.NewGuid().ToString("N");
                Bind(id, item);
            }
            return id;
        }

        public void Bind(string id, Transform item)
        {
            ids[item] = id;
            objects[id] = item;
        }

        public bool TryResolve(string id, out Transform item) => objects.TryGetValue(id, out item) && item != null;

        public Transform Resolve(string id) => objects.TryGetValue(id, out var item) && item != null
            ? item : throw new InvalidOperationException("Missing saved object: " + id);
    }
}
