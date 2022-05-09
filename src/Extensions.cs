using System;
using System.Collections.Generic;
using System.Linq;

namespace Elk;

public static class Extensions
{
    /// <returns>Whether or not the collection only has a single item
    /// and that item matches the predicate.</returns>
    public static bool HasSingle<T>(this ICollection<T> collection, Func<T, bool> predicate)
        => collection.Count == 1 && predicate(collection.First());

    public static bool IsHex(this char c)
        => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)       
        => self.Select((item, index) => (item, index));

    public static IEnumerable<(T1?, T2?)> ZipLongest<T1, T2>(this IEnumerable<T1> a, IEnumerable<T2> b)
    {
        using var enumerator1 = a.GetEnumerator();
        using var enumerator2 = b.GetEnumerator();
        bool hasNext1 = enumerator1.MoveNext();
        bool hasNext2 = enumerator2.MoveNext();
        while (hasNext1 || hasNext2)
        {
            yield return (
                hasNext1 ? enumerator1.Current : default,
                hasNext2 ? enumerator2.Current : default
            );

            hasNext1 = enumerator1.MoveNext();
            hasNext2 = enumerator2.MoveNext();
        }
    }
}