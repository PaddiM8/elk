# Error Handling

## Program Invocations

When a program returns a non-zero exit code, an exception is thrown.
The exception can be handled as described in [Catching](#catching).

## Exceptions

### Throwing

An exception can be thrown with the `throw` expression.

```elk
throw "An error occurred"
```

### Catching

An exception can be caught with the `try`/`catch` expression.

```elk
try {
    ls abc
} catch e {
    println Error: ${e}
}
```