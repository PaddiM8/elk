#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Std.Serialization;

#endregion

namespace Elk.Std.Table;

[ElkType("TableRow")]
public class RuntimeTableRow : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public List<RuntimeObject> Columns { get; }

    private readonly RuntimeTable _table;

    public RuntimeTableRow(RuntimeTable table, IEnumerable<RuntimeObject> columns)
    {
        _table = table;
        Columns = columns.ToList();
    }

    public IEnumerator<RuntimeObject> GetEnumerator()
        => Columns.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (index is RuntimeRange range)
                return new RuntimeList(Columns.GetRange(range));

            try
            {
                return Columns[ResolveIndex(index)];
            }
            catch
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
        }

        set
        {
            try
            {
                Columns[ResolveIndex(index)] = value;
            }
            catch
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
        }
    }

    private int ResolveIndex(RuntimeObject index)
    {
        if (index is RuntimeInteger indexInteger)
        {
            var indexValue = (int)indexInteger.Value;
            if (indexValue < 0)
                indexValue = Columns.Count + indexValue;

            return indexValue;
        }

        var indexFromHeader = _table.Header.IndexOf(index.As<RuntimeString>().Value);
        if (indexFromHeader == -1)
            throw new RuntimeNotFoundException(index.ToString() ?? "?");

        return indexFromHeader;
    }

    public int Count
        => Columns.Count;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeObject)
                => this,
            _ when toType == typeof(RuntimeList)
                => new RuntimeList(Columns),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Columns.Any()),
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override int GetHashCode()
        => Columns.GetHashCode();

    public override string ToString()
        => $"{string.Join("\t", Columns)}";

    public override string ToDisplayString()
        => StringFormatting.ToDisplayString(ToString());
}