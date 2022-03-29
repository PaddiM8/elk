using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeNumber : IRuntimeValue
{
    public double Value { get; }

    public RuntimeNumber(double value)
    {
        Value = value;
    }

    public T As<T>()
        where T : IRuntimeValue
    {
        IRuntimeValue converted = typeof(T) switch
        {
            var type when type == typeof(RuntimeNumber)
                => this,
            var type when type == typeof(RuntimeString)
                => new RuntimeString(Value.ToString()),
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Value != 0),
            _
                => throw new RuntimeCastException<RuntimeNumber, T>(),
        };

        return (T)converted;
    }

    public IRuntimeValue Operation(TokenKind kind)
        => kind switch
        {
            TokenKind.Minus => new RuntimeNumber(-Value),
            TokenKind.Exclamation => RuntimeBoolean.From(Value == 0),
            _ => throw new NotImplementedException(),
        };

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        var otherNumber = other.As<RuntimeNumber>();
        return kind switch
        {
            TokenKind.Plus => new RuntimeNumber(Value + otherNumber.Value),
            TokenKind.Minus => new RuntimeNumber(Value - otherNumber.Value),
            TokenKind.Star => new RuntimeNumber(Value * otherNumber.Value),
            TokenKind.Slash => new RuntimeNumber(Value / otherNumber.Value),
            TokenKind.Greater => RuntimeBoolean.From(Value > otherNumber.Value),
            TokenKind.GreaterEquals => RuntimeBoolean.From(Value >= otherNumber.Value),
            TokenKind.Less => RuntimeBoolean.From(Value < otherNumber.Value),
            TokenKind.LessEquals => RuntimeBoolean.From(Value <= otherNumber.Value),
            TokenKind.EqualsEquals => RuntimeBoolean.From(Value == otherNumber.Value),
            TokenKind.NotEquals => RuntimeBoolean.From(Value != otherNumber.Value),
            _ => throw new NotImplementedException(),
        };
    }

    public override string ToString()
        => Value.ToString();
}