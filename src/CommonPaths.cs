using System;
using System.IO;

namespace Elk;

public static class CommonPaths
{
    public static string ConfigFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/elk");

    public static string InitFile =>
        Path.Combine(ConfigFolder, "init.elk");

    public static string PathFile =>
        Path.Combine(ConfigFolder, "path.txt");
}