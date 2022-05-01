using System;
using System.Threading.Tasks;
using PrettyPrompt;
using PrettyPrompt.Highlighting;
using Elk.Interpreting;

namespace Elk;

static class Repl
{
    public static async Task Run()
    {
        var shell = new ShellSession();
        while (true)
        {
            string workingDirectory = shell.WorkingDirectory;
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (workingDirectory.StartsWith(homePath))
            {
                workingDirectory = "~" + workingDirectory[homePath.Length..];
            }
            
            var promptConfiguration = new PromptConfiguration(
                maxCompletionItemsCount: 1,
                prompt: new FormattedString(
                    workingDirectory + " >> ",
                    new FormatSpan(workingDirectory.Length, workingDirectory.Length + 2, AnsiColor.Cyan)
                )
            );

            var prompt = new Prompt(configuration: promptConfiguration);
            var promptResult = await prompt.ReadLineAsync();
            string input = promptResult.Text;

            if (input == "exit")
                break;

            shell.RunCommand(input);
        }
    }
}