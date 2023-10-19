using System.IO;
using System.Reflection;

namespace Elk;

public static class EmbeddedResourceProvider
{
    private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

    public static string? ReadAllText(string name)
    {
        var assemblyName = _assembly.GetName().Name!;
        using var stream = _assembly.GetManifestResourceStream($"{assemblyName}.Resources.{name}");
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}