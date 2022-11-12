using System;
using System.Collections.Generic;
using System.Reflection;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.DocGen;

public class ValueInfo
{
    public string? TypeName { get; }

    public string? Description { get; }

    public bool HasStaticType { get; }

    public ValueInfo(string typeName, string description)
    {
        TypeName = typeName;
        Description = description;
        HasStaticType = false;
    }

    public ValueInfo(Type type, string description)
    {
        TypeName = type.GetCustomAttribute<ElkTypeAttribute>()?.Name;
        if (type == typeof(RuntimeObject))
            TypeName = "*";

        Description = description;
        HasStaticType = true;
    }
}

public record ParameterInfo(string Name, ValueInfo ValueInfo, bool IsOptional, bool IsVariadic);

public record ClosureInfo(int ParameterCount);

public record FunctionInfo(string Name, IEnumerable<ParameterInfo> Parameters, ValueInfo ReturnValue)
{
    public string? Example { get; init; }

    public string? Summary { get; init; }

    public List<string> Errors { get; init; } = new();

    public ClosureInfo? Closure { get; init; }
}