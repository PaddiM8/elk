using System;
using System.Collections;
using System.Collections.Generic;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeRange : IRuntimeValue, IEnumerable<IRuntimeValue>
{
    public int? From { get; }

    public int? To { get; }

    public IEnumerator<IRuntimeValue> GetEnumerator()
        => new RuntimeRangeEnumerator(From, To);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimeRange(int? from, int? to)
    {
        From = from;
        To = to;
    }

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeRange)
                => this,
            var type when type == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            _
                => throw new RuntimeCastException<RuntimeInteger>(toType),
        };

    public IRuntimeValue Operation(TokenKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Range");

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        var otherRange = other.As<RuntimeRange>();
        return kind switch
        {
            TokenKind.EqualsEquals => RuntimeBoolean.From(From == otherRange.From && To == otherRange.To),
            TokenKind.NotEquals => RuntimeBoolean.From(From != otherRange.From || To != otherRange.To),
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "Range"),
        };
    }

    public override string ToString()
        => $"{From}..{To}";
}

class RuntimeRangeEnumerator : IEnumerator<IRuntimeValue>
{
    public IRuntimeValue Current
        => new RuntimeInteger(_pos);

    object IEnumerator.Current
        => Current;

    private readonly int? _from;
    private readonly int? _to;
    private int _pos;

    public RuntimeRangeEnumerator(int? from, int? to)
    {
        _to = to;
        _from = from;
        Reset();
    }

    public bool MoveNext()
    {
        if (_to != null && _pos >= _to - 1)
            return false;

        _pos++;

        return true;
    }

    public void Reset()
    {
        _pos = (_from ?? 0) - 1;
    }

    void IDisposable.Dispose()
    {
    }
}