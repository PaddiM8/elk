using System.Collections.Generic;

namespace Elk.Vm;

class ConstantTable
{
    private readonly List<object> _constants = [];

    public byte Add(object value)
    {
        _constants.Add(value);

        return (byte)(_constants.Count - 1);
    }

    public T Get<T>(byte key)
        => (T)_constants[key];
}