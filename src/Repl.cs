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
        // TODO: 'exit' command
        var interpreter = new Interpreter();
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(">> ");
            Console.ResetColor();
            string input = Console.ReadLine() ?? "";
            var result = interpreter.Interpret(input).ToString() ?? "";
            if (result == "" || result.EndsWith('\n'))
            {
                Console.Write(result);
            }
            else
            {
                Console.WriteLine(result);
            }
            /*var expressions = Parser.Parse(input);
            var settings = new JsonSerializerSettings {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.Objects,
                Converters = { new StringEnumConverter() },
            };
            Console.WriteLine(JsonConvert.SerializeObject(expressions, settings));*/
        }
    }
}