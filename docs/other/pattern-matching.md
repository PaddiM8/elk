# Pattern Matching

The `isType` function is used to check if a value is of a specific type. This 
includes the types `Iterable` and `Indexable`.

```elk
[1, 2, 3] | isType(Indexable) #=> True
123 | isType(Iterable)        #=> False
```