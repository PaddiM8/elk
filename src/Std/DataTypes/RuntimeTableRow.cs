#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes.Serialization;

#endregion

namespace Elk.Std.DataTypes;

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
            {
                var length = (range.To ?? Columns.Count) - (range.From ?? 0);

                return new RuntimeList(Columns.GetRange(range.From ?? 0, length));
            }

            var numericalIndex = index is RuntimeInteger integer
                ? (int)integer.Value
                : _table.Header.IndexOf(index.As<RuntimeString>().Value);

            return Columns.ElementAtOrDefault(numericalIndex) ??
                   throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
        }

        set
        {
            try
            {
                var numericalIndex = index is RuntimeInteger integer
                    ? (int)integer.Value
                    : _table.Header.IndexOf(index.As<RuntimeString>().Value);

                Columns[numericalIndex] = value;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
        }
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