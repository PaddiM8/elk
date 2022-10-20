namespace Elk;

record DiagnosticInfo(int Line, int Column, string Message, string? FilePath)
{
    public override string ToString()
    {
            string message = $"[{Line}:{Column}] {Message}";

            return FilePath == null
                ? message
                : $"{FilePath} {message}";
    }
}