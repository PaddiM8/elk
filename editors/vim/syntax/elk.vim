if exists('b:current_syntax')
    finish
endif

syn case match
syn iskeyword @,48-57,-,_,.,/

syn cluster elkKeyword contains=elkConditional,
            \ elkRepeat,elkLabel,elkControl,elkBoolean
syn keyword elkConditional if else
syn keyword elkRepeat while for in
syn keyword elkControl return break continue exit
syn keyword elkBoolean true false
syn keyword elkUnspecifiedKeyword with from using let nil not and or module struct new try catch throw alias unalias pub

syn keyword elkFunction fn skipwhite

syn match elkOperator '[\[\]=*~%&|<>!+-]'
syn match elkOperator '\.\.'

syn match elkComment /#.*/
syn match elkNumber /\v<[+-]=(\d+\.)=\d+>/

syn match elkBraces "[\[\]]"
syn region elkStringInterp matchgroup=elkBraces start=/\${/ end=/}/ contained contains=ALL
syntax region elkString   start=/"/ skip=/\\\\\|\\"/ end=/"/ contains=elkStringInterp
syntax region elkRawString   start=/'/ skip=/\\\\\|\\'/ end=/'/


hi def link elkBraces Delimiter
hi def link elkKeyword Keyword
hi def link elkUnspecifiedKeyword Keyword
hi def link elkFunction elkKeyword
hi def link elkConditional Conditional
hi def link elkRepeat Repeat
hi def link elkLabel Label
hi def link elkComment Comment
hi def link elkOperator Operator
hi def link elkString String
hi def link elkNumber Number
hi def link elkBoolean Boolean
hi def link elkControl Keyword

let b:current_syntax = 'elk'
