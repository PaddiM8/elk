using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Elk.Exceptions;
using Elk.Std.DataTypes;

namespace Elk.Vm;

public class ProcessContext(Process process, RuntimeObject? pipedValue, bool waitForExit)
    : IEnumerable<string>
{
    public bool HasStarted { get; private set; }

    public int? ExitCode { get; private set; }

    public bool BufferStdErrSeparately { get; set; }

    public bool Success
        => ExitCode == 0 || _allowNonZeroExit;

    private Process? _process = process;
    private readonly BlockingCollection<string> _mainBuffer = new(new ConcurrentQueue<string>());
    private readonly BlockingCollection<string> _secondaryBuffer = new(new ConcurrentQueue<string>());
    private BlockingCollection<string> _outBuffer = null!;
    private BlockingCollection<string> _errBuffer = null!;
    private bool _allowNonZeroExit;
    private int _openPipeCount;
    private bool _disposeOutput;
    private bool _disposeError;
    private readonly object _closeProcessLock = new();
    private bool _waitForExit = waitForExit;

    public IEnumerator<string> GetEnumerator()
        => _mainBuffer.GetConsumingEnumerable().GetEnumerator();

    public IEnumerator<string> GetSecondaryEnumerator()
        => _secondaryBuffer.GetConsumingEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Start()
    {
        HasStarted = true;

        _outBuffer = _mainBuffer;
        _errBuffer = _mainBuffer;
        if (BufferStdErrSeparately)
        {
            _errBuffer = _secondaryBuffer;
        }

        if (!_waitForExit)
        {
            _process!.Exited += OnProcessExited;
        }

        try
        {
            _process!.Start();
        }
        catch (Win32Exception)
        {
            Environment.SetEnvironmentVariable("?", "127");

            throw new RuntimeNotFoundException(_process!.StartInfo.FileName);
        }

        if (pipedValue != null)
            Read(pipedValue);

        if (_waitForExit)
            CloseProcess(messageOnError: false);

        return ExitCode ?? 0;
    }

    public void MakeBackground()
    {
        _waitForExit = false;
        _process!.StartInfo.RedirectStandardOutput = false;
        _process!.StartInfo.RedirectStandardError = false;
    }

    public void StartWithRedirect()
    {
        HasStarted = true;

        _outBuffer = _mainBuffer;
        _errBuffer = _mainBuffer;
        if (BufferStdErrSeparately)
        {
            _errBuffer = _secondaryBuffer;
        }

        if (!_disposeOutput)
        {
            _process!.OutputDataReceived += (_, e) =>
            {
                ProcessOutReceived(e);
            };
        }

        if (!_disposeError)
        {
            _process!.ErrorDataReceived += (_, e) =>
            {
                ProcessErrReceived(e);
            };
        }

        if (!_waitForExit)
        {
            _process!.Exited += OnProcessExited;
        }

        if (_disposeOutput)
            _process!.StartInfo.RedirectStandardOutput = true;

        if (_disposeError)
            _process!.StartInfo.RedirectStandardError = true;

        _allowNonZeroExit = _allowNonZeroExit || _process!.StartInfo.RedirectStandardError;
        _process!.EnableRaisingEvents = true;

        try
        {
            _process.Start();
        }
        catch (Win32Exception)
        {
            Environment.SetEnvironmentVariable("?", "127");

            throw new RuntimeNotFoundException(_process.StartInfo.FileName);
        }

        if (_process?.StartInfo.RedirectStandardOutput is true && !_disposeOutput)
        {
            _process?.BeginOutputReadLine();
            _openPipeCount++;
        }

        if (_process?.StartInfo.RedirectStandardError is true && !_disposeError)
        {
            _process?.BeginErrorReadLine();
            _openPipeCount++;
        }

        if (_openPipeCount == 0)
        {
            _mainBuffer.CompleteAdding();
            _secondaryBuffer.CompleteAdding();
        }

        if (pipedValue != null)
            Read(pipedValue);

        if (_waitForExit)
            CloseProcess(messageOnError: true);
    }

    public void Stop()
    {
        _process?.Kill();
    }

    public void EnableDisposeOutput()
    {
        _disposeOutput = true;
    }

    public void EnableDisposeError()
    {
        _disposeError = true;
    }

    public void AllowNonZeroExit()
    {
        _allowNonZeroExit = true;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (!_outBuffer.IsCompleted)
            _outBuffer.CompleteAdding();

        if (!_errBuffer.IsCompleted)
            _errBuffer.CompleteAdding();

        try
        {
            CloseProcess(messageOnError: true);
        }
        catch
        {
            // How would this be handled?
        }
    }

    public int Wait()
    {
        lock (_closeProcessLock)
        {
            if (_process != null)
            {
                _process.Exited -= OnProcessExited;
            }
        }

        CloseProcess(messageOnError: true);

        return ExitCode ?? 0;
    }

    private void ProcessOutReceived(DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data != null)
            _outBuffer.TryAdd(eventArgs.Data);
    }

    private void ProcessErrReceived(DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data != null)
            _errBuffer.TryAdd(eventArgs.Data);
    }

    private void Read(RuntimeObject value)
    {
        try
        {
            using var streamWriter = _process!.StandardInput;
            if (value is RuntimePipe runtimePipe)
            {
                while (runtimePipe.StreamEnumerator.MoveNext())
                    streamWriter.WriteLine(runtimePipe.StreamEnumerator.Current);
            }
            else if (value is RuntimeString)
            {
                streamWriter.Write(value);
            }
            else if (value is IEnumerable<RuntimeObject> runtimeList)
            {
                foreach (var item in runtimeList)
                    streamWriter.WriteLine(item);
            }
            else
            {
                streamWriter.Write(value);
            }
        }
        catch (IOException)
        {
            if (value is RuntimePipe runtimePipe)
                runtimePipe.Stop();
        }
    }

    private void CloseProcess(bool messageOnError)
    {
        // Sometimes the CloseProcess method is called several times,
        // which means that two threads could be waiting for exit at
        // the same time. Only one needs to dispose the process and
        // save the exit code. The other(s) should just return.
        lock (_closeProcessLock)
        {
            if (_process == null)
                return;

            _process.WaitForExit();

            if (!_outBuffer.IsCompleted)
                _outBuffer.CompleteAdding();

            if (!_errBuffer.IsCompleted)
                _errBuffer.CompleteAdding();

            ExitCode = _process.ExitCode;
            _process.Dispose();
            _process = null;

            Environment.SetEnvironmentVariable("?", ExitCode.ToString());

            if (Success)
                return;

            RuntimeObject message = messageOnError
                ? new RuntimeString("Program returned a non-zero exit code.")
                : RuntimeNil.Value;

            // TODO: Somehow get the actual signal rather than relying on exit codes
            if (ExitCode >= 128 && ExitCode <= 128 + SignalHelper.SignalNames.Length - 1)
            {
                message = new RuntimeString(
                    SignalHelper.SignalNames[(ExitCode ?? 128) - 128]
                );
            }

            throw new RuntimeUserException(new RuntimeError(message));
        }
    }
}
