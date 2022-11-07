namespace Elk.Std.DataTypes;

interface IIndexable<T>
{
    T this[RuntimeObject index] { get; set; }

    int Count { get; }
}