using System;
using System.Collections.Generic;
using System.Linq;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeList : IRuntimeValue
{
    public List<IRuntimeValue> Values { get; }

    public RuntimeList(IEnumerable<IRuntimeValue> values)
    {
        Values = values.ToList();
    }

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeList)
                => this,
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Values.Count != 0),
            var type when type == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public IRuntimeValue Operation(TokenKind kind)
        => kind switch
        {
            TokenKind.Minus => throw new RuntimeInvalidOperationException("-", "List"),
            TokenKind.Exclamation => throw new RuntimeInvalidOperationException("!", "List"),
            _ => throw new NotImplementedException(),
        };

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        var otherList = other.As<RuntimeList>();
        return kind switch
        {
            TokenKind.Plus => new RuntimeList(Values.Concat(otherList.Values)),
            _ => throw new RuntimeInvalidOperationException(kind.ToString(), "List"),
        };
    }

    public override string ToString()
        => $"[{string.Join(", ", Values.Select(x => x.As<RuntimeString>().Value))}]";
}