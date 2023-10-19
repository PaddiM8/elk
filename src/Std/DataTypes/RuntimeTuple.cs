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
public class RuntimeTuple : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public List<RuntimeObject> Values { get; }

    public RuntimeTuple(IEnumerable<RuntimeObject> values)
    {
        Values = values.ToList();
    }

    public IEnumerator<RuntimeObject> GetEnumerator()
        => Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            try
            {
                return Values[(int)index.As<RuntimeInteger>().Value];
            }
            catch (Exception)
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
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

    public override int CompareTo(RuntimeObject? other)
    {
        if (other is not RuntimeTuple otherTuple)
            return -1;

        foreach (var (item, otherItem) in Values.Zip(otherTuple))
        {
            var comparison = item.CompareTo(otherItem);
            if (comparison == 0)
                continue;

            return comparison;
        }

        return 0;
    }

    public override int GetHashCode()
        => Values.GetHashCode();

    public override string ToString()
        => $"({string.Join(", ", Values.Select(x => x.ToDisplayString()))})";
}