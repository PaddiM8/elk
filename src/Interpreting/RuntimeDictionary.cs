using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeDictionary : IRuntimeValue, IEnumerable<IRuntimeValue>, IIndexable<IRuntimeValue>
{
    public Dictionary<int, (IRuntimeValue, IRuntimeValue)> Entries { get; }

    public RuntimeDictionary(Dictionary<int, (IRuntimeValue, IRuntimeValue)> entries)
    {
        Entries = entries;
    }

    public IRuntimeValue this[IRuntimeValue index]
    {
        get
        {
            if (Entries.TryGetValue(index.GetHashCode(), out (IRuntimeValue, IRuntimeValue) value))
                return value.Item2;

            throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
        }

        set
        {
            Entries[index.GetHashCode()] = (index, value);
        }
    }

    // TODO: Create a custom enumerator
    public IEnumerator<IRuntimeValue> GetEnumerator()
        => throw new NotImplementedException();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeDictionary)
                => this,
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Entries.Any()),
            var type when type == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public IRuntimeValue Operation(TokenKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Dictionary");

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Dictionary");

    public override int GetHashCode()
        => Entries.GetHashCode();

    public override string ToString()
    {
        var stringBuilder = new StringBuilder("{\n");
        foreach (var entry in Entries)
        {
            stringBuilder.Append('\t');
            stringBuilder.AppendLine($"{entry.Value.Item1}: {entry.Value.Item2},");
        }

        stringBuilder.Remove(stringBuilder.Length - 1, 1);
        stringBuilder.Append("\n}");

        return stringBuilder.ToString();
    }
}