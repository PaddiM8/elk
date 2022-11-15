#region

using System;
using System.Collections.Generic;
using System.Text;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Struct")]
public class RuntimeStruct : RuntimeObject
{
    public Dictionary<string, RuntimeObject> Values { get; }

    internal RuntimeStruct(Dictionary<string, RuntimeObject> values)
    {
        Values = values;
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeStruct)
                => this,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            _
                => throw new RuntimeCastException<RuntimeStruct>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Struct");

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Struct");

    public override string ToString()
    {
        var stringBuilder = new StringBuilder("{\n");
        foreach (var (key, value) in Values)
            stringBuilder.AppendLine($"\t{key}: {value.ToDisplayString()},");

        stringBuilder.Append("}");

        return stringBuilder.ToString();
    }
}