---
description: (experimental)
---

# Closures

A closure is an instance of an anonymous function. A function can take a 
closure an argument using the function closure syntax, which provides a 
convenient way of passing a closure to a function call. A closure is attached 
to a function call by writing `=>` followed by a comma separated parameter 
list, followed by a block.

```elk
# By putting "=> closure" before the block, the
# function now takes a closure. The closure is
# called by calling the "closure" function.
fn select(container) => closure {
    let values = []
    for value in container: values | add(closure(value))

    values
}

# Keep in mind that the closure is added *after*
# any arguments. Eg. a(x) => ...
let values = ["1", "2", "3"];
values | map => x: int(x) #=> [1, 2, 3]
values | map => &int      #=> [1, 2, 3]
```

It is also possible to create free-standing closures using the `Fn` function.

```elk
let f = &Fn x: x * 2
f | call(2) #=> 4
```