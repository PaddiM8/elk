# Error Handling

## Program Invocations

When a program returns a non-zero exit code, an exception is thrown.
The exception can be handled as described in [Catching](#catching).

## Exceptions

### Throwing

An exception can be thrown with the `throw` expression. The given
value will be cast into an `Error` object, if it isn't one already.

```elk
throw "An error occurred"
```

### Catching

An exception can be caught with the `try`/`catch` expression. When
an exception is about the be caught, the first valid `catch` arm 
will be evaluated. The `with <type>` syntax is used to specify
which kinds of values should be caught, based on the value inside
the `Error` object.

The value of an `Error` object can be retrieved using the `error::value`
function.

```elk
try {
    ls abc
    ...
} catch e with SomeStruct {
    println SomeStruct Error: ${error::value(e)}
} catch e {
    println Error: ${error::value(e)}
}
```