# Function References

Function references are made using the `&` symbol. This can be done on both 
regular functions and programs. In order to call a function using a function 
reference, the `call` function is used.

```elk
let f = &len
f | call abc #=> 3

let ouput = if userEcho: &echo else &println
output | call hello world #=> hello world
```

It is also possible to get references of partial functions calls.
```elk
let f = &op::add(3)
f | call(4) | println #=> 7

[1, 2, 3]
    | map => &op::mul(2)
    | println #=> [2, 4, 6]
```