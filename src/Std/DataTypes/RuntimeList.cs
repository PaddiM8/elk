#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.Serialization;
using Elk.Std.Table;
using Newtonsoft.Json;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("List")]
public class RuntimeList(List<RuntimeObject> values)
    : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public List<RuntimeObject> Values { get; } = values;

    private static readonly RuntimeObjectJsonConverter _jsonConverter = new();

    public IEnumerator<RuntimeObject> GetEnumerator()
        => Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public override int CompareTo(RuntimeObject? other)
        => other is IEnumerable<RuntimeObject> otherEnumerable
            ? this.OrdinalCompare(otherEnumerable)
            : throw new RuntimeInvalidOperationException("comparison", GetType());

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (index is RuntimeRange range)
                return new RuntimeList(Values.GetRange(range));

            return Values.GetAt(index.As<RuntimeInteger>());
        }

        set
        {
            var indexValue = (int)index.As<RuntimeInteger>().Value;
            if (indexValue < 0)
                indexValue = Values.Count + indexValue;

            try
            {
                Values[indexValue] = value;
            }
            catch
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
                    Values.FirstOrDefault()?.As<RuntimeList>()
                        ?? new RuntimeList([]),
                    Values
                        .Skip(1)
                        .Select(x => x.As<RuntimeList>().ToList())
                        .ToList()
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
        var json = JsonConvert.SerializeObject(this, Formatting.Indented, _jsonConverter);

        // If it ends up being short, redo it all and simply print it on one line instead
        const int lineLimit = 100;
        var totalIndentationLength = 3 * Values.Count;

        return json.Length - totalIndentationLength < lineLimit
            ? $"[{string.Join(", ", Values.Select(x => x.ToDisplayString()))}]"
            : json;
    }
}