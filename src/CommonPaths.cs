#region

using System;
using System.IO;

#endregion

namespace Elk;

public static class CommonPaths
{
    public static string ConfigFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "elk");

    public static string InitFile =>
        Path.Combine(ConfigFolder, "init.elk");

    public static string PathFile =>
        Path.Combine(ConfigFolder, "path.txt");
}