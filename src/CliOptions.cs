using CommandLine;

namespace Shel;

class CliOptions
{
    [Value(0, MetaName = "file path", HelpText = "Path to the shel file that should be executed.")]
    public string? FilePath { get; set; }
}