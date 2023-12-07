#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
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

    public override int GetHashCode()
        => Values.GetHashCode();

    public override string ToString()
        => $"({string.Join(", ", Values.Select(x => x.ToDisplayString()))})";
}