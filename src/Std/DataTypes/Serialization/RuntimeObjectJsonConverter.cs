using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Elk.Std.DataTypes.Serialization;

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
            RuntimeDictionary dictionary => BuildDictionary(dictionary),
            RuntimeStruct @struct => BuildStruct(@struct),
            _ => BuildValue(value),
        };

    private JArray BuildList(RuntimeList list)
        => new(list.Values.Select(Build));

    private JObject BuildDictionary(RuntimeDictionary dictionary)
    {
        var result = new JObject();
        foreach (var (key, value) in dictionary.Entries.Values)
        {
            var keyString = key.As<RuntimeString>().Value;
            result[keyString] = Build(value);
        }

        return result;
    }

    private JObject BuildStruct(RuntimeStruct structValue)
    {
        var result = new JObject();
        foreach (var (key, value) in structValue.Values)
            result[key] = Build(value);

        return result;
    }

    private JValue BuildValue(RuntimeObject value)
        => new(value.ToString() ?? "");

    public override RuntimeObject ReadJson(
        JsonReader reader,
        Type objectType,
        RuntimeObject? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        return Get(JObject.Load(reader));
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
        var dictionary = new Dictionary<int, (RuntimeObject, RuntimeObject)>();
        foreach (var (key, valueData) in data)
        {
            var value = valueData == null
                ? RuntimeNil.Value
                : Get(valueData);
            dictionary[key.GetHashCode()] = (new RuntimeString(key), value);
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