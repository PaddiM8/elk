using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;

namespace Elk.Interpreting;

public class RuntimeRange : IRuntimeValue, IEnumerable<IRuntimeValue>
{
    public int? From { get; }

    public int? To { get; }

    public int Increment { get; set; }

    public IEnumerator<IRuntimeValue> GetEnumerator()
        => new RuntimeRangeEnumerator(From, To, Increment);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimeRange(int? from, int? to, int increment = 1)
    {
        From = from;
        To = to;
        Increment = increment;
    }

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeRange)
                => this,
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            var type when type == typeof(RuntimeList)
                => new RuntimeList(AsEnumerable()),
            var type when type == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeInteger>(toType),
        };

    public IRuntimeValue Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Range");

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
    {
        var otherRange = other.As<RuntimeRange>();
        return kind switch
        {
            OperationKind.EqualsEquals => RuntimeBoolean.From(From == otherRange.From && To == otherRange.To),
            OperationKind.NotEquals => RuntimeBoolean.From(From != otherRange.From || To != otherRange.To),
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "Range"),
        };
    }

    public override int GetHashCode()
        => (To, From).GetHashCode();

    public override string ToString()
        => $"{From}..{To}";

    private IEnumerable<RuntimeInteger> AsEnumerable()
    {
        int from = From ?? 0;
        int count = (To ?? from) - from;

        return Enumerable.Range(from, count).Select(x => new RuntimeInteger(x));
    }
}

class RuntimeRangeEnumerator : IEnumerator<IRuntimeValue>
{
    public IRuntimeValue Current
        => _reversed
            ?  new RuntimeInteger(_to!.Value - _pos)
            : new RuntimeInteger(_pos);

    object IEnumerator.Current
        => Current;

    private readonly int? _from;
    private readonly int? _to;
    private readonly int _increment;
    private int _pos;
    private readonly bool _reversed;

    public RuntimeRangeEnumerator(int? from, int? to, int increment)
    {
        if (to == null || to > from)
        {
            _reversed = false;
            _to = to;
            _from = from;
        }
        else
        {
            _reversed = true;
            _to = from;
            _from = to;
        }

        _increment = increment;

        Reset();
    }

    public bool MoveNext()
    {
        if (_to != null && _pos >= _to - 1)
            return false;

        _pos += _increment;

        return true;
    }

    public void Reset()
    {
        _pos = (_from ?? 0) - _increment;
    }

    void IDisposable.Dispose()
    {
    }
}