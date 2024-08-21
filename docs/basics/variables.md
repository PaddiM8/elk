# Variables

## Declaration

Variables have to be declared before being used, and can contain values of any 
data type. Regular variables are _not_ treated as environment variables. The 
syntax for declarations is the following:

```elk
let variableName = value
let x, y = (2, 3) # tuple deconstruction syntax
```

::: info
Variables are accessible only from within the scope they were created in, 
including child scopes. Shadowing of variables is possible, meaning a variable 
can be declared with the same name as a pre-existing variable in the same 
scope. If it declared in a child scope of the pre-existing variable, it is 
treated as a separate variable within that scope.
:::

## Usage

Variables are accessed by typing their name. Unlike in Bash, it is not 
necessary to use a prefix (eg. `$`) when accessing variables in Elk. The 
following operations can be used to update the value of a variable:

| Operation            | Symbol |
|----------------------| ------ |
| Assignment           | =      |
| Add to value         | +=     |
| Subtract from value  | -=     |
| Divide value         | /=     |
| Multiply value       | \*=    |
| Assign value if nil  | ??=    |
| Raise value to power | ^      |

```elk
let variableName = 1 # value is 1
variableName = 2     # value is 2
variableName += 1    # value is 3
```

## Environment Variables

Environment variables can be created and accessed by putting a dollar sign in 
front of the variable name. Any variable name with a leading dollar sign is 
treated as an environment variable. Naturally, this means that these types of 
variables can only be assigned string values. An environment variable can
be assigned to with or without the `let` keyword.

```elk
let $ENV_VAR = "hello"
echo($ENV_VAR)
$ENV_VAR += " world"
```

:::tip
The $? variable is automatically set to the exit code of the last
executed program.
:::