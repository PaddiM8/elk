namespace Elk.ReadLine;

public interface IHintHandler
{
    public string Hint(string promptText);

    public void Reset();
}