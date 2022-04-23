using System;
using System.Collections;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using Elk.Lexing;

namespace Elk.Interpreting;

class RuntimeTuple : IRuntimeValue, IIndexable<IRuntimeValue>
{
    public ImmutableArray<IRuntimeValue> Values { get; }

    public RuntimeTuple(IEnumerable<IRuntimeValue> values)
    {
        Values = values.ToImmutableArray();
    }

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

    public IRuntimeValue Operation(TokenKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Tuple");

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Tuple");

    public override int GetHashCode()
        => Values.GetHashCode();

    public override string ToString()
        => $"({string.Join(", ", Values.Select(x => x.ToString()))})";
}