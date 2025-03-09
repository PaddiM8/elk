# Data Types

The following page describes the most important data types. Some types may not be described here.

## Boolean

Booleans are expressed using the `true` and `false` keywords.

### Conversions

| Target type | Resulting value       |
| ----------- | --------------------- |
| String      | `"true"` or `"false"` |

## Dictionary

Dictionaries represent key-value pairs with unique keys. This can be expressed 
by putting comma-separated key-value pairs inside braces, such as `{ key1: 
value1, key2: value2 }`. Indexing is done with square brackets, eg. 
`dict["key1"]` or with the indexing operator, eg. `dict->key1`.

```elk
let dict = {
    "key1": "x",
    "key2": "y",
}
assert(dict["key1"] == "x")
assert(dict->key2 == "y")
```

### Conversions

| Target type | Resulting value                                             |
| ----------- | ----------------------------------------------------------- |
| Boolean     | `true` if the dictionary is non-empty                       |
| String      | A string with comma-separated key-value pairs inside braces |

### Iteration

When a dictionary is iterated over, a tuple containing the key and the value is 
given.

```elk
for key, value in dict: echo("${key}: ${value}")
```

## Error

The error type is used to represent errors and contains a string with an error 
message. An error message can be created using the `error` function, and the 
message can be retrieved by calling the `message` function.

```elk
let err = error("some error message")
if err | isType(Error): assert(message(err) == "some error message")
```

### Conversions

| Target type | Resulting value |
| ----------- | --------------- |
| Boolean     | `false`         |

## Float

Floats represent 64-bit floating point numbers and are created from number 
literals that contain decimals.

### Conversions

| Target type | Resulting value                    |
| ----------- | ---------------------------------- |
| Boolean     | `true`if the value is non-zero     |
| Integer     | The number as an Integer (floored) |
| String      | A string value                     |

## Integer

Integers represent 64-bit integer numbers and are created from number literals 
that do _not_ contain decimals.

### Conversions

| Target type | Resulting value                |
| ----------- | ------------------------------ |
| Boolean     | `true`if the value is non-zero |
| Float       | The number as a float          |
| String      | A string value                 |

## List

A list is a mutable dynamic data structure that can contain several values. 
Values are separated by commas. Indexing is done with square brackets and 
starts at zero. It is possible to index a list based on a range value in order 
to get a sub-list.

```elk
let items = [1, 2, 3]
assert(items[0] == 1)
items | add(4)
items | remove(0)

for item in items: echo(item)
```

### Conversions

| Target type | Resulting value                                                                                                    |
| ----------- |--------------------------------------------------------------------------------------------------------------------|
| Boolean     | `true`if the list is non-empty                                                                                     |
| String      | A string containing the string representations of the values separated by commas and surrounded by square brackets |

## Nil

Nil values are represented by the `nil` keyword.

### Conversions

| Target type | Resulting value |
| ----------- | --------------- |
| Boolean     | `false`         |

## Generator

A Generator represents lazily evaluated values. A Generator can be collected with the function `iter::collect`.

```elk
# prints "hello" indefinitely
# `iter::repeat` returns a generator
for x in iter::repeat("hello") {
    println(x)
}
```

### Conversions

| Target type | Resulting value                |
|-------------|--------------------------------|
| List        | A list of the collected values |
| Boolean     | `true`                         |

## Pipe

Pipe objects are returned by program invocations and let you iterate over the 
program's stdout/stderr (depending on if it was piped with `|`, `|err` or 
`|all`). When iterating over a Pipe, the output stream is consumed line by 
line, but also buffered inside the object itself in order to make it possible 
to access these values later. Pipes can be implicitly converted to strings.

::: info
Normally, the content of a Pipe is kept in memory for as long as the Pipe 
exists. However, when a Pipe object is piped to a program invocation or an std 
call that consumes it immediately, data is not stored in the Pipe, since it is 
consumed immediately anyway.

This means that you do not have to worry about excessive memory usage in these 
cases.
:::

```elk
# `cat` returns a Pipe
# `map` will iterate over the file content line by line 
cat file.txt | map => x: x + "!"

# gets the 6th line
cat("file.txt")[5] | println

# converts the Pipe into a string and then applies str::upper
cat file.txt | str::upper
```

### Conversions

| Target type | Resulting value                                                                                                         |
|-------------|-------------------------------------------------------------------------------------------------------------------------|
| String      | The output lines concatenated                                                                                           |
| List        | A list of lines                                                                                                         |
| Boolean     | `true` for non-empty Pipes                                                                                              |
| Integer     | An integer value of the number represented in the output. A runtime error is thrown if the string is not a valid number. |
| Float       | A float value of the number represented in the output. A runtime error is thrown if the string is not a valid number.   |
| Regex       | A Regex object                                                                                                          |

### Iteration

Iterating over a Pipe yields the individual lines.

## Range

A range expresses a numerical range between two values. The syntax for a 
regular range is `x..y` where `x` and `y`are any type of expressions. Both 
expressions are expected to be integer values. When the first value is 
excluded, eg. `..y`, the range will start at 0. When the last value is 
excluded, eg. `x..`, the range will not have a defined end.

Regular ranges are exclusive, meaning they don't include the value of the 
second expression. Iterating over the range `0..10` only yields the numbers 0 
to 9. To instead make a range inclusive, an equal sign is added after the dots, 
eg. `0..=10`.

::: info
It is sometimes necessary to place a range inside parentheses, such as `(1..)` 
to avoid expressions around it to be parsed as a part of the range.
:::

```elk
for i in 0..10: echo(i)
let list = [1, 2, 3, 4, 5]
echo(list[1..3])
```

### Conversions

| Target type | Resulting value                                                                                 |
|-------------|-------------------------------------------------------------------------------------------------|
| Boolean     | `true`                                                                                          |
| List        | A list containing all the numbers of the range                                                  |
| String      | A string of the format `x..y` where `x` and `y`and the starting and ending values of the range. |

### Iteration

Iterating over a range yields every number in the range.

## Regex

A compiled regex pattern can be built by putting a pattern inside two slashes. 
Functions from the regex module and others can then be used together with the 
regex value.

```elk
# the regex functions take a Regex object, but
# will also implicitly convert strings to Regex
"abcdabdeghab" | re::findAll '[abc]'

# if you're going to use a pattern several times,
# it's better to create the Regex object immediately
let pattern = into::regex('[abc]')
"abcdabdeghab" | re::findAll(pattern)
```

### Conversions

| Target type | Resulting value                                                       |
| ----------- |-----------------------------------------------------------------------|
| String      | The string representation of the regex pattern including the slashes. |

## Set

A set represents a collection of unique unordered values with hashing.

```elk
2 in { "a", 2, true } #=> true
let s = into::set()
s | push(5)
```

## String

Strings represent text values and are created by surrounding text with double 
quotes or single quotes. Backslashes are used to escape characters. However, in 
single quote literals, it is only possible to escape single quotes. String 
operations are done immutably, but some standard library functions may result 
in mutation.

### Conversions

| Target type | Resulting value                                                                                                          |
|-------------|--------------------------------------------------------------------------------------------------------------------------|
| Boolean     | `true` for non-empty strings                                                                                             |
| Integer     | An integer value of the number represented in the string. A runtime error is thrown if the string is not a valid number. |
| Float       | A float value of the number represented in the string. A runtime error is thrown if the string is not a valid number.    |
| Regex       | A Regex object |                                                                                                          |

### Escape Sequences

| Escape sequence | Symbol                      |
| --------------- | --------------------------- |
| \b              | Backspace                   |
| \f              | Form feed                   |
|                 | New line                    |
|                 | Carriage return             |
|                 | Horizontal tab              |
| \v              | Vertical tab                |
| \0              | Null                        |
| \x...           | Unicode character, eg. \x1b |

### Interpolation

String interpolation is done by surrounding code with braces preceded by a
dollar sign in a string literal, for example `"Value: ${x + 1}"`. This is only
possible with double quote strings. Environment variables can also be interpolated,
for example `Var: $VAR`.

### Iteration

Iterating over a string yields each character in the string, one by one.

## Tuple

A tuple is an immutable data structure containing values. A tuple is created by
surrounding comma-separated values with parentheses.

```elk
let a, b = (1, 2)
```

### Conversions

| Target type | Resulting value                                                                                                |
| ----------- |----------------------------------------------------------------------------------------------------------------|
| Boolean     | `true`if the tuple is non-empty                                                                                |
| List        | A list containing the same values                                                                              |
| String      | A string containing the string representations of the values separated by commas and surrounded by parentheses |

## Type

A type in Elk is a static value that represents a data type. It is created by
writing the name of the data type.

```elk
assert("some string" | isType(String))
let dataType = Integer;
```

### Conversions

| Target type | Resulting value             |
| ----------- | --------------------------- |
| Boolean     | `true`                      |
| String      | A string of the type's name |
