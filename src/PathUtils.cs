using System;

namespace Elk;

public static class PathUtils
{
    public static string Combine(params string[] paths)
        => ReplaceBackslashesIfWindows(System.IO.Path.Combine(paths));

    public static string Join(params string[] paths)
        => ReplaceBackslashesIfWindows(System.IO.Path.Join(paths));

    public static string ReplaceBackslashesIfWindows(string input)
    {
        return !OperatingSystem.IsWindows()
            ? input
            : input.Replace("\\", "/");
    }
}