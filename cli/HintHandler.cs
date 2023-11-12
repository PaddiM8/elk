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
        // For some reason TextArgumentsInfo.Arguments contains elements for the spaces
        // that separate the arguments. This should probably be fixed at some point.
        var completions = GetCompletions(
            string.Concat(invocationInfo.TextArgumentsInfo.Arguments).TrimStart(),
            completionTarget,
            invocationInfo,
            isTextArgument: activeTextArgument != null
        );
        var completion = completions.FirstOrDefault();
        if (completion == null)
            return "";

        var completionStart = completionTarget.LastIndexOf('/') + 1;
        var fileNameLength = completionTarget.Length - completionStart;
        if (fileNameLength >= completion.CompletionText.Length)
        {
            Debug.Assert(fileNameLength == completion.CompletionText.Length);

            return "";
        }

        var trailingSpace = completion.HasTrailingSpace
            ? " "
            : "";

        return Utils.Escape(completion.CompletionText[fileNameLength..]) + trailingSpace;
    }

    private IEnumerable<Completion> GetCompletions(
        string allTextArguments,
        string completionTarget,
        ShellStyleInvocationInfo? invocationInfo,
        bool isTextArgument)
    {
        var completionParser = invocationInfo == null || !isTextArgument
            ? null
            : _customCompletionProvider.Get(invocationInfo.Name);
        if (completionParser != null)
            return completionParser.GetCompletions(allTextArguments, null, CompletionKind.Hint);

        return FileUtils.GetPathCompletions(
            Utils.Unescape(completionTarget),
            _shell.WorkingDirectory,
            isTextArgument
                ? FileType.All
                : FileType.Executable,
            CompletionKind.Hint
        );
    }

    public void Reset()
    {
        _previousHadHistoryMatch = false;
        _previousPromptText = "";
    }
}