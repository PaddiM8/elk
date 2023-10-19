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

[ElkType("List")]
public class RuntimeList : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public List<RuntimeObject> Values
    {
        get
        {
            if (_collectedValues == null)
            {
                _collectedValues = _uncollectedValues!.ToList();
                _uncollectedValues = null;
            }

            return _collectedValues;
        }
    }

    private List<RuntimeObject>? _collectedValues;
    private IEnumerable<RuntimeObject>? _uncollectedValues;

    public RuntimeList(IEnumerable<RuntimeObject> values)
    {
        _uncollectedValues = values;
    }

    public IEnumerator<RuntimeObject> GetEnumerator()
        => (_uncollectedValues ?? _collectedValues!).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (index is RuntimeRange range)
            {
                var length = (range.To ?? Values.Count) - (range.From ?? 0);

                return new RuntimeList(Values.GetRange(range.From ?? 0, length));
            }

            return Values.ElementAtOrDefault((int)index.As<RuntimeInteger>().Value) ??
                   throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
        }

        set
        {
            try
            {
                Values[(int)index.As<RuntimeInteger>().Value] = value;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new RuntimeItemNotFoundException(index.ToString() ?? "?");
            }
        }
    }

    public int Count
        => Values.Count;

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeList)
                => this,
            _ when toType == typeof(RuntimeTable)
                => new RuntimeTable(
                    Values.FirstOrDefault()?.As<RuntimeList>() ?? new RuntimeList(new List<RuntimeObject>()),
                    Values.Skip(1).Select(x => x.As<RuntimeList>())
                ),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Values.Any()),
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override int GetHashCode()
        => Values.GetHashCode();

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine("[");

        var hasNestedList = false;
        foreach (var value in Values)
        {
            builder.Append("    ");

            // Print nested lists on one line
            if (value is RuntimeList list)
            {
                hasNestedList = true;
                builder.Append('[');
                builder.Append(
                    string.Join(
                        ", ",
                        list.Select(x => x.ToDisplayString())
                    )
                );
                builder.AppendLine("],");

                continue;
            }

            builder.Append(value.ToDisplayString());
            builder.AppendLine(",");
        }

        builder.AppendLine("]");

        // If it ends up being short and doesn't contain a nested list,
        // redo it all and simply print it on one line instead.
        const int lineLimit = 100;
        var totalIndentationLength = 3 * Values.Count;
        if (!hasNestedList && builder.Length - totalIndentationLength < lineLimit)
            return $"[{string.Join(", ", Values.Select(x => x.ToDisplayString()))}]";

        return builder.ToString();
    }
}