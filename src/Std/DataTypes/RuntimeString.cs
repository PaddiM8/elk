#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("String")]
public class RuntimeString : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public string Value { get; }

    public RuntimeString(string value)
    {
        Value = value;
    }

    public RuntimeObject this[RuntimeObject index]
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

    public int Count
        => Value.Length;

    public IEnumerator<RuntimeObject> GetEnumerator()
        => new RuntimeStringEnumerator(Value);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeString)
                => this,
            _ when toType == typeof(RuntimeInteger) && int.TryParse(Value, out int number)
                => new RuntimeInteger(number),
            _ when toType == typeof(RuntimeFloat) && double.TryParse(Value, out double number)
                => new RuntimeFloat(number),
            _ when toType == typeof(RuntimeRegex)
                => new RuntimeRegex(new System.Text.RegularExpressions.Regex(Value)),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Value.Length != 0),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => kind switch
        {
            OperationKind.Subtraction => As<RuntimeFloat>().Operation(kind),
            OperationKind.Not => RuntimeBoolean.From(Value.Length == 0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
    {
        if (kind is OperationKind.Subtraction or OperationKind.Multiplication or OperationKind.Division)
        {
            return As<RuntimeFloat>().Operation(kind, other);
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
        => Value;

    public override string ToDisplayString()
        => $"\"{Value}\"";
}

class RuntimeStringEnumerator : IEnumerator<RuntimeObject>
{
    public RuntimeObject Current
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