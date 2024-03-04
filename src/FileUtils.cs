using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.ReadLine;
using Elk.Std.Serialization.CommandLine;

namespace Elk;

public enum FileType
{
    All,
    Directory,
    Executable,
}

public static class FileUtils
{
    public static bool FileIsExecutable(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return false;

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            return false;

        return (
            fileInfo.UnixFileMode & (
                UnixFileMode.OtherExecute |
                UnixFileMode.GroupExecute |
                UnixFileMode.UserExecute
            )
        ) != 0;
    }

    public static bool ExecutableExists(string name, string workingDirectory)
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (name.StartsWith('~'))
            name = homePath + name[1..];

        if (name.StartsWith('.'))
        {
            var absolutePath = Path.Combine(workingDirectory, name);

            return FileIsExecutable(absolutePath);
        }

        if (name.StartsWith('/'))
            return FileIsExecutable(name);

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(":")
            .Any(x => Directory.Exists(x) && FileIsExecutable(Path.Combine(x, name))) is true;
    }

    public static bool IsValidStartOfPath(string path, string workingDirectory)
    {
        path = ExpandPath(path);
        var absolutePath = path.StartsWith('/')
            ? path
            : Path.Combine(workingDirectory, path);
        if (absolutePath == "/")
            return true;

        if (File.Exists(absolutePath) || Directory.Exists(absolutePath))
            return true;

        var parentPath = Path.GetDirectoryName(absolutePath);
        if (!Directory.Exists(parentPath))
            return false;

        var fileName = Path.GetFileName(path);

        return Directory.GetFileSystemEntries(parentPath)
            .Select(Path.GetFileName)
            .Any(x => x?.StartsWith(fileName) is true);
    }

    public static IList<Completion> GetPathCompletions(
        string path,
        string workingDirectory,
        FileType fileType,
        CompletionKind completionKind = CompletionKind.Normal)
    {
        try
        {
            return GetPathCompletionsInternal(path, workingDirectory, fileType, completionKind);
        }
        catch
        {
            return Array.Empty<Completion>();
        }
    }

    private static IList<Completion> GetPathCompletionsInternal(
        string path,
        string workingDirectory,
        FileType fileType,
        CompletionKind completionKind = CompletionKind.Normal)
    {
        var lastSlashIndex = path
            .WithIndex()
            .LastOrDefault(x => x.item == '/' && path.ElementAtOrDefault(x.index - 1) != '\\')
            .index;
        // Get the full path of the folder, without the part that is being completed
        var pathWithoutCompletion = lastSlashIndex == 0 && path.StartsWith('/')
            ? "/"
            : path[..lastSlashIndex];
        var fullPath = path.StartsWith('/')
            ? ExpandPath(pathWithoutCompletion)
            : Path.Combine(workingDirectory, ExpandPath(pathWithoutCompletion));
        var completionTarget = path.EndsWith('/')
            ? ""
            : ExpandPath(path[lastSlashIndex..].TrimStart('/'));

        if (!Directory.Exists(fullPath))
            return Array.Empty<Completion>();

        var includeHidden = path.StartsWith('.');
        IList<Completion> directories = Directory.GetDirectories(fullPath)
            .Select(Path.GetFileName)
            .Where(x => includeHidden || !x!.StartsWith('.'))
            .Where(x => x!.StartsWith(completionTarget))
            .Order()
            .Select(x => new Completion(Path.Join(pathWithoutCompletion, x), $"{x}/"))
            .ToList();

        IEnumerable<Completion> files = Array.Empty<Completion>();
        if (fileType != FileType.Directory)
        {
            files = Directory.GetFiles(fullPath)
                .Select(x => (path: x, name: Path.GetFileName(x)))
                .Where(x => includeHidden || !x.name.StartsWith('.'))
                .Where(x => x.name.StartsWith(completionTarget))
                .Where(x => fileType != FileType.Executable || FileIsExecutable(x.path))
                .Order()
                .Select(x => new Completion(Path.Join(pathWithoutCompletion, x.name), x.name)
                {
                    HasTrailingSpace = true,
                })
                .ToList();
        }

        if (completionKind != CompletionKind.Hint && !directories.Any() && !files.Any())
        {
            const StringComparison comparison = StringComparison.CurrentCultureIgnoreCase;
            directories = Directory.GetDirectories(fullPath)
                .Select(Path.GetFileName)
                .Where(x => x!.Contains(completionTarget, comparison))
                .Order()
                .Select(x => new Completion(Path.Join(pathWithoutCompletion, x), $"{x}/"))
                .ToList();

            if (fileType != FileType.Directory)
            {
                files = Directory.GetFiles(fullPath)
                    .Select(x => (path: x, name: Path.GetFileName(x)))
                    .Where(x => x.name.Contains(completionTarget, comparison))
                    .Where(x => fileType != FileType.Executable || FileIsExecutable(x.path))
                    .Order()
                    .Select(x => new Completion(Path.Join(pathWithoutCompletion, x.name), x.name)
                    {
                        HasTrailingSpace = true,
                    })
                    .ToList();
            }
        }

        // Add a trailing slash if it's the only one, since
        // there are no tab completions to scroll through
        // anyway and the user can continue tabbing directly.
        if (directories.Count == 1 && !files.Any())
        {
            directories[0] = new Completion(
                $"{directories[0].CompletionText}/",
                directories[0].DisplayText
            );
        }

        var completions = directories.Concat(files).ToList();
        if (completions.Count > 1 && path.Length > 0 &&
            !"./~".Contains(path.Last()))
        {
            completions.Insert(
                0,
                new Completion(
                    Path.Join(pathWithoutCompletion, completionTarget),
                    "..."
                )
            );
        }

        return completions;
    }

    public static string ExpandPath(string path)
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.StartsWith('~')
            ? homePath + path[1..]
            : path;
    }
}