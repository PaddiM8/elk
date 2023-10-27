# Command Line Arguments

Command line arguments can be retrieved from the `argv` variable, which is a 
global variable available at all times. The value of this variable is a list of 
strings.

```elk
echo(argv[1])
```