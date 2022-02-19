using System;
using System.Collections.Generic;
using System.Linq;

namespace Shel;

public static class Extensions
{
    /// <returns>Whether or not the collection only has a single item
    /// and that item matches the predicate.</returns>
    public static bool HasSingle<T>(this ICollection<T> collection, Func<T, bool> predicate)
        => collection.Count == 1 && predicate(collection.First());
}