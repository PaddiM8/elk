if exists('b:did_indent')
    finish
endif
let b:did_indent = 1

setlocal cinoptions& cinoptions+=j1,L0
setlocal indentexpr=ElkIndent()
setlocal indentkeys=0},0),!^F,o,O,e,<CR>

function! ElkIndent()
    if v:lnum == 0
        return 0
    endif

    let prev_num = prevnonblank(v:lnum - 1)
    let prev = getline(prev_num)
    let prev_prev_num = prevnonblank(prev_num - 1)
    let prev_prev = getline(prev_prev_num)
    let prev_indent = indent(prev_num)
    let cur = getline(v:lnum)

    let prev_open_paren = prev =~ '^.*(\s*$'
    let cur_close_paren = cur =~ '^\s*).*$'
    let prev_close_paren = prev =~ '^\s*).*$'

    let prev_open_brace = prev =~ '^.*{\s*$'
    let cur_close_brace = cur =~ '^\s*}.*$'
    let prev_close_brace = prev =~ '^\s*}.*$'

    if (prev_close_brace && !prev_open_brace) || (prev_close_paren && !prev_open_paren)
        " find the start of the block
        let i = v:lnum - 2
        while i >= 0 && indent(i) > prev_indent && getline(i) != ""
            let i -= 1
        endwhile

        " ...and check if it starts with a pipe
        " if it does, we need to account for the pipe's
        " additional indentation and remove that too
        if getline(i) =~ "^\\s*\|"
            let prev_indent -= shiftwidth()
        endif
    endif

    if prev =~ "^\\s*\|"
        let prev_indent -= shiftwidth()
    endif

    if cur =~ "^\\s*\|"
        return prev_indent + shiftwidth()
    endif

    let trailing_keyword_re = '\(\s\)\+\(or\|and\|\\\)\s*$'
    if prev_prev =~# trailing_keyword_re
        let prev_indent -= shiftwidth()
    endif

    if prev =~# trailing_keyword_re
        return prev_indent + shiftwidth()
    endif

    if (prev_open_paren || prev_open_brace) && prev =~ "^\\s*\|"
        let prev_indent += shiftwidth()
    endif

    if prev_open_paren && !cur_close_paren || prev_open_brace && !cur_close_brace
        return prev_indent + shiftwidth()
    endif

    if cur_close_paren && !prev_open_paren || cur_close_brace && !prev_open_brace
        return prev_indent - shiftwidth()
    endif

    return prev_indent
endfunction
