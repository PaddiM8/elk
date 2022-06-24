#region

using System;
using System.Collections;
using System.Collections.Generic;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("String")]
public class RuntimeString : IRuntimeValue, IEnumerable<IRuntimeValue>, IIndexable<IRuntimeValue>
{
    public string Value { get; }

    public RuntimeString(string value)
    {
        Value = value;
    }

    public IRuntimeValue this[IRuntimeValue index]
    {
        get
        {
            if (index is RuntimeRange range)
            {
                int length = (range.To ?? Value.Length) - (range.From ?? 0);

                return new RuntimeString(Value.Substring(range.From ?? 0, length));
            }

            return new RuntimeString(Value[(int)index.As<RuntimeInteger>().Value].ToString());
        }

        set
        {
            throw new RuntimeException("Cannot modify immutable value");
        }
    }

    public IEnumerator<IRuntimeValue> GetEnumerator()
        => new RuntimeStringEnumerator(Value);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeString)
                => this,
            var type when type == typeof(RuntimeInteger) && int.TryParse(Value, out int number)
                => new RuntimeInteger(number),
            var type when type == typeof(RuntimeFloat) && double.TryParse(Value, out double number)
                => new RuntimeFloat(number),
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Value.Length != 0),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public IRuntimeValue Operation(OperationKind kind)
        => kind switch
        {
            OperationKind.Subtraction => ((IRuntimeValue)this).As<RuntimeFloat>().Operation(kind),
            OperationKind.Not => RuntimeBoolean.From(Value.Length == 0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
    {
        if (kind is OperationKind.Subtraction or OperationKind.Multiplication or OperationKind.Division)
        {
            return ((IRuntimeValue)this).As<RuntimeFloat>().Operation(kind, other);
        }

        var otherString = other.As<RuntimeString>();
        return kind switch
        {
            OperationKind.Addition => new RuntimeString(Value + otherString.Value),
            OperationKind.Greater => RuntimeBoolean.From(string.CompareOrdinal(Value, otherString.Value) > 0),
            OperationKind.GreaterEquals => RuntimeBoolean.From(string.CompareOrdinal(Value, otherString.Value) >= 0),
            OperationKind.Less => RuntimeBoolean.From(string.CompareOrdinal(Value, otherString.Value) < 0),
            OperationKind.LessEquals => RuntimeBoolean.From(string.CompareOrdinal(Value, otherString.Value) <= 0),
            OperationKind.EqualsEquals => RuntimeBoolean.From(Value == otherString.Value),
            OperationKind.NotEquals => RuntimeBoolean.From(Value != otherString.Value),
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "String"),
        };
    }

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => $"{Value}";
}

class RuntimeStringEnumerator : IEnumerator<IRuntimeValue>
{
    public IRuntimeValue Current
        => new RuntimeString(_currentChar.ToString());
    
    object IEnumerator.Current
        => Current;

    private readonly string _value;
    private char _currentChar;
    private int _index;

    public RuntimeStringEnumerator(string value)
    {
        _value = value;
        Reset();
    }

    public bool MoveNext()
    {
        _index++;
        if (_index >= _value.Length)
            return false;

        _currentChar = _value[_index];

        return true;
    }

    public void Reset()
    {
        _index = -1;
    }

    void IDisposable.Dispose()
    {
    }
}