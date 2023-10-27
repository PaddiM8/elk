# Multi-line expressions

It is often possible to have expressions that span multiple lines without 
adding anything. However, in some cases it is necessary to add a backslash 
before the new line in order for the parser to know that an expression 
continues on the next line. An example of a situation like this is a 
shell-style function call.

```elk
echo this line \
    spans several \
    lines
```