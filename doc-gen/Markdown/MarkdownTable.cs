using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Elk.DocGen.Markdown;

public class MarkdownTable
{
    public bool HasRows
        => _rows.Any();

    private readonly string[] _headerColumns;
    private readonly int[] _columnWidths;
    private List<string[]> _rows = new();

    public MarkdownTable(params string[] headerColumns)
    {
        _headerColumns = headerColumns;
        _columnWidths = headerColumns.Select(x => x.Length).ToArray();
    }

    public void AddRow(params string[] columns)
    {
        _rows.Add(columns);
        foreach (var (column, i) in columns.WithIndex())
        {
            _columnWidths[i] = Math.Max(_columnWidths[i], column.Length);
        }
    }

    private string RowToString(IEnumerable<string> columns)
    {
        var builder = new StringBuilder();
        foreach (var (column, i) in columns.WithIndex())
        {
            int width = _columnWidths[i];
            builder.Append($"| {column.PadRight(width)} ");
        }

        builder.Append("|");

        return builder.ToString();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine(RowToString(_headerColumns));
        builder.AppendLine(RowToString(_columnWidths.Select(x => new string('-', x))));
        foreach (var row in _rows)
            builder.AppendLine(RowToString(row));

        return builder.ToString();
    }
}