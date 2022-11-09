namespace Elk.Std.DataTypes;

public interface IIndexable<T>
{
    T this[RuntimeObject index] { get; set; }

    int Count { get; }
}