#region

using System;
using System.Collections;
using System.Collections.Generic;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;
using Elk.Std.DataTypes.Serialization;

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
                var length = (range.To ?? Value.Length) - (range.From ?? 0);
                if (range.From < 0 || range.From >= Value.Length || range.To < 0 || range.To > Value.Length)
                    throw new RuntimeItemNotFoundException($"{range.From}..{range.To}");

                return new RuntimeString(Value.Substring(range.From ?? 0, length));
            }

            var indexValue = (int)index.As<RuntimeInteger>().Value;
            if (indexValue < 0 || indexValue >= Value.Length)
                throw new RuntimeItemNotFoundException(indexValue.ToString());

            return new RuntimeString(Value[indexValue].ToString());
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

    public override bool Equals(object? obj)
        => obj is RuntimeObject runtimeObject &&
            Operation(OperationKind.EqualsEquals, runtimeObject) is RuntimeBoolean { IsTrue: true };

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeString)
                => this,
            _ when toType == typeof(RuntimeInteger) && int.TryParse(Value, out var number)
                => new RuntimeInteger(number),
            _ when toType == typeof(RuntimeFloat) && double.TryParse(Value, out var number)
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
            _ => throw InvalidOperation(kind),
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
            _ => throw InvalidOperation(kind),
        };
    }

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value;

    public override string ToDisplayString()
        => StringFormatting.ToDisplayString(Value);
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