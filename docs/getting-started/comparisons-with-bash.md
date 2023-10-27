# Comparisons with Bash

### Writing to files

```elk
# bash
echo hello world > file.txt
echo appended line >> file.txt

# elk
"hello world" | write file.txt
"appended line" | append file.txt
```

### Conditionals

```elk
# bash
if [[ -n "$var" ]]; then
   echo is not empty
else
   echo is empty
fi

# elk
if var != nil {
   echo is not empty
} else {
   echo is empty
}

# or
if var != nil: "is not empty" else "is empty" | println
```

### Reading a file line by line

```elk
# bash
cat lines.txt | while read line 
do
   echo Line: $line
done

# elk
# option 1
cat lines.txt | select => x: echo Line: ${line}

# otion 2
for line in cat("lines.txt"):
   println("Line:", line)
```

### Substrings

```elk
# bash
string="hello"
substring=${string:0:3}

# elk
let string = "hello"
let substring = string[..3]
```

### Grepping the last lines of a file

```elk
# bash
tail -n 5 file.txt | grep hello

# elk
tail -n 5 file.txt | grep hello

# it's the same!
```