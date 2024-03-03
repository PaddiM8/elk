using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;

namespace Elk.Std.DataTypes;

[ElkType("Generator")]
public class RuntimeGenerator(IEnumerable<RuntimeObject> values) : RuntimeObject, IEnumerable<RuntimeObject>
{
    public IEnumerable<RuntimeObject> Values { get; } = values;

    public IEnumerator<RuntimeObject> GetEnumerator()
        => Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeGenerator)
                => this,
            _ when toType == typeof(RuntimeList)
                => new RuntimeList(Values.ToList()),
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            _
                => throw new RuntimeCastException<RuntimeGenerator>(toType),
        };

    public override int GetHashCode()
        => Values.GetHashCode();

    public override string ToString()
        => new RuntimeList(Values.ToList()).ToString();
}