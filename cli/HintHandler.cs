using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Elk.ReadLine;
using Elk.Cli.Database;
using Elk.Std.DataTypes.Serialization.CommandLine;

namespace Elk.Cli;

class HintHandler : IHintHandler
{
    private readonly HistoryRepository _historyRepository;
    private readonly ShellSession _shell;
    private readonly HighlightHandler _highlightHandler;
    private readonly CustomCompletionProvider _customCompletionProvider;
    private bool _previousHadHistoryMatch;
    private string _previousPromptText = "";

    public HintHandler(
        HistoryRepository historyRepository,
        ShellSession shell,
        HighlightHandler highlightHandler,
        CustomCompletionProvider customCompletionProvider)
    {
        _historyRepository = historyRepository;
        _shell = shell;
        _highlightHandler = highlightHandler;
        _customCompletionProvider = customCompletionProvider;
    }

    public string Hint(string promptText)
    {
        // If the previous text had the same start and no results, don't do anything
        if (!_previousHadHistoryMatch && _previousPromptText.Any() && promptText.StartsWith(_previousPromptText))
            return GetFileHint();

        var suggestion = _historyRepository.GetSingleWithPathAndStart(
            _shell.WorkingDirectory,
            promptText
        );
        _previousHadHistoryMatch = suggestion != null;
        _previousPromptText = promptText;

        return _previousHadHistoryMatch
            ? suggestion!.Content[promptText.Length..]
            : GetFileHint();
    }

    private string GetFileHint()
    {
        var invocationInfo = _highlightHandler.LastShellStyleInvocations.LastOrDefault();
        var activeTextArgument = invocationInfo?.TextArgumentsInfo.Arguments
            .ElementAtOrDefault(invocationInfo.TextArgumentsInfo.CaretAtArgumentIndex);
        if (activeTextArgument == "" || invocationInfo == null)
            return "";

        var completionTarget = activeTextArgument ?? invocationInfo.Name;
        var completions = GetCompletions(
            completionTarget,
            invocationInfo,
            isTextArgument: activeTextArgument != null
        );
        var fullPathCompletion = completions.FirstOrDefault();
        if (fullPathCompletion == null)
            return "";

        var completionStart = completionTarget.LastIndexOf('/') + 1;
        var fileNameLength = completionTarget.Length - completionStart;
        if (fileNameLength >= fullPathCompletion.Length)
        {
            Debug.Assert(fileNameLength == fullPathCompletion.Length);

            return "";
        }

        return Utils.Escape(fullPathCompletion[fileNameLength..]);
    }

    private IEnumerable<string> GetCompletions(
        string completionTarget,
        ShellStyleInvocationInfo? invocationInfo,
        bool isTextArgument)
    {
        var completionParser = invocationInfo == null || !isTextArgument
            ? null
            : _customCompletionProvider.Get(invocationInfo.Name);
        if (completionParser != null)
        {
            return completionParser
                .GetCompletions(completionTarget, null, CompletionKind.Hint)
                .Select(x => x.CompletionText);
        }

        var fileCompletions = FileUtils.GetPathCompletions(
            Utils.Unescape(completionTarget),
            _shell.WorkingDirectory,
            isTextArgument
                ? FileType.All
                : FileType.Executable,
            CompletionKind.Hint
        );

        return fileCompletions.Select(x => x.CompletionText);
    }

    public void Reset()
    {
        _previousHadHistoryMatch = false;
        _previousPromptText = "";
    }
}