using System;
using Elk.Interpreting;

enum ReturnKind
{
    None,
    BreakLoop,
    ContinueLoop,
    ReturnFunction,
}

class ReturnHandler
{
    public bool Active
        => ReturnKind != ReturnKind.None;

    public ReturnKind ReturnKind { get; private set; }

    private IRuntimeValue? _returnValue;

    public void TriggerReturn(ReturnKind type, IRuntimeValue value)
    {
        ReturnKind = type;
        _returnValue = value;
    }

    public IRuntimeValue Collect()
    {
        ReturnKind = ReturnKind.None;
        var value = _returnValue ?? throw new InvalidOperationException("Cannot collect return value. No value is being returned.");
        _returnValue = null;

        return value;
    }
}