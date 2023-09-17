using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("sort")]
public class Sort
{
    /// <param name="container">The container to sort.</param>
    /// <param name="key">(for tables) The column to order by.</param>
    /// <returns>A copy of the given container in ascending order.</returns>
    [ElkFunction("asc")]
    public static RuntimeObject Asc(RuntimeObject container, RuntimeObject? key = null)
    {
        return container switch
        {
            RuntimeTable table => SortTable(table, key, descending: false),
            _ => new RuntimeList(container.As<RuntimeList>().Values.OrderBy(x => x)),
        };
    }

    /// <summary>Sorts a container in-place in ascending order.</summary>
    /// <param name="container">The container to sort.</param>
    /// <param name="key">(for tables) The column to order by.</param>
    [ElkFunction("ascMut")]
    public static void AscMut(RuntimeObject container, RuntimeObject? key = null)
    {
        if (container is RuntimeTable table)
        {
            SortTableMut(table, key, descending: false);

            return;
        }

        container.As<RuntimeList>().Values.Sort();
    }

    /// <param name="container">The container to sort.</param>
    /// <param name="key">(for tables) The column to order by.</param>
    /// <returns>A copy of the given container in descending order.</returns>
    [ElkFunction("desc")]
    public static RuntimeObject Desc(RuntimeObject container, RuntimeObject? key = null)
    {
        return container switch
        {
            RuntimeTable table => SortTable(table, key, descending: true),
            _ => new RuntimeList(container.As<RuntimeList>().Values.OrderByDescending(x => x)),
        };
    }

    /// <summary>Sorts a container in-place in descending order.</summary>
    /// <param name="container">The container to sort.</param>
    /// <param name="key">(for tables) The column to order by.</param>
    [ElkFunction("descMut")]
    public static void DescMut(RuntimeObject container, RuntimeObject? key = null)
    {
        if (container is RuntimeTable table)
        {
            SortTableMut(table, key, descending: true);

            return;
        }

        container.As<RuntimeList>().Values.Sort((a, b) => b.CompareTo(a));
    }

    private static RuntimeTable SortTable(RuntimeTable table, RuntimeObject? key, bool descending)
    {
        int index = key == null
            ? 0
            : table.Header.IndexOf(key.As<RuntimeString>().Value);
        var orderedRows = descending
            ? table.Rows.OrderByDescending(x => x.Columns.ElementAtOrDefault(index))
            : table.Rows.OrderBy(x => x.Columns.ElementAtOrDefault(index));

        return new RuntimeTable(table.Header, orderedRows);
    }

    private static void SortTableMut(RuntimeTable table, RuntimeObject? key, bool descending)
    {
        int index = key == null
            ? 0
            : table.Header.IndexOf(key.As<RuntimeString>().Value);

        if (descending)
        {
            table.Rows.Sort(
                (a, b) => b.Columns
                    .ElementAtOrDefault(index)?
                    .CompareTo(a.Columns.ElementAtOrDefault(index)) ?? 1
            );

            return;
        }

        table.Rows.Sort(
            (a, b) => a.Columns
                .ElementAtOrDefault(index)?
                .CompareTo(b.Columns.ElementAtOrDefault(index)) ?? 1
        );
    }
}