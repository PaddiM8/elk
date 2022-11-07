#region

using System;
using System.Reflection;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Type")]
public class RuntimeType : RuntimeObject
{
    public string Name
        => Type.GetCustomAttribute<ElkTypeAttribute>()!.Name;

    public Type Type { get; }

    public RuntimeType(Type type)
    {
        Type = type;
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeType)
                => this,
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            var type when type == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Type");

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Type");

    public override int GetHashCode()
        => Type.GetHashCode();

    public override string ToString()
        => Name;
}