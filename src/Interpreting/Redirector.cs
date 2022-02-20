namespace Shel.Interpreting;

enum RedirectorStatus
{
    Closed,
    ExpectingInput,
    HasData,
}

class Redirector
{
    public RedirectorStatus Status => _status;

    private IRuntimeValue? _buffer;
    private RedirectorStatus _status = RedirectorStatus.Closed;

    public void Open()
    {
        _status = RedirectorStatus.ExpectingInput;
    }

    public void Send(IRuntimeValue input)
    {
        _status = RedirectorStatus.HasData;
        _buffer = input;
    }

    public IRuntimeValue? Receive()
    {
        _status = RedirectorStatus.Closed;

        return _buffer;
    }
}