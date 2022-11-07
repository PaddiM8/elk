#region

using Elk.Std.DataTypes;

#endregion

namespace Elk.Interpreting;

enum RedirectorStatus
{
    Closed,
    ExpectingInput,
    HasData,
}

class Redirector
{
    public RedirectorStatus Status { get; private set; } = RedirectorStatus.Closed;

    private RuntimeObject? _buffer;

    public void Open()
    {
        Status = RedirectorStatus.ExpectingInput;
    }

    public void Send(RuntimeObject input)
    {
        Status = RedirectorStatus.HasData;
        _buffer = input;
    }

    public RuntimeObject? Receive()
    {
        Status = RedirectorStatus.Closed;

        return _buffer;
    }
}