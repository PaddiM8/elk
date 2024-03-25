using System.Collections.Generic;
using Elk.Exceptions;

namespace Elk.Vm;

class ConstantTable
{
    private readonly List<object> _constants = [];

    public T Get<T>(ushort key)
        => (T)_constants[key];

    public ushort Add(object value)
    {
        if (_constants.Count >= ushort.MaxValue)
            throw new RuntimeException($"Too many constants in function. There can be at most {ushort.MaxValue} constants");

        _constants.Add(value);

        return (ushort)(_constants.Count - 1);
    }

    public void Update(ushort key, object value)
    {
        _constants[key] = value;
    }
}