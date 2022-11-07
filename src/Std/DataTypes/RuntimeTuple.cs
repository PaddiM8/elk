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
            var type when type == typeof(RuntimeTuple)
                => this,
            var type when type == typeof(RuntimeList)
                => new RuntimeList(Values),
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Values.Any()),
            var type when type == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Tuple");

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Tuple");

    public override int GetHashCode()
        => Values.GetHashCode();

    public override string ToString()
        => $"({string.Join(", ", Values.Select(x => x.ToDisplayString()))})";
}