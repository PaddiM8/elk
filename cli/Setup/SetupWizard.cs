using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.ReadLine.Render.Formatting;
using Elk.Scoping;
using Elk.Std.DataTypes;
using Elk.Vm;

namespace Elk.Cli.Setup;

class SetupWizard
{
    private List<Action> Sections { get; set; } = [];

    private readonly SetupState _state = new();
    private int _currentSection;

    public static void Run()
    {
        var wizard = new SetupWizard();
        wizard.Sections =
        [
            wizard.BranchSymbol,
            wizard.Prompt,
            wizard.Done,
        ];

        while (wizard.Next())
        {
        }
    }

    private bool Next()
    {
        Console.Clear();
        Console.WriteLine(Ansi.Format("Welcome to the elk shell setup wizard!", AnsiForeground.Blue));
        Console.WriteLine(
            Ansi.Format(
                $"{_currentSection + 1} / {Sections.Count}",
                AnsiForeground.DarkGray
            )
        );

        Sections[_currentSection].Invoke();
        _currentSection++;

        return _currentSection < Sections.Count;
    }

    private void BranchSymbol()
    {
        Console.WriteLine("Which symbol would you like to be shown next to git branch names in the shell prompt?");
        List<string> choices =
        [
            "Don't show branch names",
            "|",
            "\ue0a0",
        ];
        var choiceIndex = PromptHelper.PromptList(choices);
        _state.BranchSymbol = choiceIndex == 0
            ? null
            : choices[choiceIndex];
    }

    private void Prompt()
    {
        Console.WriteLine("What do you want your prompt to look like? You can easily tweak it later.");

        var shellSession = new ShellSession(
            new RootModuleScope(null, null),
            new VirtualMachineOptions()
        );
        var promptScriptPaths = ResourceProvider.GetFolderEntries("prompts")
            .Order()
            .ToList();
        var prompts = new List<string>();

        // Prepare an example path with enough depth to show off env::prettyPwd
        var previousWorkingDirectory = ShellEnvironment.WorkingDirectory;
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "elk/logs"
        );
        Directory.CreateDirectory(appDataPath);
        ShellEnvironment.WorkingDirectory = appDataPath;

        foreach (var promptPath in promptScriptPaths)
        {
            var scriptContent = _state.GenerateElkPromptFunction(promptPath);
            shellSession.RunCommand(scriptContent);

            var prompt = shellSession.RunFunction("elkPrompt", Array.Empty<RuntimeObject>());
            prompts.Add(prompt.ToString() ?? "");
        }

        ShellEnvironment.WorkingDirectory = previousWorkingDirectory;
        if (Directory.GetFiles(appDataPath).Length == 0)
            Directory.Delete(appDataPath);

        var choiceIndex = PromptHelper.PromptList(prompts);
        _state.PromptScriptPath = promptScriptPaths[choiceIndex];
    }

    private void Done()
    {
        var configPath = Path.Combine(CommonPaths.ConfigFolder, "init.elk");
        if (File.Exists(configPath))
        {
            Console.WriteLine("Warning: An init.elk file already exists. The new configuration will be written to a different file.");
            configPath = Path.Combine(CommonPaths.ConfigFolder, "init.new.elk");
        }

        _state.GenerateInitFile(configPath);
        Console.WriteLine($"Done! Configuration written to {configPath}");
    }
}