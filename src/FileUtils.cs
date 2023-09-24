using System;
using System.IO;
using System.Linq;
using Mono.Unix;

namespace Elk;

public class FileUtils
{
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
        string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (name.StartsWith('~'))
            name = name[1..] + homePath;

        if (name.StartsWith('.'))
        {
            string absolutePath = Path.Combine(workingDirectory, name);

            return FileIsExecutable(absolutePath);
        }

        if (name.StartsWith('/'))
            return FileIsExecutable(name);

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(":")
            .Any(x => Directory.Exists(x) && FileIsExecutable(Path.Combine(x, name))) is true;
    }
}