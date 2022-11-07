#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Dictionary")]
public class RuntimeDictionary : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public Dictionary<int, (RuntimeObject, RuntimeObject)> Entries { get; }

    public RuntimeDictionary(Dictionary<int, (RuntimeObject, RuntimeObject)> entries)
    {
        Entries = entries;
    }

    public RuntimeObject this[RuntimeObject index]
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

    public int Count
        => Entries.Count;

    public IEnumerator<RuntimeObject> GetEnumerator()
        => new RuntimeDictionaryEnumerator(Entries.Values);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public override RuntimeObject As(Type toType)
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

    public override RuntimeObject Operation(OperationKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Dictionary");

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Dictionary");

    public override int GetHashCode()
        => Entries.GetHashCode();

    public override string ToString()
    {
        var stringBuilder = new StringBuilder("{\n");
        foreach (var entry in Entries)
        {
            stringBuilder.Append('\t');
            string key = entry.Value.Item1.ToDisplayString();
            string value = entry.Value.Item2.ToDisplayString();
            stringBuilder.AppendLine($"{key}: {value},");
        }

        stringBuilder.Remove(stringBuilder.Length - 1, 1);
        stringBuilder.Append("\n}");

        return stringBuilder.ToString();
    }
}

class RuntimeDictionaryEnumerator : IEnumerator<RuntimeObject>
{
    public RuntimeObject Current { get; private set; }

    object IEnumerator.Current
        => Current;

    private readonly IEnumerable<(RuntimeObject, RuntimeObject)> _keyValueSets;
    private IEnumerator<(RuntimeObject, RuntimeObject)> _enumerator;

    public RuntimeDictionaryEnumerator(IEnumerable<(RuntimeObject, RuntimeObject)> keyValueSets)
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