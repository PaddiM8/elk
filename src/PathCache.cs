using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elk;

static class PathCache
{
    private static readonly Dictionary<string, string> _cache = [];
    private static string? _lastPathValue = null;
    private static object _updateLock = new();

    public static void RefreshInBackground()
    {
        Task.Run(() => EnsureUpdated());
    }

    public static bool IsExecutable(string name, bool waitForUpdate = true)
    {
        if (!waitForUpdate && _lastPathValue == null)
            return true;

        EnsureUpdated();

        return _cache.ContainsKey(name);
    }

    private static void EnsureUpdated()
    {
        if (Environment.GetEnvironmentVariable("PATH") == _lastPathValue)
            return;

        lock (_updateLock)
        {
            Update();
        }
    }

    private static void Update()
    {
        var isWsl = OperatingSystem.IsLinux() &&
            File.Exists("/proc/version") &&
            File.ReadAllText("/proc/version").Contains("-microsoft-");
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var path in paths)
        {
            // Ignore /mnt/... on WSL since it's super slow to look up
            if (!Directory.Exists(path) || (isWsl && path.StartsWith("/mnt/")))
                continue;

            foreach (var filePath in Directory.GetFiles(path))
            {
                if (!FileUtils.FileIsExecutable(filePath))
                    continue;

                var fileName = Path.GetFileName(filePath);
                _cache[fileName] = filePath;

                if (OperatingSystem.IsWindows() && filePath.EndsWith(".exe"))
                {
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    _cache[fileNameWithoutExtension] = filePath;
                }
            }
        }

        _lastPathValue = Environment.GetEnvironmentVariable("PATH");
    }
}
