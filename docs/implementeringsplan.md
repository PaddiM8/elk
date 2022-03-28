# Implementeringsplan

## Projektfaser

* Definition av grammatik
* Implementation av lexer/parser för enkla strukturer (aritmetik, anrop, variabler)
* Implementation av interpretator för enkla strukturer
* Implementation av satser och scope
* Felhantering
* Implementation av standardbibliotek

## Implementationsdetaljer

Programmet skrivs i programspråket C#. Lexikalisk och syntaktisk analys
implementeras från grunden.

## Plan

### Definition av grammatik
* Definiera grammatiken för större delen av språket i BNF-form

### Implementation av lexer/parser för enkla strukturer (aritmetik, anrop, variabler)
* Implementera en lexer för hela av den definierade grammatiken
* Skapa klasser för det abstrakta syntaxträdet för aritkmetik, funktionsanrop och variabler
* Implementera en parser för aritmetik, funktionsanrop och variabler

### Implementation av interpretator för enkla strukturer
* Skapa en interpretator-klass som går igenom det abstrakta syntaxträdet
* Skapa klasser som representerar språkets olika runtime-datatyper
* Implementera interpretation av det abstrakta syntaxträdet returnerat av pasern,
  samt automatisk typkonvertering av datatyper
* Lös "redirection" av standard output från program-anrop

### Implementation av satser och scope
* Implementera block-sats och därmed nästlat scope
* Implementera if- och for-satser
* Implementera sats för funktionsdefinition

### Skriv tester för allt hittills

### Felhantering
* Lägg till felhantering för typkonvertering
* Hantera parser- och interpretatorfel

### Implementation av standardbibliotek
* Implementera dynamiskt anrop av standardbiblioteksfunktioner
* Implementera matematiska funktioner i standardbibliotek

### Skriv tester för allt hittills

### Implementera listor
* Implementera parsning och interpretation av list-uttryck
* Implementera runtime-datatyp för listor och typkonvertering
* Implementera standardbiblioteksfunktioner för listor

### Skriv resterande tester

## Svårigheter
### Parsning av funktionsanrop/program-anrop av bash-stil
Funktionsanrop utan paranteser bör parsas ungefär som i bash. All text efter
funktionsnamnet bör parsas som sträng-argument fram till en symbol som `|`
eller `)`. Då variabler inte har något prefix blir det även nödvändigt att 
först kontrollera om en identifier-token är för en variabel eller inte.

Ett potentiell lösning till det här problemet skulle kunna vara att låta
lexern analysera det som vanligt, men se till att inkluera textvärdet i
varje token, och sedan låta parsern sätta ihop dessa värden. Det är då
nödvändigt att spara **all** text i tokens, även symboler som inte
explicit hanteras och mellanrumstecken.

### Omdirigering av standard output
Vid program-anrop bör interpretatorn kunna avgöra om standard output borde
dirigeras om och sparas som värde eller visas direkt i terminalen. Detta
beror på sammanhang. Om något i programkoden använder sig av värdet
returnerat av anropet är det lämpligt att dirigera om standard output.
Annars är det viktigt att se till att det inte dirigeras om, till exempel
så att `echo` verkligen skriver till terminalen.

Ett alternativ skulle kunna vara att hantera detta annorlunda beroende på
syntax, till exempel att anrop utan paranteser leder till att standard
output *inte* dirigeras, medan anrop med paranteser leder till att det
dirigeras. I detta fall behövs någon annan syntax än endast paranteser för
att inkludera programkod i ett anrop av bash-stil, till exempel
`echo $(x + 1)`.