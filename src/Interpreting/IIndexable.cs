using Shel.Interpreting;

interface IIndexable<T>
{
    T this[IRuntimeValue index] { get; set; }
}