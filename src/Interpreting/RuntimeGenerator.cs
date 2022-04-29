using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;

namespace Elk.Interpreting;

class RuntimeGenerator : IRuntimeValue, IAsyncEnumerable<IRuntimeValue?>
{
    private readonly BlockExpr _block;
    private readonly LocalScope _scope;
    
    public RuntimeGenerator(BlockExpr block, LocalScope scope)
    {
        _block = block;
        _scope = scope;
    }

    public IAsyncEnumerator<IRuntimeValue?> GetAsyncEnumerator(CancellationToken cancellationToken = new())
        => new RuntimeGeneratorEnumerator(_block, _scope);

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

class RuntimeGeneratorEnumerator : IAsyncEnumerator<IRuntimeValue?>
{
    public IRuntimeValue? Current { get; private set;  }

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

    public async ValueTask<bool> MoveNextAsync()
    {
        if (!_movedOnce)
            Init();

        if (_pauseTokenSource.Finished)
            return false;

        await _pauseTokenSource.ResumeAsync();
        Current = _pauseTokenSource.PauseAsync().GetAwaiter().GetResult();

        if (_pauseTokenSource.Finished)
            await _pauseTokenSource.ResumeAsync();

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
            async () =>
            {
                await Interpreter.InterpretBlock(_block, _scope, _pauseTokenSource.Token);
            }
        );
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        _task?.Dispose();

        return default;
    }
}