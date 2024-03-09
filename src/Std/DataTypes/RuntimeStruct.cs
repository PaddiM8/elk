#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Struct")]
public class RuntimeStruct : RuntimeObject, IIndexable<RuntimeObject>
{
    internal StructSymbol Symbol { get; }

    public Dictionary<string, RuntimeObject> Values { get; }

    public int Count
        => Values.Count;

    internal RuntimeStruct(StructSymbol symbol, Dictionary<string, RuntimeObject> values)
    {
        Symbol = symbol;
        Values = values;
    }

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (Values.TryGetValue(index.As<RuntimeString>().Value, out var value))
                return value;

            throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
        }

        set
        {
            try
            {
                Values[index.As<RuntimeString>().Value] = value;
            }
            catch
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
        }
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeStruct)
                => this,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _ when toType == typeof(RuntimeDictionary)
                => ToDictionary(),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            _
                => throw new RuntimeCastException<RuntimeStruct>(toType),
        };

    private RuntimeDictionary ToDictionary()
    {
        var dict = new Dictionary<int, (RuntimeObject, RuntimeObject)>();
        foreach (var (key, value) in Values)
        {
            var keyValue = new RuntimeString(key);
            dict[keyValue.GetHashCode()] = (keyValue, value);
        }

        return new(dict);
    }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder("{\n");
        foreach (var (key, value) in Values)
            stringBuilder.AppendLine($"    {key}: {value.ToDisplayString()},");

        stringBuilder.Append('}');

        return stringBuilder.ToString();
    }
}