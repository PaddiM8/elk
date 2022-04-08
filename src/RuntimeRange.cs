using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeRange : IRuntimeValue
{
    public int? From { get; }

    public int? To { get; }

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