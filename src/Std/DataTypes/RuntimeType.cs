#region

using System;
using System.Reflection;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Type")]
public class RuntimeType : RuntimeObject
{
    public string Name
        => Type?.GetCustomAttribute<ElkTypeAttribute>()!.Name ??
               StructSymbol!.Expr.Identifier.Value;

    internal Type? Type { get; }

   internal StructSymbol? StructSymbol { get; }

    internal RuntimeType(Type type)
    {
        Type = type;
    }

    internal RuntimeType(StructSymbol structSymbol)
    {
        StructSymbol = structSymbol;
    }

    public bool IsAssignableTo(RuntimeObject value)
        => Type?.IsInstanceOfType(value) is true ||
               value is RuntimeStruct valueStruct && valueStruct.Symbol == StructSymbol;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeType)
                => this,
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Type");

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Type");

    public override int GetHashCode()
        => Type?.GetHashCode() ?? StructSymbol!.GetHashCode();

    public override string ToString()
        => Name;
}