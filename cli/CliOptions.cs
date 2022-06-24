using System.Collections.Generic;
using CommandLine;

namespace Elk.Cli;

class CliOptions
{
    [Value(0, MetaName = "file path", HelpText = "Path to the elk file that should be executed.")]
    public string? FilePath { get; set; }

    [Value(1, MetaName = "arguments", HelpText = "Arguments for the script.")]
    public IEnumerable<string>? Arguments { get; set; }
}