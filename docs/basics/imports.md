# Imports

The import system makes it possible to split up code into multiple files. One 
can either import a file as a module, import all the functions of a file or 
import some specific functions. When a file is imported as a module, all the 
functions are available through the syntax `moduleName::functionName` where 
`moduleName`is simply the file name without the extension. Note that only
public symbols can be imported.

The `with` keyword followed by a file path (excl. the extension) is used to 
import a file as a module, while the `using` keyword is used in the same way to 
import all the functions in a file. When the `with` keyword is used together 
with the `from` keyword, the file path is put after the `from` keyword, and the 
function names after the `with` keyword.

::: info
Circular imports are allowed, meaning two files can import each other.
:::

```elk
# import as a module
with someModule
someModule::someFunction()

# import a file as a module
with ./someDirectory/someFile # the extension is emitted
```

```elk
# import all the functions
using someModule
someFunction()

# import all the functions in a file
using ./someDirectory/someFile # the extension is emitted
```

```elk
# import specific functions
with fun1, fun2 from someModule
fun1()
fun2()
```

### Standard Library Imports

Standard library modules are imported by default, meaning any standard library 
function can be accessed with the syntax `moduleName::functionName` without 
importing the module before-hand. Some of the functions are also imported by 
default, meaning it is not necessary to specify the module when calling these.

```elk
# some functions are imported by default
print("hello")
let x = input("> ")
```

```elk
# other functions are accessed by specifying the module name
"hello" | str::upper
"hello" | str::endsWith("o")
```

```elk
# it is also possible to import all the functions of a standard library module
using str
"hello" | upper
"hello" | endsWith("o")
```