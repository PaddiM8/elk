using Elk.Std.Attributes;

namespace Elk.Std.DataTypes;

[ElkType("Indexable")]
public interface IIndexable<T>
{
    T this[RuntimeObject index] { get; set; }

    int Count { get; }
}