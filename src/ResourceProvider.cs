using System;
using System.IO;

namespace Elk;

public static class ResourceProvider
{
    private static readonly string _resourcePath;

    static ResourceProvider()
    {
        var adjacentDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        var systemDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../share/elk");
        if (Directory.Exists(adjacentDirectory))
        {
            _resourcePath = adjacentDirectory;
        }
        else if (Directory.Exists(systemDirectory))
        {
            _resourcePath = systemDirectory;
        }
        else
        {
            _resourcePath = AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    public static string ReadFile(string relativePath)
    {
        var absolutePath = Path.Combine(_resourcePath, relativePath);

        return File.ReadAllText(absolutePath);
    }
}
