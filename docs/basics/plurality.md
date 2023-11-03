---
description: (experimental)
---

# Plurality

A function or program can easily be called on a list of values using plurality 
syntax. This is done by adding a `!` symbol at the end of the function name in 
a call.

```elk
["hello", "world"] | len! #=> [5, 5]
"123" | into::int!        #=> [1, 2, 3]
```
