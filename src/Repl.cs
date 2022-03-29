using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shel.Interpreting;

namespace Shel;

class Repl
{
    public static void Run()
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

            Console.Write(workingDirectory);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(" >> ");
            Console.ResetColor();
            string input = Console.ReadLine() ?? "";
            if (input == "exit")
                break;

            var result = interpreter.Interpret(input).ToString() ?? "";
            if (result == "" || result.EndsWith('\n'))
            {
                Console.Write(result);
            }
            else
            {
                Console.WriteLine(result);
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