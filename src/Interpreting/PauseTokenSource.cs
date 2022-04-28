using System;
using System.Threading;
using System.Threading.Tasks;

namespace Elk.Interpreting;

// Modified version of https://stackoverflow.com/a/21712588
public class PauseTokenSource<T>
{
    public bool Finished { get; private set; }
    
    public PauseToken<T> Token
        => new(this);

    private TaskCompletionSource<bool>? _resumeRequestTcs;
    private TaskCompletionSource<bool>? _pauseConfirmationTcs;

    private readonly SemaphoreSlim _stateAsyncLock = new(1);
    private readonly SemaphoreSlim _pauseRequestAsyncLock = new(1);

    private bool _paused;
    private bool _pauseRequested;
    private T? _retrievedValue;

    public async Task FinishAsync()
    {
        Finished = true;
        await PauseIfRequestedAsync(default);
    }

    public async Task ResumeAsync()
    {
        await _stateAsyncLock.WaitAsync();
        try
        {
            if (!_paused)
            {
                return;
            }

            await _pauseRequestAsyncLock.WaitAsync();
            try
            {
                var resumeRequestTcs = _resumeRequestTcs;
                _paused = false;
                _pauseRequested = false;
                _resumeRequestTcs = null;
                _pauseConfirmationTcs = null;
                resumeRequestTcs?.TrySetResult(true);
            }
            finally
            {
                _pauseRequestAsyncLock.Release();
            }
        }
        finally
        {
            _stateAsyncLock.Release();
        }
    }

    public async Task<T?> PauseAsync(CancellationToken token = default)
    {
        await _stateAsyncLock.WaitAsync();
        try
        {
            if (_paused)
            {
                return _retrievedValue;
            }

            Task pauseConfirmationTask;

            await _pauseRequestAsyncLock.WaitAsync();
            try
            {
                _pauseRequested = true;
                _resumeRequestTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pauseConfirmationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                pauseConfirmationTask = WaitForPauseConfirmationAsync(token);
            }
            finally
            {
                _pauseRequestAsyncLock.Release();
            }

            await pauseConfirmationTask;

            _paused = true;
        }
        finally
        {
            _stateAsyncLock.Release();
        }

        return _retrievedValue;
    }

    private async Task WaitForResumeRequestAsync(CancellationToken token)
    {
        await using (token.Register(() => _resumeRequestTcs!.TrySetCanceled(), useSynchronizationContext: false))
        {
            await _resumeRequestTcs!.Task;
        }
    }

    private async Task WaitForPauseConfirmationAsync(CancellationToken token)
    {
        await using (token.Register(() => _pauseConfirmationTcs!.TrySetCanceled(), useSynchronizationContext: false))
        {
            await _pauseConfirmationTcs!.Task;
        }
    }


    internal async Task PauseIfRequestedAsync(T? returnValue)
    {
        _retrievedValue = returnValue;
        Task resumeRequestTask;
        await _pauseRequestAsyncLock.WaitAsync(CancellationToken.None);
        try
        {
            if (!_pauseRequested)
                return;

            resumeRequestTask = WaitForResumeRequestAsync(CancellationToken.None);
            _pauseConfirmationTcs?.TrySetResult(true);
        }
        finally
        {
            _pauseRequestAsyncLock.Release();
        }

        await resumeRequestTask;
    }
}

public readonly struct PauseToken<T>
{
    private readonly PauseTokenSource<T> _source;

    public PauseToken(PauseTokenSource<T> source)
    {
        _source = source;
    }

    public Task FinishAsync()
        => _source.FinishAsync();

    public Task PauseIfRequestedAsync(T? returnValue)
        => _source.PauseIfRequestedAsync(returnValue);
}