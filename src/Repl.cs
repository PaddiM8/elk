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
        var interpreter = new Interpreter();
        while (true)
        {
            string workingDirectory = interpreter.ShellEnvironment.WorkingDirectory;
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (workingDirectory.StartsWith(homePath))
            {
                workingDirectory = "~" + workingDirectory[homePath.Length..];
            }

            /*Console.Write(workingDirectory);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(" >> ");
            Console.ResetColor();
            string input = Console.ReadLine() ?? "";*/
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

            var result = interpreter.Interpret(input, null);
            if (result is RuntimeNil)
                continue;

            string resultString = result.ToString() ?? "";
            if (resultString == "" || resultString.EndsWith('\n'))
            {
                Console.Write(resultString);
            }
            else
            {
                Console.WriteLine(resultString);
            }

            /*var expressions = Parsing.Parser.Parse(Lexing.Lexer.Lex(input), new GlobalScope());
            var settings = new JsonSerializerSettings {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.Objects,
                Converters = { new StringEnumConverter() },
            };
            Console.WriteLine(JsonConvert.SerializeObject(expressions, settings));*/
        }
    }
}