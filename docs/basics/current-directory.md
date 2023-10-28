# Current Directory

The current directory can be changed using the built-in `cd` command, just like 
with other shells. The `$PWD` environment variable can be accessed to get the 
path to the current directory. Further more, `cd -` changes to the previous directory.

```elk
cd directory
echo($PWD)
cd ..
```

## Script Path

The path of the folder containing the currently executed script can be 
retrieved by calling the `scriptPath` function.

```elk
assert(scriptPath() == "/home/user/scripts")
```