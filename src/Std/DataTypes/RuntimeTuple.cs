#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Tuple")]
public class RuntimeTuple(IEnumerable<RuntimeObject> values)
    : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public List<RuntimeObject> Values { get; } = values.ToList();

    public IEnumerator<RuntimeObject> GetEnumerator()
        => Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public override bool Equals(object? obj)
        => obj is RuntimeObject runtimeObject &&
            Operation(OperationKind.EqualsEquals, runtimeObject) is RuntimeBoolean { IsTrue: true };

    public override int CompareTo(RuntimeObject? other)
        => other is IEnumerable<RuntimeObject> otherEnumerable
            ? this.OrdinalCompare(otherEnumerable)
            : throw new RuntimeInvalidOperationException("comparison", GetType());

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (index is RuntimeRange range)
                return new RuntimeTuple(Values.GetRange(range));

            return Values.GetAt(index.As<RuntimeInteger>());
        }

        set
        {
            throw new RuntimeException("Cannot modify immutable value");
        }
    }

    public int Count
        => Values.Count;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeTuple)
                => this,
            _ when toType == typeof(RuntimeList)
                => new RuntimeList(Values),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Values.Any()),
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
    {
        if (other is not IEnumerable<RuntimeObject> otherTuple)
            throw InvalidOperation(kind);

        return kind switch
        {
            OperationKind.EqualsEquals => RuntimeBoolean.From(this.OrdinalCompare(otherTuple) == 0),
            OperationKind.NotEquals => RuntimeBoolean.From(this.OrdinalCompare(otherTuple) != 0),
            OperationKind.Greater => RuntimeBoolean.From(this.OrdinalCompare(otherTuple) > 0),
            OperationKind.GreaterEquals => RuntimeBoolean.From(this.OrdinalCompare(otherTuple) >= 0),
            OperationKind.Less => RuntimeBoolean.From(this.OrdinalCompare(otherTuple) < 0),
            OperationKind.LessEquals => RuntimeBoolean.From(this.OrdinalCompare(otherTuple) <= 0),
            _ => throw InvalidOperation(kind),
        };
    }

    public override int GetHashCode()
        => Values.Aggregate(typeof(RuntimeObject).GetHashCode(), HashCode.Combine);

    public override string ToString()
        => $"({string.Join(", ", Values.Select(x => x.ToDisplayString()))})";
}