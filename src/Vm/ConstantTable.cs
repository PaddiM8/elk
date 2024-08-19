using System;
using System.Collections.Generic;
using Elk.Exceptions;
using Elk.Std.DataTypes;

namespace Elk.Vm;

class ConstantTable
{
    private readonly List<object> _constants = [RuntimeNil.Value];
    private readonly Dictionary<(Type, object), ushort> _cache = [];

    public void ClearCache()
    {
        _cache.Clear();
    }

    public T Get<T>(ushort key)
        => (T)_constants[key];

    public ushort Add(object value)
    {
        if (_constants.Count >= ushort.MaxValue)
            throw new RuntimeException($"Too many constants in function. There can be at most {ushort.MaxValue} constants");

        if (value is RuntimeNil)
            return 0;

        var canCache = true;
        (Type, object)? cacheKey = null;
        try
        {
            cacheKey = (value.GetType(), value);
            if (_cache.TryGetValue(cacheKey.Value, out var key))
                return key;
        }
        catch
        {
            canCache = false;
        }

        _constants.Add(value);
        var newKey = (ushort)(_constants.Count - 1);
        if (canCache)
            _cache.Add(cacheKey!.Value, newKey);

        return newKey;
    }

    public void Update(ushort key, object value)
    {
        _constants[key] = value;
    }
}