using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;

namespace Elk.Interpreting;

public class RuntimeTuple : IRuntimeValue, IEnumerable<IRuntimeValue>, IIndexable<IRuntimeValue>
{
    public List<IRuntimeValue> Values { get; }

    public RuntimeTuple(IEnumerable<IRuntimeValue> values)
    {
        Values = values.ToList();
    }

    public IEnumerator<IRuntimeValue> GetEnumerator()
        => Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public IRuntimeValue this[IRuntimeValue index]
    {
        get
        {
            try
            {
                return Values[index.As<RuntimeInteger>().Value];
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

    public IRuntimeValue As(Type toType)
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

    public IRuntimeValue Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Tuple");

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Tuple");

    public override int GetHashCode()
        => Values.GetHashCode();

    public override string ToString()
        => $"({string.Join(", ", Values.Select(x => x.ToString()))})";
}