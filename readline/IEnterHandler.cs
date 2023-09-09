namespace Elk.ReadLine;

public interface IEnterHandler
{
    public EnterHandlerResponse Handle(string promptText, int caret);
}