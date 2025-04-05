using System;
using System.IO;

namespace Elk.Cli;

public static class ExceptionLogger
{
    public static void Log(Exception ex)
    {
#if DEBUG
        Console.WriteLine("Unexpected exception caught:");
        Console.WriteLine(ex);
#else
        var logDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "elk/logs"
        );
        Directory.CreateDirectory(logDirectoryPath);

        var date = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
        var logFilePath = Path.Combine(logDirectoryPath, $"exception-{date}.txt");
        File.AppendAllText(logFilePath, ex.ToString() + Environment.NewLine);
        Console.WriteLine($"Unexpected exception caught! This is a bug. Log written to: {logFilePath}");
#endif
    }
}
