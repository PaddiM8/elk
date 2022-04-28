using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;

namespace Elk.Interpreting;

class RuntimeGenerator : IRuntimeValue, IEnumerable<IRuntimeValue?>
{
    private readonly BlockExpr _block;
    private readonly LocalScope _scope;
    
    public RuntimeGenerator(BlockExpr block, LocalScope scope)
    {
        _block = block;
        _scope = scope;
    }

    public IEnumerator<IRuntimeValue?> GetEnumerator()
        => new RuntimeGeneratorEnumerator(_block, _scope);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeGenerator)
                => this,
            _
                => throw new RuntimeCastException<RuntimeBoolean>(toType),
        };

    public IRuntimeValue Operation(TokenKind kind)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Generator");

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
        => throw new RuntimeInvalidOperationException(kind.ToString(), "Generator");

    public override string ToString()
        => $"generator<{GetHashCode()}>";
}

class RuntimeGeneratorEnumerator : IEnumerator<IRuntimeValue?>
{
    public IRuntimeValue? Current { get; private set;  }

    object? IEnumerator.Current
        => Current;

    private readonly BlockExpr _block;
    private readonly LocalScope _scope;
    private Task? _task;
    private PauseTokenSource<IRuntimeValue> _pauseTokenSource = new();
    private bool _movedOnce;

    public RuntimeGeneratorEnumerator(BlockExpr block, LocalScope scope)
    {
        _block = block;
        _scope = scope;
        Current = null;
    }

    public bool MoveNext()
    {
        if (!_movedOnce)
            Init();

        if (_pauseTokenSource.Finished)
            return false;

        _pauseTokenSource.ResumeAsync().Wait();
        Current = _pauseTokenSource.PauseAsync().Result;

        if (_pauseTokenSource.Finished)
            _pauseTokenSource.ResumeAsync().Wait();

        return !_pauseTokenSource.Finished;
    }

    public void Reset()
    {
        Current = null;
        _pauseTokenSource = new();
    }
    
    private void Init()
    {
        _movedOnce = true;
        _task = Task.Run(
            () =>
            {
                Interpreter.InterpretBlock(_block, _scope, _pauseTokenSource.Token);
            }
        );
    }

    void IDisposable.Dispose()
    {
        _task?.Dispose();
    }
}