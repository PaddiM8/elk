#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("List")]
public class RuntimeList : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public List<RuntimeObject> Values { get; }

    public RuntimeList(IEnumerable<RuntimeObject> values)
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
            if (index is RuntimeRange range)
            {
                int length = (range.To ?? Values.Count) - (range.From ?? 0);

                return new RuntimeList(Values.GetRange(range.From ?? 0, length));
            }

            return Values.ElementAtOrDefault((int)index.As<RuntimeInteger>().Value) ??
                   throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
        }

        set
        {
            try
            {
                Values[(int)index.As<RuntimeInteger>().Value] = value;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
        }
    }

    public int Count
        => Values.Count;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeList)
                => this,
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
        => $"[{string.Join(", ", Values.Select(x => x.ToDisplayString()))}]";
}