using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Attributes;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;

namespace Elk.Interpreting;

[ElkType("Dictionary")]
public class RuntimeDictionary : IRuntimeValue, IEnumerable<IRuntimeValue>, IIndexable<IRuntimeValue>
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
            if (Entries.TryGetValue(index.GetHashCode(), out var value))
                return value.Item2;

            throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
        }

        set
        {
            Entries[index.GetHashCode()] = (index, value);
        }
    }

    public IEnumerator<IRuntimeValue> GetEnumerator()
        => new RuntimeDictionaryEnumerator(Entries.Values);

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

    public IRuntimeValue Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Dictionary");

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other)
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

class RuntimeDictionaryEnumerator : IEnumerator<IRuntimeValue>
{
    public IRuntimeValue Current { get; private set; }

    object IEnumerator.Current
        => Current;

    private readonly IEnumerable<(IRuntimeValue, IRuntimeValue)> _keyValueSets;
    private IEnumerator<(IRuntimeValue, IRuntimeValue)> _enumerator;

    public RuntimeDictionaryEnumerator(IEnumerable<(IRuntimeValue, IRuntimeValue)> keyValueSets)
    {
        _keyValueSets = keyValueSets;
        _enumerator = _keyValueSets.GetEnumerator();
        Current = RuntimeNil.Value;
    }

    public bool MoveNext()
    {
        bool success = _enumerator.MoveNext();
        if (success)
        {
            Current = new RuntimeTuple(new[]
            {
                _enumerator.Current.Item1,
                _enumerator.Current.Item2,
            });
        }

        return success;
    }

    public void Reset()
    {
        _enumerator = _keyValueSets.GetEnumerator();
        Current = RuntimeNil.Value;
    }

    void IDisposable.Dispose()
    {
    }
}