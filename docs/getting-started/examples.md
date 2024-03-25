# Examples

* [A sokoban game](https://github.com/PaddiM8/elk/tree/main/examples/sokoban)
* [Advent of Code 2022](https://github.com/PaddiM8/elk/tree/main/examples/advent-of-code-2022)

## Hello World

There are a few ways to write a hello world program:

```elk
# using the 'echo' program
echo hello world
echo "hello world"
echo("hello world")

# using the built-in 'println' function
println hello world
println "hello world"
println("hello world")
```

## Pipes

```elk
let hid = dmesg | grep HID
for line in hid: echo(line | ansi::color blue)
```

## String Interpolation

```elk
let kernel = uname()
echo("Kernel: ${kernel}")
echo Kernel: ${kernel}
```

## User Input

```elk
using ansi

let name = io::input("Name ${$USER}: " | color green) or $USER
let createFolder = io::input("Create folder? (y/N) " | color green) or "n"
    | str::trim
    | str::lower
```

### Environment Variables for Process

```elk
VAR1=value1, VAR2="another value": ./some-script.sh
```

## Tables

```elk
~/repo ❯ du -h | head -n 10 | parse::table | into::list
[
    ["60K", "./.git/hooks"],
    ["4.0K", "./.git/info"],
    ["16K", "./.git/logs/refs/heads"],
    ["8.0K", "./.git/logs/refs/remotes/origin"],
    ["8.0K", "./.git/logs/refs/remotes"],
    ["28K", "./.git/logs/refs"],
    ["48K", "./.git/logs"],
    ["16K", "./.git/objects/00"],
    ["8.0K", "./.git/objects/02"],
]
```

## Misc

```elk
let sizes = du | str::column 0 | map => &into::int
println Sizes:
sizes | map => x: x / 1000 | join ", " | println
```