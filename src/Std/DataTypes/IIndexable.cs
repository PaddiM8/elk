namespace Elk.Std.DataTypes;

interface IIndexable<T>
{
    T this[IRuntimeValue index] { get; set; }
}