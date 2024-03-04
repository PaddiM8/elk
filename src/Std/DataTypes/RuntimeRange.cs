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

[ElkType("Range")]
public class RuntimeRange(long? from, long? to, long increment = 1) : RuntimeObject, IEnumerable<RuntimeObject>
{
    public long? From { get; } = from;

    public long? To { get; } = to;

    public long Increment { get; set; } = increment;

    public IEnumerator<RuntimeObject> GetEnumerator()
        => new RuntimeRangeEnumerator(From, To, Increment);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeRange)
                => this,
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.True,
            _ when toType == typeof(RuntimeList)
                => new RuntimeList(this.ToList()),
            _ when toType == typeof(RuntimeTuple)
                => new RuntimeTuple(this.ToList()),
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeInteger>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
    {
        var otherRange = other.As<RuntimeRange>();
        return kind switch
        {
            OperationKind.EqualsEquals => RuntimeBoolean.From(From == otherRange.From && To == otherRange.To),
            OperationKind.NotEquals => RuntimeBoolean.From(From != otherRange.From || To != otherRange.To),
            _ => throw InvalidOperation(kind),
        };
    }

    public override int GetHashCode()
        => (To, From).GetHashCode();

    public override string ToString()
        => $"{From}..{To}";

    public bool Contains(long value)
        => value >= From && value < To;
}

class RuntimeRangeEnumerator : IEnumerator<RuntimeObject>
{
    public RuntimeObject Current
        => _reversed
            ?  new RuntimeInteger(_to!.Value - _pos)
            : new RuntimeInteger(_pos);

    object IEnumerator.Current
        => Current;

    private readonly long? _from;
    private readonly long? _to;
    private readonly long _increment;
    private long _pos;
    private readonly bool _reversed;

    public RuntimeRangeEnumerator(long? from, long? to, long increment)
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