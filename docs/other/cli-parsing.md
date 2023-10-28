# CLI Parsing

Command line arguments can be retrieved using the `getArgv` function, which
returns a list of strings, or a single string if an index was specified.

```elk
echo(getArgv)
echo(getArgv 1)
```

## Parsing

The functions in the [cli](/std/cli/index) module can be used to parse command
line arguments. These functions make it possible to parse things like
flags and verbs and will automatically generate help text for when the
`--help` flag is used.

```elk
cli::create my-program
    | cli::setDescription "My program"
    | cli::addVerb add => &handleAdd
    | cli::addArgument({
        "identifier": "path",
        "valueKind": "directory",
    })
    | cli::addFlag({
        "identifier": "version",
        "short": "v",
        "long": "version",
        "description": "display the version"
    })
    | cli::setAction => result: {
        if "version" in result:
            println v1.0.0

        if "path" in result:
            println(result->path)
    }
    | cli::parseArgv

fn handleAdd(parser) {
    parser
        | cli::setDescription "Add a file"
        | cli::addArgument({ "identifier": "file", "valueKind": "path" })
        | cli::setAction => result: {
            if "file" in result:
                println("File:", result->file)
        }
}
```
