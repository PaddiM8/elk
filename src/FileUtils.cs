using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Elk.ReadLine;
using Mono.Unix;

namespace Elk;

public enum FileType
{
    All,
    Directory,
    Executable,
}

public static class FileUtils
{
    private static readonly Regex _formattingRegex = new("[{}()|$ ]");

    public static bool FileIsExecutable(string filePath)
    {
        var fileInfo = new UnixFileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.IsDirectory)
            return false;

        var permissions = fileInfo.FileAccessPermissions;
        if (permissions.HasFlag(FileAccessPermissions.OtherExecute))
            return true;

        if (permissions.HasFlag(FileAccessPermissions.UserExecute) &&
            UnixUserInfo.GetRealUserId() == fileInfo.OwnerUserId)
        {
            return true;
        }

        if (permissions.HasFlag(FileAccessPermissions.GroupExecute) &&
            UnixUserInfo.GetRealUser().GroupId == fileInfo.OwnerGroupId)
        {
            return true;
        }

        return false;
    }

    public static bool ExecutableExists(string name, string workingDirectory)
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (name.StartsWith('~'))
            name = name[1..] + homePath;

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
        if (!path.StartsWith("./") && !path.StartsWith("~/") && !path.StartsWith("/"))
            return false;

        if (path.StartsWith("~"))
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];

        var absolutePath = path.StartsWith("/")
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
        FileType fileType)
    {
        var lastSlashIndex = path
            .WithIndex()
            .LastOrDefault(x => x.item == '/' && path.ElementAtOrDefault(x.index - 1) != '\\')
            .index;
        // Get the full path of the folder, without the part that is being completed
        var fullPath = Path.Combine(
            workingDirectory,
            path[..lastSlashIndex]
        );
        var completionTarget = path.EndsWith("/")
            ? ""
            : path[lastSlashIndex..].TrimStart('/');

        if (!Directory.Exists(fullPath))
            return Array.Empty<Completion>();

        var includeHidden = path.StartsWith(".");
        var directories = Directory.GetDirectories(fullPath)
            .Select(Path.GetFileName)
            .Where(x => includeHidden || !x!.StartsWith("."))
            .Where(x => x!.StartsWith(completionTarget))
            .Order()
            .Select(x => FormatSuggestion(x!))
            .Select(x => new Completion(x, $"{x}/"))
            .ToList();

        IEnumerable<Completion> files = Array.Empty<Completion>();
        if (fileType != FileType.Directory)
        {
            files = Directory.GetFiles(fullPath)
                .Where(x => fileType != FileType.Executable || IsExecutable(x))
                .Select(Path.GetFileName)
                .Where(x => includeHidden || !x!.StartsWith("."))
                .Where(x => x!.StartsWith(completionTarget))
                .Order()
                .Select(x => FormatSuggestion(x!))
                .Select(x => new Completion(x));
        }

        if (!directories.Any() && !files.Any())
        {
            const StringComparison comparison = StringComparison.CurrentCultureIgnoreCase;
            directories = Directory.GetDirectories(fullPath)
                .Select(Path.GetFileName)
                .Where(x => x!.Contains(completionTarget, comparison))
                .Order()
                .Select(x => FormatSuggestion(x!))
                .Select(x => new Completion(x, $"{x}/"))
                .ToList();

            if (fileType != FileType.Directory)
            {
                files = Directory.GetFiles(fullPath)
                    .Where(x => fileType != FileType.Executable || IsExecutable(x))
                    .Select(Path.GetFileName)
                    .Where(x => x!.Contains(completionTarget, comparison))
                    .Order()
                    .Select(x => FormatSuggestion(x!))
                    .Select(x => new Completion(x));
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
            completions.Insert(0, new Completion(path));
        }

        return completions;
    }

    private static bool IsExecutable(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return true;

        var handle = File.OpenHandle(filePath);

        return File.GetUnixFileMode(handle)
            is UnixFileMode.OtherExecute
            or UnixFileMode.GroupExecute
            or UnixFileMode.UserExecute;
    }

    private static string FormatSuggestion(string completion)
        => _formattingRegex.Replace(completion, m => $"\\{m.Value}");
}