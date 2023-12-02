#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes.Serialization;
using Newtonsoft.Json;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Dictionary")]
public class RuntimeDictionary(Dictionary<int, (RuntimeObject, RuntimeObject)> entries)
    : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public Dictionary<int, (RuntimeObject, RuntimeObject)> Entries { get; } = entries;

    private static readonly RuntimeObjectJsonConverter _jsonConverter = new();

    public RuntimeDictionary()
        : this(new Dictionary<int, (RuntimeObject, RuntimeObject)>())
    {
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

    public RuntimeObject this[string index]
    {
        get
        {
            if (Entries.TryGetValue(index.GetHashCode(), out var value))
                return value.Item2;

            throw new RuntimeItemNotFoundException(index);
        }

        set
        {
            var runtimeIndex = new RuntimeString(index);
            Entries[runtimeIndex.GetHashCode()] = (runtimeIndex, value);
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
            _ when toType == typeof(RuntimeDictionary)
                => this,
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Entries.Any()),
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeDictionary>(toType),
        };

    public override int GetHashCode()
        => Entries.GetHashCode();

    public override string ToString()
        => JsonConvert.SerializeObject(this, Formatting.Indented, _jsonConverter);

    public RuntimeObject? GetValue(string key)
    {
        return Entries.TryGetValue(key.GetHashCode(), out var value)
            ? value.Item2
            : default;
    }

    public T? GetValue<T>(string key)
        where T : RuntimeObject
    {
        return Entries.TryGetValue(key.GetHashCode(), out var value)
            ? value.Item2.As<T>()
            : default;
    }

    public T GetExpectedValue<T>(string key)
        where T : RuntimeObject
    {
        if (!Entries.TryGetValue(key.GetHashCode(), out var value))
            throw new RuntimeNotFoundException(key);

        return value.Item2.As<T>();
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
        var success = _enumerator.MoveNext();
        if (success)
        {
            Current = new RuntimeTuple(
            [
                _enumerator.Current.Item1,
                _enumerator.Current.Item2,
            ]);
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