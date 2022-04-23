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

    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)       
        => self.Select((item, index) => (item, index)); 
}