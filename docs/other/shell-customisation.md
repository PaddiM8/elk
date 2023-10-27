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
fn elkPrompt() {
    print((env::prettyPwd | ansi::color blue) + " ❯ ")
}
```

### Alias

Aliases can be created with the syntax `alias name="value"` and removed with 
the syntax `unalias name`.

```elk
alias l="ls --color"
unalias l
```