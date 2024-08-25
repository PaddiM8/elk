using System;
using System.Collections.Generic;
using Elk.ReadLine.Render.Formatting;

namespace Elk.Cli.Setup;

public static class PromptHelper
{
    public static int PromptList(List<string> entries)
    {
        foreach (var (entry, i) in entries.WithIndex())
        {
            Console.Write(Ansi.Format($"{i + 1}. ", AnsiForeground.DarkGray));
            Console.WriteLine(entry);
        }

        int? choice = null;
        while (!choice.HasValue || choice > entries.Count || choice <= 0)
        {
            if (choice.HasValue)
                Console.WriteLine(Ansi.Format("Invalid number. Try again", AnsiForeground.DarkRed));

            Console.Write(Ansi.Format("Choice: ", AnsiForeground.Blue));
            if (int.TryParse(Console.ReadLine(), out var parsed))
                choice = parsed;
        }

        return choice.Value - 1;
    }
}