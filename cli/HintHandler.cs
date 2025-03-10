using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Elk.ReadLine;
using Elk.Cli.Database;
using Elk.Services;
using Elk.Std.Serialization.CommandLine;

namespace Elk.Cli;

class HintHandler(
    HistoryRepository historyRepository,
    ShellSession shell,
    HighlightHandler highlightHandler,
    CustomCompletionProvider customCompletionProvider)
    : IHintHandler
{
    private bool _previousHadHistoryMatch;
    private string _previousPromptText = "";

    public string Hint(string promptText)
    {
        // If the previous text had the same start and no results, don't do anything
        if (!_previousHadHistoryMatch && _previousPromptText.Any() && promptText.StartsWith(_previousPromptText))
            return GetFileHint();

        var suggestion = historyRepository.GetSingleWithPathAndStart(
            shell.WorkingDirectory,
            promptText
        );
        _previousHadHistoryMatch = suggestion != null;
        _previousPromptText = promptText;

        if (suggestion == null || promptText.Length >= suggestion.Content.Length)
            return string.Empty;

        return _previousHadHistoryMatch
            ? suggestion!.Content[promptText.Length..]
            : GetFileHint();
    }

    private string GetFileHint()
    {
        var invocationInfo = highlightHandler.Highlighter.LastShellStyleInvocations.LastOrDefault();
        if (invocationInfo == null)
            return "";

        var arguments = invocationInfo.TextArgumentsInfo.Arguments;
        var argumentIndex = invocationInfo.TextArgumentsInfo.ActiveArgumentIndex;
        if (argumentIndex != arguments.Count - 1)
            return "";

        var activeTextArgument = arguments.ElementAtOrDefault(argumentIndex);
        if (activeTextArgument == "")
            return "";

        var completionTarget = activeTextArgument ?? invocationInfo.Name;
        // For some reason TextArgumentsInfo.Arguments contains elements for the spaces
        // that separate the arguments. This should probably be fixed at some point.
        var completions = GetCompletions(
            string.Concat(arguments).TrimStart(),
            completionTarget,
            invocationInfo,
            isTextArgument: activeTextArgument != null
        );
        var completion = completions.FirstOrDefault();
        if (completion == null)
            return "";

        if (completionTarget.Length >= completion.CompletionText.Length)
        {
            Debug.Assert(completionTarget.Length == completion.CompletionText.Length);

            return "";
        }

        var trailingSpace = completion.HasTrailingSpace
            ? " "
            : "";

        return Utils.Escape(completion.CompletionText[completionTarget.Length..]) + trailingSpace;
    }

    private IEnumerable<Completion> GetCompletions(
        string allTextArguments,
        string completionTarget,
        ShellStyleInvocationInfo? invocationInfo,
        bool isTextArgument)
    {
        var completionParser = invocationInfo == null || !isTextArgument
            ? null
            : customCompletionProvider.Get(invocationInfo.Name);
        if (completionParser != null)
            return completionParser.GetCompletions(allTextArguments, null, CompletionKind.Hint);

        return FileUtils.GetPathCompletions(
            Utils.Unescape(completionTarget),
            shell.WorkingDirectory,
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