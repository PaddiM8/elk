# Bash Literals

One inconvenience with alternative shells is that you often can not paste bash 
commands into them and instead have to rewrite them. Elk solves this with bash 
literals. Anything preceded by `$:` is evaluated as a bash command rather than 
as Elk code.

```elk
$: echo $(kalker 1 + 1)
```