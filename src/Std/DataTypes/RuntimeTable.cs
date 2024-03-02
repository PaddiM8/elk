#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Table")]
public class RuntimeTable : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public List<RuntimeTableRow> Rows { get; }

    public List<string> Header { get; }

    public RuntimeTable(List<string> header, List<List<RuntimeObject>> rows)
    {
        Header = header;
        Rows = rows
            .Select(x => new RuntimeTableRow(this, x))
            .ToList();
    }

    public RuntimeTable(List<string> header, List<RuntimeTableRow> rows)
    {
        Header = header;
        Rows = rows;
    }

    public RuntimeTable(RuntimeList header, List<List<RuntimeObject>> rows)
    {
        Header = header.Values.Select(x => x.ToString() ?? "").ToList();
        Rows = rows
            .Select(x => new RuntimeTableRow(this, x))
            .ToList();
    }

    public RuntimeTable(RuntimeList header, List<RuntimeTableRow> rows)
    {
        Header = header.Values.Select(x => x.ToString() ?? "").ToList();
        Rows = rows;
    }

    public IEnumerator<RuntimeObject> GetEnumerator()
        => Rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (index is RuntimeRange range)
            {
                return new RuntimeTable(
                    new RuntimeList(
                        Header.Select<string, RuntimeObject>(x => new RuntimeString(x)).ToList()
                    ),
                    Rows.GetRange(range)
                );
            }

            return Rows.GetAt(index.As<RuntimeInteger>());
        }

        set
        {
            var indexValue = (int)index.As<RuntimeInteger>().Value;
            if (indexValue < 0)
                indexValue = Rows.Count + indexValue;

            try
            {
                Rows[indexValue] = value.As<RuntimeTableRow>();
            }
            catch
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
        }
    }

    public int Count
        => Rows.Count;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeTable)
                => this,
            _ when toType == typeof(RuntimeList)
                => new RuntimeList(Rows.Cast<RuntimeObject>().ToList()),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Rows.Any()),
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override int GetHashCode()
        => Rows.GetHashCode();

    public override string ToString()
    {
        if (Rows.Count == 0)
            return "";

        var rows = new List<IEnumerable<string>>
        {
            Header,
        };
        rows.AddRange(
            Rows.Select(x =>
                x.Select(y => y.As<RuntimeString>().Value)
            )
        );

        var widths = new int[rows.First().Count()];
        foreach (var row in rows)
        {
            foreach (var (column, i) in row.WithIndex())
            {
                if (widths.Length <= i)
                    continue;

                widths[i] = Math.Max(column.Length, widths[i]);
            }
        }

        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            foreach (var (cell, i) in row.WithIndex())
            {
                if (widths.Length <= i)
                    continue;

                builder.Append(cell.PadRight(widths[i] + 2));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}