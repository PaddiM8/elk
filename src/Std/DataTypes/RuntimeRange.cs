#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Range")]
public class RuntimeRange(long? from, long? to, bool isInclusive = false, long increment = 1) : RuntimeObject, IEnumerable<RuntimeObject>
{
    public long? From { get; } = from;

    public long? To { get; } = to;

    public bool IsInclusive { get; } = isInclusive;

    public long Increment { get; set; } = increment;

    public IEnumerator<RuntimeObject> GetEnumerator()
        => new RuntimeRangeEnumerator(From, To, IsInclusive, Increment);

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
            OperationKind.EqualsEquals => RuntimeBoolean.From(
                From == otherRange.From &&
                    To == otherRange.To &&
                    IsInclusive == otherRange.IsInclusive &&
                    Increment == otherRange.Increment
            ),
            OperationKind.NotEquals => RuntimeBoolean.From(
                From != otherRange.From ||
                    To != otherRange.To ||
                    IsInclusive != otherRange.IsInclusive ||
                    Increment != otherRange.Increment
            ),
            _ => throw InvalidOperation(kind),
        };
    }

    // ReSharper disable once NonReadonlyMemberInGetHashCode
    public override int GetHashCode()
        => (To, From, IsInclusive, Increment).GetHashCode();

    public override string ToString()
        => $"{From}..{(IsInclusive ? "=" : "")}{To}";

    public bool Contains(long value)
    {
        var actualTo = IsInclusive ? To + Increment : To;

        return value >= From && value < actualTo;
    }
}

class RuntimeRangeEnumerator : IEnumerator<RuntimeObject>
{
    public RuntimeObject Current
        => _reversed
            ?  new RuntimeInteger((_from ?? 0) + _to!.Value - _increment - _pos)
            : new RuntimeInteger(_pos);

    object IEnumerator.Current
        => Current;

    private readonly long? _from;
    private readonly long? _to;
    private readonly long _increment;
    private long _pos;
    private readonly bool _reversed;

    public RuntimeRangeEnumerator(long? from, long? to, bool isInclusive, long increment)
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

        if (isInclusive)
            _to += increment;

        _increment = increment;

        Reset();
    }

    public bool MoveNext()
    {
        if (_to != null && _pos >= _to - _increment)
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