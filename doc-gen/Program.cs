using System;
using System.IO;
using Elk.DocGen;
using Elk.DocGen.Markdown;

if (args.Length != 2 || !File.Exists(args[0]) || !args[0].EndsWith(".xml"))
{
    Console.Error.WriteLine("Expected path to XML docs file and a path to the output directory. The XML file is generated when building a debug version of Elk.csproj, and can be found in the appropriate bin directory.");

    return;
}

MarkdownGenerator.Generate(new SymbolReader(args[0]).Read(), args[1]);