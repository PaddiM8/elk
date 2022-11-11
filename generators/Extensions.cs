#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Elk.Generators;

public static class Extensions
{
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)
        => self.Select((item, index) => (item, index));
}