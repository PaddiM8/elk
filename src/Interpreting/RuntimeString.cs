using System;
using System.Collections;
using System.Collections.Generic;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeString : IRuntimeValue, IEnumerable<IRuntimeValue>, IIndexable<IRuntimeValue>
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
            return new RuntimeString(Value[index.As<RuntimeInteger>().Value].ToString());
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

    public IRuntimeValue Operation(TokenKind kind)
        => kind switch
        {
            TokenKind.Minus => ((IRuntimeValue)this).As<RuntimeFloat>().Operation(kind),
            TokenKind.Exclamation => RuntimeBoolean.From(Value.Length == 0),
            _ => throw new NotImplementedException(),
        };

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        if (kind is TokenKind.Minus or TokenKind.Star or TokenKind.Slash)
        {
            return ((IRuntimeValue)this).As<RuntimeFloat>().Operation(kind, other);
        }

        var otherString = other.As<RuntimeString>();
        return kind switch
        {
            TokenKind.Plus => new RuntimeString(Value + otherString.Value),
            TokenKind.Greater => RuntimeBoolean.From(string.Compare(Value, otherString.Value) > 0),
            TokenKind.GreaterEquals => RuntimeBoolean.From(string.Compare(Value, otherString.Value) >= 0),
            TokenKind.Less => RuntimeBoolean.From(string.Compare(Value, otherString.Value) < 0),
            TokenKind.LessEquals => RuntimeBoolean.From(string.Compare(Value, otherString.Value) <= 0),
            TokenKind.EqualsEquals => RuntimeBoolean.From(Value == otherString.Value),
            TokenKind.NotEquals => RuntimeBoolean.From(Value != otherString.Value),
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "String"),
        };
    }

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => $"\"{Value}\"";
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