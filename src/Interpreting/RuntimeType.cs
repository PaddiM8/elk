using System;
using System.Reflection;
using Elk.Attributes;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;

namespace Elk.Interpreting;

[ElkType("Type")]
public class RuntimeType : IRuntimeValue
{
    public string Name
        => Type.GetCustomAttribute<ElkTypeAttribute>()!.Name;

    public Type Type { get; }

    public RuntimeType(Type type)
    {
        Type = type;
    }

    public IRuntimeValue As(Type toType)
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

    public IRuntimeValue Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Type");

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Type");

    public override int GetHashCode()
        => Type.GetHashCode();

    public override string ToString()
        => Name;
}