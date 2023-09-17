using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("table")]
public class Table
{
    /// <summary>
    /// Gets all the values in a specific column.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="name">The name of the column.</param>
    /// <returns>A list of the values in the column.</returns>
    [ElkFunction("column")]
    public static RuntimeList Column(RuntimeTable table, RuntimeString name)
    {
        int index = table.Header.IndexOf(name.Value);

        return new(table.Rows.Select(x => x.ElementAtOrDefault(index) ?? RuntimeNil.Value));
    }
}