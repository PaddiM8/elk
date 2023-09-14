using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Elk.Interpreting.Exceptions;
using Elk.Std.DataTypes;

namespace Elk.Interpreting;

public class ProcessContext
{
    private Process? _process;
    private readonly object _processLock = new();
    private readonly RuntimeObject? _pipedValue;
    private readonly ConcurrentQueue<string> _buffer = new();
    private readonly AutoResetEvent _dataReceived = new(false);
    private bool _allowNonZeroExit;
    private int _exitCode;

    public ProcessContext(Process process, RuntimeObject? pipedValue)
    {
        _process = process;
        _pipedValue = pipedValue;
    }

    public int Start()
    {
        try
        {
            _process!.Start();
        }
        catch (Win32Exception)
        {
            throw new RuntimeNotFoundException(_process!.StartInfo.FileName);
        }

        if (_pipedValue != null)
            Read(_pipedValue);

        _process.WaitForExit();
        int exitCode = _process.ExitCode;
        CloseProcess();

        return exitCode;
    }

    public void StartWithRedirect()
    {
        _process!.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                _buffer.Enqueue(e.Data);

            _dataReceived.Set();
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                _buffer.Enqueue(e.Data);

            _dataReceived.Set();
        };
        _process.Exited += (_, e) =>
            CloseProcess();

        _allowNonZeroExit = _process.StartInfo.RedirectStandardError;
        _process.EnableRaisingEvents = true;

        try
        {
            _process.Start();
        }
        catch (Win32Exception)
        {
            throw new RuntimeNotFoundException(_process.StartInfo.FileName);
        }

        if (_process?.StartInfo.RedirectStandardOutput is true)
            _process.BeginOutputReadLine();

        if (_process?.StartInfo.RedirectStandardError is true)
            _process.BeginErrorReadLine();

        if (_pipedValue != null)
            Read(_pipedValue);
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
            else
            {
                streamWriter.Write(value);
            }
        }
        catch (IOException e)
        {
            throw new RuntimeException(e.Message);
        }
    }

    public string? NextLine()
    {
        if (_process != null && _buffer.IsEmpty)
            _dataReceived.WaitOne();

        _buffer.TryDequeue(out var content);

        if (content == null && _exitCode != 0 && _allowNonZeroExit)
            throw new RuntimeException("");

        return content;
    }

    private void CloseProcess()
    {
        lock (_processLock)
        {
            if (_process == null)
                return;

            _process.WaitForExit();
            _exitCode = _process.ExitCode;
            _process.Dispose();
            _process = null;
            _dataReceived.Set();
        }
    }

    /*private bool NextOutput(out string? content)
    {
        if (!_isOutputDone && _buffer.Count == 0)
            _dataReceived.WaitOne();

        if (_isOutputDone)
        {
            if (_process?.StartInfo.RedirectStandardError is false)
            {
                _process.WaitForExit();
                CloseProcess();
            }

            if (_exitCode != 0 && _isErrorDone)
                throw new RuntimeException("");
        }

        content = null;
        if (_buffer.Count == 0)
            return false;

        content = _buffer.Dequeue();

        return !_isOutputDone || _buffer.Count > 0;
    }

    private bool NextError(out string? content)
    {
        if (!_isOutputDone && _buffer.Count == 0)
            _dataReceived.WaitOne();

        if (_isErrorDone)
        {
            if (_process?.StartInfo.RedirectStandardOutput is false)
            {
                _process.WaitForExit();
                CloseProcess();
            }

            if (_exitCode != 0 && _isOutputDone)
                throw new RuntimeException("");
        }

        content = null;
        if (_buffer.Count == 0)
            return false;

        content = _buffer.Dequeue();

        return !_isOutputDone || _buffer.Count > 0;
    }*/
}