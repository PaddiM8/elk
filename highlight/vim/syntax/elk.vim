if exists('b:current_syntax')
    finish
endif

syntax case match
syntax iskeyword @,48-57,-,_,.,/

syntax cluster elkKeyword contains=elkFunction,elkConditional,
            \ elkRepeat,elkLabel,elkControl,elkBoolean
syntax keyword elkConditional if else
syntax keyword elkRepeat while for in
syntax keyword elkControl return break continue exit
syntax keyword elkBoolean true false
syntax keyword elkUnspecifiedKeyword with from using let nil not and or module struct new try catch throw alias unalias pub

syntax keyword elkFunction fn nextgroup=elkFunctionName skipwhite
syntax match elkFunctionName '[^[:space:]/()-][^[:space:]/()]*' contained
            \ contains=elkString

syntax match elkOperator '[\[\]=*~%&|<>!+-]'
syntax match elkOperator '\.\.'

syntax match elkComment /#.*/
syntax match elkNumber /\v<[+-]=(\d+\.)=\d+>/

syntax match elkDoubleQuoteEscape /\\[\\"$\n]/ contained
syntax cluster elkStringEscape contains=elkSingleQuoteEscape,elkDoubleQuoteEscape

syntax region elkString start=/"/ skip=/\\"/ end=/"/ contains=elkDoubleQuoteEscape

highlight default link elkKeyword Keyword
highlight default link elkUnspecifiedKeyword Keyword
highlight default link elkFunction elkKeyword
highlight default link elkConditional Conditional
highlight default link elkRepeat Repeat
highlight default link elkLabel Label
highlight default link elkFunctionName Function
highlight default link elkComment Comment
highlight default link elkOperator Operator
highlight default link elkString String
highlight default link elkSingleQuoteEscape Special
highlight default link elkDoubleQuoteEscape Special
highlight default link elkNumber Number
highlight default link elkBoolean Boolean
highlight default link elkControl Exception

let b:current_syntax = 'elk'
