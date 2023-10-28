# Shell Customisation

### Init File

Elk looks for the init file in `~/.config/elk/init.elk`, and runs it when 
starting a new shell session if it exists. The init file can be compared to the 
`.bashrc` file used in bash.

### Custom Prompt

Elk generates the prompt by calling a function called `elkPrompt`. This 
function can be re-defined in the init file in order to customise the prompt. 
The default implementation of the `elkPrompt` function is the following:

```elk
# ~/.config/elk/init.elk
alias ls = "ls --color"
let gitExists = file::executableExists git

fn elkPrompt() {
    let branch = if gitExists {
        git branch
            |all where => &str::startsWith("* ")
            | map => x: x[2..]
            | iter::first
    }

    let pwd = env::prettyPwd | ansi::color blue
    if branch {
        let formattedBranch = " ${branch}" | ansi::color magenta
        print(pwd, formattedBranch, "❯ ")
    } else {
        print(pwd, "❯ ")
    }
}
```

### Alias

Aliases can be created with the syntax `alias name="value"` and removed with 
the syntax `unalias name`.

```elk
alias l="ls --color"
unalias l
```

### Custom Completions

The [cli](/std/cli/index) module can be used to set up custom completions for the
interactive shell. In order to register a file specifying completions for a
program, it can be added to the `~/.config/elk/completions` directory.
The completions will then automatically be loaded when a program with the
same name as the file is used. In order for this to work,
`cli::registerForCompletion` needs to be invoked in the file as well.
The built-in completions can be found [in the repository](https://github.com/PaddiM8/elk/tree/main/src/Resources/Completions).

Below is a trimmed down example of the custom completions for `git`:

```elk
# ~/.config/elk/completions/git.elk

cli::create git
    | cli::registerForCompletion
    | cli::addVerb add => &handleAdd
    | cli::addArgument({ "valueKind": "path", "variadic": true })
    | cli::addFlag({ "short": "v", "long": "version", "description": "display git version" })

fn handleAdd(parser) {
    parser
        | cli::addArgument({ "identifier": "file", "completionHandler": &unstagedFilesHandler, "variadic": true })
}

fn unstagedFilesHandler(value, state) {
    let repoPath = git rev-parse --show-toplevel
    git ls-files ${repoPath} --exclude-standard --others --modified | str::path::fuzzyFind(value)
}
```