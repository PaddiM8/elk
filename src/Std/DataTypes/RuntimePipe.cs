#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;
using Elk.Std.Serialization;
using Elk.Vm;

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
                : string.Join(System.Environment.NewLine, Values);
        }
    }

    public int Count
    {
        get
        {
            Collect();

            return Values?.Count ?? 0;
        }
    }

    public bool UseSecondaryEnumerator { get; init; }

    public int? ExitCode
        => _processContext.ExitCode;

    public IEnumerator<string> StreamEnumerator { get; private set; } = GetNullEnumerator();

    public IEnumerator<RuntimeObject> GetEnumerator()
        => new RuntimePipeEnumerator(StreamEnumerator, Values);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private readonly ProcessContext _processContext;

    public RuntimePipe(ProcessContext process, bool disableRedirectionBuffering, bool automaticStart)
    {
        _processContext = process;

        if (!disableRedirectionBuffering)
            Values = [];

        if (automaticStart)
            Start();
    }

    public RuntimeObject this[RuntimeObject index]
    {
        get
        {
            if (Values == null)
                return new RuntimeString("");

            Collect();
            if (index is RuntimeRange range)
            {
                return new RuntimeList(
                    Values
                        .GetRange(range)
                        .Select<string, RuntimeObject>(x => new RuntimeString(x))
                        .ToList()
                );
            }

            return new RuntimeString(Values.GetAt(index.As<RuntimeInteger>()));
        }

        set
        {
            throw new RuntimeException("Cannot modify immutable value");
        }
    }

    public override bool Equals(object? obj)
        => obj is RuntimeObject runtimeObject &&
            Operation(OperationKind.EqualsEquals, runtimeObject) is RuntimeBoolean { IsTrue: true };

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimePipe)
                => this,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(StringValue),
            _ when toType == typeof(RuntimeList)
                => new RuntimeList(this.ToList()),
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
        => StringFormatting.ToDisplayString(StringValue);

    public RuntimePipe CloneAsSecondary()
    {
        var cloned = new RuntimePipe(
            _processContext,
            disableRedirectionBuffering: Values == null,
            automaticStart: false
        )
        {
            UseSecondaryEnumerator = true,
        };

        cloned.StreamEnumerator = new RuntimePipeStreamEnumerator(_processContext, cloned.Values, useSecondary: true);

        return cloned;
    }

    public void Start()
    {
        if (_processContext.HasStarted)
            return;

        StreamEnumerator = new RuntimePipeStreamEnumerator(_processContext, Values);
    }

    public void MakeBackground()
    {
        if (_processContext.HasStarted)
            return;

        _processContext.MakeBackground();
    }

    public void Stop()
    {
        ((RuntimePipeStreamEnumerator?)StreamEnumerator)?.Stop();
    }

    public void EnableDisposeOutput()
    {
        _processContext.EnableDisposeOutput();
    }

    public void EnableDisposeError()
    {
        _processContext.EnableDisposeError();
    }

    public void AllowNonZeroExit()
    {
        _processContext.AllowNonZeroExit();
    }

    public void EnableSecondaryStreamForStdErr()
    {
        _processContext.BufferStdErrSeparately = true;
    }

    public int Wait()
        => _processContext.Wait();

    private void Collect()
    {
        while (StreamEnumerator.MoveNext())
        {
            // The stream enumerator adds to _values itself
        }
    }

    private static IEnumerator<string> GetNullEnumerator()
    {
        yield break;
    }
}

class RuntimePipeEnumerator(IEnumerator<string> streamEnumerator, IList<string>? values)
    : IEnumerator<RuntimeObject>
{
    public RuntimeObject Current { get; private set; } = RuntimeNil.Value;

    object IEnumerator.Current
        => Current;

    private int _valuesIndex;

    public bool MoveNext()
    {
        if (_valuesIndex < values?.Count)
        {
            Current = new RuntimeString(values[_valuesIndex]);
            _valuesIndex++;

            return true;
        }

        if (streamEnumerator.MoveNext())
        {
            Current = new RuntimeString(streamEnumerator.Current);
            _valuesIndex++;

            return true;
        }

        return false;
    }

    public void Reset()
        => streamEnumerator.Reset();

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

    public RuntimePipeStreamEnumerator(ProcessContext process, IList<string>? values, bool useSecondary = false)
    {
        _process = process;
        _values = values;

        if (!useSecondary)
            process.StartWithRedirect();

        _processEnumerator = useSecondary
            ? process.GetSecondaryEnumerator()
            : process.GetEnumerator();
    }

    public bool MoveNext()
    {
        var result = _processEnumerator.MoveNext();
        if (result)
        {
            Current = _processEnumerator.Current;
            _values?.Add(Current);
        }

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
        _process.Wait();
        _processEnumerator.Dispose();
    }
}