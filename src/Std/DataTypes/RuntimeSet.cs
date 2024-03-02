#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Set")]
public class RuntimeSet : RuntimeObject, IEnumerable<RuntimeObject>
{
    public Dictionary<int, RuntimeObject> Entries { get; }

    public RuntimeSet(Dictionary<int, RuntimeObject> entries)
    {
        Entries = entries;
    }

    public int Count
        => Entries.Count;

    public IEnumerator<RuntimeObject> GetEnumerator()
        => Entries.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeSet)
                => this,
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Entries.Any()),
            _ when toType == typeof(RuntimeList)
                => new RuntimeList(Entries.Values.ToList()),
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeSet>(toType),
        };

    public override int GetHashCode()
        => Entries.GetHashCode();

    public override string ToString()
        => $"{{ {string.Join(", ", Entries.Values.Select(x => x.ToDisplayString())) } }}";
}