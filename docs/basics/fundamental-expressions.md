# Fundamental Expressions

Note: The syntax for eg. dictionaries is documented in [Data 
Types](data-types.md).

## Arithmetic

| Operation      | Symbol |
| -------------- | ------ |
| Addition       | `+`    |
| Subtraction    | `-`    |
| Negation       | `-`    |
| Multiplication | `*`    |
| Division       | `/`    |
| Power          | `^`    |
| Modulo         | `%`    |

:::info
These operators need to be surrounded by whitespace in order
to be parsed correctly. This is because identifiers can contain
these kind of symbols in shell languages.
:::

## Boolean Operations

| Operation                       | Symbol |
| ------------------------------- | ------ |
| Logical and                     | `and`  |
| Logical or                      | `or`   |
| Logical not                     | `not`  |
| Non-redirecting and (bash-like) | `&&`   |
| Non-redirecting or (bash-like)  | `\|\|` |

::: info
The `and`/`or` keywords are used for logical operations on booleans, while the 
non-redirecting variants (`&&` and `||`) are used to chain program invocations.
:::

```elk
cd dir1 && mkdir dir2
echo(x and y)
```

## Comparisons

| Operation        | Symbol |
| ---------------- | ------ |
| Equals           | `==`   |
| Not equals       | `!=`   |
| Greater          | `>`    |
| Less             | `<`    |
| Greater or equal | `>=`   |
| Less or equal    | `<=`   |
| Contains         | `in`   |

## Blocks

A block is a single expression that can contain several other expressions that 
are evaluated consecutively. The value of a block is always the value of the 
last expression in that block.

```elk
let x = {
    "one"
    "two"
}

assert(x == "two")
```

## Pipes

Pipe expressions are written with the syntax value `| receiver` where `value` 
is the value that should be sent to the `receiver` function or program. The 
behaviour is different depending on whether the receiving side is a function 
call or a program invocation.

For program invocations, it is also possible to specify which output stream
should be redirected, using `|err` to only redirect stderr or `|all` to
redirect both stdout and stderr.

### With Function Calls

When the receiving side is a function call, the pipe expression's `value` is 
prepended to the call's argument list, meaning it becomes the first argument of 
the call.

```elk
"1,2,3" | split(",")
```

is equivalent to

```elk
split("1,2,3", ",")
```

### With Program Invocations

When the receiving side is a program invocation, the pipe expression's value is 
sent to the standard input of the process created by the invocation. This 
behaviour can be compared to how pipes work in Bash.

```elk
cat file.txt | grep line
some-program |err write errors.txt
another-program |all write all-output.txt
```