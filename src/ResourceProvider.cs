using System;
using System.IO;

namespace Elk;

public static class ResourceProvider
{
    private static readonly string _resourcePath;

    static ResourceProvider()
    {
        var adjacentDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        _resourcePath = Directory.Exists(adjacentDirectory)
            ? adjacentDirectory
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../share/elk");
    }

    public static string ReadFile(string relativePath)
    {
        var absolutePath = Path.Combine(_resourcePath, relativePath);

        return File.ReadAllText(absolutePath);
    }
}