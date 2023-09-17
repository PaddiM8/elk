#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Table")]
public class RuntimeTable : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public List<RuntimeTableRow> Rows
    {
        get
        {
            if (_collectedRows == null)
            {
                _collectedRows = _uncollectedRows!.ToList();
                _uncollectedRows = null;
            }

            return _collectedRows;
        }
    }

    public List<string> Header { get; }

    private List<RuntimeTableRow>? _collectedRows;
    private IEnumerable<RuntimeTableRow>? _uncollectedRows;

    public RuntimeTable(List<string> header, IEnumerable<IEnumerable<RuntimeObject>> rows)
    {
        Header = header;
        _uncollectedRows = rows.Select(x => new RuntimeTableRow(this, x));
    }

    public RuntimeTable(RuntimeList header, IEnumerable<IEnumerable<RuntimeObject>> rows)
    {
        Header = header.Values.Select(x => x.ToString() ?? "").ToList();
        _uncollectedRows = rows.Select(x => new RuntimeTableRow(this, x));
    }

    public IEnumerator<RuntimeObject> GetEnumerator()
        => (_uncollectedRows ?? _collectedRows!).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (index is RuntimeRange range)
            {
                int length = (range.To ?? Rows.Count) - (range.From ?? 0);

                return new RuntimeTable(
                    new RuntimeList(Header.Select(x => new RuntimeString(x))),
                    Rows.GetRange(range.From ?? 0, length)
                );
            }

            return Rows.ElementAtOrDefault((int)index.As<RuntimeInteger>().Value) ??
                   throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
        }

        set
        {
            try
            {
                Rows[(int)index.As<RuntimeInteger>().Value] = value.As<RuntimeTableRow>();
            }
            catch (ArgumentOutOfRangeException)
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
                => new RuntimeList(Rows),
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
        var rows = Rows.Select(row => row.ToString());

        return $"{string.Join("\t", Header)}\n{string.Join("\n", rows)}";
    }
}