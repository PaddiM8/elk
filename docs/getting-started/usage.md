# Usage

## Running Code

When the Elk-program is executed without any command line arguments, it drops 
the user into the interactive shell mode. This allows you to type commands and 
get results immediately, like you would expect from a shell program. When a 
file path is given as a command line argument, the code in that file is 
evaluated.

```elk
elk someFile.elk
```

## Interactive Mode

The following shortcuts are available in interactive mode:

| Action                          | Shortcut         |
| ------------------------------- | ---------------- |
| Move cursor a word to the left  | Ctrl+Left Arrow  |
| Move cursor a word to the right | Ctrl+Right Arrow |
| Next in history                 | Up Arrow         |
| Previous in history             | Down arrow       |
| Move cursor to start            | Ctrl+A, Home     |
| Move cursor to end              | Ctrl+E, End      |
| Delete                          | Ctrl+D, Delete   |
| Remove to end                   | Ctrl+K           |
| Clear line                      | Ctrl+L           |
| Transpose characters            | Ctrl+T           |
| Remove to home                  | Ctrl+U           |
| Remove word to the left         | Ctrl+W           |
| Next autocomplete               | Tab              |
| Previous autocomplete           | Shift+Tab        |