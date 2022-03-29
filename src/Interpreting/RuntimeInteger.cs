using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeInteger : IRuntimeValue
{
    public int Value { get; }

    public RuntimeInteger(int value)
    {
        Value = value;
    }

    public T As<T>()
        where T : IRuntimeValue
    {
        IRuntimeValue converted = typeof(T) switch
        {
            var type when type == typeof(RuntimeInteger)
                => this,
            var type when type == typeof(RuntimeFloat)
                => new RuntimeFloat(Value),
            var type when type == typeof(RuntimeString)
                => new RuntimeString(Value.ToString()),
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Value != 0),
            _
                => throw new RuntimeCastException<RuntimeInteger, T>(),
        };

        return (T)converted;
    }

    public IRuntimeValue Operation(TokenKind kind)
        => kind switch
        {
            TokenKind.Minus => new RuntimeInteger(-Value),
            TokenKind.Exclamation => RuntimeBoolean.From(Value == 0),
            _ => throw new NotImplementedException(),
        };

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        if (other is RuntimeFloat)
        {
            return As<RuntimeFloat>().Operation(kind, other);
        }

        var otherNumber = other.As<RuntimeInteger>();
        return kind switch
        {
            TokenKind.Plus => new RuntimeInteger(Value + otherNumber.Value),
            TokenKind.Minus => new RuntimeInteger(Value - otherNumber.Value),
            TokenKind.Star => new RuntimeInteger(Value * otherNumber.Value),
            TokenKind.Slash => new RuntimeFloat((double)Value / otherNumber.Value),
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