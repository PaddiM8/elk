# Program Invocation

The syntax for starting a program is the same as the syntax for function calls. 
In Elk, function calls and program invocations are the exact same thing 
syntactically. Read more about how this works in 
[Functions](functions-and-structs#usage). It is also possible to specify a path in order 
to run a program. Characters in paths can be escaped using a backslash.

Program invocations always return a [pipe](data-types#pipe) 
object. Iterating over a Pipe yields one line at a time.

```elk
echo("hello world")
echo hello world
cat("file") | grep("line")
cat file | grep line

./someScript.sh
../someOtherScript.sh(cat file)
```

::: info
Keep in mind that all arguments that are given to a program invocation are 
converted into string values.
:::

### Exec

The `exec` function is used to invoke a program by passing a string value as 
the name. This is useful when invoking programs with names containing special 
characters, in order to avoid having to escape all the characters using 
backslashes.

```elk
exec "../some script|with&&a weirdname" arg1 arg2
```

## Standard Output Redirection

In Elk, it is not necessary to be explicit about when the standard output of a 
process should be redirected. Instead, redirection happens automatically when 
the value of a program invocation expression is used in some way. In short, 
this means that standard output is captured automatically unless the program 
invocation expression is a free-standing line in the global scope or in a 
block. If it is the last line of a block, redirection only happens if the value 
of that block is used.

```elk
# redirection happens, the output of `echo` is captured
# and therefore not printed to the terminal
let hello = echo hello world
assert(hello == "hello world")

# no redirection, gets printed to the terminal
echo hello world

fn files(path) {
    ls(path)
}

 # the result of `ls` is saved to the variable and
 # therefore prevented from being printed to the terminal
let a = files("/bin")

# the result of `ls` is not used, meaning standard output
# is not captured and instead printed to the terminal
files("/bin")
```