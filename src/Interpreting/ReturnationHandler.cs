using System;
using Shel.Interpreting;

enum ReturnationType
{
    None,
    BreakLoop,
    ContinueLoop,
    ReturnFunction,
}

class ReturnationHandler
{
    public bool Active
        => ReturnationType != ReturnationType.None;

    public ReturnationType ReturnationType { get; private set; }

    private IRuntimeValue? _returnValue;

    public void TriggerReturn(ReturnationType type, IRuntimeValue value)
    {
        ReturnationType = type;
        _returnValue = value;
    }

    public IRuntimeValue Collect()
    {
        ReturnationType = ReturnationType.None;
        var value = _returnValue ?? throw new InvalidOperationException("Cannot collect return value. No value is being returned.");
        _returnValue = null;

        return value;
    }
}