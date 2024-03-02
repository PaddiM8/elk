using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("sort")]
public class Sort
{
    /// <param name="container">The container to sort.</param>
    /// <returns>A copy of the given container in ascending order.</returns>
    [ElkFunction("asc")]
    public static RuntimeObject Asc(RuntimeObject container)
    {
        return container switch
        {
            RuntimeTable table => new RuntimeTable(
                table.Header,
                table.Rows.OrderBy(x => x).ToList()
            ),
            IEnumerable<RuntimeObject> enumerable => new RuntimeList(
                enumerable.OrderBy(x => x).ToList()
            ),
            _ => throw new RuntimeCastException(container.GetType(), "Iterable"),
        };
    }

    /// <param name="container">The container to sort.</param>
    /// <param name="closure">The container to sort.</param>
    /// <returns>A copy of the given container in ascending order.</returns>
    [ElkFunction("ascBy")]
    public static RuntimeObject AscBy(RuntimeObject container, Func<RuntimeObject, RuntimeObject> closure)
    {
        return container switch
        {
            RuntimeTable table => new RuntimeTable(
                table.Header,
                table.Rows
                    .OrderBy(closure)
                    .Cast<RuntimeTableRow>()
                    .ToList()
            ),
            IEnumerable<RuntimeObject> enumerable => new RuntimeList(enumerable.OrderBy(closure).ToList()),
            _ => throw new RuntimeCastException(container.GetType(), "Iterable"),
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
    /// <returns>A copy of the given container in descending order.</returns>
    [ElkFunction("desc")]
    public static RuntimeObject Desc(RuntimeObject container)
    {
        return container switch
        {
            RuntimeTable table => new RuntimeTable(
                table.Header,
                table.Rows.OrderByDescending(x => x).ToList()
            ),
            IEnumerable<RuntimeObject> enumerable => new RuntimeList(enumerable.OrderByDescending(x => x).ToList()),
            _ => throw new RuntimeCastException(container.GetType(), "Iterable"),
        };
    }

    /// <param name="container">The container to sort.</param>
    /// <param name="closure">The container to sort.</param>
    /// <returns>A copy of the given container in descending order.</returns>
    [ElkFunction("descBy")]
    public static RuntimeObject DescBy(RuntimeObject container, Func<RuntimeObject, RuntimeObject> closure)
    {
        return container switch
        {
            RuntimeTable table => new RuntimeTable(
                table.Header,
                table.Rows
                    .OrderByDescending(closure)
                    .Cast<RuntimeTableRow>()
                    .ToList()
            ),
            IEnumerable<RuntimeObject> enumerable => new RuntimeList(
                enumerable.OrderByDescending(closure).ToList()
            ),
            _ => throw new RuntimeCastException(container.GetType(), "Iterable"),
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

    private static void SortTableMut(RuntimeTable table, RuntimeObject? key, bool descending)
    {
        var index = key == null
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