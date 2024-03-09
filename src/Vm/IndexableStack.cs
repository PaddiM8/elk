using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Elk.Vm;

class IndexableStack<T> : IEnumerable<object>
{
    public int Count
        => _items.Count;

    private readonly List<object> _items = new();

    public T this[int index]
    {
        get => (T)_items[index];
        set => _items[index] = value!;
    }

    public T Peek()
        => (T)_items.Last();

    public object PeekObject()
        => _items.Last();

    public void Push(T item)
    {
        _items.Add(item!);
    }

    public void PushObject(object item)
    {
        _items.Add(item);
    }

    public T Pop()
        => (T)PopObject();

    public object PopObject()
    {
        var item = _items.Last();
        _items.RemoveAt(_items.Count - 1);

        return item;
    }

    public IEnumerator<object> GetEnumerator()
        => Enumerate().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private IEnumerable<object> Enumerate()
    {
        for (var i = _items.Count - 1; i >= 0; i--)
            yield return _items[i];
    }
}