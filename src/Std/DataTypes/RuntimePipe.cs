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
            while (StreamEnumerator.MoveNext())
            {
                // The stream enumerator appends to _value itself
            }

            return _value.ToString();
        }
    }

    public int Count
        => Value.Length;

    public IEnumerator<RuntimeObject> GetEnumerator()
        => new RuntimePipeEnumerator(StreamEnumerator);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public IEnumerator<string> StreamEnumerator
        => _streamEnumerator;

    private readonly StringBuilder _value = new();
    private readonly RuntimePipeStreamEnumerator _streamEnumerator;

    public RuntimePipe(ProcessContext process)
    {
        _streamEnumerator = new RuntimePipeStreamEnumerator(process, _value);
    }

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (index is RuntimeRange range)
            {
                int length = (range.To ?? Value.Length) - (range.From ?? 0);
                if (range.From < 0 || range.From >= Value.Length || range.To < 0 || range.To > Value.Length)
                    throw new RuntimeItemNotFoundException($"{range.From}..{range.To}");

                return new RuntimeString(Value.Substring(range.From ?? 0, length));
            }

            int indexValue = (int)index.As<RuntimeInteger>().Value;
            if (indexValue < 0 || indexValue >= Value.Length)
                throw new RuntimeItemNotFoundException(indexValue.ToString());

            return new RuntimeString(Value[indexValue].ToString());
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
    private readonly StringBuilder _builder;

    public RuntimePipeStreamEnumerator(ProcessContext process, StringBuilder builder)
    {
        _process = process;
        _builder = builder;
        _process.StartWithRedirect();
    }

    public bool MoveNext()
    {
        var data = _process.NextLine();
        if (data == null)
            return false;

        Current = data;
        _builder.AppendLine(data);

        return true;
    }

    public void Reset()
    {
    }

    void IDisposable.Dispose()
    {
    }

}