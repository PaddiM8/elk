using System.Collections.Generic;

namespace Elk.Vm;

class ConstantTable
{
    private readonly List<object> _constants = [];

    public T Get<T>(byte key)
        => (T)_constants[key];

    public byte Add(object value)
    {
        _constants.Add(value);

        return (byte)(_constants.Count - 1);
    }

    public void Update(byte key, object value)
    {
        _constants[key] = value;
    }
}