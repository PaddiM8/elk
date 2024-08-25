using System;
using System.IO;
using Regex = System.Text.RegularExpressions.Regex;

namespace Elk.Cli.Setup;

public class SetupState
{
    public string? BranchSymbol { get; set; }

    public string? PromptScriptPath { get; set; }

    public string GenerateElkPromptFunction(string? promptScriptPath = null)
    {
        promptScriptPath ??= PromptScriptPath;
        if (promptScriptPath == null)
            throw new ArgumentException("Expected prompt script path.");

        var promptScript = ResourceProvider.ReadFile(promptScriptPath)!;
        if (BranchSymbol == null)
        {
            promptScript = Regex.Replace(promptScript, @"[ ]*{{ GIT_BRANCH_VAR }}\n?", "");
            promptScript = Regex.Replace(promptScript, @"[ ]*{{ GIT_BRANCH_SECTION }}\n?", "");
        }
        else
        {
            const string branchVarCode = """
            let branch = git branch
                    |all where => &str::startsWith("* ")
                    | map => x: x[2..]
                    | iter::firstOrNil
                    || nil
                    
            """;
            var branchSectionCode = $"ansi::color(\" {BranchSymbol} ${{branch}}\", \"magenta\") if branch,";
            promptScript = promptScript.Replace("{{ GIT_BRANCH_VAR }}", branchVarCode);
            promptScript = promptScript.Replace("{{ GIT_BRANCH_SECTION }}", branchSectionCode);
        }

        return promptScript;
    }

    public void GenerateInitFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var scriptContent = GenerateElkPromptFunction();
        File.WriteAllText(path, scriptContent);
    }
}