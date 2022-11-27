#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
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
            try
            {
                if (index is RuntimeRange range)
                {
                    int length = (range.To ?? Values.Count) - (range.From ?? 0);

                    return new RuntimeList(Values.GetRange(range.From ?? 0, length));
                }

                return Values[(int)index.As<RuntimeInteger>().Value];
            }
            catch (Exception)
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
        }

        set
        {
            Values[(int)index.As<RuntimeInteger>().Value] = value;
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

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
    {
        var otherList = other.As<RuntimeList>();
        return kind switch
        {
            OperationKind.Addition => new RuntimeList(Values.Concat(otherList.Values)),
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "List"),
        };
    }

    public override int GetHashCode()
        => Values.GetHashCode();

    public override string ToString()
        => $"[{string.Join(", ", Values.Select(x => x.ToDisplayString()))}]";
}