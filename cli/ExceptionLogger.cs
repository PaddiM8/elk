using System;
using System.IO;

namespace Elk.Cli;

public static class ExceptionLogger
{
    public static void Log(Exception ex)
    {
        var logDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "elk/logs"
        );
        Directory.CreateDirectory(logDirectoryPath);

        var date = DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss");
        var logFilePath = Path.Combine(logDirectoryPath, $"exception-{date}.txt");
        File.WriteAllText(logFilePath, ex.ToString());
        Console.WriteLine($"Unexpected exception caught! This is a bug. Log written to: {logFilePath}");
    }
}