#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;

#endregion

namespace Elk.Std.DataTypes;

[ElkType("Pipe")]
public class RuntimePipe : RuntimeObject, IEnumerable<RuntimeObject>, IIndexable<RuntimeObject>
{
    public string Value
    {
        get
        {
            Collect();

            return _value?.ToString() ?? "";
        }
    }

    public int Count
        => Value.Length;

    public IEnumerator<string> StreamEnumerator { get; }

    public IEnumerator<RuntimeObject> GetEnumerator()
        => new RuntimePipeEnumerator(StreamEnumerator);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private readonly StringBuilder? _value;

    public RuntimePipe(ProcessContext process, bool disableRedirectionBuffering)
    {
        if (!disableRedirectionBuffering)
            _value = new();

        StreamEnumerator = new RuntimePipeStreamEnumerator(process, _value);
    }

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (_value == null)
                return new RuntimeString("");

            if (index is RuntimeRange range)
            {
                Collect();

                int length = (range.To ?? _value.Length) - (range.From ?? 0);
                if (range.From < 0 || range.From >= _value.Length || range.To < 0 || range.To > _value.Length)
                    throw new RuntimeItemNotFoundException($"{range.From}..{range.To}");

                return new RuntimeString(_value.ToString(range.From ?? 0, length));
            }

            int indexValue = (int)index.As<RuntimeInteger>().Value;
            if (indexValue < 0 || indexValue >= _value.Length)
                throw new RuntimeItemNotFoundException(indexValue.ToString());

            return new RuntimeString(_value[indexValue].ToString());
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
                => new RuntimeString(Value),
            _ when toType == typeof(RuntimeInteger) && int.TryParse(Value, out int number)
                => new RuntimeInteger(number),
            _ when toType == typeof(RuntimeFloat) && double.TryParse(Value, out double number)
                => new RuntimeFloat(number),
            _ when toType == typeof(RuntimeRegex)
                => new RuntimeRegex(new System.Text.RegularExpressions.Regex(Value)),
            _ when toType == typeof(RuntimeBoolean)
                => RuntimeBoolean.From(Value.Length != 0),
            _
                => throw new RuntimeCastException<RuntimeString>(toType),
        };

    public override RuntimeObject Operation(OperationKind kind)
        => kind switch
        {
            OperationKind.Subtraction => As<RuntimeFloat>().Operation(kind),
            OperationKind.Not => RuntimeBoolean.From(Value.Length == 0),
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
            OperationKind.Addition => new RuntimeString(Value + otherString.Value),
            OperationKind.Greater => RuntimeBoolean.From(string.CompareOrdinal(Value, otherString.Value) > 0),
            OperationKind.GreaterEquals => RuntimeBoolean.From(string.CompareOrdinal(Value, otherString.Value) >= 0),
            OperationKind.Less => RuntimeBoolean.From(string.CompareOrdinal(Value, otherString.Value) < 0),
            OperationKind.LessEquals => RuntimeBoolean.From(string.CompareOrdinal(Value, otherString.Value) <= 0),
            OperationKind.EqualsEquals => RuntimeBoolean.From(Value == otherString.Value),
            OperationKind.NotEquals => RuntimeBoolean.From(Value != otherString.Value),
            _ => throw InvalidOperation(kind),
        };
    }

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value;

    public override string ToDisplayString()
        => $"\"{Value.Replace("\n", "\\n").Replace("\"", "\\\"")}\"";

    public void Stop()
    {
        ((RuntimePipeStreamEnumerator)StreamEnumerator).Stop();
    }

    private void Collect()
    {
        while (StreamEnumerator.MoveNext())
        {
            // The stream enumerator appends to _value itself
        }
    }
}

class RuntimePipeEnumerator : IEnumerator<RuntimeObject>
{
    public RuntimeObject Current
        => new RuntimeString(_streamEnumerator.Current);

    object IEnumerator.Current
        => Current;

    private readonly IEnumerator<string> _streamEnumerator;

    public RuntimePipeEnumerator(IEnumerator<string> streamEnumerator)
    {
        _streamEnumerator = streamEnumerator;
    }

    public bool MoveNext()
        => _streamEnumerator.MoveNext();

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
    private readonly StringBuilder? _builder;
    private readonly IEnumerator<string> _processEnumerator;

    public RuntimePipeStreamEnumerator(ProcessContext process, StringBuilder? builder)
    {
        _process = process;
        _builder = builder;
        process.StartWithRedirect();
        _processEnumerator = process.GetEnumerator();
    }

    public bool MoveNext()
    {
        bool result = _processEnumerator.MoveNext();
        if (result)
        {
            Current = _processEnumerator.Current;
            _builder?.AppendLine(Current);
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