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

    public bool Success
        => ExitCode == 0 || _allowNonZeroExit;

    private Process? _process = process;
    private readonly BlockingCollection<string> _buffer = new(new ConcurrentQueue<string>());
    private bool _allowNonZeroExit;
    private int _openPipeCount;
    private bool _disposeOutput;
    private bool _disposeError;
    private readonly object _closeProcessLock = new();

    public IEnumerator<string> GetEnumerator()
        => _buffer.GetConsumingEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Start()
    {
        HasStarted = true;

        try
        {
            _process!.Start();
        }
        catch (Win32Exception)
        {
            throw new RuntimeNotFoundException(_process!.StartInfo.FileName);
        }

        if (pipedValue != null)
            Read(pipedValue);

        CloseProcess(messageOnError: false);

        return ExitCode ?? 0;
    }

    public void MakeBackground()
    {
        waitForExit = false;
        _process!.StartInfo.RedirectStandardOutput = false;
        _process!.StartInfo.RedirectStandardError = false;
    }

    public void StartWithRedirect()
    {
        HasStarted = true;

        if (!_disposeOutput)
        {
            _process!.OutputDataReceived += Process_DataReceived;
        }

        if (!_disposeError)
            _process!.ErrorDataReceived += Process_DataReceived;

        if (!waitForExit)
        {
            _process!.Exited += (_, _) =>
            {
                try
                {
                    CloseProcess(messageOnError: true);
                }
                catch
                {
                    // How would this be handled?
                }
            };
        }

        if (_disposeOutput)
            _process!.StartInfo.RedirectStandardOutput = true;

        if (_disposeError)
            _process!.StartInfo.RedirectStandardError = true;

        _allowNonZeroExit = _process!.StartInfo.RedirectStandardError;
        _process.EnableRaisingEvents = true;

        try
        {
            _process.Start();
        }
        catch (Win32Exception)
        {
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
            _buffer.CompleteAdding();

        if (pipedValue != null)
            Read(pipedValue);

        if (waitForExit)
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

    public int Wait()
    {
        CloseProcess(messageOnError: true);

        return ExitCode ?? 0;
    }

    private void Process_DataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data == null)
        {
            if (Interlocked.Decrement(ref _openPipeCount) == 0)
                _buffer.CompleteAdding();
        }
        else
        {
            _buffer.Add(eventArgs.Data);
        }
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
            else if (value is RuntimeList runtimeList)
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
        if (_process == null)
            return;

        _process.WaitForExit();

        // Sometimes the CloseProcess method is called several times,
        // which means that two threads could be waiting for exit at
        // the same time. Only one needs to dispose the process and
        // save the exit code. The other(s) should just return.
        lock (_closeProcessLock)
        {
            if (_process == null)
                return;

            ExitCode = _process.ExitCode;
            _process.Dispose();
            _process = null;

            Environment.SetEnvironmentVariable("?", ExitCode.ToString());

            if (ExitCode == 0)
                return;

            RuntimeObject message = messageOnError
                ? new RuntimeString("Program returned a non-zero exit code.")
                : RuntimeNil.Value;
            // TODO: Somehow get the actual signal rather than relying on exit codes
            if (ExitCode >= 128 && ExitCode <= 128 + SignalHelper.SignalNames.Length)
            {
                message = new RuntimeString(
                    SignalHelper.SignalNames[ExitCode ?? 0 - 128]
                );
            }

            throw new RuntimeUserException(new RuntimeError(message));
        }
    }
}