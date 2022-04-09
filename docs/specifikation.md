# Språkspecifikation

Shel är ett språk med dynamisk typning och omfattande implicit
typkonvertering. Värden skickas alltid i grunden till funktioner
som objektreferenser. Varje block-sats har sitt egna scope.

## Specialfall
### Funktionsanrop
Funktionsanrop kan ske både med eller utan parenteser. Anrop
utan parenteser tolkas precis som programanrop i bash, till exempel
`echo hello world` där allt efter `echo` tolkas som strängar.
Anrop med parenteser fungerar som vanliga funktionsanrop. I
slutändan är det ingen semantisk skillnad på de olika
syntaxerna. Om det inte finns en funktion med det angivna
namnet sker ett programanrop istället.

```shell
wc("-l", ls) # samma som wc -l `ls` i bash
echo hello world
```

### Variabler utan prefix
I Shel har variabler inte någon prefix-symbol som `$`
eller `@`. Detta betyder att följande exempel
har samma syntax men annorlunda semantik:

```shell
>> let x = 1
>> x + 2
3
>> echo + 2
+ 2
```

Grammatiken har alltså ett visst kontextberoende där
det är nödvändigt att veta om en identifierare tillhör
en variabel eller inte.

### Redirections
I vissa fall vill är det nödvändigt att fånga ett programs
output för att på något sätt hantera den returnerade texten.
I andra fall räcker det att låta terminalen skriva ut programmets
output. I bash behöver användaren explicit visa att
ett programs output ska hämtas (och därmed inte heller
visas i terminalen). Shel analyserar automatiskt koden
för att bestämma hur output bör hanteras. Om värdet
av ett anrop inte hanteras i koden låter Shel terminalen
skriva ut det. Om värdet av ett anrop hanteras (till
exempel `let x = ls`) "stjäler" Shel programmets
output och skapar ett objekt med värdet. Detta leder
till att det inte heller skrivs ut i terminalen.

```shell
>> let x = echo hello | wc -l
>> x
1
>> echo hello | wc -l
1
```

## Exempel
```rust
let num = ~/.scripts/get_num.sh
let rows = []
for i in 0..num {
    let row = if i % 2 == 0: num else "-"
    row | append(rows)
    echo(row)
}

let person = {
    first_name: read(),
    last_name: read(),
    age: int(read()),
}

echo("Age: " + (person["age"] ?? "unknown"))
```

## Grammatik
### Satser
```bnf
<expr> ::= <fn>
    | <let>
    | <include>
    | break <expr>
    | continue
    | return <expr>
    | <or>

<fn> ::= fn <identifier> ( <identifierList> ) <blockOrSingle>
<let> ::= let <identifier> = <expr>
<if> ::= if <expr> <blockOrSingle>
    | if <expr> <blockOrSingle> else <expr>
<for> ::= for <identifier> in <expr> <blockOrSingle>
<include> ::= include <string>

<identifierList> ::= <identifierList> , <identifier>
    | <identifier>
    | <empty>
<blockOrSingle> ::= <block>
    | : <expr>
```
### Uttryck
```bnf
<block> ::= { <blockContent> }
<blockContent> ::= <blockContent> \n <block>
    | <block>
    | <empty>

<pipe> ::= <assignment>
    | <pipe> | <assignment>
<binaryIf> ::= <assignment> if <assignment>
    | <assignment>
<assignment> ::= <identifier> = <or>
    | <identifier> += <or>
    | <identifier> -= <or>
    | <identifier> *= <or>
    | <identifier> /= <or>
    | <identifier> %= <or>
    | <identifier> ^= <or>
    | <or>
<or> ::= <and>
    | <or> || <and>
<and> ::= <comparison>
    | <and> && <comparison>
<comparison> ::= <range>
    | <range> \> <range>
    | <range> \>= <range>
    | <range> \< <range>
    | <range> \<= <range>
    | <range> == <range>
    | <range> != <range>
    | <range> =~ <range>
<range> ::= <coalescing>..<coalescing>
    | <coalescing>..=<coalescing>
    | ..<coalescing>
    | <coalescing>..
    | <coalescing>
<coalescing> ::= <additive> ?? <additive>
    | <additive>
<additive> ::= <multiplicative>
    | <additive> + <multiplicative>
    | <additive> - <multiplicative>
<multiplicative> ::= <unary>
    | <multiplicative> * <unary>
    | <multiplicative> / <unary>
    | <multiplicative> % <unary>
<power> ::= <unary> ^ <power>
    | <unary>
<unary> ::= - <indexer>
    | ! <indexer>
    | <indexer>
<indexer> ::= <primary> [ <expr> ]
    | <primary>
<primary> ::= <number>
    | <string>
    | <regex>
    | nil
    | true
    | false
    | ( <expr> )
    | <if>
    | <for>
    | <match>
    | <tuple>
    | <list>
    | <dict>
    | <var>
    | <call>
<list> ::= [ <exprList> ]
    | [ <exprList> , ]
<tuple> ::= ( <exprList> )
    | ( <exprList> , )
<dict> ::= { <dictEntries> }
    | { <dictEntries> , }
<dictEntries> ::= <dictEntries> , <dictEntry>
    | <dictEntry>
<dictEntry> ::= <identifier> : expr

<match> ::= match <expr> { matchArms }
    | match <expr> { matchArms , }
<matchArms> ::= <matchArms> , <matchArm>
    | <matchArm>
<matchArm> ::= <pattern> => <expr>
<pattern> ::= <pattern> '|' <literal>
    | <literal>
    | _
<literal> ::= <number>
    | <string>
    | true
    | false
    | <literalRange>
<literalRange> ::= <literal>..<literal>
    | <literal>..=<literal>
    | ..<literal>
    | <literal>..
    | <literal>
```

### Variabler och funktionsanrop
```bnf
# This part is unfortunately context-dependent, which seems
# to be necessary in order to avoid a variable prefix like $.
# In order to figure out whether it's a variable or function
# call, a parser would do a (variable) symbol table look-up.
# Even though this is a bit slow and awkward, it is deemed
# to be worth the cost in order to get a cleaner syntax. Due
# to the nature of the language, performance is not a big
# priority.
<var> ::= <identifier>
<call> ::= <path> ( <exprList> )
    | <path> ( <exprList> , )
    | <path> text

<exprList> ::= <exprList> , <expr>
    | <expr>
    | <empty>
<textArguments> ::= <textArguments> <textArgument>
    | <textArgument>
<path> ::= / <path>
    | ./ <path>
    | ../ <path>
    | ~/ <path>
    | <identifier>
```

### Tokens
```bnf
<textArgument> ::= /[^|)}\n\s]*/
<identifier> ::= /[A-Za-z_][A-Za-z0-9_]*/
<number> ::= /\d+(\.\d+)?|0b[01]+|0o[0-8]+|0x[0-0a-fA-F]+/
<string> ::= /"[^\"]"/
<regex> ::= /\/[^/]+\//
<comment> ::= /#.*\n/
```