namespace Elk.LanguageServer;

public interface ILogger
{
    void LogError(string message);

    void LogInfo(string message);

    void LogOutput(string message);
}