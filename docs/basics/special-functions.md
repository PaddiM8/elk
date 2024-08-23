# Special Functions

## call

## cd
The current directory can be changed using the built-in `cd` command, just like 
with other shells. The `$PWD` environment variable can be accessed to get the 
path to the current directory. Further more, `cd -` changes to the previous directory.

```elk
cd directory
echo($PWD)
cd ..
```

## exec

Described in [program-invocation#exec](/basics/program-invocation#exec).

## scriptPath

The path of the folder containing the currently executed script can be 
retrieved by calling the `scriptPath` function.

```elk
assert(scriptPath() == "/home/user/scripts")
```

## time

The `time` function measures how long it takes for the closure to
evaluate.

```elk
time =>: sleep 3
```

## __onExit (user-defined)

If an `__onExit` function is defined in a script, it will be called automatically
before the program exits.

```elk
echo hi

fn __onExit() {
    echo bye
}
```