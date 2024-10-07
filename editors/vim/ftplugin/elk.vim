if exists('b:did_ftplugin')
    finish
end
let b:did_ftplugin = 1

let s:save_cpo = &cpo
set cpo&vim

setlocal comments=:#
setlocal commentstring=#%s
setlocal define=\\v^\\s*fn>
setlocal formatoptions+=jn1
setlocal formatoptions-=t
setlocal include=\\v^\\s*\\.>
setlocal iskeyword=@,48-57,+,-,_,.
setlocal suffixesadd^=.elk

let b:undo_ftplugin = "
            \ setlocal comments< commentstring< define< foldexpr< formatoptions<
            \ | setlocal include< iskeyword< suffixesadd< omnifunc< formatprg<
            \"

let &cpo = s:save_cpo
unlet s:save_cpo

if has("nvim")
    hi! link @lsp.type.string String

lua << EOF
    vim.lsp.start({
        name = "elk",
        cmd = {"elk", "--lsp"}
    })
EOF
endif
