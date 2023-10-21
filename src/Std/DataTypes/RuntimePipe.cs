#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Pipe")]
public class RuntimePipe : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public List<string>? Values { get; }

    private string StringValue
    {
        get
        {
            Collect();

            return Values == null
                ? ""
                : string.Join("", Values);
        }
    }

    public int Count
        => Values?.Count ?? 0;

    public IEnumerator<string> StreamEnumerator { get; }

    public IEnumerator<RuntimeObject> GetEnumerator()
        => new RuntimePipeEnumerator(StreamEnumerator, Values);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimePipe(ProcessContext process, bool disableRedirectionBuffering)
    {
        if (!disableRedirectionBuffering)
            Values = new List<string>();

        StreamEnumerator = new RuntimePipeStreamEnumerator(process, Values);
    }

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (Values == null)
                return new RuntimeString("");

            if (index is RuntimeRange range)
            {
                Collect();

                var length = (range.To ?? Values.Count) - (range.From ?? 0);
                if (range.From < 0 || range.From >= Values.Count || range.To < 0 || range.To > Values.Count)
                    throw new RuntimeItemNotFoundException($"{range.From}..{range.To}");

                return new RuntimeList(
                    Values
                        .GetRange(range.From ?? 0, length)
                        .Select(x => new RuntimeString(x))
                );
            }

            var indexValue = (int)index.As<RuntimeInteger>().Value;
            if (indexValue < 0 || indexValue >= Values.Count)
                throw new RuntimeItemNotFoundException(indexValue.ToString());

            return new RuntimeString(Values[indexValue]);
        }

        set
        {
            throw new RuntimeException("Cannot modify immutable value");
        }
    }

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimePipe)
                => this,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(StringValue),
            _ when toType == typeof(RuntimeList)
                => new RuntimeList(this),
            _ when toType == typeof(RuntimeInteger) && int.TryParse(StringValue, out var number)
                => new RuntimeInteger(number),
            _ when toType == typeof(RuntimeFloat) && double.TryParse(StringValue, out var number)
                => new RuntimeFloat(number),
            _ when toType == typeof(RuntimeRegex)
                => new RuntimeRegex(new System.Text.RegularExpressions.Regex(StringValue)),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(StringValue.Length != 0),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => kind switch
        {
            OperationKind.Subtraction => As<RuntimeFloat>().Operation(kind),
            OperationKind.Not => RuntimeBoolean.From(StringValue.Length == 0),
            _ => throw InvalidOperation(kind),
        };

    public override RuntimeObject Operation(OperationKind kind, RuntimeObject other)
    {
        if (kind is OperationKind.Subtraction or OperationKind.Multiplication or OperationKind.Division)
        {
            return As<RuntimeFloat>().Operation(kind, other);
        }

        var otherString = other.As<RuntimeString>();
        return kind switch
        {
            OperationKind.Addition => new RuntimeString(StringValue + otherString.Value),
            OperationKind.Greater => RuntimeBoolean.From(string.CompareOrdinal(StringValue, otherString.Value) > 0),
            OperationKind.GreaterEquals => RuntimeBoolean.From(string.CompareOrdinal(StringValue, otherString.Value) >= 0),
            OperationKind.Less => RuntimeBoolean.From(string.CompareOrdinal(StringValue, otherString.Value) < 0),
            OperationKind.LessEquals => RuntimeBoolean.From(string.CompareOrdinal(StringValue, otherString.Value) <= 0),
            OperationKind.EqualsEquals => RuntimeBoolean.From(StringValue == otherString.Value),
            OperationKind.NotEquals => RuntimeBoolean.From(StringValue != otherString.Value),
            _ => throw InvalidOperation(kind),
        };
    }

    public override int GetHashCode()
        => StringValue.GetHashCode();

    public override string ToString()
        => StringValue;

    public override string ToDisplayString()
        => $"\"{StringValue.Replace("\n", "\\n").Replace("\"", "\\\"")}\"";

    public void Stop()
    {
        ((RuntimePipeStreamEnumerator)StreamEnumerator).Stop();
    }

    private void Collect()
    {
        while (StreamEnumerator.MoveNext())
        {
            // The stream enumerator adds to _values itself
        }
    }
}

class RuntimePipeEnumerator : IEnumerator<RuntimeObject>
{
    public RuntimeObject Current { get; private set; } = RuntimeNil.Value;

    object IEnumerator.Current
        => Current;

    private readonly IEnumerator<string> _streamEnumerator;
    private readonly IList<string>? _values;
    private int _valuesIndex;

    public RuntimePipeEnumerator(IEnumerator<string> streamEnumerator, IList<string>? values)
    {
        _streamEnumerator = streamEnumerator;
        _values = values;
    }

    public bool MoveNext()
    {
        if (_valuesIndex < _values?.Count)
        {
            Current = new RuntimeString(_values[_valuesIndex]);
            _valuesIndex++;

            return true;
        }

        if (_streamEnumerator.MoveNext())
        {
            Current = new RuntimeString(_streamEnumerator.Current);
            _valuesIndex++;

            return true;
        }

        return false;
    }

    public void Reset()
        => _streamEnumerator.Reset();

    public void Dispose()
    {
    }
}

class RuntimePipeStreamEnumerator : IEnumerator<string>
{
    public string Current { get; private set; } = null!;

    object IEnumerator.Current
        => Current;

    private readonly ProcessContext _process;
    private readonly IList<string>? _values;
    private readonly IEnumerator<string> _processEnumerator;

    public RuntimePipeStreamEnumerator(ProcessContext process, IList<string>? values)
    {
        _process = process;
        _values = values;
        process.StartWithRedirect();
        _processEnumerator = process.GetEnumerator();
    }

    public bool MoveNext()
    {
        var result = _processEnumerator.MoveNext();
        if (result)
        {
            Current = _processEnumerator.Current;
            _values?.Add(Current);
        }

        if (!result && !_process.Success)
            throw new RuntimeException("");

        return result;
    }

    public void Reset()
    {
        _processEnumerator.Reset();
    }

    public void Stop()
    {
        _process.Stop();
    }

    void IDisposable.Dispose()
    {
        _processEnumerator.Dispose();
    }
}