# Control Flow

## If

The syntax for if expressions is `if condition {}` or `if condition: 
expression`. It is also possible to have an else branch, for example `if 
condition: expression1 else expression2`. The if expression gets the value of 
the evaluated branch.

```elk
let result = if x < 5 {
    x + 1
} else {
    -1
}
let result = if x < 5: x + 1 else -1
```

### Postfix If

A postfix if is used to evaluate an expression only if a condition is true. The 
syntax for this is `expression if condition.`

```elk
echo(x) if x > 0
```

### Nil Coalescing

With the nil coalescing operator, the right hand side is used if the left hand
side is nil.

```elk
nil ?? 5     #=> 5
"hello" ?? 5 #=> "hello"
"" ?? 5      #=> ""
```

## Loops

### While

A while loop evaluates its branch repeatedly for as long as the given condition 
is true, with the syntax `while condition {}` or `while condition: expression`.

```elk
while x > 0 {
    x -= 1
}

while x > 0: x -= 1
```

### For

A for loop iterates over an iterable value and has the syntax `for itemName in 
value {}` or `for itemName in value: expression` where `itemName`is a 
user-chosen name for the variable created each iteration. It is also possible 
to specify multiple variable names here if the value is a tuple.

```elk
for item in [1, 2, 3]: echo(item)
for item, i in [1, 2, 3] | withIndex: echo("${i}: ${item}")
for i in 0..10: echo(i)
```

### Break and Continue

A loop can be stopped at any time using the `break` keyword. When an expression 
is put after a `break`keyword, the loop expression itself gets the value of 
this expression. It is also possible to immediately skip to the next iteration 
using the `continue` keyword.

```elk
# result is going to be either 123 or nil
let result = while x > 0 {
    break 123 if x == 2
    x -= random(1, 3)
}
```