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

An interactive shell session can be started by running `elk` without any
command line arguments. The following shortcuts are available:

| Key Combination        | Action                                                                           |
| ---------------------- |----------------------------------------------------------------------------------|
| Left Arrow, Ctrl+B     | Move cursor left                                                                 |
| Right Arrow, Ctrl+F    | Move cursor right, Apply hint                                                    |
| Up Arrow, Ctrl+P       | Next item in history (that starts with the text currently in the prompt, if any) |
| Down Arrow, Ctrl+N     | Previous item in history                                                         |
| Ctrl+Left Arrow        | Move one word to the left                                                        |
| Ctrl+Right Arrow       | move one word to the right                                                       |
| Home, Ctrl+A           | Move to the start of the line                                                    |
| End, Ctrl+E            | Move to the end of the line                                                      |
| Backspace              | Remove the character to the left                                                 |
| Delete                 | Remove the character to the right                                                |
| Ctrl+Backspace, Ctrl+W | Remove the word to the left                                                      |
| Ctrl+L                 | Clear the console                                                                |
| Ctrl+U                 | Remove everything to the left                                                    |
| Ctrl+R                 | Search in history                                                                |
| Tab                    | Next tab completion                                                              |
| Shift+Tab              | Previous tab completion                                                          |

### Multi-Line Input

There are several ways to insert a new line into the command line prompt
without submitting:

* Leave a brace unclosed and press enter, eg. `if x {`
* Leave a string literal unclosed and press enter, eg. `"hello world`
* Press enter after typing a `|`symbol (this will also put the pipe on a new
  line)
* Press enter after typing a `\` symbol
