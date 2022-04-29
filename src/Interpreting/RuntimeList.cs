using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Lexing;

namespace Elk.Interpreting;

class RuntimeList : IRuntimeValue, IEnumerable<IRuntimeValue>, IIndexable<IRuntimeValue>
{
    public List<IRuntimeValue> Values { get; }

    public RuntimeList(IEnumerable<IRuntimeValue> values)
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
                if (index is RuntimeRange range)
                {
                    int length = (range.To ?? Values.Count) - (range.From ?? 0);

                    return new RuntimeList(Values.GetRange(range.From ?? 0, length));
                }

                return Values[index.As<RuntimeInteger>().Value];
            }
            catch (Exception)
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
        }

        set
        {
            Values[index.As<RuntimeInteger>().Value] = value;
        }
    }

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeList)
                => this,
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Values.Any()),
            var type when type == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public IRuntimeValue Operation(TokenKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "List");

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        var otherList = other.As<RuntimeList>();
        return kind switch
        {
            TokenKind.Plus => new RuntimeList(Values.Concat(otherList.Values)),
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "List"),
        };
    }

    public override int GetHashCode()
        => Values.GetHashCode();

    public override string ToString()
        => $"[{string.Join(", ", Values.Select(x => x.ToString()))}]";
}