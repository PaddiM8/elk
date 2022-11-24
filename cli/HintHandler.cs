using System.Linq;
using BetterReadLine;
using Elk.Cli.Database;

namespace Elk.Cli;

class HintHandler : IHintHandler
{
    private readonly HistoryRepository _historyRepository;
    private readonly ShellSession _shell;
    private bool _previousHadMatch;
    private string _previousPromptText = "";
    
    public HintHandler(HistoryRepository historyRepository, ShellSession shell)
    {
        _historyRepository = historyRepository;
        _shell = shell;
    }

    public string Hint(string promptText)
    {
        // If the previous text had the same start and no results, don't do anything
        if (!_previousHadMatch && _previousPromptText.Any() && promptText.StartsWith(_previousPromptText))
            return "";
        
        var suggestion = _historyRepository.GetSingleWithPathAndStart(
            _shell.WorkingDirectory,
            promptText
        );
        _previousHadMatch = suggestion != null;
        _previousPromptText = promptText;
        
        return _previousHadMatch
            ? suggestion!.Content[promptText.Length..]
            : "";
    }

    public void Reset()
    {
        _previousHadMatch = false;
        _previousPromptText = "";
    }
}