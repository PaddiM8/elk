namespace Shel.Interpreting;

enum RedirectorStatus
{
    Closed,
    Send,
    Receive,
}

class Redirector
{
    public RedirectorStatus Status => _status;

    private IRuntimeValue? _buffer;
    private RedirectorStatus _status = RedirectorStatus.Closed;

    public void Open()
    {
        _status = RedirectorStatus.Send;
    }

    public void Send(IRuntimeValue input)
    {
        _status = RedirectorStatus.Receive;
        _buffer = input;
    }

    public IRuntimeValue? Receive()
    {
        _status = RedirectorStatus.Closed;

        return _buffer;
    }
}