using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("sort")]
public class Sort
{
    /// <param name="list">The list to sort.</param>
    /// <returns>A copy of the given list in ascending order.</returns>
    [ElkFunction("asc")]
    public static RuntimeList Asc(RuntimeList list)
        => new(list.Values.OrderBy(x => x));

    /// <summary>Sorts a list in-place in ascending order.</summary>
    /// <param name="list">The list to sort.</param>
    [ElkFunction("ascMut")]
    public static void AscMut(RuntimeList list)
    {
        list.Values.Sort();
    }

    /// <param name="list">The list to sort.</param>
    /// <returns>A copy of the given list in descending order.</returns>
    [ElkFunction("desc")]
    public static RuntimeList Desc(RuntimeList list)
        => new(list.Values.OrderByDescending(x => x));

    /// <summary>Sorts a list in-place in descending order.</summary>
    /// <param name="list">The list to sort.</param>
    [ElkFunction("descMut")]
    public static void DescMut(RuntimeList list)
    {
        list.Values.Sort((a, b) => b.CompareTo(a));
    }
}