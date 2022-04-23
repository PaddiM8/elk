using Elk.Interpreting;

interface IIndexable<T>
{
    T this[IRuntimeValue index] { get; set; }
}