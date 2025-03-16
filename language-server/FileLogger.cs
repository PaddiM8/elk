namespace Elk.LanguageServer;

public enum LogLevel
{
    Stdio,
    Info,
    Error,
}

public class FileLogger : ILogger
{
    public LogLevel LogLevel { get; init; }

    private readonly DateTimeOffset _date = DateTimeOffset.Now;
    private readonly string _logDirectoryPath;

    public FileLogger()
    {
        _logDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "elk/logs/lsp"
        );
        Directory.CreateDirectory(_logDirectoryPath);
    }

    public void LogError(string message)
    {
        File.AppendAllText(
            Path.Combine(_logDirectoryPath, _date.ToString("yyyy-MM-dd-HH:mm:ss") + ".log"),
            $"ERROR: {message}{Environment.NewLine}"
        );
    }

    public void LogInfo(string message)
    {
        if (LogLevel > LogLevel.Info)
            return;

        File.AppendAllText(
            Path.Combine(_logDirectoryPath, _date.ToString("yyyy-MM-dd-HH:mm:ss") + ".log"),
            $"INFO: {message}{Environment.NewLine}"
        );
    }

    public void LogOutput(string message)
    {
        if (LogLevel > LogLevel.Stdio)
            return;

        File.AppendAllText(
            Path.Combine(_logDirectoryPath, _date.ToString("yyyy-MM-dd-HH:mm:ss") + "-stdout.log"),
            message
        );
    }
}