using System;
using System.IO;
using Elk.Interpreting;

namespace Elk;

public class ShellSession
{
    public string WorkingDirectory
        => _interpreter.ShellEnvironment.WorkingDirectory;
    
    private readonly Interpreter _interpreter = new();
    
    public ShellSession()
    {
        Init();
    }

    private void Init()
    {
        string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string initFilePath = Path.Combine(homePath, ".config/elk/init.elk");
        if (File.Exists(initFilePath))
        {
            RunFile(initFilePath);
        }
    }

    public void RunCommand(string command)
    {
        var result = _interpreter.Interpret(command, null);
        if (result is RuntimeNil)
            return;

        string resultString = result.ToString() ?? "";
        if (resultString == "" || resultString.EndsWith('\n'))
        {
            Console.Write(resultString);
        }
        else
        {
            Console.WriteLine(resultString);
        }
    }

    public void RunFile(string filePath)
    {
        _interpreter.Interpret(File.ReadAllText(filePath), filePath);
    }
}