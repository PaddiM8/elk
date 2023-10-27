# Functions

## Declaration

Functions are declared with the syntax `fn functionName(param1, param2) {}` or 
`fn functionName(param1, param2): expression` where there can be an arbitrary 
amount of parameters. With the former, the return value is either the value of 
the block or a value passed to a `return` expression. With the latter, the 
return value is simply the value of the expression after the colon.

```elk
fn add(x, y) {
    x + y
}
```

is equivalent to

```elk
fn add(x, y) {
    return x + y
}
```

and

```elk
fn add(x, y): x + y
```

### Default Parameters

Function parameters can be assigned a default value by putting an equal sign 
followed by a value after the parameter name. This makes the parameter 
optional. If an argument is not given for an optional parameter, the default 
value is used instead. Parameters with default values must be at the end of the 
parameter list.

```elk
fn greet(name = nil) {
    println(if name: "Hello {name}!" else "Hello!")
}
```

### Variadic

A function that can take an arbitrary amount of arguments is declared by 
putting three dots at the end of the last parameter.

```elk
fn add(values...) {
    let sum = 0
    for value in values: sum += value
    sum
}
```

## Usage

There are two different ways to express a function call: parenthesised and 
shell-style.

### Parenthesised

The syntax for parenthesised function calls is similar to that of 
general-purpose languages like Python. Arguments can be of any data type.

```elk
someFunction("hello", 3/2)
```

### Shell-style

Shell-style function calls take _text_ arguments separated by white-space, 
similar to shell languages. Anything after the function name up until the end 
of the function call is parsed as a string value. Double quotes for arguments 
are optional.

```elk
someFunction hello world # equivalent to someFunction("hello", "world")
```

The end of a shell-style function call is detected upon reaching one of the 
following tokens:

* `)`
* `{`
* `}`
* `|`
* `&&`
* `||`
* `;`
* New line
* `->` (incl. surrounding spaces)

```elk
let line = read file | len
let fileContents = read file
(someFunction a b) + "c"
```

#### Interpolation

Interpolation is done by putting an expression inside the braces preceded by a 
dollar sign, such as `${x}`.  It is also possible to put string literals 
anywhere in a text argument, as well as environment variables.

```elk
let x = 5
let $VALUE = "yes"
echo hello "world" #=> hello world
echo hello ${x}    #=> hello 5
echo hello $VAR    #=> hello yes
```