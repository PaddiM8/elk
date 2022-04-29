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

    private IRuntimeValue? _buffer;

    public void Open()
    {
        Status = RedirectorStatus.ExpectingInput;
    }

    public void Send(IRuntimeValue input)
    {
        Status = RedirectorStatus.HasData;
        _buffer = input;
    }

    public IRuntimeValue? Receive()
    {
        Status = RedirectorStatus.Closed;

        return _buffer;
    }
}