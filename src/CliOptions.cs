using CommandLine;

namespace Elk;

class CliOptions
{
    [Value(0, MetaName = "file path", HelpText = "Path to the elk file that should be executed.")]
    public string? FilePath { get; set; }
}