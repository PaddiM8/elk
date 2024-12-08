using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Std.DataTypes;
using Elk.Std.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Elk.Std.Serialization;

public class RuntimeObjectJsonConverter : JsonConverter<RuntimeObject>
{
    public override void WriteJson(
        JsonWriter writer,
        RuntimeObject? value,
        JsonSerializer serializer)
    {
        if (value != null)
            serializer.Serialize(writer, Build(value));
    }

    private JToken Build(RuntimeObject value)
        => value switch
        {
            RuntimeList list => BuildList(list),
            RuntimeGenerator generator => BuildList(generator),
            RuntimeTuple tuple => BuildTuple(tuple),
            RuntimeDictionary dictionary => BuildDictionary(dictionary),
            RuntimeSet set => BuildSet(set),
            RuntimeStruct @struct => BuildStruct(@struct),
            RuntimeTable table => BuildTable(table),
            _ => BuildValue(value),
        };

    private JArray BuildList(IEnumerable<RuntimeObject> list)
        => new(list.Select(Build));

    private JArray BuildTuple(RuntimeTuple list)
        => new(list.Values.Select(Build));

    private JObject BuildDictionary(RuntimeDictionary dictionary)
    {
        var result = new JObject();
        foreach (var (key, value) in dictionary.Entries)
        {
            var keyString = key.As<RuntimeString>().Value;
            result[keyString] = Build(value);
        }

        return result;
    }

    private JArray BuildSet(RuntimeSet set)
        => new(set.Entries.Select(Build));

    private JObject BuildStruct(RuntimeStruct structValue)
    {
        var result = new JObject();
        foreach (var (key, value) in structValue.Values)
            result[key] = Build(value);

        return result;
    }

    private JArray BuildTable(RuntimeTable table)
        => new(
            table.Rows
                .Select(x => new JArray(x.Select(Build)))
                .Prepend(new JArray(table.Header.Select(x => new JValue(x))))
        );

    private JValue BuildValue(RuntimeObject value)
    {
        return value switch
        {
            RuntimeBoolean boolean => new JValue(boolean.IsTrue),
            RuntimeFloat floatValue => new JValue(floatValue.Value),
            RuntimeInteger integerValue => new JValue(integerValue.Value),
            RuntimeNil => JValue.CreateNull(),
            _ => new JValue(value?.ToString() ?? ""),
        };
    }

    public override RuntimeObject ReadJson(
        JsonReader reader,
        Type objectType,
        RuntimeObject? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        return Get(JToken.Load(reader));
    }

    private RuntimeObject Get(JToken data)
    {
        return data switch
        {
            JArray array => GetList(array),
            JObject obj => GetDictionary(obj),
            JValue value => GetValue(value),
            _ => new RuntimeString(data.ToString()),
        };
    }

    private RuntimeList GetList(JArray data)
        => new(data.Select(Get).ToList());

    private RuntimeDictionary GetDictionary(JObject data)
    {
        var dictionary = new Dictionary<RuntimeObject, RuntimeObject>();
        foreach (var (key, valueData) in data)
        {
            var value = valueData == null
                ? RuntimeNil.Value
                : Get(valueData);
            dictionary[new RuntimeString(key)] = value;
        }

        return new RuntimeDictionary(dictionary);
    }

    private RuntimeObject GetValue(JValue data)
        => data.Type switch
        {
            JTokenType.Boolean => RuntimeBoolean.From(data.ToObject<bool>()),
            JTokenType.Float => new RuntimeFloat(data.ToObject<float>()),
            JTokenType.Integer => new RuntimeInteger(data.ToObject<int>()),
            JTokenType.Null => RuntimeNil.Value,
            JTokenType.Undefined => RuntimeNil.Value,
            JTokenType.String => new RuntimeString(data.ToObject<string>()!),
            _ => new RuntimeString(data.ToString()),
        };
}