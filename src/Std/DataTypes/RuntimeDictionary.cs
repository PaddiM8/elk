#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.Serialization;
using Newtonsoft.Json;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Dictionary")]
public class RuntimeDictionary(Dictionary<RuntimeObject, RuntimeObject> entries)
    : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public Dictionary<RuntimeObject, RuntimeObject> Entries { get; } = entries;

    public RuntimeDictionary()
        : this(new Dictionary<RuntimeObject, RuntimeObject>())
    {
    }

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (Entries.TryGetValue(index, out var value))
                return value;

            throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
        }

        set
        {
            Entries[index] = value;
        }
    }

    public RuntimeObject this[string index]
    {
        get
        {
            if (Entries.TryGetValue(new RuntimeString(index), out var value))
                return value;

            throw new RuntimeItemNotFoundException(index);
        }

        set
        {
            var runtimeIndex = new RuntimeString(index);
            Entries[runtimeIndex] = value;
        }
    }

    public int Count
        => Entries.Count;

    public IEnumerator<RuntimeObject> GetEnumerator()
        => new RuntimeDictionaryEnumerator(Entries);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeDictionary)
                => this,
            _ when toType == typeof(RuntimeList)
                => new RuntimeList(
                    Entries
                        .Select(x => new RuntimeTuple([x.Key, x.Value]))
                        .Cast<RuntimeObject>()
                        .ToList()
                ),
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
        => ElkJsonSerializer.Serialize(this, Formatting.Indented);

    public RuntimeObject? GetValue(string key)
        => Entries.GetValueOrDefault(new RuntimeString(key));

    public T? GetValue<T>(string key)
        where T : RuntimeObject
    {
        return Entries.TryGetValue(new RuntimeString(key), out var value)
            ? value.As<T>()
            : default;
    }

    public T GetExpectedValue<T>(string key)
        where T : RuntimeObject
    {
        if (!Entries.TryGetValue(new RuntimeString(key), out var value))
            throw new RuntimeNotFoundException(key);

        return value.As<T>();
    }
}

class RuntimeDictionaryEnumerator : IEnumerator<RuntimeObject>
{
    public RuntimeObject Current { get; private set; }

    object IEnumerator.Current
        => Current;

    private readonly IEnumerable<KeyValuePair<RuntimeObject, RuntimeObject>> _keyValuePairs;
    private IEnumerator<KeyValuePair<RuntimeObject, RuntimeObject>> _enumerator;

    public RuntimeDictionaryEnumerator(IEnumerable<KeyValuePair<RuntimeObject, RuntimeObject>> keyValuePairs)
    {
        _keyValuePairs = keyValuePairs;
        _enumerator = _keyValuePairs.GetEnumerator();
        Current = RuntimeNil.Value;
    }

    public bool MoveNext()
    {
        var success = _enumerator.MoveNext();
        if (success)
        {
            Current = new RuntimeTuple(
            [
                _enumerator.Current.Key,
                _enumerator.Current.Value,
            ]);
        }

        return success;
    }

    public void Reset()
    {
        _enumerator = _keyValuePairs.GetEnumerator();
        Current = RuntimeNil.Value;
    }

    void IDisposable.Dispose()
    {
    }
}