using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;

namespace Elk.Interpreting;

class RuntimeGenerator : IRuntimeValue, IAsyncEnumerable<IRuntimeValue?>
{
    private readonly BlockExpr? _block;
    private readonly LocalScope? _scope;
    private readonly IEnumerable<IRuntimeValue>? _values;

    public RuntimeGenerator(BlockExpr block, LocalScope scope)
    {
        _block = block;
        _scope = scope;
    }

    public RuntimeGenerator(IEnumerable<IRuntimeValue> values)
    {
        _values = values;
    }

    public IAsyncEnumerator<IRuntimeValue> GetAsyncEnumerator(CancellationToken cancellationToken = new())
        => _values != null
            ? _values.ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken)
            : new RuntimeGeneratorEnumerator(_block!, _scope!);

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

class RuntimeGeneratorEnumerator : IAsyncEnumerator<IRuntimeValue>
{
    public IRuntimeValue Current { get; private set;  }

    private readonly BlockExpr _block;
    private readonly LocalScope _scope;
    private readonly PauseTokenSource<IRuntimeValue> _pauseTokenSource = new();
    private bool _movedOnce;

    public RuntimeGeneratorEnumerator(BlockExpr block, LocalScope scope)
    {
        _block = block;
        _scope = scope;
        Current = RuntimeNil.Value;
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (!_movedOnce)
            Init();

        if (_pauseTokenSource.Finished)
            return false;

        await _pauseTokenSource.ResumeAsync();
        Current = await _pauseTokenSource.PauseAsync() ?? RuntimeNil.Value;

        if (_pauseTokenSource.Finished)
            await _pauseTokenSource.ResumeAsync();

        return !_pauseTokenSource.Finished;
    }
    
    private void Init()
    {
        _movedOnce = true;
        Task.Run(
            async () =>
            {
                await Interpreter.InterpretBlock(_block, _scope, _pauseTokenSource.Token);
            }
        );
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return default;
    }
}